// Assets/_Project/Scripts/Core/Text/Coach.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Services;

namespace NonaRoyale.Core.Text
{
    /// <summary>The first-match tips, in the order they are offered when more than one is due.</summary>
    public enum CoachTip
    {
        // A moment, gone by the next batch: offered first.
        Collision = 0,
        Knockout = 1,
        SafeCell = 2,
        HomeColumn = 3,

        // A state that holds a while.
        Goal = 4,
        Deploy = 5,
        Landings = 6,
        Doubles = 7,
        Ability = 8,
    }

    /// <summary>
    /// First-match tips (LAUNCH_UI_PASS.md, G10a): which rule a human player is
    /// meeting for the first time, and one or two sentences on it.
    /// </summary>
    /// <remarks>
    /// <b>Rules, not clicks.</b> The top bar's prompt already says what to
    /// press ("Deploy with your 6 — click a piece in your yard"), so a tip says
    /// what the prompt cannot: why, and what follows. The gaps are the ones
    /// STRANGER_TEST.md expects a stranger to trip on: what a safe cell does,
    /// that only a 6 leaves the yard, that the bright landing spends the whole
    /// roll.
    ///
    /// <b>Pure, like the rest of <c>Core/Text</c>.</b> <see cref="Due"/> reads
    /// the match and the batch of events the view has just presented, and
    /// answers which unseen tip applies. The view owns what has been seen,
    /// the card and the setting; nothing here is stored.
    ///
    /// <b>The same rule as the glossary: no typed numbers.</b> The deploy
    /// face, the rolls per turn, the pity turns, the collision damage, the
    /// bounty and the energy divisor come from the configs.
    ///
    /// <b>Phase-gated where the engine is not.</b> Ability readiness ignores
    /// the phase (a card can read ready before the roll, and the cast is then
    /// refused), so <see cref="CoachTip.Ability"/> waits for the action phase.
    /// </remarks>
    public static class Coach
    {
        /// <summary>Every tip, in offer order.</summary>
        public static readonly IReadOnlyList<CoachTip> All = (CoachTip[])Enum.GetValues(typeof(CoachTip));

        /// <summary>
        /// The first unseen tip that applies after <paramref name="events"/>,
        /// or null. Moments involving a human seat come first; then states,
        /// on a human seat's turn only.
        /// </summary>
        /// <param name="isHuman">Whether a seat is played by a person here.</param>
        /// <param name="seen">Whether a tip has already been shown.</param>
        public static CoachTip? Due(MatchFactory.Match match, IReadOnlyList<IGameEvent> events,
            Func<PlayerColor, bool> isHuman, Func<CoachTip, bool> seen)
        {
            if (match == null || isHuman == null || seen == null) return null;

            foreach (var tip in All)
                if (!seen(tip) && Applies(tip, match, events, isHuman))
                    return tip;

            return null;
        }

        /// <summary>Whether <paramref name="tip"/> applies now. Public for the tests.</summary>
        public static bool Applies(CoachTip tip, MatchFactory.Match match, IReadOnlyList<IGameEvent> events,
            Func<PlayerColor, bool> isHuman)
        {
            var engine = match.Engine;
            if (engine.MatchOver) return false;

            switch (tip)
            {
                case CoachTip.Collision: return AnyCollision(events, isHuman);
                case CoachTip.Knockout: return AnyKnockout(events, isHuman);
                case CoachTip.SafeCell: return AnySafeStop(match, events, isHuman);
                case CoachTip.HomeColumn: return AnyHomeEntry(match, events, isHuman);
            }

            // States: only while a person is playing the turn.
            if (!isHuman(engine.CurrentPlayer.Color)) return false;

            switch (tip)
            {
                case CoachTip.Goal: return engine.Phase == TurnPhase.AwaitingRoll;
                case CoachTip.Deploy: return engine.Phase == TurnPhase.Action && engine.MustDeploy;
                case CoachTip.Landings: return engine.Phase == TurnPhase.Action && engine.CanMove;
                case CoachTip.Doubles: return engine.CanRollAgain && engine.RollAgainFromDoubles;
                case CoachTip.Ability: return engine.Phase == TurnPhase.Action && AnyCastable(match);
                default: return false;
            }
        }

        /// <summary>The tip's heading, as the card shows it.</summary>
        public static string Title(CoachTip tip)
        {
            switch (tip)
            {
                case CoachTip.Collision: return "Collisions";
                case CoachTip.Knockout: return "Knocked out";
                case CoachTip.SafeCell: return "Safe cells";
                case CoachTip.HomeColumn: return "The home column";
                case CoachTip.Goal: return "How to win";
                case CoachTip.Deploy: return "Leaving the yard";
                case CoachTip.Landings: return "Spending the roll";
                case CoachTip.Doubles: return "Doubles";
                case CoachTip.Ability: return "Abilities";
                default: return tip.ToString();
            }
        }

