// Assets/_Project/Scripts/Unity/View/TurnButton.cs
using NonaRoyale.Core;
using NonaRoyale.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The one turn button, at the board's bottom-right corner: ROLL, then
    /// MOVE FIRST while dice are owed, then END TURN (GUI increment F2).
    /// </summary>
    /// <remarks>
    /// <b>The card-game end-turn button.</b> "How do I end a turn?" had no good
    /// answer while Roll and End turn sat side by side in the tray, one of them
    /// always greyed out without saying why. A single button in a fixed place
    /// that always names the next step answers it. It lights up and breathes
    /// when pressing it is the next step. While movement is owed, it says so
    /// and shows the dice left.
    ///
    /// <b>Upright it takes the screen's bottom-right corner instead of the
    /// board's</b> (MOBILE.md, M3). A phone's board is framed edge to edge, so
    /// a button floating over its corner would sit on playable cells; down in
    /// the corner of the screen it lands in the slot
    /// <see cref="ActionTray"/> leaves beside Cast, which is also where a
    /// thumb already is.
    ///
    /// <b>Every state is an engine answer</b> (PRESENTATION §1): the phase,
    /// <c>DiceOwed</c>, <c>MustDeploy</c>, <c>CanMove</c>, <c>CanRollAgain</c>
    /// and the unspent dice. Space and E still work, and the button shows its
    /// key — unless there is no keyboard to press it on (M4).
    ///
    /// <b>ROLL counts down</b> while the roll clock runs (<c>TurnPacer</c>,
    /// 2026-09-25). Only the hint's text changes each second; the button is
    /// not rebuilt, so its pulse does not restart.
    ///
    /// Only the button catches the pointer.
    /// </remarks>
    public sealed class TurnButton : MonoBehaviour
    {
        private const float ButtonWidth = 210f;
        private const float ButtonHeight = 62f;
        private const float Gap = 14f;

        /// <summary>The slot the tray keeps open for it upright (M3).</summary>
        public const float UprightWidth = 168f;

        private const float UprightHeight = 60f;
        private const float UprightGap = 12f;

        private IControlPanelHost _host;
        private RectTransform _area;
        private RectTransform _slot;
        private string _shown;
        private bool _placedPortrait;

        // The ROLL hint, kept so the countdown can rewrite it in place.
        private TMP_Text _rollHint;
        private string _rollHintBase;
        private int _countdownShown = -1;

        public void Bind(RectTransform canvasRect, IControlPanelHost host)
        {
            _host = host;

            if (_area == null)
            {
                _area = UiKit.Rect("turn_button_area", canvasRect);
                _area.anchorMin = Vector2.zero;
                _area.anchorMax = Vector2.one;

                _slot = UiKit.Rect("turn_button", _area);
                _slot.anchorMin = new Vector2(1f, 0f);
                _slot.anchorMax = new Vector2(1f, 0f);
                _slot.pivot = new Vector2(1f, 0f);
            }

            Place();
            _shown = null;
        }

        /// <summary>
        /// Keeps the button in the free board area's bottom-right corner, or,
        /// upright, in the screen's own.
        /// </summary>
        public void SetArea(float left, float right, float top, float bottom)
        {
            if (_area == null) return;

            // Upright the button belongs to the tray's bottom row, not to the
            // board, so the board's insets do not move it.
            if (ScreenLayout.IsPortrait)
            {
                _area.offsetMin = Vector2.zero;
                _area.offsetMax = Vector2.zero;
            }
            else
            {
                _area.offsetMin = new Vector2(left, bottom);
                _area.offsetMax = new Vector2(-right, -top);
            }

            Place();
        }

        /// <summary>Sizes and seats the slot for the arrangement in force.</summary>
        private void Place()
        {
            if (_slot == null) return;

            bool portrait = ScreenLayout.IsPortrait;

            _slot.anchoredPosition = portrait
                ? new Vector2(-UprightGap, UprightGap)
                : new Vector2(-Gap, Gap);
            _slot.sizeDelta = portrait
                ? new Vector2(UprightWidth, UprightHeight)
                : new Vector2(ButtonWidth, ButtonHeight);

            if (_placedPortrait != portrait)
            {
                _placedPortrait = portrait;
                _shown = null; // the label sizes differ, so it is rebuilt
            }
        }

        private void LateUpdate()
        {
            if (_placedPortrait != ScreenLayout.IsPortrait) Place();
        }

        /// <summary>
        /// The roll clock's seconds left, or null when it is not running. Shown
        /// on the ROLL hint as whole seconds, rounded up.
        /// </summary>
        public void SetCountdown(float? secondsLeft)
        {
            int whole = secondsLeft.HasValue ? Mathf.CeilToInt(secondsLeft.Value) : -1;
            if (whole == _countdownShown) return;
            _countdownShown = whole;
            ApplyCountdown();
        }

        private void ApplyCountdown()
        {
            if (_rollHint == null) return;

            string clock = _countdownShown >= 0 ? $"{_countdownShown}s" : null;
            _rollHint.text = string.IsNullOrEmpty(_rollHintBase)
                ? clock ?? ""
                : clock == null ? _rollHintBase : $"{_rollHintBase} · {clock}";
            _rollHint.gameObject.SetActive(!string.IsNullOrEmpty(_rollHint.text));
        }

        /// <summary>Redraws from the engine. Cheap when nothing changed.</summary>
        public void Refresh(GameEngine engine)
        {
            if (_slot == null || engine == null) return;

            var dice = engine.UnspentDice;
            bool cpu = _host != null && _host.CpuTurn;
            bool portrait = ScreenLayout.IsPortrait;
            string key = $"{engine.Phase}|{engine.MatchOver}|{engine.MustSpendRoll}|{engine.DiceOwed}|{engine.MustDeploy}|{engine.CanRollAgain}|{string.Join(",", dice)}|{cpu}|{(cpu ? engine.CurrentPlayer.Color.ToString() : "")}|{portrait}|{ScreenLayout.Touch}";
            if (key == _shown) return;
            _shown = key;
            _rollHint = null;
            _rollHintBase = null;

            for (int i = _slot.childCount - 1; i >= 0; i--)
            {
                var child = _slot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            if (engine.MatchOver)
            {
                _slot.gameObject.SetActive(false);
                return;
            }

            _slot.gameObject.SetActive(true);

            string label;
            string hint;
            bool enabled;
            bool pulse;
            Color tint;
            Color accent;

            if (cpu)
            {
                // A CPU seat is playing: the button names it and does nothing (BOT2).
                var seat = engine.CurrentPlayer.Color;
                label = portrait
                    ? $"{seat.ToString().ToUpperInvariant()} THINKING"
                    : $"{seat.ToString().ToUpperInvariant()} IS THINKING";
                hint = ScreenLayout.Key("hold Space to hurry");
                enabled = false;
                pulse = false;
                tint = UiTheme.ButtonFill;
                accent = UiTheme.Readable(BoardLayout.ColourOf(seat));
            }
            else if (engine.Phase == TurnPhase.AwaitingRoll || (engine.CanRollAgain && !engine.DiceOwed))
            {
                label = engine.Phase == TurnPhase.AwaitingRoll ? "ROLL" : "ROLL AGAIN";
                hint = ScreenLayout.Key("Space");
                enabled = true;
                pulse = true;

                // Rolling is the turn's ritual, not a live state: gold.
                tint = UiTheme.GoldDeep;
                accent = UiTheme.GoldBright;
            }
            else if (engine.Phase == TurnPhase.Action && engine.MustSpendRoll)
            {
                // Spawn or move (2026-09-25): a held 6 with nothing to move owes a deploy.
                label = engine.MustDeploy && !engine.CanMove ? "DEPLOY FIRST" : "MOVE FIRST";
                hint = $"{string.Join(" + ", dice)} left";
                enabled = false;
                pulse = false;
                tint = UiTheme.ButtonFill;
                accent = UiTheme.Line;
            }
            else
            {
                label = "END TURN";
                hint = ScreenLayout.Key("E");
                enabled = true;
                pulse = true;

                // Ending is the live next step: the cool register (ART_DIRECTION §8).
                tint = UiTheme.CyanDeep;
                accent = UiTheme.Cyan;
            }

            System.Action press = label.StartsWith("ROLL") ? (System.Action)_host.Roll : _host.EndTurn;

            var button = UiKit.Button(_slot, "", press, interactable: enabled, tint: tint, edge: accent);
            var rect = (RectTransform)button.transform;
            UiKit.Stretch(rect);

            var column = UiKit.Column(rect, 0f, 6);
            column.childAlignment = TextAnchor.MiddleCenter;

            float titleSize = portrait ? (cpu ? 14f : 19f) : (cpu ? 17f : 22f);

            var title = UiKit.Label(rect, label, titleSize,
                enabled ? UiTheme.Text : cpu ? accent : UiTheme.TextDim, TextAlignmentOptions.Center, bold: true);
            title.characterSpacing = UiTheme.HeadingSpacing * 0.5f;

            // The first roll's hint is always made, even empty, so the roll
            // clock has somewhere to count down on a phone too.
            bool firstRoll = !cpu && engine.Phase == TurnPhase.AwaitingRoll;

            if (!string.IsNullOrEmpty(hint) || firstRoll)
            {
                var hintLabel = UiKit.Label(rect, hint ?? "", 13f, enabled ? accent : UiTheme.TextOff,
                    TextAlignmentOptions.Center);

                if (firstRoll)
                {
                    _rollHint = hintLabel;
                    _rollHintBase = hint;
                    ApplyCountdown();
                }
            }

            // The showpiece control floats over the board, so it gets a floating
            // card's fans: small and dim (G3), and none while it waits.
            if (enabled) UiKit.CornerFans(rect, UiTheme.WithAlpha(accent, UiTheme.FanAlpha));

            if (pulse) UiKit.Pulse(button, accent);
        }
    }
}
