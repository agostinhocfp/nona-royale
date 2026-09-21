// Assets/Tests/EditMode/Abilities/NuetuTests.cs
using System.Collections.Generic;
using System.Linq;
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
    /// Nuetu as the answer to mobility (COMBAT_SYSTEMS §10.7, designer,
    /// 2026-09-21): Bio-Link Rage leaves Burdened on its target, and Killzone
    /// comes round often enough to be standing when somebody closes.
    /// </summary>
    /// <remarks>
    /// The burden's arithmetic is <c>BurdenTests</c>' subject and is not
    /// repeated here. What this fixture pins is that the rider is applied at
    /// all, to enemies only, and that it reaches the operator it was written
    /// for — a hasted one, whose bonus it cancels (§5.16).
    /// </remarks>
    [TestFixture]
    public class NuetuTests
    {
        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;
        private EnergyLedger _energy;
        private DamagePipeline _damage;
        private AbilityResolver _abilities;

        private OperatorState _nuetu;
        private OperatorState _ally;
        private OperatorState _kurbyn;
        private PlayerState _red;
        private PlayerState _blue;
        private List<OperatorState> _board;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _clock = new FakeClock();
            _statuses = new StatusRegistry(_clock, CombatConfig.Default);
            _targeting = new TargetingRules(_map, _statuses);
            _energy = new EnergyLedger(EnergyConfig.Default);
            _damage = new DamagePipeline(_statuses, new SeededRandom(3));

            // TRACK cells, never progress, and none of them a start cell
            // (1, 14, 27, 40): Nuetu at 10 with an ally beside him, Kurbyn at
            // 11 — inside his range 2, which is the only band he has.
            _nuetu = AtTrack(1, "Nuetu", PlayerColor.Red, Nuetu.MaxHealth, 10);
            _ally = AtTrack(2, "Ally", PlayerColor.Red, 7, 9);
            _kurbyn = AtTrack(3, "Kurbyn", PlayerColor.Blue, Kurbyn.MaxHealth, 11);

            _red = new PlayerState(PlayerColor.Red, new[] { _nuetu, _ally });
            _blue = new PlayerState(PlayerColor.Blue, new[] { _kurbyn });
            _board = new List<OperatorState> { _nuetu, _ally, _kurbyn };

            _statuses.ApplyPassive(_kurbyn, StatusKind.Hastened);

            _abilities = new AbilityResolver(
                _map, _clock, _energy, _statuses, _targeting, _damage,
                new DeferredCellEffects(_clock, _targeting, _damage, _statuses),
                new DeferredOperatorEffects(_clock, _targeting, _damage, _statuses),
                null, new[] { _red, _blue });

            _clock.BeginTurnFor(PlayerColor.Red);
            _red.BeginTurn();
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));
            _red.BeginTurn();
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));
        }

        private OperatorState AtTrack(int id, string name, PlayerColor owner, int hp, int track)
        {
            var op = new OperatorState(id, name, owner, hp, 1.0);
            int circuit = _map.Profile.CircuitLength;
            op.MoveTo(((track - _map.StartTrackIndex(owner)) % circuit + circuit) % circuit);
            return op;
        }

        [Test]
        public void TheDesignersNumbers()
        {
            // 2026-09-21. Changing them is a deliberate act that also updates
            // §10.7.
            Assert.That(Nuetu.MaxHealth, Is.EqualTo(7));
            Assert.That(Nuetu.Speed, Is.EqualTo(1.0));
            Assert.That(Nuetu.BioLinkBurdenTurns, Is.EqualTo(2));

            Assert.That((Nuetu.BioLinkRage.EnergyCost, Nuetu.BioLinkRage.CooldownTurns, Nuetu.BioLinkRage.Range),
                Is.EqualTo((3, 2, 2)));
            Assert.That((Nuetu.Killzone.EnergyCost, Nuetu.Killzone.CooldownTurns, Nuetu.Killzone.Range),
                Is.EqualTo((9, 4, 2)), "cooldown 6 → 4, 2026-09-21");
            Assert.That(Nuetu.AblativePlating.CooldownTurns, Is.EqualTo(4), "untouched by the pass");
        }

        [Test]
        public void BioLinkRage_Burdens_WhatItHits()
        {
            var result = _abilities.Use(_nuetu, Nuetu.BioLinkRage, _kurbyn, _red, _board);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_kurbyn.Health, Is.EqualTo(Kurbyn.MaxHealth - 3), "the hit is unchanged");
            // Applied outside the target's own turn, so it takes hold on his
            // next one (§5) — which is the turn the cells are counted on.
            Assert.That(_statuses.HasOnNextTurn(_kurbyn, StatusKind.Burdened), Is.True);
            Assert.That(result.Outcomes.Any(o =>
                o.Kind == EffectOutcomeKind.StatusApplied && o.Status == StatusKind.Burdened), Is.True);
        }

        [Test]
        public void TheBurden_CancelsAHastedTarget_WithoutStoppingIt()
        {
            _abilities.Use(_nuetu, Nuetu.BioLinkRage, _kurbyn, _red, _board);

            // Both are flat-cell channels and neither is speed, so the two sit
            // on the operator at once and settle against each other when a move
            // is measured (§5.16). What this pins is that the burden is really
            // on the operator whose passive it answers.
            _clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(_statuses.IsHastened(_kurbyn), Is.True, "his passive is untouched");
            Assert.That(_statuses.IsBurdened(_kurbyn), Is.True);
            Assert.That(_statuses.SpeedModifier(_kurbyn), Is.EqualTo(0.0), "neither is speed");
        }

        [Test]
        public void ItIsEnemiesOnly_AndAFriendlyCastIsRefused()
        {
            var result = _abilities.Use(_nuetu, Nuetu.BioLinkRage, _ally, _red, _board);

            Assert.That(result.Approved, Is.False, "an ally target is refused and costs nothing");
            Assert.That(_statuses.Has(_ally, StatusKind.Burdened), Is.False);
            Assert.That(_ally.Health, Is.EqualTo(7));
        }

        [Test]
        public void TheBurden_IsTimed_AndNotAStun()
        {
            _abilities.Use(_nuetu, Nuetu.BioLinkRage, _kurbyn, _red, _board);

            Assert.That(_statuses.IsStunned(_kurbyn), Is.False, "the floor is one cell, never zero");

            // Applied outside its own turn, it takes hold on the target's next
            // turn and expires at the end of its second (§5).
            for (int turn = 0; turn < Nuetu.BioLinkBurdenTurns; turn++)
            {
                _clock.BeginTurnFor(PlayerColor.Blue);
                _statuses.ExpireCompleted(_kurbyn);
            }

            Assert.That(_statuses.IsBurdened(_kurbyn), Is.False, "it runs out; he has to be hit again");
        }
    }
}
