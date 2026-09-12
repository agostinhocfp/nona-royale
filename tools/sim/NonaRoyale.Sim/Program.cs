// tools/sim/NonaRoyale.Sim/Program.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;

namespace NonaRoyale.Sim
{
    /// <summary>
    /// Headless Monte Carlo harness, driving the real rules.
    /// </summary>
    /// <remarks>
    /// This replaces the Python model in <c>tools/sim/nona_sim.py</c>. That
    /// version approximated the rules — no auras, no stun, no stealth, abilities
    /// flattened to "6 energy for 3 damage" — so its figures described a game
    /// adjacent to this one. Every number printed here comes from
    /// <c>NonaRoyale.Core</c> itself, which means the harness is now also an
    /// integration test: a rule that throws under ten thousand matches was never
    /// going to survive a playtest either.
    ///
    /// It compiles against the core directly because the core references zero
    /// Unity types (ADR-0004). That wall was asserted in an asmdef months before
    /// anything needed it; this is the first thing to actually depend on it.
    /// </remarks>
    public static class Program
    {
        private const int MaxTurnsPerMatch = 2000;

        public static void Main(string[] args)
        {
            int matches = args.Length > 0 && int.TryParse(args[0], out var n) ? n : 2000;

            Console.WriteLine($"Nona Royale — simulation against the live core, {matches} matches per row\n");

            if (args.Length > 1 && args[1] == "laps") { Laps.Run(matches); return; }
            if (args.Length > 1 && args[1] == "opening") { Opening.Run(matches); return; }
            if (args.Length > 1 && args[1] == "reach") { Reach.Run(matches); return; }
            if (args.Length > 1 && args[1] == "dynamic") { Dynamic.Run(matches); return; }

            Header("SPEED BAND — 4 players, Standard board");
            foreach (var band in new[]
                     {
                         ("flat 1.0", new RosterSpeeds(1.0, 1.0, 1.0)),
                         ("1.0 / 1.5 / 1.5", new RosterSpeeds(1.0, 1.5, 1.0)),
                         ("adopted 1.5 / 2.0 / 2.0", RosterSpeeds.Default),
                         ("fast 2.0 / 2.5 / 2.5", new RosterSpeeds(2.0, 2.5, 2.0))
                     })
            {
                Row(band.Item1, 4, Run(matches, 4, BoardProfile.Standard, band.Item2));
            }

            Header("BOARD PROFILE — 4 players, adopted band");
            foreach (var board in new[] { BoardProfile.Sprint, BoardProfile.Standard, BoardProfile.Long })
                Row(board.ToString(), 4, Run(matches, 4, board, RosterSpeeds.Default));

            Header("PLAYER COUNT — Standard board, adopted band");
            for (int seats = 2; seats <= 4; seats++)
                Row($"{seats} players", seats, Run(matches, seats, BoardProfile.Standard, RosterSpeeds.Default));

            Header("COLLISION DAMAGE — 4 players, Standard board");
            foreach (int damage in new[] { 2, 3, 4, 6 })
            {
                Row($"CollisionDamage = {damage}", 4,
                    Run(matches, 4, BoardProfile.Standard, RosterSpeeds.Default,
                        new CombatConfig(collisionDamage: damage)));
            }
        }

        private static List<MatchStats> Run(
            int matches, int seats, BoardProfile board, RosterSpeeds speeds, CombatConfig combat = null)
        {
            var colours = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow };
            var results = new List<MatchStats>(matches);

            for (int i = 0; i < matches; i++)
            {
                var match = MatchFactory.CreateAlphaMatch(
                    colours.Take(seats).ToList(), seed: i, board: board,
                    combatConfig: combat, speeds: speeds);

                var stats = new MatchStats();
                stats.Observe(match.Engine.Start());

                var player = new ScriptedPlayer(match, stats);

                while (!match.Engine.MatchOver && stats.Turns < MaxTurnsPerMatch)
                    player.TakeTurn();

                stats.Completed = match.Engine.MatchOver;
                results.Add(stats);
            }

            return results;
        }

        private static void Header(string title)
        {
            Console.WriteLine($"\n{title}");
            Console.WriteLine($"{"",-26} {"turns",6} {"p90",5} {"neut",6} {"abil",6} {"coll",6} " +
                              $"{"burn",6} {"3-up",6} {"done",5}");
        }

        private static void Row(string label, int seats, List<MatchStats> runs)
        {
            // Reported per seat, which is what ADR-0002 quotes: the match total
            // divided by however many seats were taking turns.
            var turns = runs.Select(r => (double)r.Turns / seats).OrderBy(t => t).ToList();

            Console.WriteLine(
                $"{label,-26} {turns.Average(),6:0.0} {turns[(int)(0.9 * (turns.Count - 1))],5:0} " +
                $"{runs.Average(r => r.Neutralizes),6:0.0} {runs.Average(r => r.AbilitiesFired),6:0.0} " +
                $"{runs.Average(r => r.Collisions),6:0.0} {runs.Average(r => r.EnergyBurned),6:0.0} " +
                $"{runs.Average(r => r.FullSquadShare) * 100,5:0}% {runs.Count(r => r.Completed) * 100.0 / runs.Count,4:0}%");
        }
    }
}