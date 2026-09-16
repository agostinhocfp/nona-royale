// Assets/_Project/Scripts/Unity/View/IMatchFlowHost.cs
using System.Collections.Generic;
using NonaRoyale.Core;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a match is set up: which seats play, whose squads, which seed
    /// (GUI increment I).
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

        /// <summary>Squads drafted from the whole roster, or the alpha three for every seat.</summary>
        public bool Drafted { get; set; }

        public int Seed { get; set; }

        public MatchSettings(IEnumerable<PlayerColor> seats, bool drafted, int seed)
        {
            foreach (var seat in seats) SetSeat(seat, true);
            Drafted = drafted;
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

        public MatchSettings Clone() => new MatchSettings(_seats, Drafted, Seed);
    }

    /// <summary>
    /// What the setup and end screens may read and ask for. The composition
    /// root implements it (GUI increment I).
    /// </summary>
    public interface IMatchFlowHost
    {
        /// <summary>The settings the current (or next) match uses. A copy is safe to edit.</summary>
        MatchSettings Settings { get; }

        /// <summary>The match on the table, or null before the first deal.</summary>
        MatchFactory.Match Match { get; }

        /// <summary>Deals a new match with these settings.</summary>
        void Deal(MatchSettings settings);

        /// <summary>The same table again with the next seed.</summary>
        void Rematch();

        /// <summary>Opens the setup screen.</summary>
        void OpenSetup();

        /// <summary>Setup's BACK: to the match if one is on the table, otherwise to the title (GUI increment J).</summary>
        void CancelSetup();

        /// <summary>Abandons any match and returns to the title screen (GUI increment J).</summary>
        void MainMenu();
    }
}
