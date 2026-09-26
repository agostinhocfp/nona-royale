// Assets/_Project/Scripts/Core/Text/RulesLine.cs
using System;
using System.Collections.Generic;
using System.Text;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Core.Text
{
    /// <summary>What one piece of a rules line is (OPERATOR_GUIDE.md §2).</summary>
    public enum RunKind
    {
        /// <summary>Words.</summary>
        Text = 0,

        /// <summary>A value read from the definitions: a damage figure, a duration, a radius.</summary>
        Number = 1,

        /// <summary>A term with a glossary entry: a status, a damage type, a board word.</summary>
        Keyword = 2,

        /// <summary>
        /// A name that belongs to a seat: an operator's, or the seat's own
        /// (G7b). The view draws it in that seat's colour.
        /// </summary>
        Named = 3
    }

    /// <summary>
    /// One piece of a rules line. The core decides the words, the values and
    /// which of them are keywords; the view decides how each kind looks.
    /// </summary>
    public readonly struct RulesRun
    {
        public RulesRun(RunKind kind, string text, string keyword = null, PlayerColor seat = PlayerColor.None)
        {
            Kind = kind;
            Text = text ?? "";
            Keyword = keyword;
            Seat = seat;
        }

        public RunKind Kind { get; }

        /// <summary>What is shown.</summary>
        public string Text { get; }

        /// <summary>The glossary id a <see cref="RunKind.Keyword"/> run links to; null otherwise.</summary>
        public string Keyword { get; }

        /// <summary>The seat a <see cref="RunKind.Named"/> run belongs to; <see cref="PlayerColor.None"/> otherwise.</summary>
        public PlayerColor Seat { get; }

        public override string ToString() => Text;
    }

    /// <summary>
    /// A rules line: generated text about an ability, a passive or an aura,
    /// as runs rather than a finished string (OPERATOR_GUIDE.md D2, §2).
    /// </summary>
    /// <remarks>
    /// <b>Never a finished string</b>, because the same line is drawn in three
    /// places with three different needs: the dossier links its keywords, the
    /// draft panel colours them, and the tests read it plain. A string with
    /// markup already baked in would have to be parsed back apart by two of
    /// them.
    /// </remarks>
    public sealed class RulesLine
    {
        private readonly List<RulesRun> _runs = new List<RulesRun>();

        public IReadOnlyList<RulesRun> Runs => _runs;

        /// <summary>True when nothing has been written.</summary>
        public bool IsEmpty => _runs.Count == 0;

        /// <summary>
        /// Whether the formatter met something it could not describe. A test
        /// fails on this (§6): a silent fallback is how a guide quietly lies.
        /// </summary>
        public bool HasUnwritten
        {
            get
            {
                foreach (var run in _runs)
                    if (run.Text.StartsWith(RulesText.UnwrittenPrefix, StringComparison.Ordinal)) return true;
                return false;
            }
        }

        /// <summary>Every glossary id this line links to, in order, without repeats.</summary>
        public IReadOnlyList<string> Keywords
        {
            get
            {
                var seen = new List<string>();
                foreach (var run in _runs)
                    if (run.Kind == RunKind.Keyword && run.Keyword != null && !seen.Contains(run.Keyword))
                        seen.Add(run.Keyword);
                return seen;
            }
        }

        public RulesLine Text(string text)
        {
            if (!string.IsNullOrEmpty(text)) _runs.Add(new RulesRun(RunKind.Text, text));
            return this;
        }

        public RulesLine Number(int value) => Number(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public RulesLine Number(string value)
        {
            _runs.Add(new RulesRun(RunKind.Number, value));
            return this;
        }

        public RulesLine Keyword(string text, string keyword)
        {
            _runs.Add(new RulesRun(RunKind.Keyword, text, keyword));
            return this;
        }

        /// <summary>A name drawn in its seat's colour: an operator's, or the seat's own.</summary>
        public RulesLine Named(string text, PlayerColor seat)
        {
            _runs.Add(new RulesRun(RunKind.Named, text, null, seat));
            return this;
        }

        /// <summary>Appends another line's runs.</summary>
        public RulesLine Append(RulesLine other)
        {
            if (other != null) _runs.AddRange(other._runs);
            return this;
        }

        /// <summary>The line as plain text. For tests, logs and anything that cannot draw markup.</summary>
        public string ToPlainText()
        {
            var text = new StringBuilder();
            foreach (var run in _runs) text.Append(run.Text);
            return text.ToString();
        }

        public override string ToString() => ToPlainText();
    }
}
