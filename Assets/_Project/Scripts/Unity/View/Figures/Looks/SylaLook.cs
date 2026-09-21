// Assets/_Project/Scripts/Unity/View/Figures/Looks/SylaLook.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Syla, The Blood Hound (ART_DIRECTION §5.1, row 2): a light core in a
    /// dark frame; the downward triangle.
    /// </summary>
    /// <remarks>
    /// <b>No cape on the board figure</b> (designer, 2026-09-21). Drawn at
    /// board scale the cape swallowed her and read as a hooded shape. It is
    /// dropped from this recipe only: her character, her portrait and the
    /// generator briefs (ART_PROMPTS) keep it.
    ///
    /// Without it, the frame is carried by the rest of her black: the drone
    /// housings on her shoulders, which give the triangle its wide top, the
    /// long opera gloves down both sides of the gown, the hip cradles and the
    /// hard bob. The bone-ivory column gown is the core and narrows to the
    /// ankle, so the figure still tapers from the shoulders to the feet. No
    /// gold anywhere on her.
    /// </remarks>
    public static class SylaLook
    {
        public static readonly OperatorLook Look = new OperatorLook("Syla", 1.30f, Draw);

        private static FigureDrawing Draw(LookBookPalette p)
        {
            // ── Silhouette: the downward triangle ──────────────────────
            // The gown: a column from a straight neckline, full at the hip,
            // narrowing to the ankle.
            var gown = FigureShape.Polygon(
                0.05f, 2.18f,
                -0.28f, 2.18f,
                -0.22f, 1.70f,     // waist
                -0.28f, 1.24f,     // hip
                -0.20f, 0.40f,
                -0.14f, 0.00f,
                0.05f, 0.00f).Symmetric();

            // Pale shoulders and throat above the neckline.
            var shoulders = FigureShape.Polygon(
                0.05f, 2.36f,
                -0.08f, 2.36f,
                -0.42f, 2.28f,
                -0.44f, 2.16f,
                0.05f, 2.14f).Symmetric();

            // Arms in black opera gloves, held close down both sides of the
            // gown so only their outer edge takes the rim.
            var arms = FigureShape.Polygon(
                -0.36f, 2.28f,
                -0.46f, 2.20f,
                -0.48f, 1.30f,     // wrist
                -0.32f, 1.28f,
                -0.26f, 2.10f).Symmetric();
            var hands = FigureShape.Ellipse(-0.42f, 1.20f, 0.07f, 0.10f).Symmetric();

            // The drones on her shoulders: the triangle's wide top.
            // Stepped wedges that slope down onto the arms, so the shoulder
            // line is the widest, hardest edge on her and everything below
            // it narrows.
            var drones = FigureShape.Polygon(
                -0.80f, 2.42f,
                -0.34f, 2.44f,
                -0.34f, 2.22f,
                -0.50f, 2.10f,
                -0.62f, 2.20f,
                -0.80f, 2.28f).Symmetric();
            var cradles = FigureShape.Rect(-0.40f, 1.36f, -0.28f, 1.60f).Symmetric();

            var head = FigureShape.Ellipse(0f, 2.56f, 0.16f, 0.20f);
            // The hard bob: a black mass cut straight at the jaw, the face
            // set into it.
            var bob = FigureShape.Ellipse(0f, 2.60f, 0.20f, 0.20f).And(FigureShape.HalfPlane(0f, 2.44f, 0f, -1f));
            var face = FigureShape.Ellipse(0f, 2.52f, 0.12f, 0.16f);
            var neck = FigureShape.Rect(-0.06f, 2.30f, 0.06f, 2.42f);

            var silhouette = FigureShape.Union(gown, shoulders, arms, hands, drones, cradles, head, bob, neck);
            var drawing = new FigureDrawing(silhouette, p.SylaGown, p.Ink, DecoMotifs.LineWeight);

            // ── Blocks ─────────────────────────────────────────────────
            drawing.Block(shoulders.Or(neck).Or(head), p.SylaSkin);
            drawing.Block(bob.Minus(face.And(FigureShape.HalfPlane(0f, 2.66f, 0f, 1f))), p.SylaCape);
            drawing.Block(arms.Or(hands), p.SylaCape);
            drawing.Block(drones.Or(cradles), p.Obsidian);

            // The Deco furniture: a black chevron band low on the gown,
            // closing the frame at the bottom.
            drawing.Block(DecoMotifs.Chevron(0.62f, 0.26f, 0.10f, 0.04f).And(gown), p.SylaCape);

            // ── Shade and light ────────────────────────────────────────
            var shadow = FigureShape.Union(
                DecoMotifs.ShadowSide(0.06f, 1.60f),
                DecoMotifs.Crescent(0f, 2.56f, 0.18f, -0.07f, 0.04f));
            drawing.Shade(shadow, p.Shade);

            // The lit left plane of the gown, and the drones' top faces.
            var lit = FigureShape.Union(
                FigureShape.Polygon(-0.26f, 2.14f, -0.14f, 2.14f, -0.12f, 0.04f, -0.18f, 0.04f, -0.26f, 0.60f, -0.28f, 1.24f, -0.20f, 1.70f),
                FigureShape.Polygon(-0.80f, 2.42f, -0.34f, 2.44f, -0.34f, 2.37f, -0.80f, 2.35f));
            drawing.Light(lit.Minus(shadow), p.SuitSheen);

            drawing.Rim(DecoMotifs.RimOnDark, p.Rim, DecoMotifs.RimFloor);

            // ── Ink ────────────────────────────────────────────────────
            drawing.InkLine(FigureShape.Polyline(-0.28f, 2.16f, 0.28f, 2.16f));

            // The tell: one slit in each drone housing.
            drawing.Powered(FigureShape.Rect(-0.72f, 2.30f, -0.42f, 2.33f).Symmetric(), p.Powered);

            return drawing;
        }
    }
}
