// Assets/Tests/EditMode/Unity/CooldownSweepTests.cs
using System.Collections.Generic;
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The cooldown sweep's geometry and arithmetic: how much of a card the
    /// veil covers for a cooldown, where its hand points, and that the fan
    /// keeps to the card's chamfered outline.
    /// </summary>
    [TestFixture]
    public class CooldownSweepTests
    {
        private static readonly Vector2 Card = new Vector2(150f, 46f);
        private const float Cut = CooldownSweepGeometry.ButtonCut;

        [Test]
        public void Fraction_CountsTheTurnOfTheCast()
        {
            // Cooldown 2: just cast (3 left), then 2, then 1, then ready.
            Assert.AreEqual(1f, CooldownSweepGeometry.Fraction(3, 2), 1e-6f);
            Assert.AreEqual(2f / 3f, CooldownSweepGeometry.Fraction(2, 2), 1e-6f);
            Assert.AreEqual(1f / 3f, CooldownSweepGeometry.Fraction(1, 2), 1e-6f);
            Assert.AreEqual(0f, CooldownSweepGeometry.Fraction(0, 2));
        }

        [Test]
        public void Fraction_IsNothing_WithoutACooldown()
        {
            Assert.AreEqual(0f, CooldownSweepGeometry.Fraction(0, 0));
            Assert.AreEqual(0f, CooldownSweepGeometry.Fraction(3, 0));
            Assert.AreEqual(0f, CooldownSweepGeometry.Fraction(-1, 2));
        }

        [Test]
        public void Fraction_NeverOverfills()
        {
            Assert.AreEqual(1f, CooldownSweepGeometry.Fraction(9, 2));
        }

        [Test]
        public void TheHand_TurnsClockwiseAsTheVeilShrinks()
        {
            Assert.AreEqual(0f, CooldownSweepGeometry.HandAngle(1f), 1e-4f);
            Assert.AreEqual(90f, CooldownSweepGeometry.HandAngle(0.75f), 1e-4f);
            Assert.AreEqual(180f, CooldownSweepGeometry.HandAngle(0.5f), 1e-4f);
            Assert.AreEqual(360f, CooldownSweepGeometry.HandAngle(0f), 1e-4f);
        }

        [Test]
        public void TheOutline_MeetsTheEdgesStraightOn()
        {
            AssertNear(new Vector2(0f, Card.y), CooldownSweepGeometry.Outline(0f, Card, Cut));
            AssertNear(new Vector2(Card.x, 0f), CooldownSweepGeometry.Outline(90f, Card, Cut));
            AssertNear(new Vector2(0f, -Card.y), CooldownSweepGeometry.Outline(180f, Card, Cut));
            AssertNear(new Vector2(-Card.x, 0f), CooldownSweepGeometry.Outline(270f, Card, Cut));
        }

        [Test]
        public void TheOutline_StaysInsideTheChamfer()
        {
            for (float degrees = 0f; degrees < 360f; degrees += 0.5f)
            {
                var p = CooldownSweepGeometry.Outline(degrees, Card, Cut);
                Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(Card.x + 1e-3f), $"{degrees}°");
                Assert.That(Mathf.Abs(p.y), Is.LessThanOrEqualTo(Card.y + 1e-3f), $"{degrees}°");
                Assert.That(Mathf.Abs(p.x) + Mathf.Abs(p.y), Is.LessThanOrEqualTo(Card.x + Card.y - Cut + 1e-3f), $"{degrees}°");

                // And on it: one of the three limits is met exactly.
                float slack = Mathf.Min(Card.x - Mathf.Abs(p.x), Card.y - Mathf.Abs(p.y),
                    Card.x + Card.y - Cut - Mathf.Abs(p.x) - Mathf.Abs(p.y));
                Assert.That(slack, Is.LessThan(1e-3f), $"{degrees}°");
            }
        }

        [Test]
        public void TheOutline_CutsTheCorners()
        {
            // Towards the top-right corner the ray meets the chamfer, not the box's corner.
            float degrees = Mathf.Atan2(Card.x, Card.y) * Mathf.Rad2Deg;
            var p = CooldownSweepGeometry.Outline(degrees, Card, Cut);
            Assert.AreEqual(Card.x + Card.y - Cut, p.x + p.y, 1e-3f);
            Assert.That(p.x, Is.LessThan(Card.x));
        }

        [Test]
        public void TheFan_RunsFromTheHandRoundToTwelve_InOrder()
        {
            var angles = new List<float>();
            CooldownSweepGeometry.FanAngles(130f, Card, Cut, angles);

            Assert.AreEqual(130f, angles[0], 1e-4f);
            Assert.AreEqual(360f, angles[angles.Count - 1], 1e-4f);
            for (int i = 1; i < angles.Count; i++)
                Assert.That(angles[i], Is.GreaterThanOrEqualTo(angles[i - 1]));
        }

        [Test]
        public void TheFan_KeepsEveryCornerItCovers()
        {
            var angles = new List<float>();
            CooldownSweepGeometry.FanAngles(0f, Card, Cut, angles);

            // A whole veil covers all eight corners of the chamfered box.
            var corners = new[]
            {
                new Vector2(Card.x - Cut, Card.y), new Vector2(Card.x, Card.y - Cut),
                new Vector2(Card.x, -(Card.y - Cut)), new Vector2(Card.x - Cut, -Card.y),
                new Vector2(-(Card.x - Cut), -Card.y), new Vector2(-Card.x, -(Card.y - Cut)),
                new Vector2(-Card.x, Card.y - Cut), new Vector2(-(Card.x - Cut), Card.y)
            };

            foreach (var corner in corners)
            {
                float degrees = Mathf.Atan2(corner.x, corner.y) * Mathf.Rad2Deg;
                if (degrees < 0f) degrees += 360f;
                Assert.That(angles.Exists(a => Mathf.Abs(a - degrees) < 1e-3f), $"corner at {degrees}°");
            }
        }

        [Test]
        public void TheFan_IsEmpty_WhenTheCardIsReady()
        {
            var angles = new List<float> { 1f, 2f };
            CooldownSweepGeometry.FanAngles(360f, Card, Cut, angles);
            Assert.That(angles, Is.Empty);
        }

        [Test]
        public void EachOperatorsAbility_HasItsOwnKey()
        {
            Assert.AreNotEqual(CooldownSweepGeometry.KeyOf(1, 2), CooldownSweepGeometry.KeyOf(2, 1));
            Assert.AreNotEqual(CooldownSweepGeometry.KeyOf(1, 101), CooldownSweepGeometry.KeyOf(2, 101));
            Assert.AreEqual(CooldownSweepGeometry.KeyOf(7, 1101), CooldownSweepGeometry.KeyOf(7, 1101));
        }

        private static void AssertNear(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-3f);
            Assert.AreEqual(expected.y, actual.y, 1e-3f);
        }
    }
}
