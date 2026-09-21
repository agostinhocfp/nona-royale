// Assets/_Project/Scripts/Unity/View/Figures/Rig/RigAnimator.cs
using System;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Which clip a rigged piece plays, and where in it (OPERATOR_LOOKBOOK.md,
    /// LB5b and LB5c). Plain C#: the piece tells it what is happening, and it
    /// answers with two poses and a blend, which <c>RigView</c> places.
    /// </summary>
    /// <remarks>
    /// <b>What wins</b>, highest first: seated (the seated loop, with its
    /// activity), the knockout's fold, the hit's recoil, the rise, the walk,
    /// the cast, the idle. The walk beats the cast because the cast's return
    /// is cosmetic and a walk that follows it must not wait.
    ///
    /// <b>Reduced motion</b> keeps the step, since it is how the figure
    /// walks rather than decoration on top of it, and drops the idle and the
    /// seated loop, as MO2 dropped the breathing and the sway: the figure
    /// holds its rest or seated pose. The rise, the cast, the recoil and the
    /// fold stay; they are what the events look like, and the piece shortens
    /// them through <c>MotionSettings.Tween</c> as MO2 shortens every tween.
    ///
    /// The clocks are fed by the piece as MO2's were: the idle in scaled time,
    /// every event in scaled time times <c>MotionSettings.Rate</c>. The step
    /// has no clock of its own; it follows the cells covered.
    /// </remarks>
    public sealed class RigAnimator
    {
        /// <summary>How deep the crouch a rise starts from is, toward the knockout pose.</summary>
        public const float CrouchDepth = 0.45f;

        /// <summary>The steepest a cast is aimed up or down, in degrees, however steep the line to the target.</summary>
        public const float MaxAim = 30f;

        private float _idle;
        private readonly Clock _rise = new Clock();
        private readonly Clock _cast = new Clock();
        private readonly Clock _hit = new Clock();
        private readonly Clock _knockout = new Clock();
        private RigPose _castPose;

        /// <param name="phaseSeconds">Where this piece's idle starts, so pieces breathe out of step.</param>
        public RigAnimator(float phaseSeconds = 0f)
        {
            _idle = Math.Max(0f, phaseSeconds);
        }

        public bool Seated { get; set; }

        public bool ReducedMotion { get; set; }

        /// <summary>Cells covered in the current walk, or null when the piece is not walking.</summary>
        public float? WalkCells { get; set; }

        public bool Rising => _rise.Running;

        public bool Casting => _cast.Running;

        public bool Recoiling => _hit.Running;

        /// <summary>True from the knockout's start until <see cref="Clear"/>: the fold holds at its end.</summary>
        public bool KnockedOut => _knockout.Started;

        /// <summary>True once the fold has finished: time for the shatter.</summary>
        public bool Folded => _knockout.Started && !_knockout.Running;

        /// <summary>Whether the device shows its cyan tell now: from the cast's reach to its release, standing only.</summary>
        public bool Powered => !Seated && !_knockout.Started && !_hit.Running && !_rise.Running &&
                               WalkCells == null && _cast.Running && RigClips.CastPowered(_cast.Phase);

        /// <summary>
        /// Moves the clocks on: the idle by <paramref name="idleSeconds"/>
        /// (scaled time, as MO2's breathing), every event by
        /// <paramref name="ratedSeconds"/> (scaled time times the animation
        /// speed, as MO2's pop).
        /// </summary>
        public void Advance(float idleSeconds, float ratedSeconds)
        {
            if (idleSeconds > 0f) _idle += idleSeconds;
            if (ratedSeconds <= 0f) return;

            _rise.Advance(ratedSeconds);
            _cast.Advance(ratedSeconds);
            _hit.Advance(ratedSeconds);
            _knockout.Advance(ratedSeconds);
        }

        /// <summary>Starts the stand from a crouch, over <paramref name="seconds"/>.</summary>
        public void Rise(float seconds) => _rise.Start(seconds);

        /// <summary>
        /// Plays <paramref name="aimedCast"/>, the rig's cast turned toward the
        /// target (<see cref="Aimed"/>), over <paramref name="seconds"/>.
        /// </summary>
        public void Cast(RigPose aimedCast, float seconds)
        {
            _castPose = aimedCast;
            _cast.Start(seconds);
        }

        /// <summary>Drops a cast in progress: a walk that follows it, a facing change.</summary>
        public void StopCast() => _cast.Stop();

        /// <summary>The recoil, over <paramref name="seconds"/>.</summary>
        public void Hit(float seconds) => _hit.Start(seconds);

        /// <summary>The fold before the shatter, over <paramref name="seconds"/>; it holds its end until <see cref="Clear"/>.</summary>
        public void Knockout(float seconds) => _knockout.Start(seconds);

        /// <summary>Drops a rise in progress (a snap).</summary>
        public void Stop() => _rise.Stop();

        /// <summary>Drops every event: the piece is back, seated or snapped.</summary>
        public void Clear()
        {
            _rise.Stop();
            _cast.Stop();
            _hit.Stop();
            _knockout.Stop();
        }

        /// <summary>
        /// The two poses of <paramref name="rig"/> to blend now, and how far.
        /// <paramref name="crouch"/> is the rig's crouch for this facing
        /// (<see cref="Crouch"/>), built once rather than every frame.
        /// </summary>
        public void Sample(OperatorRig rig, RigPose crouch, out RigPose a, out RigPose b, out float t)
        {
            if (Seated)
            {
                if (ReducedMotion)
                {
                    a = b = rig.Pose(RigPoseNames.Seated);
                    t = 0f;
                }
                else
                {
                    RigClips.Blend(rig, RigClips.Seated, RigClips.Phase(RigClips.Seated, _idle), out a, out b, out t);
                }

                return;
            }

            if (_knockout.Started)
            {
                RigClips.Blend(rig, RigClips.Knockout, _knockout.Phase, out a, out b, out t);
                return;
            }

            if (_hit.Running)
            {
                RigClips.Blend(rig, RigClips.Hit, _hit.Phase, out a, out b, out t);
                return;
            }

            if (_rise.Running && crouch != null)
            {
                a = crouch;
                b = rig.Pose(RigPoseNames.Rest);
                t = Overshoot(_rise.Phase);
                return;
            }

            if (WalkCells.HasValue)
            {
                RigClips.Blend(rig, RigClips.Step, RigClips.StepPhase(WalkCells.Value), out a, out b, out t);
                return;
            }

            if (_cast.Running)
            {
                RigClips.BlendCast(rig.Pose(RigPoseNames.Rest), _castPose ?? rig.Pose(RigPoseNames.Cast), _cast.Phase,
                    out a, out b, out t);
                return;
            }

            if (ReducedMotion)
            {
                a = b = rig.Pose(RigPoseNames.Rest);
                t = 0f;
                return;
            }

            RigClips.Blend(rig, RigClips.Idle, RigClips.Phase(RigClips.Idle, _idle), out a, out b, out t);
        }

        /// <summary>The crouch a rise starts from: part of the way down toward the knockout.</summary>
        public static RigPose Crouch(OperatorRig rig) =>
            RigPose.Lerp(rig.Pose(RigPoseNames.Rest), rig.Pose(RigPoseNames.Knockout), CrouchDepth);

        /// <summary>
        /// The rig's cast with the casting arm (<see cref="OperatorRig.AimBone"/>) turned
        /// <paramref name="elevation"/> degrees further up toward a target
        /// above, or down toward one below, clamped to <see cref="MaxAim"/>.
        /// A left-facing rig's poses are mirrored, so its turn is too.
        /// </summary>
        public static RigPose Aimed(OperatorRig rig, float elevation)
        {
            var cast = rig.Pose(RigPoseNames.Cast);
            float aim = Math.Max(-MaxAim, Math.Min(MaxAim, elevation));
            if (Math.Abs(aim) < 0.01f) return cast;

            var arm = cast.Get(rig.AimBone);
            float turn = rig.FacesLeft ? -aim : aim;
            return cast.Copy(RigPoseNames.Cast + ".aimed")
                .Turn(rig.AimBone, arm.Degrees + turn, arm.Dx, arm.Dy, arm.Scale);
        }

        /// <summary>
        /// How far up (positive) or down a target sits from the caster, in
        /// degrees, from the board positions: the line's angle above the
        /// horizontal, taken across however far apart they are sideways.
        /// </summary>
        public static float Elevation(float fromX, float fromY, float toX, float toY)
        {
            float dx = Math.Abs(toX - fromX), dy = toY - fromY;
            if (dx < 1e-4f && Math.Abs(dy) < 1e-4f) return 0f;
            return (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
        }

        /// <summary>
        /// Which way a figure faces to look from <paramref name="fromX"/>
        /// toward <paramref name="toX"/>: true for left. Inside
        /// <paramref name="deadZone"/> it keeps <paramref name="facesLeft"/>, so
        /// a piece straight above or below its target does not flicker.
        /// </summary>
        public static bool FaceLeft(float fromX, float toX, bool facesLeft, float deadZone)
        {
            float dx = toX - fromX;
            if (dx > deadZone) return false;
            if (dx < -deadZone) return true;
            return facesLeft;
        }

        /// <summary>Eases out past the end and settles: a stand with a little spring in it.</summary>
        private static float Overshoot(float t)
        {
            const float s = 1.2f;
            float u = t - 1f;
            return 1f + u * u * ((s + 1f) * u + s);
        }

        /// <summary>One event's clock: started, how far, finished.</summary>
        private sealed class Clock
        {
            private float _seconds = -1f;
            private float _length = 1f;

            public bool Started => _seconds >= 0f;
            public bool Running => _seconds >= 0f && _seconds < _length;
            public float Phase => _seconds < 0f ? 0f : Math.Min(1f, _seconds / _length);

            public void Start(float length)
            {
                _length = Math.Max(0.01f, length);
                _seconds = 0f;
            }

            public void Advance(float seconds)
            {
                if (_seconds >= 0f && _seconds < _length) _seconds += seconds;
            }

            public void Stop() => _seconds = -1f;
        }
    }
}
