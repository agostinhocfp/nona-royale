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
        Hunted = 10,

        /// <summary>
        /// A self-anchored damage field: while it is active, enemies near the
        /// holder take damage at each of the holder's owner-upkeeps (§5.14).
        /// Mimi's Cryo Field.
        /// </summary>
        /// <remarks>
        /// <b>The status is the field.</b> The ticking payload lives in
        /// <c>DeferredOperatorEffects</c> — the board-reading half — and this
        /// entry is its visible, cleanseable tell, the <see cref="ZeroDayCharge"/>
        /// pattern again: stripping the status cancels the field, and neutralize
        /// strips it with everything else (§1.2), so the field ends when the
        /// holder does. Unlike a charge it is centred on the caster herself and
        /// repeats every upkeep rather than resolving once.
        /// </remarks>
        CryoField = 11,

        /// <summary>
        /// A marker with no gameplay effect of its own: a watch is pending on
        /// this operator, and if it moves by dice before its owner's next
        /// upkeep the watch trips and strikes it, once (§5.15, §6.7).
        /// Kurbyn's Predator's Read.
        /// </summary>
        /// <remarks>
        /// The <see cref="ZeroDayCharge"/> pattern a third time — the pending
        /// strike lives in <c>DeferredOperatorEffects</c>, this is its tell,
        /// and a cleanse that strips it cancels the watch.
        ///
        /// A separate kind rather than a reused <see cref="Hunted"/>, for
        /// <see cref="Hunted"/>'s own reason: the registry holds one entry per
        /// kind, and a target carrying both a follow-up and a watch (Luka and
        /// Kurbyn can share a squad) must keep both — one resolution consuming
        /// a shared marker would read the other as cleansed. The fiction is
        /// near-identical and the trigger is the opposite: Hunted punishes
        /// staying, Watched punishes moving.
        /// </remarks>
        Watched = 12,

        /// <summary>
        /// The mirror of <see cref="Hastened"/>: the holder's first move from
        /// each roll is 1 cell shorter when the roll totals 6 or less, 2
        /// shorter above, and never shorter than 1 cell (§5.16). Sanity's
        /// passive.
        /// </summary>
        /// <remarks>
        /// <b>Not speed.</b> Like haste it is flat cells applied after the
        /// speed formula, so it stays countable and skips the speed channel.
        /// A holder that is also hastened collects both, and they cancel.
        /// </remarks>
        Burdened = 13,

        /// <summary>
        /// Damage an ability deals the holder at the moment it is used is
        /// scaled by that ability's cost: 3 or less doubles it, 6 or more
        /// halves it (at least 1), 4–5 leaves it alone (§5.17). Revú's
        /// passive.
        /// </summary>
        /// <remarks>
        /// <b>A price rule, not armour.</b> It applies to every damage type,
        /// Atomic included, and before every mitigation layer. Anything that
        /// lands later — zones, beacons, charges, follow-ups, fields, watches —
        /// and collisions, bleed and marks are not casts and pass untouched.
        /// </remarks>
        Equilibrium = 14
    }
}