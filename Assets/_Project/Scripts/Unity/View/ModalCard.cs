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
    /// </remarks>
    public abstract class ModalCard : MonoBehaviour
    {
        protected const float ButtonHeight = 54f;

        private RectTransform _root;
        private RectTransform _card;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        protected bool IsBuilt => _root != null;

        /// <summary>The full-screen scrim the card sits on.</summary>
        protected RectTransform Root => _root;

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
            _card.sizeDelta = new Vector2(CardWidth, 0f);
            if (Framed) UiKit.Panel(_card, blocksPointer: true);

            var column = UiKit.Column(_card, 10f, 36);
            column.padding.top = 30;
            column.padding.bottom = 30;
            _card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _root.gameObject.SetActive(false);
        }

        protected void Show()
        {
            if (_root == null) return;

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Rebuild();
        }

        public virtual void Close()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        protected virtual void LateUpdate()
        {
            if (IsOpen && _root.GetSiblingIndex() != _root.parent.childCount - 1)
                _root.SetAsLastSibling();
        }

        protected void Rebuild()
        {
            if (!IsOpen) return;

            for (int i = _card.childCount - 1; i >= 0; i--)
            {
                var child = _card.GetChild(i);
                if (!child.name.StartsWith("content_")) continue;

                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            Compose();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_card);
        }

        /// <summary>Fills the card. Called on every rebuild.</summary>
        protected abstract void Compose();

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>A laid-out slot on the card, named so a rebuild can find it.</summary>
        protected RectTransform Slot(string name, float height = -1f)
        {
            var slot = UiKit.Rect("content_" + name, _card);
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

        protected static string WithKey(string label, string key) =>
            string.IsNullOrEmpty(key)
                ? label
                : $"{label}  <size=60%><color=#{UiTheme.Hex(UiTheme.Gold)}>{key}</color></size>";
    }
}
