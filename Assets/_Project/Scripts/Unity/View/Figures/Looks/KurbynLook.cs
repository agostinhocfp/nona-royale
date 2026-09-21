// Assets/_Project/Scripts/Unity/View/Figures/Looks/KurbynLook.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Kurbyn, DarkGrave (ART_DIRECTION §5.1, row 4): mid-dark, broken by bare
    /// forearms; the coiled four-point.
    /// </summary>
    /// <remarks>
    /// A crouched spring: shoulders rolled up and forward, elbows out, fists
    /// low, feet planted wide. The four points are the two elbows and the two
    /// feet, and the gaps between his arms and body are part of the shape.
    /// ART_PROMPTS asks that he read wider and lower than Luka's forward
    /// blade; he is.
    ///
    /// Charcoal waistcoat and shirt, the tie pulled loose, aged-brass braces.
    /// The bare pale forearms are his only light and his value break: he
    /// never gets gloves. The neural rig sits at the nape, out of sight from
    /// the front, so only its tell shows (a filament at the neck).
    /// </remarks>
    public static class KurbynLook
    {
        public static readonly OperatorLook Look = new OperatorLook("Kurbyn", 1.05f, Draw);

        private static FigureDrawing Draw(LookBookPalette p)
        {
            // ── Silhouette: the coiled four-point ──────────────────────
            var torso = FigureShape.Polygon(
                0.05f, 2.28f,
                -0.20f, 2.28f,
                -0.62f, 2.14f,     // rolled shoulder
                -0.50f, 1.66f,
                -0.30f, 1.06f,     // narrow hip
                0.05f, 1.06f).Symmetric();

            var upperArm = FigureShape.Polygon(
                -0.62f, 2.14f,
                -0.46f, 2.02f,
                -0.72f, 1.62f,
                -0.88f, 1.70f).Symmetric();

            var forearm = FigureShape.Polygon(
                -0.88f, 1.70f,
                -0.72f, 1.62f,
                -0.50f, 1.26f,
                -0.62f, 1.18f).Symmetric();

            var fists = FigureShape.Ellipse(-0.54f, 1.18f, 0.11f, 0.10f).Symmetric();

            // A wide, planted stance: thighs angle out from the hip, knees
            // bent outward over the feet, shins near vertical. The knees
            // never point at each other (designer, 2026-09-21).
            var legs = FigureShape.Polygon(
                -0.30f, 1.10f,     // hip
                -0.04f, 1.04f,     // crotch
                -0.36f, 0.58f,     // inside of the knee
                -0.44f, 0.12f,     // inside of the ankle
                -0.38f, 0.00f,
                -0.78f, 0.00f,     // shoe
                -0.66f, 0.10f,
                -0.62f, 0.58f).Symmetric();   // outside of the knee

            var head = FigureShape.Ellipse(0f, 2.42f, 0.17f, 0.21f);

            var silhouette = FigureShape.Union(torso, upperArm, forearm, fists, legs, head);
            var drawing = new FigureDrawing(silhouette, p.KurbynCloth, p.Ink, DecoMotifs.LineWeight);

            // ── Blocks ─────────────────────────────────────────────────
            drawing.Block(legs, p.Obsidian);
            drawing.Block(forearm.Or(fists), p.KurbynSkin);
            drawing.Block(head, p.KurbynSkin);

            // Hair pushed back: one solid black shape over the crown.
            drawing.Block(head.And(FigureShape.HalfPlane(0f, 2.47f, 0f, -1f)), p.Obsidian);

            // The waistcoat, open: two panels either side of the shirt.
            var waistcoat = FigureShape.Polygon(-0.14f, 2.22f, -0.48f, 2.06f, -0.30f, 1.08f, -0.06f, 1.08f).Symmetric()
                .Minus(FigureShape.Polygon(-0.08f, 2.26f, 0.08f, 2.26f, 0.05f, 1.06f, -0.05f, 1.06f));
            drawing.Block(waistcoat, p.Obsidian);

            // Brass braces down the shirt, and the loose tie.
            drawing.Block(FigureShape.Rect(-0.26f, 1.10f, -0.21f, 2.12f).Symmetric().Minus(waistcoat), p.Brass);
            drawing.Block(FigureShape.Polygon(-0.04f, 2.20f, 0.05f, 2.18f, 0.10f, 1.60f, 0.02f, 1.56f), p.Obsidian);

            // ── Shade and light ────────────────────────────────────────
            var shadow = FigureShape.Union(
                DecoMotifs.ShadowSide(0.10f, 1.60f),
                DecoMotifs.Crescent(0f, 2.42f, 0.19f, -0.07f, 0.05f));
            drawing.Shade(shadow, p.Shade);

            var lit = FigureShape.Union(
                FigureShape.Polygon(-0.20f, 2.28f, -0.62f, 2.14f, -0.88f, 1.70f, -0.78f, 1.66f, -0.56f, 2.02f, -0.20f, 2.16f));
            drawing.Light(lit.Minus(shadow).Minus(forearm), p.SuitSheen);
            drawing.Light(DecoMotifs.Crescent(0f, 2.42f, 0.19f, 0.07f, -0.05f).Minus(shadow), p.Key);

            // No rim on the legs: on trousers this narrow a rim down both
            // sides turns them into two lit wires at board scale.
            drawing.Rim(DecoMotifs.RimOnDark, p.Rim, 1.06f);

            // ── Ink ────────────────────────────────────────────────────
            drawing.InkLine(FigureShape.Polyline(-0.72f, 1.62f, -0.88f, 1.70f).Symmetric());

            // The tell: the nape filaments at the neck.
            drawing.Powered(FigureShape.Rect(0.10f, 2.20f, 0.16f, 2.34f), p.Powered);

            return drawing;
        }
    }
}
