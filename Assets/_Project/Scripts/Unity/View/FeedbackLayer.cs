// Assets/_Project/Scripts/Unity/View/FeedbackLayer.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Plays the one-off effects that explain what just happened: numbers,
    /// misses, blocks, and the burst where an operator was neutralized.
    /// </summary>
    /// <remarks>
    /// <c>COMBAT_SYSTEMS</c> §9.3 says <c>DamageDealt</c>, <c>DamageEvaded</c>
    /// and <c>DamageAbsorbed</c> are separate events precisely because the view
    /// has to play three visibly different things. Until now it played none of
    /// them, so an evaded hit and a landed one looked identical on the board and
    /// the difference lived only in the log.
    /// </remarks>
    public sealed class FeedbackLayer : MonoBehaviour
    {
        private float _scale = 1f;

        public void Bind(float cellSize) => _scale = cellSize;

        public void Damage(Vector3 at, int amount) =>
            FloatingText.Spawn(transform, at, $"-{amount}", new Color(1f, 0.45f, 0.40f), _scale);

        public void Heal(Vector3 at, int amount) =>
            FloatingText.Spawn(transform, at, $"+{amount}", new Color(0.50f, 0.95f, 0.55f), _scale);

        public void Evaded(Vector3 at)
        {
            FloatingText.Spawn(transform, at, "MISS", new Color(0.45f, 0.90f, 0.85f), _scale);
            Pulse(at, new Color(0.45f, 0.90f, 0.85f, 0.8f), 2.6f);
        }

        public void Absorbed(Vector3 at)
        {
            FloatingText.Spawn(transform, at, "BLOCK", new Color(0.85f, 0.87f, 0.95f), _scale);
            Pulse(at, new Color(0.85f, 0.87f, 0.95f, 0.9f), 2.2f);
        }

        /// <summary>
        /// The burst where an operator fell. Played at the cell it died on, not
        /// at the yard it reappears in — the piece teleports home, and without
        /// this there is nothing at all to mark the most consequential event in
        /// the game.
        /// </summary>
        public void Neutralized(Vector3 at, Color seatColour)
        {
            FloatingText.Spawn(transform, at, "DOWN", seatColour, _scale * 1.2f);
            Pulse(at, seatColour, 4.2f);
        }

        private void Pulse(Vector3 at, Color colour, float finalScale)
        {
            var go = new GameObject("pulse");
            go.transform.SetParent(transform, false);
            go.transform.position = at;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Primitives.Ring;
            renderer.color = colour;
            renderer.sortingOrder = 15;

            go.AddComponent<ExpandingPulse>().Begin(_scale * 0.5f, _scale * finalScale);
        }
    }

    /// <summary>An expanding, fading ring. Self-destructing, so nothing tracks it.</summary>
    public sealed class ExpandingPulse : MonoBehaviour
    {
        private const float Lifetime = 0.45f;

        private SpriteRenderer _renderer;
        private float _from;
        private float _to;
        private float _age;

        public void Begin(float from, float to)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _from = from;
            _to = to;
            transform.localScale = Vector3.one * from;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Lifetime);

            transform.localScale = Vector3.one * Mathf.Lerp(_from, _to, t);

            if (_renderer != null)
            {
                var colour = _renderer.color;
                colour.a = 1f - t;
                _renderer.color = colour;
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }
}