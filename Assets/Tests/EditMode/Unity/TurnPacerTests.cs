// Assets/Tests/EditMode/Unity/TurnPacerTests.cs
using System;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Unity.Composition;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.Composition
{
    /// <summary>
    /// The roll clock and auto end-turn for human seats (2026-09-25). Plain C#
    /// timing, driven with fixed ticks.
    /// </summary>
    [TestFixture]
    public class TurnPacerTests
    {
        private const float Frame = 0.1f;

        private static MatchFactory.Match Started(int seed = 3, int opening = 0)
        {
            var match = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red, PlayerColor.Blue }, seed, openingDeployments: opening);
            match.Engine.Start();
            return match;
        }

        /// <summary>Ticks until a command comes back or the time runs out; returns it and the time taken.</summary>
        private static ICommand TickUntil(TurnPacer pacer, MatchFactory.Match match, float seconds,
            out float elapsed, bool human = true, bool busy = false, bool rollClock = true, bool autoEnd = true)
        {
            elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Frame;
                var command = pacer.Tick(match, human, busy, Frame, rollClock, autoEnd);
                if (command != null) return command;
            }

            return null;
        }

        // ── Roll clock ───────────────────────────────────────────────────

        [Test]
        public void RollClock_RollsForAHumanSeat_AfterFifteenSeconds()
        {
            var pacer = new TurnPacer();
            var command = TickUntil(pacer, Started(), 20f, out float elapsed);

            Assert.That(command, Is.InstanceOf<RollDiceCommand>());
            Assert.That(elapsed, Is.EqualTo(TurnPacer.RollClockSeconds).Within(Frame * 1.5f));
        }

        [Test]
        public void RollClock_ReportsTheSecondsLeft_WhileItRuns()
        {
            var pacer = new TurnPacer();
            var match = Started();

            pacer.Tick(match, true, false, 5f);

            Assert.That(pacer.RollSecondsLeft, Is.EqualTo(TurnPacer.RollClockSeconds - 5f).Within(0.01f));
        }

        [Test]
        public void RollClock_NeverRuns_OnACpuTurn()
        {
            var pacer = new TurnPacer();
            Assert.That(TickUntil(pacer, Started(), 30f, out _, human: false), Is.Null);
            Assert.That(pacer.RollSecondsLeft, Is.Null);
        }

        [Test]
        public void RollClock_WaitsForTheBoard()
        {
            var pacer = new TurnPacer();
            var match = Started();

            Assert.That(TickUntil(pacer, match, 30f, out _, busy: true), Is.Null);
            Assert.That(pacer.RollSecondsLeft, Is.EqualTo(TurnPacer.RollClockSeconds).Within(0.01f),
                "a busy board does not spend the clock");
        }

        [Test]
        public void RollClock_Off_NeverRolls()
        {
            Assert.That(TickUntil(new TurnPacer(), Started(), 30f, out _, rollClock: false), Is.Null);
        }

        [Test]
        public void RollClock_StartsAgain_ForTheNextSeat()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var match = Started(seed);
                var engine = match.Engine;
                var pacer = new TurnPacer();

                // The first seat spends 10 s, then rolls a turn that owes nothing.
                pacer.Tick(match, true, false, 10f);
                var rolled = engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;
                if (rolled.Contains(6) || rolled.IsDouble) continue;

                var first = engine.CurrentPlayer.Color;
                engine.Execute(new EndTurnCommand());
                Assert.That(engine.CurrentPlayer.Color, Is.Not.EqualTo(first), "precondition: handed over");

                pacer.Tick(match, true, false, 0f);
                Assert.That(pacer.RollSecondsLeft, Is.EqualTo(TurnPacer.RollClockSeconds).Within(0.01f));
                return;
            }

            Assert.Fail("no seed under 500 opened on a roll that owes nothing");
        }

        // ── Auto end-turn ────────────────────────────────────────────────

        [Test]
        public void AutoEnd_EndsATurnWithNothingLeft_AfterABeat()
        {
            // Every operator seated and a roll with no 6 and no doubles: nothing
            // can move, deploy or cast.
            for (int seed = 0; seed < 500; seed++)
            {
                var match = Started(seed);
                var rolled = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;
                if (rolled.Contains(6) || rolled.IsDouble) continue;

                var command = TickUntil(new TurnPacer(), match, 5f, out float elapsed);

                Assert.That(command, Is.InstanceOf<EndTurnCommand>());
                Assert.That(elapsed, Is.GreaterThanOrEqualTo(TurnPacer.AutoEndDelaySeconds));
                Assert.That(elapsed, Is.LessThan(TurnPacer.AutoEndDelaySeconds + TurnPacer.AutoEndPollSeconds + 2 * Frame));
                return;
            }

            Assert.Fail("no seed under 500 opened on a roll that owes nothing");
        }

        [Test]
        public void AutoEnd_WaitsWhileAMoveIsOwed()
        {
            var match = Started(opening: 2);
            match.Engine.Execute(new RollDiceCommand());

            Assert.That(match.Engine.CanMove, Is.True, "precondition");
            Assert.That(TickUntil(new TurnPacer(), match, 10f, out _), Is.Null);
        }

        [Test]
        public void AutoEnd_Off_NeverEnds()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var match = Started(seed);
                var rolled = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;
                if (rolled.Contains(6) || rolled.IsDouble) continue;

                Assert.That(TickUntil(new TurnPacer(), match, 10f, out _, autoEnd: false), Is.Null);
                return;
            }

            Assert.Fail("no seed under 500 opened on a roll that owes nothing");
        }
    }
}
