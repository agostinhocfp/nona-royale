// Assets/_Project/Scripts/Unity/View/BoardTextures.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Painted, tileable board textures, found by name (GUI increment G4).
    /// </summary>
    /// <remarks>
    /// <b>Optional, like operator art.</b> Textures live under
    /// <c>Assets/_Project/Art/Resources/Art/Board/</c> as
    /// <c>board_marble</c>, <c>board_felt</c> and <c>board_carpet</c>. A
    /// missing file is not an error: the board keeps its procedural surface
    /// for that material (<see cref="BoardArt"/>). The editor's
    /// <c>BoardTextureImporter</c> sets the import settings the first time a
    /// file lands there.
    ///
    /// <b>What each one becomes.</b>
    /// <list type="bullet">
    /// <item><b>Marble</b> fills the cross, in its own colours, one tile per
    /// <see cref="MarbleTileCells"/> cells. Paint it as dark as the floor
    /// should look; the veining is the texture's.</item>
    /// <item><b>Felt</b> is read as brightness only, so the seat colour
    /// still tints it. Its average brightness is divided out, so a light or a
    /// dark felt tints the same; only its grain shows.</item>
    /// <item><b>Carpet</b> covers the square table around the cross, tiled
    /// every <see cref="CarpetTileCells"/> cells and dimmed by
    /// <see cref="UiTheme.CarpetTint"/>.</item>
    /// </list>
    ///
    /// <b>Marble and felt are read on the CPU</b>, baked into the board's
    /// sprites once per board, so they need Read/Write on. A texture that
    /// can't be read is skipped with one warning.
    /// </remarks>
    public static class BoardTextures
    {
        public const string Folder = "Art/Board/";
        public const string MarbleName = "board_marble";
        public const string FeltName = "board_felt";
        public const string CarpetName = "board_carpet";

        /// <summary>Cells one marble tile spans on the cross.</summary>
        public const float MarbleTileCells = 4f;

        /// <summary>How many felt tiles span a table's diameter.</summary>
        public const float FeltRepeats = 2f;

        /// <summary>Cells one carpet tile spans on the table.</summary>
        public const float CarpetTileCells = 2f;

        private static readonly Dictionary<string, TileSampler> Samplers = new Dictionary<string, TileSampler>();
        private static readonly HashSet<string> Looked = new HashSet<string>();
        private static Sprite _carpet;
        private static bool _carpetLooked;

        /// <summary>The marble, ready to sample, or null.</summary>
        public static TileSampler Marble => Sampler(MarbleName);

        /// <summary>The felt, ready to sample, or null.</summary>
        public static TileSampler Felt => Sampler(FeltName);

        /// <summary>
        /// The carpet as a sprite for tiled drawing, one tile per
        /// <see cref="CarpetTileCells"/> local units, or null.
        /// </summary>
        public static Sprite Carpet
        {
            get
            {
                if (_carpetLooked) return _carpet;
                _carpetLooked = true;

                var texture = Resources.Load<Texture2D>(Folder + CarpetName);
                if (texture == null) return null;

                texture.wrapMode = TextureWrapMode.Repeat;
                _carpet = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), texture.width / CarpetTileCells, 0, SpriteMeshType.FullRect);
                _carpet.name = CarpetName;
                return _carpet;
            }
        }

        /// <summary>Forgets every lookup, so the next board loads again. For tests and tools.</summary>
        public static void ClearCache()
        {
            Samplers.Clear();
            Looked.Clear();
            _carpet = null;
            _carpetLooked = false;
        }

        private static TileSampler Sampler(string name)
        {
            if (Looked.Contains(name))
                return Samplers.TryGetValue(name, out var cached) ? cached : null;

            Looked.Add(name);

            var texture = Resources.Load<Texture2D>(Folder + name);
            if (texture == null) return null;

            if (!texture.isReadable)
            {
                Debug.LogWarning($"[BoardTextures] {Folder}{name} is not readable, so the procedural surface stays. " +
                                 "Turn on Read/Write in its import settings (BoardTextureImporter does this for new files).");
                return null;
            }

            var sampler = new TileSampler(texture.GetPixels32(), texture.width, texture.height);
            Samplers[name] = sampler;
            return sampler;
        }
    }

    /// <summary>
    /// A tileable image held as pixels, sampled with wrapping and bilinear
    /// filtering. The pixels are read once; sampling them directly is much
    /// faster than a <c>GetPixelBilinear</c> call per texel of a whole cross.
    /// </summary>
    public sealed class TileSampler
    {
        private readonly Color32[] _pixels;
        private readonly int _width;
        private readonly int _height;

        /// <summary>The image's average brightness, 0 to 1.</summary>
        public float MeanLuminance { get; }

        public TileSampler(Color32[] pixels, int width, int height)
        {
            _pixels = pixels;
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);

            double sum = 0;
            foreach (var p in pixels) sum += Luminance(p);
            MeanLuminance = pixels.Length > 0 ? (float)(sum / pixels.Length) : 0f;
        }

        /// <summary>The colour at (u, v), in tiles: 1 is one tile, and it wraps.</summary>
        public Color Sample(float u, float v)
        {
            float x = Wrap(u) * _width - 0.5f;
            float y = Wrap(v) * _height - 0.5f;

            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;

            Color c00 = At(x0, y0), c10 = At(x0 + 1, y0);
            Color c01 = At(x0, y0 + 1), c11 = At(x0 + 1, y0 + 1);

            return Color.Lerp(Color.Lerp(c00, c10, fx), Color.Lerp(c01, c11, fx), fy);
        }

        /// <summary>Brightness at (u, v), 0 to 1.</summary>
        public float Brightness(float u, float v)
        {
            var c = Sample(u, v);
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        private Color At(int x, int y)
        {
            x = ((x % _width) + _width) % _width;
            y = ((y % _height) + _height) % _height;
            return _pixels[y * _width + x];
        }

        private static float Wrap(float t) => t - Mathf.Floor(t);

        private static float Luminance(Color32 p) => (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255f;
    }
}