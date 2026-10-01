// Assets/_Project/Scripts/Unity/View/TouchGestures.cs
using System;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Reads the board's touches each frame (CORE_GAMEPLAY.md, CG7): one
    /// finger lifted where it went down is a tap, and two fingers pinch and
    /// drag the board through <see cref="BoardZoom"/>.
    /// </summary>
    /// <remarks>
    /// <b>A tap counts on release, not on press.</b> A pinch starts with one
    /// finger a few milliseconds before the other, and a tap that fired on
    /// press would select, move or aim with that first finger. So a touch is
    /// only a tap if it ends without a second finger joining and without
    /// travelling, and a touch that started over the HUD is never a board tap.
    /// The mouse keeps clicking on press: it has no second finger to wait for.
    ///
    /// <b>One finger does not pan.</b> A drag with one finger simply stops
    /// being a tap, so a slip never moves the board out from under a choice.
    /// Panning takes two fingers, which is also when zooming happens, so the
    /// two arrive together.
    /// </remarks>
    public sealed class TouchGestures
    {
        /// <summary>How far a finger may travel and still tap, in inches; 12 px when the screen has no DPI.</summary>
        private const float TapSlopInches = 0.1f;

        private bool _candidate;
        private Vector2 _start;

        private bool _pinching;
        private Vector2 _lastMid;
        private float _lastDistance;

        /// <summary>True on the frame a one-finger tap ends. <c>Input.mousePosition</c> is where it ended.</summary>
        public bool Tapped { get; private set; }

        /// <summary>True while two or more fingers are down.</summary>
        public bool Pinching => _pinching;

        /// <summary>
        /// Reads this frame's touches. <paramref name="overHud"/> answers
        /// whether the pointer is over the HUD as a finger lands;
        /// <paramref name="zoom"/> takes the pinch, or nothing when null.
        /// </summary>
        public void Tick(BoardZoom zoom, Func<bool> overHud)
        {
            Tapped = false;
            int count = Input.touchCount;

            if (count >= 2)
            {
                _candidate = false;

                var a = Input.GetTouch(0).position;
                var b = Input.GetTouch(1).position;
                var mid = (a + b) * 0.5f;
                float distance = Vector2.Distance(a, b);

                if (_pinching && zoom != null)
                {
                    if (_lastDistance > 1f && distance > 1f) zoom.ZoomAt(distance / _lastDistance, mid);
                    zoom.PanBy(mid - _lastMid);
                }

                _pinching = true;
                _lastMid = mid;
                _lastDistance = distance;
                return;
            }

            _pinching = false;

            if (count == 0)
            {
                _candidate = false;
                return;
            }

            var touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _candidate = overHud == null || !overHud();
                    _start = touch.position;
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (_candidate && (touch.position - _start).magnitude > Slop) _candidate = false;
                    break;

                case TouchPhase.Ended:
                    if (_candidate && (touch.position - _start).magnitude <= Slop) Tapped = true;
                    _candidate = false;
                    break;

                default:
                    _candidate = false;
                    break;
            }
        }

        private static float Slop => Screen.dpi > 0f ? Mathf.Max(12f, Screen.dpi * TapSlopInches) : 12f;
    }
}