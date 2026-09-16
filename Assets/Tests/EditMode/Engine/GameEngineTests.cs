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

        /// <summary>
        /// Spends whatever is in hand and closes the turn.
        /// </summary>
        /// <remarks>
        /// <b>Movement is compulsory (§6), so a bare <c>EndTurnCommand</c> is
        /// refused while any operator could still move.</b> Every helper here
        /// that advances the turn has to move first — a test that simply ended
        /// the turn would loop or stall rather than fail, which is the worst way
        /// for a fixture to be wrong.
        ///
        /// The pooled form is used deliberately: it clears every unspent die in
        /// one command, so nothing is left that could refuse the handover.
        /// </remarks>
        private static void SpendRollAndEndTurn(GameEngine engine)
        {
            var mover = engine.CurrentPlayer.Operators.FirstOrDefault(o => !o.IsInYard);
            if (mover != null) engine.Execute(new MoveCommand(mover.Id));

            engine.Execute(new EndTurnCommand());
        }

        /// <summary>Rolls until the dice hand us what a test needs, ending turns in between.</summary>
        /// <remarks>
        /// <b>It does not care whose turn it stops on.</b> Every caller below
        /// happens to land on Red at the seeds used; a caller that needs a
        /// particular seat has to check for itself.
        /// </remarks>
        private IReadOnlyList<IGameEvent> RollUntil(Func<DiceRoll, bool> wanted, int maxTurns = 200)
        {
            for (int i = 0; i < maxTurns; i++)
            {
                var events = _engine.Execute(new RollDiceCommand());
                var rolled = First<DiceRolled>(events);

                if (rolled != null && wanted(rolled.Roll)) return events;

                SpendRollAndEndTurn(_engine);
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

            // Doubles buy a fresh roll, not an escape from the one in hand: the
            // engine refuses a re-roll while the dice could still be spent (§6).
            // Every operator is in the yard here, so nothing can spend them and
            // the re-roll is allowed.
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
        public void CanDeploy_IsTrue_ForAYardOperator_WhenASixIsUnspent()
        {
            RollUntil(r => r.Contains(6) && !r.IsDouble);
            var syla = Op(_engine.CurrentPlayer.Color, "Syla");
            Assert.That(syla.IsInYard, Is.True, "precondition");

            Assert.That(_engine.CanDeploy(syla), Is.True);
            Assert.That(Has<OperatorDeployed>(_engine.Execute(new DeployCommand(syla.Id))), Is.True,
                "the query and the command agree");
            Assert.That(_engine.CanDeploy(syla), Is.False, "already on the board");
        }

        [Test]
        public void CanDeploy_IsFalse_BeforeRolling_ForAnotherSeat_OrWithoutASix()
        {
            Assert.That(_engine.CanDeploy(Op(PlayerColor.Red, "Syla")), Is.False, "nothing rolled yet");

            RollUntil(r => r.Contains(6) && !r.IsDouble);
            var other = _engine.CurrentPlayer.Color == PlayerColor.Red ? PlayerColor.Blue : PlayerColor.Red;
            Assert.That(_engine.CanDeploy(Op(other, "Syla")), Is.False, "not this seat's operator");

            SpendRollAndEndTurn(_engine);
            RollUntil(r => !r.Contains(6));

            var waiting = Op(_engine.CurrentPlayer.Color, "Syla");
            Assert.That(waiting.IsInYard, Is.True, "precondition");

            Assert.That(_engine.CanDeploy(waiting), Is.False, "no unspent 6");
            Assert.That(Has<CommandRejected>(_engine.Execute(new DeployCommand(waiting.Id))), Is.True,
                "the query and the command agree");
        }

        [Test]
        public void EnergyCap_ReportsTheConfiguredCap()
        {
            Assert.That(_engine.EnergyCap, Is.EqualTo(Core.Config.EnergyConfig.Default.EnergyCap));
        }

        [Test]
        public void Round_AdvancesWhenTheFirstSeatBeginsAgain()
        {
            Assert.That(_engine.Round, Is.EqualTo(1));

            _engine.Execute(new RollDiceCommand());
            SpendRollAndEndTurn(_engine);
            Assert.That(_engine.CurrentPlayer.Color, Is.EqualTo(PlayerColor.Blue), "precondition");
            Assert.That(_engine.Round, Is.EqualTo(1), "the second seat is still round 1");

            _engine.Execute(new RollDiceCommand());
            SpendRollAndEndTurn(_engine);
            Assert.That(_engine.CurrentPlayer.Color, Is.EqualTo(PlayerColor.Red), "precondition");
            Assert.That(_engine.Round, Is.EqualTo(2));
        }

        [Test]
        public void Winner_IsNullUntilTheMatchIsWon()
        {
            Assert.That(_engine.Winner, Is.Null);

            foreach (var op in _match.Players[0].Operators)
                op.MoveTo(BoardProfile.Standard.Journey);

            _engine.Execute(new RollDiceCommand());
            _engine.Execute(new EndTurnCommand());

            Assert.That(_engine.Winner, Is.EqualTo(PlayerColor.Red));
        }

        [Test]
        public void IsHome_IsTrue_OnlyForAnOperatorThatFinished()
        {
            var bouncer = Op(PlayerColor.Red, "Bouncer");
            Assert.That(_engine.IsHome(bouncer), Is.False);

            bouncer.MoveTo(BoardProfile.Standard.Journey);
            Assert.That(_engine.IsHome(bouncer), Is.True);
        }

        [Test]
        public void IsFinalStretch_IsFalse_AtTheStart()
        {
            Assert.That(_engine.IsFinalStretch, Is.False);
        }

        [Test]
        public void IsFinalStretch_IsFalse_WithOnlyOneOperatorHome()
        {
            Op(PlayerColor.Red, "Bouncer").MoveTo(BoardProfile.Standard.Journey);

            Assert.That(_engine.IsFinalStretch, Is.False);
        }

        [Test]
        public void IsFinalStretch_IsTrue_WhenASeatHasAllButOneHome()
        {
            // The alpha squad is three: two home leaves one to go.
            Op(PlayerColor.Blue, "Bouncer").MoveTo(BoardProfile.Standard.Journey);
            Op(PlayerColor.Blue, "Syla").MoveTo(BoardProfile.Standard.Journey);

            Assert.That(_engine.IsFinalStretch, Is.True);
        }

        [Test]
        public void IsFinalStretch_IsFalse_OnceTheMatchIsOver()
        {
            // Blue is one from home, but Red has already won.
            Op(PlayerColor.Blue, "Bouncer").MoveTo(BoardProfile.Standard.Journey);
            Op(PlayerColor.Blue, "Syla").MoveTo(BoardProfile.Standard.Journey);
            foreach (var op in _match.Players[0].Operators)
                op.MoveTo(BoardProfile.Standard.Journey);

            _engine.Execute(new RollDiceCommand());
            _engine.Execute(new EndTurnCommand());

            Assert.That(_engine.MatchOver, Is.True);
            Assert.That(_engine.IsFinalStretch, Is.False);
        }

        [Test]
        public void AnOperatorInTheYard_CannotMove()
        {
            _engine.Execute(new RollDiceCommand());

            var events = _engine.Execute(new MoveCommand(Op(PlayerColor.Red, "Syla").Id));

            Assert.That(First<CommandRejected>(events).Reason, Does.Contain("yard"));
        }

        [Test]
        public void APooledMove_SpendsEveryDieAtOnce()
        {
            // Replaces ExactlyOneOperatorMovesPerRoll, which asserted a rule §6
            // deleted. A second move is now refused because the dice are gone,
            // not because one operator has already had its go.
            RollUntil(r => r.Contains(6) && !r.IsDouble);
            var syla = Op(PlayerColor.Red, "Syla");
            _engine.Execute(new DeployCommand(syla.Id));
            _engine.Execute(new MoveCommand(syla.Id));

            Assert.That(_engine.UnspentDice, Is.Empty);

            var second = _engine.Execute(new MoveCommand(syla.Id));

            Assert.That(First<CommandRejected>(second).Reason, Does.Contain("no dice left"));
        }

        [Test]
        public void ARollCanBeSplitBetweenTwoOperators()
        {
            // The rule that replaced "exactly one operator moves per roll" (§6).
            // Movement is compulsory per die; what is optional is whether the two
            // dice go to one operator or two. Each die spent is its own landing,
            // which is what makes a split two chances to collide rather than one.
            var match = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red, PlayerColor.Blue }, seed: 5, openingDeployments: 2);

            var engine = match.Engine;
            engine.Start();

            var roll = First<DiceRolled>(engine.Execute(new RollDiceCommand())).Roll;
            var first = match.Players[0].Operators[0];
            var second = match.Players[0].Operators[1];

            int firstBefore = first.Progress;
            int secondBefore = second.Progress;

            var firstEvents = engine.Execute(new MoveCommand(first.Id, roll.First));

            Assert.That(Has<OperatorMoved>(firstEvents), Is.True);
            Assert.That(engine.UnspentDice.Count, Is.EqualTo(1), "one die spent, one still in hand");

            var secondEvents = engine.Execute(new MoveCommand(second.Id, roll.Second));

            Assert.That(Has<OperatorMoved>(secondEvents), Is.True);
            Assert.That(engine.UnspentDice, Is.Empty);

            Assert.That(first.Progress, Is.GreaterThan(firstBefore));
            Assert.That(second.Progress, Is.GreaterThan(secondBefore));

            Assert.That(Has<CommandRejected>(engine.Execute(new EndTurnCommand())), Is.False,
                "both dice are spent, so the turn can close");
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
        public void ABouncedMove_ReportsTheLandingItAttempted()
        {
            // PRESENTATION §3: the board has to show the contested cell before
            // the bounce, and the view may not work it out for itself.
            _engine.Execute(new RollDiceCommand());          // Red, whatever it rolls
            var mover = Op(PlayerColor.Red, "Bouncer");
            mover.MoveTo(0);                                 // Red's progress is its track cell

            int landing = PooledLanding(mover);
            int circuit = _match.Map.Profile.CircuitLength;
            int blueStart = _match.Map.StartTrackIndex(PlayerColor.Blue);

            // Syla, not Bouncer: Bouncer's aura slows enemies within 3, which
            // would shorten the very move this test measures.
            var occupant = Op(PlayerColor.Blue, "Syla");
            occupant.MoveTo(((landing - blueStart) % circuit + circuit) % circuit);

            Assert.That(occupant.Health, Is.GreaterThan(NonaRoyale.Core.Config.CombatConfig.Default.CollisionDamage),
                "precondition: the occupant survives the hit");
            Assert.That(_match.Map.IsSafe(CellRef.Track(landing)), Is.False,
                "precondition: a contestable cell");
            Assert.That(PooledLanding(mover), Is.EqualTo(landing),
                "precondition: the occupant does not change where the move lands");

            var moved = First<OperatorMoved>(_engine.Execute(new MoveCommand(mover.Id)));

            Assert.That(moved.Bounced, Is.True);
            Assert.That(moved.AttemptedTo, Is.EqualTo(landing));
            Assert.That(moved.To, Is.EqualTo(landing - 1), "one step back along its own path");
        }

        [Test]
        public void AnUncontestedMove_AttemptsExactlyWhereItLands()
        {
            RollUntil(r => r.Contains(6) && !r.IsDouble);
            var syla = Op(PlayerColor.Red, "Syla");
            _engine.Execute(new DeployCommand(syla.Id));

            var moved = First<OperatorMoved>(_engine.Execute(new MoveCommand(syla.Id)));

            Assert.That(moved.Bounced, Is.False);
            Assert.That(moved.AttemptedTo, Is.EqualTo(moved.To));
        }

        private int PooledLanding(OperatorState op) =>
            _engine.PreviewLandings().First(l => l.OperatorId == op.Id && l.IsPooled).Progress;

        // DeployingAfterMoving_IsRejected was deleted. It asserted that a deploy
        // must precede movement, a rule §6 removed deliberately — GameEngine.Deploy
        // now says in as many words that "deploy no longer has to precede
        // movement." The test still passed, because a pooled move had cleared the
        // dice and left no unspent 6 for the deploy to take. A test that is green
        // for a reason unrelated to its name is worse than a red one.

        // ── Abilities ────────────────────────────────────────────────────

        [Test]
        public void AnAbilityWithoutEnergy_IsRejected()
        {
            RollUntil(r => r.Contains(6) && !r.IsDouble);
            var syla = Op(PlayerColor.Red, "Syla");
            _engine.Execute(new DeployCommand(syla.Id));

            // Nothing in range and a thin pool: either way it comes back refused.
            var events = _engine.Execute(new UseAbilityCommand(
                syla.Id, Syla.TaggedFromAbove.Id, Op(PlayerColor.Blue, "Syla").Id));

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
        public void ATurnCannotBeEndedWhileADieCanStillBeSpent()
        {
            // Movement is compulsory (§6). The check is "does a legal consumer
            // exist", not "are the dice gone" — which is why the same predicate
            // greys out the end-turn button rather than letting a player press it
            // and read a refusal.
            var match = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red, PlayerColor.Blue }, seed: 5, openingDeployments: 3);

            var engine = match.Engine;
            engine.Start();
            engine.Execute(new RollDiceCommand());

            Assert.That(engine.MustSpendRoll, Is.True);
            Assert.That(Has<CommandRejected>(engine.Execute(new EndTurnCommand())), Is.True);

            SpendRollAndEndTurn(engine);

            Assert.That(engine.CurrentPlayer.Color, Is.EqualTo(PlayerColor.Blue));
        }

        [Test]
        public void ATurnCannotBeEndedWithoutRolling()
        {
            // Rolling is compulsory. Before this, HasLegalMove's phase check made
            // the turn endable at AwaitingRoll, so a player could pass — taking no
            // energy, exposing nobody to a landing, and giving the board nothing.
            Assert.That(_engine.Phase, Is.EqualTo(TurnPhase.AwaitingRoll));
            Assert.That(_engine.MustSpendRoll, Is.True, "the end-turn button greys out before the roll too");

            var events = _engine.Execute(new EndTurnCommand());

            Assert.That(First<CommandRejected>(events).Reason, Does.Contain("roll first"));
            Assert.That(_engine.CurrentPlayer.Color, Is.EqualTo(PlayerColor.Red), "the turn did not hand over");
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

        // ── Drafting ─────────────────────────────────────────────────────

        [Test]
        public void ADraftedSquad_NeverRepeatsAnOperator()
        {
            // GDD §2.2: a seat fields three distinct operators. Two players may
            // field the same one; one player may not field it twice. Nothing
            // tested this until the roster grew past three and the rule started
            // being able to fail.
            for (int seed = 0; seed < 40; seed++)
            {
                var drafted = MatchFactory.Create(
                    new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet },
                    seed);

                foreach (var player in drafted.Players)
                {
                    var names = player.Operators.Select(o => o.Name).ToList();

                    Assert.That(names.Count, Is.EqualTo(Roster.SquadSize));
                    Assert.That(names.Distinct().Count(), Is.EqualTo(names.Count),
                        $"seed {seed}, {player.Color} drafted {string.Join(", ", names)}");
                }
            }
        }

        [Test]
        public void TheSameSeed_DraftsTheSameSquads()
        {
            // Drafting draws from the match RNG, so reproducibility covers the
            // squads as well as the dice. The harness depends on this entirely,
            // and it is also why adding an operator shifts the dice stream and
            // invalidates figures measured before the change.
            var seats = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green };

            var first = MatchFactory.Create(seats, seed: 4242);
            var second = MatchFactory.Create(seats, seed: 4242);

            Assert.That(
                second.Operators.Select(o => $"{o.Owner}:{o.Name}"),
                Is.EqualTo(first.Operators.Select(o => $"{o.Owner}:{o.Name}").ToList()));
        }

        [Test]
        public void ANamedSquad_IsFieldedExactlyAsGiven()
        {
            // The path CreateAlphaMatch takes, and the one every measurement in
            // ADR-0002 used. If this ever drafts instead, every figure in
            // COMBAT_SYSTEMS §12 quietly stops being comparable.
            var alpha = MatchFactory.CreateAlphaMatch(new[] { PlayerColor.Red }, seed: 99);

            Assert.That(
                alpha.Operators.Select(o => o.Name),
                Is.EqualTo(new[] { "Bouncer", "Syla", "Kurbyn" }));
        }

        // ── The engine says what it did ──────────────────────────────────
        //
        // Three separate faults have had the same shape: the state changed
        // correctly and nothing announced it. The board kept working, so nothing
        // screamed — badges draw from ActiveStatusesOn, pieces reposition from
        // their progress, and only the explanation went missing. These tests
        // exist to make that class of failure loud.

        /// <summary>
        /// A match with everything deployed on its start cell at full health,
        /// Red banked and on its turn, having already rolled.
        /// </summary>
        /// <remarks>
        /// <b>The pool is filled by taking turns rather than set directly.</b>
        /// <c>PlayerState.SetEnergy</c> is not visible from the test assembly,
        /// and that is right — the ledger owns the pool (§3), and a test that
        /// reached past it would be asserting against a state no match can
        /// actually reach.
        ///
        /// <b>Those turns move pieces, which is why the board is reset
        /// afterwards.</b> Energy arrives only on the first roll of a turn (§3.1),
        /// and movement is compulsory (§6) — so a turn cannot be closed without
        /// spending the roll, and several turns of spending walks operators an
        /// arbitrary distance apart and can resolve collisions on the way. The
        /// old version of this helper claimed "nothing moves in them"; that
        /// stopped being true when movement became compulsory, and the claim is
        /// now made good rather than merely stated.
        /// </remarks>
        private static MatchFactory.Match Armed(int minimumEnergy = 12, int seed = 3)
        {
            var match = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red, PlayerColor.Blue }, seed, openingDeployments: 3);

            var engine = match.Engine;
            engine.Start();

            while (match.Players[0].Energy < minimumEnergy ||
                   engine.CurrentPlayer.Color != PlayerColor.Red)
            {
                engine.Execute(new RollDiceCommand());
                SpendRollAndEndTurn(engine);
            }

            engine.Execute(new RollDiceCommand());

            foreach (var op in match.Operators)
            {
                op.MoveTo(0);
                op.SetHealth(op.MaxHealth);
            }

            return match;
        }

        private static OperatorState Of(MatchFactory.Match match, PlayerColor seat, string name) =>
            match.Operators.First(o => o.Owner == seat && o.Name == name);

        [Test]
        public void AnAbilityThatAppliesAStatus_SaysSo()
        {
            var match = Armed();
            var syla = Of(match, PlayerColor.Red, "Syla");
            var enemy = Of(match, PlayerColor.Blue, "Bouncer");

            enemy.MoveTo(1);
            syla.MoveTo(11);                        // track 11, two cells from Blue's start at 13

            var events = match.Engine.Execute(
                new UseAbilityCommand(syla.Id, Syla.FromTheHip.Id, enemy.Id));

            Assert.That(events.Any(e => e is CommandRejected), Is.False,
                string.Join(" | ", events.Select(e => e.ToString())));

            var applied = events.OfType<StatusApplied>().FirstOrDefault();

            Assert.That(applied, Is.Not.Null, "From the Hip slows, and the slow has to be announced");
            Assert.That(applied.Status, Is.EqualTo(StatusKind.Slow));
            Assert.That(applied.Target, Is.SameAs(enemy));
        }

        [Test]
        public void AHealIsAnnounced()
        {
            // The roster's only heal, and the ally mode was unreachable in the
            // view until recently — so this path had never been exercised end to
            // end by anything.
            var match = Armed();
            var bouncer = Of(match, PlayerColor.Red, "Bouncer");
            var syla = Of(match, PlayerColor.Red, "Syla");

            bouncer.MoveTo(10);
            syla.MoveTo(11);
            syla.SetHealth(2);

            var events = match.Engine.Execute(
                new UseAbilityCommand(bouncer.Id, Bouncer.AllInMauling.Id, syla.Id));

            var healed = events.OfType<HealApplied>().FirstOrDefault();

            Assert.That(healed, Is.Not.Null);
            Assert.That(healed.Target, Is.SameAs(syla));
            Assert.That(healed.Amount, Is.EqualTo(3));
        }

        [Test]
        public void APullIsAnnounced()
        {
            var match = Armed();
            var bouncer = Of(match, PlayerColor.Red, "Bouncer");
            var enemy = Of(match, PlayerColor.Blue, "Syla");

            enemy.MoveTo(1);
            bouncer.MoveTo(11);

            var events = match.Engine.Execute(
                new UseAbilityCommand(bouncer.Id, Bouncer.VelvetRope.Id, enemy.Id));

            Assert.That(events.OfType<OperatorMoved>().Any(m => ReferenceEquals(m.Operator, enemy)),
                Is.True, "the pulled operator moved, so the view has to be told");
        }

        [Test]
        public void ASwapIsAnnounced()
        {
            // Swapped had no case in EmitOutcome at all until 2026-09-13:
            // Translocation moved two pieces and the engine said nothing. Both
            // ends have to be reported, or the view draws a board that is wrong.
            var match = MatchFactory.Create(
                new[] { PlayerColor.Red, PlayerColor.Blue },
                seed: 3,
                squads: new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
                {
                    { PlayerColor.Red, new[] { Mimi.Definition, Syla.Definition, Bouncer.Definition } },
                    { PlayerColor.Blue, Roster.Alpha }
                },
                openingDeployments: 3);

            var engine = match.Engine;
            engine.Start();

            while (match.Players[0].Energy < Mimi.Translocation.EnergyCost ||
                   engine.CurrentPlayer.Color != PlayerColor.Red)
            {
                engine.Execute(new RollDiceCommand());
                SpendRollAndEndTurn(engine);
            }

            engine.Execute(new RollDiceCommand());
            foreach (var op in match.Operators) op.MoveTo(0);


            var mimi = Of(match, PlayerColor.Red, "Mimi");
            var enemy = Of(match, PlayerColor.Blue, "Syla");
            // Both past Blue's start (track 13), and neither on a safe cell —
            // safe cells are the four colour starts, at multiples of 13.
            //
            // The positions are not arbitrary. A swap exchanges cells and
            // converts that into a signed shift on each operator's own progress,
            // so the target needs progress at least equal to the shift or it
            // lands behind its own start and is refused (§7.4). Mimi on track 16
            // and the enemy on track 18 is a shift of 2 against a progress of 5.
            mimi.MoveTo(16);                        // track 16
            enemy.MoveTo(5);                        // track 18

            var events = engine.Execute(
                new UseAbilityCommand(mimi.Id, Mimi.Translocation.Id, enemy.Id));

            var moves = events.OfType<OperatorMoved>().ToList();

            Assert.That(moves.Any(m => ReferenceEquals(m.Operator, mimi)), Is.True);
            Assert.That(moves.Any(m => ReferenceEquals(m.Operator, enemy)), Is.True,
                "two pieces moved, so two have to be announced");
        }



        [Test]
        public void AnUpkeepMarkPayout_IsAnnouncedNotJustApplied()
        {
            // The fault this is named for: TurnStateMachine applies an upkeep
            // neutralize itself, so GameEngine reports rather than resolves —
            // and the hastened allies were applied to state and never emitted.
            // The badges appeared on a later refresh with nothing explaining them.
            var match = Armed();
            var syla = Of(match, PlayerColor.Red, "Syla");
            var victim = Of(match, PlayerColor.Blue, "Syla");

            victim.MoveTo(1);
            syla.MoveTo(11);
            victim.SetHealth(2);                    // one mark tick finishes it

            match.Engine.Execute(new UseAbilityCommand(syla.Id, Syla.TaggedFromAbove.Id, victim.Id));

            // Handing over runs Blue's upkeep, where the mark bills. The roll has
            // to be spent first — movement is compulsory (§6).
            var mover = Of(match, PlayerColor.Red, "Bouncer");
            match.Engine.Execute(new MoveCommand(mover.Id));
            var events = match.Engine.Execute(new EndTurnCommand());

            var down = events.OfType<OperatorNeutralized>().FirstOrDefault();
            Assert.That(down, Is.Not.Null, "the mark should have finished it");
            Assert.That(down.Cause, Is.EqualTo("mark"));

            var hastened = events.OfType<StatusApplied>()
                .Where(s => s.Status == StatusKind.Hastened)
                .ToList();

            Assert.That(hastened.Count, Is.EqualTo(3), "the marker's whole squad, announced");
            Assert.That(hastened.All(s => s.Target.Owner == PlayerColor.Red), Is.True);
        }

        [Test]
        public void AKillAtUpkeep_PaysItsMarkerOnAnotherPlayersTurn()
        {
            // The only path where a bounty lands on a seat that is not taking
            // the turn. Red spends 9 on the ult and is left under the cap, so
            // the credit is visible rather than absorbed.
            var match = Armed();
            var syla = Of(match, PlayerColor.Red, "Syla");
            var victim = Of(match, PlayerColor.Blue, "Syla");

            victim.MoveTo(1);
            syla.MoveTo(11);
            victim.SetHealth(2);

            match.Engine.Execute(new UseAbilityCommand(syla.Id, Syla.TaggedFromAbove.Id, victim.Id));
            int redEnergy = match.Players[0].Energy;

            var mover = Of(match, PlayerColor.Red, "Bouncer");
            match.Engine.Execute(new MoveCommand(mover.Id));
            var events = match.Engine.Execute(new EndTurnCommand());

            var bounty = events.OfType<EnergyGranted>()
                .FirstOrDefault(g => g.Player == PlayerColor.Red);

            Assert.That(bounty, Is.Not.Null, "the marker collected, and it has to be reported");
            Assert.That(match.Players[0].Energy, Is.GreaterThan(redEnergy));
        }

        [Test]
        public void AnUpkeepKill_IsCreditedAndTallied()
        {
            // The stats the end screen shows (GUI increment I): the kill counts
            // for the marker's seat, the loss for the victim's, on a turn that
            // belongs to neither.
            var match = Armed();
            var syla = Of(match, PlayerColor.Red, "Syla");
            var victim = Of(match, PlayerColor.Blue, "Syla");

            victim.MoveTo(1);
            syla.MoveTo(11);
            victim.SetHealth(2);

            Assert.That(match.Engine.KnockoutsScoredBy(PlayerColor.Red), Is.EqualTo(0), "precondition");

            match.Engine.Execute(new UseAbilityCommand(syla.Id, Syla.TaggedFromAbove.Id, victim.Id));

            var mover = Of(match, PlayerColor.Red, "Bouncer");
            match.Engine.Execute(new MoveCommand(mover.Id));
            var events = match.Engine.Execute(new EndTurnCommand());

            var down = events.OfType<OperatorNeutralized>().Single();
            Assert.That(down.CreditedTo, Is.EqualTo(PlayerColor.Red));

            Assert.That(match.Engine.KnockoutsScoredBy(PlayerColor.Red), Is.EqualTo(1));
            Assert.That(match.Engine.KnockoutsScoredBy(PlayerColor.Blue), Is.EqualTo(0));
            Assert.That(match.Engine.OperatorsLostBy(PlayerColor.Blue), Is.EqualTo(1));
            Assert.That(match.Engine.OperatorsLostBy(PlayerColor.Red), Is.EqualTo(0));
        }

        [Test]
        public void EveryNeutralizeInAWholeMatch_IsTallied()
        {
            // The same greedy scripted match as the black-box test below. The
            // losses counted must equal the neutralize events announced, and the
            // knockouts counted must equal the credited ones.
            var match = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet },
                seed: 20260911, board: BoardProfile.Sprint);

            var engine = match.Engine;
            var log = new List<IGameEvent>(engine.Start());

            for (int turn = 0; turn < 4000 && !engine.MatchOver; turn++)
            {
                var rolled = engine.Execute(new RollDiceCommand());
                log.AddRange(rolled);

                var squad = engine.CurrentPlayer.Operators;

                var waiting = squad.FirstOrDefault(o => o.IsInYard);
                if (waiting != null) log.AddRange(engine.Execute(new DeployCommand(waiting.Id)));

                var leader = squad
                    .Where(o => !o.IsInYard && o.Progress < BoardProfile.Sprint.Journey)
                    .OrderByDescending(o => o.Progress)
                    .FirstOrDefault();

                if (leader != null) log.AddRange(engine.Execute(new MoveCommand(leader.Id)));

                log.AddRange(engine.Execute(new EndTurnCommand()));
            }

            var downs = log.OfType<OperatorNeutralized>().ToList();
            Assert.That(downs.Count, Is.GreaterThan(0), "the scripted match should produce collisions");

            int lost = 0, scored = 0;
            foreach (var player in match.Players)
            {
                lost += engine.OperatorsLostBy(player.Color);
                scored += engine.KnockoutsScoredBy(player.Color);

                Assert.That(engine.OperatorsLostBy(player.Color),
                    Is.EqualTo(downs.Count(d => d.Operator.Owner == player.Color)));
                Assert.That(engine.KnockoutsScoredBy(player.Color),
                    Is.EqualTo(downs.Count(d => d.CreditedTo == player.Color)));
            }

            Assert.That(lost, Is.EqualTo(downs.Count));
            Assert.That(scored, Is.LessThanOrEqualTo(lost));
        }

        // ── A whole match, through the boundary only ─────────────────────

        [Test]
        public void AMatchPlaysToAWinner_UsingOnlyCommandsAndEvents()
        {
            // The first exercise of the core as a black box: no service is
            // touched, no state is set by hand. A greedy scripted player deploys
            // when it can and always advances its leader.
            var match = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet },
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
                Is.EqualTo((int)(total * (Kurbyn.BaseSpeed + Kurbyn.PassiveSpeedBonus))));
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

            Assert.That(moved.To - moved.From, Is.EqualTo((int)(total * Bouncer.Speed)));
        }


    }
}