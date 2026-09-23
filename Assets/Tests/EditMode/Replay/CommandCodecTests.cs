// Assets/Tests/EditMode/Replay/CommandCodecTests.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Replay;
using NonaRoyale.Core.Replay.Json;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Replay
{
    [TestFixture]
    public class CommandCodecTests
    {
        /// <summary>
        /// One case per command shape. Adding a command to the core without a
        /// case here fails <see cref="Codec_CoversEveryCommandInTheAssembly"/>.
        /// </summary>
        private static IEnumerable<ICommand> Samples()
        {
            yield return new RollDiceCommand();
            yield return new DeployCommand(4);
            yield return new MoveCommand(3);
            yield return new MoveCommand(3, 5);
            yield return new UseAbilityCommand(2, 1101);
            yield return new UseAbilityCommand(2, 1101, targetOperatorId: 7);
            yield return new UseAbilityCommand(2, 1102, targetCell: CellRef.Track(17));
            yield return new UseAbilityCommand(2, 1102, targetCell: CellRef.HomeColumn(PlayerColor.Violet, 3));
            yield return new UseAbilityCommand(2, 1102, targetCell: CellRef.Yard(PlayerColor.Blue));
            yield return new UseAbilityCommand(2, 1102, targetCell: CellRef.Home(PlayerColor.Green));
            yield return new UseAbilityCommand(2, 1103, 9, CellRef.Track(0));
            yield return new CashDieCommand(5, 6);
            yield return new EndTurnCommand();
        }

        private static ICommand RoundTrip(ICommand command)
        {
            var line = JsonNode.NewObject();
            CommandCodec.Write(command, line);
            return CommandCodec.Read(JsonReader.Parse(JsonWriter.Write(line)));
        }

        [Test]
        public void Codec_RoundTrips_EveryCommandType()
        {
            foreach (var command in Samples())
            {
                var back = RoundTrip(command);

                Assert.AreEqual(command.GetType(), back.GetType());
                AssertSame(command, back);
            }
        }

        private static void AssertSame(ICommand a, ICommand b)
        {
            string what = CommandCodec.Describe(a);

            if (a is DeployCommand deploy)
            {
                Assert.AreEqual(deploy.OperatorId, ((DeployCommand)b).OperatorId, what);
            }
            else if (a is MoveCommand move)
            {
                var other = (MoveCommand)b;
                Assert.AreEqual(move.OperatorId, other.OperatorId, what);
                Assert.AreEqual(move.DieFace, other.DieFace, what);
            }
            else if (a is UseAbilityCommand use)
            {
                var other = (UseAbilityCommand)b;
                Assert.AreEqual(use.CasterOperatorId, other.CasterOperatorId, what);
                Assert.AreEqual(use.AbilityId, other.AbilityId, what);
                Assert.AreEqual(use.TargetOperatorId, other.TargetOperatorId, what);
                Assert.AreEqual(use.TargetCell, other.TargetCell, what);
            }
            else if (a is CashDieCommand cash)
            {
                var other = (CashDieCommand)b;
                Assert.AreEqual(cash.OperatorId, other.OperatorId, what);
                Assert.AreEqual(cash.DieFace, other.DieFace, what);
            }
        }

        [Test]
        public void Codec_CoversEveryCommandInTheAssembly()
        {
            var covered = new HashSet<Type>();
            foreach (var command in Samples()) covered.Add(command.GetType());

            foreach (var type in typeof(ICommand).Assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || !typeof(ICommand).IsAssignableFrom(type)) continue;

                // The CLI harness compiles the tests into the core's assembly;
                // their own fakes are not commands the game can send.
                if (type.Namespace != null && type.Namespace.StartsWith("NonaRoyale.Core.Tests", StringComparison.Ordinal)) continue;

                Assert.IsTrue(covered.Contains(type),
                    $"{type.Name} has no replay round-trip case. Give CommandCodec a writer and a reader for it, then a case in Samples().");
            }
        }

        [Test]
        public void Codec_WritesTheDocumentedWireForm()
        {
            var line = JsonNode.NewObject().Set("n", 3).Set("seat", "Red");
            CommandCodec.Write(new UseAbilityCommand(3, 1101, 7), line);
            Assert.AreEqual("{\"n\":3,\"seat\":\"Red\",\"cmd\":\"Cast\",\"op\":3,\"ability\":1101,\"target\":7}", JsonWriter.Write(line));

            line = JsonNode.NewObject();
            CommandCodec.Write(new MoveCommand(3), line);
            Assert.AreEqual("{\"cmd\":\"Move\",\"op\":3}", JsonWriter.Write(line), "a pooled move writes no die");

            line = JsonNode.NewObject();
            CommandCodec.Write(new UseAbilityCommand(1, 1102, targetCell: CellRef.HomeColumn(PlayerColor.Blue, 2)), line);
            Assert.AreEqual(
                "{\"cmd\":\"Cast\",\"op\":1,\"ability\":1102,\"cell\":{\"k\":\"HomeColumn\",\"o\":\"Blue\",\"i\":2}}",
                JsonWriter.Write(line));
        }

        [Test]
        public void Codec_UnknownCommandName_FailsLoudly()
        {
            var e = Assert.Throws<JsonFormatException>(
                () => CommandCodec.Read(JsonReader.Parse("{\"cmd\":\"Teleport\",\"op\":1}")));
            StringAssert.Contains("Teleport", e.Message);
        }

        [Test]
        public void Codec_MissingOrMistypedField_FailsLoudly()
        {
            foreach (var bad in new[]
            {
                "{\"op\":1}",
                "{\"cmd\":\"Deploy\"}",
                "{\"cmd\":\"Move\",\"op\":\"three\"}",
                "{\"cmd\":\"Cash\",\"op\":1}",
                "{\"cmd\":\"Cast\",\"op\":1}",
                "{\"cmd\":\"Cast\",\"op\":1,\"ability\":2,\"cell\":{\"k\":\"Moon\",\"i\":1}}",
                "{\"cmd\":\"Cast\",\"op\":1,\"ability\":2,\"cell\":{\"k\":\"Yard\",\"o\":\"3\"}}",
                "{\"cmd\":\"Cast\",\"op\":1,\"ability\":2,\"cell\":{\"k\":\"Yard\",\"o\":\"red\"}}",
                "{\"cmd\":\"Cast\",\"op\":1,\"ability\":2,\"cell\":{\"k\":\"Track\"}}"
            })
            {
                Assert.Throws<JsonFormatException>(() => CommandCodec.Read(JsonReader.Parse(bad)), $"accepted: {bad}");
            }
        }

        private sealed class NotARealCommand : ICommand
        {
        }

        [Test]
        public void Codec_CommandWithoutAWriter_FailsLoudly()
        {
            Assert.Throws<ArgumentException>(() => CommandCodec.Write(new NotARealCommand(), JsonNode.NewObject()));
        }
    }
}