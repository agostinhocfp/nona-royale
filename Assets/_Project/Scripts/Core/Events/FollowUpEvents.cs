// Assets/_Project/Scripts/Core/Events/FollowUpEvents.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Events
{
    /// <summary>
    /// A follow-up strike was set on an operator. It resolves at its owner's
    /// next upkeep, and lands only if the caster is still close enough (§6.5).
    /// </summary>
    /// <remarks>
    /// <b>The telegraph the counterplay depends on.</b> A follow-up is a threat
    /// the target can walk away from, and a threat nobody announced cannot be
    /// walked away from. The <c>Hunted</c> marker keeps showing it on the board
    /// afterwards — the same split as <c>ZeroDayAttached</c> and its marker.
    /// </remarks>
    public sealed class FollowUpMarked : IGameEvent
    {
        public FollowUpMarked(OperatorState caster, OperatorState target)
        {
            Caster = caster;
            Target = target;
        }

        /// <summary>Who set it. The strike is measured from this operator.</summary>
        public OperatorState Caster { get; }

        /// <summary>Who is now being hunted.</summary>
        public OperatorState Target { get; }

        public override string ToString() => $"{Caster.Name} marks {Target.Name} for a follow-up";
    }

    /// <summary>
    /// A follow-up strike resolved: it landed, or the target had got clear.
    /// A miss is reported as loudly as a hit, the beacon precedent.
    /// </summary>
    /// <remarks>
    /// The damage itself lands as an ordinary <c>DamageDealt</c>,
    /// <c>DamageEvaded</c> or <c>DamageAbsorbed</c> event, with the cause
    /// <c>"follow-up"</c>. A cleansed follow-up produces nothing at all — the
    /// cleanse already announced itself.
    /// </remarks>
    public sealed class FollowUpResolved : IGameEvent
    {
        public FollowUpResolved(
            PlayerColor owner, int sourceOperatorId, OperatorState target, CellRef cell,
            bool landed, int damage, int heavyBonus)
        {
            Owner = owner;
            SourceOperatorId = sourceOperatorId;
            Target = target;
            Cell = cell;
            Landed = landed;
            Damage = damage;
            HeavyBonus = heavyBonus;
        }

        public PlayerColor Owner { get; }

        /// <summary>The operator that set the strike.</summary>
        public int SourceOperatorId { get; }

        /// <summary>The hunted operator — struck, or the one that got away.</summary>
        public OperatorState Target { get; }

        /// <summary>Where the target stood when the strike resolved, or where it fell.</summary>
        public CellRef Cell { get; }

        /// <summary>False when the caster was out of reach, off the board, or the target was already down.</summary>
        public bool Landed { get; }

        /// <summary>The strike's base damage, before mitigation.</summary>
        public int Damage { get; }

        /// <summary>The extra it dealt for a heavy target. Zero on a miss.</summary>
        public int HeavyBonus { get; }

        public override string ToString() =>
            Landed
                ? $"{Owner}'s follow-up lands on {Target?.Name} for {Damage + HeavyBonus}"
                : $"{Target?.Name} slips {Owner}'s follow-up";
    }
}
