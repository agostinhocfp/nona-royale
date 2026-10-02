// Assets/_Project/Scripts/Unity/View/StunHalo.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a stunned operator looks (CORE_GAMEPLAY.md, CG9): three small
    /// yellow stars circling the crown of its head. It replaces the STUN tag.
    /// </summary>
    /// <remarks>
    /// <b>Why not a tag.</b> The same reason haste lost its tag
    /// (<see cref="HasteTrail"/>): a word under the piece has to be read, and a
    /// picture on the piece is seen. Circling stars are the oldest shorthand
    /// for "knocked silly", so a stranger reads it without a legend, and it
    /// sits on the piece that cannot act rather than beside it.
    ///
    /// <b>Depth from the draw order.</b> The orbit is a flat ellipse round the
    /// head: a star on the near half draws in front of the figure, a star on
    /// the far half draws behind it, smaller and dimmer. That is the whole of
    /// the 3D illusion, and it needs no camera maths.
    ///
    /// <b>Engine answers only</b> (PRESENTATION §1): whether the piece is
    /// stunned comes from <c>ActiveStatusesOn</c>, handed in by the
    /// composition root through <see cref="OperatorPiece.Refresh"/>. Reduced
    /// motion holds the stars still on the near side.
    /// </remarks>
    public sealed class StunHalo : MonoBehaviour
    {
        private const int StarCount = 3;

        /// <summary>Seconds per lap of the orbit.</summary>
        private const float OrbitSeconds = 1.8f;

        /// <summary>The orbit's half-width and half-depth, in figure units (a figure is about one unit tall).</summary>
        private const float RadiusX = 0.21f;
        private const float RadiusY = 0.05f;

        /// <summary>How far above the crown the orbit runs.</summary>
        private const float Lift = 0.03f;

        private const float StarSize = 0.085f;

        /// <summary>Orders inside the piece's sorting group: in front of the body (4), or behind the outline (3).</summary>
        private const int FrontOrder = 5;
        private const int BackOrder = 2;

        private const float FrontAlpha = 0.95f;
        private const float BackAlpha = 0.5f;
        private const float BackScale = 0.75f;

        private OperatorPiece _piece;
        private MotionSettings _motion;
        private SpriteRenderer[] _stars;
        private Color _colour;

        private bool _shown;
        private float _time;

        private bool Reduced => _motion != null && _motion.ReducedMotion;

        public void Bind(OperatorPiece piece, Transform frame, MotionSettings motion)
        {
            _piece = piece;
            _motion = motion;

            // Brighter than the tag's board colour: the stars are small, and a
            // dim yellow would sink into a lit figure.
            _colour = Color.Lerp(StatusPalette.OnBoard(StatusKind.Stun), StatusPalette.For(StatusKind.Stun), 0.6f);

            _stars = new SpriteRenderer[StarCount];
            for (int i = 0; i < StarCount; i++)
            {
                var go = new GameObject($"stun_{i}");
                go.transform.SetParent(frame, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = Primitives.Star(5, 90f);
                renderer.sortingOrder = FrontOrder;
                renderer.enabled = false;
                _stars[i] = renderer;
            }
        }

        /// <summary>Turns the stars on or off.</summary>
        public void Show(bool on)
        {
            if (_shown == on) return;

            _shown = on;
            if (_stars == null) return;
            foreach (var star in _stars) star.enabled = on;
        }

        private void Update()
        {
            if (!_shown || _stars == null) return;

            _time += Time.deltaTime;

            float crown = (_piece != null ? _piece.CrownHeight : 0.9f) + Lift;
            float lap = Reduced ? 0f : _time / OrbitSeconds;

            for (int i = 0; i < StarCount; i++)
            {
                // Reduced motion spreads the three across the near half and holds them.
                float angle = Reduced
                    ? Mathf.PI * (1.25f + 0.25f * i)
                    : 2f * Mathf.PI * (lap + (float)i / StarCount);

                float x = Mathf.Cos(angle) * RadiusX;
                float depth = Mathf.Sin(angle);              // −1 nearest the viewer, +1 furthest
                bool front = depth <= 0f;

                // Eased between near and far, so a star shrinks as it rounds the back.
                float near = Mathf.InverseLerp(1f, -1f, depth);
                float scale = StarSize * Mathf.Lerp(BackScale, 1f, near);

                var star = _stars[i];
                star.transform.localPosition = new Vector3(x, crown + depth * RadiusY, 0f);
                star.transform.localRotation = Quaternion.Euler(0f, 0f, Reduced ? 0f : _time * 90f + i * 40f);
                star.transform.localScale = new Vector3(scale, scale, 1f);
                star.sortingOrder = front ? FrontOrder : BackOrder;
                star.color = UiTheme.WithAlpha(_colour, Mathf.Lerp(BackAlpha, FrontAlpha, near));
            }
        }
    }
}