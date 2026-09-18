// Assets/_Project/Scripts/Core/Bots/BotTable.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;

namespace NonaRoyale.Core.Bots
{
    /// <summary>How a bot-played match ended.</summary>
    public sealed class BotRunResult
    {
        public bool Finished { get; internal set; }
        public PlayerColor? Winner { get; internal set; }
        public int Commands { get; internal set; }
        public int Refusals { get; internal set; }
        public int Rounds { get; internal set; }

        /// <summary>Why the run stopped short, or null if it finished.</summary>
        public string StopReason { get; internal set; }

        /// <summary>
        /// The last command a bot had refused, and why. Null when there were none.
        /// A well-behaved bot never produces one, so this exists to name the
        /// offender when the sweep's refusal count is not zero.
        /// </summary>
        public string LastRefusal { get; internal set; }
    }

    /// <summary>
    /// Plays the bot seats of one match, a command at a time, with nothing in
    /// between: for tests and the sim. The game's driver paces the same calls.
    /// </summary>
    public sealed class BotTable
    {
        private readonly MatchFactory.Match _match;
        private readonly IReadOnlyDictionary<PlayerColor, IBot> _bots;

        public BotTable(MatchFactory.Match match, IReadOnlyDictionary<PlayerColor, IBot> bots)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _bots = bots ?? throw new ArgumentNullException(nameof(bots));
        }

        /// <summary>Called after every command, with what it produced. For recorders.</summary>
        public Action<PlayerColor, ICommand, IReadOnlyList<IGameEvent>> Sent;

        public bool IsBotTurn =>
            !_match.Engine.MatchOver &&
            _match.Engine.CurrentPlayer != null &&
            _bots.ContainsKey(_match.Engine.CurrentPlayer.Color);

        /// <summary>
        /// Sends one command for the current seat if it is a bot's. Returns the
        /// events, or null if it is not a bot's turn.
        /// </summary>
        public IReadOnlyList<IGameEvent> Step()
        {
            if (!IsBotTurn) return null;

            var seat = _match.Engine.CurrentPlayer.Color;
            var bot = _bots[seat];

            var command = bot.Next(_match);
            if (command == null) return null;

            var events = _match.Engine.Execute(command);
            bot.Observe(command, events);
            Sent?.Invoke(seat, command, events);
            return events;
        }

        /// <summary>
        /// Plays until the match ends, a human seat is up, or the guard trips.
        /// Starts the match first if <paramref name="start"/> is set.
        /// </summary>
        public BotRunResult PlayToEnd(int maxCommands = 20000, bool start = true)
        {
            var result = new BotRunResult();
            var engine = _match.Engine;

            if (start) engine.Start();

            int endTurnRefusalsInARow = 0;

            while (result.Commands < maxCommands)
            {
                if (engine.MatchOver) break;

                if (!IsBotTurn)
                {
                    result.StopReason = "a seat without a bot is up";
                    break;
                }

                var events = Step();
                if (events == null)
                {
                    result.StopReason = "the bot proposed nothing";
                    break;
                }

                result.Commands++;

                bool refused = false;
                bool endTurn = false;
                foreach (var e in events)
                {
                    if (e is CommandRejected) refused = true;
                    if (e is TurnBegan) endTurn = true;
                }

                if (refused)
                {
                    result.Refusals++;

                    foreach (var e in events)
                    {
                        if (!(e is CommandRejected rejected)) continue;
                        result.LastRefusal = rejected.Reason;
                        break;
                    }
                }

                // An end-turn that keeps being refused is a stall the bot cannot leave.
                if (refused && !endTurn) endTurnRefusalsInARow++;
                else endTurnRefusalsInARow = 0;

                if (endTurnRefusalsInARow > 200)
                {
                    result.StopReason = "stalled: the turn could not be ended";
                    break;
                }
            }

            result.Finished = engine.MatchOver;
            result.Winner = engine.Winner;
            result.Rounds = engine.Round;
            if (!result.Finished && result.StopReason == null) result.StopReason = "command guard reached";

            return result;
        }
    }
}