// Assets/_Project/Scripts/Unity/View/CooldownSweepGeometry.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The arithmetic and the outline behind <see cref="CooldownSweep"/>:
    /// how much of a card the veil covers, where its hand points, and the
    /// chamfered outline its fan is clipped to.
    /// </summary>
    /// <remarks>
    /// Kept apart from the graphic so the edit-mode tests can check it
    /// without referencing the UI assembly the graphic derives from.
    /// Angles are degrees clockwise from twelve o'clock.
    /// </remarks>
    public static class CooldownSweepGeometry
    {
        /// <summary>The corner cut of <see cref="DecoSprites.ButtonFill"/>, in canvas units.</summary>
        public const float ButtonCut = 6f;

        /// <summary>Degrees between fan points along the outline. The corners are always added exactly.</summary>
        public const float StepDegrees = 4f;

        /// <summary>The tray's key for one operator's ability.</summary>
        public static long KeyOf(int operatorId, int abilityId) => ((long)operatorId << 32) | (uint)abilityId;

        /// <summary>
        /// The share of a cooldown still to run, 0 (ready) to 1 (just cast).
        /// The turn of the cast counts, which is why a cooldown of N is out of
        /// N + 1 (<c>AbilityResolver.PutOnCooldown</c>).
        /// </summary>
        public static float Fraction(int turnsLeft, int cooldown)
        {
            if (cooldown <= 0 || turnsLeft <= 0) return 0f;
            return Mathf.Clamp01(turnsLeft / (float)(cooldown + 1));
        }

        /// <summary>
        /// Where the hand points for a veil of <paramref name="fraction"/>, in
        /// degrees clockwise from twelve o'clock. The veil runs from here
        /// clockwise round to twelve.
        /// </summary>
        public static float HandAngle(float fraction) => (1f - Mathf.Clamp01(fraction)) * 360f;

        /// <summary>
        /// The point on a chamfered box's outline in the direction
        /// <paramref name="degrees"/> clockwise from up, measured from its
        /// centre. <paramref name="half"/> is half its size and
        /// <paramref name="cut"/> its corner cut.
        /// </summary>
        /// <remarks>
        /// The box is |x| ≤ hx, |y| ≤ hy and |x| + |y| ≤ hx + hy − cut, all
        /// convex, so a ray from the centre leaves it at the nearest of the
        /// three limits.
        /// </remarks>
        public static Vector2 Outline(float degrees, Vector2 half, float cut)
        {
            float radians = degrees * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            float ax = Mathf.Abs(direction.x), ay = Mathf.Abs(direction.y);

            cut = Mathf.Clamp(cut, 0f, Mathf.Min(half.x, half.y));
            float t = float.MaxValue;
            if (ax > 1e-6f) t = Mathf.Min(t, half.x / ax);
            if (ay > 1e-6f) t = Mathf.Min(t, half.y / ay);
            t = Mathf.Min(t, (half.x + half.y - cut) / (ax + ay));

            return direction * t;
        }

        /// <summary>
        /// The angles a veil from <paramref name="hand"/> round to 360 is
        /// fanned at: every <see cref="StepDegrees"/>, plus each corner of the
        /// outline inside the span, so the chamfers stay straight.
        /// </summary>
        public static void FanAngles(float hand, Vector2 half, float cut, List<float> into)
        {
            into.Clear();
            if (hand >= 360f) return;

            into.Add(hand);
            for (float a = Mathf.Ceil(hand / StepDegrees) * StepDegrees; a < 360f; a += StepDegrees)
                if (a > hand) into.Add(a);

            cut = Mathf.Clamp(cut, 0f, Mathf.Min(half.x, half.y));
            float hx = half.x, hy = half.y;
            AddCorner(into, hand, hx - cut, hy);
            AddCorner(into, hand, hx, hy - cut);
            AddCorner(into, hand, hx, -(hy - cut));
            AddCorner(into, hand, hx - cut, -hy);
            AddCorner(into, hand, -(hx - cut), -hy);
            AddCorner(into, hand, -hx, -(hy - cut));
            AddCorner(into, hand, -hx, hy - cut);
            AddCorner(into, hand, -(hx - cut), hy);

            into.Add(360f);
            into.Sort();
        }

        private static void AddCorner(List<float> into, float hand, float x, float y)
        {
            float degrees = Mathf.Atan2(x, y) * Mathf.Rad2Deg;
            if (degrees < 0f) degrees += 360f;
            if (degrees > hand && degrees < 360f) into.Add(degrees);
        }
    }
}
