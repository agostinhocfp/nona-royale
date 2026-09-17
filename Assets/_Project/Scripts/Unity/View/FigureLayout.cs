// Assets/_Project/Scripts/Unity/View/FigureLayout.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Where an operator's figure and its marks go, in figure units: the
    /// piece's local space before its scale. Plain arithmetic, no Unity
    /// types, so it is tested outside the editor.
    /// </summary>
    /// <remarks>
    /// <b>The procedural figures fix the frame</b> (ART_HOOKUP.md, ART1). The
    /// piece's origin stays the centre of the old 1×1 figure sprite, because
    /// the floaters, sounds, hit testing and the health label all read
    /// <c>transform.position</c> as "the operator". A rendered figure is fitted
    /// into that frame: its lowest opaque row sits on the procedural figure's
    /// feet, and it is scaled to the procedural figure's height. So a render
    /// of any size, with any padding and any pivot, lands where the pawn
    /// stood, and "bigger means tougher" still comes from the piece's scale.
    ///
    /// The pin, halo and bar are then placed from the fitted figure's top and
    /// height, not from the procedural constants.
    /// </remarks>
    public readonly struct FigureLayout
    {
        /// <summary>The standing pawn: feet, top of head, head centre, pin, bar (BoardArt).</summary>
        public static readonly FigureLayout Pawn = new FigureLayout(1f, 0f, -0.465f, 0.425f, 0.30f, -0.14f, 0.6f);

        /// <summary>The seated bust. Its bar is never shown; the value is the old one.</summary>
        public static readonly FigureLayout Bust = new FigureLayout(1f, 0f, -0.38f, 0.33f, 0.16f, -0.2f, 0.6f);

        /// <summary>Gap between the top of the head and the health bar, as the pawn has it.</summary>
        public const float BarGap = 0.175f;

        /// <summary>Drop from the top of the head to the halo's centre, as the pawn has it.</summary>
        public const float HeadDrop = 0.125f;

        /// <summary>Chest height as a fraction of a standing render's height (6-head figure).</summary>
        public const float StandingChest = 0.70f;

        /// <summary>Chest height as a fraction of a seated render's height.</summary>
        public const float SeatedChest = 0.62f;

        /// <summary>Scale for the art child (1 for procedural figures).</summary>
        public readonly float ArtScale;

        /// <summary>Local y of the art child, where the sprite's pivot goes.</summary>
        public readonly float ArtY;

        public readonly float Feet;
        public readonly float Top;
        public readonly float HeadY;
        public readonly float PinY;
        public readonly float BarY;

        public float Height => Top - Feet;

        public FigureLayout(float artScale, float artY, float feet, float top, float headY, float pinY, float barY)
        {
            ArtScale = artScale;
            ArtY = artY;
            Feet = feet;
            Top = top;
            HeadY = headY;
            PinY = pinY;
            BarY = barY;
        }

        /// <summary>
        /// Fits a render into <paramref name="frame"/>'s feet and height.
        /// </summary>
        /// <param name="opaqueBottom">Lowest opaque point of the sprite, in its own units, measured from its pivot.</param>
        /// <param name="opaqueTop">Highest opaque point, same units.</param>
        /// <param name="frame">The procedural figure the render replaces.</param>
        /// <param name="heightScale">A tuning factor on the fitted height (1 = the procedural height).</param>
        /// <param name="chest">Chest height as a fraction of the fitted height.</param>
        public static FigureLayout Fit(float opaqueBottom, float opaqueTop, FigureLayout frame, float heightScale, float chest)
        {
            float spriteHeight = opaqueTop - opaqueBottom;
            if (spriteHeight <= 0f) return frame;

            float height = frame.Height * (heightScale > 0f ? heightScale : 1f);
            float scale = height / spriteHeight;
            float feet = frame.Feet;
            float top = feet + height;

            return new FigureLayout(
                artScale: scale,
                artY: feet - opaqueBottom * scale,
                feet: feet,
                top: top,
                headY: top - HeadDrop,
                pinY: feet + height * chest,
                barY: top + BarGap);
        }

        /// <summary>
        /// Where the transform goes so the feet stay on the floor while the
        /// figure is squashed or stretched about its centre by
        /// <paramref name="scaleY"/>. Returns the vertical offset, in world
        /// units, for a piece drawn at <paramref name="worldScale"/>.
        /// </summary>
        public float FootAnchorOffset(float worldScale, float scaleY) => Feet * worldScale * (1f - scaleY);
    }
}