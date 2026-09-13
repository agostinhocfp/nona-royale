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
    /// <b>Two of three abilities.</b> Cryo Field is deliberately absent: it
    /// needs a status that damages an area at its holder's upkeep, and no such
    /// mechanic exists (<c>OPERATORS.md</c>, In design). Shipping her partial
    /// beats shipping a stub that silently does nothing.
    ///
    /// <b>Her damage is Normal, and should be Tech.</b> The type does not exist
    /// yet and is blocked on shields having a real source. Until then Tech and
    /// Normal behave identically, so the substitution changes no outcome — but
    /// it does mean her whole identity as the anti-shield operator is currently
    /// unexpressed.
    ///
    /// <b>Reachable, and unmeasured.</b> Drafting made her fieldable, so a
    /// random-squad match or sweep can now execute both of these — neither of
    /// which has ever been simulated. Cryo-Pulse's remote origin and
    /// Translocation's cooldown are both reasoned values.
    /// </remarks>
    public static class Mimi
    {
        public const int MaxHealth = 5;

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
        /// this did more than either for two-thirds the cost, and 4 is off the
        /// 3/6/9 tier besides.
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
                    DamageType.Normal, EffectAudience.EnemyOnly, radius: 2),
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
        /// drip (§3.1) both 3 and 4 gate to roughly every turn, and 4 is off the
        /// 3/6/9 tier besides — the same objection that repriced Cryo-Pulse. A
        /// cooldown longer than the economy imposes is precisely what §3.1 says
        /// a stated cooldown is for.
        /// </remarks>
        public static AbilityDefinition Translocation { get; } = new AbilityDefinition(
            id: 402, name: "Translocation",
            description:
                "Trade places with anyone on the board, friend or enemy.",
            energyCost: 3, cooldownTurns: 4, range: 6,
            effects: new[] { AbilityEffect.Swap() });

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { CryoPulse, Translocation };

        /// <summary>Her uniform shape, for drafting. Two of three abilities.</summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Mimi",
            maxHealth: MaxHealth,
            baseSpeed: Speed,
            abilities: All);
    }
}