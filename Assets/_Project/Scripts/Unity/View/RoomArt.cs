// Assets/_Project/Scripts/Unity/View/RoomArt.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The salon's procedural sprites: the velvet wall and its trim, the
    /// chandeliers, and the side columns with their sconces (VISUAL_PASS.md,
    /// V3).
    /// </summary>
    /// <remarks>
    /// <b>Drawn in code, like everything else</b> (PRESENTATION §6). White with
    /// a grey luminance baked in where a shape is shaded, so the renderer's
    /// tint carries the palette and <see cref="UiTheme"/> stays the only place
    /// a colour lives. The rasterisers are <see cref="DecoSprites"/>'s, the way
    /// <see cref="BoardArt"/> shares them.
    ///
    /// <b>The wall is flat, on purpose.</b> The approved salon folded it with
    /// 140 cuts and lit it with two raking spots. Three mockup passes failed to
    /// make the folds read - at room C's distance, after `rake_velvet` was
    /// added to fix exactly that, and again at the shipping lens with the wall
    /// unambiguously lit, where it was still a flat field with soft blotches.
    /// A plain panel gives the same image for none of the geometry, so the
    /// folds and their two lights are not built (designer, 2026-09-18).
    ///
    /// <b>The light is painted in.</b> A chandelier carries its own glow and a
    /// sconce its own flame, the way G4's corner lamps did, so the room still
    /// reads with Lighting effects off. Real <c>Light2D</c>s over the top are a
    /// follow-up, not a requirement.
    ///
    /// <b>The room only exists under the tilt.</b> Every piece of it stands
    /// vertically in world space, which a straight-down camera sees edge-on.
    /// <see cref="RoomBackdrop"/> is what knows that; these are just shapes.
    /// </remarks>
    public static class RoomArt
    {
        /// <summary>Texels per world unit for the room's sprites. Coarser than the board: nothing here is read closely.</summary>
        private const float Texels = 24f;

        private static Sprite _wall, _pelmet, _skirting, _rail, _column, _sconce, _flame;
        private static Sprite[] _chandelier;

        /// <summary>
        /// The velvet field. Lit from above, where the chandeliers hang, and
        /// falling to near-nothing at the skirting; dimmed toward the left and
        /// right ends so the wall has no visible edge.
        /// </summary>
        public static Sprite Wall => _wall ?? (_wall = BuildWall(192, 128));

        /// <summary>The box along the wall's head, with a soft shadow under it.</summary>
        public static Sprite Pelmet => _pelmet ?? (_pelmet = BuildPelmet(64, 40));

        /// <summary>A gilt hairline under the pelmet, bright along its top.</summary>
        public static Sprite Rail => _rail ?? (_rail = BuildRail(32, 12));

        /// <summary>The board at the wall's foot, so the cloth meets something.</summary>
        public static Sprite Skirting => _skirting ?? (_skirting = BuildSkirting(64, 28));

        /// <summary>A side column: a dark shaft with one lit edge toward the table.</summary>
        public static Sprite Column => _column ?? (_column = BuildColumn(56, 224));

        /// <summary>The sconce's bracket and cup, unlit.</summary>
        public static Sprite Sconce => _sconce ?? (_sconce = BuildSconce(28, 36));

        /// <summary>A candle flame, for a sconce and for a chandelier's points.</summary>
        public static Sprite Flame => _flame ?? (_flame = BuildFlame(14, 22));

        /// <summary>
        /// The chandelier, as body and glow. Two sprites so the glow can be
        /// tinted and faded on its own, the way the vault's is.
        /// </summary>
        public static Sprite[] Chandelier => _chandelier ?? (_chandelier = BuildChandelier(224, 144));

        /// <summary>Index into <see cref="Chandelier"/>.</summary>
        public const int ChandelierBody = 0, ChandelierGlow = 1;

        // ── Builders ────────────────────────────────────────────────────

        private static Sprite BuildWall(int w, int h)
        {
            return DecoSprites.RasterizeShaded(w, h, (px, py) =>
            {
                float u = px / w;
                float v = py / h;

                // Lit from the head of the wall, falling away downward. Squared,
                // so the light gathers near the pelmet instead of washing the
                // whole cloth - the mockup's one lighting lesson was that an
                // evenly lit room stops reading as dark.
                float lift = v * v;

                // Cloth, not folds: a fine vertical grain at a few percent. Folds
                // were tried three times and never read (see the class remarks).
                float grain = 0.97f + 0.03f * Hash(Mathf.FloorToInt(px * 0.7f), 0);

                // The ends fall off, so the wall has no visible edge.
                float ends = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) / 0.18f);

                float l = (0.18f + 0.82f * lift) * grain * Mathf.Lerp(0.45f, 1f, ends);
                return new Color(l, l, l, 1f);
            }, Texels, Vector4.zero);
        }

        private static Sprite BuildPelmet(int w, int h)
        {
            return DecoSprites.RasterizeShaded(w, h, (px, py) =>
            {
                float v = py / h;

                // A lit top face, a body, then the shadow it throws on the cloth.
                if (v > 0.72f) return Grey(Mathf.Lerp(0.85f, 1f, (v - 0.72f) / 0.28f));
                if (v > 0.30f) return Grey(Mathf.Lerp(0.30f, 0.80f, (v - 0.30f) / 0.42f));

                float t = v / 0.30f;
                return new Color(0f, 0f, 0f, (1f - t) * 0.55f);
            }, Texels, Vector4.zero);
        }

        private static Sprite BuildRail(int w, int h)
        {
            return DecoSprites.RasterizeShaded(w, h, (px, py) =>
            {
                float v = py / h;
                float body = DecoSprites.Coverage(Mathf.Abs(v - 0.5f) * h - h * 0.34f);
                float shine = Mathf.Exp(-Mathf.Pow((v - 0.66f) * 7f, 2f));

                // Luminance only. Multiplying the Color would scale alpha with
                // it and eat the rail's own edge.
                float l = Mathf.Lerp(0.55f, 1.4f, shine);
                return new Color(l, l, l, body);
            }, Texels, Vector4.zero);
        }

        private static Sprite BuildSkirting(int w, int h)
        {
            return DecoSprites.RasterizeShaded(w, h, (px, py) =>
            {
                float v = py / h;
                if (v > 0.86f) return Grey(0.95f);            // the top edge catches the light
                return Grey(Mathf.Lerp(0.10f, 0.45f, v));      // and the face falls into the floor
            }, Texels, Vector4.zero);
        }

        private static Sprite BuildColumn(int w, int h)
        {
            return DecoSprites.RasterizeShaded(w, h, (px, py) =>
            {
                float u = px / w;
                float v = py / h;

                // A capital and a base, both a little wider than the shaft.
                float half = v > 0.93f || v < 0.05f ? 0.5f : 0.40f;
                float inside = DecoSprites.Coverage((Mathf.Abs(u - 0.5f) - half) * w);
                if (inside <= 0f) return new Color(0f, 0f, 0f, 0f);

                // Near-black, reading by one lit edge on the side facing the
                // table. ART_DIRECTION §6: layered silhouettes receding into dark.
                float edge = Mathf.Exp(-Mathf.Pow((u - 0.30f) * 9f, 2f));
                float l = 0.06f + 0.5f * edge * Mathf.Lerp(0.25f, 1f, v);
                return new Color(l, l, l, inside);
            }, Texels, Vector4.zero);
        }

        private static Sprite BuildSconce(int w, int h)
        {
            return DecoSprites.RasterizeShaded(w, h, (px, py) =>
            {
                float u = px / w;
                float v = py / h;

                // A cup on a short bracket: a stepped Deco shape, not a curve.
                float cup = v > 0.55f ? DecoSprites.Coverage((Mathf.Abs(u - 0.5f) - Mathf.Lerp(0.42f, 0.18f, (v - 0.55f) / 0.45f)) * w) : 0f;
                float stem = v <= 0.55f ? DecoSprites.Coverage((Mathf.Abs(u - 0.5f) - 0.12f) * w) : 0f;
                float a = Mathf.Max(cup, stem);
                if (a <= 0f) return new Color(0f, 0f, 0f, 0f);

                float l = Mathf.Lerp(0.35f, 1f, v);
                return new Color(l, l, l, a);
            }, Texels, Vector4.zero);
        }

        private static Sprite BuildFlame(int w, int h)
        {
            return DecoSprites.RasterizeShaded(w, h, (px, py) =>
            {
                float u = (px / w - 0.5f) * 2f;
                float v = py / h;

                // A teardrop: widest low, drawn to a point at the top.
                float width = Mathf.Sin(Mathf.Clamp01(v) * Mathf.PI * 0.92f);
                float a = DecoSprites.Coverage((Mathf.Abs(u) - width * 0.85f) * w * 0.5f);
                float core = Mathf.Clamp01(1f - Mathf.Abs(u) / 0.5f) * Mathf.Clamp01(1f - Mathf.Abs(v - 0.35f) * 2.4f);
                return new Color(1f, 1f, 1f, a * Mathf.Lerp(0.55f, 1f, core));
            }, Texels, Vector4.zero);
        }

        private static Sprite[] BuildChandelier(int w, int h)
        {
            // Three tiers, widest at the bottom, each a thin ellipse of gilt
            // with candle points stood on it. Drawn as an ellipse rather than a
            // circle because the tiers are seen from below and slightly to one
            // side; the camera's own perspective is not enough at this distance.
            var tiers = new[] { (y: 0.30f, rx: 0.46f, n: 12), (y: 0.52f, rx: 0.33f, n: 9), (y: 0.72f, rx: 0.20f, n: 6) };

            var body = DecoSprites.RasterizeShaded(w, h, (px, py) =>
            {
                float u = px / w - 0.5f;
                float v = py / h;
                float a = 0f, lum = 0f;

                // The stem, and a short chain up to the ceiling.
                a = Mathf.Max(a, DecoSprites.Coverage((Mathf.Abs(u) - 0.012f) * w) * (v > 0.28f ? 1f : 0f));

                foreach (var t in tiers)
                {
                    float ry = t.rx * 0.22f;
                    float d = Mathf.Sqrt(Mathf.Pow(u / t.rx, 2f) + Mathf.Pow((v - t.y) / ry, 2f));
                    float ring = DecoSprites.Line(Mathf.Abs(d - 1f) * t.rx * w * 0.5f, 1.6f);
                    if (ring > a) { a = ring; lum = 1f; }

                    // Candle points around the rim, on the near half where they read.
                    for (int i = 0; i < t.n; i++)
                    {
                        float ang = Mathf.PI * 2f * i / t.n;
                        float cx = Mathf.Cos(ang) * t.rx;
                        float cy = t.y + Mathf.Sin(ang) * ry;
                        float dot = DecoSprites.Coverage((Mathf.Sqrt(Mathf.Pow((u - cx) * w, 2f) + Mathf.Pow((v - cy) * h, 2f)) - 2.2f));
                        if (dot > a) { a = dot; lum = 1.6f; }
                    }
                }

                float l = Mathf.Lerp(0.5f, 1f, lum);
                return new Color(l, l, l, Mathf.Clamp01(a));
            }, Texels, Vector4.zero);

            var glow = DecoSprites.Rasterize(96, 96, (px, py) =>
            {
                float r = Mathf.Sqrt(Mathf.Pow(px - 48f, 2f) + Mathf.Pow(py - 48f, 2f)) / 48f;
                float t = Mathf.Clamp01(1f - r);
                return t * t * t;
            }, Texels, Vector4.zero);

            return new[] { body, glow };
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static Color Grey(float l) => new Color(l, l, l, 1f);

        /// <summary>A fixed integer hash in [0, 1), so the room is the same every run.</summary>
        private static float Hash(int x, int y)
        {
            int n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }
    }
}