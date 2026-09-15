// Assets/_Project/Scripts/Core/GameEngine.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;


namespace NonaRoyale.Core
{
    /// <summary>
    /// The core's one public entry point. Commands in, events out (ADR-0004).
    /// </summary>
    /// <remarks>
    /// <b>The view talks only to this.</b> Services stay behind it, so nothing
    /// outside the core can reach past the boundary into <c>EnergyLedger</c> or
    /// mutate an operator directly. That is also the seam online multiplayer
    /// slots into later: a command is the same object whether it came from a
    /// click or a socket.
    ///
    /// <b>The engine is always inside a turn.</b> It begins the first on
    /// construction and the next automatically when one ends, so the five
    /// commands in §9.2 are the whole surface — there is no "begin turn" for a
    /// caller to forget.
    ///
    /// <b>A rejected command is an event, not an exception.</b> Players click
    /// things they cannot do; the view needs to say why. Exceptions are kept for
    /// states the rules make impossible.
    /// </remarks>
    public sealed class GameEngine
    {
        /// <summary>
        /// The cause recorded for a kill by Miracle Pull's execute. It never
        /// passes through the damage pipeline — the operator's health is set to
        /// zero outright — so there is no <c>DamageResult</c> to take a label
        /// from (§10.3).
        /// </summary>
        private const string ExecuteCause = "execute";

        private readonly IReadOnlyList<OperatorState> _operators;
        private readonly IReadOnlyDictionary<int, AbilityDefinition> _abilityBook;

        private readonly PathMap _map;
        private readonly TurnStateMachine _turns;
        private readonly MovementResolver _movement;
        private readonly CollisionResolver _collisions;
        private readonly AbilityResolver _abilities;
        private readonly StatusRegistry _statuses;
        private readonly AuraRules _auras;
        private readonly NeutralizeRules _neutralize;
        private readonly WinConditions _win;
        private readonly CombatConfig _config;
        private readonly DeferredCellEffects _cellEffects;

        /// <summary>
        /// The match's one shared random stream. Only the pity deploy's yard
        /// pick draws from it here; the dice themselves roll inside
        /// <c>TurnStateMachine</c>. Sharing the stream is what keeps a seed
        /// reproducing the whole match (IRandom's remarks).
        /// </summary>
        private readonly IRandom _random;

        /// <summary>Whether any roll this turn showed the deploy face. Reset in <see cref="ResetRollState"/>.</summary>
        private bool _sawDeployFace;


        /// <summary>
        /// The dice from this roll that have not been spent yet.
        /// </summary>
        /// <remarks>
        /// <b>This replaced a single <c>_movementValue</c> int and a
        /// <c>_hasMovedThisRoll</c> flag.</b> Those encoded "exactly one operator
        /// moves per roll", and §6 no longer says that. The dice are now tracked
        /// individually because both new rules are about individual dice:
        /// movement is compulsory <i>per die</i>, and a split spends one die
        /// rather than the total.
        ///
        /// Deploy already worked this way — §1.3 has always consumed one die per
        /// deployed operator — so this makes movement consistent with it rather
        /// than inventing a second accounting scheme. It also removed the
        /// deploy-before-move ordering rule, which only existed because the
        /// movement total was computed up front and could not be un-spent.
        /// </remarks>
        private readonly List<int> _unspentDice = new List<int>(2);

        private readonly ReadOnlyCollection<int> _unspentView;

        private bool _hasRolled;

        public GameEngine(
            IReadOnlyList<OperatorState> operators,
            IReadOnlyDictionary<int, AbilityDefinition> abilityBook,
            PathMap map,
            TurnStateMachine turns,
            MovementResolver movement,
            CollisionResolver collisions,
            AbilityResolver abilities,
            StatusRegistry statuses,
            AuraRules auras,
            NeutralizeRules neutralize,
                        WinConditions win,
            CombatConfig config,
            DeferredCellEffects cellEffects,
            IRandom random)
        {

            _operators = operators ?? throw new ArgumentNullException(nameof(operators));
            _abilityBook = abilityBook ?? throw new ArgumentNullException(nameof(abilityBook));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _turns = turns ?? throw new ArgumentNullException(nameof(turns));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _collisions = collisions ?? throw new ArgumentNullException(nameof(collisions));
            _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
            _statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            _auras = auras ?? throw new ArgumentNullException(nameof(auras));
            _neutralize = neutralize ?? throw new ArgumentNullException(nameof(neutralize));
            _win = win ?? throw new ArgumentNullException(nameof(win));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _cellEffects = cellEffects ?? throw new ArgumentNullException(nameof(cellEffects));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            _unspentView = new ReadOnlyCollection<int>(_unspentDice);

        }

        public TurnPhase Phase => _turns.Phase;
        public PlayerState CurrentPlayer => _turns.CurrentPlayer;
        public bool MatchOver => _turns.Phase == TurnPhase.MatchOver;

