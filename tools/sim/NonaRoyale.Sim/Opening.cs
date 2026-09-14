// tools/sim/NonaRoyale.Sim/Opening.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Sim
{
    /// <summary>
    /// Sweeps how many operators start already deployed, against ability reach.
    /// Opening deployments are the only lever measured that improves a problem
    /// at no cost elsewhere.
    /// </summary>
    public static class Opening
    {
        public static void Run(int matches)
        {
            var boards = new[]
            {
                new BoardProfile("Standard 48x1", 48, 6),
                new BoardProfile("Compact 24x2", 24, 3, laps: 2)
            };

            foreach (var board in boards)
            {
                Console.WriteLine($"\n{board.Name} — 4 players, adopted band, {matches} matches");
                Console.WriteLine($"{"",-34} {"turns",6} {"p90",5} {"neut",6} {"abil",6} {"coll",6} {"3-up",6}");

                foreach (var opening in new[] { 0, 1, 2 })
                    Row($"start with {opening} deployed", board, opening, 0, matches);

                foreach (var bonus in new[] { 1, 2 })
                    Row($"start 1 deployed, reach +{bonus}", board, 1, bonus, matches);
            }
        }

        private static void Row(string label, BoardProfile board, int opening, int reach, int matches)
        {
            var colours = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow };
            var runs = new List<MatchStats>(matches);

            for (int i = 0; i < matches; i++)
            {
                var match = MatchFactory.CreateAlphaMatch(
                    colours, seed: i, board: board,
                    openingDeployments: opening, abilityRangeBonus: reach);

                var stats = new MatchStats();
                stats.Observe(match.Engine.Start());
                var player = new ScriptedPlayer(match, stats);

                while (!match.Engine.MatchOver && stats.Turns < 4000) player.TakeTurn();
                runs.Add(stats);
            }

            var turns = runs.Select(r => (double)r.Turns / 4).OrderBy(t => t).ToList();
            Console.WriteLine(
                $"{label,-34} {turns.Average(),6:0.0} {turns[(int)(0.9 * (turns.Count - 1))],5:0} " +
                $"{runs.Average(r => r.Neutralizes),6:0.0} {runs.Average(r => r.AbilitiesFired),6:0.0} " +
                $"{runs.Average(r => r.Collisions),6:0.0} {runs.Average(r => r.FullSquadShare) * 100,5:0}%");
        }
    }
}