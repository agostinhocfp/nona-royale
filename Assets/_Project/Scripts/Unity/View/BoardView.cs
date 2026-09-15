// // Assets/_Project/Scripts/Unity/View/BoardView.cs
// using System.Collections.Generic;
// using NonaRoyale.Core.Board;
// using UnityEngine;

// namespace NonaRoyale.Unity.View
// {
//     /// <summary>
//     /// Draws the cells. Static: it renders the board's shape and never changes
//     /// after construction, because the board itself never changes.
//     /// </summary>
//     public sealed class BoardView : MonoBehaviour
//     {
//         private readonly List<SpriteRenderer> _cells = new List<SpriteRenderer>();

//         public void Build(PathMap map, BoardLayout layout)
//         {
//             foreach (var cell in _cells)
//                 if (cell != null) Destroy(cell.gameObject);

//             _cells.Clear();

//             var profile = map.Profile;

//             for (int i = 0; i < profile.CircuitLength; i++)
//             {
//                 var cell = CellRef.Track(i);

//                 // Safe cells are drawn brighter. They are the only cells with a
//                 // rule attached that a player must be able to see at a glance.
//                 Spawn($"cell_{i}", layout.PositionOf(cell), layout.CellSize,
//                     map.IsSafe(cell) ? new Color(0.95f, 0.9f, 0.6f) : new Color(0.28f, 0.28f, 0.32f));
//             }

//             foreach (PlayerColor colour in new[]
//                      { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow })
//             {
//                 var tint = BoardLayout.ColourOf(colour);

//                 for (int depth = 0; depth < profile.HomeColumnLength; depth++)
//                 {
//                     Spawn($"home_{colour}_{depth}",
//                         layout.PositionOf(CellRef.HomeColumn(colour, depth)),
//                         layout.CellSize * 0.85f, tint * 0.55f);
//                 }

//                 // Kept dim and slightly smaller than before: a yard is a holding
//                 // area, and at 2.4x it overlapped the track cells beside it.
//                 Spawn($"yard_{colour}", layout.PositionOf(CellRef.Yard(colour)),
//                     layout.CellSize * 1.9f, tint * 0.25f);
//             }

//             Spawn("home", Vector3.zero, layout.CellSize * 1.6f, new Color(0.9f, 0.85f, 0.55f));
//         }

//         private void Spawn(string name, Vector3 position, float size, Color colour)
//         {
//             var go = new GameObject(name);
//             go.transform.SetParent(transform, false);
//             go.transform.position = position;
//             go.transform.localScale = Vector3.one * size;

//             var renderer = go.AddComponent<SpriteRenderer>();
//             renderer.sprite = Primitives.Disc;
//             renderer.color = colour;
//             renderer.sortingOrder = 0;

//             _cells.Add(renderer);
//         }
//     }
// }

