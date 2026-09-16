// Assets/_Project/Scripts/Core/Events/WatchEvents.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Events
{
    /// <summary>
    /// A watch was set on an operator. If it moves by dice before its owner's
    /// next upkeep, the watch trips and strikes it, once; otherwise it lapses
    /// (§6.7).
    /// </summary>
    /// <remarks>
    /// <b>The telegraph the counterplay depends on.</b> A watch is a threat the
    /// target answers by standing still, and a threat nobody announced cannot
    /// be stood still against. The <c>Watched</c> marker keeps showing it on
    /// the board afterwards — the same split as <c>FollowUpMarked</c> and its
    /// marker, with the counterplay inverted.
    /// </remarks>
    public sealed class WatchMarked : IGameEvent
    {
        public WatchMarked(OperatorState caster, OperatorState target)
        {
            Caster = caster;
            Target = target;
        }

        /// <summary>Who set the watch. The strike is credited to this operator.</summary>
        public OperatorState Caster { get; }

        /// <summary>Who is now being watched.</summary>
        public OperatorState Target { get; }

        public override string ToString() => $"{Caster.Name} reads {Target.Name}'s next move";
    }

    /// <summary>
    /// A watch tripped: the watched operator moved by dice while the marker
    /// stood, and took the strike (§6.7).
    /// </summary>
    /// <remarks>
    /// The damage itself lands as an ordinary <c>DamageDealt</c>,
    /// <c>DamageEvaded</c> or <c>DamageAbsorbed</c> event, with the cause
    /// <c>"watch"</c>. There is no miss event: a watch the target never springs
    /// lapses silently at its owner's upkeep — nothing happened, and the
    /// disappearing badge is the whole announcement. A cleansed watch produces
    /// nothing at all — the cleanse already announced itself.
    /// </remarks>
    public sealed class WatchTripped : IGameEvent
    {
        public WatchTripped(
            PlayerColor owner, int sourceOperatorId, OperatorState target, CellRef cell, int damage)
        {
            Owner = owner;
            SourceOperatorId = sourceOperatorId;
            Target = target;
            Cell = cell;
            Damage = damage;
        }

        public PlayerColor Owner { get; }

        /// <summary>The operator that set the watch. May be in its yard by now (§6.7).</summary>
        public int SourceOperatorId { get; }

        /// <summary>The watched operator, struck for moving.</summary>
        public OperatorState Target { get; }

        /// <summary>Where the target stood when the watch tripped — its landing cell.</summary>
        public CellRef Cell { get; }

        /// <summary>The strike's damage, before mitigation.</summary>
        public int Damage { get; }

        public override string ToString() =>
            $"{Owner}'s watch trips on {Target?.Name} for {Damage}";
    }
}
