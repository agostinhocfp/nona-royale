// Assets/_Project/Scripts/Core/Bots/MoveScorer.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Bots
{
    /// <summary>One way to spend dice, with the bot's opinion of it.</summary>
    public readonly struct ScoredMove
    {
        public ScoredMove(ICommand command, OperatorState op, int? dieFace, bool isDeploy, double score)
        {
            Command = command;
            Operator = op;
            DieFace = dieFace;
            IsDeploy = isDeploy;
            Score = score;
        }

        public ICommand Command { get; }
        public OperatorState Operator { get; }

        /// <summary>The die this spends, or null for the whole roll (and for a deploy, which always spends a six).</summary>
        public int? DieFace { get; }

        public bool IsDeploy { get; }
        public double Score { get; }
    }

    /// <summary>
    /// Scores every way the current seat could spend its dice: each deploy the
    /// engine allows and each landing <c>PreviewLandings</c> offers (BOTS.md
    /// decision 3).
    /// </summary>
    /// <remarks>
    /// <b>The options come from the engine; only the ranking is the bot's.</b>
    ///
    /// <b>A single-die option is credited with the die it leaves.</b> Moving
    /// one operator by one die leaves the other die for someone else, so it is
    /// scored as if that die will be spent too. Without this, the whole roll on
    /// one piece always looks better than a split, only because it moves
    /// further in one step.
    /// </remarks>
    public static class MoveScorer
    {
        /// <summary>Every option, best first. Jitter is drawn from <paramref name="random"/>, never the match's stream.</summary>
        public static List<ScoredMove> Rank(BotBoard board, BotWeights weights, IRandom random)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (weights == null) throw new ArgumentNullException(nameof(weights));

            var engine = board.Engine;
            var seat = engine.CurrentPlayer;
            var ranked = new List<ScoredMove>();
            if (seat == null || engine.UnspentDice.Count == 0) return ranked;

            int unspent = 0;
            for (int i = 0; i < engine.UnspentDice.Count; i++) unspent += engine.UnspentDice[i];

            foreach (var op in seat.Operators)
            {
                if (!op.IsInYard || !engine.CanDeploy(op)) continue;

                double score = ScoreDeploy(board, weights, op, unspent) + Jitter(board, random);
                ranked.Add(new ScoredMove(new DeployCommand(op.Id), op, null, true, score));
            }

            foreach (var preview in engine.PreviewLandings())
            {
                var op = board.ById(preview.OperatorId);
                if (op == null) continue;

                int leftover = preview.DieFace.HasValue ? unspent - preview.DieFace.Value : 0;
                double score = ScoreLanding(board, weights, op, preview.Progress, preview.Cells, leftover)
                    + Jitter(board, random);

                ranked.Add(new ScoredMove(
                    new MoveCommand(op.Id, preview.DieFace), op, preview.DieFace, false, score));
            }

            ranked.Sort((a, b) => b.Score.CompareTo(a.Score));
            return ranked;
        }

        public static double ScoreDeploy(BotBoard board, BotWeights weights, OperatorState op, int unspent)
        {
            // A deploy spends a six and places the piece on its start cell,
            // which is safe, so the only positional cost is what can reach it.
            var start = board.CellAt(op.Owner, 0);
            double score = weights.Deploy;
            score -= board.Threat(op.Owner, start) * weights.Danger * 0.5;

            // The die left after the six is assumed to be spent by someone.
            score += Math.Max(0, unspent - board.Game.DeployRequirement) * weights.Progress * 0.9;
            return score;
        }

        public static double ScoreLanding(
            BotBoard board, BotWeights weights, OperatorState op, int to, int cells, int leftoverPips)
        {
            var map = board.Map;
            int from = op.Progress;

            double score = cells * weights.Progress;

            if (map.HasFinished(to)) score += weights.ReachHome;
            else if (map.IsInHomeColumn(to) && !map.IsInHomeColumn(from)) score += weights.HomeEntry;

            // Where the piece stands afterwards, against where it stands now.
            double dangerFrom = map.IsOnOuterTrack(from) ? board.Threat(op.Owner, board.CellAt(op.Owner, from)) : 0.0;
            double dangerTo = map.IsOnOuterTrack(to) ? board.Threat(op.Owner, board.CellAt(op.Owner, to)) : 0.0;
            score += (dangerFrom - dangerTo) * weights.Danger * board.Fragility(op);

            if (map.IsOnOuterTrack(to)) score += Contact(board, weights, op, to, cells);

            // The die this option leaves is assumed to be spent by someone.
            score += leftoverPips * weights.Progress * 0.9;

            return score;
        }

        /// <summary>
        /// Landing on enemies: a collision strikes every enemy on the cell and
        /// takes the cell only if all of them fall; otherwise the mover bounces
        /// (ADR-0005). Safe cells never collide.
        /// </summary>
        private static double Contact(BotBoard board, BotWeights weights, OperatorState mover, int to, int cells)
        {
            var cell = board.CellAt(mover.Owner, to);
            if (board.Map.IsSafe(cell)) return 0.0;

            var occupants = board.EnemiesOn(mover.Owner, cell);
            if (occupants.Count == 0) return 0.0;

            double score = 0.0;
            bool allFall = true;
            int damage = board.Combat.CollisionDamage;

            foreach (var enemy in occupants)
            {
                double hit = board.ExpectedHit(enemy, damage, DamageType.Normal);
                score += Math.Min(hit, enemy.Health) * weights.CollisionDamage;

                if (hit >= enemy.Health) score += weights.Kill + Math.Max(0, enemy.Progress) * weights.KillProgress;
                else allFall = false;
            }

            if (!allFall) score -= weights.Bounce + cells * weights.Progress;

            return score;
        }

        private static double Jitter(BotBoard board, IRandom random) =>
            random == null ? 0.0 : random.NextDouble() * board.Config.Jitter;
    }
}