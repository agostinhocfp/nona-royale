// Assets/_Project/Scripts/Unity/View/LightPulse.cs
using System;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The slow swell of the room's lights (LIGHTING.md, LT1) and the shape of
    /// a one-shot flash (LT2). Plain C#, so the curves are testable outside
    /// the editor.
    /// </summary>
    public static class LightPulse
    {
        /// <summary>
        /// A light's intensity multiplier at <paramref name="time"/> seconds:
        /// 1 ± <paramref name="depth"/>, one cycle every
        /// <paramref name="period"/> seconds, offset by
        /// <paramref name="phase"/> cycles. Exactly 1 under Reduced motion,
        /// or when the period or depth is not positive.
        /// </summary>
        public static float Breath(double time, float period, float depth, float phase, bool reduced)
        {
            if (reduced || !(period > 0f) || !(depth > 0f)) return 1f;

            double angle = 2.0 * Math.PI * (time / period + phase);
            return 1f + Math.Min(depth, 1f) * (float)Math.Sin(angle);
        }

        /// <summary>How bright a flash peaks under Reduced motion, as a fraction of its full peak (LT2).</summary>
        public const float ReducedFlashPeak = 0.5f;

        /// <summary>
        /// A one-shot flash's intensity multiplier, <paramref name="elapsed"/>
        /// seconds into a flash lasting <paramref name="duration"/> (LT2): a
        /// quick rise to 1, then a falling curve to 0. Outside the flash, or
        /// with no duration, it is 0.
        /// </summary>
        /// <remarks>
        /// Under Reduced motion the rise is slower and the peak is
        /// <see cref="ReducedFlashPeak"/>: the moment still reads, without
        /// the punch.
        /// </remarks>
        public static float Flash(double elapsed, float duration, bool reduced)
        {
            if (!(duration > 0f) || !(elapsed >= 0.0) || elapsed >= duration) return 0f;

            double t = elapsed / duration;
            double attack = reduced ? 0.3 : 0.08;
            double peak = reduced ? ReducedFlashPeak : 1.0;

            if (t < attack)
            {
                double rise = t / attack;
                return (float)(peak * rise * rise * (3.0 - 2.0 * rise));
            }

            double fall = 1.0 - (t - attack) / (1.0 - attack);
            return (float)(peak * fall * fall);
        }

        /// <summary>
        /// A phase in [0, 1) for the <paramref name="index"/>-th light, spread
        /// by the golden ratio so neighbours never swell together.
        /// </summary>
        public static float Phase(int index)
        {
            const double golden = 0.6180339887498949;
            double value = Math.Abs(index) * golden;
            return (float)(value - Math.Floor(value));
        }
    }
}