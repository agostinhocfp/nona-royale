// Assets/_Project/Scripts/Core/Abilities/Roster/Syla.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #2 — Assassin. Stats and kit, transcribed from
    /// <c>COMBAT_SYSTEMS.md</c> §10.2.
    /// </summary>
    /// <remarks>
    /// <b>Her line is explicitly sequential.</b> Ace Shards applies bleed, bleed
    /// ticks Atomic, and From the Hip pays a bonus against a bleeding target.
    /// That is also her only route through Evasive Protocol, and it is a
    /// two-ability sequence rather than a single cast — which is the deliberate
    /// difference between her anti-evasion play and Bouncer's (§2.2).
    /// </remarks>
    public static class Syla
    {
        public const int MaxHealth = 6;

        /// <summary>The roster's fastest operator. ADR-0002 Amendment 4.</summary>
        public const double Speed = 1.5;

        /// <summary>
        /// Cheap, slows, and hits harder into a target that is already bleeding.
        /// </summary>
        /// <remarks>
        /// <b>A control tool with a damage rider, not a damage ability.</b> At 1
        /// base against 6 health it will not trade with anything on its own; the
        /// slow is the point, and the bleed bonus doubles the damage. It was 2
        /// base until the bleed profile proved strong enough that the base did
        /// not need to carry the ability.
        /// </remarks>
        public static AbilityDefinition FromTheHip { get; } = new AbilityDefinition(
            id: 201, name: "From the Hip",
            description:
                "A quick shot that leaves the target struggling to keep pace. It bites much deeper into someone already bleeding.",
            energyCost: 3, cooldownTurns: 1, range: 3,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 1, DamageType.Normal,
                    EffectAudience.EnemyOnly, bonusIfBleeding: 1),
                AbilityEffect.Status_(EffectScope.PrimaryTarget, StatusKind.Slow, duration: 1)
            });

        /// <summary>
        /// Self-origin area: everyone within three steps takes Normal damage and
        /// starts bleeding.
        /// </summary>
        /// <remarks>
        /// The bleed is Atomic when it ticks, which is what makes her chip
        /// damage cut through Kurbyn's evasion — indirectly, a turn later, and
        /// only if she set it up first.
        /// </remarks>
        public static AbilityDefinition AceShards { get; } = new AbilityDefinition(
            id: 202, name: "Ace Shards",
            description:
                "Scatters shrapnel around you, opening wounds on every enemy close enough to catch it.",
                       energyCost: 6, cooldownTurns: 3, range: 3,
            targeting: AbilityTargeting.None,

            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.EnemiesAroundCaster, 3, DamageType.Normal,
                    EffectAudience.EnemyOnly, radius: 3),
                AbilityEffect.Status_(EffectScope.EnemiesAroundCaster, StatusKind.Bleed,
                    duration: 1, radius: 3, stacks: 1)
            });

        /// <summary>
        /// Marks an enemy and cloaks Syla. The mark deals Atomic damage at the
        /// marked operator's upkeep every turn it is active (§5.7), and its
        /// payout is resolved in <c>NeutralizeRules</c> — a neutralize by the
        /// marker's side is a condition no ability can evaluate at cast time.
        /// </summary>
        /// <remarks>
        /// <b>Duration 2, and the mark's duration is the payout window.</b> The
        /// ability previously carried a separate "within 3 of Syla's turns"
        /// timer that duplicated it and counted against a different operator's
        /// turn index. At 2 turns and 2 damage a mark totals 4 — enough to leave
        /// a 6-health target inside every finisher on the roster, not enough to
        /// kill on its own and make the payout self-fulfilling.
        ///
        /// Two numbers here were walked back under measurement: the squad buff
        /// was +3 in the original roster, which produced a 35-cell turn, and the
        /// mark was pure bookkeeping until 2026-09-12 — a 9-energy ultimate that
        /// did nothing on the turn it was cast.
        /// </remarks>
        public static AbilityDefinition TaggedFromAbove { get; } = new AbilityDefinition(
            id: 203, name: "Tagged From Above",
            description:
                "Paints an enemy for the squad and slips you out of sight. If your side finishes them while the mark holds, everyone moves faster.",
            energyCost: 9, cooldownTurns: 2, range: 3,
            effects: new[]
            {
                AbilityEffect.Status_(EffectScope.PrimaryTarget, StatusKind.Mark, duration: 2),
                AbilityEffect.Status_(EffectScope.Caster, StatusKind.Stealth, duration: 2,
                    audience: EffectAudience.Any)
            });

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { FromTheHip, AceShards, TaggedFromAbove };

        /// <summary>Her uniform shape, for drafting. No aura, no passive.</summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Syla",
            maxHealth: MaxHealth,
            baseSpeed: Speed,
            abilities: All);
    }
}