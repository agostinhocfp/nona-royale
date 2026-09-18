// Assets/Tests/EditMode/Bots/FortunaBotTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Bots
{
    /// <summary>
    /// What the bots had to learn for Fortuna (2026-09-18): a die is worth what
    /// the best landing it could buy is worth, a re-deal is worth the pips it
    /// expects to gain, a set double is worth the roll it is owed, and a table is
    /// worth the traffic behind it.
    /// </summary>
    /// <remarks>
    /// Without any of this she would measure as the worst operator on the roster
    /// — the Predator's Read failure, where an ability the planner had no branch
    /// for was cast 0.00 times a match.
    /// </remarks>
    [TestFixture]
    public class FortunaBotTests
    {
        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };

        private MatchFactory.Match _match;
        private PlayerState _seat;
        private OperatorState _fortuna;
        private OperatorState _bouncer;
        private OperatorState _mimi;
        private OperatorState[] _foes;

        [SetUp]
        public void SetUp()
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                { PlayerColor.Red, new[] { Fortuna.Definition, Bouncer.Definition, Mimi.Definition } },
                { PlayerColor.Blue, new[] { Kian.Definition, Nuetu.Definition, Javi.Definition } }
            };

            _match = MatchFactory.Create(Two, 19, squads);
            _match.Engine.Start();
            _seat = _match.Players[0];

            _fortuna = Op(PlayerColor.Red, "Fortuna");
            _bouncer = Op(PlayerColor.Red, "Bouncer");
            _mimi = Op(PlayerColor.Red, "Mimi");
            _foes = new[] { Op(PlayerColor.Blue, "Kian"), Op(PlayerColor.Blue, "Nuetu"), Op(PlayerColor.Blue, "Javi") };

            // Red's three on track 8–10, Blue's parked far away until a test
            // brings them in. No start cells (0, 13, 26, 39).
            Place(_fortuna, 8);
            Place(_bouncer, 9);
            Place(_mimi, 10);
            for (int i = 0; i < _foes.Length; i++) Place(_foes[i], 30 + i);
        }

        private OperatorState Op(PlayerColor seat, string name)
        {
            foreach (var op in _match.Operators)
                if (op.Owner == seat && op.Name == name) return op;

            throw new ArgumentException($"{seat} has no {name}");
        }

        private void Place(OperatorState op, int track)
        {
            int circuit = _match.Map.Profile.CircuitLength;
            op.MoveTo((track - _match.Map.StartTrackIndex(op.Owner) + circuit) % circuit);
        }

        private CellRef Track(int track) => CellRef.Track(track);

        private static void Yard(OperatorState op) => op.MoveTo(PathMap.YardProgress);

        private static BotWeights Weights => BotWeights.For(BotPersonality.Brawler);

        private BotBoard Board() => new BotBoard(_match);

        private ScoredCast Score(AbilityDefinition ability, CellRef? cell = null) =>
            CastPlanner.Score(Board(), Weights, _seat, _fortuna, ability, null, cell, null);

        /// <summary>Rolls for the seat whose turn it is, so the dice are in hand.</summary>
        private void Roll() => _match.Engine.Execute(new RollDiceCommand());

        // ── The cashed die (§3.4) ────────────────────────────────────────

        [Test]
        public void CashingIsWorthNothingWhenTheDieCouldDoSomething()
        {
            Roll();

            // Nothing threatens Red and everyone can move, so every die has a
            // landing worth more than two energy.
            var moves = MoveScorer.Rank(Board(), Weights, null);
            var cash = CashPlanner.Best(Board(), Weights, moves);

            Assert.That(cash, Is.Null, "a die is worth more than the pool most turns");
        }

        [Test]
        public void CashingWinsWhenEveryLandingIsWorse()
        {
            Roll();

            // A move scorer that hates every option: the die is then worth only
            // what the pool pays for it. Passing an empty list is the honest way
            // to express "no landing is worth anything" without staging a board
            // that takes twenty cells of setup to arrange.
            var cash = CashPlanner.Best(Board(), Weights, new List<ScoredMove>());

            Assert.That(cash, Is.Not.Null);
            Assert.That(cash.Value.Seller.Id, Is.EqualTo(_fortuna.Id), "only she can sell");
            Assert.That(cash.Value.Value, Is.EqualTo(_match.Engine.CashedDieEnergy * Weights.EnergyGain));
            Assert.That(_match.Engine.UnspentDice, Contains.Item(cash.Value.DieFace));
        }

        [Test]
        public void ADeployIsNeverSold()
        {
            // A six with somebody in the yard outscores the pool by a mile, and
            // the comparison — not a special case — is what says so.
            // Scanned for a roll holding a six, with two of Red's three waiting
            // in the yard, so the deploy is on the table to be compared against.
            for (int seed = 1; seed < 4000; seed++)
            {
                var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
                {
                    [PlayerColor.Red] = new[] { Fortuna.Definition, Bouncer.Definition, Mimi.Definition }
                };

                var match = MatchFactory.Create(new[] { PlayerColor.Red }, seed, squads, openingDeployments: 1);
                match.Engine.Start();
                match.Engine.Execute(new RollDiceCommand());

                if (!match.Engine.UnspentDice.Contains(6)) continue;
                if (match.Operators.First(o => o.Name == "Fortuna").IsInYard) continue;

                var board = new BotBoard(match);
                var moves = MoveScorer.Rank(board, Weights, null);
                var cash = CashPlanner.Best(board, Weights, moves);

                Assert.That(cash == null || cash.Value.DieFace != 6, Is.True,
                    "a six with somebody in the yard is worth more than any pool");
                return;
            }

            Assert.Fail("no seed dealt a six with the yard occupied");
        }

        [Test]
        public void TheBrainSellsTheDie_AndTheEngineTakesIt()
        {
            // End to end: a brain on a seat holding Fortuna produces the command,
            // and the engine accepts it.
            Roll();

            var brain = new BotBrain(BotPersonality.Banker, new NonaRoyale.Core.Rng.SeededRandom(3));
            var commands = new List<ICommand>();

            for (int step = 0; step < 12; step++)
            {
                var command = brain.Next(_match);
                if (command == null) break;

                commands.Add(command);
                var events = _match.Engine.Execute(command);
                brain.Observe(command, events);

                Assert.That(events.OfType<CommandRejected>().Any(), Is.False,
                    $"the brain proposed something illegal: {BotBrain.Key(command)}");

                if (command is EndTurnCommand) break;
            }

            Assert.That(brain.RefusalCount, Is.EqualTo(0));
        }

        // ── The dice (§6.8) ──────────────────────────────────────────────

        [Test]
        public void ARedealIsWorthMoreWithAWorseDieInHand()
        {
            Roll();

            var hand = _match.Engine.UnspentDice.ToList();
            double value = Score(Fortuna.DealAgain).Defence;

            // The low die is what a re-deal replaces, so the worse it is, the
            // more the cast is worth. Both figures come from the same hand.
            double expected = Math.Max(0.0, 3.5 - hand.Min()) * Weights.Progress * Weights.Defence;

            Assert.That(value, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void ARedealIsWorthNothingWithTwoHighDice()
        {
            // A hand of sixes has nothing to gain from the cup. Scanned rather
            // than staged: the engine owns the roll.
            for (int seed = 1; seed < 4000; seed++)
            {
                var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
                {
                    [PlayerColor.Red] = new[] { Fortuna.Definition, Bouncer.Definition, Mimi.Definition }
                };

                var match = MatchFactory.Create(new[] { PlayerColor.Red }, seed, squads, openingDeployments: 3);
                match.Engine.Start();
                match.Engine.Execute(new RollDiceCommand());

                if (match.Engine.UnspentDice.Min() < 5) continue;

                var board = new BotBoard(match);
                var fortuna = match.Operators.First(o => o.Name == "Fortuna");
                var scored = CastPlanner.Score(
                    board, Weights, match.Players[0], fortuna, Fortuna.DealAgain, null, null, null);

                Assert.That(scored.Defence, Is.EqualTo(0.0), "nothing to gain from a hand of high dice");
                Assert.That(scored.Score, Is.LessThan(0.0), "so the cast is worth less than its cost");
                return;
            }

            Assert.Fail("no seed rolled two dice of five or better");
        }

        [Test]
        public void BoxcarsIsWorthThePipsAndTheRollItBuys()
        {
            Roll();

            int held = _match.Engine.UnspentDice.Sum();
            double value = Score(Fortuna.Boxcars).Defence;

            // Twelve pips instead of what is in hand, plus a discounted extra
            // roll, all in cells (§6.8).
            double pips = (12 - held) * Weights.Progress;
            double extra = 2 * 3.5 * Weights.Progress * Weights.DelayedDiscount;

            Assert.That(value, Is.EqualTo((pips + extra) * Weights.Defence).Within(1e-9));
            Assert.That(value, Is.GreaterThan(Score(Fortuna.DealAgain).Defence), "the ult buys more than the basic");
        }

        [Test]
        public void ARedealIsWorthADeployWhenSomebodyIsWaiting()
        {
            Roll();
            double quiet = Score(Fortuna.DealAgain).Defence;

            Yard(_mimi);
            double waiting = Score(Fortuna.DealAgain).Defence;

            Assert.That(waiting, Is.GreaterThan(quiet), "a six takes somebody out of the yard");
        }

        // ── The table (§7.7) ─────────────────────────────────────────────

        [Test]
        public void ATableIsWorthNothingWithNoTrafficBehindIt()
        {
            // Blue is parked on 30–32, and a table on 20 is behind all of them.
            Assert.That(Score(Fortuna.TheTable, Track(20)).Offence, Is.EqualTo(0.0));
        }

        [Test]
        public void ATableIsWorthMoreInFrontOfARunner()
        {
            double empty = Score(Fortuna.TheTable, Track(20)).Offence;

            // Blue moves toward higher track indices from its start at 13, so a
            // runner on 18 is two cells short of a table on 20.
            Place(_foes[0], 18);
            double traffic = Score(Fortuna.TheTable, Track(20)).Offence;

            Assert.That(empty, Is.EqualTo(0.0));
            Assert.That(traffic, Is.GreaterThan(0.0));
        }

        [Test]
        public void ATableIgnoresWhatHasAlreadyPassedIt()
        {
            // A runner one cell beyond the table can never be stopped by it.
            Place(_foes[0], 21);

            Assert.That(Score(Fortuna.TheTable, Track(20)).Offence, Is.EqualTo(0.0));
        }

        [Test]
        public void TheDraftPaysForHerTempo()
        {
            // The same kit with the dice and the passive taken out. The gap is
            // the tempo term and nothing else: two dice abilities and the House
            // Edge, at DraftTempo each, plus the table's control.
            var weights = BotWeights.For(BotPersonality.Brawler);

            var stripped = new OperatorDefinition(
                "Stripped", Fortuna.MaxHealth, Fortuna.Speed,
                new[] { Fortuna.TheTable });

            double gap = DraftPicker.Value(Fortuna.Definition, weights)
                         - DraftPicker.Value(stripped, weights);

            Assert.That(gap, Is.EqualTo(3 * weights.DraftTempo).Within(1e-9));
        }

        [Test]
        public void TheDraftStillReadsHerLast_AndThatIsRecorded()
        {
            // An honest failure rather than a weight bent until it passes: the
            // picker values damage, burst and sustain, and she has none of the
            // three (BOTS.md, 2026-09-18). It costs nothing in the sweep, where
            // squads are drafted at random, and it is an open item for the draft
            // screen.
            var weights = BotWeights.For(BotPersonality.Brawler);
            double her = DraftPicker.Value(Fortuna.Definition, weights);

            var field = Roster.All
                .Where(op => op.Name != "Fortuna")
                .Select(op => DraftPicker.Value(op, weights))
                .ToList();

            Assert.That(her, Is.LessThan(field.Min()));
            Assert.That(her, Is.GreaterThan(field.Min() * 0.5),
                "last is one thing; unpickable is another");
        }
    }
}
