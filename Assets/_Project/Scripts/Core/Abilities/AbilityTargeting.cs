// Assets/_Project/Scripts/Core/Abilities/AbilityTargeting.cs
namespace NonaRoyale.Core.Abilities
{
    /// <summary>What a player has to pick before an ability can be cast.</summary>
    /// <remarks>
    /// <b>This replaced a bool.</b> <c>RequiresTarget</c> answered "operator or
    /// nothing", which covered every ability until Drone Strike named a
    /// <i>cell</i> (ADR-0006). A second bool beside the first would have allowed
    /// a combination that means nothing — targets an operator and a cell — so the
    /// two states became three.
    ///
    /// <c>AbilityDefinition.RequiresTarget</c> survives as a computed property,
    /// so every reader still works unchanged; only the four abilities that
    /// declared it explicitly had to move.
    /// </remarks>
    public enum AbilityTargeting
    {
        /// <summary>One operator, ally or enemy. The default and the common case.</summary>
        Operator = 0,

        /// <summary>Nothing. Self-origin areas — Ace Shards, Dargin Pulse, both of Kian's first two.</summary>
        None = 1,

        /// <summary>
        /// One cell of the outer track, occupied or not. Drone Strike (ADR-0006).
        /// </summary>
        /// <remarks>
        /// The cell need not hold anybody. Painting empty ground and being right
        /// about it a round later is the whole ability.
        /// </remarks>
        Cell = 2
    }
}