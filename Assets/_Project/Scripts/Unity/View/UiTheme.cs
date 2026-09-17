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
    /// double rules everywhere. Resting edges are now one hairline of
    /// <see cref="Line"/>, and full-strength gold marks only headings, the
    /// active seat's plate and ROLL. The richness moved into the board's
    /// surfaces instead.
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
        /// Every resting edge and rule: one gilt hairline at half strength
        /// (GUI increment G3). Full gold is kept for headings, the active
        /// seat's plate and ROLL, so it means something where it appears.
        /// </summary>
        public static readonly Color Line = WithAlpha(Gold, 0.5f);

        /// <summary>Corner fans on floating cards: present, never loud (G3).</summary>
        public const float FanAlpha = 0.4f;

        public static readonly Color ButtonFill = Hex("231B20");
        public static readonly Color ButtonOff = Hex("120E11");

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
        public static readonly Color Move = Hex("D9D6D0");

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

        /// <summary>The Deco sunburst set into the cross floor (G3): texture, not ornament.</summary>
        public static readonly Color FloorPattern = WithAlpha(Gold, 0.04f);

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

        /// <summary>Felt: the seat colour, darkened. The felt sprite shades it further toward the rim.</summary>
        public const float FeltBrightness = 0.62f;

        /// <summary>The tables' gilt rim: gold warmed toward the highlight. The sprite adds the streak.</summary>
        public static readonly Color TableRim = Color.Lerp(Gold, GoldBright, 0.6f);

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
