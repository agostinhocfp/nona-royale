// Assets/_Project/Scripts/Editor/OperatorArtImporter.cs
using System;
using UnityEditor;
using UnityEngine;

namespace NonaRoyale.EditorTools
{
    /// <summary>
    /// Sets the import settings for operator art the first time a file lands
    /// in <c>Art/Resources/Art/Operators/</c> (ART_PIPELINE.md §4 and §9).
    /// </summary>
    /// <remarks>
    /// <b>First import only.</b> A file that already has import settings is
    /// left alone, so a change made in the Inspector survives a re-import.
    /// To re-apply these defaults, delete the file's <c>.meta</c> and let
    /// Unity import it again.
    ///
    /// <b>Why each setting:</b>
    /// <list type="bullet">
    /// <item>Sprite, single, bottom-centre pivot: renders are standing or
    /// seated figures; the feet are the natural anchor. The game fits by the
    /// opaque rows anyway, so padding and pivot cannot misplace a figure.</item>
    /// <item>Read/Write on, uncompressed: <c>OperatorArtLibrary</c> reads the
    /// pixels once to measure the figure and to build the white hit-flash
    /// silhouette. Compressed formats cannot always be read back. The cost is
    /// about 1.2 MB per 512×768 render, twice (CPU and GPU copies). Revisit if
    /// all operators ship with three images each.</item>
    /// <item>Mipmaps on, bilinear, clamp. A 768-pixel render is drawn about
    /// 90 to 180 pixels tall (1080p to 4K), and shrinking that far without
    /// mipmaps shimmers, worst while the figure breathes and hops. (Off until
    /// 2026-09-17; files imported before then need Generate Mipmaps ticked.)</item>
    /// <item>Pixels per unit 512: arbitrary, since the game scales by height;
    /// 512 makes a 512-wide render one unit wide in the Scene view.</item>
    /// </list>
    /// </remarks>
    public sealed class OperatorArtImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/_Project/Art/Resources/Art/Operators/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, StringComparison.OrdinalIgnoreCase)) return;
            if (!assetImporter.importSettingsMissing) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512f;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            Debug.Log($"[OperatorArt] Import settings applied to {assetPath}");
        }
    }
}