        /// <summary>
        /// The faces still unspent on the current roll, in the order they were
        /// rolled. Empty before the first roll and after the roll is used up.
        /// </summary>
        /// <remarks>
        /// A live read-only view rather than a copy: <c>OnGUI</c> reads it many
        /// times a frame, and allocating a fresh array each read is the kind of
        /// small waste that only shows up once a profiler is pointed at it.
        /// </remarks>
        public IReadOnlyList<int> UnspentDice => _unspentView;

        /// <summary>
        /// Whether the current player still owes the board a move. True exactly
        /// when <see cref="EndTurnCommand"/> would be rejected.
        /// </summary>
        /// <remarks>
        /// For greying out the end-turn button rather than letting a player press
        /// it and read a refusal — the same reasoning that put
        /// <see cref="CheckAbility"/> here.
        /// </remarks>
        public bool MustSpendRoll => Phase == TurnPhase.AwaitingRoll || HasLegalMove();

        /// <summary>Opens the match. Runs the first upkeep and reports it.</summary>
        public IReadOnlyList<IGameEvent> Start()
        {
            var events = new List<IGameEvent>();
            BeginTurn(events);
            return events;
        }

        public IReadOnlyList<IGameEvent> Execute(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            var events = new List<IGameEvent>();

            if (MatchOver)
            {
                events.Add(new CommandRejected("the match is over"));
                return events;
            }

            // An if-chain rather than a switch on type patterns, which older
            // C# compilers reject. Same dispatch, no version constraint.
            if (command is RollDiceCommand) Roll(events);
            else if (command is DeployCommand deploy) Deploy(deploy, events);
            else if (command is MoveCommand move) Move(move, events);
            else if (command is UseAbilityCommand ability) UseAbility(ability, events);
            else if (command is EndTurnCommand) EndTurn(events);
            else events.Add(new CommandRejected($"unknown command {command.GetType().Name}"));

            return events;
        }

        // ── Turn flow ────────────────────────────────────────────────────

        private void BeginTurn(List<IGameEvent> events)
        {
            var upkeep = _turns.BeginTurn();
            events.Add(new TurnBegan(_turns.CurrentPlayer.Color, _turns.CurrentPlayer.TurnIndex));

            foreach (var tick in upkeep.BleedTicks)
            {
                var bleeding = FindOperator(tick.TargetOperatorId);
                events.Add(new DamageDealt(bleeding, tick.AmountApplied, tick.RemainingHealth, tick.Cause));
            }

            // Beacons fire at upkeep and can kill, so they are reported before
            // the neutralize loop below — the beam has to land on screen before
            // the piece it finished disappears (ADR-0006).
            //
            // Each hit goes through EmitDamage rather than being written as a
            // plain DamageDealt: the beam is Normal, so it can be evaded or
            // absorbed, and those are three visibly different things (§9.3).
            foreach (var resolved in upkeep.CellEffects)
            {
                // A beacon and a zone tick are different events because they read
                // differently: one beam resolving once, against ground doing its
                // work for the third round running (ADR-0006, ADR-0007).
                if (resolved.Cause == "beacon")
                {
                    events.Add(new BeaconFired(
                        resolved.Owner, resolved.Cell, resolved.Caught.Count, resolved.DamagePerTarget));
                }
                else
                {
                    events.Add(new ZoneTicked(
                        resolved.Owner, resolved.Cell, resolved.IsDetonation,
                        resolved.Caught.Count, resolved.DamagePerTarget));

                    // Only the detonation stuns, so only it has statuses to
                    // announce. A lingering tick that emitted nothing here is
                    // correct, not a missing case.
                    foreach (var stunned in resolved.Stunned)
                        events.Add(new StatusApplied(stunned, StatusKind.Stun, 2));
                }

                for (int i = 0; i < resolved.Damage.Count; i++)
                    EmitDamage(resolved.Caught[i], resolved.Damage[i], events);
            }

            // Operator-anchored charges detonate at the same upkeep, and are
            // reported before the neutralize loop for the reason beacons are:
            // the blast has to land on screen before the piece it finished
            // disappears (§6.4). Unlike a zone tick the resolution carries its
            // own status kind and duration — nothing here may hardcode them.
            foreach (var charge in upkeep.OperatorEffects)
            {
                events.Add(new ZeroDayDetonated(
                    charge.Owner, charge.Cell, charge.Caught.Count,
                    charge.DamagePerTarget, charge.MarkedTargetBonus));

                foreach (var statused in charge.Statused)
                    events.Add(new StatusApplied(statused, charge.Status, charge.StatusDuration));

                for (int i = 0; i < charge.Damage.Count; i++)
                    EmitDamage(charge.Caught[i], charge.Damage[i], events);
            }


            // Neutralizes from upkeep are applied by TurnStateMachine, so this
            // reports rather than resolves. It reports the cause, the mark

            // payout and the bounty too — all three were previously applied to
            // state and never announced, so the view learned of a payout only
            // when badges appeared on a later refresh.
            foreach (var down in upkeep.Neutralized)
            {
                events.Add(new OperatorNeutralized(down.Operator, down.Cause));

                foreach (var ally in down.Hastened)
                    events.Add(new StatusApplied(ally, StatusKind.Hastened, _config.HasteDurationTurns));

                EmitBounty(down.Outcome, events);
            }

            ResetRollState();

            EvaluateRegen(events);
        }

