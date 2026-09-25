// Assets/Tests/EditMode/Engine/PityDeployTests.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Engine
{
    /// <summary>
    /// Bad-luck deploy protection (§6): the Nth straight turn without a 6,
    /// with an operator seated, deploys one free. N is 2 since 2026-09-25.
    /// </summary>
    /// <remarks>
    /// Played out through real matches rather than staged, and checked against
    /// a drought counted here from the events, so the test states the rule and
    /// not the engine's bookkeeping.
    /// </remarks>
    [TestFixture]
    public class PityDeployTests
    {
        [Test]
        public void TheDefault_IsTwoTurns()
        {
            Assert.That(GameConfig.Default.PityDeployAfterTurns, Is.EqualTo(2));
        }

        [Test]
        public void PityDeploy_FiresOnTheSecondStraightTurnWithoutASix_AndNeverEarlier()
        {
            int fired = 0;

            for (int seed = 0; seed < 60; seed++)
            {
                var match = MatchFactory.CreateAlphaMatch(
                    new[] { PlayerColor.Red, PlayerColor.Blue }, seed, openingDeployments: 2);
                var engine = match.Engine;
                engine.Start();

                var drought = new Dictionary<PlayerColor, int> { [PlayerColor.Red] = 0, [PlayerColor.Blue] = 0 };

                for (int turn = 0; turn < 40 && !engine.MatchOver; turn++)
                {
                    var seat = engine.CurrentPlayer;
                    bool sawSix = false;

                    var rolled = engine.Execute(new RollDiceCommand());
                    sawSix |= rolled.OfType<DiceRolled>().Any(d => d.Roll.Contains(6));

                    // Pay what is owed, watching every re-roll for a 6 too.
                    for (int guard = 0; guard < 20 && engine.MustSpendRoll; guard++)
                    {
                        if (engine.MustDeploy)
                            engine.Execute(new DeployCommand(seat.Operators.First(o => o.IsInYard).Id));
                        else if (engine.CanMove)
                        {
                            bool moved = false;
                            foreach (var op in seat.Operators.Where(o => !o.IsInYard && !engine.IsHome(o)))
                            {
                                if (engine.Execute(new MoveCommand(op.Id)).OfType<CommandRejected>().Any()) continue;
                                moved = true;
                                break;
                            }
                            if (!moved) break;
                        }
                        else if (engine.MustRollAgain)
                        {
                            var again = engine.Execute(new RollDiceCommand());
                            sawSix |= again.OfType<DiceRolled>().Any(d => d.Roll.Contains(6));
                        }
                        else break;
                    }

                    bool seated = seat.Operators.Any(o => o.IsInYard);
                    var ended = engine.Execute(new EndTurnCommand());
                    var pity = ended.OfType<OperatorPityDeployed>().SingleOrDefault();

                    drought[seat.Color] = !sawSix && seated ? drought[seat.Color] + 1 : 0;

                    if (drought[seat.Color] >= 2)
                    {
                        Assert.That(pity, Is.Not.Null, $"seed {seed}, turn {turn}: second dry turn with a seat waiting");
                        Assert.That(pity.Operator.Owner, Is.EqualTo(seat.Color));
                        drought[seat.Color] = 0;
                        fired++;
                    }
                    else
                    {
                        Assert.That(pity, Is.Null, $"seed {seed}, turn {turn}: fired after {drought[seat.Color]} dry turn(s)");
                    }
                }
            }

            Assert.That(fired, Is.GreaterThan(0), "the mechanic never fired, so nothing was tested");
        }
    }
}
