// Assets/_Project/Scripts/Core/Services/StatusRegistry.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Applies, queries and expires every status effect (COMBAT_SYSTEMS §5), and
    /// serves as the mitigation layer the damage pipeline consults.
    /// </summary>
    /// <remarks>
    /// <b>Timers are absolute indices, not counters.</b> Each entry records the
    /// first turn it is active and the last, both resolved at application time
    /// against the <i>target owner's</i> turn count. Nothing decrements. That
    /// removes the classic off-by-one where a 1-turn stun applied on an
    /// opponent's turn ticks away before the target ever acts, and it makes the
    /// boundary case a single assertion instead of a simulation.
    ///
    /// <b>Passives are stored separately from applied statuses.</b> Neutralize
    /// clears everything an operator is carrying, but a passive is who an
    /// operator <i>is</i> — an operator returning to its yard is still itself
    /// (§1.2, §5.1). Keeping them in their own dictionary makes that structural
    /// rather than something every future caller of <see cref="ClearAll"/> has
    /// to remember to undo.
    ///
    /// <b>Damage is reported, never applied here.</b> Bleed and marks deal
    /// damage, and the damage pipeline consults this registry for evasion and
    /// shields — having the registry call the pipeline would close a dependency
    /// cycle. So <see cref="ConsumeBleed"/> and <see cref="MarkTickDamage"/>
    /// return what the tick is worth; the caller feeds it through the pipeline
    /// as Atomic damage. The registry decides <i>what</i> is owed, the pipeline
    /// decides how damage lands, and neither knows the other exists.
    ///
    /// <b><see cref="StatusKind.Shield"/> stores its remaining pool in
    /// <c>Entry.Magnitude</c>.</b> It is already a <c>double</c>, already written
    /// by <see cref="Apply"/>, and already refreshed on re-application, so the
    /// pool needed no new storage — only a reader that decrements it.
    /// </remarks>
    public sealed class StatusRegistry : IDamageMitigation
    {
        private const int Permanent = int.MaxValue;

        private sealed class Entry
        {
            public double Magnitude;
            public int Stacks;
            public int FirstActiveTurn;
            public int LastActiveTurn;
            public int SourceOperatorId;
        }

        private readonly Dictionary<int, Dictionary<StatusKind, Entry>> _byOperator =
            new Dictionary<int, Dictionary<StatusKind, Entry>>();

        /// <summary>Permanent passives. Untouched by <see cref="ClearAll"/> and by expiry.</summary>
        private readonly Dictionary<int, Dictionary<StatusKind, Entry>> _passives =
            new Dictionary<int, Dictionary<StatusKind, Entry>>();

        private readonly HashSet<int> _evasionSpentThisRound = new HashSet<int>();

        private readonly ITurnClock _clock;
        private readonly CombatConfig _config;

        public StatusRegistry(ITurnClock clock, CombatConfig config)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        // ── Applying ─────────────────────────────────────────────────────

        /// <summary>
        /// Applies a status for <paramref name="duration"/> of the target's own
        /// turns.
        /// </summary>
        /// <remarks>
        /// When applied outside the target's turn — which is the normal case for
        /// a debuff — it takes hold on the target's <i>next</i> turn, so a
        /// 1-turn stun blocks a real action phase before expiring. Applied on
        /// the target's own turn, as a self-buff like Stealth is, it takes hold
        /// immediately and "current turn + 1" is simply duration 2.
        ///
        /// Re-application refreshes the duration and keeps the stronger
        /// magnitude. Only <see cref="StatusKind.Bleed"/> accumulates stacks
        /// (§5.3).
        ///
        /// <b>For a shield that means re-casting tops the pool up rather than
        /// adding to it</b> — a 2-point plate cast on an ally with 1 point left
        /// goes back to 2, and cast on an ally at full does nothing but refresh
        /// the duration. That is §5.2's "sources do not stack; the strongest
        /// applies" holding for a pool the same way it holds for a slow, and it
        /// is what stops two supports from stacking an arbitrarily deep wall.
        ///
        /// A magnitude of zero means "use this kind's default", which is how
        /// Slow and Shield get their size without <c>AlphaRoster</c>
        /// needing a dependency on config. An ability that wants a different
        /// size states one and it is honoured.
        /// </remarks>
        public void Apply(
            OperatorState target,
            StatusKind kind,
            int duration = 1,
            double magnitude = 0.0,
            int stacks = 1,
            int sourceOperatorId = 0)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (duration < 1) throw new ArgumentOutOfRangeException(nameof(duration));
            if (stacks < 1) throw new ArgumentOutOfRangeException(nameof(stacks));

            if (magnitude == 0.0) magnitude = DefaultMagnitudeFor(kind);

            int ownerTurn = _clock.TurnIndexOf(target.Owner);
            bool onTargetsOwnTurn = _clock.ActivePlayer == target.Owner;
            int firstActive = onTargetsOwnTurn ? ownerTurn : ownerTurn + 1;

            var entries = EntriesFor(target.Id);

            if (entries.TryGetValue(kind, out var existing))
            {
                existing.FirstActiveTurn = Math.Min(existing.FirstActiveTurn, firstActive);
                existing.LastActiveTurn = Math.Max(existing.LastActiveTurn, firstActive + duration - 1);
                existing.SourceOperatorId = sourceOperatorId;

                // Sources do not stack; the strongest applies (§5.2). Compared
                // by absolute value because magnitudes are signed — a plain
                // Math.Max would let a weak slow override a strong one.
                if (Math.Abs(magnitude) > Math.Abs(existing.Magnitude))
                    existing.Magnitude = magnitude;

                if (kind == StatusKind.Bleed) existing.Stacks += stacks;
                return;
            }

            entries[kind] = new Entry
            {
                Magnitude = magnitude,
                Stacks = stacks,
                FirstActiveTurn = firstActive,
                LastActiveTurn = firstActive + duration - 1,
                SourceOperatorId = sourceOperatorId
            };
        }

        /// <summary>
        /// Grants a permanent passive — Kurbyn's Evasive Protocol. Never expires,
        /// survives neutralize, and is unaffected by stun: a passive is who an
        /// operator is, not what it does (§1.2, §5.1).
        /// </summary>
        /// <remarks>
        /// <paramref name="magnitude"/> is a signed speed delta, read by
        /// <see cref="SpeedModifier"/>. Evasive Protocol's +0.5 lives here
        /// rather than as a constant in movement code, which is what lets one
        /// passive carry both a mitigation effect and a speed effect without a
        /// special case anywhere.
        ///
        /// <b>Not a route for shields.</b> <see cref="AbsorbFrom"/> reads only
        /// applied entries, so a passive shield would present as an
        /// undepletable pool. If one is ever wanted it needs a decision, not a
        /// call to this method.
        /// </remarks>
        public void ApplyPassive(OperatorState target, StatusKind kind, double magnitude = 0.0)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            PassivesFor(target.Id)[kind] = new Entry
            {
                Magnitude = magnitude,
                Stacks = 1,
                FirstActiveTurn = int.MinValue,
                LastActiveTurn = Permanent,
                SourceOperatorId = target.Id
            };
        }

        // ── Querying ─────────────────────────────────────────────────────

        public bool Has(OperatorState op, StatusKind kind) => ActiveEntry(op, kind) != null;

        /// <summary>Cannot move and cannot spend energy this turn (§5.1).</summary>
        public bool IsStunned(OperatorState op) => Has(op, StatusKind.Stun);

        /// <summary>
        /// Net speed modifier from every active status and passive, ready to
        /// hand to <c>MovementResolver.EffectiveSpeed</c>.
        /// </summary>
        /// <remarks>
        /// Summed across kinds rather than switched on one. A slowed and hasted
        /// operator correctly nets to zero, and a passive carrying a speed bonus
        /// needs no special case here. Within a single kind there is only ever
        /// one entry, so "sources do not stack" (§5.2) is enforced at
        /// application time rather than in this sum.
        ///
        /// An applied status shadows a passive of the same kind — the same
        /// precedence <see cref="ActiveEntry"/> uses — so a temporary override
        /// of a passive never double-counts.
        ///
        /// <b>Shield is skipped.</b> Its magnitude is a damage pool, not a speed
        /// delta. Every other kind either carries a signed speed value or
        /// carries zero, which is why this could be a blind sum until now.
        ///
        /// <b>Hastened is skipped too (2026-09-16).</b> Haste is flat extra
        /// cells per roll now, applied by <c>GameEngine</c>, not speed. An
        /// ability that stated a magnitude for it would otherwise leak into
        /// the speed channel.
        /// </remarks>
        public double SpeedModifier(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            int ownerTurn = _clock.TurnIndexOf(op.Owner);
            double total = 0.0;

            _byOperator.TryGetValue(op.Id, out var applied);
            _passives.TryGetValue(op.Id, out var passives);

            if (applied != null)
            {
                foreach (var pair in applied)
                {
                    if (IsNotSpeed(pair.Key)) continue;
                    if (IsActive(pair.Value, ownerTurn)) total += pair.Value.Magnitude;
                }
            }

            if (passives != null)
            {
                foreach (var pair in passives)
                {
                    if (IsNotSpeed(pair.Key)) continue;
                    if (applied != null && applied.ContainsKey(pair.Key)) continue;
                    total += pair.Value.Magnitude;
                }
            }

            return total;
        }

        /// <summary>
        /// Whether <see cref="StatusKind.Hastened"/> is active on this operator
        /// right now. The engine turns it into extra cells per roll
        /// (COMBAT_SYSTEMS §5.9).
        /// </summary>
        public bool IsHastened(OperatorState op) => Has(op, StatusKind.Hastened);

        /// <summary>
        /// Whether <see cref="StatusKind.Burdened"/> is active on this operator
        /// — Sanity's passive. The engine turns it into fewer cells per roll
        /// (COMBAT_SYSTEMS §5.16).
        /// </summary>
        public bool IsBurdened(OperatorState op) => Has(op, StatusKind.Burdened);

        /// <summary>Whether this operator carries Equilibrium — Revú's passive (§5.17).</summary>
        public bool ScalesCastDamage(OperatorState target) => Has(target, StatusKind.Equilibrium);

        private static bool IsNotSpeed(StatusKind kind) =>
            kind == StatusKind.Shield || kind == StatusKind.Hastened || kind == StatusKind.Burdened;

        public int BleedStacks(OperatorState op)
        {
            var entry = ActiveEntry(op, StatusKind.Bleed);
            return entry?.Stacks ?? 0;
        }

        /// <summary>
        /// True while at least one stack is unspent. Read by Syla's From the Hip
        /// for its bonus damage (§5.3).
        /// </summary>
        public bool IsBleeding(OperatorState op) => BleedStacks(op) > 0;

        /// <summary>
        /// Damage the target's shield can still absorb, or 0 if unshielded.
        /// For the view.
        /// </summary>
        /// <remarks>
        /// The badge layer cannot infer this from the event stream: the pool is
        /// decremented inside <see cref="AbsorbFrom"/> during damage resolution,
        /// and a partial absorb emits no status event at all. A shield drawn as
        /// a binary on/off badge would tell a player a 1-point remnant is the
        /// same protection as a fresh plate.
        /// </remarks>
        public int ShieldPool(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            var entry = AppliedActiveEntry(op, StatusKind.Shield);
            return entry == null ? 0 : Math.Max(0, (int)entry.Magnitude);
        }

        /// <summary>
        /// Whether <paramref name="target"/> can be picked as a single target by
        /// an operator of <paramref name="by"/>.
        /// </summary>
        /// <remarks>
        /// Stealth scopes untargetability to <b>enemies only</b>, so it never
        /// locks an operator out of its own team's repositioning or healing
        /// (§5.4). It is also the whole of what stealth does: AOE, passive auras,
        /// collision and already-applied bleed or marks all still reach it.
        /// </remarks>
        public bool CanBeSingleTargetedBy(OperatorState target, PlayerColor by)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (!Has(target, StatusKind.Stealth)) return true;
            return target.Owner == by;
        }

        /// <summary>
        /// Every status currently active on an operator, applied and passive
        /// alike. For the view to draw badges.
        /// </summary>
        /// <remarks>
        /// <b>The view cannot infer this from the event stream.</b> Passives are
        /// granted at match start without an event, and bleed stacks are
        /// consumed at upkeep without a <c>StatusExpired</c>. A badge layer built
        /// from events drifts from the truth, and a board that lies about status
        /// is worse than one that shows none.
        ///
        /// An applied status shadows a passive of the same kind, matching
        /// <see cref="ActiveEntry"/> and <see cref="SpeedModifier"/>, so a kind
        /// is never reported twice.
        /// </remarks>
        public IReadOnlyList<StatusKind> ActiveKinds(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            int ownerTurn = _clock.TurnIndexOf(op.Owner);
            var kinds = new List<StatusKind>();

            Dictionary<StatusKind, Entry> applied;
            Dictionary<StatusKind, Entry> passives;

            _byOperator.TryGetValue(op.Id, out applied);
            _passives.TryGetValue(op.Id, out passives);

            if (applied != null)
            {
                foreach (var pair in applied)
                    if (IsActive(pair.Value, ownerTurn)) kinds.Add(pair.Key);
            }

            if (passives != null)
            {
                foreach (var pair in passives)
                    if (applied == null || !applied.ContainsKey(pair.Key)) kinds.Add(pair.Key);
            }

            return kinds;
        }

        /// <summary>Who marked this operator, or null if unmarked (§5.7).</summary>
        public int? MarkedBy(OperatorState op) => ActiveEntry(op, StatusKind.Mark)?.SourceOperatorId;

        // ── Turn boundaries ──────────────────────────────────────────────

        /// <summary>
        /// Removes every bleed stack and returns the Atomic damage they owe.
        /// Called at the holder's upkeep.
        /// </summary>
        /// <remarks>
        /// Bleed is delayed damage, not a lingering condition: a stack ticks once
        /// and is gone. The caller pushes the returned amount through the damage
        /// pipeline, which is where it may neutralize — an operator dying at
        /// upkeep never gets its turn (§5.3).
        /// </remarks>
        public int ConsumeBleed(OperatorState op)
        {
            var entry = ActiveEntry(op, StatusKind.Bleed);
            if (entry == null) return 0;

            int damage = entry.Stacks * _config.BleedDamagePerStack;
            EntriesFor(op.Id).Remove(StatusKind.Bleed);
            return damage;
        }

        /// <summary>
        /// Atomic damage the mark owes this turn, or 0 if unmarked. Called at the
        /// marked operator's upkeep.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="ConsumeBleed"/> this leaves the entry standing. A
        /// mark is a lingering condition that bills every turn until its duration
        /// runs out, cleared only by expiry, neutralize, or its payout firing
        /// (§5.7) — named <c>Tick</c> rather than <c>Consume</c> for exactly that
        /// reason. Like bleed, it may neutralize, and an operator dying at
        /// upkeep never gets its turn.
        /// </remarks>
        public int MarkTickDamage(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            return ActiveEntry(op, StatusKind.Mark) != null ? _config.MarkDamagePerTurn : 0;
        }

        /// <summary>
        /// Re-arms the evasion charge. Called at the holder's upkeep, which makes
        /// "round" mean <i>since this operator's last turn began</i> — the window
        /// during which opponents actually attack it (§5.5).
        /// </summary>
        public void RefreshEvasion(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            _evasionSpentThisRound.Remove(op.Id);
        }

        /// <summary>
        /// Sweeps statuses whose last active turn is the one now closing, and
        /// reports them so the caller can emit <c>StatusExpired</c> events.
        /// Passives are never swept.
        /// </summary>
        /// <remarks>
        /// <b>Called at End of turn, which is why the comparison includes the
        /// current turn.</b> A 1-turn stun applied during an opponent's turn is
        /// active for exactly the target's next turn; at the end of that turn it
        /// is done. Testing for <c>ownerTurn &gt; lastActive</c> here would leave
        /// it standing until the turn after, which is the very off-by-one the
        /// absolute-index model exists to remove.
        ///
        /// Queries elsewhere stay lazy and use the stricter comparison, so a
        /// status is correctly active <i>during</i> its final turn. Skipping this
        /// call would therefore never change a rule outcome — only what the view
        /// is told, and when.
        ///
        /// <b>A shield can leave by either door.</b> Duration expires it here; a
        /// spent pool removes it in <see cref="AbsorbFrom"/>. Whichever comes
        /// first, and the other then finds nothing.
        /// </remarks>
        public IReadOnlyList<StatusKind> ExpireCompleted(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            var entries = EntriesFor(op.Id);
            int ownerTurn = _clock.TurnIndexOf(op.Owner);
            var expired = new List<StatusKind>();

            foreach (var pair in entries)
            {
                if (pair.Value.LastActiveTurn != Permanent && ownerTurn >= pair.Value.LastActiveTurn)
                    expired.Add(pair.Key);
            }

            foreach (var kind in expired) entries.Remove(kind);
            return expired;
        }

        /// <summary>
        /// Strips every applied status from an operator and reports what went,
        /// leaving passives and the evasion charge alone. Javi's Neural Purge.
        /// </summary>
        /// <remarks>
        /// <b>Not <see cref="ClearAll"/>.</b> That method also clears the
        /// spent-evasion flag, which on neutralize is correct — the operator is
        /// leaving the board. Here it would silently re-arm an ally's evasion
        /// charge mid-round, a second benefit nobody asked the ability for.
        ///
        /// Statuses that have not taken hold yet go too. A stun applied on an
        /// opponent's turn is not active until its target's next one (§5), and a
        /// cleanse that could not remove it would be unable to answer the only
        /// window in which it matters.
        ///
        /// <b>It strips a shield along with everything else.</b> A cleanse is
        /// indiscriminate by design, so Neural Purge on a plated ally destroys
        /// the plate — a real cost of casting the two in the wrong order, and
        /// one the player can see coming from the badge.
        /// </remarks>
        public IReadOnlyList<StatusKind> ClearApplied(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            if (!_byOperator.TryGetValue(op.Id, out var entries) || entries.Count == 0)
                return Array.Empty<StatusKind>();

            var removed = new List<StatusKind>(entries.Keys);
            _byOperator.Remove(op.Id);
            return removed;
        }

        /// <summary>
        /// Removes one applied status by kind. Returns whether anything was
        /// removed.
        /// </summary>
        /// <remarks>
        /// <b>For an effect that consumes its own marker.</b> Zero-Day's
        /// detonation strips the charge it just fired (§5.10), and bleed's tick
        /// already removes its own entry the same way, from inside this class.
        /// <see cref="ClearApplied"/> is the indiscriminate version and stays
        /// the cleanse's: an effect that knows exactly what it is taking should
        /// not also strip whatever else the operator happens to be carrying.
        /// Passives are unreachable here, as they are from every applied-status
        /// path.
        /// </remarks>
        public bool Remove(OperatorState op, StatusKind kind)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            return _byOperator.TryGetValue(op.Id, out var entries) && entries.Remove(kind);
        }

        /// <summary>
        /// Strips every applied status on neutralize — stun, slow, bleed,
        /// stealth, shield, mark, haste (§1.2). <b>Passives survive</b>: they
        /// live in their own store and an operator returning to the yard is
        /// still itself.
        /// </summary>
        public void ClearAll(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            _byOperator.Remove(op.Id);
            _evasionSpentThisRound.Remove(op.Id);
        }

        // ── IDamageMitigation ────────────────────────────────────────────

        /// <summary>
        /// The first Normal instance each round may be negated on a seeded roll
        /// at <c>CombatConfig.EvasionChance</c>. Every instance after it that
        /// round lands automatically.
        /// </summary>
        /// <remarks>
        /// <b>The charge is spent on the attempt, not the success.</b> If a
        /// failed roll left it intact the holder would keep rolling against
        /// every hit until one landed, and the per-round cap — the thing that
        /// bounds the worst case — would stop binding at all.
        ///
        /// The cap is load-bearing. Uncapped, a roll across the half-dozen
        /// attacks a target sees in a match does not average out; it decides
        /// games, and it can eat a four-turn ultimate investment in one go
        /// (§5.5).
        /// </remarks>
        public bool TryEvade(OperatorState target, IRandom random)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (random == null) throw new ArgumentNullException(nameof(random));

            if (!Has(target, StatusKind.Evasion)) return false;
            if (_evasionSpentThisRound.Contains(target.Id)) return false;

            _evasionSpentThisRound.Add(target.Id);
            return random.NextDouble() < _config.EvasionChance;
        }

        /// <summary>
        /// Takes what the shield pool can from <paramref name="amount"/>,
        /// decrements the pool by what it took, and removes the shield once the
        /// pool is spent (§5.6).
        /// </summary>
        /// <remarks>
        /// <b>Applied entries only.</b> A passive shield would be read here and
        /// then not removed, because passives live in a store
        /// <see cref="EntriesFor"/> cannot reach — leaving an operator carrying
        /// a permanently exhausted, permanently uncleanable plate. Nothing
        /// grants one today; this reads narrowly so nothing can start.
        ///
        /// A pool found already at or below zero is swept rather than returned
        /// as a no-op, so a shield can never outlive its own usefulness by
        /// sitting in the registry drawing a badge.
        /// </remarks>
        public int AbsorbFrom(OperatorState target, int amount)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (amount <= 0) return 0;

            var entry = AppliedActiveEntry(target, StatusKind.Shield);
            if (entry == null) return 0;

            int pool = (int)entry.Magnitude;
            if (pool <= 0)
            {
                EntriesFor(target.Id).Remove(StatusKind.Shield);
                return 0;
            }

            int absorbed = Math.Min(pool, amount);
            entry.Magnitude = pool - absorbed;

            if (entry.Magnitude <= 0) EntriesFor(target.Id).Remove(StatusKind.Shield);

            return absorbed;
        }

        /// <summary>
        /// Whether the target holds an active tech ward (§5.12). Luka's
        /// Hermes' Ring.
        /// </summary>
        /// <remarks>
        /// Reads passives too, through <see cref="Has"/>: a permanent ward would
        /// be a permanent immunity, and nothing grants one today. Unlike the
        /// shield nothing is decremented, so there is no store it could fail to
        /// clean up.
        /// </remarks>
        public bool BlocksTech(OperatorState target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            return Has(target, StatusKind.TechWard);
        }

        // ── Internals ────────────────────────────────────────────────────

        private Dictionary<StatusKind, Entry> EntriesFor(int operatorId) =>
            StoreFor(_byOperator, operatorId);

        private Dictionary<StatusKind, Entry> PassivesFor(int operatorId) =>
            StoreFor(_passives, operatorId);

        private static Dictionary<StatusKind, Entry> StoreFor(
            Dictionary<int, Dictionary<StatusKind, Entry>> store, int operatorId)
        {
            if (!store.TryGetValue(operatorId, out var entries))
            {
                entries = new Dictionary<StatusKind, Entry>();
                store[operatorId] = entries;
            }

            return entries;
        }

        private static bool IsActive(Entry entry, int ownerTurn)
        {
            if (ownerTurn < entry.FirstActiveTurn) return false;
            return entry.LastActiveTurn == Permanent || ownerTurn <= entry.LastActiveTurn;
        }

        /// <summary>
        /// The live entry for a kind, or null. An applied status takes
        /// precedence over a passive of the same kind, so a temporary override
        /// works without the passive having to be removed and restored.
        /// </summary>
        private Entry ActiveEntry(OperatorState op, StatusKind kind)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            int ownerTurn = _clock.TurnIndexOf(op.Owner);

            if (_byOperator.TryGetValue(op.Id, out var applied)
                && applied.TryGetValue(kind, out var entry)
                && IsActive(entry, ownerTurn))
            {
                return entry;
            }

            if (_passives.TryGetValue(op.Id, out var passives)
                && passives.TryGetValue(kind, out var passive))
            {
                return passive;
            }

            return null;
        }

        /// <summary>
        /// The live <i>applied</i> entry for a kind, ignoring passives. For
        /// callers that mutate the entry in place and must be able to remove it.
        /// </summary>
        private Entry AppliedActiveEntry(OperatorState op, StatusKind kind)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            int ownerTurn = _clock.TurnIndexOf(op.Owner);

            if (_byOperator.TryGetValue(op.Id, out var applied)
                && applied.TryGetValue(kind, out var entry)
                && IsActive(entry, ownerTurn))
            {
                return entry;
            }

            return null;
        }

        /// <summary>
        /// The size a kind takes when an ability does not state one. Signed for
        /// the speed kinds: negative slows, positive hastens. For
        /// <see cref="StatusKind.Shield"/> it is a damage pool, always positive.
        /// </summary>
        /// <remarks>
        /// <b>Shield must have a non-zero default.</b> Under the old
        /// whole-instance absorb its magnitude was never read, so a shield
        /// applied without one worked fine. Under a pool, a zero default
        /// produces a shield that exists, draws a badge, absorbs nothing, and
        /// cannot be distinguished from a real one until the hit lands.
        /// </remarks>
        private double DefaultMagnitudeFor(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Slow: return -_config.SlowSpeedPenalty;
                case StatusKind.Shield: return _config.ShieldPoolDefault;
                default: return 0.0;
            }
        }
    }
}