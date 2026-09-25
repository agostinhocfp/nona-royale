// Assets/_Project/Scripts/Core/TurnOptions.cs
using System;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;

namespace NonaRoyale.Core
{
    /// <summary>
    /// What the seat to play can still do this turn, asked of the whole match
    /// because the engine alone does not know each operator's abilities.
    /// </summary>
    /// <remarks>
    /// <b>For the view's auto end-turn (2026-09-25).</b> A turn where the only
    /// legal command left is <see cref="Commands.EndTurnCommand"/> ends itself,
    /// so nobody waits on a button press that decides nothing. Every answer
    /// here is an engine answer: the view still evaluates no rule.
    /// </remarks>
    public static class TurnOptions
    {
        /// <summary>
        /// True when the current turn has nothing left but ending it: the dice
        /// are rolled, nothing is owed (<see cref="GameEngine.MustSpendRoll"/>),
        /// no optional doubles re-roll is open, no die can be cashed, and no
        /// ability can be cast at any legal aim.
        /// </summary>
        public static bool OnlyEndTurnRemains(MatchFactory.Match match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));

            var engine = match.Engine;
            if (engine.MatchOver || engine.Phase != TurnPhase.Action) return false;
            if (engine.MustSpendRoll || engine.CanRollAgain) return false;

            var seat = engine.CurrentPlayer;
            if (seat == null) return false;

            foreach (var op in seat.Operators)
            {
                // Cashing needs a die that could have moved her, so it is
                // already covered by MustSpendRoll today; asked anyway, so the
                // answer cannot go stale if that rule changes.
                for (int i = 0; i < engine.UnspentDice.Count; i++)
                    if (engine.CanCash(op, engine.UnspentDice[i])) return false;

                if (!match.AbilitiesByOperator.TryGetValue(op.Id, out var abilities) || abilities == null)
                    continue;

                foreach (var ability in abilities)
                    if (IsCastable(engine, op, ability)) return false;
            }

            return true;
        }

        /// <summary>
        /// Whether the ability could be cast right now at some legal aim: ready,
        /// affordable, and with a target or cell to point at when it needs one.
        /// </summary>
        public static bool IsCastable(GameEngine engine, OperatorState caster, AbilityDefinition ability)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (caster == null || ability == null) return false;

            if (engine.CheckAbility(caster, ability) != AbilityAvailability.Ready) return false;
            if (ability.RequiresTarget) return engine.LegalTargetsFor(caster, ability).Count > 0;
            if (ability.RequiresCell) return engine.LegalCellsFor(caster, ability).Count > 0;
            return true;
        }
    }
}
