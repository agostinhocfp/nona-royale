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
    /// <b>The pointer says what a click would do while aiming</b> (GUI
    /// increment G5). The <see cref="CursorSkin"/> shows the gold arrow
    /// everywhere, an amber reticle when an ability waits for a target and
    /// the pointer is over one it would take, and a grey reticle over
    /// anything else on the board. It asks the same questions the click does
    /// (<see cref="PointerCursor"/>), so the two cannot disagree.
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

        [Tooltip("How the table is divided into sides at the first deal (ADR-0012). " +
                 "Crossed Pairs is 1v1: Red+Green against Blue+Violet, and it uses all four seats. " +
                 "The setup screen overrides it.")]
        public TableMode tableMode = TableMode.FreeForAll;

        [Tooltip("Seats at the first deal, filled Red, Blue, Green, Violet. The setup screen overrides it. " +
                 "Ignored by a crossed table, which always seats four.")]
        [Range(2, 4)] public int players = 4;

        [Tooltip("Seats the CPU plays at the first deal (useful with Skip Setup). The setup screen overrides it.")]
        public PlayerColor[] cpuSeats = new PlayerColor[0];

        [Tooltip("How fast CPU seats act. Remembered between sessions; this is the first-run default.")]
        public BotSpeed cpuSpeed = BotSpeed.Normal;

        [Tooltip("A human seat that has not rolled 15 s into its turn has the dice rolled for it (TurnPacer). " +
                 "CPU seats pace themselves; pause and menus freeze the clock.")]
        public bool rollClock = true;

        [Tooltip("End a human seat's turn on its own once End Turn is the only legal command left (TurnPacer).")]
        public bool autoEndTurn = true;

        [Tooltip("Operators already on the board at the start. 2 is the adopted value.")]
        [Range(0, 3)] public int openingDeployments = 2;

        [Tooltip("The match seed: the dice, who plays first and any random picks. Drawn fresh at launch " +
                 "and for every new match and rematch, unless Pin Seed is on.")]
        public int seed = 20260912;

        [Tooltip("Keep the seed above instead of drawing a fresh one: at launch, in setup, and a rematch or " +
                 "RESTART steps it by one. For reproducing a bug; off for play.")]
        public bool pinSeed = false;

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

        /// <summary>The match in the player's words, for the event log (G7b). <see cref="_log"/> keeps the raw lines for the dev panel.</summary>
        private readonly MatchLog _matchLog = new MatchLog();

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
        private OperatorGuideScreen _guide;

        /// <summary>
        /// True while the guide was opened from a match rather than the title,
        /// so the match keeps its music and counts as paused (OG4).
        /// </summary>
        private bool _guideOverMatch;
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

        /// <summary>The game's pointer (G5). Built once, at Start; released on destroy.</summary>
        private CursorSkin _cursor;

        // ── Presentation (MO1) ───────────────────────────────────────────

        /// <summary>How long a hit's numbers own the board before the next step, in scaled seconds.</summary>
        private const float HitHoldSeconds = 0.3f;

        /// <summary>How long a knockout burst shows before the piece returns to its yard, in scaled seconds.</summary>
        private const float KnockoutHoldSeconds = 0.45f;

        /// <summary>
        /// The silence before an execute lands (AUDIO.md AU3), in scaled
        /// seconds like the other holds, so it shortens with the queue.
        /// </summary>
        private const float ExecuteHushSeconds = 0.2f;

        /// <summary>
        /// The longest a hush may last in real seconds if the knockout that
        /// ends it never plays (a flush, a teardown).
        /// </summary>
        private const float HushCapSeconds = 1.5f;

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
        private RoomBackdrop _room;
        private TableBody _tableBody;

        /// <summary>What the menus sit on when there is no match (G6d). Replaces the empty board there.</summary>
        private MenuBackdrop _menuBackdrop;

        /// <summary>Light that answers play: selection, casts, knockouts, HOME (LT2).</summary>
        private EventLights _eventLights;

        /// <summary>Plays every sound (AU1). Built once, at Start; it outlives matches.</summary>
        private AudioDirector _audio;

        /// <summary>The volume settings, shared with the director and the sound page (AU1).</summary>
        private readonly AudioLevels _levels = new AudioLevels();
        private readonly AudioLevels _savedLevels = new AudioLevels();

        /// <summary>The display settings, shared with the display page (2026-09-17).</summary>
        private readonly DisplaySettings _display = new DisplaySettings();
        private readonly DisplaySettings _savedDisplay = new DisplaySettings();

        /// <summary>Reduced motion, animation speed and hurry, shared with every animated view (MO2).</summary>
        private readonly MotionSettings _motion = new MotionSettings();

        /// <summary>True from a roll's arrival until its settle: the tray waits for the dice moment.</summary>
        private bool _diceHeld;

        private BufferedIntent _buffered;
        private float _bufferedAt;

        /// <summary>Whether the board is still catching up with the engine.</summary>
        private bool Busy => _queue != null && _queue.IsBusy;

        /// <summary>The roll clock and auto end-turn for human seats (2026-09-25).</summary>
        private readonly TurnPacer _pacer = new TurnPacer();

        /// <summary>The screens the app moves between (GUI increment J).</summary>
        public enum AppScreen { Title, Guide, Setup, Draft, Match, Paused, Results }

        /// <summary>
        /// Which screen is showing, read from the open cards, topmost first.
        /// No card open means the match itself.
        /// </summary>
        public AppScreen CurrentScreen =>
            _guide != null && _guide.IsOpen ? AppScreen.Guide :
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

            // The inspector's seed is a starting value for debugging, not the
            // game's: every launch used to open on the same dice.
            if (!pinSeed) seed = FreshSeed();

            // A crossed table is two sides of two, so it seats four whatever
            // the inspector says (ADR-0012).
            _seats.AddRange(MatchSettings.AllSeats.Take(
                tableMode.IsTeams() ? MatchSettings.AllSeats.Length : players));

            _seatPlan.Table = tableMode;

            if (cpuSeats != null)
                foreach (var seat in cpuSeats) _seatPlan.SetKind(seat, SeatKind.Cpu);

            LoadSettings();
            SyncMotion();

            // The pointer is the first thing the player sees move, so it is ours from the first frame (G5).
            _cursor = new CursorSkin();
            _cursor.Show(CursorLook.Arrow);

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

            // Under every card, and up whenever there is no match (G6d).
            _menuBackdrop = Ensure<MenuBackdrop>();
            _menuBackdrop.Build(_hudRoot.BackdropLayer);
            _menuBackdrop.Visible = _match == null;

            if (skipSetup)
            {
                // A drafted mode still opens the draft; the others deal at once.
                ((IMatchFlowHost)this).Deal(new MatchSettings(_seats, squadMode, seed, tableMode));
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
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            // No dev panel for players (G7c), whatever a development build saved.
            showDevPanel = false;
#endif

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

            SettingsStore.LoadDisplay(_display);
            ApplyDisplay(_display);
            _savedDisplay.CopyFrom(_display);
        }

        /// <summary>Pushes the display settings into the engine: mode and size, VSync, and the cap with VSync off.</summary>
        /// <remarks>
        /// <b>The size and the mode are skipped where the screen is the
        /// device's</b> (MOBILE.md, M7). This is not a tidiness point. The
        /// stored windowed size is 1920×1080, and on Android
        /// <c>Screen.SetResolution</c> takes it literally: the log showed the
        /// surface come up correct at 1080×2340 and then be reset to 1920×1080
        /// the moment the settings were applied, which the compositor then
        /// stretched across an upright screen. The board looked squashed and
        /// every aspect-driven decision in the HUD was answering for a window
        /// nobody had.
        ///
        /// VSync and the frame cap stay: both mean something on a phone.
        /// </remarks>
        private static void ApplyDisplay(DisplaySettings display)
        {
            if (!ScreenLayout.FixedScreen)
            {
                var mode = display.Mode == ScreenMode.Fullscreen
                    ? FullScreenMode.FullScreenWindow
                    : FullScreenMode.Windowed;
                Screen.SetResolution(display.Width, display.Height, mode);
            }

            QualitySettings.vSyncCount = display.VSync ? 1 : 0;
            Application.targetFrameRate = display.VSync || display.FrameCap <= 0 ? -1 : display.FrameCap;
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

            if (!_display.SameAs(_savedDisplay))
            {
                _savedDisplay.CopyFrom(_display);
                ApplyDisplay(_display);
                SettingsStore.SaveDisplay(_display);
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
            if (_guide != null) _guide.Close();
            _guideOverMatch = false;
            if (_end != null) _end.Close();

            // Opened before the table is dressed: FrameCamera asks the title
            // whether it is on screen, and the room asks the camera whether it
            // can be seen at all. Opening last showed one tilted frame.
            _title.Open();
            TearDownMatch();
            ShowEmptyTable();

            // Set here as well as in Update, so no frame shows the board.
            if (_menuBackdrop != null) _menuBackdrop.Visible = true;
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
            _matchLog.Clear();
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

            // Before the room: building it settles whether it is visible, and
            // that answer is BoardTilt, which FrameCamera writes.
            FrameCamera();

            Slab().Build(_layout);
            Room().Build(_layout);
            if (_lighting != null) _lighting.Arrange(_layout, map);
            if (_eventLights != null) _eventLights.Bind(_lighting, _layout, _motion);
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
            _guide = GetComponent<OperatorGuideScreen>() ?? gameObject.AddComponent<OperatorGuideScreen>();
            _guide.Bind(_hudRoot.Root, CloseGuide);
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
            _matchLog.Clear();
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

            // Table order rotated so a seat drawn from the seed opens (TurnOrder).
            // _seats stays in table order: it is what setup shows and edits.
            var seats = TurnOrder.Opening(_seats, seed);

            // Who is on whose side (ADR-0012). One map for the whole match,
            // handed to the factory rather than consulted by the view: the
            // view decides nothing about the rules, it only says which table
            // the player chose.
            var teams = _seatPlan.Teams;

            switch (squadMode)
            {
                case SquadMode.Alpha:
                    _match = MatchFactory.CreateAlphaMatch(
                        seats, seed,
                        board: board,
                        openingDeployments: openingDeployments,
                        teams: teams);
                    break;

                case SquadMode.Random:
                    _match = MatchFactory.Create(
                        seats, seed,
                        squads: null,                  // null draws every seat at random
                        board: board,
                        openingDeployments: openingDeployments,
                        teams: teams);
                    break;

                default:
                    // The drafted squads. A seat the draft did not cover (there
                    // should be none) is drawn at random by the factory.
                    _match = MatchFactory.Create(
                        seats, seed,
                        squads: _squads,
                        board: board,
                        openingDeployments: openingDeployments,
                        teams: teams);
                    break;
            }

            _layout = new BoardLayout(board, cellSpacing);

            var boardView = GetComponent<BoardView>() ?? gameObject.AddComponent<BoardView>();
            int seatsPerTable = 0;
            foreach (var player in _match.Players)
                seatsPerTable = Mathf.Max(seatsPerTable, player.Operators.Count);

            boardView.Build(_match.Map, _layout, seatsPerTable);
            Slab().Build(_layout);
            Room().Build(_layout);
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

            // Rigs and look-book figures render on the thread pool while the
            // rest of the match builds; a piece that binds first waits for its
            // own. An operator with a rig never draws its look-book figure.
            var figureNames = _match.Operators.Select(o => o.Name).Distinct().ToList();
            OperatorRigArt.Prewarm(figureNames);
            OperatorLookBook.Prewarm(figureNames.Where(n => !OperatorRigArt.Has(n)));

            foreach (var op in _match.Operators)
            {
                var go = new GameObject();
                go.transform.SetParent(transform, false);

                var piece = go.AddComponent<OperatorPiece>();
                piece.BoardCentre = _layout.HomeGoalPosition;
                piece.Bind(op, _layout.CellSize, cellSpacing, _motion);
                piece.Stepped += OnPieceStepped;
                _pieces.Add(piece);
            }

            // The HUD scaffold survives a reseed — only the per-piece labels
            // are rebuilt, since the pieces they tracked were just destroyed.
            _hudRoot = GetComponent<HudRoot>() ?? gameObject.AddComponent<HudRoot>();
            _hudRoot.MatchLayerVisible = true;
            if (_menuBackdrop != null) _menuBackdrop.Visible = false;
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
            _logPanel.Bind(hud, _matchLog);
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
                _audio.WarmSignatures(_match.Operators.Select(o => o.Name).Distinct().SelectMany(AbilitySounds.SlugsOf));
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

            // The HUD's tweens read the same flag (UI_MOTION.md U1).
            View.UiTween.ReducedMotion = reducedMotion;

            // Space hurries a CPU turn's animation as well as its thinking.
            _motion.Hurry = _match != null && CpuTurn && Hurrying ? HurrySpeed : 1f;

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

        /// <summary>
        /// A batch's ability signature and which of its moments have been
        /// heard (AUDIO.md AU3). One per batch, captured by its hit and
        /// knockout steps, so a splash plays the impact once, not per piece.
        /// </summary>
        private sealed class CastImpact
        {
            public CastImpact(string slug) => Slug = slug;

            public string Slug { get; }
            public bool ImpactHeard;
            public bool AssistHeard;
        }

        /// <summary>
        /// Plays the batch's signature for <paramref name="moment"/> unless
        /// it has been heard already.
        /// </summary>
        /// <returns>
        /// True if the ability has a sound for the moment (heard now or
        /// earlier in the batch), so the caller skips the generic cue.
        /// </returns>
        private bool Signature(CastImpact signature, SignatureMoment moment, Vector3 at)
        {
            if (signature == null || signature.Slug == null || _audio == null) return false;

            if (moment == SignatureMoment.Assist)
            {
                if (!signature.AssistHeard) signature.AssistHeard = _audio.PlaySignature(signature.Slug, moment, at);
                return signature.AssistHeard;
            }

            if (!signature.ImpactHeard) signature.ImpactHeard = _audio.PlaySignature(signature.Slug, moment, at);
            return signature.ImpactHeard;
        }

        /// <summary>The damage-type layer under a hit, if its type has one (AU3).</summary>
        private void Layer(DamageDealt damaged, Vector3 at)
        {
            var layer = AbilitySounds.LayerFor(damaged.Type, damaged.Cause);
            if (layer.HasValue) Sound(layer.Value, at);
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

        private void OnDestroy()
        {
            UiKit.ButtonPressed -= OnUiButton;

            // The system cursor comes back when the root goes (G5).
            if (_cursor != null)
            {
                _cursor.Release();
                _cursor = null;
            }
        }

        /// <summary>
        /// Re-applies the cursor when the window comes back, since some
        /// platforms reset it while the game is in the background (G5).
        /// </summary>
        private void OnApplicationFocus(bool focused)
        {
            if (focused && _cursor != null) _cursor.Invalidate();
        }

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

            // The guide over a match is a pause, not a trip to the title: the
            // match's music keeps playing under it, ducked as it is paused.
            bool guideOverMatch = screen == AppScreen.Guide && _guideOverMatch;

            _audio.Music = live && (screen == AppScreen.Match || screen == AppScreen.Paused || guideOverMatch)
                ? (_match.Engine.IsFinalStretch ? MusicCue.Showdown : MusicCue.Match)
                : MusicCue.Title;
            _audio.Paused = screen == AppScreen.Paused || guideOverMatch;
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
        private bool _framedOnTitle;
        private BoardCamera _framedCamera;

        /// <summary>
        /// The screen shape the framing was solved for (MOBILE.md, M1). A turn
        /// of the phone changes what every panel reserves, so the solve has to
        /// run again.
        /// </summary>
        private int _framedLayout = -1;

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
        ///
        /// <b>Two cameras since V1</b> (VISUAL_PASS.md). The flat camera is
        /// below, unchanged. The tilted one solves for a pose with
        /// <see cref="TiltFraming"/> instead of a size and a shift, because a
        /// perspective camera's mapping from viewport fraction to world offset
        /// depends on depth, so the shift trick that works above does not carry
        /// over. Everything after the two branches — the nudge's resting place
        /// and the HUD insets — is shared, and a tilt that cannot be solved
        /// falls back to the flat camera rather than showing nothing.
        /// </remarks>
        private void FrameCamera()
        {
            var camera = Camera.main;
            if (camera == null) return;

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

            bool hud = _match != null;
            bool title = _title != null && _title.IsShowing;

            // Nothing takes the sides off a menu screen; the room, and the
            // title's board, are centred.
            float leftUnits = hud ? LeftReservedUnits(scale) : 0f;
            float rightUnits = hud ? HistoryStrip.ReservedWidth : 0f;

            // The title reserves its two bands the way a match reserves the
            // strip and the tray, so the board sits whole between the wordmark
            // and the menu row rather than under them (V3b). The other menus
            // reserve nothing: their cards are centred over a room, and the
            // room is the picture.
            //
            // Upright, the rail and the history are bands rather than columns
            // (MOBILE.md, M3), so they reserve height instead of width. Both
            // arrangements are added unconditionally: whichever one is not in
            // force reports zero.
            float topUnits = hud ? TurnStrip.ReservedHeight + SquadRail.ReservedHeight
                : title ? TitleScreen.ReservedTop : 0f;
            float bottomUnits = hud ? ActionTray.ReservedHeight + HistoryStrip.ReservedHeight
                : title ? TitleScreen.ReservedBottom : 0f;

            // The safe area comes off the same edges as the panels do
            // (MOBILE.md, M2). The player settings render outside it so the
            // room runs under the cut-out; the board must not, or a cell ends
            // up behind a camera hole. HudRoot insets the chrome by the same
            // amounts, so the two agree on where the screen ends.
            var safe = ScreenLayout.SafePixels;
            float safeLeft = Mathf.Max(0f, safe.xMin);
            float safeRight = Mathf.Max(0f, width - safe.xMax);
            float safeTop = Mathf.Max(0f, height - safe.yMax);
            float safeBottom = Mathf.Max(0f, safe.yMin);

            float left = Mathf.Clamp((leftUnits * scale + safeLeft) / width, 0f, 0.45f);
            float right = Mathf.Clamp((rightUnits * scale + safeRight) / width, 0f, 0.45f);
            float top = Mathf.Clamp((topUnits * scale + safeTop) / height, 0f, ScreenLayout.Pick(0.3f, 0.32f));
            float bottom = Mathf.Clamp((bottomUnits * scale + safeBottom) / height, 0f, ScreenLayout.Pick(0.35f, 0.42f));

            // Never let the panels claim so much of a tiny window that the board
            // is sized into nothing.
            float usableWidth = Mathf.Max(0.25f, 1f - left - right);
            float usableHeight = Mathf.Max(0.25f, 1f - top - bottom);

            // The Board camera setting is a play preference: the tilt is opted
            // into because a player counts cells to plan a move (V1a), and a menu
            // has no cells to count. So it gates the match alone. That is also
            // what makes the room worth building - every part of it stands
            // vertically, and a straight-down camera sees a vertical wall
            // edge-on (V3).
            //
            // The title is then the one screen that opts out (V3b). Its lockup
            // runs along the top and bottom edges with the board whole between
            // them, and that composition is drawn for a board lying straight
            // down. The room goes with the tilt, and goes quietly: RoomBackdrop
            // hides itself under a flat camera rather than draw a wall edge-on.
            // Setup, draft and the end card keep the salon.
            bool wantsTilt = !title && (!hud || _display.Camera == BoardCamera.Tilted);
            bool tilted = wantsTilt &&
                          FrameTilted(camera, extent, aspect, left, 1f - right, bottom, 1f - top, hud);

            if (!tilted) FrameFlat(camera, extent, aspect, left, right, top, bottom, usableWidth, usableHeight);

            // The pieces, the pointer and the hop all follow the pitch (V1b).
            // Written here rather than on the setting, so a tilt that could not
            // be solved leaves everything flat to match the camera.
            BoardTilt.Set(tilted ? (hud ? TiltFraming.MatchPitch : TiltFraming.MenuPitch) : 0f, extent);

            // A nudge in progress continues around the new resting place.
            if (_nudge != null) _nudge.SetBase(camera.transform.position);

            // The chrome's own insets, in canvas units. The safe area is not
            // added: every layer below is parented inside HudRoot's safe-area
            // rect already, so adding it here would inset it twice.
            float chromeTop = TurnStrip.ReservedHeight + SquadRail.ReservedHeight;
            float chromeBottom = ActionTray.ReservedHeight + HistoryStrip.ReservedHeight;

            if (_tray != null) _tray.SetInsets(leftUnits, rightUnits);
            if (_toasts != null) _toasts.SetArea(leftUnits, rightUnits, chromeTop);
            if (_banner != null) _banner.SetArea(leftUnits, rightUnits, chromeTop);
            if (_turnButton != null) _turnButton.SetArea(leftUnits, rightUnits, chromeTop, chromeBottom);
            if (_pieceHud != null) _pieceHud.SetCeiling(hud ? chromeTop + TurnBanner.ReservedHeight : 0f);

            _framedWidth = Screen.width;
            _framedHeight = Screen.height;
            _framedWithPanel = showDevPanel;
            _framedLegacy = useLegacyPanel;
            _framedScale = scale;
            _framedWithHud = hud;
            _framedOnTitle = title;
            _framedCamera = tilted ? BoardCamera.Tilted : BoardCamera.TopDown;
            _framedLayout = ScreenLayout.Version;
        }

        /// <summary>The flat camera: an orthographic size, and a shift away from the panels.</summary>
        /// <remarks>
        /// The clip planes are set here as well as in the tilted branch. The
        /// long lens stands tens of units back and pushes the near plane out to
        /// match; leaving that behind on the way back to the flat camera, which
        /// sits ten units from the board, clips the board away entirely.
        /// </remarks>
        private static void FrameFlat(Camera camera, float extent, float aspect,
            float left, float right, float top, float bottom, float usableWidth, float usableHeight)
        {
            camera.orthographic = true;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;

            camera.orthographicSize = Mathf.Max(
                extent * FrameMargin / usableHeight,
                extent * FrameMargin / (aspect * usableWidth));

            float halfWidth = camera.orthographicSize * aspect;
            float halfHeight = camera.orthographicSize;

            // Centre of the free rectangle, as an offset from the screen centre
            // in viewport units (bottom-left origin).
            float centreX = left + usableWidth * 0.5f - 0.5f;
            float centreY = bottom + usableHeight * 0.5f - 0.5f;

            camera.transform.SetPositionAndRotation(
                new Vector3(-centreX * 2f * halfWidth, -centreY * 2f * halfHeight, -10f),
                Quaternion.identity);
        }

        /// <summary>
        /// The tilted camera (VISUAL_PASS.md, V1). False when no pose fits, so
        /// the caller can fall back to the flat one.
        /// </summary>
        /// <remarks>
        /// The pitch is steeper on the menu screens than in a match, because
        /// there is no HUD taking the edges and a steeper look shows more of
        /// the room behind the table.
        /// </remarks>
        private bool FrameTilted(Camera camera, float extent, float aspect,
            float x0, float x1, float y0, float y1, bool hud)
        {
            float pitch = hud ? TiltFraming.MatchPitch : TiltFraming.MenuPitch;
            float fov = TiltFraming.DefaultFieldOfView;

            // The same 12% of air the flat camera leaves around the board.
            var frame = TiltFraming.Solve(pitch, fov, aspect, extent * FrameMargin, x0, x1, y0, y1);
            if (!frame.Fitted) return false;

            TiltFraming.CameraPose(pitch, frame.Distance, frame.AimX, frame.AimY,
                out float px, out float py, out float pz);

            camera.orthographic = false;
            camera.fieldOfView = fov;

            // The board lies in the z = 0 plane and the flat camera looks down
            // +z at it, so the tilt is a rotation about x alone.
            camera.transform.SetPositionAndRotation(
                new Vector3(px, py, pz), Quaternion.Euler(-pitch, 0f, 0f));

            // A long lens sits far back, and the board has depth now, so the
            // clip planes have to reach it.
            camera.nearClipPlane = Mathf.Max(0.05f, frame.Distance - extent * 4f);
            camera.farClipPlane = frame.Distance + extent * 8f;
            return true;
        }

        /// <summary>
        /// Canvas units taken on the left: the dev panel, the OnGUI panel
        /// (converted from pixels), or the squad rail.
        /// </summary>
        private float LeftReservedUnits(float scale)
        {
            // Upright there is no width to give either panel: the rail lies
            // down under the top bar (M3) and the dev panel, which is a
            // debugging tool and not skinned, simply draws over the board.
            if (ScreenLayout.IsPortrait) return 0f;

            if (!showDevPanel) return SquadRail.ReservedWidth;
            if (useLegacyPanel) return PanelWidth / Mathf.Max(0.01f, scale);
            return ControlPanel.ReservedWidth;
        }

        /// <summary>The menu backdrop (VISUAL_PASS.md, V3), made on first use.</summary>
        private RoomBackdrop Room() =>
            _room ?? (_room = GetComponent<RoomBackdrop>() ?? gameObject.AddComponent<RoomBackdrop>());

        /// <summary>
        /// The table's near edge (VISUAL_PASS.md, V2), made on first use. It
        /// takes itself off screen under a flat camera, so unlike the room it
        /// needs nothing said to it once it is built.
        /// </summary>
        private TableBody Slab() =>
            _tableBody ?? (_tableBody = GetComponent<TableBody>() ?? gameObject.AddComponent<TableBody>());

        private void Update()
        {
            // A menu backdrop, so it is up whenever a card is. RoomBackdrop
            // holds it off when the camera could not be tilted.
            if (_room != null) _room.Visible = CurrentScreen != AppScreen.Match;

            // No match, no board behind the menus: the title, setup, draft and
            // guide sit on the backdrop instead (G6d). A card over a match
            // (pause, NEW MATCH, results) keeps the match behind it.
            if (_menuBackdrop != null) _menuBackdrop.Visible = _match == null;

            bool paused = ModalOpen;

            // The menu mirrors these flags, so their keys stay dead while it
            // is open rather than changing what it shows under the pointer.
            if (!paused)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (Input.GetKeyDown(KeyCode.Tab)) showDevPanel = !showDevPanel;
#endif
                if (Input.GetKeyDown(KeyCode.F2)) useLegacyPanel = !useLegacyPanel;
                if (Input.GetKeyDown(KeyCode.H)) showPieceHealth = !showPieceHealth;
                if (Input.GetKeyDown(KeyCode.L)) showFullLog = !showFullLog;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Dev only, compiled out of release builds. Ctrl+Shift+Numpad 0
                // wins the match for the seat to play (G7); Ctrl+Shift+Numpad 9
                // knocks out everyone on the track (G8c).
                if (DevChord(KeyCode.Keypad0)) DevWin();
                if (DevChord(KeyCode.Keypad9)) DevKnockOut();
#endif
            }

            // Driven every frame rather than on the keypress, so flipping the
            // inspector checkbox works too.
            if (_pieceHud != null)
            {
                _pieceHud.Visible = showPieceHealth;

                // Every frame, like the flag above: selection changes in half a
                // dozen places and a readout that missed one would sit wrong
                // until the next hit (H3).
                _pieceHud.SetFocus(_hovered != null ? _hovered.Operator : null, _selectedOperator);
            }

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
                (_title != null && _title.IsShowing) != _framedOnTitle ||
                _display.Camera != _framedCamera ||
                ScreenLayout.Version != _framedLayout ||
                (_hudRoot != null && !Mathf.Approximately(_hudRoot.ScaleFactor, _framedScale)))
            {
                FrameCamera();
            }

            SyncAudio();

            if (paused)
            {
                // Every full-screen card gets the plain arrow (G5).
                ShowCursor(CursorLook.Arrow);
                HandleModalKeys();
                return;
            }

            SyncMotion();

            if (_match == null)
            {
                ShowCursor(CursorLook.Arrow);
                return;
            }

            DriveBots();

            HandleKeys();
            UpdateHover();

            // A click on a board that is still catching up would aim at where
            // pieces were, not where they are.
            if (!Busy && Input.GetMouseButtonDown(0)) HandleBoardClick();
            if (Input.GetMouseButtonDown(1)) StepBack();

            ReleaseBufferedIntent();
            DrivePacer();

            // Last, so it reflects this frame's clicks and keys rather than the last frame's.
            // Esc may have opened the pause menu above; ModalOpen is read again for that.
            ShowCursor(ModalOpen ? CursorLook.Arrow : PointerCursor());
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

        private bool AnyPieceKnockingOut()
        {
            foreach (var piece in _pieces)
                if (piece != null && piece.IsKnockingOut) return true;

            return false;
        }

        private bool AnyPieceMoving()
        {
            foreach (var piece in _pieces)
                if (piece != null && piece.IsMoving) return true;

            return false;
        }

        /// <summary>
        /// Rolls for a human seat that let the roll clock run out, and ends a
        /// turn with nothing left in it (TurnPacer). Real time, because a clock
        /// the player reads is in seconds they feel; this runs only while no
        /// card is open, so pause freezes it all the same.
        /// </summary>
        private void DrivePacer()
        {
            // A press still waiting for the board is the player's own answer.
            bool waiting = _buffered != BufferedIntent.None;

            var command = _pacer.Tick(_match, humanTurn: !CpuTurn, busy: Busy || AnyPieceMoving() || waiting,
                Time.unscaledDeltaTime, rollClock, autoEndTurn);

            if (_turnButton != null) _turnButton.SetCountdown(_pacer.RollSecondsLeft);

            if (command == null) return;

            _log.Add(command is RollDiceCommand
                ? $"[clock] rolled for the seat after {TurnPacer.RollClockSeconds:0} s"
                : "[auto] nothing left to do: turn ended");
            Send(command);
        }

        // ── CPU seats (BOT2) ─────────────────────────────────────────────

        /// <summary>Whether the seat to play is a CPU's. Human input is ignored while it is.</summary>
        private bool CpuTurn => _bots != null && _match != null && _bots.IsCpuTurn(_match.Engine);

        /// <summary>
        /// Whether the player is asking the CPU to get on with it: Space held,
        /// or — with no keyboard to hold it on — a finger held anywhere
        /// (MOBILE.md, M4).
        /// </summary>
        /// <remarks>
        /// Held, not tapped, and only while a CPU is playing, so it cannot be
        /// confused with a tap on the board: nothing on the board is
        /// commandable on a CPU's turn anyway.
        /// </remarks>
        private static bool Hurrying =>
            Input.GetKey(KeyCode.Space) || (ScreenLayout.Touch && Input.touchCount > 0);

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
                cpuSpeed, hurry: Hurrying);
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
            // A passive's card from the operator card sits over the match: Esc
            // closes it, and the shortcuts wait under it as clicks do.
            if (GlossaryCard.IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) GlossaryCard.Close();
                return;
            }

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

            // Space rolls only when a roll is on offer, or when the board is
            // busy and the roll may be due once it settles (G7a). Pressed with
            // the dice spent, it used to send the roll anyway and toast the
            // engine's refusal, "a second roll needs doubles and a remaining
            // roll in the budget", at a player who was simply done.
            if (Input.GetKeyDown(KeyCode.Space) && (Busy || RollOnOffer)) Host.Roll();
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

                case AppScreen.Guide:
                    _guide.HandleKeys();
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

            // A finger that has left the glass is nowhere, but Input.mousePosition
            // still reports where it last was, which left a piece lifted and a
            // rail row washed for the rest of the turn (MOBILE.md, M4).
            if (ScreenLayout.Touch && Input.touchCount == 0)
            {
                SetHovered(null);
                return;
            }

            if (!_match.Engine.MatchOver && !PointerOverPanel() && BoardPointer.TryWorldPoint(out var world))
            {
                var piece = BoardPointer.PieceAt(world, Input.mousePosition, _pieces, PieceClickRadius * cellSpacing);

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
            RefreshAuraLane();

            var op = piece != null ? piece.Operator : null;
            if (_rail != null) _rail.SetHovered(op);
            if (_controls != null) _controls.SetHovered(op);
        }

        /// <summary>Hands a look to the cursor skin, if it exists.</summary>
        /// <remarks>
        /// There is no cursor on a touch screen, and dressing one that is not
        /// drawn costs a texture swap per frame for nothing (M4).
        /// </remarks>
        private void ShowCursor(CursorLook look)
        {
            if (ScreenLayout.Touch) return;
            if (_cursor != null) _cursor.Show(look);
        }

        /// <summary>
        /// What the pointer should look like over the match (G5): the arrow,
        /// unless an ability waits for a target.
        /// </summary>
        /// <remarks>
        /// <b>It asks what <see cref="AimAt"/> would do with a click here,</b>
        /// with the same radii and the same engine answers
        /// (<see cref="_legalCells"/>, <see cref="_castTargets"/>), so the
        /// reticle never promises a click the board then ignores.
        ///
        /// <b>The reticle is for aiming only.</b> An ability that fires from
        /// the caster has nothing to aim, so it keeps the arrow. Over one of
        /// the seat's own pieces that isn't a target, a click switches the
        /// selection, which is not an aim either, so that is the arrow too.
        ///
        /// <b>Blocked while the board is busy</b>, because the click is
        /// dropped then (<see cref="Update"/>).
        /// </remarks>
        private CursorLook PointerCursor()
        {
            if (_match.Engine.MatchOver || CpuTurn) return CursorLook.Arrow;
            if (_selectedOperator == null || _selectedAbility == null) return CursorLook.Arrow;
            if (!_selectedAbility.RequiresCell && !_selectedAbility.RequiresTarget) return CursorLook.Arrow;
            if (PointerOverPanel() || !BoardPointer.TryWorldPoint(out var world)) return CursorLook.Arrow;

            if (_selectedAbility.RequiresCell)
            {
                var cell = BoardPointer.CellAt(world, _legalCells, _layout, CellSnapRadius * cellSpacing);
                return cell != null && !Busy ? CursorLook.Aim : CursorLook.AimBlocked;
            }

            var piece = BoardPointer.PieceAt(world, Input.mousePosition, _pieces, PieceClickRadius * cellSpacing);

            if (piece != null && _castTargets.Contains(piece.Operator))
                return Busy ? CursorLook.AimBlocked : CursorLook.Aim;

            if (piece != null && IsCommandable(piece.Operator)) return CursorLook.Arrow;

            return CursorLook.AimBlocked;
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

            var piece = BoardPointer.PieceAt(world, Input.mousePosition, _pieces, PieceClickRadius * cellSpacing);

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Ctrl+Shift+<paramref name="key"/>, either Ctrl and either Shift.</summary>
        private static bool DevChord(KeyCode key) =>
            Input.GetKeyDown(key)
            && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        /// <summary>
        /// <b>Development only.</b> Wins the match for the side to play, to reach
        /// the results screen without playing a match out (LAUNCH_UI_PASS.md G7).
        /// </summary>
        /// <remarks>
        /// The engine does the work (<c>GameEngine.DevForceWin</c>): it sends
        /// the side home and ends the turn, and the win arrives as an ordinary
        /// <c>GameWon</c>, so the end screen, the history and the toasts all
        /// take their normal path. The view hands the events over, as it does
        /// for any command.
        /// </remarks>
        private void DevWin()
        {
            if (_match == null || _match.Engine.MatchOver) return;

            // Handle drops the selection once the match is over.
            _log.Add($"[DEV] forced a win for {_match.Engine.CurrentPlayer?.Color}");
            Handle(_match.Engine.DevForceWin(), immediate: false);
        }

        /// <summary>
        /// <b>Development only.</b> Knocks out every operator on the track, every
        /// seat's, so knockouts (G8c's burn) can be watched on demand.
        /// </summary>
        /// <remarks>
        /// The engine does the work (<c>GameEngine.DevKnockOutTrack</c>): each
        /// goes through the ordinary neutralize path to its yard, and the view
        /// gets plain <c>OperatorNeutralized</c> events. So the shatter, the burn,
        /// the sound, the voice lines, the history and the return to the yard all
        /// take their normal path. The turn goes on. With nobody on the track,
        /// the engine refuses and the refusal shows as any other would.
        /// </remarks>
        private void DevKnockOut()
        {
            if (_match == null || _match.Engine.MatchOver) return;

            _log.Add("[DEV] knocked out everyone on the track");
            Handle(_match.Engine.DevKnockOutTrack(), immediate: false);
        }
#endif

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
            bool walks = false, hits = false, knockouts = false, refused = false, home = false, executed = false;
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
                    case DamageDealt dealt when dealt.Cause == GameEngine.ExecuteCause: executed = true; break;
                    case DamageEvaded _:
                    case DamageAbsorbed _:
                    case DamageSheltered _:
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

            // The ability's signature, shared by the hit and knockout steps so
            // its impact is heard once however many pieces it lands on (AU3).
            var impact = new CastImpact(cast != null ? AbilitySounds.SlugOf(cast.Id) : null);

            if (hits)
                _queue.Enqueue(PresentationBeat.Hit, () => PlayFeedback(events, castBy, impact, knockouts: false),
                    hold: _motion.Tween(HitHoldSeconds));

            // An execute is preceded by silence: nothing reads as final like a
            // gap (AU3). The knockout step lifts it as the blow lands.
            if (executed && knockouts)
                _queue.Enqueue(PresentationBeat.Hush, () =>
                    {
                        if (_audio != null) _audio.Hush(HushCapSeconds);
                    },
                    hold: ExecuteHushSeconds);

            // A rigged figure folds before it shatters (LOOKBOOK LB5c), so the
            // beat also waits for every fall to land, not only for its hold.
            if (knockouts)
                _queue.Enqueue(PresentationBeat.Knockout, () => PlayFeedback(events, castBy, impact, knockouts: true),
                    () => !AnyPieceKnockingOut(),
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
                    // A rigged caster turns to its aim and raises its device (LOOKBOOK LB5c).
                    Vector3? aimAt = target != null ? target.Ground
                        : cell.HasValue ? _layout.PositionOf(cell.Value) : (Vector3?)null;
                    caster.Cast(aimAt, hold);

                    _tells.Play(
                        caster.transform.position,
                        target != null ? target.transform.position : (Vector3?)null,
                        cell.HasValue ? _layout.PositionOf(cell.Value) : (Vector3?)null);
                    // The ability's own tell if it has one, else the generic sweep or drop (AU3).
                    if (_audio == null ||
                        !_audio.PlaySignature(AbilitySounds.SlugOf(use.AbilityId), SignatureMoment.Tell,
                            caster.transform.position))
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

            // A hush whose knockout was flushed away must not outlive its batch.
            if (_audio != null) _audio.ReleaseHush();

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

            _matchLog.Add(events, engine.Round, refusals: !fromBot);
            if (_logPanel != null) _logPanel.MarkDirty();

            if (_banner == null) return;

            if (batch.TurnBegan != null && !engine.MatchOver)
            {
                _banner.Show(batch.TurnBegan.Player, engine.Round,
                    cpu: _bots != null && _bots.IsCpu(batch.TurnBegan.Player));
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
        /// The caster is included when the ability opts in to self-cast
        /// (<see cref="AbilityDefinition.AllowsSelfTarget"/>). Self-cast was
        /// settled on 2026-09-17 as per-ability opt-in — blanket self-cast was
        /// rejected because All-In Mauling's friendly mode is a heal — so the
        /// rule lives in the core's legality query, and this list simply passes
        /// it through. Javi's three and Lethe's Nano Cell declare it; nothing
        /// else does.
        /// </remarks>
        IReadOnlyList<OperatorState> IControlPanelHost.CastTargets()
        {
            if (_match == null || _selectedOperator == null || _selectedAbility == null)
                return new List<OperatorState>();

            return _match.Engine
                .LegalTargetsFor(_selectedOperator, _selectedAbility)
                .ToList();
        }

        bool IControlPanelHost.CpuTurn => CpuTurn;

        bool IControlPanelHost.DiceHeld => _diceHeld;

        string IControlPanelHost.SeatTag(PlayerColor seat) => SeatTag(seat);

        // Every intent below is ignored on a CPU's turn: the seat is not the pointer's to command.
        // Commands also wait for a busy board (MO1): Roll and End turn are kept briefly, the rest dropped.

        /// <summary>
        /// Whether a roll would be accepted now: the turn's opening roll, or a
        /// doubles re-roll with nothing owed. The engine's answers, the same
        /// ones the tray and the turn button read.
        /// </summary>
        private bool RollOnOffer
        {
            get
            {
                var engine = _match?.Engine;
                if (engine == null || engine.MatchOver) return false;
                return engine.Phase == TurnPhase.AwaitingRoll || (engine.CanRollAgain && !engine.DiceOwed);
            }
        }

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

        /// <remarks>
        /// A fresh seed, not the next one: System.Random's first draws for
        /// neighbouring seeds are correlated, so seed + 1 opened on dice much
        /// like the last match's. A pinned seed still steps by one, so a
        /// debugging run walks a known sequence.
        /// </remarks>
        void IControlPanelHost.Reseed()
        {
            seed = pinSeed ? seed + 1 : FreshSeed();
            NewMatch();
        }

        /// <summary>
        /// A seed for a new match. Choosing one is the view's business, not a
        /// rule (the setup screen's SHUFFLE draws the same way); the dice it
        /// gives come from the core.
        /// </summary>
        private static int FreshSeed() => Random.Range(1, 100000000);

        /// <summary>
        /// A silhouette in the squad rail was tapped: that operator's dossier,
        /// over the match (OPERATOR_GUIDE.md OG4). The guide is a full-screen
        /// card, so the bots and the board wait under it as they do under the
        /// pause menu, and leaving it comes straight back here.
        /// </summary>
        void IControlPanelHost.OpenDossier(OperatorState op)
        {
            if (op == null || _guide == null) return;

            SetHovered(null);
            _guideOverMatch = true;
            _guide.Open(Roster.ByName(op.Name), () => _guideOverMatch = false);
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
        DisplaySettings ISettingsHost.Display => _display;

        void IPauseHost.MainMenu()
        {
            SpeakQuit();
            ShowTitle();
        }

        /// <summary>From the pause menu; leaving the guide goes back to it (OG4).</summary>
        void IPauseHost.OpenGuide()
        {
            if (_guide == null) return;

            _guideOverMatch = true;
            _guide.Open(null, () =>
            {
                _guideOverMatch = false;
                OpenPause();
            });
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
            if (_guide != null) _guide.Close();
            _guideOverMatch = false;

            SetHovered(null);
            if (_match != null) RefreshMarks();

            // A new match gets a new seed. Only the setup's copy changes, so
            // BACK to a live match leaves that match's seed as it was; DEAL
            // commits it.
            var settings = ((IMatchFlowHost)this).Settings;
            if (!pinSeed) settings.Seed = FreshSeed();
            _setup.Open(settings);
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

        /// <summary>
        /// Title → OPERATORS (OPERATOR_GUIDE.md OG2). The title closes under
        /// it and comes back when the guide does; nothing else on the table
        /// changes, because nothing is at stake on this screen.
        /// </summary>
        void ITitleHost.OpenGuide()
        {
            if (_title != null) _title.Close();
            _guideOverMatch = false;
            _guide.Open();
        }

        private void CloseGuide()
        {
            if (_title != null && !_title.IsOpen) _title.Open();
        }

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
        ///
        /// <b>AU3:</b> a cast's first hit on anyone but the caster plays the
        /// ability's impact in place of the generic hit, once for the batch;
        /// its self-inflicted price keeps the generic hit. Every hit adds its
        /// damage-type layer (<see cref="AbilitySounds.LayerFor"/>). A heal from
        /// a cast adds the ability's assist; Kurbyn's own dodge replaces the
        /// miss; an execute's knockout carries the impact and lifts the hush.
        /// </remarks>
        private void PlayFeedback(
            IReadOnlyList<IGameEvent> events, OperatorState castBy, CastImpact signature, bool knockouts)
        {
            if (_feedback == null) return;

            if (knockouts && _audio != null) _audio.ReleaseHush();

            OperatorPiece impact = null;

            // Who a hit came from, for a rigged figure's recoil (LOOKBOOK LB5c):
            // the caster, or failing that the operator that walked into it.
            var striker = PieceFor(castBy ?? VoiceCasting.Mover(events));

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
                    piece.Recoil(striker != null && striker != piece ? striker.Ground : (Vector3?)null);
                    piece.ShowHealth(damaged.RemainingHealth);

                    bool big = damaged.RemainingHealth <= 0 ||
                               damaged.Amount >= BigHitFraction * damaged.Target.MaxHealth;
                    if (big) impact = piece;

                    bool self = castBy != null && ReferenceEquals(damaged.Target, castBy);
                    if (self || !Signature(signature, SignatureMoment.Impact, piece.transform.position))
                        Sound(big ? SoundCue.HitBig : SoundCue.Hit, piece.transform.position);
                    Layer(damaged, piece.transform.position);
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

                        // An operator whose passive is evasion has a dodge of its own (AU3).
                        if (_audio == null ||
                            !_audio.PlaySignature(AbilitySounds.EvasionOf(evaded.Target.Name), SignatureMoment.Impact,
                                piece.transform.position))
                            Sound(SoundCue.Miss, piece.transform.position);
                    }
                    continue;
                }

                var sheltered = e as DamageSheltered;
                if (sheltered != null)
                {
                    var piece = PieceFor(sheltered.Target);
                    if (piece != null)
                    {
                        _feedback.Sheltered(piece.transform.position);
                        Sound(SoundCue.Block, piece.transform.position);
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
                        Signature(signature, SignatureMoment.Assist, piece.transform.position);
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

                // Debt (COMBAT_SYSTEMS §3.3): the board shows a seat's debt
                // only while something happens to it; the standing figure is
                // in the rail and the top bar. Collection at a turn's end has
                // no piece to land on, so it shows there alone.
                var loan = e as DebtIncurred;
                if (loan != null)
                {
                    var piece = PieceFor(loan.Debtor);
                    if (piece != null) _feedback.DebtIncurred(piece.transform.position, loan.Amount);
                    continue;
                }

                var called = e as DebtCalled;
                if (called != null)
                {
                    var piece = PieceFor(called.Target);
                    if (piece != null) _feedback.DebtCalled(piece.transform.position, called.Amount);
                    continue;
                }

                var burned = e as DebtBurned;
                if (burned != null)
                {
                    var piece = PieceFor(burned.Creditor);
                    if (piece != null) _feedback.DebtBurned(piece.transform.position, burned.Amount);
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
                        if (down.Cause == GameEngine.ExecuteCause)
                            Signature(signature, SignatureMoment.Impact, piece.transform.position);
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
            RefreshAuraLane();
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

                // Every seat's HOME is drawn at the one vault (BoardLayout.PositionOf),
                // so finished pieces fan as one stack, whoever owns them (G6a).
                // Keyed by owner, three seats' finished pieces stood on one spot.
                if (cell.Kind == CellKind.Home) cell = CellRef.Home(PlayerColor.Red);

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
                    // A figure still folding (a flushed batch) comes back the same way.
                    if (piece.IsHidden || piece.IsCollapsing) piece.Reappear(position);
                    else if (immediate) piece.Place(position);
                    else piece.Settle(position);

                    // Statuses come from the engine, never from replaying
                    // StatusApplied/StatusExpired (PRESENTATION §1). Evasion and
                    // haste are drawn on the piece; everything else is a tag.
                    var statuses = _match.Engine.ActiveStatusesOn(piece.Operator);
                    bool hastened = statuses.Contains(StatusKind.Hastened);

                    piece.Refresh(
                        evasive: statuses.Contains(StatusKind.Evasion),
                        hastened: hastened,
                        travel: hastened && !yard ? TravelAt(piece.Operator) : (Vector3?)null);

                    if (_pieceHud != null)
                    {
                        _pieceHud.ShowStatuses(piece, statuses);

                        // Yard pieces show no readouts, so only stacks on the track spread.
                        _pieceHud.SetStack(piece, i, pair.Value.Count);
                        _pieceHud.SetRetired(piece, pair.Key.Kind == CellKind.Home);
                    }
                }
            }
        }

        /// <summary>
        /// The world direction from an operator's cell to the next one on its
        /// own path — which way its haste streaks trail from. At the last cell
        /// the step behind it is read instead.
        /// </summary>
        private Vector3? TravelAt(OperatorState op)
        {
            var map = _match.Map;
            int progress = op.Progress;
            if (progress < 0) return null;

            bool last = progress + 1 >= map.Profile.Journey;
            int from = last ? progress - 1 : progress;
            if (from < 0) return null;

            var a = _layout.PositionOf(map.CellAt(op.Owner, from));
            var b = _layout.PositionOf(map.CellAt(op.Owner, from + 1));
            var direction = b - a;
            return direction.sqrMagnitude > 1e-6f ? direction : (Vector3?)null;
        }

        /// <summary>
        /// The aura lane (2026-09-24): the cells the hovered — or else the
        /// selected — operator's aura covers, faintly, in its side's colour.
        /// Catalyst draws its radius and the wake behind Lethe in haste lime;
        /// Bouncer's drag draws in slow's colour. The cells are the engine's
        /// (<c>GameEngine.AuraCellsOf</c>).
        /// </summary>
        private void RefreshAuraLane()
        {
            if (_highlights == null) return;

            var subject = _hovered != null ? _hovered.Operator : _selectedOperator;
            if (_match == null || subject == null || _match.Engine.MatchOver)
            {
                _highlights.ClearAura();
                return;
            }

            var side = _match.Engine.AuraSideOf(subject);
            if (side == null)
            {
                _highlights.ClearAura();
                return;
            }

            var colour = side == AuraSide.Allies
                ? StatusPalette.OnBoard(StatusKind.Hastened)
                : StatusPalette.OnBoard(StatusKind.Slow);

            _highlights.ShowAura(_match.Engine.AuraCellsOf(subject), colour);
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