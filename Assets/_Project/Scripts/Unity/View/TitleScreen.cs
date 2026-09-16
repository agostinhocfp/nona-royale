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

        /// <summary>Leaves the game. Stops Play Mode in the editor.</summary>
        void Quit();
    }

    /// <summary>
    /// The title screen: the wordmark over the empty room, and PLAY,
    /// SETTINGS, QUIT (GUI increment J).
    /// </summary>
    /// <remarks>
    /// <b>No card.</b> The wordmark and the buttons sit on a light scrim, so
    /// the four felt tables and the lit vault read through: the room is the
    /// backdrop, the way ART_DIRECTION §6.1 wants the board to be a place.
    /// A warm glow sits behind the wordmark.
    ///
    /// <b>The only way out of the game.</b> In-match menus return here
    /// (MAIN MENU); QUIT lives on this screen alone, and asks twice.
    ///
    /// Enter plays; Esc goes back from settings. Settings are the same rows as
    /// the pause menu's and are remembered between sessions.
    /// </remarks>
    public sealed class TitleScreen : ModalCard
    {
        private enum Page { Main, Settings }

        private ITitleHost _host;
        private Page _page;
        private bool _quitArmed;

        protected override float CardWidth => 520f;
        protected override float ScrimAlpha => 0.45f;
        protected override bool Framed => false;

        public void Bind(RectTransform canvasRect, ITitleHost host)
        {
            _host = host;

            if (!IsBuilt)
            {
                BuildOnce(canvasRect, "title_screen");
                AddGlow();
            }

            if (IsOpen) Close();
        }

        public void Open()
        {
            _page = Page.Main;
            _quitArmed = false;
            Show();
        }

        /// <summary>Enter: play, from the main page.</summary>
        public void Confirm()
        {
            if (IsOpen && _page == Page.Main) Play();
        }

        /// <summary>Esc: settings go back; the main page disarms QUIT.</summary>
        public void Back()
        {
            if (!IsOpen) return;

            if (_page == Page.Settings) _page = Page.Main;
            _quitArmed = false;
            Rebuild();
        }

        private void Play()
        {
            Close();
            _host.Play();
        }

        /// <summary>A warm light behind the wordmark, under the card.</summary>
        private void AddGlow()
        {
            var glow = UiKit.Rect("title_glow", Root);
            glow.SetSiblingIndex(0);
            glow.anchorMin = glow.anchorMax = new Vector2(0.5f, 0.5f);
            glow.sizeDelta = new Vector2(1100f, 760f);
            glow.anchoredPosition = new Vector2(0f, 120f);

            var image = UiKit.Fill(glow, UiTheme.WithAlpha(UiTheme.GoldBright, 0.16f));
            image.sprite = DecoSprites.Glow;
        }

        protected override void Compose()
        {
            Wordmark();

            if (_page == Page.Main) MainPage();
            else SettingsPage();
        }

        private void Wordmark()
        {
            var nona = UiKit.Caption(Slot("wordmark", 112f), "NONA", 108f, UiTheme.GoldBright,
                TextAlignmentOptions.Center);
            nona.fontStyle = FontStyles.Bold;
            nona.characterSpacing = 28f;
            nona.overflowMode = TextOverflowModes.Overflow;

            // ROYALE between two rules, each ending in a diamond.
            var line = Slot("royale", 40f);
            var row = UiKit.Row(line, 14f);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandHeight = false;

            Rule(line);
            var royale = UiKit.Label(line, "ROYALE", 30f, UiTheme.Gold, TextAlignmentOptions.Center, bold: true);
            royale.characterSpacing = 42f;
            royale.overflowMode = TextOverflowModes.Overflow;
            UiKit.Fixed(royale, 250f, 40f);
            Rule(line);

            var tagline = UiKit.Caption(Slot("tagline", 26f), "Nine operators. One vault.",
                UiTheme.FontBody, UiTheme.TextDim, TextAlignmentOptions.Center);
            tagline.fontStyle = FontStyles.Italic;

            Gap(26f);
        }

        /// <summary>A short gold rule with a diamond at its outer end.</summary>
        private static void Rule(Transform parent)
        {
            var box = UiKit.Rect("rule", parent);
            UiKit.Fixed(box, 70f, 16f);

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

        private void MainPage()
        {
            Choice("PLAY", "Enter", Play, UiTheme.CyanDeep, UiTheme.Cyan);
            Choice("SETTINGS", "", () => { _page = Page.Settings; _quitArmed = false; });

            if (_quitArmed)
            {
                Choice("CONFIRM QUIT", "", () => _host.Quit(), UiTheme.GoldDeep, UiTheme.Threat);
                Note("Leaves the game.", UiTheme.Threat);
            }
            else
            {
                Choice("QUIT", "", () => _quitArmed = true);
            }

            Gap(18f);
            Note("Prototype build · pieces and board drawn in code", UiTheme.TextOff);
        }

        private void SettingsPage()
        {
            Heading("Settings");
            SettingsRows.Build(ColumnSlot, _host, Rebuild);
            Note("Remembered between sessions.", UiTheme.TextOff);
            Gap(6f);
            Choice("BACK", "Esc", Back);
        }
    }
}
