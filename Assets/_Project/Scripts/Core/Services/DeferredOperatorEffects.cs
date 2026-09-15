// Assets/_Project/Scripts/Core/Services/DeferredOperatorEffects.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>One pending operator-anchored charge that resolved, and what it did.</summary>
    /// <remarks>
    /// The operator-anchored twin of <see cref="CellEffectResolution"/>, kept
    /// the same shape so the reporting loop in <c>GameEngine</c> reads like the
    /// one it already has. A charge that caught nobody still reports — silence
    /// would be indistinguishable from a charge that was never attached.
    /// </remarks>
    public readonly struct OperatorEffectResolution
    {
        public OperatorEffectResolution(
            CellRef cell,
            PlayerColor owner,
            int sourceOperatorId,
            string cause,
            IReadOnlyList<OperatorState> caught,
            IReadOnlyList<DamageResult> damage,
            int damagePerTarget,
            int markedTargetBonus,
            IReadOnlyList<OperatorState> statused,
            StatusKind status,
            int statusDuration)
        {
            Cell = cell;
            Owner = owner;
            SourceOperatorId = sourceOperatorId;
            Cause = cause;
            Caught = caught ?? Array.Empty<OperatorState>();
            Damage = damage ?? Array.Empty<DamageResult>();
            DamagePerTarget = damagePerTarget;
            MarkedTargetBonus = markedTargetBonus;
            Statused = statused ?? Array.Empty<OperatorState>();
            Status = status;
            StatusDuration = statusDuration;
        }

        /// <summary>The cell the charge detonated on — the target's current cell, or its death cell.</summary>
        public CellRef Cell { get; }

        /// <summary>The seat that attached it, which is who a kill pays.</summary>
        public PlayerColor Owner { get; }

        /// <summary>The operator that attached it. May be in its yard by now.</summary>
        public int SourceOperatorId { get; }

        /// <summary>"zero-day", for the view and for a kill's label.</summary>
        public string Cause { get; }

        /// <summary>Every enemy the blast reached, in the order it struck them.</summary>
        public IReadOnlyList<OperatorState> Caught { get; }

        /// <summary>One per entry in <see cref="Caught"/>, same order.</summary>
        public IReadOnlyList<DamageResult> Damage { get; }

        /// <summary>What each one took before mitigation, before the marked target's bonus.</summary>
        public int DamagePerTarget { get; }

        /// <summary>
        /// The extra the marked target took on top of <see cref="DamagePerTarget"/>.
        /// Zero on the resolution when the target died before detonation — the
        /// bonus has no living recipient (§6.4).
        /// </summary>
        public int MarkedTargetBonus { get; }

        /// <summary>Operators the detonation applied its status to. One per caught enemy.</summary>
        public IReadOnlyList<OperatorState> Statused { get; }

        /// <summary>The status applied, and for how long — carried so the view announces what actually landed.</summary>
        public StatusKind Status { get; }
        public int StatusDuration { get; }

        public bool HitSomething => Caught.Count > 0;

        public override string ToString() =>
            HitSomething
                ? $"{Owner} {Cause} on {Cell} hits {Caught.Count} for {DamagePerTarget} each"
                : $"{Owner} {Cause} on {Cell} hits nothing";
    }

    /// <summary>
    /// Effects that name a victim and a later moment (§6.4). Zero-Day's grenade
    /// is the first shape: a charge attached to an operator that follows it and
    /// detonates at the caster's next upkeep, on whatever cell the target then
    /// occupies.
    /// </summary>
    /// <remarks>
    /// <b>Anchored to a victim, not to the board.</b> That is the deliberate
    /// opposite of <see cref="DeferredCellEffects"/>, whose remarks explain why
    /// a beacon must not follow anybody. An attached charge is a delayed
    /// certainty rather than a bet on position, and it pays for that in two
    /// counterplay features rather than in damage: it is telegraphed on
    /// attachment, and a cleanse strips the marker and cancels it (§5.10).
    ///
    /// <b>The marker status is the source of truth for cancellation.</b> The
    /// pending entry holds the payload; the <see cref="StatusKind.ZeroDayCharge"/>
    /// on the target says the device is still attached. At fire time a target
    /// standing in play without the marker was cleansed, and the entry is
    /// discarded unresolved — no notification plumbing between the status
    /// registry and this one, which would be the dependency cycle
    /// <c>StatusRegistry</c>'s own remarks warn about.
    ///
    /// <b>It resolves whatever has happened to the operator that attached
    /// it</b>, exactly as a beacon does (ADR-0006): a deployed device is not its
    /// operator, and a kill still credits the recorded source.
    ///
    /// <b>The death cell is reported, not inferred.</b> <c>NeutralizeRules</c>
    /// calls <see cref="OperatorDied"/> before it yards the piece, because once
    /// the target is in its yard its last board cell is unrecoverable — progress
    /// is relative to a colour's own start, and the yard has no cell at all.
    ///
    /// <b>Known limitation: two charges on one target share one marker.</b> The
    /// registry stores one entry per status kind per operator, so two seats
    /// attaching to the same victim leave a single badge; the first detonation
    /// consumes it and the second charge then reads as cleansed. A four-seat
    /// edge the design has not needed to answer; recorded rather than solved.
    /// </remarks>
    public sealed class DeferredOperatorEffects
    {
        /// <summary>Cause recorded on the damage these produce, for the view (§2.1).</summary>
        private const string ChargeCause = "zero-day";

        /// <summary>
        /// How long the attachment marker lasts, in the target's own turns.
        /// </summary>
        /// <remarks>
        /// <b>Two, and the value is derived, not tuned.</b> The charge detonates
        /// at its owner's next upkeep, which always falls after the target's
        /// next turn ends and before the one after — whatever the seat order.
        /// Duration 1 would expire at the end of the target's first turn, just
        /// before the earliest possible detonation, and a lapsed marker reads as
        /// a cleanse: the charge would cancel itself. Duration 2 is the shortest
        /// span that always covers the window, and the marker is consumed by the
        /// detonation itself, so it never outlives its usefulness either.
        /// </remarks>
        public const int MarkerDurationTurns = 2;

        private sealed class Pending
        {
            public OperatorState Target;
            public PlayerColor Owner;
            public int SourceOperatorId;
            public int ResolvesOnOwnerTurn;
            public CellRef LastKnownCell;

            public int Radius;
            public DamageType DamageType;

            /// <summary>Dealt to every enemy in the radius.</summary>
            public int SplashDamage;

            /// <summary>Added for the marked target itself, on top of the splash.</summary>
            public int PrimaryBonus;

            public StatusKind Status;
            public int StatusDuration;
        }

        private readonly List<Pending> _pending = new List<Pending>();

        private readonly ITurnClock _clock;
        private readonly TargetingRules _targeting;
        private readonly DamagePipeline _damage;
        private readonly StatusRegistry _statuses;

        public DeferredOperatorEffects(
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

        /// <summary>Whether a seat has a charge riding on this operator. For the view and for tests.</summary>
        public bool HasChargeOn(OperatorState target, PlayerColor owner)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            foreach (var entry in _pending)
                if (entry.Owner == owner && ReferenceEquals(entry.Target, target)) return true;

            return false;
        }

        /// <summary>
        /// Attaches a charge to <paramref name="target"/>. It detonates at
        /// <paramref name="owner"/>'s next upkeep — one full round, the same
        /// warning a beacon gives (ADR-0006).
        /// </summary>
        /// <remarks>
        /// <b>Re-attaching to a target you already hold replaces the charge
        /// rather than stacking.</b> Same shape as re-painting a cell or
        /// re-applying a status (§5.2); two grenades on one victim is a
        /// different mechanic and is not adopted.
        /// </remarks>
        public void Attach(
            OperatorState target,
            PlayerColor owner,
            int sourceOperatorId,
            int splashDamage,
            int primaryBonus,
            int radius,
            DamageType damageType,
            StatusKind status,
            int statusDuration)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (owner == PlayerColor.None)
                throw new ArgumentException("A charge needs a seat to pay its kills.", nameof(owner));

            var entry = Find(target, owner) ?? NewEntry(target, owner);

            entry.SourceOperatorId = sourceOperatorId;
            entry.ResolvesOnOwnerTurn = _clock.TurnIndexOf(owner) + 1;
            entry.LastKnownCell = _targeting.CellOf(target);
            entry.Radius = radius;
            entry.DamageType = damageType;
            entry.SplashDamage = splashDamage;
            entry.PrimaryBonus = primaryBonus;
            entry.Status = status;
            entry.StatusDuration = statusDuration;
        }

        /// <summary>
        /// Records where an operator died, for any charge riding on it. Called
        /// by <c>NeutralizeRules</c> before the piece is yarded — afterwards its
        /// last cell is unrecoverable.
        /// </summary>
        /// <remarks>
        /// The charge still detonates, on the death cell, with no living
        /// recipient for the primary bonus (§6.4). An attached device outlives
        /// its carrier exactly as it outlives its caster.
        /// </remarks>
        public void OperatorDied(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            var cell = _targeting.CellOf(op);

            foreach (var entry in _pending)
                if (ReferenceEquals(entry.Target, op)) entry.LastKnownCell = cell;
        }

        /// <summary>
        /// Resolves every charge of <paramref name="owner"/>'s that has come
        /// due, and reports what each one did. Called at that seat's upkeep,
        /// beside <see cref="DeferredCellEffects.Fire"/>.
        /// </summary>
        /// <remarks>
        /// <b>A cleansed charge cancels silently.</b> The cleanse already
        /// announced itself when it stripped the marker; there is nothing new
        /// to report at the moment the grenade fails to go off, and an event
        /// here would surface a round after the play that caused it.
        /// </remarks>
        public IReadOnlyList<OperatorEffectResolution> Fire(
            PlayerColor owner, IReadOnlyList<OperatorState> allOperators)
        {
            if (allOperators == null) throw new ArgumentNullException(nameof(allOperators));

            int ownerTurn = _clock.TurnIndexOf(owner);
            List<OperatorEffectResolution> fired = null;

            // Backwards so a removal cannot skip the next entry.
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var entry = _pending[i];

                if (entry.Owner != owner) continue;
                if (ownerTurn < entry.ResolvesOnOwnerTurn) continue;

                // A target standing in play without its marker was cleansed:
                // the attachment is gone and the charge never goes off.
                if (_targeting.IsInPlay(entry.Target) &&
                    !_statuses.Has(entry.Target, StatusKind.ZeroDayCharge))
                {
                    _pending.RemoveAt(i);
                    continue;
                }

                if (fired == null) fired = new List<OperatorEffectResolution>();
                fired.Add(Resolve(entry, allOperators));
                _pending.RemoveAt(i);
            }

            if (fired == null) return Array.Empty<OperatorEffectResolution>();

            fired.Reverse();
            return fired;
        }

        /// <summary>
        /// Removes everything a seat holds. For a caller that needs to clear the
        /// board — a match reset, or a future effect that disarms devices.
        /// </summary>
        /// <remarks>
        /// Neutralizing the operator that attached a charge deliberately does
        /// <b>not</b> call this, for the same reason it does not clear beacons
        /// (ADR-0006): a deployed device outlives its operator.
        /// </remarks>
        public void ClearFor(PlayerColor owner)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
                if (_pending[i].Owner == owner) _pending.RemoveAt(i);
        }

        // ── Internals ────────────────────────────────────────────────────

        private OperatorEffectResolution Resolve(Pending entry, IReadOnlyList<OperatorState> allOperators)
        {
            // The charge detonates where the target is now; a dead target's
            // charge detonates on its death cell, recorded by OperatorDied.
            bool targetAlive = _targeting.IsInPlay(entry.Target);
            var cell = targetAlive ? _targeting.CellOf(entry.Target) : entry.LastKnownCell;

            var caught = _targeting.EnemiesInArea(cell, entry.Radius, entry.Owner, allOperators);

            var results = new List<DamageResult>(caught.Count);
            List<OperatorState> statused = null;

            foreach (var victim in caught)
            {
                // The status lands before the damage, the beacon's precedent: a
                // victim the blast kills is slowed first and then cleared by the
                // neutralize, rather than being slowed in its yard afterwards.
                _statuses.Apply(
                    victim, entry.Status, entry.StatusDuration,
                    sourceOperatorId: entry.SourceOperatorId);

                if (statused == null) statused = new List<OperatorState>(caught.Count);
                statused.Add(victim);

                int amount = entry.SplashDamage;
                if (ReferenceEquals(victim, entry.Target)) amount += entry.PrimaryBonus;

                results.Add(_damage.Apply(victim, new DamageInstance(
                    amount, entry.DamageType, entry.SourceOperatorId, ChargeCause)));
            }

            // The detonation consumes the marker; a cleansed-looking target
            // afterwards is correct, because the grenade is spent either way.
            if (targetAlive) _statuses.Remove(entry.Target, StatusKind.ZeroDayCharge);

            return new OperatorEffectResolution(
                cell, entry.Owner, entry.SourceOperatorId, ChargeCause,
                caught, results, entry.SplashDamage,
                targetAlive ? entry.PrimaryBonus : 0,
                statused, entry.Status, entry.StatusDuration);
        }

        private Pending Find(OperatorState target, PlayerColor owner)
        {
            foreach (var entry in _pending)
                if (entry.Owner == owner && ReferenceEquals(entry.Target, target)) return entry;

            return null;
        }

        private Pending NewEntry(OperatorState target, PlayerColor owner)
        {
            var entry = new Pending { Target = target, Owner = owner };
            _pending.Add(entry);
            return entry;
        }
    }
}
