// Assets/_Project/Scripts/Unity/View/Figures/LookBookPalette.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The colours a look-book figure may use (OPERATOR_LOOKBOOK.md). Built
    /// from <c>UiTheme</c>, which owns every colour in the view; plain C# so
    /// the recipes, the rasteriser and the judging tools run outside the
    /// editor and on a worker thread.
    /// </summary>
    /// <remarks>
    /// <b>Shared first, then one block per operator</b>, in roster order as
    /// recipes land (LB2). A recipe reads only the shared colours and its own
    /// block; the value ledger (ART_DIRECTION §5.1) is why the blocks are
    /// separate at all.
    /// </remarks>
    public sealed class LookBookPalette
    {
        // ── Shared: the §2.2 drawn light and the §3 swatches ────────────

        /// <summary>The one ink line, <c>#1C0E12</c>.</summary>
        public FigureColour Ink;

        /// <summary>The cool rim on a dark figure: steel, never holo cyan (§5, devices are dark at rest).</summary>
        public FigureColour Rim;

        /// <summary>The cool rim on a light figure.</summary>
        public FigureColour RimOnLight;

        /// <summary>Multiplied into the hard shadow shape: the cool dark ambient.</summary>
        public FigureColour Shade;

        /// <summary>Screened into the hard highlight shape: the warm gold key.</summary>
        public FigureColour Key;

        /// <summary>Screened into a dark garment's lit planes: cooler than the key, or black cloth goes brown.</summary>
        public FigureColour SuitSheen;

        /// <summary>The hard specular wedge on black plate or black cloth.</summary>
        public FigureColour PlateSheen;

        /// <summary>Operator hardware (§3: aged brass, never gilt).</summary>
        public FigureColour Brass;

        /// <summary>The cast tell (holo cyan). Only ever drawn in the powered state.</summary>
        public FigureColour Powered;

        public FigureColour Bone;
        public FigureColour Obsidian;

        // ── Bouncer: black mass split by a hard white V ─────────────────
        public FigureColour BouncerSuit;
        public FigureColour BouncerSkin;

        // ── Syla: light core in a dark frame ────────────────────────────
        public FigureColour SylaGown;
        public FigureColour SylaCape;
        public FigureColour SylaSkin;

        // ── Kurbyn: mid-dark, broken by bare forearms ───────────────────
        public FigureColour KurbynCloth;
        public FigureColour KurbynSkin;

        // ── Javi: dark waistcoat block, two white sleeves ───────────────
        public FigureColour JaviWaistcoat;
        public FigureColour JaviGlove;
        public FigureColour JaviHair;
        public FigureColour JaviSkin;
        public FigureColour JaviSteel;
        public FigureColour JaviFrost;

        // ── Sanity: large mid-brown mass ────────────────────────────────
        public FigureColour SanityApron;
        public FigureColour SanityLivery;
        public FigureColour SanitySkin;
        public FigureColour SanitySteel;

        // ── Mimi: near-black and small, bright pale hardware ────────────
        public FigureColour MimiCoat;
        public FigureColour MimiRig;
        public FigureColour MimiSteel;
        public FigureColour MimiSkin;

        // ── Revú: the only red torso ────────────────────────────────────
        public FigureColour RevuJacket;
        public FigureColour RevuTrousers;
        public FigureColour RevuHair;
        public FigureColour RevuSkin;

        // ── Kian: the only green torso ──────────────────────────────────
        public FigureColour KianJacket;
        public FigureColour KianShirt;
        public FigureColour KianSkin;

        // ── Luka: the warm light torso, camel (LB5d) ────────────────────
        public FigureColour LukaBlazer;
        public FigureColour LukaShirt;
        public FigureColour LukaSkin;

        // ── Nuetu: the only all-light mass, cool dove-grey ──────────────
        public FigureColour NuetuGrey;
        public FigureColour NuetuPlate;
        public FigureColour NuetuSkin;
    }
}
