// Assets/_Project/Scripts/Unity/View/FxBurn.cs
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A knockout's burn (LAUNCH_UI_PASS.md, G8c): a copy of the figure as it
    /// stood, burning away from a glowing edge in the seat's colour while the
    /// shards fly. It destroys itself when the figure is gone.
    /// </summary>
    /// <remarks>
    /// <b>A copy, not the piece.</b> The piece still hides at the shatter and
    /// comes back at <see cref="OperatorPiece.Reappear"/> exactly as before, and
    /// never carries an effect material into its next life. The copy takes each
    /// part's sprite, colour, flip, order and world pose at the moment of the
    /// shatter, inside its own <see cref="SortingGroup"/> at the piece's layer
    /// and order. So it draws where the figure drew, and the 2D lights light it
    /// the same way.
    ///
    /// One material instance per burn, shared by its parts so they burn
    /// together, and destroyed with it. It runs on scaled time times
    /// <see cref="MotionSettings.Rate"/>, like <see cref="FxSprite"/>, so pause
    /// freezes it. A dissolve isn't movement, so Reduced motion keeps it.
    ///
    /// When <see cref="ShaderFx.BurnLit"/> is unavailable, <see cref="Spawn"/>
    /// makes nothing and the shatter runs alone, as it did before G8.
    /// </remarks>
    public sealed class FxBurn : MonoBehaviour
    {
        private Material _material;
        private MotionSettings _motion;
        private float _seconds;
        private float _age;

        /// <summary>
        /// Burns a copy of <paramref name="parts"/> over <paramref name="seconds"/>.
        /// False when the effect is unavailable or nothing among the parts is drawn.
        /// </summary>
        /// <param name="parent">Where the copy hangs: the piece's parent, so it outlives the piece's hiding.</param>
        /// <param name="parts">The figure's renderers. Hidden, empty or inactive ones are skipped.</param>
        /// <param name="sortingLayer">The piece's sorting layer, from its group.</param>
        /// <param name="sortingOrder">The piece's sorting order, from its group.</param>
        /// <param name="edge">The burning edge's colour.</param>
        /// <param name="label">Who burns, for the development log line.</param>
        public static bool Spawn(Transform parent, IReadOnlyList<SpriteRenderer> parts, int sortingLayer,
            int sortingOrder, Color edge, float seconds, MotionSettings motion, string label = null)
        {
            if (parts == null) return false;

            var drawn = new List<SpriteRenderer>(parts.Count);
            foreach (var part in parts)
            {
                if (part != null && part.enabled && part.sprite != null && part.gameObject.activeInHierarchy)
                    drawn.Add(part);
            }

            if (drawn.Count == 0) return false;

            var material = ShaderFx.Instance(ShaderFx.BurnLit);
            if (material == null) return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // One line per knockout, so a Play Mode check can tell "ran but
            // too subtle" from "never ran".
            Debug.Log($"[FxBurn] {label ?? parent.name}: {drawn.Count} parts over {seconds.ToString("0.00", CultureInfo.InvariantCulture)} s");
#endif

            material.SetColor(ShaderFx.FadeBurnColor, edge);
            material.SetFloat(ShaderFx.FadeAmount, ShaderFx.FadeNone);

            var root = new GameObject("fx_burn");
            root.transform.SetParent(parent, false);

            var group = root.AddComponent<SortingGroup>();
            group.sortingLayerID = sortingLayer;
            group.sortingOrder = sortingOrder;

            var rootScale = root.transform.lossyScale;

            foreach (var part in drawn)
            {
                var copy = new GameObject(part.name);
                copy.transform.SetParent(root.transform, false);
                copy.transform.SetPositionAndRotation(part.transform.position, part.transform.rotation);
                copy.transform.localScale = Divide(part.transform.lossyScale, rootScale);

                var renderer = copy.AddComponent<SpriteRenderer>();
                renderer.sprite = part.sprite;
                renderer.color = part.color;
                renderer.flipX = part.flipX;
                renderer.flipY = part.flipY;
                renderer.sortingLayerID = part.sortingLayerID;
                renderer.sortingOrder = part.sortingOrder;
                renderer.maskInteraction = part.maskInteraction;
                renderer.sharedMaterial = material;
            }

            var burn = root.AddComponent<FxBurn>();
            burn._material = material;
            burn._motion = motion;
            burn._seconds = Mathf.Max(0.01f, seconds);
            return true;
        }

        private void Update()
        {
            float rate = _motion != null ? _motion.Rate : 1f;
            _age += Time.deltaTime * rate;

            // Eased in: the figure holds while the shards cover it, then burns
            // faster as they clear. Starts at 0, not FadeNone, so no frame is spent
            // with nothing happening.
            float t = Mathf.Clamp01(_age / _seconds);
            _material.SetFloat(ShaderFx.FadeAmount, Mathf.Lerp(0f, ShaderFx.FadeAll, t * t));

            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        private static Vector3 Divide(Vector3 a, Vector3 b) =>
            new Vector3(Safe(a.x, b.x), Safe(a.y, b.y), Safe(a.z, b.z));

        private static float Safe(float a, float b) => Mathf.Abs(b) > 1e-6f ? a / b : a;
    }
}
