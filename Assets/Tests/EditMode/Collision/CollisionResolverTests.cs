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

        /// <summary>Blue starts at track 12, so its track cell is progress + 12.</summary>
        private static OperatorState BlueAssassin(int progress) =>
            Op(3, "Kurbyn", PlayerColor.Blue, 6, progress);

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
            // Red moves 0 -> 20. Blue sits at progress 8, which is track 20.
            var mover = RedTank(0);
            var victim = BlueAssassin(8);

            var result = Move(mover, 20, victim);

            Assert.That(result.Occurred, Is.True);
            Assert.That(result.FirstDamage.AmountApplied, Is.EqualTo(3));
            Assert.That(victim.Health, Is.EqualTo(3));
        }

        [Test]
        public void SurvivingOccupant_HoldsCellAndMoverBouncesBack()
        {
            var mover = RedTank(0);
            var victim = BlueAssassin(8);       // 6 hp, survives 3 damage

            var result = Move(mover, 20, victim);

            Assert.That(result.MoverBouncedBack, Is.True);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(19), "one step back along the mover's own path");
            Assert.That(victim.Progress, Is.EqualTo(8), "the occupant does not move");
        }

        [Test]
        public void NeutralizedOccupant_YieldsCellToMover()
        {
            var mover = RedTank(0);
            var victim = BlueAssassin(8);
            victim.SetHealth(3);                // one collision finishes it

            var result = Move(mover, 20, victim);

            Assert.That(result.AllOccupantsNeutralized, Is.True);
            Assert.That(result.MoverBouncedBack, Is.False);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(20));
        }

        [Test]
        public void TheMoverNeverTakesDamage()
        {
            // Collision is one-directional. Running into a 12-health tank costs
            // the mover position, never health.
            var mover = RedAssassin(0);
            var victim = Op(4, "Bouncer", PlayerColor.Blue, 12, 8);

            Move(mover, 20, victim);

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
                var victim = Op(9, "any", PlayerColor.Blue, hp, 8);

                Move(RedTank(0), 20, victim);

                Assert.That(victim.Health, Is.AtLeast(1), $"{hp} hp operator");
            }
        }

        // ── When collision does not happen ───────────────────────────────

        [Test]
        public void LandingOnEnemyOnSafeCell_DoesNotCollide()
        {
            // Track 12 is Blue's start cell, and safe for anyone standing on it.
            var mover = RedTank(0);
            var victim = BlueAssassin(0);       // sitting on its own start, track 12

            var result = Move(mover, 12, victim);

            Assert.That(result.Occurred, Is.False);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(12), "both simply share the cell");
            Assert.That(victim.Health, Is.EqualTo(6));
        }

        [Test]
        public void LandingOnFriendlyOperator_DoesNotCollide()
        {
            // Friendly operators stack freely; there is no blocking in the MVP.
            var mover = RedTank(0);
            var ally = RedAssassin(20);

            var result = Move(mover, 20, ally);

            Assert.That(result.Occurred, Is.False);
            Assert.That(ally.Health, Is.EqualTo(6));
        }

        [Test]
        public void PassingThroughOccupiedCell_DoesNotCollide()
        {
            // Blue sits on track 20; Red passes over it and lands on track 30.
            var mover = RedTank(0);
            var passedOver = BlueAssassin(8);

            var result = Move(mover, 30, passedOver);

            Assert.That(result.Occurred, Is.False);
            Assert.That(passedOver.Health, Is.EqualTo(6));
        }

        [Test]
        public void LandingInAHomeColumn_CannotCollide()
        {
            // Home columns are out of the fight entirely (§4.3), and they are
            // private anyway — no enemy can be standing there.
            var mover = RedTank(46);

            var result = Move(mover, 4, BlueAssassin(8));

            Assert.That(result.Occurred, Is.False);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(50));
        }

        [Test]
        public void AnOperatorInItsHomeColumn_CannotBeCollidedWith()
        {
            // Blue at progress 50 is inside its own column. Red landing on the
            // track cell that would otherwise be shared must not reach it.
            var mover = RedTank(0);
            var sheltered = Op(3, "Kurbyn", PlayerColor.Blue, 6, 50);

            var result = Move(mover, 20, sheltered);

            Assert.That(result.Occurred, Is.False);
            Assert.That(sheltered.Health, Is.EqualTo(6));
        }

        [Test]
        public void AnOperatorInTheYard_IsNotOnTheBoard()
        {
            var mover = RedTank(0);
            var undeployed = Op(3, "Kurbyn", PlayerColor.Blue, 6, PathMap.YardProgress);

            var result = Move(mover, 20, undeployed);

            Assert.That(result.Occurred, Is.False);
        }

        // ── Mitigation and placement ─────────────────────────────────────

        [Test]
        public void EvadedCollisionDamage_StillBouncesMoverBack()
        {
            // Evasion negates damage, never movement (§5.5). The occupant
            // survived, so it holds the cell — how it survived is irrelevant.
            var mover = RedTank(0);
            var victim = BlueAssassin(8);
            victim.SetHealth(3);                // would die if the hit landed
            _mitigation.EvadeNext = true;

            var result = Move(mover, 20, victim);

            Assert.That(result.FirstDamage.Outcome, Is.EqualTo(DamageOutcome.Evaded));
            Assert.That(victim.Health, Is.EqualTo(3), "no health lost");
            Assert.That(result.MoverBouncedBack, Is.True);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(19));
        }

        [Test]
        public void ShieldedOccupant_AbsorbsPartOfTheCollisionAndStillHoldsTheCell()
        {
            // A collision is 3 and a Trauma Plate pool is 2, so the common case
            // under the pool rule is a partial absorb — the shield blunts the
            // hit rather than erasing it (§5.6). Before the rework this instance
            // was swallowed whole regardless of its size.
            var mover = RedTank(0);
            var victim = BlueAssassin(8);
            _mitigation.ShieldPool = 2;

            var result = Move(mover, 20, victim);

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
            // Two Blue operators, on track 19 and track 20. Red lands on 20,
            // bounces to 19 — and must not strike the operator standing there.
            // Bounce-back is placement, not movement (§7.2).
            var mover = RedTank(0);
            var struck = BlueAssassin(8);       // track 20
            var behind = Op(4, "Bouncer", PlayerColor.Blue, 12, 7);  // track 19

            var result = Move(mover, 20, struck, behind);

            Assert.That(result.MoverFinalProgress, Is.EqualTo(19));
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
            var first = BlueAssassin(8);                             // track 20
            var second = Op(5, "Syla", PlayerColor.Green, 6, 44);    // green starts at 24 -> track 20

            var result = Move(mover, 20, first, second);

            Assert.That(result.Occurred, Is.True);
            Assert.That(result.Occupants.Count, Is.EqualTo(2));
            Assert.That(first.Health, Is.EqualTo(3));
            Assert.That(second.Health, Is.EqualTo(3));
        }

        [Test]
        public void AStackWithAnySurvivor_HoldsTheCell()
        {
            var mover = RedTank(0);
            var dying = BlueAssassin(8);
            dying.SetHealth(3);                                      // dies to the collision
            var survivor = Op(5, "Bouncer", PlayerColor.Green, 12, 44);

            var result = Move(mover, 20, dying, survivor);

            Assert.That(result.MoverBouncedBack, Is.True);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(19));
        }

        [Test]
        public void AStackWipedOut_YieldsTheCell()
        {
            var mover = RedTank(0);
            var first = BlueAssassin(8);
            first.SetHealth(3);
            var second = Op(5, "Syla", PlayerColor.Green, 6, 44);
            second.SetHealth(2);

            var result = Move(mover, 20, first, second);

            Assert.That(result.MoverBouncedBack, Is.False);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(20));
        }

        [Test]
        public void AMoveWithNobodyElseOnTheBoard_ResolvesCleanly()
        {
            var result = Move(RedTank(0), 20);

            Assert.That(result.Occurred, Is.False);
            Assert.That(result.MoverFinalProgress, Is.EqualTo(20));
            Assert.That(result.Occupant, Is.Null);
        }

        [Test]
        public void ResolvingDoesNotMoveTheMover()
        {
            // The resolver decides; the caller commits.
            var mover = RedTank(0);

            var result = Move(mover, 20, BlueAssassin(8));

            Assert.That(mover.Progress, Is.EqualTo(0));
            Assert.That(result.MoverFinalProgress, Is.EqualTo(19));
        }
    }
}