// Assets/_Project/Scripts/Unity/View/Figures/FigureDrawing.cs
using System;
using System.Collections.Generic;
using System.Globalization;

namespace NonaRoyale.Unity.View
{
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
        public FigureColour Ink { get; }

        /// <summary>The one line weight, in figure units (§2.2 rule 2).</summary>
        public float LineWeight { get; }

        public float RimWidth { get; private set; }
        public FigureColour RimColour { get; private set; }

        /// <summary>No rim below this height: the rim is a light on the body, not on the shoes.</summary>
        public float RimFloor { get; private set; }

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

        public FigureDrawing Rim(float width, FigureColour colour, float floor)
        {
            RimWidth = Math.Max(0f, width);
            RimColour = colour;
            RimFloor = floor;
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
        public FigureDrawing Cropped(float waist)
        {
            var cut = FigureShape.Intersect(Silhouette, FigureShape.HalfPlane(0f, waist, 0f, -1f));
            var copy = new FigureDrawing(cut, Base, Ink, LineWeight);

            copy._blocks.AddRange(_blocks);
            copy._shades.AddRange(_shades);
            copy._lights.AddRange(_lights);
            copy._lines.AddRange(_lines);
            copy._powered.AddRange(_powered);
            copy.RimWidth = RimWidth;
            copy.RimColour = RimColour;
            copy.RimFloor = Math.Max(RimFloor, waist);
            return copy;
        }

        private FigureDrawing Add(List<FigureLayer> list, FigureShape shape, FigureColour colour, FigureBlend blend)
        {
            list.Add(new FigureLayer(shape, colour, blend));
            return this;
        }
    }
}
