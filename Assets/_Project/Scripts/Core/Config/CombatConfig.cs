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
            double evasionChance = 0.3,
            int bleedDamagePerStack = 1,
            double slowSpeedPenalty = 0.5,
            int markDamagePerTurn = 2,
            double hasteSpeedBonus = 0.5,
            int hasteDurationTurns = 2,
            int hasteBonusCellCap = 3,
            int neutralizeEnergyBounty = 3,
            int shieldPoolDefault = 2,
            int regenEveryTurns = 3,
            int regenAmount = 1)
        {
            if (slowSpeedPenalty < 0) throw new ArgumentOutOfRangeException(nameof(slowSpeedPenalty));
            if (collisionDamage < 0) throw new ArgumentOutOfRangeException(nameof(collisionDamage));
            if (evasionChance < 0.0 || evasionChance > 1.0)
                throw new ArgumentOutOfRangeException(nameof(evasionChance), "A probability, so within [0,1].");
            if (bleedDamagePerStack < 0) throw new ArgumentOutOfRangeException(nameof(bleedDamagePerStack));
            if (markDamagePerTurn < 0) throw new ArgumentOutOfRangeException(nameof(markDamagePerTurn));
            if (hasteSpeedBonus < 0) throw new ArgumentOutOfRangeException(nameof(hasteSpeedBonus));
            if (hasteDurationTurns < 1) throw new ArgumentOutOfRangeException(nameof(hasteDurationTurns));
            if (hasteBonusCellCap < 0)
                throw new ArgumentOutOfRangeException(nameof(hasteBonusCellCap),
                    "Zero means haste adds nothing; pass int.MaxValue to lift the cap.");
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

            CollisionDamage = collisionDamage;
            EvasionChance = evasionChance;
            BleedDamagePerStack = bleedDamagePerStack;
            SlowSpeedPenalty = slowSpeedPenalty;
            MarkDamagePerTurn = markDamagePerTurn;
            HasteSpeedBonus = hasteSpeedBonus;
            HasteDurationTurns = hasteDurationTurns;
            HasteBonusCellCap = hasteBonusCellCap;
            NeutralizeEnergyBounty = neutralizeEnergyBounty;
            ShieldPoolDefault = shieldPoolDefault;
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
        /// <b>0.3, not the 0.5 the prose still says in places.</b> §12 named
        /// this as the next dial if Kurbyn stayed dominant after the targeted
        /// counter in §10.1, and it was pulled. At 0.3 it prevents 0.65 a round
        /// where 0.5 prevented 1.08.
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
        /// variance per event untouched — a 30% negation of a 3-damage instance
        /// is rarer than a 50% one and no more predictable. If evasion still
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
        /// Speed added by <c>StatusKind.Hastened</c>, the default magnitude for
        /// Tagged From Above's squad payout (COMBAT_SYSTEMS §10.2).
        /// </summary>
        /// <remarks>
        /// At 0.5 against the 1.0–1.5 band a hasted squad moves at 1.5–2.0 for
        /// the duration: a real tempo swing a player can still follow on the
        /// board. The payout was +3 in the original roster, which produced a
        /// 35-cell turn — three-quarters of the loop from one ability.
        /// </remarks>
        public double HasteSpeedBonus { get; }

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
        /// At +0.5 speed the haste bonus grows with the roll: a pooled 12 at
        /// 1.0× gained 6 cells, and at 1.5× (hasted to 2.0×) it also gained 6.
        /// Capped at 3, the payout stays a tempo nudge a player can count on the
        /// board rather than a swing decided by how high the dice came up.
        ///
        /// <b>Per operator, per turn — not per move.</b> Movement rounds per
        /// move (§6.3), so a per-move cap would let a split roll collect it
        /// twice: 6 + 6 at 1.0× moves 9 + 9, and neither move reaches the cap,
        /// which would make splitting strictly better than pooling for a
        /// hastened operator. <c>GameEngine</c> keeps the per-turn budget;
        /// doubles re-rolls draw from the same budget because they are the same
        /// turn.
        ///
        /// Only the Hastened status is capped. Evasive Protocol's passive speed
        /// and aura modifiers are not haste and are not counted against it.
        /// </remarks>
        public int HasteBonusCellCap { get; }

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
        /// <b>Eligible means all three at once</b>: in play, below half health
        /// (<c>health × 2 &lt; maxHealth</c>, integers — a 9-health operator
        /// regens at ≤4, a 5-health at ≤2), and not on a safe cell. An
        /// ineligible upkeep resets the streak.
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