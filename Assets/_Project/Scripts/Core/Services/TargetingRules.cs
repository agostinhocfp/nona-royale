// Assets/_Project/Scripts/Core/Services/TargetingRules.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Who can be hit by what, and from where (COMBAT_SYSTEMS §4).
    /// </summary>
    /// <remarks>
    /// <b>Reachability, not defence.</b> Everything this type enforces — home
    /// columns, stealth, range — decides whether damage may be <i>aimed</i>.
    /// Mitigation is a separate question the pipeline asks later. That split is
    /// what makes Atomic coherent: Atomic ignores every mitigation layer, but it
    /// never reaches something this type refuses to target. <i>Atomic can't be
    /// blocked, but it can't reach what it can't touch.</i>
    ///
    /// <b>Safe cells do not appear here at all.</b> Safe means safe from
    /// collision and nothing more — an operator standing on a start cell can be
    /// shot, pulled, stunned and bled. If safe cells blocked abilities they would
    /// become free parking and the combat layer would stall on them (§4.4).
    /// </remarks>
    public sealed class TargetingRules
    {
        private readonly PathMap _map;
        private readonly StatusRegistry _statuses;

        public TargetingRules(PathMap map, StatusRegistry statuses)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
        }

        /// <summary>
        /// Whether an operator is in the fight at all: deployed, on the shared
        /// loop, not in a home column and not home.
        /// </summary>
        /// <remarks>
        /// One predicate for both ends of a targeting check, because §4.3 makes
        /// the rule symmetric — an operator in its home column can neither be
        /// targeted nor target.
        /// </remarks>
        public bool IsInPlay(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            return _map.IsOnOuterTrack(op.Progress);
        }

        /// <summary>
        /// Steps along the circuit between two operators, in whichever direction
        /// is shorter. Null when either is off the loop.
        /// </summary>
        public int? Distance(OperatorState a, OperatorState b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (!IsInPlay(a) || !IsInPlay(b)) return null;

            return _map.TrackDistance(
                _map.CellAt(a.Owner, a.Progress),
                _map.CellAt(b.Owner, b.Progress));
        }

        /// <summary>
        /// Full legality for picking one operator as an ability's target.
        /// Works for allies as well as enemies — Velvet Rope and All-In Mauling
        /// both have friendly modes, and stealth is scoped so it never blocks a
        /// teammate.
        /// </summary>
        public TargetingResult CanSingleTarget(OperatorState caster, OperatorState target, int range)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (range < 0) throw new ArgumentOutOfRangeException(nameof(range));

            if (!IsInPlay(caster))
                return TargetingResult.Illegal(TargetingVerdict.CasterOutOfPlay);

            if (!IsInPlay(target))
                return TargetingResult.Illegal(TargetingVerdict.TargetOutOfPlay);

            int? distance = Distance(caster, target);
            if (distance == null || distance > range)
                return TargetingResult.Illegal(TargetingVerdict.OutOfRange, distance);

            // Checked last so "out of range" wins over "stealthed" — a player
            // who cannot reach a target does not need to learn it was hidden.
            if (!_statuses.CanBeSingleTargetedBy(target, caster.Owner))
                return TargetingResult.Illegal(TargetingVerdict.Stealthed, distance);

            return TargetingResult.Legal(distance.Value);
        }

        // ── Area of effect ───────────────────────────────────────────────

        /// <summary>
        /// Cells covered by "within N": N steps in each direction from the
        /// origin, origin included, so <c>2N + 1</c> (§4.2).
        /// </summary>
        public int AreaCellCount(int radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            return (2 * radius) + 1;
        }

        /// <summary>Whether a cell falls inside an area centred on <paramref name="origin"/>.</summary>
        public bool IsInArea(CellRef origin, CellRef cell, int radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));

            int? distance = _map.TrackDistance(origin, cell);
            return distance != null && distance <= radius;
        }

        /// <summary>
        /// Every enemy of <paramref name="casterColor"/> inside an area.
        /// </summary>
        /// <remarks>
        /// <b>Stealth does not protect against this.</b> Stealth stops an
        /// operator being <i>aimed at</i>, not from being in the room (§5.4), so
        /// Ace Shards and Dargin Pulse sweep it up like anyone else.
        ///
        /// <paramref name="exclude"/> exists for Miracle Pull, whose splash
        /// originates on the primary target and leaves that target out — it has
        /// already taken the direct hit (§4.2).
        /// </remarks>
        public IReadOnlyList<OperatorState> EnemiesInArea(
            CellRef origin,
            int radius,
            PlayerColor casterColor,
            IEnumerable<OperatorState> candidates,
            OperatorState exclude = null)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (casterColor == PlayerColor.None)
                throw new ArgumentException("An area needs a caster's seat to know who its enemies are.", nameof(casterColor));

            var hit = new List<OperatorState>();

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                if (candidate.Owner == casterColor) continue;
                if (exclude != null && ReferenceEquals(candidate, exclude)) continue;
                if (!IsInPlay(candidate)) continue;

                if (IsInArea(origin, _map.CellAt(candidate.Owner, candidate.Progress), radius))
                    hit.Add(candidate);
            }

            return hit;
        }

        /// <summary>
        /// Every operator belonging to <paramref name="casterColor"/> inside an
        /// area. Javi's Nanite Infusion, whose splash heal is centred on the
        /// enemy it just damaged.
        /// </summary>
        /// <remarks>
        /// <b>The caster is included when it stands close enough.</b> It is an
        /// ally in the area, and excluding it would be an arbitrary carve-out —
        /// the ability already prices being near an enemy, which is the tension
        /// it exists for.
        ///
        /// Stealth is irrelevant here for a different reason than in
        /// <see cref="EnemiesInArea"/>: stealth is scoped to enemies only (§5.4),
        /// so it never hides an operator from its own side under any
        /// circumstances.
        /// </remarks>
        public IReadOnlyList<OperatorState> AlliesInArea(
            CellRef origin,
            int radius,
            PlayerColor casterColor,
            IEnumerable<OperatorState> candidates,
            OperatorState exclude = null)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (casterColor == PlayerColor.None)
                throw new ArgumentException("An area needs a caster's seat to know who its allies are.", nameof(casterColor));

            var hit = new List<OperatorState>();

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                if (candidate.Owner != casterColor) continue;
                if (exclude != null && ReferenceEquals(candidate, exclude)) continue;
                if (!IsInPlay(candidate)) continue;

                if (IsInArea(origin, _map.CellAt(candidate.Owner, candidate.Progress), radius))
                    hit.Add(candidate);
            }

            return hit;
        }

        /// <summary>The cell an operator occupies, for use as an area's origin.</summary>
        public CellRef CellOf(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            return _map.CellAt(op.Owner, op.Progress);
        }
    }
}