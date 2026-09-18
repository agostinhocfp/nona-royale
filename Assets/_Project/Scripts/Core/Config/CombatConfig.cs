// Assets/_Project/Scripts/Core/Config/CombatConfig.cs
using System;

namespace NonaRoyale.Core.Config
{
    /// <summary>
    /// Combat dials, as named config (CONVENTIONS: "Config, not literals").
    /// </summary>
    /// <remarks>
    /// <c>COMBAT_SYSTEMS</c> §12 ranks these by effect per turn of match length
    /// spent, so they are reached for in a known order rather than tuned at
    /// random. That ranking has been replaced three times under measurement —
    /// read §12 rather than the amendment that first produced it.
    /// </remarks>
    public sealed class CombatConfig
    {
        public CombatConfig(
            int collisionDamage = 3,
            double evasionChance = 0.12,
            int bleedDamagePerStack = 1,
            double slowSpeedPenalty = 0.5,
            int markDamagePerTurn = 2,
            int hasteDurationTurns = 2,
            int hasteBonusCellCap = 3,
            int hasteRollThreshold = 6,
            int hasteCellsAtOrBelowThreshold = 1,
            int hasteCellsAboveThreshold = 2,
            int neutralizeEnergyBounty = 3,
            int shieldPoolDefault = 2,
            int regenEveryTurns = 3,
            int regenAmount = 1,
            int burdenCellsAtOrBelowThreshold = 1,
            int burdenCellsAboveThreshold = 2,
            int equilibriumCheapCostMax = 3,
            int equilibriumDearCostMin = 6,
            int speedBonusCellCap = 2)
        {
            if (slowSpeedPenalty < 0) throw new ArgumentOutOfRangeException(nameof(slowSpeedPenalty));
            if (collisionDamage < 0) throw new ArgumentOutOfRangeException(nameof(collisionDamage));
            if (evasionChance < 0.0 || evasionChance > 1.0)
                throw new ArgumentOutOfRangeException(nameof(evasionChance), "A probability, so within [0,1].");
            if (bleedDamagePerStack < 0) throw new ArgumentOutOfRangeException(nameof(bleedDamagePerStack));
            if (markDamagePerTurn < 0) throw new ArgumentOutOfRangeException(nameof(markDamagePerTurn));
            if (hasteDurationTurns < 1) throw new ArgumentOutOfRangeException(nameof(hasteDurationTurns));
            if (hasteBonusCellCap < 0)
                throw new ArgumentOutOfRangeException(nameof(hasteBonusCellCap),
                    "Zero means haste adds nothing; pass int.MaxValue to lift the cap.");
            if (hasteCellsAtOrBelowThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(hasteCellsAtOrBelowThreshold));
            if (hasteCellsAboveThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(hasteCellsAboveThreshold));
            if (neutralizeEnergyBounty < 0)
                throw new ArgumentOutOfRangeException(nameof(neutralizeEnergyBounty),
                    "A bounty cannot take energy away; zero disables it.");
            if (shieldPoolDefault < 1)
                throw new ArgumentOutOfRangeException(nameof(shieldPoolDefault),
                    "A shield that absorbs nothing is worse than no shield — it draws a badge and lies.");
            if (regenEveryTurns < 0)
                throw new ArgumentOutOfRangeException(nameof(regenEveryTurns),
                    "Negative makes no sense; zero disables regeneration.");
            if (regenAmount < 0)
                throw new ArgumentOutOfRangeException(nameof(regenAmount));
            if (burdenCellsAtOrBelowThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(burdenCellsAtOrBelowThreshold));
            if (burdenCellsAboveThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(burdenCellsAboveThreshold));
            if (equilibriumDearCostMin <= equilibriumCheapCostMax)
                throw new ArgumentOutOfRangeException(nameof(equilibriumDearCostMin),
                    "The dear band must start above the cheap one, or a cost would be both.");
            if (speedBonusCellCap < 0)
                throw new ArgumentOutOfRangeException(nameof(speedBonusCellCap),
                    "Zero means speed pays no bonus at all; pass int.MaxValue to lift the cap.");

            CollisionDamage = collisionDamage;
            EvasionChance = evasionChance;
            BleedDamagePerStack = bleedDamagePerStack;
            SlowSpeedPenalty = slowSpeedPenalty;
            MarkDamagePerTurn = markDamagePerTurn;
            HasteDurationTurns = hasteDurationTurns;
            HasteBonusCellCap = hasteBonusCellCap;
            HasteRollThreshold = hasteRollThreshold;
            HasteCellsAtOrBelowThreshold = hasteCellsAtOrBelowThreshold;
            HasteCellsAboveThreshold = hasteCellsAboveThreshold;
            NeutralizeEnergyBounty = neutralizeEnergyBounty;
            ShieldPoolDefault = shieldPoolDefault;
            RegenEveryTurns = regenEveryTurns;
            RegenAmount = regenAmount;
            BurdenCellsAtOrBelowThreshold = burdenCellsAtOrBelowThreshold;
            BurdenCellsAboveThreshold = burdenCellsAboveThreshold;
            EquilibriumCheapCostMax = equilibriumCheapCostMax;
            EquilibriumDearCostMin = equilibriumDearCostMin;
            SpeedBonusCellCap = speedBonusCellCap;
        }

