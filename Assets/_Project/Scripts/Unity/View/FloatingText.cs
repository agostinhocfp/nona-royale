// Assets/_Project/Scripts/Unity/View/FloatingText.cs
using TMPro;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A short-lived label that rises and fades — damage, healing, a miss.
    /// </summary>
    /// <remarks>
    /// A world-space <see cref="TextMeshPro"/> on the HUD's data face, so a
    /// damage number reads like the tray it belongs to (UI_MOTION.md increment
    /// U3, moved off the TMP default in G5); the legacy <c>TextMesh</c> it
    /// replaces never did. The pop at
    /// birth — a quick settle from 1.35× — is what separates "a number
    /// appeared" from "a hit landed".
    /// </remarks>
    public sealed class FloatingText : MonoBehaviour
    {
        private const float Lifetime = 0.9f;
        private const float RiseSpeed = 1.6f;
        private const float PopFrom = 1.35f;
        private const float PopSeconds = 0.2f;

        // Matched to the old TextMesh (fontSize 64 × characterSize 0.06 ≈ 0.4
        // world units before the caller's scale); tune in Play Mode.
        private const float FontSize = 3.8f;

        private TextMeshPro _text;
        private float _scale;
        private float _age;

        public static FloatingText Spawn(Transform parent, Vector3 position, string message, Color colour, float scale)
        {
            var go = new GameObject($"float_{message}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var floating = go.AddComponent<FloatingText>();
            floating._scale = scale;
            go.transform.localScale = Vector3.one * scale * PopFrom;

            var text = go.AddComponent<TextMeshPro>();
            text.text = message;
            text.color = colour;
            text.fontSize = FontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            // Over everything the board draws (V1b, FigureTilt).
            text.sortingOrder = FigureTilt.FloatingTextOrder;
            UiFonts.ApplyData(text);
            floating._text = text;

            return floating;
        }

        private void Update()
        {
            _age += Time.deltaTime;

            transform.position += Vector3.up * RiseSpeed * Time.deltaTime;

            // The birth pop: overshoot, then settle (UI_MOTION.md U3).
            float pop = UiEasing.Evaluate(UiEase.OutCubic, Mathf.Clamp01(_age / PopSeconds));
            transform.localScale = Vector3.one * _scale * Mathf.LerpUnclamped(PopFrom, 1f, pop);

            if (_text != null)
            {
                var colour = _text.color;
                colour.a = Mathf.Clamp01(1f - _age / Lifetime);
                _text.color = colour;
            }

            if (_age >= Lifetime) Destroy(gameObject);
        }
    }
}
