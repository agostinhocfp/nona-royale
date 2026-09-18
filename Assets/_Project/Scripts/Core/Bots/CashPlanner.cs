// Assets/_Project/Scripts/Core/Bots/CashPlanner.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Bots
{
    /// <summary>One die the seat could sell instead of moving, with the bot's opinion of it.</summary>
    public readonly struct ScoredCash
    {
        public ScoredCash(OperatorState seller, int dieFace, double value, double bestMove)
        {
            Seller = seller;
            DieFace = dieFace;
            Value = value;
            BestMove = bestMove;
        }

        public OperatorState Seller { get; }
        public int DieFace { get; }

        /// <summary>What the energy is worth, in the same units a cell of progress is.</summary>
        public double Value { get; }

        /// <summary>The best thing the seat could do with that die instead.</summary>
        public double BestMove { get; }

        /// <summary>How much better selling it is than spending it. Negative means keep it.</summary>
        public double Margin => Value - BestMove;

        public ICommand ToCommand() => new CashDieCommand(Seller.Id, DieFace);
    }

    /// <summary>
    /// Whether to cash a die rather than move it (COMBAT_SYSTEMS §3.4). Fortuna's
    /// House Edge, and the only decision in the game that spends a die on nothing.
    /// </summary>
    /// <remarks>
    /// <b>It is a comparison, not a rule.</b> The die is worth whatever the best
    /// landing it could buy is worth — which the move scorer has already priced,
    /// including the danger of the cell it would end on. When that figure is below
    /// what two energy buys, the die is worth more sold. Most turns it is not:
    /// a die averages three and a half cells and a seat needs very nearly every
    /// one of them, which is exactly the tension the passive is priced around.
    ///
    /// <b>The cases it fires in are the ones the ability exists for:</b> a one that
    /// lands nobody anywhere useful, and a roll whose only legal move walks a
    /// wounded operator into something — where the best landing scores negative and
    /// selling is strictly better than being forced (§6.1).
    ///
    /// <b>A deploy is never sold.</b> A six with somebody in the yard is worth far
    /// more than any pool, and the move scorer says so; no special case is needed
    /// here, which is the point of comparing rather than ruling.
    /// </remarks>
    public static class CashPlanner
    {
        /// <summary>
        /// The die worth selling, or null. <paramref name="moves"/> is the ranked
        /// move list the brain already computed, so nothing is scored twice.
        /// </summary>
        public static ScoredCash? Best(
            BotBoard board, BotWeights weights, IReadOnlyList<ScoredMove> moves)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (weights == null) throw new ArgumentNullException(nameof(weights));

            var engine = board.Engine;
            var seat = engine.CurrentPlayer;
            if (seat == null || engine.UnspentDice.Count == 0) return null;

            double value = engine.CashedDieEnergy * weights.EnergyGain;
            ScoredCash? best = null;

            foreach (var seller in seat.Operators)
            {
                for (int i = 0; i < engine.UnspentDice.Count; i++)
                {
                    int face = engine.UnspentDice[i];
                    if (!engine.CanCash(seller, face)) continue;

                    var option = new ScoredCash(seller, face, value, BestMoveWith(moves, face));
                    if (option.Margin <= 0.0) continue;

                    if (best == null || option.Margin > best.Value.Margin) best = option;
                }
            }

            return best;
        }

        /// <summary>
        /// The best thing the seat could do with this die: the best single-die
        /// option that spends it, or — when every option pools the whole roll —
        /// the best pooled one, since taking the die away is what selling costs.
        /// </summary>
        private static double BestMoveWith(IReadOnlyList<ScoredMove> moves, int face)
        {
            if (moves == null || moves.Count == 0) return 0.0;

            double single = double.NegativeInfinity;
            double pooled = double.NegativeInfinity;

            foreach (var move in moves)
            {
                // A deploy spends the six whatever its DieFace says.
                bool spendsThisDie = move.IsDeploy
                    ? face == 6
                    : move.DieFace == face;

                if (spendsThisDie && move.Score > single) single = move.Score;
                else if (move.DieFace == null && !move.IsDeploy && move.Score > pooled) pooled = move.Score;
            }

            if (!double.IsNegativeInfinity(single)) return single;
            if (!double.IsNegativeInfinity(pooled)) return pooled;

            return 0.0;
        }
    }
}
