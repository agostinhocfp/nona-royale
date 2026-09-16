// tools/sim/NonaRoyale.Sim/BotSweep.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;

namespace NonaRoyale.Sim
{
    /// <summary>
    /// The game's CPU players (<c>NonaRoyale.Core.Bots</c>) measured against the
    /// scripted players and against each other (BOTS.md decision 10).
    /// </summary>
    /// <remarks>
    /// <b>Nothing here changes <see cref="ScriptedPlayer"/> or its policies.</b>
    /// Every earlier sweep keeps its meaning; this one adds rows beside them.
    ///
    /// <b>What it answers:</b>
    /// <list type="number">
    /// <item>Is each personality better than the scripted player? Two bot seats
    /// against two spendthrift seats, with the bots on alternate diagonals
    /// match to match, so seat order cancels out.</item>
    /// <item>Are the personalities close to each other? Four bot seats, one of
    /// each and a fourth drawn in rotation, random squads.</item>
    /// <item>How does each operator fare when its squad is played well?
    /// Appearances, wins and casts from the all-bot matches. This is the first
    /// read on the operators the scripted players never fielded well.</item>
    /// </list>
    ///
    /// A bot is still not a balance oracle. It plays one way per personality,
    /// and its values are estimates. Treat a large skew as a question, not a
    /// verdict.
    /// </remarks>
    public static class BotSweep
    {
        private const int CommandGuard = 60000;

        private static readonly PlayerColor[] Seats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
        };

        public static void Run(int matches)
        {
            Versus(matches);
            RoundRobin(matches);
        }

        // ── 1. Bots against scripted players ─────────────────────────────

        private static void Versus(int matches)
        {
            Console.WriteLine($"\nBOTS AGAINST SCRIPTED — 2 bots v 2 spendthrift, random squads, opening 2, {matches} matches per row");
            Console.WriteLine($"{"",-10} {"bot win",8} {"turns",6} {"neut",6} {"abil",6} {"done",5}");

            foreach (BotPersonality personality in Enum.GetValues(typeof(BotPersonality)))
            {
                int botWins = 0, completed = 0;
                var runs = new List<MatchStats>();

                for (int seed = 0; seed < matches; seed++)
                {
                    var match = MatchFactory.Create(Seats, seed, openingDeployments: 2);
                    var random = BotConfig.Default.RandomFor(seed);
                    bool redGreen = seed % 2 == 0;

                    var bots = new Dictionary<PlayerColor, IBot>();
                    foreach (var seat in Seats)
                    {
                        bool diagonal = seat == PlayerColor.Red || seat == PlayerColor.Green;
                        if (diagonal == redGreen) bots[seat] = new BotBrain(personality, random);
                    }

                    var stats = new MatchStats();
                    stats.Observe(match.Engine.Start());

                    var scripted = new ScriptedPlayer(match, stats);
                    var table = new BotTable(match, bots);
                    table.Sent = (s, c, events) => Record(stats, c, events);

                    int guard = 0;
                    while (!match.Engine.MatchOver && guard++ < CommandGuard)
                    {
                        if (table.IsBotTurn) table.Step();
                        else scripted.TakeTurn();
                    }

                    stats.Completed = match.Engine.MatchOver;
                    runs.Add(stats);

                    if (!stats.Completed) continue;
                    completed++;
                    if (bots.ContainsKey(match.Engine.Winner.Value)) botWins++;
                }

                Console.WriteLine(
                    $"{personality,-10} {Percent(botWins, completed),8} {runs.Average(r => r.Turns / 4.0),6:0.0} " +
                    $"{runs.Average(r => r.Neutralizes),6:0.0} {runs.Average(r => r.AbilitiesFired),6:0.0} " +
                    $"{Percent(completed, matches),5}");
            }

            Console.WriteLine("50% is parity with the scripted player. Turns are per seat, both kinds counted.");
        }

        // ── 2. Bots against each other ───────────────────────────────────

