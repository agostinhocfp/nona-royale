// Assets/Tests/EditMode/Turn/TurnOrderTests.cs
using System;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Turn
{
    [TestFixture]
    public class TurnOrderTests
    {
        private static readonly PlayerColor[] Four =
            { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet };

        private static readonly PlayerColor[] Opposite = { PlayerColor.Red, PlayerColor.Green };

        [Test]
        public void Opening_SameSeed_SameOrder()
        {
            for (int seed = 0; seed < 50; seed++)
                CollectionAssert.AreEqual(TurnOrder.Opening(Four, seed), TurnOrder.Opening(Four, seed));
        }

        [Test]
        public void Opening_IsARotation_OfTableOrder()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var order = TurnOrder.Opening(Four, seed);
                int start = Array.IndexOf(Four, order[0]);

                for (int i = 0; i < Four.Length; i++)
                    Assert.AreEqual(Four[(start + i) % Four.Length], order[i], $"seed {seed}, position {i}");
            }
        }

        [Test]
        public void Opening_DoesNotChangeTheInput()
        {
            var seats = (PlayerColor[])Four.Clone();
            TurnOrder.Opening(seats, 12345);
            CollectionAssert.AreEqual(Four, seats);
        }

        [Test]
        public void Opening_OneSeat_IsThatSeat()
        {
            CollectionAssert.AreEqual(new[] { PlayerColor.Blue }, TurnOrder.Opening(new[] { PlayerColor.Blue }, 7));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void FirstSeat_IsRoughlyFair_AcrossNeighbouringSeeds(int seats)
        {
            // Neighbouring seeds on purpose: a rematch can use the seed after
            // the last one, and System.Random correlates those.
            const int draws = 4000;
            var counts = new int[seats];
            for (int seed = 20260912; seed < 20260912 + draws; seed++)
                counts[TurnOrder.FirstSeatIndex(seats, seed)]++;

            double expected = (double)draws / seats;
            foreach (int count in counts)
                Assert.That(count, Is.InRange(expected * 0.85, expected * 1.15), string.Join(", ", counts));
        }

        [Test]
        public void FirstSeat_NeighbouringSeeds_DoNotStepInLockstep()
        {
            // The failure this guards: seed n and n+1 always opening on
            // neighbouring seats, so every rematch rotates the opener by one.
            int sameStep = 0;
            for (int seed = 1000; seed < 1400; seed++)
            {
                int a = TurnOrder.FirstSeatIndex(4, seed);
                int b = TurnOrder.FirstSeatIndex(4, seed + 1);
                if ((a + 1) % 4 == b) sameStep++;
            }

            Assert.Less(sameStep, 150, "about 100 expected by chance out of 400");
        }

        [Test]
        public void Opening_TwoSeatsOpposite_EitherCanOpen()
        {
            bool redFirst = false, greenFirst = false;
            for (int seed = 0; seed < 50; seed++)
            {
                var order = TurnOrder.Opening(Opposite, seed);
                redFirst |= order[0] == PlayerColor.Red;
                greenFirst |= order[0] == PlayerColor.Green;
            }

            Assert.IsTrue(redFirst && greenFirst);
        }
    }
}
