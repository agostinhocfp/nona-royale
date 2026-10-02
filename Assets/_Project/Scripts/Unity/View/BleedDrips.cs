// Assets/_Project/Scripts/Unity/View/BleedDrips.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a bleeding operator looks (CORE_GAMEPLAY.md, CG14): dark red
    /// drops falling from the body to the floor, more often the more stacks
    /// it carries. It replaces the BLEED tag.
    /// </summary>
    /// <remarks>
    /// <b>The rate is the size.</b> Bleed stacks add (§5.3), so the drip
    /// quickens with them: one stack drips about once a second, three or
    /// more about three times a second. The count comes from
    /// <c>GameEngine.BleedStacksOn</c>; the exact damage stays in the tray.
    /// Each drop lands in a small stain that fades. Reduced motion shows one
    /// still drop on the body instead.
    /// </remarks>
    public sealed class BleedDrips : MonoBehaviour
    {
        private const int Order = 5;

        private const float DropWidth = 0.035f;
        private const float DropLength = 0.075f;
        private const float FallSeconds = 0.45f;

        private const float StainWidth = 0.14f;
        private const float StainDepth = 0.05f;
        private const float StainSeconds = 0.6f;

        private OperatorPiece _piece;
        private Transform _frame;
        private MotionSettings _motion;
        private SpriteRenderer _still;
        private Color _colour;

        private bool _shown;
        private int _stacks;
        private float _untilNext;
        private int _side;

        private bool Reduced => _motion != null && _motion.ReducedMotion;

        public void Bind(OperatorPiece piece, Transform frame, MotionSettings motion)
        {
            _piece = piece;
            _frame = frame;
            _motion = motion;
            _colour = Color.Lerp(StatusPalette.For(StatusKind.Bleed), new Color(0.45f, 0.02f, 0.05f), 0.35f);

            _still = CueKit.Renderer("bleed_still", frame, Primitives.Disc, Order);
        }

        /// <summary>Turns the drip on or off; <paramref name="stacks"/> sets its rate.</summary>
        public void Show(bool on, int stacks)
        {
            _stacks = Mathf.Max(1, stacks);
            if (_shown == on) return;

            _shown = on;
            _untilNext = 0.15f;
            _still.enabled = on && Reduced;
        }

        private void Update()
        {
            if (!_shown || _piece == null) return;

            _piece.CueBody(out float centre, out float radius);

            if (Reduced)
            {
                _still.enabled = true;
                _still.transform.localPosition = new Vector3(radius * 0.3f, centre - radius * 0.1f, 0f);
                _still.transform.localScale = new Vector3(DropWidth * 1.4f, DropLength * 1.4f, 1f);
                _still.color = UiTheme.WithAlpha(_colour, 0.9f);
                return;
            }

            _still.enabled = false;
            _untilNext -= Time.deltaTime;
            if (_untilNext > 0f) return;

            // About one drop a second, up to three, with a little unevenness.
            float perSecond = Mathf.Min(3f, _stacks);
            _untilNext = (1f / perSecond) * (0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 3f, 0.7f));

            _side = (_side + 1) % 3;
            float x = radius * (_side - 1) * 0.28f;
            float top = centre - radius * 0.05f;
            float floor = centre - radius;

            var drop = new GameObject("bleed_drop");
            drop.transform.SetParent(_frame, false);
            drop.transform.localPosition = new Vector3(x, top, 0f);
            drop.transform.localScale = new Vector3(DropWidth, DropLength, 1f);
            var renderer = drop.AddComponent<SpriteRenderer>();
            renderer.sprite = Primitives.Disc;
            renderer.sortingOrder = Order;
            renderer.color = UiTheme.WithAlpha(_colour, 0.95f);
            drop.AddComponent<FallingDrop>().Begin(top - floor, FallSeconds, StainSeconds,
                new Vector3(StainWidth, StainDepth, 1f));
        }
    }

    /// <summary>A drop that falls a distance, then flattens into a stain that fades and removes itself.</summary>
    public sealed class FallingDrop : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Vector3 _start;
        private float _distance;
        private float _fall;
        private float _stain;
        private Vector3 _stainScale;
        private float _age;
        private float _alpha;

        public void Begin(float distance, float fallSeconds, float stainSeconds, Vector3 stainScale)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _start = transform.localPosition;
            _distance = Mathf.Max(0f, distance);
            _fall = Mathf.Max(0.01f, fallSeconds);
            _stain = Mathf.Max(0.01f, stainSeconds);
            _stainScale = stainScale;
            _alpha = _renderer != null ? _renderer.color.a : 1f;
        }

        private void Update()
        {
            _age += Time.deltaTime;

            if (_age < _fall)
            {
                // Gravity: slow at first, quick at the end.
                float t = _age / _fall;
                transform.localPosition = _start - new Vector3(0f, _distance * t * t, 0f);
                return;
            }

            float s = Mathf.Clamp01((_age - _fall) / _stain);
            transform.localPosition = _start - new Vector3(0f, _distance, 0f);
            transform.localScale = _stainScale;

            if (_renderer != null)
            {
                var colour = _renderer.color;
                colour.a = _alpha * 0.7f * (1f - s);
                _renderer.color = colour;
            }

            if (s >= 1f) Destroy(gameObject);
        }
    }
}