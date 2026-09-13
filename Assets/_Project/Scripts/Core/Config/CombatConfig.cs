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
            double evasionChance = 0.5,
            int bleedDamagePerStack = 1,
            double slowSpeedPenalty = 0.5,
            int markDamagePerTurn = 2,
            double hasteSpeedBonus = 0.5,
            int hasteDurationTurns = 2,
            int neutralizeEnergyBounty = 3)
        {
            if (slowSpeedPenalty < 0) throw new ArgumentOutOfRangeException(nameof(slowSpeedPenalty));
            if (collisionDamage < 0) throw new ArgumentOutOfRangeException(nameof(collisionDamage));
            if (evasionChance < 0.0 || evasionChance > 1.0)
                throw new ArgumentOutOfRangeException(nameof(evasionChance), "A probability, so within [0,1].");
            if (bleedDamagePerStack < 0) throw new ArgumentOutOfRangeException(nameof(bleedDamagePerStack));
            if (markDamagePerTurn < 0) throw new ArgumentOutOfRangeException(nameof(markDamagePerTurn));
            if (hasteSpeedBonus < 0) throw new ArgumentOutOfRangeException(nameof(hasteSpeedBonus));
            if (hasteDurationTurns < 1) throw new ArgumentOutOfRangeException(nameof(hasteDurationTurns));
            if (neutralizeEnergyBounty < 0)
                throw new ArgumentOutOfRangeException(nameof(neutralizeEnergyBounty),
                    "A bounty cannot take energy away; zero disables it.");

            CollisionDamage = collisionDamage;
            EvasionChance = evasionChance;
            BleedDamagePerStack = bleedDamagePerStack;
            SlowSpeedPenalty = slowSpeedPenalty;
            MarkDamagePerTurn = markDamagePerTurn;
            HasteSpeedBonus = hasteSpeedBonus;
            HasteDurationTurns = hasteDurationTurns;
            NeutralizeEnergyBounty = neutralizeEnergyBounty;
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

        /// <summary>Chance the first Normal instance each round is negated (COMBAT_SYSTEMS §5.5).</summary>
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

        public static CombatConfig Default => new CombatConfig();
    }
}