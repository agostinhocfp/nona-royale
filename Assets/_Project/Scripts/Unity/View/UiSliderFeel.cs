// Assets/_Project/Scripts/Unity/View/UiSliderFeel.cs
using UnityEngine;
using UnityEngine.EventSystems;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The hover and drag feel of a kit slider: the diamond knob grows a
    /// little under the pointer, and a little more while dragged
    /// (UI_MOTION.md increment U2). Sliders have no Selectable transition of
    /// their own (<see cref="UiKit.SliderRow"/>), so this is their only
    /// feedback.
    /// </summary>
    public sealed class UiSliderFeel : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverScale = 1.2f;
        private const float DragScale = 1.35f;
        private const float Seconds = 0.1f;

        private RectTransform _knob;
        private UiTween _tween;
        private bool _hovered;
        private bool _dragged;

        /// <summary>Adds the feel to <paramref name="hitArea"/>, growing <paramref name="knob"/>.</summary>
        public static void Attach(GameObject hitArea, RectTransform knob)
        {
            if (hitArea == null || knob == null) return;
            hitArea.AddComponent<UiSliderFeel>()._knob = knob;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            TweenTo(_dragged ? DragScale : HoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            TweenTo(_dragged ? DragScale : 1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _dragged = true;
            TweenTo(DragScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _dragged = false;
            TweenTo(_hovered ? HoverScale : 1f);
        }

        private void TweenTo(float target)
        {
            if (_knob == null) return;
            if (_tween != null) _tween.Stop();

            float from = _knob.localScale.x;
            _tween = UiTween.Run(this, Seconds,
                t => _knob.localScale = Vector3.one * Mathf.LerpUnclamped(from, target, t), UiEase.OutCubic);
        }

        private void OnDisable()
        {
            _hovered = _dragged = false;
            if (_knob != null) _knob.localScale = Vector3.one;
        }
    }
}
