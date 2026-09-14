// Assets/_Project/Scripts/Core/Services/TargetingVerdict.cs
namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Why a target is or is not legal. A reason rather than a bare false,
    /// because the view has to tell a player <i>why</i> their click did nothing —
    /// "out of range" and "that one is stealthed" are different problems with
    /// different fixes.
    /// </summary>
    public enum TargetingVerdict
    {
        Legal = 0,

        /// <summary>The caster is in a home column, a yard, or already home — out of the fight (§4.3).</summary>
        CasterOutOfPlay = 1,

        /// <summary>The target is out of the fight for the same reasons.</summary>
        TargetOutOfPlay = 2,

        /// <summary>Too many steps along the track. Never a Euclidean distance (§4.1).</summary>
        OutOfRange = 3,

        /// <summary>Stealthed against this caster. Allies are unaffected (§5.4).</summary>
        Stealthed = 4,

        /// <summary>
        /// A swap would carry an operator behind its own start cell or into a
        /// home column (§7.4).
        /// </summary>
        /// <remarks>
        /// The odd one out: every verdict above is about whether the target can
        /// be <i>aimed at</i>, and this one is about where the effect would
        /// <i>put</i> someone. It lives here anyway because the view needs one
        /// place to look up why a click did nothing, and inventing a second
        /// vocabulary for the same job would be worse than the mild
        /// inconsistency.
        /// </remarks>
        SwapWouldLeaveTheTrack = 5,

        /// <summary>
        /// The target is on the wrong side for anything this ability does — a
        /// heal aimed at an enemy, or a cleanse aimed at one.
        /// </summary>
        /// <remarks>
        /// Without this, an ability whose every effect is audience-scoped away
        /// resolves successfully, does nothing, and still charges for it. Cast
        /// mode is decided once from who was targeted (§10), so an ability with
        /// no applicable effect in that mode was never a legal cast.
        /// </remarks>
        WrongSide = 6,

        /// <summary>
        /// The chosen cell is not on the shared outer track — a home column, a
        /// yard, or HOME (ADR-0006).
        /// </summary>
        /// <remarks>
        /// The cell-targeted twin of <see cref="TargetOutOfPlay"/>. §4.3 makes a
        /// home column unreachable in both directions, and a beacon painted
        /// inside one would be a way to strike into a place no ability may reach.
        /// </remarks>
        CellOutOfPlay = 7
    }
}