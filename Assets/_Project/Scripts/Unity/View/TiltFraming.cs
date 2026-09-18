// Assets/_Project/Scripts/Unity/View/TiltFraming.cs
using System;

namespace NonaRoyale.Unity.View
{
    /// <summary>Where a tilted camera has to stand to frame the board.</summary>
    public struct TiltFrame
    {
        /// <summary>False when no camera in range fits the board; the caller falls back to top-down.</summary>
        public bool Fitted;

        /// <summary>Distance from the aim point to the camera, world units.</summary>
        public float Distance;

        /// <summary>The point on the board plane the camera looks at.</summary>
        public float AimX;
        public float AimY;

        /// <summary>The board's projected area in viewport units, for the log and the tests.</summary>
        public float Area;
    }

    /// <summary>
    /// The framing maths for the tilted view (VISUAL_PASS.md, V1): where to put
    /// a perspective camera so the board fills the rectangle the HUD leaves.
    /// </summary>
    /// <remarks>
    /// <b>Plain C#.</b> Nothing here touches Unity, so the projection and the
    /// search are testable outside the editor — which matters, because the one
    /// thing the mockups proved is that this fit cannot be eyeballed.
    ///
    /// <b>Why a fit has to be computed at all</b> (V3 mockup findings).
    /// Perspective makes the board's near edge the widest part of it. Framing
    /// that only looks at the board's centre pushes the near yards off the
    /// bottom of the screen, under the action tray. So the fit is solved
    /// against the board's four corners, not its middle.
    ///
    /// <b>The camera model.</b> The flat camera looks down +z at a board lying
    /// in the z = 0 plane. Tipping it about x by <c>pitch</c> makes it look up
    /// the board from the near edge. With the rotation only about x, and the
    /// aim point on the board plane, the projection collapses to three lines:
    /// for a board point (wx, wy) and an aim (tx, ty) at distance d,
    /// <code>
    /// eye.x = wx - tx
    /// eye.y = (wy - ty) * cos(pitch)
    /// eye.z = d + (wy - ty) * sin(pitch)
    /// </code>
    /// The far half of the board (wy above the aim) sits deeper, so it shrinks;
    /// the near half comes forward and spreads. That is the whole effect, and
    /// it is why the board gains screen area over the flat view: the near edge
    /// uses width that a square board leaves empty.
    ///
    /// <b>Pitch is measured from straight down</b>, to match the mockups and
    /// `ART_DIRECTION` — 0° is the flat view, 40° is the match, 58° the title.
    ///
    /// <b>The search.</b> For a fixed aim, whether the board fits is monotone
    /// in distance (push the camera back far enough and the board shrinks onto
    /// the optical axis), so the smallest fitting distance comes from a binary
    /// search. The aim runs down the board on a coarse scan, and the winner is
    /// the aim whose projected area is largest, less a small penalty for
    /// sitting off the free rectangle's centre. Horizontal aim is solved after
    /// the fact by a short fixed-point pass, because there is no lens shift:
    /// the optical axis is the middle of the screen, so moving the board
    /// sideways means moving the camera sideways.
    /// </remarks>
    public static class TiltFraming
    {
        /// <summary>
        /// A long lens, chosen against measurement (VISUAL_PASS.md, V1). A wide
        /// lens balloons the board's near edge, and the fit then has to shrink
        /// the whole board to keep that edge clear of the action tray. A long
        /// one keeps the trapezoid closer to a rectangle, so the same rectangle
        /// holds more board: 12° at 52° gives 1.45× the flat view's area with
        /// only 1.22× foreshortening, against 1.23× and 1.40× for 30° at 40°.
        /// </summary>
        public const float DefaultFieldOfView = 12f;

        /// <summary>
        /// Pitch from vertical in a match, and on the menu screens
        /// (VISUAL_PASS decision 1, re-measured for V1). Below about 30° the
        /// tilt costs screen area instead of gaining it: the board foreshortens
        /// and the near edge is not yet wide enough to pay for it.
        /// </summary>
        public const float MatchPitch = 52f;
        public const float MenuPitch = 58f;

        /// <summary>Pitch outside this range is clamped: 0 is the flat view, and past 80 the board is a line.</summary>
        public const float MinPitch = 0f;
        public const float MaxPitch = 80f;

        /// <summary>How far inside the free rectangle the board's corners must land, in viewport units.</summary>
        public const float DefaultMargin = 0.02f;

        /// <summary>Aim points tried down the board, and bisections per aim.</summary>
        private const int AimSamples = 49;
        private const int Bisections = 22;

