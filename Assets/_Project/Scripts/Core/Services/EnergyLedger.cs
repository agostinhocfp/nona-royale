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
            int before = player.Energy;
            int uncapped = before + earned;
            int after = Math.Min(uncapped, _config.EnergyCap);

            player.SetEnergy(after);
            player.MarkEnergyGranted();

            return new EnergyGrant(
                earned: earned,
                stored: after - before,
                burned: uncapped - after,
                total: after);
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
    }
}