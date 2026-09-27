// Assets/_Project/Scripts/Unity/View/UiTheme.cs
using NonaRoyale.Core.Board;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Every colour and type size the view draws with (GUI phase, increments
    /// G and G3).
    /// </summary>
    /// <remarks>
    /// <b>One file for the look.</b> The palette is ART_DIRECTION §3, still
    /// marked "proposal" there. Locking it means editing the hex strings here
    /// and nothing else: the HUD, the board and the pieces all read from this
    /// class.
    ///
    /// <b>Two registers</b> (ART_DIRECTION §2.1 and §8). Static chrome is warm:
    /// obsidian and charcoal surfaces, gilt gold and aged brass for trim and
    /// headings. Anything live is cool: selection, landings, a ready ability,
    /// energy, powered safe cells. Holo cyan never fills a surface at full
    /// strength; <see cref="CyanDeep"/> is the only cyan-leaning fill, and it
    /// marks a live control.
    ///
    /// <b>Threat is amber, not cyan</b> (decided 2026-09-15). A cell or piece
    /// a strike would land on is a warning, not a powered state, and amber
    /// stays readable next to a cyan selection ring on the same screen. Blood
    /// velvet would clash with the red seat's pieces (§6, contrast discipline).
    ///
    /// <b>Seat colours stay saturated.</b> They are gameplay information, and
    /// the palette's "accents, not base colours" rule is for the world, not
    /// for telling four players apart.
    ///
    /// <b>Gold is rationed</b> (increment G3, 2026-09-17). ART_DIRECTION §3
    /// allows roughly a fifth of a frame in gold; G and G2 ran past it with
    /// double rules everywhere. Full-strength gold marks only headings, the
    /// active seat's plate and ROLL. The richness moved into the board's
    /// surfaces instead.
    ///
    /// <b>Controls are borderless</b> (G7f, designer's pick 2026-09-27).
    /// Buttons, rows, chips and cards at rest have no edge; a slightly lighter
    /// fill (<see cref="ButtonFill"/>) separates them from the panel, and hover
    /// lightens it. The gold hairline (<see cref="Line"/>) is kept for what
    /// frames: floating cards with their corner fans, the floating pill and
    /// toasts, the docked bars' inner rule, dividers. A page of gold boxes read
    /// as a lattice; the frame alone is the Deco signature. Cyan edges still
    /// mean selected or ready, seat edges still mean whose.
    ///
    /// <b>Controls are plates</b> (G9a, designer's pick 2026-09-27). With the
    /// edges gone the controls lacked weight, so each one stands off its
    /// panel: a face lit from the top, a darker lip showing its thickness, a
    /// shadow on the table, a warm light along the top edge, and it sinks
    /// when pressed (<see cref="UiPlate"/>). The values are the Plate* block.
    ///
    /// <b>The match bars are furniture</b> (G9b, G9c). The top bar, the squad
    /// rail, the history strip and the tray are lacquered (<see cref="BarTop"/>
    /// to <see cref="BarFoot"/>) and end in a rounded edge catching the light
    /// (<see cref="BarLip"/>), with no line. The padded oxblood leather rail
    /// (<see cref="Leather"/>) is on the table, round the board, the way a
    /// gaming table has it. The dice and the health bar sit in wells.
    ///
    /// Static values, not a ScriptableObject (decided 2026-09-15): the HUD is
    /// built from code and caches colours when it builds, so Inspector tuning
    /// would not show live without extra plumbing.
    /// </remarks>
    public static class UiTheme
    {
        // ── ART_DIRECTION §3 swatches ───────────────────────────────────

        public static readonly Color Obsidian = Hex("0A0709");
        public static readonly Color Charcoal = Hex("141013");
        public static readonly Color Gunmetal = Hex("1A1519");
        public static readonly Color Gold = Hex("C99A3C");
        public static readonly Color GoldBright = Hex("F4D98B");
        public static readonly Color Brass = Hex("7C5A1E");
        public static readonly Color BloodVelvet = Hex("5A1626");
        public static readonly Color Emerald = Hex("0F6E56");
        public static readonly Color Ink = Hex("1C0E12");
        public static readonly Color Cyan = Hex("5FE0E8");
        public static readonly Color CyanBright = Hex("C9FBFF");

        // ── HUD surfaces ────────────────────────────────────────────────

        /// <summary>Docked panels: top bar, rail, tray, strip.</summary>
        public static readonly Color Panel = WithAlpha(Obsidian, 0.95f);

        /// <summary>Cards and rows that sit on a panel.</summary>
        public static readonly Color PanelRaised = WithAlpha(Charcoal, 0.97f);

        /// <summary>Chips and inset wells, one step lighter than a card.</summary>
        public static readonly Color PanelInset = Hex("211A1F");

        /// <summary>Floating things over the board: toasts, the turn pill, cell labels.</summary>
        public static readonly Color Scrim = WithAlpha(Obsidian, 0.88f);

        /// <summary>
        /// Frames and rules: one gilt hairline at half strength (GUI increment
        /// G3). Not on controls since G7f. Full gold is kept for headings, the
        /// active seat's plate and ROLL, so it means something where it appears.
        /// </summary>
        public static readonly Color Line = WithAlpha(Gold, 0.5f);

        /// <summary>
        /// An edge that is there but not seen, for a control whose edge is
        /// repainted later (the draft's focus edge, G7f).
        /// </summary>
        public static readonly Color NoEdge = new Color(0f, 0f, 0f, 0f);

        /// <summary>Corner fans on floating cards: present, never loud (G3).</summary>
        public const float FanAlpha = 0.4f;

        /// <summary>
        /// A floating panel's elevation (UI_MOTION.md increment U4): two hard
        /// offset shades under it, the far one larger and fainter, which reads
        /// as one soft shadow. Elevation lives here, not in per-widget alphas.
        /// </summary>
        public static readonly Color PanelShadowNear = WithAlpha(Color.black, 0.55f);
        public static readonly Color PanelShadowFar = WithAlpha(Color.black, 0.32f);
        public static readonly Vector2 PanelShadowNearOffset = new Vector2(0f, -8f);
        public static readonly Vector2 PanelShadowFarOffset = new Vector2(0f, -24f);

        /// <summary>A whisper of light at a panel's top edge, so the fill is not dead flat (U4).</summary>
        /// <remarks>
        /// 4%, was 10% (G7a). The project blends in linear colour space, where
        /// white at 10% over a near-black card reads as a grey wash rather than
        /// a whisper; it went unnoticed only because the sheen was drawn at the
        /// bottom of every card until G7a turned it the right way up.
        /// </remarks>
        public static readonly Color PanelSheen = WithAlpha(Color.white, 0.04f);

        /// <summary>
        /// A control at rest: the top of its plate. One step lighter than it
        /// was (G7f), because with no edge the fill alone has to lift it off
        /// the panel, and one more (G9a), because the plate shades down from
        /// this towards its foot.
        /// </summary>
        public static readonly Color ButtonFill = Hex("352A31");
        public static readonly Color ButtonOff = Hex("120E11");

        // ── Match bars (G9b) ──

        /// <summary>A match bar's lacquer: lit at the top, near black at the foot.</summary>
        public static readonly Color BarTop = WithAlpha(Hex("211819"), 0.97f);
        public static readonly Color BarFoot = WithAlpha(Hex("0A0708"), 0.97f);

        /// <summary>
        /// A bar's board-facing edge rolling toward the light (G9c): warm, and
        /// low, because the project blends in linear space.
        /// </summary>
        public static readonly Color BarLip = WithAlpha(Color.Lerp(GoldBright, Color.white, 0.6f), 0.07f);

        /// <summary>
        /// The table's padded rail (G9c): oxblood at the tube's highlight, near
        /// black in its folds, and a dashed seam of waxed brown thread.
        /// </summary>
        public static readonly Color Leather = Hex("6A2F3B");
        public static readonly Color LeatherFold = Hex("150709");
        public static readonly Color RailStitch = Hex("8C6A45");

        /// <summary>The rail's shadow on the table and the void: a near copy and a softer far one.</summary>
        public static readonly Color RailShadowNear = WithAlpha(Color.black, 0.55f);
        public static readonly Color RailShadowFar = WithAlpha(Color.black, 0.28f);

        /// <summary>
        /// A recess in a bar or a card: darker than the bar, but not black, so
        /// its inner shadow has something to darken (G9c: was 080607, which
        /// read as a flat hole).
        /// </summary>
        public static readonly Color Well = Hex("150F12");

        // ── Plates (G9a) ──

        /// <summary>A plate's foot, as a fraction of its tint; the top is the full tint.</summary>
        public const float PlateFaceFoot = 0.74f;

        /// <summary>The lip under a plate, as a fraction of its tint.</summary>
        public const float PlateLipShade = 0.42f;

        /// <summary>
        /// The light along a plate's top edge: warm, like the room's lamps.
        /// Low, because the project blends in linear space (see <see cref="PanelSheen"/>).
        /// </summary>
        public static readonly Color PlateRim = WithAlpha(Color.Lerp(GoldBright, Color.white, 0.6f), 0.16f);

        /// <summary>The shadow right under a plate's lip, and how far below the lip it shows.</summary>
        public static readonly Color PlateShadowNear = WithAlpha(Color.black, 0.5f);
        public const float PlateShadowNearDrop = 1.5f;

        /// <summary>
        /// The soft shadow further out: it reaches <see cref="PlateShadowFarReach"/>
        /// times the depth plus <see cref="PlateShadowFarDrop"/> below the face,
        /// so it tightens as the plate is pressed.
        /// </summary>
        public static readonly Color PlateShadowFar = WithAlpha(Color.black, 0.24f);
        public const float PlateShadowFarReach = 1.75f;
        public const float PlateShadowFarDrop = 3.5f;

        /// <summary>A live control's fill: a selected card, a ready Cast, END TURN.</summary>
        public static readonly Color CyanDeep = Hex("0C3A3E");

        /// <summary>The ROLL button's fill: static gold, dimmed to a surface.</summary>
        public static readonly Color GoldDeep = Hex("3E2C0C");

        public static readonly Color Track = new Color(1f, 1f, 1f, 0.08f);

        // ── Text ────────────────────────────────────────────────────────

        public static readonly Color Text = Hex("EDE6DA");
        public static readonly Color TextDim = Hex("A39A8C");
        public static readonly Color TextOff = Hex("655E56");

        /// <summary>
        /// Small explanatory lines on the full-screen cards (title, setup,
        /// end). Brighter than <see cref="TextDim"/> so 13 pt reads on the
        /// scrim, and still under <see cref="Text"/> so the buttons lead.
        /// </summary>
        public static readonly Color TextNote = Hex("CFC8BC");

        /// <summary>Section headings: small spaced capitals in gold.</summary>
        public static readonly Color Heading = Gold;

        // ── Game meaning ────────────────────────────────────────────────

        /// <summary>Low health, knockouts.</summary>
        public static readonly Color Danger = Hex("D0433F");

        /// <summary>Aim: pieces and cells a strike would land on.</summary>
        public static readonly Color Threat = Hex("F5B547");

        /// <summary>A refused command.</summary>
        public static readonly Color Reject = Hex("F2785C");

        public static readonly Color Damage = Hex("FF7366");
        public static readonly Color OverTime = Hex("D95ABF");
        public static readonly Color Heal = Hex("80F28C");
        public static readonly Color Evade = Cyan;
        public static readonly Color Block = Hex("D9DEF2");

        /// <summary>A hit safe ground voided: not the target's defence, the cell's.</summary>
        public static readonly Color Sheltered = Hex("E8C877");
        public static readonly Color Move = Hex("D9D6D0");

        /// <summary>
        /// Debt's numerals on the board (COMBAT_SYSTEMS §3.3): a pale oxblood,
        /// Revú's jacket lifted until it reads on the dark floor. Kept off the
        /// damage coral and the heal green so a numeral is never mistaken for
        /// health; the Roman figure and the display face do the rest.
        /// </summary>
        public static readonly Color Debt = Hex("E9A1A8");

        /// <summary>The plate a seat's debt sits on in the HUD: blood velvet, the one red the house allows.</summary>
        public static readonly Color DebtPlate = BloodVelvet;

        /// <summary>A debt burned by a collision: ember, not coral, since nothing was hurt.</summary>
        public static readonly Color DebtBurn = Hex("F2A65A");

        public static readonly Color DieFace = Hex("EDE3CC");
        public static readonly Color DieInk = Ink;

        // ── Board (ART_DIRECTION §6.1: atmospheric at rest) ─────────────

        /// <summary>The camera's clear colour, around the table.</summary>
        public static readonly Color BoardVoid = Hex("08060A");

        /// <summary>The square table the cross sits on.</summary>
        public static readonly Color BoardField = Hex("0E0B0E");

        /// <summary>Faint gold veins in the table.</summary>
        public static readonly Color BoardVeins = WithAlpha(Gold, 0.07f);

        /// <summary>
        /// The table's single gilt rule, inset from its edge (G3). Kept low:
        /// the table's corners stay dark.
        /// </summary>
        public static readonly Color TableRule = WithAlpha(Gold, 0.32f);

        /// <summary>
        /// The cross-shaped floor the track runs on. The marble sprite shades
        /// it down by up to a sixth, so it starts a touch above the old flat
        /// value.
        /// </summary>
        public static readonly Color CrossFloor = Hex("1B161E");

        /// <summary>
        /// The cross's tint when a painted marble is in use (G4): white, so
        /// the painting shows in its own colours.
        /// </summary>
        public static readonly Color PaintedFloor = Color.white;

        /// <summary>
        /// A painted carpet's tint on the square table (G4): dimmed well down,
        /// so the table stays the darkest surface and its corners stay quiet.
        /// </summary>
        public static readonly Color CarpetTint = new Color(0.42f, 0.4f, 0.42f, 1f);

        /// <summary>The Deco sunburst set into the cross floor (G3): texture, not ornament.</summary>
        public static readonly Color FloorPattern = WithAlpha(Gold, 0.04f);

        /// <summary>
        /// The room's light raking across the polished floor (V5). Warm, and
        /// within G3's few percent: the sheen has to read as the stone being
        /// polished, never as a shape competing with the lit cells.
        /// </summary>
        public static readonly Color FloorSheen = WithAlpha(Color.Lerp(GoldBright, Color.white, 0.35f), 0.07f);

        /// <summary>The cross's gilt edge, thinner since G3.</summary>
        public static readonly Color CrossEdge = WithAlpha(Color.Lerp(Brass, Gold, 0.45f), 0.9f);

        /// <summary>The lane down the middle of each arm: the home column's road.</summary>
        public static readonly Color CrossLane = WithAlpha(Brass, 0.42f);

        /// <summary>
        /// Warm haze drifting in the light pools (G3). The pools' lights tint
        /// it further, so it all but vanishes in the dark.
        /// </summary>
        public static readonly Color Haze = WithAlpha(Hex("FFE6C4"), 0.035f);

        /// <summary>Warm light pooled in each arm.</summary>
        public static readonly Color ArmGlow = WithAlpha(Gold, 0.07f);

        /// <summary>Drop shadows under the cross and the tables.</summary>
        public static readonly Color Shadow = WithAlpha(Color.black, 0.75f);

        /// <summary>
        /// A track cell at rest: a whisper of marble and inlay (decided
        /// 2026-09-15). Your turn's landings light up over it.
        /// </summary>
        public static readonly Color CellWhisper = WithAlpha(Hex("2A2328"), 0.35f);
        public static readonly Color CellInlay = WithAlpha(Gold, 0.16f);

        /// <summary>A safe cell: still a whisper, but powered.</summary>
        public static readonly Color SafeInlay = WithAlpha(Cyan, 0.45f);
        public static readonly Color SafeGlow = WithAlpha(Cyan, 0.13f);

        /// <summary>A start cell's inlay alpha, in its seat's colour.</summary>
        public const float StartInlayAlpha = 0.6f;

        /// <summary>A home column's seat wash and inlay alpha, at its mouth and beside HOME.</summary>
        public const float HomeWashNear = 0.05f;
        public const float HomeWashFar = 0.16f;
        public const float HomeInlayNear = 0.22f;
        public const float HomeInlayFar = 0.5f;

        // ── Deco board skin (BS2, BOARD_SKIN.md) ────────────────────────

        /// <summary>
        /// A Deco track tile's face: near-black lacquer, held at a lower
        /// contrast than the visual target so the turn's highlights still pop
        /// over it (D1, 2026-09-27).
        /// </summary>
        public static readonly Color DecoTrackFace = Hex("1E191C");

        /// <summary>
        /// A Deco tile's gilt hairline, in shade and in the light. Baked into
        /// the trim sprite (<c>DecoBoardArt.TrimGilt</c>); a safe cell's trim
        /// uses <see cref="Cyan"/> and <see cref="CyanBright"/> instead.
        /// </summary>
        public static readonly Color DecoGilt = Color.Lerp(Brass, Gold, 0.7f);
        public static readonly Color DecoGiltLight = Color.Lerp(Gold, GoldBright, 0.6f);

        /// <summary>How strongly the trim (bevel and hairline) draws over a Deco tile.</summary>
        public static readonly Color DecoTrim = WithAlpha(Color.white, 0.85f);

        /// <summary>How much of its seat colour a home-column tile and a start tile keep.</summary>
        public const float DecoHomeFace = 0.5f;
        public const float DecoStartFace = 0.62f;

        /// <summary>The home column's engraved ring.</summary>
        public static readonly Color DecoHomeRing = WithAlpha(Color.Lerp(Gold, GoldBright, 0.35f), 0.8f);

        /// <summary>The start cell's compass: pale gold, the one bright mark on the path.</summary>
        public static readonly Color DecoEmblem = Color.Lerp(GoldBright, Color.white, 0.35f);

        /// <summary>The Deco medallion's face (BS3): black lacquer, a shade under the tiles.</summary>
        public static readonly Color DecoMedallionFace = Hex("0D0A0C");

        /// <summary>
        /// The Deco tables' felt (BS4): the seat colour, deeper than a tile
        /// face. 0.8 in BS4 made the tables the loudest thing on the board
        /// (BS5 checkpoint); 0.66 keeps them the seat's own jewel tone.
        /// </summary>
        public const float DecoFeltBrightness = 0.66f;

        /// <summary>The Deco table rim's tint (BS6): the baked gilt, dimmed so the rims sit under the path.</summary>
        public static readonly Color DecoRimTint = new Color(0.8f, 0.8f, 0.8f, 1f);

        /// <summary>The corner wedges' tint (BS6): darker gilt, so the four read as spikes, not a gold X.</summary>
        public static readonly Color DecoWedgeTint = new Color(0.82f, 0.82f, 0.82f, 1f);

        /// <summary>The gilt lattice between the Deco tiles, and its rivets (BS6).</summary>
        public static readonly Color DecoLattice = WithAlpha(Color.Lerp(DecoGilt, Brass, 0.3f), 0.85f);
        public static readonly Color DecoRivet = WithAlpha(DecoGiltLight, 0.9f);

        /// <summary>How much of its arm's seat colour a corner facet keeps (BS6).</summary>
        public const float DecoFacetFace = 0.6f;

        /// <summary>A Deco yard panel's lacquer (BS7), a shade over the board's black.</summary>
        public static readonly Color DecoYardFloor = Hex("100C0F");

        /// <summary>How far the yard panel's lacquer leans toward its seat colour (BS7).</summary>
        public const float DecoYardWarmth = 0.08f;

        /// <summary>How much of the seat colour the yard panel's band keeps (BS7).</summary>
        public const float DecoYardBand = 0.75f;

        /// <summary>The yard panel's gilt frame and corners (BS7), a little under full so the table's rim leads.</summary>
        public static readonly Color DecoYardGilt = new Color(0.85f, 0.85f, 0.85f, 1f);

        /// <summary>A yard panel's fill: the lacquer, leaning a little toward its seat.</summary>
        public static Color DecoYardFill(Color seat)
        {
            var fill = Color.Lerp(DecoYardFloor, seat, DecoYardWarmth);
            fill.a = 1f;
            return fill;
        }

        /// <summary>The Deco table's centre compass: gilt, between shade and light.</summary>
        public static readonly Color DecoTableEmblem = Color.Lerp(DecoGilt, DecoGiltLight, 0.55f);

        /// <summary>The tint for a painted, greyscale chair: its seat, lifted a little toward white.</summary>
        public static Color DecoChairTint(Color seat) => Color.Lerp(seat, Color.white, 0.15f);

        /// <summary>
        /// A Deco tile's soft drop shadow (BS5). 0.7 in BS5; 0.5 since BS7, with
        /// a shorter offset and a softer bevel, when the designer asked for the
        /// cells a little less raised.
        /// </summary>
        public static readonly Color DecoTileShadow = WithAlpha(Color.black, 0.5f);

        /// <summary>The corner wedges' crisp shadow (BS5).</summary>
        public static readonly Color DecoWedgeShadow = WithAlpha(Color.black, 0.55f);

        /// <summary>A seat colour darkened to a tile face, keeping <paramref name="keep"/> of it.</summary>
        public static Color DecoSeatFace(Color seat, float keep) =>
            new Color(seat.r * keep, seat.g * keep, seat.b * keep, 1f);

        /// <summary>The tint for a painted, greyscale start emblem: its seat, lifted most of the way to white.</summary>
        public static Color DecoEmblemTint(Color seat) => Color.Lerp(seat, Color.white, 0.6f);

        /// <summary>Felt: the seat colour, darkened. The felt sprite shades it further toward the rim.</summary>
        public const float FeltBrightness = 0.62f;

        /// <summary>The tables' gilt rim: gold warmed toward the highlight. The sprite adds the streak.</summary>
        public static readonly Color TableRim = Color.Lerp(Gold, GoldBright, 0.6f);

        /// <summary>
        /// The table's side, seen along its near edge under the tilt (V2): a
        /// warm dark the surface can sit a step above. The sprite carries the
        /// fall from the lip, so this is the colour at its brightest.
        /// </summary>
        /// <remarks>
        /// Set by eye against the void, not measured. It and
        /// <see cref="TableBand"/> are the first two values to try in Play
        /// Mode if the slab reads as a hole or as a shelf.
        /// </remarks>
        public static readonly Color TableEdge = Color.Lerp(Gunmetal, Brass, 0.45f);

        /// <summary>The gilt band at the table's lip, at the top of its roll.</summary>
        public static readonly Color TableBand = Color.Lerp(Brass, Gold, 0.55f);

        /// <summary>Dotted ring, arc and chips on the felt.</summary>
        public static readonly Color FeltTrim = WithAlpha(Gold, 0.5f);
        public static readonly Color FeltChip = WithAlpha(GoldBright, 0.85f);

        /// <summary>An empty seat at a table: where an operator stood up from.</summary>
        public static readonly Color SeatMark = WithAlpha(Color.black, 0.35f);

        public static readonly Color VaultPlate = Hex("0F0B08");
        public static readonly Color VaultFrame = Color.Lerp(Gold, GoldBright, 0.35f);
        public static readonly Color VaultGlow = WithAlpha(GoldBright, 0.28f);
        public static readonly Color VaultBoss = GoldBright;

        // ── Pieces ──────────────────────────────────────────────────────

        /// <summary>The silhouette outline (ART_DIRECTION §5).</summary>
        public static readonly Color PieceOutline = Ink;

        public static readonly Color PieceWaiting = Hex("66666B");

        /// <summary>The operator's shape, worn as a gilt pin on the figure.</summary>
        public static readonly Color PieceEmblem = GoldBright;

        /// <summary>How far a figure's seat colour is lifted, since the figure sprite shades it down.</summary>
        public const float FigureLift = 0.12f;
        public static readonly Color PieceBarBack = WithAlpha(Obsidian, 0.85f);
        public static readonly Color Select = Cyan;

        // ── Look book (OPERATOR_LOOKBOOK.md) ────────────────────────────
        // Procedural operator figures, drawn to ART_DIRECTION §2.2. The ink,
        // brass, obsidian and cyan come from the §3 swatches above.

        /// <summary>The drawn cool rim on a dark figure: steel, never holo cyan (§5: devices are dark at rest).</summary>
        public static readonly Color LookRim = Hex("8C9DB0");

        /// <summary>The same rim on a light figure, where steel would read as a shadow.</summary>
        public static readonly Color LookRimOnLight = Hex("E6EEF5");

        /// <summary>Multiplied into a figure's hard shadow shape: the cool dark ambient (§2.2 rule 5).</summary>
        public static readonly Color LookShade = Hex("7E7A94");

        /// <summary>Screened into a hard highlight shape: the warm gold key from the upper left.</summary>
        public static readonly Color LookKey = Hex("4A3A22");

        /// <summary>A dark suit's lit planes. Cooler than the key, or black cloth goes brown.</summary>
        public static readonly Color LookSuitSheen = Hex("3A3548");

        /// <summary>The hard specular wedge on black plate.</summary>
        public static readonly Color LookPlateSheen = Hex("3E3C48");

        /// <summary>Shirts and collars: bone, not white.</summary>
        public static readonly Color LookBone = Hex("E8E1D3");

        // One block per operator, per the §5.1 value ledger (LB2).

        // Bouncer: black mass split by a hard white V.
        public static readonly Color LookBouncerSuit = Hex("221D26");
        public static readonly Color LookBouncerSkin = Hex("7A5140");

        // Syla: light core in a dark frame.
        public static readonly Color LookSylaGown = Hex("E4DCCB");
        public static readonly Color LookSylaCape = Hex("121014");
        public static readonly Color LookSylaSkin = Hex("D9C6B4");

        // Kurbyn: mid-dark, broken by bare forearms.
        public static readonly Color LookKurbynCloth = Hex("3A3538");
        public static readonly Color LookKurbynSkin = Hex("D2B59C");

        // Javi: dark waistcoat block, two white sleeves.
        public static readonly Color LookJaviWaistcoat = Hex("2E2A2F");
        public static readonly Color LookJaviGlove = Hex("8B8F94");
        public static readonly Color LookJaviHair = Hex("8E8A84");
        public static readonly Color LookJaviSkin = Hex("B99A80");
        public static readonly Color LookJaviSteel = Hex("7E878D");
        public static readonly Color LookJaviFrost = Hex("D8E0E3");

        // Sanity: large mid-brown mass. Umber, never tan (tan is Luka's value).
        public static readonly Color LookSanityApron = Hex("5A4231");
        public static readonly Color LookSanityLivery = Hex("2E2A2E");
        public static readonly Color LookSanitySkin = Hex("9A7560");
        public static readonly Color LookSanitySteel = Hex("6F777D");

        // Mimi: near-black and small, bright pale hardware.
        public static readonly Color LookMimiCoat = Hex("141117");
        public static readonly Color LookMimiRig = Hex("DCE3E6");
        public static readonly Color LookMimiSteel = Hex("8E9AA3");
        public static readonly Color LookMimiSkin = Hex("CDBBAA");

        // Revú: the only red torso. Oxblood darker than blood velvet (§3, §6.1).
        public static readonly Color LookRevuJacket = Hex("4A1320");
        public static readonly Color LookRevuTrousers = Hex("2A262B");
        public static readonly Color LookRevuHair = Hex("A9A9AD");
        public static readonly Color LookRevuSkin = Hex("C3A58E");

        // Kian: the only green torso, in the §3 emerald itself.
        public static readonly Color LookKianJacket = Emerald;
        public static readonly Color LookKianShirt = Hex("141117");
        public static readonly Color LookKianSkin = Hex("BFA088");

        // Luka: the warm light torso, camel, which only he may carry (§5.1).
        // Matched to his render; the rig draws only if the render is missing.
        public static readonly Color LookLukaBlazer = Hex("B7A083");
        public static readonly Color LookLukaShirt = Hex("1C181B");
        public static readonly Color LookLukaSkin = Hex("C28B6B");

        // Fortuna: the only gold-dominant figure, and the only bright metal (§3, §5.1).
        public static readonly Color LookFortunaCloth = Hex("2B272D");
        public static readonly Color LookFortunaShirt = Hex("17141A");
        public static readonly Color LookFortunaSkin = Hex("C9A58C");
        public static readonly Color LookFortunaHair = Hex("1B1416");
        public static readonly Color LookFortunaGold = Gold;
        public static readonly Color LookFortunaGoldLight = GoldBright;

        // Lethe: the only true mid-grey figure; tarnished silver, never gold.
        public static readonly Color LookLetheGown = Hex("7C7A82");
        public static readonly Color LookLetheGlove = Hex("66646C");
        public static readonly Color LookLetheSilver = Hex("A3A4A6");
        public static readonly Color LookLetheSkin = Hex("D6C8BE");
        public static readonly Color LookLetheHair = Hex("221E24");

        // Nuetu: the only all-light mass, cool dove-grey, black plates on grey.
        public static readonly Color LookNuetuGrey = Hex("B3B7C1");
        public static readonly Color LookNuetuPlate = Hex("17141A");
        public static readonly Color LookNuetuSkin = Hex("A5836B");

        // ── Seats ───────────────────────────────────────────────────────

        public static readonly Color SeatRed = new Color(0.84f, 0.27f, 0.31f);
        public static readonly Color SeatBlue = new Color(0.32f, 0.56f, 0.88f);
        public static readonly Color SeatGreen = new Color(0.34f, 0.72f, 0.44f);
        public static readonly Color SeatViolet = new Color(0.50f, 0.40f, 0.84f);
        public static readonly Color SeatNone = Hex("7A7570");

        public static Color Seat(PlayerColor colour)
        {
            switch (colour)
            {
                case PlayerColor.Red: return SeatRed;
                case PlayerColor.Blue: return SeatBlue;
                case PlayerColor.Green: return SeatGreen;
                case PlayerColor.Violet: return SeatViolet;
                default: return SeatNone;
            }
        }

        /// <summary>A seat colour lifted toward white, readable on a dark panel.</summary>
        public static Color Readable(Color seat) => Color.Lerp(seat, Color.white, 0.35f);

        // ── Type ────────────────────────────────────────────────────────

        public const float FontTiny = 11f;
        public const float FontSmall = 15f;
        public const float FontBody = 18f;
        public const float FontLarge = 22f;
        public const float FontTitle = 26f;

        /// <summary>TMP character spacing for headings, in em/100. Deco capitals breathe.</summary>
        public const float HeadingSpacing = 12f;

        // ── Helpers ─────────────────────────────────────────────────────

        /// <summary>Hex for rich-text colour tags.</summary>
        public static string Hex(Color colour) => ColorUtility.ToHtmlStringRGB(colour);

        public static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }

        /// <summary>Dark ink on a light background, ivory on a dark one.</summary>
        public static Color TextOn(Color background)
        {
            float luminance = 0.2126f * background.r + 0.7152f * background.g + 0.0722f * background.b;
            return luminance < 0.5f ? Text : DieInk;
        }

        /// <summary>
        /// "RRGGBB" to a colour, in plain C#.
        /// </summary>
        /// <remarks>
        /// Not <c>ColorUtility.TryParseHtmlString</c>: that is a native call,
        /// and these fields initialise whenever the class is first touched,
        /// which should never depend on which thread touched it.
        /// </remarks>
        private static Color Hex(string rgb)
        {
            int value = System.Convert.ToInt32(rgb, 16);
            return new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
        }
    }
}
