// Assets/_Project/Scripts/Unity/View/Figures/Looks/JaviLook.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Javi (ART_DIRECTION §5.1, row 5): a dark waistcoat block and two white
    /// sleeves; the upright cross.
    /// </summary>
    /// <remarks>
    /// The most vertical figure on the board: a narrow body and legs close
    /// together, crossed by one horizontal bar made of his arms, held out and
    /// ready, and the canister bandolier across his chest. The charcoal
    /// waistcoat covers the whole torso, so the white stays in the sleeves and
    /// the collar (ratified 2026-09-19 to protect Luka's light torso).
    ///
    /// Canisters are matte steel with frosted seals, on aged-brass fittings;
    /// thin grey surgical gloves.
    /// </remarks>
    public static class JaviLook
    {
        public static readonly OperatorLook Look = new OperatorLook("Javi", 1.15f, Draw);

        private static FigureDrawing Draw(LookBookPalette p)
        {
            // ── Silhouette: the upright cross ──────────────────────────
            var torso = FigureShape.Polygon(
                0.05f, 2.30f,
                -0.30f, 2.24f,
                -0.26f, 1.14f,
                0.05f, 1.14f).Symmetric();

            // Arms held out level from the shoulder, forearms turned up a little.
            var arms = FigureShape.Polygon(
                -0.26f, 2.22f,
                -0.74f, 2.14f,
                -0.92f, 2.26f,
                -0.98f, 2.16f,
                -0.80f, 1.98f,
                -0.26f, 2.00f).Symmetric();

            var gloves = FigureShape.Ellipse(-0.96f, 2.27f, 0.07f, 0.09f).Symmetric();

            var legs = FigureShape.Polygon(
                -0.24f, 1.16f,
                -0.03f, 1.16f,
                -0.05f, 0.06f,
                -0.26f, 0.00f,
                -0.24f, 0.10f).Symmetric();

            var head = FigureShape.Ellipse(0f, 2.54f, 0.16f, 0.21f);

            var silhouette = FigureShape.Union(torso, arms, gloves, legs, head);
            var drawing = new FigureDrawing(silhouette, p.JaviWaistcoat, p.Ink, DecoMotifs.LineWeight);

            // ── Blocks ─────────────────────────────────────────────────
            drawing.Block(arms, p.Bone);
            drawing.Block(gloves, p.JaviGlove);
            drawing.Block(legs, p.Obsidian);
            drawing.Block(head, p.JaviSkin);
            drawing.Block(head.And(FigureShape.HalfPlane(0f, 2.60f, 0f, -1f)), p.JaviHair);

            // The white collar, the only white on the torso.
            drawing.Block(FigureShape.Polygon(-0.12f, 2.30f, 0.12f, 2.30f, 0.04f, 2.18f, -0.04f, 2.18f), p.Bone);

            // The bandolier: a brass strap across the chest, steel canisters
            // racked in order with frosted seals. Two are spent.
            var strap = FigureShape.Rect(-0.30f, 1.84f, 0.30f, 1.90f);
            drawing.Block(strap, p.Brass);
            for (int i = 0; i < 5; i++)
            {
                float x = -0.22f + i * 0.11f;
                var can = FigureShape.Rect(x - 0.035f, 1.74f, x + 0.035f, 1.98f);
                bool spent = i == 3;
                drawing.Block(can, spent ? p.Obsidian : p.JaviSteel);
                if (!spent) drawing.Block(FigureShape.Rect(x - 0.035f, 1.93f, x + 0.035f, 1.98f), p.JaviFrost);
            }

            // The Deco furniture: a chevron band at the waistcoat's hem.
            drawing.Block(DecoMotifs.Chevron(1.36f, 0.26f, 0.10f, 0.035f).And(torso), p.PlateSheen);

            // ── Shade and light ────────────────────────────────────────
            var shadow = FigureShape.Union(
                DecoMotifs.ShadowSide(0.08f, 1.60f),
                DecoMotifs.Crescent(0f, 2.54f, 0.18f, -0.06f, 0.05f),
                FigureShape.Rect(0.26f, 1.98f, 1.1f, 2.07f));
            drawing.Shade(shadow, p.Shade);

            var lit = FigureShape.Union(
                FigureShape.Polygon(-0.30f, 2.24f, -0.12f, 2.28f, -0.12f, 1.20f, -0.24f, 1.20f),
                DecoMotifs.Crescent(0f, 2.54f, 0.18f, 0.06f, -0.05f).And(FigureShape.HalfPlane(0f, 2.60f, 0f, 1f)));
            drawing.Light(lit.Minus(shadow), p.SuitSheen);

            // No rim on the legs: on trousers this narrow a rim down both
            // sides turns them into two lit wires at board scale.
            drawing.Rim(DecoMotifs.RimOnDark, p.Rim, 1.16f);

            // ── Ink ────────────────────────────────────────────────────
            // Where the sleeves meet the waistcoat, and the pockets.
            drawing.InkLine(FigureShape.Polyline(-0.28f, 2.22f, -0.28f, 2.00f).Symmetric());
            drawing.InkLine(FigureShape.Polyline(-0.22f, 1.52f, -0.08f, 1.52f).Symmetric());

            // The tell: a charge line on each canister.
            drawing.Powered(FigureShape.Rect(-0.24f, 1.80f, 0.24f, 1.82f), p.Powered);

            return drawing;
        }
    }
}
