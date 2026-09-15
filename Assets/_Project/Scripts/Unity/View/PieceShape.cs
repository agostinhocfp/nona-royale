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

                // Javi, Support: a cross — the medic's mark. The only concave
                // silhouette besides Kian's star, and the read is instant.
                case "Javi": return Primitives.Cross;

                // Kian, Artillery: a five-pointed star, point up. Sharp
                // corners from every angle for the operator who can strike
                // from anywhere on the board.
                case "Kian": return Primitives.Star(5, 90f);

                // Sanity, Engineer: an octagon — the nearest a polygon gets to
                // a solid block of machinery. At twelve health he is also the
                // largest piece on the board, so the hulking read comes from
                // size and the silhouette only has to stay distinct from
                // Bouncer's hexagon at the same end of the scale.
                case "Sanity": return Primitives.Polygon(8, 0f);

                // Luka, Duelist: a four-pointed star turned to an X — a
                // thrown blade. Turned, because on the axes its points would
                // read as Javi's cross; four points, because five is Kian's.
                case "Luka": return Primitives.Star(4, 45f);

                default: return Primitives.Disc;
            }
        }

        /// <summary>Scale relative to a board cell, taken from maximum health.</summary>
        public static float SizeFor(OperatorState op) =>
            Mathf.Lerp(0.52f, 0.84f, Mathf.InverseLerp(SmallestHealth, LargestHealth, op.MaxHealth));
    }
}