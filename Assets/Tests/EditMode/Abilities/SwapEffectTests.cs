// Assets/Tests/EditMode/Abilities/SwapEffectTests.cs
using System.Linq;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Abilities
{
    /// <summary>
    /// Translocation, and the placement rules a swap introduced (COMBAT_SYSTEMS
    /// §7.4). Its own fixture rather than an addition to
    /// <c>AbilityResolverTests</c>: every case here turns on board geometry, and
    /// the setup that makes a wrap reachable is nothing like the setup the alpha
    /// roster's tests want.
    /// </summary>
    /// <remarks>
    /// <b>Fixtures are stated in track cells and converted per owner</b>, with
    /// the same <c>ProgressAtTrack</c> helper <c>AbilityResolverTests</c> uses.
    /// This fixture was written against the 48-cell circuit with literal
    /// progress values (a Blue operator's cell was its progress plus 12) and
    /// went red when the board became 52/6 (ADR-0002 Amendment 6). Stated as
    /// cells, it survives the next board change.
    ///
    /// Red starts at track 0, so a Red operator's progress is its track cell.
    ///
    /// Reuses <c>FakeClock</c> from <c>AbilityResolverTests</c>, which lives in
    /// this namespace.
    /// </remarks>
    [TestFixture]
    public class SwapEffectTests
    {
        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;
        private EnergyLedger _energy;
        private DamagePipeline _damage;
        private DeferredCellEffects _cellEffects;
        private AbilityResolver _abilities;

        private OperatorState _mimi;
        private OperatorState _ally;
        private OperatorState _enemy;
        private PlayerState _red;
        private List<OperatorState> _board;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _clock = new FakeClock();
            _statuses = new StatusRegistry(_clock, CombatConfig.Default);
            _targeting = new TargetingRules(_map, _statuses);
            _energy = new EnergyLedger(EnergyConfig.Default);
            _damage = new DamagePipeline(_statuses, new SeededRandom(1));
            _cellEffects = new DeferredCellEffects(_clock, _targeting, _damage, _statuses);
            _abilities = new AbilityResolver(_map, _clock, _energy, _statuses, _targeting, _damage, _cellEffects);

            _mimi = AtTrack(1, "Mimi", PlayerColor.Red, Mimi.MaxHealth, MimiCell);
            _ally = AtTrack(2, "Syla", PlayerColor.Red, 6, TargetCell);
            _enemy = AtTrack(3, "Enemy", PlayerColor.Blue, 6, TargetCell);

            _red = new PlayerState(PlayerColor.Red, new[] { _mimi, _ally });
            _board = new List<OperatorState> { _mimi, _ally, _enemy };

            _clock.BeginTurnFor(PlayerColor.Red);
            Fund();
        }

        /// <summary>Mimi's cell in most tests.</summary>
        private const int MimiCell = 18;

        /// <summary>The swap target's cell: four on from Mimi, inside Translocation's range.</summary>
        private const int TargetCell = 22;

        private int Circuit => _map.Profile.CircuitLength;
        private int TrackLength => _map.Profile.TrackLength;
        private int BlueStart => _map.StartTrackIndex(PlayerColor.Blue);

        private OperatorState AtTrack(int id, string name, PlayerColor owner, int hp, int track)
        {
            var op = new OperatorState(id, name, owner, hp, 1.0);
            op.MoveTo(ProgressAtTrack(owner, track));
            return op;
        }

        private int ProgressAtTrack(PlayerColor owner, int track) =>
            ((track - _map.StartTrackIndex(owner)) % Circuit + Circuit) % Circuit;

        private int Wrap(int track) => (track % Circuit + Circuit) % Circuit;

        /// <summary>Fills the pool to the 12 cap without going through the dice.</summary>
        private void Fund()
        {
            _red.BeginTurn();
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6
            _red.BeginTurn();
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6, capped at 12
        }

        private AbilityResolution Use(AbilityDefinition ability, OperatorState target = null) =>
            _abilities.Use(_mimi, ability, target, _red, _board);

        private int CellOf(OperatorState op) => _map.CellAt(op.Owner, op.Progress).Index;

        // ── The swap itself ──────────────────────────────────────────────

        [Test]
        public void ASwap_ExchangesBothOperatorsCells()
        {
            // Mimi and the enemy four cells apart, inside Translocation's range.
            var result = Use(Mimi.Translocation, _enemy);

            Assert.That(result.Approved, Is.True);
            Assert.That(CellOf(_mimi), Is.EqualTo(TargetCell));
            Assert.That(CellOf(_enemy), Is.EqualTo(MimiCell));
        }

        [Test]
        public void ASwap_ReachesNineCells() => SwapAcross(9, approved: true);

        [Test]
        public void ASwap_DoesNotReachTen() => SwapAcross(10, approved: false);

        private void SwapAcross(int gap, bool approved)
        {
            // Range 6 → 9 (designer, 2026-09-24): the reach is her mobility.
            Assert.That(Mimi.Translocation.Range, Is.EqualTo(9));
            _enemy.MoveTo(ProgressAtTrack(PlayerColor.Blue, MimiCell + gap));

            var result = Use(Mimi.Translocation, _enemy);

            Assert.That(result.Approved, Is.EqualTo(approved));
            Assert.That(CellOf(_mimi), Is.EqualTo(approved ? MimiCell + gap : MimiCell));
        }

        [Test]
        public void ASwap_ShiftsEachOperatorByTheSameCellDistanceOnItsOwnPath()
        {
            // The point of the conversion: the two operators enter the circuit
            // at different cells, so identical cells mean different progress.
            // Four cells forward for Mimi is four cells back for the enemy.
            int shift = TargetCell - MimiCell;
            int mimiBefore = _mimi.Progress;
            int enemyBefore = _enemy.Progress;

            Use(Mimi.Translocation, _enemy);

            Assert.That(_mimi.Progress, Is.EqualTo(mimiBefore + shift), "four on");
            Assert.That(_enemy.Progress, Is.EqualTo(enemyBefore - shift), "four back");
            Assert.That(mimiBefore - enemyBefore, Is.Not.EqualTo(0),
                "precondition: the two enter the circuit at different cells");
        }

        [Test]
        public void ASwap_WorksOnAnAlly()
        {
            // Translocation is audience-Any: a rescue and an abduction are the
            // same effect, not two branches.
            var result = Use(Mimi.Translocation, _ally);

            Assert.That(result.Approved, Is.True);
            Assert.That(CellOf(_mimi), Is.EqualTo(TargetCell));
            Assert.That(CellOf(_ally), Is.EqualTo(MimiCell));
        }

        [Test]
        public void ASwap_ChangesNothingButPosition()
        {
            _enemy.SetHealth(4);
            int mimiHealth = _mimi.Health;

            Use(Mimi.Translocation, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(4));
            Assert.That(_mimi.Health, Is.EqualTo(mimiHealth));
        }

        [Test]
        public void ASwappedOperator_DoesNotCollideOnArrival()
        {
            // Placement never collides, even onto an occupied cell (§7.4).
            // A second enemy shares the destination; nothing should resolve.
            var bystander = AtTrack(4, "Enemy2", PlayerColor.Blue, 6, TargetCell);   // same cell
            _board.Add(bystander);

            var result = Use(Mimi.Translocation, _enemy);

            Assert.That(result.Approved, Is.True, "precondition: the swap happened");
            Assert.That(bystander.Health, Is.EqualTo(6));
            Assert.That(_mimi.Health, Is.EqualTo(Mimi.MaxHealth));
            Assert.That(CellOf(bystander), Is.EqualTo(TargetCell), "the bystander was not moved");
        }

        // ── Leaving the track ────────────────────────────────────────────

        [Test]
        public void ASwapThatWouldGoBehindAStartCell_IsRefused()
        {
            // The enemy is one cell past its own start (not on it: a start cell
            // refuses enemy single-targeting, §4.4 amended), and Mimi stands
            // three cells behind that start. Swapping sends the enemy four cells
            // backwards, where its path does not exist. Clamping the way a pull
            // does would send it to its start instead of Mimi's cell, which is a
            // total progress wipe rather than a swap.
            int mimiCell = Wrap(BlueStart - 3);
            _mimi.MoveTo(ProgressAtTrack(PlayerColor.Red, mimiCell));
            _enemy.MoveTo(1);

            var result = Use(Mimi.Translocation, _enemy);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.SwapWouldLeaveTheTrack));
            Assert.That(CellOf(_mimi), Is.EqualTo(mimiCell), "neither operator moved");
            Assert.That(_enemy.Progress, Is.EqualTo(1));
        }

        [Test]
        public void ASwapThatWouldEnterAHomeColumn_IsRefused()
        {
            // Mimi is two cells from the end of her circuit, the enemy three
            // cells on from her, across the seam at track 0. Swapping her
            // forward would land her one cell inside her own home column,
            // skipping the rest of the loop for three energy.
            int mimiProgress = TrackLength - 2;
            int enemyCell = Wrap(mimiProgress + 3);
            _mimi.MoveTo(mimiProgress);
            _enemy.MoveTo(ProgressAtTrack(PlayerColor.Blue, enemyCell));
            int enemyProgress = _enemy.Progress;

            Assert.That(_map.IsSafe(CellRef.Track(enemyCell)), Is.False,
                "precondition: an ordinary cell, so targeting does not refuse first");

            var result = Use(Mimi.Translocation, _enemy);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.SwapWouldLeaveTheTrack));
            Assert.That(_mimi.Progress, Is.EqualTo(mimiProgress));
            Assert.That(_enemy.Progress, Is.EqualTo(enemyProgress));
        }

        [Test]
        public void ARefusedSwap_CostsNoEnergyAndNoCooldown()
        {
            // The legality check runs before payment, and that ordering is the
            // only reason a refused swap is free. Nothing else proves it.
            _mimi.MoveTo(ProgressAtTrack(PlayerColor.Red, Wrap(BlueStart - 3)));
            _enemy.MoveTo(1);
            int before = _red.Energy;

            var result = Use(Mimi.Translocation, _enemy);

            Assert.That(result.Approved, Is.False);
            Assert.That(_red.Energy, Is.EqualTo(before));
            Assert.That(_abilities.IsReady(_mimi, Mimi.Translocation), Is.True);
        }

        [Test]
        public void ASwapOutOfRange_IsRefusedByTargetingFirst()
        {
            // Range 6 is the longest in the game, and it still ends somewhere.
            _enemy.MoveTo(ProgressAtTrack(PlayerColor.Blue, MimiCell + 18));   // eighteen cells away

            var result = Use(Mimi.Translocation, _enemy);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.OutOfRange));
        }

        // ── Cryo-Pulse's inclusive area ──────────────────────────────────

        [Test]
        public void CryoPulse_HitsThePrimaryTargetAsWellAsTheRingAroundIt()
        {
            // The distinction from Miracle Pull's splash: the target takes no
            // separate direct hit here, so excluding it would make the operator
            // the player aimed at the one enemy the field misses.
            var second = AtTrack(4, "Enemy2", PlayerColor.Blue, 6, TargetCell + 1);
            _board.Add(second);
            _mimi.MoveTo(TargetCell - 2);                                  // within range 3 of the target

            var result = Use(Mimi.CryoPulse, _enemy);

            Assert.That(result.Approved, Is.True);
            Assert.That(_enemy.Health, Is.EqualTo(4), "the primary target is inside its own field");
            Assert.That(second.Health, Is.EqualTo(4));
        }

        [Test]
        public void CryoPulse_NeverHitsItsOwnSide()
        {
            _mimi.MoveTo(TargetCell - 2);
            _ally.MoveTo(TargetCell - 1);    // inside the window

            Use(Mimi.CryoPulse, _enemy);

            Assert.That(_ally.Health, Is.EqualTo(6));
            Assert.That(_mimi.Health, Is.EqualTo(Mimi.MaxHealth));
        }

        [Test]
        public void CryoPulse_BleedsAndSlowsEveryEnemyItHits()
        {
            _mimi.MoveTo(TargetCell - 2);

            Use(Mimi.CryoPulse, _enemy);

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.IsBleeding(_enemy), Is.True);
            Assert.That(_statuses.SpeedModifier(_enemy), Is.EqualTo(-CombatConfig.Default.SlowSpeedPenalty));
        }
    }
}
