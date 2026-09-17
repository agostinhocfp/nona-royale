// Assets/_Project/Scripts/Unity/View/OperatorArtLibrary.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>Which figure an operator is drawn as.</summary>
    public enum FigurePose
    {
        Standing,
        Seated,
    }

    /// <summary>
    /// A rendered figure, with what the piece needs to fit and flash it.
    /// </summary>
    public sealed class FigureArt
    {
        public Sprite Sprite { get; }

        /// <summary>The same shape in solid white, for the hit flash. Null if the texture is not readable.</summary>
        public Sprite Silhouette { get; }

        /// <summary>Lowest opaque point, in sprite units from the pivot.</summary>
        public float OpaqueBottom { get; }

        /// <summary>Highest opaque point, in sprite units from the pivot.</summary>
        public float OpaqueTop { get; }

        public FigureArt(Sprite sprite, Sprite silhouette, float opaqueBottom, float opaqueTop)
        {
            Sprite = sprite;
            Silhouette = silhouette;
            OpaqueBottom = opaqueBottom;
            OpaqueTop = opaqueTop;
        }
    }

    /// <summary>
    /// Finds rendered operator art by name, and remembers what it found.
    /// </summary>
    /// <remarks>
    /// <b>A naming convention, not an asset per operator</b> (designer,
    /// 2026-09-17; ART_HOOKUP.md decision 3). Art lives under
    /// <c>Assets/_Project/Art/Resources/Art/Operators/</c> as
    /// <c>&lt;name&gt;_standing</c>, <c>&lt;name&gt;_seated</c> and
    /// <c>&lt;name&gt;_portrait</c>, where the name is the operator's,
    /// lowercased, with accents stripped (<c>Revú</c> → <c>revu</c>). Loading
    /// by name keeps the no-scene-wiring rule, and audio already works this
    /// way. A missing file is not an error: the caller falls back to the
    /// procedural figure or shape, pose by pose.
    ///
    /// <b>The importer makes the textures readable</b>
    /// (<c>Editor/OperatorArtImporter</c>), so this class can measure the
    /// opaque rows and build a white silhouette for the hit flash. If a
    /// texture is not readable, the figure still draws: it is fitted by its
    /// rectangle and it does not flash.
    /// </remarks>
    public static class OperatorArtLibrary
    {
        public const string Folder = "Art/Operators/";

        /// <summary>Alpha at or below this counts as empty when measuring a render.</summary>
        private const byte OpaqueThreshold = 8;

        private static readonly Dictionary<string, FigureArt> Figures = new Dictionary<string, FigureArt>();
        private static readonly Dictionary<string, Sprite> Portraits = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Warned = new HashSet<string>();

        /// <inheritdoc cref="OperatorArtNames.Key"/>
        public static string Key(string operatorName) => OperatorArtNames.Key(operatorName);

        /// <summary>The resource path for a pose, e.g. <c>Art/Operators/luka_standing</c>.</summary>
        public static string PathFor(string operatorName, FigurePose pose) =>
            Folder + Key(operatorName) + (pose == FigurePose.Seated ? "_seated" : "_standing");

        /// <summary>The rendered figure, or null when the operator has none for this pose.</summary>
        public static FigureArt Figure(string operatorName, FigurePose pose)
        {
            var path = PathFor(operatorName, pose);
            if (Figures.TryGetValue(path, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>(path);
            var art = sprite != null ? Measure(sprite, path) : null;
            Figures[path] = art;
            return art;
        }

        /// <summary>The portrait, or null. Callers fall back to <see cref="PieceShape"/>.</summary>
        public static Sprite Portrait(string operatorName)
        {
            var path = Folder + Key(operatorName) + "_portrait";
            if (Portraits.TryGetValue(path, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>(path);
            Portraits[path] = sprite;
            return sprite;
        }

        /// <summary>
        /// Uses <paramref name="sprite"/> for a pose as if it had been found on
        /// disk; null records "no art". For tests and tools.
        /// </summary>
        public static void Register(string operatorName, FigurePose pose, Sprite sprite)
        {
            var path = PathFor(operatorName, pose);
            Figures[path] = sprite != null ? Measure(sprite, path) : null;
        }

        /// <summary>Forgets every lookup, so the next one loads again. For tests and tools.</summary>
        public static void ClearCache()
        {
            Figures.Clear();
            Portraits.Clear();
            Warned.Clear();
        }

        private static FigureArt Measure(Sprite sprite, string path)
        {
            var rect = sprite.rect;
            float ppu = sprite.pixelsPerUnit;
            var pivot = sprite.pivot;   // pixels, from the rect's bottom-left

            var texture = sprite.texture;
            if (texture == null || !texture.isReadable || sprite.packed)
            {
                WarnOnce(path, "is not readable (or is packed in an atlas); fitted by its rectangle, with no hit flash. " +
                               "Tick Read/Write in its import settings.");
                return new FigureArt(sprite, null, -pivot.y / ppu, (rect.height - pivot.y) / ppu);
            }

            int x0 = Mathf.RoundToInt(rect.x);
            int y0 = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);

            Color32[] source;
            try
            {
                if (x0 == 0 && y0 == 0 && width == texture.width && height == texture.height)
                {
                    source = texture.GetPixels32();
                }
                else
                {
                    var block = texture.GetPixels(x0, y0, width, height);
                    source = new Color32[block.Length];
                    for (int i = 0; i < block.Length; i++) source[i] = block[i];
                }
            }
            catch (System.Exception e) when (e is UnityException || e is System.ArgumentException)
            {
                // Some compressed formats cannot be read back on some platforms.
                WarnOnce(path, "could not be read back (compressed format?); fitted by its rectangle, with no hit flash.");
                return new FigureArt(sprite, null, -pivot.y / ppu, (rect.height - pivot.y) / ppu);
            }

            int lowest = -1;
            int highest = -1;
            var white = new Color32[source.Length];

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                bool any = false;

                for (int x = 0; x < width; x++)
                {
                    byte alpha = source[row + x].a;
                    white[row + x] = new Color32(255, 255, 255, alpha);
                    if (alpha > OpaqueThreshold) any = true;
                }

                if (!any) continue;
                if (lowest < 0) lowest = y;
                highest = y;
            }

            if (lowest < 0)
            {
                WarnOnce(path, "is fully transparent; ignored.");
                return null;
            }

            var mask = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = sprite.name + "_silhouette",
                filterMode = texture.filterMode,
                wrapMode = TextureWrapMode.Clamp,
            };
            mask.SetPixels32(white);
            mask.Apply(false, true);

            var silhouette = Sprite.Create(mask, new Rect(0f, 0f, width, height),
                new Vector2(pivot.x / width, pivot.y / height), ppu, 0, SpriteMeshType.FullRect);
            silhouette.name = mask.name;

            return new FigureArt(sprite, silhouette,
                (lowest - pivot.y) / ppu,
                (highest + 1 - pivot.y) / ppu);
        }

        private static void WarnOnce(string path, string message)
        {
            if (!Warned.Add(path)) return;
            Debug.LogWarning($"[OperatorArt] Resources/{path} {message}");
        }
    }
}