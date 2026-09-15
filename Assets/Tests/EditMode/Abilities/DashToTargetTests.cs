// Assets/Tests/EditMode/Abilities/DashToTargetTests.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Abilities
{
    /// <summary>
    /// Sanity's ultimate: the first effect that repositions the caster without
    /// swapping (§7.6). The dash rakes enemies on the traversed cells, places
    /// the caster one step past the target — one short when that is occupied —
    /// and the target's hit and stun are ordinary enemy-audience effects, so
    /// an ally anchor is pure mobility.
    /// </summary>
    [TestFixture]
    public class DashToTargetTests
    {
        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;
        private EnergyLedger _energy;
        private DamagePipeline _damage;
        private DeferredCellEffects _cellEffects;
        private AbilityResolver _abilities;

        private OperatorState _sanity;
        private OperatorState _ally;
        private OperatorState _target;
        private OperatorState _pathEnemy;
        private OperatorState _bystander;
        private PlayerState _red;
        private List<OperatorState> _board;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _clock = new FakeClock();
            _statuses = new StatusRegistry(_clock, CombatConfig.Default);
            _targeting = new TargetingRules(_map, _statuses);
            _energy = new EnergyLedger(EnergyConfig.Default);
            _damage = new DamagePipeline(_statuses, new SeededRandom(1));
            _cellEffects = new DeferredCellEffects(_clock, _targeting, _damage, _statuses);
            _abilities = new AbilityResolver(_map, _clock, _energy, _statuses, _targeting, _damage, _cellEffects);

            // Positions are stated as TRACK cells, never progress. Caster at
            // 10, an ally just ahead, an enemy on the path at 12, the target
            // at 14 (four steps, inside range 5), and an enemy well clear of
            // the line at 20. None of these is a start cell, so nothing here
            // is safe.
            _sanity = AtTrack(1, "Sanity", PlayerColor.Red, Sanity.MaxHealth, 10);
            _ally = AtTrack(2, "Ally", PlayerColor.Red, 6, 11);
            _pathEnemy = AtTrack(3, "PathEnemy", PlayerColor.Blue, 6, 12);
            _target = AtTrack(4, "Target", PlayerColor.Blue, 6, 14);
            _bystander = AtTrack(5, "Bystander", PlayerColor.Blue, 6, 20);

            _red = new PlayerState(PlayerColor.Red, new[] { _sanity, _ally });
            _board = new List<OperatorState> { _sanity, _ally, _pathEnemy, _target, _bystander };

            _clock.BeginTurnFor(PlayerColor.Red);
            _red.BeginTurn();
            Fund(12);
        }

        private OperatorState AtTrack(int id, string name, PlayerColor owner, int hp, int track)
        {
            var op = new OperatorState(id, name, owner, hp, 1.5);
            op.MoveTo(ProgressAtTrack(owner, track));
            return op;
        }

        private int ProgressAtTrack(PlayerColor owner, int track)
        {
            int circuit = _map.Profile.CircuitLength;
            int start = _map.StartTrackIndex(owner);

            return ((track - start) % circuit + circuit) % circuit;
        }

        /// <summary>Tops the pool up to a known figure without going through the dice.</summary>
        private void Fund(int amount)
        {
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6
            _red.BeginTurn();
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6, capped at 12
            if (amount < 12) _energy.Spend(_red, 12 - amount);
        }

        private AbilityResolution Use(OperatorState target) =>
            _abilities.Use(_sanity, Sanity.Collision, target, _red, _board);

        private CellRef CellOf(OperatorState op) => _map.CellAt(op.Owner, op.Progress);

        [Test]
        public void Collision_DamagesAndStunsAnEnemyTarget()
        {
            var result = Use(_target);

            Assert.That(result.Approved, Is.True);
            Assert.That(_target.Health, Is.EqualTo(3), "the anchor hit, and no path damage on top of it");

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.IsStunned(_target), Is.True);
        }

        [Test]
        public void Collision_RakesOnlyEnemiesOnTheTraversedCells()
        {
            Use(_target);

            Assert.That(_pathEnemy.Health, Is.EqualTo(5),
                "1 for standing on the path — not 3, because the dash is not a collision (§7.1)");
            Assert.That(_ally.Health, Is.EqualTo(6), "the rake is an enemy weapon");
            Assert.That(_bystander.Health, Is.EqualTo(6), "off the line, untouched");
            Assert.That(_target.Health, Is.EqualTo(3), "the anchor takes its own hit, not the rake's");
        }

        [Test]
        public void Collision_LandsOneStepPastTheTarget_AlongTheDashDirection()
        {
            var result = Use(_target);

            Assert.That(result.Outcomes.Any(o => o.Kind == EffectOutcomeKind.Dashed), Is.True);
            Assert.That(CellOf(_sanity), Is.EqualTo(CellRef.Track(15)),
                "a cell behind the target, as the description promises");
        }

        [Test]
        public void Collision_FallsBackOneShort_WhenThePastCellIsOccupied()
        {
            // The ally moves to the landing cell; the dash then lands on the
            // caster's side of the target instead. Placement onto an occupied
            // cell would resolve nothing (§7.4), but the rule picks the free
            // side first.
            _ally.MoveTo(ProgressAtTrack(PlayerColor.Red, 15));

            Use(_target);

            Assert.That(CellOf(_sanity), Is.EqualTo(CellRef.Track(13)),
                "one short of the target, on the side the caster came from");
        }

        [Test]
        public void Collision_DashThroughContestsNothing()
        {
            // Passing through an occupied cell is not a landing (§7.1), and
            // the dash is placement besides (§7.4): the path enemy keeps its
            // cell and the caster does not bounce.
            Use(_target);

            Assert.That(CellOf(_pathEnemy), Is.EqualTo(CellRef.Track(12)), "nobody is displaced");
            Assert.That(CellOf(_sanity), Is.EqualTo(CellRef.Track(15)), "and nobody bounces");
        }

        [Test]
        public void Collision_OnAnAllyTarget_IsAPureMobilityAnchor()
        {
            // The hit and the stun are enemy-audience effects, so the cast-mode
            // system filters them out — the ally is untouched, the rake still
            // applies, and the caster still lands a cell past.
            _ally.MoveTo(ProgressAtTrack(PlayerColor.Red, 14));
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 20));
            _bystander.MoveTo(ProgressAtTrack(PlayerColor.Blue, 24));

            var result = Use(_ally);

            Assert.That(result.Approved, Is.True);
            Assert.That(_ally.Health, Is.EqualTo(6), "an ally anchor takes no damage");
            Assert.That(_pathEnemy.Health, Is.EqualTo(5), "the rake does not care who the anchor was");
            Assert.That(CellOf(_sanity), Is.EqualTo(CellRef.Track(15)));

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_statuses.IsStunned(_ally), Is.False, "and no stun");
        }

        [Test]
        public void Collision_DashesBackwards_AgainstTheRaceDirection()
        {
            // Range is a distance in either direction (§4.1), so a target
            // behind the caster is dashed to against the direction of travel —
            // the escape the rest of the kit refuses to give him.
            var behind = AtTrack(6, "Behind", PlayerColor.Blue, 6, 6);
            var behindPath = AtTrack(7, "BehindPath", PlayerColor.Blue, 6, 8);
            _board.Add(behind);
            _board.Add(behindPath);

            var result = Use(behind);

            Assert.That(result.Approved, Is.True);
            Assert.That(behind.Health, Is.EqualTo(3));
            Assert.That(behindPath.Health, Is.EqualTo(5), "the rake runs whichever way the dash does");
            Assert.That(_pathEnemy.Health, Is.EqualTo(6), "ahead of the caster, off the line");
            Assert.That(CellOf(_sanity), Is.EqualTo(CellRef.Track(5)),
                "one step past the target, further backwards");
        }

        [Test]
        public void Collision_CountsAsPlacement_ForTheCampingRule()
        {
            // A dash moves the caster rather than the target, but it is the
            // same taxi with the seats exchanged: a sheltered engineer may not
            // dash to an ally behind himself (§4.4, second amendment).
            Assert.That(Sanity.Collision.ContainsPlacement, Is.True);

            _sanity.MoveTo(ProgressAtTrack(PlayerColor.Red, _map.StartTrackIndex(PlayerColor.Red)));
            _ally.MoveTo(ProgressAtTrack(PlayerColor.Red,
                (_map.StartTrackIndex(PlayerColor.Red) - 5 + _map.Profile.CircuitLength) % _map.Profile.CircuitLength));

            int before = _red.Energy;
            var result = Use(_ally);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.AimedBehindFromSafeCell));
            Assert.That(_red.Energy, Is.EqualTo(before), "a refusal costs nothing");
        }
    }
}
