// Assets/_Project/Scripts/Unity/View/PauseMenu.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The pause menu: resume, new match, settings, main menu (GUI increments H to J).
    /// </summary>
    /// <remarks>
    /// <b>Opened by Esc when nothing is selected</b>, or by the MENU button on
    /// the top bar. With a selection, Esc still steps back first
    /// (PRESENTATION §4.1), so a player backing out of an aim never lands in
    /// the menu by surprise. While open, Esc goes back a page and then
    /// resumes.
    ///
    /// <b>Pausing stops the clock.</b> <c>Time.timeScale</c> is 0 while the
    /// menu is up, so walks, flashes and floaters freeze where they are. The
    /// HUD's own animations (pulses, toasts, the turn pill) run on unscaled
    /// time and keep breathing. The composition root ignores board input and
    /// game keys while <see cref="IsOpen"/>.
    ///
    /// <b>MAIN MENU asks twice</b> (it was QUIT until increment J; only the
    /// title screen quits now). The first press turns the button amber and
    /// says what will be lost; the second press does it. Any other press
    /// disarms it. NEW MATCH (was RESTART until increment I) opens the setup
    /// screen, which has its own way back, so it does not ask.
    ///
    /// <b>Two pages, rebuilt on every change</b>, like the rail and the tray.
    /// The scrim covers the whole canvas and catches the pointer, so nothing
    /// under the menu can be clicked.
    /// </remarks>
    public sealed class PauseMenu : MonoBehaviour
    {
        private const float CardWidth = 440f;
        private const float ButtonHeight = 54f;

        private enum Page { Main, Settings, Sound, Display }

        private IPauseHost _host;
        private RectTransform _root;
        private RectTransform _card;
        private CanvasGroup _fader;
        private CanvasGroup _cardFader;
        private Page _page;
        private string _armed;
        private float _timeScale = 1f;
        private bool _closing;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Bind(RectTransform canvasRect, IPauseHost host)
        {
            _host = host;
            if (_root == null) Build(canvasRect);
            if (IsOpen) Close();
        }

        public void Open()
        {
            if (_root == null || IsOpen) return;

            _page = Page.Main;
            _armed = null;
            _closing = false;

            // A hit-stop (MO2) may have slowed the clock; resuming must never keep that.
            _timeScale = Time.timeScale >= 1f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            _fader.blocksRaycasts = true;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Rebuild();

            UiTween.FadeIn(_fader, 0.18f);
            UiTween.SlideIn(_card, new Vector2(0f, -18f), 0.22f);
            UiTween.StaggerIn(_card);
        }

        public void Close()
        {
            if (_root == null || !IsOpen || _closing) return;

            _closing = true;
            _fader.blocksRaycasts = false;
            Time.timeScale = _timeScale > 0f ? _timeScale : 1f;
            UiTween.Fade(_fader, 0f, 0.12f,
                done: () => { if (_root != null) _root.gameObject.SetActive(false); });
        }

        /// <summary>Esc while open: a sub-page goes back a page, the main page resumes.</summary>
        public void Back()
        {
            if (!IsOpen) return;

            if (_page != Page.Main)
            {
                _page = _page == Page.Settings ? Page.Main : Page.Settings;
                _armed = null;
                Rebuild();
                PageTransition();
                return;
            }

            Close();
        }

        private void LateUpdate()
        {
            // Overlays that open later (the log, a hover card) put themselves
            // last; the menu takes the top back.
            if (IsOpen && _root.GetSiblingIndex() != _root.parent.childCount - 1)
                _root.SetAsLastSibling();
        }

        private void OnDisable()
        {
            // Never leave the game frozen behind a menu that is gone.
            if (IsOpen) Close();
        }

        // ── Scaffold ─────────────────────────────────────────────────────

        private void Build(RectTransform canvasRect)
        {
            _root = UiKit.Rect("pause_menu", canvasRect);
            UiKit.Stretch(_root);
            UiKit.Fill(_root, UiTheme.WithAlpha(UiTheme.Obsidian, 0.78f), blocksPointer: true);

            _card = UiKit.Rect("card", _root);
            _card.anchorMin = new Vector2(0.5f, 0.5f);
            _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(CardWidth, 0f);
            UiKit.Panel(_card, blocksPointer: true);

            var column = UiKit.Column(_card, 10f, 34);
            column.padding.top = 30;
            column.padding.bottom = 28;
            _card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _fader = _root.gameObject.AddComponent<CanvasGroup>();
            _cardFader = _card.gameObject.AddComponent<CanvasGroup>();

            _root.gameObject.SetActive(false);
        }

        /// <summary>A quick fade and settle for a page swap inside the card (U1).</summary>
        private void PageTransition()
        {
            UiTween.FadeIn(_cardFader, 0.12f);
            UiTween.ScaleIn(_card, 0.99f, 0.12f);
        }

        private void Rebuild()
        {
            // Children after the card's frame and fans are content.
            for (int i = _card.childCount - 1; i >= 0; i--)
            {
                var child = _card.GetChild(i);
                if (!child.name.StartsWith("content_")) continue;

                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            if (_page == Page.Main) MainPage();
            else if (_page == Page.Sound) SoundPage();
            else if (_page == Page.Display) DisplayPage();
            else SettingsPage();

            LayoutRebuilder.ForceRebuildLayoutImmediate(_card);
        }

        // ── Pages ────────────────────────────────────────────────────────

        private void MainPage()
        {
            Title("Paused", _host?.PauseSummary ?? "");

            Choice("RESUME", "Esc", Close, UiTheme.CyanDeep, UiTheme.Cyan);
            Space(4f);

            Choice("NEW MATCH", "", () => { Close(); _host?.OpenSetup(); });
            Choice("SETTINGS", "", () => { _page = Page.Settings; _armed = null; Rebuild(); PageTransition(); });
            Destructive("menu", "MAIN MENU", "Back to the title. This match is lost.", () => { Close(); _host?.MainMenu(); });

            Footer("Esc resumes");
        }

        private void SettingsPage()
        {
            Title("Settings", "Changes apply at once.");

            if (_host != null) SettingsRows.Build(Content, _host, Rebuild,
                () => { _page = Page.Sound; Rebuild(); PageTransition(); },
                () => { _page = Page.Display; Rebuild(); PageTransition(); });

            Space(4f);
            Choice("BACK", "Esc", Back);
        }

        private void SoundPage()
        {
            Title("Sound", "Changes apply at once.");

            if (_host != null) SettingsRows.BuildSound(Content, _host, Rebuild);

            Space(4f);
            Choice("BACK", "Esc", Back);
        }

        private void DisplayPage()
        {
            Title("Display", "Changes apply at once.");

            if (_host != null) SettingsRows.BuildDisplay(Content, _host, Rebuild);

            Space(4f);
            Choice("BACK", "Esc", Back);
        }

        // ── Pieces ───────────────────────────────────────────────────────

        private void Title(string title, string subtitle)
        {
            var heading = UiKit.Label(Content("title"), title.ToUpperInvariant(), 34f, UiTheme.GoldBright,
                TextAlignmentOptions.Center, bold: true);
            UiFonts.ApplyDisplay(heading);
            heading.characterSpacing = UiTheme.HeadingSpacing * 1.5f;
            UiKit.Size(heading, height: 44f);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var line = UiKit.Label(Content("subtitle"), subtitle, UiTheme.FontSmall, UiTheme.TextDim,
                    TextAlignmentOptions.Center);
                UiKit.Size(line, height: 22f);
            }

            UiKit.Divider(Content("divider"), vertical: false);
        }

        private void Choice(string label, string key, System.Action press, Color? fill = null, Color? edge = null)
        {
            var button = UiKit.Button(Content("action"), Caption(label, key, UiTheme.Gold),
                () => { _armed = null; press(); }, size: UiTheme.FontLarge, tint: fill, edge: edge);
            UiKit.Size(button, height: ButtonHeight);
        }

        /// <summary>A button that asks again before it acts.</summary>
        private void Destructive(string id, string label, string warning, System.Action act)
        {
            bool armed = _armed == id;

            var button = UiKit.Button(Content(id),
                armed ? $"CONFIRM {label}" : label,
                () =>
                {
                    if (_armed == id)
                    {
                        _armed = null;
                        act();
                        return;
                    }

                    _armed = id;
                    Rebuild();
                },
                size: UiTheme.FontLarge,
                tint: armed ? UiTheme.GoldDeep : (Color?)null,
                edge: armed ? UiTheme.Threat : (Color?)null);
            UiKit.Size(button, height: ButtonHeight);

            if (armed)
            {
                var note = UiKit.Label(Content(id + "_note"), warning, 13f, UiTheme.Threat,
                    TextAlignmentOptions.Center, wrap: true);
                UiKit.Size(note, height: 20f);
            }
        }

        private void Footer(string text)
        {
            var footer = UiKit.Label(Content("footer"), text, 13f, UiTheme.TextOff, TextAlignmentOptions.Center);
            UiKit.Size(footer, height: 18f);
        }

        private void Space(float height) => UiKit.Size(Content("space"), height: height);

        /// <summary>A laid-out slot on the card, named so a rebuild can find it.</summary>
        private RectTransform Content(string name)
        {
            var slot = UiKit.Rect("content_" + name, _card);
            var column = UiKit.Column(slot, 0f);
            column.childForceExpandHeight = true;
            return slot;
        }

        private static string Caption(string label, string key, Color keyColour) =>
            string.IsNullOrEmpty(key)
                ? label
                : $"{label}  <size=60%><color=#{UiTheme.Hex(keyColour)}>{key}</color></size>";
    }
}
