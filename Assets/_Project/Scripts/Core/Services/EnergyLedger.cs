// Assets/_Project/Scripts/Core/Services/EnergyLedger.cs
using System;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// The only place energy is created or spent (COMBAT_SYSTEMS §3).
    /// </summary>
    /// <remarks>
    /// <b>Why this is one service.</b> The old codebase computed energy in two
    /// files and the two disagreed — <c>sum &lt;= 8</c> in one, <c>sum &lt; 9</c>
    /// in the other. Nobody noticed because neither could be tested without the
    /// engine running. That single bug is most of the reason ADR-0004 exists, so
    /// the formula lives here once, behind tests that run in milliseconds.
    ///
    /// <b>What it does not decide.</b> Whether a <i>particular</i> operator may
    /// spend is someone else's question: a stunned operator cannot act (§5.1),
    /// and one in a home column is out of the fight (§4.3). The ledger only
    /// knows whether the pool can pay.
    /// </remarks>
    public sealed class EnergyLedger
    {
        private readonly EnergyConfig _config;

        public EnergyLedger(EnergyConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>The most a pool can hold (§3.1). Read-only, for display.</summary>
        public int Cap => _config.EnergyCap;

        /// <summary>
        /// Grants this turn's energy: <c>floor(diceTotal / 2)</c>, capped, with
        /// the overflow burned rather than stored.
        /// </summary>
        /// <remarks>
        /// Granted <b>once per turn, on the first roll only</b>. A doubles
        /// re-roll returns a refused grant rather than throwing — the turn
        /// machine re-rolls without needing to remember whether it has already
        /// paid, and the rule is enforced in the one place that owns it.
        /// </remarks>
        public EnergyGrant GrantForTurn(PlayerState player, DiceRoll roll)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            if (player.HasBeenGrantedEnergyThisTurn)
                return EnergyGrant.AlreadyGranted(player.Energy);

            int earned = roll.Total / _config.DiceDivisor;   // integer division floors
            var grant = Credit(player, earned);

            // Marked here and nowhere else: the bounty must not consume the
            // turn's grant, and the turn's grant must not be repeatable.
            player.MarkEnergyGranted();

            return grant;
        }

        /// <summary>
        /// Grants energy outside the turn cycle — the bounty a neutralize pays
        /// its attacker (§1.2). Pays what the pool can hold and no more.
        /// </summary>
        /// <remarks>
        /// <b>Deliberately not <see cref="GrantForTurn"/>.</b> It does not set
        /// the once-per-turn flag and does not consult it, because a kill is not
        /// the turn's income and a player may earn more than one in a turn.
        ///
        /// <b>It never burns.</b> A player sitting at the cap collects nothing
        /// from a kill, and nothing is destroyed either — the bounty simply pays
        /// what fits. Two reasons, and the second is the load-bearing one:
        ///
        /// Holding energy is a strategy. Several operators want a full pool at a
        /// chosen moment, and a bounty that punished the bank would tax the
        /// choice rather than reward the kill. If hoarding is the wrong play in
        /// a given match, the cost is already the abilities not cast; the system
        /// does not need to add one.
        ///
        /// And <c>burned</c> is a measurement, not bookkeeping. It answers "how
        /// much energy did the economy generate that a player could not hold",
        /// which is a fact about the drip and the cap (§3.1) and is tracked as
        /// such in the harness. Charging a bounty's overflow to it would make
        /// that figure mean two different things at once.
        /// </remarks>
        public EnergyGrant GrantBounty(PlayerState player, int amount)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount),
                    "A bounty cannot take energy away; zero disables it.");

            int before = player.Energy;
            int credited = Math.Max(0, Math.Min(amount, _config.EnergyCap - before));

            player.SetEnergy(before + credited);

            // earned == stored, burned == 0: the unpaid remainder was never
            // earned rather than destroyed.
            return new EnergyGrant(
                earned: credited,
                stored: credited,
                burned: 0,
                total: player.Energy);
        }

        /// <summary>Whether the pool can cover a cost. Passives are free and never ask.</summary>
        public bool CanAfford(PlayerState player, int cost)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));

            return player.Energy >= cost;
        }

        /// <summary>
        /// Spends from the shared pool, or refuses. There is no cap on abilities
        /// per turn — cooldowns and the ceiling are the regulators, and banking
        /// to 12 to fire two abilities in one turn is a combo worth having (§3.2).
        /// </summary>
        public SpendResult Spend(PlayerState player, int cost)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (cost < 0)
                throw new ArgumentOutOfRangeException(nameof(cost),
                    "Abilities cannot refund energy; a free ability costs zero.");

            if (player.Energy < cost)
                return SpendResult.Refused(cost, player.Energy);

            player.SetEnergy(player.Energy - cost);
            return SpendResult.Paid(cost, player.Energy);
        }

        /// <summary>
        /// Adds this turn's income and reports what the cap destroyed.
        /// </summary>
        /// <remarks>
        /// Only the turn grant burns. See <see cref="GrantBounty"/> for why the
        /// bounty does not, and why the distinction is worth keeping.
        /// </remarks>
        private EnergyGrant Credit(PlayerState player, int earned)
        {
            int before = player.Energy;
            int uncapped = before + earned;
            int after = Math.Min(uncapped, _config.EnergyCap);

            player.SetEnergy(after);

            return new EnergyGrant(
                earned: earned,
                stored: after - before,
                burned: uncapped - after,
                total: after);
        }
    }
}