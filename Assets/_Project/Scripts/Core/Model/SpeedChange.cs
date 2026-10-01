// Assets/_Project/Scripts/Core/Model/SpeedChange.cs
using System;

namespace NonaRoyale.Core.Model
{
    /// <summary>
    /// A speed adjustment kept as two numbers rather than one: the strongest
    /// bonus reaching an operator and the strongest penalty on it
    /// (COMBAT_SYSTEMS §5.2).
    /// </summary>
    /// <remarks>
    /// <b>This type exists because penalties do not stack and bonuses are not
    /// penalties</b> (designer, 2026-10-01). A single number cannot express
    /// that: once a bonus and a penalty are added together, nothing downstream
    /// can tell a −1.0 that is one deep slow from a −1.0 that is a −1.5 slow
    /// under a +0.5 aura, and the two must combine differently with a third
    /// source.
    ///
    /// <b>Combining is <see cref="Strongest"/>, never addition.</b> Two sources
    /// of slow give the deeper of the two, which is the rule §5.2 always
    /// stated and which <c>StatusRegistry</c> already enforced within a kind
    /// and <c>AuraRules</c> already enforced across aura sources. What it did
    /// not survive was the trip through <c>GameEngine</c>, which summed the two
    /// channels' totals — so a cast slow and an enemy aura stacked, and every
    /// measurement taken before 2026-10-01 was taken with them stacking.
    ///
    /// <b>Bonuses add nothing to the argument and still do not stack either.</b>
    /// No shipped aura carries a positive speed delta and no status does since
    /// Hastened became flat cells, so the bonus channel is empty today. It is
    /// carried through anyway, resolved the same way, so the first positive
    /// speed source does not have to discover this rule for itself.
    /// </remarks>
    public readonly struct SpeedChange
    {
        public SpeedChange(double bonus, double penalty)
        {
            if (bonus < 0.0)
                throw new ArgumentOutOfRangeException(nameof(bonus), "A bonus is never negative; pass it as the penalty.");
            if (penalty > 0.0)
                throw new ArgumentOutOfRangeException(nameof(penalty), "A penalty is never positive; pass it as the bonus.");

            Bonus = bonus;
            Penalty = penalty;
        }

        /// <summary>No source reaching this operator.</summary>
        public static readonly SpeedChange None = new SpeedChange(0.0, 0.0);

        /// <summary>The strongest positive delta, or zero.</summary>
        public double Bonus { get; }

        /// <summary>The deepest negative delta, or zero.</summary>
        public double Penalty { get; }

        /// <summary>What the movement arithmetic adds to the base multiplier.</summary>
        public double Total => Bonus + Penalty;

        /// <summary>
        /// The two sides resolved independently: the better bonus and the worse
        /// penalty. Order-independent by construction, which is the property
        /// that makes it safe to fold any number of channels together.
        /// </summary>
        public static SpeedChange Strongest(SpeedChange a, SpeedChange b) =>
            new SpeedChange(
                a.Bonus > b.Bonus ? a.Bonus : b.Bonus,
                a.Penalty < b.Penalty ? a.Penalty : b.Penalty);

        /// <summary>A single delta of unknown sign, sorted into the right side.</summary>
        public static SpeedChange Of(double delta) =>
            delta >= 0.0 ? new SpeedChange(delta, 0.0) : new SpeedChange(0.0, delta);

        public override string ToString() =>
            Bonus > 0.0 && Penalty < 0.0 ? $"+{Bonus} {Penalty}"
            : Bonus > 0.0 ? $"+{Bonus}"
            : Penalty < 0.0 ? Penalty.ToString()
            : "no change";
    }
}
