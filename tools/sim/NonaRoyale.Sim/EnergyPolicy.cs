// tools/sim/NonaRoyale.Sim/EnergyPolicy.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
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
    /// rather than play differently, and a player that declined to deploy would
    /// be measuring the deploy rule rather than a decision. Holding everything
    /// else fixed is what makes a difference between two runs attributable to the
    /// thing that differed.
    ///
    /// <b>Every figure in COMBAT_SYSTEMS §12 was measured under exactly one of
    /// these</b> — <see cref="Spendthrift"/>, which has been the only policy the
    /// harness has ever had. Those numbers are not wrong, but they are
    /// conditional on a player that never banks and picks targets by nothing,
    /// and nobody has ever checked how much of the pacing came from that.
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
    /// Never casts. The control, and the floor the other two have to beat.
    /// </summary>
    /// <remarks>
    /// <b>It is the measurement the design has never taken.</b> The GDD's stated
    /// priority is 70% combat to 30% race. If a seat that never spends a point of
    /// energy wins as often as one that spends all of it, the combat layer is
    /// decoration — and no figure the harness has produced so far could have told
    /// you, because every seat played the same way.
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
    /// is accepted. The harness's original and only player.
    /// </summary>
    /// <remarks>
    /// <b>Lifted verbatim from <c>ScriptedPlayer.SpendEnergy</c>, deliberately.</b>
    /// It is the status quo in the comparison, so any change to it would
    /// invalidate every figure it is being compared against.
    ///
    /// It does not check legality itself — it sends a command and reads whether
    /// the engine rejected it. That keeps the harness free of a second copy of
    /// the rules, which is the thing that would make its numbers meaningless.
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
                        if (ability.RequiresTarget)
                        {
                            foreach (var target in match.Operators.Where(o => o.Owner != seat.Color))
                            {
                                if (send(new UseAbilityCommand(caster.Id, ability.Id, target.Id))
                                    .Any(e => e is EnergySpent))
                                {
                                    stats.RecordCast(ability.Id);
                                    fired = true;
                                    break;
                                }
                            }
                        }
                        else if (send(new UseAbilityCommand(caster.Id, ability.Id)).Any(e => e is EnergySpent))
                        {
                            stats.RecordCast(ability.Id);
                            fired = true;
                        }

                        if (fired) break;
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
    /// <b>The threshold is the squad's own ceiling, not a literal 9.</b> A
    /// drafted squad may hold nothing above 6 — Bouncer and Javi between them
    /// top out there — and a policy hard-coded to 9 would simply never cast for
    /// that squad, turning the Banker into a Racer without saying so.
    ///
    /// <b>Why it is worth running at all.</b> §3.1 says the drip is the real
    /// cooldown on anything costing 6 or more, and that banking to 12 to fire two
    /// abilities in one turn is a combo worth having. Nothing has tested either
    /// claim: the only player the harness has had spends the moment it can, so
    /// every figure describes a game in which nobody ever saves.
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
                {
                    if (ability.RequiresTarget)
                    {
                        foreach (var target in match.Operators.Where(o => o.Owner != seat.Color))
                        {
                            if (send(new UseAbilityCommand(caster.Id, ability.Id, target.Id))
                                .Any(e => e is EnergySpent))
                            {
                                stats.RecordCast(ability.Id);
                                return;
                            }
                        }
                    }
                    else if (send(new UseAbilityCommand(caster.Id, ability.Id)).Any(e => e is EnergySpent))
                    {
                        stats.RecordCast(ability.Id);
                        return;
                    }
                }
            }
        }
    }
}