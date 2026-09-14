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
    /// ADR-0002 originally stated the constraint as
    /// <c>HomeColumnLength = PlayerStartOffset / 2</c>, implying one integer
    /// defines a board. That rule is <b>dead</b> as of Amendment 6: it does not
    /// describe a drawable Ludo cross. The real family is
    /// <c>CircuitLength = 8L + 4, HomeColumnLength = L</c> for an arm length L —
    /// see <see cref="Cross"/>, and <c>BoardLayout</c> for the derivation.
    ///
    /// Both values are still supplied explicitly and validated here rather than
    /// derived, because the rules do not care about arm geometry and nothing in
    /// the core should start caring. The constructor therefore stays open to any
    /// circuit divisible by four: the harness measures boards that will never be
    /// drawn, and <c>BoardLayout</c> is where a board has to be drawable.
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
        /// private home columns. 76 on the Standard profile (ADR-0002 Amdt 5).
        /// </summary>
        public int TotalPathPositions => CircuitLength + (PlayerCount * HomeColumnLength);

        /// <summary>
        /// Builds the board that arm length <paramref name="armLength"/> draws as
        /// a continuous Ludo cross: <c>8L + 4</c> outer cells and a home column
        /// of <c>L</c>, on a <c>2L + 3</c> grid.
        /// </summary>
        /// <remarks>
        /// This is the only family <c>BoardLayout</c> can render, and the family
        /// classic Ludo belongs to (L=6 gives 52/6 on 15x15). Prefer it to the
        /// constructor for any board intended to ship or be drawn.
        /// </remarks>
        public static BoardProfile Cross(string name, int armLength, int laps = 1)
        {
            if (armLength < 1)
                throw new ArgumentOutOfRangeException(nameof(armLength),
                    $"Arm length must be at least 1, was {armLength}.");

            return new BoardProfile(name, 8 * armLength + 4, armLength, laps);
        }

        /// <summary>
        /// Builds a profile from a single integer by halving the arm.
        /// </summary>
        /// <remarks>
        /// <b>Superseded by <see cref="Cross"/>; kept for the harness only.</b>
        /// The halving rule produces boards that cannot be drawn as a cross — it
        /// is what produced the 48/6 Standard, whose loop had to jump a cell at
        /// each arm tip (ADR-0002 Amendment 6). Do not use it for a shipping
        /// board.
        /// </remarks>
        public static BoardProfile FromCircuitLength(string name, int circuitLength)
        {
            if (circuitLength >= MinimumCircuitLength && circuitLength % PlayerCount == 0)
            {
                int arm = circuitLength / PlayerCount;
                if (arm % 2 != 0)
                    throw new ArgumentException(
                        $"Circuit {circuitLength} gives an arm of {arm}, which does not halve into a " +
                        "whole home column. Use BoardProfile.Cross(name, armLength) instead.",
                        nameof(circuitLength));

                return new BoardProfile(name, circuitLength, arm / 2);
            }

            // Fall through so the constructor raises the one canonical message
            // for a bad circuit, rather than duplicating its wording here.
            return new BoardProfile(name, circuitLength, 1);
        }

        /// <summary>
        /// The shipping board (ADR-0002 Amendment 6): classic Ludo, 52/6 on a
        /// 15x15 grid, journey 58. Was 48/6, which could not be drawn without a
        /// gap at each arm tip.
        /// </summary>
        public static BoardProfile Standard { get; } = Cross("Standard", 6);

        /// <summary>
        /// Combat-iteration board, not a product. Everyone stays permanently in
        /// range, which yields roughly four times the ability-resolution reps
        /// per hour. Same topology, fewer cells — needs no new art.
        /// 28/3 on 9x9; was 24/3, which is not a drawable cross.
        /// </summary>
        public static BoardProfile Sprint { get; } = Cross("Sprint", 3);

        /// <summary>
        /// Retained for measurement only; not a shipping candidate. 60/7 on
        /// 17x17 — already a valid cross, unchanged by Amendment 6.
        /// </summary>
        public static BoardProfile Long { get; } = Cross("Long", 7);

        public override string ToString() =>
            Laps == 1
                ? $"{Name} ({CircuitLength}/{HomeColumnLength}, journey {Journey})"
                : $"{Name} ({CircuitLength}x{Laps}/{HomeColumnLength}, journey {Journey})";
    }
}