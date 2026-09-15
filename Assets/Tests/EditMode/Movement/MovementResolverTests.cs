// Assets/Tests/EditMode/Movement/MovementResolverTests.cs
using System;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Movement
{
    [TestFixture]
    public class MovementResolverTests
    {
        private const int Bouncer = 1;
        private const int Syla = 2;

        private PathMap _map;
        private GameConfig _config;
        private MovementResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _config = GameConfig.Default;
            _resolver = new MovementResolver(_map, _config);
        }

        private static OperatorState Tank() =>
            new OperatorState(Bouncer, "Bouncer", PlayerColor.Red, 12, 1.5);

        private static OperatorState Assassin() =>
            new OperatorState(Syla, "Syla", PlayerColor.Red, 6, 2.0);

        // Progress values near the end of the journey are derived from the
        // profile. They were literals for the 48-cell board (46, 49, 50, 52)
        // and silently stopped testing the home column when Standard became
        // 52/6 (ADR-0002 Amendment 6): 50 is still on the outer track now.
        private static int TrackLength => BoardProfile.Standard.TrackLength;
        private static int Journey => BoardProfile.Standard.Journey;

        private static OperatorState Deployed(OperatorState op, int progress)
        {
            op.MoveTo(progress);
            return op;
        }

        // ── Dice to cells ────────────────────────────────────────────────

        [Test]
        public void CellsMoved_IsTheDiceTotalTimesSpeed()
        {
            Assert.That(_resolver.CellsFor(8, 1.5), Is.EqualTo(12));
            Assert.That(_resolver.CellsFor(8, 2.0), Is.EqualTo(16));
        }

        [Test]
        public void HalfStepSpeed_FloorsRatherThanRounding()
        {
            // 7 x 1.5 = 10.5. Floored to 10, never rounded up to 11 — a player
            // must be able to halve the roll in their head and get the answer.
            Assert.That(_resolver.CellsFor(7, 1.5), Is.EqualTo(10));
        }

        [Test]
        public void DoubleSixAtTopOfBand_IsTheLongestSingleMove()
        {
            // 12 x 2.0 = 24, under half the loop. Half the loop is the
            // readability ceiling that fixed the speed band (ADR-0002
            // Amendment 2); the band has since dropped to 1.0-1.5
            // (Amendment 4), so 2.0 is a bound on the schema, not a speed any
            // operator ships with.
            Assert.That(_resolver.CellsFor(12, 2.0), Is.EqualTo(24));
            Assert.That(_resolver.CellsFor(12, 2.0),
                Is.LessThanOrEqualTo(BoardProfile.Standard.CircuitLength / 2));
        }

        // ── Effective speed ──────────────────────────────────────────────

        [Test]
        public void SlowReducesSpeed_ByItsMagnitude()
        {
            Assert.That(_resolver.EffectiveSpeed(2.0, -0.5), Is.EqualTo(1.5));
        }

        [Test]
        public void PassiveBonusRaisesSpeed()
        {
            // Kurbyn: 1.5 base, +0.5 from Evasive Protocol.
            Assert.That(_resolver.EffectiveSpeed(1.5, 0.5), Is.EqualTo(2.0));
        }

        [Test]
        public void StackedSlows_FloorAtTheMinimumMultiplier()
        {
            // Never zero. A zero multiplier is a stun, and stun is a separate
            // mechanic an opponent has to apply deliberately.
            Assert.That(_resolver.EffectiveSpeed(1.5, -3.0), Is.EqualTo(_config.MinSpeedMultiplier));
            Assert.That(_resolver.EffectiveSpeed(1.0, -1.0), Is.EqualTo(0.5));
        }

        [Test]
        public void AnOperatorAtTheSpeedFloor_StillMoves()
        {
            Assert.That(_resolver.CellsFor(6, 0.5), Is.EqualTo(3));
        }

        [Test]
        public void BelowFullSpeed_HalfCellsRoundUp()
        {
            // Sanity's rule (2026-09-15): below 1.0x the floor taxed every odd
            // die a second time. A 5 moves 3, a 7 moves 4 — the half is his.
            Assert.That(_resolver.CellsFor(5, 0.5), Is.EqualTo(3));
            Assert.That(_resolver.CellsFor(7, 0.5), Is.EqualTo(4));
        }

        [Test]
        public void AtOrAboveFullSpeed_HalfCellsStillFloor()
        {
            // The round-up is scoped below 1.0x: 1.5x keeps the documented
            // floor, so Syla and Javi move exactly as before the amendment.
            Assert.That(_resolver.CellsFor(7, 1.5), Is.EqualTo(10));
            Assert.That(_resolver.CellsFor(5, 1.5), Is.EqualTo(7));
        }

        [Test]
        public void SlowedOperatorAtTheFloor_AlsoRoundsUp()
        {
            // The rule is general, not Sanity's: anyone slowed to 0.5x gets
            // the same grace on an odd die.
            Assert.That(_resolver.CellsFor(3, 0.5), Is.EqualTo(2));
        }

        // ── Deploy ───────────────────────────────────────────────────────

        [Test]
        public void RollContainingSix_DeploysAndLeavesOtherDieAsMovement()
        {
            var option = _resolver.GetDeployOption(new DiceRoll(6, 3), operatorsInYard: 3);

            Assert.That(option.IsAvailable, Is.True);
            Assert.That(option.MaxOperators, Is.EqualTo(1));
            Assert.That(option.MovementAfterDeploying(1), Is.EqualTo(3));
        }

        [Test]
        public void DeployIsOptional_DecliningKeepsTheFullTotal()
        {
            var option = _resolver.GetDeployOption(new DiceRoll(6, 3), operatorsInYard: 3);

            Assert.That(option.MovementAfterDeploying(0), Is.EqualTo(9));
            Assert.That(option.TotalIfDeclined, Is.EqualTo(9));
        }

        [Test]
        public void DoubleSix_DeploysTwoAndForfeitsMovement()
        {
            var option = _resolver.GetDeployOption(new DiceRoll(6, 6), operatorsInYard: 3);

            Assert.That(option.MaxOperators, Is.EqualTo(2));
            Assert.That(option.MovementAfterDeploying(2), Is.EqualTo(0));
        }

        [Test]
        public void DoubleSixWithOneOperatorWaiting_DeploysOneAndKeepsTheOtherSix()
        {
            // Each deploy consumes one die. Consuming both to deploy a single
            // operator would make a double 6 strictly worse than a single 6.
            var option = _resolver.GetDeployOption(new DiceRoll(6, 6), operatorsInYard: 1);

            Assert.That(option.MaxOperators, Is.EqualTo(1));
            Assert.That(option.MovementAfterDeploying(1), Is.EqualTo(6));
        }

        [Test]
        public void RollWithoutSix_CannotDeploy()
        {
            var option = _resolver.GetDeployOption(new DiceRoll(5, 4), operatorsInYard: 3);

            Assert.That(option.IsAvailable, Is.False);
            Assert.That(option.MaxOperators, Is.EqualTo(0));
            Assert.That(option.TotalIfDeclined, Is.EqualTo(9));
        }

        [Test]
        public void EmptyYard_OffersNoDeployEvenOnASix()
        {
            var option = _resolver.GetDeployOption(new DiceRoll(6, 6), operatorsInYard: 0);

            Assert.That(option.IsAvailable, Is.False);
            Assert.That(option.TotalIfDeclined, Is.EqualTo(12));
        }

        [Test]
        public void DeployingMoreThanTheRollAllows_IsRejected()
        {
            var option = _resolver.GetDeployOption(new DiceRoll(6, 2), operatorsInYard: 3);

            Assert.Throws<ArgumentOutOfRangeException>(() => option.MovementAfterDeploying(2));
        }

        [Test]
        public void ADeployedOperator_StandsOnItsOwnStartCell()
        {
            var op = Deployed(Tank(), _resolver.DeployProgress);

            Assert.That(_map.CellAt(op.Owner, op.Progress),
                Is.EqualTo(CellRef.Track(_map.StartTrackIndex(PlayerColor.Red))));
            Assert.That(_map.IsSafe(_map.CellAt(op.Owner, op.Progress)), Is.True);
        }

        // ── Moving ───────────────────────────────────────────────────────

        [Test]
        public void AMove_ReportsWhereItEndsWithoutChangingTheOperator()
        {
            // The resolver computes; the caller commits. This is what lets a
            // collision inspect the landing before anyone moves.
            var op = Deployed(Tank(), 10);

            var result = _resolver.ResolveMove(op, 5);

            Assert.That(result.To, Is.EqualTo(15));
            Assert.That(op.Progress, Is.EqualTo(10), "ResolveMove must not mutate the operator.");
        }

        [Test]
        public void AMoveOnTheLoop_CanBeContested()
        {
            var result = _resolver.ResolveMove(Deployed(Assassin(), 10), 5);

            Assert.That(result.CanBeContested, Is.True);
            Assert.That(result.Destination, Is.EqualTo(CellRef.Track(15)));
        }

        [Test]
        public void AMoveIntoTheHomeColumn_CannotBeContested()
        {
            // Home columns are out of the fight entirely (COMBAT_SYSTEMS §4.3).
            var result = _resolver.ResolveMove(Deployed(Tank(), TrackLength - 2), 4);

            Assert.That(result.To, Is.EqualTo(TrackLength + 2));
            Assert.That(result.EnteredHomeColumn, Is.True);
            Assert.That(result.CanBeContested, Is.False);
        }

        [Test]
        public void EnteringHomeColumn_IsReportedOnlyOnTheCrossingMove()
        {
            var alreadyInside = _resolver.ResolveMove(Deployed(Tank(), TrackLength + 1), 2);

            Assert.That(_map.IsInHomeColumn(TrackLength + 1), Is.True, "precondition: already inside");
            Assert.That(alreadyInside.EnteredHomeColumn, Is.False);
        }

        [Test]
        public void ReachingJourneyExactly_Finishes()
        {
            var result = _resolver.ResolveMove(Deployed(Tank(), Journey - 4), 4);

            Assert.That(result.Finished, Is.True);
            Assert.That(result.Overshot, Is.False);
            Assert.That(result.Destination, Is.EqualTo(CellRef.Home(PlayerColor.Red)));
        }

        [Test]
        public void OvershootingHome_Finishes_RatherThanBouncing()
        {
            // Home entry is automatic in the MVP and needs no exact roll.
            var result = _resolver.ResolveMove(Deployed(Tank(), TrackLength), BoardProfile.Standard.HomeColumnLength + 3);

            Assert.That(result.Finished, Is.True);
            Assert.That(result.Overshot, Is.True);
            Assert.That(result.To, Is.EqualTo(BoardProfile.Standard.Journey));
        }

        [Test]
        public void AnOperatorInTheYard_CannotMove()
        {
            Assert.Throws<InvalidOperationException>(() => _resolver.ResolveMove(Tank(), 5));
        }

        [Test]
        public void BackwardsMovement_IsRejected()
        {
            // Pulls and pushes are placement, not movement (COMBAT_SYSTEMS §7.4).
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _resolver.ResolveMove(Deployed(Tank(), 10), -1));
        }

        [Test]
        public void AFullLap_LandsOnTheHomeColumnMouth()
        {
            // The circuit is exactly one lap: CircuitLength cells from the start cell puts
            // an operator at its own column, not back on its start.
            var result = _resolver.ResolveMove(Deployed(Tank(), 0), BoardProfile.Standard.CircuitLength);

            Assert.That(result.Destination, Is.EqualTo(CellRef.HomeColumn(PlayerColor.Red, 0)));
        }

        // ── Bounce-back ──────────────────────────────────────────────────

        [Test]
        public void BounceBack_IsOneStepBackAlongTheMoversOwnPath()
        {
            Assert.That(_resolver.BounceBackProgress(15), Is.EqualTo(14));
        }

        [Test]
        public void BounceBackFromTheStartCell_IsImpossible()
        {
            // A collision can only happen on a non-safe cell, and the only cell
            // an operator can occupy right after deploying is its start cell,
            // which is safe. So this case cannot arise in play — if it ever
            // does, something upstream is wrong and should say so loudly.
            Assert.Throws<InvalidOperationException>(() => _resolver.BounceBackProgress(0));
        }
    }
}
