// Assets/_Project/Scripts/Core/Abilities/Roster/Mimi.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #4 — Controller. Content, not logic — same contract as the
    /// alpha three.
    /// </summary>
    /// <remarks>
    /// <b>Complete since 2026-09-16.</b> Cryo Field was the last gap: it needed
    /// a status that damages an area at its holder's upkeep, which arrived as
    /// the field shape of <c>DeferredOperatorEffects</c> (§6.6) carrying a
    /// <see cref="StatusKind.CryoField"/> marker (§5.14).
    ///
    /// <b>Her direct damage is Tech; her field is not.</b> (2026-09-15, §2.2).
    /// The type arrived with Luka, whose Hermes' Ring blocks it. Cryo Field is
    /// a self-centred emission and stays Normal under the same rule, so a
    /// warded Luka is no longer immune to her — the field goes through the
    /// ward and his shield decides what it stops. Her old identity as the
    /// anti-shield operator is still unexpressed: "Tech can be amplified" is
    /// the designer's stated direction, and nothing amplifies it yet.
    ///
    /// <b>Reachable, and unmeasured.</b> Drafting made her fieldable, so a
    /// random-squad match or sweep can now execute all three of these — none
    /// of which has ever been simulated. Cryo-Pulse's remote origin,
    /// Translocation's cooldown and Cryo Field's tick are all reasoned values.
    /// </remarks>
    public static class Mimi
    {
        /// <summary>
        /// Six, still the lowest on the roster: 5 plus the roster-wide +1 of 2026-09-16 (COMBAT_SYSTEMS
        /// §1.1), which cut knockouts, and so match length, in the bots sweep.
        /// </summary>
        public const int MaxHealth = 6;

        /// <summary>
        /// The only operator below 6 health, which is what prices her kit.
        /// Concretely: Ace Shards into From the Hip kills her, and so does
        /// Dargin Pulse into a collision — two-ability sequences every other
        /// operator survives.
        /// </summary>
        public const double Speed = 1.0;

        /// <summary>
        /// An entropy field around a target foe: everything near it loses
        /// kinetic energy and heat at once, then cracks as it thaws.
        /// </summary>
        /// <remarks>
        /// Priced against its peers, not its flavour. Dargin Pulse is 6 for 2
        /// area damage plus Stun; Ace Shards is 6 for 3 plus Bleed. At 4 energy
        /// this did more than either for two-thirds the cost.
        ///
        /// <b>The 3/6/9 cost tier was a second reason to reprice it, and the
        /// tier is now abolished</b> (2026-09-13, <c>Roster</c>). The price does
        /// not move back: the peer comparison above was always the real
        /// argument, and the tier was a second reason stacked on a sufficient
        /// first one.
        ///
        /// The "shattering damage next round" is <see cref="StatusKind.Bleed"/>,
        /// not a second damage type — delayed damage at the target's upkeep at
        /// one point is exactly what bleed already is (§5.3).
        ///
        /// The scope is the <i>inclusive</i> area: the operator the player aimed
        /// at takes no separate hit, so excluding it the way Miracle Pull's
        /// splash does would make the chosen target the one enemy the field
        /// misses.
        ///
        /// <b>Unmeasured, and possibly stronger than Dargin Pulse.</b> Same
        /// cost, same radius, same damage, but it originates on a target three
        /// cells away rather than on the caster, and applies two statuses rather
        /// than one. For a 1.0-speed operator, remote origin is most of the
        /// game. Kept as written pending a sweep.
        /// </remarks>
        public static AbilityDefinition CryoPulse { get; } = new AbilityDefinition(
            id: 401, name: "Cryo-Pulse",
            description:
                "Freezes the ground around an enemy. Everything caught in it is wounded, slowed, and cracks open as it thaws.",
            energyCost: 6, cooldownTurns: 3, range: 3,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.EnemiesAroundPrimaryTargetInclusive, 2,
                    DamageType.Tech, EffectAudience.EnemyOnly, radius: 2),
                AbilityEffect.Status_(EffectScope.EnemiesAroundPrimaryTargetInclusive,
                    StatusKind.Bleed, duration: 1, radius: 2),
                AbilityEffect.Status_(EffectScope.EnemiesAroundPrimaryTargetInclusive,
                    StatusKind.Slow, duration: 1, radius: 2)
            });

        /// <summary>
        /// Tachyon-targeted spatial rearrangement: Mimi and one other operator,
        /// friend or foe, exchange coordinates instantly.
        /// </summary>
        /// <remarks>
        /// Range 6 is the longest in the game, and that is the point. It is the
        /// only compensation a 5-health operator gets for being in a fight at
        /// all: she can open an exchange from outside everything else's reach,
        /// and leave one the same way.
        ///
        /// Because progress moves one-for-one with cells, the range also bounds
        /// the swing: a swap shifts either operator by at most 6 cells of
        /// journey, never the whole board. A swap that would carry either of
        /// them off its own track is refused before it is paid for
        /// (<c>AbilityResolver.TrySwapProgress</c>).
        ///
        /// <b>Cooldown 4, and the cooldown is the whole limiter.</b> This is the
        /// cheapest denial tool in the design — swap with an operator four cells
        /// from its home mouth and it goes backwards while you take its cell,
        /// which is the play <c>_HANDOFF_opt_out_home_entry.md</c> prices at 3–6
        /// energy as an entire new mechanic.
        ///
        /// Raising the cost would not have limited it. Against a 3.5-per-turn
        /// drip (§3.1) both 3 and 4 gate to roughly every turn, so the cost is
        /// not the dial that binds here whatever it is set to. A cooldown longer
        /// than the economy imposes is precisely what §3.1 says a stated
        /// cooldown is for.
        ///
        /// The 3/6/9 tier was a second reason to stay at 3, and it is now
        /// abolished (<c>Roster</c>). The first reason stands, so the price does
        /// not move.
        /// </remarks>
        public static AbilityDefinition Translocation { get; } = new AbilityDefinition(
            id: 402, name: "Translocation",
            description:
                "Trade places with anyone on the board, friend or enemy.",
            energyCost: 3, cooldownTurns: 4, range: 6,
            effects: new[] { AbilityEffect.Swap() });

        /// <summary>
        /// A field Mimi raises around herself and carries: while it stands,
        /// everything hostile near her freezes a little more each time her
        /// turn comes round.
        /// </summary>
        /// <remarks>
        /// <b>1 Normal per tick, and the number is a placeholder.</b> The design
        /// table leaves the amount blank; 1 is the smallest instrument in the
        /// game, and the ability is a zoning tool, not a nuke. Tuning it is a
        /// one-line edit to <see cref="CryoFieldTickDamage"/>. Self-centred, so
        /// Normal rather than Tech (§2.2 — her own emission, not a guided
        /// device), which also means a warded Luka is no longer immune to her
        /// (§5.12).
        ///
        /// <b>Duration 3 is the designed "2 turns".</b> A self-applied status
        /// counts the cast turn as its first (§5), and the field only bills at
        /// upkeeps — the cast turn's upkeep has already passed. Duration 3 spans
        /// her cast turn and her next two, which is exactly two ticks.
        ///
        /// <b>It ends with her.</b> The field is anchored to her body, not
        /// deployed like a beacon: neutralize strips the marker with every other
        /// applied status (§1.2), and a field centred on an operator in her
        /// yard is centred nowhere. The follow-up precedent, not the beacon one
        /// (§6.5 vs ADR-0006).
        /// </remarks>
        public static AbilityDefinition CryoField { get; } = new AbilityDefinition(
            id: 403, name: "Cryo Field",
            description:
                "Mimi surrounds herself with a deepening cold. For a while, enemies near her are bitten by frost each time her turn begins.",
            energyCost: 6, cooldownTurns: 3, range: 0,
            targeting: AbilityTargeting.None,
            effects: new[]
            {
                AbilityEffect.Field(
                    CryoFieldTickDamage, CryoFieldRadius, CryoFieldDurationTurns,
                    DamageType.Normal)
            });

        /// <summary>Per-tick field damage. Designer tuning flag — the design table left it blank.</summary>
        public const int CryoFieldTickDamage = 1;

        /// <summary>The field's reach in track steps, each way. The design row's radius.</summary>
        public const int CryoFieldRadius = 2;

        /// <summary>
        /// The marker's span in her own turns. 3, not the design row's 2: a
        /// self-applied status counts the cast turn as its first, and the field
        /// ticks only at upkeeps, so 3 yields the designed two ticks (§5, §6.6).
        /// </summary>
        public const int CryoFieldDurationTurns = 3;

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { CryoPulse, CryoField, Translocation };

        /// <summary>Her uniform shape, for drafting. All three abilities.</summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Mimi",
            maxHealth: MaxHealth,
            baseSpeed: Speed,
            abilities: All);
    }
}