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
    /// <b>⚠ The cell arithmetic below is stale, and these tests are red.</b>
    /// It was written against the 48-cell circuit, where the start offset was 12
    /// and a Blue operator's cell was its progress plus 12. The board is 52 as of
    /// ADR-0002 Amendment 6 and the offset is <b>13</b>, so every hardcoded
    /// progress here lands one cell further round than its comment claims.
    ///
    /// Concretely, in <see cref="ASwap_ExchangesBothOperatorsCells"/>: Mimi at
    /// progress 18 is track 18, but the enemy at progress 10 is track <i>23</i>,
    /// not 22. The swap shift is therefore 5 rather than 4, Mimi lands on 23, and
    /// the assertion expects 22. The same error runs through every case that
    /// names a cell.
    ///
    /// <b>The fix is not new numbers.</b> <c>AbilityResolverTests</c> hit this
    /// exact drift and answered it with a <c>ProgressAtTrack(owner, track)</c>
    /// helper, so its fixtures are stated in <i>track cells</i> and converted per
    /// owner — which survives the next board change. This fixture should be
    /// rewritten the same way rather than re-tuned against 52.
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
            _cellEffects = new DeferredCellEffects(_clock, _targeting, _damage);
            _abilities = new AbilityResolver(_map, _clock, _energy, _statuses, _targeting, _damage, _cellEffects);

            _mimi = Deployed(1, "Mimi", PlayerColor.Red, Mimi.MaxHealth, 18);    // track 18
            _ally = Deployed(2, "Syla", PlayerColor.Red, 6, 22);                 // track 22
            _enemy = Deployed(3, "Enemy", PlayerColor.Blue, 6, 10);              // track 22

            _red = new PlayerState(PlayerColor.Red, new[] { _mimi, _ally });
            _board = new List<OperatorState> { _mimi, _ally, _enemy };

            _clock.BeginTurnFor(PlayerColor.Red);
            Fund();
        }

        private static OperatorState Deployed(int id, string name, PlayerColor owner, int hp, int progress)
        {
            var op = new OperatorState(id, name, owner, hp, 1.0);
            op.MoveTo(progress);
            return op;
        }

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
            // Mimi on track 18, the enemy on track 22 — four cells apart, well
            // inside Translocation's range of 6.
            var result = Use(Mimi.Translocation, _enemy);

            Assert.That(result.Approved, Is.True);
            Assert.That(CellOf(_mimi), Is.EqualTo(22));
            Assert.That(CellOf(_enemy), Is.EqualTo(18));
        }

        [Test]
        public void ASwap_ShiftsEachOperatorByTheSameCellDistanceOnItsOwnPath()
        {
            // The point of the conversion: the two operators enter the circuit
            // at different cells, so identical cells mean different progress.
            // Four cells forward for Mimi is four cells back for the enemy.
            Use(Mimi.Translocation, _enemy);

            Assert.That(_mimi.Progress, Is.EqualTo(22), "18 + 4");
            Assert.That(_enemy.Progress, Is.EqualTo(6), "10 - 4");
        }

        [Test]
        public void ASwap_WorksOnAnAlly()
        {
            // Translocation is audience-Any: a rescue and an abduction are the
            // same effect, not two branches.
            var result = Use(Mimi.Translocation, _ally);

            Assert.That(result.Approved, Is.True);
            Assert.That(CellOf(_mimi), Is.EqualTo(22));
            Assert.That(CellOf(_ally), Is.EqualTo(18));
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
            var bystander = Deployed(4, "Enemy2", PlayerColor.Blue, 6, 10);   // track 22, same cell
            _board.Add(bystander);

            Use(Mimi.Translocation, _enemy);

            Assert.That(bystander.Health, Is.EqualTo(6));
            Assert.That(_mimi.Health, Is.EqualTo(Mimi.MaxHealth));
            Assert.That(CellOf(bystander), Is.EqualTo(22), "the bystander was not moved");
        }

        // ── Leaving the track ────────────────────────────────────────────

        [Test]
        public void ASwapThatWouldGoBehindAStartCell_IsRefused()
        {
            // The enemy sits on its own start. Swapping it two cells backwards
            // has nowhere to put it: its path does not extend behind track 12.
            // Clamping the way a pull does would send it to its start instead of
            // Mimi's cell, which is a total progress wipe rather than a swap.
            _mimi.MoveTo(10);            // track 10
            _enemy.MoveTo(0);            // track 12, its start

            var result = Use(Mimi.Translocation, _enemy);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.SwapWouldLeaveTheTrack));
            Assert.That(_mimi.Progress, Is.EqualTo(10), "neither operator moved");
            Assert.That(_enemy.Progress, Is.EqualTo(0));
        }

        [Test]
        public void ASwapThatWouldEnterAHomeColumn_IsRefused()
        {
            // Mimi is four cells from the end of the circuit. Swapping her
            // forward would land her at progress 50 — inside her own home
            // column, skipping the rest of the loop for three energy.
            _mimi.MoveTo(46);            // track 46
            _enemy.MoveTo(38);           // track 2, four cells on from 46

            var result = Use(Mimi.Translocation, _enemy);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.SwapWouldLeaveTheTrack));
            Assert.That(_mimi.Progress, Is.EqualTo(46));
            Assert.That(_enemy.Progress, Is.EqualTo(38));
        }

        [Test]
        public void ARefusedSwap_CostsNoEnergyAndNoCooldown()
        {
            // The legality check runs before payment, and that ordering is the
            // only reason a refused swap is free. Nothing else proves it.
            _mimi.MoveTo(10);
            _enemy.MoveTo(0);
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
            _enemy.MoveTo(24);           // track 36, eighteen cells away

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
            var second = Deployed(4, "Enemy2", PlayerColor.Blue, 6, 11);   // track 23
            _board.Add(second);
            _mimi.MoveTo(20);                                              // within range 3 of track 22

            var result = Use(Mimi.CryoPulse, _enemy);

            Assert.That(result.Approved, Is.True);
            Assert.That(_enemy.Health, Is.EqualTo(4), "the primary target is inside its own field");
            Assert.That(second.Health, Is.EqualTo(4));
        }

        [Test]
        public void CryoPulse_NeverHitsItsOwnSide()
        {
            _mimi.MoveTo(20);
            _ally.MoveTo(21);            // track 21, inside the window

            Use(Mimi.CryoPulse, _enemy);

            Assert.That(_ally.Health, Is.EqualTo(6));
            Assert.That(_mimi.Health, Is.EqualTo(Mimi.MaxHealth));
        }

        [Test]
        public void CryoPulse_BleedsAndSlowsEveryEnemyItHits()
        {
            _mimi.MoveTo(20);

            Use(Mimi.CryoPulse, _enemy);

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.IsBleeding(_enemy), Is.True);
            Assert.That(_statuses.SpeedModifier(_enemy), Is.EqualTo(-CombatConfig.Default.SlowSpeedPenalty));
        }
    }
}