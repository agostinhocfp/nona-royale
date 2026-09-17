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
    /// <b>He is the reason the shield layer matters.</b> Shields were granted
    /// only by a deferred board space, which could never generate enough uptime
    /// for a mitigation type to mean anything. A support who puts them up on
    /// demand is what makes the Normal/Force/Tech distinction real, and
    /// therefore what unblocks Mimi's identity as well as his own. <b>The Tech
    /// and Force types are still unbuilt</b> — this removed their blocker, not
    /// the work.
    ///
    /// <b>Complete as of 2026-09-13.</b> Trauma Plate landed with the shield
    /// half of the mitigation pass, which reworked
    /// <c>IDamageMitigation.TryAbsorb</c> into <c>AbsorbFrom</c> and turned a
    /// whole-instance absorb into a pool. The evasion half of that pass — a flat
    /// reduction replacing the roll — was <b>declined</b>; the rate moved to 0.3
    /// instead. Anyone reading <c>_HANDOFF_mitigation.md</c> should know only
    /// half of it was adopted.
    ///
    /// <b>Ranges raised on 2026-09-15</b> (<c>e85d710</c>): Nanite Infusion
    /// and Neural Purge 3 → 5, Trauma Plate 3 → 4. The kit was designed at 3
    /// across the board. Neural Purge now reaches one cell short of Mimi's
    /// Translocation, which COMBAT_SYSTEMS §10.4 treats as her only
    /// compensation for 5 health — the gap to watch if either moves again.
    ///
    /// <b>He may be the operator that tips a combat game into a race.</b> A
    /// dedicated healer with a castable shield makes kills materially harder to
    /// land, on a board whose stated priority is 70% combat. The kill bounty
    /// (§1.2) pushes the other way and landed at the same time. Neither
    /// direction has been measured, and no figure in §12 was taken with either
    /// of them in play — so his strength is an open question, not an unsettled
    /// design.
    /// </remarks>
    public static class Javi
    {
        /// <summary>
        /// Seven: 6 plus the roster-wide +1 of 2026-09-16 (COMBAT_SYSTEMS
        /// §1.1), which cut knockouts, and so match length, in the bots sweep.
        /// </summary>
        public const int MaxHealth = 7;

        /// <summary>
        /// His abilities reach 5, 4 and 5. A support who cannot reach the fight
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
            id: 501, name: "Nanite Infusion",
            description:
                "Nanites seal an ally's wounds. Turned on an enemy they do the opposite, and your squad standing near them catches the runoff.",
            energyCost: 3, cooldownTurns: 2, range: 5,
            // Opts in to self-cast (§10, 2026-09-17): his toolkit is defensive,
            // and a healer who cannot treat himself is half one.
            allowsSelfTarget: true,
            effects: new[]
            {
                AbilityEffect.Heal(EffectScope.PrimaryTarget, 2, EffectAudience.AllyOnly),
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 2, DamageType.Normal,
                    EffectAudience.EnemyOnly),
                AbilityEffect.Heal(EffectScope.AlliesAroundPrimaryTarget, 1,
                    EffectAudience.EnemyOnly, radius: 2)
            });

        /// <summary>
        /// A ballistic insert bolted onto an ally's plate carrier. It takes what
        /// comes, and then it fails.
        /// </summary>
        /// <remarks>
        /// <b>The name is the mechanic.</b> A ballistic insert absorbs a fixed
        /// amount and then stops working, which is exactly what a pool does.
        /// Nothing about it needs a number to be understood, which is the bar
        /// every ability description has to clear.
        ///
        /// <b>Pool 2, against a Normal spread of 1, 2, 2, 2, 3 and collision at
        /// 3.</b> It eats one small hit whole or takes the edge off a collision,
        /// never both. A 1-point pool was rejected: it cancels From the Hip
        /// outright, halves three of the four 2-damage abilities, and saves
        /// nobody from the collision that actually kills them — blunting
        /// everything that does not matter and nothing that does.
        ///
        /// <b>Cost walked 3 → 6 → 4.</b> The original sketch was 3, which was
        /// too cheap next to a 6-energy Velvet Rope. 6 was settled on 2026-09-12
        /// and then reconsidered: the same 6 buys Atomic damage that ignores
        /// every defence in the game, and 2 points of absorb is a poor rate
        /// against that. 4 is the compromise and is <b>reasoned, not
        /// measured</b> — revisit after the harness re-baseline.
        ///
        /// <b>Cooldown 3 against duration 2, deliberately.</b> The original
        /// cooldown 1 gave permanent uptime: he could hold plates on two
        /// operators forever and near-cover all three, which is not a shield at
        /// all but flat damage reduction on a squad. At 3 the plate is up for two
        /// of every four of the holder's turns, so choosing <i>when</i> is the
        /// whole skill of the ability.
        ///
        /// <b>Range 4, not the sketched 6.</b> Range 6 is Mimi's, and §10.4 makes
        /// it the sole compensation for her 5 health. Designed at 3; raised to 4
        /// in the 2026-09-15 pass, one less than his other two abilities.
        ///
        /// <b>Ally-only, so a hostile cast is refused and costs nothing.</b>
        /// Every effect scoped away by the cast mode returns
        /// <c>TargetingVerdict.WrongSide</c> before payment.
        ///
        /// <b>Neural Purge destroys it.</b> A cleanse is indiscriminate and
        /// strips the shield along with everything else, so casting his own two
        /// abilities in the wrong order on the same ally wastes one of them.
        /// That is a real cost, and the badge shows the player it coming.
        /// </remarks>
        public static AbilityDefinition TraumaPlate { get; } = new AbilityDefinition(
            id: 502, name: "Trauma Plate",
            description:
                "Bolts a ballistic insert onto an ally's carrier. It takes what comes until it is spent, then fails.",
            energyCost: 4, cooldownTurns: 3, range: 4,
            // Opts in to self-cast (§10, 2026-09-17): defensive toolkit — a
            // support plates himself when the fight comes to him.
            allowsSelfTarget: true,
            effects: new[]
            {
                AbilityEffect.Status_(
                    EffectScope.PrimaryTarget, StatusKind.Shield, duration: 2,
                    EffectAudience.AllyOnly, magnitude: 2)
            });

        /// <summary>
        /// A cortical dampening field floods an ally's pain pathways with
        /// inhibitory signals, and everything riding those pathways goes with it.
        /// </summary>
        /// <remarks>
        /// <b>Redesigned from damage reduction, which duplicated Trauma Plate
        /// and lost.</b> A flat pool absorbs more than a percentage cut and does
        /// it predictably; and a percentage forces fractional health into a
        /// pipeline that has none — half of 3 is 1.5, and the rounding rule would
        /// decide more than the design did.
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
        ///
        /// <b>It does not re-arm an evasion charge</b> (§5.8), and it
        /// <i>does</i> strip a friendly Trauma Plate, because
        /// <c>StatusRegistry.ClearApplied</c> is indiscriminate on purpose.
        /// </remarks>
        public static AbilityDefinition NeuralPurge { get; } = new AbilityDefinition(
            id: 503, name: "Neural Purge",
            description:
                "Floods an ally's nerves with inhibitory signals, washing out everything riding them.",
            energyCost: 6, cooldownTurns: 3, range: 5,
            // Opts in to self-cast (§10, 2026-09-17): defensive toolkit — he
            // washes his own stuns and marks like anyone else's.
            allowsSelfTarget: true,
            effects: new[] { AbilityEffect.Cleanse() });

        /// <remarks>Cast order, and id order — 501, 502, 503.</remarks>
        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { NaniteInfusion, TraumaPlate, NeuralPurge };

        /// <summary>
        /// His uniform shape, for drafting. No aura, no passive — three
        /// abilities, at ranges 5, 4 and 5, all pointed at keeping somebody else
        /// alive.
        /// </summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Javi",
            maxHealth: MaxHealth,
            baseSpeed: Speed,
            abilities: All);
    }
}