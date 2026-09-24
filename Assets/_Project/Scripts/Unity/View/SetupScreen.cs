// Assets/_Project/Scripts/Unity/View/SetupScreen.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
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
    /// can sit opposite (Red and Green), as in classic Ludo. A tile cycles
    /// EMPTY → HUMAN → CPU (BOT3); a CPU tile carries a chip that cycles its
    /// style. The last two seats refuse to switch off, and say why. A table of
    /// CPUs only is allowed: it is watch mode.
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

        private static readonly TableMode[] Tables =
            { TableMode.FreeForAll, TableMode.CrossedPairs };

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

            // ── Table ──
            Heading("Table");

            var tables = ButtonRow("table");
            foreach (var mode in Tables) TableOption(tables, mode);

            Note(TableNote(_edit.Table), UiTheme.TextNote, 38f);

            // ── Seats ──
            Gap(4f);
            Heading($"Seats · {_edit.Seats.Count} playing");

            var seats = ButtonRow("seats", 124f);
            foreach (var seat in MatchSettings.AllSeats) SeatTile(seats, seat);

            Note(_notice ?? SeatNote(), _notice != null ? UiTheme.Threat : UiTheme.TextNote, 38f);

            // ── Squads ──
            Gap(4f);
            Heading("Squads");

            var squads = ButtonRow("squads");
            foreach (var mode in Modes) SquadOption(squads, mode);

            Note(SquadNote(_edit.Squads), UiTheme.TextNote, 38f);

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

            Note("The seed fixes the dice, who plays first and, outside ALL PICK, any random picks. " +
                 "A new match draws a fresh one.", UiTheme.TextNote, 38f);

            // ── Go ──
            Gap(10f);

            Choice(_edit.Squads.IsDraft() ? "DRAFT" : "DEAL", "Enter", Deal, UiTheme.CyanDeep, UiTheme.Cyan);

            Choice(HasMatch ? "BACK TO THE MATCH" : "BACK", "Esc", Back);
        }

        private string SeatNote()
        {
            if (_edit.Table.IsTeams())
            {
                return _edit.HumanCount == 0
                    ? "No human seats: watch mode, two CPU sides."
                    : "A seat and its partner cycle together — one player holds both. " +
                      "The chip under a CPU side sets its style.";
            }

            if (_edit.HumanCount == 0)
                return "No human seats: watch mode. Esc pauses; hold Space to hurry the CPUs.";

            return "Click a seat: empty, human, CPU. The chip under a CPU sets its style. " +
                   "Opposite seats make a fair two-player table.";
        }

        private static string TableNote(TableMode mode)
        {
            switch (mode)
            {
                case TableMode.CrossedPairs:
                    return "Two players, four seats. One holds Red and Green, the other Blue and Violet — " +
                           "partners start opposite each other. A side wins when all six of its operators are home.";
                default:
                    return "Every seat for itself: two to four players, first squad home wins.";
            }
        }

        private void TableOption(Transform row, TableMode mode)
        {
            UiKit.Button(row, mode.Label(), () => SetTable(mode), Rebuild,
                selected: _edit.Table == mode, size: UiTheme.FontSmall);
        }

        /// <summary>
        /// Switches the table mode, and fills the four seats when it needs
        /// them (ADR-0012).
        /// </summary>
        /// <remarks>
        /// A crossed table with an empty seat is a side of one against a side
        /// of two, which no one is choosing on purpose. Filling is done here
        /// rather than refused at DEAL so the screen shows what it is about to
        /// deal instead of arguing with the player afterwards; every filled
        /// seat keeps whatever kind it was last set to.
        /// </remarks>
        private void SetTable(TableMode mode)
        {
            _notice = null;
            _edit.Table = mode;

            if (!mode.IsTeams()) return;

            if (_edit.Seats.Count < MatchSettings.AllSeats.Length)
            {
                _edit.FillTable();
                _notice = "A crossed table needs all four seats.";
            }

            // Both seats of a side belong to one player, so their kinds have
            // to agree from the moment the mode is chosen, not only when a
            // tile is next clicked.
            foreach (var seat in MatchSettings.AllSeats) MatchPartner(seat);
        }

        /// <summary>Copies a seat's kind and style onto its partner, if it has one.</summary>
        private void MatchPartner(PlayerColor seat)
        {
            var partner = _edit.PartnerOf(seat);
            if (partner == null) return;

            // Only one direction, and the caller walks the seats in table
            // order, so a side settles on its first seat's kind.
            if (_edit.KindOf(partner.Value) == _edit.KindOf(seat)) return;

            _edit.SetKind(partner.Value, _edit.KindOf(seat));
            _edit.SetPersonality(partner.Value, _edit.PersonalityOf(seat));
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

        /// <summary>A seat: its diamond, name and who plays it; a CPU seat adds its style chip.</summary>
        private void SeatTile(Transform row, PlayerColor seat)
        {
            bool on = _edit.Has(seat);
            bool cpu = on && _edit.KindOf(seat) == SeatKind.Cpu;
            var colour = UiTheme.Seat(seat);

            var button = UiKit.Button(row, "", () => CycleSeat(seat), Rebuild, selected: on);

            var column = UiKit.Column((RectTransform)button.transform, 4f, 8);
            column.childAlignment = TextAnchor.MiddleCenter;
            column.childForceExpandWidth = false;
            column.padding.top = 10;

            UiKit.Diamond(button.transform, on ? colour : UiTheme.WithAlpha(colour, 0.3f), 16f, 24f);

            var name = UiKit.Label(button.transform, seat.ToString().ToUpperInvariant(), UiTheme.FontBody,
                on ? UiTheme.Readable(colour) : UiTheme.TextOff, TextAlignmentOptions.Center, bold: true);
            UiKit.Size(name, ScreenLayout.Pick(120f, 84f), 24f);

            // At a crossed table a tile says which side it is on rather than
            // just who plays it: two tiles reading "HUMAN" and two reading
            // "CPU" does not tell a player which two are partners.
            string kind = !on ? "EMPTY" : cpu ? "CPU" : "HUMAN";

            if (on && _edit.Table.IsTeams())
            {
                var partner = _edit.PartnerOf(seat);
                if (partner != null)
                    kind += $" · {partner.Value.ToString().ToUpperInvariant()}";
            }
            var state = UiKit.Label(button.transform, kind, 11f,
                on ? UiTheme.Cyan : UiTheme.TextOff, TextAlignmentOptions.Center, bold: cpu);
            state.characterSpacing = UiTheme.HeadingSpacing * 0.5f;
            UiKit.Size(state, ScreenLayout.Pick(120f, 84f), 16f);

            if (!cpu)
            {
                UiKit.Space(button.transform, ScreenLayout.Pick(120f, 84f), 26f);
                return;
            }

            // The style chip: its own button inside the tile, so it cycles without touching the seat.
            var personality = _edit.PersonalityOf(seat);
            var chip = UiKit.Button(button.transform, personality.Label(), () =>
                _edit.SetPersonality(seat, NextPersonality(personality)), Rebuild,
                size: 12f, tint: UiTheme.GoldDeep, edge: UiTheme.Line);
            UiKit.Size(chip, ScreenLayout.Pick(120f, 84f), 26f);
        }

        /// <summary>EMPTY → HUMAN → CPU → EMPTY. The last two seats skip EMPTY and say why.</summary>
        /// <remarks>
        /// At a crossed table the cycle is HUMAN ↔ CPU only, and it moves the
        /// partner seat with it (ADR-0012): the two seats are one player, so
        /// switching one off, or making it a CPU on its own, would describe a
        /// table the mode does not allow.
        /// </remarks>
        private void CycleSeat(PlayerColor seat)
        {
            _notice = null;

            if (_edit.Table.IsTeams())
            {
                var kind = _edit.KindOf(seat) == SeatKind.Human ? SeatKind.Cpu : SeatKind.Human;

                _edit.SetKind(seat, kind);
                MatchPartner(seat);
                return;
            }

            if (!_edit.Has(seat))
            {
                _edit.SetSeat(seat, true);
                _edit.SetKind(seat, SeatKind.Human);
                return;
            }

            if (_edit.KindOf(seat) == SeatKind.Human)
            {
                _edit.SetKind(seat, SeatKind.Cpu);
                return;
            }

            if (!_edit.SetSeat(seat, false))
            {
                _edit.SetKind(seat, SeatKind.Human);
                _notice = "A match needs at least two seats.";
            }
        }

        private static BotPersonality NextPersonality(BotPersonality personality) =>
            personality == BotPersonality.Brawler ? BotPersonality.Runner
            : personality == BotPersonality.Runner ? BotPersonality.Banker
            : BotPersonality.Brawler;

        private void SquadOption(Transform row, SquadMode mode)
        {
            UiKit.Button(row, mode.Label(), () => _edit.Squads = mode, Rebuild,
                selected: _edit.Squads == mode, size: UiTheme.FontSmall);
        }
    }
}
