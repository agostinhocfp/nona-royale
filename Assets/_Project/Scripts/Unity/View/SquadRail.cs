// Assets/_Project/Scripts/Unity/View/SquadRail.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
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
    /// having to find three small pieces in a corner of the board.
    ///
    /// <b>Your seat is expanded; the CPUs are folded</b> (HUD_PASS.md, H2).
    /// Four seats of three full rows made twelve entries of equal weight, only
    /// three of which the player commands, in 290 units of permanent width - and
    /// at that width the personality, the energy and the state line were all
    /// being truncated. A folded seat keeps one line: its colour, its name, its
    /// home count, and its three operators in the silhouettes
    /// <see cref="PieceShape"/> teaches, tinted by health. It opens on hover and
    /// stays open for its whole turn.
    ///
    /// <b>Folded is not deleted.</b> Identity, position and rough health are
    /// readable off the board already - the silhouette is keyed on the
    /// operator's name, not the seat, and piece size carries health - so the
    /// fold drops what the board repeats and keeps what it cannot say. What it
    /// genuinely costs is reading all four seats in detail at once; hover and
    /// the playing seat's auto-open are what buy that back.
    ///
    /// <b>Folding never rebuilds.</b> Both forms are built and one is switched
    /// off, the same reasoning as the hover wash below: the pointer moves at
    /// pointer speed, and a rebuild would destroy the very rect whose exit
    /// event is owed. The relay sits on a wrapper that holds both, so swapping
    /// them cannot pull the hover target out from under the pointer.
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
        public const float Width = 220f;
        private const float RowHeight = 52f;

        /// <summary>A folded seat's single line, and the size of the operator silhouettes on it.</summary>
        private const float SpineHeight = 30f;
        private const float SpinePip = 14f;

        // Warm and faint: the wash echoes where the pointer is, it is not a
        // live control, so it stays clear of the cyan register (ART_DIRECTION
        // §2.1) and of the selected row's cyan fill and edge.
        private const float HoverGlowAlpha = 0.10f;

        /// <summary>Canvas units the rail claims from the left edge.</summary>
        public static float ReservedWidth => Width;

        public bool Visible { get; set; } = true;

        private IControlPanelHost _host;
        private RectTransform _rail;
        private RectTransform _content;
        private bool _dirty;

        // The board-hover echo: the row of the piece under the pointer gets a
        // soft wash, so the eye can tie the 3D piece to its rail entry. Kept
        // out of the rebuild path — hover moves at pointer speed, and toggling
        // a wash is one Image.enabled, not a rebuild.
        private OperatorState _hovered;
        private readonly List<RowGlow> _rowGlows = new List<RowGlow>();

        // Which folded seat the pointer is over, if any. Kept out of the
        // rebuild path for the same reason the hover wash is.
        private PlayerColor? _hoveredSeat;
        private readonly List<SeatFold> _folds = new List<SeatFold>();

        /// <summary>A seat's two forms, and which one the state asks for.</summary>
        private sealed class SeatFold
        {
            public PlayerColor Colour;
            public bool Cpu;
            public bool Playing;
            public GameObject Spine;
            public GameObject Block;
        }

        // The fraction each row's health bar last showed, so a hit or a heal
        // glides from the old value instead of snapping (UI_MOTION.md U2).
        // Keyed by the state object: rows are rebuilt, the operators are not.
        private readonly Dictionary<OperatorState, float> _lastFractions = new Dictionary<OperatorState, float>();

        /// <summary>One operator row's hover wash, and whether selection already owns the row.</summary>
        private sealed class RowGlow
        {
            public OperatorState Operator;
            public Image Glow;
            public bool Selected;
        }

        public void Bind(RectTransform canvasRect, IControlPanelHost host)
        {
            _host = host;
            _lastFractions.Clear();
            if (_rail == null) Build(canvasRect);
            _dirty = true;
        }

        public void MarkDirty() => _dirty = true;

        /// <summary>
        /// Mirrors the board's hovered piece onto its row. Instant on/off: the
        /// board mark is instant too, and a fade here would lag the pointer.
        /// </summary>
        public void SetHovered(OperatorState op)
        {
            _hovered = op;
            ApplyHover();
        }

        /// <summary>
        /// Selection wins: a selected row already reads as live (cyan fill,
        /// cyan double edge), so the hover wash stays off it.
        /// </summary>
        private void ApplyHover()
        {
            foreach (var row in _rowGlows)
                row.Glow.enabled = ReferenceEquals(row.Operator, _hovered) && !row.Selected;
        }

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

            _rowGlows.Clear();
            _folds.Clear();

            var match = _host?.Match;
            if (match == null) return;

            var engine = match.Engine;

            foreach (var seat in match.Players)
            {
                bool playing = !engine.MatchOver && seat.Color == engine.CurrentPlayer.Color;
                bool cpu = _host.SeatTag(seat.Color) != null;

                // One wrapper per seat holds both forms, so switching between
                // them cannot move the hover target out from under the pointer.
                var wrapper = UiKit.Rect($"seat_{seat.Color}", _content);
                UiKit.Column(wrapper, 4f);
                wrapper.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                if (cpu) UiKit.Fill(wrapper, Color.clear, blocksPointer: true);

                var spine = cpu ? Spine(wrapper, seat, engine) : null;
                var block = Block(wrapper, seat, playing, engine);

                if (cpu)
                {
                    var colour = seat.Color;
                    HoverRelay.On(wrapper, () => HoverSeat(colour), () => HoverSeat(null));
                }

                _folds.Add(new SeatFold
                {
                    Colour = seat.Color, Cpu = cpu, Playing = playing, Spine = spine, Block = block,
                });

                UiKit.Space(_content, height: 8f);
            }

            // Rows were recreated disabled; the hover may outlive a rebuild.
            ApplyHover();
            ApplyFold();
        }

        /// <summary>A seat's full form: its header and one row per operator.</summary>
        private GameObject Block(RectTransform parent, PlayerState seat, bool playing, Core.GameEngine engine)
        {
            var block = UiKit.Rect("block", parent);
            UiKit.Column(block, 4f);
            block.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            SeatHeader(block, seat, playing, engine.EnergyCap, engine);

            // A CPU seat's rows are never buttons, even on its own turn (BOT2).
            bool commandable = playing && !_host.CpuTurn;
            foreach (var op in seat.Operators)
                OperatorRow(block, op, commandable, engine);

            return block.gameObject;
        }

        /// <summary>
        /// A folded seat: colour, name, its three operators as health-tinted
        /// silhouettes, and how many are home (H2).
        /// </summary>
        private GameObject Spine(RectTransform parent, PlayerState seat, Core.GameEngine engine)
        {
            var colour = BoardLayout.ColourOf(seat.Color);

            var spine = UiKit.Rect("spine", parent);
            UiKit.Size(spine, height: SpineHeight);

            var row = UiKit.Row(spine, 6f);
            row.padding = new RectOffset(8, 8, 0, 0);
            row.childAlignment = TextAnchor.MiddleLeft;

            UiKit.Diamond(spine, colour, 7f, 10f);

            var name = UiKit.Label(spine,
                $"<color=#{UiTheme.Hex(UiTheme.Readable(colour))}>{seat.Color.ToString().ToUpperInvariant()}</color>",
                13f, bold: true);
            UiKit.Size(name, flexibleWidth: 1f);

            var pips = UiKit.Rect("pips", spine);
            UiKit.Row(pips, 4f).childAlignment = TextAnchor.MiddleCenter;

            int home = 0;

            foreach (var op in seat.Operators)
            {
                bool atHome = engine.IsHome(op);
                if (atHome) home++;

                // The same three states the full row draws, in one mark: home
                // fades out, a yard piece takes the waiting tint, and a piece on
                // the board runs from the seat colour toward danger as it is hurt.
                float fraction = (float)op.Health / Mathf.Max(1, op.MaxHealth);
                var tint = atHome ? UiTheme.WithAlpha(colour, 0.3f)
                    : op.IsInYard ? Color.Lerp(colour, UiTheme.PieceWaiting, 0.55f)
                    : Color.Lerp(UiTheme.Danger, colour, fraction);

                UiKit.Icon(pips, PieceShape.For(op), tint, SpinePip);
            }

            UiKit.Label(spine, $"{home}/{seat.Operators.Count}", 13f, UiTheme.TextDim,
                align: TextAlignmentOptions.MidlineRight);

            return spine.gameObject;
        }

        private void HoverSeat(PlayerColor? seat)
        {
            if (_hoveredSeat.Equals(seat)) return;

            _hoveredSeat = seat;
            ApplyFold();
        }

        /// <summary>
        /// Which form each seat shows. A CPU folds unless it is playing or the
        /// pointer is on it; the player's own seat never folds.
        /// </summary>
        private void ApplyFold()
        {
            foreach (var fold in _folds)
            {
                if (!fold.Cpu) continue;

                bool open = fold.Playing || (_hoveredSeat.HasValue && _hoveredSeat.Value == fold.Colour);
                bool folded = !open;

                if (fold.Spine != null && fold.Spine.activeSelf != folded) fold.Spine.SetActive(folded);
                if (fold.Block != null && fold.Block.activeSelf != open) fold.Block.SetActive(open);
            }
        }

        private void SeatHeader(RectTransform parent, PlayerState seat, bool playing, int cap, Core.GameEngine engine)
        {
            var colour = BoardLayout.ColourOf(seat.Color);

            // The seat to play gets a gilt-edged plate; the others sit on a
            // brass underline.
            var header = UiKit.Rect("header", parent);
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

        private void OperatorRow(RectTransform parent, OperatorState op, bool playing, Core.GameEngine engine)
        {
            bool home = engine.IsHome(op);
            bool deployable = playing && engine.CanDeploy(op);
            bool selected = ReferenceEquals(op, _host.SelectedOperator);

            RectTransform row;

            if (playing)
            {
                // The whole row is the button. Its label is left empty and the
                // row content is laid out on top of it.
                var button = UiKit.Button(parent, "", () =>
                    {
                        if (deployable) _host.Deploy(op);
                        else _host.ToggleOperator(op);
                    },
                    MarkDirty, selected: selected);
                row = (RectTransform)button.transform;
            }
            else
            {
                row = UiKit.Rect($"op_{op.Name}", parent);
                UiKit.Fill(row, Color.clear);
            }

            UiKit.Size(row, height: RowHeight);

            // The hover wash is added before the row's content, so it draws
            // under the icon and labels; layout ignores it and it never
            // catches the pointer (ADR-0008 consequence 9).
            var glowRect = UiKit.Rect("hover_glow", row);
            UiKit.Stretch(glowRect);
            glowRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var glow = UiKit.Fill(glowRect, UiTheme.WithAlpha(UiTheme.Gold, HoverGlowAlpha));
            glow.enabled = false;
            _rowGlows.Add(new RowGlow { Operator = op, Glow = glow, Selected = selected });

            var layout = UiKit.Row(row, 8f);
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childForceExpandHeight = false;

            var seatColour = BoardLayout.ColourOf(op.Owner);
            var iconColour = op.IsInYard ? Color.Lerp(seatColour, UiTheme.PieceWaiting, 0.55f) : seatColour;
            UiKit.Icon(row, PieceShape.For(op), iconColour, 26f);

            var middle = UiKit.Rect("middle", row);
            UiKit.Column(middle, 2f);
            UiKit.Size(middle, flexibleWidth: 1f);

            // "ready", not "ready to deploy": the row is the button, and at the
            // rail's new width the longer string was the part that got cut (H2).
            string where = home ? "home"
                : deployable ? $"<color=#{UiTheme.Hex(UiTheme.Cyan)}>ready</color>"
                : op.IsInYard ? "waiting"
                : "on board";

            // Name alone on the first line, so it is never the part that
            // gets cut; the state and any statuses share the second.
            UiKit.Label(middle, op.Name, UiTheme.FontBody, playing || !op.IsInYard ? UiTheme.Text : UiTheme.TextDim);

            // Statuses left the rail with H2 and live on the board alone: they
            // were drawn in both places, and the board is where they mean
            // something, because that is where the piece they belong to is.
            UiKit.Label(middle, where, 13f, UiTheme.TextDim);

            var right = UiKit.Rect("health", row);
            UiKit.Column(right, 3f).childAlignment = TextAnchor.MiddleRight;
            UiKit.Fixed(right, 52f);

            float fraction = (float)op.Health / Mathf.Max(1, op.MaxHealth);
            UiKit.Label(right, $"{op.Health}/{op.MaxHealth}", UiTheme.FontSmall, align: TextAlignmentOptions.MidlineRight, bold: true);

            // Build the bar at the last shown fraction and glide to the new
            // one; a first sight starts at the truth (UI_MOTION.md U2).
            bool seen = _lastFractions.TryGetValue(op, out float previous);
            var fill = UiKit.Bar(right, seen ? previous : fraction,
                Color.Lerp(UiTheme.Danger, seatColour, fraction), 52f, 5f);
            if (seen && !Mathf.Approximately(previous, fraction))
                UiKit.TweenBar(fill, previous, fraction, 0.35f, seatColour);
            _lastFractions[op] = fraction;
        }
    }
}
