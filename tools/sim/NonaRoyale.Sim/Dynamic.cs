// tools/sim/NonaRoyale.Sim/Dynamic.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Sim
{
    /// <summary>
    /// Can the Standard board carry Compact's combat density by slowing the
    /// speed band instead of shrinking the loop? Readability is the constraint:
    /// a move should be a fraction of the loop a player can follow.
    /// </summary>
    public static class Dynamic
    {
        public static void Run(int matches)
        {
            var standard = BoardProfile.Standard;
            var compact = new BoardProfile("Compact", 24, 3, laps: 2);

            Console.WriteLine($"\nSTANDARD BOARD, SLOWER BANDS — 4 players, opening 2, {matches} matches");
            Console.WriteLine($"{"",-34} {"turns",6} {"p90",5} {"neut",6} {"abil",6} {"coll",6} {"3-up",6} {"move%",6}");

            var labels = new[]
            {
                "1.0 / 1.0 / 1.0", "1.0 / 1.25 / 1.25", "1.0 / 1.5 / 1.5",
                "1.25 / 1.5 / 1.5", "adopted 1.5 / 2.0 / 2.0"
            };

            var bands = new[]
            {
                new RosterSpeeds(1.0, 1.0, 0.5),
                new RosterSpeeds(1.0, 1.25, 0.75),
                new RosterSpeeds(1.0, 1.5, 1.0),
                new RosterSpeeds(1.25, 1.5, 1.0),
                RosterSpeeds.Default
            };

            for (int i = 0; i < bands.Length; i++)
                Row(labels[i], standard, bands[i], 0, matches);

            Console.WriteLine("\n  with reach +1");
            for (int i = 0; i < 4; i++)
                Row(labels[i] + " r+1", standard, bands[i], 1, matches);

            Console.WriteLine("\n  for comparison");
            Row("Compact 24x2, adopted", compact, RosterSpeeds.Default, 0, matches);
        }

        private static void Row(string label, BoardProfile board, RosterSpeeds speeds, int reach, int matches)
        {
            var colours = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow };
            var runs = new List<MatchStats>(matches);

            for (int i = 0; i < matches; i++)
            {
                var match = MatchFactory.CreateAlphaMatch(
                    colours, seed: i, board: board, speeds: speeds,
                    openingDeployments: 2, abilityRangeBonus: reach);

                var stats = new MatchStats();
                stats.Observe(match.Engine.Start());
                var player = new ScriptedPlayer(match, stats);

                while (!match.Engine.MatchOver && stats.Turns < 6000) player.TakeTurn();
                runs.Add(stats);
            }

            // A mean roll of 7, at the squad's mean speed, as a share of the loop.
            double meanSpeed = (speeds.Bouncer + speeds.Syla +
                                speeds.KurbynBase + AlphaRoster.KurbynPassiveSpeedBonus) / 3.0;
            double movePercent = 7.0 * meanSpeed / board.CircuitLength * 100.0;

            var turns = runs.Select(r => (double)r.Turns / 4).OrderBy(t => t).ToList();

            Console.WriteLine(
                $"{label,-34} {turns.Average(),6:0.0} {turns[(int)(0.9 * (turns.Count - 1))],5:0} " +
                $"{runs.Average(r => r.Neutralizes),6:0.0} {runs.Average(r => r.AbilitiesFired),6:0.0} " +
                $"{runs.Average(r => r.Collisions),6:0.0} {runs.Average(r => r.FullSquadShare) * 100,5:0}% " +
                $"{movePercent,5:0}%");
        }
    }
}