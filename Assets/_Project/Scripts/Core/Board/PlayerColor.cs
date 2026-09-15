// Assets/_Project/Scripts/Core/Board/PlayerColor.cs
namespace NonaRoyale.Core.Board
{
    /// <summary>
    /// The four board seats. Numeric values are load-bearing: a colour's start
    /// cell sits at <c>(int)colour * PlayerStartOffset</c> around the outer
    /// track, which is what keeps the four starts evenly spaced (ADR-0003).
    /// </summary>
    public enum PlayerColor
    {
        /// <summary>No owner. Outer-track cells are shared, so they carry this.</summary>
        None = -1,

        Red = 0,
        Blue = 1,
        Green = 2,
        /// <summary>
        /// Was Yellow until 2026-09-15. Violet reads against the gold trim
        /// and the amber aim colour; yellow did not. The value is unchanged.
        /// </summary>
        Violet = 3
    }
}