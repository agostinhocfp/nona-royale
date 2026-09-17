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
        private const float FrameWidth = 1840f;
        private const float FrameHeight = 1020f;
        private const float FrameMargin = 40f;

        private const float CardWidth = 358f;
        private const float CardHeight = 236f;
        private const float CardGap = 14f;

        /// <summary>Rows of cards. The body is sized for exactly this many.</summary>
        private const int CardRows = 3;

        /// <summary>The grid's fixed width: three full-width cards and their gaps.</summary>
        private const float GridWidth = 3f * CardWidth + 2f * CardGap;
        private const float SlotWidth = 150f;

        /// <summary>Seconds the filled table stays up after the ALL PICK clock runs out.</summary>
        private const float TimeUpHold = 1.2f;

        private const float ClockDigitSize = 64f;
        private const float ClockWordSize = 40f;

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
            _draft = DraftState.ForMatch(_settings.Seats, _settings.Squads.ToDraftMode(), _settings.Seed);

            // One brain per CPU seat, on the bots' draft stream (BOTS.md decision 4).
            _cpu.Clear();
            var random = BotConfig.Default.DraftRandomFor(_settings.Seed);
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

            UiTween.FadeIn(_fader, 0.2f);
            UiTween.SlideIn(_frame, new Vector2(0f, -14f), 0.22f);
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
                if (_leaveArmed) Stay();
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
            _clock.text = Mathf.CeilToInt(left).ToString();
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

            _frame = UiKit.Rect("frame", _root);
            _frame.anchorMin = _frame.anchorMax = new Vector2(0.5f, 0.5f);
            _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.sizeDelta = new Vector2(FrameWidth, FrameHeight);

            var column = UiKit.Column(_frame, 16f);
            column.childForceExpandHeight = false;

            // ── Header: title and strip on the left, clock on the right ──
            var top = UiKit.Rect("top", _frame);
            UiKit.Size(top, height: 130f);
            var topRow = UiKit.Row(top, 20f);
            topRow.childForceExpandHeight = true;

            _header = UiKit.Rect("header", top);
            UiKit.Size(_header, flexibleWidth: 1f);
            UiKit.Column(_header, 6f).padding.top = 8;

            var clockBox = UiKit.Rect("clock", top);
            UiKit.Fixed(clockBox, 240f);
            UiKit.Panel(clockBox, blocksPointer: false, fans: false);
            UiKit.Column(clockBox, 0f, 10).childAlignment = TextAnchor.MiddleCenter;

            _clock = UiKit.Label(clockBox, "", 64f, UiTheme.GoldBright, TextAlignmentOptions.Center, bold: true);
            _clock.overflowMode = TextOverflowModes.Overflow;
            UiFonts.ApplyDisplay(_clock);
            UiKit.Size(_clock, height: 76f);
            _clockCaption = UiKit.Label(clockBox, "", 12f, UiTheme.Heading, TextAlignmentOptions.Center, bold: true);
            _clockCaption.characterSpacing = UiTheme.HeadingSpacing;
            UiKit.Size(_clockCaption, height: 18f);

            // ── Body: cards on the left, seats and details on the right ──
            var body = UiKit.Rect("body", _frame);
            UiKit.Size(body, height: CardRows * CardHeight + (CardRows - 1) * CardGap);
            var bodyRow = UiKit.Row(body, 24f);
            bodyRow.childForceExpandHeight = true;
            bodyRow.childAlignment = TextAnchor.UpperLeft;

            // Cell size and column count are set per draft, in RebuildGrid:
            // the pool is not known until a draft opens.
            _grid = UiKit.Rect("roster", body);
            UiKit.Fixed(_grid, GridWidth);
            _gridLayout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            _gridLayout.spacing = new Vector2(CardGap, CardGap);
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            FitGrid(Roster.All.Count);

            var side = UiKit.Rect("side", body);
            UiKit.Size(side, flexibleWidth: 1f);
            var sideColumn = UiKit.Column(side, 14f);
            sideColumn.childForceExpandHeight = false;

            _seatPanel = UiKit.Rect("seats", side);
            UiKit.Panel(_seatPanel, blocksPointer: true, fans: false);
            // No size fitter: the side column sizes it from its own column's preferred height.
            UiKit.Column(_seatPanel, 8f, 14);

            _detail = UiKit.Rect("detail", side);
            UiKit.Panel(_detail, blocksPointer: true);
            UiKit.Size(_detail, flexibleHeight: 1f);
            _detail.gameObject.AddComponent<RectMask2D>();
            var detailColumn = UiKit.Column(_detail, 6f, 22);
            detailColumn.childForceExpandHeight = false;

            // ── Footer: controls ──
            _footer = UiKit.Rect("footer", _frame);
            UiKit.Size(_footer, height: 62f);
            var footerRow = UiKit.Row(_footer, 12f);
            footerRow.childForceExpandHeight = true;

            _root.gameObject.SetActive(false);
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

            var title = UiKit.Caption(Content(_header, "title", 44f),
                allPick ? "ALL PICK" : "SNAKE DRAFT", 36f, UiTheme.GoldBright, TextAlignmentOptions.MidlineLeft);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = UiTheme.HeadingSpacing * 1.5f;
            UiFonts.ApplyDisplay(title);

            UiKit.Caption(Content(_header, "subtitle", 24f), Subtitle(), UiTheme.FontBody, UiTheme.TextDim,
                TextAlignmentOptions.MidlineLeft);

            if (allPick)
            {
                string cpus = _cpu.Count > 0 ? " CPU seats pick on their own." : "";
                UiKit.Caption(Content(_header, "hint", 22f),
                    $"Choose a seat (click it or press 1–{_draft.Seats.Count}), then click operators. " +
                    "Click a filled slot to clear it." + cpus,
                    UiTheme.FontSmall, UiTheme.TextOff, TextAlignmentOptions.MidlineLeft);
                return;
            }

            // The pick order, one diamond per pick: done ones dim, the current one large.
            var strip = Content(_header, "strip", 28f);
            var row = UiKit.Row(strip, 6f);
            row.childAlignment = TextAnchor.MiddleLeft;

            for (int i = 0; i < _draft.TotalPicks; i++)
            {
                var colour = UiTheme.Seat(_draft.SeatForPick(i));
                bool current = i == _draft.PickCount;
                bool done = i < _draft.PickCount;

                if (i > 0 && i % _draft.Seats.Count == 0) UiKit.Space(strip, 10f);

                UiKit.Diamond(strip,
                    done ? UiTheme.WithAlpha(colour, 0.3f) : colour,
                    current ? 16f : 11f, current ? 24f : 16f, outline: done);
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
            int columns = Mathf.Max(3, Mathf.CeilToInt(cards / (float)CardRows));
            float cardWidth = (GridWidth - (columns - 1) * CardGap) / columns;

            _gridLayout.cellSize = new Vector2(cardWidth, CardHeight);
            _gridLayout.constraintCount = columns;
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
            HoverRelay.On(button, () => Focus(op));

            var card = (RectTransform)button.transform;

            // Refused cards stay hoverable, so their abilities can still be read.
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = open ? 1f : 0.5f;

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

        /// <summary>What an aura does and to whom, in the detail panel's words.</summary>
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

            var heading = Content(_seatPanel, "heading", 22f);
            UiKit.Column(heading, 0f).childForceExpandHeight = true;
            UiKit.Heading(heading, $"Squads · {_draft.Seats.Count} seats");

            for (int i = 0; i < _draft.Seats.Count; i++) SeatRow(_draft.Seats[i], i);
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

        private void RebuildDetail()
        {
            _detailDirty = false;
            Clear(_detail);

            var op = _focus;
            if (op == null)
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
                // A passive's magnitude is a speed bonus (Kurbyn); Lethe's haste carries none.
                string magnitude = op.PassiveMagnitude != 0.0 ? $" ({op.PassiveMagnitude:+0.0;-0.0} speed)" : "";
                string passiveLabel = op.PassiveName ?? StatusPalette.Label(op.Passive.Value);
                traits.Add($"passive {passiveLabel}{magnitude}");
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