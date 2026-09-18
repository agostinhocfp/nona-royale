// Assets/Tests/EditMode/Abilities/FortunaTableTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Abilities
{
    /// <summary>
    /// The Table (COMBAT_SYSTEMS §7.7, ADR-0007 Amendment 3, 2026-09-18): the
    /// first effect in the game that reads the cells a move passes through, and
    /// the only one that can shorten a move.
    /// </summary>
    /// <remarks>
    /// Two halves. The interception rules are tested against
    /// <c>DeferredCellEffects</c> directly, where the path is an argument and
    /// every edge is reachable; the stop itself is tested through the engine,
    /// because truncating a move and billing the mover is the engine's work.
    /// </remarks>
    [TestFixture]
    public class FortunaTableTests
    {
        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };

        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;
        private DamagePipeline _damage;
        private DeferredCellEffects _cellEffects;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _clock = new FakeClock();
            _statuses = new StatusRegistry(_clock, CombatConfig.Default);
            _targeting = new TargetingRules(_map, _statuses);
            _damage = new DamagePipeline(_statuses, new SeededRandom(7));
            _cellEffects = new DeferredCellEffects(_clock, _targeting, _damage, _statuses);
        }

        private static OperatorState At(int id, string name, PlayerColor owner, int hp, int progress)
        {
            var op = new OperatorState(id, name, owner, hp, 1.0);
            op.MoveTo(progress);
            return op;
        }

        private static IReadOnlyList<CellRef> Path(params int[] tracks) =>
            tracks.Select(CellRef.Track).ToList();

        // ── Interception rules (§7.7) ────────────────────────────────────

        [Test]
        public void ItStopsTheFirstTableOnThePath_NotTheFurthest()
        {
            var mover = At(1, "Runner", PlayerColor.Blue, 7, 5);

            _cellEffects.SetTable(CellRef.Track(22), PlayerColor.Red, 10, Fortuna.TableDamage, 2);
            _cellEffects.SetTable(CellRef.Track(20), PlayerColor.Red, 10, Fortuna.TableDamage, 2);

            var hit = _cellEffects.FirstInterception(mover.Owner, mover.Id, Path(19, 20, 21, 22, 23));

            Assert.That(hit, Is.Not.Null);
            Assert.That(hit.Value.Cell, Is.EqualTo(CellRef.Track(20)));
            Assert.That(hit.Value.Cells, Is.EqualTo(2), "two steps along the path, so the move is two cells");
            Assert.That(hit.Value.Damage, Is.EqualTo(Fortuna.TableDamage));
        }

        [Test]
        public void ItStopsEachOperatorOnce()
        {
            var first = At(1, "First", PlayerColor.Blue, 7, 5);
            var second = At(2, "Second", PlayerColor.Blue, 7, 5);
            var cell = CellRef.Track(20);

            _cellEffects.SetTable(cell, PlayerColor.Red, 10, Fortuna.TableDamage, 2);

            var stop = _cellEffects.FirstInterception(first.Owner, first.Id, Path(20));
            Assert.That(stop, Is.Not.Null);

            _cellEffects.BillStop(stop.Value, first);

            Assert.That(_cellEffects.FirstInterception(first.Owner, first.Id, Path(20)), Is.Null,
                "a table never holds the same operator twice");
            Assert.That(_cellEffects.FirstInterception(second.Owner, second.Id, Path(20)), Is.Not.Null,
                "and it is not spent by the first one either");
        }

        [Test]
        public void ItBillsThroughThePipeline_AndCreditsTheSourceSeat()
        {
            var mover = At(1, "Runner", PlayerColor.Blue, 7, 5);
            _cellEffects.SetTable(CellRef.Track(20), PlayerColor.Red, 10, Fortuna.TableDamage, 2);

            var stop = _cellEffects.FirstInterception(mover.Owner, mover.Id, Path(20)).Value;
            var billed = _cellEffects.BillStop(stop, mover);

            Assert.That(mover.Health, Is.EqualTo(7 - Fortuna.TableDamage));
            Assert.That(billed.Cause, Is.EqualTo(DeferredCellEffects.TableCause));
            Assert.That(stop.SourceOperatorId, Is.EqualTo(10), "a kill here pays the operator that dealt it");
        }

        [Test]
        public void AlliesAndTheHouseCrossFreely()
        {
            var ally = At(1, "Ally", PlayerColor.Red, 7, 5);
            _cellEffects.SetTable(CellRef.Track(20), PlayerColor.Red, 10, Fortuna.TableDamage, 2);

            Assert.That(_cellEffects.FirstInterception(ally.Owner, ally.Id, Path(20)), Is.Null);
        }

        [Test]
        public void ItBillsNothingAtAnUpkeep()
        {
            var mover = At(1, "Runner", PlayerColor.Blue, 7, 5);
            var board = new List<OperatorState> { mover };

            _cellEffects.SetTable(CellRef.Track(20), PlayerColor.Red, 10, Fortuna.TableDamage, 2);
            mover.MoveTo(_map.Profile.CircuitLength);

            _clock.BeginTurnFor(PlayerColor.Red);
            _clock.BeginTurnFor(PlayerColor.Red);

            var fired = _cellEffects.Fire(PlayerColor.Red, board);

            Assert.That(fired, Is.Empty, "a table waits for traffic, not for a clock");
        }

        [Test]
        public void ItRetiresAfterItsLastTurn()
        {
            var cell = CellRef.Track(20);
            var mover = At(1, "Runner", PlayerColor.Blue, 7, 5);
            var board = new List<OperatorState> { mover };

            _cellEffects.SetTable(cell, PlayerColor.Red, 10, Fortuna.TableDamage, Fortuna.TableLifetimeTurns);

            for (int turn = 0; turn < Fortuna.TableLifetimeTurns; turn++)
            {
                _clock.BeginTurnFor(PlayerColor.Red);
                _cellEffects.Fire(PlayerColor.Red, board);
            }

            Assert.That(_cellEffects.HasTableOn(cell, PlayerColor.Red), Is.False);
            Assert.That(_cellEffects.FirstInterception(PlayerColor.Blue, 1, Path(20)), Is.Null);
        }

        [Test]
        public void ARedealForgetsWhoItStopped()
        {
            var mover = At(1, "Runner", PlayerColor.Blue, 7, 5);
            var cell = CellRef.Track(20);

            _cellEffects.SetTable(cell, PlayerColor.Red, 10, Fortuna.TableDamage, 2);
            _cellEffects.BillStop(_cellEffects.FirstInterception(mover.Owner, mover.Id, Path(20)).Value, mover);
            Assert.That(_cellEffects.FirstInterception(mover.Owner, mover.Id, Path(20)), Is.Null);

            _cellEffects.SetTable(cell, PlayerColor.Red, 10, Fortuna.TableDamage, 2);

            Assert.That(_cellEffects.FirstInterception(mover.Owner, mover.Id, Path(20)), Is.Not.Null,
                "paying for it again buys a fresh game");
        }

        // ── The stop, through the engine (§7.7) ──────────────────────────

        /// <summary>
        /// A Red Fortuna in reach of a cell a lone Blue runner is about to cross.
        /// Blue's other two operators are parked far behind, so nothing else of
        /// Blue's is worth moving and no collision interferes.
        /// </summary>
        private MatchFactory.Match Staged(out OperatorState fortuna, out OperatorState runner)
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                [PlayerColor.Red] = new[] { Fortuna.Definition, Bouncer.Definition, Mimi.Definition },
                [PlayerColor.Blue] = new[] { Kian.Definition, Nuetu.Definition, Javi.Definition }
            };

            var match = MatchFactory.Create(Two, 31, squads, openingDeployments: 3);
            match.Engine.Start();

            fortuna = match.Operators.First(o => o.Name == "Fortuna");
            runner = match.Operators.First(o => o.Name == "Kian" && o.Owner == PlayerColor.Blue);

            // TRACK cells. Red starts at 0 and Blue at 13, so Blue's runner at
            // track 20 walks toward 21, 22, 23. The table goes on 22, two steps
            // from a Fortuna standing on 24. No start cells (0, 13, 26, 39).
            Place(match, fortuna, 24);
            Place(match, runner, 20);
            Place(match, match.Operators.First(o => o.Name == "Nuetu"), 15);
            Place(match, match.Operators.First(o => o.Name == "Javi" && o.Owner == PlayerColor.Blue), 16);

            match.Energy.GrantBounty(match.Players[0], match.Engine.EnergyCap);
            return match;
        }

        private static void Place(MatchFactory.Match match, OperatorState op, int track)
        {
            int circuit = match.Map.Profile.CircuitLength;
            op.MoveTo((track - match.Map.StartTrackIndex(op.Owner) + circuit) % circuit);
        }

        /// <summary>
        /// Ends whoever's turn it is: spends what the seat is holding on its own
        /// last operator, then hands over.
        /// </summary>
        private static void PassTurn(MatchFactory.Match match)
        {
            var seat = match.Engine.CurrentPlayer;
            var spender = seat.Operators[seat.Operators.Count - 1];

            // Rolling is compulsory too, so a seat that has not rolled cannot
            // simply hand over (§6.1).
            if (match.Engine.Phase == TurnPhase.AwaitingRoll)
                match.Engine.Execute(new RollDiceCommand());

            while (match.Engine.MustSpendRoll && match.Engine.UnspentDice.Count > 0)
                match.Engine.Execute(new MoveCommand(spender.Id, null));

            match.Engine.Execute(new EndTurnCommand());
        }

        [Test]
        public void ItStopsARunAndBillsIt()
        {
            var match = Staged(out var fortuna, out var runner);
            var cell = CellRef.Track(22);

            match.Engine.Execute(new RollDiceCommand());
            match.Engine.Execute(new UseAbilityCommand(fortuna.Id, Fortuna.TheTable.Id, null, cell));
            Assert.That(match.Engine.HasTableOn(cell, PlayerColor.Red), Is.True);

            PassTurn(match);

            match.Engine.Execute(new RollDiceCommand());
            int before = runner.Progress;
            int health = runner.Health;

            // The whole roll on the runner: it would carry him past 22, and the
            // table takes the rest of the move.
            var events = match.Engine.Execute(new MoveCommand(runner.Id, null));
            var stopped = events.OfType<MoveIntercepted>().FirstOrDefault();

            Assert.That(stopped, Is.Not.Null, "the run should have been stopped");
            Assert.That(stopped.Cell, Is.EqualTo(cell));
            Assert.That(stopped.Owner, Is.EqualTo(PlayerColor.Red));
            Assert.That(runner.Progress, Is.EqualTo(before + 2), "two cells, not the whole roll");
            Assert.That(runner.Health, Is.EqualTo(health - Fortuna.TableDamage));
        }

        [Test]
        public void ThePreviewShowsTheShortenedLanding()
        {
            var match = Staged(out var fortuna, out var runner);

            match.Engine.Execute(new RollDiceCommand());
            match.Engine.Execute(new UseAbilityCommand(fortuna.Id, Fortuna.TheTable.Id, null, CellRef.Track(22)));
            PassTurn(match);
            match.Engine.Execute(new RollDiceCommand());

            var pooled = match.Engine.PreviewLandings()
                .Where(p => p.OperatorId == runner.Id && p.DieFace == null)
                .ToList();

            Assert.That(pooled, Is.Not.Empty);
            Assert.That(pooled[0].Cells, Is.EqualTo(2), "hiding the truncation would show a landing that never happens");
            Assert.That(pooled[0].Progress, Is.EqualTo(runner.Progress + 2));
        }

        [Test]
        public void SomebodyStandingOnItLeavesFreely()
        {
            var match = Staged(out var fortuna, out var runner);

            match.Engine.Execute(new RollDiceCommand());
            match.Engine.Execute(new UseAbilityCommand(fortuna.Id, Fortuna.TheTable.Id, null, CellRef.Track(22)));
            PassTurn(match);

            match.Engine.Execute(new RollDiceCommand());
            match.Engine.Execute(new MoveCommand(runner.Id, null));
            int stoppedAt = runner.Progress;

            // Blue hands back, Red passes, and Blue comes round again. The runner
            // is standing on the table, and the path excludes the cell a move
            // starts on — so nothing stops him.
            PassTurn(match);
            PassTurn(match);
            match.Engine.Execute(new RollDiceCommand());
            var events = match.Engine.Execute(new MoveCommand(runner.Id, null));

            Assert.That(events.OfType<MoveIntercepted>().Any(), Is.False);
            Assert.That(runner.Progress, Is.GreaterThan(stoppedAt));
        }
    }
}
