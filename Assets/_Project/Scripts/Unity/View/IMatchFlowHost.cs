// Assets/_Project/Scripts/Unity/View/IMatchFlowHost.cs
using System.Collections.Generic;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Draft;

namespace NonaRoyale.Unity.View
{
    /// <summary>Who plays a seat (BOTS.md, increment BOT3).</summary>
    public enum SeatKind
    {
        Human = 0,
        Cpu = 1
    }

    /// <summary>
    /// How the four seats are divided into sides: setup's table row
    /// (ADR-0012).
    /// </summary>
    public enum TableMode
    {
        /// <summary>The default: every seat for itself.</summary>
        FreeForAll = 0,

        /// <summary>
        /// 1v1 across the table. One player holds Red and Green, the other
        /// Blue and Violet. Needs all four seats.
        /// </summary>
        CrossedPairs = 1
    }

    public static class TableModes
    {
        /// <summary>The core side map this mode means.</summary>
        public static TeamMap ToTeamMap(this TableMode mode) =>
            mode == TableMode.CrossedPairs ? TeamMap.CrossedPairs : TeamMap.FreeForAll;

        /// <summary>Whether the mode pairs seats up, so the setup screen must fill the table.</summary>
        public static bool IsTeams(this TableMode mode) => mode != TableMode.FreeForAll;

        public static string Label(this TableMode mode) =>
            mode == TableMode.CrossedPairs ? "1v1 CROSSED" : "FREE-FOR-ALL";
    }

    /// <summary>How the seats get their squads: setup's squad row (DRAFT.md).</summary>
    public enum SquadMode
    {
        /// <summary>The default: a free 30-second draft, anyone picks at any time.</summary>
        AllPick = 0,

        /// <summary>A turn-order draft, 10 seconds per pick.</summary>
        Snake = 1,

        /// <summary>Every squad drawn at random from the match seed.</summary>
        Random = 2,

        /// <summary>Every seat fields Bouncer, Syla and Kurbyn: the measurement squad.</summary>
        Alpha = 3
    }

    public static class SquadModes
    {
        /// <summary>Whether the mode goes through the draft screen.</summary>
        public static bool IsDraft(this SquadMode mode) => mode == SquadMode.AllPick || mode == SquadMode.Snake;

        /// <summary>The core draft mode for a drafted squad mode.</summary>
        public static DraftMode ToDraftMode(this SquadMode mode) =>
            mode == SquadMode.Snake ? DraftMode.Snake : DraftMode.AllPick;

        public static string Label(this BotPersonality personality) =>
            personality.ToString().ToUpperInvariant();

        public static string Label(this SquadMode mode)
        {
            switch (mode)
            {
                case SquadMode.AllPick: return "ALL PICK";
                case SquadMode.Snake: return "SNAKE";
                case SquadMode.Random: return "RANDOM";
                case SquadMode.Alpha: return "ALPHA THREE";
                default: return mode.ToString().ToUpperInvariant();
            }
        }
    }

    /// <summary>
    /// How a match is set up: which seats play and who plays them, how squads
    /// are chosen, which seed (GUI increment I; squad modes since DR2; CPU
    /// seats since BOT3).
    /// </summary>
    /// <remarks>
    /// View-side configuration, not rules: it is what the composition root
    /// hands <c>MatchFactory</c>. The setup screen edits a copy and gives it
    /// back on DEAL, so backing out changes nothing.
    /// </remarks>
    public sealed class MatchSettings
    {
        public const int MinSeats = 2;

        public static readonly PlayerColor[] AllSeats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
        };

        private readonly List<PlayerColor> _seats = new List<PlayerColor>();
        private readonly Dictionary<PlayerColor, SeatKind> _kinds = new Dictionary<PlayerColor, SeatKind>();
        private readonly Dictionary<PlayerColor, BotPersonality> _personalities =
            new Dictionary<PlayerColor, BotPersonality>();

        /// <summary>The seats playing, always in table order (Red, Blue, Green, Violet).</summary>
        public IReadOnlyList<PlayerColor> Seats => _seats;

        /// <summary>How the squads are chosen. Replaced the Drafted flag in DR2.</summary>
        public SquadMode Squads { get; set; }

        /// <summary>
        /// How the table is divided into sides (ADR-0012). Free-for-all unless
        /// the setup screen says otherwise.
        /// </summary>
        public TableMode Table { get; set; }

        public int Seed { get; set; }

        /// <summary>The core side map for this table. What the composition root hands the factory.</summary>
        public TeamMap Teams => Table.ToTeamMap();

