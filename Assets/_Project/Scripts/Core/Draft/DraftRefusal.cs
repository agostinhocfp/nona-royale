// Assets/_Project/Scripts/Core/Draft/DraftRefusal.cs
namespace NonaRoyale.Core.Draft
{
    /// <summary>
    /// Why a draft action was refused, or <see cref="None"/> if it was allowed.
    /// The draft screen greys a card out with this reason; it never works the
    /// reason out for itself.
    /// </summary>
    public enum DraftRefusal
    {
        None = 0,

        /// <summary>Every slot is filled, or the clock has run out.</summary>
        Complete,

        /// <summary>Snake only: another seat holds the pick.</summary>
        NotYourTurn,

        /// <summary>This seat already fields that operator (GDD §2.2).</summary>
        AlreadyInSquad,

        /// <summary>This seat's three slots are all filled.</summary>
        SquadFull,

        /// <summary>The operator is not in this draft's pool.</summary>
        NotInPool,

        /// <summary>The action exists in the other mode only (clearing a slot, undo).</summary>
        WrongMode,

        /// <summary>Clearing a slot that is already empty, or undo with nothing to undo.</summary>
        NothingToRevert
    }
}