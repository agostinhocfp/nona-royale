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

    /// <summary>
    /// Bad-luck deploy protection fired: the turn ended as its player's
    /// <see cref="DroughtTurns"/>-th straight eligible turn without the deploy
    /// face, so a yard operator walks on free.
    /// </summary>
    public sealed class OperatorPityDeployed : IGameEvent
    {
        public OperatorPityDeployed(OperatorState op, CellRef cell, int droughtTurns)
        {
            Operator = op;
            Cell = cell;
            DroughtTurns = droughtTurns;
        }

        public OperatorState Operator { get; }
        public CellRef Cell { get; }

        /// <summary>Consecutive eligible turns without the deploy face that triggered this.</summary>
        public int DroughtTurns { get; }

        public override string ToString() =>
            $"{Operator.Name} deploys to {Cell} after {DroughtTurns} turns without a deploy face";
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

    /// <summary>Health was lost. Carries what took it.</summary>
    /// <remarks>
    /// <b>The cause is presentation, never a rule.</b> Nothing in the core reads
    /// it. It exists because upkeep damage resolves in a phase where nothing else
    /// moves: a bleed or mark tick drops an operator's health with no visible
    /// agent on the board, so without a stated cause the player is left to infer
    /// one (<c>PRESENTATION.md</c> §2).
    ///
    /// It is optional so a caller with nothing useful to say can omit it rather
    /// than invent a label.
    /// </remarks>
    public sealed class DamageDealt : IGameEvent
    {
        public DamageDealt(OperatorState target, int amount, int remainingHealth, string cause = null)
        {
            Target = target; Amount = amount; RemainingHealth = remainingHealth; Cause = cause;
        }
        public OperatorState Target { get; }
        public int Amount { get; }
        public int RemainingHealth { get; }

        /// <summary>"bleed", "mark", "collision", "ability", "execute", "self", or null.</summary>
        public string Cause { get; }

        public override string ToString() =>
            Cause == null
                ? $"{Target.Name} takes {Amount} ({RemainingHealth} left)"
                : $"{Target.Name} takes {Amount} from {Cause} ({RemainingHealth} left)";
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

    /// <summary>An operator reached zero health and went to its yard (§1.2). Carries what finished it.</summary>
    /// <remarks>
    /// The most consequential event in the game, and until now it did not say why
    /// it happened. A kill at upkeep is the case that forced this: the piece
    /// simply disappears from the track, and nothing on screen accounts for it.
    /// </remarks>
    public sealed class OperatorNeutralized : IGameEvent
    {
        public OperatorNeutralized(OperatorState op, string cause = null)
        {
            Operator = op; Cause = cause;
        }
        public OperatorState Operator { get; }

        /// <summary>"bleed", "mark", "collision", "ability", "execute", "self", or null.</summary>
        public string Cause { get; }

        public override string ToString() =>
            Cause == null
                ? $"{Operator.Name} is neutralized"
                : $"{Operator.Name} is neutralized by {Cause}";
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