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
        Stealthed = 4
    }
}