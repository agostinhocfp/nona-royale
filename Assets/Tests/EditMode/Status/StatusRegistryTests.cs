// Assets/Tests/EditMode/Status/StatusRegistryTests.cs
using System;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Status
{
    /// <summary>
    /// A clock driven by hand, so a test can say "now it is Blue's turn" without
    /// a turn state machine existing.
    /// </summary>
    internal sealed class FakeClock : ITurnClock
    {
        private readonly int[] _turns = new int[4];

        public PlayerColor ActivePlayer { get; private set; } = PlayerColor.Red;

        public int TurnIndexOf(PlayerColor color) => _turns[(int)color];

        /// <summary>Hands the turn to a seat and advances that seat's counter.</summary>
        public void BeginTurnFor(PlayerColor color)
        {
            ActivePlayer = color;
            _turns[(int)color]++;
        }
    }

    /// <summary>An IRandom that answers exactly what a test asks for.</summary>
    internal sealed class ScriptedRandom : IRandom
    {
        private readonly double _next;
        public ScriptedRandom(double next) { _next = next; }
        public int NextInt(int min, int max) => min;
        public double NextDouble() => _next;
    }

    [TestFixture]
    public class StatusRegistryTests
    {
        private FakeClock _clock;
        private CombatConfig _config;
        private StatusRegistry _statuses;

        private OperatorState _red;
        private OperatorState _blue;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _config = CombatConfig.Default;
            _statuses = new StatusRegistry(_clock, _config);

            _red = new OperatorState(1, "Bouncer", PlayerColor.Red, 12, 1.5);
            _blue = new OperatorState(2, "Kurbyn", PlayerColor.Blue, 6, 1.5);

            _clock.BeginTurnFor(PlayerColor.Red);
        }

        // ── The boundary case the whole timer model exists for ───────────

        [Test]
        public void StunAppliedOnOpponentTurn_BlocksTargetsNextTurn()
        {
            // Red stuns Blue during Red's turn. If the duration were a counter
            // that ticked down each turn, it would expire before Blue ever
            // acted. Absolute indices make the stun land where it should.
            _statuses.Apply(_blue, StatusKind.Stun, duration: 1);

            Assert.That(_statuses.IsStunned(_blue), Is.False, "not yet — it is still Red's turn");

            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.IsStunned(_blue), Is.True, "Blue's action phase is blocked");
        }

        [Test]
        public void AOneTurnStun_ExpiresAfterThatOneTurn()
        {
            _statuses.Apply(_blue, StatusKind.Stun, duration: 1);

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.IsStunned(_blue), Is.True);

            _clock.BeginTurnFor(PlayerColor.Red);      // opponents act
            _clock.BeginTurnFor(PlayerColor.Blue);     // Blue's next turn

            Assert.That(_statuses.IsStunned(_blue), Is.False);
        }

        [Test]
        public void ATwoTurnStun_BlocksTwoOfTheTargetsTurns()
        {
            _statuses.Apply(_blue, StatusKind.Stun, duration: 2);

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.IsStunned(_blue), Is.True, "first blocked turn");

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.IsStunned(_blue), Is.True, "second blocked turn");

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.IsStunned(_blue), Is.False);
        }

        [Test]
        public void OpponentTurnsPassing_DoNotBurnTheTargetsDuration()
        {
            // Durations are counted in the target's own turns, not in rounds.
            // Three opponent turns must not expire a 1-turn stun.
            _statuses.Apply(_blue, StatusKind.Stun, duration: 1);

            _clock.BeginTurnFor(PlayerColor.Green);
            _clock.BeginTurnFor(PlayerColor.Violet);
            _clock.BeginTurnFor(PlayerColor.Red);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.IsStunned(_blue), Is.True);
        }

        [Test]
        public void ASelfBuffAppliedOnItsOwnTurn_TakesHoldImmediately()
        {
            // Syla's Stealth is "the current turn + 1", which is duration 2
            // applied during her own turn.
            _statuses.Apply(_red, StatusKind.Stealth, duration: 2);

            Assert.That(_statuses.Has(_red, StatusKind.Stealth), Is.True, "active on the turn it was cast");

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_statuses.Has(_red, StatusKind.Stealth), Is.True, "and the turn after");

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_statuses.Has(_red, StatusKind.Stealth), Is.False);
        }

        // ── Stun scope ───────────────────────────────────────────────────

        [Test]
        public void StunnedOperator_RetainsPassiveEffects()
        {
            // A passive is who an operator is, not what it does (§5.1).
            _statuses.ApplyPassive(_blue, StatusKind.Evasion);
            _statuses.Apply(_blue, StatusKind.Stun, duration: 1);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.IsStunned(_blue), Is.True);
            Assert.That(_statuses.Has(_blue, StatusKind.Evasion), Is.True);
        }

        [Test]
        public void APassive_NeverExpires()
        {
            _statuses.ApplyPassive(_blue, StatusKind.Evasion);

            for (int i = 0; i < 20; i++) _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.Has(_blue, StatusKind.Evasion), Is.True);
        }

        // ── Slow ─────────────────────────────────────────────────────────

        [Test]
        public void Slow_ReducesSpeedByTheConfiguredPenalty()
        {
            _statuses.Apply(_blue, StatusKind.Slow, duration: 1);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.SpeedModifier(_blue), Is.EqualTo(-0.5));
        }

        [Test]
        public void SlowFromMultipleSources_DoesNotStack()
        {
            // The largest applies. Two slows are not a stun (§5.2).
            _statuses.Apply(_blue, StatusKind.Slow, duration: 1);
            _statuses.Apply(_blue, StatusKind.Slow, duration: 1);
            _statuses.Apply(_blue, StatusKind.Slow, duration: 1);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.SpeedModifier(_blue), Is.EqualTo(-0.5));
        }

        [Test]
        public void ReApplication_RefreshesTheDuration()
        {
            _statuses.Apply(_blue, StatusKind.Slow, duration: 1);
            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.Has(_blue, StatusKind.Slow), Is.True);

            _clock.BeginTurnFor(PlayerColor.Red);
            _statuses.Apply(_blue, StatusKind.Slow, duration: 1);   // refreshed
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.Has(_blue, StatusKind.Slow), Is.True);
        }

        [Test]
        public void APassiveSpeedBonus_ReachesTheSpeedModifier()
        {
            // Magnitude is the single channel for speed effects. Before this,
            // SpeedModifier tested for Slow and nothing else, so a passive bonus
            // was written into the registry and never read out again.
            _statuses.ApplyPassive(_blue, StatusKind.Evasion, 0.5);

            Assert.That(_statuses.SpeedModifier(_blue), Is.EqualTo(0.5));
        }

        [Test]
        public void APassiveBonusAndASlow_ResolveAgainstEachOther()
        {
            _statuses.ApplyPassive(_blue, StatusKind.Evasion, 0.5);
            _statuses.Apply(_blue, StatusKind.Slow, duration: 1);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.SpeedModifier(_blue), Is.EqualTo(0.0));
        }

        [Test]
        public void AStrongerSlow_OverridesAWeakerOne()
        {
            // Merging used Math.Max, which on negative magnitudes kept the
            // weaker slow. The stronger effect has to win.
            _statuses.Apply(_blue, StatusKind.Slow, duration: 1);
            _statuses.Apply(_blue, StatusKind.Slow, duration: 1, magnitude: -1.5);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.SpeedModifier(_blue), Is.EqualTo(-1.5));
        }

        [Test]
        public void AWeakerSlow_DoesNotDowngradeAStrongerOne()
        {
            _statuses.Apply(_blue, StatusKind.Slow, duration: 1, magnitude: -1.5);
            _statuses.Apply(_blue, StatusKind.Slow, duration: 1);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.SpeedModifier(_blue), Is.EqualTo(-1.5));
        }

        [Test]
        public void ClearAll_StripsAppliedStatuses_ButKeepsPassives()
        {
            // An operator returning to its yard is still itself. Nothing
            // re-grants passives after a match starts, so wiping them here cost
            // Kurbyn his Evasive Protocol permanently the first time he died.
            _statuses.ApplyPassive(_blue, StatusKind.Evasion, 0.5);
            _statuses.Apply(_blue, StatusKind.Stun, duration: 5);
            _statuses.Apply(_blue, StatusKind.Slow, duration: 5);
            _clock.BeginTurnFor(PlayerColor.Blue);

            _statuses.ClearAll(_blue);

            Assert.That(_statuses.IsStunned(_blue), Is.False);
            Assert.That(_statuses.Has(_blue, StatusKind.Evasion), Is.True);
            Assert.That(_statuses.SpeedModifier(_blue), Is.EqualTo(0.5));
        }

        [Test]
        public void AnUnslowedOperator_HasNoSpeedModifier()
        {
            Assert.That(_statuses.SpeedModifier(_blue), Is.EqualTo(0.0));
        }

        // ── Bleed ────────────────────────────────────────────────────────

        [Test]
        public void BleedStacks_AreAdditive()
        {
            _statuses.Apply(_blue, StatusKind.Bleed, duration: 1);
            _statuses.Apply(_blue, StatusKind.Bleed, duration: 1);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.BleedStacks(_blue), Is.EqualTo(2));
        }

        [Test]
        public void BleedTicks_AtTheBleedingOwnersUpkeep()
        {
            _statuses.Apply(_blue, StatusKind.Bleed, duration: 1);

            Assert.That(_statuses.ConsumeBleed(_blue), Is.EqualTo(0), "not on the applier's turn");

            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.ConsumeBleed(_blue), Is.EqualTo(_config.BleedDamagePerStack));
        }

        [Test]
        public void BleedStack_IsRemovedAfterTicking()
        {
            // Delayed damage, not a lingering condition.
            _statuses.Apply(_blue, StatusKind.Bleed, duration: 1);
            _clock.BeginTurnFor(PlayerColor.Blue);

            _statuses.ConsumeBleed(_blue);

            Assert.That(_statuses.BleedStacks(_blue), Is.EqualTo(0));
            Assert.That(_statuses.IsBleeding(_blue), Is.False);
        }

        [Test]
        public void AnOperatorWithAnUnspentStack_CountsAsBleeding()
        {
            // Read by Syla's From the Hip for its bonus damage.
            _statuses.Apply(_blue, StatusKind.Bleed, duration: 1);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.IsBleeding(_blue), Is.True);
        }

        // ── Stealth ──────────────────────────────────────────────────────

        [Test]
        public void StealthedOperator_CannotBeSingleTargetedByAnEnemy()
        {
            _statuses.Apply(_red, StatusKind.Stealth, duration: 2);

            Assert.That(_statuses.CanBeSingleTargetedBy(_red, PlayerColor.Blue), Is.False);
        }

        [Test]
        public void StealthedOperator_CanStillBeTargetedByAllies()
        {
            // Untargetability is scoped to enemies, so stealth never locks an
            // operator out of its own team's repositioning or healing (§5.4).
            _statuses.Apply(_red, StatusKind.Stealth, duration: 2);

            Assert.That(_statuses.CanBeSingleTargetedBy(_red, PlayerColor.Red), Is.True);
        }

        [Test]
        public void StealthPersists_AfterTheOwnerAttacks()
        {
            // A 9-energy effect that dies the moment its owner acts is not an
            // effect. Nothing in the registry breaks stealth on action.
            _statuses.Apply(_red, StatusKind.Stealth, duration: 2);
            _clock.BeginTurnFor(PlayerColor.Red);

            Assert.That(_statuses.Has(_red, StatusKind.Stealth), Is.True);
        }

        [Test]
        public void AnUnstealthedOperator_IsTargetableByAnyone()
        {
            Assert.That(_statuses.CanBeSingleTargetedBy(_red, PlayerColor.Blue), Is.True);
        }

        // ── Evasion ──────────────────────────────────────────────────────

        [Test]
        public void EvasionNegates_WhenTheRollSucceeds()
        {
            _statuses.ApplyPassive(_blue, StatusKind.Evasion);

            Assert.That(_statuses.TryEvade(_blue, new ScriptedRandom(0.1)), Is.True);
        }

        [Test]
        public void EvasionFails_WhenTheRollMisses()
        {
            _statuses.ApplyPassive(_blue, StatusKind.Evasion);

            Assert.That(_statuses.TryEvade(_blue, new ScriptedRandom(0.9)), Is.False);
        }

        [Test]
        public void SecondNormalInstanceInSameRound_IgnoresEvasion()
        {
            // The per-round cap is load-bearing: uncapped, a roll across the
            // attacks a target sees in a match decides games (§5.5).
            _statuses.ApplyPassive(_blue, StatusKind.Evasion);
            var alwaysEvades = new ScriptedRandom(0.0);

            Assert.That(_statuses.TryEvade(_blue, alwaysEvades), Is.True);
            Assert.That(_statuses.TryEvade(_blue, alwaysEvades), Is.False, "charge already spent");
            Assert.That(_statuses.TryEvade(_blue, alwaysEvades), Is.False);
        }

        [Test]
        public void AFailedEvasionRoll_StillSpendsTheCharge()
        {
            // The charge is the attempt, not the success. Otherwise a miss would
            // leave the operator able to try again on the very next hit.
            _statuses.ApplyPassive(_blue, StatusKind.Evasion);

            _statuses.TryEvade(_blue, new ScriptedRandom(0.9));

            Assert.That(_statuses.TryEvade(_blue, new ScriptedRandom(0.0)), Is.False);
        }

        [Test]
        public void EvasionCharge_RefreshesAtOwnersUpkeep()
        {
            _statuses.ApplyPassive(_blue, StatusKind.Evasion);
            var alwaysEvades = new ScriptedRandom(0.0);

            _statuses.TryEvade(_blue, alwaysEvades);
            Assert.That(_statuses.TryEvade(_blue, alwaysEvades), Is.False);

            _clock.BeginTurnFor(PlayerColor.Blue);
            _statuses.RefreshEvasion(_blue);

            Assert.That(_statuses.TryEvade(_blue, alwaysEvades), Is.True);
        }

        [Test]
        public void AnOperatorWithoutTheEvasionPassive_NeverEvades()
        {
            Assert.That(_statuses.TryEvade(_blue, new ScriptedRandom(0.0)), Is.False);
        }

        // ── Shield ───────────────────────────────────────────────────────

        [Test]
        public void ShieldPool_TakesWhatItCanAndIsSpentByIt()
        {
            // The rule the rework exists for: a pool absorbs an amount, not an
            // instance (§5.6). Under the old rule this shield swallowed all 3.
            _statuses.Apply(_blue, StatusKind.Shield, duration: 99, magnitude: 2);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.AbsorbFrom(_blue, 3), Is.EqualTo(2), "what the pool ate, not the instance");
            Assert.That(_statuses.AbsorbFrom(_blue, 3), Is.EqualTo(0), "the pool is gone");
            Assert.That(_statuses.Has(_blue, StatusKind.Shield), Is.False, "and so is the status");
        }

        [Test]
        public void ShieldPool_SurvivesAHitSmallerThanItself()
        {
            _statuses.Apply(_blue, StatusKind.Shield, duration: 99, magnitude: 2);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.AbsorbFrom(_blue, 1), Is.EqualTo(1));
            Assert.That(_statuses.ShieldPool(_blue), Is.EqualTo(1), "a remnant, not a spent shield");
            Assert.That(_statuses.Has(_blue, StatusKind.Shield), Is.True);
        }

        [Test]
        public void AShieldWithNoStatedPool_TakesTheConfiguredDefault()
        {
            // Under the whole-instance rule a shield's magnitude was never read,
            // so a zero default was harmless. Under a pool it would produce a
            // shield that draws a badge and stops nothing.
            _statuses.Apply(_blue, StatusKind.Shield, duration: 99);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.ShieldPool(_blue), Is.EqualTo(_config.ShieldPoolDefault));
        }

        [Test]
        public void ReapplyingAShield_TopsThePoolUpRatherThanAddingToIt()
        {
            // Sources do not stack; the strongest applies (§5.2). Two supports
            // must not be able to build an arbitrarily deep wall.
            _statuses.Apply(_blue, StatusKind.Shield, duration: 99, magnitude: 2);
            _clock.BeginTurnFor(PlayerColor.Blue);
            _statuses.AbsorbFrom(_blue, 1);

            _statuses.Apply(_blue, StatusKind.Shield, duration: 99, magnitude: 2);

            Assert.That(_statuses.ShieldPool(_blue), Is.EqualTo(2), "back to full, not 3");
        }

        [Test]
        public void AShieldedOperator_HasNoSpeedModifier()
        {
            // The pool lives in the entry's magnitude, and SpeedModifier sums
            // magnitudes blindly across kinds. Without Shield being skipped in
            // that sum, a 2-point plate would hand its holder +2 speed — a
            // support ability silently doubling an ally's movement.
            _statuses.Apply(_blue, StatusKind.Shield, duration: 99, magnitude: 2);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.SpeedModifier(_blue), Is.EqualTo(0.0));
        }

        [Test]
        public void AnUnshieldedOperator_AbsorbsNothing()
        {
            Assert.That(_statuses.AbsorbFrom(_blue, 3), Is.EqualTo(0));
            Assert.That(_statuses.ShieldPool(_blue), Is.EqualTo(0));
        }

        // ── Mark ─────────────────────────────────────────────────────────

        [Test]
        public void Mark_RecordsWhoAppliedIt_AndChangesNothingElse()
        {
            // Bookkeeping only. It applies no modifier; the payout condition
            // reads it (§5.7).
            _statuses.Apply(_blue, StatusKind.Mark, duration: 3, sourceOperatorId: _red.Id);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.MarkedBy(_blue), Is.EqualTo(_red.Id));
            Assert.That(_statuses.SpeedModifier(_blue), Is.EqualTo(0.0));
            Assert.That(_statuses.IsStunned(_blue), Is.False);
        }

        [Test]
        public void AnUnmarkedOperator_ReportsNoMark()
        {
            Assert.That(_statuses.MarkedBy(_blue), Is.Null);
        }

        // ── Clearing and expiry ──────────────────────────────────────────

        [Test]
        public void NeutralizedOperator_LosesAllStatusEffects()
        {
            _statuses.Apply(_blue, StatusKind.Stun, duration: 3);
            _statuses.Apply(_blue, StatusKind.Slow, duration: 3);
            _statuses.Apply(_blue, StatusKind.Bleed, duration: 3);
            _statuses.Apply(_blue, StatusKind.Shield, duration: 3, magnitude: 2);
            _statuses.Apply(_blue, StatusKind.Mark, duration: 3, sourceOperatorId: _red.Id);
            _clock.BeginTurnFor(PlayerColor.Blue);

            _statuses.ClearAll(_blue);

            Assert.That(_statuses.IsStunned(_blue), Is.False);
            Assert.That(_statuses.SpeedModifier(_blue), Is.EqualTo(0.0));
            Assert.That(_statuses.BleedStacks(_blue), Is.EqualTo(0));
            Assert.That(_statuses.ShieldPool(_blue), Is.EqualTo(0));
            Assert.That(_statuses.MarkedBy(_blue), Is.Null);
        }

        [Test]
        public void ExpireCompleted_ReportsWhatJustRanOut()
        {
            _statuses.Apply(_blue, StatusKind.Stun, duration: 1);
            _clock.BeginTurnFor(PlayerColor.Blue);   // the one turn it blocks

            // Swept at the End phase of that same turn: it has done its job.
            var expired = _statuses.ExpireCompleted(_blue);

            Assert.That(expired.Count, Is.EqualTo(1));
            Assert.That(expired[0], Is.EqualTo(StatusKind.Stun));
        }

        [Test]
        public void ExpireCompleted_LeavesActiveAndPermanentStatusesAlone()
        {
            _statuses.ApplyPassive(_blue, StatusKind.Evasion);
            _statuses.Apply(_blue, StatusKind.Stun, duration: 5);
            _clock.BeginTurnFor(PlayerColor.Blue);

            var expired = _statuses.ExpireCompleted(_blue);

            Assert.That(expired.Count, Is.EqualTo(0));
            Assert.That(_statuses.Has(_blue, StatusKind.Evasion), Is.True);
            Assert.That(_statuses.IsStunned(_blue), Is.True);
        }

        [Test]
        public void StatusesOnOneOperator_DoNotLeakToAnother()
        {
            var other = new OperatorState(3, "Syla", PlayerColor.Blue, 6, 2.0);

            _statuses.Apply(_blue, StatusKind.Stun, duration: 3);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.IsStunned(_blue), Is.True);
            Assert.That(_statuses.IsStunned(other), Is.False);
        }

        [Test]
        public void AZeroDuration_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _statuses.Apply(_blue, StatusKind.Stun, duration: 0));
        }
    }
}