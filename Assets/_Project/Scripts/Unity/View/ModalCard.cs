// Assets/_Project/Scripts/Unity/View/ModalCard.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A full-screen scrim with one framed card on it, rebuilt from scratch on
    /// every change: the shape shared by the setup and end screens (GUI
    /// increment I).
    /// </summary>
    /// <remarks>
    /// <b>Same pattern as <see cref="PauseMenu"/></b>, which predates this
    /// class and keeps its own copy until a later tidy-up. The scrim catches
    /// the pointer, so nothing under the card can be clicked; the card takes
    /// the top of the canvas back if another overlay opens above it.
    ///
    /// A subclass fills the card in <see cref="Compose"/> using the helpers
    /// below, and calls <see cref="Rebuild"/> whenever what it shows changes.
    ///
    /// <b>Transitions</b> (UI_MOTION.md increment U1): the scrim fades in,
    /// the card rises a little, and its rows cascade; closing fades out.
    /// A page swap inside the card gets a quick fade and settle from
    /// <see cref="PlayPageTransition"/>.
    /// </remarks>
    public abstract class ModalCard : MonoBehaviour
    {
        protected const float ButtonHeight = 54f;

        /// <summary>What the card keeps clear of the screen's edges (M5).</summary>
        private const float Margin = 16f;

        /// <summary>The card's own side padding. Narrower upright, where 36 a side is a seventh of the screen.</summary>
        private static int Padding => (int)ScreenLayout.Pick(36f, 20f);

        private RectTransform _root;
        private RectTransform _card;
        private RectTransform _viewport;
        private RectTransform _body;
        private ScrollRect _scroll;
        private CanvasGroup _fader;
        private CanvasGroup _cardFader;
        private bool _closing;
        private int _fittedLayout = -1;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        /// <summary>Open and not already on its way out.</summary>
        /// <remarks>
        /// <see cref="Close"/> fades over a beat and only deactivates the scrim
        /// when the fade lands, so <see cref="IsOpen"/> stays true through it -
        /// which is what keeps the keys routed to the card that is still on
        /// screen. Anything that should react to the decision rather than to the
        /// fade reads this instead: the title's flat camera tilts back as the
        /// card starts to go, not a tenth of a second after.
        /// </remarks>
        public bool IsShowing => IsOpen && !_closing;

        protected bool IsBuilt => _root != null;

        /// <summary>The full-screen scrim the card sits on.</summary>
        protected RectTransform Root => _root;

        /// <summary>The scrolling column the slots are built in.</summary>
        protected RectTransform Body => _body;

        /// <summary>Card width in canvas units.</summary>
        protected abstract float CardWidth { get; }

        /// <summary>How dark the scrim is. A screen shown with no match behind it can be nearly opaque.</summary>
        protected virtual float ScrimAlpha => 0.8f;

        /// <summary>Whether the card gets the Deco panel frame. The title screen composes on the open scrim.</summary>
        protected virtual bool Framed => true;

        /// <summary>Builds the scaffold once. Safe to call on every new match.</summary>
        protected void BuildOnce(RectTransform canvasRect, string name)
        {
            if (_root != null) return;

            _root = UiKit.Rect(name, canvasRect);
            UiKit.Stretch(_root);
            UiKit.Fill(_root, UiTheme.WithAlpha(UiTheme.Obsidian, ScrimAlpha), blocksPointer: true);

            _card = UiKit.Rect("card", _root);
            _card.anchorMin = new Vector2(0.5f, 0.5f);
            _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(FittedWidth, 0f);
            if (Framed) UiKit.Panel(_card, blocksPointer: true);

            // The card is a window onto its own content (MOBILE.md, M5). It
            // used to be a column that grew to whatever it held, which is fine
            // on a 1080-unit screen and wrong on a phone, where the settings
            // page is taller than the screen and the rows past the fold could
            // not be reached at all. Now the column lives inside a viewport and
            // the card takes the smaller of what it wants and what there is.
            _viewport = UiKit.Rect("viewport", _card);
            UiKit.Stretch(_viewport);
            _viewport.gameObject.AddComponent<RectMask2D>();

            _body = UiKit.Rect("body", _viewport);
            _body.anchorMin = new Vector2(0f, 1f);
            _body.anchorMax = new Vector2(1f, 1f);
            _body.pivot = new Vector2(0.5f, 1f);
            _body.sizeDelta = Vector2.zero;

            var column = UiKit.Column(_body, 10f, Padding);
            column.padding.top = 30;
            column.padding.bottom = 30;
            _body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = _card.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;
            _scroll.viewport = _viewport;
            _scroll.content = _body;

            _fader = _root.gameObject.AddComponent<CanvasGroup>();
            _cardFader = _card.gameObject.AddComponent<CanvasGroup>();

            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// The card's width, never wider than the screen it has to sit on
        /// (M5). The scaler expands rather than matching an axis, so the canvas
        /// is at least the reference wide and this is a floor, not a guess.
        /// </summary>
        private float FittedWidth =>
            Mathf.Min(CardWidth, ScreenLayout.Reference.x - 2f * Margin);

        /// <summary>
        /// Sizes the card to its content, up to what the screen allows, and
        /// lets the rest scroll.
        /// </summary>
        private void Fit()
        {
            if (_card == null) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_body);

            // The first fit can run before the canvas has laid out, when the
            // scrim's rect is still zero; the reference height is the floor
            // the scaler guarantees, so it is the right fallback.
            float screen = _root.rect.height > 1f ? _root.rect.height : ScreenLayout.Reference.y;
            float available = Mathf.Max(120f, screen - 2f * Margin);
            float wanted = LayoutUtility.GetPreferredHeight(_body);

            _card.sizeDelta = new Vector2(FittedWidth, Mathf.Min(wanted, available));

            // Nothing to scroll is the common case, and a card that can be
            // dragged an inch when it all fits reads as broken.
            _scroll.vertical = wanted > available + 1f;
            if (!_scroll.vertical) _body.anchoredPosition = Vector2.zero;
        }

        protected void Show()
        {
            if (_root == null) return;

            _closing = false;
            _fader.blocksRaycasts = true;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Rebuild();

            UiTween.FadeIn(_fader, 0.28f);
            UiTween.SlideIn(_card, new Vector2(0f, -28f), 0.3f);
            UiTween.StaggerIn(_body);
        }

        public virtual void Close()
        {
            if (_root == null || !IsOpen || _closing) return;

            _closing = true;
            _fader.blocksRaycasts = false;
            UiTween.Fade(_fader, 0f, 0.12f,
                done: () => { if (_root != null) _root.gameObject.SetActive(false); });
        }

        /// <summary>A quick fade and settle for a page swap inside the card (U1).</summary>
        protected void PlayPageTransition()
        {
            if (_cardFader == null) return;

            UiTween.FadeIn(_cardFader, 0.18f);
            UiTween.ScaleIn(_card, 0.985f, 0.18f);
        }

        protected virtual void LateUpdate()
        {
            if (IsOpen && _root.GetSiblingIndex() != _root.parent.childCount - 1)
                _root.SetAsLastSibling();

            // A turn of the phone changes both the width the card may take and
            // the height it has to fit into, and the composed rows themselves
            // read the arrangement, so the page is composed again (M5).
            if (_fittedLayout == ScreenLayout.Version || !IsOpen) return;

            _fittedLayout = ScreenLayout.Version;
            var column = _body.GetComponent<VerticalLayoutGroup>();
            if (column != null) column.padding.left = column.padding.right = Padding;
            Rebuild();
        }

        protected void Rebuild()
        {
            if (!IsOpen) return;

            for (int i = _body.childCount - 1; i >= 0; i--)
            {
                var child = _body.GetChild(i);
                if (!child.name.StartsWith("content_")) continue;

                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            Compose();
            Fit();
        }

        /// <summary>Fills the card. Called on every rebuild.</summary>
        protected abstract void Compose();

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>A laid-out slot on the card, named so a rebuild can find it.</summary>
        protected RectTransform Slot(string name, float height = -1f)
        {
            var slot = UiKit.Rect("content_" + name, _body);
            if (height >= 0f) UiKit.Size(slot, height: height);
            return slot;
        }

        /// <summary>A slot that lays out one child at the child's preferred height, for rows built elsewhere.</summary>
        protected RectTransform ColumnSlot(string name)
        {
            var slot = Slot(name);
            UiKit.Column(slot, 0f).childForceExpandHeight = true;
            return slot;
        }

        protected void Title(string title, string subtitle, Color? colour = null)
        {
            var heading = UiKit.Caption(Slot("title", 46f), title, 34f, colour ?? UiTheme.GoldBright,
                TextAlignmentOptions.Center);
            UiFonts.ApplyDisplay(heading);
            heading.fontStyle = FontStyles.Bold;
            heading.characterSpacing = UiTheme.HeadingSpacing * 1.5f;

            if (!string.IsNullOrEmpty(subtitle))
                UiKit.Caption(Slot("subtitle", 22f), subtitle, UiTheme.FontSmall, UiTheme.TextDim,
                    TextAlignmentOptions.Center);

            UiKit.Divider(Slot("divider"), vertical: false);
        }

        protected void Heading(string text)
        {
            var slot = Slot("heading", 24f);
            UiKit.Column(slot, 0f).childForceExpandHeight = true;
            UiKit.Heading(slot, text);
        }

        protected void Note(string text, Color colour, float height = 20f)
        {
            UiKit.Caption(Slot("note", height), text, 13f, colour, TextAlignmentOptions.Center)
                .textWrappingMode = TextWrappingModes.Normal;
        }

        protected void Gap(float height) => Slot("gap", height);

        /// <summary>A full-width button with its key in gold.</summary>
        protected Button Choice(string label, string key, System.Action press,
            Color? fill = null, Color? edge = null, bool interactable = true)
        {
            var slot = Slot("choice", ButtonHeight);
            UiKit.Column(slot, 0f).childForceExpandHeight = true;

            var button = UiKit.Button(slot, WithKey(label, key), press, Rebuild,
                interactable: interactable, size: UiTheme.FontLarge, tint: fill, edge: edge);
            return button;
        }

        /// <summary>A row of equal buttons; returns the row for the caller to fill.</summary>
        protected RectTransform ButtonRow(string name, float height = ButtonHeight)
        {
            var row = Slot(name, height);
            var layout = UiKit.Row(row, 10f);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row;
        }

        /// <summary>A label with its key in gold — and without it when there is no keyboard (M4).</summary>
        protected static string WithKey(string label, string key) =>
            string.IsNullOrEmpty(key) || ScreenLayout.Touch
                ? label
                : $"{label}  <size=60%><color=#{UiTheme.Hex(UiTheme.Gold)}>{key}</color></size>";
    }
}
