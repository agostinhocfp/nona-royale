// Assets/_Project/Scripts/Unity/View/IMatchFlowHost.cs
using System.Collections.Generic;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Draft;

namespace NonaRoyale.Unity.View
{
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
    /// How a match is set up: which seats play, how squads are chosen, which
    /// seed (GUI increment I; squad modes since DR2).
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

        /// <summary>The seats playing, always in table order (Red, Blue, Green, Violet).</summary>
        public IReadOnlyList<PlayerColor> Seats => _seats;

        /// <summary>How the squads are chosen. Replaced the Drafted flag in DR2.</summary>
        public SquadMode Squads { get; set; }

        public int Seed { get; set; }

        public MatchSettings(IEnumerable<PlayerColor> seats, SquadMode squads, int seed)
        {
            foreach (var seat in seats) SetSeat(seat, true);
            Squads = squads;
            Seed = seed;
        }

        public bool Has(PlayerColor seat) => _seats.Contains(seat);

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

        public MatchSettings Clone() => new MatchSettings(_seats, Squads, Seed);
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
