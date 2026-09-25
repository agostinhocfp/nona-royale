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
        /// Seven, the roster's common figure since 2026-09-17 (designer): 5 base, the roster-wide
        /// +1 of 2026-09-16 (COMBAT_SYSTEMS §1.1), and +1 more in the pass that also raised Cryo
        /// Field's tick and radius (§10.4). She was the sweep's last place at 6.
        ///
        /// Raised by 1 again on 2026-09-25 (designer, to shorten matches; COMBAT_SYSTEMS §1.1): now 8.
        /// </summary>
        public const int MaxHealth = 8;

        /// <summary>
        /// The tank's speed, so she cannot run from anything. Until 2026-09-17
        /// she was also the only operator below 7 health, which is what priced
        /// her kit: Ace Shards into From the Hip killed her, and so did Dargin
        /// Pulse into a collision — two-ability sequences every other operator
        /// survives.
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
        ///
        /// <b>Cost 4 since 2026-09-17 (designer; was 6).</b> The bots sweep had
        /// Mimi last at 16%, so the price came down rather than the payload
        /// going up. At 4 it sits with Sonic Disrupter and Inversion Matrix,
        /// Kian's area casts.
        /// </remarks>
        public static AbilityDefinition CryoPulse { get; } = new AbilityDefinition(
            id: 401, name: "Cryo-Pulse",
            description:
                "Freezes the ground around an enemy. Everything caught in it is wounded, slowed, and cracks open as it thaws.",
            energyCost: 4, cooldownTurns: 3, range: 3,
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
        /// <b>Range 9 since 2026-09-24 (designer; was 6).</b> The longest
        /// targeted reach in the game by four cells, and that is the point: it
        /// is her mobility. She has no haste on a board where four operators
        /// carry it, walked the fewest cells on the roster and was landed on
        /// the most, and the bots always spend this swapping with an enemy
        /// ahead of her, so one cast is her advance and their setback at once.
        /// Measured against 6 (4,000 paired matches, <c>MIMI_KURBYN_ANALYSIS.md</c>):
        /// 21.6% → 25.3%, 2.36 → 2.74 swaps a match, 4.7 → 6.5 cells each,
        /// home at the end 54% → 63%. Range 8 measured 23.8% and is the
        /// fallback if 9 feels harsh at the table; the cooldown is not the
        /// lever (3 measured 21.1%).
        ///
        /// Because progress moves one-for-one with cells, the range also bounds
        /// the swing: a swap shifts either operator by at most 9 cells of
        /// journey, never the whole board — up to 18 cells between two sides,
        /// which is the price to watch. A swap that would carry either of
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
            energyCost: 3, cooldownTurns: 4, range: TranslocationRange,
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
        ///
        /// <b>Range 0 means "on herself", not "reaches nothing".</b> The field
        /// is centred on Mimi and bills every enemy within
        /// <see cref="CryoFieldRadius"/> (2) of her, five cells in all. The
        /// draft card shows the range as "self".
        ///
        /// <b>Cost 4 since 2026-09-17 (designer; was 6).</b> Mimi was last in
        /// the bots sweep, and neither of her 6-cost casts was earning its
        /// price.
        /// </remarks>
        public static AbilityDefinition CryoField { get; } = new AbilityDefinition(
            id: 403, name: "Cryo Field",
            description:
                "Mimi surrounds herself with a deepening cold. For a while, enemies near her are bitten by frost each time her turn begins.",
            energyCost: 4, cooldownTurns: 3, range: 0,
            targeting: AbilityTargeting.None,
            effects: new[]
            {
                AbilityEffect.Field(
                    CryoFieldTickDamage, CryoFieldRadius, CryoFieldDurationTurns,
                    DamageType.Normal)
            });

        /// <summary>Translocation's reach in track steps. 6 until 2026-09-24; see its remarks.</summary>
        public const int TranslocationRange = 9;

        /// <summary>Per-tick field damage. Designer tuning flag — the design table left it blank.</summary>
        public const int CryoFieldTickDamage = 2;

        /// <summary>The field's reach in track steps, each way. The design row's radius.</summary>
        public const int CryoFieldRadius = 3;

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