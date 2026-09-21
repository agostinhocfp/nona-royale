// Assets/_Project/Scripts/Unity/View/Figures/FigureSprites.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The one place a rendered look-book figure becomes Unity objects
    /// (OPERATOR_LOOKBOOK.md, LB0). Everything upstream is plain C#.
    /// </summary>
    /// <remarks>
    /// <b>It hands back a <see cref="FigureArt"/></b>, the same type a real
    /// render becomes, so <see cref="OperatorPiece"/> treats a look-book
    /// figure exactly as it treats Luka's render: fitted by
    /// <see cref="FigureLayout.Fit"/>, drawn untinted over the seat disc and
    /// ring, flashed through a white silhouette. That is LB1, for free.
    ///
    /// <b>No measuring pass.</b> <c>OperatorArtLibrary.Measure</c> reads
    /// pixels back to find the opaque rows; the rasteriser already knows
    /// them, so the textures are uploaded and released from CPU memory.
    ///
    /// <b>Straight alpha is safe here.</b> Transparent texels are black, and
    /// bilinear filtering and mipmaps blend an edge toward black. Every
    /// figure's outermost ring is the ink, which is nearly black already,
    /// so there is no fringe to see.
    /// </remarks>
    public static class FigureSprites
    {
        /// <summary>The figure as a sprite with its white silhouette, or null for an empty image.</summary>
        public static FigureArt ToFigureArt(FigureImage image, string name)
        {
            if (image == null || image.IsEmpty) return null;

            var canvas = image.Canvas;
            float ppu = canvas.PixelsPerUnit;

            // The figure's own origin, the feet on the centre line, is the pivot.
            float pivotX = -canvas.Left * ppu;
            float pivotY = -canvas.Bottom * ppu;
            var pivot = new Vector2(pivotX / image.Width, pivotY / image.Height);

            var sprite = Upload(image.Pixels, image.Width, image.Height, pivot, ppu, name);

            var white = new byte[image.Pixels.Length];
            for (int i = 0; i < white.Length; i += 4)
            {
                white[i] = 255;
                white[i + 1] = 255;
                white[i + 2] = 255;
                white[i + 3] = image.Pixels[i + 3];
            }

            var silhouette = Upload(white, image.Width, image.Height, pivot, ppu, name + "_silhouette");

            return new FigureArt(sprite, silhouette,
                (image.OpaqueBottomRow - pivotY) / ppu,
                (image.OpaqueTopRow + 1 - pivotY) / ppu);
        }

        private static Sprite Upload(byte[] rgba, int width, int height, Vector2 pivot, float ppu, string name)
        {
            // Mipmapped: at board scale the figure is drawn far smaller than
            // it is rendered, and a figure that shimmers as it walks reads as
            // noise.
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            texture.SetPixelData(rgba, 0);
            texture.Apply(true, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, ppu, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
    }
}
