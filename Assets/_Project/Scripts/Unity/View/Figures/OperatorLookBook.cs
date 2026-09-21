// Assets/_Project/Scripts/Unity/View/Figures/OperatorLookBook.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Procedural Dark Deco figures for operators that have no render yet
    /// (OPERATOR_LOOKBOOK.md). Asked by <see cref="OperatorArtLibrary"/>
    /// after a real render, and never before one.
    /// </summary>
    /// <remarks>
    /// <b>The fallback order never changes:</b> a render in
    /// <c>Resources/Art/Operators</c> wins; then a look-book figure; then the
    /// procedural pawn and bust. The look book is what draws when there is
    /// no render, not a competitor to one.
    ///
    /// <b>One figure per operator, not per seat.</b> A look-book figure is
    /// drawn untinted like a render, with the seat on the disc, ring and pin
    /// (designer, 2026-09-21: LB1 rides the ART1 path). So four seats share one
    /// texture.
    ///
    /// <b>Built off the main thread.</b> The rasteriser is plain C#, so
    /// <see cref="Prewarm"/> renders every pose on the thread pool and only the
    /// texture upload happens here. A figure asked for before its render
    /// finishes waits for it; one never prewarmed renders on the spot. Every
    /// build logs its time, which is the number the frame-time check reads.
    ///
    /// <b>The recipes live in <see cref="LookRoster"/></b> (LB2), one file per
    /// operator under <c>Looks/</c>. This class only caches, prewarms and
    /// uploads; it does not know any operator by name.
    /// </remarks>
    public static class OperatorLookBook
    {
        /// <summary>
        /// Figure space x −1.1..1.108, y −0.05..3.054 at 96 texels per unit,
        /// sampled 4×4 per texel. A standing figure comes out about 280
        /// texels tall, well over what it covers on screen at board scale.
        /// </summary>
        public static readonly FigureCanvas Canvas = new FigureCanvas(-1.1f, -0.05f, 212, 298, 96f, 4);

        /// <summary>Off draws the pawn and bust instead, for a side-by-side check.</summary>
        public static bool Enabled { get; set; } = true;

        private static readonly Dictionary<string, FigureArt> Figures = new Dictionary<string, FigureArt>();
        private static readonly Dictionary<string, Task<FigureImage>> Pending = new Dictionary<string, Task<FigureImage>>();
        private static LookBookPalette _palette;

        /// <summary>Whether the operator has a look-book figure at all.</summary>
        public static bool Has(string operatorName) => LookRoster.Find(operatorName) != null;

        /// <summary>The figure for a pose, built on first use; null for an operator with no recipe.</summary>
        public static FigureArt Figure(string operatorName, FigurePose pose)
        {
            string key = OperatorArtNames.Key(operatorName);
            string id = Id(key, pose);
            if (Figures.TryGetValue(id, out var cached) && (cached == null || cached.IsAlive)) return cached;

            FigureImage image = null;
            try
            {
                if (Pending.TryGetValue(id, out var task))
                {
                    Pending.Remove(id);
                    image = task.Result;   // waits if it is still rendering
                }
                else
                {
                    var drawing = Drawing(key, pose);
                    image = drawing != null ? FigureRasterizer.Render(drawing, Canvas) : null;
                }
            }
            catch (Exception e)
            {
                // A broken recipe must never break a piece: it draws the pawn.
                Debug.LogError($"[LookBook] {id} failed to render; drawing the procedural figure. {e.GetBaseException()}");
                image = null;
            }

            var art = image != null ? FigureSprites.ToFigureArt(image, $"lookbook_{id}") : null;
            if (image != null)
                Debug.Log($"[LookBook] {id}: {image.Milliseconds:0} ms, {image.Width}×{image.Height}");

            Figures[id] = art;
            return art;
        }

        /// <summary>
        /// Starts rendering every pose of these operators on the thread pool,
        /// seated first, since the yard needs those at once. Operators with no
        /// recipe, and poses already built or started, are skipped.
        /// </summary>
        public static void Prewarm(IEnumerable<string> operatorNames)
        {
            if (!Enabled || operatorNames == null) return;

            // WebGL has no thread pool to speak of; there, figures build when asked.
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;

            var names = new List<string>(operatorNames);
            foreach (var pose in new[] { FigurePose.Seated, FigurePose.Standing })
            {
                foreach (var name in names)
                {
                    string key = OperatorArtNames.Key(name);
                    string id = Id(key, pose);
                    if (Figures.ContainsKey(id) || Pending.ContainsKey(id)) continue;

                    // A real render wins, so there is nothing to warm.
                    if (OperatorArtLibrary.Rendered(name, pose) != null) continue;

                    // The drawing is built here, on the main thread, because it
                    // reads UiTheme; only the rasterising leaves it.
                    var drawing = Drawing(key, pose);
                    if (drawing == null) continue;

                    Pending[id] = Task.Run(() => FigureRasterizer.Render(drawing, Canvas));
                }
            }
        }

        /// <summary>Forgets every figure, so the next lookup builds again. For tests and tools.</summary>
        public static void ClearCache()
        {
            foreach (var art in Figures.Values)
            {
                if (art == null) continue;
                Destroy(art.Sprite);
                Destroy(art.Silhouette);
            }

            Figures.Clear();
            Pending.Clear();
            _palette = null;
        }

        /// <summary>The drawing for an operator's pose, or null. The seated pose is the standing one cut at the waist.</summary>
        internal static FigureDrawing Drawing(string key, FigurePose pose)
        {
            var look = LookRoster.Find(key);
            return look?.Draw(Palette, pose == FigurePose.Seated);
        }

        /// <summary>The look book's colours, all from <see cref="UiTheme"/>. Public for the judging tools.</summary>
        public static LookBookPalette Palette => _palette ?? (_palette = BuildPalette());

        private static string Id(string key, FigurePose pose) =>
            key + (pose == FigurePose.Seated ? "_seated" : "_standing");

        private static LookBookPalette BuildPalette() => new LookBookPalette
        {
            Ink = F(UiTheme.Ink),
            Rim = F(UiTheme.LookRim),
            RimOnLight = F(UiTheme.LookRimOnLight),
            Shade = F(UiTheme.LookShade),
            Key = F(UiTheme.LookKey),
            SuitSheen = F(UiTheme.LookSuitSheen),
            PlateSheen = F(UiTheme.LookPlateSheen),
            Brass = F(UiTheme.Brass),
            Powered = F(UiTheme.Cyan),
            Bone = F(UiTheme.LookBone),
            Obsidian = F(UiTheme.Obsidian),
            BouncerSuit = F(UiTheme.LookBouncerSuit),
            BouncerSkin = F(UiTheme.LookBouncerSkin),
            MimiCoat = F(UiTheme.LookMimiCoat),
            MimiRig = F(UiTheme.LookMimiRig),
            MimiSteel = F(UiTheme.LookMimiSteel),
            MimiSkin = F(UiTheme.LookMimiSkin),
            NuetuGrey = F(UiTheme.LookNuetuGrey),
            NuetuPlate = F(UiTheme.LookNuetuPlate),
            NuetuSkin = F(UiTheme.LookNuetuSkin),
        };

        /// <summary>A UiTheme colour as the rasteriser's colour.</summary>
        public static FigureColour F(Color colour) => new FigureColour(colour.r, colour.g, colour.b, colour.a);

        private static void Destroy(Sprite sprite)
        {
            if (sprite == null) return;

            var texture = sprite.texture;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(sprite);
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
