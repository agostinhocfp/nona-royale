// Assets/_Project/Scripts/Unity/View/Figures/OperatorLook.cs
using System;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// One operator's recipe (OPERATOR_LOOKBOOK.md, LB2): how to draw the
    /// standing figure, and where the table cuts it for the seated one.
    /// </summary>
    /// <remarks>
    /// Plain C#: a recipe is a pure function of the palette, so it can be
    /// drawn on a worker thread, in a test and in the judging tools with the
    /// same result. Each operator's recipe lives in <c>Looks/</c> and is listed
    /// in <see cref="LookRoster"/>.
    /// </remarks>
    public sealed class OperatorLook
    {
        private readonly Func<LookBookPalette, FigureDrawing> _draw;

        /// <summary>The operator's name as the roster spells it (<c>Revú</c>, not <c>revu</c>).</summary>
        public string Name { get; }

        /// <summary>The art-file stem, as <see cref="OperatorArtNames.Key"/> makes it.</summary>
        public string Key { get; }

        /// <summary>Where the seated figure is cut, in figure units: the table hides everything below.</summary>
        public float Waist { get; }

        public OperatorLook(string name, float waist, Func<LookBookPalette, FigureDrawing> draw)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Key = OperatorArtNames.Key(name);
            Waist = waist;
            _draw = draw ?? throw new ArgumentNullException(nameof(draw));
        }

        public FigureDrawing Standing(LookBookPalette palette) => _draw(palette);

        public FigureDrawing Seated(LookBookPalette palette) => _draw(palette).Cropped(Waist);

        public FigureDrawing Draw(LookBookPalette palette, bool seated) => seated ? Seated(palette) : Standing(palette);

        public override string ToString() => Name;
    }
}
