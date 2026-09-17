// Assets/_Project/Scripts/Unity/View/UiFonts.cs
using TMPro;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The display face — Cinzel, an engraved Roman capital — for titles, the
    /// wordmark and the draft clock (UI_MOTION.md increment U3).
    /// </summary>
    /// <remarks>
    /// Loaded lazily from <c>Resources/Art/Fonts/Cinzel.ttf</c> and wrapped in
    /// a dynamic TMP font asset at runtime, so nothing needs wiring in the
    /// editor. A missing font degrades to the default face with one warning:
    /// losing the display face is a nuisance, losing the screen is not.
    /// </remarks>
    public static class UiFonts
    {
        private static TMP_FontAsset _display;
        private static bool _resolved;

        /// <summary>The display font asset, or null when the TTF is missing.</summary>
        public static TMP_FontAsset Display
        {
            get
            {
                if (_resolved) return _display;
                _resolved = true;

                var ttf = Resources.Load<Font>("Art/Fonts/Cinzel");
                if (ttf == null)
                {
                    Debug.LogWarning("UiFonts: Art/Fonts/Cinzel.ttf missing; display text keeps the default face.");
                    return null;
                }

                _display = TMP_FontAsset.CreateFontAsset(ttf, 90, 9,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                return _display;
            }
        }

        /// <summary>Swaps <paramref name="text"/> to the display face when it is available.</summary>
        public static void ApplyDisplay(TMP_Text text)
        {
            if (text == null) return;

            var font = Display;
            if (font != null) text.font = font;
        }
    }
}
