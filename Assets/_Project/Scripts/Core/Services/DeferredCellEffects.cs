// Assets/_Project/Scripts/Core/Services/DeferredCellEffects.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>One pending cell effect that resolved, and what it did.</summary>
    /// <remarks>
    /// <b>An effect that hit nobody still reports.</b> Its damage list is empty
    /// and the view says so. Silence would be indistinguishable from an effect
    /// that was never placed, and both of these abilities are bets the opponent
    /// can see being won or lost.
    /// </remarks>
    public readonly struct CellEffectResolution
    {
        public CellEffectResolution(
            CellRef cell,
            PlayerColor owner,
            int sourceOperatorId,
            string cause,
            bool isDetonation,
            IReadOnlyList<OperatorState> caught,
            IReadOnlyList<DamageResult> damage,
            int damagePerTarget,
            IReadOnlyList<OperatorState> stunned)
        {
            Cell = cell;
            Owner = owner;
            SourceOperatorId = sourceOperatorId;
            Cause = cause;
            IsDetonation = isDetonation;
            Caught = caught ?? Array.Empty<OperatorState>();
            Damage = damage ?? Array.Empty<DamageResult>();
            DamagePerTarget = damagePerTarget;
            Stunned = stunned ?? Array.Empty<OperatorState>();
        }

        /// <summary>The anchored cell. The area is centred here.</summary>
        public CellRef Cell { get; }

        /// <summary>The seat that placed it, which is who a kill pays.</summary>
        public PlayerColor Owner { get; }

        /// <summary>The operator that placed it. May be in its yard by now.</summary>
        public int SourceOperatorId { get; }

        /// <summary>"beacon" or "killzone", for the view and for a kill's label.</summary>
        public string Cause { get; }

        /// <summary>
        /// True on a zone's first resolution, which is the only one that applies
        /// its status. Always true for a beacon, which resolves once.
        /// </summary>
        public bool IsDetonation { get; }

        /// <summary>Everyone the effect reached, in the order it struck them.</summary>
        public IReadOnlyList<OperatorState> Caught { get; }

        /// <summary>One per entry in <see cref="Caught"/>, same order.</summary>
        public IReadOnlyList<DamageResult> Damage { get; }

        /// <summary>
        /// What each one took before mitigation. A beacon divides its total among
        /// those it caught; a zone bills each in full. Zero when it caught nobody.
        /// </summary>
        public int DamagePerTarget { get; }

        /// <summary>Operators the detonation stunned. Empty for a beacon or a lingering tick.</summary>
        public IReadOnlyList<OperatorState> Stunned { get; }

        public bool HitSomething => Caught.Count > 0;

        public override string ToString() =>
            HitSomething
                ? $"{Owner} {Cause} on {Cell} hits {Caught.Count} for {DamagePerTarget} each"
                : $"{Owner} {Cause} on {Cell} hits nothing";
    }

    /// <summary>
    /// Effects that name a place and a later moment (ADR-0006), and effects that
    /// name a place and hold it (ADR-0007). Drone Strike's beacon and Killzone's
    /// grenade are the two shapes; mines, traps and timed hazards are the same
    /// two primitives.
    /// </summary>
    /// <remarks>
    /// <b>Anchored to the board, not to a victim.</b> Bleed and marks are also
    /// delayed, but they are statuses the victim carries — they move when the
    /// victim moves and die when it dies. These sit on a cell and strike whoever
    /// is standing there when they resolve, which is what makes both abilities a
    /// bet on where somebody will be rather than a delayed certainty.
    ///
    /// <b>It resolves damage and status itself, like <c>CollisionResolver</c>.</b>
    /// <c>StatusRegistry</c> deliberately only reports what a tick is worth,
    /// because the pipeline consults it for mitigation and calling back would
    /// close a cycle. Nothing consults this type, so there is no cycle to avoid
    /// and no reason to push the area query and the split arithmetic up into
    /// <c>TurnStateMachine</c>, which owns ordering and nothing else.
    ///
    /// <b>Turn indices are absolute.</b> The third and fourth systems in the core
    /// to need "later", after status durations and cooldowns, and they represent
    /// it the same way. Nothing decrements, so nothing drifts.
    ///
    /// <b>Keyed on cell <i>and</i> owner.</b> Two seats can both paint or deploy
    /// on the same cell; each is independent and resolves on its own upkeep.
    /// Keying on the cell alone would let a player destroy an opponent's spent
    /// energy by clicking a square, which is griefing nobody designed.
    ///
    /// <b>A beacon and a zone are one entry type with different settings</b>
    /// rather than two stores. They share everything that is hard — the
    /// anchoring, the owner-relative clock, the area query, the kill plumbing —
    /// and differ only in how long they last, whether damage splits, and whether
    /// a status lands. Two parallel registries would have duplicated the hard
    /// part to avoid duplicating the easy one.
    /// </remarks>
    public sealed class DeferredCellEffects
    {
        /// <summary>Causes recorded on the damage these produce, for the view (§2.1).</summary>
        private const string BeaconCause = "beacon";
        private const string ZoneCause = "killzone";

        private sealed class Pending
        {
            public CellRef Cell;
            public PlayerColor Owner;
            public int SourceOperatorId;
            public int ResolvesOnOwnerTurn;
            public int TicksRemaining;
            public bool HasDetonated;

            public int Radius;
            public DamageType DamageType;

            /// <summary>Dealt on the first resolution. A beacon's whole payload.</summary>
            public int DetonationDamage;

            /// <summary>Dealt on every resolution after the first. Zero for a beacon.</summary>
            public int LingerDamage;

            /// <summary>True for a beacon: the payload divides among those caught.</summary>
            public bool SplitsDamage;

            /// <summary>Applied on the detonation only. Null for a beacon.</summary>
            public StatusKind? DetonationStatus;
            public int DetonationStatusDuration;

            public bool IsZone => LingerDamage > 0 || DetonationStatus != null || TicksRemaining > 1;
            public string Cause => SplitsDamage ? BeaconCause : ZoneCause;
        }

        private readonly List<Pending> _pending = new List<Pending>();

        private readonly ITurnClock _clock;
        private readonly TargetingRules _targeting;
        private readonly DamagePipeline _damage;
        private readonly StatusRegistry _statuses;

        public DeferredCellEffects(
            ITurnClock clock,
            TargetingRules targeting,
            DamagePipeline damage,
            StatusRegistry statuses)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
        }

        // ── Queries, for the view and for Killzone's rider ────────────────

        /// <summary>Cells holding a beacon right now.</summary>
        /// <remarks>
        /// <b>Not optional.</b> An invisible delayed strike is a trap rather than
        /// a prediction, and both abilities rest on opponents seeing the mark and
        /// choosing. The placement event announces it once; this is how the board
        /// keeps showing it, the same reason <c>StatusRegistry.ActiveKinds</c>
        /// exists.
        /// </remarks>
        public IReadOnlyList<CellRef> ActiveBeacons() => CellsWhere(false);

        /// <summary>Cells holding a lingering zone right now. Drawn differently from a beacon.</summary>
        public IReadOnlyList<CellRef> ActiveZones() => CellsWhere(true);

        /// <summary>Whether a seat has any lingering zone in play (ADR-0007).</summary>
        /// <remarks>
        /// Read by Killzone's rider on Bio-Link Rage — the first case of one
        /// ability's numbers depending on another's board state.
        ///
        /// <b>Compare <see cref="ZoneCoversOperator"/>.</b> This asks only whether
        /// a zone exists anywhere, which makes the rider an unconditional bonus
        /// for as long as it lasts. The positional query asks whether the
        /// operator is standing in it, which would make the rider a reason to
        /// walk into his own grenade. Swapping which one the resolver calls is a
        /// one-word change, and the choice is a design decision rather than an
        /// implementation one.
        /// </remarks>
        public bool HasActiveZoneFor(PlayerColor owner)
        {
            foreach (var entry in _pending)
                if (entry.Owner == owner && entry.IsZone) return true;

            return false;
        }

        /// <summary>Whether an operator stands inside a zone belonging to <paramref name="owner"/>.</summary>
        public bool ZoneCoversOperator(OperatorState op, PlayerColor owner)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            if (!_targeting.IsInPlay(op)) return false;

            var cell = _targeting.CellOf(op);

            foreach (var entry in _pending)
            {
                if (entry.Owner != owner || !entry.IsZone) continue;
                if (_targeting.IsInArea(entry.Cell, cell, entry.Radius)) return true;
            }

            return false;
        }

        /// <summary>Whether a seat already holds a pending effect on this cell.</summary>
        public bool HasBeaconOn(CellRef cell, PlayerColor owner) => Find(cell, owner) != null;

        // ── Placing ──────────────────────────────────────────────────────

        /// <summary>
        /// Paints a cell. It fires once at <paramref name="owner"/>'s next
        /// upkeep — one full round, so every opponent moves before it lands
        /// (ADR-0006).
        /// </summary>
        /// <remarks>
        /// <b>Re-painting a cell you already hold replaces it rather than
        /// stacking.</b> Same shape as re-applying a status (§5.2). Two beams on
        /// one cell is a different mechanic and is not adopted.
        /// </remarks>
        public void Paint(
            CellRef cell,
            PlayerColor owner,
            int sourceOperatorId,
            int totalDamage,
            int radius,
            DamageType damageType)
        {
            var entry = Require(cell, owner);

            entry.SourceOperatorId = sourceOperatorId;
            entry.ResolvesOnOwnerTurn = _clock.TurnIndexOf(owner) + 1;
            entry.TicksRemaining = 1;
            entry.HasDetonated = false;
            entry.Radius = radius;
            entry.DamageType = damageType;
            entry.DetonationDamage = totalDamage;
            entry.LingerDamage = 0;
            entry.SplitsDamage = true;
            entry.DetonationStatus = null;
            entry.DetonationStatusDuration = 0;
        }

        /// <summary>
        /// Deploys a lingering zone. It detonates at <paramref name="owner"/>'s
        /// next upkeep and bills again for <paramref name="lingerTicks"/> further
        /// turns of that seat's (ADR-0007).
        /// </summary>
        /// <remarks>
        /// <b>Damage is per target.</b> The opposite of a beacon, deliberately:
        /// this is strongest against a crowd where the beacon is strongest
        /// against a lone operator.
        ///
        /// <b>The status lands on the detonation only.</b> Stun blocks movement
        /// (§5.1), so re-applying it every tick would hold a victim inside the
        /// zone until it expired.
        /// </remarks>
        public void Deploy(
            CellRef cell,
            PlayerColor owner,
            int sourceOperatorId,
            int detonationDamage,
            int lingerDamage,
            int lingerTicks,
            int radius,
            DamageType damageType,
            StatusKind detonationStatus,
            int statusDuration)
        {
            if (lingerTicks < 0) throw new ArgumentOutOfRangeException(nameof(lingerTicks));
            if (statusDuration < 1) throw new ArgumentOutOfRangeException(nameof(statusDuration));

            var entry = Require(cell, owner);

            entry.SourceOperatorId = sourceOperatorId;
            entry.ResolvesOnOwnerTurn = _clock.TurnIndexOf(owner) + 1;
            entry.TicksRemaining = 1 + lingerTicks;
            entry.HasDetonated = false;
            entry.Radius = radius;
            entry.DamageType = damageType;
            entry.DetonationDamage = detonationDamage;
            entry.LingerDamage = lingerDamage;
            entry.SplitsDamage = false;
            entry.DetonationStatus = detonationStatus;
            entry.DetonationStatusDuration = statusDuration;
        }

        // ── Resolving ────────────────────────────────────────────────────

        /// <summary>
        /// Resolves everything of <paramref name="owner"/>'s that has come due,
        /// and reports what each one did. Called at that seat's upkeep.
        /// </summary>
        /// <remarks>
        /// <b>The owner's clock, not the victim's.</b> Bleed and marks resolve at
        /// the upkeep of whoever carries them, because they are carried. These
        /// are devices their owner deployed, so they resolve on their owner's
        /// turn — which is also the only arrangement giving every seat the same
        /// warning regardless of where it sits in the order.
        ///
        /// <b>They resolve whatever has happened to the operator that placed
        /// them.</b> Stunned, in a home column, home, or neutralized and sitting
        /// in its yard: none of them stop it. Stun blocks an action phase (§5.1)
        /// and upkeep is not an action; the rest are reachability rules about the
        /// operator, and a device is not its operator. A kill still credits the
        /// recorded source, which <c>NeutralizeRules</c> resolves against the full
        /// match roster rather than the track.
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
                if (ownerTurn < entry.ResolvesOnOwnerTurn) continue;

                if (fired == null) fired = new List<CellEffectResolution>();
                fired.Add(Resolve(entry, allOperators));

                entry.HasDetonated = true;
                entry.TicksRemaining--;

                if (entry.TicksRemaining <= 0) _pending.RemoveAt(i);
                else entry.ResolvesOnOwnerTurn = ownerTurn + 1;
            }

            if (fired == null) return Array.Empty<CellEffectResolution>();

            // Removal walked the list backwards, so results came out in reverse
            // placement order. The view plays them in the order they were placed.
            fired.Reverse();
            return fired;
        }

        /// <summary>
        /// Removes everything a seat holds. For a caller that needs to clear the
        /// board — a match reset, or a future effect that disarms devices.
        /// </summary>
        /// <remarks>
        /// Neutralizing the operator that placed one deliberately does <b>not</b>
        /// call this: a deployed device outlives its operator, and letting a kill
        /// refund the energy already spent would make both abilities worse than
        /// they read (ADR-0006).
        /// </remarks>
        public void ClearFor(PlayerColor owner)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
                if (_pending[i].Owner == owner) _pending.RemoveAt(i);
        }

        // ── Internals ────────────────────────────────────────────────────

        private CellEffectResolution Resolve(Pending entry, IReadOnlyList<OperatorState> allOperators)
        {
            bool detonating = !entry.HasDetonated;
            var caught = _targeting.EnemiesInArea(entry.Cell, entry.Radius, entry.Owner, allOperators);

            if (caught.Count == 0)
            {
                return new CellEffectResolution(
                    entry.Cell, entry.Owner, entry.SourceOperatorId, entry.Cause,
                    detonating, caught, null, 0, null);
            }

            int payload = detonating ? entry.DetonationDamage : entry.LingerDamage;

            // A beacon is one beam of fixed energy divided among whoever it
            // catches; a zone grinds each of them in full.
            int each = entry.SplitsDamage ? payload / caught.Count : payload;

            var results = new List<DamageResult>(caught.Count);
            List<OperatorState> stunned = null;

            foreach (var victim in caught)
            {
                if (detonating && entry.DetonationStatus != null)
                {
                    _statuses.Apply(
                        victim, entry.DetonationStatus.Value,
                        entry.DetonationStatusDuration,
                        sourceOperatorId: entry.SourceOperatorId);

                    if (stunned == null) stunned = new List<OperatorState>(caught.Count);
                    stunned.Add(victim);
                }

                // The status is applied before the damage so a victim the blast
                // kills is stunned first and then cleared by the neutralize,
                // rather than being stunned in its yard afterwards.
                results.Add(_damage.Apply(victim, new DamageInstance(
                    each, entry.DamageType, entry.SourceOperatorId, entry.Cause)));
            }

            return new CellEffectResolution(
                entry.Cell, entry.Owner, entry.SourceOperatorId, entry.Cause,
                detonating, caught, results, each, stunned);
        }

        private IReadOnlyList<CellRef> CellsWhere(bool zones)
        {
            var cells = new List<CellRef>();

            foreach (var entry in _pending)
                if (entry.IsZone == zones) cells.Add(entry.Cell);

            return cells;
        }

        private Pending Require(CellRef cell, PlayerColor owner)
        {
            if (!cell.IsOnTrack)
                throw new ArgumentException($"A cell effect needs an outer-track cell; got {cell}.", nameof(cell));
            if (owner == PlayerColor.None)
                throw new ArgumentException("A cell effect needs a seat to pay its kills.", nameof(owner));

            return Find(cell, owner) ?? NewEntry(cell, owner);
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