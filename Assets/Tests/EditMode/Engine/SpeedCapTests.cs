// Assets/Tests/EditMode/Engine/SpeedCapTests.cs
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
    /// The per-turn speed bonus cap (COMBAT_SYSTEMS §6.3, 2026-09-17): a move
    /// gains at most <c>CombatConfig.SpeedBonusCellCap</c> cells from a speed
    /// above 1.0×, per operator per turn. The cap trims the bonus, never the
    /// pips, and the budget is charged on every move that collects it.
    /// </summary>
    /// <remarks>
    /// Driven through the engine, because the bookkeeping lives there and the
    /// failures worth catching are plumbing: a preview that ignores the cap, a
    /// second move that collects a full bonus anyway, a doubles turn that beats
    /// the budget, a budget that never resets.
    ///
    /// Every match here is a solo seat with the whole squad deployed, so no
    /// enemy aura or collision can touch the distance being measured. Seeds are
    /// searched for the roll a test needs rather than hard-coded, so a change to
    /// the dice stream moves the seed, not the assertion.
    /// </remarks>
    [TestFixture]
    public class SpeedCapTests
    {
        private static CombatConfig Config => CombatConfig.Default;
        private static int Cap => Config.SpeedBonusCellCap;

        private static int Cells(int pips, double speed) => (int)Math.Floor(pips * speed);

        /// <summary>The capped distance for a move at 1.5× with a fresh budget.</summary>
        private static int Capped(int pips, double speed) =>
            pips + Math.Min(Math.Max(0, Cells(pips, speed) - pips), Cap);

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

        private static int Travelled(MatchFactory.Match match, ICommand move)
        {
            var moved = match.Engine.Execute(move).OfType<OperatorMoved>().FirstOrDefault();

            Assert.That(moved, Is.Not.Null, "the move should have been accepted");
            return moved.To - moved.From;
        }

        [Test]
        public void TheDesignersNumbers()
        {
            // 2026-09-17. A test so that changing it is a deliberate act that
            // also updates §6.3.
            Assert.That(Cap, Is.EqualTo(2));
        }

        [Test]
        public void ASix_MovesEight_NotNine()
        {
            // The case the cap exists for: floor(6 × 1.5) = 9, a +3 bonus.
            var match = RolledSolo(r => !r.IsDouble && (r.First == 6 || r.Second == 6), out var roll);
            var syla = Named(match, "Syla");
            int six = roll.First == 6 ? roll.First : roll.Second;

            Assert.That(Travelled(match, new MoveCommand(syla.Id, six)),
                Is.EqualTo(8), "6 pips plus a capped +2");
        }

        [Test]
        public void Kurbyn_PassiveSpeed_IsCappedToo()
        {
            // His 1.5 arrives through the status channel (ADR-0002 Amendment 5),
            // not his base — the cap reads effective speed, wherever it came from.
            var match = RolledSolo(r => !r.IsDouble && (r.First == 6 || r.Second == 6), out var roll);
            var kurbyn = Named(match, "Kurbyn");
            int six = roll.First == 6 ? roll.First : roll.Second;

            Assert.That(Travelled(match, new MoveCommand(kurbyn.Id, six)), Is.EqualTo(8));
        }

        [Test]
        public void ASmallBonus_FitsInsideTheBudget()
        {
            // A 3 at 1.5× floors to 4: a +1 bonus, under the cap, untouched.
            var match = RolledSolo(r => !r.IsDouble && (r.First == 3 || r.Second == 3), out var roll);
            var syla = Named(match, "Syla");
            int three = roll.First == 3 ? roll.First : roll.Second;

            Assert.That(Travelled(match, new MoveCommand(syla.Id, three)),
                Is.EqualTo(4), "the cap trims the bonus, never below what speed pays");
        }

        [Test]
        public void APooledMove_IsCappedToo()
        {
            // Pooled 7 at 1.5× would be 10; the cap holds it to 7 + 2.
            var match = RolledSolo(r => !r.IsDouble && r.Total >= 4, out var roll);
            var syla = Named(match, "Syla");

            Assert.That(Travelled(match, new MoveCommand(syla.Id)),
                Is.EqualTo(roll.Total + Cap));
        }

        [Test]
        public void TheBudget_IsSharedAcrossSplitMoves()
        {
            // 6 then 4: the 6 collects the whole budget (+2), the 4 gets none.
            var match = RolledSolo(r => !r.IsDouble && r.First == 6 && r.Second == 4, out _);
            var syla = Named(match, "Syla");

            Assert.That(Travelled(match, new MoveCommand(syla.Id, 6)), Is.EqualTo(8));
            Assert.That(Travelled(match, new MoveCommand(syla.Id, 4)),
                Is.EqualTo(4), "the budget is spent; floor(4 × 1.5) = 6 loses its bonus");
        }

        [Test]
        public void ThePreview_AgreesWithTheCap_BeforeAndAfterPaying()
        {
            var match = RolledSolo(r => !r.IsDouble && (r.First == 6 || r.Second == 6), out var roll);
            var syla = Named(match, "Syla");
            double speed = Syla.Speed;

            var before = match.Engine.PreviewLandings()
                .First(p => p.OperatorId == syla.Id && p.DieFace == roll.First);
            Assert.That(before.Cells, Is.EqualTo(Capped(roll.First, speed)),
                "a preview never promises a landing the cap then claws back");

            match.Engine.Execute(new MoveCommand(syla.Id, roll.First));

            // One die left, so the engine offers only the pooled option for it.
            var after = match.Engine.PreviewLandings()
                .First(p => p.OperatorId == syla.Id && p.IsPooled);
            int bonusLeft = Math.Max(0, Cap - Math.Min(Cap, Cells(roll.First, speed) - roll.First));

            Assert.That(after.Cells,
                Is.EqualTo(roll.Second + Math.Min(Math.Max(0, Cells(roll.Second, speed) - roll.Second), bonusLeft)),
                "what is left of the budget, no more");
            Assert.That(Travelled(match, new MoveCommand(syla.Id)), Is.EqualTo(after.Cells));
        }

        [Test]
        public void ADoublesTurn_SharesOneBudgetAcrossBothRolls()
        {
            // The re-roll is the same turn, so the same budget (§6.3).
            var match = RolledSolo(r => r.IsDouble && r.Total >= 8, out var roll);
            var syla = Named(match, "Syla");

            Assert.That(Travelled(match, new MoveCommand(syla.Id)),
                Is.EqualTo(roll.Total + Cap));
            Assert.That(match.Engine.CanRollAgain, Is.True, "precondition: doubles re-roll");

            var second = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;

            Assert.That(Travelled(match, new MoveCommand(syla.Id)),
                Is.EqualTo(second.Total), "the budget was spent on the first roll");
        }

        [Test]
        public void TheBudget_ResetsOnTheNextTurn()
        {
            var match = RolledSolo(r => !r.IsDouble && r.Total >= 8, out _);
            var syla = Named(match, "Syla");

            match.Engine.Execute(new MoveCommand(syla.Id));
            Assert.That(match.Engine.Execute(new EndTurnCommand()).OfType<CommandRejected>(), Is.Empty);

            var roll = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;

            Assert.That(Travelled(match, new MoveCommand(syla.Id)),
                Is.EqualTo(Capped(roll.Total, Syla.Speed)), "a fresh budget next turn");
        }

        [Test]
        public void APlainSpeedOperator_IsUntouched()
        {
            var match = RolledSolo(r => !r.IsDouble && r.Total >= 8, out var roll);
            var bouncer = Named(match, "Bouncer");

            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)),
                Is.EqualTo(roll.Total), "no bonus, nothing to cap");
        }
    }
}
