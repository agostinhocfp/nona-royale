// Assets/Tests/EditMode/Unity/VoiceCastingTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Unity.Audio;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.Audio
{
    [TestFixture]
    public class VoiceCastingTests
    {
        private OperatorState _luka;
        private OperatorState _javi;
        private OperatorState _syla;

        [SetUp]
        public void SetUp()
        {
            _luka = new OperatorState(1, "Luka", PlayerColor.Red, 7, 2.0);
            _javi = new OperatorState(2, "Javi", PlayerColor.Red, 7, 1.5);
            _syla = new OperatorState(3, "Syla", PlayerColor.Blue, 7, 2.0);
        }

        private static OperatorMoved Walk(OperatorState op) => new OperatorMoved(op, 3, 7, default(CellRef));

        [Test]
        public void Mover_IsTheOperatorThatWalkedForward()
        {
            var events = new IGameEvent[] { new OperatorMoved(_syla, 5, 5, default(CellRef)), Walk(_luka) };
            Assert.AreSame(_luka, VoiceCasting.Mover(events));
        }

        [Test]
        public void Mover_IsNull_WithoutAForwardWalk()
        {
            Assert.IsNull(VoiceCasting.Mover(new IGameEvent[] { new OperatorMoved(_luka, 7, 3, default(CellRef)) }));
            Assert.IsNull(VoiceCasting.Mover(null));
        }

        [Test]
        public void Killer_IsTheCaster_WhenTheKnockoutCountsForItsSeat()
        {
            var down = new OperatorNeutralized(_syla, "ability", PlayerColor.Red);
            Assert.AreSame(_luka, VoiceCasting.Killer(new IGameEvent[] { down }, _luka, down));
        }

        [Test]
        public void Killer_IsNull_WhenTheCasterIsOnAnotherSeat()
        {
            var down = new OperatorNeutralized(_javi, "ability", PlayerColor.Red);
            Assert.IsNull(VoiceCasting.Killer(new IGameEvent[] { down }, _syla, down));
        }

        [Test]
        public void Killer_IsTheMover_WhenNothingWasCast()
        {
            var down = new OperatorNeutralized(_syla, "collision", PlayerColor.Red);
            var events = new IGameEvent[] { Walk(_luka), new CollisionResolved(_luka, _syla, false), down };
            Assert.AreSame(_luka, VoiceCasting.Killer(events, null, down));
        }

        [Test]
        public void Killer_CanComeFromTheCollisionAlone()
        {
            var down = new OperatorNeutralized(_syla, "collision", PlayerColor.Red);
            var events = new IGameEvent[] { new CollisionResolved(_javi, _syla, false), down };
            Assert.AreSame(_javi, VoiceCasting.Killer(events, null, down));
        }

        [Test]
        public void Killer_IsNull_WithoutCreditOrActor()
        {
            var uncredited = new OperatorNeutralized(_syla, "bleed");
            Assert.IsNull(VoiceCasting.Killer(new IGameEvent[] { Walk(_luka), uncredited }, _luka, uncredited));

            var upkeep = new OperatorNeutralized(_syla, "mark", PlayerColor.Red);
            Assert.IsNull(VoiceCasting.Killer(new IGameEvent[] { upkeep }, null, upkeep));
        }

        [Test]
        public void Killer_IsNeverTheVictim()
        {
            var down = new OperatorNeutralized(_luka, "self", PlayerColor.Red);
            Assert.IsNull(VoiceCasting.Killer(new IGameEvent[] { down }, _luka, down));
            Assert.IsNull(VoiceCasting.Killer(new IGameEvent[] { Walk(_luka), down }, null, down));
        }

        [Test]
        public void Victor_IsTheLastWinnerToReachHome()
        {
            var events = new IGameEvent[]
            {
                new OperatorReachedHome(_javi), new OperatorReachedHome(_syla), new OperatorReachedHome(_luka),
            };
            Assert.AreSame(_luka, VoiceCasting.Victor(events, PlayerColor.Red, null));
        }

        [Test]
        public void Victor_FallsBackToTheWinningSquad()
        {
            var squad = new List<OperatorState> { _syla, _javi, _luka };
            Assert.AreSame(_javi, VoiceCasting.Victor(new IGameEvent[0], PlayerColor.Red, squad));
            Assert.IsNull(VoiceCasting.Victor(null, PlayerColor.Green, squad));
        }
    }
}