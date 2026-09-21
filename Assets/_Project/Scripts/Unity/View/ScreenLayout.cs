// Assets/_Project/Scripts/Unity/View/ScreenLayout.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The one answer to "what shape is the screen, and what part of it may be
    /// drawn on" (MOBILE.md, M1). Every HUD layer reads it rather than asking
    /// <c>Screen</c> itself.
    /// </summary>
    /// <remarks>
    /// <b>Two layouts, one HUD.</b> The match HUD was built for a wide window:
    /// a rail down the left, a strip down the right, a bar across the top and
    /// a tray across the bottom. On a phone held upright there is no width to
    /// give away — a 220-unit rail is half the screen — so in portrait the two
    /// side panels become bands stacked above and below the board, and the tray
    /// wraps into rows. Which arrangement is in force is <see cref="IsPortrait"/>,
    /// and each layer asks it for its own numbers.
    ///
    /// <b>Portrait's reference resolution is 480×1040</b>, against 1920×1080 in
    /// landscape, and the scaler expands rather than matching an axis
    /// (<see cref="HudRoot"/>). On a 1440×3120 phone that puts one canvas unit
    /// at just under one Android dp, so the sizes the HUD was written in —
    /// a 48-unit button, 18-unit body text — land on the platform's own tap
    /// target and type scales without a second set of numbers. A short, wide
    /// portrait window (a tablet) gets extra canvas width instead of larger
    /// widgets, which is what Expand buys.
    ///
    /// <b>The safe area is honoured by insetting the HUD, not the camera.</b>
    /// <see cref="HudRoot"/> parents every layer inside a rect that matches
    /// <c>Screen.safeArea</c>, so a punch-hole camera or a gesture bar takes
    /// its bite out of the chrome and never out of a button. The board is
    /// framed inside the same rectangle — <c>MatchBootstrap.FrameCamera</c>
    /// adds these insets to what the panels reserve — so nothing playable hides
    /// under a system bar either.
    ///
    /// <b>Touch is a different pointer, not a smaller one</b>
    /// (<see cref="Touch"/>): there is no hover to lean on and no right button,
    /// so keyboard hints are dropped from labels and anything that only opened
    /// on hover gets a tap as well.
    ///
    /// Static, deliberately: it holds no state a match owns, every layer needs
    /// it while it builds itself, and it is sampled once a frame from one
    /// place. <see cref="Version"/> ticks whenever an answer changes, which is
    /// what the composition root watches to re-frame and rebuild.
    /// </remarks>
    public static class ScreenLayout
    {
        public const float PortraitReferenceWidth = 480f;
        public const float PortraitReferenceHeight = 1040f;
        public const float LandscapeReferenceWidth = 1920f;
        public const float LandscapeReferenceHeight = 1080f;

        /// <summary>
        /// Below this width-over-height ratio the portrait arrangement is used.
        /// </summary>
        /// <remarks>
        /// Not 1.0: a square-ish window has the width for the side panels and
        /// reads better with them, and a window dragged across an exact square
        /// would otherwise rebuild the whole HUD twice on the way.
        /// </remarks>
        private const float PortraitAspect = 0.88f;

        /// <summary>The ratio the portrait layout is left again, so a drag across the line cannot flicker.</summary>
        private const float LandscapeAspect = 0.96f;

        private static int _sampledWidth;
        private static int _sampledHeight;
        private static Rect _safePixels = new Rect(0f, 0f, 1f, 1f);
        private static float _scale = 1f;

        /// <summary>True while the HUD is in its upright arrangement.</summary>
        public static bool IsPortrait { get; private set; }

        /// <summary>Ticks whenever anything here changes. Cheap to compare every frame.</summary>
        public static int Version { get; private set; }

        /// <summary>
        /// True when the primary pointer is a finger: no hover, no second
        /// button, no keys.
        /// </summary>
        /// <remarks>
        /// A laptop with a touchscreen keeps its mouse, so the test is "touch
        /// and no mouse", not "touch". <c>Input.mousePresent</c> is false on
        /// phones and tablets and true on desktops, including the editor, which
        /// is what makes the editor show the desktop affordances while a Game
        /// view is squeezed into a phone shape.
        /// </remarks>
        public static bool Touch { get; private set; }

        /// <summary>Screen pixels per canvas unit, as the scaler last set it.</summary>
        public static float ScaleFactor => _scale;

        /// <summary>Canvas units the system takes off the left edge (a landscape notch).</summary>
        public static float SafeLeft { get; private set; }

        /// <summary>Canvas units the system takes off the right edge.</summary>
        public static float SafeRight { get; private set; }

        /// <summary>Canvas units the system takes off the top edge (the status bar, the camera).</summary>
        public static float SafeTop { get; private set; }

        /// <summary>Canvas units the system takes off the bottom edge (the gesture bar).</summary>
        public static float SafeBottom { get; private set; }

        /// <summary>The safe area in screen pixels, as Unity reports it.</summary>
        public static Rect SafePixels => _safePixels;

        /// <summary>
        /// True where the screen's size and mode belong to the device, not to
        /// the player (MOBILE.md, M7).
        /// </summary>
        /// <remarks>
        /// A phone has no window to size and no windowed mode to leave, so the
        /// display settings' resolution and screen mode are not choices there —
        /// and acting on them is worse than useless: <c>Screen.SetResolution</c>
        /// on Android really does hand the compositor a surface of that shape,
        /// which is then stretched onto the screen the device actually has.
        /// VSync and the frame cap are still real settings and still applied.
        /// </remarks>
        public static bool FixedScreen => Application.isMobilePlatform;

        /// <summary>The reference resolution the canvas scaler should be on.</summary>
        public static Vector2 Reference => IsPortrait
            ? new Vector2(PortraitReferenceWidth, PortraitReferenceHeight)
            : new Vector2(LandscapeReferenceWidth, LandscapeReferenceHeight);

        /// <summary>Picks between a landscape value and a portrait one.</summary>
        public static float Pick(float landscape, float portrait) => IsPortrait ? portrait : landscape;

        /// <summary>The same, for a word that has to be shorter upright.</summary>
        public static string Pick(string landscape, string portrait) => IsPortrait ? portrait : landscape;

        /// <summary>
        /// Re-reads the screen. Called once a frame by <see cref="HudRoot"/>,
        /// before anything else has had a chance to lay itself out.
        /// </summary>
        /// <param name="scaleFactor">
        /// The canvas scale factor. Passed in rather than read back, because
        /// the scaler writes it in its own Update and the value here has to
        /// agree with the one the layers are being sized by this frame.
        /// </param>
        public static void Sample(float scaleFactor)
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            var safe = Screen.safeArea;

            // A zero-sized safe area is what a minimised window reports; taking
            // it would inset the whole HUD out of existence.
            if (safe.width < 1f || safe.height < 1f) safe = new Rect(0f, 0f, width, height);

            bool touch = Input.touchSupported && !Input.mousePresent;

            bool changed =
                width != _sampledWidth ||
                height != _sampledHeight ||
                safe != _safePixels ||
                touch != Touch ||
                !Mathf.Approximately(scaleFactor, _scale);

            if (!changed) return;

            _sampledWidth = width;
            _sampledHeight = height;
            _safePixels = safe;
            _scale = Mathf.Max(0.01f, scaleFactor);
            Touch = touch;

            float aspect = (float)width / height;
            IsPortrait = IsPortrait ? aspect < LandscapeAspect : aspect < PortraitAspect;

            SafeLeft = safe.xMin / _scale;
            SafeRight = (width - safe.xMax) / _scale;
            SafeTop = (height - safe.yMax) / _scale;
            SafeBottom = safe.yMin / _scale;

            Version++;
        }

        /// <summary>
        /// Drops a keyboard hint from a label when there is no keyboard
        /// (M4). "ROLL  <i>Space</i>" is a promise a phone cannot keep.
        /// </summary>
        public static string Key(string hint) => Touch ? "" : hint;

        /// <summary>
        /// The same, for a hint already wrapped in markup: returns
        /// <paramref name="markup"/> on a desktop and nothing on a phone.
        /// </summary>
        public static string KeyMarkup(string markup) => Touch ? "" : markup;
    }
}
