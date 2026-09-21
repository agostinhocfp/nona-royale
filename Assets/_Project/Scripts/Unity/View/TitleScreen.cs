// Assets/_Project/Scripts/Unity/View/TitleScreen.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>What the title screen may read and ask for (GUI increment J).</summary>
    public interface ITitleHost : ISettingsHost
    {
        /// <summary>Opens the setup screen.</summary>
        void Play();

        /// <summary>
        /// Opens the operator guide (OPERATOR_GUIDE.md OG2): the roster and the
        /// glossary, with no clock. The main menu is its primary entry.
        /// </summary>
        void OpenGuide();

        /// <summary>Leaves the game. Stops Play Mode in the editor.</summary>
        void Quit();
    }

    /// <summary>
    /// The title screen: the wordmark over the board, and PLAY, OPERATORS,
    /// SETTINGS, QUIT (GUI increment J; laid out by VISUAL_PASS.md V3b;
    /// OPERATORS since OPERATOR_GUIDE.md OG2).
    /// </summary>
    /// <remarks>
    /// <b>No card, and no column.</b> V3's mockup review found that a centred
    /// 520-wide stack of wordmark and buttons covers the board almost entirely,
    /// whatever is behind it. So the main page composes into two bands hung off
    /// the scrim instead: the wordmark pinned to the top, the buttons in one
    /// horizontal row along the bottom, and the board left whole between them.
    /// The scrim is light (0.20) because there is now something worth seeing
    /// through it.
    ///
    /// <b>The title is the one flat screen.</b> Every other menu keeps the
    /// tilted salon; here the board lies straight down, the way it did before
    /// the room existed, which is the composition the bands were laid out for.
    /// <c>MatchBootstrap.FrameCamera</c> owns that, and <see cref="RoomBackdrop"/>
    /// takes itself off screen under a flat camera without being told.
    ///
    /// <b>The bands are not the card.</b> The settings pages still build into
    /// <see cref="ModalCard"/>'s centred column, so a page swap fades and
    /// settles the card alone and the wordmark never flinches. The cost is that
    /// the card's entrance animation does not reach the bands, so
    /// <see cref="Open"/> gives them their own.
    ///
    /// <b>The only way out of the game.</b> In-match menus return here
    /// (MAIN MENU); QUIT lives on this screen alone, and asks twice.
    ///
    /// Enter plays; Esc goes back from settings. Settings are the same rows as
    /// the pause menu's and are remembered between sessions.
    /// </remarks>
    public sealed class TitleScreen : ModalCard
    {
        /// <summary>Top of the screen to the top of the wordmark, canvas units.</summary>
        private static float LockupTop => ScreenLayout.Pick(68f, 44f);

        /// <summary>Bottom of the screen to the bottom of the menu row.</summary>
        private static float MenuBottom => ScreenLayout.Pick(56f, 36f);

        private static float NonaHeight => ScreenLayout.Pick(112f, 88f);
        private static float NonaSize => ScreenLayout.Pick(108f, 78f);
        private static float RoyaleHeight => ScreenLayout.Pick(40f, 34f);
        private const float NoteHeight = 20f;
        private const float LockupSpacing = 8f;
        private const float BandSpacing = 12f;

        /// <summary>How many buttons the main menu holds.</summary>
        private const int MenuButtons = 4;

        /// <summary>What the menu row itself takes: all across, or all stacked (MOBILE.md, M5).</summary>
        private static float MenuHeight =>
            ScreenLayout.Pick(ButtonHeight, MenuButtons * ButtonHeight + (MenuButtons - 1) * MenuStackGap);

        private const float MenuStackGap = 10f;

        /// <summary>
        /// What the bands take off the top and the bottom of the screen, for
        /// the camera to frame the board inside - the same contract
        /// <see cref="TurnStrip.ReservedHeight"/> and
        /// <see cref="ActionTray.ReservedHeight"/> have in a match.
        /// </summary>
        /// <remarks>
        /// The band's own height plus its inset plus a margin, so the board's
        /// edge does not come up against the type. The armed QUIT's warning is
        /// counted whether or not it is showing: the board must not resize when
        /// QUIT is pressed.
        /// </remarks>
        public static float ReservedTop => LockupTop + NonaHeight + LockupSpacing + RoyaleHeight + 22f;

        /// <summary>What the menu row takes off the bottom. See <see cref="ReservedTop"/>.</summary>
        public static float ReservedBottom => MenuBottom + MenuHeight + NoteHeight + BandSpacing + 18f;

        /// <summary>
        /// How wide the bands are. Wide enough for the rules either side of
        /// ROYALE, and never wider than the screen (M5).
        /// </summary>
        private static float BandWidth => Mathf.Min(900f, ScreenLayout.Reference.x - 32f);

        /// <summary>The build stamp's box, and how far its corner sits off the screen's.</summary>
        private const float StampWidth = 360f, StampHeight = 18f, StampInset = 22f;

        /// <summary>
        /// Every menu button is the same width, whatever its word, so the row
        /// reads as one set rather than four sizes of thing. Sized for CONFIRM
        /// QUIT, the longest label that ever lands here. Four across is 882
        /// units against a 900-unit band, so the row still fits wide.
        /// </summary>
        private static float MenuButtonWidth => ScreenLayout.Pick(210f, 250f);

        private enum Page { Main, Settings, Sound, Display }

        private ITitleHost _host;
        private Page _page;
        private bool _quitArmed;

        private RectTransform _lockup;
        private RectTransform _menu;
        private RectTransform _row;
        private RectTransform _glow;
        private RectTransform _stamp;

        protected override float CardWidth => 520f;

        /// <summary>
        /// Light (V3b). The board is whole behind the lockup now, so the scrim
        /// only has to keep type legible; at 0.45 it was hiding the one thing
        /// the layout exists to show.
        /// </summary>
        protected override float ScrimAlpha => 0.20f;

        protected override bool Framed => false;

        public void Bind(RectTransform canvasRect, ITitleHost host)
        {
            _host = host;

            if (!IsBuilt)
            {
                BuildOnce(canvasRect, "title_screen");
                AddGlow();
                AddBands();
                PlaceFurniture();
            }

            if (IsOpen) Close();
        }

        public void Open()
        {
            _page = Page.Main;
            _quitArmed = false;
            Show();

            // Show() rises and cascades the card, which the bands are not in.
            // They come in from their own edges instead, so the screen assembles
            // from the outside rather than from a middle that is empty here.
            //
            // Put back where they belong first: the bands outlive every rebuild,
            // and SlideIn reads its destination off the rect, so a slide started
            // over an interrupted one would settle short and stay there.
            _lockup.anchoredPosition = new Vector2(0f, -LockupTop);
            _menu.anchoredPosition = new Vector2(0f, MenuBottom);

            UiTween.SlideIn(_lockup, new Vector2(0f, 26f), 0.34f);
            UiTween.SlideIn(_menu, new Vector2(0f, -22f), 0.34f);
            UiTween.StaggerIn(_row, "button", 0.06f, 0.24f);
        }

        /// <summary>Enter: play, from the main page.</summary>
        public void Confirm()
        {
            if (IsOpen && _page == Page.Main) Play();
        }

        /// <summary>Esc: a sub-page goes back a page; the main page disarms QUIT.</summary>
        public void Back()
        {
            if (!IsOpen) return;

            if (_page == Page.Sound || _page == Page.Display) _page = Page.Settings;
            else if (_page == Page.Settings) _page = Page.Main;
            _quitArmed = false;
            Rebuild();
            PlayPageTransition();
        }

        private void Play()
        {
            Close();
            _host.Play();
        }

        private void OpenGuide()
        {
            _quitArmed = false;
            Close();
            _host.OpenGuide();
        }

        /// <summary>A warm light behind the wordmark, under everything else.</summary>
        private void AddGlow()
        {
            _glow = UiKit.Rect("title_glow", Root);
            _glow.SetSiblingIndex(0);
            _glow.anchorMin = _glow.anchorMax = new Vector2(0.5f, 1f);
            _glow.pivot = new Vector2(0.5f, 0.5f);

            var image = UiKit.Fill(_glow, UiTheme.WithAlpha(UiTheme.GoldBright, 0.16f));
            image.sprite = DecoSprites.Glow;
        }

        /// <summary>
        /// Puts the glow, the bands and the stamp where this screen shape wants
        /// them (MOBILE.md, M5). Everything outside the card is built once and
        /// outlives every rebuild, so the numbers it was built with have to be
        /// written again when the screen turns.
        /// </summary>
        private void PlaceFurniture()
        {
            if (_glow != null)
            {
                _glow.sizeDelta = ScreenLayout.IsPortrait ? new Vector2(640f, 430f) : new Vector2(1200f, 720f);
                _glow.anchoredPosition = new Vector2(0f, -(LockupTop + ScreenLayout.Pick(56f, 40f)));
            }

            if (_lockup != null)
            {
                _lockup.sizeDelta = new Vector2(BandWidth, 0f);
                _lockup.anchoredPosition = new Vector2(0f, -LockupTop);
            }

            if (_menu != null)
            {
                _menu.sizeDelta = new Vector2(BandWidth, 0f);
                _menu.anchoredPosition = new Vector2(0f, MenuBottom);
            }

            // Upright the bottom corner is the menu stack's, not spare space.
            if (_stamp != null) _stamp.gameObject.SetActive(!ScreenLayout.IsPortrait);
        }

        /// <summary>
        /// The two bands the screen composes into. Built once, emptied and
        /// refilled on every rebuild like the card is.
        /// </summary>
        private void AddBands()
        {
            _lockup = Band("title_lockup", new Vector2(0.5f, 1f), new Vector2(0f, -LockupTop), LockupSpacing);
            _menu = Band("title_menu", new Vector2(0.5f, 0f), new Vector2(0f, MenuBottom), BandSpacing);
            AddStamp();
        }

        /// <summary>
        /// What this build is, in the bottom-right corner.
        /// </summary>
        /// <remarks>
        /// Outside both bands and outside the card, and built once rather than
        /// composed: it is a stamp on the screen, not a line in the menu, so it
        /// stays put while the pages change and does not move when the armed
        /// QUIT pushes the row up. It costs the board nothing either - the
        /// board is square and centred, so on any wide window the corner is
        /// empty space the framing was never going to use.
        /// </remarks>
        private void AddStamp()
        {
            _stamp = UiKit.Rect("title_stamp", Root);
            _stamp.anchorMin = _stamp.anchorMax = _stamp.pivot = new Vector2(1f, 0f);
            _stamp.sizeDelta = new Vector2(StampWidth, StampHeight);
            _stamp.anchoredPosition = new Vector2(-StampInset, StampInset);

            UiKit.Caption(_stamp, "Prototype build · pieces and board drawn in code", 12f, UiTheme.TextDim,
                TextAlignmentOptions.MidlineRight);
        }

        /// <summary>
        /// A band pinned to one edge of the scrim: a fixed-width centred column
        /// that grows away from that edge as it is filled.
        /// </summary>
        private RectTransform Band(string name, Vector2 anchor, Vector2 offset, float spacing)
        {
            var band = UiKit.Rect(name, Root);
            band.anchorMin = band.anchorMax = anchor;
            band.pivot = anchor;
            band.sizeDelta = new Vector2(BandWidth, 0f);
            band.anchoredPosition = offset;

            UiKit.Column(band, spacing);
            band.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return band;
        }

        protected override void Compose()
        {
            Clear(_lockup);
            Clear(_menu);
            PlaceFurniture();

            Wordmark();

            if (_page == Page.Main) MainPage();
            else if (_page == Page.Sound) SoundPage();
            else if (_page == Page.Display) DisplayPage();
            else SettingsPage();

            LayoutRebuilder.ForceRebuildLayoutImmediate(_lockup);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_menu);
        }

        /// <summary>
        /// Empties a band. Deactivated before it is destroyed, because Destroy
        /// only takes effect at the end of the frame and a live layout group
        /// would still count the old children in the rebuild that follows.
        /// </summary>
        private static void Clear(RectTransform band)
        {
            if (band == null) return;

            for (int i = band.childCount - 1; i >= 0; i--)
            {
                var child = band.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        /// <summary>A fixed-height slot in a band.</summary>
        private static RectTransform Row(RectTransform band, string name, float height)
        {
            var slot = UiKit.Rect(name, band);
            UiKit.Size(slot, height: height);
            return slot;
        }

        // ── The lockup ───────────────────────────────────────────────────

        /// <summary>
        /// NONA over ROYALE between two ruled diamonds. It sits on every page,
        /// outside the card, so it holds still while the pages change under it.
        /// </summary>
        private void Wordmark()
        {
            var nona = UiKit.Caption(Row(_lockup, "lockup_nona", NonaHeight), "NONA", NonaSize, UiTheme.GoldBright,
                TextAlignmentOptions.Center);
            UiFonts.ApplyDisplay(nona);
            nona.fontStyle = FontStyles.Bold;
            nona.characterSpacing = 28f;
            nona.overflowMode = TextOverflowModes.Overflow;

            // ROYALE between two rules, each ending in a diamond.
            var line = Row(_lockup, "lockup_royale", RoyaleHeight);
            var row = UiKit.Row(line, 14f);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandHeight = false;

            Rule(line);
            var royale = UiKit.Label(line, "ROYALE", 30f, UiTheme.Gold, TextAlignmentOptions.Center, bold: true);
            UiFonts.ApplyDisplay(royale);
            royale.characterSpacing = 42f;
            royale.overflowMode = TextOverflowModes.Overflow;
            UiKit.Fixed(royale, ScreenLayout.Pick(250f, 186f), RoyaleHeight);
            Rule(line);
        }

        /// <summary>A short gold rule with a diamond at its outer end.</summary>
        private static void Rule(Transform parent)
        {
            var box = UiKit.Rect("rule", parent);
            UiKit.Fixed(box, ScreenLayout.Pick(70f, 46f), 16f);

            var line = UiKit.Rect("line", box);
            UiKit.Fill(line, UiTheme.Gold);
            line.anchorMin = new Vector2(0f, 0.5f);
            line.anchorMax = new Vector2(1f, 0.5f);
            line.sizeDelta = new Vector2(0f, 2f);

            var gem = UiKit.Rect("gem", box);
            var image = UiKit.Fill(gem, UiTheme.GoldBright);
            image.sprite = DecoSprites.Diamond;
            gem.anchorMin = gem.anchorMax = new Vector2(0.5f, 0.5f);
            gem.sizeDelta = new Vector2(10f, 15f);
            gem.localEulerAngles = new Vector3(0f, 0f, 90f);
        }

        // ── The pages ────────────────────────────────────────────────────

        /// <summary>
        /// The menu, in the bottom band: four equal buttons in a row, and the
        /// armed QUIT's warning above them rather than below, which would put it
        /// off the bottom edge.
        /// </summary>
        private void MainPage()
        {
            if (_quitArmed)
                UiKit.Caption(Row(_menu, "menu_note", NoteHeight), "Leaves the game.", 13f, UiTheme.Threat,
                    TextAlignmentOptions.Center);

            _row = Row(_menu, "menu_row", MenuHeight);

            // Four 210-unit buttons need 882 units of width and a phone has
            // under 480, so upright they stack (M5). PLAY stays first, which
            // upright also puts it furthest from the thumb's resting place —
            // the one button here that should not be pressed by accident is
            // QUIT, and it ends up at the bottom either way.
            if (ScreenLayout.IsPortrait)
            {
                var stack = UiKit.Column(_row, MenuStackGap);
                stack.childAlignment = TextAnchor.MiddleCenter;
                stack.childForceExpandWidth = false;
                stack.childForceExpandHeight = false;
            }
            else
            {
                var row = UiKit.Row(_row, 14f);
                row.childAlignment = TextAnchor.MiddleCenter;
                row.childForceExpandHeight = true;
            }

            MenuButton("PLAY", "Enter", Play, UiTheme.CyanDeep, UiTheme.Cyan);

            // Second, beside PLAY: the guide is where a new player is meant to
            // learn the roster, with no draft clock running (OPERATOR_GUIDE D1).
            MenuButton("OPERATORS", "", OpenGuide);
            MenuButton("SETTINGS", "", () => { _page = Page.Settings; _quitArmed = false; PlayPageTransition(); });

            if (_quitArmed) MenuButton("CONFIRM QUIT", "", () => _host.Quit(), UiTheme.GoldDeep, UiTheme.Threat);
            else MenuButton("QUIT", "", () => _quitArmed = true);
        }

        /// <summary>One button in the bottom row. Fixed, so the row keeps its rhythm as labels change.</summary>
        private void MenuButton(string label, string key, System.Action press, Color? fill = null, Color? edge = null)
        {
            var button = UiKit.Button(_row, WithKey(label, key), press, Rebuild,
                size: UiTheme.FontLarge, tint: fill, edge: edge);
            UiKit.Fixed(button, MenuButtonWidth, ButtonHeight);
        }

        private void SettingsPage()
        {
            Heading("Settings");
            SettingsRows.Build(ColumnSlot, _host, Rebuild,
                () => { _page = Page.Sound; Rebuild(); PlayPageTransition(); },
                () => { _page = Page.Display; Rebuild(); PlayPageTransition(); });
            Note("Remembered between sessions.", UiTheme.TextNote);
            Gap(6f);
            Choice("BACK", "Esc", Back);
        }

        private void SoundPage()
        {
            Heading("Sound");
            SettingsRows.BuildSound(ColumnSlot, _host, Rebuild);
            Gap(6f);
            Choice("BACK", "Esc", Back);
        }

        private void DisplayPage()
        {
            Heading("Display");
            SettingsRows.BuildDisplay(ColumnSlot, _host, Rebuild);
            Note("Remembered between sessions.", UiTheme.TextNote);
            Gap(6f);
            Choice("BACK", "Esc", Back);
        }
    }
}
