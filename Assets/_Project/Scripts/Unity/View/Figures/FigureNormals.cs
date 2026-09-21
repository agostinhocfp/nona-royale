// Assets/_Project/Scripts/Unity/View/Figures/FigureNormals.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Cel-facet normal maps for a drawing (OPERATOR_LOOKBOOK.md, LB5d; the
    /// designer's pick, 2026-09-21). The drawing's own shapes decide where
    /// its surface turns, so URP's 2D lights brighten a figure in the same
    /// hard bands its paint already has. Plain C#.
    /// </summary>
    /// <remarks>
    /// <b>The facets.</b> Every texel of the figure gets one of four flat
    /// normals, by the topmost layer under it:
    /// <list type="bullet">
    /// <item>a light layer's shape faces the key, up and toward the figure's
    /// back (the key is upper left for a figure facing right);</item>
    /// <item>a shade layer's shape faces away from it, toward the figure's
    /// front and down;</item>
    /// <item>everything else faces the camera;</item>
    /// <item>and a band as wide as the rim, inside the silhouette, turns
    /// outward along the edge, so a pool beside a figure catches its outline.</item>
    /// </list>
    /// A block in one of the metal colours (brass, gold, silver, steel)
    /// tilts its facet further, so the metal flashes where the cloth only
    /// brightens (§3: the brass and the gold are what the light is for).
    ///
    /// <b>Facing.</b> A left-facing drawing is the right-facing one reflected,
    /// its form shadows travelling with it, so its facets are reflected too:
    /// the side plane of a figure facing left faces left, and a pool to its
    /// left lights it. The rim band follows the silhouette on its own.
    ///
    /// <b>Encoding.</b> RGBA bytes, x in R and y in G (right and up in the
    /// sprite), z in B, alpha 255: what URP's <c>UnpackNormal</c> reads on
    /// every platform, both the RGB path and the RG-or-AG path. Uploaded as
    /// a linear texture. Where the figure is empty the normal faces the
    /// camera.
    /// </remarks>
    public static class FigureNormals
    {
        /// <summary>The camera-facing normal, encoded.</summary>
        public const byte FlatX = 128, FlatY = 128, FlatZ = 255;

        /// <summary>How far a lit plane turns toward the key: back and up.</summary>
        public const float LitX = -0.40f, LitY = 0.34f;

        /// <summary>How far a shaded plane turns from the key: forward and down.</summary>
        public const float ShadeX = 0.46f, ShadeY = -0.22f;

        /// <summary>How much further a metal facet turns than cloth.</summary>
        public const float MetalTilt = 1.7f;

        /// <summary>How far the edge band turns outward, against the camera axis.</summary>
        public const float EdgeTilt = 1.3f;

        /// <summary>
        /// The normal map for <paramref name="drawing"/> on <paramref name="canvas"/>,
        /// texel for texel with <see cref="FigureRasterizer.Render"/> on the same canvas.
        /// </summary>
        /// <param name="metals">Block colours that are metal; null for none.</param>
        /// <param name="facesLeft">True for a mirrored drawing, whose facets turn the other way.</param>
        public static byte[] Render(FigureDrawing drawing, FigureCanvas canvas, IReadOnlyList<FigureColour> metals, bool facesLeft)
        {
            if (drawing == null) throw new ArgumentNullException(nameof(drawing));

            int width = canvas.Width, height = canvas.Height;
            float unit = 1f / canvas.PixelsPerUnit;
            float flip = facesLeft ? -1f : 1f;

            // The layers that turn the surface, topmost last, with the metal ones marked.
            var layers = new List<(FigureLayer layer, bool metal)>();
            foreach (var layer in drawing.BeforeRim())
            {
                if (layer.Blend == FigureBlend.Fill)
                    layers.Add((layer, IsMetal(layer.Colour, metals)));
                else if (layer.Blend == FigureBlend.Shade || layer.Blend == FigureBlend.Light)
                    layers.Add((layer, false));
            }

            var silhouette = drawing.Silhouette;
            var reach = silhouette.Bounds.Expand(drawing.LineWeight + unit * 2f);
            float band = Math.Max(drawing.RimWidth, drawing.LineWeight) + drawing.LineWeight;

            var pixels = new byte[width * height * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = FlatX;
                pixels[i + 1] = FlatY;
                pixels[i + 2] = FlatZ;
                pixels[i + 3] = 255;
            }

            for (int py = 0; py < height; py++)
            {
                float y = canvas.Bottom + (py + 0.5f) * unit;
                if (y < reach.MinY || y > reach.MaxY) continue;

                for (int px = 0; px < width; px++)
                {
                    float x = canvas.Left + (px + 0.5f) * unit;
                    if (x < reach.MinX || x > reach.MaxX) continue;

                    float d = silhouette.Distance(x, y);
                    if (d > drawing.LineWeight + unit) continue;

                    // The facet: the topmost shade or light over the point,
                    // steeper where the block beneath it is metal.
                    float fx = 0f, fy = 0f;
                    bool metal = false;
                    foreach (var (layer, isMetal) in layers)
                    {
                        if (!Inside(layer.Shape, x, y)) continue;

                        switch (layer.Blend)
                        {
                            case FigureBlend.Fill:
                                metal = isMetal;
                                break;
                            case FigureBlend.Shade:
                                fx = ShadeX * flip;
                                fy = ShadeY;
                                break;
                            case FigureBlend.Light:
                                fx = LitX * flip;
                                fy = LitY;
                                break;
                        }
                    }

                    if (metal)
                    {
                        // Bare metal that neither shade nor light touches still turns toward the key a little.
                        if (fx == 0f && fy == 0f)
                        {
                            fx = LitX * 0.5f * flip;
                            fy = LitY * 0.5f;
                        }

                        fx *= MetalTilt;
                        fy *= MetalTilt;
                    }

                    float nx = fx, ny = fy, nz = 1f;

                    // The edge band: turned outward along the silhouette's gradient.
                    if (d > -band)
                    {
                        float h = unit * 0.5f;
                        float gx = silhouette.Distance(x + h, y) - silhouette.Distance(x - h, y);
                        float gy = silhouette.Distance(x, y + h) - silhouette.Distance(x, y - h);
                        float g = (float)Math.Sqrt(gx * gx + gy * gy);
                        if (g > 1e-6f)
                        {
                            // Full turn at the edge, none at the band's inner side.
                            float t = Math.Min(1f, Math.Max(0f, (d + band) / band));
                            nx += gx / g * EdgeTilt * t;
                            ny += gy / g * EdgeTilt * t;
                        }
                    }

                    Write(pixels, (py * width + px) * 4, nx, ny, nz);
                }
            }

            return pixels;
        }

        /// <summary>Decodes a texel back to a unit normal, as the shader does.</summary>
        public static void Decode(byte[] pixels, int index, out float x, out float y, out float z)
        {
            x = pixels[index] / 255f * 2f - 1f;
            y = pixels[index + 1] / 255f * 2f - 1f;
            z = pixels[index + 2] / 255f * 2f - 1f;
        }

        private static bool IsMetal(FigureColour colour, IReadOnlyList<FigureColour> metals)
        {
            if (metals == null) return false;
            foreach (var metal in metals)
                if (Math.Abs(metal.R - colour.R) < 0.004f && Math.Abs(metal.G - colour.G) < 0.004f && Math.Abs(metal.B - colour.B) < 0.004f)
                    return true;
            return false;
        }

        private static bool Inside(FigureShape shape, float x, float y)
        {
            var box = shape.Bounds;
            if (x < box.MinX || x > box.MaxX || y < box.MinY || y > box.MaxY) return false;
            return shape.Distance(x, y) <= 0f;
        }

        private static void Write(byte[] pixels, int i, float x, float y, float z)
        {
            float length = (float)Math.Sqrt(x * x + y * y + z * z);
            x /= length;
            y /= length;
            z /= length;

            pixels[i] = Encode(x);
            pixels[i + 1] = Encode(y);
            pixels[i + 2] = Encode(z);
            pixels[i + 3] = 255;
        }

        private static byte Encode(float v)
        {
            float b = (v * 0.5f + 0.5f) * 255f + 0.5f;
            return b <= 0f ? (byte)0 : b >= 255f ? (byte)255 : (byte)b;
        }
    }
}
