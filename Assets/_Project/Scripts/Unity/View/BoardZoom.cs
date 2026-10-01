// Assets/_Project/Scripts/Unity/View/BoardZoom.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Zoom and pan over the framed board (CORE_GAMEPLAY.md, CG7): pinch and
    /// two-finger drag on touch, the mouse wheel on a desktop.
    /// </summary>
    /// <remarks>
    /// <b>The framing still owns the camera's resting place.</b> The
    /// composition root fits the board to the screen and hands the result to
    /// <see cref="SetFit"/>. This component only narrows the view inside that
    /// fit and moves it about, every frame in <c>LateUpdate</c>. At a zoom of
    /// 1 it writes back exactly the fitted pose, so nothing that frames the
    /// camera has to know it exists.
    ///
    /// <b>The view never leaves the fitted picture.</b> The pan is clamped so
    /// the zoomed view stays inside what the fit showed, and zooming back out
    /// to 1 brings the pan back to nothing. There is no reset gesture to learn:
    /// pinching out or rolling the wheel back is the reset.
    ///
    /// <b>A zoom is a narrower lens, not a closer camera.</b> The perspective
    /// camera narrows its field of view and the flat one its orthographic
    /// size, so the clip planes and the tilt the framing solved stay valid.
    ///
    /// <b>It runs before the nudge.</b> The execution order puts this
    /// <c>LateUpdate</c> ahead of <see cref="CameraNudge"/>'s, and every frame
    /// hands the nudge the zoomed pose as its base, so a shake on a big hit
    /// shakes the view the player is looking at.
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    public sealed class BoardZoom : MonoBehaviour
    {
        /// <summary>The closest the view goes: two and a half times the fitted board.</summary>
        public const float MaxZoom = 2.5f;

        /// <summary>The zoom one notch of the mouse wheel applies.</summary>
        public const float WheelStep = 1.15f;

        private CameraNudge _nudge;

        private bool _hasFit;
        private Vector3 _fitPosition;
        private bool _fitOrthographic;
        private float _fitSize;
        private float _fitFieldOfView;

        /// <summary>Half the fitted view's height at the board, in world units. The pan is measured in these.</summary>
        private float _halfHeight = 1f;

        private float _zoom = 1f;

        /// <summary>The view's offset from the fit, in fitted half-heights, along the camera's right and up.</summary>
        private Vector2 _pan;

        /// <summary>1 when the board is fitted; up to <see cref="MaxZoom"/>.</summary>
        public float Zoom => _zoom;

        public void Bind(CameraNudge nudge) => _nudge = nudge;

        /// <summary>
        /// Takes the camera's current pose as the fitted one. Call right after
        /// framing it, before this frame's <c>LateUpdate</c>.
        /// </summary>
        public void SetFit(Camera camera)
        {
            if (camera == null) return;

            _fitPosition = camera.transform.position;
            _fitOrthographic = camera.orthographic;
            _fitSize = camera.orthographicSize;
            _fitFieldOfView = camera.fieldOfView;

            if (_fitOrthographic)
            {
                _halfHeight = Mathf.Max(0.01f, _fitSize);
            }
            else
            {
                // The distance to the board along the line of sight; the board
                // lies in z = 0 and every camera here looks along +z.
                var forward = camera.transform.forward;
                float distance = forward.z > 0.0001f ? -_fitPosition.z / forward.z : 10f;
                _halfHeight = Mathf.Max(0.01f, distance * Mathf.Tan(_fitFieldOfView * 0.5f * Mathf.Deg2Rad));
            }

            _hasFit = true;
            Clamp(camera.aspect);
        }

        /// <summary>Back to the fitted board.</summary>
        public void ResetView()
        {
            _zoom = 1f;
            _pan = Vector2.zero;
        }

        /// <summary>
        /// Zooms by <paramref name="factor"/>, keeping the board point under
        /// <paramref name="screen"/> where it is: under the cursor, or between
        /// the two fingers.
        /// </summary>
        public void ZoomAt(float factor, Vector2 screen)
        {
            var camera = Camera.main;
            if (!_hasFit || camera == null || !(factor > 0f) || Screen.height <= 0) return;

            float next = Mathf.Clamp(_zoom * factor, 1f, MaxZoom);
            if (Mathf.Approximately(next, _zoom)) return;

            // The point's offset from the screen centre in visible half-heights.
            float half = Screen.height * 0.5f;
            var offset = new Vector2((screen.x - Screen.width * 0.5f) / half, (screen.y - half) / half);

            // The board point under it sits at pan + offset / zoom; keep it there.
            _pan += offset * (1f / _zoom - 1f / next);
            _zoom = next;
            Clamp(camera.aspect);
        }

        /// <summary>Drags the board by <paramref name="screenDelta"/> pixels, so it follows the fingers.</summary>
        public void PanBy(Vector2 screenDelta)
        {
            var camera = Camera.main;
            if (!_hasFit || camera == null || Screen.height <= 0) return;

            _pan -= screenDelta * (2f / (_zoom * Screen.height));
            Clamp(camera.aspect);
        }

        /// <summary>Keeps the zoomed view inside the fitted one.</summary>
        private void Clamp(float aspect)
        {
            float slack = 1f - 1f / _zoom;
            _pan.x = Mathf.Clamp(_pan.x, -Mathf.Max(0.1f, aspect) * slack, Mathf.Max(0.1f, aspect) * slack);
            _pan.y = Mathf.Clamp(_pan.y, -slack, slack);
        }

        private void LateUpdate()
        {
            if (!_hasFit) return;

            var camera = Camera.main;
            if (camera == null) return;

            var transform = camera.transform;
            var position = _fitPosition + (transform.right * _pan.x + transform.up * _pan.y) * _halfHeight;

            if (_fitOrthographic)
            {
                camera.orthographicSize = _fitSize / _zoom;
            }
            else
            {
                float tan = Mathf.Tan(_fitFieldOfView * 0.5f * Mathf.Deg2Rad) / _zoom;
                camera.fieldOfView = 2f * Mathf.Atan(tan) * Mathf.Rad2Deg;
            }

            transform.position = position;
            if (_nudge != null) _nudge.SetBase(position);
        }
    }
}