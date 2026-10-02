// Assets/_Project/Scripts/Unity/View/SlowFrost.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a slowed operator looks (CORE_GAMEPLAY.md, CG11): pale rime on
    /// the floor round its feet and a few ice motes drifting slowly up it. It
    /// replaces the SLOW tag, and it is haste's mirror: haste rushes, slow
    /// settles.
    /// </summary>
    /// <remarks>
    /// <b>Two parts on two parents.</b> The rime lies on the board with the
    /// seat disc, so it is a child of the piece itself; the motes rise along
    /// the figure, so they ride the figure's frame and lean with it under the
    /// tilt. Reduced motion keeps the rime and holds the motes still.
    /// </remarks>
    public sealed class SlowFrost : MonoBehaviour
    {
        private const int MoteCount = 4;

        /// <summary>Seconds for a mote to climb from the feet to the crown.</summary>
        private const float RiseSeconds = 2.6f;

        private const float MoteSize = 0.06f;
        private const float MoteSpread = 0.3f;

        /// <summary>The rime: a flattened disc, a little wider than the seat ring.</summary>
        private const float RimeWidth = 0.95f;
        private const float RimeDepth = 0.38f;
        private const float RimeAlpha = 0.5f;

        /// <summary>Orders inside the piece's group: the rime with the seat ring (1), the motes over the body (4).</summary>
        private const int RimeOrder = 1;
        private const int MoteOrder = 5;

        private OperatorPiece _piece;
        private MotionSettings _motion;
        private SpriteRenderer _rime;
        private SpriteRenderer[] _motes;
        private Color _colour;

        private bool _shown;
        private float _time;

        private bool Reduced => _motion != null && _motion.ReducedMotion;

        public void Bind(OperatorPiece piece, Transform frame, MotionSettings motion)
        {
            _piece = piece;
            _motion = motion;
            _colour = Color.Lerp(StatusPalette.For(StatusKind.Slow), new Color(0.85f, 0.95f, 1f), 0.5f);

            var rime = new GameObject("slow_rime");
            rime.transform.SetParent(piece.transform, false);
            rime.transform.localScale = new Vector3(RimeWidth, RimeDepth, 1f);
            _rime = rime.AddComponent<SpriteRenderer>();
            _rime.sprite = BoardArt.SoftDisc;
            _rime.sortingOrder = RimeOrder;
            _rime.color = UiTheme.WithAlpha(_colour, RimeAlpha);
            _rime.enabled = false;

            _motes = new SpriteRenderer[MoteCount];
            for (int i = 0; i < MoteCount; i++)
            {
                var go = new GameObject($"slow_mote_{i}");
                go.transform.SetParent(frame, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = Primitives.Star(4, 45f);
                renderer.sortingOrder = MoteOrder;
                renderer.enabled = false;
                _motes[i] = renderer;
            }
        }

        /// <summary>Turns the frost on or off.</summary>
        public void Show(bool on)
        {
            if (_shown == on) return;

            _shown = on;
            if (_rime != null) _rime.enabled = on;
            if (_motes == null) return;
            foreach (var mote in _motes) mote.enabled = on;
        }

        private void Update()
        {
            if (!_shown || _motes == null || _piece == null) return;

            _time += Time.deltaTime;
            _piece.CueBody(out float centre, out float radius);
            float bottom = centre - radius;
            float height = 2f * radius;

            for (int i = 0; i < MoteCount; i++)
            {
                // Each mote climbs on its own phase and sways a little as it goes.
                float phase = Reduced ? 0.2f + 0.2f * i : Mathf.Repeat(_time / RiseSeconds + (float)i / MoteCount, 1f);
                float side = ((i % 2 == 0) ? -1f : 1f) * MoteSpread * (0.55f + 0.15f * i);
                float sway = Reduced ? 0f : 0.04f * Mathf.Sin(_time * 1.7f + i * 2.3f);

                var mote = _motes[i];
                mote.transform.localPosition = new Vector3(side * radius + sway, bottom + phase * height * 1.05f, 0f);
                mote.transform.localRotation = Quaternion.Euler(0f, 0f, Reduced ? 0f : _time * 25f + i * 30f);
                mote.transform.localScale = Vector3.one * MoteSize;

                // In at the feet, out at the crown.
                float alpha = Mathf.Sin(phase * Mathf.PI) * 0.85f;
                mote.color = UiTheme.WithAlpha(_colour, Reduced ? 0.6f : alpha);
            }
        }
    }
}