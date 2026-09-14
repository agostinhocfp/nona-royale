// Assets/_Project/Scripts/Core/Services/TurnPhase.cs
namespace NonaRoyale.Core.Services
{
    /// <summary>Where a turn currently stands (COMBAT_SYSTEMS §6).</summary>
    public enum TurnPhase
    {
        /// <summary>No turn in progress. The next call must be BeginTurn.</summary>
        BetweenTurns = 0,

        /// <summary>Upkeep has run; the player owes a roll.</summary>
        AwaitingRoll = 1,

        /// <summary>Deploy, move and spend energy. The caller owns this window.</summary>
        Action = 2,

        /// <summary>Someone has all their operators home. Nothing further resolves.</summary>
        MatchOver = 3
    }
}