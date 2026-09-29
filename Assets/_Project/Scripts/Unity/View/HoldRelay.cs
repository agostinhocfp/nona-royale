// Assets/_Project/Scripts/Unity/View/HoldRelay.cs
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Reports a press held on a uGUI element, and a short tap on one, to
    /// callbacks (CAST_ONBOARDING.md CO3, 2026-09-29). Shaped like
    /// <see cref="HoverRelay"/>. The element needs a graphic that is a
    /// raycast target (a button has one).
    /// </summary>
    /// <remarks>
    /// <b>A hold is not a click.</b> When a press has been held, its release
    /// is made ineligible for a click, so the button under it does not fire:
    /// holding an ability card reads it and never arms it.
    ///
    /// <b>It works on a disabled button.</b> A button that is not
    /// interactable still takes the pointer (its image is a raycast target),
    /// and this component answers it: a card that cannot be cast can still
    /// be held, or tapped, to read.
    ///
    /// <b>Feedback.</b> From <see cref="FeedbackAfter"/>, a thin cyan bar
    /// fills along the element's bottom edge, so the player learns that
    /// holding does something. It is gone on release.
    ///
    /// Leaving the element cancels the press. Unscaled time, so a paused or
    /// slowed match still reads its cards.
    /// </remarks>
    public sealed class HoldRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        /// <summary>How long a press must last to count as a hold.</summary>
        public const float HoldSeconds = 0.4f;

        /// <summary>When the fill bar starts, so a quick tap never flickers it.</summary>
        public const float FeedbackAfter = 0.15f;

        private const float BarHeight = 3f;

        /// <summary>A press reached <see cref="HoldSeconds"/>. Fires once, while still held.</summary>
        public Action Held;

        /// <summary>A press released on the element before it became a hold. Null leaves taps to the button.</summary>
        public Action Tapped;

        private bool _pressed;
        private bool _held;
        private float _pressedAt;
        private int _pointerId;
        private RectTransform _bar;

        /// <summary>Whether a press is down on the element.</summary>
        public bool Pressed => _pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            _pressed = true;
            _held = false;
            _pointerId = eventData.pointerId;
            _pressedAt = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressed || eventData.pointerId != _pointerId) return;

            bool held = _held;
            Cancel();

            // The release after a hold must not reach the button as a click.
            if (held)
            {
                eventData.eligibleForClick = false;
                return;
            }

            Tapped?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_pressed || eventData.pointerId != _pointerId) return;

            // A hold that already fired stays read; only a press still
            // counting is cancelled. Its release will not click either way:
            // the pointer is no longer over the button.
            if (!_held) Cancel();
        }

        private void Update()
        {
            if (!_pressed) return;

            float elapsed = Time.unscaledTime - _pressedAt;

            if (!_held)
            {
                ShowBar(Mathf.InverseLerp(FeedbackAfter, HoldSeconds, elapsed), elapsed >= FeedbackAfter);

                if (elapsed >= HoldSeconds)
                {
                    _held = true;
                    ShowBar(1f, false);
                    Held?.Invoke();
                }
            }
        }

        private void OnDisable() => Cancel();

        private void Cancel()
        {
            _pressed = false;
            _held = false;
            ShowBar(0f, false);
        }

        private void ShowBar(float fill, bool visible)
        {
            if (!visible)
            {
                if (_bar != null) _bar.gameObject.SetActive(false);
                return;
            }

            if (_bar == null) BuildBar();

            _bar.gameObject.SetActive(true);
            _bar.anchorMax = new Vector2(Mathf.Clamp01(fill), 0f);
        }

        private void BuildBar()
        {
            var go = new GameObject("hold_bar", typeof(RectTransform), typeof(Image));
            _bar = (RectTransform)go.transform;
            _bar.SetParent(transform, false);
            _bar.anchorMin = Vector2.zero;
            _bar.anchorMax = new Vector2(0f, 0f);
            _bar.pivot = new Vector2(0f, 0f);
            _bar.offsetMin = new Vector2(0f, 0f);
            _bar.offsetMax = new Vector2(0f, BarHeight);

            var image = go.GetComponent<Image>();
            image.color = UiTheme.Cyan;
            image.raycastTarget = false;

            // Ignore the card's layout group: the bar is drawn over the card, not laid out in it.
            go.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        /// <summary>Adds (or reuses) the relay on <paramref name="target"/>.</summary>
        public static HoldRelay On(Component target, Action held, Action tapped = null)
        {
            // Unity's == null, not ??: a destroyed or missing component can be a "fake null".
            var relay = target.gameObject.GetComponent<HoldRelay>();
            if (relay == null) relay = target.gameObject.AddComponent<HoldRelay>();
            relay.Held = held;
            relay.Tapped = tapped;
            return relay;
        }
    }
}
