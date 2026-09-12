// Assets/_Project/Scripts/Core/Abilities/EffectKind.cs
namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// The complete vocabulary an ability is built from. Deliberately small:
    /// every ability on the alpha roster is a short list of these, and the
    /// remaining six operators should be too.
    /// </summary>
    /// <remarks>
    /// When a new operator needs something this list cannot express, that is a
    /// signal to amend <c>COMBAT_SYSTEMS.md</c> and add a kind here — not to
    /// write a special case in one operator's stat block (§12, item 9).
    /// </remarks>
    public enum EffectKind
    {
        /// <summary>Damage through the pipeline, or straight to health when aimed at self.</summary>
        Damage = 0,

        /// <summary>Restore health, capped at maximum.</summary>
        Heal = 1,

        /// <summary>Apply a status for a duration.</summary>
        ApplyStatus = 2,

        /// <summary>Placement: move the target adjacent to the caster. Never collides (§7.4).</summary>
        PullToCaster = 3,

        /// <summary>
        /// Neutralize outright if the target is below a health fraction at cast
        /// time; otherwise deal the fallback damage instead.
        /// </summary>
        Execute = 4,

        /// <summary>
        /// Placement: caster and target exchange board cells. Never collides and
        /// triggers nothing, exactly as <see cref="PullToCaster"/> (§7.4).
        /// </summary>
        /// <remarks>
        /// Unlike every other kind, this one can be <i>illegal</i> for reasons
        /// targeting cannot see: a swap that would carry either operator off its
        /// own track is refused before the ability is paid for. See
        /// <c>AbilityResolver.TrySwapProgress</c>.
        /// </remarks>
        SwapWithCaster = 5
    }
}