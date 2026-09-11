// Assets/_Project/Scripts/Core/Commands/Commands.cs
namespace NonaRoyale.Core.Commands
{
    /// <summary>Roll the dice. The first roll of a turn also grants energy.</summary>
    public sealed class RollDiceCommand : ICommand
    {
    }

    /// <summary>
    /// Put one operator on its start cell, consuming a die showing the deploy
    /// face. Optional — declining keeps the full total as movement.
    /// </summary>
    public sealed class DeployCommand : ICommand
    {
        public DeployCommand(int operatorId) { OperatorId = operatorId; }
        public int OperatorId { get; }
    }

    /// <summary>Move one operator with the movement left on this roll. Exactly one per roll.</summary>
    public sealed class MoveCommand : ICommand
    {
        public MoveCommand(int operatorId) { OperatorId = operatorId; }
        public int OperatorId { get; }
    }

    /// <summary>
    /// Spend energy on an ability. <see cref="TargetOperatorId"/> is null for
    /// self-origin area abilities, which need no chosen target.
    /// </summary>
    public sealed class UseAbilityCommand : ICommand
    {
        public UseAbilityCommand(int casterOperatorId, int abilityId, int? targetOperatorId = null)
        {
            CasterOperatorId = casterOperatorId;
            AbilityId = abilityId;
            TargetOperatorId = targetOperatorId;
        }

        public int CasterOperatorId { get; }
        public int AbilityId { get; }
        public int? TargetOperatorId { get; }
    }

    /// <summary>Close the turn and hand over to the next seat.</summary>
    public sealed class EndTurnCommand : ICommand
    {
    }
}