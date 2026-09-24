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
    /// <b>The punishment web, reworked around debt (2026-09-24).</b> Leech
    /// Round puts the target's seat in debt; the seat pays from its pool when it
    /// ends its turn, or carries the remainder with interest. Sadist calls the
    /// debt in as damage. His passive makes the cheap answers to him the
    /// dangerous ones, and the one free answer is to land on him with the dice,
    /// which also burns what you owe him (§3.3).
    ///
    /// <b>Bouncer's shape:</b> two actives, with the passive in the middle
    /// slot, so the ids run 1101 and 1102.
    ///
    /// <b>Debt lives on the seat, not on a piece</b> (designer): one figure
    /// beside the pool, and no marker on the board.
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

        /// <summary>Debt each Leech Round adds to the target's seat.</summary>
        public const int LeechDebt = 2;

        /// <summary>Enemies within this many steps of Sadist's target take the splash.</summary>
        public const int SadistSplashRadius = 2;

        /// <summary>The splash is the primary figure divided by this, rounded down.</summary>
        public const int SadistSplashDivisor = 2;

        /// <summary>
        /// The least Sadist computes against the primary target, whatever its
        /// seat owes (2026-09-21, designer; kept through the debt rework).
        /// </summary>
        /// <remarks>
        /// Without it a seat that owes nothing would take nothing, and the
        /// roster's dearest cast would be a blank. It is the smallest debt one
        /// Leech Round leaves, so it never makes calling early better than
        /// letting the loan run.
        /// </remarks>
        public const int SadistMinimumDamage = 2;

        /// <summary>
        /// A round with a loan attached: 2 damage, and the target's side owes
        /// 2 more.
        /// </summary>
        /// <remarks>
        /// <b>From the Hip's price tag; the debt is the rider.</b> Cost 3,
        /// cooldown 1, range 3 (designer, 2026-09-17), so it is a cast he can
        /// make every turn the pool allows.
        ///
        /// <b>Nothing is taken now</b> (2026-09-24). The seat pays when it ends
        /// its own turn, so it chooses between keeping energy back to settle and
        /// spending it and letting the debt grow. Paid debt is destroyed, not
        /// handed to Revú (designer).
        ///
        /// A seat already at the cap owes no more, and the event says so.
        /// </remarks>
        public static AbilityDefinition LeechRound { get; } = new AbilityDefinition(
            id: 1101, name: "Leech Round",
            description:
                "A round with a loan attached. It wounds the target, and the target's side owes the house before its turn is out.",
            energyCost: 3, cooldownTurns: 1, range: 3,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 2, DamageType.Normal, EffectAudience.EnemyOnly),
                AbilityEffect.IncurDebt(LeechDebt)
            });

        /// <summary>
        /// Collection day: the target's side takes its whole debt as damage,
        /// and half that lands on enemies within 2 of it.
        /// </summary>
        /// <remarks>
        /// <b>The readability rule is the design.</b> The figure is the debt the
        /// target's seat already owes, at least 2 — the debtor has been looking
        /// at the hit it risks since the loan was made.
        ///
        /// <b>One figure, from the target's seat</b> (designer, 2026-09-17).
        /// Splash victims take half of it, rounded down, whatever their own
        /// seats owe; a share of 0 is not dealt. The debt is then cleared.
        ///
        /// <b>Cost 7, cooldown 3, range 3, Normal</b> (repriced 2026-09-21).
        /// Leech Round into Sadist is the plan: a loan the debtor refuses to
        /// pay grows by one a turn, up to the cap, and the ultimate collects it.
        ///
        /// <b>Mirror match:</b> Equilibrium halves a Sadist aimed at another
        /// Revú (cost 7), so the most it deals him is 3.
        /// </remarks>
        public static AbilityDefinition Sadist { get; } = new AbilityDefinition(
            id: 1102, name: "Sadist",
            description:
                "Collection day. The deeper the target's side is in debt, the harder it lands, and whoever stands near the debtor pays a share. The debt is settled either way.",
            energyCost: 7, cooldownTurns: 3, range: 3,
            effects: new[]
            {
                AbilityEffect.DebtDamage(
                    SadistSplashRadius, SadistSplashDivisor, DamageType.Normal,
                    minimumDamage: SadistMinimumDamage)
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
            passiveName: "Equilibrium",
            passiveDescription:
                "Every blow against him is weighed against what it cost. Cheap shots land double; the expensive ones he shrugs half away.");
    }
}