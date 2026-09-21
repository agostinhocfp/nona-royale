// Assets/_Project/Scripts/Unity/View/DraftScreen.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Draft;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The pre-match draft: the roster as a grid of cards three rows deep,
    /// each seat's three slots, the clock and the controls (DRAFT.md,
    /// increment DR2).
    /// </summary>
    /// <remarks>
    /// <b>Full canvas, not a card.</b> The operator cards and up to four seat
    /// rows need the room. The whole layout is a fixed 1840×1020 frame that
    /// scales down to fit narrower windows, so nothing overlaps at 4:3.
    ///
    /// <b>Three rows, as many columns as the pool needs (2026-09-17).</b> Nine
    /// operators fill a 3×3 grid at full card width. Lethe made ten, so the
    /// grid keeps its footprint and narrows its cards instead: four columns
    /// hold up to twelve. Past twelve the cards would be too narrow to read,
    /// and the layout needs a real decision rather than a smaller number.
    ///
    /// <b>The core owns the draft</b> (<see cref="DraftState"/>): whose pick
    /// it is, what a seat may take, what the clock does when it runs out. The
    /// screen feeds the clock unscaled time and shows the answers. A refused
    /// card greys out with the reason from <c>CanPick</c>. The only state kept
    /// here is presentational: the seat using the pointer in ALL PICK, and
    /// the card whose abilities are spelled out on the right.
    ///
    /// <b>ALL PICK:</b> choose a seat (click its row, or 1–4), then click
    /// cards to fill its slots. A seat that fills up hands the pointer to the
    /// next seat with room. Clicking a filled slot clears it. START deals once
    /// every slot is full; at zero the empty slots are filled at random and
    /// the match deals after a short beat.
    ///
    /// <b>SNAKE:</b> cards pick for the seat on the clock. UNDO takes one step
    /// back. A finished draft waits for START.
    ///
    /// <b>Leaving is never silent.</b> BACK (Esc) with picks made asks first,
    /// and the clock stops while it asks.
    ///
    /// <b>CPU seats pick on their own</b> (BOTS.md decisions 6 and 7). In ALL
    /// PICK, one CPU pick lands every <see cref="CpuPickInterval"/> seconds,
    /// taking turns across the CPU seats; in SNAKE a CPU seat picks
    /// <see cref="CpuThink"/> seconds into its turn. The pointer never picks
    /// for a CPU seat, its slots cannot be cleared, and UNDO is refused when
    /// the last pick was a CPU's. FILL &amp; START and RANDOM REST let the CPUs
    /// choose their own remaining picks first.
    /// </remarks>
    public sealed class DraftScreen : MonoBehaviour
    {
        /// <summary>
        /// The frame the whole screen composes into (MOBILE.md, M5). Wide, it
        /// is a fixed 1840×1020 that <see cref="FitFrame"/> scales down to
        /// whatever window it is in. Upright, scaling that frame to a phone
        /// would land at about a quarter size and nothing on it would be
        /// readable, so the upright frame is the screen itself and the layout
        /// inside it changes instead: the pool above, the seats and the detail
        /// below, rather than side by side.
        /// </summary>
        private static float FrameWidth => ScreenLayout.Pick(1840f, ScreenLayout.Reference.x - 24f);

        private static float FrameHeight => ScreenLayout.Pick(1020f, ScreenLayout.Reference.y - 28f);
        private const float FrameMargin = 40f;

        private const float CardWidth = 358f;

        /// <summary>
        /// A card's height. Upright a card is a portrait tile — shape, name,
        /// role, numbers — and its three ability lines move to the detail
        /// panel, which is where a tap sends them anyway (M5).
        /// </summary>
        private static float CardHeight => ScreenLayout.Pick(236f, 124f);

        private static float CardGap => ScreenLayout.Pick(14f, 10f);

        /// <summary>Rows of cards. The body is sized for exactly this many.</summary>
        private static int CardRows => ScreenLayout.IsPortrait ? 4 : 3;

        /// <summary>Columns upright: three tiles across a phone.</summary>
        private const int UprightColumns = 3;

        /// <summary>The grid's fixed width: three full-width cards and their gaps.</summary>
        private static float GridWidth => ScreenLayout.IsPortrait
            ? FrameWidth
            : 3f * CardWidth + 2f * 14f;

        private static float SlotWidth => 150f;

        /// <summary>Upright band heights: the header, the seat strip and the two-row footer.</summary>
        private const float UprightHeaderHeight = 100f;
        private const float UprightSeatsHeight = 76f;
        private const float UprightFooterHeight = 104f;

        /// <summary>One seat's three slot pips in the upright strip.</summary>
        private const float UprightPipSize = 26f;

        /// <summary>Seconds the filled table stays up after the ALL PICK clock runs out.</summary>
        private const float TimeUpHold = 1.2f;

        private static float ClockDigitSize => ScreenLayout.Pick(64f, 40f);

        /// <summary>
        /// Mono-spacing for the clock's digits (G5). Cinzel's figures are not
        /// tabular - its 1 is 344/1000 against a 0 at 552 - so at 64 pt the
        /// countdown jumps sideways by about 13 px whenever a 1 comes or goes.
        /// Its widest figure is 0.596 em; this leaves a hair of air. A literal
        /// string, not a formatted float, because a comma-decimal culture would
        /// write "0,62em" and TMP would drop the tag. Only the digits carry it:
        /// TIME and READY are words.
        /// </summary>
        private const string ClockMono = "<mspace=0.62em>";
        private static float ClockWordSize => ScreenLayout.Pick(40f, 26f);

        /// <summary>ALL PICK: seconds between CPU picks, across all CPU seats.</summary>
        private const float CpuPickInterval = 1.5f;

        /// <summary>ALL PICK: seconds before the first CPU pick.</summary>
        private const float CpuFirstPick = 1.0f;

        /// <summary>SNAKE: seconds a CPU seat takes over its pick.</summary>
        private const float CpuThink = 0.8f;

        /// <summary>The most clock time one frame may spend, in seconds.</summary>
        private const float MaxClockStep = 0.25f;

        /// <summary>The clock turns amber at this many seconds.</summary>
        private const float ClockWarning = 5f;

        private IMatchFlowHost _host;

        private RectTransform _root;
        private RectTransform _frame;
        private CanvasGroup _fader;
        private RectTransform _header;
        private RectTransform _grid;
        private GridLayoutGroup _gridLayout;
        private RectTransform _seatPanel;
        private RectTransform _detail;

        /// <summary>Upright only: the sheet the detail rides on, and its button row (M6).</summary>
        private RectTransform _sheet;
        private RectTransform _sheetActions;
        private RectTransform _footer;
        private TMP_Text _clock;
        private TMP_Text _clockCaption;

        private MatchSettings _settings;
        private DraftState _draft;
        private PlayerColor _active = PlayerColor.None;
        private OperatorDefinition _focus;
        private bool _leaveArmed;
        private float _hold = -1f;
        private bool _dirty;
        private bool _detailDirty;
        private bool _builtPortrait;

        private readonly Dictionary<PlayerColor, BotBrain> _cpu = new Dictionary<PlayerColor, BotBrain>();
        private float _cpuClock;
        private int _cpuTurn;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Bind(RectTransform canvasRect, IMatchFlowHost host)
        {
            _host = host;
            if (_root == null) Build(canvasRect);
            if (IsOpen) Close();
        }

        public void Open(MatchSettings settings)
        {
            if (_host == null || settings == null || !settings.Squads.IsDraft()) return;

            _settings = settings.Clone();
            var mode = _settings.Squads.ToDraftMode();

            // ALL PICK's draft RNG is freshly seeded, not match-seeded
            // (2026-09-17, designer). Its picks are human and its timeouts are
            // wall-clock, so the seed never made it reproducible anyway — all
            // the anchor did was deal the same "random" fills and CPU picks
            // whenever a seed repeated. The measured modes (RANDOM, and SNAKE's
            // fills) keep the match-seeded stream. The dice are unaffected:
            // the match RNG never fed the draft (DRAFT.md decision 9).
            int draftSeed = mode == DraftMode.AllPick
                ? Random.Range(1, 100000000)
                : _settings.Seed;

            _draft = DraftState.ForMatch(_settings.Seats, mode, draftSeed);

            // One brain per CPU seat, on the bots' draft stream (BOTS.md decision 4).
            _cpu.Clear();
            var random = BotConfig.Default.DraftRandomFor(draftSeed);
            foreach (var seat in _settings.Seats)
                if (_settings.IsCpu(seat)) _cpu[seat] = new BotBrain(_settings.PersonalityOf(seat), random);

            _cpuClock = _draft.Mode == DraftMode.AllPick ? CpuFirstPick : CpuThink;
            _cpuTurn = 0;

            _active = FirstHuman();
            _focus = null;
            _leaveArmed = false;
            _hold = -1f;

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Rebuild();

            UiTween.FadeIn(_fader, 0.28f);
            UiTween.SlideIn(_frame, new Vector2(0f, -22f), 0.3f);
        }

        public void Close()
        {
            _hold = -1f;
            _leaveArmed = false;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        // ── Input ────────────────────────────────────────────────────────

        /// <summary>The draft's keys. The composition root calls this while the screen is on top.</summary>
        public void HandleKeys()
        {
            if (!IsOpen || _hold >= 0f) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // The sheet is the innermost thing open, so it goes first (M6).
                if (_sheet != null && _sheet.gameObject.activeSelf) ClearFocus();
                else if (_leaveArmed) Stay();
                else RequestLeave();
                return;
            }

            if (_leaveArmed) return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) StartMatch();
            if (Input.GetKeyDown(KeyCode.Backspace)) Undo();

            if (_draft.Mode != DraftMode.AllPick) return;

            for (int i = 0; i < _draft.Seats.Count && i < 4; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    SetActive(_draft.Seats[i]);
            }
        }

        /// <summary>
        /// The seat a card click picks for: the chosen seat in ALL PICK, the
        /// seat on the clock in SNAKE. Never a CPU seat: the pointer is a human's.
        /// </summary>
        private PlayerColor Picker
        {
            get
            {
                var seat = _draft.Mode == DraftMode.AllPick ? _active : _draft.CurrentSeat;
                return IsCpu(seat) ? PlayerColor.None : seat;
            }
        }

        private bool Locked => _leaveArmed || _hold >= 0f;

        private void PickCard(OperatorDefinition op)
        {
            // Upright, a tap on a card opens the sheet and nothing else; the
            // sheet's own button makes the pick (M6). That holds for a mouse
            // too, so the editor at phone size behaves exactly as the phone
            // does rather than quietly taking a different path.
            if (ScreenLayout.IsPortrait)
            {
                Focus(op);
                MarkDirty();
                return;
            }

            _focus = op;
            if (Locked) { MarkDirty(); return; }

            var picker = Picker;
            if (picker != PlayerColor.None && _draft.Pick(picker, op) == DraftRefusal.None)
                AfterPick(picker);

            MarkDirty();
        }

        private void RandomOne()
        {
            if (Locked) return;

            var picker = Picker;
            if (picker != PlayerColor.None && _draft.RandomPick(picker) == DraftRefusal.None)
                AfterPick(picker);

            MarkDirty();
        }

        private void RandomRest()
        {
            if (Locked) return;

            // In turn order: a CPU pick is the CPU's choice, a human pick is random.
            while (!_draft.IsComplete)
            {
                var seat = _draft.CurrentSeat;
                if (IsCpu(seat)) CpuPick(seat);
                else _draft.RandomPick(seat);
            }

            MarkDirty();
        }

        private void FillAndStart()
        {
            if (Locked) return;

            foreach (var seat in _cpu.Keys)
                while (_draft.CanPickAny(seat) == DraftRefusal.None) CpuPick(seat);

            _draft.FillRandom();
            StartMatch();
        }

        private void Undo()
        {
            if (Locked || !UndoAllowed) return;

            _draft.Undo();
            MarkDirty();
        }

        private void ClearSlot(PlayerColor seat, int slot)
        {
            if (Locked || IsCpu(seat)) return;

            if (_draft.Clear(seat, slot) == DraftRefusal.None) _active = seat;
            MarkDirty();
        }

        private void SetActive(PlayerColor seat)
        {
            if (Locked || _draft.Mode != DraftMode.AllPick || IsCpu(seat)) return;

            _active = seat;
            MarkDirty();
        }

        /// <summary>A seat that just filled up hands the pointer to the next seat, in table order, with room.</summary>
        private void AfterPick(PlayerColor seat)
        {
            if (_draft.Mode != DraftMode.AllPick || _draft.NextEmptySlot(seat) >= 0) return;

            var seats = _draft.Seats;
            int start = IndexOf(seats, seat);

            for (int step = 1; step < seats.Count; step++)
            {
                var next = seats[(start + step) % seats.Count];
                if (IsCpu(next) || _draft.NextEmptySlot(next) < 0) continue;

                _active = next;
                return;
            }
        }

        private void StartMatch()
        {
            if (Locked || !_draft.IsComplete) return;
            Finish();
        }

        private void Finish()
        {
            var settings = _settings;
            var squads = _draft.Squads();
            Close();
            _host.FinishDraft(settings, squads);
        }

        private void RequestLeave()
        {
            if (_hold >= 0f) return;

            if (_draft.PickCount == 0)
            {
                Leave();
                return;
            }

            _leaveArmed = true;
            MarkDirty();
        }

        private void Stay()
        {
            _leaveArmed = false;
            MarkDirty();
        }

        private void Leave()
        {
            var settings = _settings;
            Close();
            _host.CancelDraft(settings);
        }

        // ── CPU seats ────────────────────────────────────────────────────

        private bool IsCpu(PlayerColor seat) => seat != PlayerColor.None && _cpu.ContainsKey(seat);

        /// <summary>UNDO takes back a human's pick only; a CPU would simply pick again (decision 7).</summary>
        private bool UndoAllowed =>
            _draft.CanUndo() == DraftRefusal.None && !IsCpu(_draft.LastPickSeat);

        private PlayerColor FirstHuman()
        {
            foreach (var seat in _draft.Seats)
                if (!IsCpu(seat)) return seat;
            return PlayerColor.None;
        }

        /// <summary>The seat's CPU chooses and picks. Falls back to a random pick if it offers nothing.</summary>
        private void CpuPick(PlayerColor seat)
        {
            var choice = _cpu[seat].PickDraft(_draft, seat);
            if (choice == null || _draft.Pick(seat, choice) != DraftRefusal.None)
                _draft.RandomPick(seat);

            // The human pointer may have been on a seat that is now full.
            if (_draft.Mode == DraftMode.AllPick && _active != PlayerColor.None && _draft.NextEmptySlot(_active) < 0)
                AfterPick(_active);

            MarkDirty();
        }

        /// <summary>Runs the CPU seats' clock and makes their picks when it is time.</summary>
        private void DriveCpus(float elapsed)
        {
            if (_cpu.Count == 0 || _draft.IsComplete || _draft.IsTimeUp) return;

            if (_draft.Mode == DraftMode.Snake)
            {
                var seat = _draft.CurrentSeat;
                if (!IsCpu(seat))
                {
                    _cpuClock = CpuThink;
                    return;
                }

                _cpuClock -= elapsed;
                if (_cpuClock > 0f) return;

                CpuPick(seat);
                _cpuClock = CpuThink;
                return;
            }

            _cpuClock -= elapsed;
            if (_cpuClock > 0f) return;
            _cpuClock = CpuPickInterval;

            // The next CPU seat, in turn, that still has room.
            var seats = new List<PlayerColor>(_cpu.Keys);
            seats.Sort();
            for (int step = 0; step < seats.Count; step++)
            {
                var seat = seats[(_cpuTurn + step) % seats.Count];
                if (_draft.CanPickAny(seat) != DraftRefusal.None) continue;

                _cpuTurn = (_cpuTurn + step + 1) % seats.Count;
                CpuPick(seat);
                return;
            }
        }

        private void Focus(OperatorDefinition op)
        {
            if (_focus == op) return;
            _focus = op;
            _detailDirty = true;
        }

        private void MarkDirty() => _dirty = true;

        // ── Frame loop ───────────────────────────────────────────────────

        private void Update()
        {
            if (!IsOpen) return;

            if (_root.GetSiblingIndex() != _root.parent.childCount - 1) _root.SetAsLastSibling();

            // The screen turned: the body is a row one way and a column the
            // other, so the frame is made again rather than re-anchored (M5).
            if (_builtPortrait != ScreenLayout.IsPortrait)
            {
                var old = _frame.gameObject;
                old.SetActive(false);
                Destroy(old);

                BuildFrame();
                Rebuild();
            }

            FitFrame();

            if (_hold >= 0f)
            {
                _hold -= Time.unscaledDeltaTime;
                if (_hold < 0f)
                {
                    Finish();
                    return;
                }
            }
            else if (!_leaveArmed)
            {
                // Unscaled: the draft happens outside the match, whatever the pause menu left behind.
                // Capped, so a stalled frame (editor pause, lost focus) cannot eat the clock at once.
                float elapsed = Mathf.Min(Time.unscaledDeltaTime, MaxClockStep);
                DriveCpus(elapsed);
                if (_draft.Tick(elapsed) > 0) MarkDirty();

                if (_draft.IsTimeUp)
                {
                    _hold = TimeUpHold;
                    MarkDirty();
                }
            }

            if (_dirty) Rebuild();
            else if (_detailDirty) RebuildDetail();

            UpdateClock();
        }

        private void FitFrame()
        {
            var area = _root.rect;

            // Upright the frame is the screen, minus a margin, at full size:
            // scaling a 1840-unit layout onto a phone lands near a quarter and
            // nothing on it can be read (M5).
            if (ScreenLayout.IsPortrait)
            {
                _frame.localScale = Vector3.one;
                _frame.sizeDelta = new Vector2(
                    Mathf.Max(200f, area.width - 24f), Mathf.Max(320f, area.height - 28f));
                PlaceSheet();
                return;
            }

            float scale = Mathf.Min(1f,
                area.width / (FrameWidth + FrameMargin),
                area.height / (FrameHeight + FrameMargin));
            if (scale > 0f) _frame.localScale = new Vector3(scale, scale, 1f);
        }

        private void UpdateClock()
        {
            if (_draft.Mode == DraftMode.AllPick)
            {
                _clockCaption.text = "DRAFT CLOCK";

                if (_draft.IsTimeUp)
                {
                    _clock.fontSize = ClockWordSize;
                    _clock.text = "TIME";
                    _clock.color = UiTheme.Threat;
                    return;
                }
            }
            else
            {
                _clockCaption.text = "PICK CLOCK";

                if (_draft.IsComplete)
                {
                    _clock.fontSize = ClockWordSize;
                    _clock.text = "READY";
                    _clock.color = UiTheme.Cyan;
                    return;
                }
            }

            float left = (float)_draft.SecondsLeft;
            _clock.fontSize = ClockDigitSize;
            _clock.text = ClockMono + Mathf.CeilToInt(left);
            _clock.color = _leaveArmed ? UiTheme.TextOff
                : left <= ClockWarning ? UiTheme.Threat
                : UiTheme.GoldBright;
        }

        // ── Scaffold ─────────────────────────────────────────────────────

        private void Build(RectTransform canvasRect)
        {
            _root = UiKit.Rect("draft_screen", canvasRect);
            UiKit.Stretch(_root);
            UiKit.Fill(_root, UiTheme.WithAlpha(UiTheme.Obsidian, 0.94f), blocksPointer: true);
            _fader = _root.gameObject.AddComponent<CanvasGroup>();

            BuildFrame();

            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Everything inside the scrim, in the arrangement the screen shape
        /// asks for. Built again from scratch when the phone is turned: the
        /// body is a row one way and a column the other, and a layout group
        /// cannot be swapped in place.
        /// </summary>
        private void BuildFrame()
        {
            _builtPortrait = ScreenLayout.IsPortrait;

            _frame = UiKit.Rect("frame", _root);
            _frame.anchorMin = _frame.anchorMax = new Vector2(0.5f, 0.5f);
            _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.sizeDelta = new Vector2(FrameWidth, FrameHeight);
            _frame.localScale = Vector3.one;

            var column = UiKit.Column(_frame, ScreenLayout.Pick(16f, 10f));
            column.childForceExpandHeight = false;

            if (_builtPortrait) BuildUprightFrame();
            else BuildWideFrame();
        }

        /// <summary>The clock, in its own framed box. The same in both arrangements, at two sizes.</summary>
        private void ClockBox(RectTransform parent, float width)
        {
            var clockBox = UiKit.Rect("clock", parent);
            UiKit.Fixed(clockBox, width);
            UiKit.Panel(clockBox, blocksPointer: false, fans: false);
            UiKit.Column(clockBox, 0f, ScreenLayout.IsPortrait ? 6 : 10).childAlignment = TextAnchor.MiddleCenter;

            _clock = UiKit.Label(clockBox, "", ScreenLayout.Pick(64f, 40f), UiTheme.GoldBright,
                TextAlignmentOptions.Center, bold: true);
            _clock.overflowMode = TextOverflowModes.Overflow;
            UiFonts.ApplyDisplay(_clock);
            UiKit.Size(_clock, height: ScreenLayout.Pick(76f, 46f));

            _clockCaption = UiKit.Label(clockBox, "", ScreenLayout.Pick(12f, 10f), UiTheme.Heading,
                TextAlignmentOptions.Center, bold: true);
            _clockCaption.characterSpacing = UiTheme.HeadingSpacing;
            UiKit.Size(_clockCaption, height: 16f);
        }

        /// <summary>The grid, made once; its cells are sized per draft in <see cref="FitGrid"/>.</summary>
        private void BuildGrid(RectTransform parent, bool fixedWidth)
        {
            _grid = UiKit.Rect("roster", parent);
            if (fixedWidth) UiKit.Fixed(_grid, GridWidth);
            _gridLayout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            _gridLayout.spacing = new Vector2(CardGap, CardGap);
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            FitGrid(Roster.All.Count);
        }

        private void BuildWideFrame()
        {
            _sheet = null;
            _sheetActions = null;

            // ── Header: title and strip on the left, clock on the right ──
            var top = UiKit.Rect("top", _frame);
            UiKit.Size(top, height: 130f);
            var topRow = UiKit.Row(top, 20f);
            topRow.childForceExpandHeight = true;

            _header = UiKit.Rect("header", top);
            UiKit.Size(_header, flexibleWidth: 1f);
            UiKit.Column(_header, 6f).padding.top = 8;

            ClockBox(top, 240f);

            // ── Body: cards on the left, seats and details on the right ──
            var body = UiKit.Rect("body", _frame);
            UiKit.Size(body, height: CardRows * CardHeight + (CardRows - 1) * CardGap);
            var bodyRow = UiKit.Row(body, 24f);
            bodyRow.childForceExpandHeight = true;
            bodyRow.childAlignment = TextAnchor.UpperLeft;

            BuildGrid(body, fixedWidth: true);

            var side = UiKit.Rect("side", body);
            UiKit.Size(side, flexibleWidth: 1f);
            UiKit.Column(side, 14f).childForceExpandHeight = false;

            _seatPanel = UiKit.Rect("seats", side);
            UiKit.Panel(_seatPanel, blocksPointer: true, fans: false);
            // No size fitter: the side column sizes it from its own column's preferred height.
            UiKit.Column(_seatPanel, 8f, 14);

            _detail = UiKit.Rect("detail", side);
            UiKit.Panel(_detail, blocksPointer: true);
            UiKit.Size(_detail, flexibleHeight: 1f);
            _detail.gameObject.AddComponent<RectMask2D>();
            UiKit.Column(_detail, 6f, 22).childForceExpandHeight = false;

            // ── Footer: controls ──
            _footer = UiKit.Rect("footer", _frame);
            UiKit.Size(_footer, height: 62f);
            UiKit.Row(_footer, 12f).childForceExpandHeight = true;
        }

        /// <summary>
        /// Upright (MOBILE.md, M6): header, seat strip, pool, footer — and the
        /// detail as a sheet that rises over the pool when a card is tapped.
        /// </summary>
        /// <remarks>
        /// <b>The side column could not survive the turn.</b> Beside the pool it
        /// held four seat rows and the detail panel; stacked under it, those
        /// wanted about 510 units of height in the 250 that were left, so the
        /// detail — which is where an upright card's ability lines went — was
        /// squeezed to nothing. Each of its two halves needed its own answer.
        ///
        /// <b>The seats become a strip of four tiles</b> rather than four rows.
        /// A row wanted 510 units of width for a gem, a name and three
        /// 150-unit slots; a tile carries the same seat in 106, because the
        /// three slots become three pips and the operator's name in a pick slot
        /// is something the pool already shows.
        ///
        /// <b>The detail becomes a bottom sheet with the PICK button on it.</b>
        /// That is what pays for the ability lines the tile cannot hold: one tap
        /// opens the operator in full, and the pick is made from the sheet
        /// rather than by tapping the tile a second time — an explicit button
        /// beats a second tap that looks exactly like the first.
        /// </remarks>
        private void BuildUprightFrame()
        {
            // ── Header ──
            var top = UiKit.Rect("top", _frame);
            UiKit.Size(top, height: UprightHeaderHeight);
            var topRow = UiKit.Row(top, 10f);
            topRow.childForceExpandHeight = true;

            _header = UiKit.Rect("header", top);
            UiKit.Size(_header, flexibleWidth: 1f);
            UiKit.Column(_header, 4f).padding.top = 6;

            ClockBox(top, 116f);

            // ── Seat strip ──
            _seatPanel = UiKit.Rect("seats", _frame);
            UiKit.Size(_seatPanel, height: UprightSeatsHeight, flexibleHeight: 0f);
            var seatRow = UiKit.Row(_seatPanel, 8f);
            seatRow.childForceExpandHeight = true;
            seatRow.childForceExpandWidth = true;

            // ── Pool ──
            var body = UiKit.Rect("body", _frame);
            UiKit.Size(body, flexibleHeight: 1f);
            var bodyColumn = UiKit.Column(body, 10f);
            bodyColumn.childForceExpandHeight = false;

            BuildGrid(body, fixedWidth: false);

            // ── Footer: two rows of controls ──
            _footer = UiKit.Rect("footer", _frame);
            UiKit.Size(_footer, height: UprightFooterHeight, flexibleHeight: 0f);
            UiKit.Column(_footer, 8f).childForceExpandHeight = true;

            // ── The detail sheet, over everything but the footer ──
            _sheet = UiKit.Rect("detail_sheet", _frame);
            _sheet.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            _sheet.anchorMin = new Vector2(0f, 0f);
            _sheet.anchorMax = new Vector2(1f, 0f);
            _sheet.pivot = new Vector2(0.5f, 0f);
            UiKit.Panel(_sheet, blocksPointer: true);

            var sheetColumn = UiKit.Column(_sheet, 8f, 16);
            sheetColumn.childForceExpandHeight = false;

            // The text scrolls. An operator with three abilities and their
            // descriptions runs past the sheet's height, and a mask on its own
            // would silently cut the last one off (M6).
            var viewport = UiKit.Rect("detail_viewport", _sheet);
            UiKit.Size(viewport, flexibleHeight: 1f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            _detail = UiKit.Rect("detail", viewport);
            _detail.anchorMin = new Vector2(0f, 1f);
            _detail.anchorMax = new Vector2(1f, 1f);
            _detail.pivot = new Vector2(0.5f, 1f);
            _detail.sizeDelta = Vector2.zero;
            UiKit.Column(_detail, 6f, 0).childForceExpandHeight = false;
            _detail.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = _detail;

            _sheetActions = UiKit.Rect("actions", _sheet);
            UiKit.Size(_sheetActions, height: 50f, flexibleHeight: 0f);
            UiKit.Row(_sheetActions, 8f).childForceExpandHeight = true;

            PlaceSheet();
            _sheet.gameObject.SetActive(false);
        }

        /// <summary>Sizes and seats the detail sheet above the footer.</summary>
        private void PlaceSheet()
        {
            if (_sheet == null) return;

            float height = Mathf.Clamp(_frame.rect.height * 0.46f, 220f, 380f);
            _sheet.sizeDelta = new Vector2(0f, height);
            _sheet.anchoredPosition = new Vector2(0f, UprightFooterHeight + 10f);
        }

        private void Rebuild()
        {
            _dirty = false;
            _detailDirty = false;

            RebuildHeader();
            RebuildGrid();
            RebuildSeats();
            RebuildDetail();
            RebuildFooter();

            LayoutRebuilder.ForceRebuildLayoutImmediate(_frame);
        }

        private static void Clear(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (!child.name.StartsWith("content")) continue;

                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        /// <summary>A laid-out child a rebuild will clear.</summary>
        private static RectTransform Content(RectTransform parent, string name, float height = -1f)
        {
            var rect = UiKit.Rect("content_" + name, parent);
            if (height >= 0f) UiKit.Size(rect, height: height);
            return rect;
        }

        // ── Header ───────────────────────────────────────────────────────

        private void RebuildHeader()
        {
            Clear(_header);

            bool allPick = _draft.Mode == DraftMode.AllPick;

            bool upright = ScreenLayout.IsPortrait;

            var title = UiKit.Caption(Content(_header, "title", upright ? 30f : 44f),
                allPick ? "ALL PICK" : "SNAKE DRAFT", upright ? 24f : 36f, UiTheme.GoldBright,
                TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = UiTheme.HeadingSpacing * (upright ? 0.8f : 1.5f);
            UiFonts.ApplyDisplay(title);

            var subtitle = UiKit.Caption(Content(_header, "subtitle", upright ? 32f : 24f), Subtitle(),
                upright ? UiTheme.FontSmall : UiTheme.FontBody, UiTheme.TextDim,
                TextAlignmentOptions.MidlineLeft);
            if (upright) subtitle.textWrappingMode = TextWrappingModes.Normal;

            if (allPick)
            {
                // The upright header has no room for prose, and the subtitle
                // above already names the seat that is picking (M6).
                if (upright) return;

                string cpus = _cpu.Count > 0 ? " CPU seats pick on their own." : "";
                UiKit.Caption(Content(_header, "hint", 22f),
                    $"Choose a seat (click it or press 1–{_draft.Seats.Count}), then click operators. " +
                    "Click a filled slot to clear it." + cpus,
                    UiTheme.FontSmall, UiTheme.TextOff, TextAlignmentOptions.MidlineLeft);
                return;
            }

            // The pick order, one diamond per pick: done ones dim, the current one large.
            var strip = Content(_header, "strip", upright ? 22f : 28f);
            var row = UiKit.Row(strip, upright ? 3f : 6f);
            row.childAlignment = TextAnchor.MiddleLeft;

            for (int i = 0; i < _draft.TotalPicks; i++)
            {
                var colour = UiTheme.Seat(_draft.SeatForPick(i));
                bool current = i == _draft.PickCount;
                bool done = i < _draft.PickCount;

                if (i > 0 && i % _draft.Seats.Count == 0) UiKit.Space(strip, upright ? 6f : 10f);

                float wide = upright ? 11f : 16f, narrow = upright ? 8f : 11f;

                UiKit.Diamond(strip,
                    done ? UiTheme.WithAlpha(colour, 0.3f) : colour,
                    current ? wide : narrow, current ? wide * 1.5f : narrow * 1.45f, outline: done);
            }
        }

        private string Subtitle()
        {
            if (_leaveArmed) return "Leave the draft? Every pick is lost.";
            if (_draft.IsTimeUp) return "Time. Empty slots were filled at random.";

            if (_draft.Mode == DraftMode.AllPick)
            {
                if (_draft.IsComplete)
                    return "Every slot is filled. START deals now, or swap until the clock runs out.";

                string who = _active == PlayerColor.None ? "The CPUs are" : $"{SeatWord(_active)} is";
                return $"{who} picking · {_draft.PickCount} of {_draft.TotalPicks} slots filled";
            }

            if (_draft.IsComplete) return "The draft is complete. START deals the match.";

            var seat = _draft.CurrentSeat;
            string cpu = IsCpu(seat) ? " (CPU)" : "";
            return $"{SeatWord(seat)}{cpu} to pick · pick {_draft.PickCount + 1} of {_draft.TotalPicks}";
        }

        // ── Roster grid ──────────────────────────────────────────────────

        private void RebuildGrid()
        {
            Clear(_grid);
            FitGrid(_draft.Pool.Count);

            var picker = Picker;
            foreach (var op in _draft.Pool) Card(op, picker);
        }

        /// <summary>
        /// Columns from the pool, never fewer than three; card width from the
        /// columns, so the grid's footprint never changes.
        /// </summary>
        private void FitGrid(int cards)
        {
            int columns = ScreenLayout.IsPortrait
                ? UprightColumns
                : Mathf.Max(3, Mathf.CeilToInt(cards / (float)CardRows));

            float cardWidth = (GridWidth - (columns - 1) * CardGap) / columns;

            _gridLayout.cellSize = new Vector2(cardWidth, CardHeight);
            _gridLayout.constraintCount = columns;

            // Upright the grid is in a column, so nothing else can tell it how
            // tall the pool makes it.
            if (!ScreenLayout.IsPortrait) return;

            int rows = Mathf.Max(1, Mathf.CeilToInt(cards / (float)columns));
            UiKit.Size(_grid, height: rows * CardHeight + (rows - 1) * CardGap, flexibleHeight: 0f);
        }

        private void Card(OperatorDefinition op, PlayerColor picker)
        {
            var refusal = picker == PlayerColor.None
                ? DraftRefusal.Complete
                : _draft.CanPick(picker, op);
            if (Locked) refusal = DraftRefusal.Complete;

            bool open = refusal == DraftRefusal.None;
            var seatColour = picker == PlayerColor.None ? UiTheme.Gold : UiTheme.Seat(picker);

            var button = UiKit.Button(_grid, "", () => PickCard(op),
                edge: open ? UiTheme.WithAlpha(seatColour, 0.9f) : UiTheme.Line);
            button.name = "content_card";

            // Upright, focus is a tap and only a tap (M6): a sheet that opened
            // under a passing mouse would be unusable, and on a touch screen
            // the enter event arrives with the press anyway, which would open
            // the sheet a frame before the tap meant to open it.
            if (!ScreenLayout.Touch && !ScreenLayout.IsPortrait)
                HoverRelay.On(button, () => Focus(op));

            var card = (RectTransform)button.transform;

            // Refused cards stay hoverable, so their abilities can still be read.
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = open ? 1f : 0.5f;

            if (ScreenLayout.IsPortrait)
            {
                UprightCard(card, op, open, seatColour);
                return;
            }

            var column = UiKit.Column(card, 4f, 14);
            column.childForceExpandHeight = false;

            // ── Shape, name, role, and who already holds it ──
            var top = UiKit.Rect("top", card);
            UiKit.Size(top, height: 56f);
            var topRow = UiKit.Row(top, 12f);
            topRow.childForceExpandHeight = false;

            var iconBox = UiKit.Rect("shape", top);
            UiKit.Fixed(iconBox, 52f, 52f);
            var portrait = OperatorArtLibrary.Portrait(op.Name);
            if (portrait != null) PortraitIcon(iconBox, portrait, op, open, seatColour);
            else ShapeIcon(iconBox, op, open, seatColour);

            var names = UiKit.Rect("names", top);
            UiKit.Size(names, flexibleWidth: 1f);
            UiKit.Column(names, 0f);

            var name = UiKit.Label(names, op.Name.ToUpperInvariant(), 24f, UiTheme.Text, bold: true);
            name.characterSpacing = 4f;
            UiKit.Size(name, height: 30f);
            var role = UiKit.Label(names, OperatorCopy.Role(op.Name).ToUpperInvariant(), 12f, UiTheme.Heading, bold: true);
            role.characterSpacing = UiTheme.HeadingSpacing;
            UiKit.Size(role, height: 18f);

            var holders = _draft.SeatsHolding(op);
            if (holders.Count > 0)
            {
                var gems = UiKit.Rect("holders", top);
                var gemRow = UiKit.Row(gems, 3f);
                gemRow.childAlignment = TextAnchor.UpperRight;
                foreach (var seat in holders) UiKit.Diamond(gems, UiTheme.Seat(seat), 10f, 15f);
            }

            // ── Numbers and traits ──
            var stats = UiKit.Rect("stats", card);
            UiKit.Size(stats, height: 22f);
            var statsRow = UiKit.Row(stats, 6f);
            statsRow.childForceExpandHeight = false;

            var numbers = UiKit.Label(stats,
                $"<color=#{UiTheme.Hex(UiTheme.TextDim)}>HP</color> <b>{op.MaxHealth}</b>   " +
                $"<color=#{UiTheme.Hex(UiTheme.TextDim)}>SPD</color> <b>×{op.BaseSpeed:0.0}</b>",
                UiTheme.FontSmall);
            UiKit.Size(numbers, flexibleWidth: 1f);

            if (op.Passive.HasValue)
                UiKit.Tag(stats, StatusPalette.Label(op.Passive.Value), StatusPalette.For(op.Passive.Value), 11f);
            if (op.Passive2.HasValue)
                UiKit.Tag(stats, StatusPalette.Label(op.Passive2.Value), StatusPalette.For(op.Passive2.Value), 11f);
            if (op.Aura != null)
                UiKit.Tag(stats, "AURA", UiTheme.GoldDeep, 11f);

            UiKit.Divider(card, vertical: false);

            // ── Abilities, one line each; an aura fills the slot after them
            //    (Bouncer, Lethe), and anything still missing is shown as missing ──
            for (int i = 0; i < Roster.SquadSize; i++)
            {
                var line = UiKit.Rect("ability", card);
                UiKit.Size(line, height: 22f);
                var lineRow = UiKit.Row(line, 8f);
                lineRow.childForceExpandHeight = true;

                if (i >= op.Abilities.Count)
                {
                    if (i == op.Abilities.Count && op.Aura != null)
                    {
                        var auraName = UiKit.Label(line, op.Aura.Name, 14f, UiTheme.Text);
                        UiKit.Size(auraName, flexibleWidth: 1f);
                        UiKit.Label(line, $"aura · r{op.Aura.Radius}", 13f, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);
                        continue;
                    }

                    // A named passive fills the slot the same way (Revú's Equilibrium).
                    if (i == op.Abilities.Count && op.PassiveName != null)
                    {
                        var passiveName = UiKit.Label(line, op.PassiveName, 14f, UiTheme.Text);
                        UiKit.Size(passiveName, flexibleWidth: 1f);
                        UiKit.Label(line, "passive", 13f, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);
                        continue;
                    }

                    UiKit.Label(line, "— not yet written —", 14f, UiTheme.TextOff);
                    continue;
                }

                var ability = op.Abilities[i];
                var abilityName = UiKit.Label(line, ability.Name, 14f, UiTheme.Text);
                UiKit.Size(abilityName, flexibleWidth: 1f);
                UiKit.Label(line, AbilityMeta(ability), 13f, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);
            }

            // ── Why it cannot be picked ──
            if (!open && refusal != DraftRefusal.Complete)
            {
                var reason = UiKit.Rect("reason", card);
                reason.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                reason.anchorMin = reason.anchorMax = new Vector2(1f, 1f);
                reason.pivot = new Vector2(1f, 1f);
                reason.anchoredPosition = new Vector2(-12f, -12f);
                reason.sizeDelta = new Vector2(120f, 22f);
                UiKit.Sliced(reason, DecoSprites.ChipFill, UiTheme.PanelInset);
                UiKit.Caption(reason, RefusalWord(refusal), 11f, UiTheme.Threat, TextAlignmentOptions.Center)
                    .fontStyle = FontStyles.Bold;
            }
        }

        /// <summary>
        /// A card as a tile: the shape, the name and the two numbers, and
        /// nothing else (MOBILE.md, M5). At a third of a phone's width there
        /// is no room for three ability lines; they live on the sheet a tap
        /// opens (M6), along with the button that takes the operator.
        /// </summary>
        private void UprightCard(RectTransform card, OperatorDefinition op, bool open, Color seatColour)
        {
            bool focused = _focus == op;

            var column = UiKit.Column(card, 2f, 8);
            column.childForceExpandHeight = false;
            column.childAlignment = TextAnchor.UpperCenter;

            var iconBox = UiKit.Rect("shape", card);
            UiKit.Size(iconBox, height: 42f);
            var portrait = OperatorArtLibrary.Portrait(op.Name);
            if (portrait != null) PortraitIcon(iconBox, portrait, op, open, seatColour);
            else ShapeIcon(iconBox, op, open, seatColour);

            var name = UiKit.Label(card, op.Name.ToUpperInvariant(), 14f,
                open ? UiTheme.Text : UiTheme.TextDim, TextAlignmentOptions.Center, bold: true);
            name.characterSpacing = 2f;
            name.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Size(name, height: 18f);

            UiKit.Size(UiKit.Label(card,
                $"<color=#{UiTheme.Hex(UiTheme.TextDim)}>HP</color> <b>{op.MaxHealth}</b>  " +
                $"<color=#{UiTheme.Hex(UiTheme.TextDim)}>SPD</color> <b>×{op.BaseSpeed:0.0}</b>",
                11f, UiTheme.Text, TextAlignmentOptions.Center), height: 14f);

            // Who already holds it, as seat diamonds along the bottom, and —
            // when this is the card a tap has focused — the word that says a
            // second tap takes it.
            var foot = UiKit.Rect("foot", card);
            UiKit.Size(foot, height: 14f);
            var footRow = UiKit.Row(foot, 3f);
            footRow.childAlignment = TextAnchor.MiddleCenter;

            foreach (var seat in _draft.SeatsHolding(op))
                UiKit.Diamond(foot, UiTheme.Seat(seat), 8f, 12f);

            // The sheet carries the pick, so the tile only has to say which
            // card the sheet is showing (M6).
            if (focused)
                UiKit.Label(foot, open ? "OPEN" : "LOOKING", 10f, UiTheme.Cyan,
                    TextAlignmentOptions.Center, bold: true);
        }

        /// <summary>The operator's shape, sized by health and tinted for the picking seat.</summary>
        private static void ShapeIcon(RectTransform box, OperatorDefinition op, bool open, Color seatColour)
        {
            float iconSize = Mathf.Lerp(36f, 52f, Mathf.InverseLerp(0.52f, 0.84f, PieceShape.SizeFor(op.MaxHealth)));
            var icon = UiKit.Icon(box, PieceShape.For(op.Name), open ? seatColour : UiTheme.PieceWaiting, iconSize);
            Centre(icon, iconSize);
        }

        /// <summary>
        /// A rendered portrait, drawn as painted, with the shape as a small
        /// seat-tinted pin in its corner (ART_HOOKUP.md, ART1). The pin keeps
        /// the shape the board uses in view, so the card still teaches it.
        /// </summary>
        private static void PortraitIcon(RectTransform box, Sprite portrait, OperatorDefinition op, bool open, Color seatColour)
        {
            var image = UiKit.Icon(box, portrait, open ? Color.white : UiTheme.PieceWaiting, 52f);
            Centre(image, 52f);

            const float pinSize = 18f;
            var pin = UiKit.Icon(box, PieceShape.For(op.Name), open ? seatColour : UiTheme.PieceWaiting, pinSize);
            var pinRect = (RectTransform)pin.transform;
            pinRect.anchorMin = pinRect.anchorMax = new Vector2(1f, 0f);
            pinRect.pivot = new Vector2(1f, 0f);
            pinRect.anchoredPosition = new Vector2(2f, -2f);
            pinRect.sizeDelta = new Vector2(pinSize, pinSize);
        }

        private static void Centre(Image image, float size)
        {
            var rect = (RectTransform)image.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
        }

        /// <summary>What an aura does and to whom, in the detail panel's words.</summary>
        private static string AuraReach(AuraDefinition aura)
        {
            string who = aura.Side == AuraSide.Allies ? "allies" : "enemies";
            if (aura.GrantsHaste) return $"hastens {who}";
            return aura.SpeedModifier < 0.0 ? $"slows {who}" : $"quickens {who}";
        }

        /// <summary>Cost, reach and cooldown, as the action tray words them, shortened.</summary>
        private static string AbilityMeta(AbilityDefinition ability)
        {
            string reach = ability.HasUnlimitedRange ? "any"
                : ability.Range == 0 ? "self"
                : $"r{ability.Range}";
            string cooldown = ability.CooldownTurns > 0 ? $" · cd {ability.CooldownTurns}" : "";

            return $"<color=#{UiTheme.Hex(UiTheme.Cyan)}>{ability.EnergyCost}e</color> · {reach}{cooldown}";
        }

        private static string RefusalWord(DraftRefusal refusal)
        {
            switch (refusal)
            {
                case DraftRefusal.AlreadyInSquad: return "IN SQUAD";
                case DraftRefusal.SquadFull: return "SQUAD FULL";
                case DraftRefusal.NotYourTurn: return "NOT YOUR PICK";
                case DraftRefusal.NotInPool: return "NOT IN POOL";
                case DraftRefusal.Complete: return "DRAFT CLOSED";
                default: return refusal.ToString().ToUpperInvariant();
            }
        }

        // ── Seats ────────────────────────────────────────────────────────

        private void RebuildSeats()
        {
            Clear(_seatPanel);

            if (ScreenLayout.IsPortrait)
            {
                for (int i = 0; i < _draft.Seats.Count; i++) SeatTile(_draft.Seats[i], i);
                return;
            }

            var heading = Content(_seatPanel, "heading", 22f);
            UiKit.Column(heading, 0f).childForceExpandHeight = true;
            UiKit.Heading(heading, $"Squads · {_draft.Seats.Count} seats");

            for (int i = 0; i < _draft.Seats.Count; i++) SeatRow(_draft.Seats[i], i);
        }

        /// <summary>
        /// One seat as a tile in the upright strip (M6): its colour and name,
        /// what it is doing, and its three slots as pips.
        /// </summary>
        /// <remarks>
        /// The wide row's three 150-unit slots each carry an operator's name.
        /// Here they are pips carrying its silhouette instead — the pool above
        /// is already showing every name, and four seats have to share 456
        /// units. A filled pip still clears on a tap in ALL PICK, which is the
        /// one thing a slot has to be able to do.
        /// </remarks>
        private void SeatTile(PlayerColor seat, int index)
        {
            bool allPick = _draft.Mode == DraftMode.AllPick;
            bool cpu = IsCpu(seat);
            bool picking = allPick ? seat == _active && !_draft.IsComplete : seat == _draft.CurrentSeat;
            var colour = UiTheme.Seat(seat);

            var slot = Content(_seatPanel, "seat");
            UiKit.Size(slot, flexibleWidth: 1f);
            UiKit.Column(slot, 0f).childForceExpandHeight = true;

            var button = UiKit.Button(slot, "", () => SetActive(seat), selected: picking,
                interactable: allPick && !cpu && !Locked);
            var tile = (RectTransform)button.transform;

            var column = UiKit.Column(tile, 2f, 5);
            column.childAlignment = TextAnchor.UpperCenter;
            column.childForceExpandHeight = false;

            var head = UiKit.Rect("head", tile);
            UiKit.Size(head, height: 18f);
            var headRow = UiKit.Row(head, 4f);
            headRow.childAlignment = TextAnchor.MiddleCenter;

            UiKit.Diamond(head, colour, 8f, 12f);

            var name = UiKit.Label(head, seat.ToString().ToUpperInvariant(), 12f,
                UiTheme.Readable(colour), bold: true);
            name.overflowMode = TextOverflowModes.Overflow;

            string state = _draft.NextEmptySlot(seat) < 0 ? "READY"
                : picking ? "PICKING"
                : $"{_draft.FilledCount(seat)}/{_draft.SquadSize}";
            if (cpu) state = $"CPU {state}";

            var stateLabel = UiKit.Label(tile, state, 10f, picking ? UiTheme.Cyan : UiTheme.TextDim,
                TextAlignmentOptions.Center, bold: true);
            UiKit.Size(stateLabel, height: 13f);

            var pips = UiKit.Rect("pips", tile);
            UiKit.Size(pips, height: UprightPipSize);
            var pipRow = UiKit.Row(pips, 4f);
            pipRow.childAlignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < _draft.SquadSize; i++) SlotPip(pips, seat, i, colour);
        }

        /// <summary>One slot in the upright strip: the operator's shape, or an empty mark.</summary>
        private void SlotPip(Transform parent, PlayerColor seat, int slot, Color colour)
        {
            var op = _draft.SlotOf(seat, slot);

            if (op == null)
            {
                var empty = UiKit.Rect($"pip_{slot}", parent);
                UiKit.Sliced(empty, DecoSprites.ChipFill, UiTheme.PanelInset);
                UiKit.Fixed(empty, UprightPipSize, UprightPipSize);
                UiKit.Caption(empty, "·", 14f, UiTheme.TextOff, TextAlignmentOptions.Center);
                return;
            }

            bool clearable = _draft.CanClear(seat, slot) == DraftRefusal.None && !IsCpu(seat) && !Locked;

            var button = UiKit.Button(parent, "", () => ClearSlot(seat, slot), MarkDirty,
                interactable: clearable, tint: UiTheme.PanelInset);
            UiKit.Fixed(button, UprightPipSize, UprightPipSize);

            var rect = (RectTransform)button.transform;
            var icon = UiKit.Icon(rect, PieceShape.For(op.Name), colour, UprightPipSize - 10f);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(UprightPipSize - 10f, UprightPipSize - 10f);
        }

        private void SeatRow(PlayerColor seat, int index)
        {
            bool allPick = _draft.Mode == DraftMode.AllPick;
            bool cpu = IsCpu(seat);
            bool picking = allPick ? seat == _active && !_draft.IsComplete : seat == _draft.CurrentSeat;
            var colour = UiTheme.Seat(seat);

            var slot = Content(_seatPanel, "seat", 76f);
            UiKit.Column(slot, 0f).childForceExpandHeight = true;

            var button = UiKit.Button(slot, "", () => SetActive(seat), selected: picking,
                interactable: allPick && !cpu && !Locked);
            var row = (RectTransform)button.transform;
            var layout = UiKit.Row(row, 8f);
            layout.padding = new RectOffset(14, 10, 8, 8);
            layout.childForceExpandHeight = true;

            var gem = UiKit.Rect("gem", row);
            UiKit.Fixed(gem, 16f);
            var diamond = UiKit.Diamond(gem, colour, 14f, 21f);
            var gemRect = (RectTransform)diamond.transform;
            gemRect.anchorMin = gemRect.anchorMax = new Vector2(0.5f, 0.5f);
            gemRect.sizeDelta = new Vector2(14f, 21f);

            // Wide enough for "CPU · BRAWLER · 2/3".
            var names = UiKit.Rect("names", row);
            UiKit.Fixed(names, 150f);
            UiKit.Column(names, 0f).childAlignment = TextAnchor.MiddleLeft;

            string key = allPick && !cpu && index < 4 ? $"  <size=70%><color=#{UiTheme.Hex(UiTheme.Gold)}>{index + 1}</color></size>" : "";
            var name = UiKit.Label(names, seat.ToString().ToUpperInvariant() + key, UiTheme.FontBody,
                UiTheme.Readable(colour), bold: true);
            UiKit.Size(name, height: 26f);

            string state = _draft.NextEmptySlot(seat) < 0 ? "READY"
                : picking ? "PICKING"
                : $"{_draft.FilledCount(seat)}/{_draft.SquadSize}";
            if (cpu) state = $"CPU · {_cpu[seat].Personality.Label()} · {state}";
            var stateLabel = UiKit.Label(names, state, 11f, picking ? UiTheme.Cyan : UiTheme.TextDim, bold: true);
            stateLabel.characterSpacing = UiTheme.HeadingSpacing * 0.5f;
            UiKit.Size(stateLabel, height: 16f);

            for (int i = 0; i < _draft.SquadSize; i++) PickSlot(row, seat, i);
        }

        private void PickSlot(Transform row, PlayerColor seat, int slot)
        {
            var op = _draft.SlotOf(seat, slot);

            if (op == null)
            {
                // An empty slot does nothing on click, but still reads as a slot.
                var empty = UiKit.Button(row, "—", null, interactable: false);
                UiKit.Fixed(empty, SlotWidth);
                return;
            }

            // Only ALL PICK clears; a snake slot is a plain, hoverable label.
            bool clearable = _draft.CanClear(seat, slot) == DraftRefusal.None && !IsCpu(seat) && !Locked;
            var button = UiKit.Button(row, "", () => ClearSlot(seat, slot), interactable: clearable,
                tint: UiTheme.PanelInset);
            UiKit.Fixed(button, SlotWidth);
            HoverRelay.On(button, () => Focus(op));

            var rect = (RectTransform)button.transform;
            var layout = UiKit.Row(rect, 6f);
            layout.padding = new RectOffset(8, 6, 0, 0);
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            UiKit.Icon(rect, PieceShape.For(op.Name), UiTheme.Seat(seat), 24f);

            var name = UiKit.Label(rect, op.Name, 14f, UiTheme.Text, bold: true);
            UiKit.Size(name, flexibleWidth: 1f);

            if (_draft.WasRandom(seat, slot))
                UiKit.Label(rect, "RND", 10f, UiTheme.TextOff, TextAlignmentOptions.MidlineRight, bold: true);
        }

        // ── Detail ───────────────────────────────────────────────────────

        /// <summary>
        /// The sheet's two buttons: close it, or take the operator it is
        /// showing (M6).
        /// </summary>
        /// <remarks>
        /// <b>The pick is a button, not a second tap.</b> A phone has no hover,
        /// so the first tap on a card has to be the look; making the second tap
        /// the pick meant two identical gestures with very different
        /// consequences, on a choice that cannot be undone in ALL PICK. A
        /// labelled button says which one takes the operator.
        ///
        /// It is refused for the same reasons a card is, and says which:
        /// <c>CanPick</c> is the engine's answer, and the wide screen's greyed
        /// card is the same refusal drawn differently.
        /// </remarks>
        private void RebuildSheetActions(OperatorDefinition op)
        {
            if (_sheetActions == null) return;

            for (int i = _sheetActions.childCount - 1; i >= 0; i--)
            {
                var child = _sheetActions.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            if (op == null) return;

            var close = UiKit.Button(_sheetActions, "CLOSE", ClearFocus, MarkDirty, size: UiTheme.FontSmall);
            UiKit.Fixed(close, 110f);

            var picker = Picker;
            bool noSeat = picker == PlayerColor.None;
            var refusal = noSeat || Locked ? DraftRefusal.Complete : _draft.CanPick(picker, op);
            bool open = refusal == DraftRefusal.None;

            // A seat that was never chosen is not a closed draft: in ALL PICK
            // the tile above is what the player has to press first.
            string label = open ? $"PICK FOR {picker.ToString().ToUpperInvariant()}"
                : noSeat && !Locked && _draft.Mode == DraftMode.AllPick ? "CHOOSE A SEAT"
                : noSeat && !Locked ? "CPU IS PICKING"
                : RefusalWord(refusal);

            var pick = UiKit.Button(_sheetActions, label, () => TakeFocused(op), MarkDirty,
                interactable: open,
                tint: open ? UiTheme.CyanDeep : (Color?)null,
                edge: open ? UiTheme.Cyan : (Color?)null,
                size: UiTheme.FontBody);
            UiKit.Size(pick, flexibleWidth: 1f);
        }

        /// <summary>Takes the operator the sheet is showing, then closes it.</summary>
        private void TakeFocused(OperatorDefinition op)
        {
            var picker = Picker;
            if (Locked || picker == PlayerColor.None) return;

            if (_draft.Pick(picker, op) == DraftRefusal.None) AfterPick(picker);

            ClearFocus();
        }

        /// <summary>Closes the sheet.</summary>
        private void ClearFocus()
        {
            _focus = null;
            _detailDirty = true;
            MarkDirty();
        }

        private void RebuildDetail()
        {
            _detailDirty = false;
            Clear(_detail);

            var op = _focus;
            bool upright = ScreenLayout.IsPortrait;

            // Upright the detail is a sheet: it is not there at all until a
            // card is tapped, so it has no empty state to draw (M6).
            if (upright)
            {
                RebuildSheetActions(op);
                if (_sheet != null) _sheet.gameObject.SetActive(op != null);
                if (op == null) return;
            }
            else if (op == null)
            {
                var hint = Content(_detail, "hint", 60f);
                UiKit.Caption(hint, "Hover a card to read its abilities.", UiTheme.FontBody, UiTheme.TextOff,
                    TextAlignmentOptions.Center);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_detail);
                return;
            }

            var headingSlot = Content(_detail, "heading", 22f);
            UiKit.Column(headingSlot, 0f).childForceExpandHeight = true;
            UiKit.Heading(headingSlot, OperatorCopy.Role(op.Name));

            var title = UiKit.Caption(Content(_detail, "name", 34f), op.Name.ToUpperInvariant(), 28f,
                UiTheme.GoldBright, TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 6f;

            var traits = new List<string> { $"{op.MaxHealth} health", $"speed ×{op.BaseSpeed:0.0}" };
            if (op.Passive.HasValue)
            {
                // A passive's magnitude was a speed bonus (Kurbyn, before
                // 2026-09-17); the roster's passives now carry none.
                string magnitude = op.PassiveMagnitude != 0.0 ? $" ({op.PassiveMagnitude:+0.0;-0.0} speed)" : "";
                string passiveLabel = op.PassiveName ?? StatusPalette.Label(op.Passive.Value);
                traits.Add($"passive {passiveLabel}{magnitude}");
                if (op.Passive2.HasValue)
                    traits.Add($"passive {StatusPalette.Label(op.Passive2.Value)}");
            }
            if (op.Aura != null) traits.Add($"aura {op.Aura.Name}, radius {op.Aura.Radius}, {AuraReach(op.Aura)}");

            Paragraph("traits", string.Join("  ·  ", traits), UiTheme.TextDim, UiTheme.FontSmall);

            for (int i = 0; i < op.Abilities.Count; i++)
            {
                var ability = op.Abilities[i];
                var line = Content(_detail, "ability", 26f);
                var lineRow = UiKit.Row(line, 8f);
                lineRow.childForceExpandHeight = true;

                var name = UiKit.Label(line, ability.Name, UiTheme.FontBody, UiTheme.Text, bold: true);
                UiKit.Size(name, flexibleWidth: 1f);
                UiKit.Label(line, AbilityMeta(ability), UiTheme.FontSmall, UiTheme.TextDim,
                    TextAlignmentOptions.MidlineRight);

                Paragraph("description", ability.Description, UiTheme.TextDim, 14f);
            }

            if (op.Abilities.Count + (op.Aura != null || op.PassiveName != null ? 1 : 0) < Roster.SquadSize)
                Paragraph("missing",
                    $"{op.Name} has {op.Abilities.Count} of {Roster.SquadSize} abilities; the rest are not written yet.",
                    UiTheme.Threat, 14f);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_detail);
        }

        /// <summary>Wrapped text that takes the height it needs.</summary>
        private void Paragraph(string name, string text, Color colour, float size)
        {
            var slot = Content(_detail, name);
            UiKit.Column(slot, 0f);
            UiKit.Label(slot, text, size, colour, wrap: true);
        }

        // ── Footer ───────────────────────────────────────────────────────

        private void RebuildFooter()
        {
            Clear(_footer);

            if (ScreenLayout.IsPortrait)
            {
                RebuildUprightFooter();
                return;
            }

            if (_leaveArmed)
            {
                FooterButton("LEAVE DRAFT", "", Leave, 240f, UiTheme.GoldDeep, UiTheme.Threat);
                FooterButton("STAY", "Esc", Stay, 200f);
                FooterNote("The clock is stopped. Leaving returns to setup and loses every pick.");
                return;
            }

            bool open = _hold < 0f;
            bool allPick = _draft.Mode == DraftMode.AllPick;

            FooterButton("BACK", "Esc", RequestLeave, 180f, interactable: open);
            FooterNote(allPick
                ? "Picks are open to every seat. Two seats may field the same operator; one seat may not field it twice."
                : "Snake order reverses each round. A pick that runs out of time is made at random.");

            FooterButton("RANDOM", "", RandomOne, 170f,
                interactable: open && Picker != PlayerColor.None && _draft.CanPickAny(Picker) == DraftRefusal.None);

            if (allPick)
            {
                FooterButton("FILL & START", "", FillAndStart, 220f, interactable: open);
            }
            else
            {
                FooterButton("UNDO", "Bksp", Undo, 170f, interactable: open && UndoAllowed);
                FooterButton("RANDOM REST", "", RandomRest, 210f, interactable: open && !_draft.IsComplete);
            }

            bool ready = open && _draft.IsComplete;
            var start = FooterButton("START", "Enter", StartMatch, 220f,
                ready ? UiTheme.CyanDeep : (Color?)null, ready ? UiTheme.Cyan : (Color?)null, ready);
            if (ready) UiKit.Pulse(start, UiTheme.Cyan);
        }

        /// <summary>
        /// The controls, on two rows (M6). Five buttons need about 950 units
        /// side by side and the upright frame has 456, so they wrap — and the
        /// prose note goes, because the header's subtitle already carries the
        /// state and the rules belong on the setup screen, not under a clock.
        /// </summary>
        private void RebuildUprightFooter()
        {
            var top = Content(_footer, "row_top");
            UiKit.Row(top, 8f).childForceExpandHeight = true;
            var bottom = Content(_footer, "row_bottom");
            UiKit.Row(bottom, 8f).childForceExpandHeight = true;

            if (_leaveArmed)
            {
                UprightButton(top, "LEAVE DRAFT", Leave, UiTheme.GoldDeep, UiTheme.Threat);
                UprightButton(bottom, "STAY", Stay);
                return;
            }

            bool open = _hold < 0f;
            bool allPick = _draft.Mode == DraftMode.AllPick;

            UprightButton(top, "BACK", RequestLeave, interactable: open);
            UprightButton(top, "RANDOM", RandomOne,
                interactable: open && Picker != PlayerColor.None && _draft.CanPickAny(Picker) == DraftRefusal.None);

            if (allPick)
            {
                UprightButton(top, "FILL ALL", FillAndStart, interactable: open);
            }
            else
            {
                UprightButton(top, "UNDO", Undo, interactable: open && UndoAllowed);
                UprightButton(bottom, "RANDOM REST", RandomRest, interactable: open && !_draft.IsComplete);
            }

            bool ready = open && _draft.IsComplete;
            var start = UprightButton(bottom, "START", StartMatch,
                ready ? UiTheme.CyanDeep : (Color?)null, ready ? UiTheme.Cyan : (Color?)null, ready);
            if (ready) UiKit.Pulse(start, UiTheme.Cyan);
        }

        /// <summary>A footer button that shares its row's width rather than claiming a fixed slice.</summary>
        private Button UprightButton(Transform row, string label, System.Action press,
            Color? fill = null, Color? edge = null, bool interactable = true)
        {
            var button = UiKit.Button(row, label, press, MarkDirty, interactable, size: UiTheme.FontSmall,
                tint: fill, edge: edge);
            UiKit.Size(button, flexibleWidth: 1f);
            return button;
        }

        private Button FooterButton(string label, string key, System.Action press, float width,
            Color? fill = null, Color? edge = null, bool interactable = true)
        {
            string text = string.IsNullOrEmpty(key)
                ? label
                : $"{label}  <size=60%><color=#{UiTheme.Hex(UiTheme.Gold)}>{key}</color></size>";

            var button = UiKit.Button(_footer, text, press, MarkDirty, interactable, size: UiTheme.FontBody,
                tint: fill, edge: edge);
            button.name = "content_button";
            UiKit.Fixed(button, width);
            return button;
        }

        private void FooterNote(string text)
        {
            var note = UiKit.Label(_footer, text, 13f, UiTheme.TextOff, TextAlignmentOptions.Center, wrap: true);
            note.name = "content_note";
            UiKit.Size(note, flexibleWidth: 1f);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static string SeatWord(PlayerColor seat) =>
            $"<color=#{UiTheme.Hex(UiTheme.Readable(UiTheme.Seat(seat)))}><b>{seat.ToString().ToUpperInvariant()}</b></color>";

        private static int IndexOf(IReadOnlyList<PlayerColor> seats, PlayerColor seat)
        {
            for (int i = 0; i < seats.Count; i++)
                if (seats[i] == seat) return i;
            return 0;
        }
    }
}