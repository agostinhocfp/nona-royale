// Assets/Tests/EditMode/Targeting/TargetingRulesTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Targeting
{
    internal sealed class FakeClock : ITurnClock
    {
        private readonly int[] _turns = new int[4];

        public PlayerColor ActivePlayer { get; private set; } = PlayerColor.Red;

        public int TurnIndexOf(PlayerColor color) => _turns[(int)color];

        public void BeginTurnFor(PlayerColor color)
        {
            ActivePlayer = color;
            _turns[(int)color]++;
        }
    }

    [TestFixture]
    public class TargetingRulesTests
    {
        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _clock = new FakeClock();
            _statuses = new StatusRegistry(_clock, CombatConfig.Default);
            _targeting = new TargetingRules(_map, _statuses);
            _clock.BeginTurnFor(PlayerColor.Red);
        }

        /// <summary>Red starts at track 0, so Red's progress equals its track index.</summary>
        private static OperatorState Red(int id, int progress) => At(id, "Red op", PlayerColor.Red, progress);

        /// <summary>Blue starts at track 12, so Blue's track index is progress + 12.</summary>
        private static OperatorState Blue(int id, int progress) => At(id, "Blue op", PlayerColor.Blue, progress);

        private static OperatorState At(int id, string name, PlayerColor owner, int progress)
        {
            var op = new OperatorState(id, name, owner, 6, 1.5);
            if (progress != PathMap.YardProgress) op.MoveTo(progress);
            return op;
        }

        // ── Distance ─────────────────────────────────────────────────────

        [Test]
        public void RangeIsCountedAlongTrack_NotEuclidean()
        {
            // Red on track 0, Blue on track 24 — physically opposite each other
            // across the centre of the cross, a quarter of the loop apart in
            // play. If range were Euclidean these would be neighbours (§4.1).
            var caster = Red(1, 0);
            var target = Blue(2, 12);       // track 24

            Assert.That(_targeting.Distance(caster, target), Is.EqualTo(24));
            Assert.That(_targeting.CanSingleTarget(caster, target, 3).Verdict,
                Is.EqualTo(TargetingVerdict.OutOfRange));
        }

        [Test]
        public void RangeCountsInBothDirections()
        {
            // Red on track 2, Blue on track 46. Four steps backwards, not 44
            // forwards.
            var caster = Red(1, 2);
            var target = Blue(2, 34);       // 34 + 12 = 46

            Assert.That(_targeting.Distance(caster, target), Is.EqualTo(4));
        }

        [Test]
        public void DistanceIsSymmetric()
        {
            var a = Red(1, 5);
            var b = Blue(2, 0);             // track 12

            Assert.That(_targeting.Distance(a, b), Is.EqualTo(_targeting.Distance(b, a)));
        }

        // ── Single targeting ─────────────────────────────────────────────

        [Test]
        public void ATargetWithinRange_IsLegal()
        {
            var caster = Red(1, 10);
            var target = Blue(2, 0);        // track 12, two steps away

            var result = _targeting.CanSingleTarget(caster, target, 3);

            Assert.That(result.IsLegal, Is.True);
            Assert.That(result.Distance, Is.EqualTo(2));
        }

        [Test]
        public void ATargetExactlyAtRange_IsLegal()
        {
            var caster = Red(1, 9);
            var target = Blue(2, 0);        // track 12, three steps away

            Assert.That(_targeting.CanSingleTarget(caster, target, 3).IsLegal, Is.True);
        }

        [Test]
        public void ATargetOneStepBeyondRange_IsNot()
        {
            var caster = Red(1, 8);
            var target = Blue(2, 0);        // track 12, four steps away

            Assert.That(_targeting.CanSingleTarget(caster, target, 3).Verdict,
                Is.EqualTo(TargetingVerdict.OutOfRange));
        }

        [Test]
        public void OperatorInHomeColumn_CannotBeTargeted()
        {
            var caster = Red(1, 10);
            var sheltered = Blue(2, 50);    // inside its own column

            Assert.That(_targeting.CanSingleTarget(caster, sheltered, 3).Verdict,
                Is.EqualTo(TargetingVerdict.TargetOutOfPlay));
        }

        [Test]
        public void OperatorInHomeColumn_CannotTarget()
        {
            // The rule is symmetric: out of the fight means both ways (§4.3).
            var sheltered = Red(1, 50);
            var target = Blue(2, 0);

            Assert.That(_targeting.CanSingleTarget(sheltered, target, 3).Verdict,
                Is.EqualTo(TargetingVerdict.CasterOutOfPlay));
        }

        [Test]
        public void AnOperatorInTheYard_IsNeitherCasterNorTarget()
        {
            var undeployed = Blue(2, PathMap.YardProgress);
            var caster = Red(1, 10);

            Assert.That(_targeting.IsInPlay(undeployed), Is.False);
            Assert.That(_targeting.CanSingleTarget(caster, undeployed, 3).Verdict,
                Is.EqualTo(TargetingVerdict.TargetOutOfPlay));
        }

        [Test]
        public void OperatorOnSafeCell_CanStillBeTargetedByAbilities()
        {
            // Safe means safe from collision and nothing more. If safe cells
            // blocked abilities they would be free parking and the combat layer
            // would stall there (§4.4).
            var caster = Red(1, 10);
            var onStartCell = Blue(2, 0);   // track 12, Blue's own safe start

            Assert.That(_map.IsSafe(_targeting.CellOf(onStartCell)), Is.True);
            Assert.That(_targeting.CanSingleTarget(caster, onStartCell, 3).IsLegal, Is.True);
        }

        [Test]
        public void AnAlly_IsALegalTarget()
        {
            // Velvet Rope and All-In Mauling both have friendly modes.
            var caster = Red(1, 10);
            var ally = Red(2, 12);

            Assert.That(_targeting.CanSingleTarget(caster, ally, 3).IsLegal, Is.True);
        }

        // ── Stealth ──────────────────────────────────────────────────────

        [Test]
        public void StealthedOperator_CannotBeSingleTargetedByAnEnemy()
        {
            var caster = Red(1, 10);
            var hidden = Blue(2, 0);
            _statuses.Apply(hidden, StatusKind.Stealth, duration: 2);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_targeting.CanSingleTarget(caster, hidden, 3).Verdict,
                Is.EqualTo(TargetingVerdict.Stealthed));
        }

        [Test]
        public void StealthedOperator_CanStillBeTargetedByAllies()
        {
            var ally = Blue(1, 10 + 12 - 12);   // Blue progress 10 -> track 22
            var hidden = Blue(2, 12);           // track 24, two steps away
            _statuses.Apply(hidden, StatusKind.Stealth, duration: 2);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_targeting.CanSingleTarget(ally, hidden, 3).IsLegal, Is.True);
        }

        [Test]
        public void OutOfRangeWins_OverStealthed()
        {
            // A player who cannot reach a target does not need to be told it was
            // hidden — the verdict should name the problem they can act on.
            var caster = Red(1, 0);
            var hidden = Blue(2, 12);           // track 24, far away
            _statuses.Apply(hidden, StatusKind.Stealth, duration: 2);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_targeting.CanSingleTarget(caster, hidden, 3).Verdict,
                Is.EqualTo(TargetingVerdict.OutOfRange));
        }

        // ── Area of effect ───────────────────────────────────────────────

        [Test]
        public void AoeWithinThree_CoversSevenCells()
        {
            // Three steps each way plus the origin (§4.2).
            Assert.That(_targeting.AreaCellCount(3), Is.EqualTo(7));
            Assert.That(_targeting.AreaCellCount(2), Is.EqualTo(5));
            Assert.That(_targeting.AreaCellCount(0), Is.EqualTo(1));
        }

        [Test]
        public void AnAreaSweepsBothDirectionsFromItsOrigin()
        {
            var caster = Red(1, 10);                 // track 10
            var ahead = Blue(2, 1);                  // track 13, three ahead
            var behind = Blue(3, 43);                // track 55 % 48 = 7, three behind
            var tooFar = Blue(4, 2);                 // track 14, four ahead

            var hit = _targeting.EnemiesInArea(
                _targeting.CellOf(caster), 3, PlayerColor.Red,
                new List<OperatorState> { ahead, behind, tooFar });

            Assert.That(hit.Count, Is.EqualTo(2));
            Assert.That(hit.Contains(ahead), Is.True);
            Assert.That(hit.Contains(behind), Is.True);
            Assert.That(hit.Contains(tooFar), Is.False);
        }

        [Test]
        public void AnAreaNeverHitsTheCastersOwnSide()
        {
            var caster = Red(1, 10);
            var ally = Red(2, 11);
            var enemy = Blue(3, 0);                  // track 12

            var hit = _targeting.EnemiesInArea(
                _targeting.CellOf(caster), 3, PlayerColor.Red,
                new List<OperatorState> { ally, enemy });

            Assert.That(hit.Count, Is.EqualTo(1));
            Assert.That(hit.Contains(enemy), Is.True);
        }

        [Test]
        public void StealthedOperator_IsStillHitByAoe()
        {
            // Stealth hides you from being aimed at, not from the room (§5.4).
            var caster = Red(1, 10);
            var hidden = Blue(2, 0);                 // track 12
            _statuses.Apply(hidden, StatusKind.Stealth, duration: 2);
            _clock.BeginTurnFor(PlayerColor.Blue);

            var hit = _targeting.EnemiesInArea(
                _targeting.CellOf(caster), 3, PlayerColor.Red,
                new List<OperatorState> { hidden });

            Assert.That(hit.Count, Is.EqualTo(1));
        }

        [Test]
        public void AnAreaDoesNotReachIntoAHomeColumn()
        {
            var caster = Red(1, 10);
            var sheltered = Blue(2, 50);

            var hit = _targeting.EnemiesInArea(
                _targeting.CellOf(caster), 3, PlayerColor.Red,
                new List<OperatorState> { sheltered });

            Assert.That(hit.Count, Is.EqualTo(0));
        }

        [Test]
        public void MiraclePullSplash_ExcludesPrimaryTarget()
        {
            // The splash originates on the primary target and leaves it out —
            // it already took the direct hit (§4.2).
            var primary = Blue(2, 0);                // track 12
            var bystander = Blue(3, 1);              // track 13

            var splash = _targeting.EnemiesInArea(
                _targeting.CellOf(primary), 3, PlayerColor.Red,
                new List<OperatorState> { primary, bystander },
                exclude: primary);

            Assert.That(splash.Count, Is.EqualTo(1));
            Assert.That(splash.Contains(bystander), Is.True);
            Assert.That(splash.Contains(primary), Is.False);
        }

        [Test]
        public void ASelfOriginAoe_IncludesEnemiesSharingTheCastersCell()
        {
            // Only possible on a safe cell, which is exactly where an ability
            // should still reach.
            var caster = Red(1, 12);                 // track 12, Blue's start
            var sharing = Blue(2, 0);                // same cell

            var hit = _targeting.EnemiesInArea(
                _targeting.CellOf(caster), 2, PlayerColor.Red,
                new List<OperatorState> { sharing });

            Assert.That(hit.Count, Is.EqualTo(1));
        }

        [Test]
        public void ANegativeRadius_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _targeting.AreaCellCount(-1));
        }

        [Test]
        public void ANegativeRange_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _targeting.CanSingleTarget(Red(1, 0), Blue(2, 0), -1));
        }
    }
}