        /// <summary>
        /// Damage a mover deals by landing on an enemy. At 3, nothing on the
        /// roster dies to a single collision — deliberately. Collision is a
        /// <i>softening</i> mechanic that sets up ability kills, which is the
        /// 70/30 combat-over-race priority expressed as a number
        /// (COMBAT_SYSTEMS §7.3).
        /// </summary>
        /// <remarks>
        /// <b>Struck as a dial, then restored.</b> It was measured as inert —
        /// 2 to 6 moved neutralizes by 1.1 — because collisions fired only 2.4
        /// times a match. Under the adopted speed band they fire 4.6 times, and
        /// the same range now moves neutralizes by 2.0 for about a turn, which
        /// makes it one of the two sharpest levers available.
        ///
        /// The rule never changed; its trigger did. Anything struck in §12
        /// should be re-measured after a structural change rather than trusted.
        /// </remarks>
        public int CollisionDamage { get; }

        /// <summary>
        /// Chance the first Normal instance each round is negated
        /// (COMBAT_SYSTEMS §5.5). Prevents a mean of
        /// <c>EvasionChance × 2.17</c> per round, against the current Normal
        /// spread of 1, 2, 2, 2, 3 and collision at 3.
        /// </summary>
        /// <remarks>
        /// <b>0.12 (2026-09-17, designer).</b> It was 0.5, then 0.3 — §12 named
        /// this as the next dial if Kurbyn stayed dominant after the targeted
        /// counter in §10.1, and it was pulled — and he outlasted both, plus the
        /// per-turn speed cap (§6.3). The same day's rebuild of his kit (§10.3)
        /// cut the rate to 0.12: 0.26 prevented a round where 0.3 prevented
        /// 0.65 and 0.5 prevented 1.08. Kurbyn is the only holder, so the global
        /// dial is his number.
        ///
        /// <b>Still probabilistic, and that was a choice.</b>
        /// <c>_HANDOFF_mitigation.md</c> proposed replacing the roll with a flat
        /// reduction of 1 — zero variance, and 1.00 prevented per round. It was
        /// <b>not adopted</b>: moving the rate is a one-line config edit that can
        /// be revisited at any time, where going deterministic changes
        /// <c>IDamageMitigation</c>, removes <c>IRandom</c> from the pipeline and
        /// rewrites five test fixtures' construction. The cheap lever first.
        ///
        /// <b>What the rate does not fix.</b> Lowering the frequency leaves the
        /// variance per event untouched — a 12% negation of a 3-damage instance
        /// is rarer than a 30% one and no more predictable. If evasion still
        /// reads as arbitrary in human play, the answer is the deterministic
        /// pass, not a smaller number here. Reasoned, never measured.
        /// </remarks>
        public double EvasionChance { get; }

        public int BleedDamagePerStack { get; }

