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
        /// <summary>
        /// Seven: 6 plus the roster-wide +1 of 2026-09-16 (COMBAT_SYSTEMS
        /// §1.1), which cut knockouts, and so match length, in the bots sweep.
        ///
        /// Raised by 1 again on 2026-09-25 (designer, to shorten matches; COMBAT_SYSTEMS §1.1): now 8.
        /// </summary>
        public const int MaxHealth = 8;

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
                       energyCost: 6, cooldownTurns: 3, range: 2,
            targeting: AbilityTargeting.None,

            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.EnemiesAroundCaster, 2, DamageType.Normal,
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
        /// 
        ///  ///
        /// <b>Cooldown 2 → 4 on 2026-09-13, after human play read the ability as
        /// too strong.</b> At 9 energy against a 3.5-per-turn drip (§3.1) it
        /// takes about 2.6 turns to afford, and a cooldown of 2 makes it ready on
        /// the 3rd — so the cooldown sat at roughly the same cadence as the
        /// economy and was barely doing anything. At 4 it is ready on the 5th
        /// turn, which puts the cooldown clearly in front of the drip. That is
        /// the lever §3.1 nominates: a stated cooldown only bites when it is
        /// longer than what the economy already imposes.
        ///
        /// <b>Frequency, and deliberately nothing else.</b> Two other axes were
        /// proposed and walked back before they landed, so that nobody re-tries
        /// them blind:
        ///
        /// <list type="bullet">
        /// <item><b>The mark's damage, 2 → 1.</b> It would have totalled 2 over
        /// the mark rather than 4, leaving a 6-health target at 4 — outside
        /// collision range, outside Miracle Pull's execute window, and outside a
        /// bleeding From the Hip. The mark would have stopped setting up the kill
        /// that its own payout requires, which breaks the design rather than
        /// pricing it. It is also the mark's global dial, not this ability's, so
        /// any future mark source would inherit a decision taken about Syla.</item>
        /// <item><b>The payout's haste, 2 turns → 1.</b> A status applied on its
        /// target's own turn takes hold immediately (§5), so a payout fired by
        /// the marker's own kill would expire with that turn's movement already
        /// spent — worth nothing — while one fired at an opponent's upkeep would
        /// work normally. The ability would pay out more for a kill it did not
        /// land than for one it did.</item>
        /// </list>
        ///
        /// <b>Reasoned from play, and now measurable.</b> The harness gained
        /// per-seat win counting on 2026-09-13; a per-operator table is the
        /// instrument this change should be judged against.
        public static AbilityDefinition TaggedFromAbove { get; } = new AbilityDefinition(
            id: 203, name: "Tagged From Above",
            description:
                "Paints an enemy for the squad and slips you out of sight. If your side finishes them while the mark holds, everyone gets a cell or two of extra movement each roll.",
            energyCost: 9, cooldownTurns: 4, range: 3,
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