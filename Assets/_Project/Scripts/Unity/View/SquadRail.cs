// Assets/_Project/Scripts/Unity/View/SquadRail.cs
using NonaRoyale.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The left rail: every seat's squad, each operator shown as waiting, on
    /// the board or home, with health and statuses (GUI increment F).
    /// </summary>
    /// <remarks>
    /// <b>It answers "which of my pieces are waiting"</b>, PRESENTATION §2's
    /// deployed-operators row and question 2 of the stranger test, without
    /// having to find three small pieces in a corner of the board. It also
    /// shows the other seats, so a player can size up an opponent's squad
    /// without hovering over anything.
    ///
    /// <b>The current seat's rows are buttons.</b> A click does what a click on
    /// the piece does: it deploys a piece that can deploy, and otherwise
    /// selects it. Both go through the host's intents, so the rail, the board
    /// and the dev panel cannot disagree.
    ///
    /// <b>Every word is an engine answer</b> (PRESENTATION §1): "home" is
    /// <c>IsHome</c>, statuses are <c>ActiveStatusesOn</c>, "ready to deploy"
    /// is <c>CanDeploy</c>.
    ///
    /// Rebuilt when marked dirty, like the dev panel. The background blocks
    /// board clicks, and text never catches the pointer.
    /// </remarks>
    public sealed class SquadRail : MonoBehaviour
    {
        public const float Width = 290f;
        private const float RowHeight = 52f;

        /// <summary>Canvas units the rail claims from the left edge.</summary>
        public static float ReservedWidth => Width;

        public bool Visible { get; set; } = true;

        private IControlPanelHost _host;
        private RectTransform _rail;
        private RectTransform _content;
        private bool _dirty;

        public void Bind(RectTransform canvasRect, IControlPanelHost host)
        {
            _host = host;
            if (_rail == null) Build(canvasRect);
            _dirty = true;
        }

        public void MarkDirty() => _dirty = true;

        private void Build(RectTransform canvasRect)
        {
            _rail = UiKit.Rect("squad_rail", canvasRect);
            _rail.anchorMin = new Vector2(0f, 0f);
            _rail.anchorMax = new Vector2(0f, 1f);
            _rail.pivot = new Vector2(0f, 0.5f);
            _rail.offsetMin = new Vector2(0f, 0f);
            _rail.offsetMax = new Vector2(Width, -TurnStrip.ReservedHeight);
            UiKit.Dock(_rail, true, RectTransform.Edge.Right);

            var scroll = _rail.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = UiKit.Rect("viewport", _rail);
            UiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = UiKit.Rect("content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;

            UiKit.Column(_content, 4f, 10).padding.right = 16; // clear of the double rule
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = _content;
        }

        private void LateUpdate()
        {
            if (_rail == null) return;

            if (_rail.gameObject.activeSelf != Visible)
            {
                _rail.gameObject.SetActive(Visible);
                if (Visible) _dirty = true;
            }

            if (!_dirty || !Visible) return;

            _dirty = false;
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

            var match = _host?.Match;
            if (match == null) return;

            var engine = match.Engine;

            foreach (var seat in match.Players)
            {
                bool playing = !engine.MatchOver && seat.Color == engine.CurrentPlayer.Color;
                SeatHeader(seat, playing, engine.EnergyCap, engine);

                // A CPU seat's rows are never buttons, even on its own turn (BOT2).
                bool commandable = playing && !_host.CpuTurn;
                foreach (var op in seat.Operators)
                    OperatorRow(op, commandable, engine);

                UiKit.Space(_content, height: 8f);
            }
        }

        private void SeatHeader(PlayerState seat, bool playing, int cap, Core.GameEngine engine)
        {
            var colour = BoardLayout.ColourOf(seat.Color);

            // The seat to play gets a gilt-edged plate; the others sit on a
            // brass underline.
            var header = UiKit.Rect($"seat_{seat.Color}", _content);
            if (playing)
            {
                UiKit.Sliced(header, DecoSprites.ButtonFill, UiTheme.PanelRaised);
                UiKit.Overlay(header, DecoSprites.ButtonEdge, UiTheme.Gold);
            }
            else
            {
                var underline = UiKit.Rect("underline", header);
                UiKit.Fill(underline, UiTheme.WithAlpha(UiTheme.Line, 0.6f));
                underline.anchorMin = Vector2.zero;
                underline.anchorMax = new Vector2(1f, 0f);
                underline.pivot = new Vector2(0.5f, 0f);
                underline.sizeDelta = new Vector2(0f, 1f);
                underline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }

            UiKit.Size(header, height: 34f);
            var row = UiKit.Row(header, 8f);
            row.padding = new RectOffset(8, 8, 0, 0);
            row.childAlignment = TextAnchor.MiddleLeft;

            UiKit.Diamond(header, colour, playing ? 11f : 8f, playing ? 17f : 12f);

            int home = 0;
            foreach (var op in seat.Operators) if (engine.IsHome(op)) home++;

            string tag = _host.SeatTag(seat.Color);
            var name = UiKit.Label(header,
                $"<color=#{UiTheme.Hex(UiTheme.Readable(colour))}>{seat.Color.ToString().ToUpperInvariant()}</color>" +
                (tag != null ? $" <size=62%><color=#{UiTheme.Hex(UiTheme.Cyan)}>{tag}</color></size>" : "") +
                (playing ? $"  <size=75%><color=#{UiTheme.Hex(UiTheme.GoldBright)}>PLAYING</color></size>" : ""),
                UiTheme.FontBody, bold: true);
            UiKit.Size(name, flexibleWidth: 1f);

            UiKit.Label(header,
                $"<color=#{UiTheme.Hex(UiTheme.TextDim)}>home</color> {home}/{seat.Operators.Count}   " +
                $"<color=#{UiTheme.Hex(UiTheme.Cyan)}>{seat.Energy}</color><color=#{UiTheme.Hex(UiTheme.TextDim)}>/{cap}e</color>",
                UiTheme.FontSmall, align: TextAlignmentOptions.MidlineRight);
        }

        private void OperatorRow(OperatorState op, bool playing, Core.GameEngine engine)
        {
            bool home = engine.IsHome(op);
            bool deployable = playing && engine.CanDeploy(op);
            bool selected = ReferenceEquals(op, _host.SelectedOperator);

            RectTransform row;

            if (playing)
            {
                // The whole row is the button. Its label is left empty and the
                // row content is laid out on top of it.
                var button = UiKit.Button(_content, "", () =>
                    {
                        if (deployable) _host.Deploy(op);
                        else _host.ToggleOperator(op);
                    },
                    MarkDirty, selected: selected);
                row = (RectTransform)button.transform;
            }
            else
            {
                row = UiKit.Rect($"op_{op.Name}", _content);
                UiKit.Fill(row, Color.clear);
            }

            UiKit.Size(row, height: RowHeight);

            var layout = UiKit.Row(row, 8f);
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childForceExpandHeight = false;

            var seatColour = BoardLayout.ColourOf(op.Owner);
            var iconColour = op.IsInYard ? Color.Lerp(seatColour, UiTheme.PieceWaiting, 0.55f) : seatColour;
            UiKit.Icon(row, PieceShape.For(op), iconColour, 30f);

            var middle = UiKit.Rect("middle", row);
            UiKit.Column(middle, 2f);
            UiKit.Size(middle, flexibleWidth: 1f);

            string where = home ? "home"
                : deployable ? $"<color=#{UiTheme.Hex(UiTheme.Cyan)}>ready to deploy</color>"
                : op.IsInYard ? "waiting"
                : "on board";

            // Name alone on the first line, so it is never the part that
            // gets cut; the state and any statuses share the second.
            UiKit.Label(middle, op.Name, UiTheme.FontBody, playing || !op.IsInYard ? UiTheme.Text : UiTheme.TextDim);

            var tags = UiKit.Rect("tags", middle);
            UiKit.Row(tags, 4f);
            UiKit.Label(tags, where, 13f, UiTheme.TextDim);

            if (!op.IsInYard && !home)
            {
                foreach (var kind in engine.ActiveStatusesOn(op))
                    UiKit.Tag(tags, StatusPalette.Label(kind), StatusPalette.For(kind), 11f);
            }

            var right = UiKit.Rect("health", row);
            UiKit.Column(right, 3f).childAlignment = TextAnchor.MiddleRight;
            UiKit.Fixed(right, 64f);

            float fraction = (float)op.Health / Mathf.Max(1, op.MaxHealth);
            UiKit.Label(right, $"{op.Health}/{op.MaxHealth}", UiTheme.FontSmall, align: TextAlignmentOptions.MidlineRight, bold: true);
            UiKit.Bar(right, fraction, Color.Lerp(UiTheme.Danger, seatColour, fraction), 64f, 5f);
        }
    }
}
