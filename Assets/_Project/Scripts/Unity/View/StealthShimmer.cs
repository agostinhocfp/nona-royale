// Assets/_Project/Scripts/Unity/View/StealthShimmer.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a stealthed operator looks (CORE_GAMEPLAY.md, CG14): the figure
    /// shimmers like air over hot ground, with two faint violet copies
    /// swaying either side of it out of step. It replaces the STEALTH tag.
    /// </summary>
    /// <remarks>
    /// <b>A shimmer, never a fade.</b> Kurbyn's Evasion already fades the
    /// figure (<c>OperatorPiece</c>), and the two mean different things:
    /// Evasion may negate a hit, Stealth cannot be singled out by an enemy at
    /// all. So the body keeps its full colour and the copies do the work.
    ///
    /// <b>Drawn from the piece's own silhouette</b>, the same source the haste
    /// afterimages use (<see cref="OperatorPiece.GhostSprite"/>), so it works
    /// on a chip and a rendered figure alike. Reduced motion holds the copies
    /// still at a small offset.
    /// </remarks>
    public sealed class StealthShimmer : MonoBehaviour
    {
        private const int Copies = 2;
        private const float Sway = 0.045f;
        private const float SwaySeconds = 1.7f;
        private const float CopyAlpha = 0.3f;

        private OperatorPiece _piece;
        private MotionSettings _motion;
        private SpriteRenderer[] _copies;
        private Color _colour;

        private bool _shown;
        private float _time;

        private bool Reduced => _motion != null && _motion.ReducedMotion;

        public void Bind(OperatorPiece piece, Transform frame, MotionSettings motion)
        {
            _piece = piece;
            _motion = motion;
            _colour = Color.Lerp(StatusPalette.For(StatusKind.Stealth), Color.white, 0.25f);

            _copies = new SpriteRenderer[Copies];
            for (int i = 0; i < Copies; i++)
            {
                var go = new GameObject($"stealth_{i}");
                go.transform.SetParent(piece.transform.parent, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.enabled = false;
                _copies[i] = renderer;
            }
        }

        public void Show(bool on)
        {
            if (_shown == on) return;

            _shown = on;
            foreach (var copy in _copies) copy.enabled = on;
        }

        private void LateUpdate()
        {
            if (!_shown || _piece == null) return;

            var sprite = _piece.GhostSprite;
            var frame = _piece.GhostFrame;
            if (sprite == null || frame == null)
            {
                foreach (var copy in _copies) copy.enabled = false;
                return;
            }

            _time += Time.deltaTime;

            for (int i = 0; i < Copies; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float wave = Reduced ? 0.6f : Mathf.Sin(_time * 2f * Mathf.PI / SwaySeconds + i * 1.9f);
                float offset = side * Sway * (0.55f + 0.45f * wave);

                var copy = _copies[i];
                copy.enabled = true;
                copy.sprite = sprite;
                copy.flipX = false;
                copy.sortingLayerID = _piece.GroupLayer;
                copy.sortingOrder = _piece.GroupOrder - 1;

                // Along the figure's own sideways axis, so the sway leans with the tilt.
                copy.transform.SetPositionAndRotation(frame.position + frame.right * offset * frame.lossyScale.x, frame.rotation);
                copy.transform.localScale = frame.lossyScale;

                float flicker = Reduced ? 1f : 0.75f + 0.25f * Mathf.PerlinNoise(_time * 5f, i * 3.3f);
                copy.color = UiTheme.WithAlpha(_colour, CopyAlpha * flicker);
            }
        }

        private void OnDestroy()
        {
            if (_copies == null) return;
            foreach (var copy in _copies)
                if (copy != null) Destroy(copy.gameObject);
        }
    }
}