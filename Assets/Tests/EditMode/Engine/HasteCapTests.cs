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
    /// The haste bonus cap (COMBAT_SYSTEMS §5.9): Hastened adds at most
    /// <c>CombatConfig.HasteBonusCellCap</c> cells to one operator per turn.
    /// </summary>
    /// <remarks>
    /// Driven through the engine, not the resolver, because the budget lives in
    /// the engine and the failure worth catching is the plumbing: a preview that
    /// ignores the cap, a split that collects it twice, a budget that never
    /// resets. The pure arithmetic has its own tests in
    /// <c>MovementResolverTests</c>.
    ///
    /// Every match here is a solo seat with the whole squad deployed, so no
    /// enemy aura or collision can touch the distance being measured. Seeds are
    /// searched for the roll a test needs rather than hard-coded, so a change to
    /// the dice stream moves the seed, not the assertion.
    /// </remarks>
    [TestFixture]
    public class HasteCapTests
    {
        private static int Cap => CombatConfig.Default.HasteBonusCellCap;
        private static double Haste => CombatConfig.Default.HasteSpeedBonus;

        private static int Cells(int pips, double speed) => (int)Math.Floor(pips * speed);

        private static int UncappedBonus(int pips, double speed) =>
            Cells(pips, speed + Haste) - Cells(pips, speed);

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
            match.Statuses.Apply(op, StatusKind.Hastened, CombatConfig.Default.HasteDurationTurns);

        private static int Travelled(MatchFactory.Match match, ICommand move)
        {
            var moved = match.Engine.Execute(move).OfType<OperatorMoved>().FirstOrDefault();

            Assert.That(moved, Is.Not.Null, "the move should have been accepted");
            return moved.To - moved.From;
        }

        [Test]
        public void TheCap_IsThree()
        {
            // The designer's number (2026-09-16). A test so that changing it is
            // a deliberate act that also updates §5.9.
            Assert.That(Cap, Is.EqualTo(3));
        }

        [Test]
        public void APooledHastedMove_GainsNoMoreThanTheCap()
        {
            // Bouncer at 1.0×: any total of 8 or more would gain 4+ uncapped.
            var match = RolledSolo(r => !r.IsDouble && UncappedBonus(r.Total, Bouncer.Speed) > Cap, out var roll);
            var bouncer = Named(match, "Bouncer");
            Hasten(match, bouncer);

            int travelled = Travelled(match, new MoveCommand(bouncer.Id));

            Assert.That(travelled, Is.EqualTo(Cells(roll.Total, Bouncer.Speed) + Cap));
        }

        [Test]
        public void ASmallHastedMove_KeepsItsWholeBonus()
        {
            // Below the cap nothing changes: haste is still +0.5 speed.
            var match = RolledSolo(
                r => !r.IsDouble && UncappedBonus(r.Total, Bouncer.Speed) is int b && b > 0 && b < Cap,
                out var roll);
            var bouncer = Named(match, "Bouncer");
            Hasten(match, bouncer);

            int travelled = Travelled(match, new MoveCommand(bouncer.Id));

            Assert.That(travelled, Is.EqualTo(Cells(roll.Total, Bouncer.Speed + Haste)));
        }

        [Test]
        public void ASplitRoll_SharesOneBudget_RatherThanCollectingItTwice()
        {
            // The reason the cap is per turn: rounding is per move, so 6 + 4 at
            // 1.0× gains 3 + 2 when split. Per move, neither reaches the cap.
            var match = RolledSolo(r =>
                !r.IsDouble &&
                UncappedBonus(r.First, Bouncer.Speed) + UncappedBonus(r.Second, Bouncer.Speed) > Cap,
                out var roll);
            var bouncer = Named(match, "Bouncer");
            Hasten(match, bouncer);

            int first = Travelled(match, new MoveCommand(bouncer.Id, roll.First));
            int second = Travelled(match, new MoveCommand(bouncer.Id, roll.Second));

            int unhasted = Cells(roll.First, Bouncer.Speed) + Cells(roll.Second, Bouncer.Speed);

            Assert.That(first + second, Is.EqualTo(unhasted + Cap));
            Assert.That(first, Is.EqualTo(
                Cells(roll.First, Bouncer.Speed) + Math.Min(Cap, UncappedBonus(roll.First, Bouncer.Speed))));
        }

        [Test]
        public void ThePreview_ShowsWhatIsLeftOfTheBudget()
        {
            // A preview that ignored the spent half would promise a landing the
            // move then trims — the exact disagreement PreviewLandings exists
            // to prevent.
            var match = RolledSolo(r =>
                !r.IsDouble &&
                UncappedBonus(r.First, Bouncer.Speed) >= Cap &&
                UncappedBonus(r.Second, Bouncer.Speed) > 0,
                out var roll);
            var bouncer = Named(match, "Bouncer");
            Hasten(match, bouncer);

            match.Engine.Execute(new MoveCommand(bouncer.Id, roll.First));

            // One die left, so the engine offers only the pooled option for it.
            var preview = match.Engine.PreviewLandings()
                .First(p => p.OperatorId == bouncer.Id && p.IsPooled);

            Assert.That(preview.Cells, Is.EqualTo(Cells(roll.Second, Bouncer.Speed)),
                "the first die spent the whole budget");
            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)), Is.EqualTo(preview.Cells));
        }

        [Test]
        public void EachOperator_HasItsOwnBudget()
        {
            var match = RolledSolo(r =>
                !r.IsDouble &&
                UncappedBonus(r.First, Bouncer.Speed) >= Cap &&
                UncappedBonus(r.Second, Syla.Speed) > 0,
                out var roll);
            var bouncer = Named(match, "Bouncer");
            var syla = Named(match, "Syla");
            Hasten(match, bouncer);
            Hasten(match, syla);

            match.Engine.Execute(new MoveCommand(bouncer.Id, roll.First));
            int sylaTravelled = Travelled(match, new MoveCommand(syla.Id, roll.Second));

            Assert.That(sylaTravelled, Is.EqualTo(
                Cells(roll.Second, Syla.Speed) + Math.Min(Cap, UncappedBonus(roll.Second, Syla.Speed))));
        }

        [Test]
        public void TheBudget_ResetsOnTheNextTurn()
        {
            var match = RolledSolo(r => !r.IsDouble && UncappedBonus(r.Total, Bouncer.Speed) >= Cap, out _);
            var bouncer = Named(match, "Bouncer");
            Hasten(match, bouncer);                      // duration 2: this turn and the next

            match.Engine.Execute(new MoveCommand(bouncer.Id));
            Assert.That(match.Engine.Execute(new EndTurnCommand()).OfType<CommandRejected>(), Is.Empty);

            var roll = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;
            Assert.That(match.Statuses.Has(bouncer, StatusKind.Hastened), Is.True, "precondition: still hastened");

            int travelled = Travelled(match, new MoveCommand(bouncer.Id));

            Assert.That(travelled, Is.EqualTo(
                Cells(roll.Total, Bouncer.Speed) + Math.Min(Cap, UncappedBonus(roll.Total, Bouncer.Speed))));
            Assert.That(travelled, Is.GreaterThan(Cells(roll.Total, Bouncer.Speed)),
                "a fresh turn earns a fresh bonus");
        }

        [Test]
        public void APassiveSpeedBonus_IsNotCountedAgainstTheCap()
        {
            // Kurbyn's Evasive Protocol is speed, not haste. Only the Hastened
            // part of his move is trimmed.
            double kurbyn = Kurbyn.BaseSpeed + Kurbyn.PassiveSpeedBonus;
            var match = RolledSolo(r => !r.IsDouble && UncappedBonus(r.Total, kurbyn) > Cap, out var roll);
            var op = Named(match, "Kurbyn");
            Hasten(match, op);

            int travelled = Travelled(match, new MoveCommand(op.Id));

            Assert.That(travelled, Is.EqualTo(Cells(roll.Total, kurbyn) + Cap));
            Assert.That(travelled, Is.GreaterThan(Cells(roll.Total, Kurbyn.BaseSpeed) + Cap));
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