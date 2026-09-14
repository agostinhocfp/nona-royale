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
        /// Total speed change on an operator from every enemy aura reaching it.
        /// </summary>
        /// <remarks>
        /// Auras from several sources take the <b>largest</b> single effect, not
        /// the sum — the same rule slows follow (§5.2). Two Bouncers should be a
        /// positioning problem, not a hard stop.
        /// </remarks>
        public double SpeedModifierFor(OperatorState op, IReadOnlyList<OperatorState> allOperators)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            if (allOperators == null) throw new ArgumentNullException(nameof(allOperators));
            if (!_targeting.IsInPlay(op)) return 0.0;

            double strongest = 0.0;

            foreach (var source in allOperators)
            {
                if (source == null || ReferenceEquals(source, op)) continue;
                if (source.Owner == op.Owner) continue;
                if (!_auras.TryGetValue(source.Id, out var aura)) continue;
                if (!_targeting.IsInPlay(source)) continue;

                int? distance = _targeting.Distance(source, op);
                if (distance == null || distance > aura.Radius) continue;

                if (Math.Abs(aura.SpeedModifier) > Math.Abs(strongest))
                    strongest = aura.SpeedModifier;
            }

            return strongest;
        }
    }
}