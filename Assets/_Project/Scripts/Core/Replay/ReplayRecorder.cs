// Assets/_Project/Scripts/Core/Replay/ReplayRecorder.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;

namespace NonaRoyale.Core.Replay
{
    /// <summary>
    /// Listens to a match's engine and keeps every accepted command, in order.
    /// </summary>
    /// <remarks>
    /// <b>Only accepted commands are kept (REPLAY.md).</b> A refused command
    /// changes nothing — every refusal path in the engine adds its one
    /// <see cref="CommandRejected"/> and returns before touching state — so
    /// recording it would be noise the player would then have to skip.
    ///
    /// <b>The recorder reads and never writes.</b> It holds no RNG and sends
    /// no commands; attaching one cannot change the match it records, which a
    /// test pins by playing the same seed with and without one.
    ///
    /// <b>Attach it before the first command.</b> It sees commands from the
    /// moment it subscribes; one attached mid-match records a file that starts
    /// in the middle, which cannot replay.
    ///
    /// <b><see cref="LineRecorded"/> is for a store that appends.</b> It fires
    /// with the finished text of each new line, newline included, so the file
    /// on disk grows one command at a time and a crash keeps everything but
    /// the command in flight.
    /// </remarks>
    public sealed class ReplayRecorder : IDisposable
    {
        private readonly GameEngine _engine;
        private readonly List<ReplayEntry> _entries = new List<ReplayEntry>();
        private bool _disposed;

        public ReplayRecorder(
            MatchFactory.Match match,
            MatchRecipe recipe,
            string createdUtc,
            IReadOnlyDictionary<PlayerColor, string> seatLabels = null,
            string rulesHash = null)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));

            Header = new ReplayHeader(
                ReplayHeader.CurrentFormat,
                rulesHash ?? RulesFingerprint.Current,
                createdUtc,
                recipe,
                ReplayWriter.FieldedBy(match),
                seatLabels);

            HeaderLine = ReplayWriter.HeaderLine(Header);

            _engine = match.Engine;
            _engine.Executed += OnExecuted;
        }

        public ReplayHeader Header { get; }

        /// <summary>Line 1 of the file, newline included. Written before any command line.</summary>
        public string HeaderLine { get; }

        public IReadOnlyList<ReplayEntry> Entries => _entries;

        /// <summary>Raised with each new command line, newline included.</summary>
        public event Action<string> LineRecorded;

        /// <summary>Everything recorded so far, as a file.</summary>
        public ReplayFile ToFile() => new ReplayFile(Header, _entries.ToArray());

        /// <summary>Everything recorded so far, as text.</summary>
        public string ToText() => ReplayWriter.Write(ToFile());

        /// <summary>Stops listening. The entries recorded so far stay readable.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _engine.Executed -= OnExecuted;
        }

        private void OnExecuted(PlayerColor seat, ICommand command, IReadOnlyList<IGameEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
                if (events[i] is CommandRejected) return;

            var entry = new ReplayEntry(_entries.Count + 1, seat, command);
            _entries.Add(entry);

            var handler = LineRecorded;
            if (handler != null) handler(ReplayWriter.EntryLine(entry));
        }
    }
}