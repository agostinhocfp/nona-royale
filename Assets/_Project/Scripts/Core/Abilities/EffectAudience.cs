// Assets/_Project/Scripts/Core/Abilities/EffectAudience.cs
namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Which side an effect applies to when the ability can be aimed at either.
    /// </summary>
    /// <remarks>
    /// Velvet Rope pulls anyone but only damages enemies; All-In Mauling damages
    /// an enemy or heals an ally. Rather than branching in the resolver, both
    /// abilities list every effect they could have and each effect declares who
    /// it is for.
    /// </remarks>
    public enum EffectAudience
    {
        Any = 0,
        EnemyOnly = 1,
        AllyOnly = 2
    }
}