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
    /// <remarks>
    /// The answer was yes, and it is what ADR-0002 Amendment 4 adopted. Retained
    /// because the question recurs every time the band or the loop moves, and
    /// because the <c>move%</c> column is the only place the governing figure —
    /// mean move as a share of the loop — is computed at all.
    /// </remarks>
    public static class Dynamic
    {
        public static void Run(int matches)
        {
            var standard = BoardProfile.Standard;

            // 28x2, not the old 24x2: a circuit outside the 8L+4 family cannot
            // be drawn as a cross (ADR-0002 Amendment 6). Its journey is now 59
            // against Standard's 58, so "Compact" is the longer board — kept
            // only as the density comparison this sweep was written for.
            var compact = BoardProfile.Cross("Compact", 3, laps: 2);

            Console.WriteLine($"\nSTANDARD BOARD, SLOWER BANDS — 4 players, opening 2, {matches} matches");
            Console.WriteLine($"{"",-34} {"turns",6} {"p90",5} {"neut",6} {"abil",6} {"coll",6} {"3-up",6} {"move%",6}");

            var labels = new[]
            {
                "1.0 / 1.0 / 1.0", "1.0 / 1.25 / 1.25", "1.0 / 1.5 / 1.5",
                "1.25 / 1.5 / 1.5", "adopted (RosterSpeeds.Default)"
            };

            // Kurbyn is constructed at his BASE speed; the passive adds the rest.
            // Every label states the effective band, which is why this array
            // looks half a step low on its third column.
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
            Row($"Compact {compact.CircuitLength}x2, adopted", compact, RosterSpeeds.Default, 0, matches);
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

            // A mean roll of 7, at the squad's mean effective speed, as a share
            // of the loop. ADR-0002 Amendment 4: target roughly a fifth; past a
            // third a move stops being followable.
            double meanSpeed = (speeds.Bouncer + speeds.Syla +
                                speeds.KurbynBase + Kurbyn.PassiveSpeedBonus) / 3.0;
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