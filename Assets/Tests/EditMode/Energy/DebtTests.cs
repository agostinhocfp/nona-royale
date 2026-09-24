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
    /// growth at the end of the debtor's turn and the burn on a collision,
    /// through the engine. Nothing is ever paid. What Revú's own abilities do with it is in
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
        public void Accrue_GrowsTheWholeDebt_AndTakesNoEnergy()
        {
            SetPool(9);
            _ledger.IncurDebt(_seat, 3, Creditor);

            var growth = _ledger.AccrueDebt(_seat);

            Assert.That((growth.OwedBefore, growth.Interest, growth.Owed), Is.EqualTo((3, 1, 4)));
            Assert.That(_seat.Debt, Is.EqualTo(4));
            Assert.That(_seat.Energy, Is.EqualTo(9), "a full pool pays nothing: debt is never collected");
            Assert.That(_seat.OwesTo(Creditor), Is.True);
        }

        [Test]
        public void Accrue_NeverPassesTheCap()
        {
            _ledger.IncurDebt(_seat, 6, Creditor);

            var growth = _ledger.AccrueDebt(_seat);

            Assert.That((growth.Interest, growth.Owed), Is.EqualTo((0, 6)));
        }

        [Test]
        public void Accrue_JustBelowTheCap_ChargesOnlyWhatFits()
        {
            _ledger.IncurDebt(_seat, 5, Creditor);

            var growth = _ledger.AccrueDebt(_seat);

            Assert.That((growth.Interest, growth.Owed), Is.EqualTo((1, 6)));
        }

        [Test]
        public void Accrue_WithNoDebt_DoesNothing()
        {
            SetPool(8);

            var growth = _ledger.AccrueDebt(_seat);

            Assert.That(growth.Happened, Is.False);
            Assert.That(_seat.Debt, Is.EqualTo(0));
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

        private static IReadOnlyList<IGameEvent> PlayOutAndEnd(GameEngine engine)
        {
            engine.Execute(new RollDiceCommand());
            for (int guard = 0; guard < 8 && engine.PreviewLandings().Count > 0; guard++)
            {
                var move = engine.PreviewLandings()[0];
                engine.Execute(new MoveCommand(move.OperatorId, move.DieFace));
            }

            return engine.Execute(new EndTurnCommand());
        }

        [Test]
        public void TheEngine_GrowsTheDebt_AsTheDebtorEndsItsTurn()
        {
            var match = NewMatch(3);
            var blue = match.Players.First(p => p.Color == PlayerColor.Blue);
            var revu = match.Operators.First(o => o.Name == "Revú");
            PlaceAtTrack(match, revu, 30);   // out of everyone's way

            new EnergyLedger(EnergyConfig.Default).IncurDebt(blue, 3, revu.Id);

            match.Engine.Execute(new RollDiceCommand());
            int pool = blue.Energy;
            Assert.That(blue.Debt, Is.EqualTo(3), "nothing changes while the turn runs");

            var events = PlayOutAndEnd(match.Engine).ToList();
            var growth = events.OfType<DebtAccrued>().Single();

            Assert.That(growth.Player, Is.EqualTo(PlayerColor.Blue));
            Assert.That((growth.Interest, growth.Owed), Is.EqualTo((1, 4)));
            Assert.That(blue.Debt, Is.EqualTo(4));
            Assert.That(blue.Energy, Is.LessThanOrEqualTo(pool), "only its own casts could spend it");
            Assert.That(events.OfType<EnergySpent>(), Is.Empty, "the turn end took nothing from the pool");
            Assert.That(events.IndexOf(growth), Is.LessThan(events.FindIndex(e => e is TurnEnded)),
                "the debt grows as the debtor's own turn closes");
        }

        [Test]
        public void TheEngine_ReportsNothing_ForADebtAtTheCap()
        {
            var match = NewMatch(3);
            var blue = match.Players.First(p => p.Color == PlayerColor.Blue);
            var revu = match.Operators.First(o => o.Name == "Revú");
            PlaceAtTrack(match, revu, 30);

            new EnergyLedger(EnergyConfig.Default).IncurDebt(blue, 6, revu.Id);

            var events = PlayOutAndEnd(match.Engine);

            Assert.That(events.OfType<DebtAccrued>(), Is.Empty, "nothing new to say every turn");
            Assert.That(blue.Debt, Is.EqualTo(6));
        }

        [Test]
        public void TheEngine_ReportsNothing_ForASeatThatOwesNothing()
        {
            var match = NewMatch(3);

            var events = PlayOutAndEnd(match.Engine);

            Assert.That(events.OfType<DebtAccrued>(), Is.Empty);
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