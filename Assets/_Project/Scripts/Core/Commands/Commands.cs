// Assets/_Project/Scripts/Core/Commands/Commands.cs
namespace NonaRoyale.Core.Commands
{
    /// <summary>Roll the dice. The first roll of a turn also grants energy.</summary>
    public sealed class RollDiceCommand : ICommand
    {
    }

    /// <summary>
    /// Put one operator on its start cell, consuming a die showing the deploy
    /// face. Optional — a declined deploy leaves that die available to move with.
    /// </summary>
    public sealed class DeployCommand : ICommand
    {
        public DeployCommand(int operatorId) { OperatorId = operatorId; }
        public int OperatorId { get; }
    }

    /// <summary>
    /// Move one operator, spending either every unspent die on this roll or a
    /// single named one (COMBAT_SYSTEMS §6).
    /// </summary>
    /// <remarks>
    /// <b><see cref="DieFace"/> null means pool.</b> That is the common case and
    /// the old behaviour, so <c>new MoveCommand(id)</c> still means what it
    /// always meant. Naming a face is how a player splits a roll between two
    /// operators — or spends it on one operator in two separate steps, which is
    /// a different thing because each step lands, and a landing is what triggers
    /// a collision (§7.1).
    ///
    /// <b>The face identifies the die, not a position.</b> On a double the two
    /// dice are indistinguishable; on any other roll the face is unique. There
    /// is nothing an index would disambiguate that a face does not, and a face
    /// is what the player actually clicked.
    ///
    /// Splitting is not free: the floor in <c>CellsFor</c> applies per move, so
    /// two odd dice spent separately arrive one cell short of the same two
    /// pooled. Routing a die through a slower operator costs considerably more
    /// than that. Both are the player's to weigh, which is why
    /// <c>GameEngine.PreviewLandings</c> reports every option before one is
    /// chosen.
    /// </remarks>
    public sealed class MoveCommand : ICommand
    {
        public MoveCommand(int operatorId) : this(operatorId, null) { }

        public MoveCommand(int operatorId, int? dieFace)
        {
            OperatorId = operatorId;
            DieFace = dieFace;
        }

        public int OperatorId { get; }

        /// <summary>The single die to spend, or null to spend all of them at once.</summary>
        public int? DieFace { get; }
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

    /// <summary>
    /// Close the turn and hand over to the next seat. Rejected while an unspent
    /// die still has an operator that could legally move with it (§6).
    /// </summary>
    public sealed class EndTurnCommand : ICommand
    {
    }
}