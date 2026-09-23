// Assets/_Project/Scripts/Core/Replay/ReplayWriter.cs
using System;
using System.Collections.Generic;
using System.Text;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Replay.Json;

namespace NonaRoyale.Core.Replay
{
    /// <summary>
    /// Turns a replay into text: the header on line 1, then one line per
    /// accepted command.
    /// </summary>
    /// <remarks>
    /// <b>Every line ends in <c>\n</c>, the last one included.</b> That is how
    /// the reader tells a complete file from one a crash cut short: text after
    /// the final newline is a line that was still being written.
    ///
    /// <b>Lines can be written one at a time.</b> <see cref="HeaderLine"/> and
    /// <see cref="EntryLine"/> are what a store appends as the match goes, so a
    /// crash loses at most the command in flight.
    /// </remarks>
    public static class ReplayWriter
    {
        public const string NewLine = "\n";

        public static string Write(ReplayFile file)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            var builder = new StringBuilder();
            builder.Append(HeaderLine(file.Header));
            foreach (var entry in file.Entries) builder.Append(EntryLine(entry));
            return builder.ToString();
        }

        /// <summary>Line 1, newline included.</summary>
        public static string HeaderLine(ReplayHeader header)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));

            var node = JsonNode.NewObject()
                .Set("format", header.Format)
                .Set("rules", header.RulesHash)
                .Set("created", header.CreatedUtc)
                .Set("recipe", RecipeCodec.Write(header.Recipe))
                .Set("fielded", RecipeCodec.WriteSquads(header.Recipe.Seats, header.Fielded));

            if (header.SeatLabels.Count > 0)
            {
                var labels = JsonNode.NewObject();
                foreach (var seat in header.Recipe.Seats)
                {
                    string label;
                    if (header.SeatLabels.TryGetValue(seat, out label) && label != null)
                        labels.Set(seat.ToString(), label);
                }
                node.Set("labels", labels);
            }

            return JsonWriter.Write(node) + NewLine;
        }

        /// <summary>One command line, newline included.</summary>
        public static string EntryLine(ReplayEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            var node = JsonNode.NewObject()
                .Set("n", entry.Sequence)
                .Set("seat", entry.Seat.ToString());

            CommandCodec.Write(entry.Command, node);
            return JsonWriter.Write(node) + NewLine;
        }

        /// <summary>Each seat's fielded operators by name, read off a built match.</summary>
        public static IReadOnlyDictionary<PlayerColor, IReadOnlyList<string>> FieldedBy(MatchFactory.Match match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));

            var fielded = new Dictionary<PlayerColor, IReadOnlyList<string>>();
            foreach (var player in match.Players)
            {
                var names = new List<string>(player.Operators.Count);
                foreach (var op in player.Operators) names.Add(op.Name);
                fielded[player.Color] = names;
            }

            return fielded;
        }
    }
}