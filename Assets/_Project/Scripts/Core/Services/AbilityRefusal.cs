// Assets/_Project/Scripts/Core/Services/AbilityRefusal.cs
namespace NonaRoyale.Core.Services
{
    /// <summary>Why an ability did not go off. A reason, because the view must explain itself.</summary>
    public enum AbilityRefusal
    {
        None = 0,
        OnCooldown = 1,
        InsufficientEnergy = 2,
        IllegalTarget = 3,
        NoTarget = 4,

        /// <summary>Stunned operators cannot move or spend energy (§5.1).</summary>
        CasterStunned = 5,

        /// <summary>In a home column, a yard, or home — out of the fight (§4.3).</summary>
        CasterOutOfPlay = 6
    }
}