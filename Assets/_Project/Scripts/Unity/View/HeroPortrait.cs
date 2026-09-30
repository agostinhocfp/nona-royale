// Assets/_Project/Scripts/Unity/View/HeroPortrait.cs
using NonaRoyale.Core.Model;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The selected operator's portrait in the tray (hero portrait,
    /// 2026-09-29): the operator's chip portrait inside the Deco hero frame,
    /// in uGUI.
    /// </summary>
    /// <remarks>
    /// <b>The frame is painted</b> (designer's hero-frame sheet, 2026-09-29):
    /// black lacquer, oxblood insets, ivory clasps top and bottom, side pods
    /// and a gold inner ring, cut from the sheet by
    /// <c>tools/art/hero_frame.py</c> into
    /// <c>Resources/Art/Hero/hero_frame</c> (the frame, its centre clear) and
    /// <c>hero_frame_lit</c> (the cyan arcs alone, aligned to it). Both are
    /// square canvases centred on the gold ring, so one size places them.
    ///
    /// <b>The face is the chip's</b> (<see cref="ChipArtLibrary"/>): the same
    /// portrait the operator carries on the board, so the tray and the board
    /// show one object. An operator with no portrait yet shows its shape in
    /// gold on black lacquer.
    ///
    /// <b>Powered while armed.</b> With an ability armed, the portrait shows
    /// its lit twin, the frame's cyan arcs come on, and a glow in the
    /// operator's own colour sits on the device (<see cref="OperatorGlow"/>,
    /// ADR-0014). The arcs stay cyan: the frame is the interface, not the
    /// operator. At rest the device and the arcs are dark (ART §5).
    ///
    /// <b>No frame file, no frame.</b> Without <c>hero_frame</c> the portrait
    /// is drawn as a large chip in the seat colour, as it was first built.
    ///
    /// Nothing here catches the pointer.
    /// </remarks>
    public static class HeroPortrait
    {
        public const string FramePath = "Art/Hero/hero_frame";
        public const string FrameLitPath = "Art/Hero/hero_frame_lit";

        // The frame canvas, measured on the cut sprite (fractions of its width).

        /// <summary>The gold ring's inner edge: the portrait fills this circle and tucks under the ring.</summary>
        public const float InnerRadius = 199f / 640f;

        /// <summary>How much of the canvas the frame covers, across and down: the side pods, and the clasps.</summary>
        public const float VisibleWidth = 610f / 640f;
        public const float VisibleHeight = 513f / 640f;

        /// <summary>The clear band under the bottom clasp, which a caller standing the frame on a line takes back.</summary>
        public const float BottomInset = 57.5f / 640f;

        private const float EmblemSize = 0.26f;
        private const float GlowSize = 0.3f;

        // The chip fallback's proportions, in diameters.
        private const float ChipEdgeDrop = 0.05f;
        private const float ChipInkWidth = 0.014f;
        private const float ChipEmblemSize = 0.42f;

        private static Sprite _frame;
        private static Sprite _frameLit;
        private static bool _looked;

        /// <summary>Whether the painted frame is in the build.</summary>
        public static bool HasFrame
        {
            get
            {
                Load();
                return _frame != null;
            }
        }

        /// <summary>
        /// Builds the portrait as a child of <paramref name="parent"/>: a
        /// square <paramref name="size"/> units across, centred on the gold
        /// ring. The caller anchors and places it; the frame's visible part
        /// is <see cref="VisibleWidth"/> by <see cref="VisibleHeight"/> of it.
        /// </summary>
        /// <param name="powered">An ability is armed: the lit twin, the arcs and the device glow.</param>
        public static RectTransform Build(Transform parent, OperatorState op, float size, bool powered)
        {
            var root = UiKit.Rect("hero_portrait", parent);
            root.sizeDelta = new Vector2(size, size);

            Load();
            if (_frame != null) Framed(root, op, powered);
            else Chip(root, op, powered);

            return root;
        }

        /// <summary>Forgets the frame, so the next build loads it again. For tests and tools.</summary>
        public static void ClearCache()
        {
            _frame = null;
            _frameLit = null;
            _looked = false;
        }

        private static void Load()
        {
            // A destroyed sprite reads as null, and loading again is cheap.
            if (_looked && (_frame != null || ReferenceEquals(_frame, null))) return;
            _looked = true;
            _frame = Resources.Load<Sprite>(FramePath);
            _frameLit = Resources.Load<Sprite>(FrameLitPath);
        }

        private static void Framed(RectTransform root, OperatorState op, bool powered)
        {
            var art = ChipArtLibrary.For(op.Name);

            // The portrait's circle (a fraction of its width) fills the ring's inside, and a hair more.
            float face = (InnerRadius + 0.004f) * 2f;
            float portrait = (InnerRadius + 0.004f) / ChipArtLibrary.MaskRadius;

            Layer(root, "shadow", BoardArt.SoftDisc, UiTheme.WithAlpha(Color.black, 0.55f), VisibleWidth * 1.08f, new Vector2(0.02f, -0.05f));

            if (art != null)
            {
                var sprite = powered && art.Lit != null ? art.Lit : art.Unlit;
                Layer(root, "face", sprite, Color.white, portrait, Vector2.zero);
            }
            else
            {
                Layer(root, "face", ChipSprites.Disc, UiTheme.Obsidian, face, Vector2.zero);
                Layer(root, "emblem", PieceShape.For(op), UiTheme.PieceEmblem, EmblemSize, Vector2.zero);
            }

            Layer(root, "frame", _frame, Color.white, 1f, Vector2.zero);

            if (!powered) return;

            if (_frameLit != null) Layer(root, "frame_lit", _frameLit, Color.white, 1f, Vector2.zero);

            var device = art != null ? art.Device * portrait : Vector2.zero;
            Layer(root, "device_glow", DecoSprites.Glow, UiTheme.WithAlpha(OperatorGlow.For(op.Name), 0.75f), GlowSize, device);
        }

        /// <summary>The first build: a large chip in the seat colour. Kept for a build without the frame.</summary>
        private static void Chip(RectTransform root, OperatorState op, bool powered)
        {
            var seat = BoardLayout.ColourOf(op.Owner);
            var art = ChipArtLibrary.For(op.Name);
            var edgeAt = new Vector2(0f, -ChipEdgeDrop);
            float ink = 1f + 2f * ChipInkWidth;

            Layer(root, "shadow", BoardArt.SoftDisc, UiTheme.WithAlpha(Color.black, 0.6f), 1.14f, new Vector2(0.03f, -0.08f));
            Layer(root, "ink_edge", ChipSprites.Disc, UiTheme.Ink, ink, edgeAt);
            Layer(root, "ink", ChipSprites.Disc, UiTheme.Ink, ink, Vector2.zero);
            Layer(root, "edge", ChipSprites.Disc, Color.Lerp(Color.black, seat, 0.42f), 1f, edgeAt);
            Layer(root, "edge_inserts", ChipSprites.Inserts, Color.Lerp(Color.black, UiTheme.DieFace, 0.62f), 1f, edgeAt);
            Layer(root, "body", ChipSprites.Body, seat, 1f, Vector2.zero);
            Layer(root, "inserts", ChipSprites.Inserts, UiTheme.DieFace, 1f, Vector2.zero);

            if (art != null)
            {
                var face = powered && art.Lit != null ? art.Lit : art.Unlit;
                Layer(root, "face", face, Color.white, ChipSprites.PortraitScale, Vector2.zero);
            }
            else
            {
                Layer(root, "face", ChipSprites.Disc, UiTheme.Obsidian, ChipSprites.FaceRadius, Vector2.zero);
                Layer(root, "emblem", PieceShape.For(op), UiTheme.PieceEmblem, ChipEmblemSize, Vector2.zero);
            }

            Layer(root, "ring", ChipSprites.Ring, UiTheme.Gold, 1f, Vector2.zero);

            if (powered)
            {
                var device = art != null ? art.Device * ChipSprites.PortraitScale : Vector2.zero;
                Layer(root, "device_glow", DecoSprites.Glow, UiTheme.WithAlpha(OperatorGlow.For(op.Name), 0.75f), 0.46f, device);
            }
        }

        /// <summary>One layer: a sprite <paramref name="scale"/> of the root across, <paramref name="offset"/> of it from the centre.</summary>
        private static void Layer(RectTransform root, string name, Sprite sprite, Color colour, float scale, Vector2 offset)
        {
            var rect = UiKit.Rect(name, root);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = root.sizeDelta * scale;
            rect.anchoredPosition = offset * root.sizeDelta.x;

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
    }
}
