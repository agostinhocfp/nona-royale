// Assets/_Project/Scripts/Unity/View/BoardView.Deco.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The Deco skin's half of <see cref="BoardView"/> (board skin BS2 on,
    /// BOARD_SKIN.md). Classic's drawing stays in <c>BoardView.cs</c>,
    /// untouched; <see cref="BoardView.Build"/> picks a path per element by
    /// <see cref="BoardView.Skin"/>.
    /// </summary>
    /// <remarks>
    /// <b>The path as tiles, at lower contrast than the target</b> (D1,
    /// 2026-09-27; ART §6.1). Every track cell is a raised dark tile with a
    /// lit top-left bevel, a shaded bottom-right one and a thin gilt edge;
    /// the gap between cells is the floor's. Home columns are tiles in their
    /// seat's colour with a gilt ring each. Start cells are seat-coloured
    /// tiles with a compass. Safe cells keep their cyan glow and take a cyan
    /// edge, and every safe cell's trim glints as before (G8f).
    ///
    /// <b>Painted art first.</b> A filled <c>board_cell_track</c>,
    /// <c>board_cell_home</c> or <c>board_start_emblem</c> slot replaces the
    /// procedural tile, ring or compass (<see cref="BoardSprites"/>), with no
    /// code change. A painted track tile carries its own gilt edge, so the
    /// procedural edge is drawn over it only where it has to glint.
    ///
    /// <b>HOME is a medallion, not a vault</b> (BS3, D2). A black lacquer
    /// disc with a bevelled gilt rim and a gilt diamond, and four gilt wedges
    /// running from it toward the cross's inner corners. It is sized to clear
    /// the last cell of every home column (<see cref="DecoMedallionDiameter"/>),
    /// so it is smaller than the target's, which squeezes those cells (ART
    /// §10 ref 5); the wedges carry the target's star. No glow: the win
    /// moment leaves the centre (D2). The arrival light
    /// (<c>EventLights.VaultSwell</c>) is a light, not board art, and stays.
    ///
    /// <b>Tables in their seat's colour, rimmed in gilt</b> (BS4). Brighter
    /// felt than Classic's, a heavy bevelled gilt rim with an engraved line
    /// inside it, a gilt compass at the centre, and a chair under each seat
    /// once <c>yard_chair</c> exists (D3: until then, Classic's seat marks).
    /// Classic's dotted ring, arc, dealer's spot and felt chips are not
    /// drawn: the target's tables are quieter, and chips come back as props
    /// in BS6.
    ///
    /// <b>Weight and a moving light</b> (BS5). Every tile casts a soft drop
    /// shadow down and to the right, away from the key light, onto the floor
    /// between the cells; the wedges cast a crisp one, as the medallion and
    /// the tables already do. A slow sheen passes over the big gilt — the
    /// rims, the medallion and the wedges — every few seconds
    /// (<see cref="GiltSheen"/>), off with Lighting effects or under Reduced
    /// motion. Everything that gives weight is painted, so the board still
    /// reads finished with the lighting off. No light pools: the simple
    /// target has no candles, and its light is even.
    ///
    /// <b>Tuned against the target</b> (BS6, the BS5 checkpoint). A gilt
    /// lattice runs in the gaps between the tiles with a rivet at each
    /// crossing, as the target's cross is one latticed panel rather than
    /// loose tiles on black; the tiles' shadows fall across it. The table
    /// rims are thinner, dimmer and glint less, and the felt deeper, so the
    /// tables stop being the loudest thing on the board. The wedges are
    /// slimmer and darker, and each inner corner carries two small facets in
    /// the colours of the two arms it joins, as the target's do.
    ///
    /// <b>Yards as panels</b> (BS7, edge fixed in BS7b, aligned in BS8b, one line since BS9).
    /// Each yard block is a dark lacquer panel under its table, a dark shade of
    /// its seat, with one subtle gilt hairline just inside its edge and a Deco
    /// triangle in each corner — the simple target's yards. (BS7's seat-coloured
    /// band, and BS7b's dark band between two hairlines, were dropped at the
    /// designer's call.) It fills the largest black on
    /// the board and frames each table as its seat's own. It stays clear of
    /// every cell (<c>DecoYardTests</c>).
    ///
    /// <b>A steel frame</b> (BS8). On Deco the padded oxblood rail becomes the
    /// simple target's dark steel frame: brushed gunmetal, bevelled on both
    /// edges, lit from the upper left, a groove down its middle with bolts
    /// along it, chamfered outer corners and a gilt hairline on its inner
    /// lip. Its outer edge is exactly the rail's, so the camera frames the
    /// board as before. **One set of lines** (BS8b, the designer's catch):
    /// the frame's inner edge is the arms' tips, and each yard panel runs from
    /// its arm's edge to the frame, so the cross, the panels and the frame
    /// share their edges as the target's do (<c>DecoFrameTests</c>).
    ///
    /// <b>Readability is the constraint.</b> Highlights, reach and targets
    /// draw on the Default layer, above the whole board layer, so they
    /// always land on top of the tiles; the tiles are held dark enough that
    /// the cyan and amber markers still pop.
    /// </remarks>
    public sealed partial class BoardView
    {
        /// <summary>The lattice's line width and its rivets' diameter, in spacings. Both sit inside the 0.14 gap.</summary>
        public const float DecoLatticeWidth = 0.04f;
        public const float DecoRivetSize = 0.075f;

        /// <summary>
        /// The lattice between the tiles, in spacings from the board's
        /// centre, for a board whose arms are <paramref name="armLength"/>
        /// cells long (BS6). Per arm: the two lines between its three lanes,
        /// the line across the arm's mouth between the last two home cells,
        /// and one line across every gap between rows. The arm's outer edges
        /// are the cross's own gilt edge, so they are not repeated. Every
        /// line runs down the middle of a gap.
        /// </summary>
        public static List<(Vector2 from, Vector2 to)> DecoLatticeSegments(int armLength)
        {
            var segments = new List<(Vector2 from, Vector2 to)>();
            float near = 1.5f;
            float far = armLength + 1.5f;

            for (int k = 0; k < 4; k++)
            {
                void Add(Vector2 a, Vector2 b) => segments.Add((Turn(a, k), Turn(b, k)));

                Add(new Vector2(-0.5f, near), new Vector2(-0.5f, far));
                Add(new Vector2(0.5f, near), new Vector2(0.5f, far));
                Add(new Vector2(-0.5f, near), new Vector2(0.5f, near));
                for (int row = 1; row < armLength; row++)
                    Add(new Vector2(-1.5f, near + row), new Vector2(1.5f, near + row));
            }

            return segments;
        }

        /// <summary>The lattice's rivets: where the lines between rows cross the lines between lanes.</summary>
        public static List<Vector2> DecoLatticeRivets(int armLength)
        {
            var rivets = new List<Vector2>();
            for (int k = 0; k < 4; k++)
                for (int row = 1; row < armLength; row++)
                {
                    rivets.Add(Turn(new Vector2(-0.5f, 1.5f + row), k));
                    rivets.Add(Turn(new Vector2(0.5f, 1.5f + row), k));
                }
            return rivets;
        }

        /// <summary><paramref name="v"/> turned a quarter counter-clockwise <paramref name="quarters"/> times.</summary>
        private static Vector2 Turn(Vector2 v, int quarters)
        {
            for (int q = 0; q < quarters; q++) v = new Vector2(-v.y, v.x);
            return v;
        }

        /// <summary>The gilt lattice and its rivets, under the tiles' shadows (BS6).</summary>
        private void DrawDecoLattice(BoardLayout layout)
        {
            var centre = layout.HomeGoalPosition;
            float spacing = layout.Spacing;
            float width = DecoLatticeWidth * spacing;
            int i = 0;

            foreach (var (from, to) in DecoLatticeSegments(layout.ArmLength))
            {
                var a = centre + (Vector3)(from * spacing);
                var b = centre + (Vector3)(to * spacing);
                var line = Sprite($"lattice_{i++}", BoardArt.Solid, (a + b) * 0.5f, 1f, UiTheme.DecoLattice, LaneOrder);
                line.transform.localScale = new Vector3(Mathf.Abs(b.x - a.x) + width, Mathf.Abs(b.y - a.y) + width, 1f);
            }

            i = 0;
            foreach (var rivet in DecoLatticeRivets(layout.ArmLength))
            {
                Sprite($"rivet_{i++}", Primitives.Disc, centre + (Vector3)(rivet * spacing), DecoRivetSize * spacing,
                    UiTheme.DecoRivet, LaneOrder);
            }
        }

        /// <summary>The slow sheen over the big gilt (BS5). Cleared and refilled by every build.</summary>
        private GiltSheen _sheen;

        /// <summary>A tile's drop shadow, in tile sizes: down and right, away from the key light. (0.04, -0.06) until BS9.</summary>
        private static readonly Vector3 DecoTileShadowOffset = new Vector3(0.025f, -0.04f, 0f);

        /// <summary>The wedges' shadow, in cell spacings: crisp, close under them.</summary>
        private static readonly Vector3 DecoWedgeShadowOffset = new Vector3(0.03f, -0.05f, 0f);

        /// <summary>The start cell's compass, as a fraction of the cell.</summary>
        private const float DecoEmblemSize = 0.72f;

        /// <summary>The Deco path: tiles for the shared track and the four home columns.</summary>
        private void DrawDecoTrack(PathMap map, BoardLayout layout)
        {
            var profile = map.Profile;
            float cell = layout.CellSize;

            var starts = new Dictionary<int, PlayerColor>();
            foreach (var seat in Seats) starts[map.StartTrackIndex(seat)] = seat;

            DrawDecoLattice(layout);

            var trackArt = BoardSprites.Get(BoardSprites.CellTrack);
            var homeArt = BoardSprites.Get(BoardSprites.CellHome);
            var emblemArt = BoardSprites.Get(BoardSprites.StartEmblem);

            for (int i = 0; i < profile.CircuitLength; i++)
            {
                var at = layout.PositionOf(CellRef.Track(i));

                if (!map.IsSafe(CellRef.Track(i)))
                {
                    DecoTile($"track_{i}", at, cell, UiTheme.DecoTrackFace, DecoBoardArt.TrimGilt,
                        trackArt, tinted: false, needTrim: false);
                    continue;
                }

                Sprite($"safe_glow_{i}", DecoSprites.Glow, at, cell * 1.7f, UiTheme.SafeGlow, PowerGlowOrder);

                if (starts.TryGetValue(i, out var owner))
                {
                    var seatColour = UiTheme.Seat(owner);
                    _shine.Add(DecoTile($"start_{owner}", at, cell,
                        UiTheme.DecoSeatFace(seatColour, UiTheme.DecoStartFace), DecoBoardArt.TrimGilt,
                        null, tinted: true, needTrim: true));

                    if (emblemArt != null)
                    {
                        SlotSprite($"start_emblem_{owner}", emblemArt, at, cell * DecoEmblemSize,
                            UiTheme.DecoEmblemTint(seatColour), InlayOrder);
                    }
                    else
                    {
                        Sprite($"start_emblem_{owner}", DecoBoardArt.Compass, at, cell * DecoEmblemSize,
                            UiTheme.DecoEmblem, InlayOrder);
                    }
                }
                else
                {
                    _shine.Add(DecoTile($"safe_{i}", at, cell, UiTheme.DecoTrackFace, DecoBoardArt.TrimCyan,
                        trackArt, tinted: false, needTrim: true));
                }
            }

            foreach (var seat in Seats)
            {
                var seatColour = UiTheme.Seat(seat);
                var face = UiTheme.DecoSeatFace(seatColour, UiTheme.DecoHomeFace);

                for (int depth = 0; depth < profile.HomeColumnLength; depth++)
                {
                    var home = CellRef.HomeColumn(seat, depth);
                    var at = layout.PositionOf(home);
                    bool safe = map.IsSafe(home);

                    // A painted home tile carries its ring; tint it with the seat itself.
                    var trim = DecoTile($"home_{seat}_{depth}", at, cell, homeArt != null ? seatColour : face,
                        DecoBoardArt.TrimGilt, homeArt, tinted: true, needTrim: homeArt == null || safe);

                    if (homeArt == null)
                        Sprite($"home_ring_{seat}_{depth}", DecoBoardArt.CellRing, at, cell, UiTheme.DecoHomeRing, InlayOrder);

                    // The column's mouth is safe too, so it glints with the start cells (G8f).
                    if (safe) _shine.Add(trim);
                }
            }
        }

        /// <summary>
        /// The seat whose home column runs up the arm in <paramref name="direction"/>
        /// from the board's centre, found from the layout rather than assumed.
        /// </summary>
        private static PlayerColor SeatOfArm(BoardLayout layout, Vector3 direction)
        {
            var centre = layout.HomeGoalPosition;
            var best = Seats[0];
            float bestDot = float.MinValue;

            foreach (var seat in Seats)
            {
                var toColumn = (layout.PositionOf(CellRef.HomeColumn(seat, 0)) - centre).normalized;
                float dot = Vector3.Dot(toColumn, direction);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = seat;
                }
            }

            return best;
        }

        /// <summary>
        /// The medallion's diameter: as large as it can be while leaving a
        /// clear gap before the inner edge of each home column's last cell,
        /// which sits one spacing from the centre. 1.05 spacings on every
        /// board, well inside <see cref="BoardLayout.HomeGoalSize"/>.
        /// </summary>
        public static float DecoMedallionDiameter(BoardLayout layout) =>
            2f * (layout.Spacing - layout.CellSize * 0.5f) * 0.92f;

        /// <summary>The medallion's emblem, as a fraction of the medallion.</summary>
        private const float DecoEmblemOfMedallion = 1f;

        /// <summary>HOME on the Deco skin (BS3): wedges, a contact shadow, the medallion and its emblem.</summary>
        private void DrawDecoCentre(BoardLayout layout)
        {
            var at = layout.HomeGoalPosition;
            float spacing = layout.Spacing;
            float diameter = DecoMedallionDiameter(layout);

            // Four wedges, one per inner corner. The art points up and right;
            // each copy turns a quarter further about the board's centre.
            var wedgeArt = BoardSprites.Get(BoardSprites.CornerWedge);
            float span = DecoBoardArt.WedgeSpan * spacing;
            for (int k = 0; k < 4; k++)
            {
                var turn = Quaternion.Euler(0f, 0f, 90f * k);
                var offset = turn * new Vector3(0.5f, 0.5f, 0f) * span;

                var wedgeSprite = wedgeArt != null ? wedgeArt : DecoBoardArt.CornerWedge;
                float wedgeSize = wedgeArt != null ? span * BoardSprites.UnitScale(wedgeArt) : span;

                var shadow = Sprite($"wedge_shadow_{k}", wedgeSprite, at + offset + DecoWedgeShadowOffset * spacing,
                    wedgeSize, UiTheme.DecoWedgeShadow, VaultGlowOrder);
                shadow.transform.rotation = turn;

                var wedge = Sprite($"wedge_{k}", wedgeSprite, at + offset, wedgeSize, UiTheme.DecoWedgeTint, VaultOrder);
                wedge.transform.rotation = turn;
                _sheen.Add(wedge);

                // The two facets at this inner corner, in the colours of the arms it joins (BS6).
                var facetAt = at + turn * new Vector3(1f, 1f, 0f) * spacing;
                var upperArm = turn * Vector3.up;
                var sideArm = turn * Vector3.right;
                var upper = Sprite($"facet_upper_{k}", DecoBoardArt.CornerFacet(true), facetAt, spacing,
                    UiTheme.DecoSeatFace(UiTheme.Seat(SeatOfArm(layout, upperArm)), UiTheme.DecoFacetFace), InlayOrder);
                upper.transform.rotation = turn;
                var side = Sprite($"facet_side_{k}", DecoBoardArt.CornerFacet(false), facetAt, spacing,
                    UiTheme.DecoSeatFace(UiTheme.Seat(SeatOfArm(layout, sideArm)), UiTheme.DecoFacetFace), InlayOrder);
                side.transform.rotation = turn;
            }

            // Under the wedges' bases and the disc: the medallion stands proud of the floor.
            Sprite("medallion_shadow", BoardArt.SoftDisc, at + new Vector3(0.04f, -0.07f, 0f) * spacing,
                diameter * 1.06f, UiTheme.Shadow, VaultGlowOrder);

            var discArt = BoardSprites.Get(BoardSprites.Medallion);
            _sheen.Add(discArt != null
                ? SlotSprite("medallion", discArt, at, diameter, Color.white, VaultTrimOrder)
                : Sprite("medallion", DecoBoardArt.Medallion, at, diameter, Color.white, VaultTrimOrder));

            var emblemArt = BoardSprites.Get(BoardSprites.MedallionEmblem);
            float emblem = diameter * DecoEmblemOfMedallion;
            if (emblemArt != null) SlotSprite("medallion_emblem", emblemArt, at, emblem, Color.white, VaultBossOrder);
            else Sprite("medallion_emblem", DecoBoardArt.MedallionEmblem, at, emblem, Color.white, VaultBossOrder);
        }

        /// <summary>The table's centre emblem, and the diameter of the gilt ring round it, in cell spacings.</summary>
        public const float DecoTableEmblemSize = 1.25f;
        public const float DecoTableEmblemRing = 1.5f;

        /// <summary>A chair, in cell spacings: a little wider than the seated figure it holds.</summary>
        public const float DecoChairSize = 0.95f;

        /// <summary>
        /// The painted felt's diameter, as a fraction of the table's: to just
        /// under the rim's outer edge, so the rim covers the felt's edge.
        /// </summary>
        private const float DecoFeltOfTable = 0.9f;

        /// <summary>
        /// The z rotation, in degrees, that turns a chair drawn facing down
        /// (seat toward the sprite's bottom, back to its top) so that its seat
        /// faces the table's centre from a seat at <paramref name="seatAngle"/>
        /// (degrees counter-clockwise from east, <see cref="BoardLayout.SeatAngle"/>).
        /// </summary>
        public static float DecoChairRotation(float seatAngle) => seatAngle - 90f;

        /// <summary>
        /// The steel frame's outer edge, in spacings from the board's centre:
        /// exactly where Classic's rail ends, so framing is unchanged.
        /// </summary>
        public static float DecoFrameOuter(BoardLayout layout) =>
            layout.GridSize * 0.5f + BoardLayout.TableMargin + BoardArt.RailCells - RailOnTable;

        /// <summary>
        /// The steel frame's inner edge, in spacings from the board's centre:
        /// exactly on the arms' tips, the cross's own edge (BS8b), so the
        /// cross runs into the frame as the target's does.
        /// </summary>
        public static float DecoFrameInner(BoardLayout layout) =>
            layout.GridSize * 0.5f + BoardArt.CrossPad;

        /// <summary>The frame's shadow on the table, in spacings: down and right, away from the light.</summary>
        private static readonly Vector3 DecoFrameShadowOffset = new Vector3(0.05f, -0.1f, 0f);

        /// <summary>The steel frame (BS8): four runs, and their shadow on the table, where Classic draws its rail.</summary>
        private void DrawDecoFrame(BoardLayout layout)
        {
            var centre = layout.HomeGoalPosition;
            float spacing = layout.Spacing;
            float outer = DecoFrameOuter(layout);
            float inner = DecoFrameInner(layout);

            foreach (DecoBoardArt.FrameRun run in System.Enum.GetValues(typeof(DecoBoardArt.FrameRun)))
            {
                var sprite = DecoBoardArt.FrameRunSprite(outer, inner, run);
                Sprite($"frame_shadow_{run}", sprite, centre + DecoFrameShadowOffset * spacing, spacing,
                    UiTheme.DecoFrameShadow, RailShadowOrder);
                Sprite($"frame_{run}", sprite, centre, spacing, Color.white, RailOrder);
            }
        }

        /// <summary>
        /// A yard panel's side (BS8b): the whole yard block,
        /// <see cref="BoardLayout.ArmLength"/> spacings, from the arm's edge
        /// to the frame. The panel shares its lines with the cross and the
        /// frame instead of floating inside them.
        /// </summary>
        public static float DecoYardPanelSide(BoardLayout layout) => layout.ArmLength * layout.Spacing;

        /// <summary>
        /// A yard panel's centre (BS8b): the yard's centre, pushed out along
        /// both axes by the cross's padding (<see cref="BoardArt.CrossPad"/>),
        /// so the panel's inner edges sit on the arms' edges and its outer
        /// edges on the frame's inner edge. The table stays on the yard's
        /// centre, 0.12 spacings off the panel's.
        /// </summary>
        public static Vector3 DecoYardPanelCentre(BoardLayout layout, PlayerColor seat)
        {
            var yard = layout.PositionOf(CellRef.Yard(seat));
            var fromCentre = yard - layout.HomeGoalPosition;
            var push = new Vector3(Mathf.Sign(fromCentre.x), Mathf.Sign(fromCentre.y), 0f) * (BoardArt.CrossPad * layout.Spacing);
            return yard + push;
        }

        /// <summary>The yard's panel (BS7, BS9): dark fill, one subtle gilt hairline and its corners. Under the table.</summary>
        private void DrawDecoYardPanel(BoardLayout layout, PlayerColor seat)
        {
            var at = DecoYardPanelCentre(layout, seat);
            float side = DecoYardPanelSide(layout);
            var seatColour = UiTheme.Seat(seat);

            Sprite($"yard_panel_{seat}", BoardArt.Solid, at, side, UiTheme.DecoYardFill(seatColour), TableRuleOrder);

            Sprite($"yard_frame_{seat}", DecoBoardArt.YardFrame, at, side, UiTheme.DecoYardGilt, PatternOrder);
        }

        /// <summary>A Deco yard table (BS4): shadow, felt, rim, emblem, and chairs or seat marks.</summary>
        private void DrawDecoTable(BoardLayout layout, PlayerColor seat, int seats)
        {
            DrawDecoYardPanel(layout, seat);

            float spacing = layout.Spacing;
            var at = layout.PositionOf(CellRef.Yard(seat));
            float diameter = layout.TableDiameter;
            var seatColour = UiTheme.Seat(seat);

            Sprite($"felt_shadow_{seat}", BoardArt.SoftDisc, at + new Vector3(0.1f, -0.22f, 0f) * spacing,
                diameter * 1.06f, UiTheme.Shadow, FeltShadowOrder);

            var feltArt = BoardSprites.Get(BoardSprites.TableFelt);
            var felt = UiTheme.DecoSeatFace(seatColour, UiTheme.DecoFeltBrightness);
            if (feltArt != null) SlotSprite($"felt_{seat}", feltArt, at, diameter * DecoFeltOfTable, felt, FeltOrder);
            else Sprite($"felt_{seat}", BoardArt.Felt, at, diameter, felt, FeltOrder);

            // The centre: a gilt compass in a thin engraved ring (the home cells' ring, drawn larger).
            Sprite($"felt_emblem_ring_{seat}", DecoBoardArt.CellRing, at,
                DecoTableEmblemRing * spacing / (2f * DecoBoardArt.CellRingRadius), UiTheme.DecoHomeRing, FeltTrimOrder);
            var emblemArt = BoardSprites.Get(BoardSprites.TableEmblem);
            if (emblemArt != null)
                SlotSprite($"felt_emblem_{seat}", emblemArt, at, DecoTableEmblemSize * spacing, Color.white, FeltTrimOrder);
            else
                Sprite($"felt_emblem_{seat}", DecoBoardArt.Compass, at, DecoTableEmblemSize * spacing,
                    UiTheme.DecoTableEmblem, FeltTrimOrder);

            // A chair under every seat once the art exists (D3); Classic's marks until then.
            var chairArt = BoardSprites.Get(BoardSprites.Chair);
            for (int i = 0; i < seats; i++)
            {
                var place = layout.YardSeat(seat, i);
                if (chairArt != null)
                {
                    var chair = SlotSprite($"chair_{seat}_{i}", chairArt, place, DecoChairSize * spacing,
                        UiTheme.DecoChairTint(seatColour), SeatMarkOrder);
                    chair.transform.rotation = Quaternion.Euler(0f, 0f, DecoChairRotation(BoardLayout.SeatAngle(i)));
                }
                else
                {
                    Sprite($"seat_{seat}_{i}", Primitives.Disc, place, 0.46f * spacing, UiTheme.SeatMark, SeatMarkOrder);
                }
            }

            var rimArt = BoardSprites.Get(BoardSprites.TableRim);
            _sheen.Add(rimArt != null
                ? SlotSprite($"rim_{seat}", rimArt, at, diameter, UiTheme.DecoRimTint, RimOrder)
                : Sprite($"rim_{seat}", DecoBoardArt.TableRim, at, diameter, UiTheme.DecoRimTint, RimOrder));
        }

        /// <summary>
        /// One Deco cell: the painted slot art if there is any, else the
        /// procedural face, plus its trim (bevel and edge) when the cell needs
        /// one. Returns the trim, for the powered glint, or null when none is drawn.
        /// </summary>
        /// <param name="face">The colour the tile's face should read as.</param>
        /// <param name="trim"><see cref="DecoBoardArt.TrimGilt"/>, or <see cref="DecoBoardArt.TrimCyan"/> on a safe cell.</param>
        /// <param name="art">The slot's sprite, or null for the procedural tile.</param>
        /// <param name="tinted">Whether painted art is tinted by <paramref name="face"/> (a greyscale slot).</param>
        /// <param name="needTrim">
        /// Draw the procedural trim even over painted art, which carries its
        /// own. The procedural face always gets one.
        /// </param>
        private SpriteRenderer DecoTile(string name, Vector3 at, float size, Color face, Sprite trim,
            Sprite art, bool tinted, bool needTrim)
        {
            // The code draws every shadow (the contract: art never bakes one), under the tile.
            Sprite(name + "_shadow", DecoBoardArt.TileShadow, at + DecoTileShadowOffset * size,
                size * DecoBoardArt.TileShadowScale, UiTheme.DecoTileShadow, PowerGlowOrder);

            if (art != null)
            {
                SlotSprite(name, art, at, size, tinted ? face : Color.white, CellOrder);
                if (!needTrim) return null;
            }
            else
            {
                Sprite(name, DecoBoardArt.TileBody, at, size, DecoBoardArt.FaceTint(face), CellOrder);
            }

            return Sprite(name + "_trim", trim, at, size, UiTheme.DecoTrim, InlayOrder);
        }

        /// <summary>
        /// A sprite from the contract's slots, drawn <paramref name="size"/>
        /// across whatever its pixels-per-unit (<see cref="BoardSprites.UnitScale"/>).
        /// </summary>
        private SpriteRenderer SlotSprite(string name, Sprite sprite, Vector3 position, float size, Color colour, int order)
        {
            var renderer = Sprite(name, sprite, position, size, colour, order);
            renderer.transform.localScale *= BoardSprites.UnitScale(sprite);
            return renderer;
        }
    }
}
