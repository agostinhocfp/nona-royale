// Assets/Tests/EditMode/Bots/LifestealBotTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Model;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Bots
{
    /// <summary>
    /// Vendetta's lifesteal (2026-09-17) is worth something to a wounded Luka
    /// and nothing to a healthy one.
    /// </summary>
    [TestFixture]
    public class LifestealBotTests
    {
        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };

        [Test]
        public void Vendetta_IsWorthMoreToAWoundedLuka()
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                { PlayerColor.Red, new[] { Luka.Definition, Javi.Definition, Bouncer.Definition } },
                { PlayerColor.Blue, new[] { Syla.Definition, Kian.Definition, Mimi.Definition } }
            };
            var match = MatchFactory.Create(Two, 19, squads);
            match.Engine.Start();

            OperatorState luka = null, syla = null;
            foreach (var op in match.Operators)
            {
                if (op.Name == "Luka") luka = op;
                if (op.Name == "Syla") syla = op;
            }

            int circuit = match.Map.Profile.CircuitLength;
            luka.MoveTo((10 - match.Map.StartTrackIndex(PlayerColor.Red) + circuit) % circuit);
            syla.MoveTo((12 - match.Map.StartTrackIndex(PlayerColor.Blue) + circuit) % circuit);

            var weights = BotWeights.For(BotPersonality.Brawler);
            var seat = match.Players[0];

            double healthy = CastPlanner.Score(new BotBoard(match), weights, seat, luka, Luka.Vendetta, syla, null, null).Defence;

            luka.SetHealth(2);
            double wounded = CastPlanner.Score(new BotBoard(match), weights, seat, luka, Luka.Vendetta, syla, null, null).Defence;

            Assert.That(healthy, Is.EqualTo(0.0));
            Assert.That(wounded, Is.GreaterThan(0.0));
        }
    }
}