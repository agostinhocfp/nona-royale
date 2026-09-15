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
    /// <b>Every state is an engine answer</b> (PRESENTATION §1): the phase,
    /// <c>MustSpendRoll</c>, <c>CanRollAgain</c> and the unspent dice. Space
    /// and E still work, and the button shows its key.
    ///
    /// Only the button catches the pointer.
    /// </remarks>
    public sealed class TurnButton : MonoBehaviour
    {
        private const float ButtonWidth = 210f;
        private const float ButtonHeight = 62f;
        private const float Gap = 14f;

        private IControlPanelHost _host;
        private RectTransform _area;
        private RectTransform _slot;
        private string _shown;

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
                _slot.anchoredPosition = new Vector2(-Gap, Gap);
                _slot.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            }

            _shown = null;
        }

        /// <summary>Keeps the button in the free board area's bottom-right corner.</summary>
        public void SetArea(float left, float right, float top, float bottom)
        {
            if (_area == null) return;

            _area.offsetMin = new Vector2(left, bottom);
            _area.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Redraws from the engine. Cheap when nothing changed.</summary>
        public void Refresh(GameEngine engine)
        {
            if (_slot == null || engine == null) return;

            var dice = engine.UnspentDice;
            string key = $"{engine.Phase}|{engine.MatchOver}|{engine.MustSpendRoll}|{engine.CanRollAgain}|{string.Join(",", dice)}";
            if (key == _shown) return;
            _shown = key;

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

            if (engine.Phase == TurnPhase.AwaitingRoll || (engine.CanRollAgain && dice.Count == 0))
            {
                label = engine.Phase == TurnPhase.AwaitingRoll ? "ROLL" : "ROLL AGAIN";
                hint = "Space";
                enabled = true;
                pulse = true;

                // Rolling is the turn's ritual, not a live state: gold.
                tint = UiTheme.GoldDeep;
                accent = UiTheme.GoldBright;
            }
            else if (engine.Phase == TurnPhase.Action && engine.MustSpendRoll)
            {
                label = "MOVE FIRST";
                hint = $"{string.Join(" + ", dice)} left";
                enabled = false;
                pulse = false;
                tint = UiTheme.ButtonFill;
                accent = UiTheme.Line;
            }
            else
            {
                label = "END TURN";
                hint = "E";
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

            var title = UiKit.Label(rect, label, 22f, enabled ? UiTheme.Text : UiTheme.TextDim, TextAlignmentOptions.Center, bold: true);
            title.characterSpacing = UiTheme.HeadingSpacing * 0.5f;
            UiKit.Label(rect, hint, 13f, enabled ? accent : UiTheme.TextOff, TextAlignmentOptions.Center);

            // The showpiece control gets the panel's corner fans.
            UiKit.CornerFans(rect, UiTheme.WithAlpha(enabled ? accent : UiTheme.Line, 0.45f));

            if (pulse) UiKit.Pulse(button, accent);
        }
    }
}
