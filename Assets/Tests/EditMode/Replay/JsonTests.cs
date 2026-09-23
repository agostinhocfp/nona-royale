// Assets/Tests/EditMode/Replay/JsonTests.cs
using NonaRoyale.Core.Replay.Json;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Replay
{
    [TestFixture]
    public class JsonTests
    {
        [Test]
        public void Json_RoundTrips_NestedValues()
        {
            var node = JsonNode.NewObject()
                .Set("n", 42)
                .Set("neg", -7)
                .Set("big", 9007199254740993L)
                .Set("s", "Red")
                .Set("t", true)
                .Set("f", false)
                .Set("nothing", JsonNode.Null())
                .Set("list", JsonNode.NewArray().Add(JsonNode.Number(1)).Add(JsonNode.String("two")).Add(JsonNode.NewArray()))
                .Set("inner", JsonNode.NewObject().Set("k", "Track").Set("i", 5));

            string text = JsonWriter.Write(node);
            Assert.AreEqual(
                "{\"n\":42,\"neg\":-7,\"big\":9007199254740993,\"s\":\"Red\",\"t\":true,\"f\":false,\"nothing\":null," +
                "\"list\":[1,\"two\",[]],\"inner\":{\"k\":\"Track\",\"i\":5}}",
                text);

            Assert.AreEqual(text, JsonWriter.Write(JsonReader.Parse(text)));
        }

        [Test]
        public void Json_WritesNonAsciiAndControlCharacters_AsEscapes()
        {
            string text = JsonWriter.Write(JsonNode.String("Revú \"q\" \\ \n\t\u0001"));

            Assert.AreEqual("\"Rev\\u00fa \\\"q\\\" \\\\ \\n\\t\\u0001\"", text);
            foreach (char c in text) Assert.Less((int)c, 0x80, "the writer's output is pure ASCII");

            Assert.AreEqual("Revú \"q\" \\ \n\t\u0001", JsonReader.Parse(text).StringValue);
        }

        [Test]
        public void Json_ReadsEveryEscape_AndWhitespaceBetweenTokens()
        {
            var node = JsonReader.Parse(" { \"a\" : \"\\/\\b\\f\\r\\u00FA\" , \"b\" : [ 1 , 2 ] } ");

            Assert.AreEqual("/\b\f\rú", node.GetString("a"));
            Assert.AreEqual(2, node.Get("b").Items.Count);
        }

        [Test]
        public void Json_RefusesFractionsAndExponents()
        {
            Assert.Throws<JsonFormatException>(() => JsonReader.Parse("1.5"));
            Assert.Throws<JsonFormatException>(() => JsonReader.Parse("1e3"));
            Assert.Throws<JsonFormatException>(() => JsonReader.Parse("{\"seed\":2.0}"));
        }

        [Test]
        public void Json_RefusesMalformedText()
        {
            foreach (var bad in new[]
            {
                "", "{", "}", "{\"a\":1,}", "[1,]", "{\"a\" 1}", "{a:1}", "\"open", "tru", "nul",
                "012", "-", "{\"a\":1} x", "\"\\q\"", "\"\\u00g0\"", "\"line\nbreak\"",
                "99999999999999999999"
            })
            {
                Assert.Throws<JsonFormatException>(() => JsonReader.Parse(bad), $"accepted: {bad}");
            }
        }

        [Test]
        public void Json_RefusesDuplicateKeys()
        {
            Assert.Throws<JsonFormatException>(() => JsonReader.Parse("{\"n\":1,\"n\":2}"));
            Assert.Throws<System.ArgumentException>(() => JsonNode.NewObject().Set("n", 1).Set("n", 2));
        }

        [Test]
        public void Json_RefusesDeepNesting()
        {
            string deep = new string('[', JsonReader.MaxDepth + 2) + new string(']', JsonReader.MaxDepth + 2);
            Assert.Throws<JsonFormatException>(() => JsonReader.Parse(deep));
        }

        [Test]
        public void Json_TypedGetters_FailLoudlyOnTheWrongShape()
        {
            var node = JsonReader.Parse("{\"n\":\"five\",\"s\":5,\"huge\":4294967296,\"none\":null}");

            Assert.Throws<JsonFormatException>(() => node.GetInt("n"));
            Assert.Throws<JsonFormatException>(() => node.GetString("s"));
            Assert.Throws<JsonFormatException>(() => node.GetInt("huge"));
            Assert.Throws<JsonFormatException>(() => node.GetInt("missing"));
            Assert.IsNull(node.GetOptionalInt("none"));
            Assert.IsNull(node.GetOptionalInt("missing"));
        }
    }
}