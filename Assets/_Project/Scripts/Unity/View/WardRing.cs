// Assets/_Project/Scripts/Unity/View/WardRing.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a tech-warded operator looks (CORE_GAMEPLAY.md, CG14): a thin
    /// cyan ring on the floor round its feet, flickering like a projected
    /// field, with a fainter echo ring that drifts outward and fades. It
    /// replaces the WARD tag.
    /// </summary>
    /// <remarks>
    /// <b>On the floor, where the shield is round the body.</b> Both stop
    /// damage, so they must not look alike: a Shield is a bubble that bursts
    /// on one Normal hit, the Tech Ward is a field underfoot that turns away
    /// Tech while it lasts. Reduced motion keeps the ring steady and drops
    /// the echo.
    /// </remarks>
    public sealed class WardRing : MonoBehaviour
    {
        private const int Order = 1;

        private const float Width = 1.05f;
        private const float Depth = 0.42f;
        private const float EchoSeconds = 1.6f;

        private MotionSettings _motion;
        private SpriteRenderer _ring;
        private SpriteRenderer _echo;
        private Color _colour;

        private bool _shown;
        private float _time;

        private bool Reduced => _motion != null && _motion.ReducedMotion;

        public void Bind(OperatorPiece piece, Transform frame, MotionSettings motion)
        {
            _motion = motion;
            _colour = Color.Lerp(StatusPalette.For(StatusKind.TechWard), Color.white, 0.2f);

            _ring = CueKit.Renderer("ward_ring", piece.transform, Primitives.Ring, Order);
            _ring.transform.localScale = new Vector3(Width, Depth, 1f);
            _echo = CueKit.Renderer("ward_echo", piece.transform, Primitives.Ring, Order);
        }

        public void Show(bool on)
        {
            if (_shown == on) return;

            _shown = on;
            _ring.enabled = on;
            _echo.enabled = on && !Reduced;
        }

        private void Update()
        {
            if (!_shown) return;

            _time += Time.deltaTime;

            // A field's flicker: mostly steady, with quick dips.
            float flicker = Reduced ? 1f : 0.8f + 0.2f * Mathf.PerlinNoise(_time * 7f, 0.4f);
            _ring.color = UiTheme.WithAlpha(_colour, 0.8f * flicker);

            _echo.enabled = !Reduced;
            if (Reduced) return;

            float t = Mathf.Repeat(_time / EchoSeconds, 1f);
            float grow = 1f + 0.35f * t;
            _echo.transform.localScale = new Vector3(Width * grow, Depth * grow, 1f);
            _echo.color = UiTheme.WithAlpha(_colour, 0.45f * (1f - t));
        }
    }
}