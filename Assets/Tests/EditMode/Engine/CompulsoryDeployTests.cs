// Assets/Tests/EditMode/Engine/CompulsoryDeployTests.cs
using System;
using System.Linq;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Engine
{
    /// <summary>
    /// Spawn or move (COMBAT_SYSTEMS §6.1, §6.2, 2026-09-25): a held 6 with a
    /// seated operator must be used, and a doubles re-roll is taken when
    /// nothing on the board can move. Also the auto end-turn query built on
    /// the same answers (<see cref="TurnOptions"/>).
    /// </summary>
    /// <remarks>
    /// Seeds are searched for the opening roll a test needs, so a change to the
    /// dice stream moves the seed, not the assertion.
    /// </remarks>
    [TestFixture]
    public class CompulsoryDeployTests
    {
        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };

        private static bool Refused(System.Collections.Generic.IReadOnlyList<IGameEvent> events) =>
            events.OfType<CommandRejected>().Any();

        /// <summary>A started, rolled match whose first roll satisfies <paramref name="wanted"/>.</summary>
        private static MatchFactory.Match Opened(Func<DiceRoll, bool> wanted, int opening = 0)
        {
            for (int seed = 0; seed < 2000; seed++)
            {
                var match = MatchFactory.CreateAlphaMatch(Two, seed, openingDeployments: opening);
                match.Engine.Start();

                var rolled = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First();
                if (wanted(rolled.Roll)) return match;
            }

            throw new InvalidOperationException("no seed under 2000 opened on the roll this test needs");
        }

        private static OperatorState Seated(MatchFactory.Match match) =>
            match.Engine.CurrentPlayer.Operators.First(o => o.IsInYard);

        // ── A held 6 ─────────────────────────────────────────────────────

        [Test]
        public void ASixWithEveryOperatorSeated_CannotBeThrownAway()
        {
            var match = Opened(r => r.Contains(6) && !r.IsDouble);
            var engine = match.Engine;

            Assert.That(engine.MustDeploy, Is.True);
            Assert.That(engine.MustSpendRoll, Is.True, "the end-turn button greys out");

            var refused = engine.Execute(new EndTurnCommand()).OfType<CommandRejected>().SingleOrDefault();
            Assert.That(refused, Is.Not.Null);
            StringAssert.Contains("deploy", refused.Reason);
        }

        [Test]
        public void DeployingTheSix_ClearsTheDebt()
        {
            var match = Opened(r => r.Contains(6) && !r.IsDouble);
            var engine = match.Engine;

            Assert.That(Refused(engine.Execute(new DeployCommand(Seated(match).Id))), Is.False);
            Assert.That(engine.MustDeploy, Is.False);
        }

        [Test]
        public void DoubleSix_OwesTwoDeploys()
        {
            var match = Opened(r => r.CountOf(6) == 2);
            var engine = match.Engine;

            engine.Execute(new DeployCommand(Seated(match).Id));
            Assert.That(engine.MustDeploy, Is.True, "a second 6 and two more seated");

            engine.Execute(new DeployCommand(Seated(match).Id));
            Assert.That(engine.MustDeploy, Is.False);
        }

        [Test]
        public void ReRollingOnAHeldSix_IsRefused()
        {
            // Rolling clears the unspent dice, so a re-roll would forfeit the deploy.
            var match = Opened(r => r.CountOf(6) == 2);

            var refused = match.Engine.Execute(new RollDiceCommand()).OfType<CommandRejected>().SingleOrDefault();

            Assert.That(refused, Is.Not.Null);
            StringAssert.Contains("deploy", refused.Reason);
        }

        [Test]
        public void MovingWithTheSix_SpendsIt()
        {
            // "Spawn or move": with a runner out, the pooled move uses the 6 too.
            var match = Opened(r => r.Contains(6) && !r.IsDouble, opening: 2);
            var engine = match.Engine;
            var runner = engine.CurrentPlayer.Operators.First(o => !o.IsInYard);

            Assert.That(Refused(engine.Execute(new MoveCommand(runner.Id))), Is.False);
            Assert.That(engine.MustDeploy, Is.False);
            Assert.That(Refused(engine.Execute(new EndTurnCommand())), Is.False);
        }

        [Test]
        public void NoSix_OwesNoDeploy()
        {
            var match = Opened(r => !r.Contains(6) && !r.IsDouble);

            Assert.That(match.Engine.MustDeploy, Is.False);
            Assert.That(Refused(match.Engine.Execute(new EndTurnCommand())), Is.False);
        }

        // ── Doubles ──────────────────────────────────────────────────────

        [Test]
        public void DoublesWithNothingThatCanMove_MustBeReRolled()
        {
            var match = Opened(r => r.IsDouble && !r.Contains(6));
            var engine = match.Engine;

            Assert.That(engine.MustRollAgain, Is.True);
            Assert.That(engine.MustSpendRoll, Is.True);

            var refused = engine.Execute(new EndTurnCommand()).OfType<CommandRejected>().SingleOrDefault();
            Assert.That(refused, Is.Not.Null);
            StringAssert.Contains("roll again", refused.Reason);

            Assert.That(Refused(engine.Execute(new RollDiceCommand())), Is.False);
        }

        [Test]
        public void DoublesWithARunnerOut_ReRollStaysOptional()
        {
            var match = Opened(r => r.IsDouble && !r.Contains(6), opening: 2);
            var engine = match.Engine;
            var runner = engine.CurrentPlayer.Operators.First(o => !o.IsInYard);

            engine.Execute(new MoveCommand(runner.Id));

            Assert.That(engine.CanRollAgain, Is.True, "precondition: a re-roll is open");
            Assert.That(engine.MustRollAgain, Is.False);
            Assert.That(Refused(engine.Execute(new EndTurnCommand())), Is.False);
        }

        // ── Auto end-turn ────────────────────────────────────────────────

        [Test]
        public void OnlyEndTurnRemains_IsFalse_BeforeTheRoll()
        {
            var match = MatchFactory.CreateAlphaMatch(Two, 1);
            match.Engine.Start();

            Assert.That(TurnOptions.OnlyEndTurnRemains(match), Is.False);
        }

        [Test]
        public void OnlyEndTurnRemains_WithEverySeatedAndNothingOwed()
        {
            // Seated operators cast nothing (§4.3), whatever the pool holds.
            var match = Opened(r => !r.Contains(6) && !r.IsDouble);

            Assert.That(TurnOptions.OnlyEndTurnRemains(match), Is.True);
        }

        [Test]
        public void OnlyEndTurnRemains_IsFalse_WhileSomethingIsOwed()
        {
            Assert.That(TurnOptions.OnlyEndTurnRemains(Opened(r => r.Contains(6) && !r.IsDouble)), Is.False, "a 6 to deploy");
            Assert.That(TurnOptions.OnlyEndTurnRemains(Opened(r => r.IsDouble && !r.Contains(6))), Is.False, "doubles to re-roll");
            Assert.That(TurnOptions.OnlyEndTurnRemains(Opened(r => true, opening: 2)), Is.False, "a move");
        }

        [Test]
        public void OnlyEndTurnRemains_IsFalse_WhileAnOptionalReRollIsOpen()
        {
            var match = Opened(r => r.IsDouble && !r.Contains(6), opening: 2);
            var engine = match.Engine;
            engine.Execute(new MoveCommand(engine.CurrentPlayer.Operators.First(o => !o.IsInYard).Id));

            Assert.That(TurnOptions.OnlyEndTurnRemains(match), Is.False);
        }

        [Test]
        public void OnlyEndTurnRemains_AgreesWithTheCastTray_AndEndTurnIsAccepted()
        {
            // Over many opening turns with runners out: whenever the query says
            // only ending remains, nothing is castable and the engine agrees
            // the turn can end.
            int checkedTurns = 0;

            for (int seed = 0; seed < 300; seed++)
            {
                var match = MatchFactory.CreateAlphaMatch(Two, seed, openingDeployments: 3);
                var engine = match.Engine;
                engine.Start();

                for (int turn = 0; turn < 6 && !engine.MatchOver; turn++)
                {
                    engine.Execute(new RollDiceCommand());
                    TurnKit.PayWhatIsOwed(engine);

                    bool only = TurnOptions.OnlyEndTurnRemains(match);
                    bool castable = engine.CurrentPlayer.Operators.Any(op =>
                        match.AbilitiesByOperator[op.Id].Any(a => TurnOptions.IsCastable(engine, op, a)));

                    if (only)
                    {
                        checkedTurns++;
                        Assert.That(castable, Is.False);
                        Assert.That(engine.CanRollAgain, Is.False);
                    }
                    else if (!engine.CanRollAgain)
                    {
                        Assert.That(castable, Is.True, $"seed {seed}: nothing owed, no re-roll, so a cast must be why");
                    }

                    Assert.That(Refused(engine.Execute(new EndTurnCommand())), Is.False, $"seed {seed}, turn {turn}");
                }
            }

            Assert.That(checkedTurns, Is.GreaterThan(0), "the query never fired");
        }
    }
}
