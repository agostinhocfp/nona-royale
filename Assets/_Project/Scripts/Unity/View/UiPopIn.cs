// Assets/_Project/Scripts/Unity/View/UiPopIn.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Pops a HUD element in once: it grows from a little small, overshoots,
    /// and settles at full size (MOTION.md increment MO1: the tray's dice
    /// arriving). Removes itself when done.
    /// </summary>
    /// <remarks>
    /// Unscaled time, like the other HUD motion, so an element rebuilt under
    /// the pause menu does not stay stuck at a half size.
    /// </remarks>
    public sealed class UiPopIn : MonoBehaviour
    {
        private const float Seconds = 0.28f;
        private const float StartScale = 0.55f;
        private const float Overshoot = 1.18f;

        private float _time;

        /// <summary>Starts the pop on <paramref name="target"/>, after <paramref name="delay"/> unscaled seconds.</summary>
        public static void On(Component target, float delay = 0f)
        {
            if (target == null) return;

            var pop = target.gameObject.AddComponent<UiPopIn>();
            pop._time = -Mathf.Max(0f, delay);
            target.transform.localScale = Vector3.one * StartScale;
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            if (_time < 0f) return;

            float t = Mathf.Clamp01(_time / Seconds);

            // Up past full size in the first 60%, back down to it in the rest.
            float scale = t < 0.6f
                ? Mathf.Lerp(StartScale, Overshoot, t / 0.6f)
                : Mathf.Lerp(Overshoot, 1f, (t - 0.6f) / 0.4f);

            transform.localScale = Vector3.one * scale;

            if (t < 1f) return;

            transform.localScale = Vector3.one;
            Destroy(this);
        }
    }
}