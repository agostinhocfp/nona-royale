// Assets/_Project/Scripts/Core/Services/TurnReports.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>A status that lapsed at the end of a turn, and who was carrying it.</summary>
    public readonly struct ExpiredStatus
    {
        public ExpiredStatus(OperatorState op, StatusKind kind)
        {
            Operator = op;
            Kind = kind;
        }

        public OperatorState Operator { get; }
        public StatusKind Kind { get; }

        public override string ToString() => $"{Kind} expired on {Operator?.Name}";
    }

    /// <summary>What upkeep resolved: bleed ticks, and anyone they finished off.</summary>
    public sealed class UpkeepReport
    {
        public UpkeepReport(
            PlayerColor player,
            IReadOnlyList<DamageResult> bleedTicks,
            IReadOnlyList<OperatorState> neutralized)
        {
            Player = player;
            BleedTicks = bleedTicks ?? throw new ArgumentNullException(nameof(bleedTicks));
            Neutralized = neutralized ?? throw new ArgumentNullException(nameof(neutralized));
        }

        public PlayerColor Player { get; }

        public IReadOnlyList<DamageResult> BleedTicks { get; }

        /// <summary>Operators that died at upkeep. They never get this turn (§5.3).</summary>
        public IReadOnlyList<OperatorState> Neutralized { get; }

        public override string ToString() =>
            $"{Player} upkeep: {BleedTicks.Count} bleed ticks, {Neutralized.Count} neutralized";
    }

    /// <summary>The dice, what they paid, and whether another roll is coming.</summary>
    public readonly struct RollReport
    {
        public RollReport(DiceRoll roll, EnergyGrant grant, bool grantsAnotherRoll, int rollsRemaining)
        {
            Roll = roll;
            Grant = grant;
            GrantsAnotherRoll = grantsAnotherRoll;
            RollsRemaining = rollsRemaining;
        }

        public DiceRoll Roll { get; }

        /// <summary>Refused on a doubles re-roll — energy is granted once per turn.</summary>
        public EnergyGrant Grant { get; }

        /// <summary>Doubles, and budget left to use them.</summary>
        public bool GrantsAnotherRoll { get; }

        public int RollsRemaining { get; }

        public override string ToString() => $"{Roll} {Grant}";
    }

    /// <summary>What closing the turn resolved.</summary>
    public sealed class EndTurnReport
    {
        public EndTurnReport(
            PlayerColor player,
            IReadOnlyList<ExpiredStatus> expired,
            PlayerColor? winner)
        {
            Player = player;
            Expired = expired ?? throw new ArgumentNullException(nameof(expired));
            Winner = winner;
        }

        public PlayerColor Player { get; }
        public IReadOnlyList<ExpiredStatus> Expired { get; }

        /// <summary>Set once someone has every operator home. Null while the match runs.</summary>
        public PlayerColor? Winner { get; }

        public bool MatchOver => Winner != null;

        public override string ToString() =>
            MatchOver ? $"{Winner} wins" : $"{Player} ends turn, {Expired.Count} statuses expired";
    }
}