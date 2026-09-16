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
    /// <b>Evasion made him dominant in the first human sessions</b>, and the
    /// response was a targeted counter — Velvet Rope becoming Atomic — rather
    /// than touching the passive. Nothing here changed; what changed is that one
    /// operator can now reliably go through him (§2.2, §10.1).
    /// </remarks>
    public static class Kurbyn
    {
        /// <summary>
        /// Seven: 6 plus the roster-wide +1 of 2026-09-16 (COMBAT_SYSTEMS
        /// §1.1), which cut knockouts, and so match length, in the bots sweep.
        /// </summary>
        public const int MaxHealth = 7;

        /// <summary>Before Evasive Protocol, which adds its bonus on top.</summary>
        public const double BaseSpeed = 1.0;

        /// <summary>
        /// Evasive Protocol's speed bonus, on top of <see cref="BaseSpeed"/>.
        /// </summary>
        /// <remarks>
        /// Carried on <see cref="Definition"/> as the passive's magnitude, and
        /// handed to <c>StatusRegistry.ApplyPassive</c> at composition. That is
        /// what makes it reach the engine at all: for a long time this constant
        /// was read by nothing but a test that added it to the base speed, and
        /// every simulated match ran him at 1.0 (ADR-0002 Amendment 5).
        ///
        /// It also makes his mobility <b>conditional on the passive being
        /// live</b> in a way no other operator's is. A Kurbyn moving at 1.0 is a
        /// bug, not a balance state.
        /// </remarks>
        public const double PassiveSpeedBonus = 0.5;

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
            energyCost: 9, cooldownTurns: 2, range: 2,
            effects: new[]
            {
                AbilityEffect.Execute(1, 2, fallbackAmount: 3, fallbackType: DamageType.Atomic),
                AbilityEffect.Damage(EffectScope.EnemiesAroundPrimaryTarget, 2, DamageType.Atomic,
                    EffectAudience.EnemyOnly, radius: 3)
            });

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { DarginPulse, MiraclePull };

        /// <summary>
        /// His uniform shape, for drafting. The passive carries its speed bonus
        /// as a magnitude — the only route by which it reaches the engine.
        /// </summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Kurbyn",
            maxHealth: MaxHealth,
            baseSpeed: BaseSpeed,
            abilities: All,
            passive: StatusKind.Evasion,
            passiveMagnitude: PassiveSpeedBonus);
    }
}