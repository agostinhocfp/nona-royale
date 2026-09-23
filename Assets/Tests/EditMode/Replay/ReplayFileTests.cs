// Assets/Tests/EditMode/Replay/ReplayFileTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Replay;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Replay
{
    /// <summary>The <c>.nrr</c> text format: one header line, one line per accepted command.</summary>
    [TestFixture]
    public class ReplayFileTests
    {
        /// <summary>A finished bot match, recorded, as text.</summary>
        private static string RecordedText(int seed = 6, int seats = 2) =>
            ReplayKit.PlayBots(ReplayKit.RecipeFor(seed, seats), 0).Recorder.ToText();

        [Test]
        public void Writer_Reader_RoundTrip_IsTheSameText()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                string text = RecordedText(seed, 2 + seed % 3);
                var file = ReplayReader.Read(text);

                Assert.IsFalse(file.Truncated);
                Assert.AreEqual(text, ReplayWriter.Write(file), $"seed {seed}");
            }
        }

        [Test]
        public void Writer_EveryLineEndsInANewline_AndIsPureAscii()
        {
            string text = RecordedText();

            Assert.IsTrue(text.EndsWith("\n"));
            Assert.IsFalse(text.Contains("\r"));
            foreach (char c in text) Assert.Less((int)c, 0x80);

            var lines = text.Split('\n');
            Assert.AreEqual("", lines[lines.Length - 1]);
            StringAssert.StartsWith("{\"format\":1,\"rules\":\"", lines[0]);
            StringAssert.StartsWith("{\"n\":1,\"seat\":\"", lines[1]);
        }

        [Test]
        public void Header_AccentedOperator_IsWrittenAsAnEscape_AndReadsBack()
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                { PlayerColor.Red, new[] { Roster.ByName("Revú"), Roster.ByName("Kian"), Roster.ByName("Syla") } },
                { PlayerColor.Green, new[] { Roster.ByName("Luka"), Roster.ByName("Mimi"), Roster.ByName("Fortuna") } }
            };
            var recipe = new MatchRecipe(ReplayKit.SeatsFor(2), 9, SquadSource.Drafted, squads);
            var labels = new Dictionary<PlayerColor, string> { { PlayerColor.Green, "CPU · BRAWLER" } };

            var recorder = new ReplayRecorder(recipe.Build(), recipe, ReplayKit.Created, labels);
            string header = recorder.HeaderLine;

            StringAssert.Contains("\"Rev\\u00fa\"", header);
            StringAssert.Contains("\"CPU \\u00b7 BRAWLER\"", header);

            var back = ReplayReader.Read(header).Header;
            Assert.AreEqual("Revú", back.Fielded[PlayerColor.Red][0]);
            Assert.AreEqual("Revú", back.Recipe.SquadOf(PlayerColor.Red)[0].Name);
            Assert.AreEqual("CPU · BRAWLER", back.SeatLabels[PlayerColor.Green]);
            Assert.AreEqual(ReplayKit.Created, back.CreatedUtc);
            Assert.AreEqual(RulesFingerprint.Current, back.RulesHash);
        }

        // ── A file cut short ─────────────────────────────────────────────

        [Test]
        public void Replay_TruncatedFile_PlaysUpToLastCompleteLine()
        {
            string text = RecordedText();
            var whole = ReplayReader.Read(text);

            // Cut halfway through the last command line, as a crash would.
            int lastLineStart = text.LastIndexOf('\n', text.Length - 2) + 1;
            int cut = lastLineStart + (text.Length - lastLineStart) / 2;
            var file = ReplayReader.Read(text.Substring(0, cut));

            Assert.IsTrue(file.Truncated);
            Assert.AreEqual(whole.Entries.Count - 1, file.Entries.Count);

            var result = ReplayPlayer.Play(file);
            Assert.IsTrue(result.Truncated, "a truncated file says so in its result");
            Assert.AreEqual(file.Entries.Count, result.CommandsPlayed);

            var expected = ReplayPlayer.PlayTo(whole, file.Entries.Count);
            Assert.AreEqual(ReplayKit.StateOf(expected.Match), ReplayKit.StateOf(result.Match));
        }

        [Test]
        public void Reader_EveryCutPoint_EitherReadsOrIsTruncated_NeverThrows()
        {
            string text = RecordedText(seed: 3);
            int headerEnd = text.IndexOf('\n') + 1;

            // Every cut through the first few command lines is a file a crash
            // could leave. (Every cut of the whole file is quadratic, and the
            // lines are all alike.)
            int end = System.Math.Min(text.Length, headerEnd + 600);
            for (int cut = headerEnd; cut <= end; cut++)
            {
                var file = ReplayReader.Read(text.Substring(0, cut));
                bool atLineEnd = text[cut - 1] == '\n';
                if (atLineEnd) Assert.IsFalse(file.Truncated, $"cut at {cut}");
            }
        }

        [Test]
        public void Reader_TailCompleteButMissingItsNewline_IsKept()
        {
            string text = RecordedText();
            var whole = ReplayReader.Read(text);
            var file = ReplayReader.Read(text.Substring(0, text.Length - 1));

            Assert.IsFalse(file.Truncated);
            Assert.AreEqual(whole.Entries.Count, file.Entries.Count);
        }

        [Test]
        public void Reader_HeaderOnly_IsAReplayOfNoCommands()
        {
            string header = RecordedText().Split('\n')[0];

            Assert.AreEqual(0, ReplayReader.Read(header + "\n").Entries.Count);
            Assert.AreEqual(0, ReplayReader.Read(header).Entries.Count);
            Assert.AreEqual(0, ReplayPlayer.Play(ReplayReader.Read(header)).CommandsPlayed);
        }

        // ── Anything else is an error ────────────────────────────────────

        [Test]
        public void Reader_MalformedMiddleLine_ThrowsWithItsLineNumber()
        {
            var lines = new List<string>(RecordedText().Split('\n'));
            lines[3] = lines[3].Substring(0, lines[3].Length / 2);

            var e = Assert.Throws<ReplayFormatException>(() => ReplayReader.Read(string.Join("\n", lines)));
            Assert.AreEqual(4, e.Line);
        }

        [Test]
        public void Reader_SequenceGap_Throws()
        {
            var lines = new List<string>(RecordedText().Split('\n'));
            lines.RemoveAt(2);

            var e = Assert.Throws<ReplayFormatException>(() => ReplayReader.Read(string.Join("\n", lines)));
            Assert.AreEqual(3, e.Line);
            StringAssert.Contains("expected command 2", e.Message);
        }

        [Test]
        public void Reader_BlankLine_Throws()
        {
            var lines = new List<string>(RecordedText().Split('\n'));
            lines.Insert(2, "");

            Assert.Throws<ReplayFormatException>(() => ReplayReader.Read(string.Join("\n", lines)));
        }

        [Test]
        public void Reader_UnknownCommandOnTheLastLine_IsAnError_NotATruncation()
        {
            string text = RecordedText();
            int n = ReplayReader.Read(text).Entries.Count + 1;
            string forged = text + "{\"n\":" + n + ",\"seat\":\"Red\",\"cmd\":\"Teleport\"}";

            var e = Assert.Throws<ReplayFormatException>(() => ReplayReader.Read(forged));
            StringAssert.Contains("Teleport", e.Message);
        }

        [Test]
        public void Reader_EmptyOrHeaderless_Throws()
        {
            Assert.Throws<ReplayFormatException>(() => ReplayReader.Read(""));
            Assert.Throws<ReplayFormatException>(() => ReplayReader.Read("{\"n\":1,\"seat\":\"Red\",\"cmd\":\"Roll\"}\n"));
            Assert.Throws<ReplayFormatException>(() => ReplayReader.Read("not json\n"));
        }

        [Test]
        public void Reader_NewerFormat_IsIncompatible()
        {
            string text = RecordedText().Replace("{\"format\":1,", "{\"format\":2,");
            Assert.Throws<ReplayIncompatibleException>(() => ReplayReader.Read(text));
        }

        [Test]
        public void Reader_ToleratesWindowsLineEndings()
        {
            string text = RecordedText();
            var file = ReplayReader.Read(text.Replace("\n", "\r\n"));

            Assert.IsFalse(file.Truncated);
            Assert.AreEqual(text, ReplayWriter.Write(file));
        }
    }
}