// Assets/_Project/Scripts/Core/Config/CombatConfig.cs
using System;

namespace NonaRoyale.Core.Config
{
    /// <summary>
    /// Combat dials, as named config (CONVENTIONS: "Config, not literals").
    /// ADR-0002 Amendment 2 ranks these by how much each moves match length, so
    /// they are reached for in a known order rather than tuned at random.
    /// </summary>
    public sealed class CombatConfig
    {
        public CombatConfig(
            int collisionDamage = 3,
            double evasionChance = 0.5,
            int bleedDamagePerStack = 1,
            double slowSpeedPenalty = 0.5,
            int markDamagePerTurn = 2,
            double hasteSpeedBonus = 0.5,
            int hasteDurationTurns = 2)
        {
            if (slowSpeedPenalty < 0) throw new ArgumentOutOfRangeException(nameof(slowSpeedPenalty));
            if (collisionDamage < 0) throw new ArgumentOutOfRangeException(nameof(collisionDamage));
            if (evasionChance < 0.0 || evasionChance > 1.0)
                throw new ArgumentOutOfRangeException(nameof(evasionChance), "A probability, so within [0,1].");
            if (bleedDamagePerStack < 0) throw new ArgumentOutOfRangeException(nameof(bleedDamagePerStack));
            if (markDamagePerTurn < 0) throw new ArgumentOutOfRangeException(nameof(markDamagePerTurn));
            if (hasteSpeedBonus < 0) throw new ArgumentOutOfRangeException(nameof(hasteSpeedBonus));
            if (hasteDurationTurns < 1) throw new ArgumentOutOfRangeException(nameof(hasteDurationTurns));

            CollisionDamage = collisionDamage;
            EvasionChance = evasionChance;
            BleedDamagePerStack = bleedDamagePerStack;
            SlowSpeedPenalty = slowSpeedPenalty;
            MarkDamagePerTurn = markDamagePerTurn;
            HasteSpeedBonus = hasteSpeedBonus;
            HasteDurationTurns = hasteDurationTurns;
        }

        /// <summary>
        /// Damage a mover deals by landing on an enemy. At 3, nothing on the
        /// alpha roster dies to a single collision — deliberately. Collision is
        /// a <i>softening</i> mechanic that sets up ability kills, which is the
        /// 70/30 combat-over-race priority expressed as a number
        /// (COMBAT_SYSTEMS §7.3). First dial if the race layer reads as toothless.
        /// </summary>
        public int CollisionDamage { get; }

        /// <summary>Chance the first Normal instance each round is negated (COMBAT_SYSTEMS §5.5).</summary>
        public double EvasionChance { get; }

        public int BleedDamagePerStack { get; }

        /// <summary>
        /// How much Slow takes off the speed multiplier. Against the 1.0–1.5
        /// band adopted in ADR-0002 Amendment 4, 0.5 costs a fast operator a
        /// third of its movement and takes a slow one to
        /// <c>MinSpeedMultiplier</c> — sharper than it was under the original
        /// 1.5–2.0 band, and not re-measured since the band moved
        /// (COMBAT_SYSTEMS §5.2, §12 item 8).
        /// </summary>
        public double SlowSpeedPenalty { get; }

        /// <summary>
        /// Atomic damage a Mark deals at the marked operator's upkeep, every
        /// turn it is active (COMBAT_SYSTEMS §5.7).
        /// </summary>
        /// <remarks>
        /// Deliberately sub-lethal against the 6-HP operators: over a 2-turn
        /// mark it totals 4, leaving them at 2 and inside collision, execute and
        /// From the Hip range. The mark's job is to <i>hand</i> the kill to the
        /// marker's squad, which is the condition that pays out Tagged From
        /// Above. A mark that kills on its own makes that payout
        /// self-fulfilling. Set by reasoning, not simulation — re-run the
        /// harness before trusting it.
        /// </remarks>
        public int MarkDamagePerTurn { get; }

        /// <summary>
        /// Speed added by <c>StatusKind.Hastened</c>, the default magnitude for
        /// Tagged From Above's squad payout (COMBAT_SYSTEMS §10.2).
        /// </summary>
        /// <remarks>
        /// At 0.5 against the 1.0–1.5 band a hasted squad moves at 1.5–2.0 for
        /// the duration: a real tempo swing a player can still follow on the
        /// board. The payout was +3 in the original roster, which produced a
        /// 35-cell turn — three-quarters of the loop from one ability.
        /// </remarks>
        public double HasteSpeedBonus { get; }

        /// <summary>
        /// How many of the squad's own turns the payout's haste lasts.
        /// </summary>
        /// <remarks>
        /// 2, not 1. The payout can fire on the marker's own turn — a collision
        /// or ability kill — by which point that turn's movement is usually
        /// already spent, so a 1-turn buff would routinely be worth nothing. At
        /// 2 it covers the remainder of the current turn and the whole of the
        /// next, whether it fired on the marker's turn or on an opponent's
        /// upkeep. This is what COMBAT_SYSTEMS §10.2's "one round" resolves to.
        /// </remarks>
        public int HasteDurationTurns { get; }

        public static CombatConfig Default => new CombatConfig();
    }
}