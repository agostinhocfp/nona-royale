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
            int bleedDamagePerStack = 1)
        {
            if (collisionDamage < 0) throw new ArgumentOutOfRangeException(nameof(collisionDamage));
            if (evasionChance < 0.0 || evasionChance > 1.0)
                throw new ArgumentOutOfRangeException(nameof(evasionChance), "A probability, so within [0,1].");
            if (bleedDamagePerStack < 0) throw new ArgumentOutOfRangeException(nameof(bleedDamagePerStack));

            CollisionDamage = collisionDamage;
            EvasionChance = evasionChance;
            BleedDamagePerStack = bleedDamagePerStack;
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

        public static CombatConfig Default => new CombatConfig();
    }
}