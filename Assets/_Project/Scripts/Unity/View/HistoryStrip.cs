// Assets/_Project/Scripts/Unity/View/HistoryStrip.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The history strip on the right edge: one chip per action, newest on top,
    /// with the detail on hover (GUI increment F2).
    /// </summary>
    /// <remarks>
    /// <b>It replaced the text log as the right-hand panel</b>, which took 360
    /// units of width to show sentences a player mostly skims. The model is a
    /// card game's play history: each chip shows who acted (the piece's shape in
    /// its seat colour), what kind of action it was, and one number. Hovering a
    /// chip opens a card with everything the action caused. The full text log
    /// is still one key away (L), as an overlay.
    ///
    /// <b>Turns are separated</b> by a thin divider in the seat's colour with
    /// the round, so "what did Blue do last turn" is a glance.
    ///
    /// <b>The card lives on the canvas, not in the strip</b>, so the strip's
    /// scroll mask does not clip it. Chips catch the pointer (they are
    /// hoverable). The card never does, so it cannot flicker by stealing the
    /// hover from the chip under it.
    /// </remarks>
    public sealed class HistoryStrip : MonoBehaviour
    {
        public const float Width = 78f;
        private const float ChipHeight = 58f;
        private const int MaxEntries = 90;
        private const float CardWidth = 330f;

        /// <summary>Canvas units the strip claims from the right edge.</summary>
        public static float ReservedWidth => Width;

        /// <summary>Called when the Log button at the top of the strip is pressed.</summary>
        public Action LogRequested { get; set; }

        private RectTransform _canvas;
        private RectTransform _strip;
        private RectTransform _content;
        private ScrollRect _scroll;

        private RectTransform _card;
        private Image _cardAccent;
        private TMP_Text _cardTitle;
        private TMP_Text _cardBody;
        private RectTransform _cardOwner;

        private readonly Vector3[] _corners = new Vector3[4];

        public void Bind(RectTransform canvasRect)
        {
            _canvas = canvasRect;
            if (_strip == null) Build();
            Clear();
        }

        public void Clear()
        {
            if (_content == null) return;

            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            HideCard(null);
        }

        /// <summary>Adds a batch's items, newest ending up on top.</summary>
        public void Add(HistoryBatch batch)
        {
            if (_content == null || batch == null) return;

            foreach (var item in batch.Items)
            {
                var entry = item.Kind == HistoryKind.Divider ? Divider(item) : Chip(item);
                entry.SetAsFirstSibling();
            }

            for (int i = _content.childCount - 1; i >= MaxEntries; i--)
            {
                var child = _content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            _scroll.verticalNormalizedPosition = 1f;
        }

        // ── Scaffold ─────────────────────────────────────────────────────

        private void Build()
        {
            _strip = UiKit.Rect("history_strip", _canvas);
            _strip.anchorMin = new Vector2(1f, 0f);
            _strip.anchorMax = new Vector2(1f, 1f);
            _strip.pivot = new Vector2(1f, 0.5f);
            _strip.offsetMin = new Vector2(-Width, 0f);
            _strip.offsetMax = new Vector2(0f, -TurnStrip.ReservedHeight);
            UiKit.Dock(_strip, true, RectTransform.Edge.Left);

            var column = UiKit.Rect("column", _strip);
            UiKit.Stretch(column);
            UiKit.Column(column, 6f, 5).padding.left = 10; // clear of the double rule

            var log = UiKit.Button(column, "LOG <size=70%>L</size>", () => LogRequested?.Invoke(), size: 13f);
            UiKit.Size(log, height: 28f);

            var scrollRect = UiKit.Rect("scroll", column);
            UiKit.Size(scrollRect, flexibleHeight: 1f);
            _scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;

            var viewport = UiKit.Rect("viewport", scrollRect);
            UiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = UiKit.Rect("content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;
            UiKit.Column(_content, 4f);
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll.viewport = viewport;
            _scroll.content = _content;

            BuildCard();
        }

        private void BuildCard()
        {
            _card = UiKit.Rect("history_card", _canvas);
            _card.anchorMin = new Vector2(0.5f, 0.5f);
            _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(1f, 0.5f);
            _card.sizeDelta = new Vector2(CardWidth, 0f);
            UiKit.Panel(_card, blocksPointer: false);

            // Padding keeps the text clear of the corner fans.
            var column = UiKit.Column(_card, 4f, 16);
            column.padding.left = 24;
            column.padding.right = 20;
            var fit = _card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var accent = UiKit.Rect("accent", _card);
            _cardAccent = UiKit.Fill(accent, UiTheme.Gold);
            accent.anchorMin = Vector2.zero;
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            // Inside the frame, clear of the chamfer.
            accent.sizeDelta = new Vector2(4f, -28f);
            accent.anchoredPosition = new Vector2(10f, 0f);
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            _cardTitle = UiKit.Label(_card, "", UiTheme.FontBody, bold: true, wrap: true);
            _cardBody = UiKit.Label(_card, "", UiTheme.FontSmall, UiTheme.TextDim, wrap: true);

            _card.gameObject.SetActive(false);
        }

        // ── Entries ──────────────────────────────────────────────────────

        private RectTransform Divider(HistoryItem item)
        {
            var colour = BoardLayout.ColourOf(item.Seat);

            var divider = UiKit.Rect("turn", _content);
            UiKit.Size(divider, height: 18f);

            var line = UiKit.Rect("line", divider);
            UiKit.Fill(line, colour);
            line.anchorMin = new Vector2(0f, 0.5f);
            line.anchorMax = new Vector2(1f, 0.5f);
            line.sizeDelta = new Vector2(0f, 2f);

            var tag = UiKit.Rect("round", divider);
            UiKit.Sliced(tag, DecoSprites.ChipFill, UiTheme.Obsidian);
            tag.anchorMin = new Vector2(0.5f, 0f);
            tag.anchorMax = new Vector2(0.5f, 1f);
            tag.sizeDelta = new Vector2(40f, 0f);
            UiKit.Caption(tag, $"R{item.Round}", 11f, UiTheme.Readable(colour), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

            return divider;
        }

        private RectTransform Chip(HistoryItem item)
        {
            var seatColour = BoardLayout.ColourOf(item.Seat);

            var chip = UiKit.Rect($"chip_{item.Kind}", _content);
            UiKit.Sliced(chip, DecoSprites.ChipFill, UiTheme.PanelInset, blocksPointer: true);
            UiKit.Size(chip, height: ChipHeight);

            // Seat colour down the left edge.
            var stripe = UiKit.Rect("seat", chip);
            UiKit.Fill(stripe, seatColour);
            stripe.anchorMin = Vector2.zero;
            stripe.anchorMax = new Vector2(0f, 1f);
            stripe.pivot = new Vector2(0f, 0.5f);
            stripe.sizeDelta = new Vector2(3f, -10f);
            stripe.anchoredPosition = Vector2.zero;

            // Who acted: the piece's own silhouette.
            var icon = UiKit.Rect("icon", chip);
            var image = icon.gameObject.AddComponent<Image>();
            image.sprite = item.Actor != null ? PieceShape.For(item.Actor) : Primitives.Disc;
            image.color = item.Kind == HistoryKind.Win ? UiTheme.GoldBright : seatColour;
            image.preserveAspect = true;
            image.raycastTarget = false;
            icon.anchorMin = new Vector2(0f, 1f);
            icon.anchorMax = new Vector2(0f, 1f);
            icon.pivot = new Vector2(0f, 1f);
            icon.sizeDelta = new Vector2(22f, 22f);
            icon.anchoredPosition = new Vector2(8f, -5f);

            var word = UiKit.Label(chip, item.Word, 10f, UiTheme.TextDim, TextAlignmentOptions.TopRight, bold: true);
            var wordRect = (RectTransform)word.transform;
            wordRect.anchorMin = new Vector2(0f, 1f);
            wordRect.anchorMax = new Vector2(1f, 1f);
            wordRect.pivot = new Vector2(0.5f, 1f);
            wordRect.offsetMin = new Vector2(30f, -20f);
            wordRect.offsetMax = new Vector2(-5f, -6f);
            word.overflowMode = TextOverflowModes.Overflow;

            string value = item.Value;
            if (item.Knockout) value += $" <size=65%><color=#{UiTheme.Hex(UiTheme.Danger)}>KO</color></size>";

            var mark = UiKit.Label(chip, value, 20f, item.ValueColour, TextAlignmentOptions.Bottom, bold: true);
            var markRect = (RectTransform)mark.transform;
            markRect.anchorMin = Vector2.zero;
            markRect.anchorMax = new Vector2(1f, 0f);
            markRect.pivot = new Vector2(0.5f, 0f);
            markRect.offsetMin = new Vector2(4f, 4f);
            markRect.offsetMax = new Vector2(-4f, 30f);
            mark.overflowMode = TextOverflowModes.Overflow;

            var hover = chip.gameObject.AddComponent<HistoryChip>();
            hover.Entered = () => ShowCard(item, chip);
            hover.Exited = () => HideCard(chip);

            return chip;
        }

        // ── Hover card ───────────────────────────────────────────────────

        private void ShowCard(HistoryItem item, RectTransform chip)
        {
            if (_card == null || chip == null) return;

            var colour = BoardLayout.ColourOf(item.Seat);
            _cardAccent.color = colour;

            _cardTitle.text =
                $"<color=#{UiTheme.Hex(UiTheme.Readable(colour))}>{item.Seat}</color>  {item.Title}" +
                $"  <size=75%><color=#{UiTheme.Hex(UiTheme.TextDim)}>round {item.Round}</color></size>";

            _cardBody.text = item.Lines.Count > 0 ? string.Join("\n", item.Lines) : "—";

            _cardOwner = chip;
            _card.gameObject.SetActive(true);
            _card.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_card);

            // Beside the strip, level with the chip, kept on screen.
            _strip.GetWorldCorners(_corners);
            float left = _corners[0].x;

            chip.GetWorldCorners(_corners);
            float y = (_corners[0].y + _corners[1].y) * 0.5f;

            float half = _card.rect.height * _canvas.localScale.y * 0.5f;
            y = Mathf.Clamp(y, half + 4f, Screen.height - half - 4f);

            _card.position = new Vector3(left - 8f, y, 0f);
        }

        private void HideCard(RectTransform chip)
        {
            if (_card == null) return;

            // A late exit from a chip the pointer already left must not hide
            // the card another chip just opened.
            if (chip != null && !ReferenceEquals(chip, _cardOwner)) return;

            _cardOwner = null;
            _card.gameObject.SetActive(false);
        }
    }
}
