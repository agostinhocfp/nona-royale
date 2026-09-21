// Assets/_Project/Scripts/Unity/View/LogPanel.cs
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The full text log, as an overlay beside the history strip. L opens and
    /// closes it (GUI increments F and F2).
    /// </summary>
    /// <remarks>
    /// <b>An overlay, not a column.</b> It started as a 360-unit column that
    /// the board was framed around. The history strip (<see cref="HistoryStrip"/>)
    /// took that job, and this became the "everything, in words" view. It
    /// takes no width from the board when closed, and when open it covers the
    /// board's edge rather than shrinking it.
    ///
    /// <b>Refusals are the loudest lines.</b> A refused command is how a player
    /// learns a rule the screen did not teach, so it is tinted.
    ///
    /// The background blocks board clicks while open.
    /// </remarks>
    public sealed class LogPanel : MonoBehaviour
    {
        public const float Width = 380f;
        private const int Lines = 80;

        /// <summary>Whether the log is open. MatchBootstrap drives it from its inspector flag and L.</summary>
        public bool Expanded { get; set; }

        /// <summary>Called when the panel's close button is pressed.</summary>
        public System.Action CloseRequested { get; set; }

        private IControlPanelHost _host;
        private RectTransform _panel;
        private RectTransform _content;
        private ScrollRect _scroll;
        private bool _dirty;
        private int _shownCount = -1;
        private string _shownLast;
        private int _placedLayout = -1;

        public void Bind(RectTransform canvasRect, IControlPanelHost host)
        {
            _host = host;
            if (_panel == null) Build(canvasRect);

            _shownCount = -1;
            _dirty = true;
        }

        public void MarkDirty() => _dirty = true;

        private void Build(RectTransform canvasRect)
        {
            _panel = UiKit.Rect("log_overlay", canvasRect);
            _panel.anchorMin = new Vector2(1f, 0f);
            _panel.anchorMax = new Vector2(1f, 1f);
            _panel.pivot = new Vector2(1f, 0.5f);
            Place();
            UiKit.Panel(_panel, blocksPointer: true, fill: UiTheme.WithAlpha(UiTheme.Charcoal, 0.98f));

            var body = UiKit.Rect("body", _panel);
            UiKit.Stretch(body);
            UiKit.Column(body, 6f, 16);

            var header = UiKit.Rect("header", body);
            UiKit.Row(header, 8f);
            UiKit.Size(header, height: 30f);
            UiKit.Size(UiKit.Heading(header, "Event log"), flexibleWidth: 1f);
            UiKit.Fixed(UiKit.Button(header, $"Close{ScreenLayout.KeyMarkup("  <size=70%>L</size>")}",
                () => CloseRequested?.Invoke(), size: UiTheme.FontSmall), 100f, ScreenLayout.Pick(28f, 40f));

            var scrollRect = UiKit.Rect("scroll", body);
            UiKit.Size(scrollRect, flexibleHeight: 1f);
            _scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;

            var viewport = UiKit.Rect("viewport", scrollRect);
            UiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = UiKit.Rect("content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;
            UiKit.Column(_content, 3f);
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll.viewport = viewport;
            _scroll.content = _content;

            _panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Where the overlay opens. Upright there is no column beside the
        /// history to open into, so the log takes the whole board area
        /// instead (MOBILE.md, M5) — it is a reading overlay, and nothing
        /// under it is being played while it is up.
        /// </summary>
        private void Place()
        {
            if (_panel == null) return;

            float side = HistoryStrip.ReservedWidth;

            _panel.offsetMin = new Vector2(
                ScreenLayout.Pick(-side - Width, -ScreenLayout.Reference.x + 12f),
                ActionTray.ReservedHeight + HistoryStrip.ReservedHeight);
            _panel.offsetMax = new Vector2(ScreenLayout.Pick(-side, -12f),
                -TurnStrip.ReservedHeight - SquadRail.ReservedHeight);
        }

        private void LateUpdate()
        {
            if (_panel == null) return;

            if (_placedLayout != ScreenLayout.Version)
            {
                _placedLayout = ScreenLayout.Version;
                Place();
            }

            if (_panel.gameObject.activeSelf != Expanded)
            {
                _panel.gameObject.SetActive(Expanded);
                if (Expanded)
                {
                    _panel.SetAsLastSibling();
                    _dirty = true;
                }
            }

            if (!_dirty || !Expanded) return;
            _dirty = false;

            var log = _host?.Log;
            if (log == null) return;

            // Selection changes mark the whole HUD dirty; the log only changes
            // when an event arrives.
            string last = log.Count > 0 ? log[log.Count - 1] : null;
            if (log.Count == _shownCount && last == _shownLast) return;

            _shownCount = log.Count;
            _shownLast = last;
            Rebuild(log);
        }

        private void Rebuild(System.Collections.Generic.IReadOnlyList<string> log)
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            for (int i = log.Count - 1, n = 0; i >= 0 && n < Lines; i--, n++)
            {
                string line = log[i];
                bool rejected = line.StartsWith("rejected:", System.StringComparison.Ordinal);

                var colour = rejected ? UiTheme.Reject : n == 0 ? UiTheme.Text : UiTheme.TextDim;
                var label = UiKit.Label(_content, line, UiTheme.FontSmall, colour, wrap: true);
                label.richText = false;
            }

            _scroll.verticalNormalizedPosition = 1f;
        }
    }
}
