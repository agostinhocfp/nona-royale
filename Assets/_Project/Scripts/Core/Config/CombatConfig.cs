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
            double slowSpeedPenalty = 0.5)
        {
            if (slowSpeedPenalty < 0) throw new ArgumentOutOfRangeException(nameof(slowSpeedPenalty));
            if (collisionDamage < 0) throw new ArgumentOutOfRangeException(nameof(collisionDamage));
            if (evasionChance < 0.0 || evasionChance > 1.0)
                throw new ArgumentOutOfRangeException(nameof(evasionChance), "A probability, so within [0,1].");
            if (bleedDamagePerStack < 0) throw new ArgumentOutOfRangeException(nameof(bleedDamagePerStack));

            CollisionDamage = collisionDamage;
            EvasionChance = evasionChance;
            BleedDamagePerStack = bleedDamagePerStack;
            SlowSpeedPenalty = slowSpeedPenalty;
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
        /// How much Slow takes off the speed multiplier. At 0.5 against a
        /// 1.5–2.0 band it costs a fast operator a quarter of its movement and a
        /// slow one a third — felt, not crippling. A full −1 would erase most of
        /// the band and turn an aura into hard control (COMBAT_SYSTEMS §5.2).
        /// </summary>
        public double SlowSpeedPenalty { get; }

        public static CombatConfig Default => new CombatConfig();
    }
}