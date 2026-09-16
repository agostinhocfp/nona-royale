// Assets/_Project/Scripts/Unity/View/SetupScreen.cs
using NonaRoyale.Core.Board;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The match setup screen: seats, squads, seed, deal (GUI increment I).
    /// </summary>
    /// <remarks>
    /// <b>Shown from the title's PLAY</b> over the empty table, and from the
    /// pause menu's and end screen's NEW MATCH. It edits a copy of the host's
    /// settings; DEAL hands the copy back, BACK throws it away and returns to
    /// the match, or to the title when there is no match (increment J).
    ///
    /// <b>Seats are four tiles, any two to four on</b>, so a two-player game
    /// can sit opposite (Red and Green), as in classic Ludo. The last two
    /// seats refuse to switch off, and say why.
    ///
    /// <b>The seed is shown, not typed.</b> SHUFFLE draws a new one. Picking a
    /// seed is the view's own business, not a rule; the dice it produces come
    /// from the core.
    ///
    /// Enter deals; Esc goes back.
    /// </remarks>
    public sealed class SetupScreen : ModalCard
    {
        private IMatchFlowHost _host;
        private MatchSettings _draft;
        private string _notice;

        protected override float CardWidth => 640f;

        // Over an empty table at first launch; the board shows faintly through.
        protected override float ScrimAlpha => 0.72f;

        /// <summary>Whether BACK returns to a match (otherwise it returns to the title).</summary>
        private bool HasMatch => _host?.Match != null;

        public void Bind(RectTransform canvasRect, IMatchFlowHost host)
        {
            _host = host;
            BuildOnce(canvasRect, "setup_screen");
            if (IsOpen) Close();
        }

        public void Open()
        {
            if (_host == null) return;

            _draft = _host.Settings.Clone();
            _notice = null;
            Show();
        }

        /// <summary>Esc: back to the match, or to the title.</summary>
        public void Back()
        {
            if (!IsOpen) return;

            Close();
            _host.CancelSetup();
        }

        /// <summary>Enter: deal.</summary>
        public void Confirm()
        {
            if (IsOpen) Deal();
        }

        private void Deal()
        {
            var settings = _draft;
            Close();
            _host.Deal(settings);
        }

        protected override void Compose()
        {
            Title("New match", "Choose the table.");

            // ── Seats ──
            Heading($"Seats · {_draft.Seats.Count} playing");

            var seats = ButtonRow("seats", 92f);
            foreach (var seat in MatchSettings.AllSeats) SeatTile(seats, seat);

            Note(_notice ?? "Any two to four. Opposite seats make a fair two-player table.",
                _notice != null ? UiTheme.Threat : UiTheme.TextOff);

            // ── Squads ──
            Gap(4f);
            Heading("Squads");

            var squads = ButtonRow("squads");
            SquadOption(squads, "ALPHA THREE", false);
            SquadOption(squads, "DRAFTED", true);

            Note(_draft.Drafted
                    ? "Three distinct operators per seat, drawn from the whole roster by the seed."
                    : "Every seat plays Bouncer, Syla and Kurbyn: the measured baseline.",
                UiTheme.TextOff);

            // ── Seed ──
            Gap(4f);
            Heading("Seed");

            var seedRow = ButtonRow("seed", 44f);
            var seedLabel = UiKit.Label(seedRow, $"<b>{_draft.Seed}</b>", UiTheme.FontLarge, UiTheme.Text,
                TextAlignmentOptions.MidlineLeft);
            UiKit.Size(seedLabel, flexibleWidth: 2f);

            var shuffle = UiKit.Button(seedRow, "SHUFFLE", () =>
            {
                _draft.Seed = Random.Range(1, 100000000);
                _notice = null;
            }, Rebuild, size: UiTheme.FontSmall);
            UiKit.Size(shuffle, flexibleWidth: 1f);

            Note("The seed fixes the dice and the draft. Same seed, same match.", UiTheme.TextOff);

            // ── Go ──
            Gap(10f);

            Choice("DEAL", "Enter", Deal, UiTheme.CyanDeep, UiTheme.Cyan);

            Choice(HasMatch ? "BACK TO THE MATCH" : "BACK", "Esc", Back);
        }

        /// <summary>A seat: its diamond and name, lit cyan when playing.</summary>
        private void SeatTile(Transform row, PlayerColor seat)
        {
            bool on = _draft.Has(seat);
            var colour = UiTheme.Seat(seat);

            var button = UiKit.Button(row, "", () =>
            {
                bool changed = _draft.SetSeat(seat, !on);
                _notice = changed ? null : "A match needs at least two seats.";
            }, Rebuild, selected: on);

            var column = UiKit.Column((RectTransform)button.transform, 4f, 8);
            column.childAlignment = TextAnchor.MiddleCenter;
            column.childForceExpandWidth = false;
            column.padding.top = 12;

            UiKit.Diamond(button.transform, on ? colour : UiTheme.WithAlpha(colour, 0.3f), 16f, 24f);

            var name = UiKit.Label(button.transform, seat.ToString().ToUpperInvariant(), UiTheme.FontBody,
                on ? UiTheme.Readable(colour) : UiTheme.TextOff, TextAlignmentOptions.Center, bold: true);
            UiKit.Size(name, 120f, 24f);

            var state = UiKit.Label(button.transform, on ? "PLAYING" : "EMPTY", 11f,
                on ? UiTheme.Cyan : UiTheme.TextOff, TextAlignmentOptions.Center);
            state.characterSpacing = UiTheme.HeadingSpacing * 0.5f;
            UiKit.Size(state, 120f, 16f);
        }

        private void SquadOption(Transform row, string label, bool drafted)
        {
            UiKit.Button(row, label, () => _draft.Drafted = drafted, Rebuild,
                selected: _draft.Drafted == drafted, size: UiTheme.FontBody);
        }
    }
}
