// Assets/_Project/Scripts/Core/Abilities/AlphaRoster.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// The three MVP operators, transcribed from <c>COMBAT_SYSTEMS.md</c> §10.
    /// </summary>
    /// <remarks>
    /// <b>This file is content, not logic.</b> Every ability is a list of
    /// effects the resolver already knows how to run — nothing here branches on
    /// which operator is casting. That is the whole payoff of going data-driven:
    /// operators four through nine are additions to this file, and a new
    /// operator that needs a new <i>mechanic</i> is a visible, deliberate event
    /// rather than a quiet special case.
    ///
    /// Passives are not abilities and are not listed here. Intimidating Presence
    /// is an aura evaluated when movement is calculated, and Evasive Protocol is
    /// a permanent status granted at match start — neither is ever "used".
    /// </remarks>
    public static class AlphaRoster
    {
        // Stats, per COMBAT_SYSTEMS §10 and ADR-0002 Amendment 2.
        public const int BouncerMaxHealth = 12;

        /// <summary>
        /// The squad's roadblock, and genuinely the slow one (ADR-0002
        /// Amendment 4). He briefly sat at 1.5 for pacing reasons, which
        /// contradicted his own design brief; opening deployments made that
        /// compromise unnecessary.
        /// </summary>
        public const double BouncerSpeed = 1.0;

        public const int SylaMaxHealth = 6;
        public const double SylaSpeed = 1.5;

        public const int KurbynMaxHealth = 6;
        public const double KurbynBaseSpeed = 1.0;

        /// <summary>Kurbyn's Evasive Protocol adds this on top of his base speed.</summary>
        public const double KurbynPassiveSpeedBonus = 0.5;

        /// <summary>Intimidating Presence: enemies within this many steps of Bouncer are slowed.</summary>
        public const int IntimidatingPresenceRadius = 2;

        /// <summary>
        /// Bouncer's passive aura. Not an ability — it is never used, has no
        /// cost and no duration; it is simply true while an enemy stands close
        /// enough (§10.1).
        /// </summary>
        public static AuraDefinition IntimidatingPresence { get; } =
            new AuraDefinition("Intimidating Presence", IntimidatingPresenceRadius, -0.5);

        // ── Bouncer, Tank ────────────────────────────────────────────────

        /// <summary>
        /// Pull the target adjacent, damaging it if it is an enemy. Usable on an
        /// ally purely to reposition, which is why the pull is audience-Any and
        /// the damage is enemy-only.
        /// </summary>
        public static AbilityDefinition VelvetRope { get; } = new AbilityDefinition(
            id: 101, name: "Velvet Rope", energyCost: 6, cooldownTurns: 2, range: 3,
            effects: new[]
            {
                AbilityEffect.Pull(EffectAudience.Any),
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 3, DamageType.Normal, EffectAudience.EnemyOnly)
            });

        /// <summary>
        /// Wounds the target and the Bouncer alike; on an ally it is a playful
        /// grapple that heals instead. The self-damage is aimed at the caster,
        /// which routes it around the pipeline entirely (§2.3) — it cannot be
        /// evaded or shielded and it can neutralize him.
        /// </summary>
        public static AbilityDefinition AllInMauling { get; } = new AbilityDefinition(
            id: 102, name: "All-In Mauling", energyCost: 6, cooldownTurns: 0, range: 1,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 3, DamageType.Normal, EffectAudience.EnemyOnly),
                AbilityEffect.Damage(EffectScope.Caster, 3, DamageType.Normal, EffectAudience.EnemyOnly),
                AbilityEffect.Heal(EffectScope.PrimaryTarget, 3, EffectAudience.AllyOnly)
            });

        // ── Syla, Assassin ───────────────────────────────────────────────

        /// <summary>Cheap, slows, and hits harder into a target that is already bleeding.</summary>
        public static AbilityDefinition FromTheHip { get; } = new AbilityDefinition(
            id: 201, name: "From the Hip", energyCost: 3, cooldownTurns: 1, range: 3,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 2, DamageType.Normal,
                    EffectAudience.EnemyOnly, bonusIfBleeding: 1),
                AbilityEffect.Status_(EffectScope.PrimaryTarget, StatusKind.Slow, duration: 1)
            });

        /// <summary>
        /// Self-origin area: everyone within three steps takes Normal damage and
        /// starts bleeding. The bleed is Atomic when it ticks, which is what
        /// makes Syla's chip damage cut through Kurbyn's evasion.
        /// </summary>
        public static AbilityDefinition AceShards { get; } = new AbilityDefinition(
            id: 202, name: "Ace Shards", energyCost: 6, cooldownTurns: 3, range: 3,
            requiresTarget: false,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.EnemiesAroundCaster, 3, DamageType.Normal,
                    EffectAudience.EnemyOnly, radius: 3),
                AbilityEffect.Status_(EffectScope.EnemiesAroundCaster, StatusKind.Bleed,
                    duration: 1, radius: 3, stacks: 1)
            });

        /// <summary>
        /// Marks an enemy and cloaks Syla. The mark's payout is resolved
        /// elsewhere — it fires on a neutralize by Syla's side, which is a
        /// condition no ability can evaluate at cast time.
        /// </summary>
        public static AbilityDefinition TaggedFromAbove { get; } = new AbilityDefinition(
            id: 203, name: "Tagged From Above", energyCost: 9, cooldownTurns: 2, range: 3,
            effects: new[]
            {
                AbilityEffect.Status_(EffectScope.PrimaryTarget, StatusKind.Mark, duration: 3),
                AbilityEffect.Status_(EffectScope.Caster, StatusKind.Stealth, duration: 2,
                    audience: EffectAudience.Any)
            });

        // ── Kurbyn, Brawler ──────────────────────────────────────────────

        /// <summary>Self-origin area that damages and stuns. Kurbyn's control tool.</summary>
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
        /// health at cast time, so a full-health 6-health target is not dropped to
        /// exactly half and then spared — which would read as a bug at the table.
        /// </summary>
        public static AbilityDefinition MiraclePull { get; } = new AbilityDefinition(
            id: 302, name: "Miracle Pull", energyCost: 9, cooldownTurns: 2, range: 1,
            effects: new[]
            {
                AbilityEffect.Execute(1, 2, fallbackAmount: 3, fallbackType: DamageType.Atomic),
                AbilityEffect.Damage(EffectScope.EnemiesAroundPrimaryTarget, 2, DamageType.Atomic,
                    EffectAudience.EnemyOnly, radius: 3)
            });

        // ── Lookups ──────────────────────────────────────────────────────

        public static IReadOnlyList<AbilityDefinition> BouncerAbilities { get; } =
            new[] { VelvetRope, AllInMauling };

        public static IReadOnlyList<AbilityDefinition> SylaAbilities { get; } =
            new[] { FromTheHip, AceShards, TaggedFromAbove };

        public static IReadOnlyList<AbilityDefinition> KurbynAbilities { get; } =
            new[] { DarginPulse, MiraclePull };

        public static IReadOnlyList<AbilityDefinition> All { get; } = new[]
        {
            VelvetRope, AllInMauling,
            FromTheHip, AceShards, TaggedFromAbove,
            DarginPulse, MiraclePull
        };
    }
}