        /// <summary>
        /// Passive regeneration (§5.8): +<c>RegenAmount</c> after
        /// <c>RegenEveryTurns</c> straight owner-upkeeps spent in play, below
        /// half health, and off any safe cell.
        /// </summary>
        /// <remarks>
        /// <b>Engine-side for the pity deploy's reason</b>: eligibility reads
        /// the board (safe cells), which the deliberately board-blind
        /// <c>TurnStateMachine</c> cannot do — and this runs after
        /// <c>_turns.BeginTurn()</c> has resolved every upkeep tick, so an
        /// operator regenerates only if it survived its wounds. Regen is never
        /// a bleed shield.
        ///
        /// <b>Below half in integers</b>: <c>health × 2 &lt; maxHealth</c>.
        /// The tick can carry an operator to the threshold, where the next
        /// evaluation resets it — the spring compresses once.
        ///
        /// <b>An ineligible upkeep resets the streak</b> (see
        /// <c>OperatorState.TurnsTowardRegen</c>): sheltering costs the clock
        /// rather than pausing it, and no neutralize hook is needed because a
        /// yarded operator is out of play here.
        /// </remarks>
        private void EvaluateRegen(List<IGameEvent> events)
        {
            int every = _config.RegenEveryTurns;
            if (every <= 0 || _config.RegenAmount <= 0) return;

            var player = _turns.CurrentPlayer;
            if (player == null) return;

            foreach (var op in player.Operators)
            {
                bool eligible =
                    _map.IsOnOuterTrack(op.Progress) &&
                    op.Health * 2 < op.MaxHealth &&
                    !_map.IsSafe(_map.CellAt(op.Owner, op.Progress));

                if (!eligible)
                {
                    op.ResetRegenProgress();
                    continue;
                }

                op.RecordTurnTowardRegen();
                if (op.TurnsTowardRegen < every) continue;

                op.Heal(_config.RegenAmount);
                events.Add(new OperatorRegenerated(op, _config.RegenAmount, every));
                op.ResetRegenProgress();
            }
        }

        private void Roll(List<IGameEvent> events)
        {
            if (_hasRolled && !_turns.CanRollAgain)
            {
                events.Add(new CommandRejected(
                    "a second roll needs doubles and a remaining roll in the budget"));
                return;
            }

            // Doubles grant a fresh roll, not an escape from the one in hand.
            // Without this a player could roll doubles, decline to move, and
            // re-roll into a set of dice they liked better — which is the same
            // dodge compulsory movement exists to close (§6).
            if (_hasRolled && HasLegalMove())
            {
                events.Add(new CommandRejected("spend the dice you are holding before rolling again"));
                return;
            }

            var report = _turns.Roll();

            _unspentDice.Clear();
            _unspentDice.Add(report.Roll.First);
            _unspentDice.Add(report.Roll.Second);
            _hasRolled = true;

            if (report.Roll.CountOf(_movement.DeployFace) > 0) _sawDeployFace = true;

            events.Add(new DiceRolled(report.Roll, report.GrantsAnotherRoll));

            if (report.Grant.WasGranted)
            {
                events.Add(new EnergyGranted(
                    _turns.CurrentPlayer.Color, report.Grant.Stored, report.Grant.Burned, report.Grant.Total));
            }
        }

        private void EndTurn(List<IGameEvent> events)
        {
            // Rolling is compulsory, and it was not enforced. HasLegalMove tests
            // the phase first and returns false before a roll, so a turn could be
            // ended without ever rolling — skipping that turn's energy grant
            // (§3.1), every landing that could be contested, and every upkeep the
            // opponents were owed. Passing is not a legal way to play round a bad
            // board position.
            if (Phase == TurnPhase.AwaitingRoll)
            {
                events.Add(new CommandRejected("roll first: a turn cannot be skipped"));
                return;
            }

            // Movement is compulsory (§6). The check is "does a legal consumer
            // exist", not "are the dice gone" — a die nobody can spend is
            // forfeit, and a turn must always be endable or the match deadlocks.
            if (HasLegalMove())
            {
                events.Add(new CommandRejected(
                    "you must use your roll: an operator can still move with it"));
                return;
            }

            // Evaluated before _turns.EndTurn() because that call nulls
            // CurrentPlayer — the drought belongs to the player whose turn is
            // closing, and after the handoff there is no way to know who that
            // was. Placing the piece cannot change the winner, so running it
            // ahead of the win check is safe.
            EvaluatePityDeploy(events);

            var report = _turns.EndTurn();

            foreach (var expired in report.Expired)
                events.Add(new StatusExpired(expired.Operator, expired.Kind));

            events.Add(new TurnEnded(report.Player));

            if (report.MatchOver)
            {
                events.Add(new GameWon(report.Winner.Value));
                return;
            }

            BeginTurn(events);
        }

