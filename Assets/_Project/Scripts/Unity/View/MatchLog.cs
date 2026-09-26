// Assets/_Project/Scripts/Unity/View/MatchLog.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Text;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The match in the player's words, turn by turn (LAUNCH_UI_PASS.md, G7b):
    /// what the event log shows.
    /// </summary>
    /// <remarks>
    /// <b>Fed where the history strip is fed</b>, when a batch settles, so a
    /// line never appears ahead of the thing it describes on the board.
    ///
    /// <b>Every line is <see cref="EventText"/>'s</b>, drawn by
    /// <see cref="RulesMarkup"/>: names in their seat's colour, values bright,
    /// statuses in their board colours. The raw engine lines stay in
    /// MatchBootstrap's developer log, for the dev panel.
    ///
    /// A turn opens at each <c>TurnBegan</c> and carries the round the engine
    /// was on when its batch settled. Only the last <see cref="MaxTurns"/> are kept.
    /// </remarks>
    public sealed class MatchLog
    {
        public const int MaxTurns = 48;

        /// <summary>One line: markup, and whether it is a refusal.</summary>
        public readonly struct Entry
        {
            public Entry(string markup, bool refusal)
            {
                Markup = markup;
                Refusal = refusal;
            }

            public string Markup { get; }
            public bool Refusal { get; }
        }

        /// <summary>One seat's turn and everything that happened in it, in order.</summary>
        public sealed class Turn
        {
            public int Round;

            /// <summary><see cref="PlayerColor.None"/> for lines before the first turn began.</summary>
            public PlayerColor Seat = PlayerColor.None;

            public readonly List<Entry> Lines = new List<Entry>();
        }

        private readonly List<Turn> _turns = new List<Turn>();

        /// <summary>Oldest first.</summary>
        public IReadOnlyList<Turn> Turns => _turns;

        /// <summary>Changes whenever a line is added or the log is cleared.</summary>
        public int Version { get; private set; }

        public void Clear()
        {
            _turns.Clear();
            Version++;
        }

        /// <summary>Adds a settled batch.</summary>
        /// <param name="round">The engine's round once the batch has settled.</param>
        /// <param name="refusals">Whether its refusals are shown. A CPU's are the bot's business (BOTS.md decision 1).</param>
        public void Add(IReadOnlyList<IGameEvent> events, int round, bool refusals)
        {
            if (events == null) return;

            bool changed = false;

            foreach (var e in events)
            {
                if (e is TurnBegan began)
                {
                    _turns.Add(new Turn { Round = round, Seat = began.Player });
                    changed = true;
                    continue;
                }

                bool refusal = e is CommandRejected;
                if (refusal && !refusals) continue;

                var line = EventText.For(e);
                if (line == null) continue;

                Current(round).Lines.Add(new Entry(RulesMarkup.For(line, linked: false), refusal));
                changed = true;
            }

            if (_turns.Count > MaxTurns) _turns.RemoveRange(0, _turns.Count - MaxTurns);
            if (changed) Version++;
        }

        private Turn Current(int round)
        {
            if (_turns.Count == 0) _turns.Add(new Turn { Round = round });
            return _turns[_turns.Count - 1];
        }
    }
}
