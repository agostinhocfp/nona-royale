// Assets/_Project/Scripts/Unity/View/Figures/Rig/OperatorRigArt.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>One part's sprites for one facing, and where its sprite sits against its joint.</summary>
    public sealed class RigPartSprites
    {
        public RigPart Part { get; }
        public int BoneIndex { get; }

        public Sprite Standing { get; }
        public Sprite StandingSilhouette { get; }

        /// <summary>The sprite's lower-left corner against the bone's rest pivot, in figure units.</summary>
        public Vector2 StandingOffset { get; }

        /// <summary>Null when the seated pose does not draw the part.</summary>
        public Sprite Seated { get; }
        public Sprite SeatedSilhouette { get; }
        public Vector2 SeatedOffset { get; }

        /// <summary>The standing sprite with the cyan tell lit (LB5c); null for a part with none. Same size and offset as <see cref="Standing"/>.</summary>
        public Sprite Powered { get; }

        public RigPartSprites(RigPart part, int boneIndex,
            Sprite standing, Sprite standingSilhouette, Vector2 standingOffset,
            Sprite seated, Sprite seatedSilhouette, Vector2 seatedOffset,
            Sprite powered = null)
        {
            Powered = powered;
            Part = part;
            BoneIndex = boneIndex;
            Standing = standing;
            StandingSilhouette = standingSilhouette;
            StandingOffset = standingOffset;
            Seated = seated;
            SeatedSilhouette = seatedSilhouette;
            SeatedOffset = seatedOffset;
        }
    }

    /// <summary>One facing of a rig on the board: the rig, its parts' sprites back to front, its crouch.</summary>
    public sealed class RigFacingArt
    {
        public OperatorRig Rig { get; }
        public IReadOnlyList<RigPartSprites> Parts { get; }
        public RigPose Crouch { get; }

        public RigFacingArt(OperatorRig rig, IReadOnlyList<RigPartSprites> parts, RigPose crouch)
        {
            Rig = rig;
            Parts = parts;
            Crouch = crouch;
        }
    }

    /// <summary>A rig's sprites, both facings, and the extents the piece fits its layout to.</summary>
    public sealed class RigArt
    {
        public RigFacingArt Right { get; }
        public RigFacingArt Left { get; }
        public float RestBottom { get; }
        public float RestTop { get; }
        public float SeatedBottom { get; }
        public float SeatedTop { get; }

        public RigArt(RigFacingArt right, RigFacingArt left, float restBottom, float restTop, float seatedBottom, float seatedTop)
        {
            Right = right;
            Left = left;
            RestBottom = restBottom;
            RestTop = restTop;
            SeatedBottom = seatedBottom;
            SeatedTop = seatedTop;
        }

        public RigFacingArt Facing(bool left) => left ? Left : Right;

        /// <summary>False once a domain reload or a cache clear has destroyed the textures.</summary>
        public bool IsAlive
        {
            get
            {
                foreach (var part in Right.Parts)
                    if (part.Standing == null) return false;
                return true;
            }
        }
    }

    /// <summary>
    /// Rigged figures for the board (OPERATOR_LOOKBOOK.md, LB5b): a rig's
    /// parts as sprites, both facings and the seated cut, built on the thread
    /// pool like the look book and uploaded on first use.
    /// </summary>
    /// <remarks>
    /// <b>The fallback order</b> is a real render, then a rig, then a look-book
    /// figure, then the pawn and bust. <see cref="OperatorPiece"/> asks for a
    /// render first and only comes here without one.
    ///
    /// Sprites are pivoted at their lower-left corner; <c>RigView</c> places
    /// each at its offset from the joint, so a part turns about its joint
    /// without a pivot outside the sprite.
    /// </remarks>
    public static class OperatorRigArt
    {
        /// <summary>The secondary texture URP's lit sprite shader reads as the normal map.</summary>
        public const string NormalMapName = "_NormalMap";

        /// <summary>Off draws the look-book figure instead, for a side-by-side check and for the look book's own tests.</summary>
        public static bool Enabled { get; set; } = true;

        private static readonly Dictionary<string, RigArt> Built = new Dictionary<string, RigArt>();
        private static readonly Dictionary<string, Task<RigImages>> Pending = new Dictionary<string, Task<RigImages>>();
        private static readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();

        /// <summary>Whether the operator draws as a rig on the board.</summary>
        public static bool Has(string operatorName) => Enabled && RigRoster.Find(operatorName) != null;

        /// <summary>The rig's sprites, built on first use; null with no rig, or with the rig switched off.</summary>
        public static RigArt For(string operatorName)
        {
            if (!Enabled) return null;

            var rig = RigRoster.Find(operatorName);
            if (rig == null) return null;

            if (Built.TryGetValue(rig.Key, out var cached) && (cached == null || cached.IsAlive)) return cached;

            RigImages images = null;
            try
            {
                if (Pending.TryGetValue(rig.Key, out var task))
                {
                    Pending.Remove(rig.Key);
                    images = task.Result;   // waits if it is still rendering
                }
                else
                {
                    images = RigImages.Build(rig, OperatorLookBook.Palette);
                }
            }
            catch (Exception e)
            {
                // A broken rig must never break a piece: it draws the look-book figure.
                Debug.LogError($"[Rig] {rig.Key} failed to render; drawing the look-book figure. {e.GetBaseException()}");
                images = null;
            }

            RigArt art = null;
            if (images != null)
            {
                art = Upload(images);
                Debug.Log($"[Rig] {rig.Key}: {images.Milliseconds:0} ms, {images.Right.Parts.Count} parts × 2 facings");
            }

            Built[rig.Key] = art;
            return art;
        }

        /// <summary>Starts rasterising these operators' rigs on the thread pool. Operators with no rig are skipped.</summary>
        public static void Prewarm(IEnumerable<string> operatorNames)
        {
            if (!Enabled || operatorNames == null) return;
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;

            // The palette reads UiTheme, so it is built here; the rest is plain C#.
            var palette = OperatorLookBook.Palette;

            foreach (var name in operatorNames)
            {
                var rig = RigRoster.Find(name);
                if (rig == null || Built.ContainsKey(rig.Key) || Pending.ContainsKey(rig.Key)) continue;

                // A real render wins in both poses, so there is nothing to warm.
                if (OperatorArtLibrary.Rendered(name, FigurePose.Standing) != null &&
                    OperatorArtLibrary.Rendered(name, FigurePose.Seated) != null) continue;

                Pending[rig.Key] = Task.Run(() => RigImages.Build(rig, palette));
            }
        }

        /// <summary>Forgets every rig, so the next lookup builds again. For tests and tools.</summary>
        public static void ClearCache()
        {
            foreach (var owned in Owned)
            {
                if (owned == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(owned);
                else UnityEngine.Object.DestroyImmediate(owned);
            }

            Owned.Clear();
            Built.Clear();
            Pending.Clear();
        }

        private static RigArt Upload(RigImages images)
        {
            var cache = new Dictionary<FigureImage, (Sprite sprite, Sprite silhouette)>();
            return new RigArt(
                Upload(images.Right, images.Key + "_r", cache),
                Upload(images.Left, images.Key + "_l", cache),
                images.RestBottom, images.RestTop, images.SeatedBottom, images.SeatedTop);
        }

        private static RigFacingArt Upload(RigFacingImages facing, string id,
            Dictionary<FigureImage, (Sprite sprite, Sprite silhouette)> cache)
        {
            var normals = new Dictionary<byte[], Texture2D>();
            var parts = new List<RigPartSprites>();
            foreach (var entry in facing.Parts)
            {
                var bone = facing.Rig.Skeleton[entry.Part.Bone];
                string name = $"rig_{id}_{entry.Part.Name}";

                var standingNormals = Normals(entry.StandingNormals, entry.Standing, name, normals);
                var standing = Sprites(entry.Standing, standingNormals, name, cache);
                var seated = Sprites(entry.Seated, Normals(entry.SeatedNormals, entry.Seated, name + "_seated", normals), name + "_seated", cache);

                // The powered image shares the standing canvas, so it shares its normals.
                var powered = entry.Powered != null && !entry.Powered.IsEmpty
                    ? Upload(entry.Powered.Pixels, entry.Powered, name + "_powered", standingNormals)
                    : null;

                parts.Add(new RigPartSprites(entry.Part, entry.BoneIndex,
                    standing.sprite, standing.silhouette, Offset(entry.Standing, bone),
                    seated.sprite, seated.silhouette, Offset(entry.Seated, bone),
                    powered));
            }

            return new RigFacingArt(facing.Rig, parts, facing.Crouch);
        }

        private static Vector2 Offset(FigureImage image, RigBone bone) =>
            image == null ? Vector2.zero : new Vector2(image.Canvas.Left - bone.PivotX, image.Canvas.Bottom - bone.PivotY);

        /// <summary>
        /// A part image as a sprite with its normal map, and its white
        /// silhouette; one upload per image, shared by both poses when uncut.
        /// The silhouette has no normal map: the flash is a white-out, not a surface.
        /// </summary>
        private static (Sprite sprite, Sprite silhouette) Sprites(FigureImage image, Texture2D normalMap, string name,
            Dictionary<FigureImage, (Sprite, Sprite)> cache)
        {
            if (image == null || image.IsEmpty) return (null, null);
            if (cache.TryGetValue(image, out var shared)) return shared;

            var sprite = Upload(image.Pixels, image, name, normalMap);

            var white = new byte[image.Pixels.Length];
            for (int i = 0; i < white.Length; i += 4)
            {
                white[i] = 255;
                white[i + 1] = 255;
                white[i + 2] = 255;
                white[i + 3] = image.Pixels[i + 3];
            }

            var result = (sprite, Upload(white, image, name + "_silhouette"));
            cache[image] = result;
            return result;
        }

        /// <summary>
        /// A part's cel-facet normal map (<see cref="FigureNormals"/>) as a
        /// linear texture, uploaded once however many sprites share it.
        /// </summary>
        private static Texture2D Normals(byte[] encoded, FigureImage image, string name,
            Dictionary<byte[], Texture2D> uploaded)
        {
            if (encoded == null || image == null || image.IsEmpty) return null;
            if (uploaded.TryGetValue(encoded, out var shared)) return shared;

            // Linear, not sRGB: these are directions, not colours.
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, true, true)
            {
                name = name + "_normal",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            texture.SetPixelData(encoded, 0);
            texture.Apply(true, true);

            Owned.Add(texture);
            uploaded[encoded] = texture;
            return texture;
        }

        private static Sprite Upload(byte[] rgba, FigureImage image, string name, Texture2D normalMap = null)
        {
            // Mipmapped, as the look book's figures are: a part is drawn far
            // smaller than it is rendered, and a limb that shimmers as it
            // swings reads as noise.
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            texture.SetPixelData(rgba, 0);
            texture.Apply(true, true);

            // The normal map rides as the sprite's _NormalMap secondary
            // texture, which URP's lit sprite shader reads for 2D lights that
            // use normal maps (LB5d).
            var secondary = normalMap != null
                ? new[] { new SecondarySpriteTexture { name = NormalMapName, texture = normalMap } }
                : System.Array.Empty<SecondarySpriteTexture>();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, image.Width, image.Height), Vector2.zero,
                image.Canvas.PixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.zero, false, secondary);
            sprite.name = name;

            Owned.Add(texture);
            Owned.Add(sprite);
            return sprite;
        }
    }
}
