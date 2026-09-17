// Assets/_Project/Scripts/Unity/View/UiEasing.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>The easing curves HUD motion uses (UI_MOTION.md increment U1).</summary>
    public enum UiEase
    {
        Linear,
        OutQuad,
        OutCubic,
        InQuad,

        /// <summary>A small overshoot. Falls back to <see cref="OutCubic"/> under Reduced motion.</summary>
        OutBack,
    }

    /// <summary>
    /// The easing math, kept plain C# with no Unity types so the edit-mode
    /// harness can pin the curves (UI_MOTION.md increment U1).
    /// </summary>
    public static class UiEasing
    {
        /// <summary>The eased 0…1 of a raw one. Clamps outside 0…1.</summary>
        public static float Evaluate(UiEase ease, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            switch (ease)
            {
                case UiEase.OutQuad:
                    return 1f - (1f - t) * (1f - t);
                case UiEase.OutCubic:
                    float u = 1f - t;
                    return 1f - u * u * u;
                case UiEase.InQuad:
                    return t * t;
                case UiEase.OutBack:
                    const float c = 1.70158f;
                    float v = t - 1f;
                    return 1f + (c + 1f) * v * v * v + c * v * v;
                default:
                    return t;
            }
        }
    }
}
