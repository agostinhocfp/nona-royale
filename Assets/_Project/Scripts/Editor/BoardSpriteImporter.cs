// Assets/_Project/Scripts/Editor/BoardSpriteImporter.cs
using System;
using System.IO;
using NonaRoyale.Unity.View;
using UnityEditor;
using UnityEngine;

namespace NonaRoyale.EditorTools
{
    /// <summary>
    /// Sets the import settings for painted board sprites the first time a
    /// file lands in <c>Art/Resources/Art/Board/</c> (board skin BS1).
    /// </summary>
    /// <remarks>
    /// <b>Only the contract's slots</b> (<see cref="BoardSprites.Slots"/>).
    /// The three tileable textures belong to <see cref="BoardTextureImporter"/>;
    /// any other name is reported as a likely typo and left alone.
    ///
    /// <b>First import only</b>, like the other importers: an Inspector
    /// change survives a re-import, and deleting the <c>.meta</c> re-applies
    /// these. The one exception is pixels-per-unit, which is set on every
    /// import, because it is the contract ("one unit across"), not a taste.
    ///
    /// <b>Why each setting:</b>
    /// <list type="bullet">
    /// <item>Sprite, single, centre pivot, full-rect mesh: the code places,
    /// rotates and scales every slot about its centre, and the shader pack's
    /// effects work on the quad.</item>
    /// <item>Pixels per unit = the image's width: one unit across.
    /// <c>BoardSprites.UnitScale</c> reads the bounds anyway, so a wrong size
    /// can't misplace anything.</item>
    /// <item>No Read/Write: nothing reads these on the CPU (unlike marble and
    /// felt), so they keep one copy, on the GPU.</item>
    /// <item>Mipmaps, bilinear, anisotropic 4: a 1024 table top is drawn a
    /// few hundred pixels wide, and the tilted camera sees the board at a
    /// slant, where mipmaps alone blur.</item>
    /// <item>Clamp: a sprite never repeats, and clamp keeps its edge from
    /// bleeding in the opposite edge's pixels.</item>
    /// <item>High-quality compression, max 2048: gilt gradients band under
    /// the default quality; the largest slot is 1024.</item>
    /// </list>
    /// </remarks>
    public sealed class BoardSpriteImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/_Project/Art/Resources/Art/Board/";

        /// <summary>True for a file directly inside the board folder (not a subfolder).</summary>
        public static bool InBoardFolder(string assetPath)
        {
            var dir = Path.GetDirectoryName(assetPath);
            if (dir == null) return false;
            return string.Equals(dir.Replace('\\', '/') + "/", Folder, StringComparison.OrdinalIgnoreCase);
        }

        private void OnPreprocessTexture()
        {
            if (!InBoardFolder(assetPath)) return;

            var name = Path.GetFileNameWithoutExtension(assetPath);
            if (BoardTextureImporter.Owns(name)) return;

            if (!BoardSprites.TryGetSlot(name, out var slot))
            {
                Debug.LogWarning($"[BoardSprites] {assetPath} is not a contract slot, so nothing loads it. " +
                                 "Check the name against BoardSprites.Slots (HANDOFF_board_assets.md §1).");
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);

            if (width != height || width != slot.SizePx)
                Debug.LogWarning($"[BoardSprites] {slot.Name} is {width}×{height}; the contract asks for " +
                                 $"{slot.SizePx}×{slot.SizePx}. It still loads and is scaled to its slot.");

            if (width > 0) importer.spritePixelsPerUnit = width;

            if (!importer.importSettingsMissing) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            Debug.Log($"[BoardSprites] Import settings applied to {assetPath}" + (slot.Tinted ? " (tinted slot: paint it grey)" : ""));
        }
    }
}