        private static void RoundRobin(int matches)
        {
            Console.WriteLine($"\nBOTS AGAINST BOTS — 4 bots, random squads, opening 2, {matches} matches");

            var seatsByPersonality = new Dictionary<BotPersonality, int>();
            var winsByPersonality = new Dictionary<BotPersonality, int>();
            var appearances = new Dictionary<string, int>();
            var wins = new Dictionary<string, int>();
            var casts = new Dictionary<int, int>();
            var runs = new List<MatchStats>();
            int refusals = 0;

            for (int seed = 0; seed < matches; seed++)
            {
                var match = MatchFactory.Create(Seats, seed, openingDeployments: 2);
                var random = BotConfig.Default.RandomFor(seed);

                var bots = new Dictionary<PlayerColor, IBot>();
                for (int i = 0; i < Seats.Length; i++)
                {
                    var personality = (BotPersonality)((i + seed) % 3);
                    bots[Seats[i]] = new BotBrain(personality, random);
                    Bump(seatsByPersonality, personality);
                }

                foreach (var op in match.Operators) Bump(appearances, op.Name);

                var stats = new MatchStats();
                var table = new BotTable(match, bots);
                table.Sent = (s, c, events) =>
                {
                    Record(stats, c, events);
                    if (c is UseAbilityCommand cast && events.Any(e => e is EnergySpent)) Bump(casts, cast.AbilityId);
                };

                var result = table.PlayToEnd(CommandGuard);
                refusals += result.Refusals;
                stats.Completed = result.Finished;
                runs.Add(stats);

                if (!result.Finished) continue;

                var winner = result.Winner.Value;
                Bump(winsByPersonality, ((BotBrain)bots[winner]).Personality);
                foreach (var op in match.Operators)
                    if (op.Owner == winner) Bump(wins, op.Name);
            }

            Console.WriteLine(
                $"turns/seat {runs.Average(r => r.Turns / 4.0):0.0}  neut {runs.Average(r => r.Neutralizes):0.0}  " +
                $"abil {runs.Average(r => r.AbilitiesFired):0.0}  coll {runs.Average(r => r.Collisions):0.0}  " +
                $"done {Percent(runs.Count(r => r.Completed), matches)}  refusals {refusals}");

            Console.WriteLine($"\n{"personality",-12} {"seats",6} {"wins",6} {"win/seat",9}");
            foreach (BotPersonality p in Enum.GetValues(typeof(BotPersonality)))
            {
                int seats = Get(seatsByPersonality, p);
                int won = Get(winsByPersonality, p);
                Console.WriteLine($"{p,-12} {seats,6} {won,6} {Percent(won, seats),9}");
            }
            Console.WriteLine("25% is an average seat at a four-player table.");

            Console.WriteLine($"\n{"operator",-10} {"fielded",8} {"on winner",10} {"win share",10}");
            foreach (var name in appearances.Keys.OrderByDescending(n => Rate(Get(wins, n), appearances[n])))
            {
                Console.WriteLine(
                    $"{name,-10} {appearances[name],8} {Get(wins, name),10} {Percent(Get(wins, name), appearances[name]),10}");
            }
            Console.WriteLine("Win share: how often a squad fielding the operator won. 25% is neutral.");

            Console.WriteLine($"\n{"ability",-20} {"casts/match",12}");
            foreach (var ability in Roster.AllAbilities.OrderByDescending(a => Get(casts, a.Id)))
                Console.WriteLine($"{ability.Name,-20} {Get(casts, ability.Id) / (double)matches,12:0.00}");
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static void Record(MatchStats stats, ICommand command, IReadOnlyList<IGameEvent> events)
        {
            stats.Observe(events);
            if (command is EndTurnCommand && events.Any(e => e is TurnBegan || e is GameWon)) stats.Turns++;
        }

        private static void Bump<T>(Dictionary<T, int> counts, T key) => counts[key] = Get(counts, key) + 1;

        private static int Get<T>(Dictionary<T, int> counts, T key) => counts.TryGetValue(key, out var n) ? n : 0;

        private static double Rate(int part, int whole) => whole == 0 ? 0.0 : (double)part / whole;

        private static string Percent(int part, int whole) => $"{Rate(part, whole) * 100:0}%";
    }
}