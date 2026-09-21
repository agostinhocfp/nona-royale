// Assets/Tests/EditMode/Targeting/SanctuaryTests.cs
// NEW FILE — beside SafeCellCampingTests, the other half of §4.4.
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Targeting
{
    /// <summary>
    /// §4.4, third amendment (2026-09-21): safe ground takes no damage, and an
    /// operator's own spawn cell refuses slow and stun as well.
    /// </summary>
    /// <remarks>
    /// <b>Driven through the real services, never a stub.</b> The rule's whole
    /// claim is that nothing leaks — not an area, not Atomic, not an execute,
    /// not a beacon's status — so each test goes through the path a match
    /// would, with one <see cref="SanctuaryRules"/> shared the way
    /// <c>MatchFactory</c> shares it.
    ///
    /// <b>Positions are derived, never hardcoded</b>, the lesson
    /// <see cref="SafeCellCampingTests"/> records: start cells come from
    /// <see cref="PathMap.StartTrackIndex"/> and are converted to per-colour
    /// progress, so a board-family change moves the tests with it or fails
    /// them at setup.
    /// </remarks>
    [TestFixture]
    public class SanctuaryTests
    {
        private PathMap _map;
        private MatchClock _clock;
        private SanctuaryRules _sanctuary;
        private StatusRegistry _statuses;
        private DamagePipeline _damage;
        private TargetingRules _targeting;
        private AbilityResolver _abilities;

        private PlayerState _red;
        private PlayerState _blue;
        private List<OperatorState> _operators;

        private OperatorState _caster;
        private OperatorState _deployer;
        private OperatorState _visitor;

        private int _circuit;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _circuit = _map.Profile.CircuitLength;

            _caster = new OperatorState(1, "Blaster", PlayerColor.Red, 10, 1.0);
            _deployer = new OperatorState(2, "Deployer", PlayerColor.Blue, 10, 1.0);
            _visitor = new OperatorState(3, "Visitor", PlayerColor.Blue, 10, 1.0);

            _red = new PlayerState(PlayerColor.Red, new[] { _caster });
            _blue = new PlayerState(PlayerColor.Blue, new[] { _deployer, _visitor });
            _operators = new List<OperatorState> { _caster, _deployer, _visitor };

            _clock = new MatchClock(new[] { _red, _blue });
            _sanctuary = new SanctuaryRules(_map);
            _statuses = new StatusRegistry(_clock, CombatConfig.Default, sanctuary: _sanctuary);
            _damage = new DamagePipeline(_statuses, new SeededRandom(1), sanctuary: _sanctuary);
            _targeting = new TargetingRules(_map, _statuses);

            var energy = new EnergyLedger(EnergyConfig.Default);
            var cellEffects = new DeferredCellEffects(_clock, _targeting, _damage, _statuses);
            _abilities = new AbilityResolver(
                _map, _clock, energy, _statuses, _targeting, _damage, cellEffects);

            _clock.BeginTurnFor(_red);

            // The deployer on its own start cell; the visitor on Red's, which is
            // safe ground but nobody's spawn cell as far as Blue is concerned.
            _deployer.MoveTo(0);
            _visitor.MoveTo(ProgressAtTrack(PlayerColor.Blue, _map.StartTrackIndex(PlayerColor.Red)));
            _caster.MoveTo(ExposedProgress(PlayerColor.Red));
        }

        // ── Where the shelter is ─────────────────────────────────────────

        [Test]
        public void OwnStartCell_IsSpawnCell_AndSheltered()
        {
            Assert.That(_sanctuary.IsOnOwnSpawnCell(_deployer), Is.True);
            Assert.That(_sanctuary.Shelters(_deployer), Is.True);
        }

        [Test]
        public void AnotherSeatsStartCell_IsSheltered_ButNotASpawnCell()
        {
            Assert.That(_map.IsSafe(_targeting.CellOf(_visitor)), Is.True, "fixture: Red's start is safe");
            Assert.That(_sanctuary.Shelters(_visitor), Is.True);
            Assert.That(_sanctuary.IsOnOwnSpawnCell(_visitor), Is.False);
        }

        [Test]
        public void HomeColumnMouth_IsSheltered()
        {
            _deployer.MoveTo(_map.Profile.TrackLength);
            Assert.That(_map.IsSafe(_map.CellAt(_deployer.Owner, _deployer.Progress)), Is.True,
                "fixture: the first home-column cell is safe (ADR-0003)");
            Assert.That(_sanctuary.Shelters(_deployer), Is.True);
        }

        [Test]
        public void AnOrdinaryCell_IsNeither()
        {
            Assert.That(_sanctuary.Shelters(_caster), Is.False);
            Assert.That(_sanctuary.IsOnOwnSpawnCell(_caster), Is.False);
        }

        [Test]
        public void TheYard_IsNeither()
        {
            _deployer.MoveTo(PathMap.YardProgress);
            Assert.That(_sanctuary.Shelters(_deployer), Is.False);
            Assert.That(_sanctuary.IsOnOwnSpawnCell(_deployer), Is.False);
        }

        // ── Damage on safe ground ────────────────────────────────────────

        [Test]
        public void ANormalHit_OnSafeGround_IsSheltered_AndHealthIsUntouched()
        {
            var result = _damage.Apply(_visitor, new DamageInstance(4, DamageType.Normal, 0, "test"));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Sheltered));
            Assert.That(result.AmountApplied, Is.EqualTo(0));
            Assert.That(_visitor.Health, Is.EqualTo(10));
        }

        [Test]
        public void AnAtomicHit_OnSafeGround_IsShelteredToo()
        {
            // The shelter is above the Atomic gate, not one of the layers it
            // pierces: bleed and mark ticks are Atomic, and they must not leak.
            var result = _damage.Apply(_visitor, new DamageInstance(4, DamageType.Atomic, 0, "test"));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Sheltered));
            Assert.That(_visitor.Health, Is.EqualTo(10));
        }

        [Test]
        public void AShelteredHit_SpendsNoShield()
        {
            _statuses.Apply(_visitor, StatusKind.Shield, 3, magnitude: 3);
            int pool = _statuses.ShieldPool(_visitor);

            _damage.Apply(_visitor, new DamageInstance(2, DamageType.Normal, 0, "test"));

            Assert.That(_statuses.ShieldPool(_visitor), Is.EqualTo(pool), "nothing arrived to absorb");
        }

        [Test]
        public void TheSameHit_OffSafeGround_Lands()
        {
            _visitor.MoveTo(_visitor.Progress + 1);
            Assert.That(_sanctuary.Shelters(_visitor), Is.False);

            var result = _damage.Apply(_visitor, new DamageInstance(4, DamageType.Normal, 0, "test"));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(_visitor.Health, Is.EqualTo(6));
        }

        [Test]
        public void SelfDamage_OnSafeGround_StillLands()
        {
            // A price the caster chose, not damage received (§2.3). A shelter
            // that waived it would make All-In Mauling free on a start cell.
            _damage.ApplyToSelf(_deployer, 2);
            Assert.That(_deployer.Health, Is.EqualTo(8));
        }

        [Test]
        public void WithoutASanctuary_ThePipelineIsAsItWas()
        {
            var bare = new DamagePipeline(_statuses, new SeededRandom(1));

            var result = bare.Apply(_visitor, new DamageInstance(4, DamageType.Normal, 0, "test"));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
        }

        // ── Slow and stun on the spawn cell ──────────────────────────────

        [Test]
        public void ASlow_OnItsOwnSpawnCell_IsRefused()
        {
            Assert.That(_statuses.Apply(_deployer, StatusKind.Slow, 2), Is.False);
            Assert.That(_statuses.HasOnNextTurn(_deployer, StatusKind.Slow), Is.False);
        }

        [Test]
        public void AStun_OnItsOwnSpawnCell_IsRefused()
        {
            Assert.That(_statuses.Apply(_deployer, StatusKind.Stun, 1), Is.False);
            Assert.That(_statuses.HasOnNextTurn(_deployer, StatusKind.Stun), Is.False);
        }

        [Test]
        public void ASlow_OnSomeoneElsesStartCell_StillLands()
        {
            // Safe ground shelters from damage; only your own spawn cell
            // shelters from control.
            Assert.That(_statuses.Apply(_visitor, StatusKind.Slow, 2), Is.True);
            Assert.That(_statuses.HasOnNextTurn(_visitor, StatusKind.Slow), Is.True);
        }

        [Test]
        public void OtherStatuses_StillLandOnTheSpawnCell()
        {
            Assert.That(_statuses.Apply(_deployer, StatusKind.Bleed, 1), Is.True);
            Assert.That(_statuses.Apply(_deployer, StatusKind.Mark, 2), Is.True);
        }

        [Test]
        public void ASlowAlreadyHeld_IsKept_WhenPlacedBackOnTheSpawnCell()
        {
            // The rule refuses a new slow; it is not a cleanse.
            _deployer.MoveTo(1);
            _clock.BeginTurnFor(_blue);
            Assert.That(_statuses.Apply(_deployer, StatusKind.Slow, 3), Is.True);

            _deployer.MoveTo(0);

            Assert.That(_statuses.Has(_deployer, StatusKind.Slow), Is.True);
        }

        // ── Through the resolver ─────────────────────────────────────────

        [Test]
        public void AnAreaBlast_ThatCatchesTheSpawnCell_NeitherHurtsNorSlowsIt_AndSaysSo()
        {
            _caster.MoveTo(ProgressAtTrack(PlayerColor.Red, TrackAhead(_map.StartTrackIndex(PlayerColor.Blue), 1)));

            var resolution = _abilities.Use(_caster, FrostBlast(radius: 1), null, _red, _operators);

            Assert.That(resolution.Approved, Is.True, resolution.Refusal.ToString());
            Assert.That(_deployer.Health, Is.EqualTo(10));
            Assert.That(_statuses.HasOnNextTurn(_deployer, StatusKind.Slow), Is.False);

            var mine = resolution.Outcomes.Where(o => o.Recipient == _deployer).ToList();
            Assert.That(mine.Any(o => o.Kind == EffectOutcomeKind.StatusApplied), Is.False,
                "a refused slow must not be reported as landed");
            Assert.That(mine.Single(o => o.Kind == EffectOutcomeKind.Damaged).Damage.Outcome,
                Is.EqualTo(DamageOutcome.Sheltered));
        }

        [Test]
        public void AnAreaBlast_OnAVisitorToAStartCell_SlowsButDoesNotHurt()
        {
            _caster.MoveTo(ProgressAtTrack(PlayerColor.Red, TrackAhead(_map.StartTrackIndex(PlayerColor.Red), 1)));

            var resolution = _abilities.Use(_caster, FrostBlast(radius: 1), null, _red, _operators);

            Assert.That(resolution.Approved, Is.True, resolution.Refusal.ToString());
            Assert.That(_visitor.Health, Is.EqualTo(10), "safe ground");
            Assert.That(_statuses.HasOnNextTurn(_visitor, StatusKind.Slow), Is.True, "but not its spawn cell");
        }

        // ── Through a whole match ────────────────────────────────────────

        [Test]
        public void ABleed_ResolvingOnSafeGround_IsSpent_AndDealsNothing()
        {
            // The consequence worth pinning: bleed is delayed damage, consumed
            // whole at the holder's upkeep (§5.3). Resolving on safe ground, it
            // is spent and voided — ending a turn on a start cell shrugs it off.
            var match = MatchFactory.CreateAlphaMatch(new[] { PlayerColor.Red }, seed: 11, openingDeployments: 3);
            match.Engine.Start();
            var subject = match.Operators.First(o => o.Name == "Syla");
            var mover = match.Operators.First(o => o.Name == "Bouncer");
            Assert.That(match.Engine.IsSheltered(subject), Is.True, "opening deployment is on her start cell");

            match.Statuses.Apply(subject, StatusKind.Bleed, 2, stacks: 3);
            int before = subject.Health;

            var events = PassTurn(match.Engine, mover);

            Assert.That(subject.Health, Is.EqualTo(before));
            Assert.That(match.Statuses.IsBleeding(subject), Is.False, "the bleed is spent either way");
            Assert.That(events.OfType<DamageSheltered>().Any(e => e.Target == subject), Is.True);
        }

        [Test]
        public void TheSameBleed_OffSafeGround_Lands()
        {
            var match = MatchFactory.CreateAlphaMatch(new[] { PlayerColor.Red }, seed: 11, openingDeployments: 3);
            match.Engine.Start();
            var subject = match.Operators.First(o => o.Name == "Syla");
            var mover = match.Operators.First(o => o.Name == "Bouncer");

            subject.MoveTo(Enumerable.Range(1, match.Map.Profile.TrackLength - 1)
                .First(p => !match.Map.IsSafe(match.Map.CellAt(PlayerColor.Red, p))));
            Assert.That(match.Engine.IsSheltered(subject), Is.False);

            match.Statuses.Apply(subject, StatusKind.Bleed, 2, stacks: 3);
            int before = subject.Health;

            PassTurn(match.Engine, mover);

            Assert.That(subject.Health, Is.LessThan(before));
        }

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>A caster-centred area that slows and hurts: the shape the rule has to stop.</summary>
        private static AbilityDefinition FrostBlast(int radius) =>
            new AbilityDefinition(
                id: 99951, name: "Test Frost", description: "Test double: area slow and damage.",
                energyCost: 0, cooldownTurns: 0, range: 0,
                effects: new[]
                {
                    AbilityEffect.Status_(EffectScope.EnemiesAroundCaster, StatusKind.Slow, 2, radius: radius),
                    AbilityEffect.Damage(EffectScope.EnemiesAroundCaster, 3, DamageType.Normal, radius: radius)
                },
                targeting: AbilityTargeting.None);

        /// <summary>Rolls, spends the whole roll on the mover, and ends the turn. Returns the handover's events.</summary>
        private static IReadOnlyList<IGameEvent> PassTurn(GameEngine engine, OperatorState mover)
        {
            engine.Execute(new RollDiceCommand());
            engine.Execute(new MoveCommand(mover.Id));

            while (engine.CanRollAgain)
            {
                engine.Execute(new RollDiceCommand());
                engine.Execute(new MoveCommand(mover.Id));
            }

            var events = engine.Execute(new EndTurnCommand());
            Assert.That(events.OfType<CommandRejected>(), Is.Empty, "the turn should hand over");
            return events;
        }

        private int ExposedProgress(PlayerColor owner)
        {
            for (int p = 1; p < _circuit; p++)
            {
                if (!_map.IsOnOuterTrack(p)) continue;
                if (!_map.IsSafe(_map.CellAt(owner, p))) return p;
            }

            Assert.Fail("No exposed cell — has the board family changed?");
            return -1;
        }

        private int TrackAhead(int track, int steps) => (track + steps) % _circuit;

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
    }
}
