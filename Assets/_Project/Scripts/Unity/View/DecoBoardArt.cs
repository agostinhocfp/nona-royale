// Assets/_Project/Scripts/Unity/View/DecoBoardArt.cs
using System;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The Deco skin's procedural board art: the cells (board skin BS2) — a
    /// tile face, its trim (bevel and gilt edge), a gilt ring for the home
    /// columns and a compass for the start cells — and the centre (BS3) — the
    /// medallion, its emblem and the four corner wedges. Built once.
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

        /// <summary>The brightness the tile's face is painted at, 0 to 1.</summary>
        public const float FaceValue = 0.72f;

        /// <summary>The corners' chamfer, in pixels of <see cref="Size"/>.</summary>
        private const float Chamfer = 7f;

        /// <summary>The bevel's width, in pixels of <see cref="Size"/>.</summary>
        private const float Bevel = 9f;

        /// <summary>The gilt edge's width, in pixels of <see cref="Size"/>.</summary>
        private const float EdgeWidth = 3.2f;

        /// <summary>Where the home column's ring sits and how thick it is, in pixels.</summary>
        private const float RingRadius = 0.31f * Size;
        private const float RingWidth = 2.6f;

        private static Sprite _body, _trimGilt, _trimCyan, _ring, _compass;
        private static Sprite _medallion, _medallionEmblem, _wedge;

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
            float nx = px - Size * 0.5f;
            float ny = py - Size * 0.5f;
            float bevel = Mathf.Abs(ny) >= Mathf.Abs(nx)
                ? (ny > 0f ? 0.95f : 0.45f)
                : (nx < 0f ? 0.88f : 0.52f);

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
            float strength = lit ? (vertical ? 0.26f : 0.18f) : (vertical ? 0.5f : 0.4f);
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
        public const float WedgeWaistHalfWidth = 0.07f;
        public const float WedgeWidestAt = 1.3f;
        public const float WedgeHalfWidth = 0.2f;
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
