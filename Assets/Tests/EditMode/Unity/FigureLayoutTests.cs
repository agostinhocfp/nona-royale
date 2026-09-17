// Assets/Tests/EditMode/Unity/FigureLayoutTests.cs
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.View
{
    [TestFixture]
    public class FigureLayoutTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void Pawn_BarAndHead_FollowTheGapAndDropRules()
        {
            var pawn = FigureLayout.Pawn;
            Assert.AreEqual(pawn.Top + FigureLayout.BarGap, pawn.BarY, Tolerance);
            Assert.AreEqual(pawn.Top - FigureLayout.HeadDrop, pawn.HeadY, Tolerance);
            Assert.AreEqual(1f, pawn.ArtScale);
            Assert.AreEqual(0f, pawn.ArtY);
        }

        [Test]
        public void Fit_PutsTheLowestOpaqueRowOnTheFrameFeet()
        {
            // A 768-unit-tall render with its pivot at the bottom and 40 units of empty floor.
            var fit = FigureLayout.Fit(40f, 740f, FigureLayout.Pawn, 1f, FigureLayout.StandingChest);

            float feetDrawn = fit.ArtY + 40f * fit.ArtScale;
            float topDrawn = fit.ArtY + 740f * fit.ArtScale;

            Assert.AreEqual(FigureLayout.Pawn.Feet, feetDrawn, Tolerance);
            Assert.AreEqual(FigureLayout.Pawn.Top, topDrawn, Tolerance);
        }

        [Test]
        public void Fit_IsIndependentOfThePivot()
        {
            // The same figure measured from a bottom pivot and from a centre pivot.
            var bottom = FigureLayout.Fit(0f, 1.4f, FigureLayout.Pawn, 1f, 0.7f);
            var centre = FigureLayout.Fit(-0.75f, 0.65f, FigureLayout.Pawn, 1f, 0.7f);

            Assert.AreEqual(bottom.ArtScale, centre.ArtScale, Tolerance);
            Assert.AreEqual(bottom.ArtY + 0f * bottom.ArtScale, centre.ArtY + (-0.75f) * centre.ArtScale, Tolerance);
            Assert.AreEqual(bottom.Top, centre.Top, Tolerance);
        }

        [Test]
        public void Fit_ScalesAnyRenderToTheFrameHeight()
        {
            var small = FigureLayout.Fit(0f, 1f, FigureLayout.Pawn, 1f, 0.7f);
            var large = FigureLayout.Fit(0f, 4f, FigureLayout.Pawn, 1f, 0.7f);

            Assert.AreEqual(FigureLayout.Pawn.Height, small.Height, Tolerance);
            Assert.AreEqual(FigureLayout.Pawn.Height, large.Height, Tolerance);
            Assert.AreEqual(small.ArtScale / 4f, large.ArtScale, Tolerance);
        }

        [Test]
        public void Fit_HeightScale_GrowsUpwardFromTheFeet()
        {
            var fit = FigureLayout.Fit(0f, 1f, FigureLayout.Pawn, 1.2f, 0.7f);

            Assert.AreEqual(FigureLayout.Pawn.Feet, fit.Feet, Tolerance);
            Assert.AreEqual(FigureLayout.Pawn.Height * 1.2f, fit.Height, Tolerance);
        }

        [Test]
        public void Fit_PlacesPinHeadAndBarFromTheFittedFigure()
        {
            var fit = FigureLayout.Fit(0f, 2f, FigureLayout.Bust, 1f, FigureLayout.SeatedChest);

            Assert.AreEqual(fit.Feet + fit.Height * FigureLayout.SeatedChest, fit.PinY, Tolerance);
            Assert.AreEqual(fit.Top - FigureLayout.HeadDrop, fit.HeadY, Tolerance);
            Assert.AreEqual(fit.Top + FigureLayout.BarGap, fit.BarY, Tolerance);
            Assert.Less(fit.Feet, fit.PinY);
            Assert.Less(fit.PinY, fit.HeadY);
            Assert.Less(fit.HeadY, fit.Top);
        }

        [Test]
        public void Fit_WithAnEmptyOrInvertedRange_KeepsTheFrame()
        {
            var flat = FigureLayout.Fit(1f, 1f, FigureLayout.Pawn, 1f, 0.7f);
            var upsideDown = FigureLayout.Fit(2f, 1f, FigureLayout.Pawn, 1f, 0.7f);

            Assert.AreEqual(FigureLayout.Pawn.ArtScale, flat.ArtScale);
            Assert.AreEqual(FigureLayout.Pawn.BarY, upsideDown.BarY);
        }

        [Test]
        public void Fit_ANonPositiveHeightScale_CountsAsOne()
        {
            var fit = FigureLayout.Fit(0f, 1f, FigureLayout.Pawn, 0f, 0.7f);
            Assert.AreEqual(FigureLayout.Pawn.Height, fit.Height, Tolerance);
        }

        [Test]
        public void FootAnchor_KeepsTheFeetStillUnderSquash()
        {
            const float worldScale = 0.8f;
            var pawn = FigureLayout.Pawn;

            foreach (float scaleY in new[] { 0.94f, 1f, 1.02f, 1.18f })
            {
                float offset = pawn.FootAnchorOffset(worldScale, scaleY);
                float feetWorld = offset + pawn.Feet * worldScale * scaleY;
                Assert.AreEqual(pawn.Feet * worldScale, feetWorld, Tolerance, $"scaleY {scaleY}");
            }
        }

        [Test]
        public void FootAnchor_IsZeroAtRestAndWhenHidden()
        {
            Assert.AreEqual(0f, FigureLayout.Pawn.FootAnchorOffset(0.8f, 1f), Tolerance);
            Assert.AreEqual(0f, FigureLayout.Pawn.FootAnchorOffset(0f, 0.9f), Tolerance);
        }
    }
}