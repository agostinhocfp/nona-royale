// Assets/_Project/Scripts/Core/Services/MatchClock.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// The match's turn counter, and the one thing that knows whose turn it is.
    /// </summary>
    /// <remarks>
    /// Its own type rather than a role played by <see cref="TurnStateMachine"/>,
    /// because the registry needs a clock to construct and the machine needs the
    /// registry — a cycle. Splitting the clock out breaks it, and the machine
    /// simply drives this.
    ///
    /// Turn counts live on <see cref="PlayerState"/>, so there is exactly one
    /// copy of each and no chance of the two disagreeing.
    /// </remarks>
    public sealed class MatchClock : ITurnClock
    {
        private readonly Dictionary<PlayerColor, PlayerState> _players =
            new Dictionary<PlayerColor, PlayerState>();

        public MatchClock(IEnumerable<PlayerState> players)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));

            foreach (var player in players)
                _players[player.Color] = player;

            if (_players.Count == 0)
                throw new ArgumentException("A match needs at least one player.", nameof(players));
        }

        /// <summary>Null before the first turn begins.</summary>
        public PlayerColor ActivePlayer { get; private set; } = PlayerColor.None;

        public int TurnIndexOf(PlayerColor color) =>
            _players.TryGetValue(color, out var player) ? player.TurnIndex : 0;

        /// <summary>Hands the turn to a seat and advances that seat's counter.</summary>
        public void BeginTurnFor(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            ActivePlayer = player.Color;
            player.BeginTurn();
        }
    }
}