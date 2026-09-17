// Assets/Tests/EditMode/Bots/WatchBotTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Model;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Bots
{
    /// <summary>
    /// The planner's Watch branch (2026-09-17): before it, no bot ever cast
    /// Kurbyn's Predator's Read (0.00 casts a match), so every Kurbyn balance
    /// number was measured against two thirds of his kit.
    /// </summary>
    [TestFixture]
    public class WatchBotTests
    {
        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };
        private static readonly BotWeights Weights = BotWeights.For(BotPersonality.Brawler);

        private MatchFactory.Match _match;
        private OperatorState _kurbyn;
        private OperatorState _ally;
        private OperatorState[] _foes;

        [SetUp]
        public void SetUp()
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                { PlayerColor.Red, new[] { Kurbyn.Definition, Bouncer.Definition, Javi.Definition } },
                { PlayerColor.Blue, new[] { Syla.Definition, Kian.Definition, Nuetu.Definition } }
            };

            _match = MatchFactory.Create(Two, 31, squads);
            _match.Engine.Start();

            var foes = new List<OperatorState>();
            foreach (var op in _match.Operators)
            {
                if (op.Name == "Kurbyn") _kurbyn = op;
                else if (op.Owner == PlayerColor.Red && _ally == null) _ally = op;
                else if (op.Owner == PlayerColor.Blue) foes.Add(op);
            }
            _foes = foes.ToArray();

            // Every piece starts in its yard; the tests put on the loop what they need.
            foreach (var op in _match.Operators) op.MoveTo(PathMap.YardProgress);
            Place(_kurbyn, 10);
            Place(_ally, 5);
        }

        private void Place(OperatorState op, int track)
        {
            int circuit = _match.Map.Profile.CircuitLength;
            op.MoveTo((track - _match.Map.StartTrackIndex(op.Owner) + circuit) % circuit);
        }

        private double ReadOffence(OperatorState target) =>
            CastPlanner.Score(new BotBoard(_match), Weights, _match.Players[0],
                _kurbyn, Kurbyn.PredatorsRead, target, null, null).Offence;

        private double Strike(OperatorState target)
        {
            var board = new BotBoard(_match);
            double hit = board.ExpectedHit(target, Kurbyn.PredatorsRead.Effects[0].Amount,
                Kurbyn.PredatorsRead.Effects[0].DamageType);
            return hit >= target.Health
                ? target.Health * Weights.Damage + Weights.Kill + System.Math.Max(0, target.Progress) * Weights.KillProgress
                : hit * Weights.Damage;
        }

        [Test]
        public void PredatorsRead_IsStillOneWatch()
        {
            Assert.That(Kurbyn.PredatorsRead.Effects.Count, Is.EqualTo(1));
            Assert.That(Kurbyn.PredatorsRead.Effects[0].Kind, Is.EqualTo(EffectKind.Watch));
        }

        [Test]
        public void ALonePiece_MustMove_SoTheWholeStrikeCounts()
        {
            Place(_foes[0], 12);

            Assert.That(ReadOffence(_foes[0]), Is.EqualTo(Strike(_foes[0]) * Weights.Offence).Within(1e-9));
        }

        [Test]
        public void WithSquadmatesToMove_TheStrikeIsAChance_AndTheRestIsDenial()
        {
            Place(_foes[0], 12);
            Place(_foes[1], 30);
            Place(_foes[2], 40);

            double expected = Weights.WatchMoveOdds * Strike(_foes[0])
                              + (1.0 - Weights.WatchMoveOdds) * Weights.WatchDenial;

            Assert.That(ReadOffence(_foes[0]), Is.EqualTo(expected * Weights.Offence).Within(1e-9));
        }

        [Test]
        public void StunnedOrNeutralizedSquadmates_AreNotAlternatives()
        {
            Place(_foes[0], 12);
            Place(_foes[1], 30);
            _match.Statuses.Apply(_foes[1], StatusKind.Stun, 1);

            Assert.That(ReadOffence(_foes[0]), Is.EqualTo(Strike(_foes[0]) * Weights.Offence).Within(1e-9));
        }

        [Test]
        public void AWoundedLonePiece_IsWorthAKill()
        {
            Place(_foes[0], 12);
            _foes[0].SetHealth(1);

            Assert.That(ReadOffence(_foes[0]), Is.GreaterThan(Weights.Kill * Weights.Offence));
        }

        [Test]
        public void AnAlreadyWatchedPiece_IsWorthNothing()
        {
            Place(_foes[0], 12);
            _match.Statuses.Apply(_foes[0], StatusKind.Watched, 1);

            Assert.That(ReadOffence(_foes[0]), Is.EqualTo(0.0));
        }

        [Test]
        public void AStunnedPiece_IsWorthNothing()
        {
            Place(_foes[0], 12);
            _match.Statuses.Apply(_foes[0], StatusKind.Stun, 1);

            Assert.That(ReadOffence(_foes[0]), Is.EqualTo(0.0));
        }

        [Test]
        public void APieceOffTheLoop_IsWorthNothing()
        {
            Assert.That(ReadOffence(_foes[0]), Is.EqualTo(0.0), "in its yard");
        }

        [Test]
        public void AnAlly_IsWorthNothing()
        {
            Assert.That(ReadOffence(_ally), Is.EqualTo(0.0));
        }
    }
}