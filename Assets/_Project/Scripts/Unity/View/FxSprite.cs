// Assets/_Project/Scripts/Unity/View/FxSprite.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>One pose of a one-shot effect sprite.</summary>
    public struct FxPose
    {
        public Vector3 Position;
        public Vector3 Scale;
        public float Rotation;
        public float Alpha;

        public FxPose(Vector3 position, Vector3 scale, float rotation = 0f, float alpha = 1f)
        {
            Position = position;
            Scale = scale;
            Rotation = rotation;
            Alpha = alpha;
        }
    }

    /// <summary>
    /// A world-space sprite that tweens from one pose to another and then
    /// destroys itself (MOTION.md increment MO2): knockout shards, cast
    /// tells, deploy rings.
    /// </summary>
    /// <remarks>
    /// Scaled time, times the shared <see cref="MotionSettings.Rate"/>, so
    /// pause freezes it. Nothing tracks it and nothing catches the pointer:
    /// there are no colliders anywhere on the board.
    /// </remarks>
    public sealed class FxSprite : MonoBehaviour
    {
        public enum Ease { Linear, Out, In }

        private SpriteRenderer _renderer;
        private MotionSettings _motion;
        private FxPose _from;
        private FxPose _to;
        private Color _colour;
        private float _seconds;
        private float _age;
        private Ease _ease;

        /// <summary>Spawns the effect. <paramref name="delay"/> holds it hidden at its first pose.</summary>
        public static FxSprite Spawn(Transform parent, Sprite sprite, Color colour, int sortingOrder,
            FxPose from, FxPose to, float seconds, MotionSettings motion, float delay = 0f, Ease ease = Ease.Out)
        {
            var go = new GameObject("fx");
            go.transform.SetParent(parent, false);

            var fx = go.AddComponent<FxSprite>();
            fx._renderer = go.AddComponent<SpriteRenderer>();
            fx._renderer.sprite = sprite;
            fx._renderer.sortingOrder = sortingOrder;
            fx._colour = colour;
            fx._motion = motion;
            fx._from = from;
            fx._to = to;
            fx._seconds = Mathf.Max(0.01f, seconds);
            fx._age = -Mathf.Max(0f, delay);
            fx._ease = ease;
            fx.Apply(0f, visible: delay <= 0f);
            return fx;
        }

        private void Update()
        {
            float rate = _motion != null ? _motion.Rate : 1f;
            _age += Time.deltaTime * rate;

            if (_age < 0f) return;

            float t = Mathf.Clamp01(_age / _seconds);
            Apply(t, visible: true);

            if (t >= 1f) Destroy(gameObject);
        }

        private void Apply(float t, bool visible)
        {
            float e = _ease == Ease.Out ? 1f - (1f - t) * (1f - t)
                    : _ease == Ease.In ? t * t
                    : t;

            transform.position = Vector3.Lerp(_from.Position, _to.Position, e);
            transform.localScale = Vector3.Lerp(_from.Scale, _to.Scale, e);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(_from.Rotation, _to.Rotation, e));

            var colour = _colour;
            colour.a = visible ? _colour.a * Mathf.Lerp(_from.Alpha, _to.Alpha, e) : 0f;
            _renderer.color = colour;
        }
    }
}