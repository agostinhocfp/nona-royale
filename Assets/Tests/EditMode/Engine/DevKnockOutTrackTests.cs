// Assets/Tests/EditMode/Engine/DevKnockOutTrackTests.cs
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
    /// The dev-only knockout (<see cref="GameEngine.DevKnockOutTrack"/>,
    /// LAUNCH_UI_PASS.md G8c): every operator on the outer track goes through
    /// the ordinary neutralize path back to its yard, and nothing else moves.
    /// </summary>
    [TestFixture]
    public class DevKnockOutTrackTests
    {
        private static readonly PlayerColor[] TwoSeats = { PlayerColor.Red, PlayerColor.Blue };

        private static readonly PlayerColor[] FourSeats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
        };

        private static MatchFactory.Match Started(PlayerColor[] seats)
        {
            var match = MatchFactory.CreateAlphaMatch(seats, seed: 7);
            match.Engine.Start();
            return match;
        }

        /// <summary>Puts one operator of every seat on its own start cell, so each test has victims.</summary>
        private static void DeployOneEach(MatchFactory.Match match)
        {
            foreach (var player in match.Players)
            {
                if (player.Operators.Any(o => match.Map.IsOnOuterTrack(o.Progress))) continue;
                player.Operators.First(o => o.IsInYard).MoveTo(0);
            }
        }

        [Test]
        public void EveryOperatorOnTheTrack_IsKnockedOutToItsYard_AtFullHealth()
        {
            var match = Started(FourSeats);
            DeployOneEach(match);
            var onTrack = match.Operators.Where(o => match.Map.IsOnOuterTrack(o.Progress)).ToList();
            Assert.That(onTrack, Is.Not.Empty);

            var events = match.Engine.DevKnockOutTrack();

            var down = events.OfType<OperatorNeutralized>().ToList();
            Assert.That(down.Select(e => e.Operator), Is.EquivalentTo(onTrack));
            Assert.That(down.All(e => e.Cause == GameEngine.DevCause), Is.True);
            foreach (var op in onTrack)
            {
                Assert.That(op.IsInYard, Is.True, op.Name);
                Assert.That(op.Health, Is.EqualTo(op.MaxHealth), op.Name);
            }
        }

        [Test]
        public void YardAndHomeColumnOperators_AreLeftAlone()
        {
            var match = Started(TwoSeats);
            DeployOneEach(match);
            var player = match.Players[0];
            var inColumn = player.Operators.First(o => o.IsInYard);
            inColumn.MoveTo(match.Map.Profile.TrackLength + 1);
            Assert.That(match.Map.IsInHomeColumn(inColumn.Progress), Is.True);
            var yarded = match.Players[1].Operators.Where(o => o.IsInYard).ToList();

            var events = match.Engine.DevKnockOutTrack();

            var down = events.OfType<OperatorNeutralized>().Select(e => e.Operator).ToList();
            Assert.That(down, Has.No.Member(inColumn));
            Assert.That(inColumn.Progress, Is.EqualTo(match.Map.Profile.TrackLength + 1));
            foreach (var op in yarded) Assert.That(down, Has.No.Member(op), op.Name);
        }

        [Test]
        public void NoKillerMeansNoBountyAndNoCredit_ButTheLossIsCounted()
        {
            var match = Started(TwoSeats);
            DeployOneEach(match);
            var engine = match.Engine;
            int onTrack = match.Operators.Count(o => match.Map.IsOnOuterTrack(o.Progress));

            var events = engine.DevKnockOutTrack();

            Assert.That(events.OfType<EnergyGranted>().Any(), Is.False);
            Assert.That(events.OfType<OperatorNeutralized>().All(e => e.CreditedTo == null), Is.True);
            Assert.That(TwoSeats.Sum(s => engine.KnockoutsScoredBy(s)), Is.EqualTo(0));
            Assert.That(TwoSeats.Sum(s => engine.OperatorsLostBy(s)), Is.EqualTo(onTrack));
        }

        [Test]
        public void TheTurnGoesOn()
        {
            var match = Started(TwoSeats);
            DeployOneEach(match);
            var engine = match.Engine;
            var seat = engine.CurrentPlayer.Color;
            var phase = engine.Phase;

            var events = engine.DevKnockOutTrack();

            Assert.That(events.OfType<TurnEnded>().Any(), Is.False);
            Assert.That(engine.CurrentPlayer.Color, Is.EqualTo(seat));
            Assert.That(engine.Phase, Is.EqualTo(phase));
            Assert.That(engine.MatchOver, Is.False);
        }

        [Test]
        public void ItWorksMidTurn_AfterARoll()
        {
            var match = Started(TwoSeats);
            DeployOneEach(match);
            var engine = match.Engine;
            engine.Execute(new RollDiceCommand());
            Assert.That(engine.Phase, Is.EqualTo(TurnPhase.Action));

            var events = engine.DevKnockOutTrack();

            Assert.That(events.OfType<OperatorNeutralized>().Any(), Is.True);
            Assert.That(engine.Phase, Is.EqualTo(TurnPhase.Action));
        }

        [Test]
        public void WithNobodyOnTheTrack_ItIsRefused()
        {
            var match = Started(TwoSeats);
            foreach (var op in match.Operators) op.MoveTo(PathMap.YardProgress);

            var events = match.Engine.DevKnockOutTrack();

            Assert.That(events.OfType<CommandRejected>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<OperatorNeutralized>().Any(), Is.False);
        }

        [Test]
        public void BeforeTheFirstTurn_AndAfterTheMatch_ItIsRefused()
        {
            var fresh = MatchFactory.CreateAlphaMatch(TwoSeats, seed: 7);
            Assert.That(fresh.Engine.DevKnockOutTrack().OfType<CommandRejected>().Count(), Is.EqualTo(1));

            var over = Started(TwoSeats);
            over.Engine.DevForceWin();
            Assert.That(over.Engine.MatchOver, Is.True);
            var events = over.Engine.DevKnockOutTrack();
            Assert.That(events.OfType<CommandRejected>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<OperatorNeutralized>().Any(), Is.False);
        }

        [Test]
        public void ItIsNotACommand_SoNoExecutedListenerHearsIt()
        {
            var match = Started(TwoSeats);
            DeployOneEach(match);
            int heard = 0;
            match.Engine.Executed += (seat, command, events) => heard++;

            match.Engine.DevKnockOutTrack();

            Assert.That(heard, Is.EqualTo(0));
        }
    }
}
