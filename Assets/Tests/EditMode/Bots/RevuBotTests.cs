// Assets/Tests/EditMode/Bots/RevuBotTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Bots
{
    /// <summary>
    /// What the bots had to learn for Revú (2026-09-17): Equilibrium rescales
    /// what a cast is worth against him, a drain is worth the energy it can
    /// actually take, and Sadist is worth what the target's pool is missing.
    /// </summary>
    [TestFixture]
    public class RevuBotTests
    {
        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };

        private MatchFactory.Match _match;
        private PlayerState _red;
        private PlayerState _blue;
        private OperatorState _revu;
        private OperatorState _syla;
        private OperatorState _enemyRevu;

        [SetUp]
        public void SetUp()
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                { PlayerColor.Red, new[] { Revu.Definition, Syla.Definition, Bouncer.Definition } },
                { PlayerColor.Blue, new[] { Revu.Definition, Kian.Definition, Mimi.Definition } }
            };

            _match = MatchFactory.Create(Two, 23, squads);
            _match.Engine.Start();
            _red = _match.Players[0];
            _blue = _match.Players[1];

            foreach (var op in _match.Operators)
            {
                if (op.Owner == PlayerColor.Red && op.Name == "Revú") _revu = op;
                if (op.Owner == PlayerColor.Red && op.Name == "Syla") _syla = op;
                if (op.Owner == PlayerColor.Blue && op.Name == "Revú") _enemyRevu = op;
            }

            Place(_revu, 10);
            Place(_syla, 11);
            Place(_enemyRevu, 12);
        }

        private void Place(OperatorState op, int track)
        {
            int circuit = _match.Map.Profile.CircuitLength;
            op.MoveTo((track - _match.Map.StartTrackIndex(op.Owner) + circuit) % circuit);
        }

        private void SetPool(PlayerState player, int amount)
        {
            var ledger = new EnergyLedger(Config.EnergyConfig.Default);
            ledger.Drain(player, player.Energy);
            if (amount > 0) ledger.GrantBounty(player, amount);
        }

        private static BotWeights Weights => BotWeights.For(BotPersonality.Brawler);

        private ScoredCast Score(OperatorState caster, AbilityDefinition ability, OperatorState target) =>
            CastPlanner.Score(new BotBoard(_match), Weights, _red, caster, ability, target, null, null);

        [Test]
        public void ExpectedHit_ReadsEquilibrium()
        {
            var board = new BotBoard(_match);

            Assert.That(board.ExpectedHit(_enemyRevu, 1, DamageType.Normal, castCost: 3), Is.EqualTo(2.0));
            Assert.That(board.ExpectedHit(_enemyRevu, 3, DamageType.Atomic, castCost: 6), Is.EqualTo(1.0));
            Assert.That(board.ExpectedHit(_enemyRevu, 3, DamageType.Normal), Is.EqualTo(3.0), "not a cast");
        }

        [Test]
        public void ACheapCast_IsWorthMore_AgainstRevu()
        {
            // From the Hip on a Revú deals 2 instead of 1.
            OperatorState kian = null;
            foreach (var op in _match.Operators)
                if (op.Owner == PlayerColor.Blue && op.Name == "Kian") kian = op;
            Place(kian, 12);

            double onRevu = Score(_syla, Syla.FromTheHip, _enemyRevu).Offence;
            double onKian = Score(_syla, Syla.FromTheHip, kian).Offence;

            Assert.That(onRevu, Is.GreaterThan(onKian), "same health, same cell, double the hit");
        }

        [Test]
        public void LeechRound_IsWorthTheEnergyItCanTake()
        {
            SetPool(_blue, 6);
            double full = Score(_revu, Revu.LeechRound, _enemyRevu).Offence;

            SetPool(_blue, 0);
            double dry = Score(_revu, Revu.LeechRound, _enemyRevu).Offence;

            Assert.That(full - dry, Is.EqualTo(2 * Weights.EnergyDenial * Weights.Offence).Within(1e-9));
        }

        [Test]
        public void Sadist_IsWorthLess_AgainstAFullPool()
        {
            // It was worth nothing until the floor landed (2026-09-21); now the
            // bot still reads a full pool as the weakest case, not a blank.
            SetPool(_blue, 12);
            double full = Score(_revu, Revu.Sadist, _enemyRevu).Offence;

            SetPool(_blue, 0);
            double dry = Score(_revu, Revu.Sadist, _enemyRevu).Offence;

            Assert.That(full, Is.GreaterThan(0.0), "the floor is worth something");
            Assert.That(dry, Is.GreaterThan(full), "an empty pool is still the play");
        }
    }
}