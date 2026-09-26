// Assets/_Project/Scripts/Unity/View/UiKit.cs
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Shared widget construction for the in-match HUD (GUI phase, increments
    /// F and G).
    /// </summary>
    /// <remarks>
    /// <b>One place for the shapes; <see cref="UiTheme"/> for the colours.</b>
    /// The top bar, squad rail, action tray, history strip and overlays all
    /// build their widgets here. Increment G moved every colour and type size
    /// to <see cref="UiTheme"/> and gave the widgets their Deco frames from
    /// <see cref="DecoSprites"/>: chamfered corners, gilt rules, corner fans,
    /// diamond pips (ART_DIRECTION §8). Increment G3 quietened the trim: every
    /// resting edge is one anti-aliased hairline at half strength, and live
    /// states keep their cyan double edge, which now reads more clearly.
    ///
    /// <b>Two kinds of panel.</b> A docked panel (<see cref="Dock"/>) is flush
    /// with a screen edge, so it is a plain dark field with a hairline along
    /// its inner edge and no ornament. A floating panel (<see cref="Panel"/>)
    /// is a chamfered card with a hairline frame and small, dim corner fans.
    ///
    /// <b>Creation-time defaults carry the ADR-0008 rules.</b> Text, frames and
    /// ornaments never catch the pointer (consequence 9). Buttons have no
    /// keyboard navigation and drop focus after a click, so Enter and Space,
    /// which are game keys, never press a button a second time. Ornaments
    /// ignore layout, so a layout group on the framed rect never places them.
    ///
    /// The dev panel (<see cref="ControlPanel"/>) keeps its own helpers. It is
    /// a debugging tool, and it is not skinned.
    /// </remarks>
    public static class UiKit
    {
        /// <summary>
        /// Raised after any kit button is pressed (AUDIO.md increment AU1), so
        /// the audio director can click without every caller passing it along.
        /// A plain event, not a service: the composition root subscribes and
        /// unsubscribes, and nothing else reads it.
        /// </summary>
        public static event Action ButtonPressed;

        // ── Layout primitives ────────────────────────────────────────────

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>A plain tinted quad. Catches the pointer only when asked.</summary>
        public static Image Fill(RectTransform rect, Color colour, bool blocksPointer = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = blocksPointer;
            return image;
        }

        /// <summary>A nine-sliced sprite as the rect's own background.</summary>
        public static Image Sliced(RectTransform rect, Sprite sprite, Color colour, bool blocksPointer = false)
        {
            var image = Fill(rect, colour, blocksPointer);
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            return image;
        }

        /// <summary>
        /// A sliced sprite stretched over <paramref name="rect"/> as a child:
        /// an edge or a glow on top of the background. Ignores layout and the
        /// pointer.
        /// </summary>
        public static Image Overlay(RectTransform rect, Sprite sprite, Color colour, string name = "edge")
        {
            var child = Rect(name, rect);
            Stretch(child);
            Decoration(child);
            return Sliced(child, sprite, colour);
        }

        /// <summary>Marks a child as decoration: a layout group on its parent leaves it alone.</summary>
        private static void Decoration(RectTransform rect) =>
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        // ── Panels ───────────────────────────────────────────────────────

        /// <summary>
        /// A panel docked to a screen edge: a dark field with one gilt
        /// hairline along <paramref name="rule"/> (G3; it was a double rule
        /// with a diamond at its middle).
        /// </summary>
        /// <remarks>
        /// Flat, deliberately. G5 gave the field a translucency ramp so the
        /// room would show through its board-facing edge, and it was removed:
        /// FrameCamera reserves the screen edges and fits the board inside
        /// them, so a dock has void behind it, not board. There was nothing
        /// to see through to.
        /// </remarks>
        public static void Dock(RectTransform rect, bool blocksPointer, RectTransform.Edge rule)
        {
            Fill(rect, UiTheme.Panel, blocksPointer);
            EdgeRule(rect, rule, UiTheme.Line);
        }

        /// <summary>
        /// A floating chamfered panel: raised fill, a gilt hairline frame,
        /// and a small, dim fan in each corner. Elevated (U4): two offset
        /// shades under it and a whisper of sheen across the top.
        /// </summary>
        public static Image Panel(RectTransform rect, bool blocksPointer, bool fans = true, Color? fill = null)
        {
            var body = Sliced(rect, DecoSprites.PanelFill, fill ?? UiTheme.PanelRaised, blocksPointer);
            Elevate(body);
            Sheen(rect);
            Overlay(rect, DecoSprites.PanelEdge, UiTheme.Line);
            if (fans) CornerFans(rect, UiTheme.WithAlpha(UiTheme.Gold, UiTheme.FanAlpha));
            return body;
        }

        /// <summary>
        /// A panel's elevation (UI_MOTION.md increment U4): two hard offset
        /// shades from the graphic's own silhouette, the far one larger and
        /// fainter, which together read as one soft shadow.
        /// </summary>
        public static void Elevate(Graphic graphic)
        {
            var near = graphic.gameObject.AddComponent<Shadow>();
            near.effectColor = UiTheme.PanelShadowNear;
            near.effectDistance = UiTheme.PanelShadowNearOffset;

            var far = graphic.gameObject.AddComponent<Shadow>();
            far.effectColor = UiTheme.PanelShadowFar;
            far.effectDistance = UiTheme.PanelShadowFarOffset;
        }

        /// <summary>A whisper of light across a panel's top, inset so the edge hairline stays crisp (U4).</summary>
        private static void Sheen(RectTransform rect)
        {
            var child = Rect("sheen", rect);
            Stretch(child, 3f);
            Decoration(child);

            var image = Fill(child, UiTheme.PanelSheen);
            image.sprite = DecoSprites.PanelSheen;
        }

        /// <summary>A quarter sunburst in each of the rect's corners, opening inward.</summary>
        public static void CornerFans(RectTransform rect, Color colour)
        {
            Fan(rect, new Vector2(0f, 0f), 0f, colour);
            Fan(rect, new Vector2(1f, 0f), 90f, colour);
            Fan(rect, new Vector2(1f, 1f), 180f, colour);
            Fan(rect, new Vector2(0f, 1f), 270f, colour);
        }

        private static void Fan(RectTransform parent, Vector2 corner, float rotation, Color colour)
        {
            var fan = Rect("fan", parent);
            Decoration(fan);
            var image = Fill(fan, colour);
            image.sprite = DecoSprites.CornerFan;

            fan.anchorMin = corner;
            fan.anchorMax = corner;
            fan.pivot = Vector2.zero;
            fan.sizeDelta = new Vector2(DecoSprites.FanSize, DecoSprites.FanSize);

            // Inset toward the panel's centre, whichever corner this is.
            var inward = new Vector2(corner.x < 0.5f ? 1f : -1f, corner.y < 0.5f ? 1f : -1f);
            fan.anchoredPosition = inward * DecoSprites.FanInset;
            fan.localEulerAngles = new Vector3(0f, 0f, rotation);
        }

        /// <summary>
        /// An anti-aliased hairline along one inside edge of
        /// <paramref name="rect"/>, flush with it.
        /// </summary>
        /// <remarks>
        /// Drawn from <see cref="DecoSprites.RuleAlong"/> rather than a solid
        /// quad: a quad's edge snaps to whole pixels, so a thin one comes and
        /// goes as the canvas scales. The sprite's box is centred on the line,
        /// half a line in from the edge.
        /// </remarks>
        private static void EdgeRule(RectTransform rect, RectTransform.Edge edge, Color colour)
        {
            var line = Rect("rule", rect);
            var image = Fill(line, colour);
            Decoration(line);

            bool horizontal = edge == RectTransform.Edge.Top || edge == RectTransform.Edge.Bottom;
            image.sprite = horizontal ? DecoSprites.RuleAlong : DecoSprites.RuleUp;

            float centre = DecoSprites.HairlineWidth * 0.5f;
            line.pivot = new Vector2(0.5f, 0.5f);
            line.sizeDelta = horizontal ? new Vector2(0f, DecoSprites.RuleBox) : new Vector2(DecoSprites.RuleBox, 0f);

            switch (edge)
            {
                case RectTransform.Edge.Top:
                    line.anchorMin = new Vector2(0f, 1f); line.anchorMax = Vector2.one;
                    line.anchoredPosition = new Vector2(0f, -centre);
                    break;
                case RectTransform.Edge.Bottom:
                    line.anchorMin = Vector2.zero; line.anchorMax = new Vector2(1f, 0f);
                    line.anchoredPosition = new Vector2(0f, centre);
                    break;
                case RectTransform.Edge.Left:
                    line.anchorMin = Vector2.zero; line.anchorMax = new Vector2(0f, 1f);
                    line.anchoredPosition = new Vector2(centre, 0f);
                    break;
                default:
                    line.anchorMin = new Vector2(1f, 0f); line.anchorMax = Vector2.one;
                    line.anchoredPosition = new Vector2(-centre, 0f);
                    break;
            }
        }

        // ── Layout groups ────────────────────────────────────────────────

        public static VerticalLayoutGroup Column(RectTransform rect, float spacing = 4f, int padding = 0)
        {
            var column = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = spacing;
            column.padding = new RectOffset(padding, padding, padding, padding);
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            return column;
        }

        public static HorizontalLayoutGroup Row(RectTransform rect, float spacing = 6f, int padding = 0)
        {
            var row = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.padding = new RectOffset(padding, padding, padding, padding);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            return row;
        }

        /// <summary>Fixes, or makes flexible, a layout child's size. A negative value leaves that axis alone.</summary>
        public static LayoutElement Size(Component child, float width = -1f, float height = -1f, float flexibleWidth = -1f, float flexibleHeight = -1f)
        {
            var element = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();

            if (width >= 0f) { element.minWidth = width; element.preferredWidth = width; }
            if (height >= 0f) { element.minHeight = height; element.preferredHeight = height; }
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0f) element.flexibleHeight = flexibleHeight;

            return element;
        }

        /// <summary>
        /// Fixes a layout child's width and stops it growing.
        /// </summary>
        /// <remarks>
        /// <b>A column or row that force-expands its children reports itself
        /// as flexible</b> (Unity counts every force-expanded child as
        /// flexible 1). A section with a fixed width but its own layout group
        /// therefore takes a share of any spare room unless its flexible
        /// width is pinned to 0 here. That is what made the tray sections
        /// wider than their stated widths.
        /// </remarks>
        public static LayoutElement Fixed(Component child, float width, float height = -1f) =>
            Size(child, width, height, flexibleWidth: 0f);

        public static RectTransform Space(Transform parent, float width = 0f, float height = 0f, bool flexible = false)
        {
            var rect = Rect("space", parent);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.minHeight = height;
            element.preferredHeight = height;
            if (flexible) element.flexibleWidth = 1f;
            return rect;
        }

        /// <summary>
        /// Makes <paramref name="viewport"/> a vertical scroll area and returns
        /// its content: a column that grows with what is put in it.
        /// </summary>
        /// <remarks>
        /// The viewport is sized by whoever owns it; this only masks it, adds
        /// the scroll, and hangs the content from its top edge. Buttons and
        /// links inside keep working: a press that becomes a drag scrolls, and
        /// the event system withdraws its click (<c>HudRoot</c> sets the drag
        /// threshold from the screen's density).
        /// </remarks>
        public static RectTransform ScrollColumn(RectTransform viewport, float spacing = 6f, int padding = 0)
        {
            viewport.gameObject.AddComponent<RectMask2D>();

            // A scroll needs something under the pointer to catch a drag that
            // starts between two lines; a clear fill is that something.
            if (viewport.GetComponent<Graphic>() == null) Fill(viewport, Color.clear, blocksPointer: true);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var content = Rect("scroll_content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            Column(content, spacing, padding).childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            Scrollbar(scroll, viewport);
            return content;
        }

        /// <summary>
        /// A slim gold bar at a scrolling column's right edge, shown only when
        /// there is more than fits (G7c). The glossary ran past its panel's
        /// bottom edge with nothing to say that more was below.
        /// </summary>
        private static void Scrollbar(ScrollRect scroll, RectTransform viewport)
        {
            var bar = Rect("scrollbar", viewport);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(4f, -8f);
            bar.anchoredPosition = Vector2.zero;
            Fill(bar, UiTheme.WithAlpha(UiTheme.Line, 0.25f));

            var area = Rect("sliding_area", bar);
            Stretch(area);

            var handle = Rect("handle", area);
            Stretch(handle);
            var knob = Fill(handle, UiTheme.WithAlpha(UiTheme.Gold, 0.75f));

            var scrollbar = bar.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = knob;
            scrollbar.transition = Selectable.Transition.None;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        // ── Content ──────────────────────────────────────────────────────

        /// <summary>How far, as a fraction of the type size, a single-line label may overhang its box above and below (G7a).</summary>
        private const float LineSlack = 0.3f;

        /// <summary>A speed multiplier as the HUD prints it: "×1.5".</summary>
        /// <remarks>
        /// Always a point. A bare <c>{x:0.0}</c> formats with the player's
        /// culture, so a Portuguese machine printed "×1,5" in an English UI
        /// (G6a). Any fractional number shown to the player goes through the
        /// invariant culture, the way the core's rules text already does.
        /// </remarks>
        public static string Multiplier(double value) =>
            "×" + value.ToString("0.0", CultureInfo.InvariantCulture);


        public static TMP_Text Label(
            Transform parent, string text, float size = UiTheme.FontBody, Color? colour = null,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool wrap = false, bool bold = false)
        {
            var rect = Rect("label", parent);

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.richText = true;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.color = colour ?? UiTheme.Text;
            label.alignment = align;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;

            // A single line gets vertical slack (G7a). In Ellipsis mode a line
            // taller than its box draws nothing at all, and a box sized to the
            // type (18-unit body text in an 18-unit row, 34-point Cinzel in 44
            // units) is just short of the face's full line height. That was
            // four bugs: the blank CPU chip and LOG button, the missing health
            // number, and the pause menu's missing PAUSED. Negative top and
            // bottom margins let the line overhang the box; the width still
            // truncates with an ellipsis, and a centred line stays centred.
            if (!wrap)
            {
                float slack = size * LineSlack;
                label.margin = new Vector4(0f, -slack, 0f, -slack);
            }

            label.text = text;

            // The data face carries every label; Heading overrides it with the
            // display face afterwards (G5).
            UiFonts.ApplyData(label);
            return label;
        }

        /// <summary>
        /// A section heading: small spaced gold capitals, the Deco signage
        /// voice. Static chrome, so warm (ART_DIRECTION §8).
        /// </summary>
        public static TMP_Text Heading(Transform parent, string text, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var label = Label(parent, text.ToUpperInvariant(), 13f, UiTheme.Heading, align, bold: true);
            label.characterSpacing = UiTheme.HeadingSpacing;
            UiFonts.ApplyDisplay(label);
            return label;
        }

        /// <summary>A label filling its parent rect, for text inside a fixed box.</summary>
        public static TMP_Text Caption(RectTransform parent, string text, float size, Color colour, TextAlignmentOptions align, float inset = 0f)
        {
            var label = Label(parent, text, size, colour, align);
            Stretch((RectTransform)label.transform, inset);
            return label;
        }

        /// <summary>A sprite drawn inside a fixed box, aspect kept.</summary>
        public static Image Icon(Transform parent, Sprite sprite, Color colour, float size)
        {
            var rect = Rect("icon", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.preserveAspect = true;
            image.raycastTarget = false;
            Size(image, size, size);
            return image;
        }

        /// <summary>A tall diamond of a fixed size, for pips and bullets.</summary>
        public static Image Diamond(Transform parent, Color colour, float width, float height, bool outline = false)
        {
            var rect = Rect("diamond", parent);
            var image = Fill(rect, colour);
            image.sprite = outline ? DecoSprites.DiamondOutline : DecoSprites.Diamond;
            Size(image, width, height);
            return image;
        }

        /// <summary>A horizontal bar filled to <paramref name="fraction"/>. Returns the fill, for <see cref="TweenBar"/>.</summary>
        public static RectTransform Bar(Transform parent, float fraction, Color fill, float width, float height)
        {
            var back = Rect("bar", parent);
            Fill(back, UiTheme.Track);
            Size(back, width, height);

            var front = Rect("fill", back);
            Fill(front, fill);
            front.anchorMin = Vector2.zero;
            front.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            front.offsetMin = Vector2.zero;
            front.offsetMax = Vector2.zero;
            return front;
        }

        /// <summary>
        /// Glides a bar's fill from <paramref name="from"/> to
        /// <paramref name="to"/> (UI_MOTION.md increment U2), its colour
        /// following the fraction toward <paramref name="full"/>, the colour
        /// the caller wants at full health.
        /// </summary>
        public static void TweenBar(RectTransform fill, float from, float to, float seconds, Color full)
        {
            if (fill == null) return;

            var image = fill.GetComponent<Image>();
            UiTween.Value(fill, from, to, seconds, v =>
            {
                float fraction = Mathf.Clamp01(v);
                fill.anchorMax = new Vector2(fraction, 1f);
                if (image != null) image.color = Color.Lerp(UiTheme.Danger, full, fraction);
            });
        }

        /// <summary>
        /// A divider between tray sections: a gilt hairline with a small
        /// diamond at its middle, both at <see cref="UiTheme.Line"/> strength.
        /// Vertical in a row, horizontal in a column.
        /// </summary>
        public static void Divider(Transform parent, bool vertical)
        {
            var box = Rect("divider", parent);
            if (vertical) Fixed(box, 12f);
            else Size(box, height: 12f);

            var line = Rect("line", box);
            var rule = Fill(line, UiTheme.Line);
            rule.sprite = vertical ? DecoSprites.RuleUp : DecoSprites.RuleAlong;
            line.anchorMin = vertical ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
            line.anchorMax = vertical ? new Vector2(0.5f, 1f) : new Vector2(1f, 0.5f);
            line.sizeDelta = vertical
                ? new Vector2(DecoSprites.RuleBox, -16f)
                : new Vector2(-16f, DecoSprites.RuleBox);

            var gem = Rect("gem", box);
            var image = Fill(gem, UiTheme.Line);
            image.sprite = DecoSprites.Diamond;
            gem.anchorMin = gem.anchorMax = new Vector2(0.5f, 0.5f);
            gem.sizeDelta = new Vector2(6f, 9f);
            if (!vertical) gem.localEulerAngles = new Vector3(0f, 0f, 90f);
        }

        /// <summary>A status word on its own colour, as under the pieces.</summary>
        public static void Tag(Transform parent, string word, Color colour, float size = 12f)
        {
            var rect = Rect($"tag_{word}", parent);
            Sliced(rect, DecoSprites.ChipFill, colour);

            var pad = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            pad.padding = new RectOffset(6, 6, 1, 1);
            pad.childControlWidth = true;
            pad.childControlHeight = true;
            pad.childForceExpandWidth = false;
            pad.childForceExpandHeight = true;

            var text = Label(rect, word, size, UiTheme.TextOn(colour), TextAlignmentOptions.Center, bold: true);
            text.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>
        /// A thin rectangular border inside <paramref name="rect"/>, drawn as
        /// four strips that layout ignores. For square-cornered boxes; framed
        /// widgets use <see cref="Overlay"/> with a Deco edge instead.
        /// </summary>
        public static Image[] Border(RectTransform rect, Color colour, float thickness)
        {
            return new[]
            {
                Strip(rect, colour, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -thickness), Vector2.zero),
                Strip(rect, colour, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness)),
                Strip(rect, colour, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f)),
                Strip(rect, colour, new Vector2(1f, 0f), Vector2.one, new Vector2(-thickness, 0f), Vector2.zero),
            };
        }

        /// <summary>
        /// A double edge that breathes, to mark the next thing to press. Gold
        /// by default; a live control passes cyan.
        /// </summary>
        public static void Pulse(Component target, Color? colour = null)
        {
            var rect = (RectTransform)target.transform;
            var edge = Overlay(rect, DecoSprites.ButtonEdgeDouble, colour ?? UiTheme.GoldBright, "pulse");

            var pulse = rect.gameObject.AddComponent<UiPulse>();
            pulse.Targets = new Graphic[] { edge };
        }

        private static Image Strip(RectTransform parent, Color colour, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var strip = Rect("border", parent);
            var image = Fill(strip, colour);
            strip.anchorMin = anchorMin;
            strip.anchorMax = anchorMax;
            strip.offsetMin = offsetMin;
            strip.offsetMax = offsetMax;
            Decoration(strip);
            return image;
        }

        /// <summary>
        /// A setting as one wide button: its name, its key in gold, and an ON
        /// or OFF chip (GUI increments H and J).
        /// </summary>
        /// <remarks>
        /// <b>The chip carries the state, not the row</b> (G7c). The row used
        /// to take the selected fill while on, so a page of settings read as a
        /// page of focused buttons, and cyan fill means "selected" everywhere
        /// else in the game. Now only the chip turns cyan.
        /// </remarks>
        public static UnityEngine.UI.Button ToggleRow(Transform parent, string label, string key, bool on, Action press)
        {
            var button = Button(parent, "", press);
            var rect = (RectTransform)button.transform;

            var row = Row(rect, 10f);
            row.padding = new RectOffset(18, 14, 0, 0);
            row.childAlignment = TextAnchor.MiddleLeft;

            var name = Label(rect, label, UiTheme.FontBody);
            Size(name, flexibleWidth: 1f);

            if (!string.IsNullOrEmpty(key))
                Label(rect, key, 13f, UiTheme.Gold, TextAlignmentOptions.MidlineRight);

            var chip = Rect("state", rect);
            Sliced(chip, DecoSprites.ChipFill, on ? UiTheme.Cyan : UiTheme.PanelInset);
            Fixed(chip, 54f, 24f);
            Caption(chip, on ? "ON" : "OFF", 13f, on ? UiTheme.DieInk : UiTheme.TextDim,
                TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

            return button;
        }

        /// <summary>
        /// A volume setting (AUDIO.md decision 5): its name, a slider, and the
        /// value as a percentage. Dragging reports every change through
        /// <paramref name="changed"/> and updates the percentage itself; it
        /// never asks the page to rebuild, which would end the drag.
        /// </summary>
        public static Slider SliderRow(Transform parent, string label, float value, Action<float> changed,
            bool dimmed = false)
        {
            var rect = Rect("slider_row", parent);
            Sliced(rect, DecoSprites.ButtonFill, UiTheme.ButtonFill);
            Overlay(rect, DecoSprites.ButtonEdge, UiTheme.WithAlpha(UiTheme.Line, dimmed ? 0.35f : 1f));

            var row = Row(rect, 12f);
            row.padding = new RectOffset(18, 14, 0, 0);
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            var name = Label(rect, label, UiTheme.FontBody, dimmed ? UiTheme.TextDim : UiTheme.Text);
            Size(name, flexibleWidth: 1f, height: 28f);

            // The track: the whole box catches the pointer, so the thin line is easy to grab.
            var track = Rect("track", rect);
            Fixed(track, 190f, 28f);
            var hit = track.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var line = Rect("line", track);
            line.anchorMin = new Vector2(0f, 0.5f);
            line.anchorMax = new Vector2(1f, 0.5f);
            line.sizeDelta = new Vector2(0f, 6f);
            Sliced(line, DecoSprites.ChipFill, UiTheme.PanelInset);

            var fillArea = Rect("fill_area", track);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(0f, 6f);

            var fill = Rect("fill", fillArea);
            fill.sizeDelta = Vector2.zero;
            Sliced(fill, DecoSprites.ChipFill, dimmed ? UiTheme.TextOff : UiTheme.Cyan);

            var handleArea = Rect("handle_area", track);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(7f, 0f);
            handleArea.offsetMax = new Vector2(-7f, 0f);

            var handle = Rect("handle", handleArea);
            handle.sizeDelta = new Vector2(14f, 0f);
            var knob = Fill(handle, dimmed ? UiTheme.TextDim : UiTheme.GoldBright);
            knob.sprite = DecoSprites.Diamond;
            knob.preserveAspect = true;

            var percent = Label(rect, "", 15f, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);
            Fixed(percent, 52f, 28f);

            var slider = track.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));

            percent.text = $"{Mathf.RoundToInt(slider.value * 100f)}%";

            slider.onValueChanged.AddListener(v =>
            {
                percent.text = $"{Mathf.RoundToInt(v * 100f)}%";
                changed?.Invoke(v);
            });

            UiSliderFeel.Attach(track.gameObject, handle);

            return slider;
        }

        /// <summary>
        /// A setting with more than two values, as one wide button that cycles
        /// them: its name, a hint, and a chip naming the current value (BOT3).
        /// </summary>
        /// <remarks>
        /// <b>A value is not a state</b> (G7c), so neither the row nor the chip
        /// is lit: FULLSCREEN or TOP-DOWN is a choice, not "on". A key hint
        /// ("hold Space") is gold like every key. A row whose setting does not
        /// apply right now (the windowed size in fullscreen, the frame cap
        /// under VSync) is dimmed as a whole, with a dim hint saying when it
        /// does apply. It can still be cycled, ready for when it does.
        /// </remarks>
        /// <param name="active">Whether the setting applies now. False dims the row.</param>
        /// <param name="keyHint">Whether the hint names a key (gold) rather than a condition (dim).</param>
        public static UnityEngine.UI.Button ChoiceRow(Transform parent, string label, string hint, string value,
            Action press, bool active = true, bool keyHint = false)
        {
            var button = Button(parent, "", press);
            var rect = (RectTransform)button.transform;

            var row = Row(rect, 10f);
            row.padding = new RectOffset(18, 14, 0, 0);
            row.childAlignment = TextAnchor.MiddleLeft;

            var name = Label(rect, label, UiTheme.FontBody, active ? UiTheme.Text : UiTheme.TextDim);
            Size(name, flexibleWidth: 1f);

            if (!string.IsNullOrEmpty(hint))
                Label(rect, hint, 13f, keyHint ? UiTheme.Gold : UiTheme.TextDim, TextAlignmentOptions.MidlineRight);

            var chip = Rect("value", rect);
            Sliced(chip, DecoSprites.ChipFill, UiTheme.PanelInset);
            Fixed(chip, 96f, 24f);
            Caption(chip, value, 13f, active ? UiTheme.Text : UiTheme.TextDim,
                TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;

            return button;
        }

        /// <summary>
        /// Gives a freshly built <see cref="Selectable"/> its colours without
        /// the fade from white that assigning them would otherwise start.
        /// </summary>
        /// <remarks>
        /// <b>This was the white flicker.</b> A Selectable added in code takes
        /// the default <see cref="ColorBlock"/> on enable, whose normal colour
        /// is white, and paints its target graphic that colour at once. Setting
        /// <see cref="Selectable.colors"/> afterwards is a property change, and
        /// in play mode a property change transitions <i>with</i> the fade — so
        /// every button spent its first ~80 ms fading from white to its real
        /// tint. The panels rebuild wholesale on a dirty flag, which means a
        /// single click redrew every button in the tray, the rail or the draft
        /// at once, all of them flashing white together. On the noir palette
        /// that reads as the screen blinking.
        ///
        /// Assigning once with no fade snaps the graphic to whatever state the
        /// control is actually in (disabled included); assigning again with the
        /// real duration finds the colour already there and starts nothing, so
        /// hover and press keep their fade.
        /// </remarks>
        public static void SetColoursWithoutFade(Selectable selectable, ColorBlock colours)
        {
            float fade = colours.fadeDuration;

            colours.fadeDuration = 0f;
            selectable.colors = colours;

            colours.fadeDuration = fade;
            selectable.colors = colours;
        }

        /// <summary>
        /// A chamfered button with a brass edge. The click runs
        /// <paramref name="onClick"/> and then <paramref name="afterClick"/>,
        /// usually the owner's MarkDirty.
        /// </summary>
        /// <remarks>
        /// A selected button is a live state, so it fills with
        /// <see cref="UiTheme.CyanDeep"/> and takes a cyan double edge
        /// (ART_DIRECTION §8). <paramref name="edge"/> overrides the resting
        /// edge colour.
        /// </remarks>
        public static UnityEngine.UI.Button Button(
            Transform parent, string text, Action onClick, Action afterClick = null,
            bool interactable = true, bool selected = false, float size = UiTheme.FontBody,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Color? tint = null, Color? edge = null)
        {
            var rect = Rect("button", parent);

            // White, so the button's tint is the colour shown rather than
            // being multiplied by a second one.
            var image = Sliced(rect, DecoSprites.ButtonFill, Color.white, blocksPointer: true);

            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.interactable = interactable;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var baseTint = tint ?? (selected ? UiTheme.CyanDeep : UiTheme.ButtonFill);
            var colours = ColorBlock.defaultColorBlock;
            colours.normalColor = baseTint;
            colours.highlightedColor = Color.Lerp(baseTint, Color.white, 0.10f);
            colours.pressedColor = Color.Lerp(baseTint, Color.black, 0.25f);
            colours.selectedColor = baseTint;
            colours.disabledColor = UiTheme.ButtonOff;
            colours.colorMultiplier = 1f;
            colours.fadeDuration = 0.08f;
            SetColoursWithoutFade(button, colours);

            if (selected)
            {
                Overlay(rect, DecoSprites.ButtonEdgeDouble, UiTheme.Cyan);
            }
            else
            {
                var resting = edge ?? UiTheme.Line;
                Overlay(rect, DecoSprites.ButtonEdge, interactable ? resting : UiTheme.WithAlpha(resting, 0.35f));
            }

            button.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

                ButtonPressed?.Invoke();
                onClick?.Invoke();
                afterClick?.Invoke();
            });

            UiButtonFeel.Attach(button);

            // The caption ignores layout, so a button can also hold laid-out
            // content of its own (the squad rail's rows do).
            // Inset left and right only. An 8-unit inset on all four sides left
            // a 26-unit chip a 10-unit text box, and an Ellipsis label whose
            // first line does not fit its height draws nothing at all: that was
            // the blank CPU style chip on setup and the blank LOG button (G6a).
            var label = Caption(rect, text, size, interactable ? UiTheme.Text : UiTheme.TextOff, align);
            var labelRect = (RectTransform)label.transform;
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Decoration((RectTransform)label.transform);

            return button;
        }
    }
}
