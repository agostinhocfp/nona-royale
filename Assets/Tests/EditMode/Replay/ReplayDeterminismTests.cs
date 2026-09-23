// Assets/Tests/EditMode/Replay/ReplayDeterminismTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Replay;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Replay
{
    /// <summary>
    /// The headline of Stage 6: a seed plus the accepted commands reproduce a
    /// whole match, event for event (REPLAY.md). If these fail, the core has
    /// hidden nondeterminism, and that is the finding.
    /// </summary>
    [TestFixture]
    public class ReplayDeterminismTests
    {
        // ── The determinism proof ────────────────────────────────────────

        [Test]
        public void Replay_OfSeededBotMatch_ProducesIdenticalEventStream()
        {
            // 20 seeds x 2, 3 and 4 seats. The personality rotation moves with
            // the seed, so every personality plays every seat count, and the
            // recipe moves with it: all three squad sources, both side maps,
            // both boards, 0 to 2 opening deployments.
            int matches = 0;

            for (int seed = 1; seed <= 20; seed++)
            {
                for (int seats = 2; seats <= 4; seats++)
                {
                    var recipe = ReplayKit.RecipeFor(seed, seats);
                    var played = ReplayKit.PlayBots(recipe, personalityOffset: seed % 3);
                    string label = $"seed {seed}, {seats} seats, {recipe.Source}";

                    Assert.IsTrue(played.Run.Finished, $"{label}: the bot match did not finish ({played.Run.StopReason})");
                    Assert.Greater(played.Recorder.Entries.Count, 0, label);

                    // Through text and back: the file, not just the objects.
                    var file = ReplayReader.Read(played.Recorder.ToText());
                    var replayed = ReplayPlayer.Play(file);
                    var events = ReplayKit.Texts(replayed.Events);

                    int at = ReplayKit.FirstDifference(played.Events, events);
                    if (at >= 0)
                        Assert.Fail($"{label}: event {at} differs.\n live:   {ReplayKit.At(played.Events, at)}\n replay: {ReplayKit.At(events, at)}");

                    Assert.AreEqual(ReplayKit.StateOf(played.Match), ReplayKit.StateOf(replayed.Match), label);
                    Assert.IsTrue(replayed.Match.Engine.MatchOver, label);
                    matches++;
                }
            }

            Assert.AreEqual(60, matches);
        }

        [Test]
        public void Recorder_DoesNotConsumeMatchRng()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                var recipe = ReplayKit.RecipeFor(seed, 3);

                var with = ReplayKit.PlayBots(recipe, personalityOffset: 0, record: true);
                var without = ReplayKit.PlayBots(recipe, personalityOffset: 0, record: false);

                Assert.AreEqual(-1, ReplayKit.FirstDifference(with.Events, without.Events), $"seed {seed}");
                Assert.AreEqual(ReplayKit.StateOf(without.Match), ReplayKit.StateOf(with.Match), $"seed {seed}");
            }
        }

        [Test]
        public void Player_PlayTo_StopsAtTheRightState()
        {
            var played = ReplayKit.PlayBots(ReplayKit.RecipeFor(7, 3), personalityOffset: 1);
            var file = played.Recorder.ToFile();
            int total = file.Entries.Count;

            foreach (int n in new[] { 0, 1, 2, total / 3, total / 2, total - 1, total })
            {
                var result = ReplayPlayer.PlayTo(file, n);
                var expected = played.Events.GetRange(0, played.Boundaries[n]);

                Assert.AreEqual(n, result.CommandsPlayed);
                Assert.AreEqual(-1, ReplayKit.FirstDifference(expected, ReplayKit.Texts(result.Events)), $"after command {n}");
            }

            // Past the end is a caller's mistake, not a shorter replay.
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ReplayPlayer.PlayTo(file, total + 1));
        }

        // ── The engine seam ──────────────────────────────────────────────

        [Test]
        public void Engine_Executed_ReportsTheSeatWhoseTurnItWas()
        {
            // Includes every end-turn, where the current player has already
            // changed by the time the events come back.
            var played = ReplayKit.PlayBots(ReplayKit.RecipeFor(4, 4), personalityOffset: 2);

            Assert.AreEqual(played.SentBy.Count, played.ExecutedBy.Count);
            for (int i = 0; i < played.SentBy.Count; i++)
                Assert.AreEqual(played.SentBy[i], played.ExecutedBy[i], $"command {i + 1}");
        }

        [Test]
        public void Engine_ARefusedCommand_EmitsOnlyItsRejection()
        {
            // The recorder drops refused commands on the strength of this: a
            // refusal changes nothing, so it reports nothing but itself.
            var match = MatchFactory.CreateAlphaMatch(ReplayKit.SeatsFor(2), seed: 3);
            var engine = match.Engine;
            engine.Start();

            var refused = new List<IReadOnlyList<IGameEvent>>();
            int executed = 0;
            engine.Executed += (seat, command, events) => executed++;

            // Before a roll.
            refused.Add(engine.Execute(new EndTurnCommand()));
            refused.Add(engine.Execute(new MoveCommand(1)));
            refused.Add(engine.Execute(new DeployCommand(1)));
            refused.Add(engine.Execute(new UseAbilityCommand(1, 999999)));
            refused.Add(engine.Execute(new CashDieCommand(1, 6)));

            Assert.IsFalse(ReplayKit.HasRejection(engine.Execute(new RollDiceCommand())));

            // After it: ids that do not exist or are not ours, faces not rolled.
            refused.Add(engine.Execute(new MoveCommand(999)));
            refused.Add(engine.Execute(new MoveCommand(4)));
            refused.Add(engine.Execute(new MoveCommand(1, 99)));
            refused.Add(engine.Execute(new DeployCommand(999)));
            refused.Add(engine.Execute(new UseAbilityCommand(1, 999999)));
            refused.Add(engine.Execute(new CashDieCommand(1, 99)));

            foreach (var events in refused)
            {
                Assert.AreEqual(1, events.Count, string.Join("; ", ReplayKit.Texts(events)));
                Assert.IsInstanceOf<CommandRejected>(events[0]);
            }

            Assert.AreEqual(refused.Count + 1, executed);
        }

        [Test]
        public void Recorder_KeepsOnlyAcceptedCommands_InOrder()
        {
            var recipe = new MatchRecipe(ReplayKit.SeatsFor(2), 3, SquadSource.Alpha);
            var match = recipe.Build();
            var lines = new List<string>();

            using (var recorder = new ReplayRecorder(match, recipe, ReplayKit.Created))
            {
                recorder.LineRecorded += lines.Add;
                match.Engine.Start();

                match.Engine.Execute(new EndTurnCommand());       // refused: roll first
                match.Engine.Execute(new RollDiceCommand());      // accepted
                match.Engine.Execute(new MoveCommand(999));       // refused

                Assert.AreEqual(1, recorder.Entries.Count);
                Assert.AreEqual(1, recorder.Entries[0].Sequence);
                Assert.AreEqual(PlayerColor.Red, recorder.Entries[0].Seat);
                Assert.IsInstanceOf<RollDiceCommand>(recorder.Entries[0].Command);

                Assert.AreEqual(1, lines.Count);
                Assert.AreEqual("{\"n\":1,\"seat\":\"Red\",\"cmd\":\"Roll\"}\n", lines[0]);

                recorder.Dispose();
                match.Engine.Execute(new EndTurnCommand());
                Assert.AreEqual(1, recorder.Entries.Count, "a disposed recorder stops listening");
            }
        }

        // ── Refusals ─────────────────────────────────────────────────────

        [Test]
        public void Replay_UnderChangedConfig_IsRefused()
        {
            var file = ReplayKit.PlayBots(ReplayKit.RecipeFor(2, 2), 0).Recorder.ToFile();

            string changed = RulesFingerprint.Of(
                GameConfig.Default, new CombatConfig(collisionDamage: 4), EnergyConfig.Default,
                Roster.All, RosterSpeeds.Default);

            var e = Assert.Throws<ReplayIncompatibleException>(() => ReplayPlayer.Play(file, changed));
            StringAssert.Contains(file.Header.RulesHash, e.Message);
            StringAssert.Contains(changed, e.Message);
        }

        [Test]
        public void Replay_TamperedCommand_DesyncsAtItsSequenceNumber()
        {
            var file = ReplayKit.PlayBots(ReplayKit.RecipeFor(5, 2), 0).Recorder.ToFile();

            // The first move, re-aimed at a die nobody rolled.
            int index = -1;
            for (int i = 0; i < file.Entries.Count && index < 0; i++)
                if (file.Entries[i].Command is MoveCommand) index = i;
            Assert.GreaterOrEqual(index, 0, "the match made no move");

            var original = file.Entries[index];
            var move = (MoveCommand)original.Command;
            var entries = new List<ReplayEntry>(file.Entries);
            entries[index] = new ReplayEntry(original.Sequence, original.Seat, new MoveCommand(move.OperatorId, 99));

            var e = Assert.Throws<ReplayDesyncException>(() => ReplayPlayer.Play(new ReplayFile(file.Header, entries)));
            Assert.AreEqual(original.Sequence, e.Sequence);
            StringAssert.Contains("99", e.Message);
        }

        [Test]
        public void Replay_CommandFromTheWrongSeat_Desyncs()
        {
            var file = ReplayKit.PlayBots(ReplayKit.RecipeFor(5, 2), 0).Recorder.ToFile();

            var first = file.Entries[0];
            var entries = new List<ReplayEntry>(file.Entries);
            entries[0] = new ReplayEntry(1, PlayerColor.Violet, first.Command);

            var e = Assert.Throws<ReplayDesyncException>(() => ReplayPlayer.Play(new ReplayFile(file.Header, entries)));
            Assert.AreEqual(1, e.Sequence);
            StringAssert.Contains("Violet", e.Message);
        }

        [Test]
        public void Replay_WhenTheRebuiltSquadsDiffer_DesyncsBeforeTheFirstCommand()
        {
            var file = ReplayKit.PlayBots(ReplayKit.RecipeFor(1, 2), 0).Recorder.ToFile();
            var header = file.Header;

            var fielded = new Dictionary<PlayerColor, IReadOnlyList<string>>();
            foreach (var pair in header.Fielded) fielded[pair.Key] = pair.Value;
            var seat = header.Recipe.Seats[0];
            fielded[seat] = new[] { "Mimi", "Mimi", "Mimi" };

            var forged = new ReplayHeader(header.Format, header.RulesHash, header.CreatedUtc, header.Recipe, fielded);

            var e = Assert.Throws<ReplayDesyncException>(() => ReplayPlayer.Play(new ReplayFile(forged, file.Entries)));
            Assert.AreEqual(0, e.Sequence);
            StringAssert.Contains(seat.ToString(), e.Message);
        }

        [Test]
        public void Replay_PastTheEndOfTheMatch_Desyncs()
        {
            var file = ReplayKit.PlayBots(ReplayKit.RecipeFor(8, 2), 0).Recorder.ToFile();
            var last = file.Entries[file.Entries.Count - 1];

            var entries = new List<ReplayEntry>(file.Entries);
            entries.Add(new ReplayEntry(last.Sequence + 1, last.Seat, new RollDiceCommand()));

            var e = Assert.Throws<ReplayDesyncException>(() => ReplayPlayer.Play(new ReplayFile(file.Header, entries)));
            Assert.AreEqual(last.Sequence + 1, e.Sequence);
        }
    }
}