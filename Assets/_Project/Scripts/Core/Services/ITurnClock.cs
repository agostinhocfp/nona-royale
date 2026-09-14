// Assets/_Project/Scripts/Core/Services/ITurnClock.cs
using NonaRoyale.Core.Board;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Whose turn it is, and how many turns each seat has taken.
    /// </summary>
    /// <remarks>
    /// Status durations are counted in <b>the affected operator's own turns</b>,
    /// not in rounds or in the applier's turns (COMBAT_SYSTEMS §5). A stun
    /// applied during an opponent's turn has to survive until the target
    /// actually gets to act — which is only expressible if the registry can ask
    /// what turn that target's owner is on.
    ///
    /// An interface rather than a concrete clock so the registry can be tested
    /// by driving turns by hand, without a turn state machine existing yet.
    /// </remarks>
    public interface ITurnClock
    {
        /// <summary>The seat currently taking its turn.</summary>
        PlayerColor ActivePlayer { get; }

        /// <summary>How many turns that seat has taken. Monotonic, never reset mid-match.</summary>
        int TurnIndexOf(PlayerColor color);
    }
}