        /// <summary>
        /// How much Slow takes off the speed multiplier. Against the 1.0–1.5
        /// band adopted in ADR-0002 Amendment 4, 0.5 costs a fast operator a
        /// third of its movement and takes a slow one to
        /// <c>MinSpeedMultiplier</c> — sharper than it was under the original
        /// 1.5–2.0 band, and not re-measured since the band moved
        /// (COMBAT_SYSTEMS §5.2, and §12's <c>SlowSpeedPenalty</c> item).
        /// </summary>
        /// <remarks>
        /// It also lands more often than it used to: Intimidating Presence
        /// widened from radius 2 to 3, and Syla's slow became her primary
        /// contribution when From the Hip's base damage was halved.
        /// </remarks>
        public double SlowSpeedPenalty { get; }

        /// <summary>
        /// Atomic damage a Mark deals at the marked operator's upkeep, every
        /// turn it is active (COMBAT_SYSTEMS §5.7).
        /// </summary>
        /// <remarks>
        /// Deliberately sub-lethal against the 6-health operators: over a 2-turn
        /// mark it totals 4, leaving them at 2 and inside collision range,
        /// inside Miracle Pull's execute window, and inside a From the Hip
        /// against a bleeding target. The mark's job is to <i>hand</i> the kill
        /// to the marker's squad, which is the condition that pays out Tagged
        /// From Above. A mark that kills on its own makes that payout
        /// self-fulfilling. Set by reasoning, not simulation — re-run the
        /// harness before trusting it.
        /// </remarks>
        public int MarkDamagePerTurn { get; }

        /// <summary>
        /// A roll totalling this or less earns a hastened operator
        /// <see cref="HasteCellsAtOrBelowThreshold"/> extra cells; above it,
        /// <see cref="HasteCellsAboveThreshold"/> (COMBAT_SYSTEMS §5.9).
        /// </summary>
        /// <remarks>
        /// <b>Haste is flat cells, not speed (designer, 2026-09-16).</b> It
        /// was +0.5 speed: +3 in the original roster (a 35-cell turn), cut to
        /// +0.5, then capped at 3 cells a turn the same day. The designer then
        /// cut it hard to +1 on a roll of 6 or less and +2 above, so the bonus
        /// no longer scales with the operator's own speed.
        ///
        /// <b>The whole roll decides, not the move.</b> The two dice's total is
        /// what counts, whatever they were spent on, and each hastened operator
        /// collects the bonus once per roll, on its first move with it. A
        /// single die is always 6 or less, so reading the move's own pips would
        /// have made splitting a low roll pay double.
        /// </remarks>
        public int HasteRollThreshold { get; }

        /// <summary>Extra cells for a roll at or below <see cref="HasteRollThreshold"/>.</summary>
        public int HasteCellsAtOrBelowThreshold { get; }

        /// <summary>Extra cells for a roll above <see cref="HasteRollThreshold"/>.</summary>
        public int HasteCellsAboveThreshold { get; }

        /// <summary>
        /// The haste bonus a roll with this total earns, before the per-turn
        /// cap (§5.9).
        /// </summary>
        public int HasteCellsFor(int rollTotal) =>
            rollTotal <= HasteRollThreshold ? HasteCellsAtOrBelowThreshold : HasteCellsAboveThreshold;

        /// <summary>Cells a burdened operator loses on a roll at or below <see cref="HasteRollThreshold"/> (§5.16).</summary>
        /// <remarks>
        /// <b>Haste run backwards (designer, 2026-09-17)</b>, with the same
        /// threshold, so a hastened and burdened operator nets to nothing. It
        /// replaced Sanity's 0.5 speed: the same crawl in spirit, at about 5.4
        /// cells a roll instead of 3.75, and countable without halving.
        /// </remarks>
        public int BurdenCellsAtOrBelowThreshold { get; }

        /// <summary>Cells a burdened operator loses on a roll above <see cref="HasteRollThreshold"/>.</summary>
        public int BurdenCellsAboveThreshold { get; }

        /// <summary>
        /// The burden a roll with this total costs (§5.16). No per-turn cap: a
        /// penalty needs no ceiling, and a move never drops below one cell.
        /// </summary>
        public int BurdenCellsFor(int rollTotal) =>
            rollTotal <= HasteRollThreshold ? BurdenCellsAtOrBelowThreshold : BurdenCellsAboveThreshold;

        /// <summary>An ability costing this or less deals double to an Equilibrium holder (§5.17).</summary>
        public int EquilibriumCheapCostMax { get; }

        /// <summary>An ability costing this or more deals half, at least 1, to an Equilibrium holder.</summary>
        public int EquilibriumDearCostMin { get; }

