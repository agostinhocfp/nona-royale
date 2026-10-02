// Assets/_Project/Scripts/Unity/View/FrostTexture.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A tileable frost surface, drawn in code (CORE_GAMEPLAY.md, CG12):
    /// fine crystal lines over a pale rime, for the cells Mimi's Cryo Field
    /// reaches.
    /// </summary>
    /// <remarks>
    /// <b>One frame, breathing.</b> Frost does not flow, so unlike
    /// <see cref="LavaTexture"/> there is no loop: the layer breathes its
    /// alpha instead. It shares the lava's periodic noise, sharpened into
    /// thin ridges, which read as crystals rather than veins. Edges fade, so
    /// a tile shows no seam.
    /// </remarks>
    public static class FrostTexture
    {
        public const int Size = 64;

        private const float EdgeFade = 7f;

        private static Sprite _sprite;

        /// <summary>The frost tile; built on first use.</summary>
        public static Sprite Sprite => _sprite != null ? _sprite : (_sprite = Build());

        private static Sprite Build()
        {
            var pixels = new Color32[Size * Size];
            var rime = new Color(0.62f, 0.82f, 0.95f);
            var crystal = new Color(0.92f, 0.98f, 1f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (float)Size;
                    float v = y / (float)Size;

                    // Two ridged layers at different scales cross, which is
                    // what branching frost looks like from above.
                    float a = 1f - Mathf.Abs(2f * LavaTexture.Fbm(u, v, 4, 3, 53) - 1f);
                    float b = 1f - Mathf.Abs(2f * LavaTexture.Fbm(u + 0.37f, v + 0.61f, 7, 2, 71) - 1f);
                    float lines = Mathf.Clamp01(Mathf.Max(Mathf.Pow(a, 9f), 0.8f * Mathf.Pow(b, 12f)) * 1.3f);
                    float haze = LavaTexture.Fbm(u, v, 2, 3, 89);

                    float edge = Mathf.Min(Mathf.Min(x, Size - 1 - x), Mathf.Min(y, Size - 1 - y));
                    float mask = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / EdgeFade));

                    var colour = Color.Lerp(rime, crystal, lines);
                    colour.a = (0.18f + 0.22f * haze + 0.6f * lines) * mask;
                    pixels[y * Size + x] = colour;
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "frost",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = texture.name;
            return sprite;
        }
    }
}