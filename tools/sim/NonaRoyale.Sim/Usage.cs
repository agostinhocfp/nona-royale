// tools/sim/NonaRoyale.Sim/Usage.cs
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
    /// Counts how often each ability is actually cast, per policy.
    /// </summary>
    /// <remarks>
    /// <b>It exists because a nerf produced bit-identical output.</b> Tagged From
    /// Above's cooldown doubled and every spendthrift row in the policy sweep
    /// came back to the digit — 65.9/34.1, 19.1 turns, 3.1 neutralizes, 22.1
    /// abilities, twice over 2000 matches. Identical rows mean an identical
    /// command stream, which means the ability was never cast in the first place.
    /// <c>SpendthriftPolicy</c> fires the first affordable thing it finds and
    /// reaches Bouncer's 6-cost abilities every turn, so it never banks to 9.
    ///
    /// <b>Two conclusions follow, and the second is the larger one.</b> A
    /// before-and-after on a dial is meaningless unless the ability under test is
    /// being cast — and every sweep other than <c>policy</c> runs spendthrift on
    /// all four seats, so every figure in COMBAT_SYSTEMS §12 describes a game in
    /// which the 9-cost tier is not cast at all.
    ///
    /// <b>This is the one measurement that does not need a good player.</b> An
    /// ability nobody casts is a design failure whatever the policy, and the
    /// count is honest even when the policy is not: reading zero against Killzone
    /// tells you something real, where reading a win rate against it would not.
    ///
    /// It reports per <i>policy</i> rather than pooled, because the whole point is
    /// that the two disagree about which half of the roster exists.
    /// </remarks>
    public static class Usage
    {
        private const int TurnGuard = 4000;

        private static readonly PlayerColor[] Seats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
        };

        public static void Run(int matches)
        {
            Console.WriteLine(
                $"\nABILITY USAGE — 4 players, Standard board, opening 2, {matches} matches per policy");
            Console.WriteLine("Casts per match, all seats. A zero means the ability is not in the game.\n");

            Table("ALPHA SQUADS — Bouncer, Syla, Kurbyn", drafted: false, matches: matches);
            Table("DRAFTED SQUADS — random from the full roster", drafted: true, matches: matches);
        }

        private static void Table(string title, bool drafted, int matches)
        {
            var policies = new IEnergyPolicy[] { new SpendthriftPolicy(), new BankerPolicy() };
            var columns = new List<Dictionary<int, int>>();

            foreach (var policy in policies)
                columns.Add(Measure(policy, drafted, matches));

            Console.WriteLine($"\n{title}");
            Console.WriteLine(
                $"{"ability",-26} {"cost",4} {policies[0].Name,13} {policies[1].Name,13}");

            // Every ability in the roster, not only the ones that were cast —
            // a row of zeroes is the finding this sweep exists to produce.
            foreach (var ability in Roster.AllAbilities.OrderBy(a => a.EnergyCost).ThenBy(a => a.Id))
            {
                var cells = columns
                    .Select(c => Per(c, ability.Id, matches))
                    .ToList();

                if (cells.All(v => v == 0.0) && !drafted && !InAlpha(ability)) continue;

                Console.WriteLine(
                    $"{ability.Name,-26} {ability.EnergyCost,4} {cells[0],13:0.00} {cells[1],13:0.00}");
            }
        }

        /// <summary>
        /// Whether an ability belongs to the alpha three. An ability that cannot
        /// be fielded is a different fact from one that is fielded and never
        /// chosen, and the table should not conflate them.
        /// </summary>
        private static bool InAlpha(AbilityDefinition ability)
        {
            foreach (var op in Roster.Alpha)
                foreach (var candidate in op.Abilities)
                    if (candidate.Id == ability.Id) return true;

            return false;
        }

        private static double Per(Dictionary<int, int> counts, int abilityId, int matches)
        {
            int total;
            counts.TryGetValue(abilityId, out total);
            return matches == 0 ? 0.0 : (double)total / matches;
        }

        private static Dictionary<int, int> Measure(IEnergyPolicy policy, bool drafted, int matches)
        {
            var totals = new Dictionary<int, int>();

            var everySeat = new Dictionary<PlayerColor, IEnergyPolicy>();
            foreach (var seat in Seats) everySeat[seat] = policy;

            for (int i = 0; i < matches; i++)
            {
                var match = drafted
                    ? MatchFactory.Create(Seats, seed: i, board: BoardProfile.Standard, openingDeployments: 2)
                    : MatchFactory.CreateAlphaMatch(Seats, seed: i, board: BoardProfile.Standard, openingDeployments: 2);

                var stats = new MatchStats();
                stats.Observe(match.Engine.Start());

                var player = new ScriptedPlayer(match, stats, everySeat);
                while (!match.Engine.MatchOver && stats.Turns < TurnGuard) player.TakeTurn();

                foreach (var pair in stats.CastsByAbility)
                {
                    int running;
                    totals.TryGetValue(pair.Key, out running);
                    totals[pair.Key] = running + pair.Value;
                }
            }

            return totals;
        }
    }
}