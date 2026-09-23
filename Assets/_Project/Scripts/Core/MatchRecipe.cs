// Assets/_Project/Scripts/Core/MatchRecipe.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Core
{
    /// <summary>Where a match's squads come from. Each one is a different call into the factory.</summary>
    public enum SquadSource
    {
        /// <summary>Named per seat, by the draft screen or a test. Passed to the factory as they are.</summary>
        Drafted = 0,

        /// <summary>
        /// Drawn by the factory from the match seed. <b>The draw consumes the
        /// match RNG</b>, so the dice that follow depend on it having happened.
        /// </summary>
        Random = 1,

        /// <summary>Every seat fields Bouncer, Syla and Kurbyn at the measurement speeds.</summary>
        Alpha = 2
    }

    /// <summary>
    /// Everything needed to build the same match twice: seats, seed, how the
    /// squads are chosen, board, opening deployments and sides.
    /// </summary>
    /// <remarks>
    /// <b>Why this exists (REPLAY.md, 2026-09-22).</b> The composition root
    /// used to pick one of three factory calls itself, and a replay would have
    /// had to copy that choice. The copy is where they drift apart: a Random
    /// match rebuilt with its squads named explicitly skips the factory's draw,
    /// leaves the RNG three draws behind, and every die after that differs.
    /// Now the view and the replay player both call <see cref="Build"/>, so
    /// there is one path and nothing to keep in step.
    ///
    /// <b>Rules are not in here.</b> The configs and the roster are what
    /// <c>Replay.RulesFingerprint</c> guards; a recipe says which game was
    /// set up, not what the rules of the game were.
    /// </remarks>
    public sealed class MatchRecipe
    {
        private readonly PlayerColor[] _seats;
        private readonly Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> _squads;

        public MatchRecipe(
            IReadOnlyList<PlayerColor> seats,
            int seed,
            SquadSource source,
            IReadOnlyDictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> squads = null,
            BoardProfile board = null,
            int openingDeployments = 0,
            TeamMap teams = null)
        {
            if (seats == null) throw new ArgumentNullException(nameof(seats));
            if (seats.Count == 0) throw new ArgumentException("A match needs at least one seat.", nameof(seats));
            if (openingDeployments < 0) throw new ArgumentOutOfRangeException(nameof(openingDeployments));

            _seats = new PlayerColor[seats.Count];
            for (int i = 0; i < seats.Count; i++)
            {
                var seat = seats[i];
                if (seat == PlayerColor.None) throw new ArgumentException("None is not a seat.", nameof(seats));
                for (int j = 0; j < i; j++)
                    if (_seats[j] == seat) throw new ArgumentException($"{seat} is seated twice.", nameof(seats));
                _seats[i] = seat;
            }

            if (source == SquadSource.Drafted)
            {
                if (squads == null)
                    throw new ArgumentException("A drafted recipe names every seat's squad.", nameof(squads));

                _squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>();

                foreach (var seat in _seats)
                {
                    IReadOnlyList<OperatorDefinition> squad;
                    if (!squads.TryGetValue(seat, out squad) || squad == null)
                        throw new ArgumentException($"{seat} has no squad.", nameof(squads));
                    if (squad.Count != Roster.SquadSize)
                        throw new ArgumentException(
                            $"{seat} was given {squad.Count} operators; a squad is {Roster.SquadSize}.", nameof(squads));

                    var copy = new OperatorDefinition[squad.Count];
                    for (int i = 0; i < squad.Count; i++)
                        copy[i] = squad[i] ?? throw new ArgumentException($"{seat}'s squad has a gap.", nameof(squads));
                    _squads[seat] = copy;
                }

                foreach (var pair in squads)
                {
                    if (Array.IndexOf(_seats, pair.Key) < 0)
                        throw new ArgumentException($"{pair.Key} has a squad but no seat.", nameof(squads));
                }
            }
            else if (squads != null)
            {
                // A squad the factory would ignore is a squad the caller
                // believes is in play; refusing it is kinder than obeying.
                throw new ArgumentException($"A {source} recipe draws its own squads; pass none.", nameof(squads));
            }

            Seed = seed;
            Source = source;
            Board = board ?? BoardProfile.Standard;
            OpeningDeployments = openingDeployments;
            Teams = teams ?? TeamMap.FreeForAll;
        }

        /// <summary>The seats, in turn order.</summary>
        public IReadOnlyList<PlayerColor> Seats => _seats;

        public int Seed { get; }
        public SquadSource Source { get; }
        public BoardProfile Board { get; }
        public int OpeningDeployments { get; }
        public TeamMap Teams { get; }

        /// <summary>
        /// The named squad for <paramref name="seat"/>. Only a
        /// <see cref="SquadSource.Drafted"/> recipe has any; the others learn
        /// theirs from the built match.
        /// </summary>
        public IReadOnlyList<OperatorDefinition> SquadOf(PlayerColor seat)
        {
            IReadOnlyList<OperatorDefinition> squad;
            return _squads != null && _squads.TryGetValue(seat, out squad) ? squad : null;
        }

        /// <summary>Builds the match. Two calls on one recipe build two identical matches.</summary>
        public MatchFactory.Match Build()
        {
            switch (Source)
            {
                case SquadSource.Alpha:
                    return MatchFactory.CreateAlphaMatch(
                        _seats, Seed,
                        board: Board,
                        openingDeployments: OpeningDeployments,
                        teams: Teams);

                case SquadSource.Random:
                    return MatchFactory.Create(
                        _seats, Seed,
                        squads: null,
                        board: Board,
                        openingDeployments: OpeningDeployments,
                        teams: Teams);

                case SquadSource.Drafted:
                    return MatchFactory.Create(
                        _seats, Seed,
                        squads: _squads,
                        board: Board,
                        openingDeployments: OpeningDeployments,
                        teams: Teams);

                default:
                    throw new InvalidOperationException($"No build for squad source {Source}.");
            }
        }
    }
}