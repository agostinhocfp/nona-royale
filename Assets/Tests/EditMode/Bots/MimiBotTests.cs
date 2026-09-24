// Assets/Tests/EditMode/Bots/MimiBotTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Model;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Bots
{
    /// <summary>
    /// Mimi's 2026-09-17 price cut, and the Cryo Field scoring the bots were
    /// missing: before this, no bot ever cast it (0.00 casts a match).
    /// </summary>
    [TestFixture]
    public class MimiBotTests
    {
        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };

        private MatchFactory.Match _match;
        private OperatorState _mimi;
        private OperatorState[] _foes;

        [SetUp]
        public void SetUp()
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                { PlayerColor.Red, new[] { Mimi.Definition, Bouncer.Definition, Javi.Definition } },
                { PlayerColor.Blue, new[] { Syla.Definition, Kian.Definition, Nuetu.Definition } }
            };

            _match = MatchFactory.Create(Two, 29, squads);
            _match.Engine.Start();

            var foes = new List<OperatorState>();
            foreach (var op in _match.Operators)
            {
                if (op.Name == "Mimi") _mimi = op;
                else if (op.Owner == PlayerColor.Blue) foes.Add(op);
            }
            _foes = foes.ToArray();

            Place(_mimi, 10);
            for (int i = 0; i < _foes.Length; i++) Place(_foes[i], 30 + i);
        }

        private void Place(OperatorState op, int track)
        {
            int circuit = _match.Map.Profile.CircuitLength;
            op.MoveTo((track - _match.Map.StartTrackIndex(op.Owner) + circuit) % circuit);
        }

        private double CryoFieldOffence() =>
            CastPlanner.Score(new BotBoard(_match), BotWeights.For(BotPersonality.Brawler), _match.Players[0],
                _mimi, Mimi.CryoField, null, null, null).Offence;

        [Test]
        public void TheDesignersNumbers()
        {
            // 2026-09-17, pass one: both of her 6-cost casts down to 4.
            // Pass two: +1 health, Cryo Field tick 1 → 2, radius 2 → 3.
            Assert.That(Mimi.MaxHealth, Is.EqualTo(7));
            Assert.That(Mimi.CryoPulse.EnergyCost, Is.EqualTo(4));
            Assert.That(Mimi.CryoField.EnergyCost, Is.EqualTo(4));
            Assert.That(Mimi.Translocation.EnergyCost, Is.EqualTo(3));
            // 2026-09-24: Translocation's range 6 → 9, her mobility.
            Assert.That(Mimi.Translocation.Range, Is.EqualTo(9));
            Assert.That(Mimi.CryoField.Range, Is.EqualTo(0), "centred on herself");
            Assert.That(Mimi.CryoField.Effects[0].Radius, Is.EqualTo(Mimi.CryoFieldRadius));
            Assert.That(Mimi.CryoFieldRadius, Is.EqualTo(3));
            Assert.That(Mimi.CryoFieldTickDamage, Is.EqualTo(2));
        }

        [Test]
        public void CryoField_IsWorthNothing_WithNobodyNear()
        {
            Assert.That(CryoFieldOffence(), Is.EqualTo(0.0));
        }

        [Test]
        public void CryoField_IsWorthMore_TheMoreEnemiesStandNearHer()
        {
            Place(_foes[0], 11);
            double one = CryoFieldOffence();

            Place(_foes[1], 12);
            double two = CryoFieldOffence();

            Place(_foes[2], 14);   // four away: outside the field
            double stillTwo = CryoFieldOffence();

            Assert.That(one, Is.GreaterThan(0.0));
            Assert.That(two, Is.GreaterThan(one));
            Assert.That(stillTwo, Is.EqualTo(two));
        }

        [Test]
        public void CryoField_IsWorthNothing_WhileOneIsUp()
        {
            Place(_foes[0], 11);
            _match.Statuses.Apply(_mimi, StatusKind.CryoField, 3);

            Assert.That(CryoFieldOffence(), Is.EqualTo(0.0));
        }
    }
}