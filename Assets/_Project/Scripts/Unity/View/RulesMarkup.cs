// Assets/_Project/Scripts/Unity/View/RulesMarkup.cs
using System;
using System.Text;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Text;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Draws a <see cref="RulesLine"/> as TextMeshPro markup (OPERATOR_GUIDE.md
    /// §2): values bright, keywords in the colour the board uses for them, and
    /// — where the label can be tapped — each keyword a link to its glossary
    /// card.
    /// </summary>
    /// <remarks>
    /// <b>The core chose the words; this only chooses how they look.</b> A
    /// status keyword is tinted with <see cref="StatusPalette.For"/>, the colour
    /// its tag has on a piece, so the word in the guide and the chip on the
    /// board are visibly the same thing.
    /// </remarks>
    public static class RulesMarkup
    {
        /// <summary>The markup for a line.</summary>
        /// <param name="linked">
        /// Wrap keywords in <c>&lt;link&gt;</c> tags and underline them. Only
        /// where a <see cref="KeywordLinks"/> is listening — an underline that
        /// does nothing when tapped is a promise the screen breaks.
        /// </param>
        public static string For(RulesLine line, bool linked)
        {
            if (line == null) return "";

            var text = new StringBuilder();

            foreach (var run in line.Runs)
            {
                switch (run.Kind)
                {
                    case RunKind.Number:
                        text.Append("<b><color=#").Append(UiTheme.Hex(UiTheme.GoldBright)).Append('>')
                            .Append(Escape(run.Text)).Append("</color></b>");
                        break;

                    case RunKind.Keyword:
                        var colour = UiTheme.Hex(ColourOf(run.Keyword, run.Text));
                        if (linked) text.Append("<link=\"").Append(run.Keyword).Append("\"><u>");
                        text.Append("<color=#").Append(colour).Append('>').Append(Escape(run.Text)).Append("</color>");
                        if (linked) text.Append("</u></link>");
                        break;

                    default:
                        text.Append(Escape(run.Text));
                        break;
                }
            }

            return text.ToString();
        }

        /// <summary>The colour a keyword is drawn in.</summary>
        public static Color ColourOf(string keyword, string shown = null)
        {
            if (keyword == null) return UiTheme.Text;

            if (keyword.StartsWith("status:", StringComparison.Ordinal) &&
                Enum.TryParse(keyword.Substring("status:".Length), out StatusKind status))
                return StatusPalette.For(status);

            if (keyword.StartsWith("damage:", StringComparison.Ordinal) &&
                Enum.TryParse(keyword.Substring("damage:".Length), out DamageType damage))
                return DamageColour(damage);

            // "Enemy:" and "Ally:" name the two halves of a two-sided cast.
            if (keyword == Keywords.Mode)
                return shown != null && shown.StartsWith("Enemy", StringComparison.Ordinal) ? UiTheme.Threat : UiTheme.Heal;

            return UiTheme.Cyan;
        }

        /// <summary>Normal is plain, Tech is the device cyan, Atomic the over-time magenta that nothing stops.</summary>
        public static Color DamageColour(DamageType type)
        {
            switch (type)
            {
                case DamageType.Tech: return UiTheme.Cyan;
                case DamageType.Atomic: return UiTheme.OverTime;
                default: return UiTheme.Text;
            }
        }

        /// <summary>Rules text is plain words; a stray angle bracket must not open a tag.</summary>
        private static string Escape(string text) =>
            text.IndexOf('<') < 0 ? text : text.Replace("<", "<noparse><</noparse>");
    }
}
