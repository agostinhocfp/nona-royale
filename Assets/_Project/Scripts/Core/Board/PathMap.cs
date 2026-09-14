// Assets/_Project/Scripts/Core/Board/PathMap.cs
using System;

namespace NonaRoyale.Core.Board
{
    /// <summary>
    /// The board's geometry, and nothing else. Answers three questions every
    /// other core service ends up asking:
    ///
    /// <list type="number">
    /// <item>Where is an operator, given how far it has travelled?</item>
    /// <item>Is that a safe cell?</item>
    /// <item>How far apart are two cells, measured along the track?</item>
    /// </list>
    ///
    /// It holds no game state and makes no rules decisions. Whether a collision
    /// happens is <c>CollisionResolver</c>'s call; whether an operator can be
    /// targeted is <c>TargetingRules</c>'. This type only says what is where.
    /// </summary>
    /// <remarks>
    /// <b>Progress</b> is the single coordinate an operator moves along:
    /// <c>-1</c> in the yard, <c>0</c> on its own start cell, increasing by one
    /// per cell travelled. It crosses into the home column at
    /// <c>CircuitLength</c> and finishes at <c>Journey</c>. Because each colour
    /// starts at a different point on the loop, the same progress value maps to
    /// a different track cell per colour — which is the whole trick of a Ludo
    /// board, and the reason progress and cell identity are separate types here.
    ///
    /// Per ADR-0003 the circuit is a single continuous loop; how it threads down
    /// and back up each arm is a *rendering* concern. Range and movement count
    /// cells, and the arm geometry is invisible to both.
    /// </remarks>
    public sealed class PathMap
    {
        /// <summary>Progress value meaning "undeployed, in the yard".</summary>
        public const int YardProgress = -1;

        private readonly BoardProfile _profile;

        public PathMap(BoardProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public BoardProfile Profile => _profile;

        /// <summary>
        /// The absolute track index of a colour's start cell. Per ADR-0003 the
        /// four starts are one quarter of the loop apart, derived from the
        /// colour's ordinal rather than written out as four literals — so the
        /// spacing survives a change of board profile.
        /// </summary>
        public int StartTrackIndex(PlayerColor color)
        {
            RequireRealColor(color, nameof(color));
            return (int)color * _profile.PlayerStartOffset;
        }

        /// <summary>
        /// Maps a colour's progress to the board position it occupies.
        /// Progress beyond <see cref="BoardProfile.Journey"/> clamps to HOME:
        /// home entry does not require an exact roll, so an overshooting move
        /// finishes rather than bouncing.
        /// </summary>
        public CellRef CellAt(PlayerColor color, int progress)
        {
            RequireRealColor(color, nameof(color));

            if (progress < YardProgress)
                throw new ArgumentOutOfRangeException(nameof(progress),
                    $"Progress cannot be below {YardProgress} (the yard); was {progress}.");

            if (progress == YardProgress)
                return CellRef.Yard(color);

            if (progress >= _profile.Journey)
                return CellRef.Home(color);

            if (progress >= _profile.TrackLength)
                return CellRef.HomeColumn(color, progress - _profile.TrackLength);

            int index = (StartTrackIndex(color) + progress) % _profile.CircuitLength;
            return CellRef.Track(index);
        }

        /// <summary>
        /// True for the four start cells and for each home column's mouth
        /// (ADR-0003). Safe means <b>safe from collision only</b> — abilities
        /// still reach an operator standing here (COMBAT_SYSTEMS §4.4).
        /// </summary>
        public bool IsSafe(CellRef cell)
        {
            switch (cell.Kind)
            {
                case CellKind.Track:
                    // A start cell is safe for everyone standing on it, not
                    // only for its owner — standard Ludo.
                    return cell.Index % _profile.PlayerStartOffset == 0;

                case CellKind.HomeColumn:
                    return cell.Index == 0;

                default:
                    // Yard and HOME are off the board entirely, not "safe cells".
                    return false;
            }
        }

        /// <summary>
        /// Steps between two cells along the outer loop, counted in whichever
        /// direction is shorter, or <c>null</c> if either cell is not on the
        /// loop.
        /// </summary>
        /// <remarks>
        /// Never Euclidean. Two cells can sit physically beside each other
        /// across the board's centre and be a quarter of the loop apart; letting
        /// range cross that gap would make the track — the only topology the
        /// game has — meaningless (COMBAT_SYSTEMS §4.1).
        ///
        /// Returns null rather than a sentinel distance because "unreachable"
        /// and "very far" are different answers, and a caller that forgets the
        /// difference should fail loudly.
        /// </remarks>
        public int? TrackDistance(CellRef a, CellRef b)
        {
            if (!a.IsOnTrack || !b.IsOnTrack)
                return null;

            int raw = Math.Abs(a.Index - b.Index);
            return Math.Min(raw, _profile.CircuitLength - raw);
        }

        /// <summary>True while the operator is on the shared outer loop, where it can be collided with.</summary>
        public bool IsOnOuterTrack(int progress) =>
            progress >= 0 && progress < _profile.TrackLength;

        /// <summary>True once the operator has left the loop for its own home column.</summary>
        public bool IsInHomeColumn(int progress) =>
            progress >= _profile.TrackLength && progress < _profile.Journey;

        /// <summary>True once the operator has reached HOME and is out of the match.</summary>
        public bool HasFinished(int progress) => progress >= _profile.Journey;

        private static void RequireRealColor(PlayerColor color, string parameterName)
        {
            // PlayerColor.None exists so shared track cells can say "nobody owns
            // this". It is never a seat, so it must not reach the offset maths —
            // its ordinal is negative and would silently produce a wrong start.
            if (color == PlayerColor.None)
                throw new ArgumentException("PlayerColor.None is not a seat.", parameterName);

            if (color < PlayerColor.Red || color > PlayerColor.Yellow)
                throw new ArgumentException($"Unknown player colour: {color}.", parameterName);
        }
    }
}