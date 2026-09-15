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
            UiKit.Frame(_rail, UiKit.Panel, true, RectTransform.Edge.Right);

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

            UiKit.Column(_content, 4f, 10);
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

                foreach (var op in seat.Operators)
                    OperatorRow(op, playing, engine);

                UiKit.Space(_content, height: 8f);
            }
        }

        private void SeatHeader(PlayerState seat, bool playing, int cap, Core.GameEngine engine)
        {
            var colour = BoardLayout.ColourOf(seat.Color);

            var header = UiKit.Rect($"seat_{seat.Color}", _content);
            UiKit.Fill(header, playing ? UiKit.PanelRaised : new Color(0f, 0f, 0f, 0f));
            UiKit.Size(header, height: 32f);
            var row = UiKit.Row(header, 8f);
            row.padding = new RectOffset(0, 6, 0, 0);
            row.childForceExpandHeight = true;

            var swatch = UiKit.Rect("swatch", header);
            UiKit.Fill(swatch, colour);
            UiKit.Size(swatch, playing ? 8f : 4f);

            int home = 0;
            foreach (var op in seat.Operators) if (engine.IsHome(op)) home++;

            var name = UiKit.Label(header,
                $"<color=#{UiKit.Hex(UiKit.Readable(colour))}>{seat.Color.ToString().ToUpperInvariant()}</color>" +
                (playing ? $"  <size=75%><color=#{UiKit.Hex(UiKit.GoldBright)}>PLAYING</color></size>" : ""),
                UiKit.FontBody, bold: true);
            UiKit.Size(name, flexibleWidth: 1f);

            UiKit.Label(header,
                $"<color=#{UiKit.Hex(UiKit.TextDim)}>home</color> {home}/{seat.Operators.Count}   " +
                $"<color=#{UiKit.Hex(UiKit.Cyan)}>{seat.Energy}</color><color=#{UiKit.Hex(UiKit.TextDim)}>/{cap}e</color>",
                UiKit.FontSmall, align: TextAlignmentOptions.MidlineRight);
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
                UiKit.Fill(row, new Color(0f, 0f, 0f, 0f));
            }

            UiKit.Size(row, height: RowHeight);

            var layout = UiKit.Row(row, 8f);
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childForceExpandHeight = false;

            var seatColour = BoardLayout.ColourOf(op.Owner);
            var iconColour = op.IsInYard ? Color.Lerp(seatColour, Color.gray, 0.55f) : seatColour;
            UiKit.Icon(row, PieceShape.For(op), iconColour, 30f);

            var middle = UiKit.Rect("middle", row);
            UiKit.Column(middle, 2f);
            UiKit.Size(middle, flexibleWidth: 1f);

            string where = home ? "home"
                : deployable ? $"<color=#{UiKit.Hex(UiKit.Cyan)}>ready to deploy</color>"
                : op.IsInYard ? "waiting"
                : "on board";

            // Name alone on the first line, so it is never the part that
            // gets cut; the state and any statuses share the second.
            UiKit.Label(middle, op.Name, UiKit.FontBody, playing || !op.IsInYard ? UiKit.Text : UiKit.TextDim);

            var tags = UiKit.Rect("tags", middle);
            UiKit.Row(tags, 4f);
            UiKit.Label(tags, where, 13f, UiKit.TextDim);

            if (!op.IsInYard && !home)
            {
                foreach (var kind in engine.ActiveStatusesOn(op))
                    UiKit.Tag(tags, StatusPalette.Label(kind), StatusPalette.For(kind), 11f);
            }

            var right = UiKit.Rect("health", row);
            UiKit.Column(right, 3f).childAlignment = TextAnchor.MiddleRight;
            UiKit.Fixed(right, 64f);

            float fraction = (float)op.Health / Mathf.Max(1, op.MaxHealth);
            UiKit.Label(right, $"{op.Health}/{op.MaxHealth}", UiKit.FontSmall, align: TextAlignmentOptions.MidlineRight, bold: true);
            UiKit.Bar(right, fraction, Color.Lerp(UiKit.Danger, seatColour, fraction), 64f, 5f);
        }
    }
}
