// Assets/_Project/Scripts/Core/Services/WinConditions.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Who has won (COMBAT_SYSTEMS §8): the side whose every operator has
    /// reached HOME.
    /// </summary>
    /// <remarks>
    /// There is no losing condition. Player elimination is not a mechanic in the
    /// MVP — the GDD's line about play continuing "until only one player is
    /// left" is an artifact of an early pass. Knocking someone out of a
    /// fifteen-minute game leaves them watching.
    ///
    /// <b>A side, not a seat (ADR-0012).</b> Under
    /// <see cref="TeamMap.FreeForAll"/> a side is one seat and this reads
    /// exactly as it did before team play existed. In a team match both of a
    /// player's seats must bring every operator home — all six — so neither
    /// seat can be sandbagged as a pure escort while the other races.
    /// </remarks>
    public sealed class WinConditions
    {
        private readonly PathMap _map;
        private readonly TeamMap _teams;

        /// <param name="teams">
        /// Who is on whose side (ADR-0012). Null is
        /// <see cref="TeamMap.FreeForAll"/>.
        /// </param>
        public WinConditions(PathMap map, TeamMap teams = null)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _teams = teams ?? TeamMap.FreeForAll;
        }

        /// <summary>
        /// True once an operator has reached HOME. It is out of play for the rest
        /// of the match: it cannot be targeted, moved, or returned.
        /// </summary>
        public bool HasFinished(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            return _map.HasFinished(op.Progress);
        }

        /// <summary>
        /// Whether one seat has every operator home.
        /// </summary>
        /// <remarks>
        /// <b>A seat, not a side.</b> In a team match this being true is half a
        /// win, and <see cref="Winner"/> is the thing that decides the match.
        /// Kept seat-scoped because the HUD reports per seat and because every
        /// existing caller means the seat.
        /// </remarks>
        public bool HasWon(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            foreach (var op in player.Operators)
                if (!HasFinished(op)) return false;

            return true;
        }

        /// <summary>How many of a player's operators are home. For the HUD, not for rules.</summary>
        public int FinishedCount(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            int count = 0;
            foreach (var op in player.Operators)
                if (HasFinished(op)) count++;

            return count;
        }

        /// <summary>
        /// The seat the winning side is named after, or null while the match is
        /// still running: its lowest-ordered seat at the table, which under
        /// free-for-all is the winning seat itself.
        /// </summary>
        /// <remarks>
        /// A <see cref="PlayerColor"/> rather than a side identifier because
        /// every consumer — the end screen, the history feed, the turn strip,
        /// the bot harness — already knows how to render a colour and none of
        /// them knows how to render a side. <see cref="WinningSeats"/> is there
        /// for the ones that want to name both.
        /// </remarks>
        public PlayerColor? Winner(IEnumerable<PlayerState> players)
        {
            var seats = WinningSeats(players);
            return seats.Count == 0 ? (PlayerColor?)null : seats[0];
        }

        /// <summary>
        /// Every seat of the winning side, in table order, or empty while the
        /// match is still running. One seat under free-for-all, two in a 1v1
        /// team match.
        /// </summary>
        public IReadOnlyList<PlayerColor> WinningSeats(IEnumerable<PlayerState> players)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));

            var seated = Seated(players);

            foreach (var player in seated)
            {
                if (!HasWon(player)) continue;

                var side = SideOf(seated, player.Color);
                bool sideIsHome = true;

                // Only the seats actually at the table. A side whose partner
                // seat is not playing is a side of one and wins on its own,
                // which is what makes a team map safe to leave switched on for
                // a two- or three-seat match.
                foreach (var ally in side)
                {
                    if (HasWon(ally)) continue;
                    sideIsHome = false;
                    break;
                }

                if (!sideIsHome) continue;

                var colours = new PlayerColor[side.Count];
                for (int i = 0; i < side.Count; i++) colours[i] = side[i].Color;

                return colours;
            }

            return Array.Empty<PlayerColor>();
        }

        /// <summary>
        /// Whether the match is in its final stretch: some side has every
        /// operator but one home, so one more arrival wins it.
        /// </summary>
        /// <remarks>
        /// A presentation query, never a rule — it switches the music to the
        /// showdown (AUDIO.md). It moved here from <c>GameEngine</c> with team
        /// play, because "how close is anyone to winning" is this type's
        /// business and has to move when the win condition does: counted per
        /// seat, a team match would cue the showdown while the partner seat
        /// still had three operators in its yard.
        ///
        /// A one-operator side has no stretch to speak of.
        /// </remarks>
        public bool IsFinalStretch(IEnumerable<PlayerState> players)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));

            var seated = Seated(players);
            var counted = new List<PlayerColor>();

            foreach (var player in seated)
            {
                var side = SideOf(seated, player.Color);
                if (side.Count == 0) continue;

                // One evaluation per side rather than per seat, keyed on the
                // side's first seat at the table.
                if (counted.Contains(side[0].Color)) continue;
                counted.Add(side[0].Color);

                int squad = 0;
                int home = 0;

                foreach (var ally in side)
                {
                    squad += ally.Operators.Count;
                    home += FinishedCount(ally);
                }

                if (squad > 1 && squad - home == 1) return true;
            }

            return false;
        }

        /// <summary>
        /// The seats at the table on <paramref name="colour"/>'s side, in table
        /// order, that seat included.
        /// </summary>
        private List<PlayerState> SideOf(List<PlayerState> seated, PlayerColor colour)
        {
            var side = new List<PlayerState>(seated.Count);

            foreach (var player in seated)
                if (_teams.AreAllied(player.Color, colour)) side.Add(player);

            return side;
        }

        /// <summary>
        /// The players, in table order. Sorted rather than taken as given so
        /// that the seat a side is named after does not depend on the order the
        /// caller happened to build its list in.
        /// </summary>
        private static List<PlayerState> Seated(IEnumerable<PlayerState> players)
        {
            var seated = new List<PlayerState>();

            foreach (var player in players)
                if (player != null) seated.Add(player);

            seated.Sort(ByTableOrder);
            return seated;
        }

        private static int ByTableOrder(PlayerState a, PlayerState b) =>
            ((int)a.Color).CompareTo((int)b.Color);
    }
}
