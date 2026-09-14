// Assets/_Project/Scripts/Core/Services/DamageOutcome.cs
namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// How a damage instance resolved. Separate outcomes rather than a flag on
    /// one result, because the view has to play three visibly different things
    /// (COMBAT_SYSTEMS §9.3).
    /// </summary>
    public enum DamageOutcome
    {
        /// <summary>Negated by evasion. No health lost, the charge is spent.</summary>
        Evaded = 0,

        /// <summary>Absorbed whole by a shield, which is consumed.</summary>
        Absorbed = 1,

        /// <summary>Landed. Health reduced, target still standing.</summary>
        Dealt = 2,

        /// <summary>Landed and took the target to zero.</summary>
        Neutralized = 3
    }
}