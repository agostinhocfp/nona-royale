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
    /// Every seat's squad, each operator shown as waiting, on the board or
    /// home, with health (GUI increment F). A rail down the left edge on a wide
    /// screen; a ribbon under the top bar on an upright one.
    /// </summary>
    /// <remarks>
    /// <b>It answers "which of my pieces are waiting"</b>, PRESENTATION §2's
    /// deployed-operators row and question 2 of the stranger test, without
    /// having to find three small pieces in a corner of the board.
    ///
    /// <b>Your seat is expanded; the CPUs are folded</b> (HUD_PASS.md, H2).
    /// Four seats of three full rows made twelve entries of equal weight, only
    /// three of which the player commands, in 220 units of permanent width - and
    /// at that width the personality, the energy and the state line were all
    /// being truncated. A folded seat keeps one line: its colour, its name, its
    /// home count, and its three operators in the silhouettes
    /// <see cref="PieceShape"/> teaches, tinted by health. It opens on hover and
    /// stays open for its whole turn.
    ///
    /// <b>Upright it lies down</b> (MOBILE.md, M3). There is no 220-unit column
    /// to spare on a phone — that is nearly half the screen — so the rail
    /// becomes a 62-unit band under the top bar, scrolled sideways, with your
    /// own seat first: its three operators as tappable chips, then each CPU as
    /// one folded group. Your pieces are what the band is for, so they are what
    /// is on screen before anything is scrolled; the CPUs' folded form already
    /// says everything the fold keeps, so it does not open there.
    ///
    /// <b>Folding never rebuilds.</b> Both forms are built and one is switched
    /// off, the same reasoning as the hover wash below: the pointer moves at
    /// pointer speed, and a rebuild would destroy the very rect whose exit
    /// event is owed. The relay sits on a wrapper that holds both, so swapping
    /// them cannot pull the hover target out from under the pointer.
    ///
    /// <b>The current seat's rows are buttons.</b> A click, or a tap, does what
    /// one on the piece does: it deploys a piece that can deploy, and otherwise
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

        /// <summary>The band's height when the rail is lying down (M3).</summary>
        public const float BandHeight = 62f;

        /// <summary>A folded seat's single line, and the size of the operator silhouettes on it.</summary>
        private const float SpineHeight = 30f;
        private const float SpinePip = 14f;

        /// <summary>One of your own operators, as a chip in the upright band.</summary>
        private const float ChipWidth = 86f;

        // Warm and faint: the wash echoes where the pointer is, it is not a
        // live control, so it stays clear of the cyan register (ART_DIRECTION
        // §2.1) and of the selected row's cyan fill and edge.
        private const float HoverGlowAlpha = 0.10f;

        /// <summary>Canvas units the rail claims from the left edge. Nothing, lying down.</summary>
        public static float ReservedWidth => ScreenLayout.Pick(Width, 0f);

        /// <summary>Canvas units the band claims under the top bar. Nothing, standing up.</summary>
        public static float ReservedHeight => ScreenLayout.Pick(0f, BandHeight);

        public bool Visible { get; set; } = true;

        private IControlPanelHost _host;
        private RectTransform _canvas;
        private RectTransform _rail;
        private RectTransform _content;
        private bool _dirty;
        private bool _builtPortrait;

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
            _canvas = canvasRect;
            _lastFractions.Clear();
            if (_rail == null) Build();
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
                if (row.Glow != null) row.Glow.enabled = ReferenceEquals(row.Operator, _hovered) && !row.Selected;
        }

        private void Build()
        {
            _builtPortrait = ScreenLayout.IsPortrait;

            _rail = UiKit.Rect("squad_rail", _canvas);

            var scroll = _rail.gameObject.AddComponent<ScrollRect>();
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = UiKit.Rect("viewport", _rail);
            UiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = UiKit.Rect("content", viewport);

            if (_builtPortrait)
            {
                _rail.anchorMin = new Vector2(0f, 1f);
                _rail.anchorMax = new Vector2(1f, 1f);
                _rail.pivot = new Vector2(0.5f, 1f);
                _rail.sizeDelta = new Vector2(0f, BandHeight);
                _rail.anchoredPosition = new Vector2(0f, -TurnStrip.ReservedHeight);
                UiKit.Dock(_rail, true, RectTransform.Edge.Bottom);

                scroll.horizontal = true;
                scroll.vertical = false;

                _content.anchorMin = new Vector2(0f, 0f);
                _content.anchorMax = new Vector2(0f, 1f);
                _content.pivot = new Vector2(0f, 0.5f);
                _content.sizeDelta = Vector2.zero;

                var band = UiKit.Row(_content, 6f, 6);
                band.padding.bottom = 10; // clear of the rule
                band.childAlignment = TextAnchor.MiddleLeft;
                band.childForceExpandHeight = true;
                _content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                _rail.anchorMin = new Vector2(0f, 0f);
                _rail.anchorMax = new Vector2(0f, 1f);
                _rail.pivot = new Vector2(0f, 0.5f);
                _rail.offsetMin = new Vector2(0f, 0f);
                _rail.offsetMax = new Vector2(Width, -TurnStrip.ReservedHeight);
                UiKit.Dock(_rail, true, RectTransform.Edge.Right);

                scroll.horizontal = false;
                scroll.vertical = true;

                _content.anchorMin = new Vector2(0f, 1f);
                _content.anchorMax = new Vector2(1f, 1f);
                _content.pivot = new Vector2(0.5f, 1f);
                _content.sizeDelta = Vector2.zero;

                UiKit.Column(_content, 4f, 10).padding.right = 16; // clear of the rule
                _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            scroll.viewport = viewport;
            scroll.content = _content;
        }

        private void LateUpdate()
        {
            if (_rail == null) return;

            // The screen turned: both forms are built from scratch, so the old
            // one goes and the rebuild below fills the new one.
            if (_builtPortrait != ScreenLayout.IsPortrait)
            {
                var old = _rail.gameObject;
                _rail = null;
                _rowGlows.Clear();
                _folds.Clear();
                old.SetActive(false);
                Destroy(old);

                Build();
                _dirty = true;
            }

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

            if (_builtPortrait) RebuildBand(match);
            else RebuildRail(match);

            // Rows were recreated disabled; the hover may outlive a rebuild.
            ApplyHover();
            ApplyFold();
        }

        // ── The standing rail ────────────────────────────────────────────

        private void RebuildRail(Core.MatchFactory.Match match)
        {
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
        }

        // ── The lying band ───────────────────────────────────────────────

        /// <summary>
        /// Your seat's three operators as chips, then each CPU folded into one
        /// group (M3). Your own seat leads, so it is what is on screen before
        /// the band is scrolled.
        /// </summary>
        private void RebuildBand(Core.MatchFactory.Match match)
        {
            var engine = match.Engine;

            foreach (var seat in match.Players)
            {
                if (_host.SeatTag(seat.Color) != null) continue;

                bool playing = !engine.MatchOver && seat.Color == engine.CurrentPlayer.Color;
                SeatTab(seat, playing, engine);

                foreach (var op in seat.Operators)
                    OperatorChip(op, playing && !_host.CpuTurn, engine);

                UiKit.Divider(_content, vertical: true);
            }

            foreach (var seat in match.Players)
            {
                if (_host.SeatTag(seat.Color) == null) continue;

                bool playing = !engine.MatchOver && seat.Color == engine.CurrentPlayer.Color;
                CpuGroup(seat, playing, engine);
            }
        }

        /// <summary>Your seat's marker at the head of the band: colour, name, pool.</summary>
        private void SeatTab(PlayerState seat, bool playing, Core.GameEngine engine)
        {
            var colour = BoardLayout.ColourOf(seat.Color);

            var tab = UiKit.Rect($"seat_{seat.Color}", _content);
            UiKit.Fixed(tab, 74f);

            if (playing)
            {
                UiKit.Sliced(tab, DecoSprites.ButtonFill, UiTheme.PanelRaised);
                UiKit.Overlay(tab, DecoSprites.ButtonEdge, UiTheme.Gold);
            }

            var column = UiKit.Column(tab, 1f, 4);
            column.childAlignment = TextAnchor.MiddleCenter;

            var name = UiKit.Label(tab,
                $"<color=#{UiTheme.Hex(UiTheme.Readable(colour))}>{seat.Color.ToString().ToUpperInvariant()}</color>",
                12f, bold: true, align: TextAlignmentOptions.Center);
            name.overflowMode = TextOverflowModes.Overflow;

            UiKit.Label(tab,
                $"<color=#{UiTheme.Hex(UiTheme.Cyan)}>{seat.Energy}</color><color=#{UiTheme.Hex(UiTheme.TextDim)}>/{engine.EnergyCap}e</color>",
                12f, align: TextAlignmentOptions.Center);
        }

        /// <summary>
        /// One of your operators, as a tappable chip: its silhouette, its name,
        /// its health, and what it is doing in one word.
        /// </summary>
        private void OperatorChip(OperatorState op, bool commandable, Core.GameEngine engine)
        {
            bool home = engine.IsHome(op);
            bool deployable = commandable && engine.CanDeploy(op);
            bool selected = ReferenceEquals(op, _host.SelectedOperator);

            RectTransform chip;

            if (commandable)
            {
                var button = UiKit.Button(_content, "", () =>
                    {
                        if (deployable) _host.Deploy(op);
                        else _host.ToggleOperator(op);
                    },
                    MarkDirty, selected: selected);
                chip = (RectTransform)button.transform;
            }
            else
            {
                chip = UiKit.Rect($"op_{op.Name}", _content);
                UiKit.Sliced(chip, DecoSprites.ChipFill, UiTheme.PanelInset);
            }

            UiKit.Fixed(chip, ChipWidth);

            var glowRect = UiKit.Rect("hover_glow", chip);
            UiKit.Stretch(glowRect);
            glowRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var glow = UiKit.Fill(glowRect, UiTheme.WithAlpha(UiTheme.Gold, HoverGlowAlpha));
            glow.enabled = false;
            _rowGlows.Add(new RowGlow { Operator = op, Glow = glow, Selected = selected });

            var column = UiKit.Column(chip, 1f, 4);
            column.childAlignment = TextAnchor.MiddleCenter;

            var top = UiKit.Rect("top", chip);
            UiKit.Row(top, 4f).childAlignment = TextAnchor.MiddleLeft;
            UiKit.Size(top, height: 20f);

            var seatColour = BoardLayout.ColourOf(op.Owner);
            var iconColour = op.IsInYard ? Color.Lerp(seatColour, UiTheme.PieceWaiting, 0.55f) : seatColour;
            UiKit.Icon(top, PieceShape.For(op), iconColour, 18f);

            var name = UiKit.Label(top, op.Name, 13f, commandable || !op.IsInYard ? UiTheme.Text : UiTheme.TextDim);
            UiKit.Size(name, flexibleWidth: 1f);

            // "ready" is the one word that asks for a tap, so it takes the live
            // colour; the rest are states, so they stay dim.
            string where = home ? "home"
                : deployable ? $"<color=#{UiTheme.Hex(UiTheme.Cyan)}>ready</color>"
                : op.IsInYard ? "waiting"
                : "on board";

            UiKit.Label(chip, where, 12f, UiTheme.TextDim);

            float fraction = (float)op.Health / Mathf.Max(1, op.MaxHealth);
            bool seen = _lastFractions.TryGetValue(op, out float previous);
            var fill = UiKit.Bar(chip, seen ? previous : fraction,
                Color.Lerp(UiTheme.Danger, seatColour, fraction), ChipWidth - 12f, 4f);
            if (seen && !Mathf.Approximately(previous, fraction))
                UiKit.TweenBar(fill, previous, fraction, 0.35f, seatColour);
            _lastFractions[op] = fraction;
        }

        /// <summary>A CPU seat folded into one group in the band: colour, name, its three silhouettes, home count.</summary>
        private void CpuGroup(PlayerState seat, bool playing, Core.GameEngine engine)
        {
            var colour = BoardLayout.ColourOf(seat.Color);

            var group = UiKit.Rect($"seat_{seat.Color}", _content);
            UiKit.Fixed(group, 104f);
            UiKit.Sliced(group, DecoSprites.ChipFill, playing ? UiTheme.PanelRaised : UiTheme.PanelInset);
            if (playing) UiKit.Overlay(group, DecoSprites.ButtonEdge, UiTheme.Gold);

            var column = UiKit.Column(group, 1f, 5);
            column.childAlignment = TextAnchor.MiddleCenter;

            int home = 0;
            foreach (var op in seat.Operators) if (engine.IsHome(op)) home++;

            var head = UiKit.Rect("head", group);
            UiKit.Row(head, 4f).childAlignment = TextAnchor.MiddleLeft;
            UiKit.Size(head, height: 18f);

            UiKit.Diamond(head, colour, 6f, 9f);

            var name = UiKit.Label(head,
                $"<color=#{UiTheme.Hex(UiTheme.Readable(colour))}>{seat.Color.ToString().ToUpperInvariant()}</color>",
                12f, bold: true);
            UiKit.Size(name, flexibleWidth: 1f);

            UiKit.Label(head, $"{home}/{seat.Operators.Count}", 12f, UiTheme.TextDim,
                align: TextAlignmentOptions.MidlineRight);

            var pips = UiKit.Rect("pips", group);
            UiKit.Row(pips, 5f).childAlignment = TextAnchor.MiddleCenter;
            UiKit.Size(pips, height: 18f);

            foreach (var op in seat.Operators)
                UiKit.Icon(pips, PieceShape.For(op), PipTint(op, colour, engine), SpinePip);
        }

        // ── Seat forms shared by both arrangements ───────────────────────

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
                if (engine.IsHome(op)) home++;
                UiKit.Icon(pips, PieceShape.For(op), PipTint(op, colour, engine), SpinePip);
            }

            UiKit.Label(spine, $"{home}/{seat.Operators.Count}", 13f, UiTheme.TextDim,
                align: TextAlignmentOptions.MidlineRight);

            return spine.gameObject;
        }

        /// <summary>
        /// The three states a folded operator can be in, in one mark: home
        /// fades out, a yard piece takes the waiting tint, and a piece on the
        /// board runs from the seat colour toward danger as it is hurt.
        /// </summary>
        private static Color PipTint(OperatorState op, Color colour, Core.GameEngine engine)
        {
            if (engine.IsHome(op)) return UiTheme.WithAlpha(colour, 0.3f);
            if (op.IsInYard) return Color.Lerp(colour, UiTheme.PieceWaiting, 0.55f);

            float fraction = (float)op.Health / Mathf.Max(1, op.MaxHealth);
            return Color.Lerp(UiTheme.Danger, colour, fraction);
        }

        private void HoverSeat(PlayerColor? seat)
        {
            if (_hoveredSeat.Equals(seat)) return;

            _hoveredSeat = seat;
            ApplyFold();
        }

        /// <summary>
        /// Which form each seat shows. A CPU folds unless it is playing or the
        /// pointer is on it; the player's own seat never folds. The lying band
        /// builds one form per seat and has nothing to switch.
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
