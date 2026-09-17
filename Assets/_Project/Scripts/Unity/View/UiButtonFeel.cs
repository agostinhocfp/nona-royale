// Assets/_Project/Scripts/Unity/View/UiButtonFeel.cs
using UnityEngine;
using UnityEngine.EventSystems;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The press feel of a kit button: it dips a little under the pointer and
    /// comes back, sharp rather than bouncy (UI_MOTION.md increment U2;
    /// ART_DIRECTION §8 bans soft shapes, so the motion is small and quick).
    /// </summary>
    /// <remarks>
    /// Added by <see cref="UiKit.Button"/> to every button it builds. Scale is
    /// not layout-owned, so this is safe inside rows and columns. Non-
    /// interactable buttons never dip.
    /// </remarks>
    public sealed class UiButtonFeel : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressedScale = 0.96f;
        private const float DownSeconds = 0.06f;
        private const float UpSeconds = 0.14f;

        private UnityEngine.UI.Button _button;
        private UiTween _tween;

        /// <summary>Adds the feel to <paramref name="button"/> unless it is already there.</summary>
        public static void Attach(UnityEngine.UI.Button button)
        {
            if (button == null || button.GetComponent<UiButtonFeel>() != null) return;
            button.gameObject.AddComponent<UiButtonFeel>()._button = button;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            TweenTo(PressedScale, DownSeconds, UiEase.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (transform.localScale.x < 1f) TweenTo(1f, UpSeconds, UiEase.OutCubic);
        }

        private void TweenTo(float target, float seconds, UiEase ease)
        {
            if (_tween != null) _tween.Stop();

            float from = transform.localScale.x;
            _tween = UiTween.Run(this, seconds,
                t => transform.localScale = Vector3.one * Mathf.LerpUnclamped(from, target, t), ease);
        }

        private void OnDisable()
        {
            // Never leave a button dipped because the pointer left while disabled.
            transform.localScale = Vector3.one;
        }
    }
}
