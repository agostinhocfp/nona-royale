// Assets/_Project/Scripts/Unity/View/CursorSkin.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>What the pointer should look like this frame.</summary>
    public enum CursorLook
    {
        /// <summary>Everywhere by default: menus, the HUD, selecting and moving.</summary>
        Arrow,

        /// <summary>An ability waits for a target, and a click here would aim it.</summary>
        Aim,

        /// <summary>An ability waits for a target, and a click here would not aim it.</summary>
        AimBlocked,
    }

    /// <summary>
    /// The game's own pointer, drawn in code from <see cref="UiTheme"/>
    /// (GUI increment G5).
    /// </summary>
    /// <remarks>
    /// <b>A hardware cursor.</b> <c>CursorMode.Auto</c> hands the image to the
    /// operating system, which draws it at the mouse's true position. A
    /// software cursor trails the mouse by a frame, and the board asks for
    /// clicks on small cells.
    ///
    /// <b>Drawn, not imported</b>, like the pieces and the board (ADR-0008
    /// consequence 2). The palette is not signed off yet, and a drawn cursor
    /// follows <see cref="UiTheme"/> when it changes. An authored image can
    /// replace any of the three looks later without touching the callers.
    ///
    /// <b>Three looks, in the two registers.</b> The arrow is static chrome:
    /// a gold Deco dart, two-tone like the figures, with the ink outline. The
    /// reticle is aim, so it is amber like the target rings
    /// (<see cref="UiTheme.Threat"/>): four inward darts round a hollow
    /// diamond. Over something the aim can't take, the reticle turns grey and
    /// loses its diamond. Sharp corners throughout (ART_DIRECTION §8).
    ///
    /// <b>32 × 32, not scaled for DPI.</b> Unity does not scale hardware
    /// cursors. If it reads small on a high-DPI screen, a 48 px set chosen by
    /// <c>Screen.dpi</c> goes here and nowhere else.
    ///
    /// <b>Plain C#, owned by the composition root.</b> It changes the cursor
    /// only when the look changes, so calling <see cref="Show"/> every frame
    /// is cheap. <see cref="Release"/> gives the system cursor back.
    /// </remarks>
    public sealed class CursorSkin
    {
        private const int Size = 32;

        /// <summary>Supersamples per axis: 16 per pixel, for clean diagonal edges.</summary>
        private const int Samples = 4;

        private const float ArrowOutline = 1f;
        private const float ReticleOutline = 1f;

        /// <summary>The blocked reticle is dimmer as a whole, not only greyer.</summary>
        private const float BlockedOpacity = 0.85f;

        private static readonly Vector2 ArrowHotspot = new Vector2(1f, 1f);
        private static readonly Vector2 ReticleCentre = new Vector2(Size * 0.5f, Size * 0.5f);

        /// <summary>
        /// The arrow: a tall dart whose tip is the hotspot, symmetric about
        /// the diagonal. Top-left pixel space, y down.
        /// </summary>
        private static readonly Vector2[] Dart =
        {
            new Vector2(1f, 1f),
            new Vector2(24f, 12.7f),
            new Vector2(12f, 12f),
            new Vector2(12.7f, 24f),
        };

        /// <summary>
        /// One of the reticle's four darts, pointing at the centre along +v.
        /// The other three are the same dart, folded.
        /// </summary>
        private static readonly Vector2[] Tick =
        {
            new Vector2(0f, 8.5f),
            new Vector2(4.2f, 15.6f),
            new Vector2(-4.2f, 15.6f),
        };

        /// <summary>The hollow diamond at the reticle's centre, as L1 radii (|x| + |y|).</summary>
        private const float DiamondInner = 2.5f;
        private const float DiamondOuter = 7f;

        private static readonly float Root2 = Mathf.Sqrt(2f);

        private Texture2D _arrow;
        private Texture2D _aim;
        private Texture2D _blocked;

        /// <summary>The look last handed to the system. Null forces the next <see cref="Show"/> through.</summary>
        private CursorLook? _shown;

        public CursorSkin()
        {
            _arrow = Draw("cursor_arrow", ArrowPixel, 1f);
            _aim = Draw("cursor_aim", p => ReticlePixel(p, UiTheme.Threat, withDiamond: true), 1f);
            _blocked = Draw("cursor_aim_blocked", p => ReticlePixel(p, UiTheme.TextDim, withDiamond: false), BlockedOpacity);
        }

        /// <summary>Shows <paramref name="look"/>, if it is not already showing.</summary>
        public void Show(CursorLook look)
        {
            if (_arrow == null || _shown == look) return;

            _shown = look;

            switch (look)
            {
                case CursorLook.Aim:
                    Cursor.SetCursor(_aim, ReticleCentre, CursorMode.Auto);
                    break;
                case CursorLook.AimBlocked:
                    Cursor.SetCursor(_blocked, ReticleCentre, CursorMode.Auto);
                    break;
                default:
                    Cursor.SetCursor(_arrow, ArrowHotspot, CursorMode.Auto);
                    break;
            }
        }

        /// <summary>
        /// Forgets what is showing, so the next <see cref="Show"/> sets it
        /// again. For when the window regains focus: some platforms reset the
        /// cursor while the game is in the background.
        /// </summary>
        public void Invalidate() => _shown = null;

        /// <summary>Gives the system cursor back and frees the textures. The skin is dead afterwards.</summary>
        public void Release()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            Free(ref _arrow);
            Free(ref _aim);
            Free(ref _blocked);
            _shown = null;
        }

        private static void Free(ref Texture2D texture)
        {
            if (texture != null) Object.Destroy(texture);
            texture = null;
        }

        // ── Shapes (top-left pixel space, y down) ────────────────────────

        private static Color ArrowPixel(Vector2 p)
        {
            float d = SignedDistance(p, Dart);

            if (d > 0f) return Color.clear;
            if (d > -ArrowOutline) return UiTheme.Ink;

            // Two-tone, like the figures: the half on the right-hand wing catches the light.
            return p.x > p.y ? UiTheme.GoldBright : UiTheme.Gold;
        }

        private static Color ReticlePixel(Vector2 p, Color fill, bool withDiamond)
        {
            var q = p - ReticleCentre;
            float ax = Mathf.Abs(q.x);
            float ay = Mathf.Abs(q.y);

            // Fold the four darts onto the one on +v: v along the nearer axis, u across it.
            var folded = ay >= ax ? new Vector2(ax, ay) : new Vector2(ay, ax);
            float d = SignedDistance(folded, Tick);

            if (withDiamond)
            {
                float l1 = ax + ay;
                float ring = Mathf.Max((l1 - DiamondOuter) / Root2, (DiamondInner - l1) / Root2);
                d = Mathf.Min(d, ring);
            }

            if (d > 0f) return Color.clear;
            return d > -ReticleOutline ? UiTheme.Ink : fill;
        }

        /// <summary>
        /// Exact signed distance to a simple polygon, negative inside
        /// (Inigo Quilez's formulation). Winding order does not matter.
        /// </summary>
        /// <remarks>
        /// The outline is drawn inside the shape, so the silhouette keeps the
        /// polygon's sharp corners even though the distance rounds them
        /// outside.
        /// </remarks>
        private static float SignedDistance(Vector2 p, Vector2[] v)
        {
            float d = (p - v[0]).sqrMagnitude;
            float sign = 1f;

            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];

                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, b.sqrMagnitude);

                bool above = p.y >= v[i].y;
                bool below = p.y < v[j].y;
                bool left = e.x * w.y > e.y * w.x;

                if ((above && below && left) || (!above && !below && !left)) sign = -sign;
            }

            return sign * Mathf.Sqrt(d);
        }

        // ── Raster ───────────────────────────────────────────────────────

        /// <summary>
        /// Paints a cursor texture by supersampling <paramref name="paint"/>,
        /// which answers each sample with a solid colour or clear.
        /// </summary>
        /// <remarks>
        /// <b>What the system cursor needs:</b> RGBA32, readable, no mip chain,
        /// and, in the editor, <c>alphaIsTransparency</c>. Without these Unity
        /// logs "Invalid texture used for cursor" and keeps the old one.
        ///
        /// Colours are averaged weighted by coverage, so edges fade in alpha
        /// without darkening. The theme's colours are written as they are:
        /// the system shows the bytes directly, whatever the project's colour
        /// space.
        /// </remarks>
        private static Texture2D Draw(string name, System.Func<Vector2, Color> paint, float opacity)
        {
            var pixels = new Color32[Size * Size];
            float step = 1f / Samples;
            int count = Samples * Samples;

            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;

                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            var c = paint(new Vector2(px + (sx + 0.5f) * step, py + (sy + 0.5f) * step));
                            r += c.r * c.a;
                            g += c.g * c.a;
                            b += c.b * c.a;
                            a += c.a;
                        }
                    }

                    var colour = a > 0f
                        ? new Color(r / a, g / a, b / a, a / count * opacity)
                        : Color.clear;

                    // Texture rows run bottom-up; the shapes are drawn top-down.
                    pixels[(Size - 1 - py) * Size + px] = colour;
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

#if UNITY_EDITOR
            texture.alphaIsTransparency = true;
#endif

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }
    }
}