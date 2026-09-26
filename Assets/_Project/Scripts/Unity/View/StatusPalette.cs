// Assets/_Project/Scripts/Unity/View/StatusPalette.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A colour and a word per status, for the tags drawn under each operator.
    /// </summary>
    /// <remarks>
    /// The default cases matter: a status kind added to the core later still
    /// gets a tag, in grey and named after its enum member, without anyone
    /// remembering to update the view. A board that silently omits a status is
    /// worse than one that shows an unfamiliar mark (PRESENTATION §5).
    ///
    /// <b>Words, not letters.</b> Stun, Slow, Stealth and Shield all start with
    /// S, so single letters would need a legend, and a legend is what these
    /// tags exist to avoid. Words are short verbs or nouns a stranger can read
    /// without asking.
    /// </remarks>
    public static class StatusPalette
    {
        public static Color For(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Stun: return new Color(0.95f, 0.85f, 0.30f);          // yellow, held in place
                case StatusKind.Slow: return new Color(0.55f, 0.65f, 0.85f);          // cold blue
                case StatusKind.Bleed: return new Color(0.85f, 0.20f, 0.22f);         // red
                case StatusKind.Stealth: return new Color(0.55f, 0.45f, 0.80f);       // violet
                case StatusKind.Evasion: return new Color(0.35f, 0.80f, 0.70f);       // teal, a passive
                case StatusKind.Shield: return new Color(0.80f, 0.80f, 0.86f);        // steel
                case StatusKind.Mark: return new Color(0.95f, 0.55f, 0.20f);          // orange, tagged
                case StatusKind.Hastened: return new Color(0.60f, 0.85f, 0.30f);      // lime, sped up
                case StatusKind.ZeroDayCharge: return new Color(0.90f, 0.40f, 0.70f); // magenta, a pending payload
                case StatusKind.TechWard: return new Color(0.45f, 0.75f, 0.95f);      // bright cyan, a ward
                case StatusKind.Hunted: return new Color(0.95f, 0.30f, 0.45f);        // crimson, a pending strike
                case StatusKind.CryoField: return new Color(0.60f, 0.90f, 0.95f);     // pale ice, a carried chill
                case StatusKind.Watched: return new Color(0.75f, 0.55f, 0.95f);       // pale violet, a read pending
                case StatusKind.Burdened: return new Color(0.62f, 0.52f, 0.40f);      // dull bronze, weighed down
                case StatusKind.Equilibrium: return new Color(0.85f, 0.72f, 0.35f);   // coin gold, a price on everything
                case StatusKind.HouseEdge: return new Color(0.80f, 0.68f, 0.45f);     // brass, the house's cut
                default: return new Color(0.65f, 0.65f, 0.68f);
            }
        }

        /// <summary>
        /// The board's version of a status colour (HUD_PASS.md, H3): the same
        /// hue, off full chroma and brightness.
        /// </summary>
        /// <remarks>
        /// The palette above is tuned for a chip on a panel. The same chips over
        /// the near-black board were the brightest thing on screen after the
        /// vault, which put a passive status above the piece it described. The
        /// hue still tells them apart; it just stops shouting. The tray's
        /// operator card keeps the full-strength colour - it shows one piece at
        /// a time, on a panel, which is where these were tuned.
        ///
        /// Toward <see cref="UiTheme.Text"/> rather than toward grey, because
        /// that warm off-white is what everything else on this board
        /// desaturates into.
        /// </remarks>
        public static Color OnBoard(StatusKind kind)
        {
            var mixed = Color.Lerp(For(kind), UiTheme.Text, BoardMix);
            return new Color(mixed.r * BoardValue, mixed.g * BoardValue, mixed.b * BoardValue, 1f);
        }

        private const float BoardMix = 0.22f;
        private const float BoardValue = 0.78f;

        /// <summary>
        /// True for a status the piece itself shows, which therefore gets no tag.
        /// </summary>
        /// <remarks>
        /// Evasion, which fades the silhouette (<c>OperatorPiece</c>), and since
        /// 2026-09-24 Hastened, which trails lime streaks (<c>HasteTrail</c>).
        /// Both sit on the same pieces most of the match — passives, and
        /// Catalyst's wake — so as tags they taught nothing after the first turn.
        /// </remarks>
        public static bool IsDrawnOnPiece(StatusKind kind) =>
            kind == StatusKind.Evasion || kind == StatusKind.Hastened;

        /// <summary>The word printed on the tag.</summary>
        public static string Label(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Stun: return "STUN";
                case StatusKind.Slow: return "SLOW";
                case StatusKind.Bleed: return "BLEED";
                // The status's own name (G7c): HIDDEN on the board and Stealth in
                // the guide read as two different things.
                case StatusKind.Stealth: return "STEALTH";
                case StatusKind.Evasion: return "EVADE";
                case StatusKind.Shield: return "SHIELD";
                case StatusKind.Mark: return "MARKED";
                case StatusKind.Hastened: return "HASTE";
                case StatusKind.ZeroDayCharge: return "0-DAY";
                case StatusKind.TechWard: return "WARD";
                case StatusKind.Hunted: return "HUNTED";
                case StatusKind.CryoField: return "CRYO";
                case StatusKind.Watched: return "WATCHED";
                case StatusKind.Burdened: return "BURDEN";
                case StatusKind.Equilibrium: return "BALANCE";
                case StatusKind.HouseEdge: return "HOUSE";
                default: return kind.ToString().ToUpperInvariant();
            }
        }
    }
}
