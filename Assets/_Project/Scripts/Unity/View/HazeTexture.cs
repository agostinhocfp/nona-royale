// Assets/_Project/Scripts/Unity/View/HazeTexture.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A looping, tileable haze, drawn in code (CORE_GAMEPLAY.md, CG13): a
    /// sinister green murk with silver wisps curling through it, drifting
    /// slowly, for Lethe's Eris' Exploit.
    /// </summary>
    /// <remarks>
    /// <b>Silver is hers.</b> Lethe is the roster's tarnished silver
    /// (<see cref="UiTheme.LookLetheSilver"/>), so the wisps carry her
    /// colour; the green is the poison of the apple Eris threw. The murk and
    /// the wisps drift against each other, so the cell churns rather than
    /// slides.
    ///
    /// <b>Built like <see cref="LavaTexture"/></b>, from the same periodic
    /// noise, and it loops exactly for the same reason: every layer drifts a
    /// whole period per loop. Softer than lava where it matters: no hot
    /// veins, a slower loop, and a wider edge fade, so a cell reads as fog
    /// pooled on the floor rather than a tile.
    /// </remarks>
    public static class HazeTexture
    {
        public const int Size = 64;
        public const int FrameCount = 32;

        /// <summary>Seconds per loop of the drift: slower than lava's, as fog is.</summary>
        public const float LoopSeconds = 11f;

        /// <summary>Pixels over which a tile fades out at its edge.</summary>
        private const float EdgeFade = 12f;

        private static Sprite[] _frames;

        /// <summary>The frames, in order; built on first use.</summary>
        public static IReadOnlyList<Sprite> Frames => _frames ?? (_frames = Build());

        private static Sprite[] Build()
        {
            var frames = new Sprite[FrameCount];
            var pixels = new Color32[Size * Size];

            var murk = new Color(0.03f, 0.12f, 0.06f);
            var poison = new Color(0.22f, 0.62f, 0.30f);
            var silver = Color.Lerp(UiTheme.LookLetheSilver, Color.white, 0.35f);

            for (int f = 0; f < FrameCount; f++)
            {
                float t = f / (float)FrameCount;

                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float u = x / (float)Size;
                        float v = y / (float)Size;

                        float wx = LavaTexture.Fbm(u + t, v, 2, 3, 13) - 0.5f;
                        float wy = LavaTexture.Fbm(u, v + t, 2, 3, 29) - 0.5f;

                        // The murk drifts one way, the wisps the other.
                        float green = LavaTexture.Fbm(u + 0.6f * wx + t, v + 0.6f * wy, 2, 4, 41);
                        float wisps = LavaTexture.Fbm(u - 0.5f * wy - t, v + 0.5f * wx - t, 3, 4, 67);

                        float g = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, green));
                        float ridge = 1f - Mathf.Abs(2f * wisps - 1f);
                        float s = Mathf.Clamp01(Mathf.Pow(ridge, 4f) * 1.3f);

                        var colour = Color.Lerp(murk, poison, g);
                        colour = Color.Lerp(colour, silver, s * 0.85f);

                        float edge = Mathf.Min(Mathf.Min(x, Size - 1 - x), Mathf.Min(y, Size - 1 - y));
                        float mask = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / EdgeFade));

                        colour.a = Mathf.Clamp01(0.12f + 0.45f * g + 0.6f * s) * mask;
                        pixels[y * Size + x] = colour;
                    }
                }

                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                {
                    name = $"haze_{f}",
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
    }
}