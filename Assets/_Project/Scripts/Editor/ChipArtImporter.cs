// Assets/_Project/Scripts/Editor/ChipArtImporter.cs
using System;
using System.IO;
using NonaRoyale.Unity.View;
using UnityEditor;
using UnityEngine;

namespace NonaRoyale.EditorTools
{
    /// <summary>
    /// Imports chip portraits dropped into <c>Art/Resources/Art/Chips/</c>
    /// (chip pieces, 2026-09-29), and cuts each one to a circle; and the hero
    /// frame in <c>Art/Resources/Art/Hero/</c>, with the same settings and no cut.
    /// </summary>
    /// <remarks>
    /// <b>The generator's output goes in as it is.</b> A chip portrait comes
    /// out square, with black outside its painted circle. Rather than ask
    /// for a cleaning step, the import keeps the circle of
    /// <see cref="ChipArtLibrary.MaskRadius"/> and makes the rest
    /// transparent, on every mip level, with a one-texel soft edge. The gilt
    /// ring drawn over the chip covers that edge. This runs on every import,
    /// so a replaced file is cut the same way.
    ///
    /// <b>A file with no alpha channel still gets one</b> (fixed 2026-09-29).
    /// The generator saves RGB, and a texture imported from RGB has no alpha
    /// to cut into: the first chips kept their black corners. Such a file is
    /// imported with its alpha taken from its grey values, which forces a
    /// format with alpha, and the cut then writes the alpha outright.
    ///
    /// <b>Settings, first import only</b>, like the other importers (an
    /// Inspector change survives a re-import; deleting the <c>.meta</c>
    /// re-applies them), except pixels-per-unit, which is set every time
    /// because it is the contract: one unit across, whatever the size.
    /// <list type="bullet">
    /// <item>Sprite, single, centre pivot, full rect: <see cref="ChipView"/>
    /// scales the face about its centre.</item>
    /// <item>Max 512: a chip is drawn well under 200 pixels across even at
    /// 4K, and the generator's 1254-pixel files would otherwise cost six
    /// times the memory for nothing.</item>
    /// <item>Mipmaps, trilinear: the chip moves and shrinks a long way.</item>
    /// <item>High-quality compression, no Read/Write: nothing reads the
    /// pixels at run time; the cut happens here.</item>
    /// </list>
    /// </remarks>
    public sealed class ChipArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/_Project/Art/Resources/Art/Chips/";
        public const string HeroFolder = "Assets/_Project/Art/Resources/Art/Hero/";

        /// <summary>True for a file directly inside the chips folder (not a subfolder).</summary>
        public static bool InChipFolder(string assetPath) => InFolder(assetPath, Folder);

        /// <summary>True for a file directly inside the hero frame's folder.</summary>
        public static bool InHeroFolder(string assetPath) => InFolder(assetPath, HeroFolder);

        private static bool InFolder(string assetPath, string folder)
        {
            var dir = Path.GetDirectoryName(assetPath);
            if (dir == null) return false;
            return string.Equals(dir.Replace('\\', '/') + "/", folder, StringComparison.OrdinalIgnoreCase);
        }

        private void OnPreprocessTexture()
        {
            bool chip = InChipFolder(assetPath);
            if (!chip && !InHeroFolder(assetPath)) return;

            var name = Path.GetFileNameWithoutExtension(assetPath);
            if (chip && !name.EndsWith("_chip_unlit", StringComparison.Ordinal) && !name.EndsWith("_chip_lit", StringComparison.Ordinal))
                Debug.LogWarning($"[ChipArt] {assetPath} is not named <operator>_chip_unlit or <operator>_chip_lit, so nothing loads it.");

            var importer = (TextureImporter)assetImporter;

            // An RGB file has no alpha to cut into; take it from grey so the
            // format keeps one, and the cut below writes it outright.
            if (chip)
                importer.alphaSource = importer.DoesSourceTextureHaveAlpha()
                    ? TextureImporterAlphaSource.FromInput
                    : TextureImporterAlphaSource.FromGrayScale;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != height)
                Debug.LogWarning($"[ChipArt] {assetPath} is {width}×{height}; chip portraits are square. The cut is centred on its width.");

            if (!importer.importSettingsMissing)
            {
                importer.spritePixelsPerUnit = Mathf.Min(width, importer.maxTextureSize);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = chip ? 512 : 1024;
            if (!chip) importer.alphaSource = TextureImporterAlphaSource.FromInput;

            // One unit across at the imported size, which the cap may have shrunk.
            importer.spritePixelsPerUnit = Mathf.Min(width, importer.maxTextureSize);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            Debug.Log($"[ChipArt] Import settings applied to {assetPath}");
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!InChipFolder(assetPath)) return;

            // Grey-derived alpha is not transparency: overwrite it rather than multiply.
            bool replace = ((TextureImporter)assetImporter).alphaSource == TextureImporterAlphaSource.FromGrayScale;

            for (int level = 0; level < texture.mipmapCount; level++)
            {
                int width = Mathf.Max(1, texture.width >> level);
                int height = Mathf.Max(1, texture.height >> level);

                var pixels = texture.GetPixels(level);
                Cut(pixels, width, height, replace);
                texture.SetPixels(pixels, level);
            }
        }

        /// <summary>
        /// Keeps the centred circle of <see cref="ChipArtLibrary.MaskRadius"/>
        /// (a fraction of the width) and fades everything outside it to clear,
        /// over one texel.
        /// </summary>
        /// <param name="replace">Write the circle as the alpha, ignoring what the texture had.</param>
        internal static void Cut(Color[] pixels, int width, int height, bool replace)
        {
            float cx = width * 0.5f;
            float cy = height * 0.5f;
            float radius = ChipArtLibrary.MaskRadius * width;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float coverage = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);

                int i = y * width + x;
                pixels[i].a = replace ? coverage : pixels[i].a * coverage;
            }
        }
    }
}
