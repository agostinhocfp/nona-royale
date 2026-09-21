// Assets/_Project/Scripts/Unity/View/Figures/Looks/NuetuLook.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Nuetu (ART_DIRECTION §5.1, row 11): the only all-light mass, cool
    /// dove-grey, black plates on grey; the disc, the one silhouette with no
    /// points at all.
    /// </summary>
    /// <remarks>
    /// Promoted from the LB0 sketch. His shaved round head replaces the
    /// sketch's visor, a tarnished bio-link collar sits at the throat, and the
    /// two coaster-sized discs at his belt are his piece shape (ART_PROMPTS,
    /// Nuetu: "lock it before someone changes his silhouette").
    /// </remarks>
    public static class NuetuLook
    {
        public static readonly OperatorLook Look = new OperatorLook("Nuetu", 0.95f, Draw);

        private static FigureDrawing Draw(LookBookPalette p)
        {
            // ── Silhouette: the disc ───────────────────────────────────
            var disc = FigureShape.Circle(0f, 1.52f, 0.84f);
            var head = FigureShape.Circle(0f, 2.46f, 0.22f);
            // Short thick legs set well inside the disc, so the round mass is
            // the whole read and the outline stays apart from Bouncer's slab.
            var legs = FigureShape.Polygon(
                -0.40f, 0.84f, -0.13f, 0.84f, -0.14f, 0.00f, -0.43f, 0.00f).Symmetric();

            var silhouette = FigureShape.Union(disc, head, legs);
            var drawing = new FigureDrawing(silhouette, p.NuetuGrey, p.Ink, DecoMotifs.LineWeight);

            // ── Head and collar ────────────────────────────────────────
            drawing.Block(head, p.NuetuSkin);
            var collar = FigureShape.Rect(-0.15f, 2.22f, 0.15f, 2.29f).And(head.Offset(0.08f));
            drawing.Block(collar, p.Brass);

            // ── Black plates on grey ───────────────────────────────────
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
            var fan = DecoMotifs.Fan(0f, 1.08f, 5, 60f, 0.80f, 0.028f).And(plate.Offset(-0.045f));
            drawing.Block(fan, p.NuetuGrey);

            // Belt, and the two discs that are his piece shape.
            drawing.Block(FigureShape.Rect(-0.46f, 0.84f, 0.46f, 0.96f).And(disc), p.NuetuPlate);
            var discs = FigureShape.Circle(-0.52f, 0.98f, 0.10f).Symmetric().And(disc);
            drawing.Block(discs, p.NuetuPlate);

            // ── Shade and light ────────────────────────────────────────
            // The cel sphere: an offset circle carves a crescent of shadow
            // on the lower right and a crescent of light on the upper left.
            var shadow = FigureShape.Union(
                disc.Minus(FigureShape.Circle(-0.16f, 1.66f, 0.84f)),
                DecoMotifs.Crescent(0f, 2.46f, 0.22f, -0.07f, 0.05f),
                FigureShape.Rect(-0.26f, 0f, -0.12f, 0.84f),
                FigureShape.Rect(0.28f, 0f, 0.44f, 0.84f));
            drawing.Shade(shadow, p.Shade);

            var plates = FigureShape.Union(pauldron, plate, discs);
            var lit = FigureShape.Union(
                disc.Minus(FigureShape.Circle(0.12f, 1.40f, 0.84f)),
                DecoMotifs.Crescent(0f, 2.46f, 0.22f, 0.07f, -0.06f));
            drawing.Light(lit.Minus(shadow).Minus(plates).Minus(collar), p.Key);

            // A hard specular wedge on each black plate.
            drawing.Light(FigureShape.Circle(-0.70f, 2.10f, 0.20f).And(pauldron).Minus(shadow), p.PlateSheen);
            drawing.Light(FigureShape.Polygon(-0.26f, 1.94f, -0.12f, 1.94f, -0.12f, 1.60f, -0.26f, 1.52f).And(plate), p.PlateSheen);

            drawing.Rim(DecoMotifs.RimOnLight, p.RimOnLight, DecoMotifs.RimFloor);

            // Where the legs leave the disc.
            drawing.InkLine(FigureShape.Polyline(-0.40f, 0.84f, -0.13f, 0.84f).Symmetric());

            // The tell: charge seams light along the plates.
            drawing.Powered(FigureShape.Rect(-0.012f, 1.10f, 0.012f, 1.90f), p.Powered);

            return drawing;
        }
    }
}
