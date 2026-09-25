// Assets/Tests/EditMode/Abilities/FortunaTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Abilities
{
    /// <summary>
    /// Fortuna, operator #12 (COMBAT_SYSTEMS §10.12, 2026-09-18): the House
    /// Edge's cashed die (§3.4), Deal Again and Boxcars on the roll itself
    /// (§6.8), and the table that stops a run (§7.7).
    /// </summary>
    /// <remarks>
    /// Driven through the engine, like <c>BurdenTests</c> and
    /// <c>HasteCapTests</c>: the dice are the engine's state, and every rule
    /// here is about them. A solo Red seat wherever the roll is what matters, so
    /// no opponent's turn intervenes; a second seat only where an enemy has to
    /// move.
    /// </remarks>
    [TestFixture]
    public class FortunaTests
    {
        private static readonly PlayerColor[] Solo = { PlayerColor.Red };
        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };

        private static OperatorState Named(MatchFactory.Match match, string name) =>
            match.Operators.First(o => o.Name == name);

        /// <summary>
        /// A Red seat of Fortuna, Bouncer and Mimi, all deployed, whose first
        /// roll satisfies <paramref name="wanted"/>. Seeds are scanned rather
        /// than the dice faked: the engine owns the roll, and a fixture that
        /// injected faces would test the fixture.
        /// </summary>
        private static MatchFactory.Match RolledSeat(Func<DiceRoll, bool> wanted, out DiceRoll roll)
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                [PlayerColor.Red] = new[] { Fortuna.Definition, Bouncer.Definition, Mimi.Definition }
            };

            for (int seed = 1; seed < 4000; seed++)
            {
                var match = MatchFactory.Create(Solo, seed, squads, openingDeployments: 3);
                match.Engine.Start();

                var rolled = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().FirstOrDefault();

                if (rolled != null && wanted(rolled.Roll))
                {
                    roll = rolled.Roll;
                    return match;
                }
            }

            throw new InvalidOperationException("No seed produced the roll this test needs.");
        }

        private static MatchFactory.Match PlainRoll(out DiceRoll roll) =>
            RolledSeat(r => !r.IsDouble && r.First != 6 && r.Second != 6, out roll);

        private static ICommand Cash(MatchFactory.Match match, int face) =>
            new CashDieCommand(Named(match, "Fortuna").Id, face);

        private static ICommand Cast(MatchFactory.Match match, AbilityDefinition ability, CellRef? cell = null) =>
            new UseAbilityCommand(Named(match, "Fortuna").Id, ability.Id, null, cell);

        private static bool Rejected(IReadOnlyList<IGameEvent> events) =>
            events.OfType<CommandRejected>().Any();

        /// <summary>Fills the Red pool to the cap, so a cast is never refused for money.</summary>
        private static void Fund(MatchFactory.Match match) =>
            match.Energy.GrantBounty(match.Players[0], match.Engine.EnergyCap);

        // ── The House Edge (§3.4) ────────────────────────────────────────

        [Test]
        public void TheDesignersNumbers()
        {
            // 2026-09-18. Changing any of them is a deliberate act that also
            // updates §10.12.
            var fortuna = Roster.ByName("Fortuna");

            Assert.That(fortuna.MaxHealth, Is.EqualTo(8)); // +1 on 2026-09-25
            Assert.That(fortuna.BaseSpeed, Is.EqualTo(1.0), "at 1.5 she banks and races");
            Assert.That(fortuna.Passive, Is.EqualTo(StatusKind.HouseEdge));
            Assert.That(fortuna.PassiveName, Is.EqualTo("The House Edge"));

            // Designer, 2026-09-21: The Table 6 → 5 and its lifetime 2 → 3
            // turns, Boxcars 9 → 7.
            Assert.That(Fortuna.DealAgain.EnergyCost, Is.EqualTo(3));
            Assert.That(Fortuna.TheTable.EnergyCost, Is.EqualTo(5));
            Assert.That(Fortuna.Boxcars.EnergyCost, Is.EqualTo(7));
            Assert.That(Fortuna.TheTable.Range, Is.EqualTo(4));
            Assert.That(Fortuna.TableDamage, Is.EqualTo(2));
            Assert.That(Fortuna.TableLifetimeTurns, Is.EqualTo(3));
        }

        [Test]
        public void Cash_TakesTheDieAndPaysTheHouse()
        {
            var match = PlainRoll(out var roll);
            var seat = match.Players[0];

            int before = seat.Energy;
            var events = match.Engine.Execute(Cash(match, roll.First));
            var cashed = events.OfType<DieCashed>().FirstOrDefault();

            Assert.That(cashed, Is.Not.Null);
            Assert.That(cashed.DieFace, Is.EqualTo(roll.First));
            Assert.That(seat.Energy, Is.EqualTo(before + 2), "two energy a die (§3.4)");
            Assert.That(match.Engine.UnspentDice.Count, Is.EqualTo(1), "the die is consumed");
        }

        [Test]
        public void Cash_IsOnceATurn()
        {
            var match = RolledSeat(r => !r.IsDouble && r.First != 6 && r.Second != 6, out var roll);

            match.Engine.Execute(Cash(match, roll.First));
            var second = match.Engine.Execute(Cash(match, roll.Second));

            Assert.That(Rejected(second), Is.True, "the house takes one die a turn");
            Assert.That(match.Engine.UnspentDice.Count, Is.EqualTo(1), "the second die is still there");
        }

        [Test]
        public void Cash_IsHersAlone()
        {
            var match = PlainRoll(out var roll);

            var events = match.Engine.Execute(new CashDieCommand(Named(match, "Bouncer").Id, roll.First));

            Assert.That(Rejected(events), Is.True);
            Assert.That(match.Engine.UnspentDice.Count, Is.EqualTo(2));
        }

        [Test]
        public void Cash_RefusedWhileStunned()
        {
            var match = PlainRoll(out var roll);
            var fortuna = Named(match, "Fortuna");

            // Off her own start cell first: a spawn cell refuses a stun (§4.4,
            // third amendment), and this test is about the stun, not the cell.
            fortuna.MoveTo(1);
            Assert.That(match.Statuses.Apply(fortuna, StatusKind.Stun, 2), Is.True, "fixture: the stun must land");

            Assert.That(Rejected(match.Engine.Execute(Cash(match, roll.First))), Is.True);
        }

        [Test]
        public void Cash_AtTheCap_StillSpendsTheDie()
        {
            var match = PlainRoll(out var roll);
            var seat = match.Players[0];
            Fund(match);

            var cashed = match.Engine.Execute(Cash(match, roll.First)).OfType<DieCashed>().FirstOrDefault();

            Assert.That(cashed, Is.Not.Null);
            Assert.That(cashed.Stored, Is.EqualTo(0), "legal and stupid");
            Assert.That(seat.Energy, Is.EqualTo(match.Engine.EnergyCap));
            Assert.That(match.Engine.UnspentDice.Count, Is.EqualTo(1));
        }

        [Test]
        public void Cash_AnswersCompulsoryMovement()
        {
            // The escape valve (§6.1): a turn whose last die would only walk
            // somebody somewhere bad can be ended by selling it instead.
            var match = PlainRoll(out var roll);

            match.Engine.Execute(new MoveCommand(Named(match, "Bouncer").Id, roll.First));
            Assert.That(match.Engine.MustSpendRoll, Is.True, "one die is still in hand");

            match.Engine.Execute(Cash(match, roll.Second));

            Assert.That(match.Engine.MustSpendRoll, Is.False);
            Assert.That(Rejected(match.Engine.Execute(new EndTurnCommand())), Is.False);
        }

        // ── Deal Again and Boxcars (§6.8) ────────────────────────────────

        [Test]
        public void DealAgain_ReDealsTheLowestDie()
        {
            // The ruling: the command carries no face, so the worst die goes
            // back in the cup. The high die must come out untouched.
            for (int seed = 1; seed < 4000; seed++)
            {
                var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
                {
                    [PlayerColor.Red] = new[] { Fortuna.Definition, Bouncer.Definition, Mimi.Definition }
                };

                var match = MatchFactory.Create(Solo, seed, squads, openingDeployments: 3);
                match.Engine.Start();

                var roll = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;
                if (roll.IsDouble) continue;

                int high = Math.Max(roll.First, roll.Second);
                int low = Math.Min(roll.First, roll.Second);

                Fund(match);
                var dealt = match.Engine.Execute(Cast(match, Fortuna.DealAgain))
                    .OfType<DiceDealt>().FirstOrDefault();

                if (dealt == null) continue;

                Assert.That(dealt.Faces.Count, Is.EqualTo(2), "a re-deal replaces, it never adds");
                Assert.That(dealt.Faces.Count(f => f == high), Is.GreaterThanOrEqualTo(1),
                    "the high die was not the one re-dealt");

                // Only interested in the seeds where the re-deal visibly moved
                // the low die; the rest prove nothing either way.
                if (dealt.Faces.Contains(low) && dealt.Faces.Count(f => f == low) == 1 && low != high) continue;

                Assert.That(match.Engine.UnspentDice.Count, Is.EqualTo(2));
                return;
            }

            Assert.Fail("no seed re-dealt the low die into a different face");
        }

        [Test]
        public void DealAgain_NeverCreatesTheDoublesRoll()
        {
            // The doubles roll is decided when the dice leave the cup (§6.8).
            for (int seed = 1; seed < 6000; seed++)
            {
                var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
                {
                    [PlayerColor.Red] = new[] { Fortuna.Definition, Bouncer.Definition, Mimi.Definition }
                };

                var match = MatchFactory.Create(Solo, seed, squads, openingDeployments: 3);
                match.Engine.Start();

                var roll = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;
                if (roll.IsDouble) continue;

                Fund(match);
                var dealt = match.Engine.Execute(Cast(match, Fortuna.DealAgain))
                    .OfType<DiceDealt>().FirstOrDefault();

                if (dealt == null || dealt.Faces[0] != dealt.Faces[1]) continue;

                Assert.That(dealt.GrantsAnotherRoll, Is.False);
                Assert.That(match.Engine.CanRollAgain, Is.False, "a dealt double from a re-roll owes nothing");
                return;
            }

            Assert.Fail("no seed re-dealt a non-double into a double");
        }

        [Test]
        public void DealAgain_RefusedWithNothingInHand_AndCostsNothing()
        {
            var match = PlainRoll(out _);
            var seat = match.Players[0];
            Fund(match);

            match.Engine.Execute(new MoveCommand(Named(match, "Bouncer").Id, null));
            Assert.That(match.Engine.UnspentDice.Count, Is.EqualTo(0));

            var events = match.Engine.Execute(Cast(match, Fortuna.DealAgain));

            Assert.That(Rejected(events), Is.True);
            Assert.That(seat.Energy, Is.EqualTo(match.Engine.EnergyCap), "a refusal is free");
        }

        [Test]
        public void Boxcars_SetsBothDiceToSix_AndOwesARoll()
        {
            var match = PlainRoll(out _);
            Fund(match);

            var dealt = match.Engine.Execute(Cast(match, Fortuna.Boxcars))
                .OfType<DiceDealt>().FirstOrDefault();

            Assert.That(dealt, Is.Not.Null);
            Assert.That(dealt.Faces, Is.EquivalentTo(new[] { 6, 6 }));
            Assert.That(dealt.GrantsAnotherRoll, Is.True, "a dealt double is owed its roll");
            Assert.That(match.Engine.UnspentDice, Is.EquivalentTo(new[] { 6, 6 }));
        }

        [Test]
        public void Boxcars_RefusedOnAHalfSpentRoll_AndCostsNothing()
        {
            var match = PlainRoll(out var roll);
            var seat = match.Players[0];
            Fund(match);

            match.Engine.Execute(new MoveCommand(Named(match, "Bouncer").Id, roll.First));

            var events = match.Engine.Execute(Cast(match, Fortuna.Boxcars));

            Assert.That(Rejected(events), Is.True);
            Assert.That(seat.Energy, Is.EqualTo(match.Engine.EnergyCap));
            Assert.That(match.Engine.UnspentDice.Count, Is.EqualTo(1), "the die in hand is untouched");
        }

        [Test]
        public void Boxcars_IsNotOfferedOnceADieIsSpent()
        {
            var match = PlainRoll(out var roll);
            Fund(match);
            var fortuna = Named(match, "Fortuna");

            Assert.That(match.Engine.CheckAbility(fortuna, Fortuna.Boxcars),
                Is.EqualTo(AbilityAvailability.Ready));

            match.Engine.Execute(new MoveCommand(Named(match, "Bouncer").Id, roll.First));

            Assert.That(match.Engine.CheckAbility(fortuna, Fortuna.Boxcars),
                Is.EqualTo(AbilityAvailability.DiceNotHeld));
        }

        [Test]
        public void Boxcars_AtTheRollCap_SetsTheFacesAndNothingMore()
        {
            // Three rolls is the budget (§6.2). At the cap the faces still
            // change; the roll a double is owed simply is not there.
            for (int seed = 1; seed < 8000; seed++)
            {
                var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
                {
                    [PlayerColor.Red] = new[] { Fortuna.Definition, Bouncer.Definition, Mimi.Definition }
                };

                var match = MatchFactory.Create(Solo, seed, squads, openingDeployments: 3);
                match.Engine.Start();
                Fund(match);

                var first = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;
                if (!first.IsDouble) continue;

                match.Engine.Execute(new MoveCommand(Named(match, "Bouncer").Id, null));
                var second = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().FirstOrDefault();
                if (second == null || !second.Roll.IsDouble) continue;

                match.Engine.Execute(new MoveCommand(Named(match, "Mimi").Id, null));
                var third = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().FirstOrDefault();
                if (third == null) continue;

                var dealt = match.Engine.Execute(Cast(match, Fortuna.Boxcars))
                    .OfType<DiceDealt>().FirstOrDefault();

                Assert.That(dealt, Is.Not.Null);
                Assert.That(dealt.Faces, Is.EquivalentTo(new[] { 6, 6 }));
                Assert.That(dealt.GrantsAnotherRoll, Is.False, "the budget still binds");
                Assert.That(match.Engine.CanRollAgain, Is.False);
                return;
            }

            Assert.Fail("no seed rolled two doubles in a row");
        }

        // ── The Table (§7.7) ─────────────────────────────────────────────

        [Test]
        public void Table_RefusedOnASafeCell_AndCostsNothing()
        {
            var match = PlainRoll(out _);
            var seat = match.Players[0];
            Fund(match);

            // Track 13 is a start cell, and a table there would shelter whoever
            // it stopped. She stands on 10, three steps away, so the refusal is
            // the safe cell and not the range.
            Named(match, "Fortuna").MoveTo(10);

            var events = match.Engine.Execute(Cast(match, Fortuna.TheTable, CellRef.Track(13)));

            Assert.That(Rejected(events), Is.True);
            Assert.That(seat.Energy, Is.EqualTo(match.Engine.EnergyCap), "a refusal is free");

            // The same cast one cell short of it is legal, which is what proves
            // the refusal above was the safe cell.
            Assert.That(Rejected(match.Engine.Execute(Cast(match, Fortuna.TheTable, CellRef.Track(12)))), Is.False);
        }

        [Test]
        public void Table_IsDealtAndDrawn()
        {
            var match = PlainRoll(out _);
            Fund(match);

            var cell = CellRef.Track(3);
            var events = match.Engine.Execute(Cast(match, Fortuna.TheTable, cell));

            Assert.That(events.OfType<TableDealt>().Any(), Is.True);
            Assert.That(match.Engine.HasTableOn(cell, PlayerColor.Red), Is.True);
            Assert.That(match.Engine.ActiveTables(), Contains.Item(cell));
        }
    }
}
