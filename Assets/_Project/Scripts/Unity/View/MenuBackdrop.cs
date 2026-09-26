// Assets/_Project/Scripts/Unity/View/MenuBackdrop.cs
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// What the menus sit on when there is no match: a dark room lit from
    /// below by a warm pool, a faint Deco sunburst rising out of it, haze in
    /// the light, a spotlight that sweeps across it now and then, a vignette,
    /// and one gilt hairline framing the screen (LAUNCH_UI_PASS.md, G6d and
    /// G6e).
    /// </summary>
    /// <remarks>
    /// <b>It replaces the empty board</b> behind the title, setup, draft and
    /// guide. The designer: the board there "doesn't look premium". A board
    /// with no pieces on it reads as a game not yet loaded, and the title's
    /// flat board and setup's tilted one did not agree with each other either.
    /// The room is still built underneath, because the first deal wants it
    /// ready; this layer is opaque and covers it.
    ///
    /// <b>UI, not world.</b> It is a full-canvas layer under the safe-area
    /// root (<see cref="HudRoot.BackdropLayer"/>), edge to edge on a phone, so
    /// it does not depend on the camera's framing, its tilt, the lighting
    /// setting or the room's own visibility rules. Every card draws over it.
    ///
    /// <b>Drawn in code, like the rest of the chrome.</b> The pool reuses
    /// <see cref="DecoSprites.Glow"/> and the haze <see cref="BoardArt.Haze"/>;
    /// the sunburst, the beam and the vignette are rasterised once here. All of it is quiet by design: gold at 7% for the rays and 10% for
    /// the pool, so the wordmark and the cards stay the brightest things on
    /// screen (ART_DIRECTION §3's gold budget, G3's "stop the gold
    /// performing").
    ///
    /// <b>Two things give the light some life</b> (G6e). <i>Haze:</i> three
    /// slow puffs over the pool on the board lighting's loop (G3), so the light
    /// has something to fall through. <i>A beam:</i> every
    /// <see cref="SweepCycle"/> seconds a soft wedge of warm light swings
    /// across the sunburst from its origin, like a spotlight crossing a Deco
    /// ceiling, and fades out at either end.
    ///
    /// <b>No film grain</b> (G6e, removed after Play Mode). It was meant to
    /// sit at a few percent, but the project renders in linear colour space,
    /// where a low-alpha white over near-black lands several times brighter
    /// than the same number suggests: the screen came out as static. The
    /// designer preferred the clean, minimal backdrop, so it went rather than
    /// being tuned down. The same linear lift is why every alpha here is small.
    ///
    /// <b>Reduced motion stills it all:</b> the haze sits at home at its base
    /// strength and the beam does not run.
    /// Nothing else here moves. Time is unscaled, so the menus never depend
    /// on what the pause menu last did to the clock.
    ///
    /// Shown whenever there is no match (<c>MatchBootstrap.Update</c>). A card
    /// opened over a match — pause, NEW MATCH, results — shows the match
    /// behind it instead.
    /// </remarks>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        private const float PoolAlpha = 0.10f;
        private const float RaysAlpha = 0.07f;
        private const float VignetteAlpha = 0.85f;
        private const float FrameAlpha = 0.28f;

        /// <summary>Rays across the half circle the sunburst fans through.</summary>
        private const int RayCount = 36;

        // ── Haze (G6e) ───────────────────────────────────────────────────

        private const float HazeAlpha = 0.06f;
        private const float HazePeriod = 46f;
        private const float HazeReach = 0.05f;
        private const float HazeBreath = 0.25f;

        /// <summary>Each puff: where it sits on the screen (0–1 across and up), its size, its phase in the loop.</summary>
        private static readonly (Vector2 at, Vector2 size, float phase)[] Puffs =
        {
            (new Vector2(0.50f, 0.14f), new Vector2(1200f, 760f), 0.00f),
            (new Vector2(0.22f, 0.22f), new Vector2(960f, 640f), 0.37f),
            (new Vector2(0.78f, 0.20f), new Vector2(960f, 680f), 0.71f),
        };

        // ── The beam (G6e) ───────────────────────────────────────────────

        private const float BeamAlpha = 0.06f;

        /// <summary>Seconds from one sweep's start to the next.</summary>
        private const float SweepCycle = 20f;

        /// <summary>Seconds a sweep takes; the rest of the cycle is dark.</summary>
        private const float SweepSeconds = 7f;

        /// <summary>Degrees from vertical the beam starts and ends at, either side.</summary>
        private const float SweepAngle = 75f;

        /// <summary>The beam's half-width: the standard deviation of its angular falloff, in degrees.</summary>
        private const float BeamSpread = 4f;

        private static Sprite _rays;
        private static Sprite _beamSprite;
        private static Sprite _vignette;

        private RectTransform _root;
        private RectTransform _frame;
        private RectTransform _beam;
        private Image _beamImage;
        private readonly RectTransform[] _puffs = new RectTransform[3];
        private readonly Image[] _puffImages = new Image[3];

        private int _placedVersion = -1;
        private Vector2 _placedSize = new Vector2(-1f, -1f);

        /// <summary>Whether the backdrop is on screen.</summary>
        public bool Visible
        {
            get => _root != null && _root.gameObject.activeSelf;
            set
            {
                if (_root != null && _root.gameObject.activeSelf != value) _root.gameObject.SetActive(value);
            }
        }

        /// <summary>Builds the backdrop into <paramref name="layer"/>. Safe to call more than once.</summary>
        public void Build(RectTransform layer)
        {
            if (_root != null || layer == null) return;

            _root = UiKit.Rect("menu_backdrop", layer);
            UiKit.Stretch(_root);
            UiKit.Fill(_root, UiTheme.Obsidian);

            // The pool: a warm light on the floor, centred on the bottom edge,
            // wider than the screen so its falloff never shows an edge.
            var pool = UiKit.Rect("pool", _root);
            pool.anchorMin = new Vector2(-0.15f, -0.55f);
            pool.anchorMax = new Vector2(1.15f, 0.55f);
            pool.offsetMin = pool.offsetMax = Vector2.zero;
            UiKit.Fill(pool, UiTheme.WithAlpha(UiTheme.GoldBright, PoolAlpha)).sprite = DecoSprites.Glow;

            // Haze in the pool's light.
            for (int i = 0; i < Puffs.Length; i++)
            {
                var puff = UiKit.Rect($"haze_{i}", _root);
                puff.anchorMin = puff.anchorMax = Puffs[i].at;
                puff.pivot = new Vector2(0.5f, 0.5f);
                puff.sizeDelta = Puffs[i].size;

                _puffs[i] = puff;
                _puffImages[i] = UiKit.Fill(puff, UiTheme.WithAlpha(UiTheme.Haze, HazeAlpha));
                _puffImages[i].sprite = BoardArt.Haze;
            }

            // The sunburst, rising out of the pool.
            var rays = UiKit.Rect("rays", _root);
            rays.anchorMin = new Vector2(-0.075f, 0f);
            rays.anchorMax = new Vector2(1.075f, 1f);
            rays.offsetMin = rays.offsetMax = Vector2.zero;
            UiKit.Fill(rays, UiTheme.WithAlpha(UiTheme.Gold, RaysAlpha)).sprite = Rays;

            // The beam, pivoting on the sunburst's origin. Sized in Place.
            _beam = UiKit.Rect("beam", _root);
            _beam.anchorMin = _beam.anchorMax = new Vector2(0.5f, 0f);
            _beam.pivot = new Vector2(0.5f, 0f);
            _beamImage = UiKit.Fill(_beam, UiTheme.WithAlpha(UiTheme.GoldBright, 0f));
            _beamImage.sprite = BeamSprite;

            // The edges fall away to black.
            var vignette = UiKit.Rect("vignette", _root);
            UiKit.Stretch(vignette);
            UiKit.Fill(vignette, UiTheme.WithAlpha(Color.black, VignetteAlpha)).sprite = Vignette;

            // One gilt hairline framing the screen, with the corner fans the
            // floating cards carry.
            _frame = UiKit.Rect("frame", _root);
            UiKit.Sliced(_frame, DecoSprites.PanelEdge, UiTheme.WithAlpha(UiTheme.Gold, FrameAlpha));
            UiKit.CornerFans(_frame, UiTheme.WithAlpha(UiTheme.Gold, FrameAlpha * UiTheme.FanAlpha * 2f));

            Place();
            _root.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_root == null || !_root.gameObject.activeSelf) return;

            float time = Time.unscaledTime;
            bool reduced = UiTween.ReducedMotion;

            Drift(time, reduced);
            Sweep(time, reduced);
        }

        private void LateUpdate()
        {
            if (_root == null) return;

            if (_placedVersion != ScreenLayout.Version || _root.rect.size != _placedSize) Place();
        }

        /// <summary>Insets the frame for the screen's shape, and sizes the beam and the haze to the screen.</summary>
        private void Place()
        {
            _placedVersion = ScreenLayout.Version;
            _placedSize = _root.rect.size;

            if (_frame != null) UiKit.Stretch(_frame, ScreenLayout.Pick(28f, 14f));

            // The beam reaches the top of the screen from the bottom edge.
            float height = Mathf.Max(1f, _placedSize.y);
            if (_beam != null) _beam.sizeDelta = new Vector2(height * 0.5f, height);

            // Upright the haze puffs are too wide for the screen at full size.
            float puffScale = ScreenLayout.Pick(1f, 0.6f);
            for (int i = 0; i < _puffs.Length; i++)
                if (_puffs[i] != null) _puffs[i].sizeDelta = Puffs[i].size * puffScale;
        }

        /// <summary>
        /// The haze's slow loop, turn and swell: the same motion as the board's
        /// haze (<c>SceneLighting.Drift</c>, G3), in canvas units.
        /// </summary>
        private void Drift(float time, bool reduced)
        {
            for (int i = 0; i < _puffs.Length; i++)
            {
                var puff = _puffs[i];
                if (puff == null) continue;

                float phase = Puffs[i].phase;
                var colour = UiTheme.WithAlpha(UiTheme.Haze, HazeAlpha);

                if (reduced)
                {
                    puff.anchoredPosition = Vector2.zero;
                    puff.localRotation = Quaternion.identity;
                    _puffImages[i].color = colour;
                    continue;
                }

                double cycle = time / HazePeriod + phase;
                float angle = (float)(2.0 * System.Math.PI * cycle);
                float reach = puff.sizeDelta.x * HazeReach;

                // A Lissajous loop, so the path never visibly repeats.
                puff.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Sin(angle * 0.5f + 1.3f)) * reach;
                puff.localRotation = Quaternion.Euler(0f, 0f, (float)((cycle * 0.25 % 1.0) * 360.0));

                colour.a *= LightPulse.Breath(time, HazePeriod * 0.37f, HazeBreath, phase, false);
                _puffImages[i].color = colour;
            }
        }

        /// <summary>
        /// One sweep every <see cref="SweepCycle"/> seconds: the beam swings
        /// from one side to the other, easing in and out, and its light rises
        /// and falls with it so neither end snaps on or off.
        /// </summary>
        private void Sweep(float time, bool reduced)
        {
            if (_beam == null) return;

            float t = (time % SweepCycle) / SweepSeconds;
            bool running = !reduced && t < 1f;

            if (!running)
            {
                if (_beamImage.color.a > 0f) _beamImage.color = UiTheme.WithAlpha(UiTheme.GoldBright, 0f);
                return;
            }

            float eased = t * t * (3f - 2f * t);
            _beam.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(SweepAngle, -SweepAngle, eased));
            _beamImage.color = UiTheme.WithAlpha(UiTheme.GoldBright, BeamAlpha * Mathf.Sin(Mathf.PI * t));
        }

        // ── Sprites ──────────────────────────────────────────────────────

        /// <summary>
        /// A half sunburst, its origin at the bottom centre: soft rays, every
        /// other one at half strength, fading in past the pool's core and out
        /// before the top of the screen, where the title's own glow sits.
        /// </summary>
        private static Sprite Rays => _rays != null ? _rays : (_rays = BuildRays(1024, 512));

        /// <summary>A soft wedge of light pointing straight up from its bottom centre, for the sweep.</summary>
        private static Sprite BeamSprite => _beamSprite != null ? _beamSprite : (_beamSprite = BuildBeam(256, 512));

        /// <summary>Clear in the middle, dark at the corners, stretched to the screen.</summary>
        private static Sprite Vignette => _vignette != null ? _vignette : (_vignette = BuildVignette(256));

        private static Sprite BuildRays(int width, int height)
        {
            float half = width * 0.5f;

            return DecoSprites.Rasterize(width, height, (px, py) =>
            {
                float dx = (px - half) / half;
                float dy = py / height;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float theta = Mathf.Atan2(dy, dx);

                float phase = theta / Mathf.PI * RayCount;
                float c = Mathf.Cos(phase * Mathf.PI);
                float ray = Mathf.Pow(c * c, 10f);
                if ((Mathf.FloorToInt(phase + 0.5f) & 1) == 1) ray *= 0.45f;

                float envelope = Smooth(0.08f, 0.30f, r) * (1f - Smooth(0.45f, 0.92f, r));
                return Mathf.Clamp01(ray * envelope);
            }, 100f, Vector4.zero, new Vector2(0.5f, 0f));
        }

        /// <remarks>
        /// Measured in heights, so the wedge's angle is true whatever the
        /// sprite's aspect: a width of half the height holds the falloff out to
        /// about 14° either side, over three spreads.
        /// </remarks>
        private static Sprite BuildBeam(int width, int height)
        {
            float half = width * 0.5f;
            float spread = BeamSpread * Mathf.Deg2Rad;

            return DecoSprites.Rasterize(width, height, (px, py) =>
            {
                float dx = (px - half) / height;
                float dy = py / height;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float theta = Mathf.Atan2(dx, dy) / spread;

                float wedge = Mathf.Exp(-theta * theta);
                float envelope = Smooth(0.06f, 0.25f, r) * (1f - Smooth(0.50f, 1.00f, r));
                return Mathf.Clamp01(wedge * envelope);
            }, 100f, Vector4.zero, new Vector2(0.5f, 0f));
        }

        private static Sprite BuildVignette(int size)
        {
            float half = size * 0.5f;

            return DecoSprites.Rasterize(size, size, (px, py) =>
            {
                float u = (px - half) / half;
                float v = (py - half) / half;
                return Smooth(0.55f, 1.35f, Mathf.Sqrt(u * u + v * v));
            }, 100f, Vector4.zero);
        }

        private static float Smooth(float from, float to, float x)
        {
            float t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }
    }
}
