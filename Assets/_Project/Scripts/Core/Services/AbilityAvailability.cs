// Assets/_Project/Scripts/Core/Services/AbilityAvailability.cs
namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Whether an ability can be used right now, and why not if it cannot.
    /// </summary>
    /// <remarks>
    /// A sibling to <see cref="AbilityRefusal"/>, and deliberately not the same
    /// type. <c>AbilityRefusal</c> explains a cast that was <i>attempted</i> and
    /// turned away, including reasons that depend on the chosen target. This
    /// answers the question a tray asks before anything is attempted, and covers
    /// only what is true of the caster and the ability.
    ///
    /// Merging them would mean the tray displaying states like
    /// <c>IllegalTarget</c> and <c>NoTarget</c> that cannot be evaluated until a
    /// target exists.
    /// </remarks>
    public enum AbilityAvailability
    {
        Ready = 0,

        /// <summary>Used too recently. Cooldowns count in the caster's own turns (§10).</summary>
        OnCooldown = 1,

        /// <summary>The shared pool cannot cover the cost (§3.2).</summary>
        InsufficientEnergy = 2,

        /// <summary>A stunned operator cannot spend energy (§5.1).</summary>
        CasterStunned = 3,

        /// <summary>In a yard, a home column, or home — out of the fight (§4.3).</summary>
        CasterOutOfPlay = 4,

        /// <summary>
        /// The seat is not holding the unspent dice the ability deals with
        /// (§6.8). Fortuna's Boxcars needs both; Deal Again needs one.
        /// </summary>
        /// <remarks>
        /// The first availability that depends on the roll rather than on the
        /// caster, and the reason it exists here rather than as a refusal: a tray
        /// can grey Boxcars out the moment the first die is spent, and a bot can
        /// stop proposing it without having to be refused first.
        /// </remarks>
        DiceNotHeld = 5
    }
}