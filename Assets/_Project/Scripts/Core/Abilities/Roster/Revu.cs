// Assets/_Project/Scripts/Core/Abilities/Roster/Revu.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #11 — Revú, the loan shark. Stats and kit, transcribed from
    /// <c>COMBAT_SYSTEMS.md</c> §10.11 (added 2026-09-17), designed in
    /// <c>OPERATOR_DRAFTS.md</c> §3.
    /// </summary>
    /// <remarks>
    /// <b>The punishment web.</b> His ultimate scales with what the enemy has
    /// spent, his passive makes the cheap answers to him feed that ultimate,
    /// and his basic drains the pool further. Every door the enemy tries has a
    /// price on it. The honest answer is collisions: dice combat costs no
    /// energy and Equilibrium ignores it.
    ///
    /// <b>Bouncer's shape:</b> two actives, with the passive in the middle
    /// slot, so the ids run 1101 and 1102.
    ///
    /// <b>Every number is the draft's, unmeasured when written.</b> Adding an
    /// eleventh operator shifts the draft's dice stream again.
    /// </remarks>
    public static class Revu
    {
        /// <summary>
        /// Eight: 7 as built, +1 by the designer on 2026-09-17 after the bots
        /// sweep had him at 21%. The draft's "4 HP" reasoning predates the
        /// roster-wide +1 and the draft-health pass.
        /// </summary>
        public const int MaxHealth = 8;

        public const double Speed = 1.0;

        /// <summary>Energy destroyed by each Leech Round.</summary>
        public const int LeechDrain = 2;

        /// <summary>Sadist deals one damage per this much energy missing.</summary>
        public const int SadistEnergyPerDamage = 3;

        /// <summary>Enemies within this many steps of Sadist's target take the splash.</summary>
        public const int SadistSplashRadius = 2;

        /// <summary>The splash is the primary figure divided by this, rounded down.</summary>
        public const int SadistSplashDivisor = 2;

        /// <summary>
        /// A round that bleeds the target's side dry of more than blood: 2
        /// damage, and 2 energy gone from the enemy pool.
        /// </summary>
        /// <remarks>
        /// <b>From the Hip's price tag; the drain is the rider.</b> Cost 3,
        /// range 3. Built at 1 damage and cooldown 2; the designer raised it
        /// to 2 damage and cooldown 1 (2026-09-17), so it is a cast he can
        /// make every turn the pool allows. The energy is destroyed, not handed to Revú
        /// (designer, 2026-09-17); transfers are Ghost's territory.
        ///
        /// <b>It feeds Sadist twice over:</b> the drain empties the pool the
        /// ultimate reads, and an enemy that answers with a cheap cast pays
        /// double into Equilibrium for it.
        ///
        /// A drain aimed at a dry pool takes nothing, and says so.
        /// </remarks>
        public static AbilityDefinition LeechRound { get; } = new AbilityDefinition(
            id: 1101, name: "Leech Round",
            description:
                "A round with a hook in it. It wounds the target, and the target's side watches its reserves bleed away.",
            energyCost: 3, cooldownTurns: 1, range: 3,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 2, DamageType.Normal, EffectAudience.EnemyOnly),
                AbilityEffect.DrainEnergy(LeechDrain)
            });

        /// <summary>
        /// Collection day: one damage for every 3 energy the target's side is
        /// missing, and half that to enemies within 2 of it.
        /// </summary>
        /// <remarks>
        /// <b>The readability rule is the design.</b> "One damage for every 3
        /// energy missing from their pool" — an empty pool takes 4, a pool of 6
        /// takes 2, a full pool takes nothing. The enemy can compute it before
        /// deciding to spend.
        ///
        /// <b>One figure, from the target's seat</b> (designer, 2026-09-17).
        /// Splash victims take half of it, rounded down, whatever their own
        /// pools hold; a share of 0 is not dealt.
        ///
        /// <b>Cost 9, cooldown 4 (5 as built; the designer cut it on
        /// 2026-09-17), range 3, Normal.</b> Leech Round into Sadist is a
        /// two-turn combo: 2 damage and 2 energy gone, then up to 4 and 2
        /// splash. It reads as setup because the target's owner sees the pool
        /// fall and gets a turn to answer.
        ///
        /// <b>Mirror match:</b> Equilibrium halves a Sadist aimed at another
        /// Revú (cost 9), so the most it deals him is 2.
        /// </remarks>
        public static AbilityDefinition Sadist { get; } = new AbilityDefinition(
            id: 1102, name: "Sadist",
            description:
                "Collection day. The emptier the enemy's reserves, the harder it lands, and whoever stands near the debtor pays a share.",
            energyCost: 9, cooldownTurns: 4, range: 3,
            effects: new[]
            {
                AbilityEffect.MissingEnergyDamage(
                    SadistEnergyPerDamage, SadistSplashRadius, SadistSplashDivisor, DamageType.Normal)
            });

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { LeechRound, Sadist };

        /// <summary>
        /// His uniform shape, for drafting: two abilities and Equilibrium
        /// (§5.17), a named passive that fills his middle slot.
        /// </summary>
        /// <remarks>
        /// <b>Equilibrium:</b> damage an ability deals him the moment it is
        /// used is doubled when that ability costs 3 or less, halved (at least
        /// 1) when it costs 6 or more, and untouched at 4–5. Every damage type,
        /// Atomic included. Collisions, bleed, marks and anything that lands
        /// later — zones, beacons, charges, follow-ups, fields, watches — pass
        /// untouched (designer, 2026-09-17). An execute still kills.
        /// </remarks>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Revú",
            maxHealth: MaxHealth,
            baseSpeed: Speed,
            abilities: All,
            passive: StatusKind.Equilibrium,
            passiveName: "Equilibrium");
    }
}