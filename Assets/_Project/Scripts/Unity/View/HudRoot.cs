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
    /// <b>Reference resolution 1920×1080, match 0.5</b> (ADR-0008). Pixel sizes
    /// in HUD code mean "at 1080p" and scale with the window, which is what
    /// closes PRESENTATION §7's "OnGUI does not scale with resolution" item —
    /// for everything drawn here. The OnGUI panel keeps its fixed pixels until
    /// it is deleted; two scaling models coexist during the side-by-side
    /// period, and that is expected, not a bug.
    ///
    /// The EventSystem gets a <see cref="StandaloneInputModule"/> because the
    /// project is on the legacy Input Manager (ADR-0008 §7), as is every
    /// <c>Input.*</c> call in MatchBootstrap. Nothing raycasts against the HUD
    /// yet — increment A is labels only — but the scaffold is built once so
    /// the interactive increments mount without rework.
    /// </remarks>
    public sealed class HudRoot : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _matchLayer;
        private Canvas _canvas;

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

        public bool MatchLayerVisible
        {
            get => MatchLayer.gameObject.activeSelf;
            set { if (MatchLayer.gameObject.activeSelf != value) MatchLayer.gameObject.SetActive(value); }
        }

        /// <summary>The canvas rect every HUD element parents under. Built on first use.</summary>
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
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            // One EventSystem per scene, ever. Unity logs a warning and
            // disables duplicates, so check before creating.
            // FindAnyObjectByType: Unity 6.6 deprecated FindFirstObjectByType,
            // and "is there one at all" does not care which one is found.
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.transform.SetParent(transform, false);
                events.AddComponent<EventSystem>();
                events.AddComponent<StandaloneInputModule>();
            }

            _root = (RectTransform)go.transform;

            var layer = new GameObject("match_hud", typeof(RectTransform));
            _matchLayer = (RectTransform)layer.transform;
            _matchLayer.SetParent(_root, false);
            _matchLayer.anchorMin = Vector2.zero;
            _matchLayer.anchorMax = Vector2.one;
            _matchLayer.offsetMin = Vector2.zero;
            _matchLayer.offsetMax = Vector2.zero;
            _matchLayer.SetAsFirstSibling();
        }
    }
}