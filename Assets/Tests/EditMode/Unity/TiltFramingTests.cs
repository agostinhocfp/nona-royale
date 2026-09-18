// Assets/Tests/EditMode/Unity/TiltFramingTests.cs
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Tests.Unity
{
    /// <summary>
    /// The tilted view's framing maths (VISUAL_PASS.md, V1). Plain C#, so it
    /// runs in the cloud harness — the one part of the tilt that does not need
    /// Play Mode to check.
    /// </summary>
    [TestFixture]
    public sealed class TiltFramingTests
    {
        // The 1080p HUD reservations: left 290, right 78, top 56, bottom 196.
        private const float HudX0 = 290f / 1920f;
        private const float HudX1 = 1f - 78f / 1920f;
        private const float HudY0 = 196f / 1080f;
        private const float HudY1 = 1f - 56f / 1080f;

        /// <summary>The standard board's <c>BoardLayout.Extent</c> (7.5) with the framing's 12% air.</summary>
        private const float Extent = 7.5f * 1.12f;

        private const float Fov = TiltFraming.DefaultFieldOfView;
        private const float Aspect = 16f / 9f;

        private static TiltFrame Match() =>
            TiltFraming.Solve(TiltFraming.MatchPitch, Fov, Aspect, Extent, HudX0, HudX1, HudY0, HudY1);

        /// <summary>The board's four corners in viewport units, in order: near-left, near-right, far-right, far-left.</summary>
        private static float[,] Corners(TiltFrame frame, float pitch = TiltFraming.MatchPitch, float aspect = Aspect)
        {
            float[,] world = { { -Extent, -Extent }, { Extent, -Extent }, { Extent, Extent }, { -Extent, Extent } };
            var uv = new float[4, 2];

            for (int c = 0; c < 4; c++)
            {
                TiltFraming.Project(pitch, Fov, aspect, frame.Distance, frame.AimX, frame.AimY,
                    world[c, 0], world[c, 1], out float u, out float v);
                uv[c, 0] = u;
                uv[c, 1] = v;
            }

            return uv;
        }

        /// <summary>The board's own area in viewport units — the trapezoid, not its bounding box.</summary>
        private static float BoardArea(TiltFrame frame)
        {
            var c = Corners(frame);
            float sum = 0f;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                sum += c[i, 0] * c[j, 1] - c[j, 0] * c[i, 1];
            }

            return System.Math.Abs(sum) * 0.5f;
        }

        /// <summary>The flat view's board: a square fitted into the same rectangle, in viewport units.</summary>
        private static float FlatArea()
        {
            float side = System.Math.Min((HudX1 - HudX0) * Aspect, HudY1 - HudY0);
            return side * side / Aspect;
        }

        [Test]
        public void Solve_FitsTheMatchFrame()
        {
            var frame = Match();

            Assert.IsTrue(frame.Fitted, "the match framing has to solve");
            Assert.Greater(frame.Distance, 0f);
            Assert.Greater(frame.Area, 0f);
        }

        [Test]
        public void Solve_KeepsEveryCornerInsideTheFreeRectangle()
        {
            var frame = Match();

            for (int c = 0; c < 4; c++)
            {
                float wx = (c & 1) == 0 ? -Extent : Extent;
                float wy = (c & 2) == 0 ? -Extent : Extent;

                TiltFraming.Project(TiltFraming.MatchPitch, Fov, Aspect,
                    frame.Distance, frame.AimX, frame.AimY, wx, wy, out float u, out float v);

                Assert.GreaterOrEqual(u, HudX0, $"corner {c} fell off the left");
                Assert.LessOrEqual(u, HudX1, $"corner {c} fell off the right");
                Assert.GreaterOrEqual(v, HudY0, $"corner {c} fell under the tray");
                Assert.LessOrEqual(v, HudY1, $"corner {c} fell behind the top bar");
            }
        }

        /// <summary>
        /// The whole reason for the tilt, measured honestly: the board's own
        /// trapezoid against the flat view's square, not the bounding box,
        /// which flatters the tilt by counting the corners it leaves empty.
        /// The chosen 52° and 12° were picked to beat flat by about 45%.
        /// </summary>
        [Test]
        public void Solve_BeatsTheFlatViewOnBoardArea()
        {
            float tilted = BoardArea(Match());
            float flat = FlatArea();

            Assert.Greater(tilted / flat, 1.35f,
                $"tilted {tilted:F4} against flat {flat:F4} is only {tilted / flat:F2}x");
        }

        /// <summary>
        /// Cells at the far edge are smaller than cells at the near edge, and
        /// the player counts cells to plan a move, so the squeeze is capped.
        /// The long lens is what keeps it down: 30° of field of view at 40° of
        /// pitch gives 1.40 here, against 1.22 for the chosen pair.
        /// </summary>
        [Test]
        public void Solve_KeepsForeshorteningMild()
        {
            var c = Corners(Match());
            float near = c[1, 0] - c[0, 0];
            float far = c[2, 0] - c[3, 0];

            Assert.Less(near / far, 1.3f, $"near/far is {near / far:F2}");
        }

        /// <summary>
        /// A shallow tilt is worse than no tilt at all: the board foreshortens
        /// and its near edge has not yet widened enough to pay for it. This is
        /// why the pitch is 52° and not the 25° a gentle tilt would suggest.
        /// </summary>
        [Test]
        public void Solve_AtAShallowPitch_LosesAreaAgainstTheFlatView()
        {
            var shallow = TiltFraming.Solve(20f, Fov, Aspect, Extent, HudX0, HudX1, HudY0, HudY1);

            Assert.IsTrue(shallow.Fitted);

            var c = Corners(shallow, 20f);
            float sum = 0f;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                sum += c[i, 0] * c[j, 1] - c[j, 0] * c[i, 1];
            }

            Assert.Less(System.Math.Abs(sum) * 0.5f, FlatArea());
        }

        [Test]
        public void Solve_CentresTheBoardSidewaysInTheFreeRectangle()
        {
            var frame = Match();

            float uMin = float.MaxValue, uMax = float.MinValue;
            for (int c = 0; c < 4; c++)
            {
                float wx = (c & 1) == 0 ? -Extent : Extent;
                float wy = (c & 2) == 0 ? -Extent : Extent;

                TiltFraming.Project(TiltFraming.MatchPitch, Fov, Aspect,
                    frame.Distance, frame.AimX, frame.AimY, wx, wy, out float u, out _);

                if (u < uMin) uMin = u;
                if (u > uMax) uMax = u;
            }

            Assert.AreEqual((HudX0 + HudX1) * 0.5f, (uMin + uMax) * 0.5f, 0.01f);
        }

        /// <summary>The left reservation is the big one, so the aim leans left of the board's middle.</summary>
        [Test]
        public void Solve_AimsLeftWhenTheSquadRailTakesTheLeft()
        {
            var frame = Match();

            Assert.Less(frame.AimX, 0f, "the free rectangle sits right of centre, so the camera looks left");
        }

        [Test]
        public void Solve_PutsTheNearEdgeBelowTheFarEdge()
        {
            var frame = Match();

            TiltFraming.Project(TiltFraming.MatchPitch, Fov, Aspect,
                frame.Distance, frame.AimX, frame.AimY, 0f, -Extent, out _, out float nearV);
            TiltFraming.Project(TiltFraming.MatchPitch, Fov, Aspect,
                frame.Distance, frame.AimX, frame.AimY, 0f, Extent, out _, out float farV);

            Assert.Less(nearV, farV, "the near edge belongs at the bottom of the screen");
        }

        [Test]
        public void Solve_MakesTheNearEdgeWiderThanTheFarEdge()
        {
            var frame = Match();

            TiltFraming.Project(TiltFraming.MatchPitch, Fov, Aspect,
                frame.Distance, frame.AimX, frame.AimY, Extent, -Extent, out float nearRight, out _);
            TiltFraming.Project(TiltFraming.MatchPitch, Fov, Aspect,
                frame.Distance, frame.AimX, frame.AimY, -Extent, -Extent, out float nearLeft, out _);
            TiltFraming.Project(TiltFraming.MatchPitch, Fov, Aspect,
                frame.Distance, frame.AimX, frame.AimY, Extent, Extent, out float farRight, out _);
            TiltFraming.Project(TiltFraming.MatchPitch, Fov, Aspect,
                frame.Distance, frame.AimX, frame.AimY, -Extent, Extent, out float farLeft, out _);

            Assert.Greater(nearRight - nearLeft, farRight - farLeft);
        }

        [Test]
        public void Solve_StandsFurtherBackForASteeperPitch()
        {
            // At the title there is no HUD, so the whole frame is free.
            var match = TiltFraming.Solve(TiltFraming.MatchPitch, Fov, Aspect, Extent, 0f, 1f, 0f, 1f);
            var menu = TiltFraming.Solve(TiltFraming.MenuPitch, Fov, Aspect, Extent, 0f, 1f, 0f, 1f);

            Assert.IsTrue(match.Fitted);
            Assert.IsTrue(menu.Fitted);
            Assert.Greater(menu.Distance, match.Distance,
                "a steeper look down sees more of the board's depth, so it needs more room");
        }

        [Test]
        public void Solve_FillsMoreOfTheFrameWithNoHudReserved()
        {
            var hud = Match();
            var free = TiltFraming.Solve(TiltFraming.MatchPitch, Fov, Aspect, Extent, 0f, 1f, 0f, 1f);

            Assert.Greater(free.Area, hud.Area);
        }

        [Test]
        public void Solve_ScalesWithTheBoard_NotTheOtherWayRound()
        {
            var small = TiltFraming.Solve(TiltFraming.MatchPitch, Fov, Aspect, 4f, 0f, 1f, 0f, 1f);
            var large = TiltFraming.Solve(TiltFraming.MatchPitch, Fov, Aspect, 16f, 0f, 1f, 0f, 1f);

            Assert.IsTrue(small.Fitted);
            Assert.IsTrue(large.Fitted);

            // Four times the board, four times the distance, and the same picture.
            Assert.AreEqual(4f, large.Distance / small.Distance, 0.05f);
            Assert.AreEqual(small.Area, large.Area, 0.01f);
        }

        [Test]
        public void Project_IsTheCentreOfTheScreenAtTheAimPoint()
        {
            TiltFraming.Project(TiltFraming.MatchPitch, Fov, Aspect, 30f, 1.5f, -2f, 1.5f, -2f,
                out float u, out float v);

            Assert.AreEqual(0.5f, u, 0.0001f);
            Assert.AreEqual(0.5f, v, 0.0001f);
        }

        [Test]
        public void Project_AtZeroPitch_IsAPlainPerspectiveLookDown()
        {
            // With no tilt both axes scale by the same depth, so a square board
            // stays square: equal offsets give equal viewport offsets once the
            // aspect is taken out.
            TiltFraming.Project(0f, Fov, Aspect, 20f, 0f, 0f, 3f, 0f, out float u, out _);
            TiltFraming.Project(0f, Fov, Aspect, 20f, 0f, 0f, 0f, 3f, out _, out float v);

            Assert.AreEqual((u - 0.5f) * Aspect, v - 0.5f, 0.0001f);
        }

        [Test]
        public void CameraPose_StandsBelowAndInFrontOfTheBoard()
        {
            TiltFraming.CameraPose(TiltFraming.MatchPitch, 30f, 1f, -2f,
                out float x, out float y, out float z);

            Assert.AreEqual(1f, x, 0.0001f, "the camera keeps the aim's x");
            Assert.Less(y, -2f, "it drops below the aim to look up the board");
            Assert.Less(z, 0f, "the board is at z = 0 and the camera is in front of it");
        }

        [Test]
        public void CameraPose_IsExactlyTheDistanceFromTheAim()
        {
            TiltFraming.CameraPose(TiltFraming.MatchPitch, 30f, 1f, -2f,
                out float x, out float y, out float z);

            float dx = x - 1f, dy = y - -2f, dz = z - 0f;
            Assert.AreEqual(30f, (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz), 0.001f);
        }

        [Test]
        public void CameraPose_AtZeroPitch_LooksStraightDownFromTheFlatDistance()
        {
            TiltFraming.CameraPose(0f, 25f, 0f, 0f, out float x, out float y, out float z);

            Assert.AreEqual(0f, x, 0.0001f);
            Assert.AreEqual(0f, y, 0.0001f);
            Assert.AreEqual(-25f, z, 0.0001f);
        }

        [Test]
        public void Solve_RefusesARectangleThatMissesTheOpticalAxis()
        {
            // No camera without lens shift can put the board in a rectangle that
            // does not contain the middle of the screen, however far it stands.
            var frame = TiltFraming.Solve(TiltFraming.MatchPitch, Fov, Aspect, Extent, 0.6f, 0.95f, 0.1f, 0.9f);

            Assert.IsFalse(frame.Fitted);
        }

        [Test]
        public void Solve_RefusesNonsense()
        {
            Assert.IsFalse(TiltFraming.Solve(40f, Fov, Aspect, 0f, 0f, 1f, 0f, 1f).Fitted, "no board");
            Assert.IsFalse(TiltFraming.Solve(40f, 0f, Aspect, Extent, 0f, 1f, 0f, 1f).Fitted, "no field of view");
            Assert.IsFalse(TiltFraming.Solve(40f, Fov, 0f, Extent, 0f, 1f, 0f, 1f).Fitted, "no aspect");
            Assert.IsFalse(TiltFraming.Solve(40f, Fov, Aspect, Extent, 0.5f, 0.5f, 0f, 1f).Fitted, "flat rectangle");
        }

        [Test]
        public void Solve_HoldsUpAtAPhoneAspect()
        {
            var frame = TiltFraming.Solve(TiltFraming.MatchPitch, Fov, 0.5f, Extent, 0.05f, 0.95f, 0.2f, 0.85f);

            Assert.IsTrue(frame.Fitted, "a tall window still has to frame the board");

            for (int c = 0; c < 4; c++)
            {
                float wx = (c & 1) == 0 ? -Extent : Extent;
                float wy = (c & 2) == 0 ? -Extent : Extent;

                TiltFraming.Project(TiltFraming.MatchPitch, Fov, 0.5f,
                    frame.Distance, frame.AimX, frame.AimY, wx, wy, out float u, out float v);

                Assert.GreaterOrEqual(u, 0.05f);
                Assert.LessOrEqual(u, 0.95f);
                Assert.GreaterOrEqual(v, 0.2f);
                Assert.LessOrEqual(v, 0.85f);
            }
        }

        [Test]
        public void Solve_ClampsPitchRatherThanFailing()
        {
            var steep = TiltFraming.Solve(200f, Fov, Aspect, Extent, 0f, 1f, 0f, 1f);
            var capped = TiltFraming.Solve(TiltFraming.MaxPitch, Fov, Aspect, Extent, 0f, 1f, 0f, 1f);

            Assert.IsTrue(steep.Fitted);
            Assert.AreEqual(capped.Distance, steep.Distance, 0.001f);
        }
    
        /// <summary>
        /// A 4:3 window is the case that needs the fit's two safety nets: the
        /// width check that ignores the sideways aim while the distance is
        /// being found, and the back-off that re-verifies once the aim is in.
        /// Without either of them this window does not solve at all.
        /// </summary>
        [Test]
        public void Solve_FitsAFourThreeWindow()
        {
            const float aspect = 4f / 3f;
            var frame = TiltFraming.Solve(TiltFraming.MatchPitch, Fov, aspect, Extent,
                290f / 1440f, 1f - 78f / 1440f, HudY0, HudY1);

            Assert.IsTrue(frame.Fitted, "a 4:3 window still has to frame the board");

            var c = Corners(frame, TiltFraming.MatchPitch, aspect);
            for (int i = 0; i < 4; i++)
            {
                Assert.GreaterOrEqual(c[i, 0], 290f / 1440f, $"corner {i} off the left");
                Assert.LessOrEqual(c[i, 0], 1f - 78f / 1440f, $"corner {i} off the right");
                Assert.GreaterOrEqual(c[i, 1], HudY0, $"corner {i} under the tray");
                Assert.LessOrEqual(c[i, 1], HudY1, $"corner {i} behind the top bar");
            }
        }

        /// <summary>
        /// The board sits in the middle of the band the HUD leaves, rather than
        /// merely inside it. Without the search's centring term the board
        /// drifts: at a phone's aspect it settles about 0.16 of the screen
        /// high, which puts the near yards against the action tray.
        /// </summary>
        [Test]
        public void Solve_CentresTheBoardVerticallyInTheFreeRectangle()
        {
            CheckVerticalCentring(TiltFraming.MatchPitch, 0.5f, 0.05f, 0.95f, 0.2f, 0.85f);
            CheckVerticalCentring(TiltFraming.MenuPitch, Aspect, 0f, 1f, 0f, 1f);
            CheckVerticalCentring(TiltFraming.MatchPitch, 4f / 3f, 290f / 1440f, 1f - 78f / 1440f, HudY0, HudY1);
        }

        private static void CheckVerticalCentring(float pitch, float aspect,
            float x0, float x1, float y0, float y1)
        {
            var frame = TiltFraming.Solve(pitch, Fov, aspect, Extent, x0, x1, y0, y1);
            Assert.IsTrue(frame.Fitted);

            float vMin = float.MaxValue, vMax = float.MinValue;
            for (int c = 0; c < 4; c++)
            {
                float wx = (c & 1) == 0 ? -Extent : Extent;
                float wy = (c & 2) == 0 ? -Extent : Extent;

                TiltFraming.Project(pitch, Fov, aspect, frame.Distance, frame.AimX, frame.AimY,
                    wx, wy, out _, out float v);

                if (v < vMin) vMin = v;
                if (v > vMax) vMax = v;
            }

            Assert.AreEqual((y0 + y1) * 0.5f, (vMin + vMax) * 0.5f, 0.02f,
                $"the board drifted in the free band at aspect {aspect}");
        }

        /// <summary>
        /// The standard board in a 1080p window with the HUD up, as measured
        /// when 52° and 12° were chosen. A guard against the fit drifting
        /// without anyone meaning it to.
        /// </summary>
        [Test]
        public void Solve_MatchesTheMeasuredFrameForTheStandardBoard()
        {
            var frame = Match();

            Assert.AreEqual(65.67f, frame.Distance, 0.5f);
            Assert.AreEqual(-1.26f, frame.AimX, 0.05f);
            Assert.AreEqual(-2.45f, frame.AimY, 0.05f);
        }
}
}