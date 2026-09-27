// Assets/_Project/Scripts/Unity/View/UiPlate.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Makes a control a raised plate (G9a): a face shaded from a lit top to a
    /// darker foot, a lip below it showing its thickness, and a shadow on the
    /// surface under it. Pressing sinks the face by the lip's depth.
    /// </summary>
    /// <remarks>
    /// <b>One mesh, not children.</b> The lip and both shadows are copies of
    /// the face's own sliced mesh, drawn underneath it in the same draw: the
    /// lip is the face moved down by the depth, so only the strip below the
    /// face shows, and it keeps the chamfered corners without its own sprite.
    /// Because they are vertex copies, the button's tint (hover, press,
    /// disabled) reaches the face and the lip alike. The shadows are black,
    /// so the tint leaves them alone. The panels' <see cref="Shadow"/>
    /// components (<see cref="UiKit.Elevate"/>) could not draw the lip.
    ///
    /// <b>The rect is the face.</b> The lip and shadows hang below the rect,
    /// in the gap a column already leaves, so content laid out inside a
    /// button stays centred on the face.
    ///
    /// <b>Pressing moves the rect.</b> Content has to sink with the face, so
    /// <see cref="Press"/> shifts the rect down by the depth and shrinks the
    /// lip by as much: the lip's foot and the near shadow stay where they
    /// are, on the table. The raycast area is padded by the same amount the
    /// other way, so the pointer is never left outside a sinking button. If a
    /// layout rebuild puts the rect back mid-press, the offset is dropped
    /// rather than applied twice.
    ///
    /// <b>Depth follows the height.</b> A 44-unit button stands 4 units
    /// proud; an 18-unit trait chip stands about 2, so small chips are not
    /// all lip. A latched control (selected) stands at half height, a
    /// disabled one nearly flush.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UiPlate : BaseMeshEffect
    {
        /// <summary>Tallest a plate stands, in canvas units.</summary>
        public const float MaxDepth = 4f;

        private const float MinDepth = 1.5f;
        private const float DepthPerHeight = 0.1f;
        private const float LatchedFactor = 0.5f;
        private const float FlushFactor = 0.25f;

        private static readonly List<UIVertex> Face = new List<UIVertex>(64);
        private static readonly List<UIVertex> Output = new List<UIVertex>(320);

        private Image _rim;
        private Color _rimColour;
        private bool _latched;
        private bool _flush;
        private float _press;

        private float _sunk;
        private Vector2 _placedAt;
        private Vector4 _padding;

        /// <summary>
        /// Makes <paramref name="face"/> a plate. The top light is a child
        /// overlay named "rim", so add it before any edge that should cover it.
        /// </summary>
        public static UiPlate Attach(Image face, bool latched = false)
        {
            var plate = face.gameObject.AddComponent<UiPlate>();
            plate._latched = latched;
            plate._rimColour = UiTheme.PlateRim;
            plate._rim = UiKit.Overlay(face.rectTransform, DecoSprites.PlateRim, plate._rimColour, "rim");
            plate.Refresh();
            return plate;
        }

        /// <summary>A disabled control sits nearly flush and its top light dims.</summary>
        public bool Flush
        {
            get => _flush;
            set
            {
                if (_flush == value) return;
                _flush = value;
                Refresh();
            }
        }

        /// <summary>How far the plate is pressed in, 0 (resting) to 1 (sunk flat).</summary>
        public float Press
        {
            get => _press;
            set
            {
                float press = Mathf.Clamp01(value);
                if (Mathf.Approximately(press, _press)) return;
                _press = press;
                Sink();
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        /// <summary>How far the plate stands at rest, for its current height and stance.</summary>
        private float RestDepth
        {
            get
            {
                float height = graphic != null ? graphic.rectTransform.rect.height : 0f;
                float depth = Mathf.Clamp(height * DepthPerHeight, MinDepth, MaxDepth);
                return depth * (_flush ? FlushFactor : _latched ? LatchedFactor : 1f);
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            Face.Clear();
            vh.GetUIVertexStream(Face);

            var rect = graphic.rectTransform.rect;
            float height = Mathf.Max(rect.height, 1f);
            float depth = RestDepth * (1f - _press);

            // The far shadow tightens as the plate goes down; the near one is
            // on the table, so it keeps its place while the rect sinks.
            Output.Clear();
            Copy(new Vector2(0f, -(depth * UiTheme.PlateShadowFarReach + UiTheme.PlateShadowFarDrop)), UiTheme.PlateShadowFar);
            Copy(new Vector2(0f, -(depth + UiTheme.PlateShadowNearDrop)), UiTheme.PlateShadowNear);
            if (depth > 0.05f) Lip(-depth);

            // The face, lit from above: full tint at the top, darker at the foot.
            foreach (var source in Face)
            {
                var v = source;
                float t = Mathf.Clamp01((v.position.y - rect.yMin) / height);
                v.color = Shade(v.color, Mathf.Lerp(UiTheme.PlateFaceFoot, 1f, t));
                Output.Add(v);
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(Output);
        }

        private static void Copy(Vector2 offset, Color colour)
        {
            foreach (var source in Face)
            {
                var v = source;
                v.position += (Vector3)offset;

                // Black, but keeping the face's own alpha, so a fading button
                // takes its shadow with it.
                Color32 c = colour;
                c.a = (byte)(c.a * v.color.a / 255);
                v.color = c;
                Output.Add(v);
            }
        }

        private static void Lip(float drop)
        {
            foreach (var source in Face)
            {
                var v = source;
                v.position.y += drop;
                v.color = Shade(v.color, UiTheme.PlateLipShade);
                Output.Add(v);
            }
        }

        private static Color32 Shade(Color32 colour, float factor) => new Color32(
            (byte)(colour.r * factor), (byte)(colour.g * factor), (byte)(colour.b * factor), colour.a);

        /// <summary>Redraws for a new stance and dims the top light of a flush plate.</summary>
        private void Refresh()
        {
            if (_rim != null)
                _rim.color = _flush ? UiTheme.WithAlpha(_rimColour, _rimColour.a * 0.35f) : _rimColour;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        /// <summary>Moves the rect down by the pressed depth, and pads the hit area back up by as much.</summary>
        private void Sink()
        {
            var rect = graphic != null ? graphic.rectTransform : transform as RectTransform;
            if (rect == null) return;

            // A layout rebuild put the rect back: it is at rest now, not sunk.
            if (_sunk != 0f && rect.anchoredPosition != _placedAt)
            {
                _sunk = 0f;
                if (graphic != null) graphic.raycastPadding = _padding;
            }

            if (_sunk == 0f && graphic != null) _padding = graphic.raycastPadding;

            float sink = RestDepth * _press;
            rect.anchoredPosition += new Vector2(0f, _sunk - sink);
            _sunk = sink;
            _placedAt = rect.anchoredPosition;

            // Padding is left, bottom, right, top; positive shrinks.
            if (graphic != null)
                graphic.raycastPadding = sink == 0f ? _padding : _padding + new Vector4(0f, sink, 0f, -sink);
        }

        protected override void OnDisable()
        {
            // Never leave a plate sunk because it was hidden mid-press.
            Press = 0f;
            base.OnDisable();
        }
    }
}
