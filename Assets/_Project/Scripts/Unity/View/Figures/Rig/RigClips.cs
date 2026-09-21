// Assets/_Project/Scripts/Unity/View/Figures/Rig/RigClips.cs
using System;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Poses over time (OPERATOR_LOOKBOOK.md, LB5): the idle loop, the step
    /// cycle, the cast and the seated loop, as blends between a rig's named
    /// poses. Plain C#, so the judging window previews exactly what
    /// <see cref="RigAnimator"/> plays on the board.
    /// </summary>
    /// <remarks>
    /// <b>Fitted to MOTION.md in LB5b.</b>
    /// <list type="bullet">
    /// <item>The idle is the old breathing's clock: <c>sin(t · 1.8)</c>, one
    /// breath in about 3.5 s.</item>
    /// <item>The seated loop is the old sway's period, about 7 s: one breath
    /// at the idle's pace, then the seated activity (LB5c), eased in, held
    /// and eased out.</item>
    /// <item>The step is one step per cell at the hop's 8 cells a second, so
    /// a full cycle (both feet) is two cells, 0.25 s. On the board it is
    /// driven by distance, not time (<see cref="StepPhase"/>), so the feet
    /// keep pace with the ground at any speed setting.</item>
    /// <item>The cast winds up in the cast tell's reach delay (0.12 s),
    /// holds through the beam and its linger (0.5 s in all), then comes back.
    /// On the board it is stretched to the tell actually played
    /// (<see cref="CastLengthFor"/>).</item>
    /// <item>The hit is the hit's hold (0.3 s): a snap back, then a slower
    /// return. The knockout folds in 0.25 s, ahead of the shatter's 0.45 s.</item>
    /// </list>
    /// </remarks>
    public static class RigClips
    {
        public const string Idle = "idle";
        public const string Step = "step";
        public const string Cast = "cast";
        public const string Seated = "seated";
        public const string Hit = "hit";
        public const string Knockout = "knockout";

        public static readonly string[] All = { Idle, Step, Cast, Seated, Hit, Knockout };

        /// <summary>Where the cast reaches its pose, and where it starts back, as fractions of the clip.</summary>
        public const float CastReach = 0.18f;
        public const float CastRelease = 0.62f;

        /// <summary>Where the hit's snap back peaks, as a fraction of the clip.</summary>
        public const float HitPeak = 0.2f;

        /// <summary>The hop's pace in MOTION.md (<c>OperatorPiece.HopsPerSecond</c>): one step per cell.</summary>
        public const float CellsPerSecond = 8f;

        /// <summary>Cells per full step cycle: near foot, then far foot.</summary>
        public const float CellsPerCycle = 2f;

        /// <summary>Seconds per cycle (loops) or in all (the cast).</summary>
        public static float Length(string clip)
        {
            switch (clip)
            {
                case Idle: return (float)(Math.PI * 2.0 / 1.8);
                case Step: return CellsPerCycle / CellsPerSecond;
                case Cast: return 0.8f;
                case Seated: return (float)(Math.PI * 2.0 / 0.9);
                case Hit: return 0.3f;
                case Knockout: return 0.25f;
                default: return 1f;
            }
        }

        public static bool Loops(string clip) => clip == Idle || clip == Step || clip == Seated;

        /// <summary>
        /// The cast's length so that its hold ends as a cast tell of
        /// <paramref name="tellSeconds"/> does.
        /// </summary>
        public static float CastLengthFor(float tellSeconds) => Math.Max(0.05f, tellSeconds) / CastRelease;

        /// <summary>Whether the device shows its cyan tell at this point of the cast: from the reach to the release.</summary>
        public static bool CastPowered(float phase) => phase >= CastReach && phase < CastRelease;

        /// <summary>
        /// Where in the step cycle a walk is after <paramref name="cells"/>
        /// cells, 0..1. Offset so every cell is crossed contact to contact and
        /// arrived on at the passing pose, which is the rest pose: a walk
        /// starts and ends with the feet together, and the idle picks up
        /// without a jump.
        /// </summary>
        public static float StepPhase(float cells) => Repeat(cells / CellsPerCycle + 0.25f, 1f);

        /// <summary>The pose at <paramref name="seconds"/> into the clip; loops wrap, the cast holds its end.</summary>
        public static RigPose Sample(OperatorRig rig, string clip, float seconds)
        {
            Blend(rig, clip, Phase(clip, seconds), out var a, out var b, out float t);
            return ReferenceEquals(a, b) ? a : RigPose.Lerp(a, b, t);
        }

        /// <summary>A clip's phase, 0..1, at <paramref name="seconds"/> in.</summary>
        public static float Phase(string clip, float seconds)
        {
            float length = Length(clip);
            return Loops(clip) ? Repeat(seconds, length) / length : Math.Min(Math.Max(seconds / length, 0f), 1f);
        }

        /// <summary>
        /// The clip at <paramref name="phase"/> as the two poses it sits
        /// between and how far along, so the board can place bones without
        /// building a pose each frame.
        /// </summary>
        public static void Blend(OperatorRig rig, string clip, float phase, out RigPose a, out RigPose b, out float t)
        {
            switch (clip)
            {
                case Idle:
                    a = rig.Pose(RigPoseNames.IdleA);
                    b = rig.Pose(RigPoseNames.IdleB);
                    t = Wave(phase);
                    return;

                case Seated:
                {
                    // A breath, then the activity: in, held, out.
                    var seated = rig.Pose(RigPoseNames.Seated);
                    if (phase < 0.5f)
                    {
                        a = seated;
                        b = rig.Pose(RigPoseNames.SeatedB);
                        t = Wave(phase * 2f);
                        return;
                    }

                    var look = rig.Pose(RigPoseNames.SeatedLook);
                    float q = (phase - 0.5f) * 2f;
                    if (q < 0.3f) { a = seated; b = look; t = Ease(q / 0.3f); }
                    else if (q < 0.7f) { a = look; b = look; t = 0f; }
                    else { a = look; b = seated; t = Ease((q - 0.7f) / 0.3f); }
                    return;
                }

                case Hit:
                {
                    // Snapped back fast, eased back slower.
                    var rest = rig.Pose(RigPoseNames.Rest);
                    var hit = rig.Pose(RigPoseNames.Hit);
                    if (phase < HitPeak) { a = rest; b = hit; t = EaseOut(phase / HitPeak); }
                    else { a = hit; b = rest; t = Ease((phase - HitPeak) / (1f - HitPeak)); }
                    return;
                }

                case Knockout:
                    a = rig.Pose(RigPoseNames.Rest);
                    b = rig.Pose(RigPoseNames.Knockout);
                    t = EaseIn(phase);
                    return;

                case Step:
                {
                    // Contact A, pass, contact B, pass.
                    var rest = rig.Pose(RigPoseNames.Rest);
                    var stepA = rig.Pose(RigPoseNames.StepA);
                    var stepB = rig.Pose(RigPoseNames.StepB);
                    float q = phase * 4f;
                    if (q < 1f) { a = stepA; b = rest; t = Ease(q); }
                    else if (q < 2f) { a = rest; b = stepB; t = Ease(q - 1f); }
                    else if (q < 3f) { a = stepB; b = rest; t = Ease(q - 2f); }
                    else { a = rest; b = stepA; t = Ease(Math.Min(q - 3f, 1f)); }
                    return;
                }

                case Cast:
                    BlendCast(rig.Pose(RigPoseNames.Rest), rig.Pose(RigPoseNames.Cast), phase, out a, out b, out t);
                    return;

                default:
                    a = b = rig.Pose(RigPoseNames.Rest);
                    t = 0f;
                    return;
            }
        }

        /// <summary>
        /// The cast between <paramref name="rest"/> and a cast pose, which on
        /// the board is the recipe's cast aimed at its target: wind up fast,
        /// hold, come back slower.
        /// </summary>
        public static void BlendCast(RigPose rest, RigPose cast, float phase, out RigPose a, out RigPose b, out float t)
        {
            if (phase < CastReach) { a = rest; b = cast; t = Ease(phase / CastReach); }
            else if (phase < CastRelease) { a = cast; b = cast; t = 0f; }
            else { a = cast; b = rest; t = Ease(Math.Min(1f, (phase - CastRelease) / (1f - CastRelease))); }
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        private static float EaseIn(float t) => t * t;

        /// <summary>0 → 1 → 0, smooth at both ends: a breath.</summary>
        private static float Wave(float t) => 0.5f - 0.5f * (float)Math.Cos(t * Math.PI * 2.0);

        private static float Ease(float t) => t * t * (3f - 2f * t);

        private static float Repeat(float t, float length) => t - (float)Math.Floor(t / length) * length;
    }
}
