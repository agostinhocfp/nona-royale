// Assets/_Project/Scripts/Unity/View/UiKit.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Shared widget construction and colours for the in-match HUD (GUI phase,
    /// increment F).
    /// </summary>
    /// <remarks>
    /// <b>One place for the look.</b> The top bar, squad rail, action tray and
    /// log all build their widgets here, so the skin pass (increment G) changes
    /// these values and methods rather than four files. The colours already
    /// lean on ART_DIRECTION §3: near-black panels, gold for static emphasis,
    /// cyan for live and selected states (§8).
    ///
    /// <b>Creation-time defaults carry the ADR-0008 rules.</b> Text and
    /// display images never catch the pointer (consequence 9). Buttons have no
    /// keyboard navigation and drop focus after a click, so Enter and Space,
    /// which are game keys, never press a button a second time.
    ///
    /// The dev panel (<see cref="ControlPanel"/>) keeps its own helpers. It is
    /// a debugging tool, and it is not skinned.
    /// </remarks>
    public static class UiKit
    {
        // ── Colours (ART_DIRECTION §3, provisional until the palette lock) ──

        public static readonly Color Panel = new Color(0.039f, 0.027f, 0.035f, 0.94f);      // obsidian
        public static readonly Color PanelRaised = new Color(0.078f, 0.063f, 0.075f, 0.96f); // charcoal velvet
        public static readonly Color Line = new Color(0.486f, 0.353f, 0.118f, 0.9f);        // aged brass
        public static readonly Color Gold = new Color(0.788f, 0.604f, 0.235f);               // gilt gold
        public static readonly Color GoldBright = new Color(0.957f, 0.851f, 0.545f);
        public static readonly Color Cyan = new Color(0.373f, 0.878f, 0.910f);               // holo cyan
        public static readonly Color Text = new Color(0.93f, 0.91f, 0.88f);
        public static readonly Color TextDim = new Color(0.62f, 0.60f, 0.58f);
        public static readonly Color TextOff = new Color(0.42f, 0.41f, 0.40f);
        public static readonly Color ButtonFill = new Color(0.12f, 0.10f, 0.11f);
        public static readonly Color ButtonOff = new Color(0.07f, 0.06f, 0.07f);
        public static readonly Color Selected = new Color(0.09f, 0.29f, 0.31f);
        public static readonly Color Danger = new Color(0.80f, 0.25f, 0.25f);
        public static readonly Color Track = new Color(1f, 1f, 1f, 0.08f);

        public const float FontSmall = 15f;
        public const float FontBody = 18f;
        public const float FontLarge = 22f;
        public const float FontTitle = 26f;

        public static string Hex(Color colour) => ColorUtility.ToHtmlStringRGB(colour);

        /// <summary>A seat colour lifted toward white, readable on a dark panel.</summary>
        public static Color Readable(Color seat) => Color.Lerp(seat, Color.white, 0.35f);

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

        /// <summary>A background with a thin brass rule along one edge, or none.</summary>
        public static void Frame(RectTransform rect, Color colour, bool blocksPointer, RectTransform.Edge? rule)
        {
            Fill(rect, colour, blocksPointer);
            if (rule == null) return;

            var line = Rect("rule", rect);
            Fill(line, Line);

            // Decoration, not content: a layout group on the framed rect must
            // not place it.
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            switch (rule.Value)
            {
                case RectTransform.Edge.Top:
                    line.anchorMin = new Vector2(0f, 1f); line.anchorMax = Vector2.one;
                    line.sizeDelta = new Vector2(0f, 2f); line.pivot = new Vector2(0.5f, 1f);
                    break;
                case RectTransform.Edge.Bottom:
                    line.anchorMin = Vector2.zero; line.anchorMax = new Vector2(1f, 0f);
                    line.sizeDelta = new Vector2(0f, 2f); line.pivot = new Vector2(0.5f, 0f);
                    break;
                case RectTransform.Edge.Left:
                    line.anchorMin = Vector2.zero; line.anchorMax = new Vector2(0f, 1f);
                    line.sizeDelta = new Vector2(2f, 0f); line.pivot = new Vector2(0f, 0.5f);
                    break;
                default:
                    line.anchorMin = new Vector2(1f, 0f); line.anchorMax = Vector2.one;
                    line.sizeDelta = new Vector2(2f, 0f); line.pivot = new Vector2(1f, 0.5f);
                    break;
            }

            line.anchoredPosition = Vector2.zero;
        }

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

        // ── Content ──────────────────────────────────────────────────────

        public static TMP_Text Label(
            Transform parent, string text, float size = FontBody, Color? colour = null,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool wrap = false, bool bold = false)
        {
            var rect = Rect("label", parent);

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.richText = true;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.color = colour ?? Text;
            label.alignment = align;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            label.text = text;
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

        /// <summary>A horizontal bar filled to <paramref name="fraction"/>.</summary>
        public static void Bar(Transform parent, float fraction, Color fill, float width, float height)
        {
            var back = Rect("bar", parent);
            Fill(back, Track);
            Size(back, width, height);

            var front = Rect("fill", back);
            Fill(front, fill);
            front.anchorMin = Vector2.zero;
            front.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            front.offsetMin = Vector2.zero;
            front.offsetMax = Vector2.zero;
        }

        /// <summary>A status word on its own colour, as under the pieces.</summary>
        public static void Tag(Transform parent, string word, Color colour, float size = 12f)
        {
            var rect = Rect($"tag_{word}", parent);
            Fill(rect, colour);

            var pad = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            pad.padding = new RectOffset(5, 5, 1, 1);
            pad.childControlWidth = true;
            pad.childControlHeight = true;
            pad.childForceExpandWidth = false;
            pad.childForceExpandHeight = true;

            float luminance = 0.2126f * colour.r + 0.7152f * colour.g + 0.0722f * colour.b;
            var text = Label(rect, word, size, luminance < 0.5f ? Color.white : new Color(0.06f, 0.06f, 0.08f),
                TextAlignmentOptions.Center, bold: true);
            text.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>
        /// A thin border inside <paramref name="rect"/>, drawn as four strips
        /// that layout ignores.
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
        /// A gold border that breathes, to mark the next thing to press.
        /// </summary>
        public static void Pulse(Component target)
        {
            var rect = (RectTransform)target.transform;
            var pulse = rect.gameObject.AddComponent<UiPulse>();
            pulse.Targets = Border(rect, GoldBright, 3f);
        }

        private static Image Strip(RectTransform parent, Color colour, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var strip = Rect("border", parent);
            Fill(strip, colour);
            strip.anchorMin = anchorMin;
            strip.anchorMax = anchorMax;
            strip.offsetMin = offsetMin;
            strip.offsetMax = offsetMax;
            strip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return (Image)strip.GetComponent<Image>();
        }

        /// <summary>
        /// A button. The click runs <paramref name="onClick"/> and then
        /// <paramref name="afterClick"/>, usually the owner's MarkDirty.
        /// </summary>
        public static UnityEngine.UI.Button Button(
            Transform parent, string text, Action onClick, Action afterClick = null,
            bool interactable = true, bool selected = false, float size = FontBody,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Color? tint = null)
        {
            var rect = Rect("button", parent);

            // White, so the button's tint is the colour shown rather than
            // being multiplied by a second one.
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;

            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.interactable = interactable;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var baseTint = tint ?? (selected ? Selected : ButtonFill);
            var colours = ColorBlock.defaultColorBlock;
            colours.normalColor = baseTint;
            colours.highlightedColor = Color.Lerp(baseTint, Color.white, 0.12f);
            colours.pressedColor = Color.Lerp(baseTint, Color.black, 0.25f);
            colours.selectedColor = baseTint;
            colours.disabledColor = ButtonOff;
            colours.colorMultiplier = 1f;
            button.colors = colours;

            if (selected) Border(rect, Cyan, 2f);

            button.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

                onClick?.Invoke();
                afterClick?.Invoke();
            });

            // The caption ignores layout, so a button can also hold laid-out
            // content of its own (the squad rail's rows do).
            var label = Caption(rect, text, size, interactable ? Text : TextOff, align, 8f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            return button;
        }
    }
}
