// Assets/_Project/Scripts/Unity/View/Figures/LookRoster.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Every operator that has a look-book recipe (OPERATOR_LOOKBOOK.md, LB2).
    /// Adding a recipe means adding a file under <c>Looks/</c> and one line here.
    /// </summary>
    public static class LookRoster
    {
        public static IReadOnlyList<OperatorLook> All { get; } = new[]
        {
            BouncerLook.Look,
            MimiLook.Look,
            NuetuLook.Look,
        };

        /// <summary>The recipe for an operator, by roster name or art key; null when there is none.</summary>
        public static OperatorLook Find(string operatorName)
        {
            string key = OperatorArtNames.Key(operatorName);
            foreach (var look in All)
                if (look.Key == key) return look;

            return null;
        }
    }
}
