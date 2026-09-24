// Assets/_Project/Scripts/Core/Abilities/Roster/Kurbyn.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #3 — Brawler. Stats and kit, transcribed from
    /// <c>COMBAT_SYSTEMS.md</c> §10.3.
    /// </summary>
    /// <remarks>
    /// <b>Evasion made him dominant in every session, human and simulated, and
    /// every tuning pass that left it in place failed to move him.</b> It was
    /// cut 50% → 30% (2026-09-15), countered with Atomic (Velvet Rope), and
    /// outlasted the per-turn speed cap (§6.3) — then the designer read the
    /// roster table and saw what the kit had become: three actives plus a
    /// permanent fourth ability's worth of defence. Evasive Protocol was meant
    /// to be a single ability.
    ///
    /// <b>The 2026-09-17 rebuild (designer).</b> Predator's Read removed (the
    /// kit is two actives and the passive again); evasion cut to 12%; the +0.5
    /// speed bonus traded for permanent flat haste — +1 cell on a roll of 6 or
    /// less, +2 above, capped at 2 cells per turn where the roster's haste cap
    /// is 3 (§5.9). The passive is now two statuses, one fiction.
    ///
    /// <b>Evasion removed (2026-09-24, designer).</b> The analysis in
    /// <c>MIMI_KURBYN_ANALYSIS.md</c> found it dodged 0.35 hits a match yet
    /// cost 2.5 points to remove: the bots priced the 12% into every hit on
    /// him and aimed elsewhere, so he took the fewest hits on the roster. A
    /// coin flip nobody could see or plan around, stacked on haste into "can't
    /// catch him, can't hit him". Uncapping the 12% changed nothing (28.8%),
    /// and a guaranteed first-hit miss was far worse (33.6% each round, 31.6%
    /// once per life). Without it: 28.9% → 26.4%, deaths 0.69 → 1.00. He keeps
    /// the name and the haste. The evasion machinery (§5.5,
    /// <c>StatusKind.Evasion</c>, <c>CombatConfig.EvasionChance</c>) stays in
    /// the core, dormant, as Predator's Read's watch did.
    /// </remarks>
    public static class Kurbyn
    {
        /// <summary>
        /// Seven: 6 plus the roster-wide +1 of 2026-09-16 (COMBAT_SYSTEMS
        /// §1.1), which cut knockouts, and so match length, in the bots sweep.
        /// </summary>
        public const int MaxHealth = 7;

        /// <summary>
        /// 1.0 — plain, since 2026-09-17. Until then his speed was 1.0 plus
        /// Evasive Protocol's +0.5, which made his mobility conditional on the
        /// passive being live in a way no other operator's is — a Kurbyn moving
        /// at 1.0 was a bug, not a balance state (ADR-0002 Amendment 5). His
        /// traversal edge is now flat haste cells on the passive, capped at 2 a
        /// turn, so the speed channel no longer carries him at all.
        /// </summary>
        public const double BaseSpeed = 1.0;

        /// <summary>Self-origin area that damages and stuns. His control tool.</summary>
        public static AbilityDefinition DarginPulse { get; } = new AbilityDefinition(
            id: 301, name: "Dargin Pulse",
            description:
                "A burst that scrambles motor function, leaving every enemy nearby unable to act.",
                       energyCost: 6, cooldownTurns: 3, range: 2,
            targeting: AbilityTargeting.None,

            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.EnemiesAroundCaster, 2, DamageType.Normal,
                    EffectAudience.EnemyOnly, radius: 2),
                AbilityEffect.Status_(EffectScope.EnemiesAroundCaster, StatusKind.Stun,
                    duration: 1, radius: 2)
            });

        /// <summary>
        /// Execute first, then splash. The order is the rule: the threshold reads
        /// health at cast time, so a full-health 6-health target is not dropped
        /// to exactly half and then spared — which would read as a bug at the
        /// table.
        /// </summary>
        /// <remarks>
        /// <b>Range 2, not 1.</b> At range 1 the finisher needed Kurbyn standing
        /// on the cell beside his target, which on a board where placement is
        /// mostly dice meant the ult was often unspendable at the moment it was
        /// worth spending. At 2 he can close from a roll rather than needing the
        /// roll to land exactly.
        ///
        /// It also widens the splash's practical reach without touching its
        /// radius, since the origin is the primary target and he can now pick a
        /// target one cell further out.
        ///
        /// Note this moves the same direction as everything else in the same
        /// pass: Bouncer lost health, reach and damage, and the operator Bouncer
        /// exists to counter gained reach on his ultimate. Whether that is one
        /// correction or an overcorrection is a measurement, not an argument.
        /// </remarks>
        public static AbilityDefinition MiraclePull { get; } = new AbilityDefinition(
            id: 302, name: "Miracle Pull",
            description:
                "A gravitic tether no defence can stop, collapsing in on everything around the target. An enemy already wounded is finished outright.",
            energyCost: 9, cooldownTurns: 3, range: 2,
            effects: new[]
            {
                AbilityEffect.Execute(1, 2, fallbackAmount: 3, fallbackType: DamageType.Atomic),
                AbilityEffect.Damage(EffectScope.EnemiesAroundPrimaryTarget, 2, DamageType.Atomic,
                    EffectAudience.EnemyOnly, radius: 3)
            });

        // Predator's Read (id 303) was removed on 2026-09-17: the kit is two
        // actives and the passive again, as designed. The watch machinery it
        // introduced (§5.15, §6.7, EffectKind.Watch) stays in the core, dormant,
        // for the next operator who wants it.

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { DarginPulse, MiraclePull };

        /// <summary>
        /// His uniform shape, for drafting. Evasive Protocol is permanent
        /// Hastened — flat cells, capped at 2 a turn rather than the roster's 3
        /// (§5.9). No magnitude: haste is not speed. Until 2026-09-24 it also
        /// carried Evasion (12%, §5.5) as a second status.
        /// </summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Kurbyn",
            maxHealth: MaxHealth,
            baseSpeed: BaseSpeed,
            abilities: All,
            passive: StatusKind.Hastened,
            passiveName: "Evasive Protocol",
            hasteCellCap: 2,
            passiveDescription:
                "By the time the shot is aimed, he is already a step further along than he should be.");
    }
}