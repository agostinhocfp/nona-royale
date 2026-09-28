// Assets/_Project/Scripts/Unity/View/DecoBoardArt.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The Deco skin's procedural board art: the cells (board skin BS2) — a
    /// tile face, its trim (bevel and gilt edge), a gilt ring for the home
    /// columns and a compass for the start cells — the centre (BS3) — the
    /// medallion, its emblem and the four corner wedges — the tables' heavy
    /// gilt rim (BS4), the tiles' soft drop shadow (BS5), the corner facets
    /// (BS6), the yard panels' frame (BS7) and the steel table frame (BS8).
    /// Built once.
    /// </summary>
    /// <remarks>
    /// <b>The fallback, not the ceiling.</b> Each piece has a sprite slot in
    /// the contract (<see cref="BoardSprites"/>): <c>board_cell_track</c>,
    /// <c>board_cell_home</c> and <c>board_start_emblem</c>. When a slot is
    /// filled, the board draws the painted art instead and this goes unused.
    ///
    /// <b>Face tinted, trim baked.</b> The face is greyscale, painted at
    /// <see cref="FaceValue"/>; <see cref="FaceTint"/> turns the face colour a
    /// caller wants into the tint that produces it, so one sprite serves the
    /// track and every seat. The trim carries its own colours: a white
    /// highlight on the top and left bevels, a black shade on the bottom and
    /// right ones, and the gilt (or, on a safe cell, cyan) hairline. A tinted
    /// bevel could only darken, and on a near-black tile it vanished, so the
    /// bevel is light and shadow laid over the face instead. The light comes
    /// from the upper left, as everywhere on the board (ART §2.2 rule 5).
    ///
    /// <b>Mipmapped, unlike the rest of the procedural art</b>, because the
    /// cells are small on a phone and seen at a slant under the tilted camera,
    /// where an unmipmapped hairline crawls. Row 0 is the bottom.
    /// </remarks>
    public static class DecoBoardArt
    {
        /// <summary>The texture size of the cell sprites, in pixels. Every sprite here is one unit across.</summary>
        public const int Size = 128;

        /// <summary>The texture size of the centre's sprites (BS3), in pixels: drawn larger than a cell.</summary>
        public const int CentreSize = 256;

        /// <summary>The texture size of the table rim (BS4): a table is five spacings across.</summary>
        public const int TableSize = 512;

        /// <summary>
        /// The table rim's inner edge, as a fraction of the table's radius. The
        /// felt runs a little under it, so no seam shows where they meet.
        /// </summary>
        public const float RimInner = 0.87f;

        /// <summary>The engraved gilt line on the felt, inside the rim, as a fraction of the radius.</summary>
        public const float FeltLine = 0.79f;

        /// <summary>The brightness the tile's face is painted at, 0 to 1.</summary>
        public const float FaceValue = 0.72f;

        /// <summary>The corners' chamfer, in pixels of <see cref="Size"/>.</summary>
        private const float Chamfer = 7f;

        /// <summary>The bevel's width, in pixels of <see cref="Size"/>. 9 until BS7, 7 until BS9 (flatter cells).</summary>
        private const float Bevel = 5f;

        /// <summary>The gilt edge's width, in pixels of <see cref="Size"/>.</summary>
        private const float EdgeWidth = 3.2f;

        /// <summary>The engraved ring's radius, as a fraction of its sprite's width.</summary>
        public const float CellRingRadius = 0.31f;

        /// <summary>Where the home column's ring sits and how thick it is, in pixels.</summary>
        private const float RingRadius = CellRingRadius * Size;
        private const float RingWidth = 2.6f;

        private static Sprite _body, _trimGilt, _trimCyan, _ring, _compass;
        private static Sprite _medallion, _medallionEmblem, _wedge, _tableRim, _tileShadow, _facetA, _facetB;
        private static Sprite _yardFrame;

        /// <summary>
        /// How far the tile shadow's sprite reaches past the tile, as a
        /// fraction of the sprite: it is drawn <see cref="TileShadowScale"/>
        /// times the tile, and the soft falloff lives in that margin.
        /// </summary>
        /// <remarks>1.3 until BS9: a tighter falloff sits the tiles closer to the board.</remarks>
        public const float TileShadowScale = 1.2f;

        /// <summary>A square tile's face, edge to edge, greyscale, with a faint tinted bevel.</summary>
        public static Sprite TileBody => _body != null ? _body : (_body = Raster("deco_tile_body", BodyAt));

        /// <summary>The tile's trim with a gilt hairline: bevel light and shade, and the edge.</summary>
        public static Sprite TrimGilt => _trimGilt != null ? _trimGilt
            : (_trimGilt = Raster("deco_trim_gilt", (x, y) => TrimAt(x, y, UiTheme.DecoGilt, UiTheme.DecoGiltLight)));

        /// <summary>The same trim with a cyan hairline, for a powered (safe) cell.</summary>
        public static Sprite TrimCyan => _trimCyan != null ? _trimCyan
            : (_trimCyan = Raster("deco_trim_cyan", (x, y) => TrimAt(x, y, UiTheme.Cyan, UiTheme.CyanBright)));

        /// <summary>A thin engraved ring at the cell's centre, for the home columns.</summary>
        public static Sprite CellRing => _ring != null ? _ring : (_ring = Raster("deco_cell_ring", RingAt));

        /// <summary>An eight-point Deco compass, faceted light and dark, for the start cells.</summary>
        public static Sprite Compass => _compass != null ? _compass : (_compass = Raster("deco_compass", CompassAt));

        /// <summary>
        /// The tint that makes <see cref="TileBody"/>'s face read as
        /// <paramref name="face"/>. Alpha passes through.
        /// </summary>
        public static Color FaceTint(Color face)
        {
            return new Color(
                Mathf.Clamp01(face.r / FaceValue),
                Mathf.Clamp01(face.g / FaceValue),
                Mathf.Clamp01(face.b / FaceValue),
                face.a);
        }

        // ── Shapes ──────────────────────────────────────────────────────

        private static Color BodyAt(float px, float py)
        {
            float d = ChamferedSquare(px, py);
            float alpha = Cover(d);
            float depth = -d;

            // The face: a touch lighter toward the top, as the key light falls.
            float face = FaceValue * (0.95f + 0.1f * (py / Size));

            // The bevel faces the nearest edge: top and left catch the light.
            // Kept close to the face since BS9 (was 0.88 / 0.55 / 0.84 / 0.6), for flatter cells.
            float nx = px - Size * 0.5f;
            float ny = py - Size * 0.5f;
            float bevel = Mathf.Abs(ny) >= Mathf.Abs(nx)
                ? (ny > 0f ? 0.8f : 0.63f)
                : (nx < 0f ? 0.78f : 0.66f);

            float t = Mathf.Clamp01(depth / Bevel);
            float lum = Mathf.Lerp(bevel, face, t * t * (3f - 2f * t));

            // A faint groove where the bevel meets the face.
            lum *= 1f - 0.22f * Line(Mathf.Abs(depth - Bevel), 1.2f);

            return Grey(lum, alpha);
        }

        private static Color TrimAt(float px, float py, Color edge, Color edgeLight)
        {
            float d = ChamferedSquare(px, py);
            float inside = Cover(d);
            float depth = -d;

            float nx = px - Size * 0.5f;
            float ny = py - Size * 0.5f;
            bool vertical = Mathf.Abs(ny) >= Mathf.Abs(nx);
            bool lit = vertical ? ny > 0f : nx < 0f;

            // The bevel, just inside the hairline: light on the top and left,
            // shade on the bottom and right, fading toward the face.
            float t = Mathf.Clamp01((depth - EdgeWidth) / Bevel);
            float fade = 1f - t * t * (3f - 2f * t);
            // Lowered twice at the designer's call: about a third off in BS7, about 40 % more in BS9.
            float strength = lit ? (vertical ? 0.11f : 0.07f) : (vertical ? 0.2f : 0.16f);
            var bevel = lit ? Color.white : Color.black;
            float bevelAlpha = strength * fade * inside;

            // The hairline, lit on the top and left, with one glint at the
            // top-left corner, where the light lands.
            float cx = px - 6f;
            float cy = py - (Size - 6f);
            float glint = Mathf.Exp(-(cx * cx + cy * cy) / (2f * 14f * 14f));
            float sideLight = lit ? (vertical ? 1f : 0.85f) : (vertical ? 0.2f : 0.35f);
            var hair = Color.Lerp(edge * 0.7f, edgeLight, Mathf.Clamp01(sideLight * 0.8f + 0.5f * glint));
            float hairAlpha = inside * Mathf.Clamp01(EdgeWidth + 0.5f + d);

            // The hairline over the bevel. Colour is kept where alpha is 0, so
            // mipmaps don't pull the edges toward black or white.
            float alpha = hairAlpha + bevelAlpha * (1f - hairAlpha);
            var colour = alpha > 1e-4f
                ? (hair * hairAlpha + bevel * bevelAlpha * (1f - hairAlpha)) / alpha
                : hair;
            colour.a = alpha;
            return colour;
        }

        private static Color RingAt(float px, float py)
        {
            float nx = px - Size * 0.5f;
            float ny = py - Size * 0.5f;
            float r = Mathf.Sqrt(nx * nx + ny * ny);

            float alpha = Cover(Mathf.Abs(r - RingRadius) - RingWidth * 0.5f);

            // Engraved: lit on the side facing away from the light, like a groove.
            float inv = 1f / Mathf.Max(r, 0.001f);
            float facing = (-0.6f * nx + 0.8f * ny) * inv;
            float lum = 0.72f + 0.28f * facing;

            return Grey(lum, alpha);
        }

        /// <summary>Tips at the four cardinal points, shorter ones on the diagonals, valleys between.</summary>
        private static readonly Vector2[] CompassOutline = BuildCompassOutline();

        private static Vector2[] BuildCompassOutline()
        {
            const float longTip = 0.47f * Size;
            const float shortTip = 0.3f * Size;
            const float valley = 0.1f * Size;

            var points = new Vector2[16];
            for (int i = 0; i < 8; i++)
            {
                float tipAngle = i * 45f * Mathf.Deg2Rad;
                float tip = i % 2 == 0 ? longTip : shortTip;
                points[2 * i] = new Vector2(Mathf.Cos(tipAngle), Mathf.Sin(tipAngle)) * tip;

                float valleyAngle = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                points[2 * i + 1] = new Vector2(Mathf.Cos(valleyAngle), Mathf.Sin(valleyAngle)) * valley;
            }

            return points;
        }

        private static Color CompassAt(float px, float py)
        {
            var p = new Vector2(px - Size * 0.5f, py - Size * 0.5f);
            float d = PolygonDistance(p, CompassOutline);
            float alpha = Cover(d);

            // Faceted: each point is split down its axis, one half lit and one
            // half in shade, turning with the light from the upper left.
            float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            float nearestTip = Mathf.Round(angle / 45f) * 45f;
            bool counterClockwise = Mathf.DeltaAngle(nearestTip, angle) > 0f;
            float lum = counterClockwise ? 1f : 0.62f;

            // A small dark boss at the centre, ringed.
            float r = p.magnitude;
            float boss = Cover(r - 0.055f * Size);
            lum = Mathf.Lerp(lum, 0.3f, boss);
            lum = Mathf.Lerp(lum, 1f, Cover(Mathf.Abs(r - 0.055f * Size) - 0.8f) * 0.8f);

            return Grey(lum, alpha);
        }

        // ── The centre (BS3) ───────────────────────────────────────────

        /// <summary>
        /// The corner wedge, in cell spacings from the board's centre along
        /// its diagonal, shaped by the cells it must pass. It starts under the
        /// medallion; it is at its narrowest where it slips between the
        /// corners of the two last home cells (0.71 out, where only 0.1 is
        /// free on either side); it swells in the open corner of the centre
        /// square; and it ends in a point short of the cross's inner corner
        /// (2.12 out). <c>DecoCentreTests</c> holds it clear of every cell.
        /// </summary>
        public const float WedgeFrom = 0.42f;
        public const float WedgeWaistAt = 0.72f;
        public const float WedgeWaistHalfWidth = 0.05f;
        public const float WedgeWidestAt = 1.3f;
        public const float WedgeHalfWidth = 0.13f;
        public const float WedgeTo = 2.05f;

        /// <summary>
        /// The square the wedge sprite covers, in spacings: from the board's
        /// centre to the cross's inner corner. The sprite's own centre sits
        /// half of this up and right of the board's centre, before rotating.
        /// </summary>
        public const float WedgeSpan = 1.5f;

        /// <summary>A black lacquer disc with a heavy bevelled gilt rim, lit from the upper left.</summary>
        public static Sprite Medallion => _medallion != null ? _medallion
            : (_medallion = Raster("deco_medallion", MedallionAt, CentreSize));

        /// <summary>The medallion's gilt diamond: an outline, a faceted core and rays, on transparent.</summary>
        public static Sprite MedallionEmblem => _medallionEmblem != null ? _medallionEmblem
            : (_medallionEmblem = Raster("deco_medallion_emblem", EmblemAt, CentreSize));

        /// <summary>One gilt wedge, pointing up and right from the board's centre, faceted down its spine.</summary>
        public static Sprite CornerWedge => _wedge != null ? _wedge
            : (_wedge = Raster("deco_corner_wedge", WedgeAt, CentreSize));

        /// <summary>
        /// The wedge's half-width at <paramref name="t"/> spacings along its
        /// diagonal, or a negative number where there is no wedge.
        /// </summary>
        public static float WedgeHalfWidthAt(float t)
        {
            if (t < WedgeFrom || t > WedgeTo) return -1f;
            if (t <= WedgeWaistAt) return WedgeWaistHalfWidth;
            if (t <= WedgeWidestAt)
                return Mathf.Lerp(WedgeWaistHalfWidth, WedgeHalfWidth, (t - WedgeWaistAt) / (WedgeWidestAt - WedgeWaistAt));
            return Mathf.Lerp(WedgeHalfWidth, 0f, (t - WedgeWidestAt) / (WedgeTo - WedgeWidestAt));
        }

        /// <summary>
        /// True inside the wedge, at <paramref name="t"/> spacings along its
        /// diagonal and <paramref name="s"/> across it.
        /// </summary>
        public static bool InWedge(float t, float s) => Mathf.Abs(s) < WedgeHalfWidthAt(t);

        private static Color MedallionAt(float px, float py)
        {
            float half = CentreSize * 0.5f;
            float dx = (px - half) / half;
            float dy = (py - half) / half;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Cover((r - 1f) * half + 0.5f);

            float inv = 1f / Mathf.Max(r, 0.0001f);
            float facing = (-0.6f * dx + 0.8f * dy) * inv;

            const float rimInner = 0.83f;
            Color colour;

            if (r >= rimInner)
            {
                // The rim: a rounded bead lit from the upper left, darker where it turns away.
                float t = Mathf.Clamp01((r - rimInner) / (1f - rimInner));
                float bead = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.7f);
                float k = Mathf.Clamp01(0.3f + 0.45f * bead + 0.35f * facing * bead);
                colour = Color.Lerp(UiTheme.DecoGilt * 0.45f, UiTheme.DecoGiltLight, k);
            }
            else
            {
                // The face: black lacquer, a soft specular toward the light, a faint engraved ring.
                colour = UiTheme.DecoMedallionFace;
                float sx = dx + 0.32f;
                float sy = dy - 0.38f;
                float sheen = Mathf.Exp(-(sx * sx + sy * sy) / 0.09f);
                colour = Color.Lerp(colour, Color.white, 0.09f * sheen);

                colour = Color.Lerp(colour, UiTheme.DecoGilt, 0.28f * Line(Mathf.Abs(r - 0.6f) * half, 1.1f));

                // A gilt hairline just inside the rim, and the groove between them.
                colour = Color.Lerp(colour, UiTheme.DecoGiltLight, 0.85f * Line(Mathf.Abs(r - 0.775f) * half, 1.3f));
                colour = Color.Lerp(colour, Color.black, 0.6f * Line(Mathf.Abs(r - 0.815f) * half, 1.5f));
            }

            colour.a = alpha;
            return colour;
        }

        private static Color EmblemAt(float px, float py)
        {
            float half = CentreSize * 0.5f;
            float x = (px - half) / half;
            float y = (py - half) / half;
            float ax = Mathf.Abs(x);
            float ay = Mathf.Abs(y);
            float diamond = ax + ay;
            float toPixels = half / Mathf.Sqrt(2f);

            // The outline diamond, the faceted core, and rays between them.
            float outline = Cover((Mathf.Abs(diamond - 0.6f) * toPixels) - 1.6f);
            float core = Cover((diamond - 0.27f) * toPixels);

            float axisRay = Mathf.Min(ax, ay) * half;
            float onAxis = diamond > 0.27f && diamond < 0.6f ? Cover(axisRay - 1.1f) : 0f;

            float diagonal = Mathf.Abs(ax - ay) / Mathf.Sqrt(2f) * half;
            float onDiagonal = diamond > 0.24f && diamond < 0.44f ? Cover(diagonal - 0.9f) : 0f;

            float alpha = Mathf.Max(Mathf.Max(outline, core), Mathf.Max(onAxis, onDiagonal));

            // Facets on the core: the two faces toward the light are lit.
            float lit = y > 0f ? (x < 0f ? 1f : 0.78f) : (x < 0f ? 0.62f : 0.42f);
            float k = core > 0f && diamond < 0.27f ? lit : 0.8f;
            var colour = Color.Lerp(UiTheme.DecoGilt * 0.6f, UiTheme.DecoGiltLight, k);
            colour.a = alpha;
            return colour;
        }

        private static Color WedgeAt(float px, float py)
        {
            // The sprite covers the square from the board's centre (its bottom
            // left) to the inner corner (its top right), WedgeSpan spacings across.
            float perPixel = WedgeSpan / CentreSize;
            float x = px * perPixel;
            float y = py * perPixel;
            float t = (x + y) / Mathf.Sqrt(2f);
            float s = (y - x) / Mathf.Sqrt(2f);

            float width = WedgeHalfWidthAt(Mathf.Clamp(t, WedgeFrom, WedgeTo));
            float across = (Mathf.Abs(s) - width) / perPixel;
            float along = Mathf.Max(WedgeFrom - t, t - WedgeTo) / perPixel;
            float alpha = Cover(Mathf.Max(across, along));

            // Faceted down its spine: the side toward the light is lit, and a
            // dark crease runs along the middle.
            float k = s > 0f ? 0.95f : 0.45f;
            var colour = Color.Lerp(UiTheme.DecoGilt * 0.55f, UiTheme.DecoGiltLight, k);
            colour = Color.Lerp(colour, UiTheme.DecoGilt * 0.35f, 0.5f * Line(Mathf.Abs(s) / perPixel, 0.9f));
            colour.a = alpha;
            return colour;
        }

        // ── The tables (BS4) ───────────────────────────────────────────

        /// <summary>
        /// A heavy gilt ring for a yard table, lit from the upper left: a
        /// stepped inner lip, a rounded bead with one glint, and a dark outer
        /// edge; plus an engraved gilt line on the felt just inside it. The
        /// centre is transparent.
        /// </summary>
        public static Sprite TableRim => _tableRim != null ? _tableRim
            : (_tableRim = Raster("deco_table_rim", TableRimAt, TableSize));

        private static Color TableRimAt(float px, float py)
        {
            float half = TableSize * 0.5f;
            float dx = (px - half) / half;
            float dy = (py - half) / half;
            float r = Mathf.Sqrt(dx * dx + dy * dy);

            float inv = 1f / Mathf.Max(r, 0.0001f);
            float facing = (-0.6f * dx + 0.8f * dy) * inv;

            // The felt's engraved line: thin, and faint enough to stay under the figures.
            float line = 0.55f * Line(Mathf.Abs(r - FeltLine) * half, 1.4f);

            float outer = 1f - 1.5f / half;
            float ring = Cover((r - outer) * half) * Cover((RimInner - r) * half);
            if (ring <= 0f && line <= 0f) return new Color(UiTheme.DecoGilt.r, UiTheme.DecoGilt.g, UiTheme.DecoGilt.b, 0f);

            Color colour;
            if (r < RimInner + 0.5f / half)
            {
                colour = UiTheme.DecoGiltLight;
            }
            else
            {
                float t = Mathf.Clamp01((r - RimInner) / (outer - RimInner));

                // A stepped lip (the inner fifth), then the bead.
                float k;
                if (t < 0.22f)
                {
                    k = 0.35f + 0.25f * facing;
                }
                else
                {
                    float u = (t - 0.22f) / 0.78f;
                    float bead = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.6f);
                    k = 0.25f + 0.5f * bead + 0.3f * facing * bead;

                    // One glint on the bead, facing the light.
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    float off = Mathf.DeltaAngle(angle, 128f);
                    k += 0.25f * Mathf.Exp(-(off * off) / (2f * 16f * 16f)) * bead;
                }

                colour = Color.Lerp(UiTheme.DecoGilt * 0.4f, UiTheme.DecoGiltLight, Mathf.Clamp01(k));

                // A dark groove where the lip meets the bead, and a dark outer edge.
                colour = Color.Lerp(colour, Color.black, 0.55f * Line(Mathf.Abs(t - 0.22f) * (outer - RimInner) * half, 1.3f));
                colour = Color.Lerp(colour, Color.black, 0.5f * Line((outer - r) * half, 2f));
            }

            colour.a = Mathf.Max(ring, line);
            return colour;
        }

        // ── Weight (BS5) ───────────────────────────────────────────────

        /// <summary>
        /// A tile's soft drop shadow: the tile's chamfered square, solid to its
        /// edge and fading out over the margin around it. White, so the caller's
        /// colour sets its darkness. Drawn <see cref="TileShadowScale"/> times
        /// the tile and offset down and right, away from the light.
        /// </summary>
        public static Sprite TileShadow => _tileShadow != null ? _tileShadow
            : (_tileShadow = Raster("deco_tile_shadow", TileShadowAt, 64));

        private static Color TileShadowAt(float px, float py)
        {
            const int size = 64;
            float half = size * 0.5f;
            float tileHalf = half / TileShadowScale;
            float blur = half - tileHalf;

            float x = Mathf.Abs(px - half);
            float y = Mathf.Abs(py - half);
            float box = Mathf.Max(x, y) - tileHalf;
            float corner = (x + y - (2f * tileHalf - tileHalf * 0.11f)) / Mathf.Sqrt(2f);
            float d = Mathf.Max(box, corner);

            // Solid a little inside the edge, gone by the sprite's border.
            float t = Mathf.Clamp01((d + blur * 0.35f) / (blur * 1.35f));
            float alpha = 1f - t * t * (3f - 2f * t);
            return new Color(1f, 1f, 1f, alpha);
        }

        // ── Corner facets (BS6) ────────────────────────────────────────

        /// <summary>
        /// How far the corner facets reach in from the cross's inner corner
        /// along each edge, in spacings. They fill the corner of the centre
        /// square's empty corner cell, split by the wedge's diagonal.
        /// </summary>
        public const float FacetLeg = 0.55f;

        /// <summary>
        /// One corner facet, in white for the seat tint: the half of the
        /// corner triangle on the far side of the diagonal from the board's
        /// centre, toward the arm above it (<paramref name="upper"/>) or the
        /// arm beside it. The sprite covers the empty corner cell, from 0.5 to
        /// 1.5 spacings out on both axes, for the up-right corner; the code
        /// turns it with the wedge.
        /// </summary>
        public static Sprite CornerFacet(bool upper)
        {
            if (upper) return _facetA != null ? _facetA : (_facetA = Raster("deco_facet_upper", (x, y) => FacetAt(x, y, true), 64));
            return _facetB != null ? _facetB : (_facetB = Raster("deco_facet_side", (x, y) => FacetAt(x, y, false), 64));
        }

        /// <summary>True inside a facet, at (x, y) spacings from the board's centre, for the up-right corner.</summary>
        public static bool InFacet(float x, float y, bool upper)
        {
            if (x > 1.5f || y > 1.5f) return false;
            if (x + y < 3f - FacetLeg) return false;
            return upper ? y >= x : x >= y;
        }

        private static Color FacetAt(float px, float py, bool upper)
        {
            const int size = 64;
            float perPixel = 1f / size;
            float x = 0.5f + px * perPixel;
            float y = 0.5f + py * perPixel;

            // Antialiased against the triangle's long edge and the diagonal.
            float edge = ((3f - FacetLeg) - (x + y)) / Mathf.Sqrt(2f) / perPixel;
            float split = (upper ? x - y : y - x) / Mathf.Sqrt(2f) / perPixel;
            float outer = (Mathf.Max(x, y) - 1.5f) / perPixel;
            float alpha = Cover(Mathf.Max(Mathf.Max(edge, split), outer));

            // Lit toward the corner, where the light off the gilt edge falls.
            float depth = Mathf.Clamp01(((x + y) - (3f - FacetLeg)) / FacetLeg);
            float lum = upper ? 0.8f + 0.2f * depth : 0.6f + 0.2f * depth;
            return Grey(lum, alpha);
        }

        // ── Yard panels (BS7) ──────────────────────────────────────────

        /// <summary>The yard panel's texture size: a panel is nearly six spacings across.</summary>
        public const int YardSize = 512;

        /// <summary>
        /// The panel's one gilt hairline, inset from its edge as a fraction of
        /// the side (BS9). BS7 drew two hairlines with a band between them; the
        /// target has a single subtle line, and the panel's edge already lies
        /// on the arms' edges and the frame's (BS8b).
        /// </summary>
        public const float YardLine = 0.035f;

        /// <summary>How strongly the hairline is drawn, 0 to 1: under the corner ornaments, so it stays subtle.</summary>
        public const float YardLineStrength = 0.6f;

        /// <summary>The corner ornaments' reach from the hairline's corner, as a fraction of the side.</summary>
        public const float YardCornerReach = 0.15f;

        /// <summary>
        /// The panel's gilt: one subtle hairline and a Deco triangle in each of
        /// its corners, faceted toward the light. Colours baked.
        /// </summary>
        public static Sprite YardFrame => _yardFrame != null ? _yardFrame
            : (_yardFrame = Raster("deco_yard_frame", YardFrameAt, YardSize));

        /// <summary>Distance, as a fraction of the side, from (u, v) to the nearest edge of the square inset by <paramref name="inset"/>.</summary>
        private static float ToInsetSquare(float u, float v, float inset)
        {
            float x = Mathf.Abs(u - 0.5f);
            float y = Mathf.Abs(v - 0.5f);
            return Mathf.Max(x, y) - (0.5f - inset);
        }

        private static Color YardFrameAt(float px, float py)
        {
            float u = px / YardSize;
            float v = py / YardSize;

            float line = YardLineStrength * Cover((Mathf.Abs(ToInsetSquare(u, v, YardLine)) * YardSize) - 0.9f);

            // The corner ornament: distances in from the hairline's
            // nearest corner. A solid triangle at the corner, and a line
            // across the corner beyond it.
            float cx = Mathf.Abs(u - 0.5f);
            float cy = Mathf.Abs(v - 0.5f);
            float edge = 0.5f - YardLine;
            float dx = edge - cx;
            float dy = edge - cy;
            float inside = Mathf.Min(dx, dy);
            float sum = dx + dy;

            float solid = inside >= 0f ? Cover((sum - YardCornerReach * 0.55f) * YardSize) : 0f;
            float across = inside >= 0f ? Cover((Mathf.Abs(sum - YardCornerReach) * YardSize / Mathf.Sqrt(2f)) - 0.9f) : 0f;

            float alpha = Mathf.Max(line, Mathf.Max(solid, across));

            // Faceted toward the light: the corners and lines on the top and
            // left catch it; a solid triangle is split along its diagonal.
            bool top = v > 0.5f;
            bool left = u < 0.5f;
            float k = (top ? 0.45f : 0f) + (left ? 0.3f : 0f) + 0.2f;
            if (solid > 0f) k += (dx > dy ? 0.15f : -0.1f);
            var colour = Color.Lerp(UiTheme.DecoGilt * 0.55f, UiTheme.DecoGiltLight, Mathf.Clamp01(k));
            colour.a = alpha;
            return colour;
        }

        // ── The steel frame (BS8) ──────────────────────────────────────

        /// <summary>The frame's resolution: texels per cell. About 94 across the standard frame's width.</summary>
        public const int FrameTexelsPerCell = 120;

        /// <summary>The frame's outer corners are cut at 45°, this far along each edge, in cells.</summary>
        public const float FrameChamfer = 0.9f;

        /// <summary>The bolts along the frame: their spacing along a run and their diameter, in cells.</summary>
        public const float FrameBoltPitch = 2.2f;
        public const float FrameBoltSize = 0.13f;

        /// <summary>The four runs of the steel frame: top and bottom full width with the corners, left and right between them.</summary>
        public enum FrameRun { Top, Bottom, Left, Right }

        private static readonly Dictionary<(int, int), Sprite[]> Frames = new Dictionary<(int, int), Sprite[]>();

        /// <summary>
        /// The dark steel frame round the table (BS8), as four runs, for a
        /// frame whose outer and inner edges sit <paramref name="outer"/> and
        /// <paramref name="inner"/> cells from the board's centre. Each run's
        /// sprite is sized in cells (pixels per unit = <see cref="FrameTexelsPerCell"/>);
        /// its pivot is the frame's centre, so every run is drawn at the
        /// board's centre and they meet exactly.
        /// </summary>
        /// <remarks>
        /// <b>Four runs, not one frame,</b> for the reason the Classic rail
        /// gives (G9c): a single sprite would carry the empty table in its
        /// texture. <b>Not tiled:</b> each run is one texture its own length,
        /// so the bevels, bolts and chamfered corners never repeat or smear.
        /// <b>One shape:</b> every texel is computed from the same function of
        /// its position on the whole frame, so the runs join without a seam.
        /// </remarks>
        public static Sprite FrameRunSprite(float outer, float inner, FrameRun run)
        {
            var key = (Mathf.RoundToInt(outer * 1000f), Mathf.RoundToInt(inner * 1000f));
            if (!Frames.TryGetValue(key, out var runs) || runs[0] == null)
            {
                runs = BuildFrame(outer, inner);
                Frames[key] = runs;
            }
            return runs[(int)run];
        }

        /// <summary>
        /// The frame's colour at (u, v) cells from the board's centre, with
        /// alpha 0 off the frame.
        /// </summary>
        private static Color FrameAt(float u, float v, float outer, float inner)
        {
            float perTexel = 1f / FrameTexelsPerCell;
            float au = Mathf.Abs(u);
            float av = Mathf.Abs(v);
            float edge = Mathf.Max(au, av);

            // Inside the outer square and its chamfered corners, outside the inner square.
            float toOuter = Mathf.Min(outer - edge, (2f * outer - FrameChamfer - au - av) / Mathf.Sqrt(2f));
            float toInner = edge - inner;
            float alpha = Mathf.Clamp01(toOuter / perTexel + 0.5f) * Mathf.Clamp01(toInner / perTexel + 0.5f);

            // Which run this texel belongs to decides which way its bevels face.
            bool horizontal = av >= au;
            bool outerLit = horizontal ? v > 0f : u < 0f;
            float width = outer - inner;
            float across = Mathf.Clamp01(toInner / width);

            // Brushed steel: fine streaks along the run, from a hash across it.
            float streakAt = horizontal ? v : u;
            float streak = Hash01(Mathf.FloorToInt(streakAt * FrameTexelsPerCell * 0.5f)) * 2f - 1f;
            float lum = 0.22f + 0.03f * streak;

            // Slightly raised in the middle, lit from the upper left.
            lum += 0.05f * Mathf.Sin(Mathf.PI * across);

            // Bevels: the outer one faces away from the board, the inner one toward it.
            const float bevel = 0.09f;
            if (toOuter < bevel) lum = Mathf.Lerp(outerLit ? 0.62f : 0.06f, lum, toOuter / bevel);
            if (toInner < bevel) lum = Mathf.Lerp(outerLit ? 0.08f : 0.52f, lum, toInner / bevel);

            // A dark groove down the middle of the run.
            float mid = Mathf.Abs(across - 0.5f) * width;
            lum *= 1f - 0.45f * Line(mid / perTexel, 1.2f);

            var colour = Color.Lerp(UiTheme.DecoSteelShade, UiTheme.DecoSteelLight, Mathf.Clamp01(lum));

            // Bolts on the groove, evenly along the run, clear of the corners.
            float along = horizontal ? u : v;
            float nearest = Mathf.Round(along / FrameBoltPitch) * FrameBoltPitch;
            if (Mathf.Abs(nearest) < inner - 0.2f)
            {
                float midline = inner + width * 0.5f;
                float cx = horizontal ? nearest : Mathf.Sign(u) * midline;
                float cy = horizontal ? Mathf.Sign(v) * midline : nearest;
                float bx = u - cx;
                float by = v - cy;
                float r = Mathf.Sqrt(bx * bx + by * by);
                float boltR = FrameBoltSize * 0.5f;
                float bolt = Mathf.Clamp01((boltR - r) / perTexel + 0.5f);
                if (bolt > 0f)
                {
                    // A domed head lit from the upper left, with a slot.
                    float dome = 0.5f + 0.4f * Mathf.Clamp01((-bx * 0.6f + by * 0.8f) / boltR + 0.3f);
                    var head = Color.Lerp(UiTheme.DecoSteelShade, UiTheme.DecoSteelLight, dome);
                    head = Color.Lerp(head, UiTheme.DecoSteelShade, 0.7f * Line(Mathf.Abs(bx + by) / Mathf.Sqrt(2f) / perTexel, 1f));
                    colour = Color.Lerp(colour, head, bolt);
                }
            }

            // The gilt hairline on the frame's inner lip.
            colour = Color.Lerp(colour, UiTheme.DecoGiltLight, 0.85f * Line(toInner / perTexel, 2.2f));

            colour.a = alpha;
            return colour;
        }

        private static Sprite[] BuildFrame(float outer, float inner)
        {
            int full = Mathf.CeilToInt(2f * outer * FrameTexelsPerCell);
            int band = Mathf.CeilToInt((outer - inner) * FrameTexelsPerCell) + 2;
            int between = full - 2 * band;
            var runs = new Sprite[4];

            // Top and bottom: full width, band tall. Left and right: band wide, between the two.
            runs[(int)FrameRun.Top] = FrameSprite("deco_frame_top", full, band, 0, full - band, outer, inner, full);
            runs[(int)FrameRun.Bottom] = FrameSprite("deco_frame_bottom", full, band, 0, 0, outer, inner, full);
            runs[(int)FrameRun.Left] = FrameSprite("deco_frame_left", band, between, 0, band, outer, inner, full);
            runs[(int)FrameRun.Right] = FrameSprite("deco_frame_right", band, between, full - band, band, outer, inner, full);
            return runs;
        }

        /// <summary>
        /// One run: the <paramref name="width"/> × <paramref name="height"/>
        /// texels of the whole frame starting at (<paramref name="x0"/>,
        /// <paramref name="y0"/>), pivoted on the frame's centre.
        /// </summary>
        private static Sprite FrameSprite(string name, int width, int height, int x0, int y0,
            float outer, float inner, int full)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: true)
            {
                name = name,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4,
            };

            float half = full * 0.5f;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x0 + x + 0.5f - half) / FrameTexelsPerCell;
                    float v = (y0 + y + 0.5f - half) / FrameTexelsPerCell;
                    pixels[y * width + x] = FrameAt(u, v, outer, inner);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);

            // The pivot is the frame's centre, in this run's own normalised coordinates.
            var pivot = new Vector2((half - x0) / width, (half - y0) / height);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot,
                FrameTexelsPerCell, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        /// <summary>A stable pseudo-random value in 0..1 for an integer.</summary>
        private static float Hash01(int n)
        {
            unchecked
            {
                uint h = (uint)n * 747796405u + 2891336453u;
                h = ((h >> (int)((h >> 28) + 4u)) ^ h) * 277803737u;
                return ((h >> 22) ^ h) / (float)uint.MaxValue;
            }
        }

        // ── Geometry ────────────────────────────────────────────────────

        /// <summary>Signed distance, in pixels, to the tile's chamfered square outline; negative inside.</summary>
        private static float ChamferedSquare(float px, float py)
        {
            float half = Size * 0.5f;
            float x = Mathf.Abs(px - half);
            float y = Mathf.Abs(py - half);
            float edge = half - 0.5f;

            float box = Mathf.Max(x, y) - edge;
            float corner = (x + y - (2f * edge - Chamfer)) / Mathf.Sqrt(2f);
            return Mathf.Max(box, corner);
        }

        /// <summary>Signed distance, in pixels, to a closed polygon; negative inside.</summary>
        private static float PolygonDistance(Vector2 p, Vector2[] poly)
        {
            float best = float.MaxValue;
            bool inside = false;

            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[j];
                var b = poly[i];

                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (p - (a + ab * t)).sqrMagnitude);

                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }

            float distance = Mathf.Sqrt(best);
            return inside ? -distance : distance;
        }

        /// <summary>Antialiased coverage of a signed distance, over one pixel.</summary>
        private static float Cover(float d) => Mathf.Clamp01(0.5f - d);

        /// <summary>A soft line <paramref name="width"/> pixels wide at distance 0.</summary>
        private static float Line(float distance, float width) => Mathf.Clamp01(1f - distance / width);

        /// <summary>
        /// Brightness everywhere, alpha only where the shape is: transparent
        /// pixels keep the shape's grey, so mipmaps don't darken the edges.
        /// </summary>
        private static Color Grey(float lum, float alpha)
        {
            lum = Mathf.Clamp01(lum);
            return new Color(lum, lum, lum, alpha);
        }

        private static Sprite Raster(string name, Func<float, float, Color> colourAt, int size = Size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
            {
                name = name,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = colourAt(x + 0.5f, y + 0.5f);
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                size, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
    }
}
