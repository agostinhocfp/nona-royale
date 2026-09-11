// Assets/_Project/Scripts/Core/Model/DamageType.cs
namespace NonaRoyale.Core.Model
{
    /// <summary>
    /// The two damage types (COMBAT_SYSTEMS §2.2).
    /// </summary>
    /// <remarks>
    /// The one-sentence version, for the table: <b>Atomic can't be blocked, but
    /// it can't reach what it can't touch.</b> Atomic ignores every mitigation
    /// layer, but it does not bypass <i>targeting</i> — safe cells, home columns
    /// and stealth are reachability rules, not defences, and damage that cannot
    /// legally be aimed never enters the pipeline at all.
    /// </remarks>
    public enum DamageType
    {
        /// <summary>Subject to Evasion, Shield, and anything added later. Collision damage is Normal.</summary>
        Normal = 0,

        /// <summary>Ignores all mitigation. Bleed ticks and every part of Miracle Pull.</summary>
        Atomic = 1
    }
}