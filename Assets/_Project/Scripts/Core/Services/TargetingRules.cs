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

        // ── Lines ────────────────────────────────────────────────────────

        /// <summary>
        /// Every enemy standing on the next <paramref name="length"/> cells
        /// ahead of the caster along the loop, in the caster's own direction of
        /// travel. The caster's own cell is excluded. Kian's Inversion Matrix.
        /// </summary>
        /// <remarks>
        /// <b>Directional, unlike every other query here.</b> "Within N" is
        /// symmetric and covers <c>2N + 1</c> cells; this covers exactly
        /// <paramref name="length"/>, all on one side. A symmetric version would
        /// be indistinguishable from <see cref="EnemiesInArea"/> centred on the
        /// caster, which is what Dargin Pulse and Ace Shards already are.
        ///
        /// <b>Direction is the caster's, and it is a rule.</b> Every colour
        /// travels the loop in the same rotational direction, so "ahead" means
        /// increasing track index — the same direction the caster's own progress
        /// carries it. This is the first rule in the game that reads the loop's
        /// orientation rather than only distances along it.
        ///
        /// <b>Computed from track indices, so it wraps and never runs out.</b>
        /// A caster three cells from completing its own lap still projects a
        /// full-length line: the line is cast onto the shared circuit, and where
        /// the caster happens to be in its own journey is irrelevant to where
        /// the emitters point.
        ///
        /// <b>Stealth does not protect against it</b>, for the same reason it
        /// does not protect against an area: it stops an operator being aimed
        /// at, not from standing somewhere (§5.4).
        /// </remarks>
        public IReadOnlyList<OperatorState> EnemiesInLineAhead(
            OperatorState caster,
            int length,
            IEnumerable<OperatorState> candidates)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));

            var hit = new List<OperatorState>();
            if (!IsInPlay(caster)) return hit;

            int circuit = _map.Profile.CircuitLength;
            int casterIndex = CellOf(caster).Index;

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                if (ReferenceEquals(candidate, caster)) continue;
                if (candidate.Owner == caster.Owner) continue;
                if (!IsInPlay(candidate)) continue;

                int candidateIndex = _map.CellAt(candidate.Owner, candidate.Progress).Index;

                // Steps forward only. Zero is the caster's own cell and is not
                // in the line — the emitters fire away from the operator
                // carrying them, not through it.
                int forward = ((candidateIndex - casterIndex) % circuit + circuit) % circuit;

                if (forward >= 1 && forward <= length) hit.Add(candidate);
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