        /// <summary>
        /// Bad-luck deploy protection (§6): a turn ending as its player's Nth
        /// straight eligible turn without the deploy face deploys a random
        /// yard operator, free.
        /// </summary>
        /// <remarks>
        /// <b>Evaluated here for the same reason compulsory rolling is:</b>
        /// "did the deploy face appear this turn" is only decidable once
        /// rolling has definitively concluded, and with optional doubles
        /// re-rolls that point is the end of the turn — a mid-roll check would
        /// either fire while a re-roll could still produce the face, or need a
        /// second evaluation at EndTurn anyway. The consequence is accepted:
        /// the piece walks on as the turn closes and acts on its owner's next
        /// turn, never inheriting forfeit dice.
        ///
        /// <b>An empty yard resets rather than pauses</b> — a turn that could
        /// not have used the deploy was not spent waiting for one, and a
        /// paused streak would leak stale history: a two-turn drought from the
        /// opening would let an operator neutralized ten turns later walk back
        /// after one bad roll.
        ///
        /// <b>The pick consumes the shared stream only when it is a pick.</b>
        /// With one yard operator — the common case under two opening
        /// deployments — no RNG is drawn, so the dice sequence of most matches
        /// is untouched by the mechanic existing.
        ///
        /// <b>Free is the design, not an oversight.</b> An ordinary deploy
        /// consumes the 6 that paid for it; this one's price was already paid
        /// in turns spent an operator down.
        /// </remarks>
        private void EvaluatePityDeploy(List<IGameEvent> events)
        {
            int threshold = _movement.PityDeployAfterTurns;
            if (threshold <= 0) return;

            var player = _turns.CurrentPlayer;

            if (_sawDeployFace)
            {
                player.ResetDeployDrought();
                return;
            }

            var yard = new List<OperatorState>();
            foreach (var op in player.Operators)
                if (op.IsInYard) yard.Add(op);

            if (yard.Count == 0)
            {
                player.ResetDeployDrought();
                return;
            }

            player.RecordDeployDroughtTurn();
            if (player.DeployDroughtTurns < threshold) return;

            var chosen = yard.Count == 1 ? yard[0] : yard[_random.NextInt(0, yard.Count)];
            chosen.MoveTo(_movement.DeployProgress);
            player.ResetDeployDrought();

            events.Add(new OperatorPityDeployed(
                chosen, _map.CellAt(chosen.Owner, chosen.Progress), threshold));
        }

        // ── Actions ──────────────────────────────────────────────────────

        private void Deploy(DeployCommand command, List<IGameEvent> events)
        {
            if (!RequireAction(events)) return;

            var op = FindOwnedOperator(command.OperatorId, events);
            if (op == null) return;

            if (!op.IsInYard)
            {
                events.Add(new CommandRejected($"{op.Name} is already on the board"));
                return;
            }

            int face = _movement.DeployFace;

            if (!_unspentDice.Contains(face))
            {
                events.Add(new CommandRejected($"deploying needs an unspent {face}"));
                return;
            }

            // Deploy no longer has to precede movement. It used to, because the
            // movement total was computed up front from the whole roll and a
            // later deploy could not take a die back out of it. Now a deploy
            // just removes a die from the unspent set, so moving with one die
            // and deploying with the other in either order is coherent (§6).
            _unspentDice.Remove(face);

            op.MoveTo(_movement.DeployProgress);

            events.Add(new OperatorDeployed(op, _map.CellAt(op.Owner, op.Progress)));
        }

