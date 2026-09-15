// Assets/_Project/Scripts/Unity/View/Primitives.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Generates every sprite the prototype needs, at runtime.
    /// </summary>
    /// <remarks>
    /// No art assets, no prefabs, nothing to import. This build exists to answer
    /// whether the game is fun, and shapes drawn in code answer that as well as
    /// shapes drawn in Photoshop. `ART_PIPELINE` replaces all of it later.
    /// </remarks>
    public static class Primitives
    {
        private static Sprite _disc;
        private static Sprite _ring;
        private static Sprite _square;
        private static Sprite _cross;
        private static readonly Dictionary<int, Sprite> Polygons = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> Stars = new Dictionary<int, Sprite>();

        public static Sprite Disc => _disc ?? (_disc = BuildDisc(64, filled: true));
        public static Sprite Ring => _ring ?? (_ring = BuildDisc(64, filled: false));

        /// <summary>Board cells. Squares read as a Ludo grid; discs read as beads.</summary>
        public static Sprite Square => _square ?? (_square = BuildSquare(8));

        /// <summary>
        /// A plus sign. Not a polygon — its concave corners are what make it
        /// read as a medic's mark rather than a rotated square.
        /// </summary>
        public static Sprite Cross => _cross ?? (_cross = BuildCross(96));

        /// <summary>
        /// A regular polygon, cached per shape. Sides and rotation are how
        /// operators are told apart before any art exists — a triangle and a
        /// hexagon are distinguishable at a glance and at any zoom, which
        /// colour alone is not once every seat already owns a colour.
        /// </summary>
        public static Sprite Polygon(int sides, float rotationDegrees)
        {
            int key = sides * 1000 + Mathf.RoundToInt(rotationDegrees);

            if (!Polygons.TryGetValue(key, out var sprite))
            {
                sprite = BuildPolygon(96, sides, rotationDegrees);
                Polygons[key] = sprite;
            }

            return sprite;
        }

        /// <summary>
        /// A star, cached per shape like <see cref="Polygon"/>. Concave like
        /// the cross — a polygon generator cannot produce it, and the indents
        /// are exactly what separates it from a decagon at board scale.
        /// </summary>
        public static Sprite Star(int points, float rotationDegrees)
        {
            int key = points * 1000 + Mathf.RoundToInt(rotationDegrees);

            if (!Stars.TryGetValue(key, out var sprite))
            {
                sprite = BuildStar(96, points, rotationDegrees);
                Stars[key] = sprite;
            }

            return sprite;
        }

        private static Sprite BuildDisc(int size, bool filled)
        {
            float centre = (size - 1) * 0.5f;
            float outer = centre - 1f;
            float inner = outer * 0.62f;

            return Rasterize(size, (x, y) =>
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));
                bool on = filled ? distance <= outer : distance <= outer && distance >= inner;
                return on ? Mathf.Clamp01(outer - distance + 1f) : 0f;
            });
        }

        private static Sprite BuildPolygon(int size, int sides, float rotationDegrees)
        {
            float centre = (size - 1) * 0.5f;
            float circumradius = centre - 1.5f;
            float rotation = rotationDegrees * Mathf.Deg2Rad;
            float wedge = 2f * Mathf.PI / sides;
            float apothem = circumradius * Mathf.Cos(Mathf.PI / sides);

            return Rasterize(size, (x, y) =>
            {
                float dx = x - centre;
                float dy = y - centre;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                if (distance < 0.0001f) return 1f;

                // Distance from the centre to the edge, in this direction.
                float angle = Mathf.Atan2(dy, dx) - rotation;
                float within = Mathf.Repeat(angle, wedge) - wedge * 0.5f;
                float edge = apothem / Mathf.Cos(within);

                return Mathf.Clamp01(edge - distance + 1f);
            });
        }

        private static Sprite BuildCross(int size)
        {
            float centre = (size - 1) * 0.5f;
            float outer = centre - 1.5f;
            float arm = outer * 0.36f;

            return Rasterize(size, (x, y) =>
            {
                float ax = Mathf.Abs(x - centre);
                float ay = Mathf.Abs(y - centre);

                // Signed distance to each bar's rectangle; inside either bar
                // is inside the cross. Negative inside, so 1 - d fades the edge.
                float horizontal = Mathf.Max(ax - outer, ay - arm);
                float vertical = Mathf.Max(ay - outer, ax - arm);

                return Mathf.Clamp01(1f - Mathf.Min(horizontal, vertical));
            });
        }

        private static Sprite BuildStar(int size, int points, float rotationDegrees)
        {
            float centre = (size - 1) * 0.5f;
            float outer = centre - 1.5f;
            float inner = outer * 0.382f; // the classic five-point proportion
            float rotation = rotationDegrees * Mathf.Deg2Rad;
            float halfWedge = Mathf.PI / points;

            // The edge runs from an outer vertex at (outer, 0) to the indent
            // at halfWedge; precompute that segment and its cross with the
            // outer vertex, then ray-cast against it per pixel.
            float dx = inner * Mathf.Cos(halfWedge) - outer;
            float dy = inner * Mathf.Sin(halfWedge);
            float edgeCross = outer * dy;

            return Rasterize(size, (x, y) =>
            {
                float px = x - centre;
                float py = y - centre;
                float distance = Mathf.Sqrt(px * px + py * py);

                if (distance < 0.0001f) return 1f;

                // Fold the direction into [0, halfWedge]: one edge segment.
                float angle = Mathf.Atan2(py, px) - rotation;
                float a = Mathf.Repeat(angle, 2f * halfWedge);
                if (a > halfWedge) a = 2f * halfWedge - a;

                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);
                float denom = cos * dy - sin * dx;
                float edge = denom > 0.0001f ? edgeCross / denom : outer;

                return Mathf.Clamp01(edge - distance + 1f);
            });
        }

        private static Sprite BuildSquare(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[size * size];

            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite Rasterize(int size, System.Func<int, int, float> alphaAt)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alphaAt(x, y));

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}