// Assets/_Project/Scripts/Unity/View/StatusPalette.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A colour per status, for the badges drawn under each operator.
    /// </summary>
    /// <remarks>
    /// The default case matters: a status kind added to the core later still
    /// gets a badge, in grey, without anyone remembering to update the view. A
    /// board that silently omits a status is worse than one that shows an
    /// unfamiliar mark.
    /// </remarks>
    public static class StatusPalette
    {
        public static Color For(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Stun: return new Color(0.95f, 0.85f, 0.30f);   // yellow, held in place
                case StatusKind.Slow: return new Color(0.55f, 0.65f, 0.85f);   // cold blue
                case StatusKind.Bleed: return new Color(0.85f, 0.20f, 0.22f);  // red
                case StatusKind.Stealth: return new Color(0.55f, 0.45f, 0.80f); // violet
                case StatusKind.Evasion: return new Color(0.35f, 0.80f, 0.70f); // teal, a passive
                case StatusKind.Shield: return new Color(0.80f, 0.80f, 0.86f);  // steel
                case StatusKind.Mark: return new Color(0.95f, 0.55f, 0.20f);    // orange, tagged
                default: return new Color(0.65f, 0.65f, 0.68f);
            }
        }
    }
}