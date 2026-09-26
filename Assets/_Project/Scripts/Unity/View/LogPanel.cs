// Assets/_Project/Scripts/Unity/View/LogPanel.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The full event log, as an overlay beside the history strip. L opens and
    /// closes it (GUI increments F and F2; rebuilt in G7b).
    /// </summary>
    /// <remarks>
    /// <b>An overlay, not a column.</b> It started as a 360-unit column that
    /// the board was framed around. The history strip (<see cref="HistoryStrip"/>)
    /// took that job, and this became the "everything, in words" view. It
    /// takes no width from the board when closed, and when open it covers the
    /// board's edge rather than shrinking it.
    ///
    /// <b>In the player's words, by round, newest first</b> (G7b). It reads
    /// <see cref="MatchLog"/>, whose lines are <see cref="EventText"/>'s, not
    /// the engine's own strings. Each round opens with a heading; inside it,
    /// the newest turn comes first, and each turn reads top to bottom as it
    /// happened, under the seat's name in its colour.
    ///
    /// <b>Refusals are the loudest lines.</b> A refused command is how a player
    /// learns a rule the screen did not teach, so it is tinted.
    ///
    /// The background blocks board clicks while open.
    /// </remarks>
    public sealed class LogPanel : MonoBehaviour
    {
        public const float Width = 380f;

        /// <summary>How many turns are drawn. The log keeps more; this is what is worth scrolling.</summary>
        private const int ShownTurns = 16;

        /// <summary>Whether the log is open. MatchBootstrap drives it from its inspector flag and L.</summary>
        public bool Expanded { get; set; }

        /// <summary>Called when the panel's close button is pressed.</summary>
        public System.Action CloseRequested { get; set; }

        private MatchLog _log;
        private RectTransform _panel;
        private RectTransform _content;
        private ScrollRect _scroll;
        private bool _dirty;
        private int _shownVersion = -1;
        private int _placedLayout = -1;

        public void Bind(RectTransform canvasRect, MatchLog log)
        {
            _log = log;
            if (_panel == null) Build(canvasRect);

            _shownVersion = -1;
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

            if (!_dirty || !Expanded || _log == null) return;
            _dirty = false;

            // Selection changes mark the whole HUD dirty; the log only changes
            // when a batch settles.
            if (_log.Version == _shownVersion) return;

            _shownVersion = _log.Version;
            Rebuild();
        }

        private void Rebuild()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            var turns = _log.Turns;
            int round = -1;

            for (int i = turns.Count - 1, shown = 0; i >= 0 && shown < ShownTurns; i--, shown++)
            {
                var turn = turns[i];

                if (turn.Round != round)
                {
                    round = turn.Round;
                    RoundHeading(round, first: shown == 0);
                }

                if (turn.Seat != PlayerColor.None)
                {
                    var heading = UiKit.Label(_content, RulesMarkup.For(EventText.TurnHeading(turn.Seat), linked: false),
                        UiTheme.FontSmall, UiTheme.Text, bold: true);
                    // Overflow, so the vertical slack a single line gets (G7a) is not
                    // needed: in a column it would pull the next line up into this one.
                    heading.overflowMode = TextOverflowModes.Overflow;
                    heading.margin = Vector4.zero;
                }

                // The turn in play reads at full strength; older ones recede.
                var colour = i == turns.Count - 1 ? UiTheme.Text : UiTheme.TextDim;

                foreach (var line in turn.Lines)
                {
                    var label = UiKit.Label(_content, line.Markup, UiTheme.FontSmall,
                        line.Refusal ? UiTheme.Reject : colour, wrap: true);
                    label.margin = new Vector4(12f, 0f, 0f, 0f);
                }

                if (turn.Lines.Count == 0 && turn.Seat != PlayerColor.None)
                {
                    var quiet = UiKit.Label(_content, "nothing yet", UiTheme.FontSmall, UiTheme.TextDim);
                    quiet.overflowMode = TextOverflowModes.Overflow;
                    quiet.margin = new Vector4(12f, 0f, 0f, 0f);
                    quiet.fontStyle = FontStyles.Italic;
                }
            }

            _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>"ROUND 3" in gold, with a little air above it unless it opens the list.</summary>
        private void RoundHeading(int round, bool first)
        {
            if (!first)
            {
                var gap = UiKit.Rect("gap", _content);
                UiKit.Size(gap, height: 8f);
            }

            var label = UiKit.Label(_content,
                "ROUND " + round.ToString(System.Globalization.CultureInfo.InvariantCulture),
                UiTheme.FontSmall, UiTheme.Gold, bold: true);
            label.characterSpacing = 6f;
            label.overflowMode = TextOverflowModes.Overflow;
            label.margin = Vector4.zero;
        }
    }
}
