// Assets/_Project/Scripts/Unity/View/Figures/Rig/RigClips.cs
using System;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Poses over time (OPERATOR_LOOKBOOK.md, LB5): the idle loop, the step
    /// cycle, the cast and the seated loop, as blends between a rig's named
    /// poses. Plain C#, so the judging window previews exactly what
    /// <c>RigAnimator</c> will play on the board (LB5b).
    /// </summary>
    /// <remarks>
    /// Timings are placeholders until LB5b fits them to MOTION.md: the step
    /// has to fit the hop's time per cell, the cast the cast tell's duration.
    /// </remarks>
    public static class RigClips
    {
        public const string Idle = "idle";
        public const string Step = "step";
        public const string Cast = "cast";
        public const string Seated = "seated";

        public static readonly string[] All = { Idle, Step, Cast, Seated };

        /// <summary>Seconds per cycle (loops) or in all (the cast).</summary>
        public static float Length(string clip)
        {
            switch (clip)
            {
                case Idle: return 2.8f;
                case Step: return 0.5f;
                case Cast: return 1.1f;
                case Seated: return 3.4f;
                default: return 1f;
            }
        }

        public static bool Loops(string clip) => clip != Cast;

        /// <summary>The pose at <paramref name="seconds"/> into the clip; loops wrap, the cast holds its end.</summary>
        public static RigPose Sample(OperatorRig rig, string clip, float seconds)
        {
            float length = Length(clip);
            float t = Loops(clip) ? Repeat(seconds, length) / length : Math.Min(Math.Max(seconds / length, 0f), 1f);

            switch (clip)
            {
                case Idle:
                    return RigPose.Lerp(rig.Pose(RigPoseNames.IdleA), rig.Pose(RigPoseNames.IdleB), Wave(t));

                case Seated:
                    return RigPose.Lerp(rig.Pose(RigPoseNames.Seated), rig.Pose(RigPoseNames.SeatedB), Wave(t));

                case Step:
                {
                    // Contact A, pass, contact B, pass.
                    var rest = rig.Pose(RigPoseNames.Rest);
                    var a = rig.Pose(RigPoseNames.StepA);
                    var b = rig.Pose(RigPoseNames.StepB);
                    float q = t * 4f;
                    if (q < 1f) return RigPose.Lerp(a, rest, Ease(q));
                    if (q < 2f) return RigPose.Lerp(rest, b, Ease(q - 1f));
                    if (q < 3f) return RigPose.Lerp(b, rest, Ease(q - 2f));
                    return RigPose.Lerp(rest, a, Ease(q - 3f));
                }

                case Cast:
                {
                    // Wind up fast, hold, come back slower.
                    var rest = rig.Pose(RigPoseNames.Rest);
                    var cast = rig.Pose(RigPoseNames.Cast);
                    if (t < 0.18f) return RigPose.Lerp(rest, cast, Ease(t / 0.18f));
                    if (t < 0.62f) return cast;
                    return RigPose.Lerp(cast, rest, Ease((t - 0.62f) / 0.38f));
                }

                default:
                    return rig.Pose(RigPoseNames.Rest);
            }
        }

        /// <summary>0 → 1 → 0, smooth at both ends: a breath.</summary>
        private static float Wave(float t) => 0.5f - 0.5f * (float)Math.Cos(t * Math.PI * 2.0);

        private static float Ease(float t) => t * t * (3f - 2f * t);

        private static float Repeat(float t, float length) => t - (float)Math.Floor(t / length) * length;
    }
}
