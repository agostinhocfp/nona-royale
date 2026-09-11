// Assets/_Project/Scripts/Core/Board/PlayerId.cs
namespace NonaRoyale.Core.Board
{
    /// <summary>
    /// The four board positions. The underlying value is the quarter index used
    /// to derive a player's start offset around the circuit, so the numbering is
    /// load-bearing: do not reorder.
    /// </summary>
    /// <remarks>per ADR-0003: start cells are offset one quarter apart.</remarks>
    public enum PlayerId
    {
        Red = 0,
        Blue = 1,
        Green = 2,
        Yellow = 3
    }

    /// <summary>What kind of position on the board a <see cref="CellRef"/> refers to.</summary>
    public enum CellKind
    {
        /// <summary>Holding area. Not a path position; an operator here has not deployed.</summary>
        Yard = 0,

        /// <summary>A cell on the shared outer circuit. The only kind two players can contest.</summary>
        Track = 1,

        /// <summary>A cell in one colour's home column. Out of the fight entirely.</summary>
        HomeColumn = 2,

        /// <summary>The goal. An operator here is removed from play for the rest of the match.</summary>
        Home = 3
    }
}