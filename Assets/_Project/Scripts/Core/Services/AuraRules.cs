// Assets/_Project/Scripts/Core/Services/AuraRules.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
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
    /// the projector's own squad (Lethe). The projector is never its own
    /// recipient: Lethe's own haste is her passive, not her aura.
    /// </remarks>
    public sealed class AuraRules
    {
        private readonly TargetingRules _targeting;
        private readonly IReadOnlyDictionary<int, AuraDefinition> _auras;

        public AuraRules(TargetingRules targeting, IReadOnlyDictionary<int, AuraDefinition> auras)
        {
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            _auras = auras ?? throw new ArgumentNullException(nameof(auras));
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
        /// The aura <paramref name="source"/> projects onto <paramref name="op"/>
        /// right now, or null: right side, both in play, within radius.
        /// </summary>
        private AuraDefinition Reaching(OperatorState source, OperatorState op)
        {
            if (source == null || ReferenceEquals(source, op)) return null;
            if (!_auras.TryGetValue(source.Id, out var aura)) return null;

            bool sameSeat = source.Owner == op.Owner;
            if (aura.Side == AuraSide.Enemies && sameSeat) return null;
            if (aura.Side == AuraSide.Allies && !sameSeat) return null;

            if (!_targeting.IsInPlay(source)) return null;

            int? distance = _targeting.Distance(source, op);
            if (distance == null || distance > aura.Radius) return null;

            return aura;
        }
    }
}