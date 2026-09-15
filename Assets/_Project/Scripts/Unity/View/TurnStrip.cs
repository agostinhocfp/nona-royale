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
    /// Nothing here is a raycast target (ADR-0008 consequence 9).
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

        private const int MaxPips = 20;

        /// <summary>
        /// Builds the bar under the HUD canvas. Safe to call on every NewMatch:
        /// the bar tracks no piece, so it survives a reseed.
        /// </summary>
        public void Bind(RectTransform canvasRect)
        {
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
            UiKit.Frame(_rect, UiKit.Panel, false, RectTransform.Edge.Bottom);

            var row = UiKit.Row(_rect, 16f);
            row.padding = new RectOffset(0, 18, 0, 2);
            row.childForceExpandHeight = true;

            // A block of seat colour on the left edge, readable from across the table.
            var accent = UiKit.Rect("seat_colour", _rect);
            _accent = UiKit.Fill(accent, Color.gray);
            UiKit.Fixed(accent, 12f);

            _seat = UiKit.Label(_rect, "", UiKit.FontTitle, bold: true);
            UiKit.Fixed(_seat, 230f);

            var energyBox = UiKit.Rect("energy", _rect);
            var energyRow = UiKit.Row(energyBox, 8f);
            energyRow.childForceExpandHeight = false;
            energyRow.childAlignment = TextAnchor.MiddleLeft;
            UiKit.Fixed(energyBox, 330f);

            UiKit.Label(energyBox, "ENERGY", UiKit.FontSmall, UiKit.TextDim, bold: true);

            _pips = UiKit.Rect("pips", energyBox);
            var pipRow = UiKit.Row(_pips, 3f);
            pipRow.childAlignment = TextAnchor.MiddleLeft;

            for (int i = 0; i < MaxPips; i++)
            {
                var pip = UiKit.Rect($"pip_{i}", _pips);
                _pipImages[i] = UiKit.Fill(pip, UiKit.Track);
                UiKit.Size(pip, 10f, 18f);
            }

            _energy = UiKit.Label(energyBox, "", UiKit.FontBody, bold: true);

            _round = UiKit.Label(_rect, "", UiKit.FontBody, UiKit.TextDim);
            UiKit.Fixed(_round, 110f);

            _prompt = UiKit.Label(_rect, "", UiKit.FontLarge, UiKit.GoldBright, TextAlignmentOptions.Center);
            UiKit.Size(_prompt, flexibleWidth: 1f);

            UiKit.Label(_rect,
                "<b>Space</b> roll  <b>E</b> end  <b>1–3</b> ability  <b>Enter</b> cast  <b>Esc</b> back  <b>L</b> log  <b>H</b> health  <b>Tab</b> dev",
                UiKit.FontSmall, UiKit.TextDim, TextAlignmentOptions.MidlineRight);

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

            _round.text = $"Round <b>{engine.Round}</b>";

            if (engine.MatchOver)
            {
                var winner = engine.Winner;
                var colour = winner.HasValue ? BoardLayout.ColourOf(winner.Value) : Color.gray;

                _accent.color = colour;
                _seat.text = winner.HasValue
                    ? $"<color=#{UiKit.Hex(UiKit.Readable(colour))}>{winner.Value.ToString().ToUpperInvariant()}</color> WINS"
                    : "MATCH OVER";
                _energy.text = "";
                _prompt.text = "Match over — start a new one from the dev panel (Tab)";
                SetPips(0, 0);
                return;
            }

            var seat = engine.CurrentPlayer;
            var seatColour = BoardLayout.ColourOf(seat.Color);

            _accent.color = seatColour;
            _seat.text = $"<color=#{UiKit.Hex(UiKit.Readable(seatColour))}>{seat.Color.ToString().ToUpperInvariant()}</color> <size=70%><color=#{UiKit.Hex(UiKit.TextDim)}>to play</color></size>";
            _energy.text = $"{seat.Energy}<color=#{UiKit.Hex(UiKit.TextDim)}>/{engine.EnergyCap}</color>";
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
                pip.gameObject.SetActive(i < shown);
                pip.color = i < energy ? UiKit.Cyan : UiKit.Track;
            }
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
