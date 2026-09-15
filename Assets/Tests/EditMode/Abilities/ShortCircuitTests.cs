// Assets/Tests/EditMode/Abilities/ShortCircuitTests.cs
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
    /// Sanity's basic: 1 Normal and a stun at melee range. The kit's one
    /// ability built entirely from pre-existing kinds, so these tests are
    /// mostly about the roster invariants reaching him — cost, cooldown and
    /// cast mode behaving for operator #8 exactly as they do for the rest.
    /// </summary>
    [TestFixture]
    public class ShortCircuitTests
    {
        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;
        private EnergyLedger _energy;
        private DamagePipeline _damage;
        private DeferredCellEffects _cellEffects;
        private AbilityResolver _abilities;

        private OperatorState _sanity;
        private OperatorState _ally;
        private OperatorState _target;
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

            // Positions are stated as TRACK cells, never progress — progress is
            // relative to a colour's own start and a literal silently changes
            // meaning when the board does. Track 12 is one step from 11 and is
            // nobody's start cell, so it is single-targetable.
            _sanity = AtTrack(1, "Sanity", PlayerColor.Red, Sanity.MaxHealth, 11);
            _ally = AtTrack(2, "Ally", PlayerColor.Red, 6, 10);
            _target = AtTrack(3, "Target", PlayerColor.Blue, 6, 12);

            _red = new PlayerState(PlayerColor.Red, new[] { _sanity, _ally });
            _board = new List<OperatorState> { _sanity, _ally, _target };

            _clock.BeginTurnFor(PlayerColor.Red);
            _red.BeginTurn();
            Fund(12);
        }

        private OperatorState AtTrack(int id, string name, PlayerColor owner, int hp, int track)
        {
            var op = new OperatorState(id, name, owner, hp, 1.5);
            op.MoveTo(ProgressAtTrack(owner, track));
            return op;
        }

        private int ProgressAtTrack(PlayerColor owner, int track)
        {
            int circuit = _map.Profile.CircuitLength;
            int start = _map.StartTrackIndex(owner);

            return ((track - start) % circuit + circuit) % circuit;
        }

        /// <summary>Tops the pool up to a known figure without going through the dice.</summary>
        private void Fund(int amount)
        {
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6
            _red.BeginTurn();
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6, capped at 12
            if (amount < 12) _energy.Spend(_red, 12 - amount);
        }

        private AbilityResolution Use(OperatorState caster, AbilityDefinition ability, OperatorState target = null) =>
            _abilities.Use(caster, ability, target, _red, _board);

        [Test]
        public void ShortCircuit_DamagesAndStuns()
        {
            var result = Use(_sanity, Sanity.ShortCircuit, _target);

            Assert.That(result.Approved, Is.True);
            Assert.That(_target.Health, Is.EqualTo(5), "1 base — the stun is what is being bought");

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.IsStunned(_target), Is.True,
                "duration 1, applied outside the target's turn, is its next turn (§5.1)");
        }

        [Test]
        public void ShortCircuit_RefusedOnCooldown_AndReadyAgainAfterOneOwnerTurn()
        {
            Use(_sanity, Sanity.ShortCircuit, _target);

            var second = Use(_sanity, Sanity.ShortCircuit, _target);

            Assert.That(second.Refusal, Is.EqualTo(AbilityRefusal.OnCooldown));

            // Cooldown 1: unusable for the caster's next turn, back the turn
            // after — the declared cooldown is what stops two casts in one
            // banked turn (§3.1).
            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_abilities.IsReady(_sanity, Sanity.ShortCircuit), Is.False, "the blocked turn");

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_abilities.IsReady(_sanity, Sanity.ShortCircuit), Is.True);
        }

        [Test]
        public void ShortCircuit_RefusedWithoutEnergy_AndCostsNothing()
        {
            _energy.Spend(_red, _red.Energy);   // broke

            var result = Use(_sanity, Sanity.ShortCircuit, _target);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.InsufficientEnergy));
            Assert.That(_target.Health, Is.EqualTo(6), "a refused ability does nothing");
            Assert.That(_abilities.IsReady(_sanity, Sanity.ShortCircuit), Is.True,
                "and takes no cooldown");
        }

        [Test]
        public void ShortCircuit_AimedAtAnAlly_IsRefusedAsWrongSide()
        {
            // Every effect is enemy-audience, so the friendly cast mode scopes
            // them all away — a stun prod aimed at a friend was never a legal
            // cast, and it costs nothing (§10).
            int before = _red.Energy;

            var result = Use(_sanity, Sanity.ShortCircuit, _ally);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.WrongSide));
            Assert.That(_red.Energy, Is.EqualTo(before));
        }
    }
}
