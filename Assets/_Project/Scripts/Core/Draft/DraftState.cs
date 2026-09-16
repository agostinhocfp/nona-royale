// Assets/_Project/Scripts/Core/Draft/DraftState.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Draft
{
    /// <summary>
    /// One pre-match draft: which seat holds which operator in which slot, whose
    /// pick it is, and how long is left (DRAFT.md).
    /// </summary>
    /// <remarks>
    /// <b>Each seat has <see cref="Roster.SquadSize"/> slots.</b> A pick fills
    /// the seat's first empty slot. In ALL PICK a slot can be cleared again
    /// while the clock runs, so a seat's squad can have gaps; the next pick
    /// fills the first gap.
    ///
    /// <b>Distinct within a seat, duplicated freely across seats</b> (GDD §2.2).
    /// Operators are matched by name, so two definitions with the same name
    /// count as the same operator.
    ///
    /// <b>The clock is part of the rules.</b> What happens when time runs out
    /// is a draft rule, not a presentation detail, so it lives here and is
    /// tested here. The view only reports elapsed time through
    /// <see cref="Tick"/>.
    ///
    /// <b>Random picks draw from the draft's own RNG</b>, never the match's
    /// (<see cref="DraftConfig.RandomFor"/>). A refused action returns a
    /// <see cref="DraftRefusal"/>; only misuse throws.
    /// </remarks>
    public sealed class DraftState
    {
        private readonly List<PlayerColor> _seats;
        private readonly List<OperatorDefinition> _pool;
        private readonly OperatorDefinition[][] _slots;
        private readonly bool[][] _randomSlots;
        private readonly IRandom _random;

        private int _pickCount;
        private double _secondsLeft;
        private bool _timeUp;

        // Snake keeps one step of history: the seat index and slot of the last pick.
        private bool _canUndo;
        private int _lastSeatIndex;
        private int _lastSlot;

        public DraftState(
            IReadOnlyList<PlayerColor> seats,
            DraftMode mode,
            IRandom random,
            DraftConfig config = null,
            IReadOnlyList<OperatorDefinition> pool = null)
        {
            if (seats == null) throw new ArgumentNullException(nameof(seats));
            if (seats.Count == 0) throw new ArgumentException("A draft needs at least one seat.", nameof(seats));
            if (random == null) throw new ArgumentNullException(nameof(random));

            _seats = new List<PlayerColor>(seats.Count);
            foreach (var seat in seats)
            {
                if (seat == PlayerColor.None)
                    throw new ArgumentException("A draft seat cannot be None.", nameof(seats));
                if (_seats.Contains(seat))
                    throw new ArgumentException($"{seat} is seated twice.", nameof(seats));
                _seats.Add(seat);
            }

            pool = pool ?? Roster.All;
            _pool = new List<OperatorDefinition>(pool.Count);
            foreach (var op in pool)
            {
                if (op == null) throw new ArgumentException("The pool holds a null operator.", nameof(pool));
                if (IndexByName(_pool, op.Name) >= 0)
                    throw new ArgumentException($"The pool lists {op.Name} twice.", nameof(pool));
                _pool.Add(op);
            }

            if (_pool.Count < Roster.SquadSize)
                throw new ArgumentException(
                    $"The pool holds {_pool.Count} operators; a squad needs {Roster.SquadSize}.", nameof(pool));

            Mode = mode;
            Config = config ?? DraftConfig.Default;
            _random = random;

            _slots = new OperatorDefinition[_seats.Count][];
            _randomSlots = new bool[_seats.Count][];
            for (int i = 0; i < _seats.Count; i++)
            {
                _slots[i] = new OperatorDefinition[Roster.SquadSize];
                _randomSlots[i] = new bool[Roster.SquadSize];
            }

            _secondsLeft = Config.SecondsFor(mode);
        }

        /// <summary>A draft whose random picks draw from the match seed's draft RNG.</summary>
        public static DraftState ForMatch(
            IReadOnlyList<PlayerColor> seats,
            DraftMode mode,
            int matchSeed,
            DraftConfig config = null)
        {
            config = config ?? DraftConfig.Default;
            return new DraftState(seats, mode, config.RandomFor(matchSeed), config);
        }

        // ── Queries ──────────────────────────────────────────────────────

        public DraftMode Mode { get; }

        public DraftConfig Config { get; }

        public IReadOnlyList<PlayerColor> Seats => _seats;

        public IReadOnlyList<OperatorDefinition> Pool => _pool;

        /// <summary>Slots per seat.</summary>
        public int SquadSize => Roster.SquadSize;

        public int TotalPicks => _seats.Count * Roster.SquadSize;

        /// <summary>Filled slots across every seat.</summary>
        public int PickCount => _pickCount;

        /// <summary>Every slot is filled.</summary>
        public bool IsComplete => _pickCount == TotalPicks;

        /// <summary>
        /// ALL PICK only: the shared clock reached zero. The empty slots were
        /// filled at random, and the draft is closed; the view deals now.
        /// </summary>
        public bool IsTimeUp => _timeUp;

        /// <summary>
        /// Seconds left on the running clock: the whole draft in ALL PICK, the
        /// current pick in SNAKE. Zero once the snake draft is complete or the
        /// ALL PICK clock has run out. In ALL PICK it keeps running after every
        /// slot is filled, so slots can still be swapped until START or zero.
        /// </summary>
        public double SecondsLeft => _secondsLeft;

        /// <summary>SNAKE: the seat holding the pick. ALL PICK, or a complete draft: <see cref="PlayerColor.None"/>.</summary>
        public PlayerColor CurrentSeat =>
            Mode == DraftMode.Snake && !IsComplete ? SeatForPick(_pickCount) : PlayerColor.None;

        /// <summary>
        /// SNAKE: the seat that makes pick <paramref name="pickIndex"/>
        /// (zero-based), for the pick-order strip. ALL PICK has no order and
        /// returns <see cref="PlayerColor.None"/>.
        /// </summary>
        public PlayerColor SeatForPick(int pickIndex)
        {
            if (pickIndex < 0 || pickIndex >= TotalPicks)
                throw new ArgumentOutOfRangeException(nameof(pickIndex));

            if (Mode != DraftMode.Snake) return PlayerColor.None;

            int n = _seats.Count;
            int round = pickIndex / n;
            int within = pickIndex % n;

            return round % 2 == 0 ? _seats[within] : _seats[n - 1 - within];
        }

        /// <summary>The operator in a slot, or null if the slot is empty.</summary>
        public OperatorDefinition SlotOf(PlayerColor seat, int slot)
        {
            CheckSlot(slot);
            return _slots[IndexOf(seat)][slot];
        }

        /// <summary>Whether a slot was filled at random (the RANDOM button or a timeout).</summary>
        public bool WasRandom(PlayerColor seat, int slot)
        {
            CheckSlot(slot);
            return _randomSlots[IndexOf(seat)][slot];
        }

        public int FilledCount(PlayerColor seat)
        {
            var slots = _slots[IndexOf(seat)];
            int count = 0;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) count++;
            return count;
        }

        /// <summary>The seat's operators in slot order, without the gaps.</summary>
        public IReadOnlyList<OperatorDefinition> PicksOf(PlayerColor seat)
        {
            var slots = _slots[IndexOf(seat)];
            var picks = new List<OperatorDefinition>(slots.Length);
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) picks.Add(slots[i]);
            return picks;
        }

        /// <summary>The slot the seat's next pick fills, or -1 if the seat is full.</summary>
        public int NextEmptySlot(PlayerColor seat) => FirstEmpty(IndexOf(seat));

        /// <summary>
        /// What the seat could still add: the pool minus its own picks. Empty
        /// when the seat is full. Ignores whose turn it is; ask
        /// <see cref="CanPick"/> for that.
        /// </summary>
        public IReadOnlyList<OperatorDefinition> Available(PlayerColor seat) => AvailableFor(IndexOf(seat));

        /// <summary>Whether <paramref name="seat"/> may pick <paramref name="op"/> now.</summary>
        public DraftRefusal CanPick(PlayerColor seat, OperatorDefinition op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            int index = IndexOf(seat);

            var refusal = SeatRefusal(seat, index);
            if (refusal != DraftRefusal.None) return refusal;

            if (IndexByName(_pool, op.Name) < 0) return DraftRefusal.NotInPool;
            if (IndexByName(_slots[index], op.Name) >= 0) return DraftRefusal.AlreadyInSquad;

            return DraftRefusal.None;
        }

        /// <summary>Whether the seat may make a pick at all now, whatever the operator.</summary>
        public DraftRefusal CanPickAny(PlayerColor seat) => SeatRefusal(seat, IndexOf(seat));

        /// <summary>ALL PICK: whether a slot may be emptied now.</summary>
        public DraftRefusal CanClear(PlayerColor seat, int slot)
        {
            CheckSlot(slot);
            int index = IndexOf(seat);

            if (Mode != DraftMode.AllPick) return DraftRefusal.WrongMode;
            if (_timeUp) return DraftRefusal.Complete;
            if (_slots[index][slot] == null) return DraftRefusal.NothingToRevert;

            return DraftRefusal.None;
        }

        /// <summary>SNAKE: whether the last pick can be taken back.</summary>
        public DraftRefusal CanUndo()
        {
            if (Mode != DraftMode.Snake) return DraftRefusal.WrongMode;
            if (!_canUndo) return DraftRefusal.NothingToRevert;
            return DraftRefusal.None;
        }

        // ── Actions ──────────────────────────────────────────────────────

        /// <summary>Puts <paramref name="op"/> in the seat's first empty slot.</summary>
        public DraftRefusal Pick(PlayerColor seat, OperatorDefinition op)
        {
            var refusal = CanPick(seat, op);
            if (refusal != DraftRefusal.None) return refusal;

            // Store the pool's own instance, so a squad never carries a stray copy.
            Place(IndexOf(seat), _pool[IndexByName(_pool, op.Name)], random: false);
            return DraftRefusal.None;
        }

        /// <summary>Fills the seat's first empty slot with a uniform draw from <see cref="Available"/>.</summary>
        public DraftRefusal RandomPick(PlayerColor seat)
        {
            int index = IndexOf(seat);

            var refusal = SeatRefusal(seat, index);
            if (refusal != DraftRefusal.None) return refusal;

            PlaceRandom(index);
            return DraftRefusal.None;
        }

        /// <summary>
        /// Fills every empty slot at random: in seat order for ALL PICK, in pick
        /// order for SNAKE. Returns how many picks it made.
        /// </summary>
        public int FillRandom()
        {
            int made = 0;

            if (Mode == DraftMode.Snake)
            {
                while (!IsComplete)
                {
                    PlaceRandom(IndexOf(CurrentSeat));
                    made++;
                }

                return made;
            }

            for (int i = 0; i < _seats.Count; i++)
            {
                while (FirstEmpty(i) >= 0)
                {
                    PlaceRandom(i);
                    made++;
                }
            }

            return made;
        }

        /// <summary>ALL PICK: empties a slot while the clock still runs.</summary>
        public DraftRefusal Clear(PlayerColor seat, int slot)
        {
            var refusal = CanClear(seat, slot);
            if (refusal != DraftRefusal.None) return refusal;

            int index = IndexOf(seat);
            _slots[index][slot] = null;
            _randomSlots[index][slot] = false;
            _pickCount--;

            return DraftRefusal.None;
        }

        /// <summary>SNAKE: takes back the last pick. That pick's clock starts over.</summary>
        public DraftRefusal Undo()
        {
            var refusal = CanUndo();
            if (refusal != DraftRefusal.None) return refusal;

            _slots[_lastSeatIndex][_lastSlot] = null;
            _randomSlots[_lastSeatIndex][_lastSlot] = false;
            _pickCount--;
            _canUndo = false;
            _secondsLeft = Config.SnakePickSeconds;

            return DraftRefusal.None;
        }

        /// <summary>
        /// Runs the clock forward by <paramref name="elapsedSeconds"/> and
        /// applies any timeout. Returns how many picks the timeout made.
        /// </summary>
        /// <remarks>
        /// ALL PICK: at zero, every empty slot is filled and the draft closes.
        /// SNAKE: each time a pick's clock reaches zero, that pick is made at
        /// random and the next pick's clock starts; a long tick can make
        /// several. A complete snake draft has no clock.
        /// </remarks>
        public int Tick(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || elapsedSeconds < 0.0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

            if (Mode == DraftMode.AllPick)
            {
                if (_timeUp) return 0;

                if (elapsedSeconds < _secondsLeft)
                {
                    _secondsLeft -= elapsedSeconds;
                    return 0;
                }

                _secondsLeft = 0.0;
                _timeUp = true;
                return FillRandom();
            }

            int made = 0;
            while (!IsComplete)
            {
                if (elapsedSeconds < _secondsLeft)
                {
                    _secondsLeft -= elapsedSeconds;
                    break;
                }

                elapsedSeconds -= _secondsLeft;
                PlaceRandom(IndexOf(CurrentSeat));
                made++;
            }

            return made;
        }

        /// <summary>The finished squads, keyed by seat, for <c>MatchFactory.Create</c>.</summary>
        public IReadOnlyDictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> Squads()
        {
            if (!IsComplete)
                throw new InvalidOperationException(
                    $"The draft has {_pickCount} of {TotalPicks} picks; squads exist only once it is complete.");

            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>(_seats.Count);
            for (int i = 0; i < _seats.Count; i++)
                squads[_seats[i]] = new List<OperatorDefinition>(_slots[i]);
            return squads;
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private DraftRefusal SeatRefusal(PlayerColor seat, int index)
        {
            if (IsComplete || _timeUp) return DraftRefusal.Complete;
            if (Mode == DraftMode.Snake && seat != CurrentSeat) return DraftRefusal.NotYourTurn;
            if (FirstEmpty(index) < 0) return DraftRefusal.SquadFull;
            return DraftRefusal.None;
        }

        private void PlaceRandom(int index)
        {
            var available = AvailableFor(index);
            Place(index, available[_random.NextInt(0, available.Count)], random: true);
        }

        private void Place(int index, OperatorDefinition op, bool random)
        {
            int slot = FirstEmpty(index);

            _slots[index][slot] = op;
            _randomSlots[index][slot] = random;
            _pickCount++;

            if (Mode != DraftMode.Snake) return;

            _canUndo = true;
            _lastSeatIndex = index;
            _lastSlot = slot;
            _secondsLeft = IsComplete ? 0.0 : Config.SnakePickSeconds;
        }

        private List<OperatorDefinition> AvailableFor(int index)
        {
            var slots = _slots[index];
            var available = new List<OperatorDefinition>(_pool.Count);

            if (FirstEmpty(index) < 0) return available;

            foreach (var op in _pool)
                if (IndexByName(slots, op.Name) < 0) available.Add(op);

            return available;
        }

        private int FirstEmpty(int index)
        {
            var slots = _slots[index];
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == null) return i;
            return -1;
        }

        private int IndexOf(PlayerColor seat)
        {
            int index = _seats.IndexOf(seat);
            if (index < 0) throw new ArgumentException($"{seat} is not in this draft.", nameof(seat));
            return index;
        }

        private static void CheckSlot(int slot)
        {
            if (slot < 0 || slot >= Roster.SquadSize) throw new ArgumentOutOfRangeException(nameof(slot));
        }

        private static int IndexByName(IReadOnlyList<OperatorDefinition> list, string name)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && string.Equals(list[i].Name, name, StringComparison.Ordinal)) return i;
            return -1;
        }
    }
}