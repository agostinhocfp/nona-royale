// Assets/_Project/Scripts/Core/Events/FieldEvents.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Events
{
    /// <summary>
    /// A self-anchored field was projected onto an operator. It first bills at
    /// its owner's next upkeep, and keeps billing while its marker stands
    /// (§6.6). Mimi's Cryo Field.
    /// </summary>
    /// <remarks>
    /// <b>The telegraph the zoning depends on.</b> A field the opponent cannot
    /// see is a trap rather than ground to play around — the same split as
    /// <c>ZeroDayAttached</c> and its marker. The <c>CryoField</c> badge keeps
    /// showing it on the board afterwards.
    /// </remarks>
    public sealed class FieldProjected : IGameEvent
    {
        public FieldProjected(OperatorState holder)
        {
            Holder = holder;
        }

        /// <summary>Who is now carrying the field. It is centred on her, and follows her.</summary>
        public OperatorState Holder { get; }

        public override string ToString() => $"{Holder.Name} raises a field";
    }

    /// <summary>
    /// A field billed its upkeep tick. It may have caught nobody — a tick on
    /// empty ground is reported as loudly as a hit, exactly as a beacon's miss
    /// is, because being stood outside it is the counterplay working.
    /// </summary>
    /// <remarks>
    /// The damage itself lands as ordinary <c>DamageDealt</c>,
    /// <c>DamageEvaded</c> or <c>DamageAbsorbed</c> events with the cause
    /// <c>"cryo-field"</c> — the tick is Normal damage through the same
    /// pipeline as everything else. A cleansed or expired field produces
    /// nothing at all; its ending was already announced by what removed it.
    /// </remarks>
    public sealed class FieldTicked : IGameEvent
    {
        public FieldTicked(PlayerColor owner, CellRef cell, int caught, int damagePerTarget)
        {
            Owner = owner;
            Cell = cell;
            Caught = caught;
            DamagePerTarget = damagePerTarget;
        }

        public PlayerColor Owner { get; }

        /// <summary>Where the field billed from — the holder's current cell, not her cast cell.</summary>
        public CellRef Cell { get; }

        public int Caught { get; }
        public int DamagePerTarget { get; }

        public override string ToString() =>
            $"{Owner} field ticks on {Cell}, catching {Caught} for {DamagePerTarget}";
    }
}
