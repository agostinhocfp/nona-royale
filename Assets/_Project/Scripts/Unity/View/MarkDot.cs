// Assets/_Project/Scripts/Unity/View/MarkDot.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a marked operator looks (CORE_GAMEPLAY.md, CG11): a small laser
    /// dot on its chest, with a soft glow, holding not quite still. It
    /// replaces the MARKED tag.
    /// </summary>
    /// <remarks>
    /// <b>The painted target.</b> A sight's dot on a body is shorthand from a
    /// thousand films: someone has a bead on this one. The jitter is the hand
    /// holding the sight, small and slow, so it reads as alive without
    /// drawing the eye off the rest of the board. Reduced motion holds it
    /// dead still.
    /// </remarks>
    public sealed class MarkDot : MonoBehaviour
    {
        /// <summary>Orders inside the piece's group: over the body (4).</summary>
        private const int GlowOrder = 5;
        private const int DotOrder = 6;

        private const float DotSize = 0.07f;
        private const float GlowSize = 0.24f;
        private const float Jitter = 0.018f;

        /// <summary>How far above the body's middle the dot sits, as a share of its half-height.</summary>
        private const float ChestLift = 0.2f;

        private OperatorPiece _piece;
        private MotionSettings _motion;
        private SpriteRenderer _dot;
        private SpriteRenderer _glow;
        private Color _colour;

        private bool _shown;
        private float _time;

        private bool Reduced => _motion != null && _motion.ReducedMotion;

        public void Bind(OperatorPiece piece, Transform frame, MotionSettings motion)
        {
            _piece = piece;
            _motion = motion;
            _colour = Color.Lerp(StatusPalette.For(StatusKind.Mark), new Color(1f, 0.15f, 0.1f), 0.45f);

            _glow = Make("mark_glow", frame, BoardArt.SoftDisc, GlowOrder);
            _dot = Make("mark_dot", frame, Primitives.Disc, DotOrder);
        }

        /// <summary>Turns the dot on or off.</summary>
        public void Show(bool on)
        {
            if (_shown == on) return;

            _shown = on;
            if (_dot != null) _dot.enabled = on;
            if (_glow != null) _glow.enabled = on;
        }

        private void Update()
        {
            if (!_shown || _dot == null || _piece == null) return;

            _time += Time.deltaTime;
            _piece.CueBody(out float centre, out float radius);

            // Two slow noises, so the hand drifts rather than vibrates.
            var drift = Reduced
                ? Vector2.zero
                : new Vector2(Mathf.PerlinNoise(_time * 0.9f, 3.1f) - 0.5f, Mathf.PerlinNoise(7.7f, _time * 0.9f) - 0.5f) * (2f * Jitter);

            var at = new Vector3(drift.x, centre + radius * ChestLift + drift.y, 0f);
            float flicker = Reduced ? 1f : 0.85f + 0.15f * Mathf.PerlinNoise(_time * 6f, 1.3f);

            _dot.transform.localPosition = at;
            _dot.transform.localScale = Vector3.one * DotSize;
            _dot.color = UiTheme.WithAlpha(Color.Lerp(_colour, Color.white, 0.35f), flicker);

            _glow.transform.localPosition = at;
            _glow.transform.localScale = Vector3.one * GlowSize;
            _glow.color = UiTheme.WithAlpha(_colour, 0.55f * flicker);
        }

        private static SpriteRenderer Make(string name, Transform parent, Sprite sprite, int order)
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