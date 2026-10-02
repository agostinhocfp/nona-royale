// Assets/_Project/Scripts/Unity/View/LavaTexture.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A looping, tileable lava surface, drawn in code (CORE_GAMEPLAY.md,
    /// CG10): <see cref="FrameCount"/> frames of molten veins under a dark
    /// crust, flowing slowly, for Nuetu's Killzone.
    /// </summary>
    /// <remarks>
    /// <b>Frames, not a shader.</b> The pack's effects need a saved material
    /// per keyword (<see cref="ShaderFx"/>), and a texture scroll alone would
    /// slide the same picture across the cell. Domain-warped noise moved one
    /// full period per loop changes shape as it flows, which is what makes it
    /// read as molten rather than as a moving wallpaper. Built once, on first
    /// use, in a few tens of milliseconds.
    ///
    /// <b>It loops exactly.</b> Every noise layer is periodic over the tile,
    /// and every drift moves a whole number of periods per loop, so the last
    /// frame runs into the first with no seam in time. The edges fade out, so
    /// a cell's tile never shows a seam in space either.
    ///
    /// <b>Bright veins, dim crust.</b> Alpha follows the heat: the cracks
    /// carry the colour and the crust lets the board show through, so at the
    /// zone's low alpha it is a glow in the cells rather than a lid on them.
    /// </remarks>
    public static class LavaTexture
    {
        public const int Size = 64;
        public const int FrameCount = 32;

        /// <summary>Seconds per loop of the flow.</summary>
        public const float LoopSeconds = 8f;

        /// <summary>Pixels over which a tile fades out at its edge.</summary>
        private const float EdgeFade = 7f;

        private static Sprite[] _frames;

        /// <summary>The frames, in order; built on first use.</summary>
        public static IReadOnlyList<Sprite> Frames => _frames ?? (_frames = Build());

        private static Sprite[] Build()
        {
            var frames = new Sprite[FrameCount];
            var pixels = new Color32[Size * Size];

            for (int f = 0; f < FrameCount; f++)
            {
                float t = f / (float)FrameCount;
                float glow = 0.88f + 0.12f * Mathf.Sin(2f * Mathf.PI * t);

                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = x / (float)Size;
                        float v = y / (float)Size;

                        // The warp drifts one way, the surface the other, each
                        // a whole period per loop.
                        float wx = Fbm(u + t, v, 2, 3, 11) - 0.5f;
                        float wy = Fbm(u, v - t, 2, 3, 23) - 0.5f;
                        float n = Fbm(u + 0.45f * wx - t, v + 0.45f * wy, 3, 4, 37);

                        // Ridged: the heat runs along the middle of the noise,
                        // which draws veins rather than blobs.
                        float ridge = 1f - Mathf.Abs(2f * n - 1f);
                        float heat = Mathf.Clamp01(Mathf.Pow(ridge, 3.2f) * 1.25f * glow);

                        float edge = Mathf.Min(Mathf.Min(x, Size - 1 - x), Mathf.Min(y, Size - 1 - y));
                        float mask = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / EdgeFade));

                        var colour = Ramp(heat);
                        colour.a = (0.22f + 0.78f * heat) * mask;
                        pixels[y * Size + x] = colour;
                    }
                }

                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                {
                    name = $"lava_{f}",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                texture.SetPixels32(pixels);
                texture.Apply(false, true);

                frames[f] = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
                frames[f].name = texture.name;
            }

            return frames;
        }

        /// <summary>Crust to vein: dried blood, ember red, molten orange, a yellow-white core.</summary>
        private static Color Ramp(float heat)
        {
            var crust = new Color(0.20f, 0.03f, 0.02f);
            var ember = new Color(0.66f, 0.10f, 0.03f);
            var molten = new Color(0.97f, 0.42f, 0.06f);
            var core = new Color(1.00f, 0.84f, 0.45f);

            if (heat < 0.35f) return Color.Lerp(crust, ember, heat / 0.35f);
            if (heat < 0.75f) return Color.Lerp(ember, molten, (heat - 0.35f) / 0.40f);
            return Color.Lerp(molten, core, (heat - 0.75f) / 0.25f);
        }

        /// <summary>Fractal value noise, periodic over [0, 1) in both axes, in about [0, 1].</summary>
        private static float Fbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;

            for (int o = 0; o < octaves; o++)
            {
                sum += amplitude * ValueNoise(u * period, v * period, period, seed + o * 101);
                total += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }

            return sum / total;
        }

        /// <summary>Smoothly interpolated lattice noise that repeats every <paramref name="period"/> cells.</summary>
        private static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;

            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);

            float a = Lattice(x0, y0, period, seed);
            float b = Lattice(x0 + 1, y0, period, seed);
            float c = Lattice(x0, y0 + 1, period, seed);
            float d = Lattice(x0 + 1, y0 + 1, period, seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        private static float Lattice(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;

            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
    }
}