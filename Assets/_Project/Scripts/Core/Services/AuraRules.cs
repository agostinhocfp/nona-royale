// Assets/_Project/Scripts/Core/Services/AuraRules.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Evaluates aura effects at the moment they matter, rather than tracking
    /// them as state.
    /// </summary>
    /// <remarks>
    /// Auras are recomputed on demand because their truth changes with every
    /// move by either party. Storing them would mean re-deriving the whole board
    /// after each step and emitting apply/remove events nobody needs.
    ///
    /// Which operators project which aura is supplied at composition, so this
    /// service knows nothing about the roster.
    ///
    /// <b>Two sides since 2026-09-17.</b> An aura reaches enemies (Bouncer) or
    /// the projector's own side (Lethe) — which in a team match is both of
    /// that player's seats (ADR-0012). The projector is never its own
    /// recipient: Lethe's own haste is her passive, not her aura.
    /// </remarks>
    public sealed class AuraRules
    {
        private readonly TargetingRules _targeting;
        private readonly IReadOnlyDictionary<int, AuraDefinition> _auras;
        private readonly SanctuaryRules _sanctuary;

        /// <param name="sanctuary">
        /// Where a slow is refused (§4.4, third amendment). Null refuses none,
        /// as before the amendment.
        /// </param>
        public AuraRules(TargetingRules targeting, IReadOnlyDictionary<int, AuraDefinition> auras,
            SanctuaryRules sanctuary = null)
        {
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            _auras = auras ?? throw new ArgumentNullException(nameof(auras));
            _sanctuary = sanctuary;
        }

        /// <summary>
        /// Total speed change on an operator from every aura reaching it.
        /// </summary>
        /// <remarks>
        /// <b>Strongest bonus plus strongest penalty (2026-09-17).</b> Within a
        /// sign, auras from several sources take the largest single effect, not
        /// the sum — the same rule slows follow (§5.2), so two Bouncers are a
        /// positioning problem, not a hard stop. Across signs they add.
        ///
        /// The rule it replaces took the largest by absolute value, keeping the
        /// first one met on a tie. With only negative auras that was the same
        /// thing. With a +0.5 and a −0.5 in reach it made the answer depend on
        /// operator id order. Resolving each sign on its own makes the result
        /// independent of iteration order by construction.
        ///
        /// No shipped aura carries a positive speed delta — Catalyst grants
        /// haste instead — so today this returns exactly what it returned
        /// before. The rule is fixed now so the first positive speed aura does
        /// not have to find the bug.
        /// </remarks>
        public double SpeedModifierFor(OperatorState op, IReadOnlyList<OperatorState> allOperators)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            if (allOperators == null) throw new ArgumentNullException(nameof(allOperators));
            if (!_targeting.IsInPlay(op)) return 0.0;

            double strongestBonus = 0.0;
            double strongestPenalty = 0.0;

            foreach (var source in allOperators)
            {
                var aura = Reaching(source, op);
                if (aura == null) continue;

                if (aura.SpeedModifier > strongestBonus) strongestBonus = aura.SpeedModifier;
                if (aura.SpeedModifier < strongestPenalty) strongestPenalty = aura.SpeedModifier;
            }

            // A drag is a slow in every sense a player means, applied by
            // standing close rather than by a cast, and an operator on its own
            // spawn cell cannot be slowed (§4.4, third amendment). Without this
            // a Bouncer parked beside a start cell would still take the first
            // move off everything that deploys there — the camp the rule
            // exists to stop. Only the penalty goes; a bonus is not a slow.
            if (_sanctuary != null && _sanctuary.Resists(op, StatusKind.Slow)) strongestPenalty = 0.0;

            return strongestBonus + strongestPenalty;
        }

        /// <summary>
        /// Whether any aura reaching this operator right now makes it count as
        /// Hastened (§5.9). Lethe's Catalyst.
        /// </summary>
        /// <remarks>
        /// <b>A yes or no, not a stack.</b> Two Catalysts, or a Catalyst on top
        /// of Tagged From Above's payout, still pay one bonus per roll under
        /// one per-turn cap; the engine owns that bookkeeping.
        ///
        /// Read at the start of a move, where the mover stands. An ally that
        /// walks out of range keeps the cells of the move that took it out,
        /// and has none on the next.
        /// </remarks>
        public bool GrantsHaste(OperatorState op, IReadOnlyList<OperatorState> allOperators)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            if (allOperators == null) throw new ArgumentNullException(nameof(allOperators));
            if (!_targeting.IsInPlay(op)) return false;

            foreach (var source in allOperators)
            {
                var aura = Reaching(source, op);
                if (aura != null && aura.GrantsHaste) return true;
            }

            return false;
        }

        /// <summary>
        /// The track cells <paramref name="source"/>'s aura covers right now:
        /// its radius either way, and its trail behind it (2026-09-24). Empty
        /// for an operator with no aura or out of play. For the view's lane.
        /// </summary>
        public IReadOnlyList<CellRef> CellsCovered(OperatorState source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var cells = new List<CellRef>();
            if (!_auras.TryGetValue(source.Id, out var aura) || !_targeting.IsInPlay(source)) return cells;

            var centre = _targeting.CellOf(source);
            if (!centre.IsOnTrack) return cells;

            int circuit = _targeting.CircuitLength;
            var seen = new HashSet<int>();

            void Add(int offset)
            {
                int index = ((centre.Index + offset) % circuit + circuit) % circuit;
                if (seen.Add(index)) cells.Add(CellRef.Track(index));
            }

            for (int step = -aura.Radius; step <= aura.Radius; step++) Add(step);
            for (int step = 1; step <= aura.Trail; step++) Add(-step);

            return cells;
        }

        /// <summary>Which side <paramref name="source"/>'s aura reaches, or null for none.</summary>
        public AuraSide? SideOf(OperatorState source) =>
            source != null && _auras.TryGetValue(source.Id, out var aura) ? aura.Side : (AuraSide?)null;

        /// <summary>
        /// The aura <paramref name="source"/> projects onto <paramref name="op"/>
        /// right now, or null: right side, both in play, within radius.
        /// </summary>
        private AuraDefinition Reaching(OperatorState source, OperatorState op)
        {
            if (source == null || ReferenceEquals(source, op)) return null;
            if (!_auras.TryGetValue(source.Id, out var aura)) return null;

            // Side, not seat: in a team match Lethe's Catalyst reaches the
            // partner seat and Bouncer's drag does not slow it (ADR-0012).
            // Under free-for-all this is seat equality and nothing changes.
            bool sameSide = _targeting.AreAllied(source.Owner, op.Owner);
            if (aura.Side == AuraSide.Enemies && sameSide) return null;
            if (aura.Side == AuraSide.Allies && !sameSide) return null;

            if (!_targeting.IsInPlay(source)) return null;

            int? distance = _targeting.Distance(source, op);
            if (distance == null) return null;
            if (distance <= aura.Radius) return aura;

            // The slipstream (2026-09-24): the cells behind the projector, along
            // the way everyone travels, reach as far as the trail does.
            if (aura.Trail > 0)
            {
                int? behind = _targeting.StepsBehind(op, source);
                if (behind != null && behind.Value >= 1 && behind.Value <= aura.Trail) return aura;
            }

            return null;
        }
    }
}