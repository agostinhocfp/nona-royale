// tools/sim/NonaRoyale.Sim/Reach.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Sim
{
    /// <summary>
    /// Sweeps ability reach on the adopted configuration. Reach is the largest
    /// known balance lever and is deliberately unspent — see COMBAT_SYSTEMS §12.
    /// </summary>
    public static class Reach
    {
        public static void Run(int matches)
        {
            // Both must be drawable crosses. A continuous Ludo cross needs
            // CircuitLength = 8L + 4 for a whole arm length L (ADR-0002
            // Amendment 6): 48 needs L=5.5 and 24 needs L=2.5, so BoardLayout
            // refuses both. This swept two boards that could never ship, under a
            // header claiming to describe the adopted configuration.
            //
            // Sprint replaces Compact rather than Compact being corrected to
            // 28x2. That profile was adopted and then withdrawn after human play,
            // and sweeping it measures a game nobody will play. Sprint is
            // retained for measurement (ADR-0002) and is the combat-dense board,
            // which is where a change in reach should show its largest effect.
            var boards = new[] { BoardProfile.Standard, BoardProfile.Sprint };

            Console.WriteLine($"\nREACH ON THE ADOPTED CONFIGURATION — 4 players, opening 2, {matches} matches");
            Console.WriteLine($"{"",-34} {"turns",6} {"p90",5} {"neut",6} {"abil",6} {"coll",6} {"3-up",6}");

            foreach (var board in boards)
            {
                foreach (var bonus in new[] { 0, 1, 2 })
                {
                    var colours = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow };
                    var runs = new List<MatchStats>(matches);

                    for (int i = 0; i < matches; i++)
                    {
                        var match = MatchFactory.CreateAlphaMatch(
                            colours, seed: i, board: board,
                            openingDeployments: 2, abilityRangeBonus: bonus);

                        var stats = new MatchStats();
                        stats.Observe(match.Engine.Start());
                        var player = new ScriptedPlayer(match, stats);
                        while (!match.Engine.MatchOver && stats.Turns < 4000) player.TakeTurn();
                        runs.Add(stats);
                    }

                    var turns = runs.Select(r => (double)r.Turns / 4).OrderBy(t => t).ToList();
                    Console.WriteLine(
                                                $"{board.Name + " " + board.CircuitLength + "/" + board.HomeColumnLength + ", reach +" + bonus,-34} {turns.Average(),6:0.0} " +
                        $"{turns[(int)(0.9 * (turns.Count - 1))],5:0} {runs.Average(r => r.Neutralizes),6:0.0} " +
                        $"{runs.Average(r => r.AbilitiesFired),6:0.0} {runs.Average(r => r.Collisions),6:0.0} " +
                        $"{runs.Average(r => r.FullSquadShare) * 100,5:0}%");
                }
            }
        }
    }
}