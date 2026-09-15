// Assets/_Project/Scripts/Unity/Composition/MatchBootstrap.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NonaRoyale.Unity.View;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NonaRoyale.Unity.Composition
{
    /// <summary>
    /// The Unity composition root. Builds a match, renders it, and turns clicks
    /// into commands.
    /// </summary>
    /// <remarks>
    /// <b>Drop this on one empty GameObject and press Play.</b> No prefabs, no
    /// scene wiring, no imported art — the board, the pieces, the controls and
    /// the HUD canvas (ADR-0008) are all generated at runtime. The only thing
    /// this build exists to answer is whether the game is fun, and scene
    /// wiring does not help answer it.
    ///
    /// <b>It holds no rules.</b> Every action goes out as a command and comes
    /// back as events; the view never inspects a service or mutates state. If a
    /// move looks wrong on screen, the bug is in the core and there is an
    /// EditMode test missing for it.
    ///
    /// <b>The board is the primary control</b> (GUI phase, increment E). Click
    /// a piece to select it, click where it lands to move it, click a yard
    /// piece to deploy it, click a ringed piece or cell to aim. Right-click or
    /// Esc steps back. Every mark and every clickable thing comes from an
    /// engine query; the click only picks between the engine's answers.
    /// </remarks>
    public sealed class MatchBootstrap : MonoBehaviour, IControlPanelHost
    {
        [Header("Match")]
        [Tooltip("Draft three distinct operators per seat from the whole roster. " +
                 "Off means the alpha three, which is what every measurement in ADR-0002 used.")]
        public bool randomSquads = false;

        [Tooltip("Compact 28x2 has a longer journey than Standard since ADR-0002 " +
                 "Amendment 6 (59 against 58). Kept switchable for comparison only.")]
        public bool useCompactBoard = false;

        [Range(2, 4)] public int players = 4;

        [Tooltip("Operators already on the board at the start. 2 is the adopted value.")]
        [Range(0, 3)] public int openingDeployments = 2;

        public int seed = 20260912;

        [Header("Presentation")]
        [Tooltip("World units per board cell.")]
        public float cellSpacing = 1f;

        // Renamed from showDevPanel in GUI increment F, so the scene's saved
        // value does not carry over: the dev panel now starts hidden.
        [Tooltip("Show the dev panel (reseed, pip buttons) in place of the squad rail. Tab toggles it while playing.")]
        public bool showDevPanel = false;

        [Tooltip("With the dev panel shown, use the old OnGUI panel instead of the uGUI one. F2 switches while playing. " +
                 "Both exist until the stranger test passes (ADR-0008 consequence 6).")]
        public bool useLegacyPanel = false;

        // Renamed from showFullLog in GUI increment F2, when the log became an
        // overlay: it now starts closed, whatever the scene saved.
        [Tooltip("Show the full text log over the board's right edge. L toggles it while playing.")]
        public bool showFullLog = false;

        [Tooltip("Health readout above every deployed piece (ADR-0008). " +
                 "H toggles it while playing — the stranger test decides its fate.")]
        public bool showPieceHealth = true;

        private MatchFactory.Match _match;
        private BoardLayout _layout;
        private readonly List<OperatorPiece> _pieces = new List<OperatorPiece>();
        private readonly List<string> _log = new List<string>();

        private OperatorState _selectedOperator;
        private OperatorState _selectedTarget;
        private CellRef? _selectedCell;
        private IReadOnlyList<CellRef> _legalCells = new List<CellRef>();
        private IReadOnlyList<OperatorState> _castTargets = new List<OperatorState>();
        private readonly List<MoveOption> _moveOptions = new List<MoveOption>();
        private OperatorPiece _hovered;
        private CellLabelLayer _cellLabels;

        /// <summary>
        /// One way to spend the dice, as the board offers it: this operator,
        /// this die (or all of them), landing on this cell.
        /// </summary>
        private readonly struct MoveOption
        {
            public MoveOption(OperatorState op, int? die, int pips, CellRef cell)
            {
                Operator = op;
                Die = die;
                Pips = pips;
                Cell = cell;
            }

            public OperatorState Operator { get; }
            public int? Die { get; }
            public int Pips { get; }
            public CellRef Cell { get; }
        }
        private AbilityDefinition _selectedAbility;
        private HighlightLayer _highlights;
        private FeedbackLayer _feedback;
        private HudRoot _hudRoot;
        private PieceHudLayer _pieceHud;
        private TurnStrip _turnStrip;
        private DeviceLayer _devices;
        private ControlPanel _controls;
        private SquadRail _rail;
        private ActionTray _tray;
        private LogPanel _logPanel;
        private HistoryStrip _history;
        private EventToasts _toasts;
        private TurnBanner _banner;
        private TurnButton _turnButton;
        private Vector2 _panelScroll;

        private void Start() => NewMatch();

        private void NewMatch()
        {
            foreach (var piece in _pieces)
                if (piece != null) Destroy(piece.gameObject);

            _pieces.Clear();
            _log.Clear();
            _hovered = null;
            _selectedOperator = null;
            _selectedTarget = null;
            _selectedAbility = null;
            _selectedCell = null;

            // Both profiles must be drawable crosses: BoardLayout rejects a
            // circuit outside the 8L+4 family rather than drawing a track with a
            // gap in it (ADR-0002 Amendment 6). 28x2 replaces the old 24x2.
            var board = useCompactBoard
                ? BoardProfile.Cross("Compact", 3, laps: 2)
                : BoardProfile.Standard;

            var seats = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow }
                .Take(players).ToList();

            _match = randomSquads
                ? MatchFactory.Create(
                    seats, seed,
                    squads: null,                      // null drafts every seat
                    board: board,
                    openingDeployments: openingDeployments)
                : MatchFactory.CreateAlphaMatch(
                    seats, seed,
                    board: board,
                    openingDeployments: openingDeployments);

            _layout = new BoardLayout(board, cellSpacing);

            var boardView = GetComponent<BoardView>() ?? gameObject.AddComponent<BoardView>();
            boardView.Build(_match.Map, _layout);

            _highlights = GetComponent<HighlightLayer>() ?? gameObject.AddComponent<HighlightLayer>();
            _highlights.Bind(_layout);
            _highlights.Clear();

            _devices = GetComponent<DeviceLayer>() ?? gameObject.AddComponent<DeviceLayer>();
            _devices.Bind(_layout);
            _devices.Clear();

            _feedback = GetComponent<FeedbackLayer>() ?? gameObject.AddComponent<FeedbackLayer>();
            _feedback.Bind(_layout.CellSize);

            foreach (var op in _match.Operators)
            {
                var go = new GameObject();
                go.transform.SetParent(transform, false);

                var piece = go.AddComponent<OperatorPiece>();
                piece.Bind(op, _layout.CellSize, cellSpacing);
                _pieces.Add(piece);
            }

            // The HUD scaffold survives a reseed — only the per-piece labels
            // are rebuilt, since the pieces they tracked were just destroyed.
            _hudRoot = GetComponent<HudRoot>() ?? gameObject.AddComponent<HudRoot>();
            _pieceHud = GetComponent<PieceHudLayer>() ?? gameObject.AddComponent<PieceHudLayer>();
            _pieceHud.Bind(_hudRoot.Root, _pieces, cellSpacing * 0.55f);
            _turnStrip = GetComponent<TurnStrip>() ?? gameObject.AddComponent<TurnStrip>();
            _turnStrip.Bind(_hudRoot.Root);
            _controls = GetComponent<ControlPanel>() ?? gameObject.AddComponent<ControlPanel>();
            _controls.Bind(_hudRoot.Root, this);
            _rail = GetComponent<SquadRail>() ?? gameObject.AddComponent<SquadRail>();
            _rail.Bind(_hudRoot.Root, this);
            _tray = GetComponent<ActionTray>() ?? gameObject.AddComponent<ActionTray>();
            _tray.Bind(_hudRoot.Root, this);
            _logPanel = GetComponent<LogPanel>() ?? gameObject.AddComponent<LogPanel>();
            _logPanel.Bind(_hudRoot.Root, this);
            _logPanel.CloseRequested = () => showFullLog = false;
            _history = GetComponent<HistoryStrip>() ?? gameObject.AddComponent<HistoryStrip>();
            _history.Bind(_hudRoot.Root);
            _history.LogRequested = () => showFullLog = !showFullLog;
            _toasts = GetComponent<EventToasts>() ?? gameObject.AddComponent<EventToasts>();
            _toasts.Bind(_hudRoot.Root);
            _banner = GetComponent<TurnBanner>() ?? gameObject.AddComponent<TurnBanner>();
            _banner.Bind(_hudRoot.Root);
            _turnButton = GetComponent<TurnButton>() ?? gameObject.AddComponent<TurnButton>();
            _turnButton.Bind(_hudRoot.Root, this);
            _cellLabels = GetComponent<CellLabelLayer>() ?? gameObject.AddComponent<CellLabelLayer>();
            _cellLabels.Bind(_hudRoot.Root);

            FrameCamera();
            Handle(_match.Engine.Start(), immediate: true);
        }

        /// <summary>Screen width the OnGUI panel occupies, including its margin. Pixels.</summary>
        private const float PanelWidth = 330f;

        private const float FrameMargin = 1.12f;

        private int _framedWidth;
        private int _framedHeight;
        private bool _framedWithPanel;
        private bool _framedLegacy;
        private float _framedScale;

        /// <summary>
        /// Sizes the camera and centres the board in the part of the screen the
        /// HUD leaves free.
        /// </summary>
        /// <remarks>
        /// <b>Four reservations since GUI increment F:</b> the squad rail or
        /// the dev panel on the left, the log on the right, the top bar and the
        /// action tray. The board is fitted into the rectangle between them,
        /// and the camera moves so that rectangle's centre is the board's.
        ///
        /// <b>The camera moves away from a panel, not toward it.</b> Moving a
        /// camera right pushes the world left on screen, so the shift is the
        /// negative of the free rectangle's offset. Getting this backwards once
        /// herded the board under the panel.
        ///
        /// <b>Width is sized for, not just height.</b> The side panels take a
        /// fixed width, so the share of the view they eat grows as the window
        /// narrows. HUD sizes are canvas units and are converted with the
        /// canvas scale factor (ADR-0008 consequence 5). The OnGUI panel's
        /// width is in pixels.
        /// </remarks>
        private void FrameCamera()
        {
            var camera = Camera.main;
            if (camera == null) return;

            camera.orthographic = true;

            // Solid colour, not the skybox. A 2D board rendered against a sky
            // gradient washes out the cells, and clearFlags defaults to Skybox
            // on every camera the templates create.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiTheme.BoardVoid;

            float extent = _layout?.Extent ?? 8f;
            float aspect = Mathf.Max(0.1f, camera.aspect);
            float scale = _hudRoot != null ? _hudRoot.ScaleFactor : 1f;

            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);

            float leftUnits = LeftReservedUnits(scale);
            float rightUnits = HistoryStrip.ReservedWidth;

            float left = Mathf.Clamp(leftUnits * scale / width, 0f, 0.45f);
            float right = Mathf.Clamp(rightUnits * scale / width, 0f, 0.45f);
            float top = Mathf.Clamp(TurnStrip.ReservedHeight * scale / height, 0f, 0.3f);
            float bottom = Mathf.Clamp(ActionTray.ReservedHeight * scale / height, 0f, 0.35f);

            // Never let the panels claim so much of a tiny window that the board
            // is sized into nothing.
            float usableWidth = Mathf.Max(0.25f, 1f - left - right);
            float usableHeight = Mathf.Max(0.25f, 1f - top - bottom);

            camera.orthographicSize = Mathf.Max(
                extent * FrameMargin / usableHeight,
                extent * FrameMargin / (aspect * usableWidth));

            float halfWidth = camera.orthographicSize * aspect;
            float halfHeight = camera.orthographicSize;

            // Centre of the free rectangle, as an offset from the screen centre
            // in viewport units (bottom-left origin).
            float centreX = left + usableWidth * 0.5f - 0.5f;
            float centreY = bottom + usableHeight * 0.5f - 0.5f;

            camera.transform.position = new Vector3(
                -centreX * 2f * halfWidth,
                -centreY * 2f * halfHeight,
                -10f);

            if (_tray != null) _tray.SetInsets(leftUnits, rightUnits);
            if (_toasts != null) _toasts.SetArea(leftUnits, rightUnits, TurnStrip.ReservedHeight);
            if (_banner != null) _banner.SetArea(leftUnits, rightUnits, TurnStrip.ReservedHeight);
            if (_turnButton != null) _turnButton.SetArea(leftUnits, rightUnits, TurnStrip.ReservedHeight, ActionTray.ReservedHeight);

            _framedWidth = Screen.width;
            _framedHeight = Screen.height;
            _framedWithPanel = showDevPanel;
            _framedLegacy = useLegacyPanel;
            _framedScale = scale;
        }

        /// <summary>
        /// Canvas units taken on the left: the dev panel, the OnGUI panel
        /// (converted from pixels), or the squad rail.
        /// </summary>
        private float LeftReservedUnits(float scale)
        {
            if (!showDevPanel) return SquadRail.ReservedWidth;
            if (useLegacyPanel) return PanelWidth / Mathf.Max(0.01f, scale);
            return ControlPanel.ReservedWidth;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) showDevPanel = !showDevPanel;

            if (Input.GetKeyDown(KeyCode.F2)) useLegacyPanel = !useLegacyPanel;

            if (Input.GetKeyDown(KeyCode.H)) showPieceHealth = !showPieceHealth;

            // Driven every frame rather than on the keypress, so flipping the
            // inspector checkbox works too.
            if (_pieceHud != null) _pieceHud.Visible = showPieceHealth;

            if (Input.GetKeyDown(KeyCode.L)) showFullLog = !showFullLog;

            if (_controls != null)
            {
                _controls.Visible = showDevPanel && !useLegacyPanel;

                // The key legend moved to the top bar (GUI increment F).
                _controls.HintVisible = false;
            }

            if (_rail != null) _rail.Visible = !showDevPanel;
            if (_logPanel != null) _logPanel.Expanded = showFullLog;

            // Game view resizing is routine while prototyping, and both the size
            // and the shift depend on aspect and on whether the panel is up.
            // The scale factor is checked too: the CanvasScaler updates it a
            // frame after a resize, so the strip reservation catches up then.
            if (Screen.width != _framedWidth ||
                Screen.height != _framedHeight ||
                showDevPanel != _framedWithPanel ||
                useLegacyPanel != _framedLegacy ||
                (_hudRoot != null && !Mathf.Approximately(_hudRoot.ScaleFactor, _framedScale)))
            {
                FrameCamera();
            }

            if (_match == null) return;

            HandleKeys();
            UpdateHover();

            if (Input.GetMouseButtonDown(0)) HandleBoardClick();
            if (Input.GetMouseButtonDown(1)) StepBack();
        }

        // ── Board-first input ────────────────────────────────────────────

        private const float PieceClickRadius = 0.45f;   // cells
        private const float CellSnapRadius = 0.55f;     // cells

        /// <summary>
        /// Keyboard shortcuts. Each one is a panel button pressed another way,
        /// so it goes through the same intent.
        /// </summary>
        private void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) StepBack();

            if (_match.Engine.MatchOver) return;

            if (Input.GetKeyDown(KeyCode.Space)) Host.Roll();
            if (Input.GetKeyDown(KeyCode.E)) Host.EndTurn();

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Host.Cast();

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectAbilityAt(0);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectAbilityAt(1);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectAbilityAt(2);
        }

        private void SelectAbilityAt(int index)
        {
            if (_selectedOperator == null) return;
            if (!_match.AbilitiesByOperator.TryGetValue(_selectedOperator.Id, out var abilities)) return;
            if (index < 0 || index >= abilities.Count) return;

            Host.ToggleAbility(abilities[index]);
            MarkHudDirty();
        }

        /// <summary>
        /// Undoes the last choice: the aim, then the ability, then the operator.
        /// </summary>
        private void StepBack()
        {

            if (_selectedTarget != null || _selectedCell != null)
            {
                _selectedTarget = null;
                _selectedCell = null;
            }
            else if (_selectedAbility != null) _selectedAbility = null;
            else if (_selectedOperator != null) _selectedOperator = null;
            else return;

            SelectionChanged();
        }

        /// <summary>
        /// Whether the pointer is over either panel. The EventSystem sees uGUI
        /// only, so the OnGUI rect is still guarded by hand while that panel
        /// exists (ADR-0008 consequence 4); both go in the same commit.
        /// </summary>
        private bool PointerOverPanel()
        {
            if (showDevPanel && useLegacyPanel)
            {
                var gui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (new Rect(10, 10, PanelWidth - 20f, Screen.height - 20).Contains(gui)) return true;
            }

            return BoardPointer.IsOverHud;
        }

        /// <summary>
        /// Lifts the piece under the pointer, but only if clicking it would do
        /// something: the current seat's own pieces, and legal targets.
        /// </summary>
        private void UpdateHover()
        {
            OperatorPiece hovered = null;

            if (!_match.Engine.MatchOver && !PointerOverPanel() && BoardPointer.TryWorldPoint(out var world))
            {
                var piece = BoardPointer.PieceAt(world, _pieces, PieceClickRadius * cellSpacing);

                if (piece != null && (IsCommandable(piece.Operator) || _castTargets.Contains(piece.Operator)))
                    hovered = piece;
            }

            if (ReferenceEquals(hovered, _hovered)) return;

            _hovered = hovered;
            RefreshMarks();
        }

        /// <summary>
        /// Turns a board click into an intent: aim, move, deploy or select.
        /// </summary>
        /// <remarks>
        /// <b>With an ability selected, a click aims it.</b> A cell ability
        /// snaps to the nearest legal cell, and a miss clears the aim rather
        /// than guessing, so a stray click never silently re-aims a strike. A
        /// target ability takes an amber-ringed piece. Clicking one of your own
        /// pieces that is not a legal target switches the selection to it.
        ///
        /// <b>Otherwise a landing wins over a piece, but only for the selected
        /// operator.</b> A landing can sit on an enemy (that is a collision) or
        /// a friend, and the player aimed at the landing. With nothing
        /// selected, a piece wins: clicking your own piece must never move a
        /// different operator whose landing happens to share its cell.
        ///
        /// <b>A deployable yard piece deploys on click.</b> Deploy spends no
        /// energy and lands on a safe cell, so it keeps the one-click rule
        /// PRESENTATION §4 gives it.
        /// </remarks>
        private void HandleBoardClick()
        {
            if (_match.Engine.MatchOver || PointerOverPanel()) return;
            if (!BoardPointer.TryWorldPoint(out var world)) return;

            var piece = BoardPointer.PieceAt(world, _pieces, PieceClickRadius * cellSpacing);

            if (_selectedOperator != null && _selectedAbility != null)
            {
                AimAt(world, piece);
                return;
            }

            if (_selectedOperator != null && TryMoveAt(world, focusOnSelected: true)) return;

            if (piece != null && IsCommandable(piece.Operator))
            {
                if (_match.Engine.CanDeploy(piece.Operator)) Host.Deploy(piece.Operator);
                else Host.ToggleOperator(piece.Operator);

                MarkHudDirty();
                return;
            }

            if (_selectedOperator == null)
            {
                TryMoveAt(world, focusOnSelected: false);
                return;
            }

            // A click on empty board lets go of the selection.
            Host.ToggleOperator(_selectedOperator);
        }

        private void AimAt(Vector3 world, OperatorPiece piece)
        {
            if (_selectedAbility.RequiresCell)
            {
                _selectedCell = BoardPointer.CellAt(world, _legalCells, _layout, CellSnapRadius * cellSpacing);
                SelectionChanged();
                return;
            }

            if (piece != null && _selectedAbility.RequiresTarget && _castTargets.Contains(piece.Operator))
            {
                Host.ToggleTarget(piece.Operator);
                return;
            }

            if (piece != null && IsCommandable(piece.Operator))
            {
                Host.ToggleOperator(piece.Operator);
                return;
            }

            if (_selectedAbility.RequiresTarget && _selectedTarget != null)
            {
                _selectedTarget = null;
                SelectionChanged();
            }
        }

        /// <summary>
        /// Moves the operator whose landing is under the pointer, if exactly
        /// one operator could land there.
        /// </summary>
        /// <remarks>
        /// <b>When several dice land on the same cell, the fewest pips win.</b>
        /// Rounding and HOME can make a single die and the whole roll arrive at
        /// the same place; spending less for the same result is never worse, and
        /// the label on the cell already shows the die this picks.
        /// </remarks>
        private bool TryMoveAt(Vector3 world, bool focusOnSelected)
        {
            var candidates = focusOnSelected
                ? _moveOptions.Where(o => ReferenceEquals(o.Operator, _selectedOperator)).ToList()
                : _moveOptions;

            if (candidates.Count == 0) return false;

            var cell = BoardPointer.CellAt(
                world, candidates.Select(o => o.Cell).Distinct(), _layout, CellSnapRadius * cellSpacing);

            if (cell == null) return false;

            var here = candidates.Where(o => o.Cell.Equals(cell.Value)).ToList();
            if (here.Select(o => o.Operator.Id).Distinct().Count() != 1) return false;

            var best = here.OrderBy(o => o.Pips).First();
            Host.Move(best.Operator, best.Die);
            MarkHudDirty();
            return true;
        }

        private bool IsCommandable(OperatorState op) =>
            !_match.Engine.MatchOver && op.Owner == _match.Engine.CurrentPlayer.Color;

        // ── Driving the engine ───────────────────────────────────────────

        private void Send(ICommand command, OperatorState castBy = null, AbilityDefinition cast = null) =>
            Handle(_match.Engine.Execute(command), immediate: false, castBy, cast);

        private void Handle(
            IReadOnlyList<IGameEvent> events, bool immediate,
            OperatorState castBy = null, AbilityDefinition cast = null)
        {
            foreach (var e in events)
            {
                _log.Add(e.ToString());

                // A rejection is information, not a failure. Surfacing it is how
                // a player learns the rules without a tutorial.
                if (e is CommandRejected) continue;
            }

            if (_log.Count > 200) _log.RemoveRange(0, _log.Count - 200);

            // The selection belongs to the seat that made it.
            if (_selectedOperator != null &&
                (_match.Engine.MatchOver || _selectedOperator.Owner != _match.Engine.CurrentPlayer.Color))
            {
                _selectedOperator = null;
                _selectedAbility = null;
                _selectedTarget = null;
                _selectedCell = null;
            }

            if (!immediate)
            {
                PlayFeedback(events);
                WalkMoves(events);
            }

            Reposition(immediate);
            RefreshHighlights();

            // Beacons and zones are redrawn from the engine every time
            // (ADR-0006 decision 6), so a spent one disappears on its own.
            if (_devices != null) _devices.Show(_match.Engine.ActiveCellEffects());

            if (_turnStrip != null) _turnStrip.Refresh(_match.Engine);
            if (_turnButton != null) _turnButton.Refresh(_match.Engine);

            ShowHistory(events, castBy, cast);
            MarkHudDirty();
        }

        /// <summary>
        /// Feeds a batch to the history strip, the toasts and the turn banner
        /// (GUI increment F2).
        /// </summary>
        /// <remarks>
        /// The banner opens when a batch begins a turn and fades as soon as the
        /// engine is past the roll, however the roll was made.
        /// </remarks>
        private void ShowHistory(IReadOnlyList<IGameEvent> events, OperatorState castBy, AbilityDefinition cast)
        {
            var engine = _match.Engine;
            var batch = HistoryFeed.Build(events, castBy, cast, engine.Round);

            if (_history != null) _history.Add(batch);
            if (_toasts != null) _toasts.Show(batch);

            if (_banner == null) return;

            if (batch.TurnBegan != null && !engine.MatchOver)
                _banner.Show(batch.TurnBegan.Player, engine.Round);

            if (engine.MatchOver || engine.Phase != TurnPhase.AwaitingRoll)
                _banner.Hide();
        }

        /// <summary>Redraws everything that depends on the selection.</summary>
        private void SelectionChanged()
        {
            RefreshHighlights();
            MarkHudDirty();
        }

        /// <summary>Asks every rebuilt HUD panel to redraw at the end of the frame.</summary>
        private void MarkHudDirty()
        {
            if (_controls != null) _controls.MarkDirty();
            if (_rail != null) _rail.MarkDirty();
            if (_tray != null) _tray.MarkDirty();
            if (_logPanel != null) _logPanel.MarkDirty();
        }

        // ── Intents (IControlPanelHost) ──────────────────────────────────
        //
        // Both panels act only through these, so a button does the same thing
        // in either one while they coexist. Explicit implementations keep them
        // off the component's public surface; the OnGUI panel reaches them
        // through Host.

        private IControlPanelHost Host => this;

        MatchFactory.Match IControlPanelHost.Match => _match;
        bool IControlPanelHost.RandomSquads => randomSquads;
        IReadOnlyList<string> IControlPanelHost.Log => _log;

        OperatorState IControlPanelHost.SelectedOperator => _selectedOperator;
        AbilityDefinition IControlPanelHost.SelectedAbility => _selectedAbility;
        OperatorState IControlPanelHost.SelectedTarget => _selectedTarget;
        CellRef? IControlPanelHost.SelectedCell => _selectedCell;

        bool IControlPanelHost.CastReady =>
            _selectedOperator != null &&
            _selectedAbility != null &&
            (!_selectedAbility.RequiresCell || _selectedCell != null) &&
            (!_selectedAbility.RequiresTarget || _selectedTarget != null);

        /// <remarks>
        /// <b>The engine decides who is legal.</b> Range, stealth and home
        /// columns are rules (§4) and the view must not evaluate them
        /// (PRESENTATION §1). The query also drops targets whose cast mode
        /// scopes every effect away — Neural Purge aimed at an enemy was never
        /// a legal cast — which is most of what keeps the list short.
        ///
        /// Allies are included because they always were legal: Velvet Rope
        /// pulls a friend, All-In Mauling heals one, Nanite Infusion and Neural
        /// Purge exist for them, and Translocation swaps with one. The first
        /// target list showed enemies only, so the roster's healing had never
        /// been castable at all.
        ///
        /// The caster is filtered out here rather than in the engine. Aiming at
        /// yourself is legal by §10's mode rule and would let the Bouncer heal
        /// himself for the price of the ability — defensible, undecided, and not
        /// something the UI should settle by offering it.
        /// </remarks>
        IReadOnlyList<OperatorState> IControlPanelHost.CastTargets()
        {
            if (_match == null || _selectedOperator == null || _selectedAbility == null)
                return new List<OperatorState>();

            return _match.Engine
                .LegalTargetsFor(_selectedOperator, _selectedAbility)
                .Where(o => !ReferenceEquals(o, _selectedOperator))
                .ToList();
        }

        void IControlPanelHost.Roll() => Send(new RollDiceCommand());

        void IControlPanelHost.EndTurn() => Send(new EndTurnCommand());

        void IControlPanelHost.Deploy(OperatorState op) => Send(new DeployCommand(op.Id));

        void IControlPanelHost.Move(OperatorState op, int? dieFace) => Send(new MoveCommand(op.Id, dieFace));

        void IControlPanelHost.ToggleOperator(OperatorState op)
        {
            _selectedOperator = ReferenceEquals(op, _selectedOperator) ? null : op;
            _selectedAbility = null;      // an ability belongs to its caster
            _selectedTarget = null;       // and a target belongs to its ability
            _selectedCell = null;         // as does a cell

            SelectionChanged();
        }

        void IControlPanelHost.ToggleAbility(AbilityDefinition ability)
        {
            if (_match == null || _selectedOperator == null || ability == null) return;

            bool chosen = _selectedAbility != null && _selectedAbility.Id == ability.Id;

            // Selecting needs the engine's say-so; clearing never does.
            if (!chosen && _match.Engine.CheckAbility(_selectedOperator, ability) != AbilityAvailability.Ready)
                return;

            _selectedAbility = chosen ? null : ability;
            _selectedTarget = null;
            _selectedCell = null;

            SelectionChanged();
        }

        void IControlPanelHost.ToggleTarget(OperatorState op)
        {
            _selectedTarget = ReferenceEquals(op, _selectedTarget) ? null : op;
            SelectionChanged();
        }

        void IControlPanelHost.Cast()
        {
            if (!Host.CastReady) return;

            Send(new UseAbilityCommand(
                _selectedOperator.Id, _selectedAbility.Id,
                _selectedAbility.RequiresTarget && _selectedTarget != null
                    ? _selectedTarget.Id
                    : (int?)null,
                _selectedAbility.RequiresCell ? _selectedCell : null),
                _selectedOperator, _selectedAbility);

            _selectedAbility = null;
            _selectedTarget = null;
            _selectedCell = null;

            // Send already refreshed the highlights, but with the old
            // selection still set; clear them now that it is gone.
            SelectionChanged();
        }

        void IControlPanelHost.Reseed()
        {
            seed++;
            NewMatch();
        }

        /// <summary>
        /// Turns damage, healing and neutralize events into one-off effects.
        /// </summary>
        /// <remarks>
        /// Played <b>before</b> pieces are repositioned, so an effect lands on
        /// the cell where the thing happened rather than where the victim ends
        /// up. That matters most for a neutralize: the operator's progress is
        /// already the yard by the time the event arrives, and the burst belongs
        /// on the cell it fell on.
        /// </remarks>
        private void PlayFeedback(IReadOnlyList<IGameEvent> events)
        {
            if (_feedback == null) return;

            foreach (var e in events)
            {
                var damaged = e as DamageDealt;
                if (damaged != null && damaged.Amount > 0)
                {
                    var piece = PieceFor(damaged.Target);
                    if (piece == null) continue;

                    _feedback.Damage(piece.transform.position, damaged.Amount, damaged.Cause);
                    piece.Flash();
                    continue;
                }

                var evaded = e as DamageEvaded;
                if (evaded != null)
                {
                    var piece = PieceFor(evaded.Target);
                    if (piece != null) _feedback.Evaded(piece.transform.position);
                    continue;
                }

                var absorbed = e as DamageAbsorbed;
                if (absorbed != null)
                {
                    var piece = PieceFor(absorbed.Target);
                    if (piece != null) _feedback.Absorbed(piece.transform.position);
                    continue;
                }

                var healed = e as HealApplied;
                if (healed != null)
                {
                    var piece = PieceFor(healed.Target);
                    if (piece != null) _feedback.Heal(piece.transform.position, healed.Amount);
                    continue;
                }

                var regen = e as OperatorRegenerated;
                if (regen != null)
                {
                    var piece = PieceFor(regen.Target);
                    if (piece != null) _feedback.Heal(piece.transform.position, regen.Amount);
                    continue;
                }

                var down = e as OperatorNeutralized;
                if (down != null)
                {
                    var piece = PieceFor(down.Operator);
                    if (piece != null)
                    {
                        _feedback.Neutralized(
                            piece.transform.position,
                            BoardLayout.ColourOf(down.Operator.Owner),
                            down.Cause);
                    }
                }
            }
        }

        private OperatorPiece PieceFor(OperatorState op) =>
            op == null ? null : _pieces.Find(p => ReferenceEquals(p.Operator, op));

        /// <summary>
        /// Turns each move into a sequence of cells to walk through.
        /// </summary>
        /// <remarks>
        /// Only forward travel is walked. A pull or swap reports the same
        /// progress twice and is placement rather than movement (§7.4);
        /// animating it as a walk would show the player a journey the rules say
        /// never happened. The step back of a bounce (§7.2) is placement too,
        /// which is why it settles rather than walks.
        ///
        /// This is the only place the view enumerates intermediate cells, which
        /// makes it the one thing that can expose a discontinuous layout: if two
        /// consecutive progress values are not adjacent on screen, the piece
        /// visibly leaps. That is how the arm-tip gap in the old 48-cell board
        /// was caught (ADR-0002 Amendment 6). Keep it walking one cell at a time.
        ///
        /// <b>A bounced move walks to the cell it attempted</b>, rests there,
        /// and then settles back to where the collision left it (PRESENTATION
        /// §3). The attempted landing comes from the event; the view does not
        /// work it out.
        ///
        /// <b>A split roll produces two walks, one per command</b> (§6). On two
        /// different pieces they run side by side and read fine. On the same
        /// piece twice they arrive back to back, and whether the second
        /// interrupts the first is <c>OperatorPiece.Walk</c>'s business —
        /// PRESENTATION §3 wants them sequenced, not overlapping.
        /// </remarks>
        private void WalkMoves(IReadOnlyList<IGameEvent> events)
        {
            foreach (var e in events)
            {
                var moved = e as OperatorMoved;
                if (moved == null || moved.AttemptedTo <= moved.From) continue;

                var piece = _pieces.Find(p => ReferenceEquals(p.Operator, moved.Operator));
                if (piece == null) continue;

                var path = new List<Vector3>();

                for (int progress = moved.From + 1; progress <= moved.AttemptedTo; progress++)
                    path.Add(_layout.PositionOf(_match.Map.CellAt(moved.Operator.Owner, progress)));

                Vector3? bouncedTo = moved.Bounced
                    ? _layout.PositionOf(_match.Map.CellAt(moved.Operator.Owner, moved.To))
                    : (Vector3?)null;

                piece.Walk(path, bouncedTo);
            }
        }

        /// <summary>
        /// Rebuilds the landing ghosts, the cells or pieces the selected ability
        /// can aim at, and every piece's marks.
        /// </summary>
        /// <remarks>
        /// Landings come from the engine rather than being recomputed here.
        /// Distance depends on slows and auras, and a preview that did its own
        /// arithmetic would disagree with the rules exactly when a player is
        /// leaning on it.
        ///
        /// Since a roll can be split (§6) the engine reports several options per
        /// operator. The pooled landing is drawn bold and each single-die landing
        /// faintly, so what splitting costs is visible on the board before a die
        /// is clicked rather than discovered after.
        ///
        /// <b>A selected operator gets its landings alone, labelled with the
        /// pips each spends.</b> With nothing selected, every landing is shown
        /// unlabelled: three operators' labels at once read as one menu when
        /// they are three.
        /// </remarks>
        private void RefreshHighlights()
        {
            if (_highlights == null || _match == null) return;

            _highlights.Clear();
            _cellLabels?.Clear();
            _moveOptions.Clear();
            _legalCells = new List<CellRef>();
            _castTargets = new List<OperatorState>();

            if (_match.Engine.MatchOver)
            {
                RefreshMarks();
                return;
            }

            CollectMoveOptions();
            DrawMoveOptions();

            if (_selectedOperator != null && _selectedAbility != null && !_selectedOperator.IsInYard)
                DrawAim();

            RefreshMarks();
        }

        private void CollectMoveOptions()
        {
            int pooledPips = _match.Engine.UnspentDice.Sum();

            foreach (var landing in _match.Engine.PreviewLandings())
            {
                var op = _match.Operators.FirstOrDefault(o => o.Id == landing.OperatorId);
                if (op == null) continue;

                _moveOptions.Add(new MoveOption(
                    op, landing.DieFace, landing.DieFace ?? pooledPips,
                    _match.Map.CellAt(op.Owner, landing.Progress)));
            }
        }

        private void DrawMoveOptions()
        {
            bool focus = _selectedOperator != null &&
                         _moveOptions.Any(o => ReferenceEquals(o.Operator, _selectedOperator));

            var shown = focus
                ? _moveOptions.Where(o => ReferenceEquals(o.Operator, _selectedOperator))
                : _moveOptions;

            var pooled = new List<CellRef>();
            var perDie = new List<CellRef>();

            // One marker per operator per cell: the option a click would pick.
            foreach (var group in shown.GroupBy(o => (o.Operator.Id, o.Cell)))
            {
                var best = group.OrderBy(o => o.Pips).First();
                bool bold = group.Any(o => o.Die == null);

                (bold ? pooled : perDie).Add(best.Cell);

                if (focus && _cellLabels != null)
                {
                    var at = _layout.PositionOf(best.Cell) + new Vector3(0.3f, 0.3f, 0f) * cellSpacing;
                    _cellLabels.Show(at, best.Pips.ToString(), strong: bold);
                }
            }

            _highlights.ShowLandings(pooled, perDie);
        }

        private void DrawAim()
        {
            // A cell-targeted ability shows the cells a cast would actually
            // accept instead of a bare range ring — range, home columns and
            // the camping rule (§4.4) come pre-applied from the engine, and
            // the chosen cell is drawn bold so the aim is visible before the
            // energy is spent.
            if (_selectedAbility.RequiresCell)
            {
                _legalCells = _match.Engine.LegalCellsFor(_selectedOperator, _selectedAbility);

                // A choice can stop being legal under the player's feet — the
                // caster stepped onto a safe cell, say — so a stale cell is
                // dropped rather than cast.
                if (_selectedCell != null && !_legalCells.Contains(_selectedCell.Value))
                    _selectedCell = null;

                _highlights.ShowCellTargets(_legalCells, _selectedCell);
                return;
            }

            if (_selectedAbility.RequiresTarget)
            {
                _castTargets = Host.CastTargets();

                if (_selectedTarget != null && !_castTargets.Contains(_selectedTarget))
                    _selectedTarget = null;
            }

            // An unlimited range has no ring to draw, and handing int.MaxValue to
            // a routine that iterates it is not a large highlight — it is a hang.
            if (!_selectedAbility.HasUnlimitedRange)
            {
                _highlights.ShowRange(
                    _match.Map,
                    _match.Map.CellAt(_selectedOperator.Owner, _selectedOperator.Progress),
                    _selectedAbility.Range);
            }
        }

        /// <summary>
        /// Tells every piece how it is marked. Every mark is an engine answer
        /// or the player's own selection.
        /// </summary>
        private void RefreshMarks()
        {
            if (_match == null) return;

            var engine = _match.Engine;

            foreach (var piece in _pieces)
            {
                if (piece == null) continue;

                var op = piece.Operator;
                var marks = PieceMark.None;

                if (ReferenceEquals(op, _selectedOperator)) marks |= PieceMark.Selected;
                if (ReferenceEquals(op, _selectedTarget)) marks |= PieceMark.Target;
                else if (_castTargets.Contains(op)) marks |= PieceMark.Targetable;
                if (!engine.MatchOver && IsCommandable(op) && engine.CanDeploy(op)) marks |= PieceMark.Deployable;
                if (ReferenceEquals(piece, _hovered)) marks |= PieceMark.Hovered;

                piece.SetMarks(marks);
            }
        }

        /// <summary>
        /// Places every piece, fanning out operators that share a cell.
        /// </summary>
        /// <remarks>
        /// The fan matters more than it looks: §7.5 makes a stack of enemies a
        /// real situation, and a player has to be able to see that two pieces
        /// are on one square before deciding to charge it.
        /// </remarks>
        private void Reposition(bool immediate)
        {
            var byCell = new Dictionary<CellRef, List<OperatorPiece>>();

            foreach (var piece in _pieces)
            {
                var cell = _match.Map.CellAt(piece.Operator.Owner, piece.Operator.Progress);

                if (!byCell.TryGetValue(cell, out var list))
                {
                    list = new List<OperatorPiece>();
                    byCell[cell] = list;
                }

                list.Add(piece);
            }

            foreach (var pair in byCell)
            {
                var basePosition = _layout.PositionOf(pair.Key);

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    var piece = pair.Value[i];
                    var position = basePosition + _layout.Offset(i, pair.Value.Count);

                    if (immediate) piece.Place(position);
                    else piece.Settle(position);

                    // Statuses come from the engine, never from replaying
                    // StatusApplied/StatusExpired (PRESENTATION §1). Evasion is
                    // drawn on the piece; everything else is a tag.
                    var statuses = _match.Engine.ActiveStatusesOn(piece.Operator);

                    piece.Refresh(evasive: statuses.Contains(StatusKind.Evasion));

                    if (_pieceHud != null)
                    {
                        _pieceHud.ShowStatuses(piece, statuses);

                        // Yard pieces show no readouts, so only stacks on the track spread.
                        _pieceHud.SetStack(piece, i, pair.Value.Count);
                    }
                }
            }
        }

        // ── Controls ─────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_match == null) return;

            // Rich text is off by default on the built-in skin, so the markup
            // below would otherwise render as literal angle brackets.
            GUI.skin.label.richText = true;

            // The key hint moved to the uGUI panel, which shows it in both
            // modes whenever the panel is hidden.
            if (!showDevPanel || !useLegacyPanel) return;

            GUILayout.BeginArea(new Rect(10, 10, PanelWidth - 20f, Screen.height - 20), GUI.skin.box);

            // The panel outgrew the window the first time a caster was selected
            // with six enemies on the board. Scrolling is the floor, not the
            // fix — see DrawAbilities for the fix.
            _panelScroll = GUILayout.BeginScrollView(_panelScroll);
            DrawPanel();
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawPanel()
        {
            var engine = _match.Engine;
            var seat = engine.CurrentPlayer;

            GUILayout.Label(engine.MatchOver
                ? "MATCH OVER"
                : $"<b>{seat.Color}</b>  turn {seat.TurnIndex}   energy {seat.Energy}");

            // Read the profile's own description rather than restating its
            // numbers. A hardcoded label is a second place to forget to update,
            // which is how "Standard 48x1" would have outlived the 48-cell board.
            GUILayout.Label($"{_match.Map.Profile}   phase {engine.Phase}");

            GUILayout.Label($"<i>{(randomSquads ? "drafted squads" : "alpha three")} — Tab hides this, H toggles health, F2 switches panel</i>");

            GUILayout.Space(6);

            if (GUILayout.Button("New match (reseed)"))
            {
                Host.Reseed();
                return;
            }

            if (engine.MatchOver)
            {
                DrawLog();
                return;
            }

            GUILayout.Space(6);

            // Movement is compulsory (§6.1), so the engine refuses End Turn
            // while a die is still spendable. Greying it out says so before the
            // click rather than after — the same reasoning that put
            // CheckAbility behind the ability tray. Roll stays enabled: it is
            // the one legal action at AwaitingRoll, and an illegal re-roll is
            // answered with a rejection in the log.
            bool owesMovement = engine.MustSpendRoll;
            var dice = engine.UnspentDice;

            GUILayout.Label(dice.Count == 0
                ? "<i>no dice in hand</i>"
                : $"unspent: <b>{Faces(dice)}</b>");

            if (GUILayout.Button("Roll")) Host.Roll();

            GUI.enabled = !owesMovement;
            if (GUILayout.Button(owesMovement ? "End turn — spend your roll first" : "End turn"))
                Host.EndTurn();
            GUI.enabled = true;

            GUILayout.Space(8);
            GUILayout.Label("<b>Your operators</b>");

            foreach (var op in seat.Operators)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label($"{op.Name} {op.Health}/{op.MaxHealth}", GUILayout.Width(110));

                if (op.IsInYard)
                {
                    if (GUILayout.Button("Deploy", GUILayout.Width(78))) Host.Deploy(op);
                }
                else
                {
                    DrawMoveButtons(op, dice);
                }

                bool selected = ReferenceEquals(op, _selectedOperator);
                if (GUILayout.Toggle(selected, "cast", GUI.skin.button, GUILayout.Width(42)) != selected)
                    Host.ToggleOperator(op);

                GUILayout.EndHorizontal();
            }

            if (_selectedOperator != null) DrawAbilities(seat);

            DrawLog();
        }

        /// <summary>
        /// One button per way this operator could spend the roll: the whole thing
        /// at once, or a single die.
        /// </summary>
        /// <remarks>
        /// The split buttons only appear when there is a split to make. With one
        /// die left there is nothing to choose, and on a double the two faces are
        /// equal, so a second button would be a second way to press the first.
        ///
        /// Labelled with pips rather than cells. Cells depend on the operator's
        /// speed and the board already shows where each option lands, so putting
        /// the converted number here would be a third place for the same
        /// arithmetic to live.
        /// </remarks>
        private void DrawMoveButtons(OperatorState op, IReadOnlyList<int> dice)
        {
            if (dice.Count == 0)
            {
                GUILayout.Label("—", GUILayout.Width(78));
                return;
            }

            if (dice.Count == 1)
            {
                if (GUILayout.Button($"Move {dice[0]}", GUILayout.Width(78)))
                    Host.Move(op, null);

                return;
            }

            int total = 0;
            for (int i = 0; i < dice.Count; i++) total += dice[i];

            if (GUILayout.Button($"{total}", GUILayout.Width(30))) Host.Move(op, null);

            for (int i = 0; i < dice.Count; i++)
            {
                if (SeenEarlier(dice, i)) continue;

                if (GUILayout.Button($"{dice[i]}", GUILayout.Width(22)))
                    Host.Move(op, dice[i]);
            }
        }

        private static bool SeenEarlier(IReadOnlyList<int> dice, int index)
        {
            for (int i = 0; i < index; i++)
                if (dice[i] == dice[index]) return true;

            return false;
        }

        private static string Faces(IReadOnlyList<int> dice)
        {
            var text = "";

            for (int i = 0; i < dice.Count; i++)
                text += i == 0 ? dice[i].ToString() : $" + {dice[i]}";

            return text;
        }

        /// <summary>
        /// The caster's abilities, then — only if the chosen one needs one — a
        /// target.
        /// </summary>
        /// <remarks>
        /// <b>Ability first, and that ordering is the fix.</b> Asking for a
        /// target before knowing the ability meant listing every operator on the
        /// board: eleven buttons at four seats, and the panel ran off the bottom
        /// of the window. Most were never legal for the cast that followed.
        ///
        /// <b>Several abilities take no target at all</b> — every self-origin
        /// area, and both of Kian's, who has no single-target ability whatsoever.
        /// For those the list is not merely long, it is entirely noise.
        /// </remarks>
        private void DrawAbilities(PlayerState seat)
        {
            GUILayout.Space(8);
            GUILayout.Label($"<b>{_selectedOperator.Name}</b>");

            if (!_match.AbilitiesByOperator.TryGetValue(_selectedOperator.Id, out var abilities)) return;

            // Select, then cast. The extra click buys a look at the range before
            // spending energy, which matters on a board where reach turned out
            // to be the binding constraint on the whole combat layer.
            foreach (var ability in abilities)
            {
                bool chosen = _selectedAbility != null && _selectedAbility.Id == ability.Id;

                // Readiness comes from the engine, not from the view checking
                // energy and cooldowns itself (ADR-0004 amendment). Before this,
                // a player found out an ability was on cooldown by pressing it
                // and reading the rejection.
                var availability = _match.Engine.CheckAbility(_selectedOperator, ability);
                bool usable = availability == AbilityAvailability.Ready;

                string reach = ability.HasUnlimitedRange ? "any" : $"r{ability.Range}";

                string label = usable
                    ? $"{ability.Name}  ({ability.EnergyCost}e, {reach})"
                    : $"{ability.Name}  — {ControlPanel.Explain(availability)}";
                var previous = GUI.color;
                if (!usable) GUI.color = new Color(0.6f, 0.6f, 0.62f);

                if (GUILayout.Toggle(chosen, label, GUI.skin.button) != chosen && usable)
                    Host.ToggleAbility(ability);

                GUI.color = previous;
            }

            if (_selectedAbility == null) return;

            if (_selectedAbility.RequiresTarget) DrawTargetList(seat);
            else if (_selectedAbility.RequiresCell)
            {
                GUILayout.Label(_selectedCell == null
                    ? "<i>click a highlighted cell on the board</i>"
                    : $"<i>target cell: <b>{_selectedCell.Value}</b> — click another to change</i>");
            }
            else GUILayout.Label("<i>no target — it fires around the caster</i>");

            GUILayout.Space(2);

            bool ready = Host.CastReady;

            string missing = _selectedAbility.RequiresCell ? "click a cell" : "pick a target";

            GUI.enabled = ready;

            if (GUILayout.Button(ready
                    ? $"CAST {_selectedAbility.Name}"
                    : $"CAST {_selectedAbility.Name} — {missing}"))
            {
                Host.Cast();
            }

            GUI.enabled = true;
        }

        /// <summary>
        /// Who this cast could be aimed at, split by side. The list comes from
        /// <see cref="IControlPanelHost.CastTargets"/>; its remarks say why it
        /// holds what it holds.
        /// </summary>
        private void DrawTargetList(PlayerState seat)
        {
            GUILayout.Space(4);
            GUILayout.Label("<b>Target</b>");

            var legal = Host.CastTargets();

            if (legal.Count == 0)
            {
                GUILayout.Label("<i>nothing in reach</i>");
                return;
            }

            DrawTargets("Enemies", legal.Where(o => o.Owner != seat.Color));
            DrawTargets("Allies", legal.Where(o => o.Owner == seat.Color));
        }

        /// <summary>One side's targets, or nothing at all if that side has none.</summary>
        private void DrawTargets(string heading, IEnumerable<OperatorState> candidates)
        {
            var list = candidates.ToList();
            if (list.Count == 0) return;

            GUILayout.Label($"<i>{heading}</i>");

            foreach (var candidate in list)
            {
                bool selected = ReferenceEquals(candidate, _selectedTarget);

                if (GUILayout.Toggle(selected,
                        $"{candidate.Owner} {candidate.Name} {candidate.Health}/{candidate.MaxHealth}",
                        GUI.skin.button) != selected)
                {
                    Host.ToggleTarget(candidate);
                }
            }
        }

        /// <summary>
        /// The last handful of events, newest first.
        /// </summary>
        /// <remarks>
        /// No scroll of its own: the whole panel scrolls now, and a scroll inside
        /// a scroll is miserable to use — the outer one steals the wheel the
        /// moment the pointer leaves the inner rect. Fewer lines shown instead,
        /// since the log is a tail rather than a record.
        /// </remarks>
        private void DrawLog()
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>Events</b>");

            for (int i = _log.Count - 1; i >= 0 && i > _log.Count - 18; i--)
                GUILayout.Label(_log[i]);
        }
    }
}
