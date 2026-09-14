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

    /// <summary>
    /// A lingering zone was deployed on a cell. It detonates at its owner's next
    /// upkeep and holds the ground afterwards (ADR-0007).
    /// </summary>
    public sealed class ZoneDeployed : IGameEvent
    {
        public ZoneDeployed(OperatorState caster, CellRef cell, int detonationDamage)
        {
            Caster = caster;
            Cell = cell;
            DetonationDamage = detonationDamage;
        }

        public OperatorState Caster { get; }
        public CellRef Cell { get; }

        /// <summary>What each enemy caught by the detonation takes. Not divided.</summary>
        public int DetonationDamage { get; }

        public override string ToString() => $"{Caster.Name} deploys a killzone on {Cell}";
    }

    /// <summary>
    /// A zone resolved — either its detonation or one of its lingering ticks.
    /// </summary>
    /// <remarks>
    /// <b>The two read differently and the view has to tell them apart.</b> A
    /// detonation is a blast that stuns; a lingering tick is ground doing its
    /// work, and it stuns nobody. Reporting both as one event would make the
    /// second look like a bug the first time a player was not stunned again.
    /// </remarks>
    public sealed class ZoneTicked : IGameEvent
    {
        public ZoneTicked(
            PlayerColor owner, CellRef cell, bool isDetonation, int caught, int damagePerTarget)
        {
            Owner = owner;
            Cell = cell;
            IsDetonation = isDetonation;
            Caught = caught;
            DamagePerTarget = damagePerTarget;
        }

        public PlayerColor Owner { get; }
        public CellRef Cell { get; }

        /// <summary>True for the blast, false for the ground afterwards.</summary>
        public bool IsDetonation { get; }

        /// <summary>How many the zone reached. Zero is a real outcome.</summary>
        public int Caught { get; }

        /// <summary>What each of them took before mitigation.</summary>
        public int DamagePerTarget { get; }

        public override string ToString()
        {
            string what = IsDetonation ? "detonates" : "lingers";

            return Caught == 0
                ? $"{Owner}'s killzone {what} on {Cell} and catches nobody"
                : $"{Owner}'s killzone {what} on {Cell}, {Caught} caught for {DamagePerTarget} each";
        }
    }
}