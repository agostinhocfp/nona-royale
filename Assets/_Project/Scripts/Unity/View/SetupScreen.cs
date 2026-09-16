// Assets/_Project/Scripts/Unity/View/SetupScreen.cs
using NonaRoyale.Core.Board;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The match setup screen: seats, squads, seed, deal (GUI increment I;
    /// squad modes since DR2).
    /// </summary>
    /// <remarks>
    /// <b>Shown from the title's PLAY</b> over the empty table, and from the
    /// pause menu's and end screen's NEW MATCH. It edits a copy of the host's
    /// settings; DEAL hands the copy back, BACK throws it away and returns to
    /// the match, or to the title when there is no match (increment J). The
    /// draft's BACK reopens it with the settings the draft was given, so a
    /// player who backs out of a draft finds their choices still set.
    ///
    /// <b>Seats are four tiles, any two to four on</b>, so a two-player game
    /// can sit opposite (Red and Green), as in classic Ludo. The last two
    /// seats refuse to switch off, and say why.
    ///
    /// <b>Squads are one of four modes</b> (DRAFT.md): ALL PICK and SNAKE go
    /// through the draft screen, so the confirm button reads DRAFT; RANDOM
    /// and ALPHA THREE deal at once.
    ///
    /// <b>The seed is shown, not typed.</b> SHUFFLE draws a new one. Picking a
    /// seed is the view's own business, not a rule; the dice it produces come
    /// from the core.
    ///
    /// Enter deals (or drafts); Esc goes back.
    /// </remarks>
    public sealed class SetupScreen : ModalCard
    {
        private static readonly SquadMode[] Modes =
            { SquadMode.AllPick, SquadMode.Snake, SquadMode.Random, SquadMode.Alpha };

        private IMatchFlowHost _host;
        private MatchSettings _edit;
        private string _notice;

        protected override float CardWidth => 700f;

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

        /// <summary>Opens on the host's current settings.</summary>
        public void Open()
        {
            if (_host == null) return;
            Open(_host.Settings);
        }

        /// <summary>Opens on the given settings (the draft's BACK).</summary>
        public void Open(MatchSettings settings)
        {
            if (_host == null || settings == null) return;

            _edit = settings.Clone();
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
            var settings = _edit;
            Close();
            _host.Deal(settings);
        }

        protected override void Compose()
        {
            Title("New match", "Choose the table.");

            // ── Seats ──
            Heading($"Seats · {_edit.Seats.Count} playing");

            var seats = ButtonRow("seats", 92f);
            foreach (var seat in MatchSettings.AllSeats) SeatTile(seats, seat);

            Note(_notice ?? "Any two to four. Opposite seats make a fair two-player table.",
                _notice != null ? UiTheme.Threat : UiTheme.TextOff);

            // ── Squads ──
            Gap(4f);
            Heading("Squads");

            var squads = ButtonRow("squads");
            foreach (var mode in Modes) SquadOption(squads, mode);

            Note(SquadNote(_edit.Squads), UiTheme.TextOff, 38f);

            // ── Seed ──
            Gap(4f);
            Heading("Seed");

            var seedRow = ButtonRow("seed", 44f);
            var seedLabel = UiKit.Label(seedRow, $"<b>{_edit.Seed}</b>", UiTheme.FontLarge, UiTheme.Text,
                TextAlignmentOptions.MidlineLeft);
            UiKit.Size(seedLabel, flexibleWidth: 2f);

            var shuffle = UiKit.Button(seedRow, "SHUFFLE", () =>
            {
                _edit.Seed = Random.Range(1, 100000000);
                _notice = null;
            }, Rebuild, size: UiTheme.FontSmall);
            UiKit.Size(shuffle, flexibleWidth: 1f);

            Note("The seed fixes the dice and any random picks. Same seed, same dice.", UiTheme.TextOff);

            // ── Go ──
            Gap(10f);

            Choice(_edit.Squads.IsDraft() ? "DRAFT" : "DEAL", "Enter", Deal, UiTheme.CyanDeep, UiTheme.Cyan);

            Choice(HasMatch ? "BACK TO THE MATCH" : "BACK", "Esc", Back);
        }

        private static string SquadNote(SquadMode mode)
        {
            switch (mode)
            {
                case SquadMode.AllPick:
                    return "A shared 30-second draft: any seat picks at any time. Empty slots are filled at random when time runs out.";
                case SquadMode.Snake:
                    return "Picks in turn, reversing each round, 10 seconds a pick. A missed pick is made at random.";
                case SquadMode.Random:
                    return "Three distinct operators per seat, drawn from the whole roster by the seed.";
                case SquadMode.Alpha:
                    return "Every seat plays Bouncer, Syla and Kurbyn: the measured baseline.";
                default:
                    return "";
            }
        }

        /// <summary>A seat: its diamond and name, lit cyan when playing.</summary>
        private void SeatTile(Transform row, PlayerColor seat)
        {
            bool on = _edit.Has(seat);
            var colour = UiTheme.Seat(seat);

            var button = UiKit.Button(row, "", () =>
            {
                bool changed = _edit.SetSeat(seat, !on);
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

        private void SquadOption(Transform row, SquadMode mode)
        {
            UiKit.Button(row, mode.Label(), () => _edit.Squads = mode, Rebuild,
                selected: _edit.Squads == mode, size: UiTheme.FontSmall);
        }
    }
}
