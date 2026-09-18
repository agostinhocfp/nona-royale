// Assets/_Project/Scripts/Unity/View/CameraNudge.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A small, quickly damped camera shove on big hits and knockouts
    /// (MOTION.md decision 3). Off under Reduced motion.
    /// </summary>
    /// <remarks>
    /// <b>The framing owns the camera's resting place.</b> The composition
    /// root frames the board and passes the result to <see cref="SetBase"/>;
    /// this component only adds a decaying offset on top, in
    /// <c>LateUpdate</c>, and puts the camera back on its base when the
    /// nudge is over. Scaled time, so a pause holds the camera still.
    /// </remarks>
    public sealed class CameraNudge : MonoBehaviour
    {
        private const float Seconds = 0.22f;

        /// <summary>Oscillations over the nudge.</summary>
        private const float Wobbles = 2.5f;

        private MotionSettings _motion;
        private Vector3 _base;
        private bool _hasBase;
        private Vector2 _direction;
        private float _strength;
        private float _age = Seconds;

        public void Bind(MotionSettings motion) => _motion = motion;

        /// <summary>The camera's framed position. Call whenever the framing moves it.</summary>
        public void SetBase(Vector3 position)
        {
            _base = position;
            _hasBase = true;
        }

        /// <summary>Shoves the camera along <paramref name="direction"/> by <paramref name="strength"/> world units, then settles.</summary>
        public void Nudge(Vector2 direction, float strength)
        {
            if (_motion != null && !_motion.Impacts) return;

            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
            _strength = strength;
            _age = 0f;
        }

        /// <summary>Stops any nudge and returns the camera to its base.</summary>
        public void Stop()
        {
            _age = Seconds;
            Apply(Vector2.zero);
        }

        private void LateUpdate()
        {
            if (_age >= Seconds) return;

            _age += Time.deltaTime * (_motion != null ? _motion.Rate : 1f);
            float t = Mathf.Clamp01(_age / Seconds);

            float wave = Mathf.Sin(t * Mathf.PI * Wobbles) * (1f - t);
            Apply(t >= 1f ? Vector2.zero : _direction * (_strength * wave));
        }

        /// <remarks>
        /// <b>The shove is along the screen, not along the world</b>
        /// (VISUAL_PASS.md, V1). A nudge is a camera shake: "down and left"
        /// means down and left of the picture. Under the flat camera the screen
        /// axes are the world's x and y, so adding the offset straight to the
        /// position was the same thing; under the tilted camera it is not, and
        /// a downward shake would have pushed the camera into the table. The
        /// camera's own right and up are the screen axes in both modes.
        /// </remarks>
        private void Apply(Vector2 offset)
        {
            var camera = Camera.main;
            if (camera == null || !_hasBase) return;

            var transform = camera.transform;
            camera.transform.position = _base + transform.right * offset.x + transform.up * offset.y;
        }
    }
}