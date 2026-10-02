// Assets/_Project/Scripts/Unity/View/ShieldBubble.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a shielded operator looks (CORE_GAMEPLAY.md, CG11): a faint steel
    /// bubble round the piece that breathes, and bursts into shards when the
    /// shield is spent. It replaces the SHIELD tag.
    /// </summary>
    /// <remarks>
    /// <b>The burst is the shield ending, not a hit.</b> The piece learns that
    /// the shield is gone the way it learns everything (PRESENTATION §1): the
    /// status is no longer in <c>ActiveStatusesOn</c>. Absorbing a hit is the
    /// common way it goes, so the burst lands on the beat a player expects,
    /// and an expiry or a cleanse bursting it too is still true to the board.
    /// A piece knocked out, sent to the yard or hidden loses its bubble with
    /// no burst, since the shatter or the yard says more.
    ///
    /// <b>Mostly rim.</b> The bubble's centre is nearly clear and its edge
    /// carries the colour, with a highlight at the upper left, so the figure
    /// inside stays readable and the outline says "sealed". Reduced motion
    /// keeps the bubble still and lets it fade instead of bursting.
    /// </remarks>
    public sealed class ShieldBubble : MonoBehaviour
    {
        /// <summary>Order inside the piece's group: over the body (4), under the overlays (40).</summary>
        private const int Order = 6;

        private const float RestAlpha = 0.42f;
        private const float BreathSeconds = 3.2f;
        private const float BreathScale = 0.018f;

        /// <summary>How much bigger than the body the bubble is drawn.</summary>
        private const float Padding = 1.28f;

        private const int ShardCount = 9;
        private const float ShardSeconds = 0.42f;
        private const float ShardSpeed = 1.5f;
        private const float FadeSeconds = 0.25f;

        private static Sprite _bubble;

        private OperatorPiece _piece;
        private Transform _frame;
        private MotionSettings _motion;
        private SpriteRenderer _renderer;
        private Color _colour;

        private bool _shown;
        private float _time;

        private bool Reduced => _motion != null && _motion.ReducedMotion;

        public void Bind(OperatorPiece piece, Transform frame, MotionSettings motion)
        {
            _piece = piece;
            _frame = frame;
            _motion = motion;
            _colour = Color.Lerp(StatusPalette.For(StatusKind.Shield), Color.white, 0.25f);

            var go = new GameObject("shield_bubble");
            go.transform.SetParent(frame, false);

            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.sprite = Bubble;
            _renderer.sortingOrder = Order;
            _renderer.enabled = false;
        }

        /// <summary>
        /// Turns the bubble on or off. <paramref name="burst"/> says whether
        /// losing it here should burst it: the piece is still on the board.
        /// </summary>
        public void Show(bool on, bool burst)
        {
            if (_shown == on) return;

            bool ending = _shown && !on;
            _shown = on;
            if (_renderer == null) return;

            _renderer.enabled = on;
            if (ending && burst) Burst();
        }

        private void Update()
        {
            if (!_shown || _renderer == null || _piece == null) return;

            _time += Time.deltaTime;
            _piece.CueBody(out float centre, out float radius);

            float breath = Reduced ? 0f : Mathf.Sin(_time * 2f * Mathf.PI / BreathSeconds);
            float size = 2f * radius * Padding * (1f + BreathScale * breath);

            _renderer.transform.localPosition = new Vector3(0f, centre, 0f);
            _renderer.transform.localScale = new Vector3(size, size, 1f);
            _renderer.color = UiTheme.WithAlpha(_colour, RestAlpha * (1f + 0.12f * breath));
        }

        /// <summary>Shards outward from the rim, fading as they go; or, under Reduced motion, a plain fade.</summary>
        private void Burst()
        {
            if (_piece == null || _frame == null) return;

            _piece.CueBody(out float centre, out float radius);
            float rim = radius * Padding;
            var origin = new Vector3(0f, centre, 0f);

            if (Reduced)
            {
                var ghost = Piece("shield_fade", Bubble, origin, 2f * rim, 0f);
                ghost.AddComponent<FadingGhost>().Begin(FadeSeconds, RestAlpha);
                return;
            }

            for (int i = 0; i < ShardCount; i++)
            {
                float angle = 2f * Mathf.PI * (i + 0.35f * Mathf.Sin(i * 12.9898f)) / ShardCount;
                var outward = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                var shard = Piece("shield_shard", Primitives.Polygon(3, i * 37f), origin + outward * rim, 0.09f + 0.03f * (i % 3), i * 40f);
                shard.AddComponent<FlyingShard>().Begin(outward * ShardSpeed * rim, ShardSeconds, 0.85f);
            }
        }

        private GameObject Piece(string name, Sprite sprite, Vector3 position, float size, float spin)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_frame, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, spin);
            go.transform.localScale = new Vector3(size, size, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = Order;
            renderer.color = UiTheme.WithAlpha(_colour, 0.85f);
            return go;
        }

        /// <summary>A soft bubble: nearly clear inside, bright at the rim, with a highlight at the upper left.</summary>
        private static Sprite Bubble => _bubble != null ? _bubble : (_bubble = BuildBubble(128));

        private static Sprite BuildBubble(int size)
        {
            var pixels = new Color32[size * size];
            float centre = (size - 1) * 0.5f;
            var light = new Vector2(-0.42f, 0.46f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = 0f;
                    if (r <= 1f)
                    {
                        // A thin fill, a rim that rises over the last fifth, and an anti-aliased edge.
                        float fill = 0.10f;
                        float rimGlow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 0.97f, r));
                        float edge = Mathf.Clamp01((1f - r) * size * 0.5f);
                        float spot = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(dx, dy), light) / 0.28f);

                        alpha = Mathf.Clamp01(fill + 0.75f * rimGlow + 0.55f * spot * spot) * edge;
                    }

                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "shield_bubble",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }

    /// <summary>A sprite that flies along a velocity, slows, spins and fades, then removes itself.</summary>
    public sealed class FlyingShard : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Vector3 _velocity;
        private float _seconds;
        private float _from;
        private float _age;

        public void Begin(Vector3 velocity, float seconds, float from)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _velocity = velocity;
            _seconds = Mathf.Max(0.01f, seconds);
            _from = from;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _seconds);

            transform.localPosition += _velocity * (Time.deltaTime * (1f - t));
            transform.localRotation *= Quaternion.Euler(0f, 0f, 360f * Time.deltaTime);

            if (_renderer != null)
            {
                var colour = _renderer.color;
                colour.a = _from * (1f - t);
                _renderer.color = colour;
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }
}