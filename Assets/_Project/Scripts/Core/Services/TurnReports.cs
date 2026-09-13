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

    /// <summary>An operator that fell at upkeep, what finished it, and what that paid out.</summary>
    /// <remarks>
    /// <b>A bare operator was not enough.</b> Upkeep resolves in a phase where
    /// nothing else moves, so an operator that loses its last health there simply
    /// vanishes to its yard. Carrying the cause is what lets the view say
    /// <i>bleed</i> rather than leaving the player to infer it.
    ///
    /// <b>The hastened allies are carried for a different reason.</b>
    /// <c>TurnStateMachine</c> applies an upkeep neutralize itself, so
    /// <c>GameEngine.BeginTurn</c> reports rather than resolves — and a mark
    /// payout triggered there was applied to state and never announced. Badges
    /// appeared on a later refresh with no event explaining them
    /// (<c>PRESENTATION.md</c> §7).
    /// </remarks>
    public readonly struct UpkeepNeutralize
    {
        public UpkeepNeutralize(OperatorState op, string cause, NeutralizeOutcome outcome)
        {
            Operator = op;
            Cause = cause;
            Outcome = outcome;
        }

        public OperatorState Operator { get; }

        /// <summary>What finished it — "bleed" or "mark" (§5.3, §5.7).</summary>
        public string Cause { get; }

        /// <summary>The mark payout and the bounty this death produced.</summary>
        public NeutralizeOutcome Outcome { get; }

        /// <summary>Allies hastened by a mark payout this kill triggered (§10.2). Never null.</summary>
        public IReadOnlyList<OperatorState> Hastened => Outcome.Hastened;

    }

    /// <summary>What upkeep resolved: over-time ticks, and anyone they finished off.</summary>
    public sealed class UpkeepReport
    {
        public UpkeepReport(
            PlayerColor player,
            IReadOnlyList<DamageResult> bleedTicks,
            IReadOnlyList<UpkeepNeutralize> neutralized)
        {
            Player = player;
            BleedTicks = bleedTicks ?? throw new ArgumentNullException(nameof(bleedTicks));
            Neutralized = neutralized ?? throw new ArgumentNullException(nameof(neutralized));
        }

        public PlayerColor Player { get; }

        /// <summary>
        /// Every over-time tick this upkeep, bleed and mark alike. Each result
        /// carries its own cause, so the name is now narrower than the contents.
        /// </summary>
        public IReadOnlyList<DamageResult> BleedTicks { get; }

        /// <summary>Operators that died at upkeep. They never get this turn (§5.3).</summary>
        public IReadOnlyList<UpkeepNeutralize> Neutralized { get; }

        public override string ToString() =>
            $"{Player} upkeep: {BleedTicks.Count} ticks, {Neutralized.Count} neutralized";
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