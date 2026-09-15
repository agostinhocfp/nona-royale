// Assets/Tests/EditMode/Collision/CollisionResolverTests.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Collision
{
    [TestFixture]
    public class CollisionResolverTests
    {
        private PathMap _map;
        private CombatConfig _combat;
        private MovementResolver _movement;
        private DamagePipeline _pipeline;
        private CollisionResolver _resolver;
        private EvasiveMitigation _mitigation;

        /// <summary>
        /// Lets a single test say "this hit is evaded" without chasing a seed.
        /// </summary>
        /// <remarks>
        /// The shield is a pool rather than a switch, matching
        /// <c>StatusRegistry.AbsorbFrom</c>: it takes what it can and keeps the
        /// rest. A collision is the largest Normal instance in the game, so a
        /// partial absorb is the common case here rather than an edge one.
        /// </remarks>
        private sealed class EvasiveMitigation : IDamageMitigation
        {
            public bool EvadeNext;
            public int ShieldPool;

            public bool TryEvade(OperatorState target, IRandom random)
            {
                if (!EvadeNext) return false;
                EvadeNext = false;
                return true;
            }

            public int AbsorbFrom(OperatorState target, int amount)
            {
                if (ShieldPool <= 0 || amount <= 0) return 0;

                int absorbed = Math.Min(ShieldPool, amount);
                ShieldPool -= absorbed;
                return absorbed;
            }

            /// <summary>Collision damage is Normal, so a tech ward never matters here.</summary>
            public bool BlocksTech(OperatorState target) => false;
        }

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _combat = CombatConfig.Default;
            _movement = new MovementResolver(_map, GameConfig.Default);
            _mitigation = new EvasiveMitigation();
            _pipeline = new DamagePipeline(_mitigation, new SeededRandom(1));
            _resolver = new CollisionResolver(_map, _combat, _pipeline, _movement);
        }

        // ── Geometry ─────────────────────────────────────────────────────
        //
        // Fixtures are stated in track cells and converted per owner, so they
        // survive a board change. These tests were written against the 48-cell
        // board with literal progress values (Blue = progress + 12) and went red
        // when Standard became 52/6 (ADR-0002 Amendment 6): every enemy stood one
        // cell away from where its comment said, so no collision happened.

        /// <summary>The contested cell most tests use. Red starts at track 0, so it is also Red's progress.</summary>
        private const int Landing = 20;

        private int Circuit => _map.Profile.CircuitLength;
        private int TrackLength => _map.Profile.TrackLength;

        /// <summary>The progress at which <paramref name="owner"/> stands on track cell <paramref name="track"/>.</summary>
        private int ProgressAtTrack(PlayerColor owner, int track) =>
            ((track - _map.StartTrackIndex(owner)) % Circuit + Circuit) % Circuit;

        private static OperatorState Op(int id, string name, PlayerColor owner, int hp, int progress)
        {
            var op = new OperatorState(id, name, owner, hp, 1.5);
            if (progress != PathMap.YardProgress) op.MoveTo(progress);
            return op;
        }

        /// <summary>Red's tank, deployed and healthy. Red starts at track 0, so progress == track index.</summary>
        private static OperatorState RedTank(int progress) =>
            Op(1, "Bouncer", PlayerColor.Red, 12, progress);

        private static OperatorState RedAssassin(int progress) =>
            Op(2, "Syla", PlayerColor.Red, 6, progress);

        /// <summary>A Blue assassin standing on the given <b>track cell</b>.</summary>
        private OperatorState BlueAssassinOn(int track) =>
            Op(3, "Kurbyn", PlayerColor.Blue, 6, ProgressAtTrack(PlayerColor.Blue, track));

        /// <summary>Any operator standing on the given <b>track cell</b>.</summary>
        private OperatorState OpOn(int id, string name, PlayerColor owner, int hp, int track) =>
            Op(id, name, owner, hp, ProgressAtTrack(owner, track));

        [Test]
        public void TheLandingCell_IsAnOrdinaryContestableCell()
        {
            // Every "a collision happens" test below leans on this. If a board
            // change ever made the landing cell safe, they would pass or fail
            // for the wrong reason.
            Assert.That(_map.IsSafe(CellRef.Track(Landing)), Is.False);
            Assert.That(_map.IsSafe(CellRef.Track(Landing - 1)), Is.False, "the bounce cell too");
            Assert.That(Landing + 10, Is.LessThan(TrackLength), "and far from Red's home column");
        }

        private CollisionResult Move(OperatorState mover, int cells, params OperatorState[] board)
        {
            var move = _movement.ResolveMove(mover, cells);
            var all = new List<OperatorState>(board) { mover };
            return _resolver.Resolve(mover, move, all);
        }

        // ── The core rule ────────────────────────────────────────────────

        [Test]
        public void LandingOnEnemy_DealsThreeNormalDamage()
        {
            // Red moves 0 -> Landing, where a Blue operator is standing.
            var mover = RedTank(0);
            var victim = BlueAssassinOn(Landing);

            var result = Move(mover, Landing, victim);

            Assert.That(result.Occurred, Is.True);
            Assert.That(result.FirstDamage.AmountApplied, Is.EqualTo(3));
            Assert.That(victim.Health, Is.EqualTo(3));
        }

        [Test]
        public void SurvivingOccupant_HoldsCellAndMoverBouncesBack()
        {
            var mover = RedTank(0);
            var victim = BlueAssassinOn(Landing);   // 6 hp, survives 3 damage
            int victimProgress = victim.Progress;

            var result = Move(mover, Landing, victim);

            Assert.That(result.MoverBouncedBack, Is.True);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(Landing - 1), "one step back along the mover's own path");
            Assert.That(victim.Progress, Is.EqualTo(victimProgress), "the occupant does not move");
        }

        [Test]
        public void NeutralizedOccupant_YieldsCellToMover()
        {
            var mover = RedTank(0);
            var victim = BlueAssassinOn(Landing);
            victim.SetHealth(3);                // one collision finishes it

            var result = Move(mover, Landing, victim);

            Assert.That(result.AllOccupantsNeutralized, Is.True);
            Assert.That(result.MoverBouncedBack, Is.False);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(Landing));
        }

        [Test]
        public void TheMoverNeverTakesDamage()
        {
            // Collision is one-directional. Running into a 12-health tank costs
            // the mover position, never health.
            var mover = RedAssassin(0);
            var victim = OpOn(4, "Bouncer", PlayerColor.Blue, 12, Landing);

            var result = Move(mover, Landing, victim);

            Assert.That(result.Occurred, Is.True, "precondition: the collision happened");
            Assert.That(mover.Health, Is.EqualTo(6));
        }

        [Test]
        public void NothingOnTheRoster_DiesToASingleCollision()
        {
            // Deliberate. Collision softens and sets up ability kills; it is not
            // a kill mechanic itself (COMBAT_SYSTEMS §7.3). If this ever goes
            // red, CollisionDamage was raised and the design should know.
            foreach (var hp in new[] { 6, 12 })
            {
                var victim = OpOn(9, "any", PlayerColor.Blue, hp, Landing);

                var result = Move(RedTank(0), Landing, victim);

                Assert.That(result.Occurred, Is.True, "precondition: the collision happened");
                Assert.That(victim.Health, Is.AtLeast(1), $"{hp} hp operator");
            }
        }

        // ── When collision does not happen ───────────────────────────────

        [Test]
        public void LandingOnEnemyOnSafeCell_DoesNotCollide()
        {
            // Blue's start cell is safe for anyone standing on it.
            int blueStart = _map.StartTrackIndex(PlayerColor.Blue);
            var mover = RedTank(0);
            var victim = BlueAssassinOn(blueStart);   // sitting on its own start

            var result = Move(mover, blueStart, victim);

            Assert.That(_map.CellAt(mover.Owner, blueStart), Is.EqualTo(_map.CellAt(victim.Owner, victim.Progress)),
                "precondition: the mover really lands on the victim's cell");
            Assert.That(result.Occurred, Is.False);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(blueStart), "both simply share the cell");
            Assert.That(victim.Health, Is.EqualTo(6));
        }

        [Test]
        public void LandingOnFriendlyOperator_DoesNotCollide()
        {
            // Friendly operators stack freely; there is no blocking in the MVP.
            var mover = RedTank(0);
            var ally = RedAssassin(Landing);

            var result = Move(mover, Landing, ally);

            Assert.That(result.Occurred, Is.False);
            Assert.That(ally.Health, Is.EqualTo(6));
        }

        [Test]
        public void PassingThroughOccupiedCell_DoesNotCollide()
        {
            // Blue sits on the landing cell; Red passes over it and lands ten further on.
            var mover = RedTank(0);
            var passedOver = BlueAssassinOn(Landing);

            var result = Move(mover, Landing + 10, passedOver);

            Assert.That(result.Occurred, Is.False);
            Assert.That(passedOver.Health, Is.EqualTo(6));
        }

        [Test]
        public void LandingInAHomeColumn_CannotCollide()
        {
            // Home columns are out of the fight entirely (§4.3), and they are
            // private anyway — no enemy can be standing there.
            // Two cells short of the column, moving four: the move ends two
            // cells inside it. A Blue operator stands on the track cell a wrap
            // would have reached, so a bug that kept counting round the loop
            // would collide.
            var mover = RedTank(TrackLength - 2);

            var result = Move(mover, 4, BlueAssassinOn(2));

            Assert.That(_map.IsInHomeColumn(TrackLength + 2), Is.True, "precondition: the move ends inside the column");
            Assert.That(result.Occurred, Is.False);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(TrackLength + 2));
        }

        [Test]
        public void AnOperatorInItsHomeColumn_CannotBeCollidedWith()
        {
            // Blue is two cells inside its own column. Red landing anywhere on
            // the track must not reach it.
            var mover = RedTank(0);
            var sheltered = Op(3, "Kurbyn", PlayerColor.Blue, 6, TrackLength + 2);

            Assert.That(_map.IsInHomeColumn(sheltered.Progress), Is.True, "precondition");

            var result = Move(mover, Landing, sheltered);

            Assert.That(result.Occurred, Is.False);
            Assert.That(sheltered.Health, Is.EqualTo(6));
        }

        [Test]
        public void AnOperatorInTheYard_IsNotOnTheBoard()
        {
            var mover = RedTank(0);
            var undeployed = Op(3, "Kurbyn", PlayerColor.Blue, 6, PathMap.YardProgress);

            var result = Move(mover, Landing, undeployed);

            Assert.That(result.Occurred, Is.False);
        }

        // ── Mitigation and placement ─────────────────────────────────────

        [Test]
        public void EvadedCollisionDamage_StillBouncesMoverBack()
        {
            // Evasion negates damage, never movement (§5.5). The occupant
            // survived, so it holds the cell — how it survived is irrelevant.
            var mover = RedTank(0);
            var victim = BlueAssassinOn(Landing);
            victim.SetHealth(3);                // would die if the hit landed
            _mitigation.EvadeNext = true;

            var result = Move(mover, Landing, victim);

            Assert.That(result.FirstDamage.Outcome, Is.EqualTo(DamageOutcome.Evaded));
            Assert.That(victim.Health, Is.EqualTo(3), "no health lost");
            Assert.That(result.MoverBouncedBack, Is.True);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(Landing - 1));
        }

        [Test]
        public void ShieldedOccupant_AbsorbsPartOfTheCollisionAndStillHoldsTheCell()
        {
            // A collision is 3 and a Trauma Plate pool is 2, so the common case
            // under the pool rule is a partial absorb — the shield blunts the
            // hit rather than erasing it (§5.6). Before the rework this instance
            // was swallowed whole regardless of its size.
            var mover = RedTank(0);
            var victim = BlueAssassinOn(Landing);
            _mitigation.ShieldPool = 2;

            var result = Move(mover, Landing, victim);

            Assert.That(result.FirstDamage.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(result.FirstDamage.AmountApplied, Is.EqualTo(1));
            Assert.That(result.FirstDamage.AmountMitigated, Is.EqualTo(2));
            Assert.That(victim.Health, Is.EqualTo(5));
            Assert.That(_mitigation.ShieldPool, Is.EqualTo(0), "the pool is spent by what it ate");
            Assert.That(result.MoverBouncedBack, Is.True);
        }

        [Test]
        public void BounceBack_DoesNotTriggerSecondCollision()
        {
            // Two Blue operators, on the landing cell and the one before it.
            // Red lands, bounces back one — and must not strike the operator
            // standing there. Bounce-back is placement, not movement (§7.2).
            var mover = RedTank(0);
            var struck = BlueAssassinOn(Landing);
            var behind = OpOn(4, "Bouncer", PlayerColor.Blue, 12, Landing - 1);

            var result = Move(mover, Landing, struck, behind);

            Assert.That(result.MoverFinalProgress, Is.EqualTo(Landing - 1));
            Assert.That(behind.Health, Is.EqualTo(12), "the bounce destination is untouched");
            Assert.That(struck.Health, Is.EqualTo(3), "only the contested cell resolved");
        }

        // ── Invariants ───────────────────────────────────────────────────

        [Test]
        public void LandingOnAStackOfEnemies_StrikesEveryOneOfThem()
        {
            // A contested cell can hold several enemies: friendly operators
            // stack freely (§4.5), and bounce-back and pulls are placement that
            // never collides. The mover hits all of them.
            var mover = RedTank(0);
            var first = BlueAssassinOn(Landing);
            var second = OpOn(5, "Syla", PlayerColor.Green, 6, Landing);   // a second seat, same cell

            var result = Move(mover, Landing, first, second);

            Assert.That(result.Occurred, Is.True);
            Assert.That(result.Occupants.Count, Is.EqualTo(2));
            Assert.That(first.Health, Is.EqualTo(3));
            Assert.That(second.Health, Is.EqualTo(3));
        }

        [Test]
        public void AStackWithAnySurvivor_HoldsTheCell()
        {
            var mover = RedTank(0);
            var dying = BlueAssassinOn(Landing);
            dying.SetHealth(3);                                      // dies to the collision
            var survivor = OpOn(5, "Bouncer", PlayerColor.Green, 12, Landing);

            var result = Move(mover, Landing, dying, survivor);

            Assert.That(result.Occurred, Is.True, "precondition: the collision happened");
            Assert.That(result.MoverBouncedBack, Is.True);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(Landing - 1));
        }

        [Test]
        public void AStackWipedOut_YieldsTheCell()
        {
            var mover = RedTank(0);
            var first = BlueAssassinOn(Landing);
            first.SetHealth(3);
            var second = OpOn(5, "Syla", PlayerColor.Green, 6, Landing);
            second.SetHealth(2);

            var result = Move(mover, Landing, first, second);

            Assert.That(result.Occurred, Is.True, "precondition: the collision happened");
            Assert.That(result.MoverBouncedBack, Is.False);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(Landing));
        }

        [Test]
        public void AMoveWithNobodyElseOnTheBoard_ResolvesCleanly()
        {
            var result = Move(RedTank(0), Landing);

            Assert.That(result.Occurred, Is.False);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(Landing));
            Assert.That(result.Occupant, Is.Null);
        }

        [Test]
        public void ResolvingDoesNotMoveTheMover()
        {
            // The resolver decides; the caller commits.
            var mover = RedTank(0);

            var result = Move(mover, Landing, BlueAssassinOn(Landing));

            Assert.That(mover.Progress, Is.EqualTo(0));
            Assert.That(result.MoverFinalProgress, Is.EqualTo(Landing - 1));
        }
    }
}
