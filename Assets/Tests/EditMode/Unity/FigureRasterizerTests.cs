// Assets/Tests/EditMode/Unity/FigureRasterizerTests.cs
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The look book's shape maths and rasteriser (OPERATOR_LOOKBOOK.md, LB0). The recipes' own rules are in <c>LookBookRulesTests</c>.
    /// Plain C# on both sides, so none of this touches a texture.
    /// </summary>
    [TestFixture]
    public class FigureRasterizerTests
    {
        private static readonly FigureColour Ink = FigureColour.Hex("1C0E12");
        private static readonly FigureColour Grey = FigureColour.Hex("808080");
        private static readonly FigureColour Cyan = FigureColour.Hex("5FE0E8");

        // ── Distances ───────────────────────────────────────────────────

        [Test]
        public void Polygon_IsNegativeInside_AndExactOutside()
        {
            var square = FigureShape.Polygon(0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f);

            Assert.AreEqual(-0.5f, square.Distance(0.5f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, square.Distance(2f, 0.5f), 1e-5f);
            Assert.AreEqual(0f, square.Distance(1f, 0.5f), 1e-5f);
        }

        [Test]
        public void Polygon_EitherWindingGivesTheSameShape()
        {
            var ccw = FigureShape.Polygon(0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f);
            var cw = FigureShape.Polygon(0f, 1f, 1f, 1f, 1f, 0f, 0f, 0f);

            Assert.AreEqual(ccw.Distance(0.3f, 0.6f), cw.Distance(0.3f, 0.6f), 1e-5f);
            Assert.AreEqual(ccw.Distance(-0.4f, 0.2f), cw.Distance(-0.4f, 0.2f), 1e-5f);
        }

        [Test]
        public void Symmetric_MirrorsAcrossTheCentreLine()
        {
            var left = FigureShape.Rect(-0.8f, 0f, -0.2f, 1f);
            var both = left.Symmetric();

            Assert.Less(both.Distance(0.5f, 0.5f), 0f);
            Assert.Less(both.Distance(-0.5f, 0.5f), 0f);
            Assert.Greater(both.Distance(0f, 0.5f), 0f);
            Assert.AreEqual(-0.8f, both.Bounds.MinX, 1e-5f);
            Assert.AreEqual(0.8f, both.Bounds.MaxX, 1e-5f);
        }

        [Test]
        public void Offset_GrowsByExactlyTheLineWeight()
        {
            var circle = FigureShape.Circle(0f, 0f, 1f);
            Assert.AreEqual(0f, circle.Offset(0.25f).Distance(1.25f, 0f), 1e-5f);
        }

        [Test]
        public void Union_SkippingFarChildren_DoesNotChangeTheAnswer()
        {
            var near = FigureShape.Circle(0f, 0f, 0.5f);
            var far = FigureShape.Circle(10f, 0f, 0.5f);
            var union = FigureShape.Union(near, far);

            Assert.AreEqual(near.Distance(0.9f, 0.1f), union.Distance(0.9f, 0.1f), 1e-6f);
            Assert.AreEqual(far.Distance(9.2f, 0f), union.Distance(9.2f, 0f), 1e-6f);
        }

        [Test]
        public void Rotate_TurnsAboutThePivot()
        {
            // A thin bar along +x, turned 90° about the origin, lies along +y.
            var bar = FigureShape.Rect(0f, -0.05f, 1f, 0.05f).Rotate(90f);

            Assert.Less(bar.Distance(0f, 0.5f), 0f);
            Assert.Greater(bar.Distance(0.5f, 0f), 0f);
        }

        // ── Rasterising ─────────────────────────────────────────────────

        [Test]
        public void Interior_IsOpaque_AndOutside_IsClear()
        {
            var image = Render(new FigureDrawing(FigureShape.Rect(-0.5f, 0f, 0.5f, 1f), Grey, Ink, 0f));

            Assert.AreEqual(255, image.Alpha(8, 8));
            Assert.AreEqual(0, image.Alpha(0, 8));
        }

        [Test]
        public void AnEdgeThroughATexel_GivesPartialCoverage()
        {
            // 16 texels per unit, canvas from x = −1: the edge at x = 0.5 + 1/32
            // cuts texel 24 in half.
            var image = Render(new FigureDrawing(FigureShape.Rect(-0.5f, 0f, 0.5f + 1f / 32f, 1f), Grey, Ink, 0f));

            Assert.AreEqual(128, image.Alpha(24, 8), 3);
        }

        [Test]
        public void TheSymmetricHalves_LeaveNoSeamOnTheCentreLine()
        {
            // Authored the look book's way: a left half that overlaps the centre.
            var half = FigureShape.Rect(-0.6f, 0f, 0.05f, 1f);
            var image = Render(new FigureDrawing(half.Symmetric(), Grey, Ink, 0f));

            for (int x = 14; x <= 17; x++)
                Assert.AreEqual(255, image.Alpha(x, 8), $"texel {x}");
        }

        [Test]
        public void TheInk_SurroundsTheSilhouette_ByOneLineWeight()
        {
            // Line weight 1/8 unit = 2 texels at 16 per unit.
            var image = Render(new FigureDrawing(FigureShape.Rect(-0.5f, 0.25f, 0.5f, 1f), Grey, Ink, 0.125f));

            // Texel 24 is x 0.5..0.5625: just outside the grey, inside the ink.
            int i = (8 * image.Width + 24) * 4;
            Assert.AreEqual(255, image.Pixels[i + 3]);
            Assert.AreEqual(0x1C, image.Pixels[i], 1);

            // Inside: grey.
            i = (8 * image.Width + 16) * 4;
            Assert.AreEqual(0x80, image.Pixels[i], 1);
        }

        [Test]
        public void OpaqueRows_AreMeasured()
        {
            // Canvas bottom −0.25 at 16 per unit: y 0.25..1 is rows 8..19.
            var image = Render(new FigureDrawing(FigureShape.Rect(-0.5f, 0.25f, 0.5f, 1f), Grey, Ink, 0f));

            Assert.AreEqual(8, image.OpaqueBottomRow);
            Assert.AreEqual(19, image.OpaqueTopRow);
        }

        [Test]
        public void ShadeAndLight_ChangeColourOnly_InsideTheSilhouette()
        {
            var drawing = new FigureDrawing(FigureShape.Rect(-0.5f, 0f, 0.5f, 1f), Grey, Ink, 0f)
                .Shade(FigureShape.HalfPlane(0f, 0f, -1f, 0f), FigureColour.Hex("7F7F7F"));

            var image = Render(drawing);

            // Left of centre untouched, right of centre multiplied by half.
            Assert.AreEqual(0x80, image.Pixels[(8 * image.Width + 12) * 4], 1);
            Assert.AreEqual(0x40, image.Pixels[(8 * image.Width + 20) * 4], 2);

            // The shade's half-plane runs off the figure; nothing is drawn there.
            Assert.AreEqual(0, image.Alpha(28, 8));
        }

        [Test]
        public void PoweredLayers_AreNeverDrawnAtRest()
        {
            var drawing = new FigureDrawing(FigureShape.Rect(-0.5f, 0f, 0.5f, 1f), Grey, Ink, 0f)
                .Powered(FigureShape.Rect(-0.25f, 0.25f, 0.25f, 0.75f), Cyan);

            Assert.IsFalse(HasCyan(FigureRasterizer.Render(drawing, SmallCanvas)));
            Assert.IsTrue(HasCyan(FigureRasterizer.Render(drawing, SmallCanvas, powered: true)));
        }

        [Test]
        public void Cropped_CutsTheFigureAtTheWaist()
        {
            var drawing = new FigureDrawing(FigureShape.Rect(-0.5f, 0f, 0.5f, 1f), Grey, Ink, 0f).Cropped(0.5f);
            var image = Render(drawing);

            Assert.AreEqual(0, image.Alpha(16, 6));      // y ≈ 0.16, below the cut
            Assert.AreEqual(255, image.Alpha(16, 16));   // y ≈ 0.78
        }

        // ── Helpers ─────────────────────────────────────────────────────

        /// <summary>x −1..1, y −0.25..1.75 at 16 texels per unit: 32×32.</summary>
        private static readonly FigureCanvas SmallCanvas = new FigureCanvas(-1f, -0.25f, 32, 32, 16f, 4);

        private static FigureImage Render(FigureDrawing drawing) => FigureRasterizer.Render(drawing, SmallCanvas);

        private static bool HasCyan(FigureImage image) => SilhouetteMetrics.Count(image, SilhouetteMetrics.IsCyan) > 0;
    }
}
