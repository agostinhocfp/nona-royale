// Assets/_Project/Scripts/Core/Abilities/Mimi.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #4, transcribed from <c>OPERATORS.md</c>. Content, not logic —
    /// same contract as <see cref="AlphaRoster"/>.
    /// </summary>
    /// <remarks>
    /// <b>Her own file, not an addition to AlphaRoster.</b> The alpha three are
    /// a fixed set the harness and the MVP scene both build directly; Mimi is
    /// the first of the other six, and at nine operators one file per operator
    /// is the shape that stays readable. <see cref="All"/> exists so a future
    /// aggregate roster can pick her up without anyone editing this file.
    ///
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

        // ── Abilities ────────────────────────────────────────────────────

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
        /// </remarks>
        public static AbilityDefinition CryoPulse { get; } = new AbilityDefinition(
            id: 401, name: "Cryo-Pulse", energyCost: 6, cooldownTurns: 3, range: 3,
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
        /// Range 6 is double the longest range in the game, and that is the
        /// point — it is the only compensation a 5-health operator gets for
        /// being in a fight at all. She can open an exchange from outside
        /// everything else's reach, and leave one the same way.
        ///
        /// Because progress moves one-for-one with cells, the range also bounds
        /// the swing: a swap shifts either operator by at most 6 cells of
        /// journey, never the whole board. A swap that would carry either of
        /// them off its own track is refused before it is paid for
        /// (<c>AbilityResolver.TrySwapProgress</c>).
        ///
        /// Priced at 3, on tier: it deals no damage and applies no status.
        /// </remarks>
        public static AbilityDefinition Translocation { get; } = new AbilityDefinition(
            id: 402, name: "Translocation", energyCost: 3, cooldownTurns: 2, range: 6,
            effects: new[] { AbilityEffect.Swap() });

        // ── Lookups ──────────────────────────────────────────────────────

        public static IReadOnlyList<AbilityDefinition> Abilities { get; } =
            new[] { CryoPulse, Translocation };

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { CryoPulse, Translocation };
    }
}