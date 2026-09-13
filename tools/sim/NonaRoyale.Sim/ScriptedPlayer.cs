// tools/sim/NonaRoyale.Sim/ScriptedPlayer.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Sim
{
    /// <summary>
    /// A deliberately simple automated player: deploy whenever possible, fire
    /// whatever is affordable and legal, then advance the operator closest to
    /// home.
    /// </summary>
    /// <remarks>
    /// <b>It does not check legality itself.</b> It sends a command and reads
    /// whether the engine rejected it. That keeps the harness free of a second
    /// copy of the rules — the thing that would make its numbers meaningless —
    /// and exercises the command boundary on every single action.
    ///
    /// A real player is better than this, so the turn counts it produces are a
    /// mild over-estimate. What the harness measures reliably is the
    /// <i>relative</i> effect of a config change, not an absolute figure.
    ///
    /// <b>It never splits a roll.</b> See <see cref="Move"/> — this is a
    /// deliberate choice, and it bounds what the current figures mean.
    /// </remarks>
    public sealed class ScriptedPlayer
    {
        private readonly MatchFactory.Match _match;
        private readonly MatchStats _stats;

        public ScriptedPlayer(MatchFactory.Match match, MatchStats stats)
        {
            _match = match;
            _stats = stats;
        }

        /// <summary>Plays one full turn for whichever seat is active.</summary>
        public void TakeTurn()
        {
            var engine = _match.Engine;
            var seat = engine.CurrentPlayer;

            Record(seat);

            bool rollAgain = true;
            while (rollAgain && !engine.MatchOver)
            {
                var rolled = Send(new RollDiceCommand());
                if (rolled.Any(e => e is CommandRejected)) break;

                rollAgain = rolled.OfType<DiceRolled>().Any(d => d.GrantsAnotherRoll);

                Deploy(seat);
                Move(seat);
                SpendEnergy(seat);
            }

            Send(new EndTurnCommand());
            _stats.Turns++;
        }

        private void Record(PlayerState seat)
        {
            _stats.OccupancySamples++;

            bool everyoneOut = seat.Operators.All(o =>
                !o.IsInYard && o.Progress < _match.Map.Profile.Journey);

            if (everyoneOut) _stats.FullSquadTurns++;
        }

        private void Deploy(PlayerState seat)
        {
            foreach (var waiting in seat.Operators.Where(o => o.IsInYard).ToList())
            {
                var events = Send(new DeployCommand(waiting.Id));
                if (events.Any(e => e is CommandRejected)) break;
            }
        }

        /// <summary>
        /// Spends the roll on movement, pooling rather than splitting, and keeps
        /// going until the engine says nothing is owed.
        /// </summary>
        /// <remarks>
        /// <b>Pooling is a policy, and it is the one that measures least.</b>
        /// Any split policy is a tactical judgement the harness would be making
        /// on the player's behalf — the same flaw that made its neutralize
        /// figures an upper bound — so this makes the conservative choice and
        /// declines to split at all. The consequence is worth stating plainly:
        /// <b>these runs measure compulsory movement, not splitting.</b>
        /// Splitting roughly doubles landings per roll, and a landing is what
        /// triggers a collision (§7.1), so its effect on contact will not appear
        /// in any figure this file produces.
        ///
        /// The loop exists because pooling can fail. If the leader is stunned or
        /// already home, that command is rejected and the dice are still owed —
        /// and <c>EndTurnCommand</c> now refuses while any operator could move,
        /// so a single attempt would hang the turn rather than merely waste it.
        /// It tries each candidate in turn and gives up only when the engine
        /// accepts nothing, which is precisely the case where the dice are
        /// genuinely forfeit.
        /// </remarks>
        private void Move(PlayerState seat)
        {
            var engine = _match.Engine;

            while (engine.MustSpendRoll)
            {
                var candidates = seat.Operators
                    .Where(o => !o.IsInYard && o.Progress < _match.Map.Profile.Journey)
                    .OrderByDescending(o => o.Progress)
                    .ToList();

                bool spent = false;

                foreach (var op in candidates)
                {
                    if (!Send(new MoveCommand(op.Id)).Any(e => e is CommandRejected))
                    {
                        spent = true;
                        break;
                    }
                }

                if (!spent) break;
            }
        }

        /// <summary>
        /// Tries every ability of every owned operator against every enemy until
        /// nothing more is accepted. Crude, but it means energy actually gets
        /// spent — which is the throughput the harness exists to measure.
        /// </summary>
        private void SpendEnergy(PlayerState seat)
        {
            bool fired = true;

            while (fired)
            {
                fired = false;

                foreach (var caster in seat.Operators)
                {
                    if (!_match.AbilitiesByOperator.TryGetValue(caster.Id, out var abilities)) continue;

                    foreach (var ability in abilities)
                    {
                        if (ability.RequiresTarget)
                        {
                            foreach (var target in _match.Operators.Where(o => o.Owner != seat.Color))
                            {
                                if (Send(new UseAbilityCommand(caster.Id, ability.Id, target.Id))
                                    .Any(e => e is EnergySpent))
                                {
                                    fired = true;
                                    break;
                                }
                            }
                        }
                        else if (Send(new UseAbilityCommand(caster.Id, ability.Id)).Any(e => e is EnergySpent))
                        {
                            fired = true;
                        }

                        if (fired) break;
                    }

                    if (fired) break;
                }
            }
        }

        private IReadOnlyList<IGameEvent> Send(ICommand command)
        {
            var events = _match.Engine.Execute(command);
            _stats.Observe(events);
            return events;
        }
    }
}