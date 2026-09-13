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
        public const int MaxHealth = 6;

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
            id: 301, name: "Dargin Pulse", energyCost: 6, cooldownTurns: 3, range: 2,
            requiresTarget: false,
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
        public static AbilityDefinition MiraclePull { get; } = new AbilityDefinition(
            id: 302, name: "Miracle Pull", energyCost: 9, cooldownTurns: 2, range: 1,
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