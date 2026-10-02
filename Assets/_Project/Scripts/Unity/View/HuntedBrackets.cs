// Assets/_Project/Scripts/Unity/View/HuntedBrackets.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a hunted operator looks (CORE_GAMEPLAY.md, CG14): four crimson
    /// corner brackets framing the piece, slowly closing in and easing out,
    /// the way a sight locks on. It replaces the HUNTED tag.
    /// </summary>
    /// <remarks>
    /// <b>A frame, where the Mark is a dot.</b> Both say "someone has a bead
    /// on you", so they must not look alike: the Mark's dot sits on the
    /// chest, and Luka's follow-up boxes the whole piece. A target carrying
    /// both shows both. Reduced motion holds the brackets at rest.
    /// </remarks>
    public sealed class HuntedBrackets : MonoBehaviour
    {
        private const int Order = 6;

        /// <summary>How far out the corners sit, as a share of the body's radius, at their widest and tightest.</summary>
        private const float Wide = 1.32f;
        private const float Tight = 1.12f;

        private const float Arm = 0.16f;
        private const float Thickness = 0.03f;
        private const float BreathSeconds = 2.2f;

        private OperatorPiece _piece;
        private MotionSettings _motion;
        private SpriteRenderer[] _bars;
        private Color _colour;

        private bool _shown;
        private float _time;

        private bool Reduced => _motion != null && _motion.ReducedMotion;

        public void Bind(OperatorPiece piece, Transform frame, MotionSettings motion)
        {
            _piece = piece;
            _motion = motion;
            _colour = Color.Lerp(StatusPalette.For(StatusKind.Hunted), new Color(0.9f, 0.1f, 0.15f), 0.35f);

            // Two bars per corner: one along x, one along y.
            _bars = new SpriteRenderer[8];
            for (int i = 0; i < _bars.Length; i++)
                _bars[i] = CueKit.Renderer($"hunted_{i}", frame, Primitives.Square, Order);
        }

        public void Show(bool on)
        {
            if (_shown == on) return;

            _shown = on;
            foreach (var bar in _bars) bar.enabled = on;
        }

        private void Update()
        {
            if (!_shown || _piece == null) return;

            _time += Time.deltaTime;
            _piece.CueBody(out float centre, out float radius);

            float squeeze = Reduced ? 0.5f : 0.5f + 0.5f * Mathf.Sin(_time * 2f * Mathf.PI / BreathSeconds);
            float reach = radius * Mathf.Lerp(Wide, Tight, squeeze);
            float arm = Arm * Mathf.Max(0.6f, radius * 1.6f);
            var colour = UiTheme.WithAlpha(_colour, 0.75f + 0.2f * squeeze);

            int b = 0;
            for (int corner = 0; corner < 4; corner++)
            {
                float sx = corner == 0 || corner == 3 ? -1f : 1f;
                float sy = corner < 2 ? 1f : -1f;
                var tip = new Vector3(sx * reach, centre + sy * reach, 0f);

                // The horizontal arm runs inward from the corner along x, the vertical one along y.
                Place(_bars[b++], tip + new Vector3(-sx * arm * 0.5f, 0f, 0f), new Vector3(arm, Thickness, 1f), colour);
                Place(_bars[b++], tip + new Vector3(0f, -sy * arm * 0.5f, 0f), new Vector3(Thickness, arm, 1f), colour);
            }
        }

        private static void Place(SpriteRenderer bar, Vector3 position, Vector3 scale, Color colour)
        {
            bar.transform.localPosition = position;
            bar.transform.localScale = scale;
            bar.color = colour;
        }
    }
}