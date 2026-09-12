// Assets/Tests/EditMode/Engine/GameEngineTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Engine
{
    [TestFixture]
    public class GameEngineTests
    {
        private MatchFactory.Match _match;
        private GameEngine _engine;

        [SetUp]
        public void SetUp()
        {
            _match = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red, PlayerColor.Blue }, seed: 7);
            _engine = _match.Engine;
            _engine.Start();
        }

        private static bool Has<T>(IReadOnlyList<IGameEvent> events) where T : IGameEvent =>
            events.Any(e => e is T);

        private static T First<T>(IReadOnlyList<IGameEvent> events) where T : class, IGameEvent =>
            events.FirstOrDefault(e => e is T) as T;

        private OperatorState Op(PlayerColor seat, string name) =>
            _match.Operators.First(o => o.Owner == seat && o.Name == name);

        /// <summary>Rolls until the dice hand us what a test needs, ending turns in between.</summary>
        private IReadOnlyList<IGameEvent> RollUntil(Func<DiceRoll, bool> wanted, int maxTurns = 200)
        {
            for (int i = 0; i < maxTurns; i++)
            {
                var events = _engine.Execute(new RollDiceCommand());
                var rolled = First<DiceRolled>(events);

                if (rolled != null && wanted(rolled.Roll)) return events;

                _engine.Execute(new EndTurnCommand());
            }

            throw new InvalidOperationException("The dice never produced the roll this test needs.");
        }

        // ── The boundary itself ──────────────────────────────────────────

        [Test]
        public void StartingAMatch_BeginsTheFirstTurn()
        {
            var fresh = MatchFactory.CreateAlphaMatch(new[] { PlayerColor.Red }, seed: 1);

            var events = fresh.Engine.Start();

            Assert.That(Has<TurnBegan>(events), Is.True);
            Assert.That(fresh.Engine.Phase, Is.EqualTo(TurnPhase.AwaitingRoll));
        }

        [Test]
        public void EveryCommandReturnsEvents_AndNeverThrows()
        {
            // The view reacts to events and nothing else. A command it should
            // not have sent must come back as a rejection, not an exception.
            var events = _engine.Execute(new MoveCommand(1));

            Assert.That(Has<CommandRejected>(events), Is.True);
        }

        [Test]
        public void ActingBeforeRolling_IsRejected()
        {
            var events = _engine.Execute(new DeployCommand(Op(PlayerColor.Red, "Syla").Id));

            Assert.That(First<CommandRejected>(events).Reason, Is.EqualTo("roll first"));
        }

        [Test]
        public void CommandingAnotherSeatsOperator_IsRejected()
        {
            _engine.Execute(new RollDiceCommand());
            var enemy = Op(PlayerColor.Blue, "Syla");

            var events = _engine.Execute(new DeployCommand(enemy.Id));

            Assert.That(Has<CommandRejected>(events), Is.True);
        }

        [Test]
        public void AnUnknownOperatorId_IsRejected()
        {
            _engine.Execute(new RollDiceCommand());

            Assert.That(Has<CommandRejected>(_engine.Execute(new MoveCommand(9999))), Is.True);
        }

        // ── Rolling ──────────────────────────────────────────────────────

        [Test]
        public void TheFirstRollOfATurn_GrantsEnergy()
        {
            var events = _engine.Execute(new RollDiceCommand());

            Assert.That(Has<DiceRolled>(events), Is.True);
            Assert.That(Has<EnergyGranted>(events), Is.True);
        }

        [Test]
        public void RollingTwiceWithoutDoubles_IsRejected()
        {
            var events = RollUntil(r => !r.IsDouble);

            Assert.That(First<DiceRolled>(events).GrantsAnotherRoll, Is.False);
            Assert.That(Has<CommandRejected>(_engine.Execute(new RollDiceCommand())), Is.True);
        }

        [Test]
        public void ADoublesReroll_GrantsNoSecondEnergyEvent()
        {
            RollUntil(r => r.IsDouble);

            var second = _engine.Execute(new RollDiceCommand());

            Assert.That(Has<DiceRolled>(second), Is.True);
            Assert.That(Has<EnergyGranted>(second), Is.False);
        }

        // ── Deploy and move ──────────────────────────────────────────────

        [Test]
        public void ASixDeploysAnOperatorOntoItsStartCell()
        {
            RollUntil(r => r.Contains(6) && !r.IsDouble);
            var syla = Op(PlayerColor.Red, "Syla");

            var events = _engine.Execute(new DeployCommand(syla.Id));

            var deployed = First<OperatorDeployed>(events);
            Assert.That(deployed, Is.Not.Null);
            Assert.That(deployed.Cell, Is.EqualTo(CellRef.Track(_match.Map.StartTrackIndex(PlayerColor.Red))));
            Assert.That(syla.Progress, Is.EqualTo(0));
        }

        [Test]
        public void DeployingWithoutASix_IsRejected()
        {
            RollUntil(r => !r.Contains(6));

            var events = _engine.Execute(new DeployCommand(Op(PlayerColor.Red, "Syla").Id));

            Assert.That(Has<CommandRejected>(events), Is.True);
        }

        [Test]
        public void AnOperatorInTheYard_CannotMove()
        {
            _engine.Execute(new RollDiceCommand());

            var events = _engine.Execute(new MoveCommand(Op(PlayerColor.Red, "Syla").Id));

            Assert.That(First<CommandRejected>(events).Reason, Does.Contain("yard"));
        }

        [Test]
        public void ExactlyOneOperatorMovesPerRoll()
        {
            RollUntil(r => r.Contains(6) && !r.IsDouble);
            var syla = Op(PlayerColor.Red, "Syla");
            _engine.Execute(new DeployCommand(syla.Id));
            _engine.Execute(new MoveCommand(syla.Id));

            var second = _engine.Execute(new MoveCommand(syla.Id));

            Assert.That(First<CommandRejected>(second).Reason, Does.Contain("one operator"));
        }

        [Test]
        public void MovingEmitsWhereTheOperatorEndedUp()
        {
            RollUntil(r => r.Contains(6) && !r.IsDouble);
            var syla = Op(PlayerColor.Red, "Syla");
            _engine.Execute(new DeployCommand(syla.Id));

            var events = _engine.Execute(new MoveCommand(syla.Id));
            var moved = First<OperatorMoved>(events);

            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.To, Is.EqualTo(syla.Progress));
            Assert.That(moved.From, Is.EqualTo(0));
        }

        [Test]
        public void DeployingAfterMoving_IsRejected()
        {
            // A deploy consumes a die, so it must be declared before the
            // movement value is spent.
            RollUntil(r => r.Contains(6) && !r.IsDouble);
            var syla = Op(PlayerColor.Red, "Syla");
            var kurbyn = Op(PlayerColor.Red, "Kurbyn");
            _engine.Execute(new DeployCommand(syla.Id));
            _engine.Execute(new MoveCommand(syla.Id));

            var events = _engine.Execute(new DeployCommand(kurbyn.Id));

            Assert.That(Has<CommandRejected>(events), Is.True);
        }

        // ── Abilities ────────────────────────────────────────────────────

        [Test]
        public void AnAbilityWithoutEnergy_IsRejected()
        {
            RollUntil(r => r.Contains(6) && !r.IsDouble);
            var syla = Op(PlayerColor.Red, "Syla");
            _engine.Execute(new DeployCommand(syla.Id));

            // Nothing in range and a thin pool: either way it comes back refused.
            var events = _engine.Execute(new UseAbilityCommand(
                syla.Id, AlphaRoster.TaggedFromAbove.Id, Op(PlayerColor.Blue, "Syla").Id));

            Assert.That(Has<CommandRejected>(events), Is.True);
        }

        [Test]
        public void AnUnknownAbilityId_IsRejected()
        {
            _engine.Execute(new RollDiceCommand());

            var events = _engine.Execute(new UseAbilityCommand(Op(PlayerColor.Red, "Syla").Id, 8888));

            Assert.That(First<CommandRejected>(events).Reason, Does.Contain("ability"));
        }

        // ── Turn handover ────────────────────────────────────────────────

        [Test]
        public void EndingATurn_HandsOverAndBeginsTheNext()
        {
            _engine.Execute(new RollDiceCommand());

            var events = _engine.Execute(new EndTurnCommand());

            Assert.That(Has<TurnEnded>(events), Is.True);
            Assert.That(Has<TurnBegan>(events), Is.True);
            Assert.That(_engine.CurrentPlayer.Color, Is.EqualTo(PlayerColor.Blue));
        }

        [Test]
        public void AWonMatch_RefusesFurtherCommands()
        {
            foreach (var op in _match.Players[0].Operators)
                op.MoveTo(BoardProfile.Standard.Journey);

            _engine.Execute(new RollDiceCommand());
            var ending = _engine.Execute(new EndTurnCommand());

            Assert.That(Has<GameWon>(ending), Is.True);
            Assert.That(_engine.MatchOver, Is.True);
            Assert.That(Has<CommandRejected>(_engine.Execute(new RollDiceCommand())), Is.True);
        }

        // ── A whole match, through the boundary only ─────────────────────

        [Test]
        public void AMatchPlaysToAWinner_UsingOnlyCommandsAndEvents()
        {
            // The first exercise of the core as a black box: no service is
            // touched, no state is set by hand. A greedy scripted player deploys
            // when it can and always advances its leader.
            var match = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow },
                seed: 20260911, board: BoardProfile.Sprint);

            var engine = match.Engine;
            var log = new List<IGameEvent>(engine.Start());

            for (int turn = 0; turn < 4000 && !engine.MatchOver; turn++)
            {
                var rolled = engine.Execute(new RollDiceCommand());
                log.AddRange(rolled);

                var seat = engine.CurrentPlayer;
                var squad = seat.Operators;

                var waiting = squad.FirstOrDefault(o => o.IsInYard);
                if (waiting != null) log.AddRange(engine.Execute(new DeployCommand(waiting.Id)));

                var leader = squad
                    .Where(o => !o.IsInYard && o.Progress < BoardProfile.Sprint.Journey)
                    .OrderByDescending(o => o.Progress)
                    .FirstOrDefault();

                if (leader != null) log.AddRange(engine.Execute(new MoveCommand(leader.Id)));

                while (First<DiceRolled>(rolled) != null && First<DiceRolled>(rolled).GrantsAnotherRoll)
                {
                    rolled = engine.Execute(new RollDiceCommand());
                    log.AddRange(rolled);
                    if (Has<CommandRejected>(rolled)) break;

                    var next = squad
                        .Where(o => !o.IsInYard && o.Progress < BoardProfile.Sprint.Journey)
                        .OrderByDescending(o => o.Progress)
                        .FirstOrDefault();

                    if (next != null) log.AddRange(engine.Execute(new MoveCommand(next.Id)));
                }

                log.AddRange(engine.Execute(new EndTurnCommand()));
            }

            Assert.That(engine.MatchOver, Is.True, "the match should reach a winner");
            Assert.That(log.Any(e => e is GameWon), Is.True);
            Assert.That(log.Any(e => e is OperatorReachedHome), Is.True);
            Assert.That(log.Any(e => e is OperatorDeployed), Is.True);
            Assert.That(log.Any(e => e is OperatorMoved), Is.True);
        }

        // ── Passives, end to end ─────────────────────────────────────────

        [Test]
        public void KurbynMovesAtHisPassiveSpeed_NotHisBaseSpeed()
        {
            // Drives the whole chain: MatchFactory grants the passive with its
            // magnitude, StatusRegistry reports it, MovementResolver applies it.
            //
            // The test this replaces added two constants together and asserted
            // they summed to 1.5. It stayed green while the engine moved Kurbyn
            // at 1.0, because it never touched the engine.
            var solo = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red }, seed: 11, openingDeployments: 3);

            solo.Engine.Start();

            var kurbyn = solo.Operators.First(o => o.Name == "Kurbyn");
            int total = First<DiceRolled>(solo.Engine.Execute(new RollDiceCommand())).Roll.Total;

            var moved = First<OperatorMoved>(solo.Engine.Execute(new MoveCommand(kurbyn.Id)));

            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.To - moved.From,
                Is.EqualTo((int)(total * (AlphaRoster.KurbynBaseSpeed + AlphaRoster.KurbynPassiveSpeedBonus))));
        }

        [Test]
        public void TheTank_MovesAtItsBaseSpeed_WithNoPassiveToAdd()
        {
            // The control. If this and the test above ever agree, the passive
            // has stopped reaching the engine again.
            var solo = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red }, seed: 11, openingDeployments: 3);

            solo.Engine.Start();

            var bouncer = solo.Operators.First(o => o.Name == "Bouncer");
            int total = First<DiceRolled>(solo.Engine.Execute(new RollDiceCommand())).Roll.Total;

            var moved = First<OperatorMoved>(solo.Engine.Execute(new MoveCommand(bouncer.Id)));

            Assert.That(moved.To - moved.From, Is.EqualTo((int)(total * AlphaRoster.BouncerSpeed)));
        }

    }
}