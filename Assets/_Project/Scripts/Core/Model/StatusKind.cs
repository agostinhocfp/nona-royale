// Assets/_Project/Scripts/Core/Model/StatusKind.cs
namespace NonaRoyale.Core.Model
{
    /// <summary>
    /// Every status the alpha roster can put on an operator (COMBAT_SYSTEMS §5).
    /// </summary>
    /// <remarks>
    /// Passives live here too. <see cref="Evasion"/> is not applied by an
    /// ability — Kurbyn simply has it — but modelling it as a permanent status
    /// means "passives stay live through stun" (§5.1) is one rule rather than a
    /// special case, and the per-round charge has somewhere to live.
    /// </remarks>
    public enum StatusKind
    {
        /// <summary>Cannot move or spend energy on its next turn. Passives keep working.</summary>
        Stun = 0,

        /// <summary>Speed multiplier reduced. Sources do not stack; the largest applies.</summary>
        Slow = 1,

        /// <summary>Delayed Atomic damage, one stack at a time, at the holder's upkeep. Stacks add.</summary>
        Bleed = 2,

        /// <summary>Cannot be selected as a single target by an enemy. Everything else still reaches it.</summary>
        Stealth = 3,

        /// <summary>Permanent passive. The first Normal instance each round may be negated.</summary>
        Evasion = 4,

        /// <summary>Absorbs one whole instance of Normal damage, then expires.</summary>
        Shield = 5,

        /// <summary>Bookkeeping only. Applies no modifier; read by Tagged From Above's payout.</summary>
        Mark = 6
    }
}