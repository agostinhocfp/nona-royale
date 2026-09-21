// Assets/_Project/Scripts/Unity/View/Figures/FigureDrawing.cs
using System;
using System.Collections.Generic;
using System.Globalization;

namespace NonaRoyale.Unity.View
{
    /// <summary>Which edges of a figure take the drawn rim (§2.2 rule 5).</summary>
    /// <remarks>
    /// A whole figure takes all three. A rig part takes only the edges that
    /// are outer on the assembled figure: a far arm's outside edge, never the
    /// edge that lies against the body (OPERATOR_LOOKBOOK.md, LB5).
    /// </remarks>
    [Flags]
    public enum RimEdges
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Top = 1 << 2,
        Sides = Left | Right,
        All = Left | Right | Top,
    }

    /// <summary>A straight (not premultiplied) RGBA colour in 0..1. Plain C#, for the figure rasteriser.</summary>
    public readonly struct FigureColour
    {
        public readonly float R, G, B, A;

        public FigureColour(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>From "RRGGBB" or "#RRGGBB".</summary>
        public static FigureColour Hex(string hex)
        {
            if (hex == null) throw new ArgumentNullException(nameof(hex));
            hex = hex.TrimStart('#');
            if (hex.Length != 6) throw new ArgumentException($"Expected RRGGBB, got '{hex}'.", nameof(hex));

            int value = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new FigureColour(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f);
        }

        public override string ToString() => $"({R:0.###}, {G:0.###}, {B:0.###}, {A:0.###})";
    }

    /// <summary>How a layer changes what is under it.</summary>
    public enum FigureBlend
    {
        /// <summary>Replaces the colour: a garment, a piece of hardware, a Deco motif.</summary>
        Fill,

        /// <summary>Multiplies: a hard shadow shape that crosses every material the same way.</summary>
        Shade,

        /// <summary>Screens: a hard highlight shape from the warm key.</summary>
        Light,

        /// <summary>A one-weight ink stroke centred on the shape's edge.</summary>
        InkLine,

        /// <summary>A fill drawn only while an ability is casting (the cyan tell). Never at rest.</summary>
        Powered,
    }

    /// <summary>One flat shape in a figure, clipped to its silhouette.</summary>
    public sealed class FigureLayer
    {
        public FigureShape Shape { get; }
        public FigureColour Colour { get; }
        public FigureBlend Blend { get; }

        public FigureLayer(FigureShape shape, FigureColour colour, FigureBlend blend)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Colour = colour;
            Blend = blend;
        }
    }

    /// <summary>
    /// A procedural operator figure as an ordered stack of flat shapes
    /// (OPERATOR_LOOKBOOK.md, "The layer stack").
    /// </summary>
    /// <remarks>
    /// <b>The order is fixed here, not by the recipe</b>, so no recipe can
    /// break ART_DIRECTION §2.2:
    /// <list type="number">
    /// <item>Ink: the silhouette grown by one line weight, in the ink colour
    /// (rule 2). Automatic.</item>
    /// <item>Base: the silhouette in the figure's base value.</item>
    /// <item>Blocks (<see cref="Block"/>): garments, hardware and the Deco
    /// motif, in the order written.</item>
    /// <item>Shades (<see cref="Shade"/>): hard shadow shapes, multiplied, so
    /// one cut crosses every material with the same edge (rule 5).</item>
    /// <item>Lights (<see cref="Light"/>): hard highlight shapes from the
    /// upper-left key.</item>
    /// <item>Rim (<see cref="Rim"/>): a constant-width sliver down both side
    /// edges and along the top, taken from the silhouette itself, so every
    /// figure's rim is the same width. After the blocks, so hardware on the
    /// edge is rimmed too.</item>
    /// <item>Ink lines (<see cref="InkLine"/>): the major internal forms only,
    /// at the same single weight.</item>
    /// <item>Powered (<see cref="Powered"/>): the cyan tell. Skipped unless the
    /// rasteriser is asked for the powered state (§5, devices are dark at rest).</item>
    /// </list>
    /// Everything after the ink is clipped to the silhouette. Hardware that
    /// should break the outline belongs in the silhouette.
    /// </remarks>
    public sealed class FigureDrawing
    {
        private readonly List<FigureLayer> _blocks = new List<FigureLayer>();
        private readonly List<FigureLayer> _shades = new List<FigureLayer>();
        private readonly List<FigureLayer> _lights = new List<FigureLayer>();
        private readonly List<FigureLayer> _lines = new List<FigureLayer>();
        private readonly List<FigureLayer> _powered = new List<FigureLayer>();

        public FigureShape Silhouette { get; }
        public FigureColour Base { get; }

        /// <summary>Whether the drawing has a powered (cyan tell) layer, drawn only when powered.</summary>
        public bool HasPowered => _powered.Count > 0;
        public FigureColour Ink { get; }

        /// <summary>The one line weight, in figure units (§2.2 rule 2).</summary>
        public float LineWeight { get; }

        public float RimWidth { get; private set; }
        public FigureColour RimColour { get; private set; }

        /// <summary>No rim below this height: the rim is a light on the body, not on the shoes.</summary>
        public float RimFloor { get; private set; }

        /// <summary>Which edges the rim lights. All of them unless a rig part says otherwise.</summary>
        public RimEdges RimEdges { get; private set; } = RimEdges.All;

        public FigureDrawing(FigureShape silhouette, FigureColour baseColour, FigureColour ink, float lineWeight)
        {
            Silhouette = silhouette ?? throw new ArgumentNullException(nameof(silhouette));
            Base = baseColour;
            Ink = ink;
            LineWeight = Math.Max(0f, lineWeight);
        }

        public FigureDrawing Block(FigureShape shape, FigureColour colour) => Add(_blocks, shape, colour, FigureBlend.Fill);

        public FigureDrawing Shade(FigureShape shape, FigureColour multiply) => Add(_shades, shape, multiply, FigureBlend.Shade);

        public FigureDrawing Light(FigureShape shape, FigureColour screen) => Add(_lights, shape, screen, FigureBlend.Light);

        public FigureDrawing InkLine(FigureShape shape) => Add(_lines, shape, Ink, FigureBlend.InkLine);

        public FigureDrawing Powered(FigureShape shape, FigureColour colour) => Add(_powered, shape, colour, FigureBlend.Powered);

        public FigureDrawing Rim(float width, FigureColour colour, float floor, RimEdges edges = RimEdges.All)
        {
            RimWidth = Math.Max(0f, width);
            RimColour = colour;
            RimFloor = floor;
            RimEdges = edges;
            return this;
        }

        /// <summary>Every layer after the base, in draw order. The rim is drawn between lights and ink lines.</summary>
        public IEnumerable<FigureLayer> BeforeRim()
        {
            foreach (var layer in _blocks) yield return layer;
            foreach (var layer in _shades) yield return layer;
            foreach (var layer in _lights) yield return layer;
        }

        public IEnumerable<FigureLayer> AfterRim(bool powered)
        {
            foreach (var layer in _lines) yield return layer;
            if (!powered) yield break;
            foreach (var layer in _powered) yield return layer;
        }

        /// <summary>
        /// The same figure cut off below <paramref name="waist"/>: the seated
        /// pose, where the table hides the legs. The cut gets the ink line
        /// like any other edge.
        /// </summary>
        public FigureDrawing Cropped(float waist) => CroppedBy(0f, waist, 0f, -1f, waist);

        /// <summary>
        /// The figure cut along any line: everything on the side
        /// (<paramref name="nx"/>, <paramref name="ny"/>) points to, from the
        /// point (<paramref name="px"/>, <paramref name="py"/>), is removed.
        /// A rig part cut at the table line in its own, turned, rest space
        /// (OPERATOR_LOOKBOOK.md, LB5b). No rim is drawn below
        /// <paramref name="rimFloor"/>, as for <see cref="Cropped"/>.
        /// </summary>
        public FigureDrawing CroppedBy(float px, float py, float nx, float ny, float rimFloor)
        {
            var cut = FigureShape.Intersect(Silhouette, FigureShape.HalfPlane(px, py, nx, ny));
            var copy = new FigureDrawing(cut, Base, Ink, LineWeight);

            copy._blocks.AddRange(_blocks);
            copy._shades.AddRange(_shades);
            copy._lights.AddRange(_lights);
            copy._lines.AddRange(_lines);
            copy._powered.AddRange(_powered);
            copy.RimWidth = RimWidth;
            copy.RimColour = RimColour;
            copy.RimFloor = Math.Max(RimFloor, rimFloor);
            copy.RimEdges = RimEdges;
            return copy;
        }

        /// <summary>
        /// The same drawing reflected across x = 0, the light left where it
        /// was: every shape is mirrored, the rim's left and right swap, and
        /// the rasteriser then lights the result from the upper left as
        /// always. How a rig faces the other way without moving the key
        /// (OPERATOR_LOOKBOOK.md, LB5).
        /// </summary>
        public FigureDrawing Mirrored()
        {
            var copy = new FigureDrawing(Silhouette.Mirrored(), Base, Ink, LineWeight);

            foreach (var l in _blocks) copy._blocks.Add(new FigureLayer(l.Shape.Mirrored(), l.Colour, l.Blend));
            foreach (var l in _shades) copy._shades.Add(new FigureLayer(l.Shape.Mirrored(), l.Colour, l.Blend));
            foreach (var l in _lights) copy._lights.Add(new FigureLayer(l.Shape.Mirrored(), l.Colour, l.Blend));
            foreach (var l in _lines) copy._lines.Add(new FigureLayer(l.Shape.Mirrored(), l.Colour, l.Blend));
            foreach (var l in _powered) copy._powered.Add(new FigureLayer(l.Shape.Mirrored(), l.Colour, l.Blend));

            var edges = RimEdges & RimEdges.Top;
            if ((RimEdges & RimEdges.Left) != 0) edges |= RimEdges.Right;
            if ((RimEdges & RimEdges.Right) != 0) edges |= RimEdges.Left;

            copy.RimWidth = RimWidth;
            copy.RimColour = RimColour;
            copy.RimFloor = RimFloor;
            copy.RimEdges = edges;
            return copy;
        }

        private FigureDrawing Add(List<FigureLayer> list, FigureShape shape, FigureColour colour, FigureBlend blend)
        {
            list.Add(new FigureLayer(shape, colour, blend));
            return this;
        }
    }
}
