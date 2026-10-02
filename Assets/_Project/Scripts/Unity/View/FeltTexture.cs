// Assets/_Project/Scripts/Unity/View/FeltTexture.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A square of card-table felt with a gold rail, drawn in code
    /// (CORE_GAMEPLAY.md, CG14), for the cells Fortuna's The Table covers.
    /// </summary>
    /// <remarks>
    /// <b>An object, not a glow.</b> The lava, the haze and the frost are
    /// light on the floor and go unlit; the table is a thing standing on the
    /// cell, so it is drawn mostly opaque and takes the room's light like the
    /// board does. Fine nap noise keeps the green from reading as a flat
    /// swatch, the edges darken a little toward the rail, and the corners are
    /// rounded.
    /// </remarks>
    public static class FeltTexture
    {
        public const int Size = 64;

        private static Sprite _sprite;

        /// <summary>The felt tile; built on first use.</summary>
        public static Sprite Sprite => _sprite != null ? _sprite : (_sprite = Build());

        private static Sprite Build()
        {
            var pixels = new Color32[Size * Size];
            var felt = new Color(0.07f, 0.34f, 0.19f);
            var deep = new Color(0.03f, 0.19f, 0.10f);
            var rail = new Color(0.80f, 0.64f, 0.32f);

            const float corner = 7f;
            const float railInset = 3f;
            const float railWidth = 2f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Distance inside a rounded square: negative outside.
                    float dx = Mathf.Max(corner - x, x - (Size - 1 - corner), 0f);
                    float dy = Mathf.Max(corner - y, y - (Size - 1 - corner), 0f);
                    float inside = corner - Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Min(Mathf.Min(x, Size - 1 - x), Mathf.Min(y, Size - 1 - y));
                    float depth = dx > 0f || dy > 0f ? inside : edge;

                    float alpha = Mathf.Clamp01(depth + 0.5f);
                    if (alpha <= 0f)
                    {
                        pixels[y * Size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float u = x / (float)Size;
                    float v = y / (float)Size;
                    float nap = LavaTexture.Fbm(u, v, 16, 2, 97) - 0.5f;
                    float vignette = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((depth - railInset) / 18f));

                    var colour = Color.Lerp(deep, felt, vignette) * (1f + 0.18f * nap);

                    float railBand = Mathf.Abs(depth - railInset - railWidth * 0.5f);
                    float railMix = Mathf.Clamp01(1f - railBand / (railWidth * 0.5f + 0.5f));
                    colour = Color.Lerp(colour, rail, railMix * 0.9f);

                    colour.a = alpha * 0.92f;
                    pixels[y * Size + x] = colour;
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "felt",
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