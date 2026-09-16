// Assets/_Project/Scripts/Unity/Composition/BotDriver.cs
using System.Collections.Generic;
using NonaRoyale.Core;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Services;
using NonaRoyale.Unity.View;

namespace NonaRoyale.Unity.Composition
{
    /// <summary>
    /// Plays the CPU seats of one match at a pace a human can follow
    /// (BOTS.md, increment BOT2).
    /// </summary>
    /// <remarks>
    /// <b>Plain C#, owned and ticked by <c>MatchBootstrap</c>.</b> Each tick it
    /// either waits or hands back one command for the composition root to send
    /// through its normal path, so a CPU action animates, logs and refreshes
    /// the HUD exactly as a human one does. The root reports the events back
    /// through <see cref="Observe"/>.
    ///
    /// <b>Waiting:</b> nothing is proposed while a card is open, while the
    /// board is still presenting the last action (MO1), or before the think
    /// delay has run. The delay runs on
    /// the time the root passes in, which is scaled time, so pausing freezes
    /// the CPUs (decision 8).
    ///
    /// <b>The brain decides; the driver only paces.</b> It never judges a
    /// command. A turn that trips the brain's action cap is reported once, so
    /// the log can say so.
    /// </remarks>
    public sealed class BotDriver
    {
        /// <summary>Seconds before each action at Normal speed.</summary>
        public const float ThinkNormal = 0.55f;

        /// <summary>Seconds before a turn's first action at Normal speed, so the turn banner reads first.</summary>
        public const float FirstThinkNormal = 0.9f;

        /// <summary>Multiplier on the delays at Fast speed.</summary>
        public const float FastFactor = 0.35f;

        /// <summary>Multiplier on the delays while Space is held.</summary>
        public const float HurryFactor = 0.15f;

        private readonly Dictionary<PlayerColor, BotBrain> _bots;
        private float _waited;
        private string _turnKey;
        private readonly Dictionary<PlayerColor, int> _guardedSeen = new Dictionary<PlayerColor, int>();

        public BotDriver(IReadOnlyDictionary<PlayerColor, BotBrain> bots)
        {
            _bots = new Dictionary<PlayerColor, BotBrain>();
            if (bots == null) return;
            foreach (var pair in bots) _bots[pair.Key] = pair.Value;
        }

        public bool HasBots => _bots.Count > 0;

        public bool IsCpu(PlayerColor seat) => _bots.ContainsKey(seat);

        public BotBrain BrainFor(PlayerColor seat) => _bots.TryGetValue(seat, out var bot) ? bot : null;

        /// <summary>Whether the seat to play is a CPU's.</summary>
        public bool IsCpuTurn(GameEngine engine) =>
            engine != null && !engine.MatchOver && engine.CurrentPlayer != null &&
            _bots.ContainsKey(engine.CurrentPlayer.Color);

        /// <summary>
        /// Advances the pacing clock and returns the command to send now, or null.
        /// </summary>
        /// <param name="elapsed">Scaled seconds since the last tick.</param>
        /// <param name="mayAct">False while a card is open or the match is not on the table.</param>
        /// <param name="presentationBusy">True while the board is still presenting the last action. Ignored at Instant.</param>
        public ICommand Tick(MatchFactory.Match match, float elapsed, bool mayAct, bool presentationBusy,
            BotSpeed speed, bool hurry)
        {
            if (match == null || !IsCpuTurn(match.Engine)) { _waited = 0f; return null; }
            if (!mayAct) return null;
            if (presentationBusy && speed != BotSpeed.Instant) return null;

            var engine = match.Engine;
            var seat = engine.CurrentPlayer;

            string key = $"{seat.Color}|{seat.TurnIndex}";
            bool firstOfTurn = key != _turnKey;
            if (firstOfTurn && engine.Phase != TurnPhase.AwaitingRoll) firstOfTurn = false;

            _waited += elapsed;
            if (_waited < Delay(speed, hurry, firstOfTurn)) return null;

            _waited = 0f;
            _turnKey = key;
            return _bots[seat.Color].Next(match);
        }

        /// <summary>
        /// Reports what the last command produced. Returns a log line when the
        /// brain had to end a turn through its action cap, else null.
        /// </summary>
        public string Observe(PlayerColor seat, ICommand command, IReadOnlyList<IGameEvent> events)
        {
            if (!_bots.TryGetValue(seat, out var bot)) return null;

            bot.Observe(command, events);

            _guardedSeen.TryGetValue(seat, out int seen);
            if (bot.GuardedTurns == seen) return null;

            _guardedSeen[seat] = bot.GuardedTurns;
            return $"[CPU {seat}] turn ended by the action guard (last refusal: {bot.LastRefusal ?? "none"})";
        }

        public static float Delay(BotSpeed speed, bool hurry, bool firstOfTurn)
        {
            if (speed == BotSpeed.Instant) return 0f;

            float delay = firstOfTurn ? FirstThinkNormal : ThinkNormal;
            if (speed == BotSpeed.Fast) delay *= FastFactor;
            if (hurry) delay *= HurryFactor;
            return delay;
        }
    }
}