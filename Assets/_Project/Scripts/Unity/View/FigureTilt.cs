// Assets/_Project/Scripts/Unity/View/FigureTilt.cs
using System;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// What the tilted camera does to a piece (VISUAL_PASS.md, V1b): how far a
    /// figure leans to stand up, where its transform has to sit so its feet
    /// stay planted, and which pieces draw in front of which.
    /// </summary>
    /// <remarks>
    /// <b>Plain C#.</b> No Unity types, so every number here is tested outside
    /// the editor. <see cref="BoardTilt"/> holds the pitch the camera is
    /// actually using and is the only Unity-side part.
    ///
    /// <b>The board is the XY plane and the flat camera looks down +z at it</b>,
    /// so a piece's sprite lies *on* the table. Tipping the camera about x
    /// makes that sprite lie down in view. Standing a figure up means rotating
    /// it about x as well, and the two rotations decide how much of the art the
    /// player sees:
    /// <list type="bullet">
    /// <item>0° — flat on the table, which is what the top-down view wants.</item>
    /// <item>−90° — bolt upright in the world, like a standee. Seen from a 52°
    /// camera it is foreshortened to 79% of its height and its head leans away.</item>
    /// <item>−<c>pitch</c> — square to the camera. Full height and no
    /// distortion, but the figure is leaning back 38° from vertical, which
    /// reads as floating rather than standing.</item>
    /// </list>
    /// The mockups landed between the last two, at <see cref="LeanFraction"/>
    /// of the pitch back from upright, which shows 99% of the figure's height
    /// while still reading as standing on the table. That is what is used.
    ///
    /// <b>Feet, not middles.</b> A piece's origin is the centre of its figure
    /// frame (<see cref="FigureLayout"/>), not its feet, so rotating the figure
    /// about the origin swings its feet off the cell. The lean is applied to a
    /// child transform whose position cancels the swing, which leaves every
    /// sprite inside the figure where it already was.
    ///
    /// <b>Depth, not creation order.</b> Nothing in the view sorted by position
    /// before this: every <c>sortingOrder</c> was a compile-time constant, and
    /// all of a piece's parts shared 0–6 on one layer, so two pieces on
    /// different rows drew in whatever order Unity happened to have made them.
    /// Top-down that is invisible. Tilted, a far piece can cover a near one.
    /// </remarks>
    public static class FigureTilt
    {
        /// <summary>
        /// How far back from upright a standing figure leans, as a fraction of
        /// the camera's pitch. From the V1 mockups, where the figures were
        /// judged to read well at this angle.
        /// </summary>
        public const float LeanFraction = 0.55f;

        // ── Depth sorting ──────────────────────────────────────────────────

        /// <summary>
        /// The lowest and highest sorting order a piece may take. The band sits
        /// above the board art (−33…−12), the devices (0) and the move and aim
        /// highlights (1, 2), and below the cast tell, the feedback rings and
        /// the damage numbers, which were moved into the forties to make room.
        /// </summary>
        public const int MinOrder = 3;
        public const int MaxOrder = 39;

        /// <summary>
        /// The orders above the band. They used to be 14, 15 and 20, which left
        /// the pieces about eleven slots — too few depth levels for a 15-cell
        /// board — so they were moved up in V1b. They are declared here so the
        /// band and the things that must clear it are one list.
        /// </summary>
        public const int CastTellOrder = 42;
        public const int FeedbackOrder = 43;
        public const int FloatingTextOrder = 48;

        /// <summary>
        /// The sorting order for a piece standing at <paramref name="worldY"/>
        /// on a board reaching <paramref name="extent"/> either side of the
        /// centre. Nearer the camera is a lower y and a higher order, so it
        /// draws in front.
        /// </summary>
        public static int SortOrder(float worldY, float extent)
        {
            if (!(extent > 0f)) return MinOrder;

            // 0 at the far edge, 1 at the near edge.
            double t = 0.5 - 0.5 * Clamp(worldY / extent, -1f, 1f);
            int span = MaxOrder - MinOrder;

            return MinOrder + (int)Math.Round(t * span);
        }

        // ── Standing up ────────────────────────────────────────────────────

        /// <summary>
        /// The figure's rotation about x, in degrees, for a camera at
        /// <paramref name="pitch"/> from vertical. Zero for the flat camera, so
        /// the top-down view is left exactly as it was.
        /// </summary>
        public static float LeanDegrees(float pitch)
        {
            if (!(pitch > 0f)) return 0f;

            return -(90f - LeanFraction * Clamp(pitch, 0f, 90f));
        }

        /// <summary>
        /// How much of the figure's height survives the projection, as a
        /// fraction: 1 means the art is seen at full height. Used by the tests
        /// to hold the lean honest, and worth reading if the lean is ever
        /// retuned.
        /// </summary>
        public static float ApparentHeight(float pitch)
        {
            if (!(pitch > 0f)) return 1f;

            // The figure's height vector is (0, cos θ, sin θ) for a lean of θ
            // below flat; the camera's up is (0, cos p, −sin p). Their dot is
            // cos(p − θ), and θ here is −LeanDegrees.
            double theta = -LeanDegrees(pitch) * Deg2Rad;
            return (float)Math.Cos(Clamp(pitch, 0f, 90f) * Deg2Rad - theta);
        }

        /// <summary>
        /// Where the leaning child transform sits, in figure units, so that the
        /// point at <paramref name="feet"/> does not move when the lean is
        /// applied. Rotating about x maps (0, f, 0) to (0, f·cos a, f·sin a),
        /// so the offset is what puts it back.
        /// </summary>
        public static void LeanPivot(float pitch, float feet, out float y, out float z)
        {
            float lean = LeanDegrees(pitch);
            if (lean == 0f)
            {
                y = 0f;
                z = 0f;
                return;
            }

            double a = lean * Deg2Rad;
            y = feet - feet * (float)Math.Cos(a);
            z = -feet * (float)Math.Sin(a);
        }

        // ── Screen directions ──────────────────────────────────────────────

        /// <summary>
        /// The world direction that reads as up on screen: the camera's own up.
        /// A hop has to rise along this, or under the tilt it slides up the
        /// table instead of off it. (0, 1, 0) for the flat camera.
        /// </summary>
        public static void ScreenUp(float pitch, out float y, out float z)
        {
            double p = Clamp(pitch, 0f, 90f) * Deg2Rad;
            y = (float)Math.Cos(p);
            z = -(float)Math.Sin(p);
        }

        /// <summary>
        /// How much a circle lying on the table is squashed vertically by the
        /// projection. 1 looking straight down, and it is the camera that does
        /// the squashing — so a ground disc is drawn as a true circle under the
        /// tilt, rather than the hand-faked ellipse the flat view uses.
        /// </summary>
        public static float GroundSquash(float pitch) =>
            (float)Math.Cos(Clamp(pitch, 0f, 90f) * Deg2Rad);

        private const double Deg2Rad = 0.017453292519943295;

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }
}