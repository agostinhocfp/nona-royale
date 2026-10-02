// Assets/_Project/Scripts/Unity/View/FrostTexture.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A frosted cell, drawn in code (CORE_GAMEPLAY.md, CG12, redrawn in
    /// CG15): fern-like ice feathers creeping in from the border over a white
    /// rime crust, a couple of six-armed crystals loose in the middle, and a
    /// set of glints that twinkle at the crystals' tips. For the cells Mimi's
    /// Cryo Field reaches.
    /// </summary>
    /// <remarks>
    /// <b>Grown, not noised.</b> The first frost (CG12) was ridged noise and
    /// read as cracked ice. Real frost is crystal growth: straight stems that
    /// put out barbs at 60°, the angle of the ice lattice, and barbs on the
    /// barbs. So each feather here is a short random walk that branches that
    /// way, three levels deep, drawn as anti-aliased tapered strokes from a
    /// fixed seed, so every build draws the same frost.
    ///
    /// <b>From the edges in.</b> Frost forms where a surface is coldest and
    /// thickens toward the rim, the way it does on a window pane. That keeps
    /// the middle of the cell clear, where a piece stands, and it outlines the
    /// field's reach cell by cell.
    ///
    /// <b>Two layers.</b> <see cref="Sprite"/> is the frost itself and only
    /// breathes. <see cref="Glints"/> is a loop of frames holding nothing but
    /// sparkles, each with its own phase, and <see cref="DeviceLayer"/>
    /// cross-fades it over the frost the way it flows lava. Kept apart so the
    /// cross-fade never dims the frost between frames.
    ///
    /// <b>Not the asset pack.</b> All In 1 Sprite Shader has no frost or ice
    /// effect; its nearest, the travelling Shine, already means a powered
    /// cell on this board.
    /// </remarks>
    public static class FrostTexture
    {
        public const int Size = 128;
        public const int GlintFrameCount = 12;

        /// <summary>Seconds for every glint to twinkle once.</summary>
        public const float GlintLoopSeconds = 5f;

        /// <summary>Pixels over which the tile fades out at its edge: just enough to anti-alias it.</summary>
        private const float EdgeFade = 3f;

        /// <summary>How far in from the border the feathers take root, and how deep the rime crust reaches.</summary>
        private const float Root = 5f;
        private const float CrustDepth = 30f;

        private const int Feathers = 14;
        private const int Crystals = 2;
        private const int MaxGlints = 14;

        /// <summary>The ice lattice's branching angle.</summary>
        private const float Sixty = Mathf.PI / 3f;

        private static Sprite _sprite;
        private static Sprite[] _glints;

        /// <summary>The frost tile; built on first use.</summary>
        public static Sprite Sprite
        {
            get
            {
                if (_sprite == null) Build();
                return _sprite;
            }
        }

        /// <summary>The glint frames, in order; built with the tile.</summary>
        public static IReadOnlyList<Sprite> Glints
        {
            get
            {
                if (_glints == null) Build();
                return _glints;
            }
        }

        private static void Build()
        {
            var ice = new float[Size * Size];
            var tips = new List<Vector3>();

            Grow(ice, tips);

            _sprite = ToSprite(Paint(ice), "frost");
            _glints = BuildGlints(tips);
        }

        // ---- Growth -------------------------------------------------------

        private static void Grow(float[] ice, List<Vector3> tips)
        {
            var rng = new Rng(0x9E3779B9u);

            // Feathers rooted along the border, reaching in, a few per side.
            for (int k = 0; k < Feathers; k++)
            {
                int side = k % 4;
                float t = rng.Range(0.08f, 0.92f) * Size;

                float x, y, angle;
                switch (side)
                {
                    case 0: x = t; y = Root; angle = Mathf.PI / 2f; break;
                    case 1: x = Size - Root; y = t; angle = Mathf.PI; break;
                    case 2: x = t; y = Size - Root; angle = -Mathf.PI / 2f; break;
                    default: x = Root; y = t; angle = 0f; break;
                }

                Branch(ice, tips, rng, x, y, angle + rng.Range(-0.7f, 0.7f), rng.Range(18f, 44f), rng.Range(1.4f, 2.0f), 2);
            }

            // Two small six-armed crystals loose in the middle.
            for (int k = 0; k < Crystals; k++)
            {
                float cx = rng.Range(40f, 88f);
                float cy = rng.Range(40f, 88f);
                float turn = rng.Range(0f, Sixty);

                for (int arm = 0; arm < 6; arm++)
                    Branch(ice, tips, rng, cx, cy, turn + arm * Sixty, rng.Range(7f, 11f), 1.3f, 1);
            }
        }

        /// <summary>
        /// One stem: a slightly curving walk in short steps, tapering, that
        /// puts out barbs at 60° either side while <paramref name="depth"/>
        /// lasts. Barbs shorten toward the tip, which is what gives a feather
        /// its shape. The tip is kept for a glint.
        /// </summary>
        private static void Branch(float[] ice, List<Vector3> tips, Rng rng,
            float x, float y, float angle, float length, float width, int depth)
        {
            int steps = Mathf.Max(2, (int)(length / 3f));
            float curve = rng.Range(-0.03f, 0.03f);
            float step = length / steps;
            float brightness = depth >= 2 ? 1f : depth == 1 ? 0.92f : 0.8f;

            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps;
                float t1 = (i + 1) / (float)steps;

                angle += curve;
                float a = angle + rng.Range(-0.05f, 0.05f);
                float nx = x + Mathf.Cos(a) * step;
                float ny = y + Mathf.Sin(a) * step;

                float w0 = width * (1f - 0.65f * t0);
                float w1 = width * (1f - 0.65f * t1);
                Stroke(ice, x, y, nx, ny, w0, w1, brightness);

                if (depth > 0 && i > 0 && i < steps - 1)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (rng.Next01() >= 0.8f) continue;

                        float barb = length * rng.Range(0.25f, 0.4f) * (1f - 0.7f * t0);
                        if (barb > 2.5f)
                            Branch(ice, tips, rng, nx, ny, a + side * Sixty + rng.Range(-0.08f, 0.08f),
                                barb, Mathf.Max(1f, w1 * 0.8f), depth - 1);
                    }
                }

                x = nx;
                y = ny;
            }

            tips.Add(new Vector3(x, y, depth));
        }

        /// <summary>An anti-aliased segment whose width tapers from <paramref name="w0"/> to <paramref name="w1"/>, kept at the brightest coverage.</summary>
        private static void Stroke(float[] ice, float x0, float y0, float x1, float y1, float w0, float w1, float brightness)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - w0 - 1f));
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + w0 + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - w0 - 1f));
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + w0 + 1f));

            float dx = x1 - x0;
            float dy = y1 - y0;
            float lengthSquared = dx * dx + dy * dy + 1e-6f;

            for (int py = minY; py <= maxY; py++)
            {
                for (int px = minX; px <= maxX; px++)
                {
                    float cx = px + 0.5f;
                    float cy = py + 0.5f;

                    float t = Mathf.Clamp01(((cx - x0) * dx + (cy - y0) * dy) / lengthSquared);
                    float ex = cx - (x0 + t * dx);
                    float ey = cy - (y0 + t * dy);
                    float distance = Mathf.Sqrt(ex * ex + ey * ey);

                    float halfWidth = 0.5f * (w0 + (w1 - w0) * t);
                    float coverage = Mathf.Clamp01(halfWidth - distance + 0.5f) * brightness;

                    int index = py * Size + px;
                    if (coverage > ice[index]) ice[index] = coverage;
                }
            }
        }

        // ---- Paint --------------------------------------------------------

        /// <summary>The frost's colours: a blue-white rime crust thick at the rim, a soft bloom round every crystal, white crystal lines, a fine glitter of ice grains.</summary>
        private static Color32[] Paint(float[] ice)
        {
            var bloom = Blur(Blur(ice, 3), 3);
            var pixels = new Color32[Size * Size];

            var deep = new Color(0.42f, 0.66f, 0.90f);
            var rime = new Color(0.70f, 0.87f, 0.98f);
            var white = new Color(0.95f, 0.99f, 1f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int index = y * Size + x;
                    float u = x / (float)Size;
                    float v = y / (float)Size;

                    float haze = LavaTexture.Fbm(u, v, 3, 3, 89);
                    float rim = Mathf.Min(Mathf.Min(x, Size - 1 - x), Mathf.Min(y, Size - 1 - y));
                    float crust = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(1f - rim / CrustDepth), 1.6f) * (0.6f + 0.8f * haze));

                    float speck = Mathf.Clamp01((Grain(x, y) - 0.93f) / 0.07f);
                    float glow = Mathf.Clamp01(bloom[index] * 2.2f);
                    float line = Mathf.Clamp01(ice[index]);

                    var colour = Color.Lerp(deep, rime, Mathf.Clamp01(crust + glow + speck));
                    colour = Color.Lerp(colour, white, line);

                    float sheet = 0.08f + 0.10f * haze + 0.45f * crust;
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((rim + 0.5f) / EdgeFade));
                    colour.a = Mathf.Clamp01(sheet + 0.30f * glow + 0.3f * speck + 0.9f * line) * edge;

                    pixels[index] = colour;
                }
            }

            return pixels;
        }

        /// <summary>A box blur of radius <paramref name="radius"/>, one pass each way, clamped at the borders.</summary>
        private static float[] Blur(float[] source, int radius)
        {
            var across = new float[source.Length];
            var result = new float[source.Length];
            float norm = 1f / (2 * radius + 1);

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0f;
                    for (int d = -radius; d <= radius; d++)
                        sum += source[y * Size + Mathf.Clamp(x + d, 0, Size - 1)];
                    across[y * Size + x] = sum * norm;
                }

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float sum = 0f;
                    for (int d = -radius; d <= radius; d++)
                        sum += across[Mathf.Clamp(y + d, 0, Size - 1) * Size + x];
                    result[y * Size + x] = sum * norm;
                }

            return result;
        }

        /// <summary>A fixed per-pixel hash in [0, 1]: the ice grains.</summary>
        private static float Grain(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + 7u * 1442695041u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        // ---- Glints -------------------------------------------------------

        /// <summary>
        /// Frames of four-pointed sparkles at about half the main and barb
        /// tips away from the border. Each glint rises and falls once per loop
        /// at its own phase, sharply, so at any moment only a few are lit.
        /// </summary>
        private static Sprite[] BuildGlints(List<Vector3> tips)
        {
            var rng = new Rng(0x51ED270Bu);
            var glints = new List<Vector4>();

            foreach (var tip in tips)
            {
                if (glints.Count >= MaxGlints) break;
                if (tip.z < 1f) continue;
                if (tip.x <= 12f || tip.x >= Size - 12f || tip.y <= 12f || tip.y >= Size - 12f) continue;

                if (rng.Next01() < 0.5f)
                    glints.Add(new Vector4(tip.x, tip.y, rng.Next01(), rng.Range(5f, 8f)));
            }

            var frames = new Sprite[GlintFrameCount];
            var alpha = new float[Size * Size];
            var pixels = new Color32[Size * Size];

            for (int f = 0; f < GlintFrameCount; f++)
            {
                System.Array.Clear(alpha, 0, alpha.Length);

                foreach (var glint in glints)
                {
                    float wave = Mathf.Sin(2f * Mathf.PI * (f / (float)GlintFrameCount + glint.z));
                    if (wave <= 0f) continue;

                    float lit = wave * wave * wave * wave;
                    Sparkle(alpha, glint.x, glint.y, glint.w, lit);
                }

                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color(1f, 1f, 1f, alpha[i]);

                frames[f] = ToSprite(pixels, $"frost_glint_{f}");
            }

            return frames;
        }

        /// <summary>A four-pointed star of arm <paramref name="reach"/> with a soft core, at strength <paramref name="lit"/>.</summary>
        private static void Sparkle(float[] alpha, float gx, float gy, float reach, float lit)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(gx - reach - 1f));
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(gx + reach + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(gy - reach - 1f));
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(gy + reach + 1f));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - gx);
                    float dy = Mathf.Abs(y + 0.5f - gy);

                    float across = Mathf.Clamp01(1f - dx / reach) * Mathf.Clamp01(1f - dy / 0.9f);
                    float down = Mathf.Clamp01(1f - dy / reach) * Mathf.Clamp01(1f - dx / 0.9f);
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 1.8f);

                    float value = lit * Mathf.Max(Mathf.Max(across, down), core);
                    int index = y * Size + x;
                    if (value > alpha[index]) alpha[index] = value;
                }
            }
        }

        // ---- Plumbing -----------------------------------------------------

        private static Sprite ToSprite(Color32[] pixels, string name)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = name;
            return sprite;
        }

        /// <summary>Xorshift32: a small fixed-seed generator, so the frost is the same in every build and on every platform.</summary>
        private sealed class Rng
        {
            private uint _state;

            public Rng(uint seed) => _state = seed == 0u ? 1u : seed;

            public uint Next()
            {
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }

            /// <summary>In [0, 1).</summary>
            public float Next01() => (Next() >> 8) / 16777216f;

            public float Range(float min, float max) => min + (max - min) * Next01();
        }
    }
}
