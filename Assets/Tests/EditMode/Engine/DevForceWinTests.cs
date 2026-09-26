// Assets/Tests/EditMode/Engine/DevForceWinTests.cs
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Engine
{
    /// <summary>
    /// The dev-only win (<see cref="GameEngine.DevForceWin"/>, LAUNCH_UI_PASS.md
    /// G7): it sends the current side home and lets the ordinary win check end
    /// the match, and it never touches anyone else.
    /// </summary>
    [TestFixture]
    public class DevForceWinTests
    {
        private static readonly PlayerColor[] FourSeats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
        };

        private static MatchFactory.Match Started(PlayerColor[] seats, TeamMap teams = null)
        {
            var match = MatchFactory.CreateAlphaMatch(seats, seed: 7, teams: teams);
            match.Engine.Start();
            return match;
        }

        private static readonly PlayerColor[] TwoSeats = { PlayerColor.Red, PlayerColor.Blue };

        [Test]
        public void ItWinsTheMatchForTheSeatToPlay()
        {
            var match = Started(TwoSeats);
            var engine = match.Engine;
            var seat = engine.CurrentPlayer.Color;

            var events = engine.DevForceWin();

            var won = events.OfType<GameWon>().Single();
            Assert.That(won.Winner, Is.EqualTo(seat));
            Assert.That(engine.MatchOver, Is.True);
            Assert.That(engine.Winner, Is.EqualTo(seat));
            Assert.That(match.Players.First(p => p.Color == seat).Operators.All(engine.IsHome), Is.True);
        }

        [Test]
        public void EachOperatorThatArrivesIsReported_AndTheTurnEnds()
        {
            var match = Started(TwoSeats);
            var engine = match.Engine;
            var squad = engine.CurrentPlayer.Operators.ToList();

            var events = engine.DevForceWin();

            var arrived = events.OfType<OperatorReachedHome>().Select(e => e.Operator).ToList();
            Assert.That(arrived, Is.EquivalentTo(squad));
            Assert.That(events.OfType<TurnEnded>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<GameWon>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void ItWorksMidTurn_AfterARoll()
        {
            var match = Started(TwoSeats);
            var engine = match.Engine;
            engine.Execute(new RollDiceCommand());
            Assert.That(engine.Phase, Is.EqualTo(TurnPhase.Action));

            var events = engine.DevForceWin();

            Assert.That(events.OfType<GameWon>().Count(), Is.EqualTo(1));
            Assert.That(engine.MatchOver, Is.True);
        }

        [Test]
        public void OtherSeatsAreLeftWhereTheyWere()
        {
            var match = Started(FourSeats);
            var engine = match.Engine;
            var seat = engine.CurrentPlayer.Color;
            var before = match.Players.Where(p => p.Color != seat)
                .SelectMany(p => p.Operators)
                .ToDictionary(o => o, o => o.Progress);

            engine.DevForceWin();

            foreach (var pair in before)
                Assert.That(pair.Key.Progress, Is.EqualTo(pair.Value), pair.Key.Name);
        }

        [Test]
        public void AtACrossedTable_BothPartnersGoHome_AndTheSideWins()
        {
            var match = Started(FourSeats, TeamMap.CrossedPairs);
            var engine = match.Engine;
            var seat = engine.CurrentPlayer.Color;
            var side = TeamMap.CrossedPairs.SeatsOn(seat);

            var events = engine.DevForceWin();

            var won = events.OfType<GameWon>().Single();
            Assert.That(won.Seats, Is.EquivalentTo(side));
            foreach (var player in match.Players)
            {
                bool onSide = side.Contains(player.Color);
                Assert.That(player.Operators.All(engine.IsHome), Is.EqualTo(onSide), player.Color.ToString());
            }
        }

        [Test]
        public void OnceTheMatchIsOver_ItIsRefused()
        {
            var match = Started(TwoSeats);
            var engine = match.Engine;
            engine.DevForceWin();

            var events = engine.DevForceWin();

            Assert.That(events.OfType<CommandRejected>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<GameWon>().Any(), Is.False);
        }

        [Test]
        public void BeforeTheFirstTurn_ItIsRefused()
        {
            var match = MatchFactory.CreateAlphaMatch(TwoSeats, seed: 7);

            var events = match.Engine.DevForceWin();

            Assert.That(events.OfType<CommandRejected>().Count(), Is.EqualTo(1));
            Assert.That(match.Engine.MatchOver, Is.False);
        }

        [Test]
        public void ItIsNotACommand_SoNoExecutedListenerHearsIt()
        {
            var match = Started(TwoSeats);
            var engine = match.Engine;
            int heard = 0;
            engine.Executed += (seat, command, events) => heard++;

            engine.DevForceWin();

            Assert.That(heard, Is.EqualTo(0));
        }
    }
}
