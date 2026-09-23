// Assets/_Project/Scripts/Core/Replay/CommandCodec.cs
using System;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Replay.Json;

namespace NonaRoyale.Core.Replay
{
    /// <summary>
    /// Every command, to and from the fields of one replay line.
    /// </summary>
    /// <remarks>
    /// <b>One explicit writer and one explicit reader per command type.</b> No
    /// reflection, so what a command looks like on disk is written down here
    /// and does not move when a property is renamed. An unknown command is an
    /// error in both directions, never a skipped line: a replay missing one
    /// command is a different match, and the desync it causes surfaces far
    /// from the cause.
    ///
    /// <b>Adding a command means adding it here.</b> A test walks every
    /// concrete <see cref="ICommand"/> in the core and fails for any without a
    /// round-trip case, so the next <c>CashDieCommand</c> cannot be forgotten.
    ///
    /// <b>The wire names are short and fixed</b>: <c>Roll</c>, <c>Deploy</c>,
    /// <c>Move</c>, <c>Cast</c>, <c>Cash</c>, <c>End</c>. Renaming one breaks
    /// every file already written, so that takes a new envelope format.
    /// </remarks>
    public static class CommandCodec
    {
        public const string CommandKey = "cmd";

        public const string Roll = "Roll";
        public const string Deploy = "Deploy";
        public const string Move = "Move";
        public const string Cast = "Cast";
        public const string Cash = "Cash";
        public const string End = "End";

        /// <summary>Writes <paramref name="command"/>'s name and fields onto <paramref name="line"/>.</summary>
        public static void Write(ICommand command, JsonNode line)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (line == null) throw new ArgumentNullException(nameof(line));

            if (command is RollDiceCommand)
            {
                line.Set(CommandKey, Roll);
            }
            else if (command is DeployCommand deploy)
            {
                line.Set(CommandKey, Deploy);
                line.Set("op", deploy.OperatorId);
            }
            else if (command is MoveCommand move)
            {
                line.Set(CommandKey, Move);
                line.Set("op", move.OperatorId);
                // Absent means pool: every unspent die at once.
                if (move.DieFace.HasValue) line.Set("die", move.DieFace.Value);
            }
            else if (command is UseAbilityCommand use)
            {
                line.Set(CommandKey, Cast);
                line.Set("op", use.CasterOperatorId);
                line.Set("ability", use.AbilityId);
                if (use.TargetOperatorId.HasValue) line.Set("target", use.TargetOperatorId.Value);
                if (use.TargetCell.HasValue) line.Set("cell", WriteCell(use.TargetCell.Value));
            }
            else if (command is CashDieCommand cash)
            {
                line.Set(CommandKey, Cash);
                line.Set("op", cash.OperatorId);
                line.Set("die", cash.DieFace);
            }
            else if (command is EndTurnCommand)
            {
                line.Set(CommandKey, End);
            }
            else
            {
                throw new ArgumentException(
                    $"No replay codec for {command.GetType().Name}. Add a writer and a reader to CommandCodec.",
                    nameof(command));
            }
        }

        /// <summary>Reads the command a line names. An unknown name or a missing field throws.</summary>
        public static ICommand Read(JsonNode line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));

            string name = line.GetString(CommandKey);

            switch (name)
            {
                case Roll:
                    return new RollDiceCommand();

                case Deploy:
                    return new DeployCommand(line.GetInt("op"));

                case Move:
                    return new MoveCommand(line.GetInt("op"), line.GetOptionalInt("die"));

                case Cast:
                {
                    JsonNode cell;
                    CellRef? targetCell = null;
                    if (line.TryGet("cell", out cell) && cell.Kind != JsonKind.Null)
                        targetCell = ReadCell(cell.AsObject("cell"));

                    return new UseAbilityCommand(
                        line.GetInt("op"),
                        line.GetInt("ability"),
                        line.GetOptionalInt("target"),
                        targetCell);
                }

                case Cash:
                    return new CashDieCommand(line.GetInt("op"), line.GetInt("die"));

                case End:
                    return new EndTurnCommand();

                default:
                    throw new JsonFormatException($"unknown command '{name}'");
            }
        }

        /// <summary>A short human reading of a command, for desync messages and logs.</summary>
        public static string Describe(ICommand command)
        {
            if (command == null) return "null";

            var line = JsonNode.NewObject();
            try
            {
                Write(command, line);
            }
            catch (ArgumentException)
            {
                return command.GetType().Name;
            }

            return JsonWriter.Write(line);
        }

        // ── Cells ────────────────────────────────────────────────────────

        public static JsonNode WriteCell(CellRef cell)
        {
            var node = JsonNode.NewObject().Set("k", cell.Kind.ToString());

            switch (cell.Kind)
            {
                case CellKind.Track:
                    node.Set("i", cell.Index);
                    break;
                case CellKind.HomeColumn:
                    node.Set("o", cell.Owner.ToString());
                    node.Set("i", cell.Index);
                    break;
                case CellKind.Yard:
                case CellKind.Home:
                    node.Set("o", cell.Owner.ToString());
                    break;
                default:
                    throw new ArgumentException($"No replay form for a {cell.Kind} cell.", nameof(cell));
            }

            return node;
        }

        public static CellRef ReadCell(JsonNode node)
        {
            var kind = EnumText.Get<CellKind>(node, "k");

            switch (kind)
            {
                case CellKind.Track: return CellRef.Track(node.GetInt("i"));
                case CellKind.HomeColumn: return CellRef.HomeColumn(Seat(node), node.GetInt("i"));
                case CellKind.Yard: return CellRef.Yard(Seat(node));
                case CellKind.Home: return CellRef.Home(Seat(node));
                default: throw new JsonFormatException($"no replay form for a {kind} cell");
            }
        }

        private static PlayerColor Seat(JsonNode node) => EnumText.Get<PlayerColor>(node, "o");
    }
}