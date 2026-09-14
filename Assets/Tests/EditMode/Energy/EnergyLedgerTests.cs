// Assets/Tests/EditMode/Energy/EnergyLedgerTests.cs
using System;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Energy
{
    [TestFixture]
    public class EnergyLedgerTests
    {
        private EnergyConfig _config;
        private EnergyLedger _ledger;
        private PlayerState _player;

        [SetUp]
        public void SetUp()
        {
            _config = EnergyConfig.Default;
            _ledger = new EnergyLedger(_config);
            _player = Squad(PlayerColor.Red);
            _player.BeginTurn();
        }

        private static PlayerState Squad(PlayerColor color) =>
            new PlayerState(color, new[]
            {
                new OperatorState(1, "Bouncer", color, 12, 1.5),
                new OperatorState(2, "Syla", color, 6, 2.0),
                new OperatorState(3, "Kurbyn", color, 6, 1.5)
            });

        // ── Generation ───────────────────────────────────────────────────

        [Test]
        public void DiceTotalOfNine_GrantsFourEnergy()
        {
            // floor(9 / 2). Integer division, so an odd total loses the half.
            var grant = _ledger.GrantForTurn(_player, new DiceRoll(5, 4));

            Assert.That(grant.Earned, Is.EqualTo(4));
            Assert.That(_player.Energy, Is.EqualTo(4));
        }

        [Test]
        public void DiceTotalOfTwo_GrantsOneEnergy()
        {
            // The floor of the range. Even the worst roll pays something.
            var grant = _ledger.GrantForTurn(_player, new DiceRoll(1, 1));

            Assert.That(grant.Earned, Is.EqualTo(1));
        }

        [Test]
        public void DiceTotalOfTwelve_GrantsSixEnergy()
        {
            var grant = _ledger.GrantForTurn(_player, new DiceRoll(6, 6));

            Assert.That(grant.Earned, Is.EqualTo(6));
        }

        [Test]
        public void EveryPossibleTotal_GrantsBetweenOneAndSix()
        {
            for (int first = 1; first <= 6; first++)
            {
                for (int second = 1; second <= 6; second++)
                {
                    var player = Squad(PlayerColor.Blue);
                    player.BeginTurn();

                    var grant = _ledger.GrantForTurn(player, new DiceRoll(first, second));

                    Assert.That(grant.Earned, Is.AtLeast(1), $"[{first},{second}]");
                    Assert.That(grant.Earned, Is.LessThanOrEqualTo(6), $"[{first},{second}]");
                }
            }
        }

        // ── The cap ──────────────────────────────────────────────────────

        [Test]
        public void EnergyAboveTwelve_IsBurnedNotStored()
        {
            // Fill to 10, then earn 6. Two reach the pool, four are destroyed.
            _ledger.GrantForTurn(_player, new DiceRoll(5, 5));    // +5
            _player.BeginTurn();
            _ledger.GrantForTurn(_player, new DiceRoll(5, 5));    // +5, pool 10
            _player.BeginTurn();

            var grant = _ledger.GrantForTurn(_player, new DiceRoll(6, 6)); // earns 6

            Assert.That(grant.Earned, Is.EqualTo(6));
            Assert.That(grant.Stored, Is.EqualTo(2));
            Assert.That(grant.Burned, Is.EqualTo(4));
            Assert.That(_player.Energy, Is.EqualTo(_config.EnergyCap));
        }

        [Test]
        public void APoolAtTheCap_BurnsEverythingItEarns()
        {
            while (_player.Energy < _config.EnergyCap)
            {
                _ledger.GrantForTurn(_player, new DiceRoll(6, 6));
                _player.BeginTurn();
            }

            var grant = _ledger.GrantForTurn(_player, new DiceRoll(6, 6));

            Assert.That(grant.Stored, Is.EqualTo(0));
            Assert.That(grant.Burned, Is.EqualTo(6));
        }

        [Test]
        public void TheCapIsExactlyOneUltimate()
        {
            // Hoarding for a 9-cost ult is a visible commitment: you can hold
            // one and nothing else (COMBAT_SYSTEMS §3.1).
            Assert.That(_config.EnergyCap, Is.AtLeast(9));
            Assert.That(_config.EnergyCap, Is.LessThanOrEqualTo(12));
        }

        // ── Once per turn ────────────────────────────────────────────────

        [Test]
        public void DoublesReroll_GrantsNoAdditionalEnergy()
        {
            // Doubles buy an extra movement roll, never a second grant —
            // otherwise one double snowballs both axes at once.
            var first = _ledger.GrantForTurn(_player, new DiceRoll(4, 4));
            var second = _ledger.GrantForTurn(_player, new DiceRoll(6, 6));

            Assert.That(first.WasGranted, Is.True);
            Assert.That(second.WasGranted, Is.False);
            Assert.That(_player.Energy, Is.EqualTo(4), "only the first roll paid");
        }

        [Test]
        public void ANewTurn_ReArmsTheGrant()
        {
            _ledger.GrantForTurn(_player, new DiceRoll(4, 4));
            _player.BeginTurn();

            var grant = _ledger.GrantForTurn(_player, new DiceRoll(4, 4));

            Assert.That(grant.WasGranted, Is.True);
            Assert.That(_player.Energy, Is.EqualTo(8));
        }

        // ── Spending ─────────────────────────────────────────────────────

        [Test]
        public void EnergySpentByOneOperator_ReducesTheSharedPool()
        {
            // The pool is player-level. Syla spending leaves Bouncer poorer —
            // that trade is the whole point of a shared pool (§3).
            _ledger.GrantForTurn(_player, new DiceRoll(6, 6));   // 6

            var result = _ledger.Spend(_player, 3);

            Assert.That(result.Approved, Is.True);
            Assert.That(_player.Energy, Is.EqualTo(3));
            Assert.That(_ledger.CanAfford(_player, 6), Is.False, "the pool is shared, so it is now short");
        }

        [Test]
        public void AbilityCostExceedingPool_IsRejected()
        {
            _ledger.GrantForTurn(_player, new DiceRoll(2, 2));   // 2

            var result = _ledger.Spend(_player, 9);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.Shortfall, Is.EqualTo(7));
            Assert.That(_player.Energy, Is.EqualTo(2), "a refused spend costs nothing");
        }

        [Test]
        public void SpendingExactlyThePool_IsAllowed()
        {
            _ledger.GrantForTurn(_player, new DiceRoll(6, 6));   // 6

            var result = _ledger.Spend(_player, 6);

            Assert.That(result.Approved, Is.True);
            Assert.That(_player.Energy, Is.EqualTo(0));
        }

        [Test]
        public void TwoAbilitiesInOneTurn_AreAllowedIfThePoolCovers()
        {
            // No cap on abilities per turn. Banking to 12 and firing Velvet Rope
            // into All-In Mauling is a combo worth having (§3.2).
            for (int i = 0; i < 2; i++)
            {
                _ledger.GrantForTurn(_player, new DiceRoll(6, 6));
                _player.BeginTurn();
            }

            Assert.That(_ledger.Spend(_player, 6).Approved, Is.True);
            Assert.That(_ledger.Spend(_player, 6).Approved, Is.True);
            Assert.That(_player.Energy, Is.EqualTo(0));
        }

        [Test]
        public void AFreeAbility_CostsNothingAndIsApproved()
        {
            // Passives are free (§3.2).
            var result = _ledger.Spend(_player, 0);

            Assert.That(result.Approved, Is.True);
            Assert.That(_player.Energy, Is.EqualTo(0));
        }

        [Test]
        public void ANegativeCost_IsRejected()
        {
            // Abilities cannot refund energy.
            Assert.Throws<ArgumentOutOfRangeException>(() => _ledger.Spend(_player, -3));
        }

        // ── Player state ─────────────────────────────────────────────────

        [Test]
        public void APlayerCannotFieldAnotherSeatsOperator()
        {
            Assert.Throws<ArgumentException>(() => new PlayerState(PlayerColor.Red, new[]
            {
                new OperatorState(1, "Bouncer", PlayerColor.Blue, 12, 1.5)
            }));
        }

        [Test]
        public void APlayerFieldsAtLeastOneOperator()
        {
            Assert.Throws<ArgumentException>(
                () => new PlayerState(PlayerColor.Red, new OperatorState[0]));
        }
    }
}