// Assets/_Project/Scripts/Core/Abilities/Roster/Javi.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #5 — Support, and the first operator to fill the fourth base
    /// archetype. Content, not logic.
    /// </summary>
    /// <remarks>
    /// <b>He is the reason the shield layer will matter.</b> Shields are
    /// currently granted by a deferred board space, which could never generate
    /// enough uptime for a mitigation type to mean anything. A support who puts
    /// them up on demand is what makes the Normal/Force/Tech distinction real,
    /// and therefore what unblocks Mimi's identity as well as his own.
    ///
    /// <b>Two of three abilities.</b> Carapace is absent: a shield with a
    /// per-ability pool means reworking <c>IDamageMitigation.TryAbsorb</c> from
    /// a bool to a pool, which is the same interface and the same pipeline
    /// branch the deterministic-evasion work is rewriting. It lands with that
    /// mitigation pass, not before.
    ///
    /// <b>He may be the operator that tips a combat game into a race.</b>
    /// COMBAT_SYSTEMS §12 records that neutralizing rewards the attacker with
    /// nothing, and suspects this suppresses combat in human play in a way the
    /// harness cannot detect. A dedicated healer makes kills materially harder
    /// to land. His measured strength depends entirely on how that question is
    /// settled, so settle it before fielding him.
    /// </remarks>
    public static class Javi
    {
        public const int MaxHealth = 6;

        /// <summary>
        /// Every ability he has is range 3. A support who cannot reach the fight
        /// is a dead ability list, so he pays for his reach in fragility rather
        /// than in speed.
        /// </summary>
        public const double Speed = 1.5;

        /// <summary>
        /// Nanites seal breached suits and cauterize wounds. Turned on an enemy
        /// they do the opposite, and the squad standing around that enemy gets
        /// the runoff.
        /// </summary>
        /// <remarks>
        /// <b>Heal 2, not 3.</b> Collision is 3, so healing 2 never fully undoes
        /// a hit — he blunts damage rather than erasing it, and Bouncer's heal-3
        /// keeps a reason to exist. Cooldown 2 on a 3-cost ability is the first
        /// cooldown on the roster that binds tighter than the energy drip (§3.1),
        /// which is the only way a cheap ability gets limited at all.
        ///
        /// <b>The splash heal is declared <see cref="EffectAudience.EnemyOnly"/>
        /// despite healing allies.</b> Audience selects which <i>cast mode</i> an
        /// effect belongs to, not who receives it — the scope does that. This is
        /// the All-In Mauling pattern, where a self-damage effect is enemy-only
        /// so that the friendly cast costs nothing.
        ///
        /// The hostile mode is the interesting one: it pays a squad for standing
        /// next to an enemy, which is exactly where Ace Shards and Dargin Pulse
        /// punish them for standing.
        /// </remarks>
        public static AbilityDefinition NaniteInfusion { get; } = new AbilityDefinition(
            id: 501, name: "Nanite Infusion", energyCost: 3, cooldownTurns: 2, range: 3,
            effects: new[]
            {
                AbilityEffect.Heal(EffectScope.PrimaryTarget, 2, EffectAudience.AllyOnly),
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 2, DamageType.Normal,
                    EffectAudience.EnemyOnly),
                AbilityEffect.Heal(EffectScope.AlliesAroundPrimaryTarget, 1,
                    EffectAudience.EnemyOnly, radius: 2)
            });

        /// <summary>
        /// A cortical dampening field floods an ally's pain pathways with
        /// inhibitory signals, and everything riding those pathways goes with it.
        /// </summary>
        /// <remarks>
        /// <b>Redesigned from damage reduction, which duplicated Carapace and
        /// lost.</b> Armor 2 absorbs more than a 50% cut, at half the cost; and a
        /// percentage forces fractional health into a pipeline that has none —
        /// half of 3 is 1.5, and the rounding rule would decide more than the
        /// design did.
        ///
        /// A cleanse gives him something no other operator has and no overlap
        /// with his own shield. It is also a specific answer to two specific
        /// threats: Syla's mark, which bills every turn and pays her squad out
        /// when it kills, and Kurbyn's stun. Rock-paper-scissors rather than a
        /// second defensive slab.
        ///
        /// <b>A cleansed mark is gone, and its payout goes with it.</b> That is a
        /// rule, not an implementation detail — the mark carries the source
        /// Tagged From Above reads, and removing the status removes the source.
        /// </remarks>
        public static AbilityDefinition NeuralPurge { get; } = new AbilityDefinition(
            id: 503, name: "Neural Purge", energyCost: 6, cooldownTurns: 3, range: 3,
            effects: new[] { AbilityEffect.Cleanse() });

        // id 502 is reserved for Carapace, so his ids stay in cast order when it
        // lands rather than being renumbered around it.

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { NaniteInfusion, NeuralPurge };
    }
}