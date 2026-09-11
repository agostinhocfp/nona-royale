// Assets/_Project/Scripts/Core/Services/TurnStateMachine.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Drives the turn: upkeep, roll, action, end (COMBAT_SYSTEMS §6).
    /// </summary>
    /// <remarks>
    /// <b>It orchestrates, it does not decide.</b> Bleed damage goes through the
    /// pipeline, energy through the ledger, expiry through the registry. Nothing
    /// here re-implements a rule that lives elsewhere — this type owns only the
    /// order things happen in, which is itself a rule and the one no other
    /// service can own.
    ///
    /// <b>The phases are enforced.</b> Rolling before upkeep, or twice without
    /// doubles, throws rather than silently misbehaving. An illegal sequence is a
    /// bug in the caller and should be loud.
    ///
    /// <b>The action phase is the caller's.</b> Deploying, moving and using
    /// abilities happen between <see cref="Roll"/> and <see cref="EndTurn"/>,
    /// through the services that own them. The machine only bounds the window.
    /// </remarks>
    public sealed class TurnStateMachine
    {
        private readonly IReadOnlyList<PlayerState> _players;
        private readonly MatchClock _clock;
        private readonly GameConfig _config;
        private readonly IRandom _random;
        private readonly EnergyLedger _energy;
        private readonly StatusRegistry _statuses;
        private readonly DamagePipeline _damage;
        private readonly NeutralizeRules _neutralize;
        private readonly WinConditions _win;

        private int _seatIndex = -1;
        private int _rollsThisTurn;
        private bool _lastRollWasDouble;

        public TurnStateMachine(
            IReadOnlyList<PlayerState> players,
            MatchClock clock,
            GameConfig config,
            IRandom random,
            EnergyLedger energy,
            StatusRegistry statuses,
            DamagePipeline damage,
            NeutralizeRules neutralize,
            WinConditions win)
        {
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _energy = energy ?? throw new ArgumentNullException(nameof(energy));
            _statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _neutralize = neutralize ?? throw new ArgumentNullException(nameof(neutralize));
            _win = win ?? throw new ArgumentNullException(nameof(win));

            if (_players.Count == 0)
                throw new ArgumentException("A match needs at least one player.", nameof(players));
        }

        public TurnPhase Phase { get; private set; } = TurnPhase.BetweenTurns;

        /// <summary>Null between turns.</summary>
        public PlayerState CurrentPlayer { get; private set; }

        public int RollsRemaining => Math.Max(0, _config.MaxRollsPerTurn - _rollsThisTurn);

        /// <summary>
        /// Whether the player may roll again. Doubles buy an extra movement
        /// roll, bounded by <see cref="GameConfig.MaxRollsPerTurn"/> so one hot
        /// streak cannot run forever.
        /// </summary>
        public bool CanRollAgain =>
            Phase == TurnPhase.Action && _lastRollWasDouble && RollsRemaining > 0;

        /// <summary>
        /// Starts the next seat's turn and runs upkeep: bleed ticks, evasion
        /// charges re-arm. Cooldowns need no work — they are absolute turn
        /// indices, so advancing the turn advances them.
        /// </summary>
        public UpkeepReport BeginTurn()
        {
            RequirePhase(TurnPhase.BetweenTurns, nameof(BeginTurn));

            _seatIndex = (_seatIndex + 1) % _players.Count;
            CurrentPlayer = _players[_seatIndex];
            _clock.BeginTurnFor(CurrentPlayer);

            _rollsThisTurn = 0;
            _lastRollWasDouble = false;

            var ticks = new List<DamageResult>();
            var neutralized = new List<OperatorState>();

            foreach (var op in CurrentPlayer.Operators)
            {
                _statuses.RefreshEvasion(op);

                int bleed = _statuses.ConsumeBleed(op);
                if (bleed <= 0) continue;

                // Bleed is Atomic, so it goes around evasion and shields. An
                // operator that dies at upkeep never gets its turn (§5.3).
                var result = _damage.Apply(op, new DamageInstance(bleed, DamageType.Atomic, op.Id, "bleed"));
                ticks.Add(result);

                if (result.Outcome == DamageOutcome.Neutralized)
                {
                    _neutralize.Apply(op);
                    neutralized.Add(op);
                }
            }

            Phase = TurnPhase.AwaitingRoll;
            return new UpkeepReport(CurrentPlayer.Color, ticks, neutralized);
        }

        /// <summary>
        /// Rolls the dice and, on the turn's first roll only, grants energy.
        /// </summary>
        /// <remarks>
        /// Doubles grant an extra movement roll but never a second energy grant —
        /// otherwise one double snowballs both axes at once (§3.1). The ledger
        /// enforces that itself; this method simply calls it every time.
        /// </remarks>
        public RollReport Roll()
        {
            if (Phase != TurnPhase.AwaitingRoll && !CanRollAgain)
            {
                throw new InvalidOperationException(
                    Phase == TurnPhase.Action
                        ? "A second roll needs doubles and a remaining roll in the budget."
                        : $"Cannot roll during {Phase}.");
            }

            var roll = DiceRoll.Roll(_random, _config);
            _rollsThisTurn++;
            _lastRollWasDouble = roll.IsDouble;

            var grant = _energy.GrantForTurn(CurrentPlayer, roll);

            Phase = TurnPhase.Action;
            return new RollReport(roll, grant, CanRollAgain, RollsRemaining);
        }

        /// <summary>
        /// Closes the turn: status durations expire, then the win check runs.
        /// </summary>
        /// <remarks>
        /// Expiry sits here rather than at upkeep so a 1-turn stun applied during
        /// an opponent's turn blocks a real action phase before it lapses (§6).
        /// </remarks>
        public EndTurnReport EndTurn()
        {
            if (Phase != TurnPhase.Action && Phase != TurnPhase.AwaitingRoll)
                throw new InvalidOperationException($"Cannot end a turn during {Phase}.");

            var expired = new List<ExpiredStatus>();

            foreach (var op in CurrentPlayer.Operators)
            {
                foreach (var kind in _statuses.ExpireCompleted(op))
                    expired.Add(new ExpiredStatus(op, kind));
            }

            PlayerColor? winner = _win.Winner(_players);

            Phase = winner == null ? TurnPhase.BetweenTurns : TurnPhase.MatchOver;
            var report = new EndTurnReport(CurrentPlayer.Color, expired, winner);

            if (winner == null) CurrentPlayer = null;
            return report;
        }

        private void RequirePhase(TurnPhase expected, string action)
        {
            if (Phase != expected)
                throw new InvalidOperationException($"{action} is not legal during {Phase}.");
        }
    }
}