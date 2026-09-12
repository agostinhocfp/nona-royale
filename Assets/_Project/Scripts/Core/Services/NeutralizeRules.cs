// Assets/_Project/Scripts/Core/Services/NeutralizeRules.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Everything that happens to an operator reduced to zero health
    /// (COMBAT_SYSTEMS §1.2), including Tagged From Above's payout (§10.2).
    /// </summary>
    /// <remarks>
    /// Its own service because four different things neutralize — a collision,
    /// an ability, a bleed tick and a mark tick — and each would otherwise carry
    /// its own copy of the consequences. That is the shape of the bug ADR-0004
    /// was written about, and it is also why the payout lives here: the mark is
    /// read at the one place every death funnels through, so no call site can
    /// forget it.
    ///
    /// <b>Neutralize is a setback, not a removal.</b> There is no permanent
    /// death in the MVP: the operator goes back to its yard at full health and
    /// re-enters on a 6 like any other deployment. Its passives survive, because
    /// a passive is who an operator is (§5.1) — <c>StatusRegistry</c> keeps them
    /// in a store <see cref="StatusRegistry.ClearAll"/> does not touch.
    /// </remarks>
    public sealed class NeutralizeRules
    {
        private static readonly OperatorState[] NoPayout = new OperatorState[0];

        private readonly StatusRegistry _statuses;
        private readonly AbilityResolver _abilities;
        private readonly IReadOnlyList<OperatorState> _operators;
        private readonly CombatConfig _config;

        public NeutralizeRules(
            StatusRegistry statuses,
            AbilityResolver abilities,
            IReadOnlyList<OperatorState> operators,
            CombatConfig config)
        {
            _statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
            _operators = operators ?? throw new ArgumentNullException(nameof(operators));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Sends an operator to its yard and returns whichever operators were
        /// hastened by a mark payout — empty when the dead operator was not
        /// marked. The caller emits the events; this service owns the state.
        /// </summary>
        public IReadOnlyList<OperatorState> Apply(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            // Read the mark before anything clears it. Neutralize strips every
            // status, so a payout resolved after ClearAll would find nothing —
            // the mark's whole purpose is to be readable at this instant.
            var hastened = PayOutMark(op);

            op.MoveTo(PathMap.YardProgress);   // track progress entirely lost
            op.RestoreHealth();
            _statuses.ClearAll(op);
            _abilities.ResetCooldowns(op);

            // Energy is untouched: the pool is player-level, so a yarded
            // operator costs its owner nothing economically.

            return hastened;
        }

        /// <summary>
        /// Grants the marker's whole squad <c>Hastened</c> if the operator dying
        /// here carried a mark (§10.2).
        /// </summary>
        /// <remarks>
        /// <b>Any death of a marked operator pays out</b>, whatever killed it —
        /// a collision, an ability, the mark's own tick. That matches the design
        /// intent that the mark <i>hands</i> a kill to its owner's squad rather
        /// than scoring one itself.
        ///
        /// It does mean an operator who kills itself while marked — Bouncer's
        /// All-In Mauling is the only route — pays out the enemy squad that
        /// marked him. Checking the killer instead would need the killer's id
        /// threaded through <c>DamageResult</c>, which does not currently carry
        /// it. That is the same change bleed attribution needs, and both are
        /// deferred to one commit rather than half-solved here.
        /// </remarks>
        private IReadOnlyList<OperatorState> PayOutMark(OperatorState dying)
        {
            int? markerId = _statuses.MarkedBy(dying);
            if (markerId == null) return NoPayout;

            OperatorState marker = FindOperator(markerId.Value);

            // The marker may itself have been neutralized since casting, and a
            // yarded operator is still an operator — its squad still collects.
            if (marker == null) return NoPayout;

            var squad = new List<OperatorState>();

            foreach (var candidate in _operators)
            {
                if (candidate.Owner != marker.Owner) continue;

                _statuses.Apply(candidate, StatusKind.Hastened, _config.HasteDurationTurns);
                squad.Add(candidate);
            }

            return squad;
        }

        private OperatorState FindOperator(int id)
        {
            foreach (var op in _operators)
                if (op.Id == id) return op;

            return null;
        }
    }
}