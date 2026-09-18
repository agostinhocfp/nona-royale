// Assets/_Project/Scripts/Unity/View/UiFonts.cs
using TMPro;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The two faces the HUD draws with: a display face for the warm Deco
    /// register and a data face for everything the player reads as a number
    /// (UI_MOTION.md increment U3; the data face is GUI increment G5).
    /// </summary>
    /// <remarks>
    /// <b>One face per register</b> (ART_DIRECTION 2.1 and 8). The warm Deco
    /// register - the wordmark, screen titles, section headings, the draft
    /// clock - is <see cref="Display"/>, Cinzel. Everything else, which is to
    /// say every number on screen, is <see cref="Data"/>, Archivo. The type
    /// system then says the same thing the colours do.
    ///
    /// <b>Why Archivo, and not the default.</b> TMP falls back to Liberation
    /// Sans, which is Arial's metric clone and the house face of every
    /// unskinned Unity build. Archivo is within 2% of it on H, O and n, so
    /// nothing in the HUD reflows when it is swapped in, and it is flat-sided
    /// and deliberate where Liberation is anonymous.
    ///
    /// <b>Tabular figures are frozen into the file.</b> TMP in ugui 2.6 exposes
    /// only kern, liga, mark and mkmk through <c>fontFeatures</c> - there is no
    /// tnum to switch on - so the shipped TTFs have tnum and lnum applied to
    /// their outlines and every digit is one advance, with no runtime tag and
    /// no cost. Cinzel is <i>not</i> tabular (its 1 is 344/1000 against a 0 at
    /// 552), which is why the one place it draws digits, the draft clock, sets
    /// its own mono-spacing rather than taking this face.
    ///
    /// <b>Bold is a real face.</b> SemiBold is wired into weight 700 of the data
    /// face's weight table, so <c>FontStyles.Bold</c> swaps the typeface instead
    /// of smearing the regular one. Every bold number in the HUD goes through
    /// that path.
    ///
    /// Both faces load lazily from Resources and are wrapped in dynamic TMP
    /// assets at runtime, so nothing needs wiring in the editor. A missing font
    /// degrades to the default face with one warning: losing a face is a
    /// nuisance, losing the screen is not.
    /// </remarks>
    public static class UiFonts
    {
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasSize = 1024;

        /// <summary>Index into a TMP weight table for weight 700 (index = weight / 100).</summary>
        private const int BoldWeightIndex = 7;

        private static TMP_FontAsset _display;
        private static bool _displayResolved;

        private static TMP_FontAsset _data;
        private static bool _dataResolved;

        /// <summary>The display face - Cinzel - or null when the TTF is missing.</summary>
        public static TMP_FontAsset Display
        {
            get
            {
                if (_displayResolved) return _display;
                _displayResolved = true;
                _display = Load("Cinzel");
                return _display;
            }
        }

        /// <summary>The data face - Archivo, tabular - or null when the TTF is missing.</summary>
        public static TMP_FontAsset Data
        {
            get
            {
                if (_dataResolved) return _data;
                _dataResolved = true;

                _data = Load("Archivo");
                if (_data == null) return null;

                // Weight 700 becomes a real SemiBold rather than TMP's faux
                // bold. Without the regular face there is nothing to hang it
                // on, so this only runs once Archivo itself has loaded.
                var semiBold = Load("Archivo-SemiBold");
                if (semiBold != null)
                {
                    var weights = _data.fontWeightTable;
                    weights[BoldWeightIndex].regularTypeface = semiBold;
                    weights[BoldWeightIndex].italicTypeface = semiBold;
                }

                return _data;
            }
        }

        /// <summary>Swaps <paramref name="text"/> to the display face when it is available.</summary>
        public static void ApplyDisplay(TMP_Text text) => Apply(text, Display);

        /// <summary>Swaps <paramref name="text"/> to the data face when it is available.</summary>
        public static void ApplyData(TMP_Text text) => Apply(text, Data);

        private static void Apply(TMP_Text text, TMP_FontAsset font)
        {
            if (text == null || font == null) return;
            text.font = font;
        }

        private static TMP_FontAsset Load(string name)
        {
            var ttf = Resources.Load<Font>("Art/Fonts/" + name);
            if (ttf == null)
            {
                Debug.LogWarning($"UiFonts: Art/Fonts/{name}.ttf missing; that text keeps the default face.");
                return null;
            }

            return TMP_FontAsset.CreateFontAsset(ttf, SamplingPointSize, AtlasPadding,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, AtlasSize, AtlasSize,
                AtlasPopulationMode.Dynamic);
        }
    }
}
