// Assets/Tests/EditMode/Bots/LetheBotTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Model;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Bots
{
    /// <summary>
    /// What the bots had to learn for Lethe (2026-09-17): a shield is as deep
    /// as its pool, a stun on a friend is a price, a cleanse strips the bubble
    /// it would wash, and a crowd zone is worthless on a lone enemy. Since
    /// 2026-09-24, Eris' Exploit counts the crowd its draw can make.
    /// </summary>
    [TestFixture]
    public class LetheBotTests
    {
        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };

        private MatchFactory.Match _match;
        private PlayerState _seat;
        private OperatorState _lethe;
        private OperatorState _javi;
        private OperatorState _bouncer;
        private OperatorState[] _foes;

        [SetUp]
        public void SetUp()
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                { PlayerColor.Red, new[] { Lethe.Definition, Javi.Definition, Bouncer.Definition } },
                { PlayerColor.Blue, new[] { Syla.Definition, Kian.Definition, Mimi.Definition } }
            };

            _match = MatchFactory.Create(Two, 17, squads);
            _match.Engine.Start();
            _seat = _match.Players[0];

            _lethe = Op(PlayerColor.Red, "Lethe");
            _javi = Op(PlayerColor.Red, "Javi");
            _bouncer = Op(PlayerColor.Red, "Bouncer");
            _foes = new[] { Op(PlayerColor.Blue, "Syla"), Op(PlayerColor.Blue, "Kian"), Op(PlayerColor.Blue, "Mimi") };

            // Red's three together on track 10–12. Blue parked far away until a
            // test brings them in. No start cells (1, 14, 27, 40).
            Place(_lethe, 10);
            Place(_javi, 11);
            Place(_bouncer, 12);
            for (int i = 0; i < _foes.Length; i++) Place(_foes[i], 30 + i);
        }

        private OperatorState Op(PlayerColor seat, string name)
        {
            foreach (var op in _match.Operators)
                if (op.Owner == seat && op.Name == name) return op;
            throw new System.ArgumentException($"{seat} has no {name}");
        }

        private void Place(OperatorState op, int track)
        {
            int circuit = _match.Map.Profile.CircuitLength;
            op.MoveTo((track - _match.Map.StartTrackIndex(op.Owner) + circuit) % circuit);
        }

        private CellRef Track(int track) => _match.Map.CellAt(
            PlayerColor.Red,
            (track - _match.Map.StartTrackIndex(PlayerColor.Red) + _match.Map.Profile.CircuitLength)
            % _match.Map.Profile.CircuitLength);

        private void BubbleBouncer()
        {
            _match.Statuses.Apply(_bouncer, StatusKind.Shield, Lethe.NanoCellDurationTurns, magnitude: Lethe.NanoCellPool);
        }

        /// <summary>
        /// Blue a few cells behind Bouncer, where a roll lands on him. Blue
        /// has no energy yet, so the threat is collisions, not casts.
        /// </summary>
        private void BringTheEnemyClose()
        {
            Place(_foes[0], 9);
            Place(_foes[1], 8);
            Place(_foes[2], 7);
        }

        private static BotWeights Weights => BotWeights.For(BotPersonality.Brawler);

        private ScoredCast Score(OperatorState caster, AbilityDefinition ability, OperatorState target, CellRef? cell = null) =>
            CastPlanner.Score(new BotBoard(_match), Weights, _seat, caster, ability, target, cell, null);

        [Test]
        public void ExpectedHit_ReadsTheRealPool()
        {
            BubbleBouncer();
            var board = new BotBoard(_match);

            Assert.That(board.ShieldPool(_bouncer), Is.EqualTo(Lethe.NanoCellPool));
            Assert.That(board.ExpectedHit(_bouncer, 6, DamageType.Normal), Is.EqualTo(0.0),
                "a bot must not keep hitting a bubble");
            Assert.That(board.ExpectedHit(_bouncer, 3, DamageType.Atomic), Is.EqualTo(3.0));
        }

        [Test]
        public void ExpectedHit_APartPlate_StillLetsTheRestThrough()
        {
            _match.Statuses.Apply(_bouncer, StatusKind.Shield, 2, magnitude: 1);

            Assert.That(new BotBoard(_match).ExpectedHit(_bouncer, 3, DamageType.Normal), Is.EqualTo(2.0));
        }

        [Test]
        public void NanoCell_WithNothingNearby_IsWorthLittle()
        {
            // No stun to pay since 2026-09-24, so a quiet bubble is a small
            // plus — the heal, when there is anything to heal — not a loss.
            Assert.That(Score(_lethe, Lethe.NanoCell, _bouncer).Defence, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(Score(_lethe, Lethe.NanoCell, _bouncer).Defence, Is.LessThan(1.0));
        }

        [Test]
        public void NanoCell_UnderThreat_IsWorthMore()
        {
            double quiet = Score(_lethe, Lethe.NanoCell, _bouncer).Defence;

            BringTheEnemyClose();
            double threatened = Score(_lethe, Lethe.NanoCell, _bouncer).Defence;

            Assert.That(threatened, Is.GreaterThan(quiet));
            Assert.That(threatened, Is.GreaterThan(0.0));
        }

        [Test]
        public void NeuralPurge_DoesNotPopABubble_UnderThreat()
        {
            BringTheEnemyClose();
            BubbleBouncer();

            Assert.That(Score(_javi, Javi.NeuralPurge, _bouncer).Defence, Is.LessThanOrEqualTo(0.0));
        }

        [Test]
        public void NeuralPurge_StillWashesOutAPlainStun()
        {
            BringTheEnemyClose();
            _match.Statuses.Apply(_bouncer, StatusKind.Stun, 1);

            Assert.That(Score(_javi, Javi.NeuralPurge, _bouncer).Defence, Is.GreaterThan(0.0));
        }

        [Test]
        public void ErisExploit_CountsTheCrowdItCanDraw()
        {
            // 2026-09-24: two enemies three and four cells from the zone are
            // outside its radius of 2, but inside the draw's reach of 4, which
            // brings both in. Before the draw they would have been worth nothing.
            Place(_foes[0], 13 + 3);
            Place(_foes[1], 13 + 4);

            Assert.That(Score(_lethe, Lethe.ErisExploit, null, Track(13)).Offence, Is.GreaterThan(0.0));
        }

        [Test]
        public void ErisExploit_IsWorthNothingWhenOnlyOneEnemyCanBeDrawn()
        {
            // The drag alone is not valued: it sent the bots casting at lone
            // pieces (2026-09-24).
            Place(_foes[0], 13 + 3);

            Assert.That(Score(_lethe, Lethe.ErisExploit, null, Track(13)).Offence, Is.EqualTo(0.0));
        }

        [Test]
        public void ErisExploit_IsWorthNothingOnALoneEnemy_AndMoreOnACrowd()
        {
            Place(_foes[0], 13);
            double lone = Score(_lethe, Lethe.ErisExploit, null, Track(13)).Offence;

            Place(_foes[1], 13);
            Place(_foes[2], 15);
            double crowd = Score(_lethe, Lethe.ErisExploit, null, Track(13)).Offence;

            Assert.That(lone, Is.EqualTo(0.0));
            Assert.That(crowd, Is.GreaterThan(0.0));
        }

        [Test]
        public void DraftPicker_DoesNotCountAFriendlyStunAsControl()
        {
            // Lethe's only stun is Nano Cell's, on her own side. Re-labelled as
            // hostile, the same kit would score more control.
            var hostile = new AbilityDefinition(
                id: 99001, name: "Hostile Cell", description: "Test double.",
                energyCost: 4, cooldownTurns: 4, range: 4,
                effects: new[]
                {
                    Lethe.NanoCell.Effects[0],
                    AbilityEffect.Status_(EffectScope.PrimaryTarget, StatusKind.Stun, 2)
                });
            var twin = new OperatorDefinition("Twin", Lethe.MaxHealth, Lethe.BaseSpeed,
                new[] { hostile, Lethe.ErisExploit }, Lethe.Catalyst, StatusKind.Hastened);

            var weights = BotWeights.For(BotPersonality.Brawler);

            Assert.That(DraftPicker.Value(twin, weights) - DraftPicker.Value(Lethe.Definition, weights),
                Is.EqualTo(weights.DraftControl).Within(1e-9));
        }
    }
}