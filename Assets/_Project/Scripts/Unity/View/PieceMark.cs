// Assets/_Project/Scripts/Unity/View/PieceMark.cs
using System;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a piece is marked for the board-first controls. Flags, because a
    /// piece can be selected and hovered at the same time.
    /// </summary>
    /// <remarks>
    /// The composition root decides the marks from the engine's answers
    /// (<c>CanDeploy</c>, <c>LegalTargetsFor</c>) and hands them to the piece.
    /// The piece only draws them (PRESENTATION §1).
    /// </remarks>
    [Flags]
    public enum PieceMark
    {
        None = 0,

        /// <summary>The operator the player is commanding.</summary>
        Selected = 1 << 0,

        /// <summary>A legal target for the selected ability.</summary>
        Targetable = 1 << 1,

        /// <summary>The chosen target for the selected ability.</summary>
        Target = 1 << 2,

        /// <summary>A yard piece that a click would deploy.</summary>
        Deployable = 1 << 3,

        /// <summary>Under the pointer, and a click on it would do something.</summary>
        Hovered = 1 << 4,
    }
}
