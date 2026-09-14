// Assets/Tests/EditMode/Rng/SeededRandomTests.cs
using System;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Rng
{
    [TestFixture]
    public class SeededRandomTests
    {
        [Test]
        public void TheSameSeed_ProducesTheSameSequence()
        {
            // The whole reason randomness is injected. Without this, no rule
            // that touches dice or evasion can have a deterministic test.
            var a = new SeededRandom(1234);
            var b = new SeededRandom(1234);

            for (int i = 0; i < 50; i++)
                Assert.That(a.NextInt(1, 7), Is.EqualTo(b.NextInt(1, 7)));
        }

        [Test]
        public void DifferentSeeds_Diverge()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);

            bool anyDifference = false;
            for (int i = 0; i < 50; i++)
                if (a.NextInt(1, 7) != b.NextInt(1, 7)) anyDifference = true;

            Assert.That(anyDifference, Is.True);
        }

        [Test]
        public void AnEmptyRange_IsRejected()
        {
            var rng = new SeededRandom(7);

            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(3, 3));
        }

        [Test]
        public void RolledDice_StayWithinTheirFaces()
        {
            var rng = new SeededRandom(99);
            var config = GameConfig.Default;

            for (int i = 0; i < 500; i++)
            {
                var roll = DiceRoll.Roll(rng, config);

                Assert.That(roll.First, Is.AtLeast(1));
                Assert.That(roll.First, Is.LessThanOrEqualTo(config.DiceSides));
                Assert.That(roll.Second, Is.AtLeast(1));
                Assert.That(roll.Second, Is.LessThanOrEqualTo(config.DiceSides));
            }
        }

        [Test]
        public void ARoll_KnowsItsDoublesAndItsSixes()
        {
            var doubleSix = new DiceRoll(6, 6);

            Assert.That(doubleSix.IsDouble, Is.True);
            Assert.That(doubleSix.CountOf(6), Is.EqualTo(2));
            Assert.That(doubleSix.Total, Is.EqualTo(12));

            var mixed = new DiceRoll(6, 2);

            Assert.That(mixed.IsDouble, Is.False);
            Assert.That(mixed.CountOf(6), Is.EqualTo(1));
            Assert.That(mixed.OtherThan(6), Is.EqualTo(2));
        }

        [Test]
        public void AskingForTheOtherDie_WhenTheFaceIsAbsent_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new DiceRoll(3, 4).OtherThan(6));
        }
    }
}