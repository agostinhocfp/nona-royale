// tools/sim/NonaRoyale.Sim/Laps.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Sim
{
    /// <summary>
    /// Sweeps loop size against lap count. The sweep behind ADR-0002
    /// Amendment 3: a shorter loop travelled more times gives the same journey
    /// in half the space, which is where encounter density comes from.
    /// </summary>
    public static class Laps
    {
        public static void Run(int matches)
        {
            Console.WriteLine($"\nLAP / LOOP-SIZE SWEEP — 4 players, adopted band, {matches} matches");
            Console.WriteLine($"{"",-30} {"turns",6} {"p90",5} {"neut",6} {"abil",6} {"coll",6} {"3-up",6}");

            var profiles = new[]
            {
                new BoardProfile("48 x1", 48, 6),
                new BoardProfile("32 x1", 32, 4),
                new BoardProfile("24 x2", 24, 3, laps: 2),
                new BoardProfile("24 x3", 24, 3, laps: 3),
                new BoardProfile("16 x3", 16, 2, laps: 3),
                new BoardProfile("16 x4", 16, 2, laps: 4),
                new BoardProfile("12 x4", 12, 2, laps: 4),
                new BoardProfile("32 x2", 32, 4, laps: 2),
                new BoardProfile("48 x2", 48, 6, laps: 2),
            };

            foreach (var board in profiles)
                Row($"{board.Name} (journey {board.Journey})", 4, Play(matches, 4, board));
        }

        private static List<MatchStats> Play(int matches, int seats, BoardProfile board)
        {
            var colours = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet };
            var results = new List<MatchStats>(matches);

            for (int i = 0; i < matches; i++)
            {
                var match = MatchFactory.CreateAlphaMatch(colours.Take(seats).ToList(), seed: i, board: board);
                var stats = new MatchStats();
                stats.Observe(match.Engine.Start());
                var player = new ScriptedPlayer(match, stats);

                while (!match.Engine.MatchOver && stats.Turns < 4000) player.TakeTurn();
                stats.Completed = match.Engine.MatchOver;
                results.Add(stats);
            }

            return results;
        }

        private static void Row(string label, int seats, List<MatchStats> runs)
        {
            var turns = runs.Select(r => (double)r.Turns / seats).OrderBy(t => t).ToList();
            Console.WriteLine(
                $"{label,-30} {turns.Average(),6:0.0} {turns[(int)(0.9 * (turns.Count - 1))],5:0} " +
                $"{runs.Average(r => r.Neutralizes),6:0.0} {runs.Average(r => r.AbilitiesFired),6:0.0} " +
                $"{runs.Average(r => r.Collisions),6:0.0} {runs.Average(r => r.FullSquadShare) * 100,5:0}%");
        }
    }
}