        /// <summary>The tip itself. Numbers come from the configs; keywords link to the glossary.</summary>
        public static RulesLine Line(CoachTip tip,
            GameConfig game = null, CombatConfig combat = null, EnergyConfig energy = null)
        {
            game = game ?? GameConfig.Default;
            combat = combat ?? CombatConfig.Default;
            energy = energy ?? EnergyConfig.Default;

            var line = new RulesLine();
            switch (tip)
            {
                case CoachTip.Goal:
                    return line.Text("Race your operators round the board and up your home column. The first side to bring all of its operators home wins. Your first roll each turn also adds to your ")
                        .Keyword("energy", Keywords.Energy).Text(": its total divided by ")
                        .Number(energy.DiceDivisor).Text(", rounded down.");

                case CoachTip.Deploy:
                    return line.Text("Only a ").Number(game.DeployRequirement).Text(" brings an operator out of the ")
                        .Keyword("yard", Keywords.Yard).Text(", onto your ")
                        .Keyword("spawn cell", Keywords.SpawnCell).Text(", and a ").Number(game.DeployRequirement)
                        .Text(" you roll must be used. Go ").Number(game.PityDeployAfterTurns)
                        .Text(" turns in a row without one and an operator comes out on its own.");

                case CoachTip.Landings:
                    return line.Text("Each die can move a different operator. The bright landing spends the whole roll on one operator, the faint ones a single die. The numbers are pips: an operator's speed decides how many cells they cover. You must move if you can.");

                case CoachTip.Doubles:
                    return line.Text("A double earns another roll, up to ").Number(game.MaxRollsPerTurn)
                        .Text(" rolls a turn. Spend the dice you hold first. Only the turn's first roll adds energy.");

                case CoachTip.Ability:
                    return line.Text("Casting is paid from your seat's shared ")
                        .Keyword("energy", Keywords.Energy)
                        .Text(" and does not end your turn. Select an operator, pick one of its cards in the tray, aim it, then Cast. Once you have rolled you can cast before or after moving, and cast again while the energy lasts.");

                case CoachTip.Collision:
                    return line.Text("Ending a move on an enemy hits it for ").Number(combat.CollisionDamage)
                        .Text(". If it survives, the mover bounces back a cell. Passing over a piece never collides, and nothing collides on a ")
                        .Keyword("safe cell", Keywords.SafeCell).Text(".");

                case CoachTip.Knockout:
                    return line.Text("A knocked-out operator goes back to its ")
                        .Keyword("yard", Keywords.Yard).Text(" at full health, and the seat that knocked it out gains ")
                        .Number(combat.NeutralizeEnergyBounty).Text(" ")
                        .Keyword("energy", Keywords.Energy).Text(". It needs a ")
                        .Number(game.DeployRequirement).Text(" to come back.");

                case CoachTip.SafeCell:
                    return line.Text("The start cells and the first cell of each home column are ")
                        .Keyword("safe", Keywords.SafeCell)
                        .Text(": no collisions, no damage, and no enemy can pick you out with a single-target ability. A good place to end a turn.");

                case CoachTip.HomeColumn:
                    return line.Text("In your home column an operator is out of the fight: nothing can hit it, and it cannot cast. Any roll that reaches home is enough; you don't need an exact count. Reaching home pays your seat ")
                        .Number(combat.HomeEnergyBounty).Text(" ").Keyword("energy", Keywords.Energy)
                        .Text(" and another roll.");

                default:
                    return line;
            }
        }

        // ── Moments ──────────────────────────────────────────────────────

        private static bool AnyCollision(IReadOnlyList<IGameEvent> events, Func<PlayerColor, bool> isHuman)
        {
            if (events == null) return false;
            foreach (var e in events)
                if (e is CollisionResolved hit && (isHuman(hit.Mover.Owner) || isHuman(hit.Occupant.Owner)))
                    return true;
            return false;
        }

        private static bool AnyKnockout(IReadOnlyList<IGameEvent> events, Func<PlayerColor, bool> isHuman)
        {
            if (events == null) return false;
            foreach (var e in events)
            {
                if (!(e is OperatorNeutralized down)) continue;
                if (down.Cause == GameEngine.DevCause) continue;
                if (isHuman(down.Operator.Owner) || (down.CreditedTo.HasValue && isHuman(down.CreditedTo.Value)))
                    return true;
            }
            return false;
        }

        /// <summary>A person's operator stopping on a safe cell after a dice move. Deploying onto the spawn cell does not count.</summary>
        private static bool AnySafeStop(MatchFactory.Match match, IReadOnlyList<IGameEvent> events, Func<PlayerColor, bool> isHuman)
        {
            if (events == null) return false;
            foreach (var e in events)
            {
                if (!(e is OperatorMoved moved) || !isHuman(moved.Operator.Owner)) continue;
                if (moved.AttemptedTo <= moved.From) continue; // placements report From == To
                if (match.Map.IsSafe(moved.Cell)) return true;
            }
            return false;
        }

        /// <summary>A person's operator leaving the outer track: into its home column, or straight home.</summary>
        private static bool AnyHomeEntry(MatchFactory.Match match, IReadOnlyList<IGameEvent> events, Func<PlayerColor, bool> isHuman)
        {
            if (events == null) return false;
            foreach (var e in events)
            {
                if (e is OperatorReachedHome home && isHuman(home.Operator.Owner)) return true;
                if (e is OperatorMoved moved && isHuman(moved.Operator.Owner) &&
                    moved.Cell.Kind == CellKind.HomeColumn && !match.Map.IsInHomeColumn(moved.From))
                    return true;
            }
            return false;
        }

        // ── States ───────────────────────────────────────────────────────

        private static bool AnyCastable(MatchFactory.Match match)
        {
            var engine = match.Engine;
            foreach (var op in engine.CurrentPlayer.Operators)
            {
                if (!match.AbilitiesByOperator.TryGetValue(op.Id, out var abilities)) continue;
                foreach (var ability in abilities)
                    if (TurnOptions.IsCastable(engine, op, ability)) return true;
            }
            return false;
        }
    }
}
