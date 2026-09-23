// Assets/_Project/Scripts/Core/Replay/EnumText.cs
using System;
using NonaRoyale.Core.Replay.Json;

namespace NonaRoyale.Core.Replay
{
    /// <summary>Enums in a replay are written by name and read back strictly.</summary>
    /// <remarks>
    /// <c>Enum.TryParse</c> alone would accept "3", "red" and a comma list,
    /// and happily return an undefined value for any of them. A replay names
    /// a seat or a cell kind exactly as the code spells it, or it is malformed.
    /// </remarks>
    internal static class EnumText
    {
        public static T Parse<T>(string text, string what) where T : struct
        {
            T value;
            if (text == null
                || text.Length == 0
                || !char.IsLetter(text[0])
                || text.IndexOf(',') >= 0
                || !Enum.TryParse(text, false, out value)
                || !Enum.IsDefined(typeof(T), value))
            {
                throw new JsonFormatException($"'{what}' has no {typeof(T).Name} named '{text}'");
            }

            return value;
        }

        public static T Get<T>(JsonNode node, string key) where T : struct =>
            Parse<T>(node.GetString(key), key);
    }
}