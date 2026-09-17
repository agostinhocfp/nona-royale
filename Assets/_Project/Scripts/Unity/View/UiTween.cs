// Assets/_Project/Scripts/Unity/View/UiTween.cs
using System;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A tiny tween for HUD elements: one component per running tween, on
    /// unscaled time, removing itself when done (UI_MOTION.md increment U1).
    /// </summary>
    /// <remarks>
    /// <b>Same shape as <see cref="UiPopIn"/>:</b> no manager, no singleton; a
    /// tween is a component on the thing it moves, so a rebuild that destroys
    /// the thing kills the tween with it.
    ///
    /// <b>Reduced motion</b> (<see cref="ReducedMotion"/>, set by the
    /// composition root from <see cref="MotionSettings"/>, MO2): tweens run
    /// shorter, and overshooting eases fall back to smooth ones.
    ///
    /// <b>Layout groups own positions.</b> Slide tweens are for things a
    /// layout does not place (cards, floating pills); inside a layout, fade
    /// and scale only.
    /// </remarks>
    public sealed class UiTween : MonoBehaviour
    {
        /// <summary>Shorter tweens and no overshoot, while the composition root says so (MO2).</summary>
        public static bool ReducedMotion;

        private float _time;
        private float _seconds;
        private UiEase _ease;
        private Action<float> _apply;
        private Action _done;

        /// <summary>The core: <paramref name="apply"/> receives the eased 0…1 each unscaled frame.</summary>
        public static UiTween Run(Component host, float seconds, Action<float> apply,
            UiEase ease = UiEase.OutCubic, float delay = 0f, Action done = null)
        {
            if (host == null || apply == null) return null;

            var tween = host.gameObject.AddComponent<UiTween>();
            tween._time = -Mathf.Max(0f, delay);
            tween._seconds = Mathf.Max(0.01f, ReducedMotion ? seconds * MotionSettings.ReducedTween : seconds);
            tween._ease = ReducedMotion && ease == UiEase.OutBack ? UiEase.OutCubic : ease;
            tween._apply = apply;
            tween._done = done;
            return tween;
        }

        /// <summary>Stops a tween early, leaving the target where it is.</summary>
        public void Stop() => Destroy(this);

        /// <summary>Fades a canvas group to <paramref name="to"/>.</summary>
        public static UiTween Fade(CanvasGroup group, float to, float seconds,
            float delay = 0f, Action done = null)
        {
            if (group == null) return null;

            float from = group.alpha;
            return Run(group, seconds, t => group.alpha = Mathf.Lerp(from, to, t),
                UiEase.OutCubic, delay, done);
        }

        /// <summary>Fades a canvas group in from zero.</summary>
        public static UiTween FadeIn(CanvasGroup group, float seconds, float delay = 0f, Action done = null)
        {
            if (group == null) return null;

            group.alpha = 0f;
            return Fade(group, 1f, seconds, delay, done);
        }

        /// <summary>Slides a rect into place from <paramref name="fromOffset"/> away. Not for layout children.</summary>
        public static UiTween SlideIn(RectTransform rect, Vector2 fromOffset, float seconds,
            float delay = 0f, UiEase ease = UiEase.OutCubic)
        {
            if (rect == null) return null;

            Vector2 end = rect.anchoredPosition;
            Vector2 start = end + fromOffset;
            rect.anchoredPosition = start;
            return Run(rect, seconds, t => rect.anchoredPosition = Vector2.LerpUnclamped(start, end, t),
                ease, delay);
        }

        /// <summary>Scales a transform from <paramref name="from"/> to 1.</summary>
        public static UiTween ScaleIn(Component target, float from, float seconds,
            float delay = 0f, UiEase ease = UiEase.OutCubic)
        {
            if (target == null) return null;

            target.transform.localScale = Vector3.one * from;
            return Run(target, seconds,
                t => target.transform.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, t),
                ease, delay);
        }

        /// <summary>Animates a value, for count-ups and bar tweens.</summary>
        public static UiTween Value(Component host, float from, float to, float seconds,
            Action<float> apply, float delay = 0f, Action done = null)
        {
            if (apply == null) return null;
            return Run(host, seconds, t => apply(Mathf.LerpUnclamped(from, to, t)),
                UiEase.OutCubic, delay, done);
        }

        /// <summary>Fades the layout children named <paramref name="prefix"/> in, one after another.</summary>
        public static void StaggerIn(RectTransform parent, string prefix = "content_",
            float perItem = 0.035f, float seconds = 0.16f)
        {
            if (parent == null) return;

            int index = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.name.StartsWith(prefix)) continue;

                var group = child.GetComponent<CanvasGroup>()
                    ?? child.gameObject.AddComponent<CanvasGroup>();
                FadeIn(group, seconds, index * perItem);
                index++;
            }
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            if (_time < 0f) return;

            float t = Mathf.Clamp01(_time / _seconds);
            _apply(UiEasing.Evaluate(_ease, t));

            if (t < 1f) return;

            _done?.Invoke();
            Destroy(this);
        }
    }
}