        private void Move(MoveCommand command, List<IGameEvent> events)
        {
            if (!RequireAction(events)) return;

            var op = FindOwnedOperator(command.OperatorId, events);
            if (op == null) return;

            if (_unspentDice.Count == 0)
            {
                events.Add(new CommandRejected("this roll has no dice left to spend"));
                return;
            }

            if (op.IsInYard)
            {
                events.Add(new CommandRejected($"{op.Name} is in the yard"));
                return;
            }

            if (_win.HasFinished(op))
            {
                events.Add(new CommandRejected($"{op.Name} is already home"));
                return;
            }

            if (_statuses.IsStunned(op))
            {
                events.Add(new CommandRejected($"{op.Name} is stunned"));
                return;
            }

            int pips;

            if (command.DieFace == null)
            {
                pips = UnspentTotal();
            }
            else
            {
                if (!_unspentDice.Contains(command.DieFace.Value))
                {
                    events.Add(new CommandRejected($"no unspent die showing {command.DieFace.Value}"));
                    return;
                }

                pips = command.DieFace.Value;
            }

            int cells = _movement.CellsFor(pips, SpeedOf(op));

            // A single low die under a heavy slow can floor to nothing. Spending
            // it would be a move that moves nobody, and it would ask
            // CollisionResolver what happens when an operator lands on the cell
            // it is already standing on. Refuse instead — and HasLegalMove
            // applies the same test, so a die that can only do this is forfeit
            // rather than a turn that cannot be ended.
            if (cells <= 0)
            {
                events.Add(new CommandRejected(
                    $"{pips} moves {op.Name} nowhere at its current speed"));
                return;
            }

            var move = _movement.ResolveMove(op, cells);
            var collision = _collisions.Resolve(op, move, _operators);

            op.MoveTo(collision.MoverFinalProgress);

            if (command.DieFace == null) _unspentDice.Clear();
            else _unspentDice.Remove(command.DieFace.Value);

            events.Add(new OperatorMoved(op, move.From, collision.MoverFinalProgress,
                _map.CellAt(op.Owner, collision.MoverFinalProgress)));

            if (collision.Occurred)
            {
                for (int i = 0; i < collision.Occupants.Count; i++)
                {
                    var occupant = collision.Occupants[i];
                    var result = collision.Damage[i];

                    EmitDamage(occupant, result, events);
                    events.Add(new CollisionResolved(op, occupant, collision.MoverBouncedBack));

                    if (result.Outcome == DamageOutcome.Neutralized)
                        Neutralize(occupant, result.Cause, op.Id, events);
                }
            }

            if (_win.HasFinished(op))
                events.Add(new OperatorReachedHome(op));
        }

        private void UseAbility(UseAbilityCommand command, List<IGameEvent> events)
        {
            if (!RequireAction(events)) return;

            var caster = FindOwnedOperator(command.CasterOperatorId, events);
            if (caster == null) return;

            if (!_abilityBook.TryGetValue(command.AbilityId, out var ability))
            {
                events.Add(new CommandRejected($"no ability with id {command.AbilityId}"));
                return;
            }

            OperatorState target = null;
            if (command.TargetOperatorId != null)
            {
                target = FindOperator(command.TargetOperatorId.Value);
                if (target == null)
                {
                    events.Add(new CommandRejected($"no operator with id {command.TargetOperatorId}"));
                    return;
                }
            }

            int energyBefore = _turns.CurrentPlayer.Energy;
            var resolution = _abilities.Use(
    caster, ability, target, _turns.CurrentPlayer, _operators, command.TargetCell);


            if (!resolution.Approved)
            {
                events.Add(new CommandRejected($"{ability.Name}: {resolution.Refusal}"));
                return;
            }

            events.Add(new EnergySpent(
                _turns.CurrentPlayer.Color, energyBefore - _turns.CurrentPlayer.Energy, _turns.CurrentPlayer.Energy));

            foreach (var outcome in resolution.Outcomes)
                EmitOutcome(outcome, caster, events);
        }

        // ── Event translation ────────────────────────────────────────────

        /// <summary>
        /// Turns one effect outcome into the events the view needs.
        /// </summary>
        /// <remarks>
        /// <b>Every kind must be handled.</b> A missing case is silent: the
        /// effect still resolved, the state is still correct, and the board goes
        /// on showing it — the badge is drawn from
        /// <see cref="ActiveStatusesOn"/>, the piece is repositioned from its
        /// progress. Only the explanation disappears. That is the same class of
        /// fault as the upkeep mark payout, and it is worse here because it
        /// would cover every ability in the game at once.
        ///
        /// If <c>EffectOutcomeKind</c> gains a member, it gains a case here.
        /// </remarks>
        private void EmitOutcome(EffectOutcome outcome, OperatorState caster, List<IGameEvent> events)
        {
            switch (outcome.Kind)
            {
                case EffectOutcomeKind.Damaged:
                    EmitDamage(outcome.Recipient, outcome.Damage, events);
                    if (outcome.Damage.Outcome == DamageOutcome.Neutralized)
                        Neutralize(outcome.Recipient, outcome.Damage.Cause, caster.Id, events);
                    break;

                case EffectOutcomeKind.Executed:
                    events.Add(new DamageDealt(outcome.Recipient, 0, 0, ExecuteCause));
                    Neutralize(outcome.Recipient, ExecuteCause, caster.Id, events);
                    break;

                case EffectOutcomeKind.Healed:
                    events.Add(new HealApplied(outcome.Recipient, outcome.Amount));
                    break;

                case EffectOutcomeKind.StatusApplied:
                    events.Add(new StatusApplied(outcome.Recipient, outcome.Status, outcome.Duration));
                    break;

                case EffectOutcomeKind.Pulled:
                // Placement, all three of them. Reported as a move from a

                // progress to itself: the piece has already been placed, and
                // placement is not movement (§7.4), so the view settles it rather
                // than walking it.
                //
                // Swapped and StatusRemoved had no case here at all until now,
                // which is exactly the silent failure this method's remarks warn
                // about: Translocation moved two pieces and Neural Purge stripped
                // statuses, and neither said a word.
                case EffectOutcomeKind.Pushed:
                case EffectOutcomeKind.Swapped:
                    events.Add(new OperatorMoved(outcome.Recipient, outcome.Progress, outcome.Progress,
                        _map.CellAt(outcome.Recipient.Owner, outcome.Progress)));
                    break;

                case EffectOutcomeKind.StatusRemoved:
                    events.Add(new StatusExpired(outcome.Recipient, outcome.Status));
                    break;

                case EffectOutcomeKind.BeaconPlaced:
                    events.Add(new BeaconPlaced(outcome.Recipient, outcome.Cell, outcome.Amount));
                    break;

                case EffectOutcomeKind.ZoneDeployed:
                    events.Add(new ZoneDeployed(outcome.Recipient, outcome.Cell, outcome.Amount));
                    break;

                case EffectOutcomeKind.ChargeAttached:
                    events.Add(new ZeroDayAttached(caster, outcome.Recipient));
                    break;

                // Placement again, with the caster as the subject: reported as
                // a move from a progress to itself, exactly as a swap is,
                // because the piece has already been placed and placement is
                // not movement (§7.4).
                case EffectOutcomeKind.Dashed:
                    events.Add(new OperatorMoved(outcome.Recipient, outcome.Progress, outcome.Progress,
                        _map.CellAt(outcome.Recipient.Owner, outcome.Progress)));
                    break;

            }
        }

