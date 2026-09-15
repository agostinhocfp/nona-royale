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
            int statusDuration,
            OperatorState target = null)
        {
            Target = target;
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

        /// <summary>
        /// The operator the entry was set on — the charge's carrier, or the
        /// follow-up's quarry. Carried because a follow-up that misses catches
        /// nobody, and the view still has to say who got away.
        /// </summary>
        public OperatorState Target { get; }

        /// <summary>The cell the charge detonated on — the target's current cell, or its death cell.</summary>
        public CellRef Cell { get; }

        /// <summary>The seat that attached it, which is who a kill pays.</summary>
        public PlayerColor Owner { get; }

        /// <summary>The operator that attached it. May be in its yard by now.</summary>
        public int SourceOperatorId { get; }

        /// <summary>
        /// <see cref="DeferredOperatorEffects.ChargeCause"/> or
        /// <see cref="DeferredOperatorEffects.FollowUpCause"/> — which shape
        /// resolved, for the engine's reporting and for a kill's label.
        /// </summary>
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
        /// bonus has no living recipient (§6.4). For a follow-up, the heavy
        /// bonus; zero when the strike missed (§6.5).
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
    /// Effects that name a victim and a later moment. Two shapes, both
    /// resolving at the caster's next upkeep:
    /// <list type="bullet">
    /// <item>a <b>charge</b> (§6.4, Zero-Day) follows the target and detonates
    /// on whatever cell it then occupies, catching everyone near it;</item>
    /// <item>a <b>follow-up</b> (§6.5, Luka's Blind Spot) strikes the target alone, and
    /// only if the caster is still close enough to it.</item>
    /// </list>
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
    /// pending entry holds the payload; the marker on the target
    /// (<see cref="StatusKind.ZeroDayCharge"/> or <see cref="StatusKind.Hunted"/>)
    /// says the entry is still live. At fire time a target
    /// standing in play without the marker was cleansed, and the entry is
    /// discarded unresolved — no notification plumbing between the status
    /// registry and this one, which would be the dependency cycle
    /// <c>StatusRegistry</c>'s own remarks warn about.
    ///
    /// <b>It resolves whatever has happened to the operator that attached
    /// it</b>, exactly as a beacon does (ADR-0006): a deployed device is not its
    /// operator, and a kill still credits the recorded source. A follow-up is
    /// the exception by construction: its condition is measured from the
    /// caster, so a caster that has left the board cannot land it.
    ///
    /// <b>The death cell is reported, not inferred.</b> <c>NeutralizeRules</c>
    /// calls <see cref="OperatorDied"/> before it yards the piece, because once
    /// the target is in its yard its last board cell is unrecoverable — progress
    /// is relative to a colour's own start, and the yard has no cell at all.
    ///
    /// <b>Known limitation: two entries of one shape on one target share one
    /// marker.</b> The registry stores one entry per status kind per operator,
    /// so two seats attaching to the same victim leave a single badge; the
    /// first resolution consumes it and the second then reads as cleansed. A
    /// four-seat edge the design has not needed to answer; recorded rather
    /// than solved. The two shapes use different markers
    /// (<see cref="StatusKind.ZeroDayCharge"/>, <see cref="StatusKind.Hunted"/>),
    /// so a charge and a follow-up on the same target never collide.
    /// </remarks>
    public sealed class DeferredOperatorEffects
    {
        /// <summary>Cause recorded on a charge's damage, for the view (§2.1).</summary>
        public const string ChargeCause = "zero-day";

        /// <summary>Cause recorded on a follow-up's damage, for the view (§2.1).</summary>
        public const string FollowUpCause = "follow-up";

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
        ///
        /// A follow-up resolves at the same moment and is consumed the same
        /// way, so its marker uses the same value for the same reason.
        /// </remarks>
        public const int MarkerDurationTurns = 2;

        private enum Shape
        {
            Charge,
            FollowUp
        }

        private sealed class Pending
        {
            public Shape Shape;

            /// <summary>The status whose absence at fire time means "cleansed".</summary>
            public StatusKind Marker;

            public OperatorState Target;
            public PlayerColor Owner;
            public int SourceOperatorId;
            public int ResolvesOnOwnerTurn;
            public CellRef LastKnownCell;

            /// <summary>
            /// A charge: the blast radius around the target. A follow-up: how
            /// close the caster must stand to the target for the strike to land.
            /// </summary>
            public int Radius;
            public DamageType DamageType;

            /// <summary>Dealt to every enemy in the radius — for a follow-up, to the target alone.</summary>
            public int SplashDamage;

            /// <summary>Added for the marked target itself, on top of the splash.</summary>
            public int PrimaryBonus;

            /// <summary>False for a follow-up, which applies no status.</summary>
            public bool HasStatus;
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

            return Find(target, owner, Shape.Charge) != null;
        }

        /// <summary>Whether a seat has a follow-up pending on this operator. For the view and for tests.</summary>
        public bool HasFollowUpOn(OperatorState target, PlayerColor owner)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            return Find(target, owner, Shape.FollowUp) != null;
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

            var entry = Find(target, owner, Shape.Charge) ?? NewEntry(target, owner, Shape.Charge);

            entry.Marker = StatusKind.ZeroDayCharge;
            entry.SourceOperatorId = sourceOperatorId;
            entry.ResolvesOnOwnerTurn = _clock.TurnIndexOf(owner) + 1;
            entry.LastKnownCell = _targeting.CellOf(target);
            entry.Radius = radius;
            entry.DamageType = damageType;
            entry.SplashDamage = splashDamage;
            entry.PrimaryBonus = primaryBonus;
            entry.HasStatus = true;
            entry.Status = status;
            entry.StatusDuration = statusDuration;
        }

        /// <summary>
        /// Sets a follow-up strike on <paramref name="target"/>. At
        /// <paramref name="owner"/>'s next upkeep, if the operator that set it
        /// is within <paramref name="withinRange"/> of the target, the target
        /// takes <paramref name="damage"/> plus <paramref name="heavyBonus"/>;
        /// otherwise the strike is spent with nothing to show for it (§6.5).
        /// </summary>
        /// <remarks>
        /// <b>Re-setting replaces rather than stacks</b>, as <see cref="Attach"/>
        /// does. The heavy bonus arrives already settled — zero for a target
        /// that is not heavy — because the caller holds the rule.
        ///
        /// <b>Unlike a charge, it does not outlive its caster.</b> The strike
        /// is the caster's own blow and proximity is measured from the caster;
        /// a caster in its yard is nowhere, so the strike misses. That is the
        /// deliberate difference from a deployed device (ADR-0006), and it
        /// needs no special case: a yarded operator has no distance to anyone.
        /// </remarks>
        public void SetFollowUp(
            OperatorState target,
            PlayerColor owner,
            int sourceOperatorId,
            int damage,
            int heavyBonus,
            int withinRange,
            DamageType damageType)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (owner == PlayerColor.None)
                throw new ArgumentException("A follow-up needs a seat to pay its kills.", nameof(owner));
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (heavyBonus < 0) throw new ArgumentOutOfRangeException(nameof(heavyBonus));
            if (withinRange < 0) throw new ArgumentOutOfRangeException(nameof(withinRange));

            var entry = Find(target, owner, Shape.FollowUp) ?? NewEntry(target, owner, Shape.FollowUp);

            entry.Marker = StatusKind.Hunted;
            entry.SourceOperatorId = sourceOperatorId;
            entry.ResolvesOnOwnerTurn = _clock.TurnIndexOf(owner) + 1;
            entry.LastKnownCell = _targeting.CellOf(target);
            entry.Radius = withinRange;
            entry.DamageType = damageType;
            entry.SplashDamage = damage;
            entry.PrimaryBonus = heavyBonus;
            entry.HasStatus = false;
            entry.Status = default;
            entry.StatusDuration = 0;
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
                // the attachment is gone and the entry never resolves.
                if (_targeting.IsInPlay(entry.Target) &&
                    !_statuses.Has(entry.Target, entry.Marker))
                {
                    _pending.RemoveAt(i);
                    continue;
                }

                if (fired == null) fired = new List<OperatorEffectResolution>();
                fired.Add(entry.Shape == Shape.FollowUp
                    ? ResolveFollowUp(entry, allOperators)
                    : ResolveCharge(entry, allOperators));
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

        private OperatorEffectResolution ResolveCharge(Pending entry, IReadOnlyList<OperatorState> allOperators)
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
                if (entry.HasStatus)
                {
                    _statuses.Apply(
                        victim, entry.Status, entry.StatusDuration,
                        sourceOperatorId: entry.SourceOperatorId);

                    if (statused == null) statused = new List<OperatorState>(caught.Count);
                    statused.Add(victim);
                }

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
                statused, entry.Status, entry.StatusDuration, entry.Target);
        }

        /// <summary>
        /// Resolves a follow-up: the target alone, and only if the operator
        /// that set it is within the entry's radius of it right now (§6.5).
        /// </summary>
        /// <remarks>
        /// <b>A miss still reports</b>, with nobody caught — the target
        /// outran the strike, which is the counterplay working, and a player
        /// should see it. It resolves on the target's cell, or on its death
        /// cell when something else finished it first; a dead target is never
        /// in reach, because a yarded operator has no distance to anyone.
        ///
        /// <b>Proximity ignores safe cells and stealth</b>, as a charge's
        /// blast does. Both are rules about <i>aiming</i>, and the aim was
        /// taken — legally — when the strike was set. What the target can do
        /// about it now is move.
        /// </remarks>
        private OperatorEffectResolution ResolveFollowUp(Pending entry, IReadOnlyList<OperatorState> allOperators)
        {
            bool targetAlive = _targeting.IsInPlay(entry.Target);
            var cell = targetAlive ? _targeting.CellOf(entry.Target) : entry.LastKnownCell;

            var source = FindOperator(entry.SourceOperatorId, allOperators);
            int? distance = targetAlive && source != null
                ? _targeting.Distance(source, entry.Target)
                : null;

            bool inReach = distance != null && distance.Value <= entry.Radius;

            var caught = new List<OperatorState>(1);
            var results = new List<DamageResult>(1);

            if (inReach)
            {
                caught.Add(entry.Target);
                results.Add(_damage.Apply(entry.Target, new DamageInstance(
                    entry.SplashDamage + entry.PrimaryBonus, entry.DamageType,
                    entry.SourceOperatorId, FollowUpCause)));
            }

            // Spent either way: landed or outrun, the strike is over.
            if (targetAlive) _statuses.Remove(entry.Target, StatusKind.Hunted);

            return new OperatorEffectResolution(
                cell, entry.Owner, entry.SourceOperatorId, FollowUpCause,
                caught, results, entry.SplashDamage,
                inReach ? entry.PrimaryBonus : 0,
                statused: null, status: default, statusDuration: 0,
                target: entry.Target);
        }

        private static OperatorState FindOperator(int id, IReadOnlyList<OperatorState> allOperators)
        {
            foreach (var op in allOperators)
                if (op != null && op.Id == id) return op;

            return null;
        }

        private Pending Find(OperatorState target, PlayerColor owner, Shape shape)
        {
            foreach (var entry in _pending)
            {
                if (entry.Owner == owner && entry.Shape == shape && ReferenceEquals(entry.Target, target))
                    return entry;
            }

            return null;
        }

        private Pending NewEntry(OperatorState target, PlayerColor owner, Shape shape)
        {
            var entry = new Pending { Target = target, Owner = owner, Shape = shape };
            _pending.Add(entry);
            return entry;
        }
    }
}
