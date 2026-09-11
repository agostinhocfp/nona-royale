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
    /// <b>Bleed is reported, never applied here.</b> Bleed deals damage, and the
    /// damage pipeline consults this registry for evasion and shields — having
    /// the registry call the pipeline would close a dependency cycle. So
    /// <see cref="ConsumeBleed"/> returns what the tick is worth and clears the
    /// stacks; the caller feeds it through the pipeline as Atomic damage. The
    /// registry decides <i>what</i> bleed owes, the pipeline decides how damage
    /// lands, and neither knows the other exists.
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
        /// Re-application refreshes the duration and keeps the larger magnitude.
        /// Only <see cref="StatusKind.Bleed"/> accumulates stacks (§5.3).
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

            int ownerTurn = _clock.TurnIndexOf(target.Owner);
            bool onTargetsOwnTurn = _clock.ActivePlayer == target.Owner;
            int firstActive = onTargetsOwnTurn ? ownerTurn : ownerTurn + 1;

            var entries = EntriesFor(target.Id);

            if (entries.TryGetValue(kind, out var existing))
            {
                existing.FirstActiveTurn = Math.Min(existing.FirstActiveTurn, firstActive);
                existing.LastActiveTurn = Math.Max(existing.LastActiveTurn, firstActive + duration - 1);
                existing.Magnitude = Math.Max(existing.Magnitude, magnitude);
                existing.SourceOperatorId = sourceOperatorId;

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
        /// Grants a permanent passive — Kurbyn's Evasive Protocol. Never expires
        /// and is unaffected by stun: a passive is who an operator is, not what
        /// it does (§5.1).
        /// </summary>
        public void ApplyPassive(OperatorState target, StatusKind kind, double magnitude = 0.0)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            EntriesFor(target.Id)[kind] = new Entry
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
        /// Total speed modifier from slows. Sources do not stack; the largest
        /// applies (§5.2). Returned negative, ready to hand to
        /// <c>MovementResolver.EffectiveSpeed</c>.
        /// </summary>
        public double SpeedModifier(OperatorState op) =>
            Has(op, StatusKind.Slow) ? -_config.SlowSpeedPenalty : 0.0;

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
        /// Whether <paramref name="target"/> can be picked as a single target by
        /// an operator of <paramref name="by"/>.
        /// </summary>
        /// <remarks>
        /// Stealth scopes untargetability to <b>enemies only</b>, so it never
        /// locks an operator out of its own team's repositioning or healing
        /// (§5.4). It is also the whole of what stealth does: AOE, passive auras,
        /// collision and already-applied bleed all still reach it.
        /// </remarks>
        public bool CanBeSingleTargetedBy(OperatorState target, PlayerColor by)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (!Has(target, StatusKind.Stealth)) return true;
            return target.Owner == by;
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
        /// Strips everything on neutralize — stun, slow, bleed, stealth, shield,
        /// mark (§1.2). Passives are re-granted by whoever rebuilds the operator,
        /// since an operator returning to the yard is still itself.
        /// </summary>
        public void ClearAll(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            _byOperator.Remove(op.Id);
            _evasionSpentThisRound.Remove(op.Id);
        }

        // ── IDamageMitigation ────────────────────────────────────────────

        /// <summary>
        /// The first Normal instance each round may be negated on a seeded roll.
        /// Every instance after it that round lands automatically.
        /// </summary>
        /// <remarks>
        /// The per-round cap is load-bearing. Uncapped, a coin flip across the
        /// half-dozen attacks a target sees in a match does not average out — it
        /// decides games, and it can eat a four-turn ultimate investment on one
        /// roll (§5.5).
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

        public bool TryAbsorb(OperatorState target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (!Has(target, StatusKind.Shield)) return false;

            EntriesFor(target.Id).Remove(StatusKind.Shield);
            return true;
        }

        // ── Internals ────────────────────────────────────────────────────

        private Dictionary<StatusKind, Entry> EntriesFor(int operatorId)
        {
            if (!_byOperator.TryGetValue(operatorId, out var entries))
            {
                entries = new Dictionary<StatusKind, Entry>();
                _byOperator[operatorId] = entries;
            }

            return entries;
        }

        private Entry ActiveEntry(OperatorState op, StatusKind kind)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            if (!_byOperator.TryGetValue(op.Id, out var entries)) return null;
            if (!entries.TryGetValue(kind, out var entry)) return null;

            int ownerTurn = _clock.TurnIndexOf(op.Owner);

            if (ownerTurn < entry.FirstActiveTurn) return null;                      // not yet in effect
            if (entry.LastActiveTurn != Permanent && ownerTurn > entry.LastActiveTurn) return null;

            return entry;
        }
    }
}