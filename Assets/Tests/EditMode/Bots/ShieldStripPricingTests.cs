// Assets/Tests/EditMode/Bots/ShieldStripPricingTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Model;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Bots
{
    /// <summary>
    /// Inversion Matrix tears a shield off before its own damage (§5.8,
    /// 2026-10-03), so the planner has to price the beam against bare health
    /// rather than against the pool. Without this the bots would read a
    /// shielded enemy as immune to the very ability built to answer the shield,
    /// and the sweep would say the change did nothing.
    /// </summary>
    [TestFixture]
    public class ShieldStripPricingTests
    {
        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };

        private MatchFactory.Match _match;
        private OperatorState _kian;
        private OperatorState _victim;
        private BotWeights _weights;

        [SetUp]
        public void SetUp()
        {
            // Kian is Blue and his victim Red, because Red holds the turn after
            // Start() and a shield only takes hold on its holder's own turn
            // (§5). A shield handed to a Blue operator here would absorb
            // nothing, and every assertion below would pass on a planner that
            // ignored the strip entirely.
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                { PlayerColor.Red, new[] { Luka.Definition, Javi.Definition, Bouncer.Definition } },
                { PlayerColor.Blue, new[] { Syla.Definition, Kian.Definition, Mimi.Definition } }
            };
            _match = MatchFactory.Create(Two, 19, squads);
            _match.Engine.Start();

            foreach (var op in _match.Operators)
            {
                if (op.Name == "Kian") _kian = op;
                if (op.Name == "Javi") _victim = op;
            }

            Place(_kian, 10);
            Place(_victim, 12);
            _weights = BotWeights.For(BotPersonality.Brawler);
        }

        private void Place(OperatorState op, int track)
        {
            int circuit = _match.Map.Profile.CircuitLength;
            op.MoveTo((track - _match.Map.StartTrackIndex(op.Owner) + circuit) % circuit);
        }

        private void ShieldUp(OperatorState op)
        {
            _match.Statuses.Apply(op, StatusKind.Shield, duration: 3);
            Assert.That(_match.Engine.ShieldPoolOn(op), Is.GreaterThan(0), "the fixture's shield is live");
        }

        /// <summary>Inversion Matrix aims at nobody, so the cast carries no target.</summary>
        private double Offence() =>
            CastPlanner.Score(new BotBoard(_match), _weights, _match.Players[1],
                _kian, Kian.InversionMatrix, null, null, null).Offence;

        [Test]
        public void TheShieldCostsTheCastNothing_BecauseTheCastTakesItOff()
        {
            // The strip is worth exactly the pool it removes, so a shielded
            // enemy and a bare one price the same. This is the assertion that
            // fails if StripValue returns zero: the shielded figure drops to
            // whatever gets past a 2-deep pool, which for a 2-damage beam is
            // nothing at all.
            double bare = Offence();

            ShieldUp(_victim);
            double shielded = Offence();

            Assert.That(bare, Is.GreaterThan(0.0), "precondition: the beam is worth something");
            Assert.That(shielded, Is.EqualTo(bare).Within(1e-9));
        }

        [Test]
        public void AStrippedShield_ScoresTheKillItUncovers()
        {
            // The reason the valuation runs through Hit() rather than adding
            // damage: a pool deeper than the beam is the difference between
            // "shrugs it off" and "dies to it", and the kill bonus is the whole
            // point of spending the cast.
            _victim.SetHealth(2);
            double bare = Offence();

            ShieldUp(_victim);
            double shielded = Offence();

            Assert.That(shielded, Is.EqualTo(bare).Within(1e-9));
            Assert.That(shielded, Is.GreaterThan(_weights.Kill), "a kill, not two points of damage");
        }

        [Test]
        public void AShieldBehindHim_IsWorthNothing()
        {
            // The line points forward. A pool he cannot reach is not a pool he
            // can price.
            Place(_victim, 8);
            double bare = Offence();

            ShieldUp(_victim);
            double shielded = Offence();

            Assert.That(bare, Is.EqualTo(0.0), "nothing ahead of him");
            Assert.That(shielded, Is.EqualTo(0.0));
        }

        [Test]
        public void TheStrippedEstimate_IgnoresThePool_AndNothingElse()
        {
            ShieldUp(_victim);
            var board = new BotBoard(_match);
            int pool = _match.Engine.ShieldPoolOn(_victim);

            Assert.That(pool, Is.EqualTo(2), "the default pool, for the figures below");
            Assert.That(board.ExpectedHit(_victim, 2, DamageType.Tech), Is.EqualTo(0.0));
            Assert.That(board.ExpectedHit(_victim, 2, DamageType.Tech, null, shieldStripped: true), Is.EqualTo(2.0));

            // A ward is not a pool, and a strip is not a cleanse: stripping the
            // shield off a warded target leaves the ward blocking the beam
            // (§5.12). A fresh board, because BotBoard caches what it reads off
            // the engine for the length of one decision.
            _match.Statuses.Apply(_victim, StatusKind.TechWard, duration: 3);
            Assert.That(new BotBoard(_match).ExpectedHit(_victim, 2, DamageType.Tech, null, shieldStripped: true),
                Is.EqualTo(0.0));
        }
    }
}