        /// <summary>
        /// What a cast hit of <paramref name="amount"/> from an ability costing
        /// <paramref name="castCost"/> deals an Equilibrium holder (§5.17).
        /// </summary>
        /// <remarks>
        /// <b>Half means at least 1 (designer, 2026-09-17).</b> Floored halves
        /// turned every 1-damage blow from a dear ability — all of Vendetta,
        /// Collision's rake — into nothing, which made Revú immune to whole
        /// kits rather than priced against them. A hit of 0 stays 0.
        /// </remarks>
        public int EquilibriumScale(int castCost, int amount)
        {
            if (amount <= 0) return amount;
            if (castCost <= EquilibriumCheapCostMax) return amount * 2;
            if (castCost >= EquilibriumDearCostMin) return Math.Max(1, amount / 2);
            return amount;
        }

        /// <summary>
        /// How many of the squad's own turns the payout's haste lasts.
        /// </summary>
        /// <remarks>
        /// 2, not 1. The payout can fire on the marker's own turn — a collision
        /// or ability kill — by which point that turn's movement is usually
        /// already spent, so a 1-turn buff would routinely be worth nothing. At
        /// 2 it covers the remainder of the current turn and the whole of the
        /// next, whether it fired on the marker's turn or on an opponent's
        /// upkeep. This is what COMBAT_SYSTEMS §10.2's "one round" resolves to.
        /// </remarks>
        public int HasteDurationTurns { get; }

        /// <summary>
        /// The most extra cells <c>StatusKind.Hastened</c> can add to one
        /// operator's movement in one of its owner's turns (COMBAT_SYSTEMS §5.9).
        /// </summary>
        /// <remarks>
        /// <b>A designer balance call (2026-09-16), not a measured figure.</b>
        /// It was written for haste as +0.5 speed, where a pooled 12 gained 6
        /// cells. Since haste became +1 or +2 per roll, it only binds on a
        /// doubles turn: two high rolls would pay 4, and the cap keeps it at 3.
        /// The designer kept it for that case.
        ///
        /// <b>Per operator, per turn.</b> <c>GameEngine</c> keeps the per-turn
        /// budget; doubles re-rolls draw from the same budget because they are
        /// the same turn.
        /// </remarks>
        public int HasteBonusCellCap { get; }

        /// <summary>
        /// The most cells a move may gain from a speed above 1.0×, per operator
        /// per turn (COMBAT_SYSTEMS §6.3, 2026-09-17). The bonus is
        /// <c>floor(pips × speed) − pips</c>; the cap trims it, never the pips.
        /// </summary>
        /// <remarks>
        /// <b>Per operator, per turn, charged on every move</b> — unlike haste,
        /// speed is who the operator is, not a status paid once per roll, so
        /// each move draws what is left of the budget. Doubles re-rolls draw
        /// from the same budget because they are the same turn. A slowed
        /// operator whose effective speed drops to 1.0× or below pays nothing
        /// and charges nothing. Pass <c>int.MaxValue</c> to lift the cap.
        /// </remarks>
        public int SpeedBonusCellCap { get; }

        /// <summary>
        /// Energy paid to whoever lands a neutralize (COMBAT_SYSTEMS §1.2).
        /// Zero disables the bounty.
        /// </summary>
        /// <remarks>
        /// <b>This reverses ADR-0005</b>, which held that neutralizing grants
        /// the attacker nothing. The reason it was wrong: at four players a kill
        /// is a public good bought with private resources. The victim loses a
        /// lap and <i>every</i> opponent collects it, not only the one who spent
        /// the energy and the position to land it, so the rational play is to
        /// let somebody else do the killing — in a game whose stated priority is
        /// 70% combat.
        ///
        /// <b>Energy rather than position, and 3 rather than more.</b> Energy
        /// buys another fight; cells buy another lap, which is the axis the
        /// attacker's opponents are already being handed for free. It softens
        /// the free-rider problem rather than solving it: fully closing the gap
        /// would need a reward on the order of what the opponents gain, which is
        /// enormous and snowballs. Softened is the most an affordable reward can
        /// do — <c>_HANDOFF_neutralize_rewards.md</c> carries the analysis.
        ///
        /// <b>It never burns.</b> A player at the cap collects nothing and
        /// nothing is destroyed. Holding a full pool is a strategy, and its cost
        /// is already the abilities not cast; the system does not add one. See
        /// <c>EnergyLedger.GrantBounty</c>.
        ///
        /// <b>Unmeasured.</b> Every figure in §12 was taken at zero bounty. More
        /// energy means more abilities, which means more neutralizes, which
        /// means more energy — a small loop at 3 against a 3.5-per-turn drip,
        /// but a loop, and it has never been run.
        /// </remarks>
        public int NeutralizeEnergyBounty { get; }