        /// <summary>
        /// Yards a neutralized operator and reports everything that followed —
        /// the mark payout (COMBAT_SYSTEMS §10.2) and the attacker's bounty
        /// (§1.2).
        /// </summary>
        /// <remarks>
        /// Centralised so no call site can neutralize without announcing what it
        /// triggered. Three paths reach here — a collision, an ability's damage,
        /// and Miracle Pull's execute.
        ///
        /// <b>Both the cause and the killer are passed in rather than
        /// inferred.</b> Two of the three paths have a <c>DamageResult</c> to
        /// read a cause from and the execute does not; and no path has the
        /// killer on the result at all, because <c>DamageResult</c> does not
        /// carry its source. Each call site knows both without being told —
        /// a collision has its mover, an ability has its caster.
        /// </remarks>
        private void Neutralize(OperatorState op, string cause, int? killerId, List<IGameEvent> events)
        {
            var outcome = _neutralize.Apply(op, killerId);

            events.Add(new OperatorNeutralized(op, cause));

            foreach (var ally in outcome.Hastened)
                events.Add(new StatusApplied(ally, StatusKind.Hastened, _config.HasteDurationTurns));

            EmitBounty(outcome, events);
        }

        /// <summary>
        /// Reports a kill bounty that actually credited something.
        /// </summary>
        /// <remarks>
        /// A player at the energy cap collects nothing and nothing is destroyed
        /// — the bounty pays what the pool can hold (§3.1). There is no event
        /// for that, because there is no change: holding a full pool is a
        /// strategy, and a line saying a reward paid zero would read as a
        /// malfunction rather than as the cost of the bank.
        /// </remarks>
        private static void EmitBounty(NeutralizeOutcome outcome, List<IGameEvent> events)
        {
            if (!outcome.PaidABounty || outcome.Bounty.Stored <= 0) return;

            events.Add(new EnergyGranted(
                outcome.BountyPaidTo.Value,
                outcome.Bounty.Stored,
                outcome.Bounty.Burned,
                outcome.Bounty.Total));
        }

        private static void EmitDamage(OperatorState target, DamageResult result, List<IGameEvent> events)
        {
            // Three visibly different things for the view to play, which is why
            // they are separate events rather than a flag (§9.3).
            switch (result.Outcome)
            {
                case DamageOutcome.Evaded: events.Add(new DamageEvaded(target)); break;
                case DamageOutcome.Absorbed: events.Add(new DamageAbsorbed(target)); break;
                default:
                    events.Add(new DamageDealt(
                        target, result.AmountApplied, result.RemainingHealth, result.Cause));
                    break;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Whether an ability could be used right now, and why not if it could
        /// not. For drawing a tray that tells the truth before it is clicked.
        /// </summary>
        /// <remarks>
        /// The view must not work this out for itself (ADR-0004 amendment), and
        /// until now it could not find out at all — a player learned an ability
        /// was on cooldown by pressing it and reading the rejection.
        ///
        /// <b>Deliberately excludes target legality.</b> Whether a particular
        /// enemy is in range or stealthed depends on which target is selected,
        /// which is a per-target question the range preview already answers on
        /// the board. This reports only what is true of the caster and the
        /// ability.
        ///
        /// <b>Known wart:</b> the energy comparison is made here rather than
        /// through <c>EnergyLedger.CanAfford</c>, because <c>GameEngine</c> does
        /// not hold the ledger. It reads a public value rather than restating a
        /// rule, so it is defensible — but the right home for this whole method
        /// is a <c>CheckAbility</c> on <c>AbilityResolver</c>, which already owns
        /// every one of these validations for <c>Use</c>. Move it when that file
        /// is next opened.
        /// </remarks>
        public AbilityAvailability CheckAbility(OperatorState caster, AbilityDefinition ability)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));
            if (ability == null) throw new ArgumentNullException(nameof(ability));

