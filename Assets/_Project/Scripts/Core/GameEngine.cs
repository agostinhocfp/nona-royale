// Assets/_Project/Scripts/Core/GameEngine.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
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

        // Per-roll action state. Exactly one operator moves per roll (§6).
        private DiceRoll _currentRoll;
        private DeployOption _deployOption;
        private int _deploysThisRoll;
        private int _movementValue;
        private bool _hasRolled;
        private bool _hasMovedThisRoll;

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
            CombatConfig config)
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
        }

        public TurnPhase Phase => _turns.Phase;
        public PlayerState CurrentPlayer => _turns.CurrentPlayer;
        public bool MatchOver => _turns.Phase == TurnPhase.MatchOver;

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
                events.Add(new DamageDealt(bleeding, tick.AmountApplied, tick.RemainingHealth));
            }

            // Neutralizes from upkeep are already applied by TurnStateMachine,
            // so this reports rather than resolves. A mark payout triggered at
            // upkeep is therefore applied to state but not announced — see
            // decision log D-010.
            foreach (var op in upkeep.Neutralized)
                events.Add(new OperatorNeutralized(op));

            ResetRollState();
        }

        private void Roll(List<IGameEvent> events)
        {
            if (_hasRolled && !_turns.CanRollAgain)
            {
                events.Add(new CommandRejected(
                    "a second roll needs doubles and a remaining roll in the budget"));
                return;
            }

            var report = _turns.Roll();

            _currentRoll = report.Roll;
            _deploysThisRoll = 0;
            _hasMovedThisRoll = false;
            _hasRolled = true;

            _deployOption = _movement.GetDeployOption(_currentRoll, CountInYard(_turns.CurrentPlayer));
            _movementValue = _deployOption.TotalIfDeclined;

            events.Add(new DiceRolled(report.Roll, report.GrantsAnotherRoll));

            if (report.Grant.WasGranted)
            {
                events.Add(new EnergyGranted(
                    _turns.CurrentPlayer.Color, report.Grant.Stored, report.Grant.Burned, report.Grant.Total));
            }
        }

        private void EndTurn(List<IGameEvent> events)
        {
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

            if (!_deployOption.IsAvailable || _deploysThisRoll >= _deployOption.MaxOperators)
            {
                events.Add(new CommandRejected("this roll cannot deploy another operator"));
                return;
            }

            if (_hasMovedThisRoll)
            {
                // A deploy consumes a die, so it has to be declared before the
                // movement value is spent.
                events.Add(new CommandRejected("deploy before moving on this roll"));
                return;
            }

            op.MoveTo(_movement.DeployProgress);
            _deploysThisRoll++;
            _movementValue = _deployOption.MovementAfterDeploying(_deploysThisRoll);

            events.Add(new OperatorDeployed(op, _map.CellAt(op.Owner, op.Progress)));
        }

        private void Move(MoveCommand command, List<IGameEvent> events)
        {
            if (!RequireAction(events)) return;

            var op = FindOwnedOperator(command.OperatorId, events);
            if (op == null) return;

            if (_hasMovedThisRoll)
            {
                events.Add(new CommandRejected("exactly one operator moves per roll"));
                return;
            }

            if (_movementValue <= 0)
            {
                events.Add(new CommandRejected("this roll has no movement left"));
                return;
            }

            if (op.IsInYard)
            {
                events.Add(new CommandRejected($"{op.Name} is in the yard"));
                return;
            }

            if (_statuses.IsStunned(op))
            {
                events.Add(new CommandRejected($"{op.Name} is stunned"));
                return;
            }

            // Speed is base, plus statuses, plus any enemy aura reaching it —
            // all evaluated now, because an aura's truth changes with position.
            //
            // The two channels are summed, which means a slow and an enemy aura
            // stack even though COMBAT_SYSTEMS §5.2 says slow sources do not.
            // Pre-existing and unresolved — decision log D-007.
            double speed = _movement.EffectiveSpeed(
                op.BaseSpeedMultiplier,
                _statuses.SpeedModifier(op) + _auras.SpeedModifierFor(op, _operators));

            int cells = _movement.CellsFor(_movementValue, speed);
            var move = _movement.ResolveMove(op, cells);
            var collision = _collisions.Resolve(op, move, _operators);

            op.MoveTo(collision.MoverFinalProgress);
            _hasMovedThisRoll = true;

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
                        Neutralize(occupant, events);
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
            var resolution = _abilities.Use(caster, ability, target, _turns.CurrentPlayer, _operators);

            if (!resolution.Approved)
            {
                events.Add(new CommandRejected($"{ability.Name}: {resolution.Refusal}"));
                return;
            }

            events.Add(new EnergySpent(
                _turns.CurrentPlayer.Color, energyBefore - _turns.CurrentPlayer.Energy, _turns.CurrentPlayer.Energy));

            foreach (var outcome in resolution.Outcomes)
                EmitOutcome(outcome, events);
        }

        // ── Event translation ────────────────────────────────────────────

        private void EmitOutcome(EffectOutcome outcome, List<IGameEvent> events)
        {
            switch (outcome.Kind)
            {
                case EffectOutcomeKind.Damaged:
                    EmitDamage(outcome.Recipient, outcome.Damage, events);
                    if (outcome.Damage.Outcome == DamageOutcome.Neutralized)
                        Neutralize(outcome.Recipient, events);
                    break;

                case EffectOutcomeKind.Executed:
                    events.Add(new DamageDealt(outcome.Recipient, 0, 0));
                    Neutralize(outcome.Recipient, events);
                    break;

                case EffectOutcomeKind.Healed:
                    events.Add(new HealApplied(outcome.Recipient, outcome.Amount));
                    break;

                case EffectOutcomeKind.StatusApplied:
                    events.Add(new StatusApplied(outcome.Recipient, outcome.Status, outcome.Duration));
                    break;

                case EffectOutcomeKind.Pulled:
                    events.Add(new OperatorMoved(outcome.Recipient, outcome.Progress, outcome.Progress,
                        _map.CellAt(outcome.Recipient.Owner, outcome.Progress)));
                    break;
            }
        }

        /// <summary>
        /// Yards a neutralized operator and reports everything that followed,
        /// including a mark payout (COMBAT_SYSTEMS §10.2).
        /// </summary>
        /// <remarks>
        /// Centralised so no call site can neutralize without announcing what it
        /// triggered. Three paths reach here — a collision, an ability's damage,
        /// and Miracle Pull's execute — and before the payout existed each
        /// carried its own copy of the two lines this replaces.
        /// </remarks>
        private void Neutralize(OperatorState op, List<IGameEvent> events)
        {
            var hastened = _neutralize.Apply(op);

            events.Add(new OperatorNeutralized(op));

            foreach (var ally in hastened)
                events.Add(new StatusApplied(ally, StatusKind.Hastened, _config.HasteDurationTurns));
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
                    events.Add(new DamageDealt(target, result.AmountApplied, result.RemainingHealth));
                    break;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private bool RequireAction(List<IGameEvent> events)
        {
            if (_turns.Phase == TurnPhase.Action) return true;

            events.Add(new CommandRejected("roll first"));
            return false;
        }

        private void ResetRollState()
        {
            _hasRolled = false;
            _hasMovedThisRoll = false;
            _deploysThisRoll = 0;
            _movementValue = 0;
            _deployOption = DeployOption.Unavailable(0);
        }

        private int CountInYard(PlayerState player)
        {
            int count = 0;
            foreach (var op in player.Operators)
                if (op.IsInYard) count++;

            return count;
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

        /// <summary>
        /// Where each of the current player's operators would land if moved with
        /// the movement left on this roll. Empty before the first roll, or once
        /// an operator has already moved.
        /// </summary>
        /// <remarks>
        /// <b>Read-only, and deliberately shares <see cref="Move"/>'s arithmetic
        /// rather than restating it.</b> A preview that computes distance its own
        /// way is a second copy of a rule, and the two will disagree the first
        /// time a slow or an aura is in play — which is precisely when a player
        /// is relying on the preview.
        ///
        /// It stops short of collision: it reports the landing, not whether the
        /// landing is contested. Showing the bounce-back would be showing the
        /// player the outcome of a fight before they commit to it.
        /// </remarks>
        public IReadOnlyDictionary<int, int> PreviewLandings()
        {
            var landings = new Dictionary<int, int>();

            if (_turns.Phase != TurnPhase.Action || _hasMovedThisRoll || _movementValue <= 0)
                return landings;

            foreach (var op in _turns.CurrentPlayer.Operators)
            {
                if (op.IsInYard || _win.HasFinished(op)) continue;
                if (_statuses.IsStunned(op)) continue;

                double speed = _movement.EffectiveSpeed(
                    op.BaseSpeedMultiplier,
                    _statuses.SpeedModifier(op) + _auras.SpeedModifierFor(op, _operators));

                var move = _movement.ResolveMove(op, _movement.CellsFor(_movementValue, speed));
                landings[op.Id] = move.To;
            }

            return landings;
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

    }


}