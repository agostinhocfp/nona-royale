// Assets/Tests/EditMode/Replay/MatchRecipeTests.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Replay;
using NonaRoyale.Core.Replay.Json;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Replay
{
    [TestFixture]
    public class MatchRecipeTests
    {
        private static readonly PlayerColor[] Three = { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green };

        /// <summary>The first events of a match: the deal and the first roll, which is where a shifted RNG shows.</summary>
        private static List<string> Opening(MatchFactory.Match match)
        {
            var events = ReplayKit.Texts(match.Engine.Start());
            events.AddRange(ReplayKit.Texts(match.Engine.Execute(new RollDiceCommand())));
            return events;
        }

        [Test]
        public void Recipe_Random_BuildsWhatTheFactoryDraws()
        {
            // The trap this type exists for: the draw consumes the match RNG,
            // so the squads and the first roll must both come out the same.
            for (int seed = 1; seed <= 10; seed++)
            {
                var fromRecipe = new MatchRecipe(Three, seed, SquadSource.Random).Build();
                var fromFactory = MatchFactory.Create(Three, seed, squads: null);

                Assert.AreEqual(ReplayKit.StateOf(fromFactory), ReplayKit.StateOf(fromRecipe), $"seed {seed}");
                Assert.AreEqual(Opening(fromFactory), Opening(fromRecipe), $"seed {seed}");
            }
        }

        [Test]
        public void Recipe_NamingTheDrawnSquads_ShiftsTheDice()
        {
            // Why the source is recorded and not just the squads: the same
            // operators, named instead of drawn, leave the RNG where the draw
            // would have moved it, and the dice come out different.
            int differ = 0;

            for (int seed = 1; seed <= 10; seed++)
            {
                var drawn = new MatchRecipe(Three, seed, SquadSource.Random);

                var named = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>();
                foreach (var player in drawn.Build().Players)
                {
                    var squad = new List<OperatorDefinition>();
                    foreach (var op in player.Operators) squad.Add(Roster.ByName(op.Name));
                    named[player.Color] = squad;
                }

                var drafted = new MatchRecipe(Three, seed, SquadSource.Drafted, named);

                Assert.AreEqual(ReplayKit.StateOf(drawn.Build()), ReplayKit.StateOf(drafted.Build()), "same squads");
                if (string.Join("\n", Opening(drawn.Build())) != string.Join("\n", Opening(drafted.Build()))) differ++;
            }

            Assert.Greater(differ, 0, "naming the drawn squads should shift the dice");
        }

        [Test]
        public void Recipe_Alpha_BuildsTheMeasurementMatch()
        {
            var fromRecipe = new MatchRecipe(Three, 11, SquadSource.Alpha, openingDeployments: 2).Build();
            var fromFactory = MatchFactory.CreateAlphaMatch(Three, 11, openingDeployments: 2);

            Assert.AreEqual(ReplayKit.StateOf(fromFactory), ReplayKit.StateOf(fromRecipe));
            Assert.AreEqual(Opening(fromFactory), Opening(fromRecipe));

            foreach (var op in fromRecipe.Operators)
                Assert.That(op.Name, Is.EqualTo("Bouncer").Or.EqualTo("Syla").Or.EqualTo("Kurbyn"));
        }

        [Test]
        public void Recipe_Drafted_FieldsTheNamedSquads_InOrder()
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                { PlayerColor.Red, new[] { Roster.ByName("Luka"), Roster.ByName("Mimi"), Roster.ByName("Fortuna") } },
                { PlayerColor.Green, new[] { Roster.ByName("Revú"), Roster.ByName("Kian"), Roster.ByName("Syla") } }
            };

            var match = new MatchRecipe(ReplayKit.SeatsFor(2), 5, SquadSource.Drafted, squads).Build();
            var fielded = ReplayWriter.FieldedBy(match);

            CollectionAssert.AreEqual(new[] { "Luka", "Mimi", "Fortuna" }, fielded[PlayerColor.Red]);
            CollectionAssert.AreEqual(new[] { "Revú", "Kian", "Syla" }, fielded[PlayerColor.Green]);
        }

        [Test]
        public void Recipe_BuiltTwice_GivesTwoIdenticalMatches()
        {
            var recipe = ReplayKit.RecipeFor(10, 4);
            Assert.AreEqual(Opening(recipe.Build()), Opening(recipe.Build()));
        }

        [Test]
        public void Recipe_RefusesWhatTheFactoryWouldIgnoreOrReject()
        {
            var squad = new[] { Roster.ByName("Luka"), Roster.ByName("Mimi"), Roster.ByName("Kian") };
            var redOnly = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> { { PlayerColor.Red, squad } };

            Assert.Throws<ArgumentException>(() => new MatchRecipe(new PlayerColor[0], 1, SquadSource.Random));
            Assert.Throws<ArgumentException>(() => new MatchRecipe(new[] { PlayerColor.Red, PlayerColor.Red }, 1, SquadSource.Random));
            Assert.Throws<ArgumentException>(() => new MatchRecipe(new[] { PlayerColor.None }, 1, SquadSource.Random));
            Assert.Throws<ArgumentException>(() => new MatchRecipe(Three, 1, SquadSource.Drafted));
            Assert.Throws<ArgumentException>(() => new MatchRecipe(Three, 1, SquadSource.Drafted, redOnly));
            Assert.Throws<ArgumentException>(() => new MatchRecipe(new[] { PlayerColor.Red }, 1, SquadSource.Random, redOnly));
            Assert.Throws<ArgumentException>(() => new MatchRecipe(new[] { PlayerColor.Red }, 1, SquadSource.Drafted,
                new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> { { PlayerColor.Red, new[] { squad[0] } } }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchRecipe(Three, 1, SquadSource.Alpha, openingDeployments: -1));
        }

        // ── In and out of the header ─────────────────────────────────────

        [Test]
        public void RecipeCodec_RoundTrips_EverySourceBoardAndSideMap()
        {
            var sides = new[]
            {
                TeamMap.FreeForAll, TeamMap.CrossedPairs, TeamMap.AdjacentPairs,
                TeamMap.Of(new[] { PlayerColor.Red, PlayerColor.Violet }, new[] { PlayerColor.Blue }, new[] { PlayerColor.Green })
            };
            var boards = new[] { BoardProfile.Standard, BoardProfile.Sprint, BoardProfile.Cross("Compact", 3, laps: 2) };

            int seed = 0;
            foreach (var teams in sides)
            {
                foreach (var board in boards)
                {
                    seed++;
                    var drafted = ReplayKit.RecipeFor(3, 4);
                    var recipes = new[]
                    {
                        new MatchRecipe(ReplayKit.SeatsFor(4), seed, SquadSource.Random, null, board, 1, teams),
                        new MatchRecipe(ReplayKit.SeatsFor(4), seed, SquadSource.Alpha, null, board, 2, teams),
                        new MatchRecipe(ReplayKit.SeatsFor(4), seed, SquadSource.Drafted, SquadsOf(drafted), board, 0, teams)
                    };

                    foreach (var recipe in recipes)
                    {
                        string text = JsonWriter.Write(RecipeCodec.Write(recipe));
                        var back = RecipeCodec.Read(JsonReader.Parse(text));

                        Assert.AreEqual(text, JsonWriter.Write(RecipeCodec.Write(back)));
                        Assert.AreEqual(recipe.Source, back.Source);
                        Assert.AreEqual(recipe.Board.Name, back.Board.Name);
                        Assert.AreEqual(recipe.Board.Journey, back.Board.Journey);
                        foreach (var a in ReplayKit.SeatsFor(4))
                            foreach (var b in ReplayKit.SeatsFor(4))
                                Assert.AreEqual(teams.AreAllied(a, b), back.Teams.AreAllied(a, b), $"{teams}: {a}/{b}");

                        Assert.AreEqual(Opening(recipe.Build()), Opening(back.Build()), text);
                    }
                }
            }
        }

        [Test]
        public void RecipeCodec_NamedSideMaps_ReadBackAsThemselves()
        {
            foreach (var teams in new[] { TeamMap.FreeForAll, TeamMap.CrossedPairs, TeamMap.AdjacentPairs })
            {
                var recipe = new MatchRecipe(ReplayKit.SeatsFor(4), 1, SquadSource.Random, teams: teams);
                var back = RecipeCodec.Read(JsonReader.Parse(JsonWriter.Write(RecipeCodec.Write(recipe))));
                Assert.AreSame(teams, back.Teams);
            }
        }

        [Test]
        public void RecipeCodec_RefusesARecipeThatIsNotAMatch()
        {
            string good = JsonWriter.Write(RecipeCodec.Write(new MatchRecipe(Three, 1, SquadSource.Random)));

            foreach (var bad in new[]
            {
                good.Replace("\"Random\"", "\"Chaos\""),
                good.Replace("[\"Red\",\"Blue\",\"Green\"]", "[\"Red\",\"Red\"]"),
                good.Replace("\"circuit\":52", "\"circuit\":50"),
                good.Replace("\"teams\":[0,1,2,3]", "\"teams\":[0,1,2]"),
                good.Replace("}", ",\"squads\":{}}"),
                good.Replace("\"Random\"", "\"Drafted\"")
            })
            {
                Assert.AreNotEqual(good, bad, "the substitution should have changed something");
                Assert.Throws<JsonFormatException>(() => RecipeCodec.Read(JsonReader.Parse(bad)), bad);
            }
        }

        [Test]
        public void RecipeCodec_OperatorNames_AreMatchedExactly()
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                { PlayerColor.Red, new[] { Roster.ByName("Luka"), Roster.ByName("Mimi"), Roster.ByName("Kian") } }
            };
            string text = JsonWriter.Write(RecipeCodec.Write(
                new MatchRecipe(new[] { PlayerColor.Red }, 1, SquadSource.Drafted, squads)));

            Assert.Throws<JsonFormatException>(() => RecipeCodec.Read(JsonReader.Parse(text.Replace("\"Luka\"", "\"luka\""))));
            Assert.Throws<JsonFormatException>(() => RecipeCodec.Read(JsonReader.Parse(text.Replace("\"Luka\"", "\"Nobody\""))));
        }

        private static Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> SquadsOf(MatchRecipe recipe)
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>();
            foreach (var seat in recipe.Seats) squads[seat] = recipe.SquadOf(seat);
            return squads;
        }
    }
}