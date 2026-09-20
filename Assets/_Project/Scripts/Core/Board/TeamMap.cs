// Assets/_Project/Scripts/Core/Board/TeamMap.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Core.Board
{
    /// <summary>
    /// Which seats are on the same side (ADR-0012). The one place the game
    /// answers "friend or foe", so every rule that used to compare two
    /// <see cref="PlayerColor"/>s for equality asks this instead.
    /// </summary>
    /// <remarks>
    /// <b>Why a map and not a flag on the seat.</b> A seat's side is a property
    /// of the <i>match</i>, not of the colour: Red and Green are enemies in a
    /// four-way and partners in 1v1. Putting it on <see cref="PlayerColor"/> or
    /// on <c>PlayerState</c> would make it either a global or a thing two
    /// objects can disagree about, and the friend/foe test is read by a dozen
    /// services — exactly the shape ADR-0004 was written about.
    ///
    /// <b>Free-for-all is a team map too.</b> <see cref="FreeForAll"/> puts each
    /// seat on a side of its own, so <c>AreAllied(a, b)</c> reduces to
    /// <c>a == b</c> and every rule behaves exactly as it did before this type
    /// existed. That is deliberate: there is no "team mode" branch anywhere in
    /// the rules, only a different map, so the four-way game cannot rot while
    /// 1v1 is being worked on.
    ///
    /// <b>Seats absent from a match need no special case.</b> The map describes
    /// all four seats whatever the match fields; every lookup starts from an
    /// operator that exists, so a side with one seat at the table is simply a
    /// side of one.
    ///
    /// Immutable, and the two shipped layouts are shared instances. Nothing here
    /// holds match state.
    /// </remarks>
    public sealed class TeamMap
    {
        /// <summary>What <see cref="TeamOf"/> answers for <see cref="PlayerColor.None"/>.</summary>
        /// <remarks>
        /// Distinct from every real side, and never allied with anything —
        /// shared outer-track cells carry <c>None</c> as their owner, and a cell
        /// is on nobody's side.
        /// </remarks>
        public const int NoTeam = -1;

        private static readonly PlayerColor[] AllSeats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
        };

        private readonly int[] _teamOfSeat;                 // indexed by (int)PlayerColor
        private readonly PlayerColor[][] _seatsOfSeat;      // indexed by (int)PlayerColor

        private TeamMap(int[] teamOfSeat, bool hasTeams, string name)
        {
            _teamOfSeat = teamOfSeat;
            HasTeams = hasTeams;
            Name = name;

            _seatsOfSeat = new PlayerColor[AllSeats.Length][];

            foreach (var seat in AllSeats)
            {
                var side = new List<PlayerColor>(AllSeats.Length);

                foreach (var other in AllSeats)
                    if (teamOfSeat[(int)other] == teamOfSeat[(int)seat]) side.Add(other);

                _seatsOfSeat[(int)seat] = side.ToArray();
            }
        }

        /// <summary>
        /// Every seat for itself. The four-way game, and the default everywhere
        /// a team map is optional.
        /// </summary>
        public static TeamMap FreeForAll { get; } =
            new TeamMap(new[] { 0, 1, 2, 3 }, hasTeams: false, name: "free-for-all");

        /// <summary>
        /// 1v1 across the table: Red with Green, Blue with Violet (ADR-0012).
        /// </summary>
        /// <remarks>
        /// <b>Crossed, not adjacent, and the geometry is the reason.</b> A
        /// colour's start sits at <c>(int)colour * PlayerStartOffset</c> around
        /// the loop (ADR-0003), so seats 0 and 2 are half a circuit apart and so
        /// are 1 and 3. Partners therefore begin as far from each other as the
        /// board allows, and each side's two runners are always somewhere near
        /// opposite ends of the track. Pairing neighbours instead would hand
        /// each side one contiguous half of the board and turn the match into a
        /// standoff across one seam.
        /// </remarks>
        public static TeamMap CrossedPairs { get; } =
            new TeamMap(new[] { 0, 1, 0, 1 }, hasTeams: true, name: "crossed pairs");

        /// <summary>
        /// 1v1 down the table: Red with Blue, Green with Violet. Not shipped in
        /// any mode; here so the crossed layout is visibly a choice rather than
        /// the only thing the type can express.
        /// </summary>
        public static TeamMap AdjacentPairs { get; } =
            new TeamMap(new[] { 0, 0, 1, 1 }, hasTeams: true, name: "adjacent pairs");

        /// <summary>
        /// Whether any two seats share a side. False for
        /// <see cref="FreeForAll"/>, and the only thing presentation needs to
        /// ask — the rules never branch on it.
        /// </summary>
        public bool HasTeams { get; }

        /// <summary>For logs and the setup screen.</summary>
        public string Name { get; }

        /// <summary>
        /// Builds a map from explicit sides. Every seat must appear exactly
        /// once across them.
        /// </summary>
        /// <remarks>
        /// For the simulation harness and for tests that want a layout the two
        /// shipped ones do not cover. A seat listed twice, or left out, is a
        /// caller bug and throws — a map that quietly disagreed with itself
        /// would show up as one operator being an ally in one rule and an enemy
        /// in the next.
        /// </remarks>
        public static TeamMap Of(params PlayerColor[][] sides)
        {
            if (sides == null) throw new ArgumentNullException(nameof(sides));
            if (sides.Length == 0) throw new ArgumentException("A map needs at least one side.", nameof(sides));

            var teamOfSeat = new int[AllSeats.Length];
            for (int i = 0; i < teamOfSeat.Length; i++) teamOfSeat[i] = NoTeam;

            for (int team = 0; team < sides.Length; team++)
            {
                var side = sides[team];
                if (side == null || side.Length == 0)
                    throw new ArgumentException($"Side {team} is empty.", nameof(sides));

                foreach (var seat in side)
                {
                    RequireRealSeat(seat, nameof(sides));

                    if (teamOfSeat[(int)seat] != NoTeam)
                        throw new ArgumentException($"{seat} is on two sides.", nameof(sides));

                    teamOfSeat[(int)seat] = team;
                }
            }

            foreach (var seat in AllSeats)
            {
                if (teamOfSeat[(int)seat] == NoTeam)
                    throw new ArgumentException($"{seat} is on no side.", nameof(sides));
            }

            bool hasTeams = false;
            foreach (var side in sides) if (side.Length > 1) hasTeams = true;

            return new TeamMap(teamOfSeat, hasTeams, hasTeams ? "custom teams" : "custom free-for-all");
        }

        /// <summary>
        /// The side a seat is on. <see cref="NoTeam"/> for
        /// <see cref="PlayerColor.None"/>.
        /// </summary>
        /// <remarks>
        /// An opaque identifier: compare it, do not index anything by it and do
        /// not show it. Use <see cref="SeatsOn"/> or <see cref="LeadSeat"/> for
        /// anything a player will see.
        /// </remarks>
        public int TeamOf(PlayerColor seat) =>
            seat == PlayerColor.None ? NoTeam : _teamOfSeat[(int)seat];

        /// <summary>
        /// Whether two seats are on the same side. A seat is always allied with
        /// itself; <see cref="PlayerColor.None"/> never is, with anything.
        /// </summary>
        public bool AreAllied(PlayerColor a, PlayerColor b)
        {
            if (a == PlayerColor.None || b == PlayerColor.None) return false;
            return _teamOfSeat[(int)a] == _teamOfSeat[(int)b];
        }

        /// <summary>
        /// Whether two seats are on opposite sides. Not simply the negation of
        /// <see cref="AreAllied"/>: a seat that is nobody — an unowned cell — is
        /// neither an ally nor an enemy.
        /// </summary>
        public bool AreEnemies(PlayerColor a, PlayerColor b)
        {
            if (a == PlayerColor.None || b == PlayerColor.None) return false;
            return _teamOfSeat[(int)a] != _teamOfSeat[(int)b];
        }

        /// <summary>
        /// Every seat on this seat's side, in table order, the seat itself
        /// included. One entry under <see cref="FreeForAll"/>.
        /// </summary>
        public IReadOnlyList<PlayerColor> SeatsOn(PlayerColor seat)
        {
            RequireRealSeat(seat, nameof(seat));
            return _seatsOfSeat[(int)seat];
        }

        /// <summary>
        /// The seat a side is named after: the first of its seats in table
        /// order. The seat itself under <see cref="FreeForAll"/>.
        /// </summary>
        /// <remarks>
        /// So that a win, a scoreboard row or a log line can name a side with a
        /// <see cref="PlayerColor"/> the view already knows how to colour,
        /// rather than an integer nobody can render. It is a label, never a
        /// rule: nothing may treat the lead seat as commanding the other.
        /// </remarks>
        public PlayerColor LeadSeat(PlayerColor seat) => SeatsOn(seat)[0];

        public override string ToString() => Name;

        private static void RequireRealSeat(PlayerColor seat, string parameterName)
        {
            if (seat == PlayerColor.None)
                throw new ArgumentException("A side is made of seats; None is not one.", parameterName);
        }
    }
}
