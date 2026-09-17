// Assets/_Project/Scripts/Unity/Composition/MatchBootstrap.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NonaRoyale.Unity.Audio;
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
    ///
    /// <b>Esc with nothing selected pauses</b> (GUI increment H). While the
    /// pause menu is open, the board and the game keys are ignored.
    ///
    /// <b>The app flow</b> (GUI increments I and J). Play opens the title
    /// screen over the empty room, unless Skip Title is on. PLAY leads to
    /// setup, DEAL to the match, a win to the end screen, and MAIN MENU from
    /// the pause menu or the end screen back to the title, which tears the
    /// match down. Which screen is showing is read from the cards themselves
    /// (<see cref="CurrentScreen"/>), so a card closing itself can never leave the
    /// flow out of step. While any card is open, the board and the game keys
    /// are ignored.
    ///
    /// <b>Batches play one after another</b> (MOTION.md increment MO1). The
    /// engine answers at once; the board catches up through a
    /// <see cref="PresentationQueue"/>: dice, walks, hits, knockouts, then the
    /// settle that repositions pieces and refreshes the HUD. Commands wait
    /// while it is busy. Space and E pressed near the end of a step are kept
    /// briefly and sent when it finishes; the CPU driver waits for it too.
    /// MO2 adds the cast tell and the deploy rise to the sequence, and the
    /// knockout shatter, hit-stop and camera nudge to its hits. One
    /// <see cref="MotionSettings"/> object carries Reduced motion, the
    /// animation speed and the hurry to every animated view.
    ///
    /// <b>Sound follows the same steps</b> (AUDIO.md increment AU1). Each
    /// presentation step asks the <see cref="AudioDirector"/> for its cue as
    /// it starts, and <c>Update</c> tells the director which music fits the
    /// screen and whether the pause menu is open.
    ///
    /// <b>Display settings persist</b> (increment J): health labels, the log
    /// and the dev panel are loaded from <c>PlayerPrefs</c> at Start, with the
    /// inspector values as first-run defaults, and saved whenever they change.
    /// </remarks>
    public sealed class MatchBootstrap : MonoBehaviour, IControlPanelHost, IPauseHost, IMatchFlowHost, ITitleHost
    {
        [Header("Flow")]
        [Tooltip("Deal straight into a match with the settings below, skipping the title and setup screens.")]
        public bool skipSetup = false;

        [Header("Match")]
        // Replaced randomSquads in DR2, so the scene's saved value does not
        // carry over: the first deal now defaults to ALL PICK.
        [Tooltip("How squads are chosen at the first deal; the setup screen overrides it. " +
                 "All Pick and Snake open the draft screen. Alpha Three is what every measurement in ADR-0002 used.")]
        public SquadMode squadMode = SquadMode.AllPick;

        [Tooltip("Compact 28x2 has a longer journey than Standard since ADR-0002 " +
                 "Amendment 6 (59 against 58). Kept switchable for comparison only.")]
        public bool useCompactBoard = false;

        [Tooltip("Seats at the first deal, filled Red, Blue, Green, Violet. The setup screen overrides it.")]
        [Range(2, 4)] public int players = 4;

        [Tooltip("Seats the CPU plays at the first deal (useful with Skip Setup). The setup screen overrides it.")]
        public PlayerColor[] cpuSeats = new PlayerColor[0];

        [Tooltip("How fast CPU seats act. Remembered between sessions; this is the first-run default.")]
        public BotSpeed cpuSpeed = BotSpeed.Normal;

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

        [Tooltip("No shake, hit-stop, hop or idle sway; shorter tweens. Remembered; this is the first-run default.")]
        public bool reducedMotion = false;

        [Tooltip("How fast every seat's actions animate. Remembered; this is the first-run default.")]
        public AnimationSpeed animationSpeed = AnimationSpeed.Normal;

        [Tooltip("Pools of light, the powered-cell glow and bloom (LIGHTING.md). Off is the flat room. " +
                 "Remembered; this is the first-run default.")]
        public bool lightingEffects = true;

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
        private PauseMenu _pause;
        private SetupScreen _setup;
        private EndScreen _end;
        private TitleScreen _title;
        private DraftScreen _draftScreen;
        private bool _endQueued;

        /// <summary>
        /// The squads the last draft produced, kept for REMATCH (DRAFT.md
        /// decision 7). Null outside the drafted modes.
        /// </summary>
        private IReadOnlyDictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> _squads;

        /// <summary>Who plays each seat, and each CPU's style. Only the kinds and personalities are read.</summary>
        private readonly MatchSettings _seatPlan = new MatchSettings(new PlayerColor[0], SquadMode.AllPick, 0);

        /// <summary>Plays the CPU seats of the match on the table (BOT2). Null without a match.</summary>
        private BotDriver _bots;

        // ── Presentation (MO1) ───────────────────────────────────────────

        /// <summary>How long a hit's numbers own the board before the next step, in scaled seconds.</summary>
        private const float HitHoldSeconds = 0.3f;

        /// <summary>How long a knockout burst shows before the piece returns to its yard, in scaled seconds.</summary>
        private const float KnockoutHoldSeconds = 0.45f;

        /// <summary>How long a Space or E press waits for a busy board, in real seconds.</summary>
        private const float InputBufferSeconds = 0.4f;

        /// <summary>The queue and dice run this much faster while Space hurries a CPU turn.</summary>
        private const float HurrySpeed = 3f;

        private enum BufferedIntent { None, Roll, EndTurn }

        /// <summary>A hit this big, as a share of the target's maximum health, gets the hit-stop and the nudge.</summary>
        private const float BigHitFraction = 0.3f;

        /// <summary>How long a big hit freezes game time, in real seconds.</summary>
        private const float HitStopSeconds = 0.06f;

        /// <summary>A knockout's freeze, in real seconds.</summary>
        private const float KnockoutStopSeconds = 0.09f;

        /// <summary>Camera nudge on a big hit, and on a knockout, in cells.</summary>
        private const float HitNudgeCells = 0.08f;
        private const float KnockoutNudgeCells = 0.14f;

        /// <summary>How long a deploy's rise owns the board, in scaled seconds.</summary>
        private const float RiseHoldSeconds = 0.3f;

        private PresentationQueue _queue;
        private DiceRoller _dice;
        private CastTell _tells;
        private CameraNudge _nudge;
        private HitStop _hitStop;
        private SceneLighting _lighting;

        /// <summary>Light that answers play: selection, casts, knockouts, HOME (LT2).</summary>
        private EventLights _eventLights;

        /// <summary>Plays every sound (AU1). Built once, at Start; it outlives matches.</summary>
        private AudioDirector _audio;

        /// <summary>The volume settings, shared with the director and the sound page (AU1).</summary>
        private readonly AudioLevels _levels = new AudioLevels();
        private readonly AudioLevels _savedLevels = new AudioLevels();

        /// <summary>Reduced motion, animation speed and hurry, shared with every animated view (MO2).</summary>
        private readonly MotionSettings _motion = new MotionSettings();

        /// <summary>True from a roll's arrival until its settle: the tray waits for the dice moment.</summary>
        private bool _diceHeld;

        private BufferedIntent _buffered;
        private float _bufferedAt;

        /// <summary>Whether the board is still catching up with the engine.</summary>
        private bool Busy => _queue != null && _queue.IsBusy;

        /// <summary>The screens the app moves between (GUI increment J).</summary>
        public enum AppScreen { Title, Setup, Draft, Match, Paused, Results }

        /// <summary>
        /// Which screen is showing, read from the open cards, topmost first.
        /// No card open means the match itself.
        /// </summary>
        public AppScreen CurrentScreen =>
            _title != null && _title.IsOpen ? AppScreen.Title :
            _setup != null && _setup.IsOpen ? AppScreen.Setup :
            _draftScreen != null && _draftScreen.IsOpen ? AppScreen.Draft :
            _pause != null && _pause.IsOpen ? AppScreen.Paused :
            _end != null && _end.IsOpen ? AppScreen.Results :
            AppScreen.Match;

        private bool _savedHealth, _savedLog, _savedDev;
        private BotSpeed _savedSpeed;
        private bool _savedReduced;
        private bool _savedLighting;
        private AnimationSpeed _savedAnimation;

        /// <summary>The seats the next deal uses. Squads and seed live in the inspector fields.</summary>
        private readonly List<PlayerColor> _seats = new List<PlayerColor>();
        private Vector2 _panelScroll;

        private void Start()
        {
            _seats.Clear();
            _seats.AddRange(MatchSettings.AllSeats.Take(players));

            if (cpuSeats != null)
                foreach (var seat in cpuSeats) _seatPlan.SetKind(seat, SeatKind.Cpu);

            LoadSettings();
            SyncMotion();

            // URP's 2D Renderer lights every sprite, so the room's lights come before any board (ADR-0010, LT1).
            _lighting = Ensure<SceneLighting>();
            _lighting.Build();
            _eventLights = Ensure<EventLights>();

            // Before the first framing, which hands the camera's resting place to the nudge.
            _nudge = Ensure<CameraNudge>();
            _nudge.Bind(_motion);
            _hitStop = Ensure<HitStop>();
            _hitStop.Bind(_motion);

            _audio = Ensure<AudioDirector>();
            _audio.Bind(_levels);
            UiKit.ButtonPressed -= OnUiButton;
            UiKit.ButtonPressed += OnUiButton;

            _hudRoot = GetComponent<HudRoot>() ?? gameObject.AddComponent<HudRoot>();
            BindScreens();

            if (skipSetup)
            {
                // A drafted mode still opens the draft; the others deal at once.
                ((IMatchFlowHost)this).Deal(new MatchSettings(_seats, squadMode, seed));
                return;
            }

            ShowTitle();
        }

        // ── Settings (GUI increment J) ───────────────────────────────────

        private void LoadSettings()
        {
            showPieceHealth = SettingsStore.Load(SettingsStore.PieceHealth, showPieceHealth);
            showFullLog = SettingsStore.Load(SettingsStore.FullLog, showFullLog);
            showDevPanel = SettingsStore.Load(SettingsStore.DevPanel, showDevPanel);

            cpuSpeed = SettingsStore.LoadSpeed(cpuSpeed);
            reducedMotion = SettingsStore.Load(SettingsStore.ReducedMotion, reducedMotion);
            animationSpeed = SettingsStore.LoadAnimationSpeed(animationSpeed);
            lightingEffects = SettingsStore.Load(SettingsStore.Lighting, lightingEffects);

            _savedHealth = showPieceHealth;
            _savedLog = showFullLog;
            _savedDev = showDevPanel;
            _savedSpeed = cpuSpeed;
            _savedReduced = reducedMotion;
            _savedAnimation = animationSpeed;
            _savedLighting = lightingEffects;

            SettingsStore.LoadAudio(_levels);
            _savedLevels.CopyFrom(_levels);
        }

        /// <summary>Saves when a flag changed, however it changed: a key, a menu toggle, the inspector.</summary>
        private void SaveSettingsIfChanged()
        {
            if (cpuSpeed != _savedSpeed)
            {
                _savedSpeed = cpuSpeed;
                SettingsStore.SaveSpeed(cpuSpeed);
            }

            if (reducedMotion != _savedReduced || animationSpeed != _savedAnimation)
            {
                _savedReduced = reducedMotion;
                _savedAnimation = animationSpeed;
                SettingsStore.SaveMotion(reducedMotion, animationSpeed);
            }

            if (lightingEffects != _savedLighting)
            {
                _savedLighting = lightingEffects;
                SettingsStore.SaveLighting(lightingEffects);
            }

            // Not mid-drag: a slider reports every frame it moves, and each save flushes to disk.
            if (!_levels.SameAs(_savedLevels) && !Input.GetMouseButton(0))
            {
                _savedLevels.CopyFrom(_levels);
                SettingsStore.SaveAudio(_levels);
            }

            if (showPieceHealth == _savedHealth && showFullLog == _savedLog && showDevPanel == _savedDev) return;

            _savedHealth = showPieceHealth;
            _savedLog = showFullLog;
            _savedDev = showDevPanel;
            SettingsStore.Save(showPieceHealth, showFullLog, showDevPanel);
        }

        // ── Screens (GUI increments I and J) ─────────────────────────────

        /// <summary>
        /// The title over the empty room. Tears down a match if one is on the
        /// table, and hides the in-match HUD.
        /// </summary>
        private void ShowTitle()
        {
            if (_pause != null) _pause.Close();
            if (_setup != null) _setup.Close();
            if (_draftScreen != null) _draftScreen.Close();
            if (_end != null) _end.Close();

            TearDownMatch();
            ShowEmptyTable();
            _title.Open();
        }

        /// <summary>
        /// Removes the match: pieces, marks, devices, labels and history. The
        /// HUD widgets stay built and hidden, and rebind on the next deal.
        /// </summary>
        private void TearDownMatch()
        {
            foreach (var piece in _pieces)
                if (piece != null) Destroy(piece.gameObject);

            StopPresentation();

            _pieces.Clear();
            _log.Clear();
            _match = null;
            _bots = null;
            SetHovered(null);
            _selectedOperator = null;
            _selectedTarget = null;
            _selectedAbility = null;
            _selectedCell = null;
            _legalCells = new List<CellRef>();
            _castTargets = new List<OperatorState>();
            _moveOptions.Clear();
            _endQueued = false;

            if (_highlights != null) _highlights.Clear();
            if (_devices != null) _devices.Clear();
            if (_cellLabels != null) _cellLabels.Clear();
            if (_pieceHud != null) _pieceHud.Bind(_hudRoot.MatchLayer, _pieces, cellSpacing * 0.8f);
            if (_history != null) _history.Clear();
            if (_toasts != null) _toasts.Clear();
            if (_banner != null) _banner.Bind(_hudRoot.MatchLayer);

            _hudRoot.MatchLayerVisible = false;
        }

        private BoardProfile Board =>
            useCompactBoard ? BoardProfile.Cross("Compact", 3, laps: 2) : BoardProfile.Standard;

        /// <summary>The room before the first deal: the table and its four empty seats, no pieces.</summary>
        private void ShowEmptyTable()
        {
            var board = Board;
            _layout = new BoardLayout(board, cellSpacing);

            var boardView = GetComponent<BoardView>() ?? gameObject.AddComponent<BoardView>();
            var map = new PathMap(board);
            boardView.Build(map, _layout, 3);
            if (_lighting != null) _lighting.Arrange(_layout, map);
            if (_eventLights != null) _eventLights.Bind(_lighting, _layout, _motion);

            FrameCamera();
        }

        /// <summary>The full-screen cards. Bound before the first match, and again after each deal.</summary>
        private void BindScreens()
        {
            _title = GetComponent<TitleScreen>() ?? gameObject.AddComponent<TitleScreen>();
            _title.Bind(_hudRoot.Root, this);
            _setup = GetComponent<SetupScreen>() ?? gameObject.AddComponent<SetupScreen>();
            _setup.Bind(_hudRoot.Root, this);
            _draftScreen = GetComponent<DraftScreen>() ?? gameObject.AddComponent<DraftScreen>();
            _draftScreen.Bind(_hudRoot.Root, this);
            _end = GetComponent<EndScreen>() ?? gameObject.AddComponent<EndScreen>();
            _end.Bind(_hudRoot.Root, this);
        }

        /// <summary>Whether a full-screen card owns the input.</summary>
        private bool ModalOpen => CurrentScreen != AppScreen.Match;

        private void NewMatch()
        {
            StopPresentation();

            foreach (var piece in _pieces)
                if (piece != null) Destroy(piece.gameObject);

            _pieces.Clear();
            _log.Clear();
            SetHovered(null);
            _selectedOperator = null;
            _selectedTarget = null;
            _selectedAbility = null;
            _selectedCell = null;
            _endQueued = false;

            // Both profiles must be drawable crosses: BoardLayout rejects a
            // circuit outside the 8L+4 family rather than drawing a track with a
            // gap in it (ADR-0002 Amendment 6). 28x2 replaces the old 24x2.
            var board = Board;

            if (_seats.Count == 0) _seats.AddRange(MatchSettings.AllSeats.Take(players));
            var seats = _seats.ToList();

            switch (squadMode)
            {
                case SquadMode.Alpha:
                    _match = MatchFactory.CreateAlphaMatch(
                        seats, seed,
                        board: board,
                        openingDeployments: openingDeployments);
                    break;

                case SquadMode.Random:
                    _match = MatchFactory.Create(
                        seats, seed,
                        squads: null,                  // null draws every seat at random
                        board: board,
                        openingDeployments: openingDeployments);
                    break;

                default:
                    // The drafted squads. A seat the draft did not cover (there
                    // should be none) is drawn at random by the factory.
                    _match = MatchFactory.Create(
                        seats, seed,
                        squads: _squads,
                        board: board,
                        openingDeployments: openingDeployments);
                    break;
            }

            _layout = new BoardLayout(board, cellSpacing);

            var boardView = GetComponent<BoardView>() ?? gameObject.AddComponent<BoardView>();
            int seatsPerTable = 0;
            foreach (var player in _match.Players)
                seatsPerTable = Mathf.Max(seatsPerTable, player.Operators.Count);

            boardView.Build(_match.Map, _layout, seatsPerTable);
            if (_lighting != null) _lighting.Arrange(_layout, _match.Map);
            if (_eventLights != null) _eventLights.Bind(_lighting, _layout, _motion);

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
                piece.Bind(op, _layout.CellSize, cellSpacing, _motion);
                piece.Stepped += OnPieceStepped;
                _pieces.Add(piece);
            }

            // The HUD scaffold survives a reseed — only the per-piece labels
            // are rebuilt, since the pieces they tracked were just destroyed.
            _hudRoot = GetComponent<HudRoot>() ?? gameObject.AddComponent<HudRoot>();
            _hudRoot.MatchLayerVisible = true;
            var hud = _hudRoot.MatchLayer;

            _pieceHud = GetComponent<PieceHudLayer>() ?? gameObject.AddComponent<PieceHudLayer>();
            // Clear of a standing figure's head and its bar (increment G2).
            _pieceHud.Bind(hud, _pieces, cellSpacing * 0.8f);
            _turnStrip = GetComponent<TurnStrip>() ?? gameObject.AddComponent<TurnStrip>();
            _turnStrip.Bind(hud, OpenPause);
            _controls = GetComponent<ControlPanel>() ?? gameObject.AddComponent<ControlPanel>();
            _controls.Bind(hud, this);
            _rail = GetComponent<SquadRail>() ?? gameObject.AddComponent<SquadRail>();
            _rail.Bind(hud, this);
            _tray = GetComponent<ActionTray>() ?? gameObject.AddComponent<ActionTray>();
            _tray.Bind(hud, this);
            _logPanel = GetComponent<LogPanel>() ?? gameObject.AddComponent<LogPanel>();
            _logPanel.Bind(hud, this);
            _logPanel.CloseRequested = () => showFullLog = false;
            _history = GetComponent<HistoryStrip>() ?? gameObject.AddComponent<HistoryStrip>();
            _history.Bind(hud);
            _history.LogRequested = () => showFullLog = !showFullLog;
            _toasts = GetComponent<EventToasts>() ?? gameObject.AddComponent<EventToasts>();
            _toasts.Bind(hud);
            _banner = GetComponent<TurnBanner>() ?? gameObject.AddComponent<TurnBanner>();
            _banner.Bind(hud);
            _turnButton = GetComponent<TurnButton>() ?? gameObject.AddComponent<TurnButton>();
            _turnButton.Bind(hud, this);
            _cellLabels = GetComponent<CellLabelLayer>() ?? gameObject.AddComponent<CellLabelLayer>();
            _cellLabels.Bind(hud);

            // After the tray, so the dice draw over it on their way in.
            _queue = Ensure<PresentationQueue>();
            _dice = Ensure<DiceRoller>();
            _dice.Bind(hud, () => _tray != null ? _tray.DiceFaces : null);
            _tells = Ensure<CastTell>();
            _tells.Bind(_layout.CellSize, _motion);

            // Last, so the menu draws over every other HUD layer.
            _pause = GetComponent<PauseMenu>() ?? gameObject.AddComponent<PauseMenu>();
            _pause.Bind(_hudRoot.Root, this);
            BindScreens();

            _bots = BuildBots(seats);

            if (_audio != null)
            {
                _audio.StopVoice();
                _audio.WarmVoices(_match.Operators.Select(o => o.Name));
            }

            FrameCamera();
            Handle(_match.Engine.Start(), immediate: true);
        }

        /// <summary>
        /// One brain per CPU seat, sharing the bots' own stream for this seed
        /// (BOTS.md decision 4). A table with no CPU seats gets an empty driver.
        /// </summary>
        private BotDriver BuildBots(IReadOnlyList<PlayerColor> seats)
        {
            var brains = new Dictionary<PlayerColor, BotBrain>();
            var random = BotConfig.Default.RandomFor(seed);

            foreach (var seat in seats)
            {
                if (_seatPlan.KindOf(seat) != SeatKind.Cpu) continue;
                brains[seat] = new BotBrain(_seatPlan.PersonalityOf(seat), random);
            }

            return new BotDriver(brains);
        }

        /// <summary>
        /// The component of this type on this object, added if missing. An
        /// explicit null check rather than <c>??</c>, which a destroyed
        /// component can slip past.
        /// </summary>
        private T Ensure<T>() where T : Component
        {
            var component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        /// <summary>Drops whatever the board was still playing, before a teardown or a new deal.</summary>
        private void StopPresentation()
        {
            if (_queue != null) _queue.Clear();
            if (_dice != null) _dice.Skip();
            if (_hitStop != null) _hitStop.Release();
            if (_nudge != null) _nudge.Stop();
            if (_eventLights != null) _eventLights.Clear();

            _diceHeld = false;
            _buffered = BufferedIntent.None;
        }

        /// <summary>Copies the motion settings and the hurry into the shared object and the clocks that read it.</summary>
        private void SyncMotion()
        {
            _motion.ReducedMotion = reducedMotion;
            _motion.Speed = animationSpeed;

            // Space hurries a CPU turn's animation as well as its thinking.
            _motion.Hurry = _match != null && CpuTurn && Input.GetKey(KeyCode.Space) ? HurrySpeed : 1f;

            if (_queue != null) _queue.Speed = _motion.Rate;
            if (_dice != null)
            {
                _dice.Speed = _motion.Rate;
                _dice.Reduced = _motion.ReducedMotion;
            }
        }

        private void OnPieceStepped(OperatorPiece piece)
        {
            if (_queue != null) _queue.Raise(PresentationBeat.Step);
            Sound(SoundCue.Step, piece.transform.position);
        }

        /// <summary>Plays a cue if the director exists. Positions pan it.</summary>
        private void Sound(SoundCue cue, Vector3? at = null)
        {
            if (_audio != null) _audio.Play(cue, at);
        }

        private void OnUiButton() => Sound(SoundCue.UiClick);

        /// <summary>
        /// Asks for a voice line from <paramref name="op"/>, panned to its
        /// piece (AUDIO.md increment AU2). The rules decide whether it plays.
        /// </summary>
        private void Speak(VoiceSlot slot, OperatorState op)
        {
            if (_audio == null || op == null) return;

            var piece = PieceFor(op);
            _audio.Speak(slot, op.Name, piece != null ? piece.transform.position : (Vector3?)null);
        }

        /// <summary>
        /// The quit line, when a live match is abandoned from the pause menu:
        /// one of the squad of the seat at the keyboard (the current seat if
        /// it is human, else the first human seat).
        /// </summary>
        private void SpeakQuit()
        {
            if (_match == null || _match.Engine.MatchOver) return;

            var seat = _match.Engine.CurrentPlayer.Color;
            if (_seatPlan.KindOf(seat) != SeatKind.Human)
            {
                foreach (var player in _match.Players)
                {
                    if (_seatPlan.KindOf(player.Color) != SeatKind.Human) continue;
                    seat = player.Color;
                    break;
                }
            }

            var squad = _match.Operators.Where(o => o.Owner == seat).ToList();
            if (squad.Count == 0) return;

            Speak(VoiceSlot.Quit, squad[Random.Range(0, squad.Count)]);
        }

        private void OnDestroy() => UiKit.ButtonPressed -= OnUiButton;

        /// <summary>
        /// Which music fits the screen (AU1): the match loop while a live
        /// match is on screen or paused, the title loop everywhere else,
        /// including the results. In a match's final stretch (the engine says
        /// a seat is one operator from winning) the showdown takes over, if a
        /// file for it exists.
        /// </summary>
        private void SyncAudio()
        {
            if (_audio == null) return;

            var screen = CurrentScreen;
            bool live = _match != null && !_match.Engine.MatchOver;

            _audio.Music = live && (screen == AppScreen.Match || screen == AppScreen.Paused)
                ? (_match.Engine.IsFinalStretch ? MusicCue.Showdown : MusicCue.Match)
                : MusicCue.Title;
            _audio.Paused = screen == AppScreen.Paused;
        }

        /// <summary>Screen width the OnGUI panel occupies, including its margin. Pixels.</summary>
        private const float PanelWidth = 330f;

        private const float FrameMargin = 1.12f;

        private int _framedWidth;
        private int _framedHeight;
        private bool _framedWithPanel;
        private bool _framedLegacy;
        private float _framedScale;
        private bool _framedWithHud;

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

            // With no match on the table (the title's room), nothing is
            // reserved and the room is centred.
            bool hud = _match != null;
            float leftUnits = hud ? LeftReservedUnits(scale) : 0f;
            float rightUnits = hud ? HistoryStrip.ReservedWidth : 0f;

            float left = Mathf.Clamp(leftUnits * scale / width, 0f, 0.45f);
            float right = Mathf.Clamp(rightUnits * scale / width, 0f, 0.45f);
            float top = hud ? Mathf.Clamp(TurnStrip.ReservedHeight * scale / height, 0f, 0.3f) : 0f;
            float bottom = hud ? Mathf.Clamp(ActionTray.ReservedHeight * scale / height, 0f, 0.35f) : 0f;

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

            // A nudge in progress continues around the new resting place.
            if (_nudge != null) _nudge.SetBase(camera.transform.position);

            if (_tray != null) _tray.SetInsets(leftUnits, rightUnits);
            if (_toasts != null) _toasts.SetArea(leftUnits, rightUnits, TurnStrip.ReservedHeight);
            if (_banner != null) _banner.SetArea(leftUnits, rightUnits, TurnStrip.ReservedHeight);
            if (_turnButton != null) _turnButton.SetArea(leftUnits, rightUnits, TurnStrip.ReservedHeight, ActionTray.ReservedHeight);

            _framedWidth = Screen.width;
            _framedHeight = Screen.height;
            _framedWithPanel = showDevPanel;
            _framedLegacy = useLegacyPanel;
            _framedScale = scale;
            _framedWithHud = hud;
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
            bool paused = ModalOpen;

            // The menu mirrors these flags, so their keys stay dead while it
            // is open rather than changing what it shows under the pointer.
            if (!paused)
            {
                if (Input.GetKeyDown(KeyCode.Tab)) showDevPanel = !showDevPanel;
                if (Input.GetKeyDown(KeyCode.F2)) useLegacyPanel = !useLegacyPanel;
                if (Input.GetKeyDown(KeyCode.H)) showPieceHealth = !showPieceHealth;
                if (Input.GetKeyDown(KeyCode.L)) showFullLog = !showFullLog;
            }

            // Driven every frame rather than on the keypress, so flipping the
            // inspector checkbox works too.
            if (_pieceHud != null) _pieceHud.Visible = showPieceHealth;

            // Before the pause check: the switch lives on the pause menu.
            if (_lighting != null)
            {
                _lighting.Effects = lightingEffects;
                _lighting.Reduced = reducedMotion;
            }

            // The selection pool follows whichever piece is selected (LT2).
            if (_eventLights != null)
            {
                var selectedPiece = _match != null ? PieceFor(_selectedOperator) : null;
                _eventLights.Selected = selectedPiece != null ? selectedPiece.transform : null;
            }

            SaveSettingsIfChanged();

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
                (_match != null) != _framedWithHud ||
                (_hudRoot != null && !Mathf.Approximately(_hudRoot.ScaleFactor, _framedScale)))
            {
                FrameCamera();
            }

            SyncAudio();

            if (paused)
            {
                HandleModalKeys();
                return;
            }

            SyncMotion();

            if (_match == null) return;

            DriveBots();

            HandleKeys();
            UpdateHover();

            // A click on a board that is still catching up would aim at where
            // pieces were, not where they are.
            if (!Busy && Input.GetMouseButtonDown(0)) HandleBoardClick();
            if (Input.GetMouseButtonDown(1)) StepBack();

            ReleaseBufferedIntent();
        }

        /// <summary>Keeps a Roll or End turn pressed while the board was busy, for a moment.</summary>
        private void Buffer(BufferedIntent intent)
        {
            _buffered = intent;
            _bufferedAt = Time.unscaledTime;
        }

        /// <summary>Sends the kept press once the board is free, if it is still fresh.</summary>
        private void ReleaseBufferedIntent()
        {
            if (_buffered == BufferedIntent.None || Busy) return;

            var intent = _buffered;
            _buffered = BufferedIntent.None;

            if (Time.unscaledTime - _bufferedAt > InputBufferSeconds) return;

            if (intent == BufferedIntent.Roll) Host.Roll();
            else Host.EndTurn();
        }

        private bool AnyPieceMoving()
        {
            foreach (var piece in _pieces)
                if (piece != null && piece.IsMoving) return true;

            return false;
        }

        // ── CPU seats (BOT2) ─────────────────────────────────────────────

        /// <summary>Whether the seat to play is a CPU's. Human input is ignored while it is.</summary>
        private bool CpuTurn => _bots != null && _match != null && _bots.IsCpuTurn(_match.Engine);

        /// <summary>
        /// Lets the CPU seat act, one command at a time, through the same
        /// handling a human command gets. Called only while no card is open,
        /// on scaled time, so pause freezes it. Space held hurries it.
        /// </summary>
        private void DriveBots()
        {
            if (_bots == null || !_bots.HasBots) return;

            // The board finishes showing one action before the next is chosen.
            bool busy = Busy || AnyPieceMoving();

            var command = _bots.Tick(_match, Time.deltaTime, mayAct: true, presentationBusy: busy,
                cpuSpeed, hurry: Input.GetKey(KeyCode.Space));
            if (command == null) return;

            var seat = _match.Engine.CurrentPlayer.Color;

            // Name the cast for the history strip, as a human cast is named.
            OperatorState castBy = null;
            AbilityDefinition cast = null;
            if (command is UseAbilityCommand use)
            {
                castBy = _match.Operators.FirstOrDefault(o => o.Id == use.CasterOperatorId);
                if (castBy != null && _match.AbilitiesByOperator.TryGetValue(castBy.Id, out var abilities))
                    cast = abilities.FirstOrDefault(a => a.Id == use.AbilityId);
            }

            var events = _match.Engine.Execute(command);

            foreach (var e in events)
                if (e is CommandRejected rejected)
                    _log.Add($"[CPU {seat}] refused {command.GetType().Name}: {rejected.Reason}");

            var guard = _bots.Observe(seat, command, events);
            if (guard != null) _log.Add(guard);

            Handle(events, immediate: cpuSpeed == BotSpeed.Instant, castBy, cast, fromBot: true, command: command);
        }

        /// <summary>"CPU · BRAWLER" for a CPU seat of the match on the table, else null.</summary>
        private string SeatTag(PlayerColor seat)
        {
            if (_bots == null || !_bots.IsCpu(seat)) return null;
            return $"CPU · {_bots.BrainFor(seat).Personality.Label()}";
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
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Esc backs out of a choice first; with nothing left to back
                // out of, it pauses, or brings the results back once the
                // match is over.
                if (HasSelection) StepBack();
                else if (_match.Engine.MatchOver && _end != null) _end.Open();
                else OpenPause();
            }

            if (_match.Engine.MatchOver) return;

            if (Input.GetKeyDown(KeyCode.Space)) Host.Roll();
            if (Input.GetKeyDown(KeyCode.E)) Host.EndTurn();

            // Space and E buffer through Host while the board is busy; the rest wait for it.
            if (Busy) return;

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

        /// <summary>Esc and Enter while a full-screen card is up. The topmost open card takes them.</summary>
        private void HandleModalKeys()
        {
            bool escape = Input.GetKeyDown(KeyCode.Escape);
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

            switch (CurrentScreen)
            {
                case AppScreen.Title:
                    if (escape) _title.Back();
                    else if (enter) _title.Confirm();
                    return;

                case AppScreen.Setup:
                    if (escape) _setup.Back();
                    else if (enter) _setup.Confirm();
                    return;

                case AppScreen.Draft:
                    _draftScreen.HandleKeys();
                    return;
            }

            if (_pause != null && _pause.IsOpen)
            {
                if (escape) _pause.Back();
                return;
            }

            if (_end != null && _end.IsOpen)
            {
                if (escape) _end.Close();
                else if (enter) ((IMatchFlowHost)this).Rematch();
            }
        }

        private bool HasSelection =>
            _selectedOperator != null || _selectedAbility != null ||
            _selectedTarget != null || _selectedCell != null;

        /// <summary>Opens the pause menu, dropping the hover lift so nothing stays raised behind it.</summary>
        private void OpenPause()
        {
            if (_pause == null || _pause.IsOpen) return;

            SetHovered(null);

            _pause.Open();
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

            SetHovered(hovered);
        }

        /// <summary>
        /// Moves the hover lift to another piece, or drops it. The one place
        /// <see cref="_hovered"/> changes, so every view that mirrors the hover
        /// hears about it: the piece marks, and the panels whose operator rows
        /// echo the hovered piece with a soft wash.
        /// </summary>
        private void SetHovered(OperatorPiece piece)
        {
            if (ReferenceEquals(piece, _hovered)) return;

            _hovered = piece;
            RefreshMarks();

            var op = piece != null ? piece.Operator : null;
            if (_rail != null) _rail.SetHovered(op);
            if (_controls != null) _controls.SetHovered(op);
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
            !_match.Engine.MatchOver && !CpuTurn && op.Owner == _match.Engine.CurrentPlayer.Color;

        // ── Driving the engine ───────────────────────────────────────────

        private void Send(ICommand command, OperatorState castBy = null, AbilityDefinition cast = null) =>
            Handle(_match.Engine.Execute(command), immediate: false, castBy, cast, command: command);

        private void Handle(
            IReadOnlyList<IGameEvent> events, bool immediate,
            OperatorState castBy = null, AbilityDefinition cast = null, bool fromBot = false,
            ICommand command = null)
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

            if (immediate || _queue == null)
            {
                // Whatever was still playing is overtaken; its settle runs now.
                if (_queue != null) _queue.Flush();
                if (_dice != null) _dice.Skip();

                SettleBatch(events, immediate: true, castBy, cast, fromBot);
                return;
            }

            Present(events, castBy, cast, fromBot, command);
        }

        /// <summary>
        /// Queues a batch's presentation: dice, the cast tell, walks, rises,
        /// hits, knockouts, then the settle (MOTION.md decision 2, PRESENTATION
        /// §3.1). Steps with nothing to show are left out.
        /// </summary>
        /// <remarks>
        /// <b>Hits play after the walk, before the settle.</b> The mover has
        /// arrived and the victim has not been moved yet, so a number or a
        /// burst lands on the cell where the thing happened, which is what
        /// playing feedback before repositioning always meant to do.
        ///
        /// <b>The cast tell reads the command, not the effects.</b> An accepted
        /// <see cref="UseAbilityCommand"/> says who cast and what it aimed at;
        /// the effects that follow show what the engine did with it.
        /// </remarks>
        private void Present(
            IReadOnlyList<IGameEvent> events, OperatorState castBy, AbilityDefinition cast, bool fromBot,
            ICommand command)
        {
            DiceRolled roll = null;
            bool walks = false, hits = false, knockouts = false, refused = false, home = false;
            var rises = new List<KeyValuePair<OperatorState, CellRef>>();

            foreach (var e in events)
            {
                switch (e)
                {
                    case CommandRejected _: refused = true; break;
                    case DiceRolled rolled: roll = rolled; break;
                    case OperatorMoved moved when moved.AttemptedTo > moved.From: walks = true; break;
                    case OperatorDeployed deployed: rises.Add(new KeyValuePair<OperatorState, CellRef>(deployed.Operator, deployed.Cell)); break;
                    case OperatorPityDeployed pity: rises.Add(new KeyValuePair<OperatorState, CellRef>(pity.Operator, pity.Cell)); break;
                    case OperatorNeutralized _: knockouts = true; break;
                    case OperatorReachedHome _: home = true; break;
                    case DamageDealt damaged when damaged.Amount > 0: hits = true; break;
                    case DamageEvaded _:
                    case DamageAbsorbed _:
                    case HealApplied _:
                    case OperatorRegenerated _:
                        hits = true;
                        break;
                }
            }

            if (roll != null && _dice != null)
            {
                // The tray waits from now, not from when the step starts.
                _diceHeld = true;
                MarkHudDirty();

                var seat = _match.Engine.CurrentPlayer.Color;
                var centre = _layout.HomeGoalPosition;

                _queue.Enqueue(PresentationBeat.DiceRolled, () =>
                    {
                        _dice.Play(roll.Roll.First, roll.Roll.Second, seat, centre, roll.GrantsAnotherRoll);
                        Sound(SoundCue.DiceShake);
                    },
                    () => !_dice.IsTumbling);
                _queue.Enqueue(PresentationBeat.DiceLanded, () =>
                    {
                        Sound(SoundCue.DiceLand);
                        if (roll.Roll.IsDouble) Sound(SoundCue.Doubles);
                    },
                    () => !_dice.IsRolling);
            }

            if (command is UseAbilityCommand use && !refused) QueueCastTell(use, castBy);

            if (walks)
                _queue.Enqueue(PresentationBeat.Walk, () =>
                    {
                        WalkMoves(events);
                        Speak(VoiceSlot.Move, VoiceCasting.Mover(events));
                    },
                    () => !AnyPieceMoving());

            if (rises.Count > 0)
            {
                _queue.Enqueue(PresentationBeat.Rise, () =>
                {
                    foreach (var rise in rises)
                    {
                        var piece = PieceFor(rise.Key);
                        if (piece == null) continue;

                        var at = _layout.PositionOf(rise.Value);
                        piece.Rise(at);
                        Sound(SoundCue.Rise, at);
                        Speak(VoiceSlot.Deploy, rise.Key);
                    }
                }, hold: _motion.Tween(RiseHoldSeconds));
            }

            if (hits)
                _queue.Enqueue(PresentationBeat.Hit, () => PlayFeedback(events, castBy, knockouts: false),
                    hold: _motion.Tween(HitHoldSeconds));

            if (knockouts)
                _queue.Enqueue(PresentationBeat.Knockout, () => PlayFeedback(events, castBy, knockouts: true),
                    hold: _motion.Tween(KnockoutHoldSeconds));

            _queue.Enqueue(PresentationBeat.Settle,
                () =>
                {
                    // The vault answers an arrival as the walk lands (LT2).
                    if (home && _eventLights != null) _eventLights.VaultSwell();
                    SettleBatch(events, immediate: false, castBy, cast, fromBot);
                },
                essential: true);
        }

        /// <summary>
        /// The sweep on the caster, then the line to its target or the drop on
        /// its cell. The caster comes from the command, so a CPU's aim reads
        /// the same way a human's does.
        /// </summary>
        private void QueueCastTell(UseAbilityCommand use, OperatorState castBy)
        {
            if (_tells == null) return;

            var caster = castBy != null ? PieceFor(castBy) : _pieces.Find(p => p.Operator.Id == use.CasterOperatorId);
            if (caster == null) return;

            var target = use.TargetOperatorId.HasValue
                ? _pieces.Find(p => p.Operator.Id == use.TargetOperatorId.Value)
                : null;
            var cell = use.TargetCell;

            bool aimed = target != null || cell.HasValue;

            float hold = _tells.Duration(aimed);

            _queue.Enqueue(PresentationBeat.CastTell, () =>
                {
                    _tells.Play(
                        caster.transform.position,
                        target != null ? target.transform.position : (Vector3?)null,
                        cell.HasValue ? _layout.PositionOf(cell.Value) : (Vector3?)null);
                    Sound(cell.HasValue ? SoundCue.CastCell : SoundCue.CastTell, caster.transform.position);

                    if (_eventLights != null)
                    {
                        Vector3? aim = target != null ? target.transform.position
                            : cell.HasValue ? _layout.PositionOf(cell.Value) : (Vector3?)null;
                        _eventLights.CastFlash(caster.transform.position, aim, hold);
                    }
                    Speak(VoiceSlot.Cast, caster.Operator);
                },
                hold: hold);
        }

        /// <summary>
        /// Brings the board and the HUD up to the engine: pieces, marks,
        /// devices, the top bar, the turn button, the history and, at the end,
        /// the results.
        /// </summary>
        private void SettleBatch(
            IReadOnlyList<IGameEvent> events, bool immediate,
            OperatorState castBy, AbilityDefinition cast, bool fromBot)
        {
            if (_match == null) return;

            _diceHeld = false;

            Reposition(immediate);
            RefreshHighlights();

            // Beacons and zones are redrawn from the engine every time
            // (ADR-0006 decision 6), so a spent one disappears on its own.
            if (_devices != null) _devices.Show(_match.Engine.ActiveCellEffects());

            string tag = _match.Engine.MatchOver ? null : SeatTag(_match.Engine.CurrentPlayer.Color);
            if (_turnStrip != null) _turnStrip.Refresh(_match.Engine, tag);
            if (_turnButton != null) _turnButton.Refresh(_match.Engine);

            ShowHistory(events, castBy, cast, fromBot);
            MarkHudDirty();

            if (_match.Engine.MatchOver && !_endQueued && _end != null)
            {
                _endQueued = true;
                _end.OpenSoon();

                // The settle of the winning action, walked or instant (a CPU at Instant speed).
                if (_audio != null) _audio.Sting();

                var winner = _match.Engine.Winner;
                if (winner.HasValue)
                    Speak(VoiceSlot.Victory, VoiceCasting.Victor(events, winner.Value, _match.Operators));
            }
        }

        /// <summary>
        /// Feeds a batch to the history strip, the toasts and the turn banner
        /// (GUI increment F2).
        /// </summary>
        /// <remarks>
        /// The banner opens when a batch begins a turn and fades as soon as the
        /// engine is past the roll, however the roll was made.
        /// </remarks>
        private void ShowHistory(
            IReadOnlyList<IGameEvent> events, OperatorState castBy, AbilityDefinition cast, bool fromBot)
        {
            var engine = _match.Engine;
            var batch = HistoryFeed.Build(events, castBy, cast, engine.Round);

            // A CPU's refusal is the bot's business, not the table's: logged, never toasted (BOTS.md decision 1).
            if (fromBot) batch.Rejections.Clear();

            if (_history != null) _history.Add(batch);
            if (_toasts != null) _toasts.Show(batch);

            if (_banner == null) return;

            if (batch.TurnBegan != null && !engine.MatchOver)
            {
                _banner.Show(batch.TurnBegan.Player, engine.Round);
                Sound(SoundCue.TurnStart);
            }

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
        string IControlPanelHost.SquadSummary => squadMode.Label();
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

        bool IControlPanelHost.CpuTurn => CpuTurn;

        bool IControlPanelHost.DiceHeld => _diceHeld;

        string IControlPanelHost.SeatTag(PlayerColor seat) => SeatTag(seat);

        // Every intent below is ignored on a CPU's turn: the seat is not the pointer's to command.
        // Commands also wait for a busy board (MO1): Roll and End turn are kept briefly, the rest dropped.

        void IControlPanelHost.Roll()
        {
            if (CpuTurn) return;
            if (Busy) Buffer(BufferedIntent.Roll);
            else Send(new RollDiceCommand());
        }

        void IControlPanelHost.EndTurn()
        {
            if (CpuTurn) return;
            if (Busy) Buffer(BufferedIntent.EndTurn);
            else Send(new EndTurnCommand());
        }

        void IControlPanelHost.Deploy(OperatorState op)
        {
            if (!CpuTurn && !Busy) Send(new DeployCommand(op.Id));
        }

        void IControlPanelHost.Move(OperatorState op, int? dieFace)
        {
            if (!CpuTurn && !Busy) Send(new MoveCommand(op.Id, dieFace));
        }

        void IControlPanelHost.ToggleOperator(OperatorState op)
        {
            if (CpuTurn) return;

            _selectedOperator = ReferenceEquals(op, _selectedOperator) ? null : op;
            _selectedAbility = null;      // an ability belongs to its caster
            _selectedTarget = null;       // and a target belongs to its ability
            _selectedCell = null;         // as does a cell

            SelectionChanged();
        }

        void IControlPanelHost.ToggleAbility(AbilityDefinition ability)
        {
            if (CpuTurn) return;
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
            if (CpuTurn) return;
            _selectedTarget = ReferenceEquals(op, _selectedTarget) ? null : op;
            SelectionChanged();
        }

        void IControlPanelHost.Cast()
        {
            if (CpuTurn || Busy || !Host.CastReady) return;

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

        // ── Pause menu ───────────────────────────────────────────────────

        string IPauseHost.PauseSummary
        {
            get
            {
                if (_match == null) return "";

                var engine = _match.Engine;
                if (engine.MatchOver)
                {
                    var winner = engine.Winner;
                    return winner.HasValue
                        ? $"Round {engine.Round}  ·  {winner.Value.ToString().ToUpperInvariant()} won"
                        : $"Round {engine.Round}  ·  match over";
                }

                return $"Round {engine.Round}  ·  {engine.CurrentPlayer.Color.ToString().ToUpperInvariant()} to play";
            }
        }

        // Settings are saved by Update when they change (SaveSettingsIfChanged).
        bool ISettingsHost.ShowPieceHealth { get => showPieceHealth; set => showPieceHealth = value; }
        bool ISettingsHost.ShowFullLog { get => showFullLog; set => showFullLog = value; }
        bool ISettingsHost.ShowDevPanel { get => showDevPanel; set => showDevPanel = value; }
        BotSpeed ISettingsHost.CpuSpeed { get => cpuSpeed; set => cpuSpeed = value; }
        bool ISettingsHost.ReducedMotion { get => reducedMotion; set => reducedMotion = value; }
        bool ISettingsHost.LightingEffects { get => lightingEffects; set => lightingEffects = value; }
        AnimationSpeed ISettingsHost.AnimationSpeed { get => animationSpeed; set => animationSpeed = value; }
        AudioLevels ISettingsHost.Audio => _levels;

        void IPauseHost.MainMenu()
        {
            SpeakQuit();
            ShowTitle();
        }

        void IPauseHost.OpenSetup()
        {
            SpeakQuit();
            OpenSetup();
        }

        private void OpenSetup()
        {
            if (_pause != null) _pause.Close();
            if (_end != null) _end.Close();
            if (_title != null) _title.Close();
            if (_draftScreen != null) _draftScreen.Close();

            SetHovered(null);
            if (_match != null) RefreshMarks();

            _setup.Open();
        }

        // ── Match flow (GUI increment I) ─────────────────────────────────

        MatchSettings IMatchFlowHost.Settings
        {
            get
            {
                var settings = new MatchSettings(_seats, squadMode, seed);
                settings.CopySeatsFrom(_seatPlan);
                return settings;
            }
        }

        MatchFactory.Match IMatchFlowHost.Match => _match;

        /// <remarks>
        /// A drafted mode commits nothing here. The match on the table (if
        /// any) keeps its seats and squads until the draft finishes, so
        /// leaving the draft and then leaving setup returns to that match
        /// unchanged.
        /// </remarks>
        void IMatchFlowHost.Deal(MatchSettings settings)
        {
            if (_setup != null) _setup.Close();

            if (settings.Squads.IsDraft())
            {
                _draftScreen.Open(settings);
                return;
            }

            Commit(settings, squads: null);
            NewMatch();
        }

        void IMatchFlowHost.FinishDraft(MatchSettings settings,
            IReadOnlyDictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> squads)
        {
            Commit(settings, squads);
            NewMatch();
        }

        void IMatchFlowHost.CancelDraft(MatchSettings settings)
        {
            if (_draftScreen != null) _draftScreen.Close();
            _setup.Open(settings);
        }

        private void Commit(MatchSettings settings,
            IReadOnlyDictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> squads)
        {
            _seats.Clear();
            _seats.AddRange(settings.Seats);
            squadMode = settings.Squads;
            seed = settings.Seed;
            _squads = squads;
            _seatPlan.CopySeatsFrom(settings);
        }

        /// <summary>
        /// The same table with the next seed, like the dev panel's reseed. A
        /// replay of the same seed would repeat the same dice, which is a
        /// debugging tool, not a rematch. Drafted squads are kept (DRAFT.md
        /// decision 7); RANDOM draws again from the new seed.
        /// </summary>
        void IMatchFlowHost.Rematch()
        {
            if (_end != null) _end.Close();
            Host.Reseed();
        }

        void IMatchFlowHost.OpenSetup() => OpenSetup();

        void IMatchFlowHost.CancelSetup()
        {
            // Back to the match if there is one; the room with no match is the title's.
            if (_match == null) ShowTitle();
        }

        void IMatchFlowHost.MainMenu() => ShowTitle();

        // ── Title (GUI increment J) ──────────────────────────────────────

        void ITitleHost.Play() => OpenSetup();

        void ITitleHost.Quit()
        {
            SaveSettingsIfChanged();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
        /// <param name="castBy">The batch's caster, if any: who takes the kill line.</param>
        /// <param name="knockouts">True for the knockout bursts only, false for everything else.</param>
        /// <remarks>
        /// <b>MO2:</b> a hit also shows the health the event reports on the
        /// piece's bar and label, so the number and the bar change together. A
        /// big hit (<see cref="BigHitFraction"/> or a finishing blow) and every
        /// knockout get a hit-stop and a camera nudge; a knocked-out piece
        /// shatters. Reduced motion drops the stop and the nudge.
        ///
        /// <b>AU2:</b> a hit that leaves the target standing asks for its
        /// hit-taken line; a knockout asks for the victim's death line, then
        /// the kill line of whoever the batch credits (<see cref="VoiceCasting.Killer"/>).
        /// </remarks>
        private void PlayFeedback(IReadOnlyList<IGameEvent> events, OperatorState castBy, bool knockouts)
        {
            if (_feedback == null) return;

            OperatorPiece impact = null;

            foreach (var e in events)
            {
                if ((e is OperatorNeutralized) != knockouts) continue;

                var damaged = e as DamageDealt;
                if (damaged != null && damaged.Amount > 0)
                {
                    var piece = PieceFor(damaged.Target);
                    if (piece == null) continue;

                    _feedback.Damage(piece.transform.position, damaged.Amount, damaged.Cause);
                    piece.Flash();
                    piece.ShowHealth(damaged.RemainingHealth);

                    bool big = damaged.RemainingHealth <= 0 ||
                               damaged.Amount >= BigHitFraction * damaged.Target.MaxHealth;
                    if (big) impact = piece;
                    Sound(big ? SoundCue.HitBig : SoundCue.Hit, piece.transform.position);
                    if (damaged.RemainingHealth > 0) Speak(VoiceSlot.HitTaken, damaged.Target);
                    continue;
                }

                var evaded = e as DamageEvaded;
                if (evaded != null)
                {
                    var piece = PieceFor(evaded.Target);
                    if (piece != null)
                    {
                        _feedback.Evaded(piece.transform.position);
                        Sound(SoundCue.Miss, piece.transform.position);
                    }
                    continue;
                }

                var absorbed = e as DamageAbsorbed;
                if (absorbed != null)
                {
                    var piece = PieceFor(absorbed.Target);
                    if (piece != null)
                    {
                        _feedback.Absorbed(piece.transform.position);
                        Sound(SoundCue.Block, piece.transform.position);
                    }
                    continue;
                }

                var healed = e as HealApplied;
                if (healed != null)
                {
                    var piece = PieceFor(healed.Target);
                    if (piece != null)
                    {
                        _feedback.Heal(piece.transform.position, healed.Amount);
                        piece.ShowHealth(piece.ShownHealth + healed.Amount);
                        Sound(SoundCue.Heal, piece.transform.position);
                    }
                    continue;
                }

                var regen = e as OperatorRegenerated;
                if (regen != null)
                {
                    var piece = PieceFor(regen.Target);
                    if (piece != null)
                    {
                        _feedback.Heal(piece.transform.position, regen.Amount);
                        piece.ShowHealth(piece.ShownHealth + regen.Amount);
                        Sound(SoundCue.Heal, piece.transform.position);
                    }
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
                        Sound(SoundCue.Knockout, piece.transform.position);
                        if (_eventLights != null) _eventLights.Knockout(piece.transform.position);
                        piece.Shatter();
                        impact = piece;
                    }

                    Speak(VoiceSlot.Death, down.Operator);
                    Speak(VoiceSlot.Kill, VoiceCasting.Killer(events, castBy, down));
                }
            }

            if (impact == null) return;

            if (_hitStop != null) _hitStop.Stop(knockouts ? KnockoutStopSeconds : HitStopSeconds);

            if (_nudge != null && _layout != null)
            {
                // Away from the board's centre, toward the hit.
                var away = (Vector2)(impact.transform.position - _layout.HomeGoalPosition);
                _nudge.Nudge(away, (knockouts ? KnockoutNudgeCells : HitNudgeCells) * _layout.CellSize);
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
        /// piece twice they are two batches, and the presentation queue plays
        /// the second only once the first has landed (PRESENTATION §3.1).
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

            // A CPU's landings are its own business: the board shows where its pieces go, not where they could.
            if (!CpuTurn)
            {
                CollectMoveOptions();
                DrawMoveOptions();
            }

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
                bool yard = pair.Key.Kind == CellKind.Yard;

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    var piece = pair.Value[i];

                    // In the yard each operator has its own seat at the table
                    // (ART_DIRECTION §6.1); elsewhere a shared cell fans out.
                    var position = yard
                        ? _layout.YardSeat(piece.Operator.Owner, SeatOf(piece.Operator))
                        : basePosition + _layout.Offset(i, pair.Value.Count);

                    // A shattered piece comes back seated with a pop (MO2);
                    // everything else snaps or settles as before.
                    if (piece.IsHidden) piece.Reappear(position);
                    else if (immediate) piece.Place(position);
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

        /// <summary>An operator's place in its squad, which is its seat at the table.</summary>
        private int SeatOf(OperatorState op)
        {
            foreach (var player in _match.Players)
            {
                if (player.Color != op.Owner) continue;

                for (int i = 0; i < player.Operators.Count; i++)
                    if (ReferenceEquals(player.Operators[i], op)) return i;
            }

            return 0;
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

            GUILayout.Label($"<i>{squadMode.Label()} — Tab hides this, H toggles health, F2 switches panel</i>");

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

                // Board-hover echo: a whisper of gold over the hovered piece's
                // row (immediate mode has no way to draw behind an already
                // laid-out row, so the wash goes on top, faint enough to read
                // as a glow). GUI.color wraps only this one draw.
                if (_hovered != null && ReferenceEquals(op, _hovered.Operator) &&
                    Event.current.type == EventType.Repaint)
                {
                    var tint = GUI.color;
                    GUI.color = UiTheme.WithAlpha(UiTheme.Gold, 0.10f);
                    GUI.DrawTexture(GUILayoutUtility.GetLastRect(), Texture2D.whiteTexture);
                    GUI.color = tint;
                }
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
