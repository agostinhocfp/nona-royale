// Assets/_Project/Scripts/Unity/View/Figures/Rig/RigRoster.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Every operator with a rig (OPERATOR_LOOKBOOK.md, LB5). Judged in the
    /// Look Book window; not on the board until LB5b, so the front-view
    /// <see cref="LookRoster"/> still draws in play.
    /// </summary>
    public static class RigRoster
    {
        public static IReadOnlyList<OperatorRig> All { get; } = new[]
        {
            BouncerRig.Rig,
        };

        public static OperatorRig Find(string operatorName)
        {
            string key = OperatorArtNames.Key(operatorName);
            foreach (var rig in All)
                if (rig.Key == key) return rig;

            return null;
        }
    }
}