            if (_statuses.IsStunned(caster)) return AbilityAvailability.CasterStunned;
            if (caster.IsInYard || _win.HasFinished(caster)) return AbilityAvailability.CasterOutOfPlay;
            if (!_abilities.IsReady(caster, ability)) return AbilityAvailability.OnCooldown;

            if (_turns.CurrentPlayer == null || _turns.CurrentPlayer.Energy < ability.EnergyCost)
                return AbilityAvailability.InsufficientEnergy;

            return AbilityAvailability.Ready;
        }


        /// <summary>
        /// Who this ability could legally be aimed at right now. Empty for an
        /// ability that takes no target.
        /// </summary>
        /// <remarks>
        /// Delegated rather than computed here, for the reason
        /// <see cref="CheckAbility"/>'s own remarks give about the energy
        /// comparison: <c>AbilityResolver</c> owns target validation (§9.1) and
        /// already runs every one of these checks for <c>Use</c>. A second copy
        /// here would be a second set of rules, and the two would disagree the
        /// first time either moved.
        /// </remarks>
        public IReadOnlyList<OperatorState> LegalTargetsFor(
            OperatorState caster, AbilityDefinition ability) =>
            _abilities.LegalTargets(caster, ability, _operators);

        /// <summary>
        /// Every cell the given ability could be aimed at right now
        /// (ADR-0006). The cell-targeted twin of <see cref="LegalTargetsFor"/>,
        /// for the picker to highlight — the view must not evaluate rules.
        /// </summary>
        public IReadOnlyList<CellRef> LegalCellsFor(OperatorState caster, AbilityDefinition ability) =>
            _abilities.LegalCells(caster, ability);


        /// <summary>
        /// Every move the current player could make with the dice still in hand:
        /// per operator, the pooled move and one per distinct unspent face.
        /// </summary>
        /// <remarks>
        /// <b>Read-only, and deliberately shares <see cref="Move"/>'s arithmetic
        /// rather than restating it.</b> A preview that computes distance its own
        /// way is a second copy of a rule, and the two will disagree the first
        /// time a slow or an aura is in play — which is precisely when a player
        /// is relying on the preview.
        ///
        /// <b>Every option, not just the best one.</b> Splitting a roll costs
        /// cells — a little to the per-move floor, a lot to routing a die through
        /// a slower operator — and a player cannot weigh that against the board
        /// position unless both landings are on screen before either is chosen.
        /// The engine is the only thing that can say what they are.
        ///
        /// A double offers one split option, not two: its faces are equal, so the
        /// second would draw a marker on top of the first.
        ///
        /// It stops short of collision: it reports the landing, not whether the
        /// landing is contested. Showing the bounce-back would be showing the
        /// player the outcome of a fight before they commit to it.
        /// </remarks>
        public IReadOnlyList<LandingPreview> PreviewLandings()
        {
            var previews = new List<LandingPreview>();

            if (_turns.Phase != TurnPhase.Action || _unspentDice.Count == 0) return previews;

            int pooled = UnspentTotal();

            foreach (var op in _turns.CurrentPlayer.Operators)
            {
                if (!CanBeMoved(op)) continue;

                double speed = SpeedOf(op);

                AddPreview(previews, op, null, pooled, speed);

                if (_unspentDice.Count < 2) continue;

                for (int i = 0; i < _unspentDice.Count; i++)
                {
                    if (IsRepeatedFace(i)) continue;
                    AddPreview(previews, op, _unspentDice[i], _unspentDice[i], speed);
                }
            }

            return previews;
        }

        private void AddPreview(
            List<LandingPreview> into, OperatorState op, int? die, int pips, double speed)
        {
            int cells = _movement.CellsFor(pips, speed);
            if (cells <= 0) return;

            var move = _movement.ResolveMove(op, cells);
            into.Add(new LandingPreview(op.Id, die, move.To, cells));
        }

        private bool IsRepeatedFace(int index)
        {
            for (int i = 0; i < index; i++)
                if (_unspentDice[i] == _unspentDice[index]) return true;

            return false;
        }

