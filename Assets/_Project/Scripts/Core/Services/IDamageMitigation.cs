// Assets/_Project/Scripts/Core/Services/IDamageMitigation.cs
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// The mitigation layers the pipeline consults before applying Normal
    /// damage. <c>StatusRegistry</c> will implement this once statuses exist;
    /// until then <see cref="NoMitigation"/> stands in.
    /// </summary>
    /// <remarks>
    /// Behind an interface so the pipeline's order is fixed now and stays fixed:
    /// evasion, then shield, then apply. Adding a mitigation layer later is a
    /// change here, never a change to the order of §2.1.
    /// </remarks>
    public interface IDamageMitigation
    {
        /// <summary>
        /// Attempts to evade. Consumes the round's charge on success.
        /// Resolved <i>before</i> shield deliberately: evasion is a reflex and
        /// should not burn a consumable the operator may still need.
        /// </summary>
        bool TryEvade(OperatorState target, IRandom random);

        /// <summary>Attempts to absorb the whole instance with a shield, consuming it.</summary>
        bool TryAbsorb(OperatorState target);
    }
} 