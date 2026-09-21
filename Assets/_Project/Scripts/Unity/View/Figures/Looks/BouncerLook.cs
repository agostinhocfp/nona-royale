// Assets/_Project/Scripts/Unity/View/Figures/Looks/BouncerLook.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Bouncer (ART_DIRECTION §5.1, row 3): a black mass split by a hard white
    /// V, the wide low slab, the aged-brass gauntlet (ADR-0011).
    /// </summary>
    /// <remarks>
    /// Promoted from the LB0 sketch. Dark on a near-black floor, so the rim
    /// is the wide one and the V carries the read at 64 px.
    /// </remarks>
    public static class BouncerLook
    {
        public static readonly OperatorLook Look = new OperatorLook("Bouncer", 1.05f, Draw);

        private static FigureDrawing Draw(LookBookPalette p)
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
            // sleeve, stepped like the shoulders. Worn over his own arm.
            var gauntlet = FigureShape.Polygon(
                0.80f, 1.70f,
                1.00f, 1.66f,
                1.02f, 1.44f,
                0.99f, 1.40f,
                1.01f, 1.16f,
                0.97f, 0.92f,
                0.74f, 0.92f,
                0.74f, 1.60f);

            var silhouette = FigureShape.Union(half.Symmetric(), head, gauntlet);
            var drawing = new FigureDrawing(silhouette, p.BouncerSuit, p.Ink, DecoMotifs.LineWeight);

            // ── Blocks ─────────────────────────────────────────────────
            drawing.Block(head.Or(FigureShape.Rect(-0.15f, 2.32f, 0.15f, 2.45f)), p.BouncerSkin);

            // Dark glasses: one band where the face would be. No features.
            drawing.Block(FigureShape.Polygon(-0.21f, 2.66f, 0.21f, 2.66f, 0.19f, 2.58f, -0.19f, 2.58f), p.Obsidian);

            // The hard white V, splitting the black mass. His value solution.
            drawing.Block(FigureShape.Polygon(-0.24f, 2.33f, 0.24f, 2.33f, 0f, 1.56f), p.Bone);

            // The tie, cut back out of the V in suit black.
            drawing.Block(FigureShape.Polygon(
                -0.04f, 2.30f, 0.04f, 2.30f, 0.055f, 1.78f, 0f, 1.68f, -0.055f, 1.78f), p.BouncerSuit);

            drawing.Block(gauntlet, p.Brass);

            // ── Shade and light: three values per material ─────────────
            var shadow = FigureShape.Union(
                DecoMotifs.ShadowSide(0.30f, 1.60f),
                head.Minus(FigureShape.Circle(-0.07f, 2.66f, 0.22f)));
            drawing.Shade(shadow, p.Shade);

            // The lit planes face up and left: the shoulder tops, the left
            // lapel, the gauntlet's top segment; and the left of the skull.
            var lit = FigureShape.Union(
                FigureShape.Polygon(
                    -0.24f, 2.33f, -0.62f, 2.25f, -0.80f, 2.25f, -0.80f, 2.17f, -0.93f, 2.17f, -0.96f, 1.98f,
                    -0.86f, 2.05f, -0.72f, 2.13f, -0.60f, 2.15f, -0.26f, 2.22f),
                FigureShape.Polygon(-0.24f, 2.33f, -0.38f, 2.29f, -0.08f, 1.48f, 0f, 1.56f),
                FigureShape.Polygon(0.80f, 1.70f, 1.00f, 1.66f, 1.01f, 1.58f, 0.78f, 1.60f));
            drawing.Light(lit.Minus(shadow), p.SuitSheen);
            drawing.Light(head.Minus(FigureShape.Circle(0.10f, 2.58f, 0.22f)).Minus(shadow), p.Key);

            drawing.Rim(DecoMotifs.RimOnDark, p.Rim, DecoMotifs.RimFloor);

            // ── Ink: the major internal forms only ─────────────────────
            drawing.InkLine(FigureShape.Polyline(-0.66f, 1.98f, -0.70f, 1.02f).Symmetric());
            drawing.InkLine(FigureShape.Polyline(0.76f, 1.40f, 1.00f, 1.40f));
            drawing.InkLine(FigureShape.Polyline(0.76f, 1.16f, 1.00f, 1.16f));

            // The tell: the gauntlet's middle segment lights.
            drawing.Powered(FigureShape.Rect(0.78f, 1.20f, 0.97f, 1.36f), p.Powered);

            return drawing;
        }
    }
}
