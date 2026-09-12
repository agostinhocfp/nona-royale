// Assets/_Project/Scripts/Core/Board/BoardProfile.cs
using System;

namespace NonaRoyale.Core.Board
{
    /// <summary>
    /// The dimensions of a board, as config data rather than literals
    /// (CONVENTIONS: "Config, not literals"). Swapping profiles is a data
    /// change, never a code change — ADR-0002 depends on that being true, and
    /// the simulation harness exercises it.
    /// </summary>
    /// <remarks>
    /// ADR-0002 states the constraint as
    /// <c>HomeColumnLength = PlayerStartOffset / 2</c>, implying one integer
    /// defines a board. That is <b>not</b> generally true: it only holds when
    /// <see cref="CircuitLength"/> happens to divide evenly. It holds for 24, 48
    /// and 60, but not for the 36 fallback in the ADR's own table: the rule gives
    /// a 4-cell column where the table says 5. So both values are supplied
    /// explicitly here and validated, rather than derived and silently
    /// truncated. The halving rule remains a good guideline for
    /// *choosing* a home-column length — it keeps the column the same length as
    /// the arm, which is what makes the cross look square.
    /// </remarks>
    public sealed class BoardProfile
    {
        /// <summary>Seats on the board. Fixed by the four-arm cross topology (ADR-0003).</summary>
        public const int PlayerCount = 4;

        /// <summary>Minimum viable circuit: four seats, two cells of travel each.</summary>
        public const int MinimumCircuitLength = 8;

        public string Name { get; }

        /// <summary>Cells in the shared outer loop. Must be divisible by 4 so the four starts are evenly spaced.</summary>
        public int CircuitLength { get; }

        /// <summary>Cells in each colour's private home column, mouth to HOME.</summary>
        public int HomeColumnLength { get; }

        public BoardProfile(string name, int circuitLength, int homeColumnLength, int laps = 1)
        {
            if (laps < 1) throw new ArgumentOutOfRangeException(nameof(laps));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A board profile needs a name.", nameof(name));

            if (circuitLength < MinimumCircuitLength)
                throw new ArgumentOutOfRangeException(nameof(circuitLength),
                    $"Circuit length must be at least {MinimumCircuitLength}, was {circuitLength}.");

            // Per ADR-0003: start cells sit one quarter of the loop apart. An
            // indivisible circuit would space them unevenly and silently give
            // one seat a shorter journey than the others.
            if (circuitLength % PlayerCount != 0)
                throw new ArgumentException(
                    $"Circuit length must be divisible by {PlayerCount} so the start cells are evenly spaced; was {circuitLength}.",
                    nameof(circuitLength));

            if (homeColumnLength < 1)
                throw new ArgumentOutOfRangeException(nameof(homeColumnLength),
                    $"Home column must hold at least one cell, was {homeColumnLength}.");

            Name = name;
            CircuitLength = circuitLength;
            HomeColumnLength = homeColumnLength;
            Laps = laps;
        }

        /// <summary>
        /// Circuits an operator must complete before turning into its home
        /// column. More than one keeps operators on the board longer without
        /// making any single move less readable.
        /// </summary>
        public int Laps { get; }

        /// <summary>Cells of outer track an operator traverses in total.</summary>
        public int TrackLength => CircuitLength * Laps;

        /// <summary>Distance between adjacent colours' start cells, in track steps.</summary>
        public int PlayerStartOffset => CircuitLength / PlayerCount;

        /// <summary>
        /// Cells an operator traverses from its start cell to HOME: the full
        /// loop, then its home column. Progress at or beyond this value means
        /// the operator has finished.
        /// </summary>
        public int Journey => TrackLength + HomeColumnLength;

        /// <summary>
        /// Total distinct positions on the board — the shared loop plus four
        /// private home columns. 72 on the Standard profile (ADR-0002).
        /// </summary>
        public int TotalPathPositions => CircuitLength + (PlayerCount * HomeColumnLength);

        /// <summary>
        /// Builds a profile from a single integer, deriving the home column by
        /// halving the arm — the rule that keeps the cross square, because the
        /// arm and its centre column then read as one shape.
        /// </summary>
        /// <remarks>
        /// A convenience for the common case, <b>not</b> the definition of a
        /// valid board. It only applies where the arm halves evenly: 24, 32, 40,
        /// 48 and 56 all work; 36, 52 and 60 do not, and are rejected here.
        ///
        /// Rejection means "this shortcut does not reach that board", never
        /// "that board is invalid" — 36/5 and 60/7 are real profiles named in
        /// ADR-0002 and measured by the simulation harness, and classic Ludo
        /// itself is 52/6. Use the constructor and state both numbers for those.
        /// </remarks>
        public static BoardProfile FromCircuitLength(string name, int circuitLength)
        {
            if (circuitLength >= MinimumCircuitLength && circuitLength % PlayerCount == 0)
            {
                int arm = circuitLength / PlayerCount;
                if (arm % 2 != 0)
                    throw new ArgumentException(
                        $"Circuit {circuitLength} gives an arm of {arm}, which does not halve into a " +
                        "whole home column. That board is legal — this shortcut just cannot derive it. " +
                        "Use the constructor and state the home column length explicitly.",
                        nameof(circuitLength));

                return new BoardProfile(name, circuitLength, arm / 2);
            }

            // Fall through so the constructor raises the one canonical message
            // for a bad circuit, rather than duplicating its wording here.
            return new BoardProfile(name, circuitLength, 1);
        }

        /// <summary>The shipping board (ADR-0002). Measures ~16.7 turns at four players.</summary>
        public static BoardProfile Standard { get; } = new BoardProfile("Standard", 48, 6);

        /// <summary>
        /// Combat-iteration board, not a product. Everyone stays permanently in
        /// range, which yields roughly four times the ability-resolution reps
        /// per hour. Same topology, fewer cells — needs no new art.
        /// </summary>
        public static BoardProfile Sprint { get; } = new BoardProfile("Sprint", 24, 3);

        /// <summary>Retained for measurement only. Measures ~29.8 turns; not a shipping candidate.</summary>
        public static BoardProfile Long { get; } = new BoardProfile("Long", 60, 7);

        public override string ToString() =>
            Laps == 1
                ? $"{Name} ({CircuitLength}/{HomeColumnLength}, journey {Journey})"
                : $"{Name} ({CircuitLength}x{Laps}/{HomeColumnLength}, journey {Journey})";
    }
}