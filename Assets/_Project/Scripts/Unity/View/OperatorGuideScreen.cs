// Assets/_Project/Scripts/Unity/View/OperatorGuideScreen.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Title → OPERATORS (OPERATOR_GUIDE.md OG2): the whole roster and the
    /// glossary, with no clock and nothing at stake.
    /// </summary>
    /// <remarks>
    /// <b>The primary entry point</b> (designer, 2026-09-21). The draft is not
    /// where the roster is meant to be learned — it runs a clock, and a new
    /// player reading there is paying for it (D4). This screen is where they
    /// can take their time.
    ///
    /// <b>Two tabs.</b> OPERATORS lists the roster and shows one dossier;
    /// GLOSSARY lists every keyword, grouped. A keyword tapped anywhere opens
    /// its card (<see cref="GlossaryCard"/>) over whichever tab is showing.
    ///
    /// <b>Two shapes</b> (MOBILE.md M5). Wide, the roster list sits on the
    /// left and the dossier fills the right, and the first operator is shown
    /// on arrival so the page is never empty. Upright there is no width for
    /// both: the roster is a three-across grid, a tap opens the dossier as its
    /// own page, and BACK (or Esc) returns to the grid.
    ///
    /// <b>Built once per change, never per frame.</b> A tab, a selection or a
    /// turn of the phone rebuilds the body; nothing else does.
    /// </remarks>
    public sealed class OperatorGuideScreen : MonoBehaviour
    {
        private enum Tab { Operators, Glossary }

        private const float WideMaxWidth = 1560f;
        private const float WideListWidth = 340f;
        private const float TopBarHeight = 64f;
        private const float UprightTopBarHeight = 56f;
        private const int UprightColumns = 3;

        private Action _onClose;

        /// <summary>Where this opening returns to, when it is not the title (OG4: the match, the pause menu).</summary>
        private Action _closeOverride;

        private RectTransform _root;
        private RectTransform _frame;
        private CanvasGroup _fader;
        private RectTransform _body;

        private Tab _tab;
        private OperatorDefinition _selected;
        private bool _builtPortrait;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        /// <summary>Builds the screen under <paramref name="canvasRect"/>. <paramref name="onClose"/> runs when the player leaves it.</summary>
        public void Bind(RectTransform canvasRect, Action onClose)
        {
            _onClose = onClose;

            if (_root == null)
            {
                _root = UiKit.Rect("operator_guide", canvasRect);
                UiKit.Stretch(_root);
                UiKit.Fill(_root, UiTheme.WithAlpha(UiTheme.Obsidian, 0.96f), blocksPointer: true);
                _fader = _root.gameObject.AddComponent<CanvasGroup>();
                _root.gameObject.SetActive(false);
            }

            if (IsOpen) Close();
        }

        /// <summary>
        /// Opens on the roster, or on <paramref name="focus"/>'s dossier. Wide,
        /// someone is always showing.
        /// </summary>
        /// <param name="onClose">
        /// Where leaving goes this time, when it is not the default the screen
        /// was bound with: a match opens the guide on a piece and wants the
        /// match back, not the title (OPERATOR_GUIDE.md OG4).
        /// </param>
        public void Open(OperatorDefinition focus = null, Action onClose = null)
        {
            if (_root == null) return;

            _closeOverride = onClose;
            _tab = Tab.Operators;
            _selected = focus ?? (ScreenLayout.IsPortrait ? null : First());

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            BuildFrame();

            UiTween.FadeIn(_fader, 0.24f);
            UiTween.SlideIn(_frame, new Vector2(0f, -18f), 0.26f);
        }

        public void Close()
        {
            GlossaryCard.Close();
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>Esc: the innermost thing first — a keyword card, then an upright dossier page, then the screen.</summary>
        public void HandleKeys()
        {
            if (!IsOpen || !Input.GetKeyDown(KeyCode.Escape)) return;
            Back();
        }

        private void Back()
        {
            if (GlossaryCard.IsOpen)
            {
                GlossaryCard.Close();
                return;
            }

            if (ScreenLayout.IsPortrait && _tab == Tab.Operators && _selected != null)
            {
                _selected = null;
                BuildFrame();
                return;
            }

            var leave = _closeOverride ?? _onClose;
            _closeOverride = null;

            Close();
            leave?.Invoke();
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Over a match it shares the root with the HUD, which rebuilds
            // under it; it stays on top the way the draft does.
            if (_root.GetSiblingIndex() != _root.parent.childCount - 1) _root.SetAsLastSibling();

            if (_builtPortrait != ScreenLayout.IsPortrait)
            {
                // Wide always shows someone; upright starts on the grid.
                if (!ScreenLayout.IsPortrait && _selected == null) _selected = First();
                BuildFrame();
            }
        }

        private static OperatorDefinition First() => Roster.All.Count > 0 ? Roster.All[0] : null;

        private void ShowKeyword(string id) => GlossaryCard.Show(_root, id);

        // ── Frame ────────────────────────────────────────────────────────

        private void BuildFrame()
        {
            GlossaryCard.Close();

            if (_frame != null)
            {
                _frame.gameObject.SetActive(false);
                Destroy(_frame.gameObject);
            }

            _builtPortrait = ScreenLayout.IsPortrait;

            _frame = UiKit.Rect("frame", _root);
            _frame.anchorMin = new Vector2(0.5f, 0f);
            _frame.anchorMax = new Vector2(0.5f, 1f);
            _frame.pivot = new Vector2(0.5f, 0.5f);
            float margin = ScreenLayout.Pick(24f, 10f);
            float width = Mathf.Min(WideMaxWidth, _root.rect.width - 2f * margin);
            _frame.sizeDelta = new Vector2(Mathf.Max(280f, width), -2f * margin);

            UiKit.Column(_frame, ScreenLayout.Pick(14f, 8f)).childForceExpandHeight = false;

            TopBar();

            _body = UiKit.Rect("body", _frame);
            UiKit.Size(_body, flexibleHeight: 1f);

            if (_tab == Tab.Glossary) GlossaryBody();
            else if (_builtPortrait) UprightOperatorsBody();
            else WideOperatorsBody();

            LayoutRebuilder.ForceRebuildLayoutImmediate(_frame);
        }

        private void TopBar()
        {
            bool upright = _builtPortrait;
            var bar = UiKit.Rect("top_bar", _frame);
            UiKit.Size(bar, height: upright ? UprightTopBarHeight : TopBarHeight, flexibleHeight: 0f);
            var row = UiKit.Row(bar, upright ? 6f : 12f);
            row.childForceExpandHeight = true;

            // Upright, an open dossier's BACK returns to the grid, and says so.
            bool dossierPage = upright && _tab == Tab.Operators && _selected != null;
            string backLabel = dossierPage
                ? "BACK"
                : "BACK" + ScreenLayout.KeyMarkup($"  <size=60%><color=#{UiTheme.Hex(UiTheme.Gold)}>Esc</color></size>");
            var back = UiKit.Button(bar, backLabel, Back, size: upright ? UiTheme.FontSmall : UiTheme.FontBody);
            UiKit.Fixed(back, upright ? 88f : 150f);

            if (!upright)
            {
                var title = UiKit.Label(bar, "OPERATORS", UiTheme.FontTitle, UiTheme.GoldBright, bold: true);
                UiFonts.ApplyDisplay(title);
                title.overflowMode = TextOverflowModes.Overflow;
                title.characterSpacing = UiTheme.HeadingSpacing * 1.5f;
                UiKit.Size(title, flexibleWidth: 1f);
            }
            else
            {
                UiKit.Space(bar, flexible: true);
            }

            TabButton(bar, "ROSTER", Tab.Operators, upright);
            TabButton(bar, "GLOSSARY", Tab.Glossary, upright);
        }

        private void TabButton(RectTransform bar, string label, Tab tab, bool upright)
        {
            bool on = _tab == tab;
            var button = UiKit.Button(bar, label, () =>
            {
                if (_tab == tab) return;
                _tab = tab;
                if (_tab == Tab.Operators && !_builtPortrait && _selected == null) _selected = First();
                BuildFrame();
            }, selected: on, size: upright ? UiTheme.FontSmall : UiTheme.FontBody);
            UiKit.Fixed(button, upright ? 116f : 170f);
        }

        // ── Operators, wide ──────────────────────────────────────────────

        private void WideOperatorsBody()
        {
            var row = UiKit.Row(_body, 18f);
            row.childForceExpandHeight = true;
            row.childAlignment = TextAnchor.UpperLeft;

            var listPanel = UiKit.Rect("roster_panel", _body);
            UiKit.Fixed(listPanel, WideListWidth);
            UiKit.Panel(listPanel, blocksPointer: true, fans: false);
            var list = UiKit.ScrollColumn(ViewportIn(listPanel, 12), 6f);

            foreach (var op in Roster.All) RosterRow(list, op);

            var dossierPanel = UiKit.Rect("dossier_panel", _body);
            UiKit.Size(dossierPanel, flexibleWidth: 1f);
            UiKit.Panel(dossierPanel, blocksPointer: true);
            var dossier = UiKit.ScrollColumn(ViewportIn(dossierPanel, 26), 4f);

            if (_selected != null) OperatorDossier.Build(dossier, _selected, ShowKeyword);
        }

        /// <summary>A viewport filling a panel, inset by <paramref name="inset"/>.</summary>
        private static RectTransform ViewportIn(RectTransform panel, float inset)
        {
            var viewport = UiKit.Rect("viewport", panel);
            viewport.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UiKit.Stretch(viewport, inset);
            return viewport;
        }

        private void RosterRow(RectTransform list, OperatorDefinition op)
        {
            bool on = _selected == op;
            var button = UiKit.Button(list, "", () => { _selected = op; BuildFrame(); }, selected: on);
            UiKit.Size(button, height: 58f);

            var rect = (RectTransform)button.transform;
            var inner = UiKit.Rect("row", rect);
            inner.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UiKit.Stretch(inner, 6f);
            var row = UiKit.Row(inner, 12f);
            row.childForceExpandHeight = true;

            var iconBox = UiKit.Rect("icon", inner);
            UiKit.Fixed(iconBox, 46f);
            OperatorDossier.Icon(iconBox, op, 42f, on ? UiTheme.Cyan : UiTheme.Gold);

            var names = UiKit.Rect("names", inner);
            UiKit.Size(names, flexibleWidth: 1f);
            UiKit.Column(names, 0f).childAlignment = TextAnchor.MiddleLeft;

            var name = UiKit.Label(names, op.Name.ToUpperInvariant(), UiTheme.FontBody, UiTheme.Text, bold: true);
            name.characterSpacing = 3f;
            UiKit.Size(name, height: 24f);
            var role = UiKit.Label(names, OperatorCopy.Role(op.Name).ToUpperInvariant(), 11f, UiTheme.Heading, bold: true);
            role.characterSpacing = UiTheme.HeadingSpacing;
            UiKit.Size(role, height: 16f);
        }

        // ── Operators, upright ───────────────────────────────────────────

        private void UprightOperatorsBody()
        {
            UiKit.Column(_body, 0f).childForceExpandHeight = true;

            var panel = UiKit.Rect("panel", _body);
            UiKit.Size(panel, flexibleHeight: 1f);
            UiKit.Panel(panel, blocksPointer: true, fans: false);
            var content = UiKit.ScrollColumn(ViewportIn(panel, ScreenLayout.Pick(20f, 14f)), 4f);

            if (_selected != null)
            {
                OperatorDossier.Build(content, _selected, ShowKeyword);
                return;
            }

            UiKit.Size(UiKit.Label(content, "Tap an operator to read their dossier.", UiTheme.FontSmall, UiTheme.TextDim,
                TextAlignmentOptions.Center), height: 28f);

            // The grid rows, three across, laid out by hand so the tiles
            // share the width exactly as the draft's do.
            var all = Roster.All;
            for (int i = 0; i < all.Count; i += UprightColumns)
            {
                var line = UiKit.Rect("grid_row", content);
                UiKit.Size(line, height: 118f);
                var row = UiKit.Row(line, 8f);
                row.childForceExpandHeight = true;
                row.childForceExpandWidth = true;

                for (int c = 0; c < UprightColumns; c++)
                {
                    if (i + c < all.Count) Tile(line, all[i + c]);
                    else UiKit.Size(UiKit.Space(line), flexibleWidth: 1f);
                }
            }
        }

        private void Tile(RectTransform row, OperatorDefinition op)
        {
            var button = UiKit.Button(row, "", () => { _selected = op; BuildFrame(); });
            UiKit.Size(button, flexibleWidth: 1f);

            var rect = (RectTransform)button.transform;
            var inner = UiKit.Rect("tile", rect);
            inner.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UiKit.Stretch(inner, 6f);
            var column = UiKit.Column(inner, 2f);
            column.childAlignment = TextAnchor.UpperCenter;

            var iconBox = UiKit.Rect("icon", inner);
            UiKit.Size(iconBox, height: 50f);
            OperatorDossier.Icon(iconBox, op, 46f, UiTheme.Gold);

            var name = UiKit.Label(inner, op.Name.ToUpperInvariant(), 14f, UiTheme.Text, TextAlignmentOptions.Center, bold: true);
            name.characterSpacing = 2f;
            UiKit.Size(name, height: 20f);
            var role = UiKit.Label(inner, OperatorCopy.Role(op.Name).ToUpperInvariant(), 10f, UiTheme.Heading,
                TextAlignmentOptions.Center, bold: true);
            UiKit.Size(role, height: 14f);
        }

        // ── Glossary ─────────────────────────────────────────────────────

        private void GlossaryBody()
        {
            UiKit.Column(_body, 0f).childForceExpandHeight = true;

            var panel = UiKit.Rect("panel", _body);
            UiKit.Size(panel, flexibleHeight: 1f);
            UiKit.Panel(panel, blocksPointer: true);
            var content = UiKit.ScrollColumn(ViewportIn(panel, ScreenLayout.Pick(26f, 14f)), 4f);

            GlossaryGroup? current = null;
            foreach (var entry in Glossary.Entries())
            {
                if (current != entry.Group)
                {
                    current = entry.Group;
                    UiKit.Space(content, height: current == GlossaryGroup.Status ? 0f : 10f);
                    UiKit.Size(UiKit.Heading(content, GroupHeading(entry.Group)), height: 20f);
                }

                GlossaryRow(content, entry);
            }
        }

        private void GlossaryRow(RectTransform content, GlossaryEntry entry)
        {
            var top = UiKit.Rect("entry", content);
            UiKit.Size(top, height: 26f);
            var row = UiKit.Row(top, 8f);
            row.childForceExpandHeight = true;

            var title = UiKit.Label(top, entry.Title, UiTheme.FontLarge, RulesMarkup.ColourOf(entry.Id), bold: true);
            UiKit.Size(title, flexibleWidth: 1f);

            if (entry.Status.HasValue)
                UiKit.Tag(top, StatusPalette.Label(entry.Status.Value), StatusPalette.For(entry.Status.Value), 11f);

            var definition = UiKit.Label(content, RulesMarkup.For(entry.Definition, linked: true),
                UiTheme.FontBody, UiTheme.TextNote, wrap: true);
            KeywordLinks.Attach(definition, ShowKeyword);

            UiKit.Space(content, height: 8f);
        }

        private static string GroupHeading(GlossaryGroup group)
        {
            switch (group)
            {
                case GlossaryGroup.Status: return "Statuses";
                case GlossaryGroup.Damage: return "Damage types";
                default: return "Rules of the table";
            }
        }
    }
}
