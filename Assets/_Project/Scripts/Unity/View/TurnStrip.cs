// Assets/_Project/Scripts/Unity/View/TurnStrip.cs
using System.Text;
using NonaRoyale.Core;
using NonaRoyale.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The top bar: whose turn it is, their energy against the cap, the round,
    /// what the turn is waiting for, and the key legend.
    /// </summary>
    /// <remarks>
    /// PRESENTATION §2 puts "whose turn, and their energy" in the always-visible
    /// table. This started as a one-line strip centred over the board and
    /// became a full-width bar in GUI increment F. The name stayed, to keep the
    /// history readable.
    ///
    /// <b>Every value comes from the engine</b> (PRESENTATION §1): the cap is
    /// <c>GameEngine.EnergyCap</c>, the round is <c>GameEngine.Round</c>, and
    /// the prompt reports only the phase, the unspent dice,
    /// <c>MustSpendRoll</c> and <c>CanRollAgain</c>. It never works out what a
    /// die could do.
    ///
    /// <b>It owns the top edge.</b> <see cref="ReservedHeight"/> is what
    /// FrameCamera keeps clear, so the board is never drawn under it. The
    /// height is fixed, so the reservation is known before the first layout
    /// pass runs.
    ///
    /// Nothing here is a raycast target (ADR-0008 consequence 9) except the
    /// MENU button at the right end (increment H).
    /// </remarks>
    public sealed class TurnStrip : MonoBehaviour
    {
        private const float Height = 56f;

        /// <summary>Canvas units, at 1080p, that the bar claims from the top edge.</summary>
        public static float ReservedHeight => Height;

        private RectTransform _rect;
        private Image _accent;
        private TMP_Text _seat;
        private TMP_Text _energy;
        private TMP_Text _round;
        private TMP_Text _prompt;
        private RectTransform _pips;
        private readonly Image[] _pipImages = new Image[MaxPips];
        private string _shown;
        private System.Action _menuRequested;

        private const int MaxPips = 20;

        /// <summary>
        /// Builds the bar under the HUD canvas. Safe to call on every NewMatch:
        /// the bar tracks no piece, so it survives a reseed.
        /// </summary>
        /// <param name="menuRequested">Called by the MENU button at the bar's right end.</param>
        public void Bind(RectTransform canvasRect, System.Action menuRequested = null)
        {
            _menuRequested = menuRequested;

            if (_rect != null)
            {
                _shown = null;
                return;
            }

            _rect = UiKit.Rect("top_bar", canvasRect);
            _rect.anchorMin = new Vector2(0f, 1f);
            _rect.anchorMax = new Vector2(1f, 1f);
            _rect.pivot = new Vector2(0.5f, 1f);
            _rect.sizeDelta = new Vector2(0f, Height);
            _rect.anchoredPosition = Vector2.zero;
            UiKit.Dock(_rect, false, RectTransform.Edge.Bottom);

            // The bottom 6 units hold the double rule.
            var row = UiKit.Row(_rect, 16f);
            row.padding = new RectOffset(0, 18, 0, 8);
            row.childForceExpandHeight = true;

            // A block of seat colour on the left edge, readable from across the table.
            var accent = UiKit.Rect("seat_colour", _rect);
            _accent = UiKit.Fill(accent, Color.gray);
            UiKit.Fixed(accent, 10f);

            _seat = UiKit.Label(_rect, "", UiTheme.FontTitle, bold: true);
            UiKit.Fixed(_seat, 230f);

            var energyBox = UiKit.Rect("energy", _rect);
            var energyRow = UiKit.Row(energyBox, 8f);
            energyRow.childForceExpandHeight = false;
            energyRow.childAlignment = TextAnchor.MiddleLeft;
            UiKit.Fixed(energyBox, 330f);

            UiKit.Heading(energyBox, "Energy");

            // Tall diamonds (ART_DIRECTION §8 icon language): lit ones are
            // live, so cyan; empty ones are chrome, so a brass outline.
            _pips = UiKit.Rect("pips", energyBox);
            var pipRow = UiKit.Row(_pips, 2f);
            pipRow.childAlignment = TextAnchor.MiddleLeft;

            for (int i = 0; i < MaxPips; i++)
                _pipImages[i] = UiKit.Diamond(_pips, UiTheme.Line, 11f, 17f, outline: true);

            _energy = UiKit.Label(energyBox, "", UiTheme.FontBody, bold: true);

            _round = UiKit.Label(_rect, "", UiTheme.FontBody, UiTheme.Heading);
            _round.characterSpacing = UiTheme.HeadingSpacing * 0.5f;
            UiKit.Fixed(_round, 120f);

            _prompt = UiKit.Label(_rect, "", UiTheme.FontLarge, UiTheme.GoldBright, TextAlignmentOptions.Center);
            UiKit.Size(_prompt, flexibleWidth: 1f);

            UiKit.Label(_rect, KeyLegend(), UiTheme.FontSmall, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);

            // The pause menu's button (GUI increment H), for pointer and touch
            // players. Held in a fixed box so the bar's height does not
            // stretch it. The only thing on the bar that catches the pointer.
            var menuBox = UiKit.Rect("menu", _rect);
            UiKit.Fixed(menuBox, 104f);

            var menu = UiKit.Button(menuBox, $"MENU  <size=70%><color=#{UiTheme.Hex(UiTheme.Gold)}>Esc</color></size>",
                () => _menuRequested?.Invoke(), size: UiTheme.FontSmall);
            var menuRect = (RectTransform)menu.transform;
            menuRect.anchorMin = new Vector2(0f, 0.5f);
            menuRect.anchorMax = new Vector2(1f, 0.5f);
            menuRect.pivot = new Vector2(0.5f, 0.5f);
            menuRect.sizeDelta = new Vector2(0f, 34f);
            menuRect.anchoredPosition = new Vector2(0f, 3f);

            _shown = null;
        }

        /// <summary>
        /// Rewrites the bar from the engine. Called after every batch of
        /// events; text is set only when something changed, because TMP lays
        /// text out again on every assignment.
        /// </summary>
        public void Refresh(GameEngine engine)
        {
            if (_rect == null || engine == null) return;

            string key = engine.MatchOver
                ? $"over|{engine.Winner}|{engine.Round}"
                : $"{engine.CurrentPlayer.Color}|{engine.CurrentPlayer.Energy}|{engine.EnergyCap}|{engine.Round}|{Prompt(engine)}";

            if (key == _shown) return;
            _shown = key;

            _round.text = $"ROUND <b><color=#{UiTheme.Hex(UiTheme.GoldBright)}>{engine.Round}</color></b>";

            if (engine.MatchOver)
            {
                var winner = engine.Winner;
                var colour = winner.HasValue ? BoardLayout.ColourOf(winner.Value) : UiTheme.SeatNone;

                _accent.color = colour;
                _seat.text = winner.HasValue
                    ? $"<color=#{UiTheme.Hex(UiTheme.Readable(colour))}>{winner.Value.ToString().ToUpperInvariant()}</color> WINS"
                    : "MATCH OVER";
                _energy.text = "";
                _prompt.text = "Match over — press <b>Esc</b> for a new match";
                SetPips(0, 0);
                return;
            }

            var seat = engine.CurrentPlayer;
            var seatColour = BoardLayout.ColourOf(seat.Color);

            _accent.color = seatColour;
            _seat.text = $"<color=#{UiTheme.Hex(UiTheme.Readable(seatColour))}>{seat.Color.ToString().ToUpperInvariant()}</color> <size=70%><color=#{UiTheme.Hex(UiTheme.TextDim)}>to play</color></size>";
            _energy.text = $"{seat.Energy}<color=#{UiTheme.Hex(UiTheme.TextDim)}>/{engine.EnergyCap}</color>";
            _prompt.text = Prompt(engine);

            SetPips(seat.Energy, engine.EnergyCap);
        }

        /// <summary>One pip per point of the cap, lit up to the pool.</summary>
        private void SetPips(int energy, int cap)
        {
            int shown = Mathf.Min(cap, MaxPips);

            for (int i = 0; i < MaxPips; i++)
            {
                var pip = _pipImages[i];
                bool lit = i < energy;

                pip.gameObject.SetActive(i < shown);
                pip.sprite = lit ? DecoSprites.Diamond : DecoSprites.DiamondOutline;
                pip.color = lit ? UiTheme.Cyan : UiTheme.Line;
            }
        }

        /// <summary>The key legend, keys in gold.</summary>
        private static string KeyLegend()
        {
            string gold = UiTheme.Hex(UiTheme.Gold);
            string Key(string key, string what) => $"<color=#{gold}><b>{key}</b></color> {what}";

            return string.Join("   ",
                Key("Space", "roll"), Key("E", "end"), Key("1–3", "ability"), Key("Enter", "cast"),
                Key("Esc", "back / menu"), Key("L", "log"));
        }

        private static string Prompt(GameEngine engine)
        {
            var dice = engine.UnspentDice;

            switch (engine.Phase)
            {
                case TurnPhase.AwaitingRoll:
                    return "Roll the dice";

                case TurnPhase.Action:
                    if (dice.Count == 0)
                        return engine.CanRollAgain ? "Doubles — roll again" : "Dice spent — cast, or press <b>E</b> to end the turn";

                    // MustSpendRoll is the engine's answer to "is there a legal
                    // move for these dice". While dice are left, false means
                    // none of them can be spent.
                    return engine.MustSpendRoll
                        ? $"Move <b>{Join(dice)}</b> — click a piece, then where it lands"
                        : $"No legal move for {Join(dice)} — press <b>E</b> to end the turn";

                default:
                    return "";
            }
        }

        private static string Join(System.Collections.Generic.IReadOnlyList<int> dice)
        {
            var text = new StringBuilder();

            for (int i = 0; i < dice.Count; i++)
            {
                if (i > 0) text.Append(" + ");
                text.Append(dice[i]);
            }

            return text.ToString();
        }
    }
}
