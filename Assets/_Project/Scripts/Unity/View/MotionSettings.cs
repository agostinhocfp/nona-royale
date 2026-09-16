// Assets/_Project/Scripts/Unity/View/MotionSettings.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>How fast every seat's actions animate (MOTION.md decision 4). Remembered between sessions.</summary>
    public enum AnimationSpeed
    {
        Normal = 0,
        Fast = 1
    }

    /// <summary>
    /// The motion choices every animated view reads: Reduced motion, the
    /// animation speed, and the CPU hurry (MOTION.md increment MO2).
    /// </summary>
    /// <remarks>
    /// <b>One object, owned by the composition root</b> and handed to the
    /// pieces, the dice, the cast tells, the camera nudge and the hit-stop at
    /// bind time. No singleton: the root updates it each frame from its
    /// settings and the Space key, and the views read it when they animate.
    ///
    /// <b>CPU speed is separate</b> (<see cref="BotSpeed"/>): it paces CPU
    /// thinking only.
    /// </remarks>
    public sealed class MotionSettings
    {
        /// <summary>How much faster Fast is than Normal.</summary>
        public const float FastRate = 1.75f;

        /// <summary>How much shorter holds and tweens are under Reduced motion.</summary>
        public const float ReducedTween = 0.6f;

        /// <summary>No shake, no hit-stop, no idle sway, no hop; shorter tweens.</summary>
        public bool ReducedMotion { get; set; }

        public AnimationSpeed Speed { get; set; } = AnimationSpeed.Normal;

        /// <summary>A multiplier the root sets while Space hurries a CPU turn. 1 otherwise.</summary>
        public float Hurry { get; set; } = 1f;

        /// <summary>How fast animation clocks run: the speed setting times the hurry.</summary>
        public float Rate => (Speed == AnimationSpeed.Fast ? FastRate : 1f) * (Hurry > 0f ? Hurry : 1f);

        /// <summary>A hold or tween length, shortened under Reduced motion. Not divided by <see cref="Rate"/>; clocks do that.</summary>
        public float Tween(float seconds) => ReducedMotion ? seconds * ReducedTween : seconds;

        /// <summary>Whether shake and hit-stop may play.</summary>
        public bool Impacts => !ReducedMotion;
    }
}