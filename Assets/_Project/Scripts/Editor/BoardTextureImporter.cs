// Assets/_Project/Scripts/Editor/BoardTextureImporter.cs
using System;
using UnityEditor;
using UnityEngine;

namespace NonaRoyale.EditorTools
{
    /// <summary>
    /// Sets the import settings for painted board textures the first time a
    /// file lands in <c>Art/Resources/Art/Board/</c> (GUI increment G4).
    /// </summary>
    /// <remarks>
    /// <b>First import only</b>, like <see cref="OperatorArtImporter"/>: an
    /// Inspector change survives a re-import. Delete the <c>.meta</c> to
    /// re-apply these.
    ///
    /// <b>Why each setting:</b>
    /// <list type="bullet">
    /// <item>Default texture, not a sprite: the game makes the sprite it needs
    /// (<c>BoardTextures</c>).</item>
    /// <item>Read/Write on, uncompressed: marble and felt are baked into the
    /// board's sprites from the pixels.</item>
    /// <item>Repeat wrap and mipmaps: the carpet is drawn tiled, and a tiled
    /// texture seen smaller than its size shimmers without mipmaps.</item>
    /// <item>Max 1024: a tile a few cells across never needs more.</item>
    /// </list>
    /// </remarks>
    public sealed class BoardTextureImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/_Project/Art/Resources/Art/Board/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, StringComparison.OrdinalIgnoreCase)) return;
            if (!assetImporter.importSettingsMissing) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;

            Debug.Log($"[BoardTextures] Import settings applied to {assetPath}");
        }
    }
}