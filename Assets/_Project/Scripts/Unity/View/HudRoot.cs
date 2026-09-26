// Assets/_Project/Scripts/Unity/View/HudRoot.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Builds the one runtime Canvas every HUD layer mounts on, plus the
    /// EventSystem that will drive interactive uGUI in later increments.
    /// </summary>
    /// <remarks>
    /// Built from code because MatchBootstrap's contract is one component on an
    /// empty GameObject — no prefabs, no scene wiring — and ADR-0008 keeps it.
    ///
    /// <b>Reference resolution 1920×1080, match 0.5</b> (ADR-0008) while the
    /// window is wide. Pixel sizes in HUD code mean "at 1080p" and scale with
    /// the window, which is what closes PRESENTATION §7's "OnGUI does not scale
    /// with resolution" item — for everything drawn here. The OnGUI panel keeps
    /// its fixed pixels until it is deleted; two scaling models coexist during
    /// the side-by-side period, and that is expected, not a bug.
    ///
    /// <b>Upright, the reference becomes 480×1040 and the scaler expands</b>
    /// (MOBILE.md, M1). A phone held upright has a tenth of the width a 1080p
    /// window has once the reference is applied, and the same numbers cannot
    /// mean both. Expand — rather than matching an axis — takes the smaller of
    /// the two ratios, so the canvas is never smaller than the reference on
    /// either axis and no widget is ever cut off by a screen that is the wrong
    /// shape; a short, wide portrait window simply gets more canvas width.
    /// <see cref="ScreenLayout"/> holds both sets of numbers and decides which
    /// is in force.
    ///
    /// <b>Everything mounts inside the safe area</b> (MOBILE.md, M2). The
    /// canvas fills the screen, but <see cref="Root"/> is a child rect pinned
    /// to <c>Screen.safeArea</c>, so a punch-hole camera, a status bar or a
    /// gesture bar eats into the margin rather than into a control. The player
    /// settings render outside the safe area on purpose — that is what lets the
    /// room and the felt run edge to edge behind the cut-out — and this rect is
    /// what keeps the chrome out of it.
    ///
    /// The EventSystem gets a <see cref="StandaloneInputModule"/> because the
    /// project is on the legacy Input Manager (ADR-0008 §7), as is every
    /// <c>Input.*</c> call in MatchBootstrap. That module raises touches as
    /// pointer events, so the whole HUD is tappable without a second input
    /// path.
    /// </remarks>
    public sealed class HudRoot : MonoBehaviour
    {
        private RectTransform _canvasRect;
        private RectTransform _root;
        private RectTransform _matchLayer;
        private RectTransform _backdropLayer;
        private Canvas _canvas;
        private CanvasScaler _scaler;

        private int _appliedVersion = -1;

        /// <summary>
        /// A full-canvas layer holding the in-match HUD: top bar, rail, tray,
        /// strip, labels, toasts (GUI increment J). Hidden on the title screen,
        /// so the room shows without the match's chrome. Full-screen cards
        /// (pause, setup, end, title) parent to <see cref="Root"/> and draw
        /// over it.
        /// </summary>
        public RectTransform MatchLayer
        {
            get
            {
                if (_root == null) Build();
                return _matchLayer;
            }
        }

        /// <summary>
        /// A full-canvas layer under everything else, edge to edge rather than
        /// inset to the safe area, for the menu backdrop (G6d). It sits behind
        /// the safe-area root, so every card and the match HUD draw over it.
        /// </summary>
        public RectTransform BackdropLayer
        {
            get
            {
                if (_root == null) Build();
                return _backdropLayer;
            }
        }

        public bool MatchLayerVisible
        {
            get => MatchLayer.gameObject.activeSelf;
            set { if (MatchLayer.gameObject.activeSelf != value) MatchLayer.gameObject.SetActive(value); }
        }

        /// <summary>
        /// The rect every HUD element parents under: the canvas, inset to the
        /// safe area. Built on first use.
        /// </summary>
        public RectTransform Root
        {
            get
            {
                if (_root == null) Build();
                return _root;
            }
        }

        /// <summary>
        /// Screen pixels per canvas unit, as the CanvasScaler last set it.
        /// Anything that turns HUD sizes into screen space (FrameCamera) reads
        /// this rather than repeating the scaler's arithmetic.
        /// </summary>
        /// <remarks>
        /// The scaler updates it in its own Update, so after a resize it can be
        /// one frame stale. Callers that cache it should compare against it
        /// every frame, which is what MatchBootstrap does.
        /// </remarks>
        public float ScaleFactor
        {
            get
            {
                if (_root == null) Build();
                return _canvas.scaleFactor;
            }
        }

        private void Build()
        {
            var go = new GameObject("HUD", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas = canvas;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                ScreenLayout.LandscapeReferenceWidth, ScreenLayout.LandscapeReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            _scaler = scaler;

            go.AddComponent<GraphicRaycaster>();

            // One EventSystem per scene, ever. Unity logs a warning and
            // disables duplicates, so check before creating.
            // FindAnyObjectByType: Unity 6.6 deprecated FindFirstObjectByType,
            // and "is there one at all" does not care which one is found.
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.transform.SetParent(transform, false);
                var system = events.AddComponent<EventSystem>();
                events.AddComponent<StandaloneInputModule>();

                // The default 10 pixels is under half a millimetre on a
                // 500-dpi phone, so a finger's natural wobble turned taps into
                // drags and the click was withdrawn. Two millimetres, from the
                // screen's own density, is a swipe on any device; a screen that
                // reports no density keeps the default (OPERATOR_GUIDE.md §4).
                if (Screen.dpi > 0f)
                    system.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt(Screen.dpi * 0.08f));
            }

            _canvasRect = (RectTransform)go.transform;

            // First child of the canvas, so it draws under the safe-area root.
            var backdrop = new GameObject("backdrop", typeof(RectTransform));
            _backdropLayer = (RectTransform)backdrop.transform;
            _backdropLayer.SetParent(_canvasRect, false);
            _backdropLayer.anchorMin = Vector2.zero;
            _backdropLayer.anchorMax = Vector2.one;
            _backdropLayer.offsetMin = Vector2.zero;
            _backdropLayer.offsetMax = Vector2.zero;

            var safe = new GameObject("safe_area", typeof(RectTransform));
            _root = (RectTransform)safe.transform;
            _root.SetParent(_canvasRect, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            var layer = new GameObject("match_hud", typeof(RectTransform));
            _matchLayer = (RectTransform)layer.transform;
            _matchLayer.SetParent(_root, false);
            _matchLayer.anchorMin = Vector2.zero;
            _matchLayer.anchorMax = Vector2.one;
            _matchLayer.offsetMin = Vector2.zero;
            _matchLayer.offsetMax = Vector2.zero;
            _matchLayer.SetAsFirstSibling();

            // Sampled before anything is built on it, so the first layout pass
            // already knows which arrangement it is in.
            Sample();
            Apply();
        }

        /// <summary>
        /// Re-reads the screen before anything lays out this frame, and applies
        /// a new shape to the scaler and the safe-area rect when it changed.
        /// </summary>
        /// <remarks>
        /// Update rather than LateUpdate: every layer's own LateUpdate rebuild
        /// has to see this frame's answers, and the composition root's
        /// <c>FrameCamera</c> check reads <see cref="ScreenLayout.Version"/>
        /// in the same pass.
        /// </remarks>
        private void Update()
        {
            if (_root == null) return;

            Sample();
            if (ScreenLayout.Version != _appliedVersion) Apply();
        }

        private void Sample() => ScreenLayout.Sample(_canvas != null ? _canvas.scaleFactor : 1f);

        private void Apply()
        {
            _appliedVersion = ScreenLayout.Version;

            _scaler.referenceResolution = ScreenLayout.Reference;

            // Expand in portrait (never cut a control off a narrow screen),
            // the long-standing half-and-half match in landscape.
            if (ScreenLayout.IsPortrait)
            {
                _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            }
            else
            {
                _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                _scaler.matchWidthOrHeight = 0.5f;
            }

            // The safe area, as fractions of the canvas. Written as anchors so
            // it follows a resize without arithmetic of its own.
            var safe = ScreenLayout.SafePixels;
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);

            _root.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
            _root.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
        }
    }
}