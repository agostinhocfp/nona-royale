// Assets/Tests/EditMode/Damage/DamagePipelineTests.cs
using System;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Damage
{
    /// <summary>Mitigation stub with switches, so tests state intent instead of hunting for seeds.</summary>
    internal sealed class FakeMitigation : IDamageMitigation
    {
        public bool EvadeNext;
        public bool ShieldNext;
        public int EvadeCalls;
        public int AbsorbCalls;

        public bool TryEvade(OperatorState target, IRandom random)
        {
            EvadeCalls++;
            if (!EvadeNext) return false;
            EvadeNext = false;          // a charge, spent on use
            return true;
        }

        public bool TryAbsorb(OperatorState target)
        {
            AbsorbCalls++;
            if (!ShieldNext) return false;
            ShieldNext = false;         // a consumable, spent on use
            return true;
        }
    }

    [TestFixture]
    public class DamagePipelineTests
    {
        private FakeMitigation _mitigation;
        private DamagePipeline _pipeline;

        [SetUp]
        public void SetUp()
        {
            _mitigation = new FakeMitigation();
            _pipeline = new DamagePipeline(_mitigation, new SeededRandom(1));
        }

        private static OperatorState Assassin() =>
            new OperatorState(2, "Syla", PlayerColor.Red, 6, 2.0);

        private static DamageInstance Normal(int amount) =>
            new DamageInstance(amount, DamageType.Normal, 99, "test");

        private static DamageInstance Atomic(int amount) =>
            new DamageInstance(amount, DamageType.Atomic, 99, "test");

        [Test]
        public void NormalDamage_ReducesHealth()
        {
            var target = Assassin();

            var result = _pipeline.Apply(target, Normal(2));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(result.AmountApplied, Is.EqualTo(2));
            Assert.That(target.Health, Is.EqualTo(4));
        }

        [Test]
        public void DamageReachingZero_Neutralizes()
        {
            var target = Assassin();

            var result = _pipeline.Apply(target, Normal(6));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Neutralized));
            Assert.That(result.TargetSurvived, Is.False);
        }

        [Test]
        public void Overkill_ReportsOnlyTheHealthActuallyLost()
        {
            var target = Assassin();

            var result = _pipeline.Apply(target, Normal(50));

            Assert.That(result.AmountApplied, Is.EqualTo(6));
            Assert.That(result.RemainingHealth, Is.EqualTo(0));
        }

        [Test]
        public void NormalDamage_IsFullyAbsorbedByShieldThenShieldExpires()
        {
            var target = Assassin();
            _mitigation.ShieldNext = true;

            var first = _pipeline.Apply(target, Normal(3));

            Assert.That(first.Outcome, Is.EqualTo(DamageOutcome.Absorbed));
            Assert.That(target.Health, Is.EqualTo(6), "an absorbed instance costs no health");

            var second = _pipeline.Apply(target, Normal(3));

            Assert.That(second.Outcome, Is.EqualTo(DamageOutcome.Dealt), "the shield is spent");
            Assert.That(target.Health, Is.EqualTo(3));
        }

        [Test]
        public void EvasionResolvesBeforeShield_AndPreservesTheShield()
        {
            // Evasion is a reflex; a shield is a consumable the operator may
            // still need. Order matters and is fixed (COMBAT_SYSTEMS §2.1).
            var target = Assassin();
            _mitigation.EvadeNext = true;
            _mitigation.ShieldNext = true;

            var result = _pipeline.Apply(target, Normal(3));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Evaded));
            Assert.That(_mitigation.AbsorbCalls, Is.EqualTo(0), "the shield must not even be consulted");
            Assert.That(_mitigation.ShieldNext, Is.True, "the shield survives an evaded hit");
        }

        [Test]
        public void AtomicDamage_IgnoresEvasion()
        {
            var target = Assassin();
            _mitigation.EvadeNext = true;

            var result = _pipeline.Apply(target, Atomic(3));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(target.Health, Is.EqualTo(3));
            Assert.That(_mitigation.EvadeCalls, Is.EqualTo(0));
        }

        [Test]
        public void AtomicDamage_IgnoresShield()
        {
            var target = Assassin();
            _mitigation.ShieldNext = true;

            var result = _pipeline.Apply(target, Atomic(3));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(target.Health, Is.EqualTo(3));
            Assert.That(_mitigation.AbsorbCalls, Is.EqualTo(0));
            Assert.That(_mitigation.ShieldNext, Is.True, "Atomic goes around the shield, it does not eat it");
        }

        [Test]
        public void SelfDamage_BypassesEvasionAndShield()
        {
            // Bouncer's All-In Mauling. Straight to health (§2.3).
            var bouncer = new OperatorState(1, "Bouncer", PlayerColor.Red, 12, 1.5);
            _mitigation.EvadeNext = true;
            _mitigation.ShieldNext = true;

            var result = _pipeline.ApplyToSelf(bouncer, 3);

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(bouncer.Health, Is.EqualTo(9));
            Assert.That(_mitigation.EvadeCalls, Is.EqualTo(0));
            Assert.That(_mitigation.AbsorbCalls, Is.EqualTo(0));
        }

        [Test]
        public void SelfDamage_CanNeutralizeItsOwnCaster()
        {
            var bouncer = new OperatorState(1, "Bouncer", PlayerColor.Red, 12, 1.5);
            bouncer.SetHealth(3);

            var result = _pipeline.ApplyToSelf(bouncer, 3);

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Neutralized));
        }

        [Test]
        public void NegativeDamage_IsRejected()
        {
            // Healing is its own effect, not damage with a sign flip.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DamageInstance(-1, DamageType.Normal, 1, "test"));
        }
    }
}