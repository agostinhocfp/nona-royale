// Assets/_Project/Scripts/Unity/View/GlossaryCard.cs
using NonaRoyale.Core.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The card a tapped keyword opens (OPERATOR_GUIDE.md §3): what the word
    /// means, over whatever screen it was tapped on.
    /// </summary>
    /// <remarks>
    /// <b>One at a time, anywhere.</b> The dossier on the title, the draft's
    /// panel and the draft's full dossier all open the same card, so it is
    /// static: a keyword tapped inside the card replaces it rather than
    /// stacking a second one, and Esc closes whichever is up.
    ///
    /// <b>The board's own tag is shown for a status</b>, so the word in the
    /// guide and the chip under a piece are recognisably the same thing — which
    /// is the Stranger Test's debrief question 4 answered in place.
    /// </remarks>
    public static class GlossaryCard
    {
        private const float MaxWidth = 460f;

        private static RectTransform _open;

        /// <summary>True while a card is up.</summary>
        public static bool IsOpen => _open != null;

        /// <summary>Opens the entry for <paramref name="id"/> over <paramref name="host"/>. Unknown ids are ignored.</summary>
        public static void Show(RectTransform host, string id)
        {
            if (host == null || string.IsNullOrEmpty(id)) return;

            var entry = Glossary.Find(id);
            if (entry == null) return;

            Close();

            // The blocker: the whole host, dimmed, and a tap on it closes.
            var blocker = UiKit.Rect("glossary_card", host);
            blocker.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UiKit.Stretch(blocker);
            var dim = UiKit.Fill(blocker, UiTheme.WithAlpha(UiTheme.Obsidian, 0.62f), blocksPointer: true);
            var dismiss = blocker.gameObject.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.targetGraphic = dim;
            dismiss.onClick.AddListener(Close);
            blocker.SetAsLastSibling();

            // The card, centred and as tall as its words.
            float width = Mathf.Min(MaxWidth, Mathf.Max(240f, host.rect.width - 32f));
            var card = UiKit.Rect("card", blocker);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(width, 0f);
            UiKit.Panel(card, blocksPointer: true);
            UiKit.Column(card, 8f, 22).childForceExpandHeight = false;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UiKit.Size(UiKit.Heading(card, GroupWord(entry.Group)), height: 18f);

            var title = UiKit.Label(card, entry.Title, UiTheme.FontTitle, RulesMarkup.ColourOf(entry.Id), bold: true);
            UiFonts.ApplyDisplay(title);
            title.overflowMode = TextOverflowModes.Overflow;
            UiKit.Size(title, height: 34f);

            if (entry.Status.HasValue)
            {
                var tagRow = UiKit.Rect("tag", card);
                UiKit.Size(tagRow, height: 22f);
                var row = UiKit.Row(tagRow, 8f);
                row.childForceExpandHeight = true;
                UiKit.Label(tagRow, "On a piece:", UiTheme.FontSmall, UiTheme.TextDim);
                UiKit.Tag(tagRow, StatusPalette.Label(entry.Status.Value), StatusPalette.For(entry.Status.Value), 12f);
                UiKit.Space(tagRow, flexible: true);
            }

            var definition = UiKit.Label(card, RulesMarkup.For(entry.Definition, linked: true),
                UiTheme.FontBody, UiTheme.Text, wrap: true);
            KeywordLinks.Attach(definition, next => Show(host, next));

            UiKit.Space(card, height: 4f);

            var close = UiKit.Button(card, "CLOSE", Close, size: UiTheme.FontSmall);
            UiKit.Size(close, height: 40f);

            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            UiPopIn.On(card);

            _open = blocker;
        }

        /// <summary>Closes the card, if one is up.</summary>
        public static void Close()
        {
            if (_open == null) return;

            var go = _open.gameObject;
            _open = null;
            go.SetActive(false);
            Object.Destroy(go);
        }

        private static string GroupWord(GlossaryGroup group)
        {
            switch (group)
            {
                case GlossaryGroup.Status: return "Status";
                case GlossaryGroup.Damage: return "Damage type";
                default: return "Rule";
            }
        }
    }
}
