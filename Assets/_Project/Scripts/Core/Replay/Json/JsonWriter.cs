// Assets/_Project/Scripts/Core/Replay/Json/JsonWriter.cs
using System;
using System.Globalization;
using System.Text;

namespace NonaRoyale.Core.Replay.Json
{
    /// <summary>
    /// Writes a <see cref="JsonNode"/> as compact, single-line JSON.
    /// </summary>
    /// <remarks>
    /// <b>The output is pure ASCII.</b> Every character outside printable
    /// ASCII is escaped as <c>\uXXXX</c>, so "Revú" is written
    /// <c>Revú</c>. A replay file then reads the same under any encoding
    /// a platform picks by default, with or without a byte-order mark, which
    /// is one question Android and Windows never get to disagree on.
    ///
    /// <b>Never a newline.</b> Control characters are escaped too, so one
    /// value is always one line, which is what the one-object-per-line format
    /// depends on.
    /// </remarks>
    public static class JsonWriter
    {
        public static string Write(JsonNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            var builder = new StringBuilder();
            Append(builder, node);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, JsonNode node)
        {
            switch (node.Kind)
            {
                case JsonKind.Null:
                    builder.Append("null");
                    break;

                case JsonKind.Bool:
                    builder.Append(node.BoolValue ? "true" : "false");
                    break;

                case JsonKind.Number:
                    builder.Append(node.NumberValue.ToString(CultureInfo.InvariantCulture));
                    break;

                case JsonKind.String:
                    AppendString(builder, node.StringValue);
                    break;

                case JsonKind.Array:
                    builder.Append('[');
                    for (int i = 0; i < node.Items.Count; i++)
                    {
                        if (i > 0) builder.Append(',');
                        Append(builder, node.Items[i]);
                    }
                    builder.Append(']');
                    break;

                case JsonKind.Object:
                    builder.Append('{');
                    for (int i = 0; i < node.Members.Count; i++)
                    {
                        if (i > 0) builder.Append(',');
                        AppendString(builder, node.Members[i].Key);
                        builder.Append(':');
                        Append(builder, node.Members[i].Value);
                    }
                    builder.Append('}');
                    break;

                default:
                    throw new InvalidOperationException($"Unknown JSON kind {node.Kind}.");
            }
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20 || c > 0x7E)
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(c);
                        break;
                }
            }

            builder.Append('"');
        }
    }
}