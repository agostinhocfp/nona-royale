// Assets/_Project/Scripts/Core/Replay/Json/JsonReader.cs
using System;
using System.Globalization;
using System.Text;

namespace NonaRoyale.Core.Replay.Json
{
    /// <summary>
    /// Parses one JSON value from text: the subset <see cref="JsonWriter"/>
    /// writes, read strictly.
    /// </summary>
    /// <remarks>
    /// <b>Strict on purpose.</b> A replay is evidence. A reader that shrugged at
    /// a stray character or a fractional seed would replay something other than
    /// what was recorded, and a desync reported three hundred commands later is
    /// much harder to read than a format error on line 2. So trailing text, a
    /// fraction, an exponent and a duplicate key are all refused.
    /// </remarks>
    public static class JsonReader
    {
        /// <summary>Nesting deeper than this is refused. A replay line nests three levels.</summary>
        public const int MaxDepth = 32;

        public static JsonNode Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            var cursor = new Cursor(text);
            cursor.SkipWhitespace();
            var value = ParseValue(cursor, 0);
            cursor.SkipWhitespace();

            if (!cursor.AtEnd) throw cursor.Error("unexpected text after the value");
            return value;
        }

        private static JsonNode ParseValue(Cursor cursor, int depth)
        {
            if (depth > MaxDepth) throw cursor.Error("nested too deeply");
            if (cursor.AtEnd) throw cursor.Error("unexpected end of text");

            char c = cursor.Peek;

            if (c == '{') return ParseObject(cursor, depth);
            if (c == '[') return ParseArray(cursor, depth);
            if (c == '"') return JsonNode.String(ParseString(cursor));
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(cursor);
            if (cursor.TryConsumeWord("true")) return JsonNode.Bool(true);
            if (cursor.TryConsumeWord("false")) return JsonNode.Bool(false);
            if (cursor.TryConsumeWord("null")) return JsonNode.Null();

            throw cursor.Error($"unexpected character '{c}'");
        }

        private static JsonNode ParseObject(Cursor cursor, int depth)
        {
            var node = JsonNode.NewObject();
            cursor.Expect('{');
            cursor.SkipWhitespace();

            if (cursor.TryConsume('}')) return node;

            while (true)
            {
                cursor.SkipWhitespace();
                if (cursor.AtEnd || cursor.Peek != '"') throw cursor.Error("expected a quoted key");

                string key = ParseString(cursor);
                if (node.Has(key)) throw cursor.Error($"duplicate key '{key}'");

                cursor.SkipWhitespace();
                cursor.Expect(':');
                cursor.SkipWhitespace();
                node.Set(key, ParseValue(cursor, depth + 1));
                cursor.SkipWhitespace();

                if (cursor.TryConsume(',')) continue;
                if (cursor.TryConsume('}')) return node;
                throw cursor.Error("expected ',' or '}'");
            }
        }

        private static JsonNode ParseArray(Cursor cursor, int depth)
        {
            var node = JsonNode.NewArray();
            cursor.Expect('[');
            cursor.SkipWhitespace();

            if (cursor.TryConsume(']')) return node;

            while (true)
            {
                cursor.SkipWhitespace();
                node.Add(ParseValue(cursor, depth + 1));
                cursor.SkipWhitespace();

                if (cursor.TryConsume(',')) continue;
                if (cursor.TryConsume(']')) return node;
                throw cursor.Error("expected ',' or ']'");
            }
        }

        private static JsonNode ParseNumber(Cursor cursor)
        {
            int start = cursor.Position;

            cursor.TryConsume('-');
            if (cursor.AtEnd || cursor.Peek < '0' || cursor.Peek > '9') throw cursor.Error("expected a digit");

            // JSON forbids a leading zero before more digits ("012").
            if (cursor.Peek == '0')
            {
                cursor.Advance();
                if (!cursor.AtEnd && cursor.Peek >= '0' && cursor.Peek <= '9')
                    throw cursor.Error("a number may not start with a leading zero");
            }
            else
            {
                while (!cursor.AtEnd && cursor.Peek >= '0' && cursor.Peek <= '9') cursor.Advance();
            }

            if (!cursor.AtEnd && (cursor.Peek == '.' || cursor.Peek == 'e' || cursor.Peek == 'E'))
                throw cursor.Error("numbers in a replay are whole; fractions and exponents are refused");

            string digits = cursor.Slice(start);
            long value;
            if (!long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                throw cursor.Error($"number out of range: {digits}");

            return JsonNode.Number(value);
        }

        private static string ParseString(Cursor cursor)
        {
            cursor.Expect('"');
            var builder = new StringBuilder();

            while (true)
            {
                if (cursor.AtEnd) throw cursor.Error("unterminated string");

                char c = cursor.Peek;
                cursor.Advance();

                if (c == '"') return builder.ToString();
                if (c < 0x20) throw cursor.Error("control character inside a string");

                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (cursor.AtEnd) throw cursor.Error("unterminated escape");
                char escape = cursor.Peek;
                cursor.Advance();

                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u': builder.Append(ParseHex4(cursor)); break;
                    default: throw cursor.Error($"unknown escape '\\{escape}'");
                }
            }
        }

        private static char ParseHex4(Cursor cursor)
        {
            int value = 0;

            for (int i = 0; i < 4; i++)
            {
                if (cursor.AtEnd) throw cursor.Error("unterminated \\u escape");

                char h = cursor.Peek;
                int digit;
                if (h >= '0' && h <= '9') digit = h - '0';
                else if (h >= 'a' && h <= 'f') digit = h - 'a' + 10;
                else if (h >= 'A' && h <= 'F') digit = h - 'A' + 10;
                else throw cursor.Error($"'{h}' is not a hex digit");

                value = (value << 4) | digit;
                cursor.Advance();
            }

            return (char)value;
        }

        private sealed class Cursor
        {
            private readonly string _text;

            public Cursor(string text) { _text = text; }

            public int Position { get; private set; }
            public bool AtEnd => Position >= _text.Length;
            public char Peek => _text[Position];

            public void Advance() => Position++;

            public string Slice(int start) => _text.Substring(start, Position - start);

            public void SkipWhitespace()
            {
                while (!AtEnd && (Peek == ' ' || Peek == '\t' || Peek == '\n' || Peek == '\r')) Position++;
            }

            public bool TryConsume(char c)
            {
                if (AtEnd || Peek != c) return false;
                Position++;
                return true;
            }

            public void Expect(char c)
            {
                if (!TryConsume(c)) throw Error($"expected '{c}'");
            }

            public bool TryConsumeWord(string word)
            {
                if (string.CompareOrdinal(_text, Position, word, 0, word.Length) != 0) return false;
                if (_text.Length - Position < word.Length) return false;
                Position += word.Length;
                return true;
            }

            public JsonFormatException Error(string message) =>
                new JsonFormatException($"{message} (at character {Position + 1})");
        }
    }
}