// Assets/_Project/Scripts/Unity/View/BoardView.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Draws the board once. It renders shape, never state, because the board
    /// itself never changes during a match.
    /// </summary>
    /// <remarks>
    /// <b>The casino floor, moderately</b> (GUI increment G, ART_DIRECTION §6).
    /// A charcoal table with a double gilt frame and a pool of warm light;
    /// track cells as dark marble with a brass inlay; yards as felt tables in
    /// the seat's colour; HOME as a lit vault. §6.1 asks for a board that is
    /// atmospheric at rest and lit on demand; this stops short of the full
    /// whisper, so a stranger can still read the path at rest (decided
    /// 2026-09-15, ahead of the stranger test).
    ///
    /// <b>Safe cells are powered tiles</b>, the one place the board itself
    /// uses the cool register (§6, contrast discipline): marble lifted toward
    /// cyan, a cyan inlay and a faint cyan glow. Each seat's start cell also
    /// carries a wash of the seat's colour.
    ///
    /// <b>Home columns deepen toward HOME</b>, which gives a direction of
    /// travel without arrows, and take a gold inlay: the road to the vault.
    ///
    /// Sorting orders are all below zero. Devices draw at 0, highlights at 1
    /// and 2, pieces from 2 up.
    /// </remarks>
    public sealed class BoardView : MonoBehaviour
    {
        private static readonly PlayerColor[] Seats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow
        };

        // Back to front.
        private const int TableOrder = -12;
        private const int TableTrimOrder = -11;
        private const int FeltOrder = -10;
        private const int FeltRimOrder = -9;
        private const int VaultOrder = -8;
        private const int VaultRaysOrder = -7;
        private const int PowerGlowOrder = -5;
        private const int CellOrder = -4;
        private const int InlayOrder = -3;

        /// <summary>Table border beyond the outermost cells, in cell spacings.</summary>
        private const float TableMargin = 0.7f;

        /// <summary>A yard table's diameter, in cell sizes.</summary>
        private const float YardSize = 3.2f;

        /// <summary>World size of a table corner fan.</summary>
        private const float TableFanSize = 1.5f;

        private readonly List<GameObject> _drawn = new List<GameObject>();

        public void Build(PathMap map, BoardLayout layout)
        {
            foreach (var go in _drawn)
                if (go != null) Destroy(go);

            _drawn.Clear();

            var profile = map.Profile;

            DrawTable(layout);

            foreach (var seat in Seats) DrawYard(layout, seat);

            DrawVault(layout);

            // The shared track.
            var starts = new Dictionary<int, PlayerColor>();
            foreach (var seat in Seats) starts[map.StartTrackIndex(seat)] = seat;

            for (int i = 0; i < profile.CircuitLength; i++)
            {
                var cell = CellRef.Track(i);
                var at = layout.PositionOf(cell);

                if (!map.IsSafe(cell))
                {
                    Tile($"track_{i}", at, layout.CellSize, UiTheme.CellMarble, UiTheme.CellInlay);
                    continue;
                }

                var marble = starts.TryGetValue(i, out var owner)
                    ? Color.Lerp(UiTheme.SafeMarble, UiTheme.Seat(owner), 0.28f)
                    : UiTheme.SafeMarble;

                Sprite($"safe_glow_{i}", DecoSprites.Glow, at, layout.CellSize * 1.5f,
                    UiTheme.WithAlpha(UiTheme.Cyan, 0.22f), PowerGlowOrder);
                Tile($"safe_{i}", at, layout.CellSize, marble, UiTheme.SafeInlay);
            }

            foreach (var seat in Seats)
            {
                var tint = UiTheme.Seat(seat);

                // Home column, tip to centre.
                for (int depth = 0; depth < profile.HomeColumnLength; depth++)
                {
                    float t = depth / Mathf.Max(1f, profile.HomeColumnLength - 1f);
                    float wash = Mathf.Lerp(UiTheme.HomeTintNear, UiTheme.HomeTintFar, t);

                    Tile($"home_{seat}_{depth}",
                        layout.PositionOf(CellRef.HomeColumn(seat, depth)), layout.CellSize,
                        Color.Lerp(UiTheme.CellMarble, tint, wash),
                        UiTheme.WithAlpha(UiTheme.Gold, 0.7f));
                }
            }
        }

        // ── Pieces of the floor ──────────────────────────────────────────

        /// <summary>The table: a charcoal field, a gilt double frame, a fan in each corner, and a pool of light.</summary>
        private void DrawTable(BoardLayout layout)
        {
            // Extent is half the grid; the margin is measured in spacings.
            float spacing = layout.Spacing;
            float side = 2f * layout.Extent + 2f * TableMargin * spacing;
            var centre = layout.HomeGoalPosition;

            Sliced("table", DecoSprites.TableFill, centre, side, UiTheme.BoardField, TableOrder);
            Sliced("table_frame", DecoSprites.TableEdge, centre, side - 0.25f * spacing,
                UiTheme.WithAlpha(UiTheme.Gold, 0.85f), TableTrimOrder);

            Sprite("table_light", DecoSprites.Glow, centre, side * 0.95f, UiTheme.BoardGlow, TableTrimOrder);

            // Fans open inward from each corner of the frame.
            float half = side * 0.5f - 0.35f * spacing;
            var fan = UiTheme.WithAlpha(UiTheme.Gold, 0.35f);
            Fan(centre + new Vector3(-half, -half, 0f), 0f, fan);
            Fan(centre + new Vector3(half, -half, 0f), 90f, fan);
            Fan(centre + new Vector3(half, half, 0f), 180f, fan);
            Fan(centre + new Vector3(-half, half, 0f), 270f, fan);
        }

        /// <summary>A round felt table in the seat's colour, with a seat rim and a gilt rim.</summary>
        private void DrawYard(BoardLayout layout, PlayerColor seat)
        {
            var tint = UiTheme.Seat(seat);
            var at = layout.PositionOf(CellRef.Yard(seat));
            float size = layout.CellSize * YardSize;

            Sprite($"yard_{seat}", Primitives.Disc, at, size,
                Color.Lerp(UiTheme.Charcoal, tint, UiTheme.FeltTint), FeltOrder);
            Sprite($"yard_rim_{seat}", DecoSprites.RingThin, at, size * 0.94f,
                UiTheme.WithAlpha(tint, 0.7f), FeltRimOrder);
            Sprite($"yard_trim_{seat}", DecoSprites.RingThin, at, size * 1.06f,
                UiTheme.WithAlpha(UiTheme.Gold, 0.55f), FeltRimOrder);
        }

        /// <summary>HOME: a dark vault floor under a lit gold sunburst.</summary>
        private void DrawVault(BoardLayout layout)
        {
            var at = layout.HomeGoalPosition;
            float size = layout.HomeGoalSize;

            Sprite("home_floor", Primitives.Disc, at, size, UiTheme.VaultFloor, VaultOrder);
            Sprite("home_light", DecoSprites.Glow, at, size * 1.8f,
                UiTheme.WithAlpha(UiTheme.VaultLight, 0.3f), VaultOrder);
            Sprite("home_rays", DecoSprites.Sunburst, at, size * 0.96f,
                UiTheme.WithAlpha(UiTheme.VaultLight, 0.8f), VaultRaysOrder);
            Sprite("home_rim", DecoSprites.RingThin, at, size,
                UiTheme.Gold, VaultRaysOrder);
        }

        /// <summary>A marble cell with its inlay.</summary>
        private void Tile(string name, Vector3 at, float size, Color marble, Color inlay)
        {
            Sprite(name, Primitives.Square, at, size, marble, CellOrder);
            Sprite(name + "_inlay", DecoSprites.TileInlay, at, size, inlay, InlayOrder);
        }

        private void Fan(Vector3 corner, float rotation, Color colour)
        {
            // The HUD fan is 26 texels at 100 per unit; scale it up to world size.
            float scale = TableFanSize / (DecoSprites.FanSize / 100f);
            var renderer = Sprite("table_fan", DecoSprites.CornerFan, corner, scale, colour, TableTrimOrder);
            renderer.transform.localEulerAngles = new Vector3(0f, 0f, rotation);
        }

        // ── Renderers ────────────────────────────────────────────────────

        /// <summary>A sprite one world unit across, scaled to <paramref name="size"/>.</summary>
        private SpriteRenderer Sprite(string name, Sprite sprite, Vector3 position, float size, Color colour, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.sortingOrder = order;

            _drawn.Add(go);
            return renderer;
        }

        /// <summary>A nine-sliced square, so its frame keeps its width at any size.</summary>
        private void Sliced(string name, Sprite sprite, Vector3 position, float side, Color colour, int order)
        {
            var renderer = Sprite(name, sprite, position, 1f, colour, order);
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(side, side);
        }
    }
}
