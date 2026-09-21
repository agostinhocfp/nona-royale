// Assets/_Project/Scripts/Unity/View/GlossaryCard.cs
using NonaRoyale.Core.Model;
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
    ///
    /// <b>It also carries a passive or an aura</b> (<see cref="ShowTrait"/>,
    /// 2026-09-21), opened from the chips on the match's operator card. Same
    /// frame, same one-at-a-time rule: a keyword tapped in a trait's rules line
    /// replaces it with that keyword's entry.
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

            var card = Frame(host, GroupWord(entry.Group), entry.Title, RulesMarkup.ColourOf(entry.Id));

            if (entry.Status.HasValue) PieceTag(card, entry.Status.Value);

            var definition = UiKit.Label(card, RulesMarkup.For(entry.Definition, linked: true),
                UiTheme.FontBody, UiTheme.Text, wrap: true);
            KeywordLinks.Attach(definition, next => Show(host, next));

            Finish(card);
        }

        /// <summary>
        /// Opens a passive or an aura over <paramref name="host"/>: whose it is,
        /// its rules line and its flavour — the dossier's entry for it, alone.
        /// </summary>
        public static void ShowTrait(RectTransform host, string operatorName, KitTrait trait)
        {
            if (host == null || trait == null) return;

            string group = (trait.Kind == TraitKind.Aura ? $"Aura · r{trait.Radius}" : "Passive") +
                           (string.IsNullOrEmpty(operatorName) ? "" : " · " + operatorName);

            var card = Frame(host, group, trait.Name, UiTheme.GoldBright);

            if (trait.Status.HasValue) PieceTag(card, trait.Status.Value);

            var rules = UiKit.Label(card, RulesMarkup.For(trait.Line, linked: true),
                UiTheme.FontBody, UiTheme.Text, wrap: true);
            KeywordLinks.Attach(rules, next => Show(host, next));

            if (!string.IsNullOrEmpty(trait.Description))
            {
                var words = UiKit.Label(card, trait.Description, UiTheme.FontSmall, UiTheme.TextDim, wrap: true);
                words.fontStyle = FontStyles.Italic;
            }

            Finish(card);
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

        // ── The frame ────────────────────────────────────────────────────

        /// <summary>
        /// Closes whatever is up and opens an empty card: a dimmed blocker over
        /// the whole host that closes on a tap, and a centred panel with the
        /// small heading and the title. The caller fills it and calls
        /// <see cref="Finish"/>.
        /// </summary>
        private static RectTransform Frame(RectTransform host, string heading, string titleText, Color titleColour)
        {
            Close();

            var blocker = UiKit.Rect("glossary_card", host);
            blocker.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UiKit.Stretch(blocker);
            var dim = UiKit.Fill(blocker, UiTheme.WithAlpha(UiTheme.Obsidian, 0.62f), blocksPointer: true);
            var dismiss = blocker.gameObject.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.targetGraphic = dim;
            dismiss.onClick.AddListener(Close);
            blocker.SetAsLastSibling();
            _open = blocker;

            // The card, centred and as tall as its words.
            float width = Mathf.Min(MaxWidth, Mathf.Max(240f, host.rect.width - 32f));
            var card = UiKit.Rect("card", blocker);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(width, 0f);
            UiKit.Panel(card, blocksPointer: true);
            UiKit.Column(card, 8f, 22).childForceExpandHeight = false;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UiKit.Size(UiKit.Heading(card, heading), height: 18f);

            var title = UiKit.Label(card, titleText, UiTheme.FontTitle, titleColour, bold: true);
            UiFonts.ApplyDisplay(title);
            title.overflowMode = TextOverflowModes.Overflow;
            UiKit.Size(title, height: 34f);

            return card;
        }

        /// <summary>The board's tag for a status, so the card and the chip under a piece match.</summary>
        private static void PieceTag(RectTransform card, StatusKind kind)
        {
            var tagRow = UiKit.Rect("tag", card);
            UiKit.Size(tagRow, height: 22f);
            var row = UiKit.Row(tagRow, 8f);
            row.childForceExpandHeight = true;
            UiKit.Label(tagRow, "On a piece:", UiTheme.FontSmall, UiTheme.TextDim);
            UiKit.Tag(tagRow, StatusPalette.Label(kind), StatusPalette.For(kind), 12f);
            UiKit.Space(tagRow, flexible: true);
        }

        /// <summary>CLOSE, then lay the card out and pop it in.</summary>
        private static void Finish(RectTransform card)
        {
            UiKit.Space(card, height: 4f);

            var close = UiKit.Button(card, "CLOSE", Close, size: UiTheme.FontSmall);
            UiKit.Size(close, height: 40f);

            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            UiPopIn.On(card);
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
