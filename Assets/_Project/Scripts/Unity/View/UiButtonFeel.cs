// Assets/_Project/Scripts/Unity/View/UiButtonFeel.cs
using UnityEngine;
using UnityEngine.EventSystems;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The press feel of a kit button: under the pointer its plate sinks by
    /// the lip's depth and comes back up, sharp rather than bouncy
    /// (UI_MOTION.md increment U2, G9a; ART_DIRECTION §8 bans soft shapes, so
    /// the motion is small and quick).
    /// </summary>
    /// <remarks>
    /// Added by <see cref="UiKit.Button"/> to every button it builds. Since
    /// G9a the button is a <see cref="UiPlate"/> and the press is the plate
    /// going down, not a scale dip: a scaled plate shrank away from its own
    /// lip. A button built without a plate still dips in scale, which layout
    /// leaves alone. It also keeps the plate's stance in step with the
    /// button: a button made non-interactable after it was built sits flush.
    /// Non-interactable buttons never press.
    /// </remarks>
    public sealed class UiButtonFeel : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressedScale = 0.93f;
        private const float DownSeconds = 0.07f;
        private const float UpSeconds = 0.18f;

        private UnityEngine.UI.Button _button;
        private UiPlate _plate;
        private UiTween _tween;

        /// <summary>Adds the feel to <paramref name="button"/> unless it is already there.</summary>
        public static void Attach(UnityEngine.UI.Button button, UiPlate plate = null)
        {
            if (button == null || button.GetComponent<UiButtonFeel>() != null) return;

            var feel = button.gameObject.AddComponent<UiButtonFeel>();
            feel._button = button;
            feel._plate = plate;
            feel.Update();
        }

        private void Update()
        {
            if (_plate == null || _button == null) return;

            bool flush = !_button.interactable;
            if (_plate.Flush != flush) _plate.Flush = flush;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            TweenTo(1f, DownSeconds, UiEase.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (Pressed > 0f) TweenTo(0f, UpSeconds, UiEase.OutCubic);
        }

        /// <summary>0 at rest, 1 fully pressed, whichever way the button shows it.</summary>
        private float Pressed
        {
            get => _plate != null
                ? _plate.Press
                : Mathf.InverseLerp(1f, PressedScale, transform.localScale.x);
            set
            {
                if (_plate != null) _plate.Press = value;
                else transform.localScale = Vector3.one * Mathf.LerpUnclamped(1f, PressedScale, value);
            }
        }

        private void TweenTo(float target, float seconds, UiEase ease)
        {
            if (_tween != null) _tween.Stop();

            float from = Pressed;
            _tween = UiTween.Run(this, seconds, t => Pressed = Mathf.LerpUnclamped(from, target, t), ease);
        }

        private void OnDisable()
        {
            // Never leave a button pressed because the pointer left while disabled.
            if (_plate != null) _plate.Press = 0f;
            else transform.localScale = Vector3.one;
        }
    }
}
