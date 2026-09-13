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
        EnemiesAroundPrimaryTargetInclusive = 4,

        /// <summary>
        /// Every ally of the caster within the radius of the primary target,
        /// including the caster if it stands close enough. Javi's Nanite
        /// Infusion.
        /// </summary>
        /// <remarks>
        /// The first scope that looks for friends around an <i>enemy</i>, and
        /// the reason it exists is the tension it creates: it pays a squad for
        /// standing where Ace Shards and Dargin Pulse punish them for standing.
        /// </remarks>
        AlliesAroundPrimaryTarget = 5,

        /// <summary>
        /// Every enemy on the next <c>Radius</c> cells <b>ahead</b> of the
        /// caster along the loop, in the caster's own direction of travel. The
        /// caster's cell is not included. Kian's Inversion Matrix.
        /// </summary>
        /// <remarks>
        /// <b>The first directional scope.</b> Every other one is symmetric —
        /// "within N" covers N steps each way — and a symmetric line is simply a
        /// wider version of <see cref="EnemiesAroundCaster"/>. What makes this a
        /// line rather than an area is that it points somewhere, and pointing it
        /// costs the caster a decision the other area abilities do not ask for.
        ///
        /// <b>It reuses <c>Radius</c> to carry the line's length.</b> The name
        /// is wrong for a one-directional shape, but adding a field to
        /// <c>AbilityEffect</c> touches its private constructor and every
        /// factory on it. Same trade as the shield pool living in a status
        /// entry's magnitude.
        ///
        /// <b>It is pure circuit geometry and wraps.</b> The cells are computed
        /// from the caster's track index, not from its progress, so a caster
        /// near the end of its own lap still projects a full-length line onto
        /// the shared loop rather than running out of board.
        /// </remarks>
        EnemiesInLineFromCaster = 6
    }
}