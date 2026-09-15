// Assets/_Project/Scripts/Unity/View/CellLabelLayer.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Short screen-space labels pinned to board positions. Today: the pips
    /// each landing option spends.
    /// </summary>
    /// <remarks>
    /// <b>Screen-space, like the piece readouts,</b> and for the same reason
    /// (<see cref="PieceHudLayer"/>): a world-space label shrinks with the
    /// framing, and the framing changes with the window.
    ///
    /// <b>Rebuilt, not updated.</b> Labels follow the selection, which changes
    /// on clicks rather than frames, and there are never more than a handful.
    ///
    /// <b>Drawn beneath the rest of the HUD.</b> The container is the canvas's
    /// first child, so a label never covers a panel or a piece readout.
    ///
    /// Nothing here catches the pointer (ADR-0008 consequence 9): a label sits
    /// on the very cell the player is about to click.
    /// </remarks>
    public sealed class CellLabelLayer : MonoBehaviour
    {
        private const float FontSize = 17f;
        private const float Height = 22f;

        // Landing pips are live information, so the cool register (ART_DIRECTION §8).
        private static Color StrongBack => UiTheme.Scrim;
        private static Color FaintBack => UiTheme.WithAlpha(UiTheme.Obsidian, 0.6f);
        private static Color StrongText => UiTheme.CyanBright;
        private static Color FaintText => UiTheme.WithAlpha(UiTheme.Cyan, 0.8f);

        private sealed class Label
        {
            public GameObject Root;
            public RectTransform Rect;
            public Vector3 World;
        }

        private readonly List<Label> _labels = new List<Label>();
        private RectTransform _canvasRect;
        private RectTransform _container;

        public void Bind(RectTransform canvasRect)
        {
            _canvasRect = canvasRect;

            if (_container == null)
            {
                var go = new GameObject("cell_labels", typeof(RectTransform));
                _container = (RectTransform)go.transform;
                _container.SetParent(canvasRect, false);
                _container.anchorMin = new Vector2(0.5f, 0.5f);
                _container.anchorMax = new Vector2(0.5f, 0.5f);
                _container.sizeDelta = Vector2.zero;
            }

            _container.SetAsFirstSibling();
            Clear();
        }

        public void Clear()
        {
            foreach (var label in _labels)
                if (label.Root != null) Destroy(label.Root);

            _labels.Clear();
        }

        /// <summary>Pins <paramref name="text"/> to a world position until the next <see cref="Clear"/>.</summary>
        public void Show(Vector3 world, string text, bool strong)
        {
            if (_container == null) return;

            var go = new GameObject($"label_{text}", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_container, false);
            rect.sizeDelta = new Vector2(Mathf.Max(Height, 12f * text.Length + 10f), Height);

            var back = go.AddComponent<Image>();
            back.sprite = DecoSprites.ChipFill;
            back.type = Image.Type.Sliced;
            back.color = strong ? StrongBack : FaintBack;
            back.raycastTarget = false;

            var textGo = new GameObject("text", typeof(RectTransform));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var label = textGo.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = FontSize;
            label.fontStyle = strong ? FontStyles.Bold : FontStyles.Normal;
            label.color = strong ? StrongText : FaintText;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.text = text;

            var entry = new Label { Root = go, Rect = rect, World = world };
            _labels.Add(entry);
            Place(entry, Camera.main);
        }

        private void LateUpdate()
        {
            if (_labels.Count == 0) return;

            var camera = Camera.main;
            foreach (var label in _labels) Place(label, camera);
        }

        private void Place(Label label, Camera camera)
        {
            if (camera == null || _canvasRect == null || label.Rect == null) return;

            Vector2 screen = camera.WorldToScreenPoint(label.World);

            // Overlay canvas, so no camera in the conversion. The container
            // is centred on the canvas, so the local point is its position.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, screen, null, out var local);
            label.Rect.anchoredPosition = local;
        }
    }
}
