// Assets/_Project/Scripts/Unity/View/BoardPointer.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Turns the mouse position into what it is over on the board: a piece, or
    /// one of a given set of cells.
    /// </summary>
    /// <remarks>
    /// <b>Hit testing only. It decides nothing.</b> Which pieces and cells are
    /// worth clicking comes from the caller, who got it from the engine
    /// (PRESENTATION §1). There are no colliders: pieces and cells are few
    /// enough that the nearest one within a radius is both simpler and more
    /// forgiving than a physics raycast against sprites a third of a cell wide.
    /// </remarks>
    public static class BoardPointer
    {
        /// <summary>
        /// Whether the pointer is over a uGUI element. The EventSystem cannot
        /// see the OnGUI panel; the caller guards that one separately
        /// (ADR-0008 consequence 4).
        /// </summary>
        public static bool IsOverHud =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>The world point under the mouse, on the board plane.</summary>
        public static bool TryWorldPoint(out Vector3 world)
        {
            world = default;

            var camera = Camera.main;
            if (camera == null) return false;

            world = camera.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;
            return true;
        }

        /// <summary>
        /// The piece nearest to <paramref name="world"/>, if it lies within its
        /// own drawn radius or <paramref name="minRadius"/>, whichever is larger.
        /// </summary>
        /// <remarks>
        /// The minimum matters for the smallest pieces: Mimi is drawn at about
        /// half a cell, and a target that small is a miss waiting to happen.
        /// Stacked pieces are fanned apart, so the nearest one is the one the
        /// player aimed at.
        /// </remarks>
        public static OperatorPiece PieceAt(Vector3 world, IReadOnlyList<OperatorPiece> pieces, float minRadius)
        {
            OperatorPiece nearest = null;
            float best = float.MaxValue;

            foreach (var piece in pieces)
            {
                if (piece == null) continue;

                var position = piece.transform.position;
                float dx = position.x - world.x;
                float dy = position.y - world.y;
                float sq = dx * dx + dy * dy;

                float radius = Mathf.Max(minRadius, piece.Radius);
                if (sq > radius * radius || sq >= best) continue;

                best = sq;
                nearest = piece;
            }

            return nearest;
        }

        /// <summary>
        /// The candidate cell nearest to <paramref name="world"/> within
        /// <paramref name="radius"/>, or null.
        /// </summary>
        public static CellRef? CellAt(
            Vector3 world, IEnumerable<CellRef> candidates, BoardLayout layout, float radius)
        {
            CellRef? nearest = null;
            float best = radius * radius;

            foreach (var cell in candidates)
            {
                var position = layout.PositionOf(cell);
                float dx = position.x - world.x;
                float dy = position.y - world.y;
                float sq = dx * dx + dy * dy;

                if (sq >= best) continue;

                best = sq;
                nearest = cell;
            }

            return nearest;
        }
    }
}
