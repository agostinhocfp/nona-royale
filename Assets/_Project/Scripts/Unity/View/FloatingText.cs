// Assets/_Project/Scripts/Unity/View/FloatingText.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A short-lived label that rises and fades — damage, healing, a miss.
    /// </summary>
    /// <remarks>
    /// Uses the legacy <see cref="TextMesh"/> and a built-in font rather than
    /// TextMeshPro, for the same reason everything else here is procedural: no
    /// asset to import, nothing to wire. It is ugly and it is temporary.
    ///
    /// The font lookup is defensive because Unity renamed the built-in face —
    /// <c>Arial.ttf</c> before 2022.2, <c>LegacyRuntime.ttf</c> after. If neither
    /// resolves, the label is skipped rather than throwing: losing a damage
    /// number is a nuisance, losing the frame is not.
    /// </remarks>
    public sealed class FloatingText : MonoBehaviour
    {
        private const float Lifetime = 0.9f;
        private const float RiseSpeed = 1.6f;

        private TextMesh _text;
        private float _age;

        private static Font _font;
        private static bool _fontResolved;

        public static FloatingText Spawn(Transform parent, Vector3 position, string message, Color colour, float scale)
        {
            var font = ResolveFont();
            if (font == null) return null;

            var go = new GameObject($"float_{message}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;

            var floating = go.AddComponent<FloatingText>();

            floating._text = go.AddComponent<TextMesh>();
            floating._text.text = message;
            floating._text.font = font;
            floating._text.color = colour;
            floating._text.fontSize = 64;
            floating._text.characterSize = 0.06f;
            floating._text.anchor = TextAnchor.LowerCenter;
            floating._text.alignment = TextAlignment.Center;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 20;

            return floating;
        }

        private static Font ResolveFont()
        {
            if (_fontResolved) return _font;

            _fontResolved = true;

            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    _font = Resources.GetBuiltinResource<Font>(name);
                    if (_font != null) return _font;
                }
                catch
                {
                    // Unity logs its own error for a missing builtin; try the next.
                }
            }

            return _font;
        }

        private void Update()
        {
            _age += Time.deltaTime;

            transform.position += Vector3.up * RiseSpeed * Time.deltaTime;

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