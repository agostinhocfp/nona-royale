// Assets/Tests/EditMode/Text/CoachTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NonaRoyale.Core.Text;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Text
{
    /// <summary>
    /// First-match tips (<see cref="Coach"/>, LAUNCH_UI_PASS.md G10a): each has
    /// words, each fires for a person and never for a CPU, a seen tip stays
    /// seen, and moments are read from the events the view presented.
    /// </summary>
    [TestFixture]
    public class CoachTests
    {
        private static readonly PlayerColor[] TwoSeats = { PlayerColor.Red, PlayerColor.Blue };

        private static readonly Func<PlayerColor, bool> Everyone = _ => true;
        private static readonly Func<PlayerColor, bool> Nobody = _ => false;
        private static readonly Func<CoachTip, bool> NoneSeen = _ => false;

        private static MatchFactory.Match Started()
        {
            var match = MatchFactory.CreateAlphaMatch(TwoSeats, seed: 7);
            match.Engine.Start();
            return match;
        }

        private static OperatorState OperatorOf(MatchFactory.Match match, PlayerColor seat) =>
            match.Operators.First(o => o.Owner == seat);

        private static IReadOnlyList<IGameEvent> Batch(params IGameEvent[] events) => events;

        [Test]
        public void EveryTip_HasATitleAndWords()
        {
            foreach (var tip in Coach.All)
            {
                Assert.That(Coach.Title(tip), Is.Not.Empty, tip.ToString());
                var line = Coach.Line(tip);
                Assert.That(line.IsEmpty, Is.False, tip.ToString());
                Assert.That(line.HasUnwritten, Is.False, tip.ToString());
            }
        }

        [Test]
        public void Numbers_ComeFromTheConfigs()
        {
            var game = Config.GameConfig.Default;
            var combat = Config.CombatConfig.Default;

            Assert.That(Coach.Line(CoachTip.Deploy).ToPlainText(), Does.Contain(game.DeployRequirement.ToString()));
            Assert.That(Coach.Line(CoachTip.Doubles).ToPlainText(), Does.Contain(game.MaxRollsPerTurn.ToString()));
            Assert.That(Coach.Line(CoachTip.Collision).ToPlainText(), Does.Contain(combat.CollisionDamage.ToString()));
            Assert.That(Coach.Line(CoachTip.Knockout).ToPlainText(), Does.Contain(combat.NeutralizeEnergyBounty.ToString()));
        }

        [Test]
        public void SafeCellTip_LinksTheGlossary()
        {
            Assert.That(Coach.Line(CoachTip.SafeCell).Keywords, Has.Member(Keywords.SafeCell));
        }

        [Test]
        public void FirstTurn_OffersTheGoal_ToAPerson()
        {
            var match = Started();
            Assert.That(match.Engine.Phase, Is.EqualTo(TurnPhase.AwaitingRoll));

            Assert.That(Coach.Due(match, null, Everyone, NoneSeen), Is.EqualTo(CoachTip.Goal));
        }

        [Test]
        public void ACpuTurn_OffersNothing()
        {
            var match = Started();

            Assert.That(Coach.Due(match, null, Nobody, NoneSeen), Is.Null);
        }

        [Test]
        public void ASeenTip_IsNotOfferedAgain()
        {
            var match = Started();

            Assert.That(Coach.Due(match, null, Everyone, t => t == CoachTip.Goal), Is.Null);
        }

        [Test]
        public void Abilities_WaitForTheRoll()
        {
            var match = Started();

            Assert.That(Coach.Applies(CoachTip.Ability, match, null, Everyone), Is.False);
        }

        [Test]
        public void ACollision_IsOfferedWhenAPersonIsInIt_AndBeforeTheGoal()
        {
            var match = Started();
            var hit = new CollisionResolved(OperatorOf(match, PlayerColor.Red), OperatorOf(match, PlayerColor.Blue), true);

            Assert.That(Coach.Due(match, Batch(hit), Everyone, NoneSeen), Is.EqualTo(CoachTip.Collision));
            Assert.That(Coach.Applies(CoachTip.Collision, match, Batch(hit), Nobody), Is.False);
            Assert.That(Coach.Applies(CoachTip.Collision, match, Batch(hit), s => s == PlayerColor.Blue), Is.True);
        }

        [Test]
        public void AKnockout_CountsForTheVictimAndTheCreditedSeat_ButNotTheDevKey()
        {
            var match = Started();
            var victim = OperatorOf(match, PlayerColor.Blue);
            Func<PlayerColor, bool> redOnly = s => s == PlayerColor.Red;

            Assert.That(Coach.Applies(CoachTip.Knockout, match,
                Batch(new OperatorNeutralized(victim, "collision", PlayerColor.Red)), redOnly), Is.True);
            Assert.That(Coach.Applies(CoachTip.Knockout, match,
                Batch(new OperatorNeutralized(victim, "bleed")), redOnly), Is.False);
            Assert.That(Coach.Applies(CoachTip.Knockout, match,
                Batch(new OperatorNeutralized(victim, GameEngine.DevCause)), Everyone), Is.False);
        }

        [Test]
        public void ADiceMoveOntoASafeCell_IsOffered_ButAPlacementIsNot()
        {
            var match = Started();
            var op = OperatorOf(match, PlayerColor.Red);
            var safe = Enumerable.Range(1, match.Map.Profile.TrackLength - 1)
                .Select(CellRef.Track).First(c => match.Map.IsSafe(c));

            Assert.That(Coach.Applies(CoachTip.SafeCell, match,
                Batch(new OperatorMoved(op, safe.Index - 3, safe.Index, safe)), Everyone), Is.True);
            Assert.That(Coach.Applies(CoachTip.SafeCell, match,
                Batch(new OperatorMoved(op, safe.Index, safe.Index, safe)), Everyone), Is.False);
            Assert.That(Coach.Applies(CoachTip.SafeCell, match,
                Batch(new OperatorMoved(op, 1, 2, CellRef.Track(2))), Everyone), Is.False);
        }

        [Test]
        public void EnteringTheHomeColumn_IsOffered_OnceFromTheTrack()
        {
            var match = Started();
            var op = OperatorOf(match, PlayerColor.Red);
            int track = match.Map.Profile.TrackLength;

            Assert.That(Coach.Applies(CoachTip.HomeColumn, match,
                Batch(new OperatorMoved(op, track - 2, track + 1, CellRef.HomeColumn(op.Owner, 1))), Everyone), Is.True);
            Assert.That(Coach.Applies(CoachTip.HomeColumn, match,
                Batch(new OperatorMoved(op, track + 1, track + 3, CellRef.HomeColumn(op.Owner, 3))), Everyone), Is.False);
            Assert.That(Coach.Applies(CoachTip.HomeColumn, match,
                Batch(new OperatorReachedHome(op)), Everyone), Is.True);
        }
    }
}
