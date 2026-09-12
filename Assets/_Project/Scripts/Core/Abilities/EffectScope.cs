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
        EnemiesAroundPrimaryTarget = 3,

        /// <summary>
        /// Every enemy within the radius of the primary target, <b>including</b>
        /// that target. Mimi's Cryo-Pulse.
        /// </summary>
        /// <remarks>
        /// The distinction is whether the primary target is hit separately. A
        /// splash that follows a direct hit excludes it, or the target is struck
        /// twice by one ability. A field centred on a target it does not
        /// otherwise touch must include it, or the operator the player aimed at
        /// is the one enemy the ability misses.
        ///
        /// Worth a scope of its own rather than three duplicated effects, one
        /// aimed at the target and one at the ring around it: the same radius
        /// written twice drifts the first time somebody tunes one of them.
        /// </remarks>
        EnemiesAroundPrimaryTargetInclusive = 4
    }
}