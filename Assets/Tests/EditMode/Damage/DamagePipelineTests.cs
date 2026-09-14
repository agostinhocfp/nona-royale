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
    /// <remarks>
    /// <b>The shield is a pool here, not a flag.</b> It behaves like the real
    /// <c>StatusRegistry.AbsorbFrom</c> — takes what it can, keeps the rest —
    /// because the interesting cases are now partial absorbs, and a stub that
    /// only knew "on" or "off" could not express one.
    /// </remarks>
    internal sealed class FakeMitigation : IDamageMitigation
    {
        public bool EvadeNext;
        public int ShieldPool;
        public int EvadeCalls;
        public int AbsorbCalls;

        public bool TryEvade(OperatorState target, IRandom random)
        {
            EvadeCalls++;
            if (!EvadeNext) return false;
            EvadeNext = false;          // a charge, spent on use
            return true;
        }

        public int AbsorbFrom(OperatorState target, int amount)
        {
            AbsorbCalls++;
            if (ShieldPool <= 0 || amount <= 0) return 0;

            int absorbed = Math.Min(ShieldPool, amount);
            ShieldPool -= absorbed;
            return absorbed;
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

        // ── Unmitigated ──────────────────────────────────────────────────

        [Test]
        public void NormalDamage_ReducesHealth()
        {
            var target = Assassin();

            var result = _pipeline.Apply(target, Normal(2));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(result.AmountApplied, Is.EqualTo(2));
            Assert.That(result.AmountMitigated, Is.EqualTo(0));
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

        // ── Shield as a pool ─────────────────────────────────────────────

        [Test]
        public void ShieldPool_AbsorbsPartOfAnInstance_AndTheRestLands()
        {
            // The case the whole rework exists for. Under the old rule a
            // 2-point shield ate a 3-damage collision whole (§5.6).
            var target = Assassin();
            _mitigation.ShieldPool = 2;

            var result = _pipeline.Apply(target, Normal(3));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(result.AmountApplied, Is.EqualTo(1));
            Assert.That(result.AmountMitigated, Is.EqualTo(2));
            Assert.That(target.Health, Is.EqualTo(5));
            Assert.That(_mitigation.ShieldPool, Is.EqualTo(0), "the pool is spent by what it ate");
        }

        [Test]
        public void ShieldPool_CoveringTheWholeInstance_ReportsAbsorbed()
        {
            // Absorbed survives for the zero case only: "reduced to nothing"
            // and "shrugged off" are the same event to a player, and the view
            // already draws BLOCK off this outcome.
            var target = Assassin();
            _mitigation.ShieldPool = 2;

            var result = _pipeline.Apply(target, Normal(2));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Absorbed));
            Assert.That(result.AmountApplied, Is.EqualTo(0));
            Assert.That(result.AmountMitigated, Is.EqualTo(2));
            Assert.That(target.Health, Is.EqualTo(6));
        }

        [Test]
        public void ShieldPool_SurvivesOneHitAndDiesToTheNext()
        {
            var target = Assassin();
            _mitigation.ShieldPool = 2;

            var first = _pipeline.Apply(target, Normal(1));

            Assert.That(first.Outcome, Is.EqualTo(DamageOutcome.Absorbed));
            Assert.That(_mitigation.ShieldPool, Is.EqualTo(1), "a remnant, not a spent shield");

            var second = _pipeline.Apply(target, Normal(3));

            Assert.That(second.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(second.AmountApplied, Is.EqualTo(2));
            Assert.That(second.AmountMitigated, Is.EqualTo(1));
            Assert.That(target.Health, Is.EqualTo(4));
        }

        [Test]
        public void ExhaustedShieldPool_MitigatesNothing()
        {
            var target = Assassin();
            _mitigation.ShieldPool = 0;

            var result = _pipeline.Apply(target, Normal(3));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(result.AmountMitigated, Is.EqualTo(0));
            Assert.That(target.Health, Is.EqualTo(3));
        }

        [Test]
        public void ZeroDamageWithNoShield_IsDealtNotAbsorbed()
        {
            // Both branches produce "no health lost", and only one of them
            // should make the view play BLOCK.
            var target = Assassin();
            _mitigation.ShieldPool = 0;

            var result = _pipeline.Apply(target, Normal(0));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(result.AmountMitigated, Is.EqualTo(0));
        }

        [Test]
        public void MitigationExceedingTheInstance_NeverHeals()
        {
            // The pipeline clamps rather than trusting the layer. Without it a
            // mitigation bug would present as a healing bug, at the one place
            // health is written.
            var target = Assassin();
            target.SetHealth(4);
            _mitigation.ShieldPool = 10;

            var result = _pipeline.Apply(target, Normal(2));

            Assert.That(result.AmountMitigated, Is.EqualTo(2), "only what was offered");
            Assert.That(target.Health, Is.EqualTo(4));
        }

        // ── Ordering ─────────────────────────────────────────────────────

        [Test]
        public void EvasionResolvesBeforeShield_AndPreservesTheShield()
        {
            // Evasion still negates the whole instance, so resolving it first
            // means a successful dodge never burns a pool the defender paid
            // energy for. The order is fixed (COMBAT_SYSTEMS §2.1).
            var target = Assassin();
            _mitigation.EvadeNext = true;
            _mitigation.ShieldPool = 2;

            var result = _pipeline.Apply(target, Normal(3));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Evaded));
            Assert.That(result.AmountMitigated, Is.EqualTo(3), "an evade prevents the whole instance");
            Assert.That(_mitigation.AbsorbCalls, Is.EqualTo(0), "the shield must not even be consulted");
            Assert.That(_mitigation.ShieldPool, Is.EqualTo(2), "the pool survives an evaded hit intact");
        }

        // ── Atomic ───────────────────────────────────────────────────────

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
            _mitigation.ShieldPool = 2;

            var result = _pipeline.Apply(target, Atomic(3));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(result.AmountApplied, Is.EqualTo(3));
            Assert.That(result.AmountMitigated, Is.EqualTo(0));
            Assert.That(target.Health, Is.EqualTo(3));
            Assert.That(_mitigation.AbsorbCalls, Is.EqualTo(0));
            Assert.That(_mitigation.ShieldPool, Is.EqualTo(2), "Atomic goes around the pool, it does not eat it");
        }

        // ── Self-damage ──────────────────────────────────────────────────

        [Test]
        public void SelfDamage_BypassesEvasionAndShield()
        {
            // Bouncer's All-In Mauling. Straight to health (§2.3).
            var bouncer = new OperatorState(1, "Bouncer", PlayerColor.Red, 12, 1.5);
            _mitigation.EvadeNext = true;
            _mitigation.ShieldPool = 2;

            var result = _pipeline.ApplyToSelf(bouncer, 3);

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(bouncer.Health, Is.EqualTo(9));
            Assert.That(result.AmountMitigated, Is.EqualTo(0));
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