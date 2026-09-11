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

    [TestFixture]
    public class TurnStateMachineTests
    {
        private PathMap _map;
        private GameConfig _config;
        private MatchClock _clock;
        private StatusRegistry _statuses;
        private EnergyLedger _energy;
        private DamagePipeline _damage;
        private TargetingRules _targeting;
        private AbilityResolver _abilities;
        private NeutralizeRules _neutralize;
        private WinConditions _win;

        private PlayerState _red;
        private PlayerState _blue;
        private List<PlayerState> _players;

        private TurnStateMachine Machine(IRandom dice)
        {
            return new TurnStateMachine(
                _players, _clock, _config, dice, _energy, _statuses, _damage, _neutralize, _win);
        }

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _config = GameConfig.Default;

            _red = new PlayerState(PlayerColor.Red, new[]
            {
                new OperatorState(1, "Bouncer", PlayerColor.Red, 12, 1.5),
                new OperatorState(2, "Syla", PlayerColor.Red, 6, 2.0)
            });
            _blue = new PlayerState(PlayerColor.Blue, new[]
            {
                new OperatorState(3, "Kurbyn", PlayerColor.Blue, 6, 1.5)
            });
            _players = new List<PlayerState> { _red, _blue };

            _clock = new MatchClock(_players);
            _statuses = new StatusRegistry(_clock, CombatConfig.Default);
            _energy = new EnergyLedger(EnergyConfig.Default);
            _damage = new DamagePipeline(_statuses, new SeededRandom(1));
            _targeting = new TargetingRules(_map, _statuses);
            _abilities = new AbilityResolver(_map, _clock, _energy, _statuses, _targeting, _damage);
            _neutralize = new NeutralizeRules(_statuses, _abilities);
            _win = new WinConditions(_map);
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

        // ── Upkeep ───────────────────────────────────────────────────────

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

        [Test]
        public void Upkeep_ReArmsTheEvasionCharge()
        {
            var syla = _red.Operators.First(o => o.Name == "Syla");
            _statuses.ApplyPassive(syla, StatusKind.Evasion);
            var alwaysEvades = new ScriptedDice(3);

            var machine = Machine(alwaysEvades);
            machine.BeginTurn();
            _statuses.TryEvade(syla, new ScriptedRandomAlways(0.0));
            Assert.That(_statuses.TryEvade(syla, new ScriptedRandomAlways(0.0)), Is.False);

            machine.Roll();
            machine.EndTurn();
            machine.BeginTurn();   // Blue
            machine.Roll();
            machine.EndTurn();
            machine.BeginTurn();   // Red — charge re-arms

            Assert.That(_statuses.TryEvade(syla, new ScriptedRandomAlways(0.0)), Is.True);
        }

        private sealed class ScriptedRandomAlways : IRandom
        {
            private readonly double _value;
            public ScriptedRandomAlways(double value) { _value = value; }
            public int NextInt(int min, int max) => min;
            public double NextDouble() => _value;
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
                new OperatorState(1, "Bouncer", PlayerColor.Red, 12, 1.5),
                new OperatorState(2, "Syla", PlayerColor.Red, 6, 2.0),
                new OperatorState(3, "Kurbyn", PlayerColor.Red, 6, 1.5)
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
                new OperatorState(4, "Enemy", PlayerColor.Blue, 6, 1.5)
            });

            Assert.That(_win.Winner(new[] { _player, other }), Is.Null);
        }
    }

    [TestFixture]
    public class NeutralizeRulesTests
    {
        private PathMap _map;
        private MatchClock _clock;
        private StatusRegistry _statuses;
        private AbilityResolver _abilities;
        private NeutralizeRules _neutralize;
        private PlayerState _player;
        private OperatorState _op;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _op = new OperatorState(1, "Syla", PlayerColor.Red, 6, 2.0);
            _player = new PlayerState(PlayerColor.Red, new[] { _op });
            _clock = new MatchClock(new[] { _player });
            _statuses = new StatusRegistry(_clock, CombatConfig.Default);

            var targeting = new TargetingRules(_map, _statuses);
            var energy = new EnergyLedger(EnergyConfig.Default);
            var damage = new DamagePipeline(_statuses, new SeededRandom(1));
            _abilities = new AbilityResolver(_map, _clock, energy, _statuses, targeting, damage);
            _neutralize = new NeutralizeRules(_statuses, _abilities);

            _clock.BeginTurnFor(_player);
        }

        [Test]
        public void NeutralizedOperator_ReturnsToYardAtFullHealth()
        {
            _op.MoveTo(30);
            _op.SetHealth(0);

            _neutralize.Apply(_op);

            Assert.That(_op.IsInYard, Is.True);
            Assert.That(_op.Health, Is.EqualTo(6));
        }

        [Test]
        public void NeutralizedOperator_LosesAllTrackProgress()
        {
            _op.MoveTo(47);

            _neutralize.Apply(_op);

            Assert.That(_op.Progress, Is.EqualTo(PathMap.YardProgress));
        }

        [Test]
        public void NeutralizedOperator_LosesAllStatusEffects()
        {
            _statuses.Apply(_op, StatusKind.Slow, duration: 5);
            _statuses.Apply(_op, StatusKind.Bleed, duration: 5);

            _neutralize.Apply(_op);

            Assert.That(_statuses.SpeedModifier(_op), Is.EqualTo(0.0));
            Assert.That(_statuses.BleedStacks(_op), Is.EqualTo(0));
        }

        [Test]
        public void NeutralizedOperator_DoesNotReduceOwnersEnergyPool()
        {
            // The pool is player-level, so a yarded operator costs its owner
            // nothing economically (§1.2).
            var energy = new EnergyLedger(EnergyConfig.Default);
            energy.GrantForTurn(_player, new DiceRoll(6, 6));
            int before = _player.Energy;

            _neutralize.Apply(_op);

            Assert.That(_player.Energy, Is.EqualTo(before));
        }
    }
}