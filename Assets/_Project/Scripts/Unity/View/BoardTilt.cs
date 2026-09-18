// Assets/_Project/Scripts/Unity/View/BoardTilt.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The pitch the board camera is actually using, for the parts of the view
    /// that have to follow it (VISUAL_PASS.md, V1b).
    /// </summary>
    /// <remarks>
    /// <b>Why a static.</b> The pitch is one number that many unrelated things
    /// need every frame — every piece's lean and depth order, the hop's
    /// direction, the pointer's hit test — and it changes while the game runs,
    /// whenever the Board camera setting is flipped. Threading it through each
    /// of them would mean re-binding them all on a settings change. The
    /// composition root writes it in <c>FrameCamera</c>, every frame the
    /// framing runs, so it cannot go stale; everyone else reads.
    ///
    /// <b>Zero means the flat camera</b>, and every derived value is then the
    /// identity: no lean, world up is screen up, a ground disc is unsquashed.
    /// So the top-down view behaves exactly as it did before the tilt existed.
    ///
    /// The trigonometry is cached because it is read once per piece per frame
    /// and the pitch changes perhaps twice in a session.
    /// </remarks>
    public static class BoardTilt
    {
        /// <summary>Degrees from straight down. 0 is the flat camera.</summary>
        public static float Pitch { get; private set; }

        public static bool IsTilted => Pitch > 0f;

        /// <summary>The figure's rotation about x, in degrees (<see cref="FigureTilt.LeanDegrees"/>).</summary>
        public static float Lean { get; private set; }

        /// <summary>The world direction that reads as up on screen.</summary>
        public static Vector3 ScreenUp { get; private set; } = Vector3.up;

        /// <summary>How much a circle on the table is foreshortened. 1 when flat.</summary>
        public static float GroundSquash { get; private set; } = 1f;

        /// <summary>
        /// Half the board's width, which is the range the pieces' depth orders
        /// are spread over. It lives here because it is only ever needed for
        /// the tilt, and this is already what the camera is doing.
        /// </summary>
        public static float SortExtent { get; private set; } = 8f;

        /// <summary>Called by the composition root whenever it frames the camera.</summary>
        public static void Set(float pitch, float sortExtent)
        {
            if (sortExtent > 0f) SortExtent = sortExtent;

            float wanted = pitch > 0f ? pitch : 0f;
            if (Mathf.Approximately(wanted, Pitch) && ScreenUp != Vector3.zero) return;

            Pitch = wanted;
            Lean = FigureTilt.LeanDegrees(wanted);
            GroundSquash = FigureTilt.GroundSquash(wanted);

            FigureTilt.ScreenUp(wanted, out float y, out float z);
            ScreenUp = new Vector3(0f, y, z);
        }
    }
}