// Assets/Tests/EditMode/Bots/EvasionPricingTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Bots
{
    /// <summary>
    /// The bots discount only the hit that actually gets the round's evasion
    /// roll (2026-09-24). Evasion is dormant, so the holder here is granted it
    /// by hand; the pricing has to be right for the next operator who wants it.
    /// </summary>
    [TestFixture]
    public class EvasionPricingTests
    {
        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };

        private MatchFactory.Match _match;
        private OperatorState _holder;

        [SetUp]
        public void SetUp()
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                { PlayerColor.Red, new[] { Luka.Definition, Javi.Definition, Bouncer.Definition } },
                { PlayerColor.Blue, new[] { Syla.Definition, Kian.Definition, Mimi.Definition } }
            };
            _match = MatchFactory.Create(Two, 19, squads);
            _match.Engine.Start();

            foreach (var op in _match.Operators)
                if (op.Name == "Syla") _holder = op;

            _match.Statuses.ApplyPassive(_holder, StatusKind.Evasion);
        }

        private double Priced(DamageType type) =>
            new BotBoard(_match).ExpectedHitOnceExposed(_holder, 2, type);

        [Test]
        public void AReadyCharge_DiscountsNormalAndTech()
        {
            double kept = 2 * (1.0 - new BotBoard(_match).Combat.EvasionChance);

            Assert.That(Priced(DamageType.Normal), Is.EqualTo(kept).Within(1e-9));
            Assert.That(Priced(DamageType.Tech), Is.EqualTo(kept).Within(1e-9));
            Assert.That(Priced(DamageType.Atomic), Is.EqualTo(2.0), "Atomic ignores evasion");
        }

        [Test]
        public void ASpentCharge_DiscountsNothing()
        {
            _match.Statuses.TryEvade(_holder, new SeededRandom(1));   // the round's roll, spent

            Assert.That(_match.Engine.EvasionReady(_holder), Is.False, "precondition");
            Assert.That(Priced(DamageType.Normal), Is.EqualTo(2.0));
            Assert.That(Priced(DamageType.Tech), Is.EqualTo(2.0));
        }

        [Test]
        public void NobodyHoldsEvasion_InTheRoster()
        {
            foreach (var def in Roster.All)
            {
                Assert.That(def.Passive, Is.Not.EqualTo(StatusKind.Evasion), def.Name);
                Assert.That(def.Passive2, Is.Not.EqualTo(StatusKind.Evasion), def.Name);
            }
        }
    }
}