        /// <summary>How much an off-centre board is penalised, against its area.</summary>
        private const float CentringWeight = 0.35f;

        private const float Deg2Rad = 0.017453292519943295f;

        /// <summary>
        /// The camera pose that frames a board of half-width
        /// <paramref name="halfExtent"/> inside the viewport rectangle
        /// [<paramref name="x0"/>, <paramref name="x1"/>] ×
        /// [<paramref name="y0"/>, <paramref name="y1"/>], in viewport units
        /// with the origin bottom-left.
        /// </summary>
        public static TiltFrame Solve(float pitchDegrees, float fieldOfView, float aspect, float halfExtent,
            float x0, float x1, float y0, float y1, float margin = DefaultMargin)
        {
            var miss = default(TiltFrame);

            if (!(halfExtent > 0f) || !(fieldOfView > 0f) || !(aspect > 0f)) return miss;

            x0 += margin;
            x1 -= margin;
            y0 += margin;
            y1 -= margin;

            // A rectangle that no longer holds the optical axis cannot be filled
            // by a camera without lens shift, however far back it stands.
            if (!(x1 > x0) || !(y1 > y0)) return miss;
            if (x0 > 0.5f || x1 < 0.5f || y0 > 0.5f || y1 < 0.5f) return miss;

            float pitch = Clamp(pitchDegrees, MinPitch, MaxPitch);
            float sin = (float)Math.Sin(pitch * Deg2Rad);
            float cos = (float)Math.Cos(pitch * Deg2Rad);

            float tanV = (float)Math.Tan(Clamp(fieldOfView, 1f, 170f) * 0.5f * Deg2Rad);
            float tanH = tanV * aspect;

            // Every corner has to stay in front of the lens, whatever the aim.
            float near = 2f * halfExtent * sin + 0.1f;
            float low = Math.Max(near, halfExtent * 0.25f);
            float high = Math.Max(low * 2f, halfExtent * 60f);

            float centreY = (y0 + y1) * 0.5f;
            var best = miss;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < AimSamples; i++)
            {
                // The aim runs from the board's far edge to its near edge.
                float aimY = halfExtent * (1f - 2f * i / (float)(AimSamples - 1));

                if (!Fits(high, aimY, halfExtent, sin, cos, tanV, tanH, x0, x1, y0, y1)) continue;

                float lo = low, hi = high;
                for (int step = 0; step < Bisections; step++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Fits(mid, aimY, halfExtent, sin, cos, tanV, tanH, x0, x1, y0, y1)) hi = mid;
                    else lo = mid;
                }

                Extent(hi, 0f, aimY, halfExtent, sin, cos, tanV, tanH,
                    out float uMin, out float uMax, out float vMin, out float vMax);

                float area = (uMax - uMin) * (vMax - vMin);
                float score = area - CentringWeight * Math.Abs((vMin + vMax) * 0.5f - centreY);
                if (score <= bestScore) continue;

                bestScore = score;
                best = new TiltFrame { Fitted = true, Distance = hi, AimY = aimY, Area = area };
            }

            if (!best.Fitted) return miss;

            // The sideways aim is solved last, and it changes the projected
            // width a little, because it moves a near corner further than a far
            // one. So the fit is verified with the real aim in place and the
            // camera backed off until it holds. Two or three steps at most.
            for (int step = 0; step < 6; step++)
            {
                best.AimX = SolveAimX(best.Distance, best.AimY, halfExtent, sin, cos, tanH, x0, x1);

                Extent(best.Distance, best.AimX, best.AimY, halfExtent, sin, cos, tanV, tanH,
                    out float uMin, out float uMax, out float vMin, out float vMax);

                if (uMin >= x0 && uMax <= x1 && vMin >= y0 && vMax <= y1)
                {
                    best.Area = (uMax - uMin) * (vMax - vMin);
                    return best;
                }

                best.Distance *= 1.02f;
            }

