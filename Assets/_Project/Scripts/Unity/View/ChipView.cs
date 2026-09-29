// Assets/_Project/Scripts/Unity/View/ChipView.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// An operator drawn as a casino chip lying on the table (chip pieces,
    /// 2026-09-29): a shadow, the chip's thickness, a body in the seat
    /// colour with ivory edge inserts, a gilt ring, and the operator's
    /// portrait as the face. Plain C# owned by the piece, like
    /// <see cref="RigView"/>.
    /// </summary>
    /// <remarks>
    /// <b>Flat, and made to look thick.</b> Seen from straight above a chip is
    /// a circle, so its edge is a second disc a little lower on screen in a
    /// darker seat colour, carrying the inserts down its side, and its shadow
    /// a soft disc further down. Under the tilted camera the piece stops
    /// standing up (the chip lies on the board), and the same offsets read as
    /// the near edge.
    ///
    /// <b>Made to stand out (CP2, designer 2026-09-29: "pop more without
    /// increasing size").</b> Three things, in the order they matter:
    /// <list type="bullet">
    /// <item><b>The face and the ring are unlit</b>
    /// (<see cref="ShaderFx.ChipUnlit"/>), at <see cref="FaceTint"/>: the
    /// room's warm white, not its dim ambient. The body stays lit, so the
    /// chip still sits in the room's light.</item>
    /// <item><b>An ink outline</b> round the chip and its edge, in the style's
    /// line colour (<c>UiTheme.Ink</c>, ART §2.2), so it never melts into the
    /// dark cells or the gilt inlay. The selection rim draws outside it.</item>
    /// <item><b>Lift:</b> a thicker edge, a tight contact shadow in the seam
    /// where the chip meets the table, and a darker drop shadow.</item>
    /// </list>
    ///
    /// <b>Two faces.</b> An operator with a portrait
    /// (<see cref="ChipArtLibrary"/>) shows it. One without shows its shape
    /// (<see cref="PieceShape"/>) in gold on black lacquer, so the whole
    /// board is chips while the portraits are made.
    ///
    /// <b>The cast.</b> The lit portrait fades in over the unlit one, and a
    /// cyan glow swells over the device, for the tell's length. Devices are
    /// dark at rest (ART §5); this is the only time the chip carries cyan.
    ///
    /// Units: the chip is one unit across inside <see cref="Root"/>, which
    /// the piece scales to the chip's diameter.
    /// </remarks>
    internal sealed class ChipView
    {
        /// <summary>The thickness: how far below the chip its edge shows, in chip diameters. 0.055 before CP2.</summary>
        private const float EdgeDrop = 0.08f;

        /// <summary>The drop shadow's offset and size, in chip diameters.</summary>
        private static readonly Vector2 ShadowOffset = new Vector2(0.03f, -0.11f);
        private const float ShadowSize = 1.12f;

        /// <summary>The drop shadow's darkness. 0.55 before CP2.</summary>
        private const float ShadowAlpha = 0.7f;

        /// <summary>The contact shadow: tight and dark, just under the edge, where the chip meets the table.</summary>
        private const float ContactSize = 1.06f;
        private const float ContactAlpha = 0.75f;

        /// <summary>The ink outline's width, in chip diameters, each side.</summary>
        private const float InkWidth = 0.018f;
        private const float InkAlpha = 0.95f;

        /// <summary>The edge's colour against the seat's: the side of a chip is in its own shade.</summary>
        private const float EdgeShade = 0.42f;

        /// <summary>The inserts carried down the edge, against the ones on top: in the edge's shade.</summary>
        private const float EdgeInsertShade = 0.62f;

        /// <summary>
        /// What the unlit face and ring are multiplied by: the room's warm
        /// ambient colour (<c>SceneLighting.ambientColour</c>) at nearly full
        /// strength, so they read as lit by the room's key light rather than
        /// pasted on. Tune in Play Mode.
        /// </summary>
        private static readonly Color FaceTint = new Color(0.97f, 0.94f, 0.89f);

        /// <summary>The portrait's scale, so the importer's circle lands just under the gilt ring.</summary>
        private static float PortraitScale => (0.5f * ChipSprites.FaceRadius + 0.005f) / ChipArtLibrary.MaskRadius;

        /// <summary>The emblem's size on an emblem chip, in chip diameters.</summary>
        private const float EmblemSize = 0.42f;

        /// <summary>The device glow at its peak, in chip diameters.</summary>
        private const float FlareSize = 0.42f;

        /// <summary>How long the lit face takes to come up, and to go, in scaled seconds.</summary>
        private const float CastIn = 0.12f;
        private const float CastOut = 0.3f;

        // Orders inside the piece's sorting group: the shadows under
        // everything, then over the rim (3) and under the piece's overlay (40).
        private const int ShadowOrder = -2;
        private const int ContactOrder = -1;
        private const int InkOrder = 4;
        private const int EdgeOrder = 5;
        private const int EdgeInsertOrder = 6;
        private const int BodyOrder = 7;
        private const int InsertOrder = 8;
        private const int FaceOrder = 9;
        private const int EmblemOrder = 10;
        private const int LitOrder = 11;
        private const int RingOrder = 12;
        private const int FlareOrder = 13;

        /// <summary>The flash overlay's order in chip mode: over everything the chip draws.</summary>
        public const int FlashOrder = 14;

        private readonly Transform _root;
        private readonly SpriteRenderer _shadow;
        private readonly SpriteRenderer _contact;
        private readonly SpriteRenderer _inkTop;
        private readonly SpriteRenderer _inkEdge;
        private readonly SpriteRenderer _edge;
        private readonly SpriteRenderer _edgeInserts;
        private readonly SpriteRenderer _body;
        private readonly SpriteRenderer _inserts;
        private readonly SpriteRenderer _face;
        private readonly SpriteRenderer _emblem;
        private readonly SpriteRenderer _lit;
        private readonly SpriteRenderer _ring;
        private readonly SpriteRenderer _flare;
        private readonly List<SpriteRenderer> _parts;

        private Color _seat;
        private float _alpha = 1f;
        private float _castLeft;
        private float _castLength;
        private float _power;
        private bool _active;

        public ChipView(Transform parent, Sprite emblem)
        {
            _root = new GameObject("chip").transform;
            _root.SetParent(parent, false);

            var edgeAt = new Vector2(0f, -EdgeDrop);
            float inkScale = 1f + 2f * InkWidth;

            _shadow = Part("chip_shadow", BoardArt.SoftDisc, ShadowOrder, ShadowSize, ShadowOffset);
            _contact = Part("chip_contact", ChipSprites.Contact, ContactOrder, ContactSize, edgeAt);
            _inkTop = Part("chip_ink", ChipSprites.Disc, InkOrder, inkScale, Vector2.zero);
            _inkEdge = Part("chip_ink_edge", ChipSprites.Disc, InkOrder, inkScale, edgeAt);
            _edge = Part("chip_edge", ChipSprites.Disc, EdgeOrder, 1f, edgeAt);
            _edgeInserts = Part("chip_edge_inserts", ChipSprites.Inserts, EdgeInsertOrder, 1f, edgeAt);
            _body = Part("chip_body", ChipSprites.Body, BodyOrder, 1f, Vector2.zero);
            _inserts = Part("chip_inserts", ChipSprites.Inserts, InsertOrder, 1f, Vector2.zero);
            _face = Part("chip_face", null, FaceOrder, PortraitScale, Vector2.zero);
            _emblem = Part("chip_emblem", emblem, EmblemOrder, EmblemSize, Vector2.zero);
            _lit = Part("chip_face_lit", null, LitOrder, PortraitScale, Vector2.zero);
            _ring = Part("chip_ring", ChipSprites.Ring, RingOrder, 1f, Vector2.zero);
            _flare = Part("chip_device_flare", DecoSprites.Glow, FlareOrder, FlareSize, Vector2.zero);

            // The face, the ring and the glow ignore the room's light (CP2).
            // Without the material they stay lit, as before.
            var unlit = ShaderFx.Source(ShaderFx.ChipUnlit);
            if (unlit != null)
            {
                foreach (var renderer in new[] { _face, _emblem, _lit, _ring, _flare })
                    renderer.sharedMaterial = unlit;
            }

            _parts = new List<SpriteRenderer>
                { _inkEdge, _inkTop, _edge, _edgeInserts, _body, _inserts, _face, _emblem, _lit, _ring };

            // Dark from the first frame, before the first Tick shapes them.
            _shadow.color = new Color(0f, 0f, 0f, ShadowAlpha);
            _contact.color = new Color(0f, 0f, 0f, ContactAlpha);

            Active = false;
        }

        /// <summary>The chip's own transform: one unit across. The piece scales and places its parent.</summary>
        public Transform Root => _root;

        /// <summary>Whether the chip is drawn. Off, every part hides.</summary>
        public bool Active
        {
            get => _active;
            set
            {
                _active = value;
                _root.gameObject.SetActive(value);
            }
        }

        /// <summary>Whether the face shows a portrait rather than the emblem.</summary>
        public bool HasPortrait { get; private set; }

        /// <summary>What the knockout burns: the chip as it is drawn, without its shadows or the glow.</summary>
        public IReadOnlyList<SpriteRenderer> Parts => _parts;

        /// <summary>The white disc the hit flash and the rim are drawn from.</summary>
        public Sprite Silhouette => ChipSprites.Disc;

        /// <summary>The parts whose outline the rim follows: the chip's ink outline on top, and on its edge.</summary>
        public SpriteRenderer RimTop => _inkTop;
        public SpriteRenderer RimEdge => _inkEdge;

        /// <summary>The chip's bounds in the world, for hit testing: the body and its edge.</summary>
        public Bounds Bounds
        {
            get
            {
                var bounds = _body.bounds;
                bounds.Encapsulate(_edge.bounds);
                return bounds;
            }
        }

        /// <summary>Shows <paramref name="art"/> as the face, or the emblem when it is null.</summary>
        public void Show(ChipArt art)
        {
            HasPortrait = art != null && art.Unlit != null;

            _face.sprite = HasPortrait ? art.Unlit : ChipSprites.Disc;
            _face.transform.localScale = Vector3.one * (HasPortrait ? PortraitScale : ChipSprites.FaceRadius);
            _emblem.enabled = !HasPortrait;

            _lit.sprite = HasPortrait ? art.Lit : null;

            // The glow sits on the device; an emblem chip has its device at the centre.
            var device = HasPortrait ? art.Device * PortraitScale : Vector2.zero;
            _flare.transform.localPosition = new Vector3(device.x, device.y, 0f);

            Colour();
        }

        /// <summary>The seat's colour, for the body and its edge.</summary>
        public void SetSeat(Color seat)
        {
            _seat = seat;
            Colour();
        }

        /// <summary>The evasive fade, for every part at once.</summary>
        public void SetAlpha(float alpha)
        {
            _alpha = alpha;
            Colour();
        }

        /// <summary>Lights the device for <paramref name="seconds"/> (scaled), the fade in and out included.</summary>
        public void Cast(float seconds)
        {
            _castLength = Mathf.Max(CastIn + CastOut, seconds);
            _castLeft = _castLength;
        }

        /// <summary>Puts the device dark at once: a knockout, or a piece put back.</summary>
        public void StopCast()
        {
            _castLeft = 0f;
            _power = 0f;
            Colour();
        }

        /// <summary>
        /// One frame: the cast's light, and the shadows. The drop shadow
        /// spreads, pales and slides away as the chip leaves the table on a hop
        /// (<paramref name="air"/>, 0..1); the contact shadow is only there
        /// while the chip touches the table.
        /// </summary>
        public void Tick(float rated, float air)
        {
            if (_castLeft > 0f)
            {
                _castLeft = Mathf.Max(0f, _castLeft - rated);
                float elapsed = _castLength - _castLeft;
                _power = Mathf.Min(Mathf.Clamp01(elapsed / CastIn), Mathf.Clamp01(_castLeft / CastOut));
            }
            else
            {
                _power = 0f;
            }

            var offset = ShadowOffset * (1f + 1.2f * air);
            _shadow.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            _shadow.transform.localScale = Vector3.one * (ShadowSize * (1f + 0.15f * air));
            _shadow.color = new Color(0f, 0f, 0f, ShadowAlpha * _alpha * (1f - 0.45f * air));

            _contact.color = new Color(0f, 0f, 0f, ContactAlpha * _alpha * (1f - Mathf.Clamp01(air * 3f)));

            Colour();
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        private void Colour()
        {
            var ink = UiTheme.WithAlpha(UiTheme.Ink, InkAlpha * _alpha);
            _inkTop.color = ink;
            _inkEdge.color = ink;

            var body = _seat;
            body.a = _alpha;
            _body.color = body;

            var edge = Color.Lerp(Color.black, _seat, EdgeShade);
            edge.a = _alpha;
            _edge.color = edge;

            _inserts.color = UiTheme.WithAlpha(UiTheme.DieFace, _alpha);
            _edgeInserts.color = UiTheme.WithAlpha(Color.Lerp(Color.black, UiTheme.DieFace, EdgeInsertShade), _alpha);

            // The unlit parts take the room's warm white, so they are lit by
            // it rather than pasted over it (CP2).
            _ring.color = Tinted(UiTheme.Gold);

            // A portrait draws as painted; the emblem chip's face is black lacquer under a gold shape.
            _face.color = HasPortrait ? Tinted(Color.white) : UiTheme.WithAlpha(UiTheme.Obsidian, _alpha);
            _emblem.color = Tinted(UiTheme.PieceEmblem);

            _lit.enabled = _lit.sprite != null && _power > 0f;
            var lit = Tinted(Color.white);
            lit.a *= _power;
            _lit.color = lit;

            _flare.enabled = _power > 0f;
            _flare.color = UiTheme.WithAlpha(UiTheme.Cyan, 0.85f * _alpha * _power);
            _flare.transform.localScale = Vector3.one * (FlareSize * (0.6f + 0.4f * _power));
        }

        private Color Tinted(Color colour) =>
            new Color(colour.r * FaceTint.r, colour.g * FaceTint.g, colour.b * FaceTint.b, colour.a * _alpha);

        private SpriteRenderer Part(string name, Sprite sprite, int order, float scale, Vector2 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            go.transform.localScale = Vector3.one * scale;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
