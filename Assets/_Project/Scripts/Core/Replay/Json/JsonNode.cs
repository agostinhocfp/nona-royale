// Assets/_Project/Scripts/Core/Replay/Json/JsonNode.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Core.Replay.Json
{
    public enum JsonKind
    {
        Null = 0,
        Bool = 1,
        Number = 2,
        String = 3,
        Array = 4,
        Object = 5
    }

    /// <summary>
    /// One value of the small JSON subset a replay file uses: null, booleans,
    /// whole numbers, strings, arrays and objects (REPLAY.md).
    /// </summary>
    /// <remarks>
    /// <b>Hand-rolled because the core holds no Unity types</b>, so
    /// <c>JsonUtility</c> is out, and a package would be a dependency for a
    /// format that is a few dozen lines of grammar.
    ///
    /// <b>Numbers are whole.</b> Nothing a replay records is fractional —
    /// seeds, ids, faces, sequence numbers — so a fraction or an exponent is
    /// refused on read rather than rounded. That also keeps doubles, and the
    /// locale that formats them, out of the file entirely.
    ///
    /// <b>Object members keep their insertion order</b>, so a file written
    /// twice from the same data is the same text byte for byte.
    /// </remarks>
    public sealed class JsonNode
    {
        private readonly List<JsonNode> _items;
        private readonly List<KeyValuePair<string, JsonNode>> _members;

        private JsonNode(JsonKind kind, bool boolValue, long numberValue, string stringValue)
        {
            Kind = kind;
            BoolValue = boolValue;
            NumberValue = numberValue;
            StringValue = stringValue;

            if (kind == JsonKind.Array) _items = new List<JsonNode>();
            if (kind == JsonKind.Object) _members = new List<KeyValuePair<string, JsonNode>>();
        }

        public JsonKind Kind { get; }
        public bool BoolValue { get; }
        public long NumberValue { get; }
        public string StringValue { get; }

        /// <summary>An array's items, in order. Empty for every other kind.</summary>
        public IReadOnlyList<JsonNode> Items =>
            (IReadOnlyList<JsonNode>)_items ?? Array.Empty<JsonNode>();

        /// <summary>An object's members, in insertion order. Empty for every other kind.</summary>
        public IReadOnlyList<KeyValuePair<string, JsonNode>> Members =>
            (IReadOnlyList<KeyValuePair<string, JsonNode>>)_members
            ?? Array.Empty<KeyValuePair<string, JsonNode>>();

        // ── Construction ─────────────────────────────────────────────────

        public static JsonNode Null() => new JsonNode(JsonKind.Null, false, 0, null);

        public static JsonNode Bool(bool value) => new JsonNode(JsonKind.Bool, value, 0, null);

        public static JsonNode Number(long value) => new JsonNode(JsonKind.Number, false, value, null);

        public static JsonNode String(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return new JsonNode(JsonKind.String, false, 0, value);
        }

        public static JsonNode NewArray() => new JsonNode(JsonKind.Array, false, 0, null);

        public static JsonNode NewObject() => new JsonNode(JsonKind.Object, false, 0, null);

        /// <summary>Appends to an array. Returns this node, so calls chain.</summary>
        public JsonNode Add(JsonNode item)
        {
            if (Kind != JsonKind.Array) throw new InvalidOperationException($"Add on a {Kind}, not an array.");
            _items.Add(item ?? throw new ArgumentNullException(nameof(item)));
            return this;
        }

        /// <summary>
        /// Adds a member to an object. A duplicate key is an error: JSON leaves
        /// it undefined, and a replay has no use for one.
        /// </summary>
        public JsonNode Set(string key, JsonNode value)
        {
            if (Kind != JsonKind.Object) throw new InvalidOperationException($"Set on a {Kind}, not an object.");
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (Has(key)) throw new ArgumentException($"Duplicate key '{key}'.", nameof(key));

            _members.Add(new KeyValuePair<string, JsonNode>(key, value));
            return this;
        }

        public JsonNode Set(string key, long value) => Set(key, Number(value));

        public JsonNode Set(string key, string value) => Set(key, String(value));

        public JsonNode Set(string key, bool value) => Set(key, Bool(value));

        // ── Reading ──────────────────────────────────────────────────────

        public bool Has(string key)
        {
            JsonNode ignored;
            return TryGet(key, out ignored);
        }

        public bool TryGet(string key, out JsonNode value)
        {
            if (_members != null)
            {
                foreach (var member in _members)
                {
                    if (member.Key != key) continue;
                    value = member.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        /// <summary>The member named <paramref name="key"/>; a missing one is a format error.</summary>
        public JsonNode Get(string key)
        {
            if (Kind != JsonKind.Object)
                throw new JsonFormatException($"expected an object holding '{key}', found {Describe()}");

            JsonNode value;
            if (!TryGet(key, out value)) throw new JsonFormatException($"missing field '{key}'");
            return value;
        }

        public int GetInt(string key) => Get(key).AsInt(key);

        public string GetString(string key) => Get(key).AsString(key);

        public bool GetBool(string key) => Get(key).AsBool(key);

        /// <summary>The int at <paramref name="key"/>, or null when the member is absent or null.</summary>
        public int? GetOptionalInt(string key)
        {
            JsonNode value;
            if (!TryGet(key, out value) || value.Kind == JsonKind.Null) return null;
            return value.AsInt(key);
        }

        public int AsInt(string what = "value")
        {
            if (Kind != JsonKind.Number) throw new JsonFormatException($"'{what}' should be a number, found {Describe()}");
            if (NumberValue < int.MinValue || NumberValue > int.MaxValue)
                throw new JsonFormatException($"'{what}' is outside the range of an int: {NumberValue}");
            return (int)NumberValue;
        }

        public string AsString(string what = "value")
        {
            if (Kind != JsonKind.String) throw new JsonFormatException($"'{what}' should be a string, found {Describe()}");
            return StringValue;
        }

        public bool AsBool(string what = "value")
        {
            if (Kind != JsonKind.Bool) throw new JsonFormatException($"'{what}' should be true or false, found {Describe()}");
            return BoolValue;
        }

        public IReadOnlyList<JsonNode> AsArray(string what = "value")
        {
            if (Kind != JsonKind.Array) throw new JsonFormatException($"'{what}' should be an array, found {Describe()}");
            return _items;
        }

        public JsonNode AsObject(string what = "value")
        {
            if (Kind != JsonKind.Object) throw new JsonFormatException($"'{what}' should be an object, found {Describe()}");
            return this;
        }

        private string Describe() => Kind.ToString().ToLowerInvariant();

        public override string ToString() => JsonWriter.Write(this);
    }
}