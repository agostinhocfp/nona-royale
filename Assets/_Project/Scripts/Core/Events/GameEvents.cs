// Assets/_Project/Scripts/Core/Events/GameEvents.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Events
{
    /// <summary>
    /// Something that happened. The core returns these; the view renders them
    /// and never reads core internals (ADR-0004).
    /// </summary>
    public interface IGameEvent
    {
    }

    /// <summary>A command was refused. Carries why, so the view can say so.</summary>
    public sealed class CommandRejected : IGameEvent
    {
        public CommandRejected(string reason) { Reason = reason; }
        public string Reason { get; }
        public override string ToString() => $"rejected: {Reason}";
    }

    public sealed class TurnBegan : IGameEvent
    {
        public TurnBegan(PlayerColor player, int turnIndex) { Player = player; TurnIndex = turnIndex; }
        public PlayerColor Player { get; }
        public int TurnIndex { get; }
        public override string ToString() => $"{Player} begins turn {TurnIndex}";
    }

    public sealed class DiceRolled : IGameEvent
    {
        public DiceRolled(DiceRoll roll, bool grantsAnotherRoll) { Roll = roll; GrantsAnotherRoll = grantsAnotherRoll; }
        public DiceRoll Roll { get; }
        public bool GrantsAnotherRoll { get; }
        public override string ToString() => $"rolled {Roll}";
    }

    public sealed class EnergyGranted : IGameEvent
    {
        public EnergyGranted(PlayerColor player, int stored, int burned, int total)
        {
            Player = player; Stored = stored; Burned = burned; Total = total;
        }
        public PlayerColor Player { get; }
        public int Stored { get; }

        /// <summary>Destroyed by the cap. Its own field because burn is a design signal, not bookkeeping.</summary>
        public int Burned { get; }
        public int Total { get; }
        public override string ToString() => $"{Player} +{Stored} energy (burned {Burned})";
    }

    public sealed class EnergySpent : IGameEvent
    {
        public EnergySpent(PlayerColor player, int amount, int remaining)
        {
            Player = player; Amount = amount; Remaining = remaining;
        }
        public PlayerColor Player { get; }
        public int Amount { get; }
        public int Remaining { get; }
        public override string ToString() => $"{Player} spends {Amount}";
    }

    public sealed class OperatorDeployed : IGameEvent
    {
        public OperatorDeployed(OperatorState op, CellRef cell) { Operator = op; Cell = cell; }
        public OperatorState Operator { get; }
        public CellRef Cell { get; }
        public override string ToString() => $"{Operator.Name} deploys to {Cell}";
    }

    public sealed class OperatorMoved : IGameEvent
    {
        public OperatorMoved(OperatorState op, int from, int to, CellRef cell)
        {
            Operator = op; From = from; To = to; Cell = cell;
        }
        public OperatorState Operator { get; }
        public int From { get; }
        public int To { get; }
        public CellRef Cell { get; }
        public override string ToString() => $"{Operator.Name} {From} -> {To}";
    }

    public sealed class CollisionResolved : IGameEvent
    {
        public CollisionResolved(OperatorState mover, OperatorState occupant, bool moverBouncedBack)
        {
            Mover = mover; Occupant = occupant; MoverBouncedBack = moverBouncedBack;
        }
        public OperatorState Mover { get; }
        public OperatorState Occupant { get; }
        public bool MoverBouncedBack { get; }
        public override string ToString() =>
            $"{Mover.Name} hits {Occupant.Name}" + (MoverBouncedBack ? " and bounces" : " and takes the cell");
    }

    public sealed class DamageDealt : IGameEvent
    {
        public DamageDealt(OperatorState target, int amount, int remainingHealth)
        {
            Target = target; Amount = amount; RemainingHealth = remainingHealth;
        }
        public OperatorState Target { get; }
        public int Amount { get; }
        public int RemainingHealth { get; }
        public override string ToString() => $"{Target.Name} takes {Amount} ({RemainingHealth} left)";
    }

    public sealed class DamageEvaded : IGameEvent
    {
        public DamageEvaded(OperatorState target) { Target = target; }
        public OperatorState Target { get; }
        public override string ToString() => $"{Target.Name} evades";
    }

    public sealed class DamageAbsorbed : IGameEvent
    {
        public DamageAbsorbed(OperatorState target) { Target = target; }
        public OperatorState Target { get; }
        public override string ToString() => $"{Target.Name}'s shield absorbs it";
    }

    public sealed class HealApplied : IGameEvent
    {
        public HealApplied(OperatorState target, int amount) { Target = target; Amount = amount; }
        public OperatorState Target { get; }
        public int Amount { get; }
        public override string ToString() => $"{Target.Name} heals {Amount}";
    }

    public sealed class StatusApplied : IGameEvent
    {
        public StatusApplied(OperatorState target, StatusKind status, int duration)
        {
            Target = target; Status = status; Duration = duration;
        }
        public OperatorState Target { get; }
        public StatusKind Status { get; }
        public int Duration { get; }
        public override string ToString() => $"{Target.Name} gains {Status}";
    }

    public sealed class StatusExpired : IGameEvent
    {
        public StatusExpired(OperatorState target, StatusKind status) { Target = target; Status = status; }
        public OperatorState Target { get; }
        public StatusKind Status { get; }
        public override string ToString() => $"{Status} expires on {Target.Name}";
    }

    public sealed class OperatorNeutralized : IGameEvent
    {
        public OperatorNeutralized(OperatorState op) { Operator = op; }
        public OperatorState Operator { get; }
        public override string ToString() => $"{Operator.Name} is neutralized";
    }

    public sealed class OperatorReachedHome : IGameEvent
    {
        public OperatorReachedHome(OperatorState op) { Operator = op; }
        public OperatorState Operator { get; }
        public override string ToString() => $"{Operator.Name} is home";
    }

    public sealed class TurnEnded : IGameEvent
    {
        public TurnEnded(PlayerColor player) { Player = player; }
        public PlayerColor Player { get; }
        public override string ToString() => $"{Player} ends turn";
    }

    public sealed class GameWon : IGameEvent
    {
        public GameWon(PlayerColor winner) { Winner = winner; }
        public PlayerColor Winner { get; }
        public override string ToString() => $"{Winner} wins";
    }
}