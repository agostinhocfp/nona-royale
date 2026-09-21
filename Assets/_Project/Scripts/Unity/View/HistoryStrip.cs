// Assets/_Project/Scripts/Unity/View/HistoryStrip.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The history: one chip per action, newest first, with the detail on
    /// hover (GUI increment F2). A strip down the right edge on a wide screen;
    /// a rail above the tray on an upright one.
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
    /// <b>Upright it lies down over the tray</b> (MOBILE.md, M3), newest on the
    /// left and scrolled sideways, at 62 units tall rather than 78 wide. The
    /// same chips, turned through ninety degrees: it is the one piece of the
    /// HUD whose content is a queue, and a queue reads along either axis.
    ///
    /// <b>A finger has no hover, so a chip is also a tap</b> (M4). On a touch
    /// screen tapping a chip opens its card and tapping it again — or any other
    /// chip — closes it, which is the same one-gesture contract the hover has,
    /// without needing a pointer that can rest somewhere.
    ///
    /// <b>The card lives on the canvas, not in the strip</b>, so the strip's
    /// scroll mask does not clip it. Chips catch the pointer (they are
    /// hoverable). The card never does, so it cannot flicker by stealing the
    /// hover from the chip under it.
    /// </remarks>
    public sealed class HistoryStrip : MonoBehaviour
    {
        public const float Width = 78f;

        /// <summary>The rail's height when the history is lying down (M3).</summary>
        public const float BandHeight = 62f;

        private const float ChipHeight = 58f;
        private const float ChipWidth = 64f;
        private const int MaxEntries = 90;
        private const float CardWidth = 330f;

        /// <summary>Canvas units the strip claims from the right edge. Nothing, lying down.</summary>
        public static float ReservedWidth => ScreenLayout.Pick(Width, 0f);

        /// <summary>Canvas units the rail claims above the tray. Nothing, standing up.</summary>
        public static float ReservedHeight => ScreenLayout.Pick(0f, BandHeight);

        /// <summary>Called when the Log button on the strip is pressed.</summary>
        public Action LogRequested { get; set; }

        private RectTransform _canvas;
        private RectTransform _strip;
        private RectTransform _content;
        private ScrollRect _scroll;
        private bool _builtPortrait;

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

        /// <summary>
        /// Rebuilds in the other arrangement when the screen turns. The entries
        /// are dropped with it: they are a record of what has been shown, not
        /// state, and the next batch refills the rail.
        /// </summary>
        private void LateUpdate()
        {
            if (_strip == null || _builtPortrait == ScreenLayout.IsPortrait) return;

            HideCard(null);

            var old = _strip.gameObject;
            var oldCard = _card != null ? _card.gameObject : null;
            _strip = null;
            _content = null;
            _card = null;
            old.SetActive(false);
            Destroy(old);
            if (oldCard != null) Destroy(oldCard);

            Build();
        }

        /// <summary>Adds a batch's items, newest ending up first.</summary>
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

            // Newest first: the top of a standing strip, the left of a lying one.
            if (_builtPortrait) _scroll.horizontalNormalizedPosition = 0f;
            else _scroll.verticalNormalizedPosition = 1f;
        }

        // ── Scaffold ─────────────────────────────────────────────────────

        private void Build()
        {
            _builtPortrait = ScreenLayout.IsPortrait;

            _strip = UiKit.Rect("history_strip", _canvas);

            if (_builtPortrait) BuildLying();
            else BuildStanding();

            BuildCard();
        }

        private void BuildStanding()
        {
            _strip.anchorMin = new Vector2(1f, 0f);
            _strip.anchorMax = new Vector2(1f, 1f);
            _strip.pivot = new Vector2(1f, 0.5f);
            _strip.offsetMin = new Vector2(-Width, 0f);
            _strip.offsetMax = new Vector2(0f, -TurnStrip.ReservedHeight);
            UiKit.Dock(_strip, true, RectTransform.Edge.Left);

            var column = UiKit.Rect("column", _strip);
            UiKit.Stretch(column);
            UiKit.Column(column, 6f, 5).padding.left = 10; // clear of the rule

            LogButton(column, height: 28f, width: -1f);

            var scrollRect = UiKit.Rect("scroll", column);
            UiKit.Size(scrollRect, flexibleHeight: 1f);
            _scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
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
        }

        private void BuildLying()
        {
            _strip.anchorMin = new Vector2(0f, 0f);
            _strip.anchorMax = new Vector2(1f, 0f);
            _strip.pivot = new Vector2(0.5f, 0f);
            _strip.sizeDelta = new Vector2(0f, BandHeight);
            _strip.anchoredPosition = new Vector2(0f, ActionTray.ReservedHeight);
            UiKit.Dock(_strip, true, RectTransform.Edge.Top);

            var row = UiKit.Rect("row", _strip);
            UiKit.Stretch(row);
            var layout = UiKit.Row(row, 6f, 5);
            layout.padding.top = 8; // clear of the rule
            layout.childAlignment = TextAnchor.MiddleLeft;

            LogButton(row, height: 44f, width: 52f);

            var scrollRect = UiKit.Rect("scroll", row);
            UiKit.Size(scrollRect, flexibleWidth: 1f);
            _scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = true;
            _scroll.vertical = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;

            var viewport = UiKit.Rect("viewport", scrollRect);
            UiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = UiKit.Rect("content", viewport);
            _content.anchorMin = new Vector2(0f, 0f);
            _content.anchorMax = new Vector2(0f, 1f);
            _content.pivot = new Vector2(0f, 0.5f);
            _content.sizeDelta = Vector2.zero;
            var chips = UiKit.Row(_content, 4f);
            chips.childAlignment = TextAnchor.MiddleLeft;

            // A row controls its children's height and, left alone, would give
            // a chip its preferred height — which is nothing, because a chip
            // lays its own contents out with anchors. Force-expanding fills
            // the band instead.
            chips.childForceExpandHeight = true;
            _content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll.viewport = viewport;
            _scroll.content = _content;
        }

        private void LogButton(Transform parent, float height, float width)
        {
            var log = UiKit.Button(parent, $"LOG{ScreenLayout.KeyMarkup(" <size=70%>L</size>")}",
                () => LogRequested?.Invoke(), size: 13f);

            if (width >= 0f) UiKit.Fixed(log, width, height);
            else UiKit.Size(log, height: height);
        }

        private void BuildCard()
        {
            _card = UiKit.Rect("history_card", _canvas);
            _card.anchorMin = new Vector2(0.5f, 0.5f);
            _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = _builtPortrait ? new Vector2(0.5f, 0f) : new Vector2(1f, 0.5f);
            _card.sizeDelta = new Vector2(Mathf.Min(CardWidth, ScreenLayout.Reference.x - 24f), 0f);
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

            var line = UiKit.Rect("line", divider);
            UiKit.Fill(line, colour);

            var tag = UiKit.Rect("round", divider);
            UiKit.Sliced(tag, DecoSprites.ChipFill, UiTheme.Obsidian);

            if (_builtPortrait)
            {
                UiKit.Fixed(divider, 18f);

                line.anchorMin = new Vector2(0.5f, 0f);
                line.anchorMax = new Vector2(0.5f, 1f);
                line.sizeDelta = new Vector2(2f, 0f);

                tag.anchorMin = new Vector2(0f, 0.5f);
                tag.anchorMax = new Vector2(1f, 0.5f);
                tag.sizeDelta = new Vector2(0f, 22f);
            }
            else
            {
                UiKit.Size(divider, height: 18f);

                line.anchorMin = new Vector2(0f, 0.5f);
                line.anchorMax = new Vector2(1f, 0.5f);
                line.sizeDelta = new Vector2(0f, 2f);

                tag.anchorMin = new Vector2(0.5f, 0f);
                tag.anchorMax = new Vector2(0.5f, 1f);
                tag.sizeDelta = new Vector2(40f, 0f);
            }

            UiKit.Caption(tag, $"R{item.Round}", 11f, UiTheme.Readable(colour), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

            return divider;
        }

        private RectTransform Chip(HistoryItem item)
        {
            var seatColour = BoardLayout.ColourOf(item.Seat);

            var chip = UiKit.Rect($"chip_{item.Kind}", _content);
            UiKit.Sliced(chip, DecoSprites.ChipFill, UiTheme.PanelInset, blocksPointer: true);

            if (_builtPortrait) UiKit.Fixed(chip, ChipWidth);
            else UiKit.Size(chip, height: ChipHeight);

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

            // A finger cannot rest on a chip, so it taps one instead (M4).
            if (ScreenLayout.Touch)
            {
                hover.Clicked = () =>
                {
                    if (ReferenceEquals(_cardOwner, chip)) HideCard(chip);
                    else ShowCard(item, chip);
                };
            }
            else
            {
                hover.Entered = () => ShowCard(item, chip);
                hover.Exited = () => HideCard(chip);
            }

            return chip;
        }

        // ── The detail card ──────────────────────────────────────────────

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

            // lossyScale: the strip may sit on a layer under the canvas (increment J).
            float scale = _canvas.lossyScale.y;

            if (_builtPortrait)
            {
                // Above the rail, over the chip, kept on screen.
                _strip.GetWorldCorners(_corners);
                float top = _corners[1].y;

                chip.GetWorldCorners(_corners);
                float x = (_corners[0].x + _corners[3].x) * 0.5f;

                float halfWidth = _card.rect.width * _canvas.lossyScale.x * 0.5f;
                x = Mathf.Clamp(x, halfWidth + 4f, Screen.width - halfWidth - 4f);

                _card.position = new Vector3(x, top + 8f, 0f);
            }
            else
            {
                // Beside the strip, level with the chip, kept on screen.
                _strip.GetWorldCorners(_corners);
                float left = _corners[0].x;

                chip.GetWorldCorners(_corners);
                float y = (_corners[0].y + _corners[1].y) * 0.5f;

                float half = _card.rect.height * scale * 0.5f;
                y = Mathf.Clamp(y, half + 4f, Screen.height - half - 4f);

                _card.position = new Vector3(left - 8f, y, 0f);
            }
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
