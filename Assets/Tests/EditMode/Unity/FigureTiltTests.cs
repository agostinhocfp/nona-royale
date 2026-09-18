// Assets/Tests/EditMode/Unity/FigureTiltTests.cs
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Tests.Unity
{
    /// <summary>
    /// What the tilt does to a piece (VISUAL_PASS.md, V1b). Plain C#, so it
    /// runs in the cloud harness.
    /// </summary>
    [TestFixture]
    public sealed class FigureTiltTests
    {
        private const float Pitch = TiltFraming.MatchPitch;   // 52°
        private const float Extent = 7.5f;

        // ── Depth sorting ──────────────────────────────────────────────

        [Test]
        public void SortOrder_PutsANearPieceInFrontOfAFarOne()
        {
            int near = FigureTilt.SortOrder(-Extent, Extent);
            int far = FigureTilt.SortOrder(Extent, Extent);

            Assert.Greater(near, far, "the piece nearer the camera has to draw in front");
        }

        [Test]
        public void SortOrder_UsesTheWholeBand()
        {
            Assert.AreEqual(FigureTilt.MaxOrder, FigureTilt.SortOrder(-Extent, Extent));
            Assert.AreEqual(FigureTilt.MinOrder, FigureTilt.SortOrder(Extent, Extent));
        }

        [Test]
        public void SortOrder_StaysInTheBandBeyondTheBoard()
        {
            foreach (float y in new[] { -1000f, -Extent * 3f, 0f, Extent * 3f, 1000f })
            {
                int order = FigureTilt.SortOrder(y, Extent);

                Assert.GreaterOrEqual(order, FigureTilt.MinOrder, $"y {y}");
                Assert.LessOrEqual(order, FigureTilt.MaxOrder, $"y {y}");
            }
        }

        [Test]
        public void SortOrder_IsMonotonic()
        {
            int previous = int.MaxValue;

            for (int step = 0; step <= 40; step++)
            {
                float y = -Extent + 2f * Extent * step / 40f;
                int order = FigureTilt.SortOrder(y, Extent);

                Assert.LessOrEqual(order, previous, $"order went back up at y {y}");
                previous = order;
            }
        }

        /// <summary>
        /// The board is 15 cells across, so neighbouring rows have to land on
        /// different orders or two pieces a cell apart can still draw wrongly.
        /// </summary>
        [Test]
        public void SortOrder_SeparatesNeighbouringRows()
        {
            for (int row = 0; row < 14; row++)
            {
                float a = -Extent + row;
                float b = a + 1f;

                Assert.Greater(FigureTilt.SortOrder(a, Extent), FigureTilt.SortOrder(b, Extent),
                    $"rows {row} and {row + 1} share an order");
            }
        }

        [Test]
        public void SortOrder_SurvivesANonsenseBoard()
        {
            Assert.AreEqual(FigureTilt.MinOrder, FigureTilt.SortOrder(0f, 0f));
            Assert.AreEqual(FigureTilt.MinOrder, FigureTilt.SortOrder(0f, -4f));
        }

        [Test]
        public void SortOrder_BandSitsAboveTheHighlightsAndBelowTheEffects()
        {
            Assert.GreaterOrEqual(FigureTilt.MinOrder, 3, "the aim highlight is 2");
            Assert.Less(FigureTilt.MaxOrder, FigureTilt.CastTellOrder, "the cast tell draws over pieces");
            Assert.Less(FigureTilt.CastTellOrder, FigureTilt.FeedbackOrder);
            Assert.Less(FigureTilt.FeedbackOrder, FigureTilt.FloatingTextOrder, "damage numbers read over everything");
        }

        // ── Standing up ────────────────────────────────────────────────

        [Test]
        public void LeanDegrees_IsFlatForTheFlatCamera()
        {
            Assert.AreEqual(0f, FigureTilt.LeanDegrees(0f));
            Assert.AreEqual(0f, FigureTilt.LeanDegrees(-10f));
        }

        [Test]
        public void LeanDegrees_StandsTheFigureUpButLeansItBack()
        {
            float lean = FigureTilt.LeanDegrees(Pitch);

            // Between square-to-camera (−pitch) and bolt upright (−90).
            Assert.Less(lean, -Pitch, "square to the camera would read as floating");
            Assert.Greater(lean, -90f, "bolt upright loses a fifth of the figure's height");
        }

        [Test]
        public void ApparentHeight_KeepsNearlyAllOfTheArt()
        {
            Assert.Greater(FigureTilt.ApparentHeight(Pitch), 0.97f,
                $"only {FigureTilt.ApparentHeight(Pitch):P0} of the figure survives");
        }

        [Test]
        public void ApparentHeight_BeatsStandingBoltUpright()
        {
            // Upright would be cos(52°) off square, about 0.79.
            Assert.Greater(FigureTilt.ApparentHeight(Pitch), 0.79f);
        }

        [Test]
        public void ApparentHeight_IsEverythingForTheFlatCamera()
        {
            Assert.AreEqual(1f, FigureTilt.ApparentHeight(0f), 0.0001f);
        }

        [Test]
        public void LeanPivot_IsNothingForTheFlatCamera()
        {
            FigureTilt.LeanPivot(0f, -0.465f, out float y, out float z);

            Assert.AreEqual(0f, y);
            Assert.AreEqual(0f, z);
        }

        /// <summary>
        /// The whole point of the pivot: the feet stay exactly where they were
        /// once the lean is applied.
        /// </summary>
        [Test]
        public void LeanPivot_LeavesTheFeetWhereTheyWere()
        {
            const float feet = -0.465f;
            FigureTilt.LeanPivot(Pitch, feet, out float py, out float pz);

            double a = FigureTilt.LeanDegrees(Pitch) * System.Math.PI / 180.0;

            // The child at local (0, feet, 0), rotated about x, offset by the pivot.
            double y = py + feet * System.Math.Cos(a);
            double z = pz + feet * System.Math.Sin(a);

            Assert.AreEqual(feet, y, 0.0001, "the feet lifted or sank");
            Assert.AreEqual(0.0, z, 0.0001, "the feet came off the table");
        }

        [Test]
        public void LeanPivot_LiftsTheHeadOffTheTable()
        {
            FigureTilt.LeanPivot(Pitch, -0.465f, out float py, out float pz);

            double a = FigureTilt.LeanDegrees(Pitch) * System.Math.PI / 180.0;
            const float top = 0.425f;   // FigureLayout.Pawn.Top

            double z = pz + top * System.Math.Sin(a);

            Assert.Less(z, -0.2, "the head should stand out of the table, toward the camera");
        }

        // ── Screen directions ──────────────────────────────────────────

        [Test]
        public void ScreenUp_IsWorldUpForTheFlatCamera()
        {
            FigureTilt.ScreenUp(0f, out float y, out float z);

            Assert.AreEqual(1f, y, 0.0001f);
            Assert.AreEqual(0f, z, 0.0001f);
        }

        [Test]
        public void ScreenUp_LeansOutOfTheTableUnderTheTilt()
        {
            FigureTilt.ScreenUp(Pitch, out float y, out float z);

            Assert.Greater(y, 0f, "a hop still carries the piece up the board");
            Assert.Less(z, 0f, "and off the table, toward the camera");
            Assert.AreEqual(1f, (float)System.Math.Sqrt(y * y + z * z), 0.0001f, "it has to be a direction");
        }

        [Test]
        public void GroundSquash_IsOneLookingStraightDown()
        {
            Assert.AreEqual(1f, FigureTilt.GroundSquash(0f), 0.0001f);
        }

        [Test]
        public void GroundSquash_MatchesTheCameraAtThePitch()
        {
            Assert.AreEqual(0.6157f, FigureTilt.GroundSquash(Pitch), 0.001f);
        }
    }
}