// tools/sim/NonaRoyale.Sim/HealthSweep.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Sim
{
    /// <summary>
    /// "Too many deaths" sweep: plays four health/damage configurations of the
    /// alpha three against each other on identical seeds and counts what dies.
    /// </summary>
    /// <remarks>
    /// <b>No core file is touched.</b> Max health has no override path in
    /// <see cref="MatchFactory"/>, so this rebuilds the alpha
    /// <see cref="OperatorDefinition"/>s with adjusted health and passes them in
    /// through the explicit-squads door — the same construction the factory
    /// itself performs. Collision damage comes through <see cref="CombatConfig"/>,
    /// which was already a constructor parameter.
    ///
    /// <b>Seeds are identical across scenarios</b> (0..matches-1), so any row
    /// delta is attributable to the configuration, not the dice. Every seat runs
    /// <see cref="SpendthriftPolicy"/> in the main table — the same player every
    /// other sweep uses — and the second table repeats the
    /// spendthrift-vs-racer 2v2 from <see cref="Policies"/> under each
    /// configuration, because "combat vs racing" is only defined in this harness
    /// as a policy win share.
    /// </remarks>
    public static class HealthSweep
    {
        private const int TurnGuard = 2000;
        private const int PolicyTurnGuard = 4000;

        private static readonly PlayerColor[] Seats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
        };

        private sealed class Scenario
        {
            public string Label;
            public Func<string, int> HealthDelta;   // by operator name
            public CombatConfig Combat;             // null = shipping default
        }

        public static void Run(int matches)
        {
            var scenarios = new[]
            {
                new Scenario { Label = "baseline (9/6/6, coll 3)",
                    HealthDelta = _ => 0, Combat = null },
                new Scenario { Label = "blanket +2 HP (11/8/8)",
                    HealthDelta = _ => 2, Combat = null },
                new Scenario { Label = "floor raise (9/7/7)",
                    HealthDelta = name => name == "Bouncer" ? 0 : 1, Combat = null },
                new Scenario { Label = "collision 3->2 (9/6/6)",
                    HealthDelta = _ => 0, Combat = new CombatConfig(collisionDamage: 2) },
            };

            Console.WriteLine(
                $"\nHEALTH SWEEP — 4 players, alpha three, Standard board, opening 2, " +
                $"{matches} matches per scenario, seeds 0..{matches - 1}, spendthrift on every seat");
            Console.WriteLine(
                $"{"",-28} {"turns",6} {"p90",5} {"deaths",7} {"Boun",6} {"Syla",6} {"Kurb",6} " +
                $"{"coll-k",7} {"abil-k",7} {"1st-death",10} {"d<=10t",7} {"done",5}");

            foreach (var s in scenarios)
            {
                var runs = RunMain(s, matches);
                PrintMain(s.Label, runs);
            }

            Console.WriteLine(
                $"\nCOMBAT VS RACING — spendthrift vs racer 2v2 under each configuration " +
                $"(the only 'combat vs racing' classification this harness has; see Policies)");
            Console.WriteLine($"{"",-28} {"combat",7} {"racing",7} {"turns",6} {"deaths",7} {"unfin",6}");

            foreach (var s in scenarios)
                PrintPolicy(s.Label, RunPolicy(s, matches));
        }

        // ── Main table ───────────────────────────────────────────────────

        private static List<MatchStats> RunMain(Scenario s, int matches)
        {
            var results = new List<MatchStats>(matches);

            for (int i = 0; i < matches; i++)
            {
                var match = Build(s, i);
                var stats = new MatchStats();
                stats.Observe(match.Engine.Start());

                var player = new ScriptedPlayer(match, stats);
                while (!match.Engine.MatchOver && stats.Turns < TurnGuard)
                    player.TakeTurn();

                stats.Completed = match.Engine.MatchOver;
                results.Add(stats);
            }

            return results;
        }

        private static void PrintMain(string label, List<MatchStats> runs)
        {
            var turns = runs.Select(r => (double)r.Turns / Seats.Length).OrderBy(t => t).ToList();

            double PerOp(string name) =>
                runs.Sum(r => r.DeathsByOperator.TryGetValue(name, out int n) ? n : 0) / (double)runs.Count;

            double PerCause(string cause) =>
                runs.Sum(r => r.DeathsByCause.TryGetValue(cause, out int n) ? n : 0) / (double)runs.Count;

            var withDeath = runs.Where(r => r.FirstNeutralizeTurn >= 0).ToList();
            double firstDeath = withDeath.Count == 0 ? -1 : withDeath.Average(r => r.FirstNeutralizeTurn);
            double earlyShare = runs.Count(r => r.FirstNeutralizeTurn >= 0 && r.FirstNeutralizeTurn <= 10 * Seats.Length)
                                * 100.0 / runs.Count;

            Console.WriteLine(
                $"{label,-28} {turns.Average(),6:0.0} {turns[(int)(0.9 * (turns.Count - 1))],5:0} " +
                $"{runs.Average(r => r.Neutralizes),7:0.00} {PerOp("Bouncer"),6:0.00} {PerOp("Syla"),6:0.00} " +
                $"{PerOp("Kurbyn"),6:0.00} {PerCause("collision"),7:0.00} {PerCause("ability"),7:0.00} " +
                $"{firstDeath,10:0.0} {earlyShare,6:0.0}% " +
                $"{runs.Count(r => r.Completed) * 100.0 / runs.Count,4:0}%");
        }

        // ── Policy table ─────────────────────────────────────────────────

        private static (double combatShare, double racerShare, double turns, double deaths, int unfinished)
            RunPolicy(Scenario s, int matches)
        {
            var combat = new SpendthriftPolicy();
            var racer = new RacerPolicy();

            int combatWins = 0, racerWins = 0, unfinished = 0;
            var completed = new List<MatchStats>(matches);

            for (int i = 0; i < matches; i++)
            {
                var policies = new Dictionary<PlayerColor, IEnergyPolicy>();
                for (int seat = 0; seat < Seats.Length; seat++)
                {
                    bool evenSeat = seat % 2 == 0;
                    bool flip = i % 2 == 1;
                    policies[Seats[seat]] = evenSeat != flip ? combat : racer;
                }

                var match = Build(s, i);
                var stats = new MatchStats();
                stats.Observe(match.Engine.Start());

                var player = new ScriptedPlayer(match, stats, policies);
                while (!match.Engine.MatchOver && stats.Turns < PolicyTurnGuard)
                    player.TakeTurn();

                if (!stats.Completed || stats.Winner == null) { unfinished++; continue; }

                completed.Add(stats);
                if (ReferenceEquals(policies[stats.Winner.Value], combat)) combatWins++;
                else racerWins++;
            }

            int decided = combatWins + racerWins;
            return (
                decided == 0 ? 0 : 100.0 * combatWins / decided,
                decided == 0 ? 0 : 100.0 * racerWins / decided,
                completed.Count == 0 ? 0 : completed.Average(r => (double)r.Turns / Seats.Length),
                completed.Count == 0 ? 0 : completed.Average(r => r.Neutralizes),
                unfinished);
        }

        private static void PrintPolicy(
            string label, (double combatShare, double racerShare, double turns, double deaths, int unfinished) r)
        {
            Console.WriteLine(
                $"{label,-28} {r.combatShare,6:0.0}% {r.racerShare,6:0.0}% {r.turns,6:0.0} " +
                $"{r.deaths,7:0.00} {r.unfinished,6}");
        }

        // ── Match construction ───────────────────────────────────────────

        /// <summary>
        /// Rebuilds the alpha squad with adjusted max health and hands it to
        /// <see cref="MatchFactory.Create"/> explicitly, replicating the speed
        /// overrides <c>CreateAlphaMatch</c> would have applied.
        /// </summary>
        private static MatchFactory.Match Build(Scenario s, int seed)
        {
            var squad = Roster.Alpha
                .Select(d => new OperatorDefinition(
                    d.Name, d.MaxHealth + s.HealthDelta(d.Name), d.BaseSpeed,
                    d.Abilities, d.Aura, d.Passive, d.PassiveMagnitude))
                .ToList();

            var squads = Seats.ToDictionary(
                seat => seat, seat => (IReadOnlyList<OperatorDefinition>)squad);

            var speeds = RosterSpeeds.Default;
            var overrides = new Dictionary<string, double>
            {
                { "Bouncer", speeds.Bouncer },
                { "Syla", speeds.Syla },
                { "Kurbyn", speeds.KurbynBase }
            };

            return MatchFactory.Create(
                Seats, seed, squads, BoardProfile.Standard,
                combatConfig: s.Combat, speedOverrides: overrides, openingDeployments: 2);
        }
    }
}
