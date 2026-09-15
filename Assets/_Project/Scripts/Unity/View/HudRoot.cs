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

        /// <summary>The canvas rect every HUD element parents under. Built on first use.</summary>
        public RectTransform Root
        {
            get
            {
                if (_root == null) Build();
                return _root;
            }
        }

        private void Build()
        {
            var go = new GameObject("HUD", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            // One EventSystem per scene, ever. Unity logs a warning and
            // disables duplicates, so check before creating.
            // (FindFirstObjectByType is Unity 2023.1+/Unity 6; on an older
            // editor this line becomes FindObjectOfType<EventSystem>().)
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.transform.SetParent(transform, false);
                events.AddComponent<EventSystem>();
                events.AddComponent<StandaloneInputModule>();
            }

            _root = (RectTransform)go.transform;
        }
    }
}