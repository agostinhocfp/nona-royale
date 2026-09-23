// Assets/_Project/Scripts/Core/Replay/ReplayPlayer.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Events;

namespace NonaRoyale.Core.Replay
{
    /// <summary>What playing a replay produced: the rebuilt match and every event on the way.</summary>
    public sealed class ReplayResult
    {
        public ReplayResult(MatchFactory.Match match, IReadOnlyList<IGameEvent> events, int commandsPlayed, bool truncated)
        {
            Match = match;
            Events = events;
            CommandsPlayed = commandsPlayed;
            Truncated = truncated;
        }

        /// <summary>The match, in the state the last played command left it.</summary>
        public MatchFactory.Match Match { get; }

        /// <summary>Every event, from <c>Start</c> through the last played command.</summary>
        public IReadOnlyList<IGameEvent> Events { get; }

        public int CommandsPlayed { get; }

        /// <summary>
        /// Carried over from the file: its last line was cut short, so this is
        /// as far as the recording reached, not necessarily where the match
        /// ended.
        /// </summary>
        public bool Truncated { get; }
    }

    /// <summary>
    /// Rebuilds a recorded match from its recipe and feeds it the recorded
    /// commands, refusing anything that does not match.
    /// </summary>
    /// <remarks>
    /// <b>Refusals come in two kinds.</b> Before the first command:
    /// <see cref="ReplayIncompatibleException"/> when the rules hash differs,
    /// naming both, and <see cref="ReplayDesyncException"/> with sequence 0
    /// when the rebuilt squads differ from the fielded ones. During play:
    /// <see cref="ReplayDesyncException"/> when a command comes from the wrong
    /// seat or is refused by the engine, naming the command and the reason.
    /// Either way nothing is returned, because a partial replay handed back as
    /// a result would read as a match that ended early.
    ///
    /// <see cref="PlayTo"/> is what later tools will rewind with: the state
    /// after command <c>n</c>, for a viewer's step or a win-probability
    /// rollout.
    /// </remarks>
    public static class ReplayPlayer
    {
        public static ReplayResult Play(ReplayFile file, string currentRulesHash = null)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            return PlayTo(file, file.Entries.Count, currentRulesHash);
        }

        /// <summary>Plays the first <paramref name="sequence"/> commands; 0 is the match as dealt.</summary>
        public static ReplayResult PlayTo(ReplayFile file, int sequence, string currentRulesHash = null)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (sequence < 0 || sequence > file.Entries.Count)
                throw new ArgumentOutOfRangeException(nameof(sequence),
                    $"The replay holds commands 1 to {file.Entries.Count}; asked for {sequence}.");

            var header = file.Header;
            string current = currentRulesHash ?? RulesFingerprint.Current;

            if (header.Format > ReplayHeader.CurrentFormat)
                throw new ReplayIncompatibleException(
                    $"replay format {header.Format} is newer than this build reads ({ReplayHeader.CurrentFormat})");

            if (!string.Equals(header.RulesHash, current, StringComparison.Ordinal))
                throw new ReplayIncompatibleException(
                    $"replay recorded under rules {header.RulesHash}; this build's rules are {current}. " +
                    "A config dial or an operator changed since it was recorded.");

            var match = header.Recipe.Build();
            CheckFielded(header, match);

            var events = new List<IGameEvent>(match.Engine.Start());
            var engine = match.Engine;

            for (int i = 0; i < sequence; i++)
            {
                var entry = file.Entries[i];

                var seat = engine.CurrentPlayer != null ? engine.CurrentPlayer.Color : PlayerColor.None;
                if (seat != entry.Seat)
                    throw new ReplayDesyncException(entry.Sequence, entry.Command,
                        $"recorded for {entry.Seat}, but it is {seat}'s turn");

                var produced = engine.Execute(entry.Command);

                foreach (var e in produced)
                {
                    if (e is CommandRejected rejected)
                        throw new ReplayDesyncException(entry.Sequence, entry.Command, $"refused: {rejected.Reason}");
                }

                events.AddRange(produced);
            }

            return new ReplayResult(match, events, sequence,
                truncated: file.Truncated && sequence == file.Entries.Count);
        }

        private static void CheckFielded(ReplayHeader header, MatchFactory.Match match)
        {
            var rebuilt = ReplayWriter.FieldedBy(match);

            foreach (var seat in header.Recipe.Seats)
            {
                var recorded = header.Fielded[seat];
                IReadOnlyList<string> now;
                rebuilt.TryGetValue(seat, out now);

                bool same = now != null && now.Count == recorded.Count;
                for (int i = 0; same && i < now.Count; i++)
                    same = string.Equals(now[i], recorded[i], StringComparison.Ordinal);

                if (!same)
                    throw new ReplayDesyncException(0, null,
                        $"{seat} fielded {string.Join(", ", recorded)} when recorded, " +
                        $"but the rebuilt match fields {(now == null ? "nothing" : string.Join(", ", now))}");
            }
        }
    }
}