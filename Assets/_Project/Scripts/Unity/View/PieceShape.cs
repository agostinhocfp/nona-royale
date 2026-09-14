// Assets/_Project/Scripts/Unity/View/PieceShape.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Which shape and size an operator is drawn at.
    /// </summary>
    /// <remarks>
    /// <b>Colour is already spoken for.</b> Each seat owns a colour, so three
    /// operators of the same colour need a second channel to be told apart.
    /// Silhouette is the right one — `ART_DIRECTION` §5 already requires every
    /// operator to be identifiable in pure black at board scale, so this
    /// prototype is following the art bible's constraint rather than inventing a
    /// throwaway one.
    ///
    /// Size carries health, which means the tank reads as the tank without a
    /// bar over its head.
    /// </remarks>
    public static class PieceShape
    {
        /// <summary>
        /// Health at the smallest silhouette, and at the largest.
        /// </summary>
        /// <remarks>
        /// The floor tracks the frailest operator on the roster rather than a
        /// round number. It was 6 while the alpha three were the whole roster,
        /// which drew Mimi's 5 health at exactly Syla's size — hiding the single
        /// most important thing about her. Drop it again if anything ever ships
        /// below 5.
        /// </remarks>
        private const float SmallestHealth = 5f;
        private const float LargestHealth = 12f;

        public static Sprite For(OperatorState op)
        {
            switch (op.Name)
            {
                // Bouncer, Tank: broad and blunt. Hardest silhouette to move past.
                case "Bouncer": return Primitives.Polygon(6, 0f);

                // Syla, Assassin: a point. Reads sharp even at two-thirds size.
                case "Syla": return Primitives.Polygon(3, 90f);

                // Kurbyn, Brawler: a diamond — a square stood on its corner.
                // Zero rotation, not 45: the generator puts a 4-gon's vertices
                // on the axes already, so 45 would square it up.
                case "Kurbyn": return Primitives.Polygon(4, 0f);

                // Mimi, Controller: a pentagon, point up. The only odd-sided
                // shape besides Syla's triangle, and the two are hard to confuse
                // because size separates them — she is the smallest piece on the
                // board and Syla is not.
                case "Mimi": return Primitives.Polygon(5, 90f);

                default: return Primitives.Disc;
            }
        }

        /// <summary>Scale relative to a board cell, taken from maximum health.</summary>
        public static float SizeFor(OperatorState op) =>
            Mathf.Lerp(0.52f, 0.84f, Mathf.InverseLerp(SmallestHealth, LargestHealth, op.MaxHealth));
    }
}