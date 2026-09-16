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

            // Invariant formatting so output matches the ADR tables on any locale.
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture =
                System.Globalization.CultureInfo.InvariantCulture;

            Console.WriteLine($"Nona Royale — simulation against the live core, {matches} matches per row\n");

            if (args.Length > 1 && args[1] == "laps") { Laps.Run(matches); return; }
            if (args.Length > 1 && args[1] == "opening") { Opening.Run(matches); return; }
            if (args.Length > 1 && args[1] == "reach") { Reach.Run(matches); return; }
            if (args.Length > 1 && args[1] == "dynamic") { Dynamic.Run(matches); return; }
            if (args.Length > 1 && args[1] == "policy") { Policies.Run(matches); return; }
            if (args.Length > 1 && args[1] == "usage") { Usage.Run(matches); return; }
            if (args.Length > 1 && args[1] == "bots") { BotSweep.Run(matches); return; }

            if (args.Length > 1)
            {
                Console.WriteLine($"Unknown sweep '{args[1]}'. Try: laps, opening, reach, dynamic, policy, usage, bots.");
                return;
            }

            Header("SPEED BAND — 4 players, Standard board, opening 2");
            foreach (var band in new[]
                     {
                         ("flat 1.0", new RosterSpeeds(1.0, 1.0, 1.0)),
                         ("adopted 1.0 / 1.5 / 1.5", RosterSpeeds.Default),
                         ("1.25 / 1.75 / 1.75", new RosterSpeeds(1.25, 1.75, 1.25)),
                         ("fast 2.0 / 2.5 / 2.5", new RosterSpeeds(2.0, 2.5, 2.0))
                     })
            {
                Row(band.Item1, 4, Run(matches, 4, BoardProfile.Standard, band.Item2));
            }

            // Every drawable cross from Sprint up, not only the three named
            // profiles. 36/4 and 44/5 were unreachable while the constraint was
            // written as `CircuitLength % 8 == 0`, which ADR-0002 Amendment 6
            // struck as a false invariant — and 44/5 is the only candidate that
            // buys pacing back without giving up combat.
            Header("BOARD PROFILE — 4 players, adopted band, opening 2");
            foreach (int arm in new[] { 3, 4, 5, 6, 7 })
            {
                var board = BoardProfile.Cross(arm == 6 ? "Standard" : $"Cross-{arm}", arm);
                Row(board.ToString(), 4, Run(matches, 4, board, RosterSpeeds.Default));
            }

            Header("PLAYER COUNT — Standard board, adopted band, opening 2");
            for (int seats = 2; seats <= 4; seats++)
                Row($"{seats} players", seats, Run(matches, seats, BoardProfile.Standard, RosterSpeeds.Default));

            Header("COLLISION DAMAGE — 4 players, Standard board, opening 2");
            foreach (int damage in new[] { 2, 3, 4, 6 })
            {
                Row($"CollisionDamage = {damage}", 4,
                    Run(matches, 4, BoardProfile.Standard, RosterSpeeds.Default,
                        new CombatConfig(collisionDamage: damage)));
            }

            // The kill bounty closes a feedback loop — more energy, more
            // abilities, more kills, more energy — so it cannot be reasoned
            // about, only measured. The row at 0 is the control: it reproduces
            // the configuration every figure in COMBAT_SYSTEMS §12 was taken
            // under, so the delta is attributable to the bounty and nothing else.
            //
            // Watch `burn` as much as `neut`. A bounty pays nothing to a player
            // already at the cap, so if burn is high the reward is landing on
            // the players who need it least.
            Header("KILL BOUNTY — 4 players, Standard board, opening 2");
            foreach (int bounty in new[] { 0, 3, 6 })
            {
                Row($"NeutralizeEnergyBounty = {bounty}", 4,
                    Run(matches, 4, BoardProfile.Standard, RosterSpeeds.Default,
                        new CombatConfig(neutralizeEnergyBounty: bounty)));
            }
        }

        private static List<MatchStats> Run(
            int matches, int seats, BoardProfile board, RosterSpeeds speeds, CombatConfig combat = null)
        {
            var colours = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet };
            var results = new List<MatchStats>(matches);

            for (int i = 0; i < matches; i++)
            {
                // openingDeployments: 2 is the adopted configuration (ADR-0002
                // Amendment 3). The default sweep measured 0 for a while, which
                // meant the headline table described a board nobody ships.
                var match = MatchFactory.CreateAlphaMatch(
                    colours.Take(seats).ToList(), seed: i, board: board,
                    combatConfig: combat, speeds: speeds, openingDeployments: 2);

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