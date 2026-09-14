// tools/sim/NonaRoyale.Sim/EnergyPolicy.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Sim
{
    /// <summary>
    /// How an automated seat decides to spend energy. The one axis the harness
    /// varies between players.
    /// </summary>
    /// <remarks>
    /// <b>Only spending is a policy.</b> Deploying and moving are not: movement
    /// is compulsory (§6), so a player that declined to move would hang its turn
    /// rather than play differently. Holding everything else fixed is what makes
    /// a difference between two runs attributable to the thing that differed.
    ///
    /// <b>Every figure in COMBAT_SYSTEMS §12 was measured under
    /// <see cref="SpendthriftPolicy"/></b>, which was the only policy the harness
    /// had until 2026-09-13 — and under a version of it that could not target an
    /// ally or name a cell.
    /// </remarks>
    public interface IEnergyPolicy
    {
        /// <summary>For the output table.</summary>
        string Name { get; }

        /// <summary>
        /// Spends whatever this policy wants to, through
        /// <paramref name="send"/>. Called after each roll, before the turn ends.
        /// </summary>
        void Spend(
            MatchFactory.Match match,
            PlayerState seat,
            MatchStats stats,
            Func<ICommand, IReadOnlyList<IGameEvent>> send);
    }

    /// <summary>
    /// Attempting one cast, in every targeting mode the game has.
    /// </summary>
    /// <remarks>
    /// <b>Extracted after the usage table read 0.00 against four abilities.</b>
    /// Trauma Plate, Neural Purge, Drone Strike and Killzone had never been cast
    /// in a simulated match — not because they are poor, but because both
    /// policies iterated enemies only and never supplied a cell. Javi had
    /// therefore never played at all, no shield had ever absorbed anything, and
    /// the whole ADR-0006/0007 cell layer had run nowhere but unit tests.
    ///
    /// <b>It still does not check legality itself.</b> It sends a command and
    /// reads whether the engine rejected it, which keeps the harness free of a
    /// second copy of the rules — the thing that would make its numbers
    /// meaningless. Out of range, wrong side, no cell: all of it comes back as a
    /// refusal rather than being predicted here.
    /// </remarks>
    internal static class Casting
    {
        /// <summary>
        /// Tries one ability every way it could legally be aimed, and reports
        /// whether any of them was paid for.
        /// </summary>
        /// <remarks>
        /// <b>Enemies before allies</b>, so an ability with both modes takes its
        /// hostile one. All-In Mauling heals an ally and damages an enemy from one
        /// effect list; trying allies first would turn the Bouncer into a medic.
        ///
        /// <b>The caster is excluded from the ally list.</b> §10's mode rule makes
        /// self-targeting legal and <c>MatchBootstrap</c> filters it out anyway —
        /// undecided in both directions. But a bot that did not filter finds the
        /// hole immediately: Velvet Rope aimed at yourself resolves to a placement
        /// one cell <i>forward</i> of your own, which is a 6-energy free move. It
        /// would cast nothing else. That is a real rules question and not one the
        /// harness should answer by exploiting it.
        ///
        /// <b>A cell-targeted ability is aimed at an enemy's current cell.</b>
        /// Deliberately the naive bet, and a poor one: a beacon fires a full round
        /// later (ADR-0006), so painting where somebody stands now catches them
        /// only if they choose not to move. Predicting where they will be is
        /// exactly the judgement the harness must not make on a player's behalf,
        /// so it makes the dumbest honest choice and its usage figures should be
        /// read as a floor.
        /// </remarks>
        public static bool TryCast(
            MatchFactory.Match match,
            PlayerState seat,
            OperatorState caster,
            AbilityDefinition ability,
            MatchStats stats,
            Func<ICommand, IReadOnlyList<IGameEvent>> send)
        {
            switch (ability.Targeting)
            {
                case AbilityTargeting.None:
                    return Fire(new UseAbilityCommand(caster.Id, ability.Id), ability, stats, send);

                case AbilityTargeting.Cell:
                    foreach (var enemy in Enemies(match, seat))
                    {
                        if (enemy.IsInYard) continue;

                        var cell = match.Map.CellAt(enemy.Owner, enemy.Progress);
                        if (!cell.IsOnTrack) continue;

                        if (Fire(new UseAbilityCommand(caster.Id, ability.Id, null, cell), ability, stats, send))
                            return true;
                    }

                    return false;

                default:
                    foreach (var enemy in Enemies(match, seat))
                        if (Fire(new UseAbilityCommand(caster.Id, ability.Id, enemy.Id), ability, stats, send))
                            return true;

                    foreach (var ally in Allies(match, seat, caster))
                        if (Fire(new UseAbilityCommand(caster.Id, ability.Id, ally.Id), ability, stats, send))
                            return true;

                    return false;
            }
        }

        private static bool Fire(
            UseAbilityCommand command,
            AbilityDefinition ability,
            MatchStats stats,
            Func<ICommand, IReadOnlyList<IGameEvent>> send)
        {
            if (!send(command).Any(e => e is EnergySpent)) return false;

            stats.RecordCast(ability.Id);
            return true;
        }

        private static IEnumerable<OperatorState> Enemies(MatchFactory.Match match, PlayerState seat) =>
            match.Operators.Where(o => o.Owner != seat.Color);

        private static IEnumerable<OperatorState> Allies(
            MatchFactory.Match match, PlayerState seat, OperatorState caster) =>
            match.Operators.Where(o => o.Owner == seat.Color && !ReferenceEquals(o, caster));
    }

    /// <summary>
    /// Never casts. The control, and the floor the other two have to beat.
    /// </summary>
    /// <remarks>
    /// <b>It is the measurement the design had never taken.</b> The GDD's stated
    /// priority is 70% combat to 30% race. If a seat that never spends a point of
    /// energy wins as often as one that spends all of it, the combat layer is
    /// decoration — and no figure produced before 2026-09-13 could have told you,
    /// because every seat played the same way.
    /// </remarks>
    public sealed class RacerPolicy : IEnergyPolicy
    {
        public string Name => "racer";

        public void Spend(
            MatchFactory.Match match,
            PlayerState seat,
            MatchStats stats,
            Func<ICommand, IReadOnlyList<IGameEvent>> send)
        {
        }
    }

    /// <summary>
    /// Fires whatever is affordable at whatever it can reach, until nothing more
    /// is accepted. The harness's original player.
    /// </summary>
    /// <remarks>
    /// <b>Its target selection is nil, and that is the point.</b> It takes the
    /// first legal aim it finds, so its figures are a floor rather than an
    /// estimate — a human chooses, and choosing can only do better.
    ///
    /// <b>It now reaches allies and cells, which it never did.</b> Every figure
    /// taken before 2026-09-13 came from a version that could not cast a heal, a
    /// plate, a cleanse, a beacon or a zone. Runs either side of that change are
    /// not comparable, on alpha squads as well as drafted ones: an ally cast is
    /// tried whenever no enemy is in reach, which changes the command stream even
    /// for a squad that owns no ally-only ability.
    /// </remarks>
    public sealed class SpendthriftPolicy : IEnergyPolicy
    {
        public string Name => "spendthrift";

        public void Spend(
            MatchFactory.Match match,
            PlayerState seat,
            MatchStats stats,
            Func<ICommand, IReadOnlyList<IGameEvent>> send)
        {
            bool fired = true;

            while (fired)
            {
                fired = false;

                foreach (var caster in seat.Operators)
                {
                    if (!match.AbilitiesByOperator.TryGetValue(caster.Id, out var abilities)) continue;

                    foreach (var ability in abilities)
                    {
                        if (Casting.TryCast(match, seat, caster, ability, stats, send))
                        {
                            fired = true;
                            break;
                        }
                    }

                    if (fired) break;
                }
            }
        }
    }

    /// <summary>
    /// Holds energy until it can afford the most expensive ability its squad
    /// owns, then spends on that, most expensive first.
    /// </summary>
    /// <remarks>
    /// <b>The threshold is the squad's own ceiling, not a literal 9.</b> A drafted
    /// squad may hold nothing above 6, and a policy hard-coded to 9 would simply
    /// never cast for that squad — turning the Banker into a Racer without saying
    /// so.
    ///
    /// <b>What it measured, once the usage table existed.</b> Doubling Tagged
    /// From Above's cooldown moved every Banker row and no Spendthrift row. That
    /// is the first empirical support for §3.1's claim that the energy drip is the
    /// real cooldown on anything costing 6 or more: a player gated by energy never
    /// notices a cooldown change, and a player gated by the cooldown does.
    ///
    /// It stops after one cast per call rather than looping. Having paid for its
    /// big ability it is usually below the threshold again, and looping would
    /// quietly turn it back into a spendthrift on a full pool.
    /// </remarks>
    public sealed class BankerPolicy : IEnergyPolicy
    {
        public string Name => "banker";

        public void Spend(
            MatchFactory.Match match,
            PlayerState seat,
            MatchStats stats,
            Func<ICommand, IReadOnlyList<IGameEvent>> send)
        {
            int ceiling = 0;

            foreach (var op in seat.Operators)
            {
                if (!match.AbilitiesByOperator.TryGetValue(op.Id, out var list)) continue;

                foreach (var ability in list)
                    if (ability.EnergyCost > ceiling) ceiling = ability.EnergyCost;
            }

            if (ceiling == 0 || seat.Energy < ceiling) return;

            foreach (var caster in seat.Operators)
            {
                if (!match.AbilitiesByOperator.TryGetValue(caster.Id, out var abilities)) continue;

                foreach (var ability in abilities.OrderByDescending(a => a.EnergyCost))
                    if (Casting.TryCast(match, seat, caster, ability, stats, send)) return;
            }
        }
    }
}