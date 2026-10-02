// Assets/_Project/Scripts/Unity/View/DefianceGlow.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How an operator under Defiance looks (CORE_GAMEPLAY.md, CG14): a warm
    /// gold glow pooled at its feet, breathing slowly, that flares outward as
    /// a ring when it is spent. It replaces the DEFIANCE tag.
    /// </summary>
    /// <remarks>
    /// <b>On the floor, not round the body,</b> so it never competes with a
    /// shield's bubble on the same piece: the bubble says "hits bounce", the
    /// glow underfoot says "this one does not go down".
    ///
    /// <b>The flare is the status ending</b>, the same rule as the shield's
    /// burst: when Defiance leaves <c>ActiveStatusesOn</c> while the piece
    /// is still on the board. Saving the operator is how it usually goes,
    /// and the piece standing on at 1 health in a ring of gold is the moment
    /// players should see. Reduced motion keeps the glow still and fades it.
    /// </remarks>
    public sealed class DefianceGlow : MonoBehaviour
    {
        /// <summary>Order inside the piece's group: with the seat ring (1), under the figure.</summary>
        private const int Order = 1;

        private const float Width = 1.15f;
        private const float Depth = 0.46f;
        private const float RestAlpha = 0.55f;
        private const float BreathSeconds = 2.6f;

        private const float FlareSeconds = 0.55f;
        private const float FlareGrowth = 2.2f;

        private OperatorPiece _piece;
        private MotionSettings _motion;
        private SpriteRenderer _glow;
        private Color _colour;

        private bool _shown;
        private float _time;

        private bool Reduced => _motion != null && _motion.ReducedMotion;

        public void Bind(OperatorPiece piece, Transform frame, MotionSettings motion)
        {
            _piece = piece;
            _motion = motion;
            _colour = Color.Lerp(UiTheme.GoldBright, new Color(1f, 0.85f, 0.45f), 0.4f);

            // Floor-bound, with the seat disc: a child of the piece, not the figure.
            _glow = CueKit.Renderer("defiance_glow", piece.transform, BoardArt.SoftDisc, Order);
            _glow.transform.localScale = new Vector3(Width, Depth, 1f);
        }

        /// <summary>Turns the glow on or off; <paramref name="flare"/> says whether losing it here flares.</summary>
        public void Show(bool on, bool flare)
        {
            if (_shown == on) return;

            bool ending = _shown && !on;
            _shown = on;
            _glow.enabled = on;

            if (ending && flare) Flare();
        }

        private void Update()
        {
            if (!_shown) return;

            _time += Time.deltaTime;
            float breath = Reduced ? 0f : Mathf.Sin(_time * 2f * Mathf.PI / BreathSeconds);
            float scale = 1f + 0.05f * breath;

            _glow.transform.localScale = new Vector3(Width * scale, Depth * scale, 1f);
            _glow.color = UiTheme.WithAlpha(_colour, RestAlpha * (1f + 0.2f * breath));
        }

        /// <summary>A gold ring that grows out from the feet and fades; a plain fade under Reduced motion.</summary>
        private void Flare()
        {
            var go = new GameObject("defiance_flare");
            go.transform.SetParent(_piece.transform, false);
            go.transform.localScale = new Vector3(Width, Depth, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Reduced ? BoardArt.SoftDisc : Primitives.Ring;
            renderer.sortingOrder = Order;
            renderer.color = UiTheme.WithAlpha(_colour, 0.9f);

            go.AddComponent<GrowingRing>().Begin(FlareSeconds, Reduced ? 1f : FlareGrowth, 0.9f);
        }
    }

    /// <summary>A sprite that grows by a factor while it fades, then removes itself.</summary>
    public sealed class GrowingRing : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Vector3 _from;
        private float _growth;
        private float _seconds;
        private float _alpha;
        private float _age;

        public void Begin(float seconds, float growth, float alpha)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _from = transform.localScale;
            _growth = growth;
            _seconds = Mathf.Max(0.01f, seconds);
            _alpha = alpha;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _seconds);
            float eased = 1f - (1f - t) * (1f - t);

            transform.localScale = _from * Mathf.Lerp(1f, _growth, eased);

            if (_renderer != null)
            {
                var colour = _renderer.color;
                colour.a = _alpha * (1f - t);
                _renderer.color = colour;
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }
}