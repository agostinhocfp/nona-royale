// Assets/_Project/Scripts/Core/Bots/BotBrain.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Draft;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;

namespace NonaRoyale.Core.Bots
{
    /// <summary>
    /// The CPU seat: one command at a time, chosen from what the engine allows
    /// and ranked by the seat's personality (BOTS.md decisions 1–3).
    /// </summary>
    /// <remarks>
    /// <b>Order of a decision:</b>
    /// <list type="number">
    /// <item>Before the roll: roll.</item>
    /// <item>The best cast, if it clears the personality's bar. Casts spend no
    /// dice, so a worthwhile one is taken while it is still in reach.</item>
    /// <item>The best way to spend the dice: a deploy or a landing.</item>
    /// <item>A second roll, if doubles earned one.</item>
    /// <item>End the turn.</item>
    /// </list>
    ///
    /// <b>Refusals are remembered for the turn.</b> A command the engine
    /// refused is never proposed again in the same turn, and a turn that runs
    /// past <see cref="BotConfig.MaxActionsPerTurn"/> ends regardless. Both
    /// are guards; a correct bot never needs them.
    ///
    /// <b>One brain per seat.</b> It keeps per-turn memory, so sharing one
    /// between seats would mix their refusals.
    /// </remarks>
    public sealed class BotBrain : IBot
    {
        private readonly BotConfig _config;
        private readonly CombatConfig _combat;
        private readonly GameConfig _game;
        private readonly IRandom _random;
        private readonly HashSet<string> _refused = new HashSet<string>();

        private PlayerColor _turnSeat = PlayerColor.None;
        private int _turnIndex = -1;
        private int _actions;

        public BotBrain(
            BotPersonality personality,
            IRandom random,
            BotConfig config = null,
            BotWeights weights = null,
            CombatConfig combat = null,
            GameConfig game = null)
        {
            Personality = personality;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _config = config ?? BotConfig.Default;
            Weights = weights ?? BotWeights.For(personality);
            _combat = combat ?? CombatConfig.Default;
            _game = game ?? GameConfig.Default;
        }

        public BotPersonality Personality { get; }

        public BotWeights Weights { get; }

        /// <summary>Refusals this brain has seen, across the match. Zero for a well-behaved bot.</summary>
        public int RefusalCount { get; private set; }

        /// <summary>The last refusal's reason, for the log.</summary>
        public string LastRefusal { get; private set; }

        /// <summary>Turns this brain had to end through the action cap.</summary>
        public int GuardedTurns { get; private set; }

        public ICommand Next(MatchFactory.Match match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));

            var engine = match.Engine;
            if (engine.MatchOver) return null;

            var seat = engine.CurrentPlayer;
            if (seat == null) return null;

            if (seat.Color != _turnSeat || seat.TurnIndex != _turnIndex)
            {
                _turnSeat = seat.Color;
                _turnIndex = seat.TurnIndex;
                _refused.Clear();
                _actions = 0;
            }

            _actions++;
            if (_actions > _config.MaxActionsPerTurn)
            {
                if (_actions == _config.MaxActionsPerTurn + 1) GuardedTurns++;
                return new EndTurnCommand();
            }

            if (engine.Phase == TurnPhase.AwaitingRoll) return new RollDiceCommand();

            var board = new BotBoard(match, _config, _combat, _game);

            var cast = BestCast(board);
            if (cast != null) return cast;

            if (engine.UnspentDice.Count > 0)
            {
                foreach (var option in MoveScorer.Rank(board, Weights, _random))
                    if (!_refused.Contains(Key(option.Command))) return option.Command;
            }

            var roll = new RollDiceCommand();
            if (engine.CanRollAgain && !engine.MustSpendRoll && !_refused.Contains(Key(roll))) return roll;

            return new EndTurnCommand();
        }

        public void Observe(ICommand command, IReadOnlyList<IGameEvent> events)
        {
            if (command == null || events == null) return;

            foreach (var e in events)
            {
                if (!(e is CommandRejected rejected)) continue;

                _refused.Add(Key(command));
                RefusalCount++;
                LastRefusal = $"{command.GetType().Name}: {rejected.Reason}";
                return;
            }
        }

        public OperatorDefinition PickDraft(DraftState draft, PlayerColor seat) =>
            DraftPicker.Choose(draft, seat, Weights, _random, _config);

        /// <summary>The cast to make now, or null. Applies the personality's bar and the Banker's reserve.</summary>
        private ICommand BestCast(BotBoard board)
        {
            var seat = board.Engine.CurrentPlayer;
            int savings = Weights.SaveForBest ? CastPlanner.SavingsTarget(board) : 0;

            foreach (var option in CastPlanner.Rank(board, Weights, _random))
            {
                if (option.Score < Weights.CastThreshold) return null;

                var command = option.ToCommand();
                if (_refused.Contains(Key(command))) continue;

                if (HoldsForReserve(Weights, seat.Energy, option.Ability.EnergyCost, savings, option.Score))
                    continue;

                return command;
            }

            return null;
        }

        /// <summary>
        /// The Banker's rule: skip a cheaper cast that would leave the pool
        /// below the squad's most expensive ability, unless the play is worth
        /// <see cref="BotWeights.ReserveOverride"/> (a kill, or a real rescue).
        /// </summary>
        public static bool HoldsForReserve(BotWeights weights, int energy, int cost, int savings, double score)
        {
            if (weights == null || !weights.SaveForBest) return false;
            if (cost >= savings) return false;
            if (energy - cost >= savings) return false;
            return score < weights.ReserveOverride;
        }

        /// <summary>A command's identity within a turn, for the refusal memory.</summary>
        public static string Key(ICommand command)
        {
            switch (command)
            {
                case RollDiceCommand _: return "roll";
                case EndTurnCommand _: return "end";
                case DeployCommand deploy: return $"deploy:{deploy.OperatorId}";
                case MoveCommand move: return $"move:{move.OperatorId}:{move.DieFace?.ToString() ?? "all"}";
                case UseAbilityCommand cast:
                    string cell = cast.TargetCell.HasValue ? cast.TargetCell.Value.ToString() : "-";
                    return $"cast:{cast.CasterOperatorId}:{cast.AbilityId}:{cast.TargetOperatorId?.ToString() ?? "-"}:{cell}";
                default: return command?.GetType().Name ?? "null";
            }
        }
    }
}