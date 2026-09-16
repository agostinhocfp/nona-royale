// Assets/Tests/EditMode/Turn/TurnStateMachineTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Turn
{
    /// <summary>Deals a scripted sequence of dice faces, so a test can state the roll it needs.</summary>
    internal sealed class ScriptedDice : IRandom
    {
        private readonly Queue<int> _faces;
        private readonly int _fallback;

        public ScriptedDice(int fallback, params int[] faces)
        {
            _fallback = fallback;
            _faces = new Queue<int>(faces);
        }

        public int NextInt(int min, int max) => _faces.Count > 0 ? _faces.Dequeue() : _fallback;
        public double NextDouble() => 0.99;   // never evades
    }

    /// <summary>Always returns the same probability, so evasion is a decision rather than a roll.</summary>
    internal sealed class FixedRandom : IRandom
    {
        private readonly double _value;
        public FixedRandom(double value) { _value = value; }
        public int NextInt(int min, int max) => min;
        public double NextDouble() => _value;
    }

    [TestFixture]
    public class TurnStateMachineTests
    {
        private PathMap _map;
        private GameConfig _config;
        private CombatConfig _combat;
        private MatchClock _clock;
        private StatusRegistry _statuses;
        private EnergyLedger _energy;
        private DamagePipeline _damage;
        private TargetingRules _targeting;
        private AbilityResolver _abilities;
        private NeutralizeRules _neutralize;
        private WinConditions _win;
        private DeferredCellEffects _cellEffects;

        private PlayerState _red;
        private PlayerState _blue;
        private List<PlayerState> _players;
        private List<OperatorState> _operators;

        private TurnStateMachine Machine(IRandom dice)
        {
            return new TurnStateMachine(
                _players, _clock, _config, dice, _energy, _statuses, _damage, _neutralize, _win, _cellEffects);
        }

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _config = GameConfig.Default;
            _combat = CombatConfig.Default;

            // Speeds match ADR-0002 Amendment 4. Nothing in this fixture
            // computes movement, but stale numbers in a fixture get copied into
            // the next one that does.
            _red = new PlayerState(PlayerColor.Red, new[]
            {
                new OperatorState(1, "Bouncer", PlayerColor.Red, 12, 1.0),
                new OperatorState(2, "Syla", PlayerColor.Red, 6, 1.5)
            });
            _blue = new PlayerState(PlayerColor.Blue, new[]
            {
                new OperatorState(3, "Kurbyn", PlayerColor.Blue, 6, 1.0)
            });
            _players = new List<PlayerState> { _red, _blue };

            // NeutralizeRules needs the whole roster to pay a mark out to the
            // marker's squad, so it is assembled before the service is built.
            _operators = new List<OperatorState>();
            foreach (var player in _players) _operators.AddRange(player.Operators);

            _clock = new MatchClock(_players);
            _statuses = new StatusRegistry(_clock, _combat);
            _energy = new EnergyLedger(EnergyConfig.Default);
            _damage = new DamagePipeline(_statuses, new SeededRandom(1));
            _targeting = new TargetingRules(_map, _statuses);
            _cellEffects = new DeferredCellEffects(_clock, _targeting, _damage, _statuses);
            _abilities = new AbilityResolver(_map, _clock, _energy, _statuses, _targeting, _damage, _cellEffects);

            _neutralize = new NeutralizeRules(_statuses, _abilities, _energy, _operators, _players, _combat);
            _win = new WinConditions(_map);
        }

        /// <summary>Red takes a turn, then Blue begins one — the shortest path to Blue's upkeep.</summary>
        private UpkeepReport RunToBlueUpkeep(TurnStateMachine machine)
        {
            machine.Roll();
            machine.EndTurn();
            return machine.BeginTurn();
        }

        // ── Phase order ──────────────────────────────────────────────────

        [Test]
        public void AMatchStarts_BetweenTurns()
        {
            var machine = Machine(new SeededRandom(1));

            Assert.That(machine.Phase, Is.EqualTo(TurnPhase.BetweenTurns));
            Assert.That(machine.CurrentPlayer, Is.Null);
        }

        [Test]
        public void BeginTurn_RunsUpkeepThenAwaitsARoll()
        {
            var machine = Machine(new SeededRandom(1));

            machine.BeginTurn();

            Assert.That(machine.Phase, Is.EqualTo(TurnPhase.AwaitingRoll));
            Assert.That(machine.CurrentPlayer, Is.EqualTo(_red));
        }

        [Test]
        public void RollingBeforeUpkeep_IsRefused()
        {
            // An illegal sequence is a bug in the caller and should be loud.
            var machine = Machine(new SeededRandom(1));

            Assert.Throws<InvalidOperationException>(() => machine.Roll());
        }

        [Test]
        public void BeginningATurnMidTurn_IsRefused()
        {
            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();

            Assert.Throws<InvalidOperationException>(() => machine.BeginTurn());
        }

        [Test]
        public void TurnsRotateThroughTheSeatsInOrder()
        {
            var machine = Machine(new SeededRandom(1));

            machine.BeginTurn();
            Assert.That(machine.CurrentPlayer.Color, Is.EqualTo(PlayerColor.Red));
            machine.Roll();
            machine.EndTurn();

            machine.BeginTurn();
            Assert.That(machine.CurrentPlayer.Color, Is.EqualTo(PlayerColor.Blue));
            machine.Roll();
            machine.EndTurn();

            machine.BeginTurn();
            Assert.That(machine.CurrentPlayer.Color, Is.EqualTo(PlayerColor.Red), "back round");
        }

        [Test]
        public void EachTurn_AdvancesOnlyThatSeatsCounter()
        {
            var machine = Machine(new SeededRandom(1));

            machine.BeginTurn();
            machine.Roll();
            machine.EndTurn();

            Assert.That(_red.TurnIndex, Is.EqualTo(1));
            Assert.That(_blue.TurnIndex, Is.EqualTo(0));
        }

        // ── Rolling and doubles ──────────────────────────────────────────

        [Test]
        public void TheFirstRoll_GrantsEnergy()
        {
            var machine = Machine(new ScriptedDice(3, 5, 4));   // total 9

            machine.BeginTurn();
            var report = machine.Roll();

            Assert.That(report.Grant.WasGranted, Is.True);
            Assert.That(report.Grant.Earned, Is.EqualTo(4));
            Assert.That(_red.Energy, Is.EqualTo(4));
        }

        [Test]
        public void DoublesGrantAnotherRoll()
        {
            var machine = Machine(new ScriptedDice(3, 4, 4));

            machine.BeginTurn();
            var report = machine.Roll();

            Assert.That(report.Roll.IsDouble, Is.True);
            Assert.That(report.GrantsAnotherRoll, Is.True);
            Assert.That(machine.CanRollAgain, Is.True);
        }

        [Test]
        public void DoublesReroll_GrantsNoAdditionalEnergy()
        {
            var machine = Machine(new ScriptedDice(3, 4, 4, 6, 6));

            machine.BeginTurn();
            machine.Roll();                      // 4,4 -> 4 energy
            var second = machine.Roll();         // 6,6 -> no grant

            Assert.That(second.Grant.WasGranted, Is.False);
            Assert.That(_red.Energy, Is.EqualTo(4));
        }

        [Test]
        public void EnergyIsGrantedAgain_OnTheOwnersNextTurn()
        {
            // The once-per-turn rule is a mutable flag on PlayerState, not a
            // value derived from the turn index. A missed reset would silently
            // grant each player energy exactly once per match — no exception,
            // matches still complete, everyone just stops casting after turn one.
            var machine = Machine(new ScriptedDice(3, 5, 4, 2, 3, 5, 4));

            machine.BeginTurn();            // Red
            machine.Roll();                 // 9 -> 4
            machine.EndTurn();

            machine.BeginTurn();            // Blue
            machine.Roll();                 // 5 -> 2, and not a double
            machine.EndTurn();

            machine.BeginTurn();            // Red again
            var report = machine.Roll();    // 9 -> 4 more

            Assert.That(report.Grant.WasGranted, Is.True);
            Assert.That(_red.Energy, Is.EqualTo(8));
        }

        [Test]
        public void RollingAgainWithoutDoubles_IsRefused()
        {
            var machine = Machine(new ScriptedDice(3, 5, 4));

            machine.BeginTurn();
            machine.Roll();

            Assert.That(machine.CanRollAgain, Is.False);
            Assert.Throws<InvalidOperationException>(() => machine.Roll());
        }

        [Test]
        public void TheRollBudget_BoundsAHotStreak()
        {
            // MaxRollsPerTurn is the initial roll plus two doubles. Without the
            // cap, a run of doubles could go on indefinitely.
            var machine = Machine(new ScriptedDice(4));   // every roll is 4,4

            machine.BeginTurn();
            for (int i = 0; i < _config.MaxRollsPerTurn; i++) machine.Roll();

            Assert.That(machine.RollsRemaining, Is.EqualTo(0));
            Assert.That(machine.CanRollAgain, Is.False);
            Assert.Throws<InvalidOperationException>(() => machine.Roll());
        }

        // ── Upkeep: bleed ────────────────────────────────────────────────

        [Test]
        public void BleedTicks_AtTheBleedingOwnersUpkeep()
        {
            var syla = _red.Operators.First(o => o.Name == "Syla");
            syla.MoveTo(5);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();                 // Red turn 1
            _statuses.Apply(syla, StatusKind.Bleed, duration: 3, stacks: 2);
            machine.Roll();
            machine.EndTurn();

            machine.BeginTurn();                 // Blue
            machine.Roll();
            machine.EndTurn();

            var upkeep = machine.BeginTurn();    // Red again — bleed resolves here

            Assert.That(upkeep.BleedTicks.Count, Is.EqualTo(1));
            Assert.That(syla.Health, Is.EqualTo(4), "two stacks, one damage each");
        }

        [Test]
        public void BleedCanNeutralize_BeforeTargetActs()
        {
            var syla = _red.Operators.First(o => o.Name == "Syla");
            syla.MoveTo(5);
            syla.SetHealth(1);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            _statuses.Apply(syla, StatusKind.Bleed, duration: 3);
            machine.Roll();
            machine.EndTurn();

            machine.BeginTurn();
            machine.Roll();
            machine.EndTurn();

            var upkeep = machine.BeginTurn();

            Assert.That(upkeep.Neutralized.Count, Is.EqualTo(1));
            Assert.That(syla.IsInYard, Is.True);
            Assert.That(syla.Health, Is.EqualTo(6), "restored on neutralize");
        }

        // ── Upkeep: marks ────────────────────────────────────────────────

        [Test]
        public void MarkTicks_AtTheMarkedOwnersUpkeep()
        {
            var syla = _red.Operators.First(o => o.Name == "Syla");
            var kurbyn = _blue.Operators.First();
            kurbyn.MoveTo(5);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();                 // Red turn 1
            _statuses.Apply(kurbyn, StatusKind.Mark, duration: 2, sourceOperatorId: syla.Id);

            var upkeep = RunToBlueUpkeep(machine);

            Assert.That(kurbyn.Health, Is.EqualTo(6 - _combat.MarkDamagePerTurn));
            Assert.That(upkeep.BleedTicks.Count, Is.EqualTo(1), "the mark tick is reported as upkeep damage");
        }

        [Test]
        public void MarkStack_SurvivesTicking()
        {
            // The whole distinction from bleed: a stack ticks and is spent, a
            // mark ticks and remains (§5.7).
            var syla = _red.Operators.First(o => o.Name == "Syla");
            var kurbyn = _blue.Operators.First();
            kurbyn.MoveTo(5);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            _statuses.Apply(kurbyn, StatusKind.Mark, duration: 2, sourceOperatorId: syla.Id);

            RunToBlueUpkeep(machine);

            Assert.That(_statuses.MarkedBy(kurbyn), Is.EqualTo(syla.Id));
        }

        [Test]
        public void MarkRecordsItsSourceOperator()
        {
            var syla = _red.Operators.First(o => o.Name == "Syla");
            var kurbyn = _blue.Operators.First();

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            _statuses.Apply(kurbyn, StatusKind.Mark, duration: 2, sourceOperatorId: syla.Id);
            machine.Roll();
            machine.EndTurn();
            machine.BeginTurn();                 // Blue — the mark takes hold

            Assert.That(_statuses.MarkedBy(kurbyn), Is.EqualTo(syla.Id));
        }

        [Test]
        public void MarkDamage_IsAtomic_AndIgnoresEvasion()
        {
            var syla = _red.Operators.First(o => o.Name == "Syla");
            var kurbyn = _blue.Operators.First();
            kurbyn.MoveTo(5);

            // A pipeline that evades everything it is allowed to evade. Atomic
            // damage never reaches the roll, so the mark lands regardless.
            _damage = new DamagePipeline(_statuses, new FixedRandom(0.0));
            _statuses.ApplyPassive(kurbyn, StatusKind.Evasion);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            _statuses.Apply(kurbyn, StatusKind.Mark, duration: 2, sourceOperatorId: syla.Id);

            RunToBlueUpkeep(machine);

            Assert.That(kurbyn.Health, Is.EqualTo(6 - _combat.MarkDamagePerTurn));
        }

        [Test]
        public void MarkCanNeutralize_BeforeTargetActs()
        {
            var syla = _red.Operators.First(o => o.Name == "Syla");
            var kurbyn = _blue.Operators.First();
            kurbyn.MoveTo(5);
            kurbyn.SetHealth(_combat.MarkDamagePerTurn);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            _statuses.Apply(kurbyn, StatusKind.Mark, duration: 2, sourceOperatorId: syla.Id);

            var upkeep = RunToBlueUpkeep(machine);

            Assert.That(upkeep.Neutralized.Count, Is.EqualTo(1));
            Assert.That(kurbyn.IsInYard, Is.True);
        }

        [Test]
        public void MarkExpiresAfterTwoTicks()
        {
            var syla = _red.Operators.First(o => o.Name == "Syla");
            var kurbyn = _blue.Operators.First();
            kurbyn.MoveTo(5);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            _statuses.Apply(kurbyn, StatusKind.Mark, duration: 2, sourceOperatorId: syla.Id);

            RunToBlueUpkeep(machine);            // Blue turn 1 — first tick
            machine.Roll();
            machine.EndTurn();

            machine.BeginTurn();                 // Red
            machine.Roll();
            machine.EndTurn();

            machine.BeginTurn();                 // Blue turn 2 — second tick, then expiry
            machine.Roll();
            var report = machine.EndTurn();

            Assert.That(kurbyn.Health, Is.EqualTo(6 - 2 * _combat.MarkDamagePerTurn));
            Assert.That(report.Expired.Any(e => e.Kind == StatusKind.Mark), Is.True);
            Assert.That(_statuses.MarkedBy(kurbyn), Is.Null);
        }

        [Test]
        public void Upkeep_ReArmsTheEvasionCharge()
        {
            var syla = _red.Operators.First(o => o.Name == "Syla");
            _statuses.ApplyPassive(syla, StatusKind.Evasion);
            var dice = new ScriptedDice(3);

            var machine = Machine(dice);
            machine.BeginTurn();
            _statuses.TryEvade(syla, new FixedRandom(0.0));
            Assert.That(_statuses.TryEvade(syla, new FixedRandom(0.0)), Is.False);

            machine.Roll();
            machine.EndTurn();
            machine.BeginTurn();   // Blue
            machine.Roll();
            machine.EndTurn();
            machine.BeginTurn();   // Red — charge re-arms

            Assert.That(_statuses.TryEvade(syla, new FixedRandom(0.0)), Is.True);
        }

        // ── Upkeep: beacons (ADR-0006) ───────────────────────────────────

        /// <summary>Blue's cell for a given progress. Blue starts at track 13.</summary>
        private CellRef BlueCell(int progress) => _map.CellAt(PlayerColor.Blue, progress);

        /// <summary>
        /// Paints a beacon for Red directly on the registry, skipping the ability.
        /// </summary>
        /// <remarks>
        /// This fixture owns the <i>ordering</i> — when a beacon resolves relative
        /// to bleed, marks and the turn boundary. Whether an ability can legally
        /// place one is <c>AbilityResolver</c>'s question, and routing through it
        /// here would make these tests fail for reasons that have nothing to do
        /// with what they assert.
        /// </remarks>
        private void PaintForRed(CellRef cell, int totalDamage, int radius = 0)
        {
            _cellEffects.Paint(
                cell, PlayerColor.Red, _red.Operators.First().Id,
                totalDamage, radius, DamageType.Normal);
        }

        [Test]
        public void ABeacon_FiresAtItsOwnersNextUpkeep_AndNotAtAnybodyElses()
        {
            // The whole counterplay window: one full round, so every opponent
            // moves once before it lands. Firing on the next seat's upkeep
            // instead would strike a victim three seats away before they had a
            // turn at all.
            var kurbyn = _blue.Operators.First();
            kurbyn.MoveTo(5);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();                          // Red turn 1
            PaintForRed(BlueCell(5), totalDamage: 2);
            machine.Roll();
            machine.EndTurn();

            var blueUpkeep = machine.BeginTurn();         // Blue — not its beacon
            Assert.That(blueUpkeep.CellEffects, Is.Empty);
            Assert.That(kurbyn.Health, Is.EqualTo(6), "still standing on it, untouched");
            machine.Roll();
            machine.EndTurn();

            var redUpkeep = machine.BeginTurn();          // Red turn 2 — it fires
            Assert.That(redUpkeep.CellEffects.Count, Is.EqualTo(1));
            Assert.That(kurbyn.Health, Is.EqualTo(4));
        }

        [Test]
        public void ABeacon_IsSpentByFiring()
        {
            var kurbyn = _blue.Operators.First();
            kurbyn.MoveTo(5);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            PaintForRed(BlueCell(5), totalDamage: 2);

            machine.Roll(); machine.EndTurn();
            machine.BeginTurn(); machine.Roll(); machine.EndTurn();
            machine.BeginTurn();                          // fires here
            machine.Roll(); machine.EndTurn();
            machine.BeginTurn(); machine.Roll(); machine.EndTurn();

            var later = machine.BeginTurn();              // Red again

            Assert.That(later.CellEffects, Is.Empty, "one beacon, one beam");
            Assert.That(kurbyn.Health, Is.EqualTo(4), "and no second helping of damage");
        }

        [Test]
        public void AnOperatorThatWalksOffThePaintedCell_IsMissed()
        {
            // The bet. Nobody is standing there when the beam lands, so it hits
            // nothing — and says so, because silence would be indistinguishable
            // from a beacon that was never placed.
            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            PaintForRed(BlueCell(30), totalDamage: 6);    // empty ground

            machine.Roll(); machine.EndTurn();
            machine.BeginTurn(); machine.Roll(); machine.EndTurn();

            var redUpkeep = machine.BeginTurn();

            Assert.That(redUpkeep.CellEffects.Count, Is.EqualTo(1), "a miss is still reported");
            Assert.That(redUpkeep.CellEffects[0].Caught, Is.Empty);
            Assert.That(redUpkeep.CellEffects[0].DamagePerTarget, Is.EqualTo(0));
        }

        [Test]
        public void ABeacon_OutlivesTheOperatorThatPlacedIt_AndStillPaysItsOwner()
        {
            // A deployed device is not its operator (ADR-0006). Letting a kill
            // refund the energy already spent would make the ability worse than
            // it reads.
            var bouncer = _red.Operators.First();
            var kurbyn = _blue.Operators.First();
            kurbyn.MoveTo(5);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            PaintForRed(BlueCell(5), totalDamage: 6);     // enough to finish a 6-health operator

            _neutralize.Apply(bouncer);                   // the caster goes to its yard
            Assert.That(bouncer.IsInYard, Is.True, "precondition");

            machine.Roll(); machine.EndTurn();
            machine.BeginTurn(); machine.Roll(); machine.EndTurn();

            var redUpkeep = machine.BeginTurn();

            Assert.That(kurbyn.IsInYard, Is.True, "the beam still fired and still killed");
            Assert.That(redUpkeep.Neutralized.Count, Is.EqualTo(1));
            Assert.That(redUpkeep.Neutralized[0].Cause, Is.EqualTo("beacon"));
            Assert.That(redUpkeep.Neutralized[0].Outcome.BountyPaidTo, Is.EqualTo(PlayerColor.Red),
                "a killer sitting in its own yard still collects");
        }

        [Test]
        public void RepaintingYourOwnCell_ToppedUpRatherThanStacked()
        {
            var kurbyn = _blue.Operators.First();
            kurbyn.MoveTo(5);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            PaintForRed(BlueCell(5), totalDamage: 2);
            PaintForRed(BlueCell(5), totalDamage: 2);     // same cell, same owner

            machine.Roll(); machine.EndTurn();
            machine.BeginTurn(); machine.Roll(); machine.EndTurn();

            var redUpkeep = machine.BeginTurn();

            Assert.That(redUpkeep.CellEffects.Count, Is.EqualTo(1), "one beacon, not two");
            Assert.That(kurbyn.Health, Is.EqualTo(4), "and one beam's worth of damage");
        }

        // ── Snapshots for the board (ADR-0006, ADR-0007) ─────────────────

        [Test]
        public void ASnapshot_ShowsAPaintedArea_UntilTheBeamFires()
        {
            // The board draws from this, so it has to name the owner and every
            // cell the beam will reach, and forget the beacon once it has fired.
            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            PaintForRed(BlueCell(5), totalDamage: 2, radius: 1);

            var shown = _cellEffects.Snapshot();

            Assert.That(shown.Count, Is.EqualTo(1));
            Assert.That(shown[0].Cell, Is.EqualTo(BlueCell(5)));
            Assert.That(shown[0].Owner, Is.EqualTo(PlayerColor.Red));
            Assert.That(shown[0].IsZone, Is.False);
            Assert.That(shown[0].HasDetonated, Is.False);
            Assert.That(shown[0].Covered, Is.EquivalentTo(new[] { BlueCell(4), BlueCell(5), BlueCell(6) }),
                "radius 1 is the anchor and one cell either side, along the track");

            machine.Roll(); machine.EndTurn();
            machine.BeginTurn(); machine.Roll(); machine.EndTurn();
            machine.BeginTurn();                          // Red turn 2 — it fires

            Assert.That(_cellEffects.Snapshot(), Is.Empty, "a spent beacon is no longer drawn");
        }

        [Test]
        public void ASnapshot_KeepsAZoneAfterItsDetonation_AndSaysItHasGoneOff()
        {
            // A zone lingers (ADR-0007), so the board keeps drawing it, and the
            // armed and lingering states have to be told apart.
            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            _cellEffects.Deploy(
                cell: BlueCell(30), owner: PlayerColor.Red, sourceOperatorId: _red.Operators.First().Id,
                detonationDamage: 1, lingerDamage: 1, lingerTicks: 2, radius: 1,
                damageType: DamageType.Normal, detonationStatus: StatusKind.Stun, statusDuration: 2);

            var armed = _cellEffects.Snapshot();
            Assert.That(armed.Count, Is.EqualTo(1));
            Assert.That(armed[0].IsZone, Is.True);
            Assert.That(armed[0].HasDetonated, Is.False);

            machine.Roll(); machine.EndTurn();
            machine.BeginTurn(); machine.Roll(); machine.EndTurn();
            machine.BeginTurn();                          // Red turn 2 — detonation

            var lingering = _cellEffects.Snapshot();
            Assert.That(lingering.Count, Is.EqualTo(1), "a detonated zone is still on the board");
            Assert.That(lingering[0].HasDetonated, Is.True);
            Assert.That(lingering[0].Covered.Count, Is.EqualTo(3));
        }

        // ── End of turn ──────────────────────────────────────────────────

        [Test]
        public void StatusDurationsExpire_AtTheEndOfTheOwnersTurn()
        {
            var kurbyn = _blue.Operators.First();

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();                             // Red
            _statuses.Apply(kurbyn, StatusKind.Stun, duration: 1);
            machine.Roll();
            machine.EndTurn();

            machine.BeginTurn();                             // Blue, stunned
            Assert.That(_statuses.IsStunned(kurbyn), Is.True);
            machine.Roll();
            var report = machine.EndTurn();

            Assert.That(_statuses.IsStunned(kurbyn), Is.False);
            Assert.That(report.Expired.Any(e => e.Kind == StatusKind.Stun), Is.True);
        }

        [Test]
        public void EndingATurnBetweenTurns_IsRefused()
        {
            var machine = Machine(new SeededRandom(1));

            Assert.Throws<InvalidOperationException>(() => machine.EndTurn());
        }

        [Test]
        public void ATurnCanBeEndedWithoutActing()
        {
            // A stunned player still takes a turn; they simply do nothing with it.
            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            machine.Roll();

            Assert.That(machine.EndTurn().MatchOver, Is.False);
        }

        // ── Winning ──────────────────────────────────────────────────────

        [Test]
        public void AllOperatorsHome_WinsGame()
        {
            foreach (var op in _red.Operators) op.MoveTo(BoardProfile.Standard.Journey);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            machine.Roll();
            var report = machine.EndTurn();

            Assert.That(report.MatchOver, Is.True);
            Assert.That(report.Winner, Is.EqualTo(PlayerColor.Red));
            Assert.That(machine.Phase, Is.EqualTo(TurnPhase.MatchOver));
        }

        [Test]
        public void AWonMatch_AcceptsNoFurtherTurns()
        {
            foreach (var op in _red.Operators) op.MoveTo(BoardProfile.Standard.Journey);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            machine.Roll();
            machine.EndTurn();

            Assert.Throws<InvalidOperationException>(() => machine.BeginTurn());
        }

        [Test]
        public void APartiallyHomeSquad_HasNotWon()
        {
            _red.Operators.First().MoveTo(BoardProfile.Standard.Journey);

            var machine = Machine(new SeededRandom(1));
            machine.BeginTurn();
            machine.Roll();

            Assert.That(machine.EndTurn().MatchOver, Is.False);
        }

        // ── A whole match, headless ──────────────────────────────────────

        [Test]
        public void AMatchRunsToCompletion_WithoutAnyRuleThrowing()
        {
            // The first end-to-end exercise of the core: hundreds of turns of
            // upkeep, rolls, doubles and expiry, with every service wired
            // together. It asserts no outcome — only that the machine never ties
            // itself in a knot.
            var machine = Machine(new SeededRandom(4242));

            for (int turn = 0; turn < 300 && machine.Phase != TurnPhase.MatchOver; turn++)
            {
                machine.BeginTurn();
                machine.Roll();
                while (machine.CanRollAgain) machine.Roll();
                machine.EndTurn();
            }

            Assert.That(_red.TurnIndex, Is.AtLeast(1));
            Assert.That(_blue.TurnIndex, Is.AtLeast(1));
        }
    }

    [TestFixture]
    public class WinConditionsTests
    {
        private PathMap _map;
        private WinConditions _win;
        private PlayerState _player;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _win = new WinConditions(_map);
            _player = new PlayerState(PlayerColor.Red, new[]
            {
                new OperatorState(1, "Bouncer", PlayerColor.Red, 12, 1.0),
                new OperatorState(2, "Syla", PlayerColor.Red, 6, 1.5),
                new OperatorState(3, "Kurbyn", PlayerColor.Red, 6, 1.0)
            });
        }

        [Test]
        public void AllThreeOperatorsHome_WinsGame()
        {
            foreach (var op in _player.Operators) op.MoveTo(BoardProfile.Standard.Journey);

            Assert.That(_win.HasWon(_player), Is.True);
        }

        [Test]
        public void TwoOfThreeHome_DoesNot()
        {
            _player.Operators[0].MoveTo(BoardProfile.Standard.Journey);
            _player.Operators[1].MoveTo(BoardProfile.Standard.Journey);
            _player.Operators[2].MoveTo(53);

            Assert.That(_win.HasWon(_player), Is.False);
            Assert.That(_win.FinishedCount(_player), Is.EqualTo(2));
        }

        [Test]
        public void AnOperatorInItsHomeColumn_HasNotFinished()
        {
            // The last cell of the column is still one step short of HOME.
            _player.Operators[0].MoveTo(53);

            Assert.That(_win.HasFinished(_player.Operators[0]), Is.False);
        }

        [Test]
        public void AnOperatorInTheYard_HasNotFinished()
        {
            Assert.That(_win.HasFinished(_player.Operators[0]), Is.False);
        }

        [Test]
        public void NoWinner_WhileAnyoneIsStillTravelling()
        {
            var other = new PlayerState(PlayerColor.Blue, new[]
            {
                new OperatorState(4, "Enemy", PlayerColor.Blue, 6, 1.0)
            });

            Assert.That(_win.Winner(new[] { _player, other }), Is.Null);
        }
    }

    [TestFixture]
    public class NeutralizeRulesTests
    {
        private PathMap _map;
        private MatchClock _clock;
        private CombatConfig _combat;
        private StatusRegistry _statuses;
        private AbilityResolver _abilities;
        private NeutralizeRules _neutralize;

        private PlayerState _red;
        private PlayerState _blue;
        private List<OperatorState> _operators;

        private OperatorState _syla;
        private OperatorState _bouncer;
        private OperatorState _enemy;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _combat = CombatConfig.Default;

            _syla = new OperatorState(1, "Syla", PlayerColor.Red, 6, 1.5);
            _bouncer = new OperatorState(2, "Bouncer", PlayerColor.Red, 12, 1.0);
            _enemy = new OperatorState(3, "Kurbyn", PlayerColor.Blue, 6, 1.0);

            _red = new PlayerState(PlayerColor.Red, new[] { _syla, _bouncer });
            _blue = new PlayerState(PlayerColor.Blue, new[] { _enemy });
            _operators = new List<OperatorState> { _syla, _bouncer, _enemy };

            _clock = new MatchClock(new[] { _red, _blue });
            _statuses = new StatusRegistry(_clock, _combat);

            var targeting = new TargetingRules(_map, _statuses);
            var energy = new EnergyLedger(EnergyConfig.Default);
            var damage = new DamagePipeline(_statuses, new SeededRandom(1));
            var cellEffects = new DeferredCellEffects(_clock, targeting, damage, _statuses);
            _abilities = new AbilityResolver(_map, _clock, energy, _statuses, targeting, damage, cellEffects);
            _neutralize = new NeutralizeRules(_statuses, _abilities, energy, _operators, new[] { _red, _blue }, _combat);

            _clock.BeginTurnFor(_red);
        }

        [Test]
        public void NeutralizedOperator_ReturnsToYardAtFullHealth()
        {
            _syla.MoveTo(30);
            _syla.SetHealth(0);

            _neutralize.Apply(_syla);

            Assert.That(_syla.IsInYard, Is.True);
            Assert.That(_syla.Health, Is.EqualTo(6));
        }

        [Test]
        public void NeutralizedOperator_LosesAllTrackProgress()
        {
            _syla.MoveTo(47);

            _neutralize.Apply(_syla);

            Assert.That(_syla.Progress, Is.EqualTo(PathMap.YardProgress));
        }

        [Test]
        public void NeutralizedOperator_LosesAllStatusEffects()
        {
            _statuses.Apply(_syla, StatusKind.Slow, duration: 5);
            _statuses.Apply(_syla, StatusKind.Bleed, duration: 5);

            _neutralize.Apply(_syla);

            Assert.That(_statuses.SpeedModifier(_syla), Is.EqualTo(0.0));
            Assert.That(_statuses.BleedStacks(_syla), Is.EqualTo(0));
        }

        [Test]
        public void NeutralizedOperator_RetainsItsPassives()
        {
            // A passive is who an operator is, not what it is carrying (§1.2).
            // Before passives had their own store, Kurbyn lost Evasive Protocol
            // — evasion and speed both — the first time he died.
            _statuses.ApplyPassive(_syla, StatusKind.Evasion, 0.5);

            _neutralize.Apply(_syla);

            Assert.That(_statuses.Has(_syla, StatusKind.Evasion), Is.True);
            Assert.That(_statuses.SpeedModifier(_syla), Is.EqualTo(0.5));
        }

        [Test]
        public void NeutralizedOperator_DoesNotReduceOwnersEnergyPool()
        {
            // The pool is player-level, so a yarded operator costs its owner
            // nothing economically (§1.2).
            var energy = new EnergyLedger(EnergyConfig.Default);
            energy.GrantForTurn(_red, new DiceRoll(6, 6));
            int before = _red.Energy;

            _neutralize.Apply(_syla);

            Assert.That(_red.Energy, Is.EqualTo(before));
        }

        // ── Tagged From Above's payout (§10.2) ───────────────────────────

        [Test]
        public void TaggedFromAbove_PaysOutToTheMarkersWholeSquad()
        {
            MarkTheEnemy();

            var hastened = _neutralize.Apply(_enemy).Hastened;

            Assert.That(hastened.Count, Is.EqualTo(2));
            Assert.That(hastened, Has.Member(_syla).And.Member(_bouncer));
        }

        [Test]
        public void TaggedFromAbove_HastensTheSquadOnItsNextTurn()
        {
            MarkTheEnemy();

            _neutralize.Apply(_enemy);
            _clock.BeginTurnFor(_red);

            // Haste is flat extra cells per roll now, not speed (§5.9).
            Assert.That(_statuses.IsHastened(_syla), Is.True);
            Assert.That(_statuses.IsHastened(_bouncer), Is.True);
        }

        [Test]
        public void TaggedFromAbove_DoesNotPayOutForAnUnmarkedTarget()
        {
            _clock.BeginTurnFor(_blue);
            _enemy.SetHealth(0);

            var hastened = _neutralize.Apply(_enemy).Hastened;

            Assert.That(hastened, Is.Empty);
        }

        [Test]
        public void TaggedFromAbove_ClearsTheMarkWhenItPaysOut()
        {
            MarkTheEnemy();

            _neutralize.Apply(_enemy);

            Assert.That(_statuses.MarkedBy(_enemy), Is.Null);
        }

        /// <summary>
        /// Marks the enemy on Red's turn, then hands the turn to Blue so the
        /// mark is live. A debuff applied outside its target's turn takes hold on
        /// that target's next one (§5), so the mark is not yet readable at the
        /// instant it is cast.
        /// </summary>
        private void MarkTheEnemy()
        {
            _statuses.Apply(_enemy, StatusKind.Mark, duration: 2, sourceOperatorId: _syla.Id);
            _clock.BeginTurnFor(_blue);
            _enemy.SetHealth(0);
        }
    }
}