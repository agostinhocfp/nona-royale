// Assets/_Project/Scripts/Core/Replay/Json/JsonFormatException.cs
using System;

namespace NonaRoyale.Core.Replay.Json
{
    /// <summary>Text that is not the JSON subset a replay uses, or a value of the wrong shape.</summary>
    public sealed class JsonFormatException : Exception
    {
        public JsonFormatException(string message) : base(message) { }
    }
}