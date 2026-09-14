// Assets/_Project/Scripts/Core/Services/DeferredCellEffects.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>One beacon that fired, and what it did.</summary>
    /// <remarks>
    /// <b>A beacon that hit nobody still reports.</b> Its damage list is empty
    /// and the view says so. Silence would be indistinguishable from a beacon
    /// that was never placed, and the whole ability is a bet the opponent can
    /// see being won or lost.
    /// </remarks>
    public readonly struct CellEffectResolution
    {
        public CellEffectResolution(
            CellRef cell,
            PlayerColor owner,
            int sourceOperatorId,
            IReadOnlyList<OperatorState> caught,
            IReadOnlyList<DamageResult> damage,
            int damagePerTarget)
        {
            Cell = cell;
            Owner = owner;
            SourceOperatorId = sourceOperatorId;
            Caught = caught ?? Array.Empty<OperatorState>();
            Damage = damage ?? Array.Empty<DamageResult>();
            DamagePerTarget = damagePerTarget;
        }

        /// <summary>The painted cell. The blast is centred here.</summary>
        public CellRef Cell { get; }

        /// <summary>The seat that placed it, which is who a kill pays.</summary>
        public PlayerColor Owner { get; }

        /// <summary>The operator that placed it. May be in its yard by now.</summary>
        public int SourceOperatorId { get; }

        /// <summary>Everyone the beam reached, in the order it struck them.</summary>
        public IReadOnlyList<OperatorState> Caught { get; }

        /// <summary>One per entry in <see cref="Caught"/>, same order.</summary>
        public IReadOnlyList<DamageResult> Damage { get; }

        /// <summary>
        /// What each one took before mitigation — the beam's total divided by
        /// how many it caught. Zero when it caught nobody.
        /// </summary>
        public int DamagePerTarget { get; }

        public bool HitSomething => Caught.Count > 0;

        public override string ToString() =>
            HitSomething
                ? $"{Owner} beacon on {Cell} hits {Caught.Count} for {DamagePerTarget} each"
                : $"{Owner} beacon on {Cell} hits nothing";
    }

    /// <summary>
    /// Effects that name a place and a later moment (ADR-0006). Drone Strike's
    /// beacon is the first; mines, zones and timed hazards are the same two
    /// primitives.
    /// </summary>
    /// <remarks>
    /// <b>Anchored to the board, not to a victim.</b> Bleed and marks are also
    /// delayed, but they are statuses the victim carries — they move when the
    /// victim moves and die when it dies. A beacon sits on a cell and strikes
    /// whoever is standing there when it fires, which is what makes the ability
    /// a bet on where someone will be rather than a delayed certainty.
    ///
    /// <b>It resolves damage itself, like <c>CollisionResolver</c>.</b>
    /// <c>StatusRegistry</c> deliberately only reports what a tick is worth,
    /// because the pipeline consults it for mitigation and calling back would
    /// close a cycle. Nothing consults this type, so there is no cycle to avoid
    /// and no reason to push the split arithmetic and the area query up into
    /// <c>TurnStateMachine</c>, which owns ordering and nothing else.
    ///
    /// <b>Firing turns are absolute indices.</b> The third system in the core to
    /// need "later", after status durations and cooldowns, and the third to
    /// represent it the same way. Nothing decrements, so nothing drifts.
    ///
    /// <b>Keyed on cell <i>and</i> owner.</b> Two seats can both field a Kian and
    /// both paint the same cell; each beacon is independent and fires on its own
    /// upkeep. Keying on the cell alone would let a player destroy an opponent's
    /// spent energy by clicking a square, which is griefing nobody designed.
    /// </remarks>
    public sealed class DeferredCellEffects
    {
        /// <summary>The cause recorded on a beacon's damage, for the view (§2.1).</summary>
        private const string BeaconCause = "beacon";

        private sealed class Pending
        {
            public CellRef Cell;
            public PlayerColor Owner;
            public int SourceOperatorId;
            public int FiresOnOwnerTurn;
            public int TotalDamage;
            public int Radius;
            public DamageType DamageType;
        }

        private readonly List<Pending> _pending = new List<Pending>();

        private readonly ITurnClock _clock;
        private readonly TargetingRules _targeting;
        private readonly DamagePipeline _damage;

        public DeferredCellEffects(ITurnClock clock, TargetingRules targeting, DamagePipeline damage)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
        }

        /// <summary>Beacons currently on the board, for the view to draw.</summary>
        /// <remarks>
        /// <b>Not optional.</b> An invisible delayed area strike is a trap rather
        /// than a prediction, and the entire design rests on opponents seeing it
        /// and choosing whether to move. The placement event announces it; this
        /// is how the board keeps showing it for the round it is live, the same
        /// reason <c>StatusRegistry.ActiveKinds</c> exists.
        /// </remarks>
        public IReadOnlyList<CellRef> ActiveBeacons()
        {
            var cells = new List<CellRef>(_pending.Count);
            foreach (var entry in _pending) cells.Add(entry.Cell);

            return cells;
        }

        /// <summary>Whether a seat already has a beacon on this cell.</summary>
        public bool HasBeaconOn(CellRef cell, PlayerColor owner) => Find(cell, owner) != null;

        /// <summary>
        /// Paints a cell. It fires at <paramref name="owner"/>'s next upkeep —
        /// one full round, so every opponent moves once before it lands.
        /// </summary>
        /// <remarks>
        /// <b>Re-painting a cell you already hold tops it up rather than
        /// stacking.</b> Same shape as re-applying a status (§5.2): the entry is
        /// replaced, its firing turn refreshed, and nothing is added. Two beams
        /// on one cell is a different mechanic and is not adopted.
        ///
        /// The cell must be on the outer track — the caller checks that before
        /// paying for the ability, because a home column can be neither reached
        /// into nor out of (§4.3) and a refusal has to cost nothing.
        /// </remarks>
        public void Paint(
            CellRef cell,
            PlayerColor owner,
            int sourceOperatorId,
            int totalDamage,
            int radius,
            DamageType damageType)
        {
            if (!cell.IsOnTrack)
                throw new ArgumentException($"A beacon needs an outer-track cell; got {cell}.", nameof(cell));
            if (owner == PlayerColor.None)
                throw new ArgumentException("A beacon needs a seat to pay its kills.", nameof(owner));
            if (totalDamage < 0) throw new ArgumentOutOfRangeException(nameof(totalDamage));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));

            var entry = Find(cell, owner) ?? NewEntry(cell, owner);

            entry.SourceOperatorId = sourceOperatorId;
            entry.FiresOnOwnerTurn = _clock.TurnIndexOf(owner) + 1;
            entry.TotalDamage = totalDamage;
            entry.Radius = radius;
            entry.DamageType = damageType;
        }

        /// <summary>
        /// Fires every beacon of <paramref name="owner"/> that has come due, and
        /// reports what each one did. Called at that seat's upkeep.
        /// </summary>
        /// <remarks>
        /// <b>The owner's clock, not the victim's.</b> Bleed and marks resolve at
        /// the upkeep of whoever carries them, because they are carried. A beacon
        /// is a device its owner deployed, so it resolves on its owner's turn —
        /// which is also the only arrangement that gives every seat the same
        /// warning regardless of where it sits in the order.
        ///
        /// <b>It fires whatever has happened to the operator that placed it.</b>
        /// Stunned, in a home column, home, or neutralized and sitting in its
        /// yard: none of them stop it. Stun blocks an action phase (§5.1) and
        /// upkeep is not an action; the rest are reachability rules about the
        /// operator, and the beacon is not the operator. A kill still credits
        /// the recorded source, which <c>NeutralizeRules</c> resolves against the
        /// full match roster rather than the track.
        ///
        /// <b>Damage divides and floors, and the remainder is discarded.</b> No
        /// fractional health exists anywhere in the core. A consequence worth
        /// stating plainly: the beam is <i>weakest</i> against a crowd and
        /// strongest against a lone target, so its counterplay is to bunch up —
        /// which is the opposite of what every other area ability on the roster
        /// asks for.
        /// </remarks>
        public IReadOnlyList<CellEffectResolution> Fire(
            PlayerColor owner, IReadOnlyList<OperatorState> allOperators)
        {
            if (allOperators == null) throw new ArgumentNullException(nameof(allOperators));

            int ownerTurn = _clock.TurnIndexOf(owner);
            List<CellEffectResolution> fired = null;

            // Backwards so a removal cannot skip the next entry.
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var entry = _pending[i];

                if (entry.Owner != owner) continue;
                if (ownerTurn < entry.FiresOnOwnerTurn) continue;

                _pending.RemoveAt(i);

                if (fired == null) fired = new List<CellEffectResolution>();
                fired.Add(Resolve(entry, allOperators));
            }

            if (fired == null) return Array.Empty<CellEffectResolution>();

            // Removal walked the list backwards, so the results came out in
            // reverse placement order. The view plays them in the order they
            // were painted.
            fired.Reverse();
            return fired;
        }

        /// <summary>
        /// Removes every beacon a seat holds. For a caller that needs to clear
        /// the board — a match reset, or a future effect that disarms devices.
        /// </summary>
        /// <remarks>
        /// Neutralizing the operator that placed a beacon deliberately does
        /// <b>not</b> call this: a deployed device outlives its operator, and
        /// letting a kill refund the energy already spent would make the ability
        /// worse than it reads (ADR-0006).
        /// </remarks>
        public void ClearFor(PlayerColor owner)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
                if (_pending[i].Owner == owner) _pending.RemoveAt(i);
        }

        // ── Internals ────────────────────────────────────────────────────

        private CellEffectResolution Resolve(Pending entry, IReadOnlyList<OperatorState> allOperators)
        {
            var caught = _targeting.EnemiesInArea(entry.Cell, entry.Radius, entry.Owner, allOperators);

            if (caught.Count == 0)
            {
                return new CellEffectResolution(
                    entry.Cell, entry.Owner, entry.SourceOperatorId, caught, null, 0);
            }

            int each = entry.TotalDamage / caught.Count;   // integer division floors
            var results = new List<DamageResult>(caught.Count);

            foreach (var victim in caught)
            {
                results.Add(_damage.Apply(victim, new DamageInstance(
                    each, entry.DamageType, entry.SourceOperatorId, BeaconCause)));
            }

            return new CellEffectResolution(
                entry.Cell, entry.Owner, entry.SourceOperatorId, caught, results, each);
        }

        private Pending Find(CellRef cell, PlayerColor owner)
        {
            foreach (var entry in _pending)
                if (entry.Owner == owner && entry.Cell == cell) return entry;

            return null;
        }

        private Pending NewEntry(CellRef cell, PlayerColor owner)
        {
            var entry = new Pending { Cell = cell, Owner = owner };
            _pending.Add(entry);
            return entry;
        }
    }
}