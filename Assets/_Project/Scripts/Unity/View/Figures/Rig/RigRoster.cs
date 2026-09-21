// Assets/_Project/Scripts/Unity/View/Figures/Rig/RigRoster.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Every operator with a rig (OPERATOR_LOOKBOOK.md, LB5). Judged in the
    /// Look Book window, and drawn on the board since LB5b
    /// (<see cref="OperatorRigArt"/>): a rig replaces the operator's
    /// front-view <see cref="LookRoster"/> figure, and a real render still
    /// replaces both.
    /// </summary>
    public static class RigRoster
    {
        public static IReadOnlyList<OperatorRig> All { get; } = new[]
        {
            BouncerRig.Rig,
            NuetuRig.Rig,
            SanityRig.Rig,
            SylaRig.Rig,
            KurbynRig.Rig,
            JaviRig.Rig,
            MimiRig.Rig,
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
