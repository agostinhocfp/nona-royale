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
        /// <remarks>
        /// Public so the view can recognise an execute without restating the
        /// string (AUDIO.md AU3: the mix drops out before the blow), as
        /// <see cref="Services.DeferredOperatorEffects.ChargeCause"/> already is.
        /// </remarks>
        public const string ExecuteCause = "execute";

        /// <summary>The cause on a knockout from <see cref="DevKnockOutTrack"/>. Development only.</summary>
        public const string DevCause = "dev";

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
        private readonly DeferredOperatorEffects _operatorEffects;
        private readonly SanctuaryRules _sanctuary;

        /// <summary>
        /// The match's one shared random stream. Only the pity deploy's yard
        /// pick draws from it here; the dice themselves roll inside
        /// <c>TurnStateMachine</c>. Sharing the stream is what keeps a seed
        /// reproducing the whole match (IRandom's remarks).
        /// </summary>
        private readonly IRandom _random;

        /// <summary>Whether any roll this turn showed the deploy face. Reset in <see cref="ResetRollState"/>.</summary>
        private bool _sawDeployFace;

        /// <summary>Every seat of the winning side. Empty until the match is over.</summary>
        private IReadOnlyList<PlayerColor> _winningSeats = Array.Empty<PlayerColor>();


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

        /// <summary>
        /// Haste bonus cells each operator has already used this turn, by
        /// operator id (COMBAT_SYSTEMS §5.9). Reset in <see cref="ResetRollState"/>.
        /// </summary>
        /// <remarks>
        /// Kept per turn rather than per roll so a doubles re-roll draws from the
        /// same <c>CombatConfig.HasteBonusCellCap</c>, and per operator so one
        /// hastened operator's move never eats another's allowance.
        /// </remarks>
        private readonly Dictionary<int, int> _hasteCellsUsed = new Dictionary<int, int>();

        /// <summary>
        /// Cells of speed bonus already collected this turn, per operator
        /// (§6.3, 2026-09-17). Unlike haste, speed pays on every move, so the
        /// budget is charged on each one; per operator so one speedster's
        /// moves never eat another's allowance.
        /// </summary>
        private readonly Dictionary<int, int> _speedCellsUsed = new Dictionary<int, int>();

        /// <summary>
        /// Operators that have already collected the haste bonus from the roll
        /// in hand. Cleared on every roll (§5.9): the bonus is paid once per
        /// roll, on the operator's first move with it.
        /// </summary>
        private readonly HashSet<int> _hastePaidThisRoll = new HashSet<int>();

        /// <summary>The total of the roll in hand; it sets the haste bonus (§5.9).</summary>
        private int _rollTotal;

        /// <summary>
        /// Whether this seat has already cashed a die this turn (§3.4). Reset in
        /// <see cref="ResetRollState"/>, which runs once a turn — so a doubles
        /// chain does not buy a second sale.
        /// </summary>
        private bool _cashedThisTurn;

        private PlayerColor? _winner;

        private readonly Dictionary<PlayerColor, int> _knockoutsScored = new Dictionary<PlayerColor, int>();
        private readonly Dictionary<PlayerColor, int> _operatorsLost = new Dictionary<PlayerColor, int>();

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
            IRandom random,
            DeferredOperatorEffects operatorEffects = null,
            SanctuaryRules sanctuary = null)
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

            // Optional, matching the AbilityResolver wiring: fixtures built
            // before watches existed construct the engine without the registry,
            // and only a dice move by a watched operator can tell the
            // difference (§6.7).
            _operatorEffects = operatorEffects;

            // Optional for the same reason, but never absent: a fixture that
            // builds the engine without one still answers the two queries
            // below from the same map, and the rule is a pure function of it,
            // so the answer cannot differ from the match's own instance.
            _sanctuary = sanctuary ?? new SanctuaryRules(_map);

            _unspentView = new ReadOnlyCollection<int>(_unspentDice);

        }

        public TurnPhase Phase => _turns.Phase;
        public PlayerState CurrentPlayer => _turns.CurrentPlayer;
        public bool MatchOver => _turns.Phase == TurnPhase.MatchOver;

        /// <summary>Who won, once the match is over. Null until then.</summary>
        public PlayerColor? Winner => _winner;

        /// <summary>
        /// Every seat of the winning side, in table order; empty while the
        /// match runs (ADR-0012). One seat under free-for-all, two in a 1v1
        /// team match.
        /// </summary>
        public IReadOnlyList<PlayerColor> WinningSeats => _winningSeats;

        /// <summary>
        /// Knockouts credited to <paramref name="seat"/> this match: enemy
        /// operators neutralized by its operators, however the damage arrived
        /// (collision, ability, bleed, mark, charge). Self-inflicted and
        /// friendly kills count for nobody. Display only (GUI increment I).
        /// </summary>
        public int KnockoutsScoredBy(PlayerColor seat) =>
            _knockoutsScored.TryGetValue(seat, out int count) ? count : 0;

        /// <summary>Times an operator of <paramref name="seat"/> was neutralized this match, by anything.</summary>
        public int OperatorsLostBy(PlayerColor seat) =>
            _operatorsLost.TryGetValue(seat, out int count) ? count : 0;

        /// <summary>The current round (see <c>TurnStateMachine.Round</c>). Display only.</summary>
        public int Round => _turns.Round;

        /// <summary>The most energy a pool can hold (§3.1). Display only.</summary>
        public int EnergyCap => _turns.EnergyCap;

        /// <summary>What one cashed die pays the seat (§3.4). Read by the tray and the bots.</summary>
        public int CashedDieEnergy => _turns.CashedDieEnergy;

        /// <summary>The most a seat can owe (§3.3). Read by the bots and the rules text.</summary>
        public int DebtCap => _turns.DebtCap;

        /// <summary>Whether the current seat may roll again this turn (doubles, §6).</summary>
        public bool CanRollAgain => _turns.CanRollAgain;

        /// <summary>
        /// Rolls left in this turn's budget (§6.2). Read by the bots when they
        /// price a dealt double, which is owed a roll only if there is one left.
        /// </summary>
        public int RollsRemaining => _turns.RollsRemaining;

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
        /// Whether the current player still owes the board something before the
        /// turn can end: a roll, a move, a deploy, or a doubles re-roll. True
        /// exactly when <see cref="EndTurnCommand"/> would be rejected.
        /// </summary>
        /// <remarks>
        /// For greying out the end-turn button rather than letting a player press
        /// it and read a refusal — the same reasoning that put
        /// <see cref="CheckAbility"/> here. <b>Not "dice are owed"</b> since
        /// 2026-09-25: a forced re-roll makes it true with no dice in hand. Ask
        /// <see cref="DiceOwed"/> for that.
        /// </remarks>
        public bool MustSpendRoll =>
            Phase == TurnPhase.AwaitingRoll || DiceOwed || MustRollAgain;

        /// <summary>Whether some operator of the current player can move with the dice in hand (§6.1).</summary>
        public bool CanMove => HasLegalMove();

        /// <summary>The die face that deploys an operator (ADR-0003), for text that names it.</summary>
        public int DeployFace => _movement.DeployFace;

        /// <summary>
        /// Whether the dice in hand still have a compulsory use: a legal move,
        /// or a deploy (§6.1). A doubles re-roll is refused while this is true.
        /// </summary>
        public bool DiceOwed => HasLegalMove() || MustDeploy;

        /// <summary>
        /// Whether an unspent deploy face and a yard operator are both in hand,
        /// which makes the deploy compulsory (§6.1, 2026-09-25).
        /// </summary>
        /// <remarks>
        /// <b>Why:</b> movement was compulsory and deploying was not, so a
        /// player whose runners were all seated, stunned or home could roll a
        /// 6 and end the turn on it. With a runner free to move the 6 was
        /// already owed to the board; this closes the case where it was not.
        /// "Spawn or move" is the whole rule: moving with the 6 spends it too.
        /// Double 6 with two operators seated owes two deploys, as it always
        /// could buy.
        /// </remarks>
        public bool MustDeploy
        {
            get
            {
                if (_turns.Phase != TurnPhase.Action) return false;
                if (!_unspentDice.Contains(_movement.DeployFace)) return false;

                var player = _turns.CurrentPlayer;
                if (player == null) return false;

                foreach (var op in player.Operators)
                    if (op.IsInYard) return true;

                return false;
            }
        }

        /// <summary>
        /// Whether the doubles re-roll is compulsory: one is available, the dice
        /// in hand owe nothing, and no operator of the current player can move
        /// at all (§6.2, 2026-09-25).
        /// </summary>
        /// <remarks>
        /// <b>Only when nothing can move</b>, because only then is the re-roll
        /// free. With a runner on the board a re-roll owes the board another
        /// move, and declining it to keep a piece where it stands is a tactic
        /// worth keeping. With every runner seated, stunned or home, declining
        /// only throws away a chance at a 6.
        /// </remarks>
        public bool MustRollAgain => _turns.CanRollAgain && !DiceOwed && !HasMovableOperator();

        /// <summary>Opens the match. Runs the first upkeep and reports it.</summary>
        public IReadOnlyList<IGameEvent> Start()
        {
            var events = new List<IGameEvent>();
            BeginTurn(events);
            return events;
        }

        /// <summary>
        /// Raised once at the end of every <see cref="Execute"/>, accepted or
        /// refused, with the seat whose turn it was when the command arrived,
        /// the command, and the events it produced (REPLAY.md).
        /// </summary>
        /// <remarks>
        /// <b>One hook for every caller.</b> Humans, CPU seats, the bot table
        /// and tests all reach the rules through <see cref="Execute"/>, so a
        /// recorder listening here cannot miss a command the way one wired to
        /// each call site could. A listener must not send commands of its own
        /// from inside the callback, and nothing it does may change the match.
        ///
        /// The seat is captured before the command runs because an end-turn
        /// hands over: by the time the events are back, the current player is
        /// already the next seat.
        /// </remarks>
        public event Action<PlayerColor, ICommand, IReadOnlyList<IGameEvent>> Executed;

        public IReadOnlyList<IGameEvent> Execute(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            var seat = _turns.CurrentPlayer != null ? _turns.CurrentPlayer.Color : PlayerColor.None;
            var events = new List<IGameEvent>();

            ExecuteCore(command, events);

            Executed?.Invoke(seat, command, events);
            return events;
        }

        private void ExecuteCore(ICommand command, List<IGameEvent> events)
        {
            if (MatchOver)
            {
                events.Add(new CommandRejected("the match is over"));
                return;
            }

            // An if-chain rather than a switch on type patterns, which older
            // C# compilers reject. Same dispatch, no version constraint.
            if (command is RollDiceCommand) Roll(events);
            else if (command is DeployCommand deploy) Deploy(deploy, events);
            else if (command is MoveCommand move) Move(move, events);
            else if (command is UseAbilityCommand ability) UseAbility(ability, events);
            else if (command is CashDieCommand cash) CashDie(cash, events);
            else if (command is EndTurnCommand) EndTurn(events);
            else events.Add(new CommandRejected($"unknown command {command.GetType().Name}"));
        }

        // ── Turn flow ────────────────────────────────────────────────────

        private void BeginTurn(List<IGameEvent> events)
        {
            var upkeep = _turns.BeginTurn();
            events.Add(new TurnBegan(_turns.CurrentPlayer.Color, _turns.CurrentPlayer.TurnIndex));

            // Through EmitDamage for the one outcome an Atomic tick can still
            // have besides landing: resolving on safe ground, where it is spent
            // and voided (§4.4, third amendment) and must read as SAFE, not as
            // a zero-damage hit.
            foreach (var tick in upkeep.BleedTicks)
                EmitDamage(FindOperator(tick.TargetOperatorId), tick, events);

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
                // A follow-up is the charge's sibling (§6.5) and reads
                // differently: one blow that landed or was outrun, not a blast.
                // A field tick (§6.6) reads differently again: weather that
                // bills every upkeep, not a device going off once. Branching on
                // the cause is the beacon/zone precedent above.
                if (charge.Cause == DeferredOperatorEffects.FollowUpCause)
                {
                    events.Add(new FollowUpResolved(
                        charge.Owner, charge.SourceOperatorId, charge.Target, charge.Cell,
                        charge.HitSomething, charge.DamagePerTarget, charge.MarkedTargetBonus));
                }
                else if (charge.Cause == DeferredOperatorEffects.FieldCause)
                {
                    events.Add(new FieldTicked(
                        charge.Owner, charge.Cell, charge.Caught.Count, charge.DamagePerTarget));
                }
                else
                {
                    events.Add(new ZeroDayDetonated(
                        charge.Owner, charge.Cell, charge.Caught.Count,
                        charge.DamagePerTarget, charge.MarkedTargetBonus));
                }

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
                Tally(down.Operator, down.Outcome);
                events.Add(new OperatorNeutralized(down.Operator, down.Cause, down.Outcome.CreditedTo));

                foreach (var ally in down.Hastened)
                    events.Add(new StatusApplied(ally, StatusKind.Hastened, _config.HasteDurationTurns));

                EmitBounty(down.Outcome, events);
            }

            ResetRollState();

            EvaluateRegen(events);
        }

        /// <summary>
        /// Passive regeneration (§5.11): +<c>RegenAmount</c> after
        /// <c>RegenEveryTurns</c> straight owner-upkeeps spent in play, wounded,
        /// and off any safe cell.
        /// </summary>
        /// <remarks>
        /// <b>Engine-side for the pity deploy's reason</b>: eligibility reads
        /// the board (safe cells), which the deliberately board-blind
        /// <c>TurnStateMachine</c> cannot do — and this runs after
        /// <c>_turns.BeginTurn()</c> has resolved every upkeep tick, so an
        /// operator regenerates only if it survived its wounds. Regen is never
        /// a bleed shield.
        ///
        /// <b>Any wound counts (2026-09-16).</b> The below-half gate is gone:
        /// the designer wanted steady healing to cut knockouts, and gated
        /// regen ticked about four times a match in the bots sweep, which is
        /// next to nothing. Full health is ineligible and resets the streak, so
        /// the clock starts from the upkeep after the first wound.
        ///
        /// <b>This never ran before 2026-09-16.</b> <c>CombatConfig</c>'s
        /// constructor did not assign the two regen fields, so both read 0 and
        /// the early return below fired every time.
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
                    op.Health < op.MaxHealth &&
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

            // The same dodge, through the yard: a re-roll clears the unspent
            // dice, so rolling on a held 6 would forfeit a compulsory deploy.
            if (_hasRolled && MustDeploy)
            {
                events.Add(new CommandRejected(
                    $"deploy with your {_movement.DeployFace} before rolling again"));
                return;
            }

            var report = _turns.Roll();

            _unspentDice.Clear();
            _unspentDice.Add(report.Roll.First);
            _unspentDice.Add(report.Roll.Second);
            _hasRolled = true;
            _rollTotal = report.Roll.Total;
            _hastePaidThisRoll.Clear();

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

            // Spawn or move (2026-09-25): a deploy face with a seated operator
            // is owed to the board like a move is.
            if (MustDeploy)
            {
                events.Add(new CommandRejected(
                    $"you must use your {_movement.DeployFace}: an operator is waiting to deploy"));
                return;
            }

            // Nothing on the board can move, so the doubles re-roll costs
            // nothing and is taken.
            if (MustRollAgain)
            {
                events.Add(new CommandRejected("doubles: roll again, nothing on the board can move"));
                return;
            }

            // Evaluated before _turns.EndTurn() because that call nulls
            // CurrentPlayer — the drought belongs to the player whose turn is
            // closing, and after the handoff there is no way to know who that
            // was. Placing the piece cannot change the winner, so running it
            // ahead of the win check is safe.
            EvaluatePityDeploy(events);
            CloseTurn(events);
        }

        /// <summary>
        /// The end of a turn once every rule has let it end: the turn machine's
        /// report, interest, expiries, <see cref="TurnEnded"/>, then either the
        /// win or the next turn. Shared by <see cref="EndTurn"/> and
        /// <see cref="DevForceWin"/>.
        /// </summary>
        private void CloseTurn(List<IGameEvent> events)
        {
            var report = _turns.EndTurn();

            // Before anything expires, as the turn machine ran it (§3.3). Only
            // a debt that actually grew is reported: one already at the cap
            // has nothing new to say every turn.
            if (report.Debt.Interest > 0)
                events.Add(new DebtAccrued(report.Player, report.Debt.Interest, report.Debt.Owed));

            foreach (var expired in report.Expired)
                events.Add(new StatusExpired(expired.Operator, expired.Kind));

            events.Add(new TurnEnded(report.Player));

            if (report.MatchOver)
            {
                _winner = report.Winner.Value;
                _winningSeats = report.WinningSeats;
                events.Add(new GameWon(report.Winner.Value, report.WinningSeats));
                return;
            }

            BeginTurn(events);
        }

        /// <summary>
        /// <b>Development only.</b> Sends every operator on the current
        /// player's side HOME and closes the turn, so the match is won by that
        /// side through the ordinary win check and <see cref="GameWon"/>.
        /// </summary>
        /// <remarks>
        /// For reaching the results screen without playing a match out
        /// (LAUNCH_UI_PASS.md, G7). The view binds it to a key in the editor
        /// and development builds only; no production path calls it.
        ///
        /// <b>It is not a command.</b> Nothing about it is a rule a player can
        /// invoke, so it is not in the command set, it does not raise
        /// <see cref="Executed"/>, and a <c>ReplayRecorder</c> never sees it: a
        /// replay of a match ended this way stops where the cheat was used.
        ///
        /// <b>The win is still the engine's.</b> It moves the pieces and ends
        /// the turn; <see cref="WinConditions"/> decides the winner exactly as
        /// it would after a real last move, including both partner seats at a
        /// crossed table. Each operator that arrives is reported with
        /// <see cref="OperatorReachedHome"/>, so the view walks the same path
        /// it does for a real finish. Compulsory rolling and movement are not
        /// checked: that is the point.
        ///
        /// Refused, with a <see cref="CommandRejected"/>, once the match is over
        /// or between turns.
        /// </remarks>
        public IReadOnlyList<IGameEvent> DevForceWin()
        {
            var events = new List<IGameEvent>();

            if (MatchOver || (Phase != TurnPhase.Action && Phase != TurnPhase.AwaitingRoll))
            {
                events.Add(new CommandRejected("dev win: no turn is being played"));
                return events;
            }

            var side = _win.SideSeats(_turns.CurrentPlayer.Color);

            foreach (var player in _turns.Players)
            {
                bool onSide = false;
                foreach (var seat in side)
                    if (seat == player.Color) onSide = true;
                if (!onSide) continue;

                foreach (var op in player.Operators)
                {
                    if (_win.HasFinished(op)) continue;

                    op.MoveTo(_map.Profile.Journey);
                    events.Add(new OperatorReachedHome(op));
                }
            }

            CloseTurn(events);
            return events;
        }

        /// <summary>
        /// <b>Development only.</b> Knocks out every operator on the outer
        /// track, every seat's, through the ordinary neutralize path. Each
        /// goes back to its yard at full health, which is the game's respawn.
        /// </summary>
        /// <remarks>
        /// For watching knockouts on demand (LAUNCH_UI_PASS.md, G8c's burn).
        /// The view binds it to Ctrl+Shift+Numpad 9 in the editor and
        /// development builds only; no production path calls it.
        ///
        /// <b>It is not a command</b>, for the reasons <see cref="DevForceWin"/>
        /// gives: not in the command set, no <see cref="Executed"/>, invisible
        /// to a replay.
        ///
        /// <b>Everything else is a real knockout.</b> Each operator goes through
        /// <see cref="Neutralize"/> with <see cref="DevCause"/> and no killer.
        /// So a mark still pays out, a riding charge still learns its death
        /// cell, statuses and cooldowns clear, and the match stats count the
        /// loss. No killer means no bounty and no credit. Operators in a
        /// home column or already home are out of the fight, and are left alone.
        ///
        /// The turn is not ended. Refused, with a <see cref="CommandRejected"/>,
        /// once the match is over, between turns, or with nobody on the track.
        /// </remarks>
        public IReadOnlyList<IGameEvent> DevKnockOutTrack()
        {
            var events = new List<IGameEvent>();

            if (MatchOver || (Phase != TurnPhase.Action && Phase != TurnPhase.AwaitingRoll))
            {
                events.Add(new CommandRejected("dev knockout: no turn is being played"));
                return events;
            }

            // Collected first: a mark payout or a charge can change the board
            // while the list is being walked.
            var victims = new List<OperatorState>();
            foreach (var op in _operators)
                if (_map.IsOnOuterTrack(op.Progress)) victims.Add(op);

            if (victims.Count == 0)
            {
                events.Add(new CommandRejected("dev knockout: nobody is on the track"));
                return events;
            }

            foreach (var op in victims)
            {
                // An earlier knockout in this batch cannot move a later victim
                // off the track today; checked anyway, so it never yards twice.
                if (!_map.IsOnOuterTrack(op.Progress)) continue;
                Neutralize(op, DevCause, null, events);
            }

            return events;
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

            string refusal = DeployRefusal(op);

            if (refusal != null)
            {
                events.Add(new CommandRejected(refusal));
                return;
            }

            // Deploy no longer has to precede movement. It used to, because the
            // movement total was computed up front from the whole roll and a
            // later deploy could not take a die back out of it. Now a deploy
            // just removes a die from the unspent set, so moving with one die
            // and deploying with the other in either order is coherent (§6).
            _unspentDice.Remove(_movement.DeployFace);

            op.MoveTo(_movement.DeployProgress);

            events.Add(new OperatorDeployed(op, _map.CellAt(op.Owner, op.Progress)));
        }

        /// <summary>
        /// Why deploying this operator would be refused right now, or null if
        /// it would not be.
        /// </summary>
        /// <remarks>
        /// Shared by <see cref="Deploy"/> and <see cref="CanDeploy"/>, so the
        /// board's "click to deploy" mark and the command cannot disagree.
        /// <c>Deploy</c> runs the phase and ownership checks first with its own
        /// messages; repeating them here is what lets <c>CanDeploy</c> answer
        /// for any operator at any moment.
        /// </remarks>
        private string DeployRefusal(OperatorState op)
        {
            if (_turns.Phase != TurnPhase.Action) return "roll first";
            if (op.Owner != _turns.CurrentPlayer.Color) return $"{op.Name} is not yours to command";
            if (!op.IsInYard) return $"{op.Name} is already on the board";

            int face = _movement.DeployFace;
            if (!_unspentDice.Contains(face)) return $"deploying needs an unspent {face}";

            return null;
        }

        /// <summary>
        /// Cashes one unspent die for energy instead of moving it (§3.4, §6.8).
        /// Fortuna's House Edge.
        /// </summary>
        /// <remarks>
        /// <b>It consumes the die, which is what makes it an answer to compulsory
        /// movement</b> (§6.1): a die sold is a die with no legal consumer left,
        /// so a turn whose only legal move was a bad one can now be ended. Once a
        /// turn is the whole limiter.
        /// </remarks>
        private void CashDie(CashDieCommand command, List<IGameEvent> events)
        {
            if (!RequireAction(events)) return;

            var op = FindOwnedOperator(command.OperatorId, events);
            if (op == null) return;

            string refusal = CashRefusal(op, command.DieFace);

            if (refusal != null)
            {
                events.Add(new CommandRejected(refusal));
                return;
            }

            _unspentDice.Remove(command.DieFace);
            _cashedThisTurn = true;

            var grant = _turns.CashDie(_turns.CurrentPlayer);

            events.Add(new DieCashed(op, command.DieFace, grant.Stored, grant.Total));
        }

        /// <summary>
        /// Why cashing this die with this operator would be refused, or null if it
        /// would not be. Shared by <see cref="CashDie"/> and
        /// <see cref="CanCash"/>, exactly as <c>DeployRefusal</c> is.
        /// </summary>
        private string CashRefusal(OperatorState op, int face)
        {
            if (_turns.Phase != TurnPhase.Action) return "roll first";
            if (op.Owner != _turns.CurrentPlayer.Color) return $"{op.Name} is not yours to command";
            if (!_statuses.Has(op, StatusKind.HouseEdge)) return $"{op.Name} cannot cash a die";
            if (_cashedThisTurn) return "the house takes one die a turn";
            if (op.IsInYard) return $"{op.Name} is in the yard";
            if (_win.HasFinished(op)) return $"{op.Name} is already home";
            if (_statuses.IsStunned(op)) return $"{op.Name} is stunned";
            if (!_unspentDice.Contains(face)) return $"no unspent die showing {face}";

            // The die she cashes is a die she could have moved. Without this a
            // die with no legal consumer — one a heavy slow floors to nowhere —
            // would turn into money instead of being forfeit (§6.1).
            if (CellsFor(op, face, out _, out _) <= 0) return $"{face} moves {op.Name} nowhere at its current speed";

            return null;
        }

        /// <summary>
        /// Whether this operator could cash this die right now (§3.4) — for the
        /// tray and for the bots, on the same terms the command uses.
        /// </summary>
        public bool CanCash(OperatorState op, int face)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            return CashRefusal(op, face) == null;
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

            // Read before the move: an aura's haste belongs to where the mover
            // started, and the move may carry it out of range (§5.9).
            bool hastened = IsHastenedNow(op);
            bool burdened = _statuses.IsBurdened(op);
            int cells = CellsFor(op, pips, out int hasteCells, out int speedBonusCells);

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

            // A table stops the first enemy dice move that crosses or ends on it
            // (§7.7). The truncation happens before the move resolves, so the
            // landing — and its collision contest — is the one the table chose.
            var table = FirstTableOnPath(op, cells);

            if (table != null)
            {
                cells = table.Value.Cells;
                events.Add(new MoveIntercepted(op, table.Value.Cell, table.Value.Owner, cells));
            }

            var move = _movement.ResolveMove(op, cells);
            var collision = _collisions.Resolve(op, move, _operators);

            op.MoveTo(collision.MoverFinalProgress);

            // Charged on the attempted move, bounce or not: the cells were
            // travelled, and a bounce is placement afterwards (§7.2). The roll's
            // bonus is spent by this move even if the turn cap trimmed it to 0.
            // A burden is paid the same way, once per roll (§5.16).
            if (hastened || burdened)
            {
                _hastePaidThisRoll.Add(op.Id);
                if (hasteCells > 0) _hasteCellsUsed[op.Id] = HasteCellsUsed(op) + hasteCells;
            }

            // The speed bonus is charged on every move that collected it —
            // speed is who the operator is, not a status spent once per roll
            // (§6.3). Charged on the attempted move, bounce or not, as haste is.
            if (speedBonusCells > 0)
                _speedCellsUsed[op.Id] = SpeedCellsUsed(op) + speedBonusCells;

            if (command.DieFace == null) _unspentDice.Clear();
            else _unspentDice.Remove(command.DieFace.Value);

            // The attempted landing travels with the final one, so the view can
            // show the contested cell before the bounce (PRESENTATION §3).
            events.Add(new OperatorMoved(op, move.From, collision.MoverFinalProgress,
                _map.CellAt(op.Owner, collision.MoverFinalProgress),
                attemptedTo: move.To));

            if (collision.Occurred)
            {
                for (int i = 0; i < collision.Occupants.Count; i++)
                {
                    var occupant = collision.Occupants[i];
                    var result = collision.Damage[i];

                    EmitDamage(occupant, result, events);
                    events.Add(new CollisionResolved(op, occupant, collision.MoverBouncedBack));

                    // Landing on the one you owe burns the whole debt, whoever
                    // wins the contest (§3.3): dice combat is the free answer
                    // to Revú, and this makes it the answer to his loan too.
                    var moverSeat = PlayerOf(op.Owner);
                    if (moverSeat != null && moverSeat.OwesTo(occupant.Id))
                        events.Add(new DebtBurned(op.Owner, _turns.BurnDebt(moverSeat), op, occupant));

                    if (result.Outcome == DamageOutcome.Neutralized)
                        Neutralize(occupant, result.Cause, op.Id, events);
                }
            }

            // The table bills what it stopped, after the landing has resolved.
            // Charged on the attempted move, bounce or not: the mover reached the
            // table, and a bounce-back is placement afterwards (§7.2) — the same
            // rule the haste bonus already follows.
            if (table != null)
            {
                if (table.Value.Damage > 0)
                {
                    var billed = _cellEffects.BillStop(table.Value, op);

                    EmitDamage(op, billed, events);

                    if (billed.Outcome == DamageOutcome.Neutralized)
                        Neutralize(op, billed.Cause, table.Value.SourceOperatorId, events);
                }
                else
                {
                    _cellEffects.ConfirmStop(table.Value, op.Id);
                }
            }

            // A watched mover is struck the moment its dice movement completes
            // (§6.7) — after the landing's contest, before home credit, so a
            // read that finishes the mover yards it instead of sending it home.
            // Placement never reaches this call: pulls, pushes, swaps, dashes
            // and bounce-backs are not dice movement (§7.4), which is the
            // escape hatch the ability is priced around.
            if (_operatorEffects != null)
            {
                foreach (var tripped in _operatorEffects.NotifyDiceMovement(op))
                {
                    events.Add(new WatchTripped(
                        tripped.Owner, tripped.SourceOperatorId, tripped.Target,
                        tripped.Cell, tripped.DamagePerTarget));

                    for (int i = 0; i < tripped.Damage.Count; i++)
                    {
                        EmitDamage(tripped.Caught[i], tripped.Damage[i], events);

                        if (tripped.Damage[i].Outcome == DamageOutcome.Neutralized)
                            Neutralize(tripped.Caught[i], tripped.Cause, tripped.SourceOperatorId, events);
                    }
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

            // The dice belong to the engine, so the dice precondition is checked
            // here — before the resolver charges for the cast, which is the
            // invariant every other refusal upholds (§6.8).
            int diceNeeded = DiceNeeded(ability);
            if (diceNeeded > _unspentDice.Count)
            {
                events.Add(new CommandRejected(
                    $"{ability.Name}: the roll is not holding {diceNeeded} unspent dice"));
                return;
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

            DealDice(resolution.Outcomes, caster, events);
        }

        /// <summary>
        /// Carries out whatever the cast dealt to the dice (§6.8): a set face, or
        /// a re-roll from the match's own stream.
        /// </summary>
        /// <remarks>
        /// <b>A re-roll takes the lowest dice.</b> The command carries no die
        /// face, and the lowest is what a player wants re-dealt in every case the
        /// ability exists for — a 1 that moves nowhere useful, or the die that
        /// is not the six. Stated as a ruling rather than left to a choice the
        /// tray would have to offer (§6.8).
        ///
        /// <b>A dealt double is not a rolled one.</b> Re-rolling never creates or
        /// destroys the doubles roll; only a set double asks for one, and only
        /// inside the turn's roll budget.
        /// </remarks>
        private void DealDice(
            IReadOnlyList<EffectOutcome> outcomes, OperatorState caster, List<IGameEvent> events)
        {
            foreach (var outcome in outcomes)
            {
                if (outcome.Kind != EffectOutcomeKind.DiceDealt) continue;

                int dice = Math.Min(outcome.Amount, _unspentDice.Count);
                if (dice <= 0) continue;

                int face = outcome.Duration;

                for (int i = 0; i < dice; i++)
                {
                    int index = LowestUnspentIndex();
                    int was = _unspentDice[index];

                    _unspentDice[index] = face > 0 ? face : _turns.RollOneDie();

                    // The haste bonus reads the roll in hand, not the dice still
                    // unspent (§5.9), so the total is adjusted by what changed
                    // rather than recomputed from what is left — recomputing would
                    // shrink the roll every time a die had already been moved.
                    _rollTotal += _unspentDice[index] - was;
                }

                bool extraRoll = false;

                if (face > 0 && _unspentDice.Count > 1 && AllUnspentShow(face))
                    extraRoll = _turns.GrantDealtDouble();

                events.Add(new DiceDealt(caster, new List<int>(_unspentDice), extraRoll));
            }
        }

        private int LowestUnspentIndex()
        {
            int index = 0;

            for (int i = 1; i < _unspentDice.Count; i++)
                if (_unspentDice[i] < _unspentDice[index]) index = i;

            return index;
        }

        private bool AllUnspentShow(int face)
        {
            for (int i = 0; i < _unspentDice.Count; i++)
                if (_unspentDice[i] != face) return false;

            return true;
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

                case EffectOutcomeKind.FollowUpMarked:
                    events.Add(new FollowUpMarked(caster, outcome.Recipient));
                    break;

                case EffectOutcomeKind.WatchMarked:
                    events.Add(new WatchMarked(caster, outcome.Recipient));
                    break;

                case EffectOutcomeKind.FieldProjected:
                    events.Add(new FieldProjected(outcome.Recipient));
                    break;

                case EffectOutcomeKind.DebtIncurred:
                    events.Add(new DebtIncurred(outcome.Recipient.Owner, outcome.Amount,
                        PlayerOf(outcome.Recipient.Owner)?.Debt ?? 0, caster, outcome.Recipient));
                    break;

                case EffectOutcomeKind.DebtCalled:
                    events.Add(new DebtCalled(outcome.Recipient.Owner, outcome.Amount, caster, outcome.Recipient));
                    break;

                // Placement again, with the caster as the subject: reported as
                // a move from a progress to itself, exactly as a swap is,
                // because the piece has already been placed and placement is
                // not movement (§7.4).
                case EffectOutcomeKind.TableDealt:
                    events.Add(new TableDealt(caster, outcome.Cell, outcome.Amount));
                    break;

                // Dealt dice are applied by DealDice, once the whole cast has
                // been reported, and the event carries the faces the seat ends up
                // holding — which a re-roll does not know until it is rolled.
                case EffectOutcomeKind.DiceDealt:
                    break;

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

            Tally(op, outcome);
            events.Add(new OperatorNeutralized(op, cause, outcome.CreditedTo));

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
        /// <summary>Counts a neutralize for the match stats. Every death funnels through here or the upkeep loop.</summary>
        private void Tally(OperatorState victim, NeutralizeOutcome outcome)
        {
            _operatorsLost[victim.Owner] = OperatorsLostBy(victim.Owner) + 1;

            if (outcome.CreditedTo.HasValue)
                _knockoutsScored[outcome.CreditedTo.Value] = KnockoutsScoredBy(outcome.CreditedTo.Value) + 1;
        }

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
                case DamageOutcome.Sheltered: events.Add(new DamageSheltered(target)); break;
                default:
                    events.Add(new DamageDealt(
                        target, result.AmountApplied, result.RemainingHealth, result.Cause, result.Type));
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
            // A home column is out of the fight too (§4.3) — the resolver has
            // always refused a caster standing in one, and this answered Ready,
            // which a tray would have drawn as castable and a bot proposed. Found
            // 2026-09-18 by Fortuna, whose two self-cast abilities are the first
            // that a seat wants while an operator is on its last stretch.
            if (caster.IsInYard || _win.HasFinished(caster) || _map.IsInHomeColumn(caster.Progress))
                return AbilityAvailability.CasterOutOfPlay;
            if (!_abilities.IsReady(caster, ability)) return AbilityAvailability.OnCooldown;

            if (_turns.CurrentPlayer == null || _turns.CurrentPlayer.Energy < ability.EnergyCost)
                return AbilityAvailability.InsufficientEnergy;

            // An ability that deals dice needs dice to deal with (§6.8). Checked
            // here as well as in UseAbility so a tray greys Boxcars out the
            // moment the first die is spent.
            if (_unspentDice.Count < DiceNeeded(ability)) return AbilityAvailability.DiceNotHeld;

            return AbilityAvailability.Ready;
        }

        /// <summary>
        /// How many unspent dice an ability needs before it can be cast at all
        /// (§6.8): the largest count any of its dice effects deals with. Zero for
        /// every ability that does not touch the roll.
        /// </summary>
        private static int DiceNeeded(AbilityDefinition ability)
        {
            int needed = 0;

            for (int i = 0; i < ability.Effects.Count; i++)
            {
                var effect = ability.Effects[i];
                if (effect.Kind == EffectKind.DealDice && effect.Amount > needed) needed = effect.Amount;
            }

            return needed;
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
        /// How many of the caster's own turns until the ability is off
        /// cooldown. 0 when it is ready now.
        /// </summary>
        /// <remarks>
        /// Delegated for the reason <see cref="LegalTargetsFor"/> gives: the
        /// resolver owns cooldowns. The tray shows this beside "cooling down",
        /// which on its own tells a player nothing about when to plan the cast.
        /// </remarks>
        public int TurnsUntilReady(OperatorState caster, AbilityDefinition ability) =>
            _abilities.TurnsUntilReady(caster, ability);

        /// <summary>
        /// Whether a <see cref="DeployCommand"/> for this operator would be
        /// accepted right now.
        /// </summary>
        /// <remarks>
        /// The board deploys an operator when its yard piece is clicked, so it
        /// has to know which yard pieces to mark before the click, and it must
        /// not work that out from the dice itself (PRESENTATION §1). The answer
        /// comes from the same checks the command runs.
        /// </remarks>
        public bool CanDeploy(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            return DeployRefusal(op) == null;
        }

        /// <summary>
        /// Whether an operator has reached HOME and left play for the match (§8).
        /// </summary>
        /// <remarks>
        /// The HUD shows each operator as waiting, on the board or home.
        /// Progress alone cannot say which without restating the board's
        /// geometry in the view.
        /// </remarks>
        public bool IsHome(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            return _win.HasFinished(op);
        }

        /// <summary>
        /// Whether damage aimed at <paramref name="op"/> where it stands would
        /// be void (§4.4, third amendment): it is on a safe cell or its own
        /// spawn cell.
        /// </summary>
        /// <remarks>
        /// For the view, which draws the shelter, and for the bots, which must
        /// not spend a cast blasting a start cell. Neither may restate the rule;
        /// both ask here.
        /// </remarks>
        public bool IsSheltered(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            return _sanctuary.Shelters(op);
        }

        /// <summary>
        /// Whether <paramref name="kind"/> would be refused on
        /// <paramref name="op"/> where it stands (§4.4, third amendment): a
        /// slow or a stun, on its own spawn cell.
        /// </summary>
        public bool Resists(OperatorState op, StatusKind kind)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            return _sanctuary.Resists(op, kind);
        }

        /// <summary>
        /// Whether the match is in its final stretch: some side has every
        /// operator but one home, so one more arrival wins it.
        /// </summary>
        /// <remarks>
        /// A presentation query, never a rule: it switches the music to the
        /// showdown (AUDIO.md). It is exposed here rather than in the view
        /// because the view computes nothing (PRESENTATION §1), and it is
        /// <i>answered</i> by <see cref="WinConditions"/> because "how close is
        /// anyone to winning" is the win condition's business — counted per
        /// seat, a team match would cue the showdown while the partner seat
        /// still had three operators in its yard (ADR-0012).
        /// </remarks>
        public bool IsFinalStretch =>
            !MatchOver && _win.IsFinalStretch(_turns.Players);


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

                AddPreview(previews, op, null, pooled);

                if (_unspentDice.Count < 2) continue;

                for (int i = 0; i < _unspentDice.Count; i++)
                {
                    if (IsRepeatedFace(i)) continue;
                    AddPreview(previews, op, _unspentDice[i], _unspentDice[i]);
                }
            }

            return previews;
        }

        private void AddPreview(
            List<LandingPreview> into, OperatorState op, int? die, int pips)
        {
            int cells = CellsFor(op, pips, out _, out _);
            if (cells <= 0) return;

            // A table truncates this option, and hiding that would show the
            // player a landing the engine will not give them (§7.7, §9.1).
            var table = FirstTableOnPath(op, cells);
            if (table != null) cells = table.Value.Cells;

            var move = _movement.ResolveMove(op, cells);
            into.Add(new LandingPreview(op.Id, die, move.To, cells));
        }

        /// <summary>
        /// The first enemy table this move would cross or end on, or null (§7.7).
        /// Asks and changes nothing, so the preview may call it freely.
        /// </summary>
        /// <remarks>
        /// The path is the cells the move crosses, in order, <b>excluding the one
        /// it starts on</b> — an operator standing on a table is not stopped by it
        /// again, which is how a stopped piece gets to leave. Cells in a home
        /// column are included in the walk and never match: a table can only be
        /// dealt on the outer track, so the finish stays out of the fight (§4.3).
        /// </remarks>
        private TableInterception? FirstTableOnPath(OperatorState op, int cells)
        {
            if (cells <= 0 || !_map.IsOnOuterTrack(op.Progress)) return null;

            int journey = _map.Profile.Journey;
            var path = new List<CellRef>(cells);

            for (int step = 1; step <= cells; step++)
            {
                int progress = op.Progress + step;
                if (progress > journey) break;

                path.Add(_map.CellAt(op.Owner, progress));
            }

            if (path.Count == 0) return null;

            return _cellEffects.FirstInterception(op.Owner, op.Id, path);
        }

        private bool IsRepeatedFace(int index)
        {
            for (int i = 0; i < index; i++)
                if (_unspentDice[i] == _unspentDice[index]) return true;

            return false;
        }

        /// <summary>
        /// The track cells <paramref name="op"/>'s aura covers right now — its
        /// radius and, for Catalyst, its slipstream behind (§10.10). Empty for
        /// an operator with no aura. Display only: the lane drawn when the
        /// holder is selected or hovered.
        /// </summary>
        public IReadOnlyList<CellRef> AuraCellsOf(OperatorState op) => _auras.CellsCovered(op);

        /// <summary>Which side <paramref name="op"/>'s aura reaches, or null for none. Display only.</summary>
        public AuraSide? AuraSideOf(OperatorState op) => _auras.SideOf(op);

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
        ///
        /// <b>Haste from an ally's aura is listed too (2026-09-17).</b> It is
        /// not a status, but a player reads it as one: the HASTE tag appears
        /// on an ally the moment it stands within Lethe's Catalyst and goes
        /// the moment it leaves, which is the whole of how the aura is taught.
        /// </remarks>
        public IReadOnlyList<StatusKind> ActiveStatusesOn(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            var kinds = _statuses.ActiveKinds(op);
            if (!_auras.GrantsHaste(op, _operators)) return kinds;

            foreach (var kind in kinds)
                if (kind == StatusKind.Hastened) return kinds;

            var withHaste = new List<StatusKind>(kinds) { StatusKind.Hastened };
            return withHaste;
        }

        /// <summary>
        /// Whether the operator's next Normal or Tech hit this round would get
        /// the evasion roll (§5.5). For the bots; no operator holds Evasion
        /// since 2026-09-24.
        /// </summary>
        public bool EvasionReady(OperatorState op) => _statuses.EvasionReady(op);

        /// <summary>
        /// Whether an operator will carry a status on its owner's next turn,
        /// for callers that plan against that turn — the bots, weighing a
        /// watch against a stun (2026-09-17). Auras are not included: they
        /// depend on where pieces stand then.
        /// </summary>
        public bool HasStatusOnNextTurn(OperatorState op, StatusKind kind)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            return _statuses.HasOnNextTurn(op, kind);
        }

        /// <summary>
        /// What is left of an operator's shield pool (§5.6), for callers that
        /// weigh a hit before making it — the bots. Zero without a shield.
        /// </summary>
        public int ShieldPoolOn(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            return _statuses.ShieldPool(op);
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

        /// <summary>Cells holding a table right now (§7.7). Drawn differently again.</summary>
        /// <remarks>
        /// A table is the only cell effect that never resolves on a clock, so the
        /// board must show it for as long as it stands or a player has no way to
        /// know why a run stopped.
        /// </remarks>
        public IReadOnlyList<CellRef> ActiveTables() => _cellEffects.ActiveTables();

        /// <summary>Whether a seat holds a table on this cell (§7.7).</summary>
        public bool HasTableOn(CellRef cell, PlayerColor owner) => _cellEffects.HasTableOn(cell, owner);

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
                if (CellsFor(op, pooled, out _, out _) > 0) return true;
            }

            return false;
        }

        private bool CanBeMoved(OperatorState op) =>
            !op.IsInYard && !_win.HasFinished(op) && !_statuses.IsStunned(op);

        /// <summary>Whether any operator of the current player could move at all, whatever the dice.</summary>
        private bool HasMovableOperator()
        {
            var player = _turns.CurrentPlayer;
            if (player == null) return false;

            foreach (var op in player.Operators)
                if (CanBeMoved(op)) return true;

            return false;
        }

        /// <summary>
        /// Speed is base, plus statuses, plus any aura reaching it — all
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

        /// <summary>
        /// Cells this operator moves for <paramref name="pips"/> right now,
        /// plus the haste bonus if it is due (COMBAT_SYSTEMS §5.9).
        /// </summary>
        /// <remarks>
        /// <b>The one place movement distance is computed.</b> <see cref="Move"/>,
        /// <see cref="PreviewLandings"/> and <see cref="HasLegalMove"/> all call
        /// it, so the preview can never promise a landing the cap then trims.
        /// Only <see cref="Move"/> charges <paramref name="hasteCells"/> to the
        /// budget; the other two only ask.
        ///
        /// <b>The bonus is flat cells added after speed.</b> +1 when the roll in
        /// hand totals 6 or less, +2 above (<c>CombatConfig.HasteCellsFor</c>),
        /// once per roll per operator, and never more than what is left of
        /// this turn's <c>HasteBonusCellCap</c>. The roll's total counts even
        /// if one of its dice went on a deploy or to another operator. A move
        /// the dice alone would not make (0 cells) gets no bonus either, so
        /// haste never turns a refused move into a legal one.
        ///
        /// <b>A burden is the same adjustment with the sign flipped</b>
        /// (§5.16, 2026-09-17): −1 or −2 on the first move from a roll, never
        /// below 1 cell, with no turn budget. It is paid under the same
        /// once-per-roll flag, so a hastened, burdened operator nets the two
        /// once. The haste budget is charged the full bonus even when a burden
        /// cancels it — the bonus was granted, the burden took it back.
        /// </remarks>
        private int CellsFor(OperatorState op, int pips, out int hasteCells, out int speedBonusCells)
        {
            int cells = _movement.CellsFor(pips, SpeedOf(op));
            hasteCells = 0;
            speedBonusCells = 0;

            // The speed bonus over a 1.0× move is capped per operator per turn
            // (§6.3, 2026-09-17). The cap trims the bonus, never the pips, and
            // a slowed operator — effective speed at or under 1.0× — has no
            // bonus to trim. PreviewLandings and HasLegalMove call this too,
            // so a preview can never promise a landing the cap then claws back;
            // only Move charges the budget.
            int speedBonus = cells - pips;
            if (speedBonus > 0)
            {
                speedBonusCells = Math.Min(speedBonus,
                    Math.Max(0, _config.SpeedBonusCellCap - SpeedCellsUsed(op)));
                cells = pips + speedBonusCells;
            }

            bool hastened = IsHastenedNow(op);
            bool burdened = _statuses.IsBurdened(op);

            if (cells <= 0 || (!hastened && !burdened) || _hastePaidThisRoll.Contains(op.Id))
                return cells;

            if (hastened)
            {
                int cap = op.HasteCellCap ?? _config.HasteBonusCellCap;
                int budget = Math.Max(0, cap - HasteCellsUsed(op));
                hasteCells = Math.Min(_config.HasteCellsFor(_rollTotal), budget);
            }

            int burden = burdened ? _config.BurdenCellsFor(_rollTotal) : 0;

            return Math.Max(1, cells + hasteCells - burden);
        }

        /// <summary>
        /// Hastened by a status, a passive, or an ally's aura (§5.9) — the one
        /// question haste asks, asked the same way by the move, the preview and
        /// the legality check.
        /// </summary>
        /// <remarks>
        /// <b>Catalyst is read here, not in the status registry (2026-09-17).</b>
        /// An aura is true of a position, not of an operator, and the registry
        /// does not know where anybody stands.
        /// </remarks>
        private bool IsHastenedNow(OperatorState op) =>
            _statuses.IsHastened(op) || _auras.GrantsHaste(op, _operators);

        private PlayerState PlayerOf(PlayerColor color)
        {
            foreach (var player in _turns.Players)
                if (player.Color == color) return player;
            return null;
        }

        private int HasteCellsUsed(OperatorState op) =>
            _hasteCellsUsed.TryGetValue(op.Id, out int used) ? used : 0;

        private int SpeedCellsUsed(OperatorState op) =>
            _speedCellsUsed.TryGetValue(op.Id, out int used) ? used : 0;

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
            _hasteCellsUsed.Clear();
            _speedCellsUsed.Clear();
            _hastePaidThisRoll.Clear();
            _rollTotal = 0;
            _cashedThisTurn = false;
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