// Assets/_Project/Scripts/Unity/View/KeyHelpCard.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Every keyboard shortcut on one card, up while F1 is held
    /// (CORE_GAMEPLAY.md, CG6).
    /// </summary>
    /// <remarks>
    /// <b>Held, not toggled.</b> Letting go closes it, so it never needs
    /// dismissing and never sits over a turn. It takes no input: keys still
    /// reach the game and the pointer passes through it.
    ///
    /// <b>Its own canvas, sorted above everything.</b> The full-screen cards
    /// bring themselves to the front every frame (the draft does), so a
    /// sibling-order overlay would be buried under them. A nested canvas with
    /// a high sorting order is drawn last whatever the order of its siblings.
    ///
    /// <b>Only what a player can press.</b> The dev keys stay out, and on a
    /// touch screen with no keyboard the card is never opened.
    /// </remarks>
    public static class KeyHelpCard
    {
        private const float Width = 560f;
        private const float KeyColumn = 190f;
        private const int SortingOrder = 1000;

        private static RectTransform _open;

        public static bool IsOpen => _open != null;

        /// <summary>Opens the card over <paramref name="host"/>: the match's keys and the draft's where they apply, and the ones that work anywhere.</summary>
        public static void Show(RectTransform host, bool match, bool draft)
        {
            Close();
            if (host == null) return;

            var layer = UiKit.Rect("key_help", host);
            layer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UiKit.Stretch(layer);
            var canvas = layer.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            UiKit.Fill(layer, UiTheme.WithAlpha(UiTheme.Obsidian, 0.45f), blocksPointer: false);
            _open = layer;

            float width = Mathf.Min(Width, Mathf.Max(260f, host.rect.width - 32f));
            var card = UiKit.Rect("card", layer);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(width, 0f);
            UiKit.Panel(card, blocksPointer: false);
            UiKit.Column(card, 4f, 22).childForceExpandHeight = false;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = UiKit.Label(card, "Keys", UiTheme.FontTitle, UiTheme.GoldBright, bold: true);
            UiFonts.ApplyDisplay(title);
            UiKit.Size(title, height: 34f);

            if (match)
            {
                Section(card, "Match");
                Line(card, "Space", "Roll · hold to hurry the CPUs");
                Line(card, "E", "End turn");
                Line(card, "Tab · Shift+Tab", "Next · previous operator");
                Line(card, "1 – 3", "Pick an ability");
                Line(card, "Enter", "Cast · or double-click the target");
                Line(card, "Esc · right-click", "Step back, then pause");
                Line(card, "H", "Health above pieces");
                Line(card, "L", "Full log");
                Line(card, "Mouse wheel", "Zoom the board");
            }

            if (draft)
            {
                Section(card, "Draft");
                Line(card, "1 – 4", "Choose the seat to pick for");
                Line(card, "Backspace", "Undo the last pick");
                Line(card, "Enter", "Start the match");
                Line(card, "Esc", "Back to setup");
            }

            Section(card, "Anywhere");
            Line(card, "M", "Mute all sound");
            Line(card, "Shift+M", "Mute the music");
            Line(card, "Hold F1", "This card");

            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        }

        public static void Close()
        {
            if (_open == null) return;

            var go = _open.gameObject;
            _open = null;
            go.SetActive(false);
            Object.Destroy(go);
        }

        private static void Section(RectTransform card, string heading)
        {
            UiKit.Space(card, height: 6f);
            UiKit.Size(UiKit.Heading(card, heading), height: 20f);
        }

        private static void Line(RectTransform card, string key, string what)
        {
            var line = UiKit.Rect("line", card);
            UiKit.Size(line, height: 26f, flexibleHeight: 0f);
            UiKit.Row(line, 12f).childForceExpandHeight = true;

            var keys = UiKit.Label(line, key, UiTheme.FontSmall, UiTheme.Gold, bold: true);
            keys.overflowMode = TextOverflowModes.Overflow;
            UiKit.Fixed(keys, KeyColumn);

            var words = UiKit.Label(line, what, UiTheme.FontSmall, UiTheme.Text);
            UiKit.Size(words, flexibleWidth: 1f);
        }
    }
}