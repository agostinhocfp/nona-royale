// Assets/Tests/EditMode/Engine/HomeBountyTests.cs
using System;
using System.Collections.Generic;
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
    /// An operator reaching HOME pays its seat the home bounty (CORE_GAMEPLAY
    /// CG17, COMBAT_SYSTEMS §8): 3 energy, filling to the cap, reported on the
    /// arrival and as the pool's grant; and another roll (CG17b), owed like a
    /// doubles roll inside the turn's budget.
    /// </summary>
    public class HomeBountyTests
    {
        private static MatchFactory.Match Rolled(Func<DiceRoll, bool> wanted, out DiceRoll roll,
            CombatConfig combat = null, EnergyConfig energy = null, GameConfig game = null)
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                [PlayerColor.Red] = new[] { Bouncer.Definition, Mimi.Definition, Javi.Definition }
            };

            for (int seed = 1; seed < 3000; seed++)
            {
                var match = MatchFactory.Create(new[] { PlayerColor.Red }, seed, squads,
                    gameConfig: game, combatConfig: combat, energyConfig: energy, openingDeployments: 3);
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

        /// <summary>Puts Bouncer one cell short of HOME and walks him in.</summary>
        private static IReadOnlyList<IGameEvent> WalkHome(MatchFactory.Match match, out OperatorState bouncer, out int before)
        {
            bouncer = match.Operators.First(o => o.Name == "Bouncer");
            bouncer.MoveTo(match.Map.Profile.Journey - 1);

            before = match.Engine.CurrentPlayer.Energy;
            return match.Engine.Execute(new MoveCommand(bouncer.Id));
        }

        [Test]
        public void TheDesignersNumber()
        {
            Assert.That(CombatConfig.Default.HomeEnergyBounty, Is.EqualTo(3),
                "2026-10-03: the kill bounty's figure. Changing it should also update §8 and CG17.");
        }

        [Test]
        public void ReachingHome_PaysTheSeatThree()
        {
            var match = Rolled(r => !r.IsDouble && r.Total <= 9, out _);
            var events = WalkHome(match, out var bouncer, out int before);

            var home = events.OfType<OperatorReachedHome>().Single();
            Assert.That(home.Operator, Is.SameAs(bouncer));
            Assert.That(home.Bounty, Is.EqualTo(3));
            Assert.That(match.Engine.CurrentPlayer.Energy, Is.EqualTo(before + 3));

            // The pool's grant follows the arrival, so the log reads in order.
            var list = events.ToList();
            int arrival = list.IndexOf(home);
            var grant = list.Skip(arrival + 1).OfType<EnergyGranted>().Single();
            Assert.That(grant.Player, Is.EqualTo(PlayerColor.Red));
            Assert.That(grant.Stored, Is.EqualTo(3));
            Assert.That(grant.Burned, Is.EqualTo(0), "energy from outside the drip never burns");
            Assert.That(grant.Total, Is.EqualTo(before + 3));
        }

        [Test]
        public void AtTheCap_TheRemainderIsNeverEarned()
        {
            // Cap 5 and a roll that grants 4 (total 8 or 9): the arrival can only store 1.
            var match = Rolled(r => !r.IsDouble && (r.Total == 8 || r.Total == 9), out _,
                energy: new EnergyConfig(energyCap: 5));
            var events = WalkHome(match, out _, out int before);

            Assert.That(before, Is.EqualTo(4), "fixture: the roll fills the pool to 4");
            Assert.That(events.OfType<OperatorReachedHome>().Single().Bounty, Is.EqualTo(1));
            Assert.That(match.Engine.CurrentPlayer.Energy, Is.EqualTo(5));

            var grant = events.OfType<EnergyGranted>().Single();
            Assert.That(grant.Stored, Is.EqualTo(1));
            Assert.That(grant.Burned, Is.EqualTo(0));
        }

        [Test]
        public void AFullPool_CollectsNothing_AndReportsNoGrant()
        {
            // Cap 4 and a roll that grants 4 or more: full before the arrival.
            var match = Rolled(r => !r.IsDouble && r.Total >= 8, out _, energy: new EnergyConfig(energyCap: 4));
            var events = WalkHome(match, out _, out int before);

            Assert.That(before, Is.EqualTo(4), "fixture: the pool is full");
            Assert.That(events.OfType<OperatorReachedHome>().Single().Bounty, Is.EqualTo(0));
            Assert.That(events.OfType<EnergyGranted>(), Is.Empty);
            Assert.That(match.Engine.CurrentPlayer.Energy, Is.EqualTo(4));
        }

        [Test]
        public void AZeroBounty_TurnsItOff()
        {
            var match = Rolled(r => !r.IsDouble && r.Total <= 9, out _, combat: new CombatConfig(homeEnergyBounty: 0));
            var events = WalkHome(match, out _, out int before);

            Assert.That(events.OfType<OperatorReachedHome>().Single().Bounty, Is.EqualTo(0));
            Assert.That(events.OfType<EnergyGranted>(), Is.Empty);
            Assert.That(match.Engine.CurrentPlayer.Energy, Is.EqualTo(before));
        }

        [Test]
        public void ANegativeBounty_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatConfig(homeEnergyBounty: -1));
        }

        [Test]
        public void TheEngine_ReportsTheFigure_ForTheBotsAndTheRulesText()
        {
            var match = Rolled(r => true, out _);
            Assert.That(match.Engine.HomeEnergyBounty, Is.EqualTo(CombatConfig.Default.HomeEnergyBounty));
        }

        // ── The extra roll (CG17b) ───────────────────────────────────────

        [Test]
        public void TheDesignersRoll()
        {
            Assert.That(GameConfig.Default.HomeExtraRolls, Is.EqualTo(1),
                "2026-10-03: one roll on top of the energy. Changing it should also update §8 and CG17b.");
        }

        [Test]
        public void ReachingHome_OwesAnotherRoll_WithNoEnergyOnIt()
        {
            var match = Rolled(r => !r.IsDouble, out _);
            var events = WalkHome(match, out _, out _);

            Assert.That(events.OfType<OperatorReachedHome>().Single().GrantsAnotherRoll, Is.True);
            Assert.That(match.Engine.CanRollAgain, Is.True);
            Assert.That(match.Engine.RollAgainFromDoubles, Is.False, "the coach's doubles tip stays quiet");

            int pool = match.Engine.CurrentPlayer.Energy;
            var rolled = match.Engine.Execute(new RollDiceCommand());

            Assert.That(rolled.OfType<CommandRejected>(), Is.Empty);
            Assert.That(rolled.OfType<DiceRolled>().Count(), Is.EqualTo(1));
            Assert.That(rolled.OfType<EnergyGranted>(), Is.Empty, "only the turn's first roll adds energy (§3.1)");
            Assert.That(match.Engine.CurrentPlayer.Energy, Is.EqualTo(pool));
        }

        [Test]
        public void AfterADouble_TheHomeRoll_SurvivesAPlainReRoll()
        {
            // A double owes one roll and the arrival another; the budget (3) has two left.
            for (int seed = 1; seed < 6000; seed++)
            {
                var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
                {
                    [PlayerColor.Red] = new[] { Bouncer.Definition, Mimi.Definition, Javi.Definition }
                };
                var match = MatchFactory.Create(new[] { PlayerColor.Red }, seed, squads, openingDeployments: 3);
                match.Engine.Start();

                var first = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().FirstOrDefault();
                if (first == null || !first.Roll.IsDouble) continue;

                WalkHome(match, out _, out _);
                Assert.That(match.Engine.CanRollAgain, Is.True);

                var second = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().FirstOrDefault();
                Assert.That(second, Is.Not.Null, "the doubles roll is taken");
                if (second.Roll.IsDouble) continue;

                // The doubles roll is spent; the home roll is still owed once the dice are used.
                TurnKit.PayWhatIsOwed(match.Engine);
                Assert.That(match.Engine.CanRollAgain, Is.True, "the home roll outlives a plain re-roll");
                Assert.That(match.Engine.RollAgainFromDoubles, Is.False);
                return;
            }

            Assert.Fail("No seed produced a double followed by a plain roll.");
        }

        [Test]
        public void AtTheRollBudget_NoRollIsOwed()
        {
            var match = Rolled(r => !r.IsDouble, out _, game: new GameConfig(maxRollsPerTurn: 1));
            var events = WalkHome(match, out _, out _);

            Assert.That(events.OfType<OperatorReachedHome>().Single().GrantsAnotherRoll, Is.False);
            Assert.That(match.Engine.CanRollAgain, Is.False);
            Assert.That(events.OfType<OperatorReachedHome>().Single().Bounty, Is.EqualTo(3), "the energy still pays");
        }

        [Test]
        public void TheLastOperatorHome_OwesNoRoll()
        {
            // A seat with nobody left to move would be forced to roll for nothing.
            var match = Rolled(r => !r.IsDouble, out _);
            foreach (var other in match.Operators.Where(o => o.Owner == PlayerColor.Red && o.Name != "Bouncer"))
                other.MoveTo(match.Map.Profile.Journey);

            var events = WalkHome(match, out _, out _);

            Assert.That(events.OfType<OperatorReachedHome>().Single().GrantsAnotherRoll, Is.False);
            Assert.That(match.Engine.CanRollAgain, Is.False);
        }

        [Test]
        public void ZeroRolls_TurnsItOff()
        {
            var match = Rolled(r => !r.IsDouble, out _, game: new GameConfig(homeExtraRolls: 0));
            var events = WalkHome(match, out _, out _);

            Assert.That(events.OfType<OperatorReachedHome>().Single().GrantsAnotherRoll, Is.False);
            Assert.That(match.Engine.CanRollAgain, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameConfig(homeExtraRolls: -1));
        }
    }
}
