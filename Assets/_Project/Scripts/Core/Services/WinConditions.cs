// Assets/_Project/Scripts/Core/Services/WinConditions.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Who has won (COMBAT_SYSTEMS §8): the player whose every operator has
    /// reached HOME.
    /// </summary>
    /// <remarks>
    /// There is no losing condition. Player elimination is not a mechanic in the
    /// MVP — the GDD's line about play continuing "until only one player is
    /// left" is an artifact of an early pass. Knocking someone out of a
    /// fifteen-minute game leaves them watching.
    /// </remarks>
    public sealed class WinConditions
    {
        private readonly PathMap _map;

        public WinConditions(PathMap map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
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

        /// <summary>The winning seat, or null while the match is still running.</summary>
        public PlayerColor? Winner(IEnumerable<PlayerState> players)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));

            foreach (var player in players)
                if (HasWon(player)) return player.Color;

            return null;
        }
    }
}