// Assets/_Project/Scripts/Unity/View/DecoSprites.cs
using System;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The Art Deco frame kit, drawn in code (GUI phase, increments G and G3).
    /// </summary>
    /// <remarks>
    /// <b>Why procedural.</b> PRESENTATION §6 keeps the prototype free of art
    /// assets until the art pass, and these shapes are simple enough to
    /// describe exactly: chamfered boxes, hairline rules, corner fans, tall
    /// diamonds (ART_DIRECTION §8: sharp corners, no soft shapes). The art
    /// pass can swap any of them for a painted sprite with the same borders.
    ///
    /// <b>White, so they tint.</b> Every sprite is white with an alpha mask.
    /// The colour comes from the Image or SpriteRenderer, which keeps the
    /// palette in <see cref="UiTheme"/> and nowhere else. A frame with two
    /// colours is two sprites stacked: a fill and an edge.
    ///
    /// <b>Sliced, at one texel per canvas unit.</b> HUD sprites are 100 pixels
    /// per unit, the canvas's reference, so a 2-texel line is 2 units at 1080p
    /// whatever the size of the box. The board's own sprites live in
    /// <see cref="BoardArt"/>.
    ///
    /// Edges are anti-aliased from a signed distance, so nothing needs
    /// mipmaps or supersampling. Everything is built once and cached.
    /// </remarks>
    public static class DecoSprites
    {
        private const float HudPixelsPerUnit = 100f;

        // ── HUD frames (sliced) ─────────────────────────────────────────

        private static Sprite _panelFill, _panelEdge, _buttonFill, _buttonEdge, _buttonEdgeDouble, _chipFill;

        /// <summary>
        /// Width of every resting edge, in canvas units (G3).
        /// </summary>
        /// <remarks>
        /// Not 1: at the 1080 reference with match 0.5, a 1366×768 screen
        /// scales the canvas by about 0.71, so a 1-unit line would fall under
        /// a pixel and shimmer. 1.5 units stays at least a pixel wide there,
        /// and the edge is anti-aliased from a signed distance, so it lands
        /// soft rather than stepped at any scale.
        /// </remarks>
        public const float HairlineWidth = 1.5f;

        /// <summary>A docked or floating panel's body. Corners cut at 10.</summary>
        public static Sprite PanelFill => _panelFill ?? (_panelFill = Chamfer(10, null));

        /// <summary>A floating panel's frame: one hairline on the edge (G3; was a double rule).</summary>
        public static Sprite PanelEdge => _panelEdge ?? (_panelEdge = Chamfer(10, new[] { new Band(0f, HairlineWidth) }));

        /// <summary>A button, card or die. Corners cut at 6.</summary>
        public static Sprite ButtonFill => _buttonFill ?? (_buttonFill = Chamfer(6, null));

        /// <summary>A button's resting edge: one hairline.</summary>
        public static Sprite ButtonEdge => _buttonEdge ?? (_buttonEdge = Chamfer(6, new[] { new Band(0f, HairlineWidth) }));

        /// <summary>The edge of a control that wants pressing: 2 units and a 1-unit rule inside.</summary>
        public static Sprite ButtonEdgeDouble => _buttonEdgeDouble ?? (_buttonEdgeDouble = Chamfer(6, new[] { new Band(0f, 2f), new Band(4f, 1f) }));

        /// <summary>Small chips, tags and pills. Corners cut at 4.</summary>
        public static Sprite ChipFill => _chipFill ?? (_chipFill = Chamfer(4, null));

        // ── HUD ornaments (simple) ──────────────────────────────────────

        private static Sprite _fan, _diamond, _diamondOutline, _ruleAlong, _ruleUp;

        /// <summary>Size of <see cref="CornerFan"/> in canvas units (G3: was 22).</summary>
        public const float FanSize = 16f;

        /// <summary>
        /// How far inside a panel's corner the fan's origin sits, so the fan
        /// clears the chamfer and the hairline.
        /// </summary>
        public const float FanInset = 5f;

        /// <summary>Thickness, in canvas units, of the box a <see cref="RuleAlong"/> is drawn in.</summary>
        public const float RuleBox = 4f;

        /// <summary>
        /// A straight hairline, for a docked panel's edge or a divider: an
        /// anti-aliased line of <see cref="HairlineWidth"/> down the middle of
        /// a <see cref="RuleBox"/>-thick box. Runs along x; stretch it as a
        /// simple (not sliced) image.
        /// </summary>
        public static Sprite RuleAlong => _ruleAlong ?? (_ruleAlong = BuildRule(horizontal: true));

        /// <summary>The same hairline, running along y.</summary>
        public static Sprite RuleUp => _ruleUp ?? (_ruleUp = BuildRule(horizontal: false));

        /// <summary>
        /// A quarter sunburst radiating from its bottom-left corner. Placed at a
        /// panel's corner and rotated, it opens inward.
        /// </summary>
        public static Sprite CornerFan => _fan ?? (_fan = BuildFan((int)FanSize));

        /// <summary>A tall diamond, 2:3. Energy pips, bullets, rule centres.</summary>
        public static Sprite Diamond => _diamond ?? (_diamond = BuildDiamond(16, 24, 0f));

        /// <summary>The same diamond as an outline, for an empty pip.</summary>
        public static Sprite DiamondOutline => _diamondOutline ?? (_diamondOutline = BuildDiamond(16, 24, 1.6f));

        private static Sprite _panelSheen;

        /// <summary>
        /// A vertical falloff, strongest at the top: a panel's sheen
        /// (UI_MOTION.md increment U4). Stretched simple, never sliced.
        /// </summary>
        public static Sprite PanelSheen => _panelSheen ?? (_panelSheen = BuildSheen(64));

        // ── Board (world space) ─────────────────────────────────────────

        private static Sprite _tileInlay, _ringThin, _glow;

        /// <summary>
        /// A cell's inlay: a chamfered line inset from the tile's edge. One
        /// world unit across, like <see cref="Primitives"/>.
        /// </summary>
        public static Sprite TileInlay => _tileInlay ?? (_tileInlay = BuildTileInlay(64));

        /// <summary>A disc outline, a tenth of the radius thick. Yard rims.</summary>
        public static Sprite RingThin => _ringThin ?? (_ringThin = BuildRing(128, 0.9f));

        /// <summary>A soft radial falloff, for pools of light.</summary>
        public static Sprite Glow => _glow ?? (_glow = BuildGlow(128));

        // ── Builders ────────────────────────────────────────────────────

        /// <summary>A line inside a shape's edge: <c>Inset</c> units in, <c>Width</c> wide.</summary>
        private readonly struct Band
        {
            public readonly float Inset;
            public readonly float Width;

            public Band(float inset, float width)
            {
                Inset = inset;
                Width = width;
            }
        }

        /// <summary>
        /// A box with its corners cut at 45°, filled or drawn as bands, and
        /// sliced so only the straight middle stretches.
        /// </summary>
        private static Sprite Chamfer(int cut, Band[] bands, float pixelsPerUnit = HudPixelsPerUnit)
        {
            // The border must hold the whole corner, cut plus the deepest band.
            float deepest = 0f;
            if (bands != null)
                foreach (var band in bands) deepest = Mathf.Max(deepest, band.Inset + band.Width);

            int border = Mathf.CeilToInt(Mathf.Max(cut, deepest)) + 3;
            int size = border * 2 + 2;
            float half = size * 0.5f;

            var sprite = Rasterize(size, size, (px, py) =>
            {
                float x = Mathf.Abs(px - half);
                float y = Mathf.Abs(py - half);

                // Negative inside. The box, then the 45° cut across each corner.
                float box = Mathf.Max(x - half, y - half);
                float corner = (x + y - (2f * half - cut)) / Mathf.Sqrt(2f);
                float d = Mathf.Max(box, corner);

                if (bands == null) return Coverage(d);

                float alpha = 0f;
                foreach (var band in bands)
                    alpha = Mathf.Max(alpha, BandCoverage(d, band.Inset, band.Width));
                return alpha;
            }, pixelsPerUnit, new Vector4(border, border, border, border));

            return sprite;
        }

        /// <summary>
        /// A Deco sunrise in one quadrant: a quarter-disc core, three rays and
        /// an arc. Its origin is the texture's bottom-left corner.
        /// </summary>
        private static Sprite BuildFan(int size)
        {
            // Proportions from the original 22-unit fan, so a smaller fan is
            // the same drawing and not a crop of it.
            float k = size / 22f;
            float arc = size - 2.5f * k;

            return Rasterize(size, size, (px, py) =>
            {
                float r = Mathf.Sqrt(px * px + py * py);
                float angle = Mathf.Atan2(py, px) * Mathf.Rad2Deg;

                float alpha = Coverage(r - 3.5f * k);
                alpha = Mathf.Max(alpha, Line(Mathf.Abs(r - arc), 1f));

                if (r > 6f * k && r < arc - 2.5f * k)
                {
                    foreach (float ray in new[] { 22.5f, 45f, 67.5f })
                    {
                        float off = r * Mathf.Sin(Mathf.Abs(angle - ray) * Mathf.Deg2Rad);
                        alpha = Mathf.Max(alpha, Line(off, 1f));
                    }
                }

                return alpha;
            }, HudPixelsPerUnit, Vector4.zero, new Vector2(0f, 0f));
        }

        private static Sprite BuildRule(bool horizontal)
        {
            // Three texels per canvas unit across the line, two along it.
            const int across = 12;
            const int along = 2;
            float texelsPerUnit = across / RuleBox;

            return Rasterize(horizontal ? along : across, horizontal ? across : along, (px, py) =>
            {
                float offset = Mathf.Abs((horizontal ? py : px) - across * 0.5f) / texelsPerUnit;
                return Line(offset, HairlineWidth);
            }, HudPixelsPerUnit, Vector4.zero);
        }

        private static Sprite BuildDiamond(int width, int height, float stroke)
        {
            float hw = width * 0.5f - 0.5f;
            float hh = height * 0.5f - 0.5f;
            float scale = hw * hh / Mathf.Sqrt(hw * hw + hh * hh);

            return Rasterize(width, height, (px, py) =>
            {
                float x = Mathf.Abs(px - width * 0.5f);
                float y = Mathf.Abs(py - height * 0.5f);
                float d = (x / hw + y / hh - 1f) * scale;

                return stroke <= 0f ? Coverage(d) : BandCoverage(d, 0f, stroke);
            }, HudPixelsPerUnit, Vector4.zero);
        }

        private static Sprite BuildTileInlay(int size)
        {
            float half = size * 0.5f;
            const float inset = 6f;
            const float cut = 6f;

            return Rasterize(size, size, (px, py) =>
            {
                float x = Mathf.Abs(px - half);
                float y = Mathf.Abs(py - half);
                float edge = half - inset;

                float box = Mathf.Max(x - edge, y - edge);
                float corner = (x + y - (2f * edge - cut)) / Mathf.Sqrt(2f);
                return BandCoverage(Mathf.Max(box, corner), 0f, 2.2f);
            }, size, Vector4.zero);
        }

        private static Sprite BuildRing(int size, float innerFraction)
        {
            float half = size * 0.5f;
            float outer = half - 1f;
            float width = outer * (1f - innerFraction);

            return Rasterize(size, size, (px, py) =>
            {
                float r = Mathf.Sqrt((px - half) * (px - half) + (py - half) * (py - half));
                return BandCoverage(r - outer, 0f, width);
            }, size, Vector4.zero);
        }

        private static Sprite BuildGlow(int size)
        {
            float half = size * 0.5f;

            return Rasterize(size, size, (px, py) =>
            {
                float r = Mathf.Sqrt((px - half) * (px - half) + (py - half) * (py - half)) / half;
                float t = Mathf.Clamp01(1f - r);
                return t * t;
            }, size, Vector4.zero);
        }

        /// <summary>Top-lit vertical falloff: full at the top texel, gone by two thirds down.</summary>
        private static Sprite BuildSheen(int size)
        {
            return Rasterize(2, size, (px, py) =>
            {
                float t = Mathf.Clamp01(py / size / 0.66f);
                return (1f - t) * (1f - t);
            }, HudPixelsPerUnit, Vector4.zero);
        }

        // ── Coverage ────────────────────────────────────────────────────

        /// <summary>Anti-aliased coverage of a filled shape at signed distance <paramref name="d"/>.</summary>
        internal static float Coverage(float d) => Mathf.Clamp01(0.5f - d);

        /// <summary>Coverage of a band from <paramref name="inset"/> to inset + width inside the edge.</summary>
        internal static float BandCoverage(float d, float inset, float width)
        {
            // Distance from the band's centre line, measured inward.
            float centre = -(inset + width * 0.5f);
            return Line(Mathf.Abs(d - centre), width);
        }

        /// <summary>Coverage of a line of <paramref name="width"/> at distance <paramref name="off"/> from its centre.</summary>
        internal static float Line(float off, float width) => Mathf.Clamp01(width * 0.5f - off + 0.5f);

        /// <summary>
        /// Samples <paramref name="alphaAt"/> at each texel centre (x + 0.5, y + 0.5)
        /// into a white texture.
        /// </summary>
        internal static Sprite Rasterize(
            int width, int height, Func<float, float, float> alphaAt,
            float pixelsPerUnit, Vector4 border, Vector2? pivot = null)
        {
            return RasterizeShaded(width, height, (x, y) =>
            {
                float alpha = alphaAt(x, y);
                return new Color(1f, 1f, 1f, alpha);
            }, pixelsPerUnit, border, pivot);
        }

        /// <summary>
        /// Samples <paramref name="colourAt"/> at each texel centre. For shaded
        /// sprites: bake a grey luminance into RGB and the renderer's tint
        /// multiplies it, so one sprite shades any seat colour.
        /// </summary>
        internal static Sprite RasterizeShaded(
            int width, int height, Func<float, float, Color> colourAt,
            float pixelsPerUnit, Vector4 border, Vector2? pivot = null)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var c = colourAt(x + 0.5f, y + 0.5f);
                    pixels[y * width + x] = new Color32(
                        Byte(c.r), Byte(c.g), Byte(c.b), Byte(c.a));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(
                texture, new Rect(0, 0, width, height), pivot ?? new Vector2(0.5f, 0.5f),
                pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        }

        private static byte Byte(float value) => (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
    }
}
