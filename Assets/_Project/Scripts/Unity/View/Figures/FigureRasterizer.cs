// Assets/_Project/Scripts/Unity/View/Figures/FigureRasterizer.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace NonaRoyale.Unity.View
{
    /// <summary>Which part of figure space becomes the texture, and how finely.</summary>
    public readonly struct FigureCanvas
    {
        /// <summary>Figure-space x of the texture's left edge.</summary>
        public readonly float Left;

        /// <summary>Figure-space y of the texture's bottom edge.</summary>
        public readonly float Bottom;

        public readonly int Width;
        public readonly int Height;

        /// <summary>Texels per figure unit.</summary>
        public readonly float PixelsPerUnit;

        /// <summary>Samples per texel along each axis. 4 → 16 samples per texel.</summary>
        public readonly int Supersample;

        public FigureCanvas(float left, float bottom, int width, int height, float pixelsPerUnit, int supersample)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("A canvas needs a size.");
            if (pixelsPerUnit <= 0f) throw new ArgumentException("A canvas needs a positive scale.", nameof(pixelsPerUnit));

            Left = left;
            Bottom = bottom;
            Width = width;
            Height = height;
            PixelsPerUnit = pixelsPerUnit;
            Supersample = Math.Max(1, supersample);
        }
    }

    /// <summary>A rendered figure: straight RGBA bytes, bottom row first, as Unity's textures want.</summary>
    public sealed class FigureImage
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>RGBA, four bytes per texel, row 0 at the bottom.</summary>
        public byte[] Pixels { get; }

        /// <summary>The lowest texel row with alpha above the threshold, or −1 if empty.</summary>
        public int OpaqueBottomRow { get; }

        /// <summary>The highest such row, or −1.</summary>
        public int OpaqueTopRow { get; }

        public FigureCanvas Canvas { get; }

        /// <summary>How long the render took, for the build-time log.</summary>
        public double Milliseconds { get; internal set; }

        public FigureImage(int width, int height, byte[] pixels, int opaqueBottom, int opaqueTop, FigureCanvas canvas)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
            OpaqueBottomRow = opaqueBottom;
            OpaqueTopRow = opaqueTop;
            Canvas = canvas;
        }

        public bool IsEmpty => OpaqueBottomRow < 0;

        public byte Alpha(int x, int y) => Pixels[(y * Width + x) * 4 + 3];
    }

    /// <summary>
    /// Turns a <see cref="FigureDrawing"/> into texels (OPERATOR_LOOKBOOK.md, LB0).
    /// </summary>
    /// <remarks>
    /// <b>Supersampled analytic coverage.</b> Each texel takes
    /// <c>Supersample²</c> samples; each sample reads every shape's signed
    /// distance and turns it into coverage with the same <c>0.5 − d</c> rule
    /// as <c>DecoSprites.Coverage</c>, measured in samples. That alone would
    /// anti-alias one shape; the supersampling is what keeps two flat shapes
    /// that share an edge from leaving a seam, which a single analytic sample
    /// per texel cannot do. The samples are averaged premultiplied.
    ///
    /// <b>Layers composite inside the silhouette.</b> The ink is the outermost
    /// shape and every other layer is clipped to the silhouette, which sits
    /// inside the ink, so a sample's alpha is the ink's coverage and every
    /// layer only changes its colour. That is why there is no
    /// layer-against-layer alpha to get wrong.
    ///
    /// <b>Cost.</b> Every texel is sampled once at its centre first; only a
    /// texel an edge actually crosses takes the full supersample, which is
    /// about one in eight on a figure. Every layer is skipped outside its own
    /// box, a union skips children whose box is farther than the nearest hit,
    /// and the rim reads a texel-resolution grid of the silhouette's distance
    /// rather than three exact distances per sample. About 70–90 ms for a
    /// 212×298 figure under a desktop JIT; slower under the editor's Mono,
    /// which is why <c>OperatorLookBook.Prewarm</c> renders off the main
    /// thread. Plain C#, no Unity types, so it runs in tests, on a worker
    /// thread and outside the editor.
    /// </remarks>
    public static class FigureRasterizer
    {
        /// <summary>Alpha at or below this counts as empty, as <c>OperatorArtLibrary</c> measures renders.</summary>
        public const byte OpaqueThreshold = 8;

        private struct Prepared
        {
            public FigureShape Shape;
            public FigureBounds Box;
            public FigureBlend Blend;
            public float R, G, B;
        }

        public static FigureImage Render(FigureDrawing drawing, FigureCanvas canvas, bool powered = false)
        {
            if (drawing == null) throw new ArgumentNullException(nameof(drawing));

            var clock = System.Diagnostics.Stopwatch.StartNew();
            var job = new Job(drawing, canvas, powered);

            int ss = canvas.Supersample;
            float unitPerTexel = 1f / canvas.PixelsPerUnit;
            float unitPerSample = unitPerTexel / ss;

            // How far a sample can sit from its texel's centre, plus half a
            // sample of anti-aliasing: a shape whose edge is farther than this
            // from the centre covers every sample in the texel the same way.
            float slack = unitPerTexel * 0.7072f + unitPerSample * 0.5f;

            int width = canvas.Width;
            int height = canvas.Height;
            var pixels = new byte[width * height * 4];
            float weight = 1f / (ss * ss);
            int lowest = -1, highest = -1;

            for (int py = 0; py < height; py++)
            {
                bool rowHasAlpha = false;
                float cy = canvas.Bottom + (py + 0.5f) * unitPerTexel;

                for (int px = 0; px < width; px++)
                {
                    float cx = canvas.Left + (px + 0.5f) * unitPerTexel;

                    float r, g, b, a;
                    bool uniform = true;
                    job.Sample(cx, cy, slack, ref uniform, out r, out g, out b, out a);

                    if (!uniform)
                    {
                        // An edge crosses this texel: take every sample.
                        float sumR = 0f, sumG = 0f, sumB = 0f, sumA = 0f;
                        bool ignored = true;

                        for (int sy = 0; sy < ss; sy++)
                        {
                            float y = canvas.Bottom + (py * ss + sy + 0.5f) * unitPerSample;

                            for (int sx = 0; sx < ss; sx++)
                            {
                                float x = canvas.Left + (px * ss + sx + 0.5f) * unitPerSample;
                                job.Sample(x, y, 0f, ref ignored, out float sr, out float sg, out float sb, out float sa);
                                if (sa <= 0f) continue;

                                sumR += sr * sa;
                                sumG += sg * sa;
                                sumB += sb * sa;
                                sumA += sa;
                            }
                        }

                        if (sumA <= 0f) continue;

                        // Averaged premultiplied, then back to straight alpha.
                        r = sumR / sumA;
                        g = sumG / sumA;
                        b = sumB / sumA;
                        a = sumA * weight;
                    }

                    if (a <= 0f) continue;

                    int i = (py * width + px) * 4;
                    pixels[i] = Byte(r);
                    pixels[i + 1] = Byte(g);
                    pixels[i + 2] = Byte(b);
                    pixels[i + 3] = Byte(a);

                    if (pixels[i + 3] > OpaqueThreshold) rowHasAlpha = true;
                }

                if (!rowHasAlpha) continue;
                if (lowest < 0) lowest = py;
                highest = py;
            }

            var image = new FigureImage(width, height, pixels, lowest, highest, canvas);
            image.Milliseconds = clock.Elapsed.TotalMilliseconds;
            return image;
        }

        /// <summary>Coverage of a sample at signed distance <paramref name="samples"/>, in samples.</summary>
        public static float Coverage(float samples)
        {
            float c = 0.5f - samples;
            return c <= 0f ? 0f : c >= 1f ? 1f : c;
        }

        /// <summary>One drawing, ready to sample.</summary>
        private sealed class Job
        {
            private readonly FigureShape _sil;
            private readonly FigureBounds _inkBox;
            private readonly Prepared[] _before;
            private readonly Prepared[] _after;
            private readonly float _line, _halfLine, _spu, _margin;
            private readonly float _rim, _rimUp, _rimFloor, _rimR, _rimG, _rimB;
            private readonly float _baseR, _baseG, _baseB, _inkR, _inkG, _inkB;

            // The silhouette's distance at every texel centre, with a border.
            // The rim reads it bilinearly: three extra exact distances per
            // sample were most of the cost, and the rim's inner edge is a
            // soft-enough target that an interpolated distance is invisible.
            private const int GridBorder = 2;
            private readonly float[] _grid;
            private readonly int _gridWidth, _gridHeight;
            private readonly float _gridLeft, _gridBottom, _ppu;

            public Job(FigureDrawing drawing, FigureCanvas canvas, bool powered)
            {
                _spu = canvas.PixelsPerUnit * canvas.Supersample;
                _margin = 1f / _spu;
                _line = drawing.LineWeight;
                _halfLine = _line * 0.5f;
                _sil = drawing.Silhouette;
                _inkBox = _sil.Bounds.Expand(_line + _margin);

                _before = Prepare(drawing.BeforeRim());
                _after = Prepare(drawing.AfterRim(powered));

                _rim = drawing.RimWidth;
                _rimUp = _rim * 0.6f;
                _rimFloor = drawing.RimFloor;
                _rimR = drawing.RimColour.R; _rimG = drawing.RimColour.G; _rimB = drawing.RimColour.B;
                _baseR = drawing.Base.R; _baseG = drawing.Base.G; _baseB = drawing.Base.B;
                _inkR = drawing.Ink.R; _inkG = drawing.Ink.G; _inkB = drawing.Ink.B;

                _ppu = canvas.PixelsPerUnit;
                _gridWidth = canvas.Width + GridBorder * 2;
                _gridHeight = canvas.Height + GridBorder * 2;
                _gridLeft = canvas.Left + (0.5f - GridBorder) / _ppu;
                _gridBottom = canvas.Bottom + (0.5f - GridBorder) / _ppu;
                _grid = new float[_gridWidth * _gridHeight];

                // Exact near the figure; far from it only the sign matters.
                var near = _sil.Bounds.Expand(_line + _rim + 4f / _ppu);
                for (int gy = 0; gy < _gridHeight; gy++)
                {
                    float y = _gridBottom + gy / _ppu;
                    for (int gx = 0; gx < _gridWidth; gx++)
                    {
                        float x = _gridLeft + gx / _ppu;
                        _grid[gy * _gridWidth + gx] = near.Contains(x, y) ? _sil.Distance(x, y) : _sil.Bounds.LowerBound(x, y);
                    }
                }
            }

            /// <summary>The silhouette's distance, interpolated from the texel grid.</summary>
            private float Grid(float x, float y)
            {
                float fx = (x - _gridLeft) * _ppu;
                float fy = (y - _gridBottom) * _ppu;
                int ix = (int)Math.Floor(fx);
                int iy = (int)Math.Floor(fy);

                // Off the grid is off the canvas: the exact answer, rarely needed.
                if (ix < 0 || iy < 0 || ix >= _gridWidth - 1 || iy >= _gridHeight - 1) return _sil.Distance(x, y);

                float tx = fx - ix, ty = fy - iy;
                int i = iy * _gridWidth + ix;
                float bottom = _grid[i] + (_grid[i + 1] - _grid[i]) * tx;
                float top = _grid[i + _gridWidth] + (_grid[i + _gridWidth + 1] - _grid[i + _gridWidth]) * tx;
                return bottom + (top - bottom) * ty;
            }

            /// <summary>
            /// The colour at one point. With <paramref name="slack"/> above
            /// zero, also reports whether every edge is at least that far away,
            /// which means the whole texel around the point is this colour.
            /// </summary>
            public void Sample(float x, float y, float slack, ref bool uniform,
                out float r, out float g, out float b, out float a)
            {
                r = g = b = a = 0f;

                if (!_inkBox.Expand(slack).Contains(x, y)) return;

                // A texel centre sits on a grid node, where the grid is exact.
                float d = slack > 0f ? Grid(x, y) : _sil.Distance(x, y);
                if (slack > 0f && (Near(d - _line, slack) || Near(d, slack))) uniform = false;

                float inkCov = Coverage((d - _line) * _spu);
                if (inkCov <= 0f) return;

                float silCov = Coverage(d * _spu);

                // Start from the ink, then lay the base over it.
                r = _inkR; g = _inkG; b = _inkB;
                a = inkCov;

                if (silCov <= 0f) return;

                r += (_baseR - r) * silCov;
                g += (_baseG - g) * silCov;
                b += (_baseB - b) * silCov;

                Apply(_before, x, y, slack, silCov, ref uniform, ref r, ref g, ref b);

                if (_rim > 0f && -d < _rim + _margin + slack)
                {
                    if (slack > 0f && Near(y - _rimFloor, slack)) uniform = false;

                    if (y >= _rimFloor)
                    {
                        // Inside the silhouette, but a rim-width step sideways
                        // or up leaves it: an edge the light wraps. Constant
                        // width, whatever the shape.
                        float dl = Grid(x - _rim, y);
                        float dr = Grid(x + _rim, y);
                        float du = Grid(x, y + _rimUp);
                        if (slack > 0f && (Near(dl, slack) || Near(dr, slack) || Near(du, slack))) uniform = false;

                        float cov = silCov * (1f - Coverage(dl * _spu) * Coverage(dr * _spu) * Coverage(du * _spu));
                        if (cov > 0f)
                        {
                            r += (_rimR - r) * cov;
                            g += (_rimG - g) * cov;
                            b += (_rimB - b) * cov;
                        }
                    }
                }

                Apply(_after, x, y, slack, silCov, ref uniform, ref r, ref g, ref b);
            }

            private void Apply(Prepared[] layers, float x, float y, float slack, float clip, ref bool uniform,
                ref float r, ref float g, ref float b)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    ref var layer = ref layers[i];
                    if (!layer.Box.Contains(x, y) && !(slack > 0f && layer.Box.Expand(slack).Contains(x, y))) continue;

                    float d = layer.Shape.Distance(x, y);
                    if (layer.Blend == FigureBlend.InkLine) d = Math.Abs(d) - _halfLine;
                    if (slack > 0f && Near(d, slack)) uniform = false;

                    float cov = Coverage(d * _spu) * clip;
                    if (cov <= 0f) continue;

                    float tr, tg, tb;
                    switch (layer.Blend)
                    {
                        case FigureBlend.Shade:
                            tr = r * layer.R;
                            tg = g * layer.G;
                            tb = b * layer.B;
                            break;

                        case FigureBlend.Light:
                            tr = 1f - (1f - r) * (1f - layer.R);
                            tg = 1f - (1f - g) * (1f - layer.G);
                            tb = 1f - (1f - b) * (1f - layer.B);
                            break;

                        default:
                            tr = layer.R;
                            tg = layer.G;
                            tb = layer.B;
                            break;
                    }

                    r += (tr - r) * cov;
                    g += (tg - g) * cov;
                    b += (tb - b) * cov;
                }
            }

            private static bool Near(float distance, float slack) => distance < slack && distance > -slack;

            private Prepared[] Prepare(IEnumerable<FigureLayer> layers)
            {
                return layers.Select(layer => new Prepared
                {
                    Shape = layer.Shape,
                    Box = layer.Shape.Bounds.Expand(_margin + (layer.Blend == FigureBlend.InkLine ? _halfLine : 0f)),
                    Blend = layer.Blend,
                    R = layer.Colour.R,
                    G = layer.Colour.G,
                    B = layer.Colour.B,
                }).ToArray();
            }
        }

        private static byte Byte(float value)
        {
            float v = value * 255f + 0.5f;
            return v <= 0f ? (byte)0 : v >= 255f ? (byte)255 : (byte)v;
        }
    }
}