        /// <summary>
        /// Which statuses are currently active on an operator, for the view to
        /// draw.
        /// </summary>
        /// <remarks>
        /// The view could infer this from the event stream, and that was the
        /// first design — but events do not cover everything. Passives are
        /// granted at match start without an event, and bleed stacks are
        /// consumed at upkeep without a <c>StatusExpired</c>. An inferred badge
        /// layer drifts from the truth, and a board that lies about status is
        /// worse than one that shows none.
        /// </remarks>
        public IReadOnlyList<StatusKind> ActiveStatusesOn(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            return _statuses.ActiveKinds(op);
        }

        /// <summary>
        /// Cells with a beacon on them right now, for the view to mark
        /// (ADR-0006).
        /// </summary>
        /// <remarks>
        /// Same reason <see cref="ActiveStatusesOn"/> exists: the placement event
        /// announces a beacon once, and the board has to keep showing it for the
        /// round it is live. A beacon nobody can see is a trap rather than a
        /// prediction, and the whole ability is designed around opponents seeing
        /// it and choosing.
        /// </remarks>
        public IReadOnlyList<CellRef> ActiveBeacons() => _cellEffects.ActiveBeacons();
        /// <summary>Cells holding a lingering zone right now (ADR-0007).</summary>
        public IReadOnlyList<CellRef> ActiveZones() => _cellEffects.ActiveZones();

        /// <summary>
        /// Every pending beacon and zone, with its owner and the cells it will
        /// strike, for the board to draw (ADR-0006, ADR-0007).
        /// </summary>
        /// <remarks>
        /// <see cref="ActiveBeacons"/> and <see cref="ActiveZones"/> give only
        /// anchor cells, which is not enough to draw from. The area is a rule
        /// (§4.2), so the view must not work it out from a radius (PRESENTATION
        /// §1), and the owner is what a player needs to decide whether to step
        /// off.
        /// </remarks>
        public IReadOnlyList<CellEffectSnapshot> ActiveCellEffects() => _cellEffects.Snapshot();


        /// <summary>
        /// Whether any operator could still legally move with an unspent die.
        /// </summary>
        /// <remarks>
        /// <b>Pooling is always available, and the pooled total is at least as
        /// large as any single die</b>, so a player who cannot move by pooling
        /// cannot move at all. That is what lets this answer the question with
        /// one arithmetic check per operator instead of one per operator per die.
        ///
        /// This is the whole of "movement is compulsory": <see cref="EndTurn"/>
        /// refuses while it is true, <see cref="Roll"/> refuses a doubles re-roll
        /// while it is true, and nothing else needs to know.
        /// </remarks>
        private bool HasLegalMove()
        {
            if (_turns.Phase != TurnPhase.Action) return false;
            if (_unspentDice.Count == 0) return false;

            var player = _turns.CurrentPlayer;
            if (player == null) return false;

            int pooled = UnspentTotal();

            foreach (var op in player.Operators)
            {
                if (!CanBeMoved(op)) continue;
                if (_movement.CellsFor(pooled, SpeedOf(op)) > 0) return true;
            }

            return false;
        }

        private bool CanBeMoved(OperatorState op) =>
            !op.IsInYard && !_win.HasFinished(op) && !_statuses.IsStunned(op);

        /// <summary>
        /// Speed is base, plus statuses, plus any enemy aura reaching it — all
        /// evaluated now, because an aura's truth changes with position.
        /// </summary>
        /// <remarks>
        /// The two channels are summed, which means a slow and an enemy aura
        /// stack even though COMBAT_SYSTEMS §5.2 says slow sources do not.
        /// Pre-existing and unresolved — COMBAT_SYSTEMS §12.
        ///
        /// Extracted from <c>Move</c> once <c>PreviewLandings</c> and
        /// <c>HasLegalMove</c> both needed it. Three copies of this expression
        /// would be three places for the aura rule to drift.
        /// </remarks>
        private double SpeedOf(OperatorState op) =>
            _movement.EffectiveSpeed(
                op.BaseSpeedMultiplier,
                _statuses.SpeedModifier(op) + _auras.SpeedModifierFor(op, _operators));

        private int UnspentTotal()
        {
            int total = 0;
            for (int i = 0; i < _unspentDice.Count; i++) total += _unspentDice[i];

            return total;
        }

        private bool RequireAction(List<IGameEvent> events)
        {
            if (_turns.Phase == TurnPhase.Action) return true;

            events.Add(new CommandRejected("roll first"));
            return false;
        }

        private void ResetRollState()
        {
            _hasRolled = false;
            _sawDeployFace = false;
            _unspentDice.Clear();
        }

        private OperatorState FindOperator(int id)
        {
            foreach (var op in _operators)
                if (op.Id == id) return op;

            return null;
        }

        private OperatorState FindOwnedOperator(int id, List<IGameEvent> events)
        {
            var op = FindOperator(id);

            if (op == null)
            {
                events.Add(new CommandRejected($"no operator with id {id}"));
                return null;
            }

            if (op.Owner != _turns.CurrentPlayer.Color)
            {
                events.Add(new CommandRejected($"{op.Name} is not yours to command"));
                return null;
            }

            return op;
        }
    }
}