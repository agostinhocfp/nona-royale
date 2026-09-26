// Assets/Tests/EditMode/Text/EventTextTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Text;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Text
{
    /// <summary>
    /// The player's words for engine events (<see cref="EventText"/>,
    /// LAUNCH_UI_PASS.md G7b): every event type has them, none of them leaks
    /// engine notation, and names carry their seat.
    /// </summary>
    [TestFixture]
    public class EventTextTests
    {
        private static readonly OperatorState Kian = new OperatorState(1, "Kian", PlayerColor.Red, 8, 1.0);
        private static readonly OperatorState Revu = new OperatorState(2, "Revú", PlayerColor.Blue, 8, 1.0);

        private static string Plain(IGameEvent e) => EventText.For(e)?.ToPlainText();

        [Test]
        public void EveryEventTypeInTheCore_HasWords()
        {
            var types = typeof(IGameEvent).Assembly.GetTypes()
                .Where(t => typeof(IGameEvent).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            Assert.That(types, Is.Not.Empty);
            foreach (var type in types)
                Assert.That(EventText.Writes(type), Is.True, $"{type.Name} has no player words in EventText");
        }

        /// <summary>
        /// Whole bot matches, so every event the engine actually emits, with
        /// real operators in it, goes through the writer.
        /// </summary>
        [Test]
        public void EveryEventFromRealMatches_IsWrittenWithoutEngineNotation()
        {
            var seen = new HashSet<Type>();

            for (int seed = 1; seed <= 8; seed++)
            {
                var seats = seed % 2 == 0
                    ? new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet }
                    : new[] { PlayerColor.Red, PlayerColor.Green };
                var match = MatchFactory.Create(seats, seed, openingDeployments: 2);
                var bots = new Dictionary<PlayerColor, IBot>();
                var random = BotConfig.Default.RandomFor(seed);
                for (int i = 0; i < seats.Length; i++)
                    bots[seats[i]] = new BotBrain((BotPersonality)(i % 3), random);

                var table = new BotTable(match, bots);
                table.Sent = (seat, command, events) =>
                {
                    foreach (var e in events)
                    {
                        seen.Add(e.GetType());
                        var line = EventText.For(e);
                        if (line == null) continue;

                        string text = line.ToPlainText();
                        Assert.That(line.HasUnwritten, Is.False, text);
                        Assert.That(text, Does.Not.Contain("->"), text);
                        Assert.That(text, Does.Not.Contain("Track["), text);
                        Assert.That(text, Does.Not.Contain("burned 0"), text);
                        Assert.That(text.Trim(), Is.Not.Empty, e.GetType().Name);
                    }
                };

                table.PlayToEnd();
            }

            Assert.That(seen, Does.Contain(typeof(OperatorMoved)));
            Assert.That(seen, Does.Contain(typeof(DamageDealt)));
        }

        [Test]
        public void NamesAreSeatRuns_SoTheViewCanColourThem()
        {
            var line = EventText.For(new CollisionResolved(Kian, Revu, moverBouncedBack: false));

            var named = line.Runs.Where(r => r.Kind == RunKind.Named).ToList();
            Assert.That(named.Select(r => r.Text), Is.EqualTo(new[] { "Kian", "Revú" }));
            Assert.That(named.Select(r => r.Seat), Is.EqualTo(new[] { PlayerColor.Red, PlayerColor.Blue }));
        }

        [Test]
        public void SeatsAreWrittenAsTheScreenWritesThem()
        {
            Assert.That(Plain(new EnergyGranted(PlayerColor.Red, 2, 0, 2)), Is.EqualTo("RED gains 2 energy"));
            Assert.That(Plain(new EnergyGranted(PlayerColor.Red, 1, 1, 2)), Is.EqualTo("RED gains 1 energy (1 lost at the cap)"));
            Assert.That(EventText.TurnHeading(PlayerColor.Violet).ToPlainText(), Is.EqualTo("VIOLET's turn"));
        }

        [Test]
        public void MovesAreCells_NotProgressNumbers()
        {
            var cell = CellRef.Track(9);

            Assert.That(Plain(new OperatorMoved(Kian, 8, 13, cell)), Is.EqualTo("Kian moves 5 cells"));
            Assert.That(Plain(new OperatorMoved(Kian, 8, 9, cell)), Is.EqualTo("Kian moves 1 cell"));
            Assert.That(Plain(new OperatorMoved(Kian, 50, 52, cell, attemptedTo: 54)),
                Is.EqualTo("Kian moves 2 cells, bounced back 2"));
            Assert.That(Plain(new OperatorMoved(Kian, 50, 50, cell, attemptedTo: 51)), Is.EqualTo("Kian is bounced back 1 cell"));
            Assert.That(Plain(new OperatorMoved(Kian, 20, 20, cell)), Is.EqualTo("Kian is repositioned"));
        }

        [Test]
        public void Rolls_ReadAsDice()
        {
            Assert.That(Plain(new DiceRolled(new DiceRoll(3, 5), false)), Is.EqualTo("rolled 3 and 5"));
            Assert.That(Plain(new DiceRolled(new DiceRoll(1, 1), true)), Is.EqualTo("rolled double 1s: doubles, roll again"));
        }

        [Test]
        public void Damage_SaysHowMuch_WhatKind_FromWhat_AndWhatIsLeft()
        {
            Assert.That(Plain(new DamageDealt(Revu, 3, 5, "ability", DamageType.Tech)), Is.EqualTo("Revú takes 3 Tech (5 left)"));
            Assert.That(Plain(new DamageDealt(Revu, 2, 6, "bleed")), Is.EqualTo("Revú takes 2 from Bleed (6 left)"));
            Assert.That(Plain(new DamageDealt(Revu, 1, 7, "collision")), Is.EqualTo("Revú takes 1 in a collision (7 left)"));
        }

        [Test]
        public void Knockouts_UseTheGuidesWord()
        {
            Assert.That(Plain(new OperatorNeutralized(Revu, "bleed")), Is.EqualTo("Revú is neutralized by Bleed"));
            Assert.That(Plain(new OperatorNeutralized(Revu, GameEngine.ExecuteCause)), Is.EqualTo("Revú is neutralized outright"));
            Assert.That(Plain(new OperatorNeutralized(Revu, "ability")), Is.EqualTo("Revú is neutralized"));
        }

        [Test]
        public void Statuses_AreKeywords_WithTheirDuration()
        {
            var line = EventText.For(new StatusApplied(Revu, StatusKind.Slow, 2));

            Assert.That(line.ToPlainText(), Is.EqualTo("Revú gains Slow for 2 turns"));
            Assert.That(line.Keywords, Does.Contain(Keywords.Status(StatusKind.Slow)));
            Assert.That(Plain(new StatusApplied(Revu, StatusKind.Bleed, 0)), Is.EqualTo("Revú gains Bleed"));
        }

        [Test]
        public void TurnMarkersAreHeadings_NotLines()
        {
            Assert.That(EventText.For(new TurnBegan(PlayerColor.Red, 3)), Is.Null);
            Assert.That(EventText.For(new TurnEnded(PlayerColor.Red)), Is.Null);
        }

        [Test]
        public void ASideWinsTogether()
        {
            Assert.That(Plain(new GameWon(PlayerColor.Red)), Is.EqualTo("RED wins the match"));
            Assert.That(Plain(new GameWon(PlayerColor.Red, new[] { PlayerColor.Red, PlayerColor.Green })),
                Is.EqualTo("RED and GREEN win the match"));
        }

        [Test]
        public void ARefusalSaysSo()
        {
            Assert.That(Plain(new CommandRejected("roll first")), Is.EqualTo("Can't: roll first"));
        }
    }
}
