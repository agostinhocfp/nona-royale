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
    /// <b>Polished, not matte</b> (V5). A broad band of the room's light rakes
    /// across the cross, and the marble's veins take more of it than the stone
    /// does. It is painted into its own sprite rather than lit by a
    /// <c>Light2D</c> through a normal map, because <c>Light2D.normalMapQuality</c>
    /// is read-only in this URP and every light here is made at runtime - and
    /// because a painted sheen still reads with Lighting effects off, which the
    /// player can switch.
    ///
    /// <b>The casino floor</b> (GUI increments G, G2 and G3, ART_DIRECTION
    /// §6.1, and the designer's reference image of 2026-09-15). A dark square
    /// table with a faint gold grain and one gilt rule; a cross-shaped marble
    /// floor with a faint inlaid sunburst, a thin gilt edge (stepped where the
    /// arms meet, cracked along one length), a shadow, warm light pooled in
    /// each arm and a gold lane down each arm's middle; four felt tables with
    /// gilt rims, one per seat; and a vault door at the centre, glowing.
    ///
    /// <b>Painted surfaces</b> (G4). Painted marble, felt and carpet replace
    /// the procedural surfaces when they exist (<see cref="BoardTextures"/>).
    /// The table's corners stay dark; the corner lamps G4 tried were taken
    /// out.
    ///
    /// <b>Quiet where it matters</b> (G3). Nothing here competes with the lit
    /// cells or sits loud under a piece: the pattern and the veining stay
    /// within a few percent of the floor, and the table's corners stay dark.
    /// The room still reads finished with Lighting effects off, since all of
    /// it is painted into the sprites.
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
    ///
    /// <b>Two skins</b> (board skin BS2, BOARD_SKIN.md). <see cref="Skin"/>
    /// picks Classic, drawn by this file exactly as before, or Deco, drawn by
    /// <c>BoardView.Deco.cs</c> element by element as the BS-series lands.
    /// An element Deco doesn't draw yet is drawn as Classic.
    /// <see cref="ApplySkin"/> redraws the board when the Display page
    /// changes the skin, mid-match included: the board holds no state.
    /// </remarks>
    public sealed partial class BoardView : MonoBehaviour
    {
        private static readonly PlayerColor[] Seats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
        };

        // Back to front.
        private const int TableOrder = -33;
        private const int GrainOrder = -32;
        private const int TableRuleOrder = -31;
        private const int CrossShadowOrder = -30;
        private const int CrossOrder = -29;
        private const int PatternOrder = -28;
        private const int SheenOrder = -27;
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
        private const int RailShadowOrder = -11;
        private const int RailOrder = -10;

        /// <summary>
        /// How much of the padded rail lies on the table, in cells (G9c). The
        /// rest overhangs into the framing's air: the arms' tips stop about
        /// 0.4 cells short of the edge, and the table's gilt rule sits 0.3 in,
        /// just inside the rail, as its brass trim.
        /// </summary>
        private const float RailOnTable = 0.22f;

        /// <summary>The rail's two shadows, in cells: down and a little right, as the room's light falls.</summary>
        private static readonly Vector3 RailShadowNear = new Vector3(0.03f, -0.07f, 0f);
        private static readonly Vector3 RailShadowFar = new Vector3(0.06f, -0.15f, 0f);

        /// <summary>How far the table's gilt rule sits in from its edge, in cell spacings.</summary>
        private const float TableRuleInset = 0.3f;

        /// <summary>Width of the cross's arms, in cells: the three lanes.</summary>
        private const int ArmCells = 3;

        /// <summary>Chips on the felt, as fractions of the table's radius.</summary>
        private static readonly Vector2[] Chips =
        {
            new Vector2(-0.30f, -0.26f), new Vector2(-0.18f, -0.33f), new Vector2(0.26f, 0.30f),
        };

        private readonly List<GameObject> _drawn = new List<GameObject>();
        private PoweredShine _shine;
        private int? _layer;

        // What the last Build drew, so a skin change can draw it again (BS2).
        private PathMap _builtMap;
        private BoardLayout _builtLayout;
        private int _builtSeats;
        private BoardSkin _builtSkin;

        /// <summary>The skin the next <see cref="Build"/> draws (BS2). Set it before building.</summary>
        public BoardSkin Skin { get; set; } = DisplaySettings.DefaultSkin;

        /// <param name="seatsPerTable">Seats drawn at each table: the largest squad.</param>
        public void Build(PathMap map, BoardLayout layout, int seatsPerTable)
        {
            _builtMap = map;
            _builtLayout = layout;
            _builtSeats = seatsPerTable;
            _builtSkin = Skin;

            foreach (var go in _drawn)
                if (go != null) Destroy(go);

            // The powered cells' glint (G8f) forgets the old inlays with them.
            _shine = GetComponent<PoweredShine>() ?? gameObject.AddComponent<PoweredShine>();
            _shine.Clear();

            _drawn.Clear();
            _layer = SceneLighting.BoardLayerId;

            DrawFloor(layout);
            if (Skin == BoardSkin.Deco) DrawDecoTrack(map, layout);
            else DrawTrack(map, layout);

            foreach (var seat in Seats)
            {
                if (Skin == BoardSkin.Deco) DrawDecoTable(layout, seat, seatsPerTable);
                else DrawTable(layout, seat, seatsPerTable);
            }

            if (Skin == BoardSkin.Deco) DrawDecoCentre(layout);
            else DrawVault(layout);
        }

        /// <summary>
        /// Draws the board again in <paramref name="skin"/> if it was last
        /// built in another (BS2). Does nothing before the first build or when
        /// the skin is unchanged, so it is cheap to call every frame.
        /// </summary>
        public void ApplySkin(BoardSkin skin)
        {
            Skin = skin;
            if (_builtMap == null || _builtLayout == null || skin == _builtSkin) return;

            Build(_builtMap, _builtLayout, _builtSeats);
        }

        // ── Floor ────────────────────────────────────────────────────────

        /// <summary>
        /// The square table with its grain and rule, and the cross with its
        /// shadow, pattern, glow, edge and lanes.
        /// </summary>
        private void DrawFloor(BoardLayout layout)
        {
            float spacing = layout.Spacing;
            var centre = layout.HomeGoalPosition;
            float sideCells = layout.GridSize + 2f * BoardLayout.TableMargin;
            float side = sideCells * spacing;

            // A painted carpet replaces the plain table and its grain (G4).
            var carpet = BoardTextures.Carpet;
            if (carpet != null)
            {
                var table = Sprite("table_carpet", carpet, centre, spacing, UiTheme.CarpetTint, TableOrder);
                table.drawMode = SpriteDrawMode.Tiled;
                table.tileMode = SpriteTileMode.Continuous;
                table.size = new Vector2(sideCells, sideCells);
            }
            else
            {
                Sprite("table", BoardArt.Solid, centre, side, UiTheme.BoardField, TableOrder);
                Sprite("table_grain", BoardArt.Veins, centre, side, UiTheme.BoardVeins, GrainOrder);
            }
            Sprite("table_rule", BoardArt.TableRule(sideCells, TableRuleInset), centre, side,
                UiTheme.TableRule, TableRuleOrder);
            DrawRail(centre, spacing, sideCells);

            // The cross sprites are sized in cells, so their scale is the spacing.
            var cross = BoardArt.Cross(layout.GridSize, ArmCells);
            var shadowOffset = new Vector3(0.12f, -0.2f, 0f) * spacing;
            Sprite("cross_shadow", cross[BoardArt.CrossShadow], centre + shadowOffset, spacing, UiTheme.Shadow, CrossShadowOrder);
            var floorTint = BoardTextures.Marble != null ? UiTheme.PaintedFloor : UiTheme.CrossFloor;
            Sprite("cross", cross[BoardArt.CrossFill], centre, spacing, floorTint, CrossOrder);
            Sprite("cross_pattern", cross[BoardArt.CrossPattern], centre, spacing, UiTheme.FloorPattern, PatternOrder);

            // Over the stone and its inlay, under the arm pools: the floor is
            // polished, and its veins take the light first (V5).
            Sprite("cross_sheen", cross[BoardArt.CrossSheen], centre, spacing, UiTheme.FloorSheen, SheenOrder);

            Sprite("cross_edge", cross[BoardArt.CrossEdge], centre, spacing, UiTheme.CrossEdge, CrossEdgeOrder);

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

        /// <summary>
        /// The padded leather rail round the table's edge (G9c), with its
        /// shadow on the table and the void around it. It sits over the arm
        /// glows and under everything that stands on the board.
        /// </summary>
        private void DrawRail(Vector3 centre, float spacing, float sideCells)
        {
            float cells = sideCells + 2f * (BoardArt.RailCells - RailOnTable);

            RailRuns("rail_shadow_far", centre + RailShadowFar * spacing, spacing, cells, UiTheme.RailShadowFar, RailShadowOrder);
            RailRuns("rail_shadow", centre + RailShadowNear * spacing, spacing, cells, UiTheme.RailShadowNear, RailShadowOrder);
            RailRuns("rail", centre, spacing, cells, Color.white, RailOrder);
        }

        /// <summary>
        /// The rail's four runs round a square <paramref name="cells"/> across:
        /// the top and bottom the full width with the corners, the sides
        /// between them, so nothing overlaps and a shadow never doubles.
        /// </summary>
        private void RailRuns(string name, Vector3 centre, float spacing, float cells, Color colour, int order)
        {
            float width = BoardArt.RailCells;
            float mid = (cells - width) * 0.5f * spacing;

            RailRun(name + "_top", BoardArt.RailSide.Top, centre + Vector3.up * mid, spacing,
                new Vector2(cells, width), colour, order);
            RailRun(name + "_bottom", BoardArt.RailSide.Bottom, centre + Vector3.down * mid, spacing,
                new Vector2(cells, width), colour, order);
            RailRun(name + "_left", BoardArt.RailSide.Left, centre + Vector3.left * mid, spacing,
                new Vector2(width, cells - 2f * width), colour, order);
            RailRun(name + "_right", BoardArt.RailSide.Right, centre + Vector3.right * mid, spacing,
                new Vector2(width, cells - 2f * width), colour, order);
        }

        private void RailRun(string name, BoardArt.RailSide side, Vector3 position, float spacing,
            Vector2 size, Color colour, int order)
        {
            var renderer = Sprite(name, BoardArt.TableRail(side), position, spacing, colour, order);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = size;
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
                    _shine.Add(Tile($"start_{owner}", at, cell,
                        UiTheme.WithAlpha(Color.Lerp(UiTheme.CellWhisper, tint, 0.3f), 0.45f),
                        UiTheme.WithAlpha(tint, UiTheme.StartInlayAlpha)));
                }
                else
                {
                    _shine.Add(Tile($"safe_{i}", at, cell, UiTheme.CellWhisper, UiTheme.SafeInlay));
                }
            }

            foreach (var seat in Seats)
            {
                var tint = UiTheme.Seat(seat);

                // Home column, mouth to HOME, deepening as it goes.
                for (int depth = 0; depth < profile.HomeColumnLength; depth++)
                {
                    float t = depth / Mathf.Max(1f, profile.HomeColumnLength - 1f);

                    var home = CellRef.HomeColumn(seat, depth);
                    var inlay = Tile($"home_{seat}_{depth}",
                        layout.PositionOf(home), cell,
                        UiTheme.WithAlpha(tint, Mathf.Lerp(UiTheme.HomeWashNear, UiTheme.HomeWashFar, t)),
                        UiTheme.WithAlpha(tint, Mathf.Lerp(UiTheme.HomeInlayNear, UiTheme.HomeInlayFar, t)));

                    // The column's mouth is safe too, so it glints with the start cells (G8f).
                    if (map.IsSafe(home)) _shine.Add(inlay);
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
        /// <summary>A cell: its wash and its inlay. Returns the inlay, for the powered glint.</summary>
        private SpriteRenderer Tile(string name, Vector3 at, float size, Color fill, Color inlay)
        {
            Sprite(name, Primitives.Square, at, size, fill, CellOrder);
            return Sprite(name + "_inlay", DecoSprites.TileInlay, at, size, inlay, InlayOrder);
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
