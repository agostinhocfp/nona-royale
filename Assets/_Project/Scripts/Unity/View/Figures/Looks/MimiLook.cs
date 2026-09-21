// Assets/_Project/Scripts/Unity/View/Figures/Looks/MimiLook.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Mimi (ART_DIRECTION §5.1, row 7): near-black and small, with the
    /// brightest hardware on the board; the small dart.
    /// </summary>
    /// <remarks>
    /// <b>She reads by shape, not value</b> (§5.1's third standing rule). Her
    /// size comes from her health, like everyone's; this recipe supplies the
    /// rest: one narrow black shape, and a pale frosted cryo rig on her back
    /// and shoulders whose two emitters break the outline upward like the
    /// fins of a dart. At 64 px you see the rig, not her (ART_PROMPTS, Mimi).
    ///
    /// <b>No white V.</b> The harness straps run straight down, because a
    /// pale V on a black coat is Bouncer's value solution.
    /// </remarks>
    public static class MimiLook
    {
        public static readonly OperatorLook Look = new OperatorLook("Mimi", 1.20f, Draw);

        private static FigureDrawing Draw(LookBookPalette p)
        {
            // ── Silhouette: the small dart ─────────────────────────────
            // A narrow A-line coat-dress to the knee, narrow sleeves, black
            // leggings. Everything narrow; nothing with mass.
            var coat = FigureShape.Polygon(
                0.05f, 2.30f,
                -0.30f, 2.26f,     // shoulder
                -0.24f, 1.66f,     // waist
                -0.36f, 0.96f,     // hem, flared
                0.05f, 0.96f).Symmetric();

            // Held close: the sleeve overlaps the coat, so only its outer
            // edge takes the rim and the arm does not dissolve into two
            // lit lines at board scale.
            var arms = FigureShape.Polygon(
                -0.28f, 2.26f,
                -0.41f, 2.18f,
                -0.45f, 1.38f,     // wrist
                -0.30f, 1.36f,
                -0.26f, 1.90f).Symmetric();

            var hands = FigureShape.Ellipse(-0.40f, 1.30f, 0.055f, 0.08f).Symmetric();

            var legs = FigureShape.Polygon(
                -0.22f, 0.98f,
                -0.05f, 0.98f,
                -0.06f, 0.06f,
                -0.21f, 0.00f,
                -0.24f, 0.06f).Symmetric();

            var head = FigureShape.Ellipse(0f, 2.56f, 0.15f, 0.19f);
            var knot = FigureShape.Circle(0f, 2.80f, 0.07f);
            var neck = FigureShape.Rect(-0.06f, 2.28f, 0.06f, 2.42f);

            // The rig: a pack across the shoulder blades that shows past the
            // neck, caps over both shoulders, and two short emitters angled up
            // and out: the dart's fins.
            var pack = FigureShape.Polygon(
                -0.30f, 2.18f, 0.30f, 2.18f, 0.24f, 2.42f, -0.24f, 2.42f);
            var cap = FigureShape.Ellipse(-0.33f, 2.22f, 0.13f, 0.09f).Symmetric();
            var emitter = FigureShape.Polygon(
                -0.30f, 2.34f,
                -0.36f, 2.30f,
                -0.60f, 2.66f,
                -0.53f, 2.71f).Symmetric();
            var rig = FigureShape.Union(pack, cap, emitter);

            var silhouette = FigureShape.Union(coat, arms, hands, legs, head, knot, neck, rig);
            var drawing = new FigureDrawing(silhouette, p.MimiCoat, p.Ink, DecoMotifs.LineWeight);

            // ── Blocks, back to front ──────────────────────────────────
            drawing.Block(pack, p.MimiRig);
            drawing.Block(emitter, p.MimiSteel);

            // Frost-white tips on the emitters: the brightest points on her.
            var tips = FigureShape.Circle(-0.565f, 2.685f, 0.05f).Symmetric();
            drawing.Block(tips, p.MimiRig);

            // Her high black collar, in front of the pack.
            drawing.Block(FigureShape.Polygon(-0.10f, 2.26f, 0.10f, 2.26f, 0.08f, 2.44f, -0.08f, 2.44f), p.MimiCoat);

            // Pale face under hair scraped back into a hard knot.
            drawing.Block(head, p.MimiSkin);
            drawing.Block(head.And(FigureShape.HalfPlane(0f, 2.60f, 0f, -1f)).Or(knot), p.MimiCoat);
            drawing.Block(hands, p.MimiSkin);

            drawing.Block(cap, p.MimiRig);

            // Harness straps, straight down from the caps (never a V).
            var straps = FigureShape.Rect(-0.23f, 1.96f, -0.15f, 2.22f).Symmetric();
            drawing.Block(straps, p.MimiRig);

            // The coordinate plate on her left forearm.
            var plate = FigureShape.Polygon(0.35f, 1.50f, 0.45f, 1.51f, 0.45f, 1.68f, 0.35f, 1.66f);
            drawing.Block(plate, p.MimiRig);

            // The Deco furniture: a chevron band at the hem.
            drawing.Block(DecoMotifs.Chevron(1.16f, 0.30f, 0.10f, 0.035f).And(coat), p.PlateSheen);

            // ── Shade and light ────────────────────────────────────────
            var shadow = FigureShape.Union(
                DecoMotifs.ShadowSide(0.08f, 1.60f),
                DecoMotifs.Crescent(0f, 2.56f, 0.17f, -0.06f, 0.04f));
            drawing.Shade(shadow, p.Shade);

            var lit = FigureShape.Union(
                FigureShape.Polygon(-0.30f, 2.26f, -0.40f, 2.18f, -0.42f, 1.70f, -0.34f, 1.72f, -0.32f, 2.10f),
                FigureShape.Polygon(-0.24f, 2.20f, -0.14f, 2.20f, -0.14f, 1.66f, -0.22f, 1.66f));
            drawing.Light(lit.Minus(shadow).Minus(rig).Minus(straps), p.SuitSheen);

            // No rim below the hem: on legs this narrow the rim would be all
            // there is, and she would stand on two lit wires.
            drawing.Rim(DecoMotifs.RimOnDark, p.Rim, 1.0f);

            // ── Ink ────────────────────────────────────────────────────
            // Where the sleeves leave the coat.
            drawing.InkLine(FigureShape.Polyline(-0.30f, 2.12f, -0.33f, 1.40f).Symmetric());

            // The tell: charge lines along the emitters and the plate.
            drawing.Powered(FigureShape.Polyline(-0.33f, 2.34f, -0.55f, 2.66f).Symmetric().Offset(0.012f), p.Powered);
            drawing.Powered(plate.Offset(-0.02f), p.Powered);

            return drawing;
        }
    }
}
