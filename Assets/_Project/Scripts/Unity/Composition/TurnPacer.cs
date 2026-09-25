// Assets/_Project/Scripts/Unity/Composition/TurnPacer.cs
using NonaRoyale.Core;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Services;

namespace NonaRoyale.Unity.Composition
{
    /// <summary>
    /// Keeps a human seat's turn moving (2026-09-25, to shorten matches): the
    /// game rolls for a seat that has not rolled within <see cref="RollClockSeconds"/>,
    /// and ends a turn whose only legal command left is End Turn.
    /// </summary>
    /// <remarks>
    /// <b>Plain C#, no Unity types</b>, so the timing is testable without Play
    /// Mode. The composition root ticks it once a frame while no card is open,
    /// so pause, the guide and every menu freeze both clocks, and sends what it
    /// returns through the same path a pressed button uses.
    ///
    /// <b>It decides nothing about the rules.</b> "Only End Turn remains" is
    /// <see cref="TurnOptions.OnlyEndTurnRemains"/>, an engine answer; the
    /// pacer only chooses when to act on it.
    ///
    /// <b>Both clocks wait for the board.</b> While a walk, a knockout or the
    /// dice are still playing, neither runs: the roll clock starts when the
    /// seat can see it is their turn, and a turn never ends under a move the
    /// player is still watching.
    ///
    /// <b>The roll clock covers the first roll only</b>, as asked: "15 s to
    /// start playing". A doubles re-roll is untimed.
    /// </remarks>
    public sealed class TurnPacer
    {
        /// <summary>How long a human seat has to roll before the game rolls for it.</summary>
        public const float RollClockSeconds = 15f;

        /// <summary>
        /// How long "only End Turn remains" must hold before the turn ends: long
        /// enough to read the dice that caused it, short enough not to wait on.
        /// </summary>
        public const float AutoEndDelaySeconds = 0.9f;

        /// <summary>
        /// How often the auto-end question is asked. It walks every ability's
        /// legal aims, which is cheap but not free, and nothing needs it every frame.
        /// </summary>
        public const float AutoEndPollSeconds = 0.2f;

        private PlayerColor _seat = PlayerColor.None;
        private int _turnIndex = -1;
        private float _rollLeft;
        private float _pollIn;
        private float _endHeld;
        private bool _onlyEnd;

        /// <summary>Seconds left on the roll clock, or null when it is not running for anyone.</summary>
        public float? RollSecondsLeft { get; private set; }

        /// <summary>
        /// Advances both clocks and returns the command to send for the seat to
        /// play, or null.
        /// </summary>
        /// <param name="humanTurn">False on a CPU seat's turn: the bots pace themselves.</param>
        /// <param name="busy">True while the board is still presenting.</param>
        /// <param name="deltaTime">Real seconds since the last tick.</param>
        public ICommand Tick(
            MatchFactory.Match match, bool humanTurn, bool busy, float deltaTime,
            bool rollClock = true, bool autoEnd = true)
        {
            RollSecondsLeft = null;

            var engine = match?.Engine;
            if (engine == null || engine.MatchOver || !humanTurn || engine.CurrentPlayer == null)
            {
                ResetEnd();
                return null;
            }

            var seat = engine.CurrentPlayer;
            if (seat.Color != _seat || seat.TurnIndex != _turnIndex)
            {
                _seat = seat.Color;
                _turnIndex = seat.TurnIndex;
                _rollLeft = RollClockSeconds;
                ResetEnd();
            }

            if (engine.Phase == TurnPhase.AwaitingRoll)
            {
                ResetEnd();
                if (!rollClock) return null;

                if (!busy) _rollLeft -= deltaTime;

                RollSecondsLeft = _rollLeft > 0f ? _rollLeft : 0f;
                if (_rollLeft > 0f) return null;

                // Spent: the refusal path (a rejected roll) cannot loop, because
                // the phase moves on when the roll is accepted and the clock is
                // only read at AwaitingRoll.
                _rollLeft = RollClockSeconds;
                return new RollDiceCommand();
            }

            if (!autoEnd || busy)
            {
                ResetEnd();
                return null;
            }

            _pollIn -= deltaTime;
            if (_pollIn <= 0f)
            {
                _pollIn = AutoEndPollSeconds;
                bool was = _onlyEnd;
                _onlyEnd = TurnOptions.OnlyEndTurnRemains(match);
                if (_onlyEnd && !was) _endHeld = 0f;
            }

            if (!_onlyEnd) return null;

            _endHeld += deltaTime;
            if (_endHeld < AutoEndDelaySeconds) return null;

            ResetEnd();
            return new EndTurnCommand();
        }

        private void ResetEnd()
        {
            _onlyEnd = false;
            _endHeld = 0f;
            _pollIn = 0f;
        }
    }
}
