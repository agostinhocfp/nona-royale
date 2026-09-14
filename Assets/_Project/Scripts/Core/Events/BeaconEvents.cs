// Assets/_Project/Scripts/Core/Events/BeaconEvents.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Events
{
    /// <summary>
    /// A beacon was painted on a cell. It fires at its owner's next upkeep
    /// (ADR-0006).
    /// </summary>
    /// <remarks>
    /// <b>The view must draw this, and keep drawing it.</b> An unannounced
    /// delayed area strike is a trap; the entire design rests on opponents
    /// seeing the mark and choosing whether to move off it. The event announces
    /// the placement, and <c>GameEngine.ActiveBeacons</c> is how the board keeps
    /// showing it for the round it is live — the same split as
    /// <c>StatusApplied</c> and <c>ActiveStatusesOn</c>.
    ///
    /// <b>It carries the caster, who may be dead by the time it fires.</b> A
    /// deployed device outlives its operator, so the piece shown placing it is
    /// not necessarily on the board when <see cref="BeaconFired"/> arrives.
    /// </remarks>
    public sealed class BeaconPlaced : IGameEvent
    {
        public BeaconPlaced(OperatorState caster, CellRef cell, int totalDamage)
        {
            Caster = caster;
            Cell = cell;
            TotalDamage = totalDamage;
        }

        public OperatorState Caster { get; }
        public CellRef Cell { get; }

        /// <summary>What the beam will divide among everyone it catches.</summary>
        public int TotalDamage { get; }

        public override string ToString() => $"{Caster.Name} paints {Cell}";
    }

    /// <summary>A beacon resolved. It may have caught nobody.</summary>
    /// <remarks>
    /// <b>A miss is reported as loudly as a hit.</b> Silence would be
    /// indistinguishable from a beacon that was never placed, and a player who
    /// spent six energy and guessed wrong is entitled to see that happen. The
    /// individual damage lands as ordinary <c>DamageDealt</c>, <c>DamageEvaded</c>
    /// and <c>DamageAbsorbed</c> events — the beam is Normal damage and goes
    /// through the same mitigation as everything else.
    /// </remarks>
    public sealed class BeaconFired : IGameEvent
    {
        public BeaconFired(PlayerColor owner, CellRef cell, int caught, int damagePerTarget)
        {
            Owner = owner;
            Cell = cell;
            Caught = caught;
            DamagePerTarget = damagePerTarget;
        }

        public PlayerColor Owner { get; }
        public CellRef Cell { get; }

        /// <summary>How many operators the beam reached. Zero is a real outcome.</summary>
        public int Caught { get; }

        /// <summary>What each of them took before mitigation. Zero when it caught nobody.</summary>
        public int DamagePerTarget { get; }

        public override string ToString() =>
            Caught == 0
                ? $"{Owner}'s beacon fires on {Cell} and hits nothing"
                : $"{Owner}'s beacon fires on {Cell}, {Caught} caught for {DamagePerTarget} each";
    }
}