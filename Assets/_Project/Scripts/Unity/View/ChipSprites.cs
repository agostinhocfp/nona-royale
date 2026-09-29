// Assets/_Project/Scripts/Unity/View/ChipSprites.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The shared parts of an operator chip, drawn in code (chip pieces,
    /// 2026-09-29): the body, its edge inserts, the gilt ring round the face
    /// and a plain disc. Every sprite is one unit across, centre pivot, so
    /// <see cref="ChipView"/> scales them all by the chip's diameter.
    /// </summary>
    /// <remarks>
    /// <b>Only the portrait is painted</b> (<c>ART_PROMPTS.md</c> asset block
    /// 7). The body is greyscale and takes the seat colour as a tint, so one
    /// set of sprites serves all four seats, and no ring is painted into the
    /// portraits.
    ///
    /// <b>Mipmapped, unlike most code-drawn sprites.</b> A chip is drawn at
    /// about a third of these textures' size and moves every step, and
    /// shrinking that far without mipmaps shimmers.
    ///
    /// The proportions are fractions of the chip's radius: the face fills
    /// <see cref="FaceRadius"/>, the gilt ring runs from there to
    /// <see cref="RingOuter"/>, and the body's band carries the inserts
    /// outside that.
    /// </remarks>
    public static class ChipSprites
    {
        /// <summary>The face's radius, as a fraction of the chip's.</summary>
        public const float FaceRadius = 0.78f;

        /// <summary>The gilt ring's outer edge, as a fraction of the chip's radius. Its inner edge sits a hair inside the face.</summary>
        public const float RingOuter = 0.86f;

        /// <summary>How many edge inserts ring the body.</summary>
        public const int InsertCount = 8;

        private const int Size = 256;

        private static Sprite _body;
        private static Sprite _inserts;
        private static Sprite _ring;
        private static Sprite _disc;
        private static Sprite _contact;

        /// <summary>The body: a greyscale disc, lighter toward the upper left, with a bevel at its edge. Tinted per seat.</summary>
        public static Sprite Body => _body != null ? _body : (_body = Build("chip_body", BodyAt));

        /// <summary>The edge inserts: stepped Deco blocks round the band, in white. Tinted ivory.</summary>
        public static Sprite Inserts => _inserts != null ? _inserts : (_inserts = Build("chip_inserts", InsertsAt));

        /// <summary>The ring round the face: a greyscale bevel, tinted gold.</summary>
        public static Sprite Ring => _ring != null ? _ring : (_ring = Build("chip_ring", RingAt));

        /// <summary>A plain white disc: the chip's thickness, the emblem face's ground, the hit flash and the rim's shape.</summary>
        public static Sprite Disc => _disc != null ? _disc : (_disc = Build("chip_disc", DiscAt));

        /// <summary>
        /// A contact shadow: solid to 80% of the radius, gone at the edge.
        /// Tighter than <c>BoardArt.SoftDisc</c>, so it darkens only the seam
        /// where the chip meets the table.
        /// </summary>
        public static Sprite Contact => _contact != null ? _contact : (_contact = Build("chip_contact", ContactAt));

        // ── Builders ────────────────────────────────────────────────────

        /// <summary>Antialiased coverage of a disc of radius 1, at <paramref name="r"/> (in radii), <paramref name="px"/> radii to a texel.</summary>
        private static float Inside(float r, float edge, float px) => Mathf.Clamp01((edge - r) / px + 0.5f);

        private static Color DiscAt(float x, float y, float px)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return new Color(1f, 1f, 1f, Inside(r, 1f, px));
        }

        private static Color ContactAt(float x, float y, float px)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, r));
            return new Color(1f, 1f, 1f, alpha);
        }

        private static Color BodyAt(float x, float y, float px)
        {
            float r = Mathf.Sqrt(x * x + y * y);

            // Light from the upper left (ART §2.2): a broad slope across the
            // face of the chip, and a bevel that catches it on the upper-left
            // edge and falls away on the lower right.
            float slope = 0.5f + 0.5f * ((-x + y) * 0.7071f);
            float value = Mathf.Lerp(0.80f, 0.96f, slope);

            float bevel = Mathf.InverseLerp(0.90f, 1f, r);
            if (bevel > 0f)
            {
                float facing = r > 0.0001f ? (-x + y) / r * 0.7071f : 0f;
                value += bevel * 0.14f * facing;
            }

            // A thin dark groove where the band meets the ring, so the ring sits in the chip rather than on it.
            float groove = 1f - Mathf.Clamp01(Mathf.Abs(r - (RingOuter + 0.012f)) / 0.012f);
            value -= 0.22f * groove;

            value = Mathf.Clamp01(value);
            return new Color(value, value, value, Inside(r, 1f, px));
        }

        private static Color InsertsAt(float x, float y, float px)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            if (r < RingOuter || r > 1f) return new Color(1f, 1f, 1f, 0f);

            // Which insert this texel falls in, and how far round it.
            float step = 2f * Mathf.PI / InsertCount;
            float angle = Mathf.Atan2(y, x) + step * 0.5f;
            float within = Mathf.Repeat(angle, step) - step * 0.5f;

            // A stepped block: wide at the chip's edge, a narrower step inward (Deco, not a plain square).
            float across = Mathf.Abs(within) * r;
            float depth = Mathf.InverseLerp(0.905f, 0.975f, r);
            float halfWidth = depth > 0.5f ? 0.085f : 0.05f;

            float side = Inside(across, halfWidth, px);
            float inner = Inside(-r, -0.905f, px);
            float outer = Inside(r, 0.975f, px);

            return new Color(1f, 1f, 1f, side * inner * outer);
        }

        private static Color RingAt(float x, float y, float px)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float inner = FaceRadius - 0.01f;

            float alpha = Inside(r, RingOuter, px) * Inside(-r, -inner, px);
            if (alpha <= 0f) return new Color(1f, 1f, 1f, 0f);

            // A rounded bead: brightest along its crown, and on the side that faces the light.
            float across = Mathf.InverseLerp(inner, RingOuter, r);
            float crown = Mathf.Sin(across * Mathf.PI);
            float facing = r > 0.0001f ? (-x + y) / r * 0.7071f : 0f;

            float value = Mathf.Clamp01(0.55f + 0.3f * crown + 0.25f * facing * crown);
            return new Color(value, value, value, alpha);
        }

        /// <summary>
        /// Samples <paramref name="at"/> at each texel centre, in chip radii
        /// (-1..1, y up), into a mipmapped sprite one unit across.
        /// </summary>
        private static Sprite Build(string name, System.Func<float, float, float, Color> at)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float half = Size * 0.5f;
            float px = 1f / half;   // one texel, in radii

            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = at((x + 0.5f - half) / half, (y + 0.5f - half) / half, px);

            texture.SetPixels(pixels);
            texture.Apply(true, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size,
                0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
    }
}
