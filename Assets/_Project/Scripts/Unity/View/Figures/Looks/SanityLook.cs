// Assets/_Project/Scripts/Unity/View/Figures/Looks/SanityLook.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Sanity (ART_DIRECTION §5.1, row 6): a large mid-brown mass; the octagon.
    /// </summary>
    /// <remarks>
    /// "At 64 px he should be a dark block with a brass line across it"
    /// (ART_PROMPTS, Sanity). One heavy octagon from the shoulders to the
    /// boots, legs almost gone under the apron, a small head sunk into it:
    /// that is what keeps him apart from Bouncer's slab (square, with legs)
    /// and Nuetu's disc (round, with legs).
    ///
    /// The umber apron is the big shape and stays dark, never tan (Luka's
    /// value). The charcoal livery shows at the shoulders; the tool roll is
    /// the brass line; the prod hangs at his right hip.
    /// </remarks>
    public static class SanityLook
    {
        public static readonly OperatorLook Look = new OperatorLook("Sanity", 0.95f, Draw);

        private static FigureDrawing Draw(LookBookPalette p)
        {
            // ── Silhouette: the octagon ────────────────────────────────
            // Chamfered hard at all four corners, so it reads as an octagon
            // and not as Bouncer's square slab at 64 px.
            var mass = FigureShape.Polygon(
                0.05f, 2.32f,
                -0.40f, 2.32f,
                -0.96f, 1.76f,
                -0.96f, 1.02f,
                -0.44f, 0.46f,
                0.05f, 0.46f).Symmetric();

            // Boots: just the toes of them, under the apron.
            var boots = FigureShape.Polygon(-0.40f, 0.50f, -0.10f, 0.50f, -0.10f, 0.00f, -0.46f, 0.00f).Symmetric();

            var head = FigureShape.Ellipse(0f, 2.46f, 0.20f, 0.22f);

            // The prod at his right hip, hanging below the mass's corner.
            var prod = FigureShape.Polygon(0.80f, 1.02f, 0.88f, 1.02f, 0.90f, 0.34f, 0.82f, 0.34f);

            var silhouette = FigureShape.Union(mass, boots, head, prod);
            var drawing = new FigureDrawing(silhouette, p.SanityApron, p.Ink, DecoMotifs.LineWeight);

            // ── Blocks ─────────────────────────────────────────────────
            // The charcoal livery: shoulders and arms either side of the bib.
            var livery = mass.And(FigureShape.HalfPlane(0f, 1.62f, 0f, -1f))
                .Minus(FigureShape.Polygon(-0.34f, 2.30f, 0.34f, 2.30f, 0.44f, 1.60f, -0.44f, 1.60f));
            drawing.Block(livery, p.SanityLivery);

            // Bare forearms down the sides of the mass, and the hands.
            var forearms = FigureShape.Polygon(-0.96f, 1.62f, -0.78f, 1.62f, -0.76f, 1.00f, -0.96f, 1.00f).Symmetric().And(mass);
            drawing.Block(forearms, p.SanitySkin);

            drawing.Block(boots, p.Obsidian);
            drawing.Block(head, p.SanitySkin);

            // The tool roll: the brass line across him, with two buckles.
            var roll = FigureShape.Polygon(-0.92f, 1.86f, 0.92f, 1.66f, 0.92f, 1.56f, -0.92f, 1.76f).And(mass);
            drawing.Block(roll, p.Brass);
            drawing.Block(FigureShape.Rect(-0.46f, 1.72f, -0.36f, 1.86f).Or(FigureShape.Rect(0.36f, 1.62f, 0.46f, 1.76f)), p.Obsidian);

            // The prod: steel with a brass grip.
            drawing.Block(prod, p.SanitySteel);
            drawing.Block(FigureShape.Rect(0.80f, 0.86f, 0.90f, 1.02f), p.Brass);

            // The Deco furniture: stepped hem across the apron.
            drawing.Block(DecoMotifs.Chevron(0.84f, 0.56f, 0.12f, 0.04f).And(mass), p.SanityLivery);

            // ── Shade and light ────────────────────────────────────────
            var shadow = FigureShape.Union(
                DecoMotifs.ShadowSide(0.34f, 1.40f),
                DecoMotifs.Crescent(0f, 2.46f, 0.22f, -0.08f, 0.05f));
            drawing.Shade(shadow, p.Shade);

            var lit = FigureShape.Union(
                FigureShape.Polygon(-0.40f, 2.32f, -0.96f, 1.76f, -0.96f, 1.60f, -0.40f, 2.16f, 0.10f, 2.18f, 0.10f, 2.32f),
                FigureShape.Polygon(-0.40f, 1.60f, -0.26f, 1.60f, -0.26f, 0.50f, -0.40f, 0.52f));
            drawing.Light(lit.Minus(shadow).Minus(roll), p.SuitSheen);
            drawing.Light(DecoMotifs.Crescent(0f, 2.46f, 0.22f, 0.08f, -0.06f).Minus(shadow), p.Key);

            drawing.Rim(DecoMotifs.RimOnDark, p.Rim, DecoMotifs.RimFloor);

            // ── Ink ────────────────────────────────────────────────────
            drawing.InkLine(FigureShape.Polyline(-0.78f, 1.62f, -0.76f, 1.00f).Symmetric());

            // The tell: the arc across the prod's head.
            drawing.Powered(FigureShape.Rect(0.80f, 0.36f, 0.90f, 0.44f), p.Powered);

            return drawing;
        }
    }
}
