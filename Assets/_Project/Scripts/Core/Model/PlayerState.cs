// Assets/_Project/Scripts/Core/Model/PlayerState.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Core.Model
{
    /// <summary>
    /// One seat at the table: its three operators, its energy pool and its debt.
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

        /// <summary>Ids of the operators this seat's debt is owed to (§3.3).</summary>
        private readonly HashSet<int> _creditors = new HashSet<int>();

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

        /// <summary>
        /// Consecutive turns this player ended without the deploy face while
        /// holding at least one operator in the yard. Bad-luck deploy
        /// protection reads it; only <c>GameEngine</c> writes it.
        /// </summary>
        /// <remarks>
        /// <b>It survives <see cref="BeginTurn"/></b>, unlike the energy flag —
        /// a drought is a streak across turns, and resetting it here would make
        /// the mechanic unreachable. It resets on three things only: the deploy
        /// face appearing in any roll, a turn with an empty yard (a turn that
        /// could not have used the deploy was not spent waiting for one), and
        /// the pity deploy itself firing.
        /// </remarks>
        public int DeployDroughtTurns { get; private set; }

        /// <summary>
        /// What this seat owes (§3.3, 2026-09-24). Only <c>EnergyLedger</c>
        /// changes it. Collected from the pool at the end of this seat's turn.
        /// </summary>
        /// <remarks>
        /// <b>One figure per seat, not per operator</b> (designer): it sits
        /// beside the pool it is paid from, so the board carries no marker for
        /// it.
        /// </remarks>
        public int Debt { get; private set; }

        /// <summary>
        /// The operators this debt is owed to. A collision against any of them
        /// by one of this seat's operators burns the whole debt (§3.3).
        /// Emptied whenever the debt reaches zero.
        /// </summary>
        public IReadOnlyCollection<int> Creditors => _creditors;

        /// <summary>Whether this seat owes anything to <paramref name="operatorId"/>.</summary>
        public bool OwesTo(int operatorId) => Debt > 0 && _creditors.Contains(operatorId);

        internal void SetEnergy(int energy) => Energy = Math.Max(0, energy);

        internal void SetDebt(int debt)
        {
            Debt = Math.Max(0, debt);
            if (Debt == 0) _creditors.Clear();
        }

        internal void AddCreditor(int operatorId) => _creditors.Add(operatorId);

        internal void MarkEnergyGranted() => HasBeenGrantedEnergyThisTurn = true;

        internal void RecordDeployDroughtTurn() => DeployDroughtTurns++;

        internal void ResetDeployDrought() => DeployDroughtTurns = 0;

        /// <summary>Advances to this player's next turn and re-arms the energy grant.</summary>
        public void BeginTurn()
        {
            TurnIndex++;
            HasBeenGrantedEnergyThisTurn = false;
        }

        public override string ToString() => Debt > 0
                ? $"{Color} (energy {Energy}, owes {Debt}, turn {TurnIndex})"
                : $"{Color} (energy {Energy}, turn {TurnIndex})";
    }
}