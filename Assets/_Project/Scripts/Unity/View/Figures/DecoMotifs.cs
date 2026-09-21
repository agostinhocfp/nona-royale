// Assets/_Project/Scripts/Unity/View/Figures/DecoMotifs.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The shared vocabulary of the look book (OPERATOR_LOOKBOOK.md, LB2): the
    /// drawn light and the Deco furniture, so twelve recipes cut their shadows
    /// at one angle and build their fans and chevrons one way.
    /// </summary>
    /// <remarks>
    /// Figure space throughout: x across with 0 on the centre line, y up from
    /// the feet. A standing figure is about 2.9 tall.
    /// </remarks>
    public static class DecoMotifs
    {
        /// <summary>The one line weight, in figure units (§2.2 rule 2). About 1/96 of a figure's height.</summary>
        public const float LineWeight = 0.03f;

        /// <summary>
        /// The drawn rim on a dark figure. Wider than on a light one: on the
        /// near-black floor the rim is most of what separates a dark figure
        /// from the board (LB0 Play Mode, 2026-09-21).
        /// </summary>
        public const float RimOnDark = 0.045f;

        /// <summary>The drawn rim on a light figure, which the ink already separates.</summary>
        public const float RimOnLight = 0.032f;

        /// <summary>No rim below this height: the rim lights the body, not the shoes.</summary>
        public const float RimFloor = 0.30f;

        /// <summary>
        /// The key's shadow side of a form: everything right of a line through
        /// (x, y) leaning <paramref name="lean"/> units across per unit up. The
        /// key is upper left (§2.2 rule 5), so shadows fall on the right.
        /// </summary>
        public static FigureShape ShadowSide(float x, float y, float lean = 0.12f) => FigureShape.HalfPlane(x, y, -1f, lean);

        /// <summary>
        /// The cel sphere: the part of a circle that an offset copy of it does
        /// not cover. Offset up and left, it is the shadow crescent; down and
        /// right, the lit one.
        /// </summary>
        public static FigureShape Crescent(float cx, float cy, float r, float dx, float dy) =>
            FigureShape.Circle(cx, cy, r).Minus(FigureShape.Circle(cx + dx, cy + dy, r));

        /// <summary>A thin wedge from (x, y) at <paramref name="degrees"/> from the x axis.</summary>
        public static FigureShape Ray(float x, float y, float degrees, float length, float halfWidth) =>
            FigureShape.Polygon(
                x, y,
                x + length, y - halfWidth,
                x + length, y + halfWidth).Rotate(degrees, x, y);

        /// <summary>
        /// A radial fan rising from (x, y): <paramref name="rays"/> wedges spread
        /// over <paramref name="spread"/> degrees about straight up, longest in
        /// the middle.
        /// </summary>
        public static FigureShape Fan(float x, float y, int rays, float spread, float length, float halfWidth)
        {
            var shapes = new FigureShape[rays];
            for (int i = 0; i < rays; i++)
            {
                float t = rays == 1 ? 0.5f : i / (float)(rays - 1);
                float centred = System.Math.Abs(t - 0.5f) * 2f;
                float degrees = 90f - spread * 0.5f + spread * t;
                shapes[i] = Ray(x, y, degrees, length * (1f - 0.35f * centred), halfWidth * (1f - 0.3f * centred));
            }

            return FigureShape.Union(shapes);
        }

        /// <summary>A chevron pointing down, centred on x = 0, as a band <paramref name="thickness"/> tall.</summary>
        public static FigureShape Chevron(float y, float halfWidth, float drop, float thickness) =>
            FigureShape.Polygon(
                -halfWidth, y,
                0f, y - drop,
                halfWidth, y,
                halfWidth, y - thickness,
                0f, y - drop - thickness,
                -halfWidth, y - thickness);

        /// <summary>
        /// A tapered limb from (<paramref name="x0"/>, <paramref name="y0"/>)
        /// to (<paramref name="x1"/>, <paramref name="y1"/>), round at both
        /// ends: the rigs' joint caps (LB5d), so a turn never opens a seam and
        /// no limb reads as a box.
        /// </summary>
        public static FigureShape Limb(float x0, float y0, float r0, float x1, float y1, float r1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float length = (float)System.Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-5f) return FigureShape.Circle(x0, y0, System.Math.Max(r0, r1));

            float nx = -dy / length, ny = dx / length;
            return FigureShape.Union(
                FigureShape.Circle(x0, y0, r0),
                FigureShape.Circle(x1, y1, r1),
                FigureShape.Polygon(
                    x0 + nx * r0, y0 + ny * r0,
                    x1 + nx * r1, y1 + ny * r1,
                    x1 - nx * r1, y1 - ny * r1,
                    x0 - nx * r0, y0 - ny * r0));
        }
    }
}
