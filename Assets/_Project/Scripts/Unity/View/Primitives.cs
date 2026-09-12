// // Assets/_Project/Scripts/Unity/View/Primitives.cs
// using UnityEngine;

// namespace NonaRoyale.Unity.View
// {
//     /// <summary>
//     /// Generates the two sprites the prototype needs, at runtime.
//     /// </summary>
//     /// <remarks>
//     /// No art assets, no prefabs, nothing to import. The point of this build is
//     /// to answer whether the game is fun, and every minute spent wiring a scene
//     /// is a minute not spent finding that out. Real art replaces this later via
//     /// `ART_PIPELINE`.
//     /// </remarks>
//     public static class Primitives
//     {
//         private static Sprite _disc;
//         private static Sprite _ring;

//         public static Sprite Disc => _disc ?? (_disc = Build(64, filled: true));
//         public static Sprite Ring => _ring ?? (_ring = Build(64, filled: false));

//         private static Sprite Build(int size, bool filled)
//         {
//             var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
//             texture.filterMode = FilterMode.Bilinear;

//             float centre = (size - 1) * 0.5f;
//             float outer = centre - 1f;
//             float inner = outer * 0.62f;

//             var pixels = new Color32[size * size];

//             for (int y = 0; y < size; y++)
//             {
//                 for (int x = 0; x < size; x++)
//                 {
//                     float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));

//                     bool on = filled
//                         ? distance <= outer
//                         : distance <= outer && distance >= inner;

//                     // One pixel of feathering, so pieces do not look jagged at
//                     // the zoom levels this board is played at.
//                     float alpha = on ? Mathf.Clamp01(outer - distance + 1f) : 0f;
//                     pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
//                 }
//             }

//             texture.SetPixels32(pixels);
//             texture.Apply();

//             return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
//         }
//     }
// }

// Assets/_Project/Scripts/Unity/View/Primitives.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Generates the two sprites the prototype needs, at runtime.
    /// </summary>
    /// <remarks>
    /// No art assets, no prefabs, nothing to import. The point of this build is
    /// to answer whether the game is fun, and every minute spent wiring a scene
    /// is a minute not spent finding that out. Real art replaces this later via
    /// `ART_PIPELINE`.
    /// </remarks>
    public static class Primitives
    {
        private static Sprite _disc;
        private static Sprite _ring;
        private static Sprite _square;

        public static Sprite Disc => _disc ?? (_disc = Build(64, filled: true));
        public static Sprite Ring => _ring ?? (_ring = Build(64, filled: false));

        /// <summary>Board cells. Squares read as a Ludo grid; discs read as beads.</summary>
        public static Sprite Square => _square ?? (_square = BuildSquare(8));

        private static Sprite BuildSquare(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite Build(int size, bool filled)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            float centre = (size - 1) * 0.5f;
            float outer = centre - 1f;
            float inner = outer * 0.62f;

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));

                    bool on = filled
                        ? distance <= outer
                        : distance <= outer && distance >= inner;

                    // One pixel of feathering, so pieces do not look jagged at
                    // the zoom levels this board is played at.
                    float alpha = on ? Mathf.Clamp01(outer - distance + 1f) : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}