// Assets/Tests/EditMode/Energy/DebtTests.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Energy
{
    /// <summary>
    /// Seat debt (COMBAT_SYSTEMS §3.3, 2026-09-24): the ledger's rules, then
    /// collection at the end of the debtor's turn and the burn on a collision,
    /// through the engine. What Revú's own abilities do with it is in
    /// <c>RevuTests</c>.
    /// </summary>
    [TestFixture]
    public class DebtTests
    {
        private const int Creditor = 77;

        private EnergyLedger _ledger;
        private PlayerState _seat;

        [SetUp]
        public void SetUp()
        {
            _ledger = new EnergyLedger(EnergyConfig.Default);
            _seat = new PlayerState(PlayerColor.Blue, new[] { new OperatorState(1, "Debtor", PlayerColor.Blue, 7, 1.0) });
        }

        private void SetPool(int amount)
        {
            _ledger.Spend(_seat, _seat.Energy);
            if (amount > 0) _ledger.GrantBounty(_seat, amount);
            Assert.That(_seat.Energy, Is.EqualTo(amount), "precondition");
        }

        // ── The ledger ───────────────────────────────────────────────────

        [Test]
        public void TheDefaults_AreTheDesigners()
        {
            Assert.That(EnergyConfig.Default.DebtCap, Is.EqualTo(6));
            Assert.That(EnergyConfig.Default.DebtInterest, Is.EqualTo(1));
        }

        [Test]
        public void IncurDebt_AddsWithinTheCap_AndReportsWhatItAdded()
        {
            Assert.That(_ledger.IncurDebt(_seat, 4, Creditor), Is.EqualTo(4));
            Assert.That(_ledger.IncurDebt(_seat, 4, Creditor), Is.EqualTo(2), "only room for 2");
            Assert.That(_ledger.IncurDebt(_seat, 4, Creditor), Is.EqualTo(0));
            Assert.That(_seat.Debt, Is.EqualTo(6));
        }

        [Test]
        public void IncurDebt_TakesNoEnergy()
        {
            SetPool(9);

            _ledger.IncurDebt(_seat, 2, Creditor);

            Assert.That(_seat.Energy, Is.EqualTo(9));
        }

        [Test]
        public void TheSeatOwesTheCaster_AndNobodyElse()
        {
            _ledger.IncurDebt(_seat, 2, Creditor);

            Assert.That(_seat.OwesTo(Creditor), Is.True);
            Assert.That(_seat.OwesTo(Creditor + 1), Is.False);
        }

        [Test]
        public void ACapRefusal_StillRecordsTheCreditor()
        {
            // A second Revú lending to a seat already at the cap is still owed:
            // landing on him burns the debt too.
            _ledger.IncurDebt(_seat, 6, Creditor);
            _ledger.IncurDebt(_seat, 2, Creditor + 1);

            Assert.That(_seat.OwesTo(Creditor + 1), Is.True);
        }

        [Test]
        public void Collect_PaidInFull_SquaresTheSeat()
        {
            SetPool(5);
            _ledger.IncurDebt(_seat, 3, Creditor);

            var bill = _ledger.CollectDebt(_seat);

            Assert.That((bill.OwedBefore, bill.Paid, bill.Interest, bill.Owed), Is.EqualTo((3, 3, 0, 0)));
            Assert.That(_seat.Energy, Is.EqualTo(2), "destroyed, not moved anywhere");
            Assert.That(_seat.Debt, Is.EqualTo(0));
            Assert.That(_seat.Creditors, Is.Empty);
        }

        [Test]
        public void Collect_PaidInPart_ChargesInterestOnTheRemainder()
        {
            SetPool(1);
            _ledger.IncurDebt(_seat, 4, Creditor);

            var bill = _ledger.CollectDebt(_seat);

            Assert.That((bill.Paid, bill.Interest, bill.Owed), Is.EqualTo((1, 1, 4)), "3 unpaid, +1");
            Assert.That(_seat.Energy, Is.EqualTo(0));
            Assert.That(_seat.OwesTo(Creditor), Is.True);
        }

        [Test]
        public void Collect_InterestNeverPassesTheCap()
        {
            SetPool(0);
            _ledger.IncurDebt(_seat, 6, Creditor);

            var bill = _ledger.CollectDebt(_seat);

            Assert.That((bill.Paid, bill.Interest, bill.Owed), Is.EqualTo((0, 0, 6)));
        }

        [Test]
        public void Collect_JustBelowTheCap_ChargesOnlyWhatFits()
        {
            SetPool(0);
            _ledger.IncurDebt(_seat, 5, Creditor);

            var bill = _ledger.CollectDebt(_seat);

            Assert.That((bill.Interest, bill.Owed), Is.EqualTo((1, 6)));
        }

        [Test]
        public void Collect_WithNoDebt_DoesNothing()
        {
            SetPool(8);

            var bill = _ledger.CollectDebt(_seat);

            Assert.That(bill.Happened, Is.False);
            Assert.That(_seat.Energy, Is.EqualTo(8));
        }

        [Test]
        public void WriteOff_ClearsTheWholeDebt_AndItsCreditors()
        {
            _ledger.IncurDebt(_seat, 5, Creditor);

            Assert.That(_ledger.WriteOffDebt(_seat), Is.EqualTo(5));
            Assert.That(_seat.Debt, Is.EqualTo(0));
            Assert.That(_seat.OwesTo(Creditor), Is.False);
        }

        // ── Through the engine ───────────────────────────────────────────

        private static readonly PlayerColor[] BlueFirst = { PlayerColor.Blue, PlayerColor.Red };

        private static MatchFactory.Match NewMatch(int seed)
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                [PlayerColor.Red] = new[] { Revu.Definition, Bouncer.Definition, Mimi.Definition },
                [PlayerColor.Blue] = new[] { Bouncer.Definition, Javi.Definition, Kian.Definition }
            };

            var match = MatchFactory.Create(BlueFirst, seed, squads, openingDeployments: 3);
            match.Engine.Start();
            Assert.That(match.Engine.CurrentPlayer.Color, Is.EqualTo(PlayerColor.Blue), "Blue opens");
            return match;
        }

        private static void PlaceAtTrack(MatchFactory.Match match, OperatorState op, int track)
        {
            int circuit = match.Map.Profile.CircuitLength;
            op.MoveTo(((track - match.Map.StartTrackIndex(op.Owner)) % circuit + circuit) % circuit);
        }

        [Test]
        public void TheEngine_CollectsTheDebt_AsTheDebtorEndsItsTurn()
        {
            var match = NewMatch(3);
            var engine = match.Engine;
            var blue = match.Players.First(p => p.Color == PlayerColor.Blue);
            var revu = match.Operators.First(o => o.Name == "Revú");
            PlaceAtTrack(match, revu, 30);   // out of everyone's way

            new EnergyLedger(EnergyConfig.Default).IncurDebt(blue, 3, revu.Id);

            engine.Execute(new RollDiceCommand());
            Assert.That(blue.Debt, Is.EqualTo(3), "nothing is collected while the turn runs");

            for (int guard = 0; guard < 8 && engine.PreviewLandings().Count > 0; guard++)
            {
                var move = engine.PreviewLandings()[0];
                engine.Execute(new MoveCommand(move.OperatorId, move.DieFace));
            }

            int pool = blue.Energy;
            var events = engine.Execute(new EndTurnCommand()).ToList();

            var bill = events.OfType<DebtCollected>().Single();
            int paid = System.Math.Min(3, pool);

            Assert.That(bill.Player, Is.EqualTo(PlayerColor.Blue));
            Assert.That(bill.Paid, Is.EqualTo(paid));
            Assert.That(bill.Remaining, Is.EqualTo(pool - paid));
            Assert.That(blue.Energy, Is.EqualTo(pool - paid));
            Assert.That(blue.Debt, Is.EqualTo(bill.Owed));
            Assert.That(events.IndexOf(bill), Is.LessThan(events.FindIndex(e => e is TurnEnded)),
                "the bill closes the debtor's own turn");
        }

        [Test]
        public void TheEngine_CollectsNothing_FromASeatThatOwesNothing()
        {
            var match = NewMatch(3);
            var engine = match.Engine;

            engine.Execute(new RollDiceCommand());
            for (int guard = 0; guard < 8 && engine.PreviewLandings().Count > 0; guard++)
            {
                var move = engine.PreviewLandings()[0];
                engine.Execute(new MoveCommand(move.OperatorId, move.DieFace));
            }

            var events = engine.Execute(new EndTurnCommand());

            Assert.That(events.OfType<DebtCollected>(), Is.Empty);
        }

        [Test]
        public void LandingOnTheCreditor_BurnsTheWholeDebt()
        {
            for (int seed = 1; seed < 400; seed++)
            {
                var match = NewMatch(seed);
                var engine = match.Engine;
                var blue = match.Players.First(p => p.Color == PlayerColor.Blue);
                var revu = match.Operators.First(o => o.Name == "Revú");
                var bouncer = match.Operators.First(o => o.Owner == PlayerColor.Blue && o.Name == "Bouncer");

                PlaceAtTrack(match, revu, 10);
                PlaceAtTrack(match, bouncer, 6);
                new EnergyLedger(EnergyConfig.Default).IncurDebt(blue, 3, revu.Id);

                engine.Execute(new RollDiceCommand());

                var revuCell = match.Map.CellAt(PlayerColor.Red, revu.Progress);
                var landings = engine.PreviewLandings().Where(p =>
                    p.OperatorId == bouncer.Id && match.Map.CellAt(PlayerColor.Blue, p.Progress).Equals(revuCell)).ToList();
                if (landings.Count == 0) continue;
                var onto = landings[0];

                var events = engine.Execute(new MoveCommand(onto.OperatorId, onto.DieFace));
                var burned = events.OfType<DebtBurned>().Single();

                Assert.That(burned.Player, Is.EqualTo(PlayerColor.Blue));
                Assert.That(burned.Amount, Is.EqualTo(3));
                Assert.That(burned.Debtor, Is.SameAs(bouncer));
                Assert.That(burned.Creditor, Is.SameAs(revu));
                Assert.That(blue.Debt, Is.EqualTo(0));
                return;
            }

            Assert.Fail("No seed let Blue's Bouncer land on Revú.");
        }

        [Test]
        public void LandingOnSomebodyElse_BurnsNothing()
        {
            for (int seed = 1; seed < 400; seed++)
            {
                var match = NewMatch(seed);
                var engine = match.Engine;
                var blue = match.Players.First(p => p.Color == PlayerColor.Blue);
                var revu = match.Operators.First(o => o.Name == "Revú");
                var mimi = match.Operators.First(o => o.Name == "Mimi");
                var bouncer = match.Operators.First(o => o.Owner == PlayerColor.Blue && o.Name == "Bouncer");

                PlaceAtTrack(match, revu, 30);
                PlaceAtTrack(match, mimi, 10);
                PlaceAtTrack(match, bouncer, 6);
                new EnergyLedger(EnergyConfig.Default).IncurDebt(blue, 3, revu.Id);

                engine.Execute(new RollDiceCommand());

                var mimiCell = match.Map.CellAt(PlayerColor.Red, mimi.Progress);
                var landings = engine.PreviewLandings().Where(p =>
                    p.OperatorId == bouncer.Id && match.Map.CellAt(PlayerColor.Blue, p.Progress).Equals(mimiCell)).ToList();
                if (landings.Count == 0) continue;
                var onto = landings[0];

                var events = engine.Execute(new MoveCommand(onto.OperatorId, onto.DieFace));

                Assert.That(events.OfType<CollisionResolved>(), Is.Not.Empty, "precondition: a collision");
                Assert.That(events.OfType<DebtBurned>(), Is.Empty);
                Assert.That(blue.Debt, Is.EqualTo(3));
                return;
            }

            Assert.Fail("No seed let Blue's Bouncer land on Mimi.");
        }
    }
}