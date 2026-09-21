// Assets/_Project/Scripts/Core/Text/Keywords.cs
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Text
{
    /// <summary>
    /// The glossary ids a rules line links to (OPERATOR_GUIDE.md §3). One
    /// place, so the formatter and the glossary cannot spell an id two ways.
    /// </summary>
    public static class Keywords
    {
        public static string Status(StatusKind kind) => "status:" + kind;
        public static string Damage(DamageType type) => "damage:" + type;

        public const string Critical = "term:critical";
        public const string Heavy = "term:heavy";
        public const string Lifesteal = "term:lifesteal";
        public const string Upkeep = "term:upkeep";
        public const string Execute = "term:execute";
        public const string Cleanse = "term:cleanse";
        public const string Energy = "term:energy";
        public const string Placement = "term:placement";
        public const string Mode = "term:mode";
        public const string SafeCell = "term:safe";
        public const string SpawnCell = "term:spawn";
        public const string Yard = "term:yard";
    }
}