        /// <summary>
        /// The other seat on <paramref name="seat"/>'s side, or null when it
        /// plays alone.
        /// </summary>
        /// <remarks>
        /// The setup screen uses it to keep a partnership consistent: one
        /// player holds both seats, so "human here, CPU there" is not a table
        /// anyone wants and the tile cycles both together.
        /// </remarks>
        public PlayerColor? PartnerOf(PlayerColor seat)
        {
            foreach (var other in Teams.SeatsOn(seat))
                if (other != seat) return other;

            return null;
        }

        public MatchSettings(
            IEnumerable<PlayerColor> seats, SquadMode squads, int seed,
            TableMode table = TableMode.FreeForAll)
        {
            foreach (var seat in seats) SetSeat(seat, true);
            Squads = squads;
            Seed = seed;
            Table = table;
        }

        public bool Has(PlayerColor seat) => _seats.Contains(seat);

        /// <summary>Who plays the seat. Remembered for an empty seat too, so switching it back on keeps it.</summary>
        public SeatKind KindOf(PlayerColor seat) => _kinds.TryGetValue(seat, out var kind) ? kind : SeatKind.Human;

        public void SetKind(PlayerColor seat, SeatKind kind) => _kinds[seat] = kind;

        public bool IsCpu(PlayerColor seat) => Has(seat) && KindOf(seat) == SeatKind.Cpu;

        /// <summary>The seat's CPU style. Defaults differ by seat, so a table of CPUs is mixed.</summary>
        public BotPersonality PersonalityOf(PlayerColor seat) =>
            _personalities.TryGetValue(seat, out var personality) ? personality : DefaultPersonality(seat);

        public void SetPersonality(PlayerColor seat, BotPersonality personality) => _personalities[seat] = personality;

        /// <summary>Playing seats that a human plays.</summary>
        public int HumanCount
        {
            get
            {
                int count = 0;
                foreach (var seat in _seats) if (KindOf(seat) == SeatKind.Human) count++;
                return count;
            }
        }

        public static BotPersonality DefaultPersonality(PlayerColor seat) =>
            (BotPersonality)(((int)seat % 3 + 3) % 3);

        /// <summary>Copies who plays each seat, and how, from another settings object.</summary>
        public void CopySeatsFrom(MatchSettings other)
        {
            if (other == null) return;
            foreach (var pair in other._kinds) _kinds[pair.Key] = pair.Value;
            foreach (var pair in other._personalities) _personalities[pair.Key] = pair.Value;
            Table = other.Table;
        }

        /// <summary>
        /// Switches every seat on, in table order. A team table needs all four
        /// (ADR-0012), and the setup screen calls this when the mode changes
        /// rather than leaving a partnership half-seated.
        /// </summary>
        public void FillTable()
        {
            foreach (var seat in AllSeats) SetSeat(seat, true);
        }

        /// <summary>Turns a seat on or off. Refuses to go below <see cref="MinSeats"/>; returns whether it changed.</summary>
        public bool SetSeat(PlayerColor seat, bool on)
        {
            if (on == Has(seat)) return false;
            if (!on && _seats.Count <= MinSeats) return false;

            if (on) _seats.Add(seat);
            else _seats.Remove(seat);

            _seats.Sort();
            return true;
        }

        public MatchSettings Clone()
        {
            var copy = new MatchSettings(_seats, Squads, Seed, Table);
            copy.CopySeatsFrom(this);
            return copy;
        }
    }

    /// <summary>
    /// What the setup, draft and end screens may read and ask for. The
    /// composition root implements it (GUI increment I; draft since DR2).
    /// </summary>
    public interface IMatchFlowHost
    {
        /// <summary>The settings the current (or next) match uses. A copy is safe to edit.</summary>
        MatchSettings Settings { get; }

        /// <summary>The match on the table, or null before the first deal.</summary>
        MatchFactory.Match Match { get; }

        /// <summary>
        /// Setup's DEAL. A drafted mode opens the draft screen and commits
        /// nothing yet; the other modes deal at once.
        /// </summary>
        void Deal(MatchSettings settings);

        /// <summary>The draft is done: commit its settings and deal with its squads.</summary>
        void FinishDraft(MatchSettings settings,
            IReadOnlyDictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> squads);

        /// <summary>The draft's BACK: return to setup with the settings it was opened with.</summary>
        void CancelDraft(MatchSettings settings);

        /// <summary>The same table again with the next seed. Drafted squads are kept.</summary>
        void Rematch();

        /// <summary>Opens the setup screen.</summary>
        void OpenSetup();

        /// <summary>Setup's BACK: to the match if one is on the table, otherwise to the title (GUI increment J).</summary>
        void CancelSetup();

        /// <summary>Abandons any match and returns to the title screen (GUI increment J).</summary>
        void MainMenu();
    }
}
