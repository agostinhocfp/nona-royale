// Assets/Tests/EditMode/Targeting/SafeCellCampingTests.cs
// NEW FILE — location mirrors Assets/Tests/EditMode/Turn/.
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Targeting
{
    /// <summary>
    /// The camping rule (§4.4, second amendment): an operator on a safe cell
    /// may not aim behind itself — enemy single-targets, cell aims, and
    /// placements at allies are refused; support at allies and everything
    /// that merely radiates still passes.
    /// </summary>
    /// <remarks>
    /// <b>Every position is derived, never hardcoded.</b> The fixture scans
    /// the board for a mid-track safe cell and converts track indices to
    /// per-colour progress through <see cref="ProgressAtTrack"/> — the lesson
    /// <c>SwapEffectTests</c> taught when the 48-cell board grew to 52 and
    /// every literal in it landed one cell off. If the board family changes,
    /// these tests move with it or fail loudly at setup, never silently pass
    /// against the wrong cell.
    ///
    /// The caster camps on an <i>opposing seat's</i> start cell, deliberately:
    /// <c>PathMap.IsSafe</c> is a cell property with no colour attached, and a
    /// mid-track camp keeps three-cell backward placements and swap shifts
    /// clear of anyone's progress-0 boundary.
    /// </remarks>
    [TestFixture]
    public class SafeCellCampingTests
    {
        private PathMap _map;
        private CombatConfig _combat;
        private MatchClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;
        private AbilityResolver _abilities;

        private PlayerState _red;
        private PlayerState _blue;
        private List<OperatorState> _operators;

        private OperatorState _caster;
        private OperatorState _ally;
        private OperatorState _enemy;

        private int _circuit;
        private int _safeTrack;
        private int _casterProgress;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _combat = CombatConfig.Default;

            _caster = new OperatorState(1, "Camper", PlayerColor.Red, 6, 1.0);
            _ally = new OperatorState(2, "Friend", PlayerColor.Red, 6, 1.0);
            _enemy = new OperatorState(3, "Enemy", PlayerColor.Blue, 6, 1.0);

            _red = new PlayerState(PlayerColor.Red, new[] { _caster, _ally });
            _blue = new PlayerState(PlayerColor.Blue, new[] { _enemy });
            _operators = new List<OperatorState> { _caster, _ally, _enemy };

            _clock = new MatchClock(new[] { _red, _blue });
            _statuses = new StatusRegistry(_clock, _combat);
            _targeting = new TargetingRules(_map, _statuses);

            var energy = new EnergyLedger(EnergyConfig.Default);
            var damage = new DamagePipeline(_statuses, new SeededRandom(1));
            var cellEffects = new DeferredCellEffects(_clock, _targeting, damage, _statuses);
            _abilities = new AbilityResolver(
                _map, _clock, energy, _statuses, _targeting, damage, cellEffects);

            _clock.BeginTurnFor(_red);

            _circuit = _map.Profile.CircuitLength;
            _safeTrack = FindMidTrackSafeCell();
            _casterProgress = ProgressAtTrack(PlayerColor.Red, _safeTrack);
            _caster.MoveTo(_casterProgress);
        }

        // ── The predicate's arithmetic ───────────────────────────────────

        [Test]
        public void ThreeCellsBehindASafeCell_IsAnAimBehind()
        {
            Assert.That(
                _targeting.IsAimedBehindFromSafeCell(_caster, TrackCell(Behind(3))),
                Is.True);
        }

        [Test]
        public void ThreeCellsAhead_IsNot()
        {
            Assert.That(
                _targeting.IsAimedBehindFromSafeCell(_caster, TrackCell(Ahead(3))),
                Is.False);
        }

        [Test]
        public void TheExactOppositeCell_CountsAsAhead()
        {
            // The even-circuit tie is assigned to the targetable side, so the
            // rule never blocks more than what is strictly behind (§4.4).
            Assert.That(
                _targeting.IsAimedBehindFromSafeCell(_caster, TrackCell(Ahead(_circuit / 2))),
                Is.False);
        }

        [Test]
        public void OneCellPastTheOpposite_IsBehind()
        {
            Assert.That(
                _targeting.IsAimedBehindFromSafeCell(_caster, TrackCell(Ahead(_circuit / 2 + 1))),
                Is.True);
        }

        [Test]
        public void TheCastersOwnCell_IsNeverBehind()
        {
            // Forward offset zero. The predicate takes no position on
            // self-targeting, which is still an open rules question.
            Assert.That(
                _targeting.IsAimedBehindFromSafeCell(_caster, _targeting.CellOf(_caster)),
                Is.False);
        }

        [Test]
        public void OffASafeCell_NothingCountsAsBehind()
        {
            _caster.MoveTo(_casterProgress + 1);
            Assert.That(
                _map.IsSafe(_targeting.CellOf(_caster)), Is.False,
                "fixture assumes the neighbouring cell is not itself safe");

            // Geometrically still behind the caster; legally nothing is,
            // because the shelter is the whole of what the rule reads.
            Assert.That(
                _targeting.IsAimedBehindFromSafeCell(_caster, TrackCell(Behind(3))),
                Is.False);
        }

        [Test]
        public void BehindWrapsAcrossTheTrackSeam()
        {
            int seatZeroStart = ProgressAtTrack(PlayerColor.Red, 0);
            _caster.MoveTo(seatZeroStart);
            Assert.That(
                _map.IsSafe(_targeting.CellOf(_caster)), Is.True,
                "fixture assumes track 0 is a start cell");

            // One step behind track 0 is the last index on the circuit — the
            // modular arithmetic must see it as behind, not as far ahead.
            Assert.That(
                _targeting.IsAimedBehindFromSafeCell(_caster, TrackCell(_circuit - 1)),
                Is.True);
        }

        // ── Single-targeting: the enemy half ─────────────────────────────

        [Test]
        public void AnEnemyBehind_IsRefusedWithTheCampingVerdict()
        {
            _enemy.MoveTo(ProgressAtTrack(PlayerColor.Blue, Behind(3)));

            var result = _targeting.CanSingleTarget(_caster, _enemy, range: 3);

            Assert.That(result.IsLegal, Is.False);
            Assert.That(result.Verdict, Is.EqualTo(TargetingVerdict.AimedBehindFromSafeCell));
        }

        [Test]
        public void AnEnemyAhead_IsStillLegal()
        {
            _enemy.MoveTo(ProgressAtTrack(PlayerColor.Blue, Ahead(3)));

            Assert.That(_targeting.CanSingleTarget(_caster, _enemy, range: 3).IsLegal, Is.True);
        }

        [Test]
        public void AnAllyBehind_IsStillSingleTargetable()
        {
            // The targeting layer lets every allied aim through; only the
            // resolver, which can see what the ability contains, refuses the
            // placements. Heals and plates must survive this check.
            _ally.MoveTo(ProgressAtTrack(PlayerColor.Red, Behind(3)));

            Assert.That(_targeting.CanSingleTarget(_caster, _ally, range: 3).IsLegal, Is.True);
        }

        [Test]
        public void TheSameShot_OffASafeCell_IsLegal()
        {
            _caster.MoveTo(_casterProgress + 1);
            _enemy.MoveTo(ProgressAtTrack(PlayerColor.Blue, Behind(3)));

            Assert.That(_targeting.CanSingleTarget(_caster, _enemy, range: 4).IsLegal, Is.True);
        }

        // ── Cell aims: the artillery half ────────────────────────────────

        [Test]
        public void ACellBehind_IsRefused()
        {
            var result = _targeting.CanTargetCell(_caster, TrackCell(Behind(3)), range: 3);

            Assert.That(result.IsLegal, Is.False);
            Assert.That(result.Verdict, Is.EqualTo(TargetingVerdict.AimedBehindFromSafeCell));
        }

        [Test]
        public void ACellAhead_IsStillLegal()
        {
            Assert.That(_targeting.CanTargetCell(_caster, TrackCell(Ahead(3)), range: 3).IsLegal, Is.True);
        }

        // ── Ally placements: the safe-cell taxi ──────────────────────────

        [Test]
        public void APlacementAtAnAllyBehind_IsRefused()
        {
            _ally.MoveTo(ProgressAtTrack(PlayerColor.Red, Behind(3)));

            var result = _abilities.Use(_caster, Mimi.Translocation, _ally, _red, _operators);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.IllegalTarget));
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.AimedBehindFromSafeCell));
        }

        [Test]
        public void APlacementAtAnAllyBehind_IsNeverOffered()
        {
            _ally.MoveTo(ProgressAtTrack(PlayerColor.Red, Behind(3)));

            var offered = _abilities.LegalTargets(_caster, Mimi.Translocation, _operators);

            Assert.That(offered, Has.No.Member(_ally));
        }

        [Test]
        public void SupportAtAnAllyBehind_StillPasses()
        {
            // The rule is about camping-as-aggression and camping-as-taxi.
            // A camper plating the squad behind it is neither.
            _ally.MoveTo(ProgressAtTrack(PlayerColor.Red, Behind(3)));

            var offered = _abilities.LegalTargets(_caster, Javi.TraumaPlate, _operators);

            Assert.That(offered, Has.Member(_ally));
        }

        [Test]
        public void APlacementAtAnAllyAhead_IsStillOffered()
        {
            _ally.MoveTo(ProgressAtTrack(PlayerColor.Red, Ahead(3)));

            var offered = _abilities.LegalTargets(_caster, Mimi.Translocation, _operators);

            Assert.That(offered, Has.Member(_ally));
        }

        // ── Derived geometry ─────────────────────────────────────────────

        /// <summary>
        /// Track index of a safe cell far from anyone's progress boundaries —
        /// in practice an opposing seat's start cell. Fails loudly rather than
        /// guessing if the board family stops providing one.
        /// </summary>
        private int FindMidTrackSafeCell()
        {
            for (int p = 15; p <= _circuit - 10; p++)
            {
                var cell = _map.CellAt(PlayerColor.Red, p);
                if (_map.IsSafe(cell)) return cell.Index;
            }

            Assert.Fail("No mid-track safe cell found — has the board family changed?");
            return -1;
        }

        /// <summary>
        /// The progress at which <paramref name="owner"/> stands on a given
        /// track index. The inverse of <c>PathMap.CellAt</c>, by scan.
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

        /// <summary>
        /// A CellRef for a raw track index, resolved through whichever fixture
        /// colour can stand there — a colour may be unable to occupy the last
        /// cells before its own column entry.
        /// </summary>
        private CellRef TrackCell(int track)
        {
            foreach (var color in new[] { PlayerColor.Red, PlayerColor.Blue })
            {
                for (int p = 0; p < _circuit; p++)
                {
                    if (!_map.IsOnOuterTrack(p)) continue;
                    if (_map.CellAt(color, p).Index == track) return _map.CellAt(color, p);
                }
            }

            Assert.Fail($"Track {track} is unreachable for both fixture colours — has the board changed?");
            return default;
        }

        private int Behind(int steps) => (_safeTrack + _circuit - steps) % _circuit;

        private int Ahead(int steps) => (_safeTrack + steps) % _circuit;
    }
}