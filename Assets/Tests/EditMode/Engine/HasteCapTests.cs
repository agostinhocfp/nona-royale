// Assets/Tests/EditMode/Engine/HasteCapTests.cs
using System;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Engine
{
    /// <summary>
    /// Haste as flat extra cells (COMBAT_SYSTEMS §5.9, 2026-09-16): +1 when the
    /// roll totals 6 or less, +2 above, once per roll per operator, and at most
    /// <c>CombatConfig.HasteBonusCellCap</c> per operator per turn.
    /// </summary>
    /// <remarks>
    /// The file keeps its name from the capped-speed version it replaces.
    ///
    /// Driven through the engine, because the bookkeeping lives there and the
    /// failures worth catching are plumbing: a preview that ignores the paid
    /// bonus, a split that collects it twice, a doubles turn that beats the
    /// cap, a budget that never resets.
    ///
    /// Every match here is a solo seat with the whole squad deployed, so no
    /// enemy aura or collision can touch the distance being measured. Seeds are
    /// searched for the roll a test needs rather than hard-coded, so a change to
    /// the dice stream moves the seed, not the assertion.
    /// </remarks>
    [TestFixture]
    public class HasteCapTests
    {
        private static CombatConfig Config => CombatConfig.Default;
        private static int Cap => Config.HasteBonusCellCap;

        private static int Cells(int pips, double speed) => (int)Math.Floor(pips * speed);

        private static int Bonus(int rollTotal) => Config.HasteCellsFor(rollTotal);

        private static bool Low(DiceRoll r) => r.Total <= Config.HasteRollThreshold;

        private static OperatorState Named(MatchFactory.Match match, string name) =>
            match.Operators.First(o => o.Name == name);

        /// <summary>
        /// A started solo match, rolled, whose roll satisfies
        /// <paramref name="wanted"/>. The first seed that delivers it wins.
        /// </summary>
        private static MatchFactory.Match RolledSolo(Func<DiceRoll, bool> wanted, out DiceRoll roll)
        {
            for (int seed = 1; seed < 2000; seed++)
            {
                var match = MatchFactory.CreateAlphaMatch(
                    new[] { PlayerColor.Red }, seed, openingDeployments: 3);

                match.Engine.Start();

                var rolled = match.Engine.Execute(new RollDiceCommand())
                    .OfType<DiceRolled>().FirstOrDefault();

                if (rolled != null && wanted(rolled.Roll))
                {
                    roll = rolled.Roll;
                    return match;
                }
            }

            throw new InvalidOperationException("No seed produced the roll this test needs.");
        }

        private static void Hasten(MatchFactory.Match match, OperatorState op) =>
            match.Statuses.Apply(op, StatusKind.Hastened, Config.HasteDurationTurns);

        private static int Travelled(MatchFactory.Match match, ICommand move)
        {
            var moved = match.Engine.Execute(move).OfType<OperatorMoved>().FirstOrDefault();

            Assert.That(moved, Is.Not.Null, "the move should have been accepted");
            return moved.To - moved.From;
        }

        [Test]
        public void TheDesignersNumbers()
        {
            // 2026-09-16. A test so that changing them is a deliberate act that
            // also updates §5.9.
            Assert.That(Config.HasteRollThreshold, Is.EqualTo(6));
            Assert.That(Bonus(2), Is.EqualTo(1));
            Assert.That(Bonus(6), Is.EqualTo(1));
            Assert.That(Bonus(7), Is.EqualTo(2));
            Assert.That(Bonus(12), Is.EqualTo(2));
            Assert.That(Cap, Is.EqualTo(3));
        }

        [Test]
        public void ALowRoll_AddsOneCell()
        {
            var match = RolledSolo(r => !r.IsDouble && Low(r), out var roll);
            var bouncer = Named(match, "Bouncer");
            Hasten(match, bouncer);

            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)),
                Is.EqualTo(Cells(roll.Total, Bouncer.Speed) + 1));
        }

        [Test]
        public void AHighRoll_AddsTwoCells()
        {
            var match = RolledSolo(r => !r.IsDouble && !Low(r), out var roll);
            var bouncer = Named(match, "Bouncer");
            Hasten(match, bouncer);

            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)),
                Is.EqualTo(Cells(roll.Total, Bouncer.Speed) + 2));
        }

        [Test]
        public void HasteNoLongerScalesWithSpeed()
        {
            // Syla at 1.5 gets the same flat +2 as the Bouncer. Under the old
            // +0.5 speed a pooled 11 at 2.0 would have gained 6 cells.
            var match = RolledSolo(r => !r.IsDouble && !Low(r), out var roll);
            var syla = Named(match, "Syla");
            Hasten(match, syla);

            Assert.That(Travelled(match, new MoveCommand(syla.Id)),
                Is.EqualTo(Cells(roll.Total, Syla.Speed) + 2));
            Assert.That(match.Statuses.SpeedModifier(syla), Is.EqualTo(0.0), "haste is not speed");
        }

        [Test]
        public void APassiveSpeedBonus_StillApplies()
        {
            // Kurbyn's Evasive Protocol is speed, not haste: his move is his
            // passive speed, plus the flat bonus.
            double kurbyn = Kurbyn.BaseSpeed + Kurbyn.PassiveSpeedBonus;
            var match = RolledSolo(r => !r.IsDouble && !Low(r), out var roll);
            var op = Named(match, "Kurbyn");
            Hasten(match, op);

            Assert.That(Travelled(match, new MoveCommand(op.Id)),
                Is.EqualTo(Cells(roll.Total, kurbyn) + 2));
        }

        [Test]
        public void ASplitRoll_PaysTheBonusOnce_AndTheWholeRollSetsIt()
        {
            // 5 + 4: each die alone is low, but the roll is 9, so +2, on the
            // first move only.
            var match = RolledSolo(r => !r.IsDouble && !Low(r), out var roll);
            var bouncer = Named(match, "Bouncer");
            Hasten(match, bouncer);

            int first = Travelled(match, new MoveCommand(bouncer.Id, roll.First));
            int second = Travelled(match, new MoveCommand(bouncer.Id, roll.Second));

            Assert.That(first, Is.EqualTo(Cells(roll.First, Bouncer.Speed) + 2));
            Assert.That(second, Is.EqualTo(Cells(roll.Second, Bouncer.Speed)));
        }

        [Test]
        public void ThePreview_DropsTheBonusOnceItIsPaid()
        {
            var match = RolledSolo(r => !r.IsDouble, out var roll);
            var bouncer = Named(match, "Bouncer");
            Hasten(match, bouncer);

            var before = match.Engine.PreviewLandings()
                .First(p => p.OperatorId == bouncer.Id && p.DieFace == roll.First);
            Assert.That(before.Cells, Is.EqualTo(Cells(roll.First, Bouncer.Speed) + Bonus(roll.Total)));

            match.Engine.Execute(new MoveCommand(bouncer.Id, roll.First));

            // One die left, so the engine offers only the pooled option for it.
            var after = match.Engine.PreviewLandings()
                .First(p => p.OperatorId == bouncer.Id && p.IsPooled);

            Assert.That(after.Cells, Is.EqualTo(Cells(roll.Second, Bouncer.Speed)), "already paid this roll");
            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)), Is.EqualTo(after.Cells));
        }

        [Test]
        public void EachOperator_CollectsItsOwnBonus()
        {
            var match = RolledSolo(r => !r.IsDouble, out var roll);
            var bouncer = Named(match, "Bouncer");
            var syla = Named(match, "Syla");
            Hasten(match, bouncer);
            Hasten(match, syla);

            int b = Travelled(match, new MoveCommand(bouncer.Id, roll.First));
            int s = Travelled(match, new MoveCommand(syla.Id, roll.Second));

            Assert.That(b, Is.EqualTo(Cells(roll.First, Bouncer.Speed) + Bonus(roll.Total)));
            Assert.That(s, Is.EqualTo(Cells(roll.Second, Syla.Speed) + Bonus(roll.Total)));
        }

        [Test]
        public void ADoublesTurn_StopsAtTheCap()
        {
            // A high double pays 2; the re-roll would pay 1 or 2, but only 1 of
            // the turn's 3 is left.
            var match = RolledSolo(r => r.IsDouble && !Low(r), out var roll);
            var bouncer = Named(match, "Bouncer");
            Hasten(match, bouncer);

            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)),
                Is.EqualTo(Cells(roll.Total, Bouncer.Speed) + 2));
            Assert.That(match.Engine.CanRollAgain, Is.True, "precondition: doubles re-roll");

            var second = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;

            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)),
                Is.EqualTo(Cells(second.Total, Bouncer.Speed) + Cap - 2));
        }

        [Test]
        public void TheBudget_ResetsOnTheNextTurn()
        {
            var match = RolledSolo(r => !r.IsDouble, out _);
            var bouncer = Named(match, "Bouncer");
            Hasten(match, bouncer);                      // duration 2: this turn and the next

            match.Engine.Execute(new MoveCommand(bouncer.Id));
            Assert.That(match.Engine.Execute(new EndTurnCommand()).OfType<CommandRejected>(), Is.Empty);

            var roll = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;
            Assert.That(match.Statuses.Has(bouncer, StatusKind.Hastened), Is.True, "precondition: still hastened");

            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)),
                Is.EqualTo(Cells(roll.Total, Bouncer.Speed) + Bonus(roll.Total)));
        }

        [Test]
        public void AnUnhastedOperator_MovesExactlyAsBefore()
        {
            var match = RolledSolo(r => !r.IsDouble && r.Total >= 8, out var roll);
            var bouncer = Named(match, "Bouncer");

            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)),
                Is.EqualTo(Cells(roll.Total, Bouncer.Speed)));
        }
    }
}