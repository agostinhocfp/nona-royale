// Assets/_Project/Scripts/Unity/View/Figures/Looks/RevuLook.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Revú (ART_DIRECTION §5.1, row 9): the only red torso; the three-point
    /// barbed hook.
    /// </summary>
    /// <remarks>
    /// Tall, thin, all length: sharp peaked shoulders either side of a long
    /// neck make the three points, and the ledger case hanging from his left
    /// hand on its brass chain is the barb, pulling the outline down and out
    /// on one side. The gaps under the arms make the shape concave, so it can
    /// never read as Syla's filled triangle.
    ///
    /// Oxblood darker than the blood-velvet carpet (§3, §6.1: a Play Mode
    /// check), a narrow bone shirt front rather than a V, brass at the cuffs.
    /// </remarks>
    public static class RevuLook
    {
        public static readonly OperatorLook Look = new OperatorLook("Revú", 1.30f, Draw);

        private static FigureDrawing Draw(LookBookPalette p)
        {
            // ── Silhouette: the barbed hook ────────────────────────────
            var jacket = FigureShape.Polygon(
                0.05f, 2.24f,
                -0.12f, 2.24f,
                -0.60f, 2.32f,     // peaked shoulder
                -0.44f, 2.10f,
                -0.30f, 1.20f,     // tails
                0.05f, 1.16f).Symmetric();

            // Right arm (viewer's left) bent, hand at the lapel.
            var armRight = FigureShape.Polygon(-0.56f, 2.24f, -0.44f, 2.10f, -0.40f, 1.62f, -0.12f, 1.78f, -0.14f, 1.66f, -0.50f, 1.50f, -0.58f, 1.64f);

            // Left arm (viewer's right) hanging long and away, holding the chain.
            var armLeft = FigureShape.Polygon(0.56f, 2.24f, 0.44f, 2.10f, 0.58f, 1.40f, 0.70f, 1.42f);
            var chain = FigureShape.Polyline(0.66f, 1.34f, 0.74f, 1.00f).Offset(0.012f);
            var ledger = FigureShape.Polygon(0.62f, 1.02f, 0.88f, 1.00f, 0.90f, 0.62f, 0.64f, 0.64f);

            var hands = FigureShape.Ellipse(-0.13f, 1.72f, 0.06f, 0.08f).Or(FigureShape.Ellipse(0.65f, 1.36f, 0.06f, 0.08f));

            var legs = FigureShape.Polygon(
                -0.24f, 1.20f,
                -0.03f, 1.20f,
                -0.05f, 0.06f,
                -0.24f, 0.00f,
                -0.22f, 0.10f).Symmetric();

            var neck = FigureShape.Rect(-0.07f, 2.20f, 0.07f, 2.46f);
            var head = FigureShape.Ellipse(0f, 2.66f, 0.14f, 0.21f);

            var silhouette = FigureShape.Union(jacket, armRight, armLeft, chain, ledger, hands, legs, neck, head);
            var drawing = new FigureDrawing(silhouette, p.RevuJacket, p.Ink, DecoMotifs.LineWeight);

            // ── Blocks ─────────────────────────────────────────────────
            drawing.Block(legs, p.RevuTrousers);
            drawing.Block(neck.Or(head), p.RevuSkin);
            drawing.Block(head.And(FigureShape.HalfPlane(0f, 2.70f, 0f, -1f)), p.RevuHair);

            // A narrow shirt front and a black tie: a sliver, never a V.
            drawing.Block(FigureShape.Polygon(-0.07f, 2.24f, 0.07f, 2.24f, 0.05f, 1.86f, -0.05f, 1.86f), p.Bone);
            drawing.Block(FigureShape.Polygon(-0.02f, 2.22f, 0.02f, 2.22f, 0.03f, 1.90f, -0.03f, 1.90f), p.Obsidian);

            drawing.Block(hands, p.Obsidian);
            drawing.Block(chain, p.Brass);
            drawing.Block(ledger, p.Obsidian);

            // Brass cuffs.
            drawing.Block(FigureShape.Polygon(-0.20f, 1.64f, -0.14f, 1.66f, -0.12f, 1.78f, -0.18f, 1.77f), p.Brass);
            drawing.Block(FigureShape.Rect(0.57f, 1.40f, 0.70f, 1.46f), p.Brass);

            // ── Shade and light ────────────────────────────────────────
            var shadow = FigureShape.Union(
                DecoMotifs.ShadowSide(0.06f, 1.70f),
                DecoMotifs.Crescent(0f, 2.66f, 0.16f, -0.06f, 0.05f));
            drawing.Shade(shadow, p.Shade);

            // The lit lapel and shoulder, peaked; the lit plane of the ledger.
            var lit = FigureShape.Union(
                FigureShape.Polygon(-0.12f, 2.24f, -0.60f, 2.32f, -0.44f, 2.16f, -0.08f, 1.84f),
                FigureShape.Polygon(0.62f, 1.02f, 0.88f, 1.00f, 0.88f, 0.94f, 0.62f, 0.96f));
            drawing.Light(lit.Minus(FigureShape.Rect(-0.07f, 1.8f, 0.07f, 2.3f)), p.Key);

            // No rim on the legs: on trousers this narrow a rim down both
            // sides turns them into two lit wires at board scale.
            drawing.Rim(DecoMotifs.RimOnDark, p.Rim, 1.20f);

            // ── Ink ────────────────────────────────────────────────────
            // The peaked lapels.
            drawing.InkLine(FigureShape.Polyline(-0.08f, 2.22f, -0.30f, 2.12f, -0.16f, 1.80f).Symmetric());

            // The tell: the line down the ledger's spine.
            drawing.Powered(FigureShape.Rect(0.75f, 0.66f, 0.77f, 0.98f), p.Powered);

            return drawing;
        }
    }
}
