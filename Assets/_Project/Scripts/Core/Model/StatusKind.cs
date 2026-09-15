// Assets/_Project/Scripts/Core/Model/StatusKind.cs
namespace NonaRoyale.Core.Model
{
    /// <summary>
    /// Every status the alpha roster can put on an operator (COMBAT_SYSTEMS §5).
    /// </summary>
    /// <remarks>
    /// Passives live here too. <see cref="Evasion"/> is not applied by an
    /// ability — Kurbyn simply has it — but modelling it as a permanent status
    /// means "passives stay live through stun" (§5.1) is one rule rather than a
    /// special case, and the per-round charge has somewhere to live.
    ///
    /// <b>Values are explicit and append-only.</b> Reordering these renumbers
    /// them, and anything that ever persists a status by ordinal — a save, a
    /// replay, a serialised test fixture — would silently read back a different
    /// effect. New kinds go on the end.
    /// </remarks>
    public enum StatusKind
    {
        /// <summary>Cannot move or spend energy on its next turn. Passives keep working.</summary>
        Stun = 0,

        /// <summary>Speed multiplier reduced. Sources do not stack; the largest applies.</summary>
        Slow = 1,

        /// <summary>Delayed Atomic damage, one stack at a time, at the holder's upkeep. Stacks add.</summary>
        Bleed = 2,

        /// <summary>Cannot be selected as a single target by an enemy. Everything else still reaches it.</summary>
        Stealth = 3,

        /// <summary>Permanent passive. The first Normal instance each round may be negated.</summary>
        Evasion = 4,

        /// <summary>Absorbs one whole instance of Normal damage, then expires.</summary>
        Shield = 5,

        /// <summary>
        /// Atomic damage at the holder's upkeep, every turn it is active, and it
        /// is not spent by ticking. Records who applied it, which is what Tagged
        /// From Above's payout reads (§5.7).
        /// </summary>
        Mark = 6,

        /// <summary>
        /// Speed multiplier increased. Granted to the whole squad by Tagged From
        /// Above's payout (§10.2). Carries its size in the entry's magnitude
        /// rather than in a config constant, so two sources of haste can differ.
        /// </summary>
        Hastened = 7,

        /// <summary>
        /// A marker with no gameplay effect of its own: a Zero-Day grenade is
        /// attached to this operator and detonates at its owner's next upkeep
        /// (§5.10). It exists so the attachment is visible on the board and,
        /// above all, so a cleanse has something to remove — stripping the
        /// marker cancels the detonation.
        /// </summary>
        /// <remarks>
        /// Magnitude is irrelevant and left at zero, which also keeps it out of
        /// the speed sum. Unlike bleed or a mark it is not the payload — the
        /// pending detonation lives in <c>DeferredOperatorEffects</c>, keyed on
        /// the operator, and this status is only its tell.
        /// </remarks>
        ZeroDayCharge = 8,

        /// <summary>
        /// Tech damage is blocked outright while this is active (§5.12).
        /// Luka's Hermes' Ring. Normal and Atomic damage are unaffected.
        /// </summary>
        /// <remarks>
        /// Checked before evasion and the shield, so a blocked Tech hit spends
        /// neither the round's evasion charge nor any of the pool. Magnitude is
        /// unused and left at zero, which keeps it out of the speed sum.
        /// </remarks>
        TechWard = 9,

        /// <summary>
        /// A marker with no gameplay effect of its own: Luka's follow-up strike
        /// is pending on this operator (§5.13). The <see cref="ZeroDayCharge"/>
        /// pattern exactly — the pending strike lives in
        /// <c>DeferredOperatorEffects</c>, this is its tell, and a cleanse that
        /// strips it cancels the strike.
        /// </summary>
        /// <remarks>
        /// A separate kind rather than a reused <see cref="ZeroDayCharge"/>, so
        /// a target carrying both keeps both: the registry holds one entry per
        /// kind, and a shared kind would let one detonation cancel the other.
        /// </remarks>
        Hunted = 10
    }
}