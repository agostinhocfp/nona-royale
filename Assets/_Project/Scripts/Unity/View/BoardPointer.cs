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
        /// <remarks>
        /// <b>A ray against the board plane, not a screen-point unproject</b>
        /// (VISUAL_PASS.md, V1). Under the flat camera, unprojecting the mouse
        /// and dropping z landed on the board because the view direction is the
        /// z axis. Under the tilted camera it does not: the mouse unprojects to
        /// a point just in front of the lens, and flattening it to z = 0 drops
        /// it straight down instead of following the line of sight, so every
        /// hit test missed. Casting the ray and meeting the plane is right for
        /// both cameras, so there is no mode to branch on here.
        /// </remarks>
        public static bool TryWorldPoint(out Vector3 world)
        {
            world = default;

            var camera = Camera.main;
            if (camera == null) return false;

            var ray = camera.ScreenPointToRay(Input.mousePosition);

            // Looking along or away from the board plane there is no crossing.
            // The flat camera's direction is exactly +z, so this is 1 there.
            if (ray.direction.z <= 0.0001f) return false;

            world = ray.origin + ray.direction * (-ray.origin.z / ray.direction.z);
            world.z = 0f;
            return true;
        }

        /// <summary>
        /// The piece under the pointer: the figure the player can see at
        /// <paramref name="screen"/> when the board is tilted, and otherwise
        /// the one nearest <paramref name="world"/>.
        /// </summary>
        /// <remarks>
        /// <b>Standing figures have to be hit where they are drawn</b>
        /// (VISUAL_PASS.md, V1b). A figure stands up out of the cell it
        /// occupies, so a click on its chest becomes a board point a cell or so
        /// behind its feet, and the world test below would select the wrong
        /// piece or none. When two figures overlap the nearer one wins, which
        /// is the one the player sees in front.
        ///
        /// The world test is still the fallback, so clicking the seat disc at a
        /// figure's feet works either way, and the flat camera is unchanged.
        /// </remarks>
        public static OperatorPiece PieceAt(
            Vector3 world, Vector2 screen, IReadOnlyList<OperatorPiece> pieces, float minRadius)
        {
            if (BoardTilt.IsTilted)
            {
                var camera = Camera.main;
                OperatorPiece front = null;
                float nearest = float.MaxValue;

                foreach (var piece in pieces)
                {
                    if (piece == null) continue;
                    if (!piece.TryScreenBounds(camera, out var rect) || !rect.Contains(screen)) continue;

                    float depth = piece.transform.position.y;
                    if (depth >= nearest) continue;

                    nearest = depth;
                    front = piece;
                }

                if (front != null) return front;
            }

            return PieceAt(world, pieces, minRadius);
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
