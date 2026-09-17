// Assets/_Project/Scripts/Unity/View/OperatorArtNames.cs
using System.Globalization;
using System.Text;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// File names for operator art (ART_PIPELINE.md §5). Plain C#, so it is
    /// tested outside the editor.
    /// </summary>
    public static class OperatorArtNames
    {
        /// <summary>
        /// The file-name stem for an operator: lowercase, accents stripped,
        /// anything else that is not a letter or digit collapsed to one
        /// underscore. <c>Revú</c> → <c>revu</c>, <c>Bio Link</c> →
        /// <c>bio_link</c>.
        /// </summary>
        public static string Key(string operatorName)
        {
            if (string.IsNullOrEmpty(operatorName)) return string.Empty;

            var decomposed = operatorName.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var key = new StringBuilder(decomposed.Length);

            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;

                if (char.IsLetterOrDigit(c)) key.Append(c);
                else if (key.Length > 0 && key[key.Length - 1] != '_') key.Append('_');
            }

            return key.ToString().Trim('_');
        }
    }
}