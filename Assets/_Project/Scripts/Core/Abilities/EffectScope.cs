// Assets/_Project/Scripts/Core/Abilities/EffectScope.cs
namespace NonaRoyale.Core.Abilities
{
    /// <summary>Who an effect lands on.</summary>
    public enum EffectScope
    {
        /// <summary>The one operator the player picked.</summary>
        PrimaryTarget = 0,

        /// <summary>The caster.</summary>
        Caster = 1,

        /// <summary>Every enemy within the effect's radius of the caster. Ace Shards, Dargin Pulse.</summary>
        EnemiesAroundCaster = 2,

        /// <summary>
        /// Every enemy within the radius of the primary target, excluding that
        /// target — it has already taken the direct hit. Miracle Pull's splash.
        /// </summary>
        EnemiesAroundPrimaryTarget = 3
    }
}