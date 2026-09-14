// tools/sim/NonaRoyale.Sim/ScriptedPlayer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Sim
{
    /// <summary>
    /// A deliberately simple automated player: deploy whenever possible, advance
    /// the operator closest to home, and spend energy according to whatever
    /// policy the seat was given.
    /// </summary>
    /// <remarks>
    /// <b>It does not check legality itself.</b> It sends a command and reads
    /// whether the engine rejected it. That keeps the harness free of a second
    /// copy of the rules — the thing that would make its numbers meaningless —
    /// and exercises the command boundary on every single action.
    ///
    /// A real player is better than this, so the turn counts it produces are a
    /// mild over-estimate. What the harness measures reliably is the
    /// <i>relative</i> effect of a change, not an absolute figure.
    ///
    /// <b>It never splits a roll.</b> See <see cref="Move"/> — a deliberate
    /// choice, and it bounds what the current figures mean.
    ///
    /// <b>Spending is the only thing a policy decides.</b> Deploying and moving
    /// are fixed for every seat: movement is compulsory (§6), so a policy that
    /// declined to move would hang its turn rather than play differently. Holding
    /// everything else constant is what makes a difference between two seats
    /// attributable to the one thing that differed — see
    /// <see cref="IEnergyPolicy"/>.
    ///
    /// The two-argument constructor gives every seat <see cref="SpendthriftPolicy"/>,
    /// which is what this class always did, so every existing sweep is unchanged.
    /// </remarks>
    public sealed class ScriptedPlayer
    {
        private static readonly IEnergyPolicy Default = new SpendthriftPolicy();

        private readonly MatchFactory.Match _match;
        private readonly MatchStats _stats;
        private readonly IReadOnlyDictionary<PlayerColor, IEnergyPolicy> _policies;

        public ScriptedPlayer(MatchFactory.Match match, MatchStats stats)
            : this(match, stats, null)
        {
        }

        public ScriptedPlayer(
            MatchFactory.Match match,
            MatchStats stats,
            IReadOnlyDictionary<PlayerColor, IEnergyPolicy> policies)
        {
            _match = match;
            _stats = stats;
            _policies = policies;
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
                PolicyFor(seat.Color).Spend(_match, seat, _stats, Send);
            }

            Send(new EndTurnCommand());
            _stats.Turns++;
        }

        private IEnergyPolicy PolicyFor(PlayerColor seat)
        {
            IEnergyPolicy policy;

            if (_policies != null && _policies.TryGetValue(seat, out policy) && policy != null)
                return policy;

            return Default;
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

        private IReadOnlyList<IGameEvent> Send(ICommand command)
        {
            var events = _match.Engine.Execute(command);
            _stats.Observe(events);
            return events;
        }
    }
}