// Assets/_Project/Scripts/Unity/View/OperatorGlow.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The colour each operator's device glows when it is powered (ADR-0014,
    /// 2026-09-30): the chip's flare, the hero portrait's device glow, the
    /// cast tell and the cast's light on the board.
    /// </summary>
    /// <remarks>
    /// <b>The house's tech is cyan, like the board.</b> The house built the
    /// room and everything in it, so its four staff glow the same cyan as the
    /// board's powered cells, and so does Luka, whose ring is house tech taken
    /// off a house man. Everyone else brought their own gear from somewhere
    /// else, and it glows their own colour.
    ///
    /// <b>No glow sits on a seat's hue.</b> Seats are red, blue, green and
    /// violet (<see cref="UiTheme.Seat"/>) and they are gameplay information.
    /// Nuetu's red is the one near miss, chosen by the designer; it is brighter
    /// and hotter than the red seat and only shows while he casts.
    ///
    /// <b>The UI keeps cyan.</b> Selection, a ready ability, safe cells and
    /// the hero frame's arcs are the interface's language, not an operator's.
    /// So does the threat rim's amber (<see cref="UiTheme.Threat"/>), which is
    /// why no operator glows amber.
    ///
    /// The lit portraits are painted in the same colour, so the chip's lit
    /// face and its flare agree (<c>tools/art/recolour_glow.py</c>).
    /// </remarks>
    public static class OperatorGlow
    {
        /// <summary>The house's tech, and the colour of any operator not listed.</summary>
        public static Color House => UiTheme.Cyan;

        private static readonly Dictionary<string, Color> Colours = new Dictionary<string, Color>
        {
            { "syla", Hex(0xFF3FC8) },      // magenta: the blood hound's tracer
            { "mimi", Hex(0xDDF6FF) },      // ice white: cryo
            { "kian", Hex(0xD4FF3A) },      // chartreuse: a targeting light, over emerald
            { "nuetu", Hex(0xF12A27) },     // red, measured on the designer's painted lit chip
            { "fortuna", Hex(0xFFF1CC) },   // warm white: gilt light; a saturated gold would read as the threat rim
            { "revu", Hex(0xC04DFF) },      // orchid: the owner's money, not amber (amber is the threat rim)
            { "lethe", Hex(0xEEE6FF) },     // moon lilac: where the magic allowance is parked
        };

        /// <summary>The operator's glow; <see cref="House"/> for the house, Luka and anyone unlisted.</summary>
        public static Color For(string operatorName) =>
            Colours.TryGetValue(OperatorArtNames.Key(operatorName), out var colour) ? colour : House;

        /// <summary>Whether the operator glows the house's cyan.</summary>
        public static bool IsHouse(string operatorName) => !Colours.ContainsKey(OperatorArtNames.Key(operatorName));

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
