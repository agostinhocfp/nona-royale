// Assets/_Project/Scripts/Unity/View/Figures/Looks/KianLook.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Kian (ART_DIRECTION §5.1, row 10): the only green torso; the five-point
    /// spiked crown, the only outline that breaks upward.
    /// </summary>
    /// <remarks>
    /// Four emitter rods rise from the rack on his back, two close and two
    /// splayed, and his head is the fifth point. Round-shouldered and narrow,
    /// no mass at all. The deep emerald smoking jacket with its shawl collar
    /// and tarnished frogging is the only green on the board; black shirt to
    /// the throat, the flat disrupter disc at his sternum.
    ///
    /// The rod tips are dark at rest and light only with the tell.
    /// </remarks>
    public static class KianLook
    {
        public static readonly OperatorLook Look = new OperatorLook("Kian", 1.25f, Draw);

        private static FigureDrawing Draw(LookBookPalette p)
        {
            // ── Silhouette: the spiked crown ───────────────────────────
            var jacket = FigureShape.Polygon(
                0.05f, 2.12f,
                -0.18f, 2.12f,
                -0.46f, 2.02f,     // round shoulder
                -0.50f, 1.86f,
                -0.34f, 1.10f,
                0.05f, 1.10f).Symmetric();

            var arms = FigureShape.Polygon(-0.46f, 2.02f, -0.50f, 1.86f, -0.50f, 1.24f, -0.38f, 1.24f, -0.34f, 1.80f).Symmetric();
            var hands = FigureShape.Ellipse(-0.44f, 1.18f, 0.06f, 0.08f).Symmetric();

            var legs = FigureShape.Polygon(
                -0.26f, 1.12f,
                -0.04f, 1.12f,
                -0.06f, 0.06f,
                -0.27f, 0.00f,
                -0.25f, 0.10f).Symmetric();

            // Head slightly down, carried forward.
            var head = FigureShape.Ellipse(0f, 2.34f, 0.15f, 0.20f);

            // The rack's rods: an inner pair close to the head, an outer pair splayed.
            var inner = FigureShape.Polygon(-0.24f, 2.08f, -0.18f, 2.08f, -0.24f, 2.86f, -0.29f, 2.85f).Symmetric();
            var outer = FigureShape.Polygon(-0.42f, 2.04f, -0.36f, 2.06f, -0.66f, 2.72f, -0.71f, 2.69f).Symmetric();
            var tipsInner = FigureShape.Circle(-0.265f, 2.87f, 0.045f).Symmetric();
            var tipsOuter = FigureShape.Circle(-0.69f, 2.72f, 0.045f).Symmetric();
            var rods = FigureShape.Union(inner, outer, tipsInner, tipsOuter);

            var silhouette = FigureShape.Union(jacket, arms, hands, legs, head, rods);
            var drawing = new FigureDrawing(silhouette, p.KianJacket, p.Ink, DecoMotifs.LineWeight);

            // ── Blocks ─────────────────────────────────────────────────
            drawing.Block(rods, p.KianShirt);
            drawing.Block(legs, p.KianShirt);
            drawing.Block(hands, p.KianShirt);
            drawing.Block(head, p.KianSkin);
            drawing.Block(head.And(FigureShape.HalfPlane(0f, 2.44f, 0f, -1f)), p.KianShirt);

            // Round tinted spectacles, rimmed in brass.
            drawing.Block(FigureShape.Circle(-0.06f, 2.33f, 0.045f).Symmetric(), p.Brass);
            drawing.Block(FigureShape.Circle(-0.06f, 2.33f, 0.028f).Symmetric(), p.KianShirt);

            // The black shirt to the throat between the shawl collar's arcs.
            var shirt = FigureShape.Polygon(-0.12f, 2.12f, 0.12f, 2.12f, 0.06f, 1.60f, -0.06f, 1.60f);
            drawing.Block(shirt, p.KianShirt);

            // The disrupter disc at the sternum.
            drawing.Block(FigureShape.Circle(0f, 1.86f, 0.07f), p.Obsidian);

            // Tarnished frogging: three short brass bars across the front.
            for (int i = 0; i < 3; i++)
            {
                float y = 1.52f - i * 0.12f;
                drawing.Block(FigureShape.Rect(-0.16f, y, 0.16f, y + 0.03f), p.Brass);
            }

            // ── Shade and light ────────────────────────────────────────
            var shadow = FigureShape.Union(
                DecoMotifs.ShadowSide(0.08f, 1.60f),
                DecoMotifs.Crescent(0f, 2.34f, 0.17f, -0.06f, 0.05f));
            drawing.Shade(shadow, p.Shade);

            // The shawl collar's lit arc, and the lit plane of the left sleeve.
            var lit = FigureShape.Union(
                FigureShape.Polygon(-0.18f, 2.12f, -0.12f, 2.12f, -0.06f, 1.60f, -0.14f, 1.56f, -0.24f, 1.90f),
                FigureShape.Polygon(-0.46f, 2.02f, -0.50f, 1.86f, -0.50f, 1.30f, -0.44f, 1.30f, -0.42f, 1.92f));
            // The warm key, not the cool sheen: screened cool, emerald drifts
            // into the cyan register, which is the tech's alone (§3).
            drawing.Light(lit.Minus(shadow), p.Key);

            // No rim on the legs: on trousers this narrow a rim down both
            // sides turns them into two lit wires at board scale.
            drawing.Rim(DecoMotifs.RimOnDark, p.Rim, 1.12f);

            // ── Ink ────────────────────────────────────────────────────
            drawing.InkLine(FigureShape.Polyline(-0.34f, 1.80f, -0.38f, 1.26f).Symmetric());

            // The tell: the rod tips light.
            drawing.Powered(tipsInner.Or(tipsOuter), p.Powered);

            return drawing;
        }
    }
}
