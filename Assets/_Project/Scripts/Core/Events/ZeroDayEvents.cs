// Assets/_Project/Scripts/Core/Events/ZeroDayEvents.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Events
{
    /// <summary>
    /// A charge was attached to an operator. It detonates at its owner's next
    /// upkeep, on whatever cell the target then occupies (§6.4).
    /// </summary>
    /// <remarks>
    /// <b>The telegraph the ability is balanced around.</b> An attached grenade
    /// the opponent cannot see is a trap rather than a delayed certainty, and
    /// the two counterplay features — this announcement and the cleanse —
    /// are what pay for the charge following its victim. The
    /// <c>ZeroDayCharge</c> marker keeps showing it on the board afterwards,
    /// the same split as <c>BeaconPlaced</c> and <c>ActiveBeacons</c>.
    /// </remarks>
    public sealed class ZeroDayAttached : IGameEvent
    {
        public ZeroDayAttached(OperatorState caster, OperatorState target)
        {
            Caster = caster;
            Target = target;
        }

        /// <summary>Who threw it. May be dead by the time it detonates.</summary>
        public OperatorState Caster { get; }

        /// <summary>Who is now carrying it.</summary>
        public OperatorState Target { get; }

        public override string ToString() => $"{Caster.Name}'s charge snaps onto {Target.Name}";
    }

    /// <summary>
    /// An attached charge detonated. It may have caught nobody — a miss is
    /// reported as loudly as a hit, exactly as a beacon's is.
    /// </summary>
    /// <remarks>
    /// The individual damage lands as ordinary <c>DamageDealt</c>,
    /// <c>DamageEvaded</c> and <c>DamageAbsorbed</c> events, and the slow as
    /// <c>StatusApplied</c> — the blast is Normal damage through the same
    /// pipeline as everything else.
    /// </remarks>
    public sealed class ZeroDayDetonated : IGameEvent
    {
        public ZeroDayDetonated(
            PlayerColor owner, CellRef cell, int caught, int damagePerTarget, int markedTargetBonus)
        {
            Owner = owner;
            Cell = cell;
            Caught = caught;
            DamagePerTarget = damagePerTarget;
            MarkedTargetBonus = markedTargetBonus;
        }

        public PlayerColor Owner { get; }

        /// <summary>Where it went off — the target's current cell, or its death cell.</summary>
        public CellRef Cell { get; }

        /// <summary>How many enemies the blast reached. Zero is a real outcome.</summary>
        public int Caught { get; }

        /// <summary>What each of them took before mitigation, before the marked target's bonus.</summary>
        public int DamagePerTarget { get; }

        /// <summary>
        /// The extra the marked target took. Zero when the target died before
        /// detonation — the bonus had no living recipient (§6.4).
        /// </summary>
        public int MarkedTargetBonus { get; }

        public override string ToString() =>
            Caught == 0
                ? $"{Owner}'s charge detonates on {Cell} and hits nothing"
                : $"{Owner}'s charge detonates on {Cell}, {Caught} caught for {DamagePerTarget} each";
    }
}
