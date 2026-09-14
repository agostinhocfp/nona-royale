// Assets/_Project/Scripts/Core/Model/PlayerState.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Core.Model
{
    /// <summary>
    /// One seat at the table: its three operators and its energy pool.
    /// </summary>
    /// <remarks>
    /// <b>Energy is player-level, not per-operator.</b> The tactical question
    /// the pool creates — <i>which</i> of my three spends this — is the point of
    /// it (COMBAT_SYSTEMS §3). <c>Energy</c> as an operator stat was cut along
    /// with Energy Efficiency.
    ///
    /// Like <see cref="OperatorState"/>, this holds values and decides nothing.
    /// </remarks>
    public sealed class PlayerState
    {
        private readonly List<OperatorState> _operators;

        public PlayerState(PlayerColor color, IEnumerable<OperatorState> operators)
        {
            if (color == PlayerColor.None)
                throw new ArgumentException("A player must hold a seat.", nameof(color));
            if (operators == null) throw new ArgumentNullException(nameof(operators));

            _operators = new List<OperatorState>(operators);

            if (_operators.Count == 0)
                throw new ArgumentException("A player fields at least one operator.", nameof(operators));

            foreach (var op in _operators)
            {
                if (op.Owner != color)
                    throw new ArgumentException(
                        $"{op.Name} belongs to {op.Owner}, not {color}.", nameof(operators));
            }

            Color = color;
        }

        public PlayerColor Color { get; }

        public IReadOnlyList<OperatorState> Operators => _operators;

        /// <summary>The shared pool. Only <c>EnergyLedger</c> changes it.</summary>
        public int Energy { get; private set; }

        /// <summary>
        /// Whether this turn's energy has already been granted. Doubles grant an
        /// extra movement roll but never a second energy grant — otherwise one
        /// double snowballs both axes at once (COMBAT_SYSTEMS §3.1).
        /// </summary>
        public bool HasBeenGrantedEnergyThisTurn { get; private set; }

        /// <summary>How many turns this player has taken. Status and cooldown timers are indexed on it.</summary>
        public int TurnIndex { get; private set; }

        internal void SetEnergy(int energy) => Energy = Math.Max(0, energy);

        internal void MarkEnergyGranted() => HasBeenGrantedEnergyThisTurn = true;

        /// <summary>Advances to this player's next turn and re-arms the energy grant.</summary>
        public void BeginTurn()
        {
            TurnIndex++;
            HasBeenGrantedEnergyThisTurn = false;
        }

        public override string ToString() => $"{Color} (energy {Energy}, turn {TurnIndex})";
    }
}