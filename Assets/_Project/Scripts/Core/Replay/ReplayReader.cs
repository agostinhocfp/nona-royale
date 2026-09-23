// Assets/_Project/Scripts/Core/Replay/ReplayReader.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Replay.Json;

namespace NonaRoyale.Core.Replay
{
    /// <summary>
    /// Reads replay text back into a <see cref="ReplayFile"/>, tolerating a
    /// file cut short and nothing else.
    /// </summary>
    /// <remarks>
    /// <b>What counts as cut short.</b> The writer ends every line with a
    /// newline, so only the text after the last newline can be a line caught
    /// mid-write. If that text does not parse, it is dropped and the file is
    /// marked <see cref="ReplayFile.Truncated"/>. If it does parse, the crash
    /// landed between the closing brace and the newline, and the line is kept.
    ///
    /// <b>Everything else is an error.</b> A bad line in the middle, an unknown
    /// command, a gap in the sequence numbers or a missing header all throw
    /// <see cref="ReplayFormatException"/> naming the line. A complete line
    /// with an unknown command at the end is not a truncation either: it was
    /// fully written, and this build cannot read it.
    ///
    /// Blank lines are refused too. The writer never makes one, so one means
    /// the file was edited or damaged.
    /// </remarks>
    public static class ReplayReader
    {
        public static ReplayFile Read(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            // Tolerate CRLF: a file that passed through an editor or a Windows
            // copy is the same file.
            var lines = text.Replace("\r\n", "\n").Split('\n');

            // After a final "\n", Split yields one empty string: that is the
            // normal, complete end. Anything else in the last slot is a tail
            // with no newline after it.
            int complete = lines.Length - 1;
            string tail = lines[lines.Length - 1];

            if (complete == 0)
            {
                // No newline at all: at best a header whose newline never got
                // written. Without a header there is no replay.
                if (tail.Length == 0) throw new ReplayFormatException(1, "the file is empty");
                var onlyHeader = ReadHeader(tail);
                return new ReplayFile(onlyHeader, Array.Empty<ReplayEntry>(), truncated: false);
            }

            var header = ReadHeader(lines[0]);
            var entries = new List<ReplayEntry>();

            for (int i = 1; i < complete; i++)
                entries.Add(ReadEntry(lines[i], i + 1, entries.Count + 1));

            bool truncated = false;

            if (tail.Length > 0)
            {
                JsonNode node = null;
                try
                {
                    node = JsonReader.Parse(tail);
                }
                catch (JsonFormatException)
                {
                    truncated = true;
                }

                if (node != null) entries.Add(ReadEntry(node, lines.Length, entries.Count + 1));
            }

            return new ReplayFile(header, entries, truncated);
        }

        private static ReplayHeader ReadHeader(string text)
        {
            try
            {
                var node = JsonReader.Parse(text).AsObject("header");

                int format = node.GetInt("format");
                if (format > ReplayHeader.CurrentFormat)
                    throw new ReplayIncompatibleException(
                        $"replay format {format} is newer than this build reads ({ReplayHeader.CurrentFormat})");

                var recipe = RecipeCodec.Read(node.Get("recipe"));
                var fielded = RecipeCodec.ReadSquads(node.Get("fielded"));

                var labels = new Dictionary<PlayerColor, string>();
                JsonNode labelNode;
                if (node.TryGet("labels", out labelNode))
                {
                    foreach (var member in labelNode.AsObject("labels").Members)
                        labels[EnumText.Parse<PlayerColor>(member.Key, "labels")] = member.Value.AsString(member.Key);
                }

                return new ReplayHeader(
                    format, node.GetString("rules"), node.GetString("created"), recipe, fielded, labels);
            }
            catch (JsonFormatException e)
            {
                throw new ReplayFormatException(1, e.Message);
            }
            catch (ArgumentException e)
            {
                throw new ReplayFormatException(1, e.Message);
            }
        }

        private static ReplayEntry ReadEntry(string text, int line, int expected)
        {
            if (text.Length == 0) throw new ReplayFormatException(line, "blank line");

            JsonNode node;
            try
            {
                node = JsonReader.Parse(text);
            }
            catch (JsonFormatException e)
            {
                throw new ReplayFormatException(line, e.Message);
            }

            return ReadEntry(node, line, expected);
        }

        private static ReplayEntry ReadEntry(JsonNode node, int line, int expected)
        {
            try
            {
                node.AsObject("command line");

                int n = node.GetInt("n");
                if (n != expected)
                    throw new ReplayFormatException(line, $"expected command {expected}, found {n}");

                var seat = EnumText.Get<PlayerColor>(node, "seat");
                return new ReplayEntry(n, seat, CommandCodec.Read(node));
            }
            catch (JsonFormatException e)
            {
                throw new ReplayFormatException(line, e.Message);
            }
        }
    }
}