// Assets/_Project/Scripts/Unity/View/HeroPortrait.cs
using NonaRoyale.Core.Model;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The selected operator's portrait in the tray, drawn as a large chip
    /// (hero portrait, 2026-09-29): the same art, body, inserts and gilt ring
    /// as the operator's chip on the board, in uGUI.
    /// </summary>
    /// <remarks>
    /// <b>Why a chip and not a bust.</b> The player learns one object: the
    /// thing on the board and the thing in the tray are the same chip, one
    /// big and one small. The seat colour on the body says whose it is.
    ///
    /// <b>Powered while armed.</b> With an ability armed, the portrait shows
    /// its lit twin and a cyan glow sits on the device, the same light the
    /// chip shows on the board when the cast lands. At rest the device is
    /// dark (ART §5).
    ///
    /// An operator with no portrait yet shows its shape in gold on black
    /// lacquer, like its emblem chip. Nothing here catches the pointer.
    /// </remarks>
    public static class HeroPortrait
    {
        /// <summary>The chip's thickness, in diameters: how far below it its edge shows.</summary>
        private const float EdgeDrop = 0.05f;

        private const float InkWidth = 0.014f;
        private const float EmblemSize = 0.42f;
        private const float GlowSize = 0.46f;

        /// <summary>
        /// Builds the portrait as a child of <paramref name="parent"/>,
        /// <paramref name="diameter"/> units across, centred on the returned
        /// rect. The caller anchors and places it.
        /// </summary>
        /// <param name="powered">An ability is armed: the lit twin and the device glow.</param>
        public static RectTransform Build(Transform parent, OperatorState op, float diameter, bool powered)
        {
            var root = UiKit.Rect("hero_portrait", parent);
            root.sizeDelta = new Vector2(diameter, diameter);

            var seat = BoardLayout.ColourOf(op.Owner);
            var art = ChipArtLibrary.For(op.Name);
            var edgeAt = new Vector2(0f, -EdgeDrop);
            float ink = 1f + 2f * InkWidth;

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
                Layer(root, "emblem", PieceShape.For(op), UiTheme.PieceEmblem, EmblemSize, Vector2.zero);
            }

            Layer(root, "ring", ChipSprites.Ring, UiTheme.Gold, 1f, Vector2.zero);

            if (powered)
            {
                var device = art != null ? art.Device * ChipSprites.PortraitScale : Vector2.zero;
                Layer(root, "device_glow", DecoSprites.Glow, UiTheme.WithAlpha(UiTheme.Cyan, 0.75f), GlowSize, device);
            }

            return root;
        }

        /// <summary>One layer: a sprite <paramref name="scale"/> diameters across, <paramref name="offset"/> diameters from the centre.</summary>
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
