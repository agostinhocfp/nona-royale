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
    /// and what the turn is waiting for.
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
    /// <b>Two shapes</b> (MOBILE.md, M3). Wide, it is one row: seat, energy,
    /// round, prompt, MENU, and 56 units tall. Upright there is not the width
    /// for five things side by side, so it becomes two rows — the state on top,
    /// the turn's one instruction under it — and 96 tall. The instruction is
    /// the line a player reads every turn, so it gets its own row rather than
    /// the leftovers of a shared one.
    ///
    /// <b>It owns the top edge.</b> <see cref="ReservedHeight"/> is what
    /// FrameCamera keeps clear, so the board is never drawn under it. The
    /// height is fixed per arrangement, so the reservation is known before the
    /// first layout pass runs.
    ///
    /// Nothing here is a raycast target (ADR-0008 consequence 9) except the
    /// MENU button at the end (increment H), which is sized for a thumb rather
    /// than a pointer when there is no mouse.
    /// </remarks>
    public sealed class TurnStrip : MonoBehaviour
    {
        private const float WideHeight = 56f;
        private const float UprightHeight = 96f;

        /// <summary>Canvas units, at the reference resolution, that the bar claims from the top edge.</summary>
        public static float ReservedHeight => ScreenLayout.Pick(WideHeight, UprightHeight);

        private RectTransform _rect;
        private Image _accent;
        private TMP_Text _seat;
        private TMP_Text _energy;

        // The playing seat's debt chip (§3.3), and what it last showed for
        // whom, so a figure pops only when it changed for the same seat.
        private TMP_Text _debt;
        private NonaRoyale.Core.Board.PlayerColor? _debtSeat;
        private int _debtShown;
        private TMP_Text _round;
        private TMP_Text _prompt;
        private RectTransform _pips;
        private readonly Image[] _pipImages = new Image[MaxPips];
        private string _shown;
        private System.Action _menuRequested;
        private RectTransform _canvas;
        private bool _builtPortrait;

        /// <summary>The pool last shown, so newly lit pips can cascade (U2). -1 snaps without animating.</summary>
        private int _pipBaseline = -1;

        private const int MaxPips = 20;

        /// <summary>
        /// Builds the bar under the HUD canvas. Safe to call on every NewMatch:
        /// the bar tracks no piece, so it survives a reseed.
        /// </summary>
        /// <param name="menuRequested">Called by the MENU button at the bar's end.</param>
        public void Bind(RectTransform canvasRect, System.Action menuRequested = null)
        {
            _menuRequested = menuRequested;
            _canvas = canvasRect;

            if (_rect != null)
            {
                _shown = null;
                _pipBaseline = -1;
                return;
            }

            Build();
        }

        /// <summary>
        /// Rebuilds the bar in the other arrangement when the screen turns
        /// (M3). Both shapes are built from the same fields, so everything that
        /// holds a reference to the bar keeps working across the swap; only
        /// <see cref="_shown"/> has to be dropped, because the new labels are
        /// empty.
        /// </summary>
        private void LateUpdate()
        {
            if (_rect == null || _builtPortrait == ScreenLayout.IsPortrait) return;

            var old = _rect.gameObject;
            _rect = null;
            old.SetActive(false);
            Destroy(old);

            Build();
        }

        private void Build()
        {
            _builtPortrait = ScreenLayout.IsPortrait;

            _rect = UiKit.Rect("top_bar", _canvas);
            _rect.anchorMin = new Vector2(0f, 1f);
            _rect.anchorMax = new Vector2(1f, 1f);
            _rect.pivot = new Vector2(0.5f, 1f);
            _rect.sizeDelta = new Vector2(0f, ReservedHeight);
            _rect.anchoredPosition = Vector2.zero;
            UiKit.Dock(_rect, false, RectTransform.Edge.Bottom);

            if (_builtPortrait) BuildUpright();
            else BuildWide();

            _shown = null;
            _pipBaseline = -1;
        }

        // ── Wide ─────────────────────────────────────────────────────────

        private void BuildWide()
        {
            // The bottom 6 units hold the rule.
            var row = UiKit.Row(_rect, 16f);
            row.padding = new RectOffset(0, 18, 0, 8);
            row.childForceExpandHeight = true;

            SeatAccent(_rect, 10f);

            _seat = UiKit.Label(_rect, "", UiTheme.FontTitle, bold: true);
            UiKit.Fixed(_seat, 230f);

            var energyBox = UiKit.Rect("energy", _rect);
            var energyRow = UiKit.Row(energyBox, 8f);
            energyRow.childForceExpandHeight = false;
            energyRow.childAlignment = TextAnchor.MiddleLeft;
            UiKit.Fixed(energyBox, 330f);

            UiKit.Heading(energyBox, "Energy");
            BuildPips(energyBox, 11f, 17f);
            _energy = UiKit.Label(energyBox, "", UiTheme.FontBody, bold: true);

            // The playing seat's debt, beside its pool (§3.3). Hidden at zero,
            // so it costs the prompt no width on a turn without one.
            _debt = DebtMark.Chip(_rect, UiTheme.FontBody, 26f);

            _round = UiKit.Label(_rect, "", UiTheme.FontBody, UiTheme.Heading);
            _round.characterSpacing = UiTheme.HeadingSpacing * 0.5f;
            UiKit.Fixed(_round, 120f);

            // The turn's one instruction, and the widest thing on the bar now
            // that the key legend has gone (H4).
            _prompt = UiKit.Label(_rect, "", UiTheme.FontLarge, UiTheme.GoldBright, TextAlignmentOptions.Center);
            UiKit.Size(_prompt, flexibleWidth: 1f);

            MenuButton(_rect, 104f, 34f);
        }

        // ── Upright ──────────────────────────────────────────────────────

        /// <summary>
        /// Two rows: the seat and its pool over the turn's instruction (M3).
        /// </summary>
        /// <remarks>
        /// The round loses its own slot and rides on the seat line as a short
        /// tag — at 480 units of width a 120-unit box for "ROUND 4" is a
        /// quarter of the bar for a number that changes every four turns.
        /// </remarks>
        private void BuildUpright()
        {
            var column = UiKit.Column(_rect, 2f);
            column.padding = new RectOffset(0, 0, 0, 8);
            column.childForceExpandHeight = false;

            var top = UiKit.Rect("state", _rect);
            UiKit.Size(top, height: 46f);
            var row = UiKit.Row(top, 8f);
            row.padding = new RectOffset(0, 10, 0, 0);
            row.childForceExpandHeight = true;

            SeatAccent(top, 8f);

            _seat = UiKit.Label(top, "", UiTheme.FontLarge, bold: true);
            UiKit.Size(_seat, flexibleWidth: 1f);

            var energyBox = UiKit.Rect("energy", top);
            var energyRow = UiKit.Row(energyBox, 5f);
            energyRow.childForceExpandHeight = false;
            energyRow.childAlignment = TextAnchor.MiddleRight;
            UiKit.Size(energyBox, flexibleWidth: 0f);

            // Shorter pips than the wide bar's, and no "ENERGY" heading: the
            // cyan diamonds beside a number read as a pool on their own.
            BuildPips(energyBox, 8f, 13f);
            _energy = UiKit.Label(energyBox, "", UiTheme.FontSmall, bold: true);
            _debt = DebtMark.Chip(energyBox, UiTheme.FontSmall, 20f);

            _round = UiKit.Label(top, "", 13f, UiTheme.Heading, TextAlignmentOptions.MidlineRight);
            _round.characterSpacing = UiTheme.HeadingSpacing * 0.4f;
            UiKit.Fixed(_round, 54f);

            // 44 units square: a thumb target, not a pointer target.
            MenuButton(top, 68f, 44f);

            _prompt = UiKit.Label(_rect, "", UiTheme.FontBody, UiTheme.GoldBright,
                TextAlignmentOptions.Center, wrap: true);
            UiKit.Size(_prompt, height: 36f);
        }

        // ── Shared pieces ────────────────────────────────────────────────

        /// <summary>A block of seat colour on the leading edge, readable from across the table.</summary>
        private void SeatAccent(RectTransform parent, float width)
        {
            var accent = UiKit.Rect("seat_colour", parent);
            _accent = UiKit.Fill(accent, Color.gray);
            UiKit.Fixed(accent, width);
        }

        /// <summary>
        /// Tall diamonds (ART_DIRECTION §8 icon language): lit ones are live, so
        /// cyan; empty ones are chrome, so a brass outline.
        /// </summary>
        private void BuildPips(RectTransform parent, float width, float height)
        {
            _pips = UiKit.Rect("pips", parent);
            var pipRow = UiKit.Row(_pips, 2f);
            pipRow.childAlignment = TextAnchor.MiddleLeft;

            for (int i = 0; i < MaxPips; i++)
                _pipImages[i] = UiKit.Diamond(_pips, UiTheme.Line, width, height, outline: true);
        }

        /// <summary>
        /// The pause menu's button (GUI increment H), for pointer and touch
        /// players. Held in a fixed box so the bar's height does not stretch
        /// it. The only thing on the bar that catches the pointer.
        /// </summary>
        private void MenuButton(RectTransform parent, float width, float height)
        {
            var menuBox = UiKit.Rect("menu", parent);
            UiKit.Fixed(menuBox, width);

            var menu = UiKit.Button(menuBox, $"MENU{ScreenLayout.KeyMarkup($"  <size=70%><color=#{UiTheme.Hex(UiTheme.Gold)}>Esc</color></size>")}",
                () => _menuRequested?.Invoke(), size: UiTheme.FontSmall);
            var menuRect = (RectTransform)menu.transform;
            menuRect.anchorMin = new Vector2(0f, 0.5f);
            menuRect.anchorMax = new Vector2(1f, 0.5f);
            menuRect.pivot = new Vector2(0.5f, 0.5f);
            menuRect.sizeDelta = new Vector2(0f, height);
            menuRect.anchoredPosition = new Vector2(0f, ScreenLayout.IsPortrait ? 0f : 3f);
        }

        /// <summary>
        /// Rewrites the bar from the engine. Called after every batch of
        /// events; text is set only when something changed, because TMP lays
        /// text out again on every assignment.
        /// </summary>
        /// <param name="seatTag">"CPU · BRAWLER" when a CPU seat is playing, else null (BOT2).</param>
        public void Refresh(GameEngine engine, string seatTag = null)
        {
            if (_rect == null || engine == null) return;

            string key = engine.MatchOver
                ? $"over|{engine.Winner}|{engine.Round}"
                : $"{engine.CurrentPlayer.Color}|{engine.CurrentPlayer.Energy}|{engine.CurrentPlayer.Debt}|{engine.EnergyCap}|{engine.Round}|{Prompt(engine)}|{seatTag}";

            if (key == _shown) return;
            _shown = key;

            _round.text = ScreenLayout.IsPortrait
                ? $"<color=#{UiTheme.Hex(UiTheme.GoldBright)}>R{engine.Round}</color>"
                : $"ROUND <b><color=#{UiTheme.Hex(UiTheme.GoldBright)}>{engine.Round}</color></b>";

            if (engine.MatchOver)
            {
                var winner = engine.Winner;
                var colour = winner.HasValue ? BoardLayout.ColourOf(winner.Value) : UiTheme.SeatNone;

                _accent.color = colour;
                _seat.text = winner.HasValue
                    ? $"<color=#{UiTheme.Hex(UiTheme.Readable(colour))}>{winner.Value.ToString().ToUpperInvariant()}</color> WINS"
                    : "MATCH OVER";
                _energy.text = "";
                ShowDebt(null, 0);
                _prompt.text = ScreenLayout.Touch
                    ? "Match over — tap MENU for the results"
                    : "Match over — press <b>Esc</b> for the results";
                SetPips(0, 0);
                return;
            }

            var seat = engine.CurrentPlayer;
            var seatColour = BoardLayout.ColourOf(seat.Color);

            _accent.color = seatColour;
            string who = seatTag == null
                ? "to play"
                : $"<color=#{UiTheme.Hex(UiTheme.Cyan)}>({seatTag})</color>";
            _seat.text = $"<color=#{UiTheme.Hex(UiTheme.Readable(seatColour))}>{seat.Color.ToString().ToUpperInvariant()}</color> <size=70%><color=#{UiTheme.Hex(UiTheme.TextDim)}>{who}</color></size>";
            _energy.text = $"{seat.Energy}<color=#{UiTheme.Hex(UiTheme.TextDim)}>/{engine.EnergyCap}</color>";
            ShowDebt(seat.Color, seat.Debt);
            _prompt.text = seatTag == null
                ? Prompt(engine)
                : ScreenLayout.Touch
                    ? "The CPU is playing"
                    : "The CPU is playing — hold <b>Space</b> to hurry it, <b>Esc</b> to pause";

            SetPips(seat.Energy, engine.EnergyCap);
        }

        /// <summary>
        /// The playing seat's debt chip. It pops only when the same seat's
        /// figure changed — a turn changing hands is not news.
        /// </summary>
        private void ShowDebt(NonaRoyale.Core.Board.PlayerColor? seat, int debt)
        {
            int previous = seat.HasValue && seat == _debtSeat ? _debtShown : debt;
            DebtMark.Set(_debt, debt, previous);
            _debtSeat = seat;
            _debtShown = debt;
        }

        /// <summary>One pip per point of the cap, lit up to the pool. Gains cascade in (U2); losses snap.</summary>
        private void SetPips(int energy, int cap)
        {
            int shown = Mathf.Min(cap, MaxPips);
            int before = _pipBaseline;
            _pipBaseline = energy;

            for (int i = 0; i < MaxPips; i++)
            {
                var pip = _pipImages[i];
                if (pip == null) continue;

                bool lit = i < energy;

                pip.gameObject.SetActive(i < shown);
                pip.sprite = lit ? DecoSprites.Diamond : DecoSprites.DiamondOutline;
                pip.color = lit ? UiTheme.Cyan : UiTheme.Line;
            }

            if (before < 0 || energy <= before) return;

            for (int i = before; i < energy && i < shown; i++)
                if (_pipImages[i] != null) UiPopIn.On(_pipImages[i].transform, 0.05f * (i - before));
        }

        /// <summary>
        /// The turn's one instruction. Phrased for whichever pointer is in the
        /// player's hand (M4): a phone has no E key to press and nothing to
        /// click, so the same engine state is put as a tap.
        /// </summary>
        private static string Prompt(GameEngine engine)
        {
            var dice = engine.UnspentDice;
            bool touch = ScreenLayout.Touch;

            switch (engine.Phase)
            {
                case TurnPhase.AwaitingRoll:
                    return touch ? "Tap ROLL" : "Roll the dice";

                case TurnPhase.Action:
                    if (dice.Count == 0)
                        return engine.CanRollAgain
                            ? "Doubles — roll again"
                            : touch ? "Dice spent — cast, or END TURN" : "Dice spent — cast, or press <b>E</b> to end the turn";

                    // MustSpendRoll is the engine's answer to "is there a legal
                    // move for these dice". While dice are left, false means
                    // none of them can be spent.
                    if (engine.MustSpendRoll)
                        return touch
                            ? $"Move <b>{Join(dice)}</b> — tap a piece, then where it lands"
                            : $"Move <b>{Join(dice)}</b> — click a piece, then where it lands";

                    return touch
                        ? $"No legal move for {Join(dice)} — tap END TURN"
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