        /// <summary>
        /// Damage a <c>StatusKind.Shield</c> absorbs when whatever granted it
        /// does not state a pool of its own (COMBAT_SYSTEMS §5.6).
        /// </summary>
        /// <remarks>
        /// <b>It exists to make a silent failure loud.</b> Under the old
        /// whole-instance absorb, a shield's magnitude was never read; under a
        /// pool, a zero would produce a shield that draws a badge and stops
        /// nothing. Every status kind that needs a size now has a default here,
        /// which is what lets the roster state shields without a dependency on
        /// this class.
        ///
        /// <b>The only caller that will rely on it is the deferred board
        /// space</b> (ADR-0003, not in the MVP). Trauma Plate states its own
        /// pool in <c>Javi.cs</c>, where an operator's numbers belong and where
        /// the history of what they were walked back from is kept.
        ///
        /// At 2 against a Normal spread of 1, 2, 2, 2, 3 and collision at 3, it
        /// eats one small hit whole or takes the edge off a collision — never
        /// both. Reasoned, not measured.
        /// </remarks>
        public int ShieldPoolDefault { get; }

        /// <summary>
        /// Owner-upkeeps an operator must spend wounded and exposed before
        /// passive regeneration ticks (COMBAT_SYSTEMS §5.11). Zero disables it.
        /// </summary>
        /// <remarks>
        /// <b>Eligible means all three at once</b>: in play (on the outer
        /// track), wounded, and not on a safe cell. An ineligible upkeep
        /// resets the streak.
        ///
        /// <b>Amended 2026-09-16 (designer): any wound, not below half.</b>
        /// The gated version below ticked about four times a match in the bots
        /// sweep, too little to matter, and it had never actually run: this
        /// constructor did not assign the field, so it read 0. Now assigned.
        /// Together with the roster-wide +1 health it is the match-length
        /// pass (COMBAT_SYSTEMS §5.11, §12). The designer chose 1 every 3
        /// turns over 1 every 2 as the less drastic step. The safe-cell
        /// exclusion stays, for the free-parking reason below. The history
        /// that follows is the argument for the gates as first shipped.
        ///
        /// <b>3, walked back from a rejected 2, walked back from a rejected
        /// unconditional version.</b> Global always-on regen at +1/2 turns was
        /// argued down before it shipped: against pools of 5–9 and damage
        /// instruments of 1s and 2s it is a second health bar, it refunds the
        /// chip damage that taxes racing (the one measured result is fighting
        /// beating racing 67/33, and regen income moves it the wrong way), it
        /// answers "why pay 4 energy for Trauma Plate" with "don't" while
        /// support is already the starving archetype (1.36 casts), and regen
        /// on a safe cell re-opens free parking through the back door. The
        /// below-half gate turns income into a comeback spring; the safe-cell
        /// exclusion keeps the shelter offering nothing but shelter; per-3
        /// keeps it slower than every damage clock in the game (bleed and
        /// marks tick every turn).
        ///
        /// <b>The bet is measurable and has not been run</b>: A/B this dial
        /// (0 against 3) in the policy sweep. If racer or banker win rates
        /// rise against the spendthrift, or Javi's casts fall further, the
        /// gates are too loose — reach for 4 before touching the amount.
        /// </remarks>
        public int RegenEveryTurns { get; }

        /// <summary>
        /// Health restored per regeneration tick (§5.11). At 1 against pools of
        /// 5–9 it undoes one bleed stack's turn — deliberately the smallest
        /// instrument in the game. Capped at max by <c>OperatorState.Heal</c>.
        /// </summary>
        public int RegenAmount { get; }

        public static CombatConfig Default => new CombatConfig();


    }
}