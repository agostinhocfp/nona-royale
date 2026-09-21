// Assets/_Project/Scripts/Unity/View/Figures/Rig/RigComposer.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>One rig part rasterised at rest on its own small canvas: what becomes one sprite in play.</summary>
    public sealed class RigPartImage
    {
        public RigPart Part { get; }
        public FigureImage Image { get; }

        public RigPartImage(RigPart part, FigureImage image)
        {
            Part = part;
            Image = image;
        }
    }

    /// <summary>
    /// Rasterises a rig's parts and assembles them into a posed figure
    /// (OPERATOR_LOOKBOOK.md, LB5). Plain C#.
    /// </summary>
    /// <remarks>
    /// <b>It assembles the way the game will.</b> Each part is rasterised once,
    /// at rest, on its own canvas; a pose then turns and moves those images
    /// and lays them over each other back to front, sampling them bilinearly,
    /// which is what a <c>SpriteRenderer</c> per part does under a rotated
    /// transform. So the judging window shows what the board will show,
    /// including the ink where limbs overlap and the rest-pose shading on a
    /// raised arm.
    ///
    /// A seated pose's table line clips everything below it; in play the
    /// table does that.
    /// </remarks>
    public static class RigComposer
    {
        /// <summary>
        /// Where a posed figure is assembled for judging: wider than the
        /// look book's canvas, since a step, a hit or a knockout reaches past
        /// a standing figure's footprint.
        /// </summary>
        public static readonly FigureCanvas PoseCanvas = new FigureCanvas(-1.6f, -0.1f, 307, 307, 96f, 4);

        /// <summary>Every part at rest, each on a canvas just big enough for its ink and rim.</summary>
        public static List<RigPartImage> RenderParts(OperatorRig rig, LookBookPalette palette, float pixelsPerUnit, int supersample, bool powered = false)
        {
            var images = new List<RigPartImage>();

            foreach (var part in rig.Parts(palette))
            {
                var drawing = part.Drawing;
                float pad = drawing.LineWeight + drawing.RimWidth + 3f / pixelsPerUnit;
                var box = drawing.Silhouette.Bounds.Expand(pad);

                int width = Math.Max(1, (int)Math.Ceiling((box.MaxX - box.MinX) * pixelsPerUnit));
                int height = Math.Max(1, (int)Math.Ceiling((box.MaxY - box.MinY) * pixelsPerUnit));
                var canvas = new FigureCanvas(box.MinX, box.MinY, width, height, pixelsPerUnit, supersample);

                images.Add(new RigPartImage(part, FigureRasterizer.Render(drawing, canvas, powered)));
            }

            return images;
        }

        /// <summary>The parts laid out in a pose on <paramref name="canvas"/>, as one figure image.</summary>
        public static FigureImage Compose(OperatorRig rig, IReadOnlyList<RigPartImage> parts, RigPose pose, FigureCanvas canvas)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var world = rig.Skeleton.Evaluate(pose);

            int width = canvas.Width, height = canvas.Height;
            float unit = 1f / canvas.PixelsPerUnit;
            var sum = new float[width * height * 4];

            foreach (var entry in parts)
            {
                if (!entry.Part.VisibleIn(pose)) continue;

                var bone = rig.Skeleton[entry.Part.Bone];
                var placed = world[entry.Part.Bone];
                var image = entry.Image;
                var from = image.Canvas;

                // The part's corners, placed, bound the texels worth visiting.
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                for (int corner = 0; corner < 4; corner++)
                {
                    float cx = from.Left + ((corner & 1) == 0 ? 0f : image.Width / from.PixelsPerUnit);
                    float cy = from.Bottom + ((corner & 2) == 0 ? 0f : image.Height / from.PixelsPerUnit);
                    RigSkeleton.Transform(placed, bone.PivotX, bone.PivotY, cx, cy, out float wx, out float wy);
                    minX = Math.Min(minX, wx); maxX = Math.Max(maxX, wx);
                    minY = Math.Min(minY, wy); maxY = Math.Max(maxY, wy);
                }

                int x0 = Math.Max(0, (int)Math.Floor((minX - canvas.Left) * canvas.PixelsPerUnit) - 1);
                int x1 = Math.Min(width - 1, (int)Math.Ceiling((maxX - canvas.Left) * canvas.PixelsPerUnit) + 1);
                int y0 = Math.Max(0, (int)Math.Floor((minY - canvas.Bottom) * canvas.PixelsPerUnit) - 1);
                int y1 = Math.Min(height - 1, (int)Math.Ceiling((maxY - canvas.Bottom) * canvas.PixelsPerUnit) + 1);

                for (int py = y0; py <= y1; py++)
                {
                    float y = canvas.Bottom + (py + 0.5f) * unit;
                    if (pose != null && pose.TableLine.HasValue && y < pose.TableLine.Value) continue;

                    for (int px = x0; px <= x1; px++)
                    {
                        float x = canvas.Left + (px + 0.5f) * unit;
                        RigSkeleton.Untransform(placed, bone.PivotX, bone.PivotY, x, y, out float rx, out float ry);

                        Sample(image, (rx - from.Left) * from.PixelsPerUnit - 0.5f, (ry - from.Bottom) * from.PixelsPerUnit - 0.5f,
                            out float r, out float g, out float b, out float a);
                        if (a <= 0f) continue;

                        // Premultiplied "over".
                        int i = (py * width + px) * 4;
                        float keep = 1f - a;
                        sum[i] = r + sum[i] * keep;
                        sum[i + 1] = g + sum[i + 1] * keep;
                        sum[i + 2] = b + sum[i + 2] * keep;
                        sum[i + 3] = a + sum[i + 3] * keep;
                    }
                }
            }

            var pixels = new byte[width * height * 4];
            int lowest = -1, highest = -1;

            for (int py = 0; py < height; py++)
            {
                bool any = false;
                for (int px = 0; px < width; px++)
                {
                    int i = (py * width + px) * 4;
                    float a = sum[i + 3];
                    if (a <= 0f) continue;

                    pixels[i] = Byte(sum[i] / a);
                    pixels[i + 1] = Byte(sum[i + 1] / a);
                    pixels[i + 2] = Byte(sum[i + 2] / a);
                    pixels[i + 3] = Byte(a);
                    if (pixels[i + 3] > FigureRasterizer.OpaqueThreshold) any = true;
                }

                if (!any) continue;
                if (lowest < 0) lowest = py;
                highest = py;
            }

            var result = new FigureImage(width, height, pixels, lowest, highest, canvas);
            result.Milliseconds = clock.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>Bilinear, premultiplied; transparent outside the image.</summary>
        private static void Sample(FigureImage image, float fx, float fy, out float r, out float g, out float b, out float a)
        {
            r = g = b = a = 0f;
            int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
            if (ix < -1 || iy < -1 || ix >= image.Width || iy >= image.Height) return;

            float tx = fx - ix, ty = fy - iy;
            Tap(image, ix, iy, (1f - tx) * (1f - ty), ref r, ref g, ref b, ref a);
            Tap(image, ix + 1, iy, tx * (1f - ty), ref r, ref g, ref b, ref a);
            Tap(image, ix, iy + 1, (1f - tx) * ty, ref r, ref g, ref b, ref a);
            Tap(image, ix + 1, iy + 1, tx * ty, ref r, ref g, ref b, ref a);
        }

        private static void Tap(FigureImage image, int x, int y, float w, ref float r, ref float g, ref float b, ref float a)
        {
            if (w <= 0f || x < 0 || y < 0 || x >= image.Width || y >= image.Height) return;

            int i = (y * image.Width + x) * 4;
            float alpha = image.Pixels[i + 3] / 255f * w;
            if (alpha <= 0f) return;

            r += image.Pixels[i] / 255f * alpha;
            g += image.Pixels[i + 1] / 255f * alpha;
            b += image.Pixels[i + 2] / 255f * alpha;
            a += alpha;
        }

        private static byte Byte(float v)
        {
            float x = v * 255f + 0.5f;
            return x <= 0f ? (byte)0 : x >= 255f ? (byte)255 : (byte)x;
        }
    }
}
