// Assets/_Project/Scripts/Unity/View/OperatorCopy.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Display text about an operator that the rules have no use for: the
    /// one-word role on the draft card (DRAFT.md decision 8).
    /// </summary>
    /// <remarks>
    /// Keyed by name with a fallback, the same pattern as
    /// <see cref="PieceShape"/>: an operator added to the roster still gets a
    /// card, just with the generic word (PRESENTATION §5). The roles are the
    /// archetypes <see cref="PieceShape"/> draws.
    /// </remarks>
    public static class OperatorCopy
    {
        public const string FallbackRole = "Operator";

        public static string Role(string name)
        {
            switch (name)
            {
                case "Bouncer": return "Tank";
                case "Syla": return "Assassin";
                case "Kurbyn": return "Brawler";
                case "Mimi": return "Controller";
                case "Javi": return "Support";
                case "Kian": return "Artillery";
                case "Sanity": return "Engineer";
                case "Luka": return "Duelist";
                case "Nuetu": return "Bruiser";
                default: return FallbackRole;
            }
        }
    }
}