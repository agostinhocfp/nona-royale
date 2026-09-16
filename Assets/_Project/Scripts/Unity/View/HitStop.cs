// Assets/_Project/Scripts/Unity/View/HitStop.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A hit-stop: game time nearly freezes for a few real milliseconds on a
    /// big hit (MOTION.md decision 3). Off under Reduced motion.
    /// </summary>
    /// <remarks>
    /// <b>It never fights the pause.</b> It only slows time that is running
    /// at normal speed, and it only restores the scale it set itself: if the
    /// pause menu stopped the clock in between, the pause owns it. The pause
    /// menu, for its part, never resumes into a slowed clock.
    /// </remarks>
    public sealed class HitStop : MonoBehaviour
    {
        /// <summary>Game-time scale during the stop. Not zero, so nothing that divides by delta breaks.</summary>
        private const float StopScale = 0.02f;

        private MotionSettings _motion;
        private float _remaining;
        private bool _active;

        public void Bind(MotionSettings motion) => _motion = motion;

        /// <summary>Freezes game time for <paramref name="seconds"/> real seconds.</summary>
        public void Stop(float seconds)
        {
            if (_motion != null && !_motion.Impacts) return;

            if (!_active)
            {
                // A paused or otherwise altered clock is not ours to touch.
                if (!Mathf.Approximately(Time.timeScale, 1f)) return;

                Time.timeScale = StopScale;
                _active = true;
            }

            _remaining = Mathf.Max(_remaining, seconds);
        }

        /// <summary>Ends a stop now. Safe to call at any time.</summary>
        public void Release()
        {
            if (!_active) return;

            _active = false;
            _remaining = 0f;
            if (Mathf.Approximately(Time.timeScale, StopScale)) Time.timeScale = 1f;
        }

        private void Update()
        {
            if (!_active) return;

            // The pause took the clock: the stop is over, and the pause will restore time.
            if (!Mathf.Approximately(Time.timeScale, StopScale))
            {
                _active = false;
                _remaining = 0f;
                return;
            }

            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f) Release();
        }

        private void OnDisable() => Release();
    }
}