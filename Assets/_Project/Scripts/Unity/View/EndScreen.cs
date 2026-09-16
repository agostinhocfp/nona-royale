// Assets/_Project/Scripts/Unity/View/EndScreen.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The end-of-match screen: who won, how long it took, and each seat's
    /// squad and tally (GUI increment I; squads since DR2).
    /// </summary>
    /// <remarks>
    /// <b>It waits a beat.</b> The winning move is still walking home when the
    /// engine reports the win, so the screen opens <see cref="Delay"/> seconds
    /// later, on unscaled time.
    ///
    /// <b>Every number is an engine answer</b> (PRESENTATION §1): operators
    /// home from <c>IsHome</c>, knockouts from <c>KnockoutsScoredBy</c>,
    /// losses from <c>OperatorsLostBy</c>, the round from <c>Round</c>. A
    /// knockout counts for the seat whose operator caused it, however the
    /// damage arrived; self-inflicted and friendly kills count for nobody.
    ///
    /// <b>VIEW BOARD hides the screen</b> so the final position can be read;
    /// Esc brings it back while the match is over. MAIN MENU (was QUIT until
    /// increment J) returns to the title; the match is over, so it does not
    /// ask.
    /// </remarks>
    public sealed class EndScreen : ModalCard
    {
        private const float Delay = 1.2f;

        private IMatchFlowHost _host;
        private float _pending = -1f;

        protected override float CardWidth => 860f;
        protected override float ScrimAlpha => 0.7f;

        public void Bind(RectTransform canvasRect, IMatchFlowHost host)
        {
            _host = host;
            BuildOnce(canvasRect, "end_screen");
            _pending = -1f;
            if (IsOpen) Close();
        }

        /// <summary>Opens after the short delay that lets the winning move land.</summary>
        public void OpenSoon() => _pending = Delay;

        public void Open()
        {
            _pending = -1f;
            if (_host?.Match != null) Show();
        }

        public override void Close()
        {
            _pending = -1f;
            base.Close();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if (_pending < 0f) return;

            _pending -= Time.unscaledDeltaTime;
            if (_pending <= 0f) Open();
        }

        protected override void Compose()
        {
            var match = _host.Match;
            var engine = match.Engine;
            var winner = engine.Winner;

            if (winner.HasValue)
            {
                var colour = UiTheme.Readable(UiTheme.Seat(winner.Value));
                Title($"{winner.Value.ToString().ToUpperInvariant()} WINS",
                    $"Round {engine.Round}  ·  seed {_host.Settings.Seed}", colour);
            }
            else
            {
                Title("Match over", $"Round {engine.Round}");
            }

            // ── The tally ──
            TableRow("header", null, null, "SEAT", "HOME", "KNOCKOUTS", "LOST", header: true);

            foreach (var player in match.Players)
            {
                int home = 0;
                foreach (var op in player.Operators) if (engine.IsHome(op)) home++;

                string cpu = _host.Settings.IsCpu(player.Color)
                    ? $" <size=65%><color=#{UiTheme.Hex(UiTheme.Cyan)}>CPU</color></size>"
                    : "";

                TableRow($"seat_{player.Color}", player.Color, player.Operators,
                    player.Color.ToString().ToUpperInvariant() + cpu,
                    $"{home}/{player.Operators.Count}",
                    engine.KnockoutsScoredBy(player.Color).ToString(),
                    engine.OperatorsLostBy(player.Color).ToString(),
                    winner: winner == player.Color);
            }

            Note("Knockouts count for the seat whose operator caused them.", UiTheme.TextNote);

            // ── Next ──
            Gap(10f);

            Choice("REMATCH", "Enter", () => _host.Rematch(), UiTheme.CyanDeep, UiTheme.Cyan);

            var row = ButtonRow("next");
            UiKit.Button(row, "NEW MATCH", () => _host.OpenSetup(), size: UiTheme.FontBody);
            UiKit.Button(row, WithKey("VIEW BOARD", "Esc"), Close, size: UiTheme.FontBody);
            UiKit.Button(row, "MAIN MENU", () => { Close(); _host.MainMenu(); }, size: UiTheme.FontBody);

            Note(_host.Settings.Squads.IsDraft() || _host.Settings.Squads == SquadMode.Alpha
                    ? "Same table and squads, next seed."
                    : "Same table, next seed; squads are drawn again.",
                UiTheme.TextNote);
        }

        /// <summary>One row of the tally: a seat diamond, its name, its squad's shapes and three numbers.</summary>
        private void TableRow(string name, PlayerColor? seat, IReadOnlyList<OperatorState> squad,
            string label, string home, string kos, string lost,
            bool header = false, bool winner = false)
        {
            var row = Slot(name, header ? 26f : 40f);

            if (winner)
            {
                UiKit.Sliced(row, DecoSprites.ButtonFill, UiTheme.PanelInset);
                UiKit.Overlay(row, DecoSprites.ButtonEdge, UiTheme.Gold);
            }

            var layout = UiKit.Row(row, 8f);
            layout.padding = new RectOffset(14, 14, 0, 0);
            layout.childForceExpandHeight = true;

            var gem = UiKit.Rect("gem", row);
            UiKit.Fixed(gem, 16f);
            if (seat.HasValue)
            {
                var image = UiKit.Diamond(gem, UiTheme.Seat(seat.Value), 12f, 18f);
                var rect = (RectTransform)image.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(12f, 18f);
            }

            float size = header ? 12f : UiTheme.FontBody;
            var dim = header ? UiTheme.Heading : UiTheme.Text;
            var nameColour = header ? UiTheme.Heading
                : seat.HasValue ? UiTheme.Readable(UiTheme.Seat(seat.Value)) : UiTheme.Text;

            Cell(row, label, size, nameColour, TextAlignmentOptions.MidlineLeft, 0f, 1f, header || winner);
            Squad(row, seat, squad, header);
            Cell(row, home, size, dim, TextAlignmentOptions.Center, 110f, 0f, header);
            Cell(row, kos, size, header ? dim : UiTheme.Threat, TextAlignmentOptions.Center, 130f, 0f, true);
            Cell(row, lost, size, header ? dim : UiTheme.TextDim, TextAlignmentOptions.Center, 90f, 0f, header);
        }

        /// <summary>The seat's operators as small shapes in its colour, their names beside them.</summary>
        private static void Squad(Transform row, PlayerColor? seat, IReadOnlyList<OperatorState> squad, bool header)
        {
            if (header || squad == null || !seat.HasValue)
            {
                var title = UiKit.Label(row, header ? "SQUAD" : "", 12f, UiTheme.Heading,
                    TextAlignmentOptions.MidlineLeft, bold: true);
                title.characterSpacing = UiTheme.HeadingSpacing * 0.5f;
                UiKit.Fixed(title, SquadWidth);
                return;
            }

            var box = UiKit.Rect("squad", row);
            UiKit.Fixed(box, SquadWidth);
            var icons = UiKit.Row(box, 6f);
            icons.childAlignment = TextAnchor.MiddleLeft;

            var colour = UiTheme.Seat(seat.Value);
            var names = new List<string>(squad.Count);
            foreach (var op in squad)
            {
                UiKit.Icon(box, PieceShape.For(op), colour, 20f);
                names.Add(op.Name);
            }

            var label = UiKit.Label(box, string.Join(" · ", names), 12f, UiTheme.TextDim);
            UiKit.Size(label, flexibleWidth: 1f);
        }

        private const float SquadWidth = 250f;

        private static void Cell(Transform row, string text, float size, Color colour,
            TextAlignmentOptions align, float width, float flexible, bool bold)
        {
            var label = UiKit.Label(row, text, size, colour, align, bold: bold);
            if (flexible > 0f) UiKit.Size(label, flexibleWidth: flexible);
            else UiKit.Fixed(label, width);

            if (size < 14f) label.characterSpacing = UiTheme.HeadingSpacing * 0.5f;
        }
    }
}
