// Assets/_Project/Scripts/Unity/View/Figures/LookSheet.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>A composed sheet: straight RGBA, row 0 at the bottom, like <see cref="FigureImage"/>.</summary>
    public sealed class SheetImage
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; }

        public SheetImage(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height * 4];
        }

        public void Fill(int x0, int y0, int w, int h, FigureColour c)
        {
            byte r = B(c.R), g = B(c.G), b = B(c.B);
            for (int y = Math.Max(0, y0); y < Math.Min(Height, y0 + h); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(Width, x0 + w); x++)
            {
                int i = (y * Width + x) * 4;
                Pixels[i] = r; Pixels[i + 1] = g; Pixels[i + 2] = b; Pixels[i + 3] = 255;
            }
        }

        internal static byte B(float v) => (byte)Math.Max(0, Math.Min(255, (int)(v * 255f + 0.5f)));
    }

    /// <summary>A surface a figure is judged against: the floor, the carpet, the lit inlay, a felt.</summary>
    public readonly struct Backdrop
    {
        public readonly string Name;
        public readonly FigureColour Colour;

        public Backdrop(string name, FigureColour colour)
        {
            Name = name;
            Colour = colour;
        }
    }

    /// <summary>
    /// The look book's judging sheets, composed in plain C# (OPERATOR_LOOKBOOK.md,
    /// LB3): the contact sheet and the squint sheet. The editor window shows
    /// them and exports them; the same code previews them outside Unity.
    /// </summary>
    /// <remarks>
    /// <b>Real scale is the point.</b> A figure is shrunk to the pixel height
    /// it actually has on a screen (about 90 px on a phone held upright,
    /// about 64 on a 1440p desktop, per the LB0 screenshots), with an
    /// area-weighted filter, the way mipmaps shrink it in play. Iterating on
    /// the 280-texel render instead would judge detail nobody sees.
    /// </remarks>
    public static class LookSheet
    {
        private const int Pad = 6;

        /// <summary>
        /// One row per operator, one column per backdrop; each cell holds the
        /// standing and seated figure side by side, scaled together so the
        /// standing one is <paramref name="figureHeight"/> pixels tall.
        /// </summary>
        public static SheetImage Contact(IReadOnlyList<FigureImage[]> rows, IReadOnlyList<Backdrop> backdrops, int figureHeight)
        {
            // Every row gets the same cell width so the columns line up.
            int cellWidth = 0;
            foreach (var row in rows)
            {
                int w = Pad;
                foreach (var figure in row) w += ScaledWidth(figure, row[0], figureHeight) + Pad;
                cellWidth = Math.Max(cellWidth, w);
            }

            int cellHeight = figureHeight + Pad * 2;
            var sheet = new SheetImage(cellWidth * backdrops.Count, cellHeight * rows.Count);

            for (int r = 0; r < rows.Count; r++)
            {
                // Row 0 of the sheet is the bottom; the first operator goes at the top.
                int y = (rows.Count - 1 - r) * cellHeight;

                for (int c = 0; c < backdrops.Count; c++)
                {
                    int x = c * cellWidth;
                    sheet.Fill(x, y, cellWidth, cellHeight, backdrops[c].Colour);

                    int cursor = x + Pad;
                    foreach (var figure in rows[r])
                    {
                        Draw(sheet, figure, rows[r][0], figureHeight, cursor, y + Pad, black: false);
                        cursor += ScaledWidth(figure, rows[r][0], figureHeight) + Pad;
                    }
                }
            }

            return sheet;
        }

        /// <summary>Every standing figure as a pure black silhouette, <paramref name="height"/> pixels tall, on white.</summary>
        public static SheetImage Squint(IReadOnlyList<FigureImage> figures, int height = SilhouetteMetrics.SquintHeight)
        {
            int width = Pad;
            foreach (var figure in figures) width += ScaledWidth(figure, figure, height) + Pad * 2;

            var sheet = new SheetImage(width, height + Pad * 2);
            sheet.Fill(0, 0, sheet.Width, sheet.Height, new FigureColour(1f, 1f, 1f));

            int cursor = Pad;
            foreach (var figure in figures)
            {
                Draw(sheet, figure, figure, height, cursor, Pad, black: true);
                cursor += ScaledWidth(figure, figure, height) + Pad * 2;
            }

            return sheet;
        }

        private static float Scale(FigureImage reference, int height) =>
            reference.IsEmpty ? 1f : height / (float)(reference.OpaqueTopRow - reference.OpaqueBottomRow + 1);

        private static int ScaledWidth(FigureImage figure, FigureImage reference, int height)
        {
            Columns(figure, out int x0, out int x1);
            return Math.Max(1, (int)Math.Ceiling((x1 - x0 + 1) * Scale(reference, height)));
        }

        private static void Columns(FigureImage image, out int x0, out int x1)
        {
            x0 = image.Width; x1 = -1;
            for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                if (image.Alpha(x, y) <= FigureRasterizer.OpaqueThreshold) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
            }

            if (x1 < 0) { x0 = 0; x1 = 0; }
        }

        /// <summary>
        /// Shrinks <paramref name="figure"/> by the scale that makes
        /// <paramref name="reference"/> the given height, and lays it over the
        /// sheet with its feet at <paramref name="floorY"/>. Area-weighted and
        /// premultiplied, so edges shrink the way mipmaps shrink them.
        /// </summary>
        private static void Draw(SheetImage sheet, FigureImage figure, FigureImage reference, int height, int left, int floorY, bool black)
        {
            if (figure.IsEmpty) return;

            float scale = Scale(reference, height);
            Columns(figure, out int x0, out int x1);

            // Both poses share the standing figure's feet, so a seated cut
            // sits as high as its waist does: the table hides the rest.
            int y0 = reference.OpaqueBottomRow;
            int w = (int)Math.Ceiling((x1 - x0 + 1) * scale);
            int h = (int)Math.Ceiling((figure.OpaqueTopRow - y0 + 1) * scale);

            var sum = new float[w * h * 4];
            var weight = new float[w * h];

            for (int sy = y0; sy <= figure.OpaqueTopRow; sy++)
            {
                for (int sx = x0; sx <= x1; sx++)
                {
                    int i = (sy * figure.Width + sx) * 4;
                    float a = figure.Pixels[i + 3] / 255f;

                    float fx = (sx - x0) * scale, fy = (sy - y0) * scale;
                    int ix0 = (int)Math.Floor(fx), iy0 = (int)Math.Floor(fy);
                    int ix1 = (int)Math.Ceiling(fx + scale), iy1 = (int)Math.Ceiling(fy + scale);

                    for (int y = Math.Max(0, iy0); y < Math.Min(h, iy1); y++)
                    {
                        float oy = Math.Min(y + 1, fy + scale) - Math.Max(y, fy);
                        if (oy <= 0f) continue;

                        for (int x = Math.Max(0, ix0); x < Math.Min(w, ix1); x++)
                        {
                            float ox = Math.Min(x + 1, fx + scale) - Math.Max(x, fx);
                            if (ox <= 0f) continue;

                            float k = ox * oy;
                            int j = y * w + x;
                            weight[j] += k;
                            if (a <= 0f) continue;

                            sum[j * 4] += figure.Pixels[i] / 255f * a * k;
                            sum[j * 4 + 1] += figure.Pixels[i + 1] / 255f * a * k;
                            sum[j * 4 + 2] += figure.Pixels[i + 2] / 255f * a * k;
                            sum[j * 4 + 3] += a * k;
                        }
                    }
                }
            }

            for (int y = 0; y < h; y++)
            {
                int ty = floorY + y;
                if (ty < 0 || ty >= sheet.Height) continue;

                for (int x = 0; x < w; x++)
                {
                    int tx = left + x;
                    if (tx < 0 || tx >= sheet.Width) continue;

                    int j = y * w + x;
                    if (weight[j] <= 0f) continue;

                    float a = sum[j * 4 + 3] / weight[j];
                    if (a <= 0f) continue;

                    float r = black ? 0f : sum[j * 4] / weight[j];
                    float g = black ? 0f : sum[j * 4 + 1] / weight[j];
                    float b = black ? 0f : sum[j * 4 + 2] / weight[j];
                    if (black) a = a >= 0.5f ? 1f : 0f;

                    int t = (ty * sheet.Width + tx) * 4;
                    var p = sheet.Pixels;
                    p[t] = SheetImage.B(r + p[t] / 255f * (1f - a));
                    p[t + 1] = SheetImage.B(g + p[t + 1] / 255f * (1f - a));
                    p[t + 2] = SheetImage.B(b + p[t + 2] / 255f * (1f - a));
                    p[t + 3] = 255;
                }
            }
        }
    }
}
