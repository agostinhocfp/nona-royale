// Assets/_Project/Scripts/Unity/View/LightPulse.cs
using System;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The slow swell of the room's lights (LIGHTING.md, LT1). Plain C#, so
    /// the curve is testable outside the editor.
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