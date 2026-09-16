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
//                      { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet })
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
    /// <b>The casino floor</b> (GUI increments G and G2, ART_DIRECTION §6.1,
    /// and the designer's reference image of 2026-09-15). A dark square table
    /// with a faint gold grain; a cross-shaped floor with a gilt edge, a
    /// shadow, warm light pooled in each arm and a gold lane down each arm's
    /// middle; four felt tables with gilt rims, one per seat; and a vault door
    /// at the centre, glowing.
    ///
    /// <b>The path is a whisper at rest</b> (decided 2026-09-15): faint marble
    /// and a faint inlay, enough to count squares. The turn's landings, reach
    /// and targets light up over it (<see cref="HighlightLayer"/>). Safe cells
    /// are powered: a cyan glow, and each start cell's inlay in its seat's
    /// colour. Home columns carry a seat wash that deepens toward HOME.
    ///
    /// <b>Tables have seats.</b> Each seat is a dark mark where an operator
    /// sits (<see cref="BoardLayout.YardSeat"/>), so a table shows who has
    /// stood up. Tables are drawn for all four seats, played or not: the room
    /// has four tables.
    ///
    /// Sorting orders are all below zero. Devices draw at 0, highlights at 1
    /// and 2, pieces from 1 up.
    ///
    /// <b>Its own sorting layer</b> (LT1): everything here goes on
    /// <see cref="SceneLighting.BoardLayer"/> when that layer exists, so the
    /// powered cells' cyan light reaches the floor and not the pieces. The
    /// layer sits behind Default, so the orders above still hold.
    /// </remarks>
    public sealed class BoardView : MonoBehaviour
    {
        private static readonly PlayerColor[] Seats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
        };

        // Back to front.
        private const int TableOrder = -30;
        private const int GrainOrder = -29;
        private const int CrossShadowOrder = -28;
        private const int CrossOrder = -27;
        private const int ArmGlowOrder = -26;
        private const int CrossEdgeOrder = -25;
        private const int LaneOrder = -24;
        private const int PowerGlowOrder = -23;
        private const int CellOrder = -22;
        private const int InlayOrder = -21;
        private const int FeltShadowOrder = -20;
        private const int FeltOrder = -19;
        private const int FeltTrimOrder = -18;
        private const int SeatMarkOrder = -17;
        private const int RimOrder = -16;
        private const int VaultGlowOrder = -15;
        private const int VaultOrder = -14;
        private const int VaultTrimOrder = -13;
        private const int VaultBossOrder = -12;

        /// <summary>Table border beyond the outermost cells, in cell spacings.</summary>
        private const float TableMargin = 0.8f;

        /// <summary>Width of the cross's arms, in cells: the three lanes.</summary>
        private const int ArmCells = 3;

        /// <summary>Chips on the felt, as fractions of the table's radius.</summary>
        private static readonly Vector2[] Chips =
        {
            new Vector2(-0.30f, -0.26f), new Vector2(-0.18f, -0.33f), new Vector2(0.26f, 0.30f),
        };

        private readonly List<GameObject> _drawn = new List<GameObject>();
        private int? _layer;

        /// <param name="seatsPerTable">Seats drawn at each table: the largest squad.</param>
        public void Build(PathMap map, BoardLayout layout, int seatsPerTable)
        {
            foreach (var go in _drawn)
                if (go != null) Destroy(go);

            _drawn.Clear();
            _layer = SceneLighting.BoardLayerId;

            DrawFloor(layout);
            DrawTrack(map, layout);

            foreach (var seat in Seats) DrawTable(layout, seat, seatsPerTable);

            DrawVault(layout);
        }

        // ── Floor ────────────────────────────────────────────────────────

        /// <summary>The square table, its grain, and the cross with its shadow, glow, edge and lanes.</summary>
        private void DrawFloor(BoardLayout layout)
        {
            float spacing = layout.Spacing;
            var centre = layout.HomeGoalPosition;
            float side = (layout.GridSize + 2f * TableMargin) * spacing;

            Sprite("table", BoardArt.Solid, centre, side, UiTheme.BoardField, TableOrder);
            Sprite("table_grain", BoardArt.Veins, centre, side, UiTheme.BoardVeins, GrainOrder);

            // The cross sprites are sized in cells, so their scale is the spacing.
            var cross = BoardArt.Cross(layout.GridSize, ArmCells);
            var shadowOffset = new Vector3(0.12f, -0.2f, 0f) * spacing;
            Sprite("cross_shadow", cross[2], centre + shadowOffset, spacing, UiTheme.Shadow, CrossShadowOrder);
            Sprite("cross", cross[0], centre, spacing, UiTheme.CrossFloor, CrossOrder);
            Sprite("cross_edge", cross[1], centre, spacing, UiTheme.CrossEdge, CrossEdgeOrder);

            // Per arm: a pool of light, and the lane from the tip to the vault.
            float reach = layout.GridSize * 0.5f * spacing;
            float laneStart = (layout.HomeGoalSize * 0.5f + 0.15f * spacing);
            float laneEnd = reach - 0.6f * spacing;
            float laneLength = laneEnd - laneStart;
            float laneMid = (laneStart + laneEnd) * 0.5f;
            float lineWidth = 0.035f * spacing;

            foreach (var direction in new[] { Vector3.up, Vector3.left, Vector3.down, Vector3.right })
            {
                Sprite("arm_light", DecoSprites.Glow, centre + direction * reach * 0.6f,
                    reach * 0.6f, UiTheme.ArmGlow, ArmGlowOrder);

                var lane = Sprite("lane", BoardArt.Solid, centre + direction * laneMid, 1f, UiTheme.CrossLane, LaneOrder);
                bool vertical = direction.x == 0f;
                lane.transform.localScale = vertical
                    ? new Vector3(lineWidth, laneLength, 1f)
                    : new Vector3(laneLength, lineWidth, 1f);
            }
        }

        /// <summary>The whispered path: the shared track and the four home columns.</summary>
        private void DrawTrack(PathMap map, BoardLayout layout)
        {
            var profile = map.Profile;
            float cell = layout.CellSize;

            var starts = new Dictionary<int, PlayerColor>();
            foreach (var seat in Seats) starts[map.StartTrackIndex(seat)] = seat;

            for (int i = 0; i < profile.CircuitLength; i++)
            {
                var at = layout.PositionOf(CellRef.Track(i));

                if (!map.IsSafe(CellRef.Track(i)))
                {
                    Tile($"track_{i}", at, cell, UiTheme.CellWhisper, UiTheme.CellInlay);
                    continue;
                }

                Sprite($"safe_glow_{i}", DecoSprites.Glow, at, cell * 1.7f, UiTheme.SafeGlow, PowerGlowOrder);

                if (starts.TryGetValue(i, out var owner))
                {
                    var tint = UiTheme.Seat(owner);
                    Tile($"start_{owner}", at, cell,
                        UiTheme.WithAlpha(Color.Lerp(UiTheme.CellWhisper, tint, 0.3f), 0.45f),
                        UiTheme.WithAlpha(tint, UiTheme.StartInlayAlpha));
                }
                else
                {
                    Tile($"safe_{i}", at, cell, UiTheme.CellWhisper, UiTheme.SafeInlay);
                }
            }

            foreach (var seat in Seats)
            {
                var tint = UiTheme.Seat(seat);

                // Home column, mouth to HOME, deepening as it goes.
                for (int depth = 0; depth < profile.HomeColumnLength; depth++)
                {
                    float t = depth / Mathf.Max(1f, profile.HomeColumnLength - 1f);

                    Tile($"home_{seat}_{depth}",
                        layout.PositionOf(CellRef.HomeColumn(seat, depth)), cell,
                        UiTheme.WithAlpha(tint, Mathf.Lerp(UiTheme.HomeWashNear, UiTheme.HomeWashFar, t)),
                        UiTheme.WithAlpha(tint, Mathf.Lerp(UiTheme.HomeInlayNear, UiTheme.HomeInlayFar, t)));
                }
            }
        }

        // ── Tables ───────────────────────────────────────────────────────

        /// <summary>A felt table: shadow, felt, trim, seats, chips and a gilt rim.</summary>
        private void DrawTable(BoardLayout layout, PlayerColor seat, int seats)
        {
            float spacing = layout.Spacing;
            var at = layout.PositionOf(CellRef.Yard(seat));
            float diameter = layout.TableDiameter;
            float radius = diameter * 0.5f;
            var tint = UiTheme.Seat(seat);

            Sprite($"felt_shadow_{seat}", BoardArt.SoftDisc, at + new Vector3(0.1f, -0.22f, 0f) * spacing,
                diameter * 1.06f, UiTheme.Shadow, FeltShadowOrder);

            var felt = tint * UiTheme.FeltBrightness;
            felt.a = 1f;
            Sprite($"felt_{seat}", BoardArt.Felt, at, diameter, felt, FeltOrder);
            Sprite($"felt_ring_{seat}", BoardArt.DottedRing, at, diameter, UiTheme.FeltTrim, FeltTrimOrder);
            Sprite($"felt_arc_{seat}", BoardArt.TableArc, at, diameter, UiTheme.FeltTrim, FeltTrimOrder);

            // The dealer's spot.
            float spot = 0.62f * spacing;
            Sprite($"felt_spot_{seat}", Primitives.Disc, at, spot, UiTheme.SeatMark, FeltTrimOrder);
            Sprite($"felt_spot_rim_{seat}", DecoSprites.RingThin, at, spot, UiTheme.FeltTrim, FeltTrimOrder);

            foreach (var chip in Chips)
            {
                Sprite($"chip_{seat}", Primitives.Disc, at + (Vector3)(chip * radius), 0.14f * spacing,
                    UiTheme.FeltChip, FeltTrimOrder);
            }

            for (int i = 0; i < seats; i++)
            {
                Sprite($"seat_{seat}_{i}", Primitives.Disc, layout.YardSeat(seat, i), 0.46f * spacing,
                    UiTheme.SeatMark, SeatMarkOrder);
            }

            Sprite($"rim_{seat}", BoardArt.TableRim, at, diameter, UiTheme.TableRim, RimOrder);
        }

        // ── Vault ────────────────────────────────────────────────────────

        /// <summary>HOME: a glowing vault door with a dial and a polished hub.</summary>
        private void DrawVault(BoardLayout layout)
        {
            var at = layout.HomeGoalPosition;
            float size = layout.HomeGoalSize;

            Sprite("vault_glow", DecoSprites.Glow, at, size * 3f, UiTheme.VaultGlow, VaultGlowOrder);
            Sprite("vault_plate", BoardArt.VaultPlate, at, size, UiTheme.VaultPlate, VaultOrder);
            Sprite("vault_frame", BoardArt.VaultFrame, at, size, UiTheme.VaultFrame, VaultTrimOrder);
            Sprite("vault_dial", BoardArt.VaultDial, at, size, UiTheme.WithAlpha(UiTheme.VaultFrame, 0.9f), VaultTrimOrder);
            Sprite("vault_boss", BoardArt.Boss, at, size * 0.2f, UiTheme.VaultBoss, VaultBossOrder);
        }

        // ── Renderers ────────────────────────────────────────────────────

        /// <summary>A whispered cell: a faint square and its inlay.</summary>
        private void Tile(string name, Vector3 at, float size, Color fill, Color inlay)
        {
            Sprite(name, Primitives.Square, at, size, fill, CellOrder);
            Sprite(name + "_inlay", DecoSprites.TileInlay, at, size, inlay, InlayOrder);
        }

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
            if (_layer.HasValue) renderer.sortingLayerID = _layer.Value;

            _drawn.Add(go);
            return renderer;
        }
    }
}
