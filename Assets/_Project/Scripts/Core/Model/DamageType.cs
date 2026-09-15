// Assets/_Project/Scripts/Core/Model/DamageType.cs
namespace NonaRoyale.Core.Model
{
    /// <summary>
    /// The three damage types (COMBAT_SYSTEMS §2.2).
    /// </summary>
    /// <remarks>
    /// The one-sentence version, for the table: <b>Atomic can't be blocked, but
    /// it can't reach what it can't touch.</b> Atomic ignores every mitigation
    /// layer, but it does not bypass <i>targeting</i> — safe cells, home columns
    /// and stealth are reachability rules, not defences, and damage that cannot
    /// legally be aimed never enters the pipeline at all.
    ///
    /// <b>Values are explicit and append-only</b>, for the reason
    /// <c>StatusKind</c> gives: anything persisting a type by ordinal would
    /// silently read back a different one after a reorder.
    /// </remarks>
    public enum DamageType
    {
        /// <summary>Subject to Evasion, Shield, and anything added later. Collision damage is Normal.</summary>
        Normal = 0,

        /// <summary>Ignores all mitigation. Bleed ticks and every part of Miracle Pull.</summary>
        Atomic = 1,

        /// <summary>
        /// Normal in every respect — evasion and shields apply to it — except
        /// that a tech ward (<see cref="StatusKind.TechWard"/>) blocks it
        /// outright. Mimi's Cryo-Pulse (§2.2, 2026-09-15).
        /// </summary>
        /// <remarks>
        /// <b>"Can be amplified by specific abilities" is the design's second
        /// half, and nothing amplifies it yet.</b> When the first amplifier
        /// arrives it is an amendment to §2.2 and a step in
        /// <c>DamagePipeline</c>, not a special case in an operator file.
        /// Until then Tech is Normal plus one counter.
        /// </remarks>
        Tech = 2
    }
}