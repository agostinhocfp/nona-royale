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
    /// What the bots had to learn for Revú (2026-09-17; debt rework
    /// 2026-09-24): Equilibrium rescales what a cast is worth against him, a
    /// loan is worth what the debt cap still has room for, Sadist is worth the
    /// debt it calls in, a seat in debt pays for casting into its own bill, and
    /// landing on the creditor is worth the debt it burns.
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

        private static readonly EnergyLedger Ledger = new EnergyLedger(Config.EnergyConfig.Default);

        private static void SetPool(PlayerState player, int amount)
        {
            Ledger.Spend(player, player.Energy);
            if (amount > 0) Ledger.GrantBounty(player, amount);
        }

        private static void SetDebt(PlayerState player, int amount, OperatorState creditor)
        {
            Ledger.WriteOffDebt(player);
            if (amount > 0) Ledger.IncurDebt(player, amount, creditor.Id);
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
        public void LeechRound_IsWorthTheDebtTheCapHasRoomFor()
        {
            SetDebt(_blue, 0, _revu);
            double clear = Score(_revu, Revu.LeechRound, _enemyRevu).Offence;

            SetDebt(_blue, 5, _revu);
            double nearCap = Score(_revu, Revu.LeechRound, _enemyRevu).Offence;

            SetDebt(_blue, 6, _revu);
            double atCap = Score(_revu, Revu.LeechRound, _enemyRevu).Offence;

            double point = Weights.EnergyDenial * Weights.Offence;
            Assert.That(clear - nearCap, Is.EqualTo(point).Within(1e-9), "room for 1 of the 2");
            Assert.That(nearCap - atCap, Is.EqualTo(point).Within(1e-9), "room for none");
        }

        [Test]
        public void Sadist_IsWorthMore_AgainstADeeperDebt()
        {
            SetDebt(_blue, 0, _revu);
            double clear = Score(_revu, Revu.Sadist, _enemyRevu).Offence;

            SetDebt(_blue, 6, _revu);
            double deep = Score(_revu, Revu.Sadist, _enemyRevu).Offence;

            Assert.That(clear, Is.GreaterThan(0.0), "the floor is worth something");
            Assert.That(deep, Is.GreaterThan(clear), "a seat at the cap is the play");
        }

        [Test]
        public void DebtShortfall_IsWhatTheCastLeavesUnpaid()
        {
            SetPool(_red, 6);

            SetDebt(_red, 0, _enemyRevu);
            Assert.That(CastPlanner.DebtShortfall(_red, 3), Is.EqualTo(0), "no debt");

            SetDebt(_red, 3, _enemyRevu);
            Assert.That(CastPlanner.DebtShortfall(_red, 3), Is.EqualTo(0), "6 pays the cast and the bill");

            SetDebt(_red, 5, _enemyRevu);
            Assert.That(CastPlanner.DebtShortfall(_red, 3), Is.EqualTo(2), "3 left against 5 owed");

            SetPool(_red, 2);
            Assert.That(CastPlanner.DebtShortfall(_red, 2), Is.EqualTo(2), "already short 3; now 5");
        }

        [Test]
        public void ASeatInDebt_PaysForCastingIntoItsOwnBill()
        {
            SetPool(_red, 6);

            SetDebt(_red, 0, _enemyRevu);
            double square = Score(_revu, Revu.LeechRound, _enemyRevu).Score;

            SetDebt(_red, 5, _enemyRevu);
            double owing = Score(_revu, Revu.LeechRound, _enemyRevu).Score;

            Assert.That(square - owing, Is.EqualTo(2 * Weights.DebtShortfall).Within(1e-9));
        }

        [Test]
        public void LandingOnTheCreditor_IsWorthTheDebtItBurns()
        {
            // Red's Syla one cell behind Blue's Revú, and Red owes him 3.
            var board = new BotBoard(_match);
            int to = _syla.Progress + 1;

            SetDebt(_red, 0, _enemyRevu);
            double square = MoveScorer.ScoreLanding(board, Weights, _syla, to, 1, 0);

            SetDebt(_red, 3, _enemyRevu);
            double owing = MoveScorer.ScoreLanding(board, Weights, _syla, to, 1, 0);

            Assert.That(owing - square, Is.EqualTo(3 * Weights.DebtBurn).Within(1e-9));
        }

        [Test]
        public void LandingOnSomeoneElsesCreditor_BurnsNothing()
        {
            // Red owes a Blue operator, but not the one standing there.
            OperatorState kian = null;
            foreach (var op in _match.Operators)
                if (op.Owner == PlayerColor.Blue && op.Name == "Kian") kian = op;

            var board = new BotBoard(_match);
            int to = _syla.Progress + 1;

            SetDebt(_red, 0, kian);
            double square = MoveScorer.ScoreLanding(board, Weights, _syla, to, 1, 0);

            SetDebt(_red, 3, kian);
            double owing = MoveScorer.ScoreLanding(board, Weights, _syla, to, 1, 0);

            Assert.That(owing, Is.EqualTo(square).Within(1e-9));
        }
    }
}