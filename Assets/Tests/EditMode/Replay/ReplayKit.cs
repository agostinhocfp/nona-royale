// Assets/Tests/EditMode/Replay/ReplayKit.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Replay;

namespace NonaRoyale.Core.Tests.Replay
{
    /// <summary>
    /// Shared set-up for the replay tests: recipes that cover every squad
    /// source, board and side map, and whole bot matches played on them.
    /// </summary>
    internal static class ReplayKit
    {
        public const string Created = "2026-09-22T00:00:00Z";

        private static readonly PlayerColor[] Four =
            { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet };

        /// <summary>Two seats sit opposite, as setup suggests; three and four fill in table order.</summary>
        public static PlayerColor[] SeatsFor(int count)
        {
            if (count == 2) return new[] { PlayerColor.Red, PlayerColor.Green };

            var seats = new PlayerColor[count];
            for (int i = 0; i < count; i++) seats[i] = Four[i];
            return seats;
        }

        /// <summary>
        /// A recipe that varies with the seed: the squad source cycles through
        /// all three, a full table plays crossed pairs on even seeds, every
        /// fifth seed uses the view's compact two-lap board, and the opening
        /// deployments run 0 to 2.
        /// </summary>
        public static MatchRecipe RecipeFor(int seed, int seatCount)
        {
            var seats = SeatsFor(seatCount);
            var source = (SquadSource)(seed % 3);

            Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> squads = null;
            if (source == SquadSource.Drafted)
            {
                squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>();
                for (int i = 0; i < seats.Length; i++)
                {
                    var squad = new List<OperatorDefinition>();
                    for (int k = 0; k < Roster.SquadSize; k++)
                        squad.Add(Roster.All[(seed + i * Roster.SquadSize + k) % Roster.All.Count]);
                    squads[seats[i]] = squad;
                }
            }

            var board = seed % 5 == 0 ? BoardProfile.Cross("Compact", 3, laps: 2) : BoardProfile.Standard;
            var teams = seatCount == 4 && seed % 2 == 0 ? TeamMap.CrossedPairs : TeamMap.FreeForAll;

            return new MatchRecipe(seats, seed, source, squads, board, seed % 3, teams);
        }

        /// <summary>A whole bot match, and what it produced.</summary>
        public sealed class Played
        {
            public MatchFactory.Match Match;
            public ReplayRecorder Recorder;
            public BotRunResult Run;

            /// <summary>Every event of the match's start and of each accepted command, as text.</summary>
            public readonly List<string> Events = new List<string>();

            /// <summary><c>Events.Count</c> after the start (index 0) and after each accepted command.</summary>
            public readonly List<int> Boundaries = new List<int>();

            /// <summary>Every command sent, accepted or refused, with the seat that sent it.</summary>
            public readonly List<PlayerColor> SentBy = new List<PlayerColor>();

            /// <summary>The seats the engine reported through <c>Executed</c>, one per command.</summary>
            public readonly List<PlayerColor> ExecutedBy = new List<PlayerColor>();

            public int Refusals;
        }

        public static Played PlayBots(MatchRecipe recipe, int personalityOffset, bool record = true)
        {
            var played = new Played { Match = recipe.Build() };
            var match = played.Match;

            if (record) played.Recorder = new ReplayRecorder(match, recipe, Created);

            match.Engine.Executed += (seat, command, events) => played.ExecutedBy.Add(seat);

            var random = BotConfig.Default.RandomFor(recipe.Seed);
            var bots = new Dictionary<PlayerColor, IBot>();
            for (int i = 0; i < recipe.Seats.Count; i++)
                bots[recipe.Seats[i]] = new BotBrain((BotPersonality)((i + personalityOffset) % 3), random);

            var table = new BotTable(match, bots);
            table.Sent = (seat, command, events) =>
            {
                played.SentBy.Add(seat);

                if (HasRejection(events))
                {
                    played.Refusals++;
                    return;
                }

                AddAll(played.Events, events);
                played.Boundaries.Add(played.Events.Count);
            };

            AddAll(played.Events, match.Engine.Start());
            played.Boundaries.Add(played.Events.Count);

            played.Run = table.PlayToEnd(start: false);
            return played;
        }

        public static bool HasRejection(IReadOnlyList<IGameEvent> events)
        {
            foreach (var e in events)
                if (e is CommandRejected) return true;
            return false;
        }

        public static List<string> Texts(IReadOnlyList<IGameEvent> events)
        {
            var texts = new List<string>(events.Count);
            AddAll(texts, events);
            return texts;
        }

        private static void AddAll(List<string> into, IReadOnlyList<IGameEvent> events)
        {
            foreach (var e in events) into.Add(e.GetType().Name + ": " + e);
        }

        /// <summary>The first index where two event streams differ, or -1 when they are identical.</summary>
        public static int FirstDifference(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            int n = a.Count < b.Count ? a.Count : b.Count;
            for (int i = 0; i < n; i++)
                if (a[i] != b[i]) return i;
            return a.Count == b.Count ? -1 : n;
        }

        /// <summary>The event at <paramref name="index"/>, or "(end)" past the last one.</summary>
        public static string At(IReadOnlyList<string> events, int index) =>
            index >= 0 && index < events.Count ? events[index] : "(end)";

        /// <summary>Health and progress of every operator, as one line of text.</summary>
        public static string StateOf(MatchFactory.Match match)
        {
            var parts = new List<string>();
            foreach (var op in match.Operators)
                parts.Add($"{op.Id}:{op.Name}:{op.Health}hp@{op.Progress}");

            parts.Add($"round {match.Engine.Round}");
            parts.Add($"winner {match.Engine.Winner}");
            foreach (var player in match.Players) parts.Add($"{player.Color} {player.Energy}e");

            return string.Join(" | ", parts);
        }
    }
}