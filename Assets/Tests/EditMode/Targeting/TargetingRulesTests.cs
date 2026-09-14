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

    /// <summary>
    /// General targeting: distance, single-target legality, stealth, areas,
    /// and §4.4's <i>first</i> amendment. The second amendment — the camping
    /// rule — has its own fixture, <c>SafeCellCampingTests</c>, which also
    /// owns the resolver's ally-placement half. The split is deliberate:
    /// neither fixture asserts anything the other does.
    /// </summary>
    /// <remarks>
    /// <b>Rewritten off the 48-cell literals (2026-09-14).</b> The original
    /// carried "Blue starts at track 12" and a <c>% 48</c> in its comments;
    /// on the 52-cell circuit the offset is 13 and half the distance asserts
    /// were one cell out — the same disease <c>SwapEffectTests</c> caught.
    /// Positions are now stated as <i>track indices</i>, which is how every
    /// comment here already reasoned, and converted to per-colour progress
    /// through <see cref="ProgressAtTrack"/>. If the board family changes,
    /// these tests move with it or fail loudly, never silently pass against
    /// the wrong cell.
    /// </remarks>
    [TestFixture]
    public class TargetingRulesTests
    {
        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;
        private int _circuit;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _clock = new FakeClock();
            _statuses = new StatusRegistry(_clock, CombatConfig.Default);
            _targeting = new TargetingRules(_map, _statuses);
            _clock.BeginTurnFor(PlayerColor.Red);
            _circuit = _map.Profile.CircuitLength;
        }

        // ── Placement helpers ────────────────────────────────────────────

        private OperatorState RedAtTrack(int id, int track) =>
            AtTrack(id, "Red op", PlayerColor.Red, track);

        private OperatorState BlueAtTrack(int id, int track) =>
            AtTrack(id, "Blue op", PlayerColor.Blue, track);

        private OperatorState AtTrack(int id, string name, PlayerColor owner, int track)
        {
            var op = new OperatorState(id, name, owner, 6, 1.5);
            op.MoveTo(ProgressAtTrack(owner, track));
            return op;
        }

        /// <summary>For positions that are about progress itself — the yard and the home column.</summary>
        private static OperatorState AtProgress(int id, string name, PlayerColor owner, int progress)
        {
            var op = new OperatorState(id, name, owner, 6, 1.5);
            if (progress != PathMap.YardProgress) op.MoveTo(progress);
            return op;
        }

        /// <summary>
        /// The progress at which <paramref name="owner"/> stands on a given
        /// track index — the inverse of <c>PathMap.CellAt</c>, by scan, so no
        /// start-cell offset is ever hardcoded again.
        /// </summary>
        private int ProgressAtTrack(PlayerColor owner, int track)
        {
            for (int p = 0; p < _circuit; p++)
            {
                if (!_map.IsOnOuterTrack(p)) continue;
                if (_map.CellAt(owner, p).Index == track) return p;
            }

            Assert.Fail($"Track {track} is unreachable for {owner} — has the board changed?");
            return -1;
        }

        // ── Distance ─────────────────────────────────────────────────────

        [Test]
        public void RangeIsCountedAlongTrack_NotEuclidean()
        {
            // Half the loop apart — on the cross that is the opposite arm,
            // physically close across the centre, maximally far in play. If
            // range were Euclidean these would be near-neighbours (§4.1).
            var caster = RedAtTrack(1, 0);
            var target = BlueAtTrack(2, _circuit / 2);

            Assert.That(_targeting.Distance(caster, target), Is.EqualTo(_circuit / 2));
            Assert.That(_targeting.CanSingleTarget(caster, target, 3).Verdict,
                Is.EqualTo(TargetingVerdict.OutOfRange));
        }

        [Test]
        public void RangeCountsInBothDirections()
        {
            // Track 2 to track 50: four steps backwards across the seam, not
            // forty-eight forwards.
            var caster = RedAtTrack(1, 2);
            var target = BlueAtTrack(2, _circuit - 2);

            Assert.That(_targeting.Distance(caster, target), Is.EqualTo(4));
        }

        [Test]
        public void DistanceIsSymmetric()
        {
            var a = RedAtTrack(1, 5);
            var b = BlueAtTrack(2, 13);

            Assert.That(_targeting.Distance(a, b), Is.EqualTo(_targeting.Distance(b, a)));
        }

        // ── Single targeting ─────────────────────────────────────────────

        [Test]
        public void ATargetWithinRange_IsLegal()
        {
            var caster = RedAtTrack(1, 20);
            var target = BlueAtTrack(2, 22);

            var result = _targeting.CanSingleTarget(caster, target, 3);

            Assert.That(result.IsLegal, Is.True);
            Assert.That(result.Distance, Is.EqualTo(2));
        }

        [Test]
        public void ATargetExactlyAtRange_IsLegal()
        {
            var caster = RedAtTrack(1, 20);
            var target = BlueAtTrack(2, 23);

            Assert.That(_targeting.CanSingleTarget(caster, target, 3).IsLegal, Is.True);
        }

        [Test]
        public void ATargetOneStepBeyondRange_IsNot()
        {
            var caster = RedAtTrack(1, 20);
            var target = BlueAtTrack(2, 24);

            Assert.That(_targeting.CanSingleTarget(caster, target, 3).Verdict,
                Is.EqualTo(TargetingVerdict.OutOfRange));
        }

        [Test]
        public void OperatorInHomeColumn_CannotBeTargeted()
        {
            // Journey - 1 is the last home-column cell — guaranteed inside the
            // column on any board, unlike a hardcoded progress.
            var caster = RedAtTrack(1, 20);
            var sheltered = AtProgress(2, "Blue op", PlayerColor.Blue,
                BoardProfile.Standard.Journey - 1);

            Assert.That(_targeting.CanSingleTarget(caster, sheltered, 3).Verdict,
                Is.EqualTo(TargetingVerdict.TargetOutOfPlay));
        }

        [Test]
        public void OperatorInHomeColumn_CannotTarget()
        {
            // The rule is symmetric: out of the fight means both ways (§4.3).
            var sheltered = AtProgress(1, "Red op", PlayerColor.Red,
                BoardProfile.Standard.Journey - 1);
            var target = BlueAtTrack(2, 20);

            Assert.That(_targeting.CanSingleTarget(sheltered, target, 3).Verdict,
                Is.EqualTo(TargetingVerdict.CasterOutOfPlay));
        }

        [Test]
        public void AnOperatorInTheYard_IsNeitherCasterNorTarget()
        {
            var undeployed = AtProgress(2, "Blue op", PlayerColor.Blue, PathMap.YardProgress);
            var caster = RedAtTrack(1, 20);

            Assert.That(_targeting.IsInPlay(undeployed), Is.False);
            Assert.That(_targeting.CanSingleTarget(caster, undeployed, 3).Verdict,
                Is.EqualTo(TargetingVerdict.TargetOutOfPlay));
        }

        // ── §4.4, first amendment ────────────────────────────────────────

        [Test]
        public void AnEnemyOnASafeCell_CannotBeSingleTargeted()
        {
            // Reversed on 2026-09-13. The original rule — and this test's
            // original assertion — was that safe meant safe from collision and
            // nothing more, so an ability-proof cell could not become free
            // parking. The counterweight it did not weigh: an operator parked
            // on a start cell is an operator not winning. Areas still sweep it
            // (below), and the parking risk is paid for by the camping rule
            // (§4.4, second amendment — SafeCellCampingTests).
            var caster = RedAtTrack(1, 10);
            var onStartCell = BlueAtTrack(2, 13);   // Blue's own safe start

            Assert.That(_map.IsSafe(_targeting.CellOf(onStartCell)), Is.True, "precondition");
            Assert.That(_targeting.CanSingleTarget(caster, onStartCell, 3).Verdict,
                Is.EqualTo(TargetingVerdict.OnASafeCell));
        }

        [Test]
        public void AnAllyOnASafeCell_CanStillBeSingleTargeted()
        {
            // Scoped to enemies exactly as stealth is — and safety belongs to
            // the cell, not a colour, so a Red ally on Blue's start is
            // sheltered and still reachable by its own side.
            var caster = RedAtTrack(1, 10);
            var ally = RedAtTrack(2, 13);

            Assert.That(_map.IsSafe(_targeting.CellOf(ally)), Is.True, "precondition");
            Assert.That(_targeting.CanSingleTarget(caster, ally, 3).IsLegal, Is.True);
        }

        [Test]
        public void AnAreaStillSweepsASafeCell()
        {
            // The whole of the amendment is single-target only: a safe cell
            // stops somebody picking you out, not a blast (§4.4, §5.4).
            var caster = RedAtTrack(1, 10);
            var onStartCell = BlueAtTrack(2, 13);

            var hit = _targeting.EnemiesInArea(
                _targeting.CellOf(caster), 3, PlayerColor.Red,
                new List<OperatorState> { onStartCell });

            Assert.That(hit.Count, Is.EqualTo(1));
        }

        [Test]
        public void AnAlly_IsALegalTarget()
        {
            // Velvet Rope and All-In Mauling both have friendly modes.
            var caster = RedAtTrack(1, 20);
            var ally = RedAtTrack(2, 22);

            Assert.That(_targeting.CanSingleTarget(caster, ally, 3).IsLegal, Is.True);
        }

        // ── Stealth ──────────────────────────────────────────────────────

        [Test]
        public void StealthedOperator_CannotBeSingleTargetedByAnEnemy()
        {
            // Off any safe cell, deliberately: the safe-cell verdict is checked
            // first, so a hidden operator on a start cell reports OnASafeCell
            // and this test would pass for the wrong reason.
            var caster = RedAtTrack(1, 20);
            var hidden = BlueAtTrack(2, 22);
            _statuses.Apply(hidden, StatusKind.Stealth, duration: 2);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_targeting.CanSingleTarget(caster, hidden, 3).Verdict,
                Is.EqualTo(TargetingVerdict.Stealthed));
        }

        [Test]
        public void StealthedOperator_CanStillBeTargetedByAllies()
        {
            var ally = BlueAtTrack(1, 20);
            var hidden = BlueAtTrack(2, 22);
            _statuses.Apply(hidden, StatusKind.Stealth, duration: 2);
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_targeting.CanSingleTarget(ally, hidden, 3).IsLegal, Is.True);
        }

        [Test]
        public void OutOfRangeWins_OverStealthed()
        {
            // A player who cannot reach a target does not need to be told it was
            // hidden — the verdict should name the problem they can act on.
            var caster = RedAtTrack(1, 0);
            var hidden = BlueAtTrack(2, _circuit / 2);
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
            var caster = RedAtTrack(1, 20);
            var ahead = BlueAtTrack(2, 23);          // three ahead
            var behind = BlueAtTrack(3, 17);         // three behind
            var tooFar = BlueAtTrack(4, 24);         // four ahead

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
            var caster = RedAtTrack(1, 20);
            var ally = RedAtTrack(2, 21);
            var enemy = BlueAtTrack(3, 22);

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
            var caster = RedAtTrack(1, 20);
            var hidden = BlueAtTrack(2, 22);
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
            var caster = RedAtTrack(1, 20);
            var sheltered = AtProgress(2, "Blue op", PlayerColor.Blue,
                BoardProfile.Standard.Journey - 1);

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
            var primary = BlueAtTrack(2, 22);
            var bystander = BlueAtTrack(3, 23);

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
            var caster = RedAtTrack(1, 13);          // standing on Blue's start
            var sharing = BlueAtTrack(2, 13);        // same cell

            Assert.That(_map.IsSafe(_targeting.CellOf(caster)), Is.True, "precondition");

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
                () => _targeting.CanSingleTarget(RedAtTrack(1, 0), BlueAtTrack(2, 13), -1));
        }
    }
}