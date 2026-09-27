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
    /// <b>Readability is the constraint.</b> Highlights, reach and targets
    /// draw on the Default layer, above the whole board layer, so they
    /// always land on top of the tiles; the tiles are held dark enough that
    /// the cyan and amber markers still pop.
    /// </remarks>
    public sealed partial class BoardView
    {
        /// <summary>The start cell's compass, as a fraction of the cell.</summary>
        private const float DecoEmblemSize = 0.72f;

        /// <summary>The Deco path: tiles for the shared track and the four home columns.</summary>
        private void DrawDecoTrack(PathMap map, BoardLayout layout)
        {
            var profile = map.Profile;
            float cell = layout.CellSize;

            var starts = new Dictionary<int, PlayerColor>();
            foreach (var seat in Seats) starts[map.StartTrackIndex(seat)] = seat;

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

                var wedge = wedgeArt != null
                    ? SlotSprite($"wedge_{k}", wedgeArt, at + offset, span, Color.white, VaultOrder)
                    : Sprite($"wedge_{k}", DecoBoardArt.CornerWedge, at + offset, span, Color.white, VaultOrder);
                wedge.transform.rotation = turn;
            }

            // Under the wedges' bases and the disc: the medallion stands proud of the floor.
            Sprite("medallion_shadow", BoardArt.SoftDisc, at + new Vector3(0.04f, -0.07f, 0f) * spacing,
                diameter * 1.06f, UiTheme.Shadow, VaultGlowOrder);

            var discArt = BoardSprites.Get(BoardSprites.Medallion);
            if (discArt != null) SlotSprite("medallion", discArt, at, diameter, Color.white, VaultTrimOrder);
            else Sprite("medallion", DecoBoardArt.Medallion, at, diameter, Color.white, VaultTrimOrder);

            var emblemArt = BoardSprites.Get(BoardSprites.MedallionEmblem);
            float emblem = diameter * DecoEmblemOfMedallion;
            if (emblemArt != null) SlotSprite("medallion_emblem", emblemArt, at, emblem, Color.white, VaultBossOrder);
            else Sprite("medallion_emblem", DecoBoardArt.MedallionEmblem, at, emblem, Color.white, VaultBossOrder);
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
