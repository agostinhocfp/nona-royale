// Assets/_Project/Scripts/Core/Services/NeutralizeRules.cs
using System;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Everything that happens to an operator reduced to zero health
    /// (COMBAT_SYSTEMS §1.2).
    /// </summary>
    /// <remarks>
    /// Its own service because three different things neutralize — a collision,
    /// an ability, and a bleed tick — and each would otherwise carry its own
    /// copy of the consequences. That is the shape of the bug ADR-0004 was
    /// written about.
    ///
    /// <b>Neutralize is a setback, not a removal.</b> There is no permanent
    /// death in the MVP: the operator goes back to its yard at full health and
    /// re-enters on a 6 like any other deployment.
    /// </remarks>
    public sealed class NeutralizeRules
    {
        private readonly StatusRegistry _statuses;
        private readonly AbilityResolver _abilities;

        public NeutralizeRules(StatusRegistry statuses, AbilityResolver abilities)
        {
            _statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
        }

        public void Apply(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            op.MoveTo(PathMap.YardProgress);   // track progress entirely lost
            op.RestoreHealth();
            _statuses.ClearAll(op);
            _abilities.ResetCooldowns(op);

            // Energy is untouched: the pool is player-level, so a yarded
            // operator costs its owner nothing economically.
        }
    }
}