            return miss;
        }

        /// <summary>
        /// Where the camera stands, given a solved frame. The rotation is
        /// <c>Euler(-pitch, 0, 0)</c>; the caller builds it.
        /// </summary>
        public static void CameraPose(float pitchDegrees, float distance, float aimX, float aimY,
            out float x, out float y, out float z)
        {
            float pitch = Clamp(pitchDegrees, MinPitch, MaxPitch);
            float sin = (float)Math.Sin(pitch * Deg2Rad);
            float cos = (float)Math.Cos(pitch * Deg2Rad);

            // The aim sits on the board plane; the camera backs off along the
            // view direction (0, sin, cos), so it ends up below and in front.
            x = aimX;
            y = aimY - distance * sin;
            z = -distance * cos;
        }

        /// <summary>
        /// Projects a point on the board plane to viewport units, origin
        /// bottom-left. The camera and the tests share this, so a framing that
        /// passes here is the framing the camera gets.
        /// </summary>
        public static void Project(float pitchDegrees, float fieldOfView, float aspect,
            float distance, float aimX, float aimY, float worldX, float worldY, out float u, out float v)
        {
            float pitch = Clamp(pitchDegrees, MinPitch, MaxPitch);
            float sin = (float)Math.Sin(pitch * Deg2Rad);
            float cos = (float)Math.Cos(pitch * Deg2Rad);
            float tanV = (float)Math.Tan(Clamp(fieldOfView, 1f, 170f) * 0.5f * Deg2Rad);

            Project(distance, aimX, aimY, worldX, worldY, sin, cos, tanV, tanV * Math.Max(0.0001f, aspect),
                out u, out v);
        }

        private static void Project(float distance, float aimX, float aimY, float worldX, float worldY,
            float sin, float cos, float tanV, float tanH, out float u, out float v)
        {
            float dy = worldY - aimY;
            float depth = distance + dy * sin;
            if (depth < 0.0001f) depth = 0.0001f;

            u = 0.5f + (worldX - aimX) / (depth * 2f * tanH);
            v = 0.5f + dy * cos / (depth * 2f * tanV);
        }

        /// <summary>The projected bounding box of the board's four corners.</summary>
        private static void Extent(float distance, float aimX, float aimY, float halfExtent,
            float sin, float cos, float tanV, float tanH,
            out float uMin, out float uMax, out float vMin, out float vMax)
        {
            uMin = vMin = float.MaxValue;
            uMax = vMax = float.MinValue;

            for (int c = 0; c < 4; c++)
            {
                float wx = (c & 1) == 0 ? -halfExtent : halfExtent;
                float wy = (c & 2) == 0 ? -halfExtent : halfExtent;

                Project(distance, aimX, aimY, wx, wy, sin, cos, tanV, tanH, out float u, out float v);

                if (u < uMin) uMin = u;
                if (u > uMax) uMax = u;
                if (v < vMin) vMin = v;
                if (v > vMax) vMax = v;
            }
        }

        private static bool Fits(float distance, float aimY, float halfExtent,
            float sin, float cos, float tanV, float tanH, float x0, float x1, float y0, float y1)
        {
            Extent(distance, 0f, aimY, halfExtent, sin, cos, tanV, tanH,
                out float uMin, out float uMax, out float vMin, out float vMax);

            // Horizontal is checked as a width, because the aim slides sideways
            // afterwards; vertical is checked in place.
            return uMax - uMin <= x1 - x0 && vMin >= y0 && vMax <= y1;
        }

        /// <summary>
        /// The sideways aim that centres the board in [<paramref name="x0"/>,
        /// <paramref name="x1"/>]. Each corner's horizontal position moves with
        /// the aim at its own rate, because a near corner is closer to the lens
        /// than a far one, so this settles by iteration rather than in one step.
        /// </summary>
        private static float SolveAimX(float distance, float aimY, float halfExtent,
            float sin, float cos, float tanH, float x0, float x1)
        {
            float wanted = (x0 + x1) * 0.5f;
            float aimX = 0f;

            for (int pass = 0; pass < 4; pass++)
            {
                float uMin = float.MaxValue, uMax = float.MinValue;

                for (int c = 0; c < 4; c++)
                {
                    float wx = (c & 1) == 0 ? -halfExtent : halfExtent;
                    float wy = (c & 2) == 0 ? -halfExtent : halfExtent;

                    Project(distance, aimX, aimY, wx, wy, sin, cos, 1f, tanH, out float u, out _);

                    if (u < uMin) uMin = u;
                    if (u > uMax) uMax = u;
                }

                float centre = (uMin + uMax) * 0.5f;
                float error = centre - wanted;
                if (Math.Abs(error) < 0.0002f) break;

                // Moving the aim right pushes the board left on screen by
                // roughly error * the near corner's scale; the nearest corner
                // dominates, so its depth sets the step.
                float depth = distance - halfExtent * sin;
                if (depth < 0.0001f) depth = 0.0001f;
                aimX += error * depth * 2f * tanH;
            }

            return aimX;
        }

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }
}