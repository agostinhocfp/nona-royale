// Assets/_Project/Scripts/Core/GameEngine.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
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
            WinConditions win)
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
                    {
                        _neutralize.Apply(occupant);
                        events.Add(new OperatorNeutralized(occupant));
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
                    {
                        _neutralize.Apply(outcome.Recipient);
                        events.Add(new OperatorNeutralized(outcome.Recipient));
                    }
                    break;

                case EffectOutcomeKind.Executed:
                    events.Add(new DamageDealt(outcome.Recipient, 0, 0));
                    _neutralize.Apply(outcome.Recipient);
                    events.Add(new OperatorNeutralized(outcome.Recipient));
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
    }
}