// Assets/_Project/Scripts/Unity/View/ChargeLight.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a Zero-Day carrier looks (CORE_GAMEPLAY.md, CG14): a small
    /// magenta light stuck to the piece, blinking like an armed device. It
    /// replaces the 0-DAY tag; the blast it will set off is drawn on the
    /// floor by <see cref="DeviceLayer"/>.
    /// </summary>
    /// <remarks>
    /// <b>Off-centre, low on the body,</b> so it reads as something attached
    /// rather than as the piece's own glow, and so it never sits where the
    /// Mark's laser dot does. A quick double blink and a rest, the rhythm of
    /// a timer. Reduced motion holds it lit.
    /// </remarks>
    public sealed class ChargeLight : MonoBehaviour
    {
        private const int GlowOrder = 5;
        private const int LightOrder = 6;

        private const float LightSize = 0.065f;
        private const float GlowSize = 0.26f;

        /// <summary>Seconds per blink cycle: two flashes and a rest.</summary>
        private const float CycleSeconds = 1.3f;

        private OperatorPiece _piece;
        private MotionSettings _motion;
        private SpriteRenderer _light;
        private SpriteRenderer _glow;
        private Color _colour;

        private bool _shown;
        private float _time;

        private bool Reduced => _motion != null && _motion.ReducedMotion;

        public void Bind(OperatorPiece piece, Transform frame, MotionSettings motion)
        {
            _piece = piece;
            _motion = motion;
            _colour = StatusPalette.For(StatusKind.ZeroDayCharge);

            _glow = CueKit.Renderer("charge_glow", frame, BoardArt.SoftDisc, GlowOrder);
            _light = CueKit.Renderer("charge_light", frame, Primitives.Disc, LightOrder);
        }

        public void Show(bool on)
        {
            if (_shown == on) return;

            _shown = on;
            _light.enabled = on;
            _glow.enabled = on;
        }

        private void Update()
        {
            if (!_shown || _piece == null) return;

            _time += Time.deltaTime;
            _piece.CueBody(out float centre, out float radius);

            var at = new Vector3(radius * 0.38f, centre - radius * 0.25f, 0f);

            // Two short flashes at the start of each cycle, then dark.
            float phase = Mathf.Repeat(_time / CycleSeconds, 1f);
            bool flash = phase < 0.1f || (phase > 0.2f && phase < 0.3f);
            float on = Reduced ? 1f : flash ? 1f : 0.25f;

            _light.transform.localPosition = at;
            _light.transform.localScale = Vector3.one * LightSize;
            _light.color = UiTheme.WithAlpha(Color.Lerp(_colour, Color.white, 0.4f * on), 0.5f + 0.5f * on);

            _glow.transform.localPosition = at;
            _glow.transform.localScale = Vector3.one * GlowSize * (0.8f + 0.2f * on);
            _glow.color = UiTheme.WithAlpha(_colour, 0.6f * on);
        }
    }

    /// <summary>Small helpers shared by the on-piece status cues (CG14).</summary>
    internal static class CueKit
    {
        /// <summary>A hidden sprite renderer under <paramref name="parent"/>, at <paramref name="order"/> in the piece's group.</summary>
        public static SpriteRenderer Renderer(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }
    }
}