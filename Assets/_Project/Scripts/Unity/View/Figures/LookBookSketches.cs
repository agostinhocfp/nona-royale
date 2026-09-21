// Assets/_Project/Scripts/Unity/View/Figures/LookBookSketches.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The colours a look-book figure may use. Built from <c>UiTheme</c>, which
    /// owns every colour in the view; plain C# so the rasteriser and the
    /// sketches can run outside the editor.
    /// </summary>
    public sealed class LookBookPalette
    {
        public FigureColour Ink;

        /// <summary>The cool rim on a dark figure: steel, never holo cyan (§5, devices are dark at rest).</summary>
        public FigureColour Rim;

        /// <summary>The cool rim on a light figure.</summary>
        public FigureColour RimOnLight;

        /// <summary>Multiplied into the hard shadow shape: the cool dark ambient.</summary>
        public FigureColour Shade;

        /// <summary>Screened into the hard highlight shape: the warm gold key.</summary>
        public FigureColour Key;

        /// <summary>Operator hardware (§3: aged brass, never gilt).</summary>
        public FigureColour Brass;

        /// <summary>The cast tell (holo cyan). Only ever drawn in the powered state.</summary>
        public FigureColour Powered;

        public FigureColour Bone;
        public FigureColour Obsidian;

        public FigureColour BouncerSuit;
        public FigureColour BouncerSkin;
        /// <summary>Screened into a dark suit's lit planes: cooler than the key, or black cloth goes brown.</summary>
        public FigureColour BouncerSheen;

        /// <summary>The hard specular wedge on black plate.</summary>
        public FigureColour PlateSheen;

        public FigureColour NuetuGrey;
        public FigureColour NuetuPlate;
    }

    /// <summary>
    /// The two throwaway figures that prove the rasteriser (OPERATOR_LOOKBOOK.md,
    /// LB0). <b>Not the recipes.</b> LB2 replaces this file with
    /// <c>OperatorLook</c> and the twelve recipes from §5.1; these exist to
    /// test the hardest value solutions at both ends of the ledger: Bouncer's
    /// black mass split by a white V, and Nuetu's all-light disc.
    /// </summary>
    /// <remarks>
    /// Figure space: x across with 0 on the centre line, y up from the feet.
    /// A standing figure is about 2.9 tall and at most 2 wide, which the
    /// look book's canvas (x −1..1, y 0..3) holds with room for the ink.
    /// Half-shapes are authored on the left and overlap the centre line by
    /// 0.05 before <see cref="FigureShape.Symmetric"/>, so the two halves
    /// never meet edge to edge and leave a seam.
    /// </remarks>
    public static class LookBookSketches
    {
        /// <summary>The one line weight, in figure units (§2.2 rule 2). About 1/96 of the figure's height.</summary>
        public const float LineWeight = 0.03f;

        /// <summary>Rim width, in figure units.</summary>
        public const float RimWidth = 0.032f;

        /// <summary>Where a seated figure is cut: the table hides everything below.</summary>
        public const float BouncerWaist = 1.05f;
        public const float NuetuWaist = 0.95f;

        /// <summary>
        /// The key's shadow: everything to the lower right of a line through
        /// (x, y), cut at the same angle on every figure (§2.2 rule 5: a warm
        /// key from the upper left).
        /// </summary>
        public static FigureShape KeyShadow(float x, float y) => FigureShape.HalfPlane(x, y, -1f, 0.55f);

        public static FigureDrawing Bouncer(LookBookPalette p)
        {
            // ── Silhouette: the wide low slab ──────────────────────────
            // Stepped Deco shoulders, arms folded into the mass, short legs.
            var half = FigureShape.Polygon(
                0.05f, 2.40f,
                -0.18f, 2.40f,     // bull neck
                -0.24f, 2.33f,
                -0.62f, 2.25f,     // trapezius
                -0.80f, 2.25f,     // shoulder, step one
                -0.80f, 2.17f,
                -0.93f, 2.17f,     // step two
                -0.96f, 1.98f,
                -0.95f, 1.12f,     // arm's outer edge
                -0.90f, 0.98f,     // fist
                -0.70f, 0.98f,
                -0.68f, 1.02f,
                -0.66f, 0.10f,     // leg
                -0.72f, 0.00f,     // shoe
                -0.12f, 0.00f,
                -0.08f, 0.58f,     // between the legs
                0.05f, 0.58f);

            var head = FigureShape.Ellipse(0f, 2.62f, 0.20f, 0.25f);

            // The gauntlet breaks the right-hand outline, bulkier than a
            // sleeve, stepped like the shoulders (ADR-0011).
            var gauntlet = FigureShape.Polygon(
                0.80f, 1.70f,
                1.00f, 1.66f,
                1.02f, 1.44f,
                0.99f, 1.40f,
                1.01f, 1.16f,
                0.97f, 0.92f,
                0.74f, 0.92f,
                0.74f, 1.60f);

            var body = half.Symmetric();
            var silhouette = FigureShape.Union(body, head, gauntlet);

            var drawing = new FigureDrawing(silhouette, p.BouncerSuit, p.Ink, LineWeight);

            // ── Blocks ─────────────────────────────────────────────────
            drawing.Block(head.Or(FigureShape.Rect(-0.15f, 2.32f, 0.15f, 2.45f)), p.BouncerSkin);

            // Dark glasses: one band where the face would be. No features.
            drawing.Block(FigureShape.Polygon(-0.21f, 2.66f, 0.21f, 2.66f, 0.19f, 2.58f, -0.19f, 2.58f), p.Obsidian);

            // The hard white V, splitting the black mass.
            drawing.Block(FigureShape.Polygon(-0.24f, 2.33f, 0.24f, 2.33f, 0f, 1.56f), p.Bone);

            // The tie, cut back out of the V in suit black.
            drawing.Block(FigureShape.Polygon(
                -0.04f, 2.30f, 0.04f, 2.30f, 0.055f, 1.78f, 0f, 1.68f, -0.055f, 1.78f), p.BouncerSuit);

            drawing.Block(gauntlet, p.Brass);

            // ── Shade and light: three values per material ─────────────
            // One near-vertical cut puts the right-hand plane in shadow, and
            // an offset circle carves the head's shadow side: form shadows,
            // not a sash across the figure.
            var shadow = FigureShape.Union(
                FigureShape.HalfPlane(0.30f, 1.60f, -1f, 0.12f),
                head.Minus(FigureShape.Circle(-0.07f, 2.66f, 0.22f)));
            drawing.Shade(shadow, p.Shade);

            // The lit planes face up and left: the shoulder tops, the left
            // lapel, the left of the skull, the gauntlet's top segment.
            var lit = FigureShape.Union(
                FigureShape.Polygon(
                    -0.24f, 2.33f, -0.62f, 2.25f, -0.80f, 2.25f, -0.80f, 2.17f, -0.93f, 2.17f, -0.96f, 1.98f,
                    -0.86f, 2.05f, -0.72f, 2.13f, -0.60f, 2.15f, -0.26f, 2.22f),
                FigureShape.Polygon(-0.24f, 2.33f, -0.38f, 2.29f, -0.08f, 1.48f, 0f, 1.56f),
                FigureShape.Polygon(0.80f, 1.70f, 1.00f, 1.66f, 1.01f, 1.58f, 0.78f, 1.60f));
            drawing.Light(lit.Minus(shadow), p.BouncerSheen);
            drawing.Light(head.Minus(FigureShape.Circle(0.10f, 2.58f, 0.22f)).Minus(shadow), p.Key);

            drawing.Rim(RimWidth, p.Rim, 0.30f);

            // ── Ink: the major internal forms only ─────────────────────
            // Where the arms fold into the mass, and the gauntlet's segments.
            drawing.InkLine(FigureShape.Polyline(-0.66f, 1.98f, -0.70f, 1.02f).Symmetric());
            drawing.InkLine(FigureShape.Polyline(0.76f, 1.40f, 1.00f, 1.40f));
            drawing.InkLine(FigureShape.Polyline(0.76f, 1.16f, 1.00f, 1.16f));

            // The tell has somewhere to go, and is never drawn at rest.
            drawing.Powered(FigureShape.Rect(0.78f, 1.20f, 0.97f, 1.36f), p.Powered);

            return drawing;
        }

        public static FigureDrawing Nuetu(LookBookPalette p)
        {
            // ── Silhouette: the disc ───────────────────────────────────
            var disc = FigureShape.Circle(0f, 1.52f, 0.84f);
            var head = FigureShape.Circle(0f, 2.44f, 0.21f);
            var legs = FigureShape.Polygon(
                -0.46f, 0.84f, -0.10f, 0.84f, -0.12f, 0.00f, -0.50f, 0.00f).Symmetric();

            var silhouette = FigureShape.Union(disc, head, legs);
            var drawing = new FigureDrawing(silhouette, p.NuetuGrey, p.Ink, LineWeight);

            // ── Black plates on grey ───────────────────────────────────
            // Two pauldrons, cut to the disc.
            var pauldron = FigureShape.Circle(-0.62f, 2.02f, 0.34f).And(disc).Symmetric();
            drawing.Block(pauldron, p.NuetuPlate);

            // The chest plate: a stepped Deco shield.
            var plate = FigureShape.Polygon(
                -0.26f, 1.94f,
                0.05f, 1.94f,
                0.05f, 1.02f,
                -0.06f, 1.02f,
                -0.17f, 1.16f,
                -0.17f, 1.42f,
                -0.26f, 1.52f).Symmetric();
            drawing.Block(plate, p.NuetuPlate);

            // The Deco furniture: a radial fan set into the plate, in the
            // figure's own grey, rising from a point at its foot.
            var fan = FigureShape.Union(
                Ray(0f, 1.08f, 90f, 0.80f, 0.028f),
                Ray(0f, 1.08f, 74f, 0.76f, 0.024f),
                Ray(0f, 1.08f, 106f, 0.76f, 0.024f),
                Ray(0f, 1.08f, 60f, 0.52f, 0.020f),
                Ray(0f, 1.08f, 120f, 0.52f, 0.020f)).And(plate.Offset(-0.045f));
            drawing.Block(fan, p.NuetuGrey);

            // Belt plate, and a black visor where the face would be.
            drawing.Block(FigureShape.Rect(-0.46f, 0.84f, 0.46f, 0.96f).And(disc), p.NuetuPlate);
            var visor = FigureShape.Rect(-0.22f, 2.42f, 0.22f, 2.50f);
            drawing.Block(visor, p.NuetuPlate);

            // ── Shade and light ────────────────────────────────────────
            // The cel sphere: an offset circle carves a crescent of shadow
            // on the lower right and a crescent of light on the upper left.
            var shadow = FigureShape.Union(
                disc.Minus(FigureShape.Circle(-0.16f, 1.66f, 0.84f)),
                head.Minus(FigureShape.Circle(-0.05f, 2.48f, 0.21f)),
                FigureShape.Rect(-0.25f, 0f, -0.10f, 0.84f),
                FigureShape.Rect(0.30f, 0f, 0.50f, 0.84f));
            drawing.Shade(shadow, p.Shade);

            var plates = FigureShape.Union(pauldron, plate, visor);
            var lit = FigureShape.Union(
                disc.Minus(FigureShape.Circle(0.12f, 1.40f, 0.84f)),
                head.Minus(FigureShape.Circle(0.06f, 2.40f, 0.21f)));
            drawing.Light(lit.Minus(shadow).Minus(plates), p.Key);

            // A hard specular wedge on each black plate.
            drawing.Light(FigureShape.Circle(-0.70f, 2.10f, 0.20f).And(pauldron).Minus(shadow), p.PlateSheen);
            drawing.Light(FigureShape.Polygon(-0.26f, 1.94f, -0.12f, 1.94f, -0.12f, 1.60f, -0.26f, 1.52f).And(plate), p.PlateSheen);

            drawing.Rim(RimWidth, p.RimOnLight, 0.30f);

            // Where the legs leave the disc.
            drawing.InkLine(FigureShape.Polyline(-0.46f, 0.84f, -0.10f, 0.84f).Symmetric());

            drawing.Powered(FigureShape.Rect(-0.20f, 2.44f, 0.20f, 2.48f), p.Powered);

            return drawing;
        }

        /// <summary>A thin wedge from (x, y) at <paramref name="degrees"/> from the x axis.</summary>
        private static FigureShape Ray(float x, float y, float degrees, float length, float halfWidth)
        {
            return FigureShape.Polygon(
                x, y,
                x + length, y - halfWidth,
                x + length, y + halfWidth).Rotate(degrees, x, y);
        }
    }
}
