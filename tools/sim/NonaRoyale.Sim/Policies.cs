// tools/sim/NonaRoyale.Sim/Policies.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Sim
{
    /// <summary>
    /// Plays three ways of spending energy against each other and counts wins.
    /// </summary>
    /// <remarks>
    /// <b>The question is whether fighting beats racing.</b> The GDD's founding
    /// claim is 70% combat to 30% race, and nothing has ever tested it: every
    /// figure in COMBAT_SYSTEMS §12 measures how long a match runs and how much
    /// combat happens in it, under a single player that spends whatever it can
    /// as soon as it can. None of them answers whether spending was worth it,
    /// because every seat did the same thing.
    ///
    /// <b>Seats are 2v2 and the arrangement alternates by seed.</b> Red always
    /// moves first, so a fixed assignment would measure turn order alongside
    /// policy. Alternating puts each policy in the opening seat half the time.
    ///
    /// <b>Two tables, and the first is the controlled one.</b> Alpha gives every
    /// seat Bouncer, Syla and Kurbyn, so the only difference between two seats is
    /// how they spend. Drafted squads confound policy with roster, and are worth
    /// running only to check the answer survives the full pool — read the second
    /// table as a sanity check on the first, not as a second result.
    ///
    /// <b>What it cannot tell you.</b> A policy is not a player. If the Racer
    /// wins, that says the abilities as scripted here are not worth their energy,
    /// not that combat is worthless — a human picks targets, and this picks the
    /// first legal one. Treat a Racer win as an instruction to build a better
    /// spender before touching a single number on the roster.
    /// </remarks>
    public static class Policies
    {
        private const int TurnGuard = 4000;

        private static readonly PlayerColor[] Seats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow
        };

        public static void Run(int matches)
        {
            var racer = new RacerPolicy();
            var spendthrift = new SpendthriftPolicy();
            var banker = new BankerPolicy();

            var matchups = new[]
            {
                new[] { (IEnergyPolicy)spendthrift, racer },
                new[] { (IEnergyPolicy)banker, racer },
                new[] { (IEnergyPolicy)banker, spendthrift }
            };

            Console.WriteLine(
                $"\nENERGY POLICY — 4 players 2v2, Standard board, opening 2, {matches} matches per row");
            Console.WriteLine("Does spending energy win matches? Every §12 figure was taken under 'spendthrift' alone.\n");

            Header("ALPHA SQUADS — every seat fields Bouncer, Syla, Kurbyn");
            foreach (var pair in matchups) Row(pair[0], pair[1], matches, drafted: false);

            Header("DRAFTED SQUADS — random from the full roster; policy and roster are confounded");
            foreach (var pair in matchups) Row(pair[0], pair[1], matches, drafted: true);
        }

        private static void Header(string title)
        {
            Console.WriteLine($"\n{title}");
            Console.WriteLine(
                $"{"",-30} {"wins",7} {"wins",7} {"turns",6} {"neut",6} {"abil",6} {"unfin",6}");
        }

        private static void Row(IEnergyPolicy a, IEnergyPolicy b, int matches, bool drafted)
        {
            int aWins = 0, bWins = 0, unfinished = 0;
            var completed = new List<MatchStats>(matches);

            for (int i = 0; i < matches; i++)
            {
                // Alternating the arrangement cancels the advantage of moving
                // first, which at four seats is not small.
                var policies = new Dictionary<PlayerColor, IEnergyPolicy>();
                for (int s = 0; s < Seats.Length; s++)
                {
                    bool evenSeat = s % 2 == 0;
                    bool flip = i % 2 == 1;
                    policies[Seats[s]] = evenSeat != flip ? a : b;
                }

                var match = drafted
                    ? MatchFactory.Create(Seats, seed: i, board: BoardProfile.Standard, openingDeployments: 2)
                    : MatchFactory.CreateAlphaMatch(Seats, seed: i, board: BoardProfile.Standard, openingDeployments: 2);

                var stats = new MatchStats();
                stats.Observe(match.Engine.Start());

                var player = new ScriptedPlayer(match, stats, policies);
                while (!match.Engine.MatchOver && stats.Turns < TurnGuard) player.TakeTurn();

                if (!stats.Completed || stats.Winner == null)
                {
                    unfinished++;
                    continue;
                }

                completed.Add(stats);

                if (ReferenceEquals(policies[stats.Winner.Value], a)) aWins++;
                else bWins++;
            }

            int decided = aWins + bWins;
            double aShare = decided == 0 ? 0 : 100.0 * aWins / decided;
            double bShare = decided == 0 ? 0 : 100.0 * bWins / decided;

            string label = $"{a.Name} vs {b.Name}";
            double turns = completed.Count == 0 ? 0 : completed.Average(r => (double)r.Turns / Seats.Length);

            Console.WriteLine(
                $"{label,-30} {aShare,6:0.0}% {bShare,6:0.0}% {turns,6:0.0} " +
                $"{Avg(completed, r => r.Neutralizes),6:0.0} {Avg(completed, r => r.AbilitiesFired),6:0.0} " +
                $"{unfinished,6}");
        }

        private static double Avg(List<MatchStats> runs, Func<MatchStats, int> pick) =>
            runs.Count == 0 ? 0 : runs.Average(r => (double)pick(r));
    }
}