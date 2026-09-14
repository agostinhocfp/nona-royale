// Assets/_Project/Scripts/Core/Board/CellKind.cs
namespace NonaRoyale.Core.Board
{
    /// <summary>The four kinds of position an operator can occupy.</summary>
    public enum CellKind
    {
        /// <summary>Undeployed, in its owner's corner yard.</summary>
        Yard = 0,

        /// <summary>On the shared outer circuit. The only place collisions happen.</summary>
        Track = 1,

        /// <summary>In its own colour's home column. Out of the fight (COMBAT_SYSTEMS §4.3).</summary>
        HomeColumn = 2,

        /// <summary>Finished. Removed from play for the rest of the match.</summary>
        Home = 3
    }
}