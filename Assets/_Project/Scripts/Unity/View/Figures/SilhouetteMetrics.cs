// Assets/_Project/Scripts/Unity/View/Figures/SilhouetteMetrics.cs
using System;

namespace NonaRoyale.Unity.View
{
    /// <summary>A figure's pure black silhouette at a set height, as a bit mask, row 0 at the bottom.</summary>
    public readonly struct Silhouette
    {
        public readonly int Width;
        public readonly int Height;
        public readonly bool[] Bits;

        public Silhouette(int width, int height, bool[] bits)
        {
            Width = width;
            Height = height;
            Bits = bits;
        }

        public bool this[int x, int y] => x >= 0 && x < Width && y >= 0 && y < Height && Bits[y * Width + x];

        public int Area
        {
            get
            {
                int n = 0;
                foreach (bool bit in Bits) if (bit) n++;
                return n;
            }
        }
    }

    /// <summary>
    /// The measurements the judging tools and the look-book tests share
    /// (OPERATOR_LOOKBOOK.md, LB3): the 64 px squint silhouette, how much two
    /// silhouettes overlap, and the palette rules. Plain C#.
    /// </summary>
    /// <remarks>
    /// <b>The squint scale is ART_DIRECTION §2.2's 64 px</b>, and every
    /// silhouette is brought to the same height before comparing. In play a
    /// figure's size also carries its health, but the squint test asks
    /// whether the <i>shapes</i> can be told apart, which size must not be
    /// allowed to answer.
    /// </remarks>
    public static class SilhouetteMetrics
    {
        /// <summary>§2.2's squint height, in pixels.</summary>
        public const int SquintHeight = 64;

        /// <summary>Alpha at or above this is inside the silhouette.</summary>
        public const byte SolidAlpha = 128;

        /// <summary>The figure's silhouette, cropped to its opaque box and scaled to <paramref name="height"/> pixels tall.</summary>
        public static Silhouette At(FigureImage image, int height)
        {
            if (image == null || image.IsEmpty) return new Silhouette(0, 0, Array.Empty<bool>());

            Crop(image, out int x0, out int y0, out int x1, out int y1);
            float scale = height / (float)(y1 - y0 + 1);
            int width = Math.Max(1, (int)Math.Ceiling((x1 - x0 + 1) * scale));

            var coverage = new float[width * height];
            var weight = new float[width * height];

            // Area-weighted: each source texel adds its share to the pixels it covers.
            for (int sy = y0; sy <= y1; sy++)
            {
                for (int sx = x0; sx <= x1; sx++)
                {
                    float solid = image.Alpha(sx, sy) >= SolidAlpha ? 1f : 0f;
                    Spread((sx - x0) * scale, (sy - y0) * scale, scale, width, height, solid, coverage, weight);
                }
            }

            var bits = new bool[width * height];
            for (int i = 0; i < bits.Length; i++) bits[i] = weight[i] > 0f && coverage[i] / weight[i] >= 0.5f;

            return new Silhouette(width, height, bits);
        }

        /// <summary>
        /// Intersection over union, with both silhouettes standing on the same
        /// floor and centred on the same line: 1 is the same shape, 0 no overlap.
        /// </summary>
        public static float Overlap(Silhouette a, Silhouette b)
        {
            int width = Math.Max(a.Width, b.Width);
            int height = Math.Max(a.Height, b.Height);
            int offsetA = (width - a.Width) / 2;
            int offsetB = (width - b.Width) / 2;
            int both = 0, either = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inA = a[x - offsetA, y];
                    bool inB = b[x - offsetB, y];
                    if (inA && inB) both++;
                    if (inA || inB) either++;
                }
            }

            return either == 0 ? 0f : both / (float)either;
        }

        // ── Palette rules (ART_DIRECTION §3, §5) ───────────────────────

        /// <summary>Holo cyan and its family: the tech register, which no figure may carry at rest.</summary>
        /// <remarks>
        /// Saturation from 0.45: holo cyan itself is 0.59. Below that sit the
        /// anti-aliased texels where Kian's steel rim meets his emerald jacket
        /// (about 0.35), which are teal by arithmetic, not by intent.
        /// </remarks>
        public static bool IsCyan(byte r, byte g, byte b)
        {
            Hsv(r, g, b, out float h, out float s, out float v);
            return h >= 165f && h <= 200f && s >= 0.45f && v >= 0.55f;
        }

        /// <summary>Bright gilt gold, which is Fortuna's alone. Aged brass is too dark to count.</summary>
        public static bool IsGilt(byte r, byte g, byte b)
        {
            Hsv(r, g, b, out float h, out float s, out float v);
            return h >= 30f && h <= 55f && s >= 0.35f && v >= 0.70f;
        }

        /// <summary>How many solid texels of the image pass <paramref name="test"/>.</summary>
        public static int Count(FigureImage image, Func<byte, byte, byte, bool> test)
        {
            int n = 0;
            var p = image.Pixels;
            for (int i = 0; i < p.Length; i += 4)
                if (p[i + 3] >= SolidAlpha && test(p[i], p[i + 1], p[i + 2])) n++;

            return n;
        }

        private static void Crop(FigureImage image, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = image.Width; y0 = image.Height; x1 = -1; y1 = -1;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (image.Alpha(x, y) < SolidAlpha) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }

            if (x1 < 0) { x0 = y0 = 0; x1 = y1 = 0; }
        }

        /// <summary>Adds a source texel covering [fx, fx+size) × [fy, fy+size) to the pixels it overlaps.</summary>
        private static void Spread(float fx, float fy, float size, int width, int height, float value, float[] sum, float[] weight)
        {
            int ix0 = (int)Math.Floor(fx), iy0 = (int)Math.Floor(fy);
            int ix1 = (int)Math.Ceiling(fx + size), iy1 = (int)Math.Ceiling(fy + size);

            for (int y = Math.Max(0, iy0); y < Math.Min(height, iy1); y++)
            {
                float oy = Math.Min(y + 1, fy + size) - Math.Max(y, fy);
                if (oy <= 0f) continue;

                for (int x = Math.Max(0, ix0); x < Math.Min(width, ix1); x++)
                {
                    float ox = Math.Min(x + 1, fx + size) - Math.Max(x, fx);
                    if (ox <= 0f) continue;

                    float w = ox * oy;
                    sum[y * width + x] += value * w;
                    weight[y * width + x] += w;
                }
            }
        }

        private static void Hsv(byte r8, byte g8, byte b8, out float h, out float s, out float v)
        {
            float r = r8 / 255f, g = g8 / 255f, b = b8 / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float d = max - min;

            v = max;
            s = max <= 0f ? 0f : d / max;

            if (d <= 0f) { h = 0f; return; }
            if (max == r) h = 60f * (((g - b) / d) % 6f);
            else if (max == g) h = 60f * ((b - r) / d + 2f);
            else h = 60f * ((r - g) / d + 4f);
            if (h < 0f) h += 360f;
        }
    }
}
