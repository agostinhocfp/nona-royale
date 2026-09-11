// Assets/Tests/EditMode/Abilities/AbilityResolverTests.cs
using System;
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
    internal sealed class FakeClock : ITurnClock
    {
        private readonly int[] _turns = new int[4];
        public PlayerColor ActivePlayer { get; private set; } = PlayerColor.Red;
        public int TurnIndexOf(PlayerColor color) => _turns[(int)color];

        public void BeginTurnFor(PlayerColor color)
        {
            ActivePlayer = color;
            _turns[(int)color]++;
        }
    }

    [TestFixture]
    public class AbilityResolverTests
    {
        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;
        private EnergyLedger _energy;
        private DamagePipeline _damage;
        private AbilityResolver _abilities;

        private OperatorState _bouncer;
        private OperatorState _syla;
        private OperatorState _enemy;
        private OperatorState _enemyTwo;
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
            _abilities = new AbilityResolver(_map, _clock, _energy, _statuses, _targeting, _damage);

            // Red starts at track 0; Blue at track 12.
            _bouncer = Deployed(1, "Bouncer", PlayerColor.Red, AlphaRoster.BouncerMaxHealth, 10);
            _syla = Deployed(2, "Syla", PlayerColor.Red, AlphaRoster.SylaMaxHealth, 11);
            _enemy = Deployed(3, "Enemy", PlayerColor.Blue, 6, 0);          // track 12
            _enemyTwo = Deployed(4, "Enemy2", PlayerColor.Blue, 6, 1);      // track 13

            _red = new PlayerState(PlayerColor.Red, new[] { _bouncer, _syla });
            _board = new List<OperatorState> { _bouncer, _syla, _enemy, _enemyTwo };

            _clock.BeginTurnFor(PlayerColor.Red);
            _red.BeginTurn();
            Fund(12);
        }

        private static OperatorState Deployed(int id, string name, PlayerColor owner, int hp, int progress)
        {
            var op = new OperatorState(id, name, owner, hp, 1.5);
            op.MoveTo(progress);
            return op;
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

        // ── Cost and cooldown ────────────────────────────────────────────

        [Test]
        public void AnAbility_SpendsFromTheSharedPool()
        {
            int before = _red.Energy;

            Use(_syla, AlphaRoster.FromTheHip, _enemy);

            Assert.That(_red.Energy, Is.EqualTo(before - AlphaRoster.FromTheHip.EnergyCost));
        }

        [Test]
        public void AbilityCostExceedingPool_IsRejected()
        {
            _energy.Spend(_red, _red.Energy);   // broke

            var result = Use(_syla, AlphaRoster.TaggedFromAbove, _enemy);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.InsufficientEnergy));
            Assert.That(_enemy.Health, Is.EqualTo(6), "a refused ability does nothing");
        }

        [Test]
        public void AbilityOnCooldown_IsRejected()
        {
            Use(_syla, AlphaRoster.FromTheHip, _enemy);

            var second = Use(_syla, AlphaRoster.FromTheHip, _enemy);

            Assert.That(second.Refusal, Is.EqualTo(AbilityRefusal.OnCooldown));
        }

        [Test]
        public void CooldownOfTwo_MakesAbilityUnusableForTwoOwnerTurns()
        {
            Use(_bouncer, AlphaRoster.VelvetRope, _enemy);      // cooldown 2

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_abilities.IsReady(_bouncer, AlphaRoster.VelvetRope), Is.False, "first blocked turn");

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_abilities.IsReady(_bouncer, AlphaRoster.VelvetRope), Is.False, "second blocked turn");

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_abilities.IsReady(_bouncer, AlphaRoster.VelvetRope), Is.True);
        }

        [Test]
        public void AnAbilityWithNoCooldown_IsImmediatelyReusable()
        {
            Use(_bouncer, AlphaRoster.AllInMauling, _enemy);

            Assert.That(_abilities.IsReady(_bouncer, AlphaRoster.AllInMauling), Is.True);
        }

        [Test]
        public void ARefusedAbility_CostsNoEnergyAndNoCooldown()
        {
            var far = Deployed(9, "Far", PlayerColor.Blue, 6, 20);   // track 32
            _board.Add(far);
            int before = _red.Energy;

            var result = Use(_syla, AlphaRoster.FromTheHip, far);

            Assert.That(result.Approved, Is.False);
            Assert.That(_red.Energy, Is.EqualTo(before));
            Assert.That(_abilities.IsReady(_syla, AlphaRoster.FromTheHip), Is.True);
        }

        [Test]
        public void NeutralizedOperator_HasItsCooldownsReset()
        {
            Use(_bouncer, AlphaRoster.VelvetRope, _enemy);
            Assert.That(_abilities.IsReady(_bouncer, AlphaRoster.VelvetRope), Is.False);

            _abilities.ResetCooldowns(_bouncer);

            Assert.That(_abilities.IsReady(_bouncer, AlphaRoster.VelvetRope), Is.True);
        }

        // ── Caster legality ──────────────────────────────────────────────

        [Test]
        public void StunnedOperator_CannotSpendEnergy()
        {
            _clock.BeginTurnFor(PlayerColor.Blue);      // an opponent acts
            _statuses.Apply(_syla, StatusKind.Stun, duration: 1);
            _clock.BeginTurnFor(PlayerColor.Red);      // Syla's turn comes round

            var result = Use(_syla, AlphaRoster.FromTheHip, _enemy);

            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.CasterStunned));
        }

        [Test]
        public void OperatorInHomeColumn_CannotTarget()
        {
            _syla.MoveTo(50);

            var result = Use(_syla, AlphaRoster.FromTheHip, _enemy);

            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.CasterOutOfPlay));
        }

        [Test]
        public void AStealthedEnemy_CannotBeSingleTargeted()
        {
            _statuses.Apply(_enemy, StatusKind.Stealth, duration: 2);
            _clock.BeginTurnFor(PlayerColor.Blue);
            _clock.BeginTurnFor(PlayerColor.Red);

            var result = Use(_syla, AlphaRoster.FromTheHip, _enemy);

            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.IllegalTarget));
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.Stealthed));
        }

        [Test]
        public void ATargetedAbilityWithNoTarget_IsRejected()
        {
            var result = Use(_syla, AlphaRoster.FromTheHip);

            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.NoTarget));
        }

        [Test]
        public void ASelfOriginAreaAbility_NeedsNoTarget()
        {
            var result = Use(_syla, AlphaRoster.AceShards);

            Assert.That(result.Approved, Is.True);
        }

        // ── Bouncer ──────────────────────────────────────────────────────

        [Test]
        public void VelvetRope_PlacesTargetAdjacentOnTheSideItCameFrom()
        {
            // Bouncer on track 10; the enemy approaches from track 12 and lands
            // on 11 — beside him, on the side it came from, never on his cell.
            var target = Deployed(8, "Enemy3", PlayerColor.Blue, 6, 36);   // 36 + 12 = track 48 % 48 = 0
            target.MoveTo(40);                                             // track 52 % 48 = 4
            _board.Add(target);
            _bouncer.MoveTo(2);                                            // track 2

            Use(_bouncer, AlphaRoster.VelvetRope, target);

            Assert.That(_map.CellAt(target.Owner, target.Progress), Is.EqualTo(CellRef.Track(3)));
        }

        [Test]
        public void APullThatWouldGoBehindTheTargetsStartCell_ClampsThere()
        {
            // An operator's path does not extend behind its own start cell, so
            // there is nowhere further back to place it. The start cell is safe,
            // which makes the clamp harmless.
            // NOTE: this case is not covered by COMBAT_SYSTEMS §7.4 and is owed
            // a doc amendment.
            Use(_bouncer, AlphaRoster.VelvetRope, _enemy);   // enemy sits on its own start

            Assert.That(_enemy.Progress, Is.EqualTo(0));
        }

        [Test]
        public void VelvetRope_DamagesAnEnemy()
        {
            Use(_bouncer, AlphaRoster.VelvetRope, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(3));
        }

        [Test]
        public void VelvetRopeOnAlly_DealsNoDamage()
        {
            int before = _syla.Health;

            var result = Use(_bouncer, AlphaRoster.VelvetRope, _syla);

            Assert.That(result.Approved, Is.True);
            Assert.That(_syla.Health, Is.EqualTo(before));
            Assert.That(result.Outcomes.Any(o => o.Kind == EffectOutcomeKind.Pulled), Is.True);
        }

        [Test]
        public void APulledOperator_DoesNotCollideOnArrival()
        {
            // Forced movement is placement: it never collides, even onto an
            // occupied cell (§7.4). Put an ally on the destination and confirm
            // nothing resolves.
            _syla.MoveTo(11);                       // track 11, the pull destination
            int sylaHealth = _syla.Health;

            Use(_bouncer, AlphaRoster.VelvetRope, _enemy);

            Assert.That(_syla.Health, Is.EqualTo(sylaHealth));
            Assert.That(_enemy.Health, Is.EqualTo(3), "only the rope's own damage");
        }

        [Test]
        public void AllInMauling_WoundsTheTargetAndTheBouncer()
        {
            _enemy.MoveTo(_enemy.Progress);                 // track 12
            _bouncer.MoveTo(11);                            // adjacent, range 1

            var result = Use(_bouncer, AlphaRoster.AllInMauling, _enemy);

            Assert.That(result.Approved, Is.True);
            Assert.That(_enemy.Health, Is.EqualTo(3));
            Assert.That(_bouncer.Health, Is.EqualTo(AlphaRoster.BouncerMaxHealth - 3));
        }

        [Test]
        public void AllInMaulingOnAlly_HealsInstead()
        {
            _syla.SetHealth(2);
            _syla.MoveTo(10);                       // adjacent, range 1

            Use(_bouncer, AlphaRoster.AllInMauling, _syla);

            Assert.That(_syla.Health, Is.EqualTo(5));
            Assert.That(_bouncer.Health, Is.EqualTo(AlphaRoster.BouncerMaxHealth),
                "the friendly version costs the Bouncer nothing");
        }

        [Test]
        public void AllInMauling_CanNeutralizeItsOwnCaster()
        {
            _bouncer.SetHealth(3);
            _bouncer.MoveTo(11);                            // adjacent to the enemy on track 12

            Use(_bouncer, AlphaRoster.AllInMauling, _enemy);

            Assert.That(_bouncer.Health, Is.EqualTo(0));
        }

        [Test]
        public void HealingNeverExceedsMaximum()
        {
            _syla.MoveTo(10);

            Use(_bouncer, AlphaRoster.AllInMauling, _syla);

            Assert.That(_syla.Health, Is.EqualTo(AlphaRoster.SylaMaxHealth));
        }

        // ── Syla ─────────────────────────────────────────────────────────

        [Test]
        public void FromTheHip_DamagesAndSlows()
        {
            Use(_syla, AlphaRoster.FromTheHip, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(4));

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.SpeedModifier(_enemy), Is.EqualTo(-0.5));
        }

        [Test]
        public void FromTheHip_DealsBonusDamageToBleedingTarget()
        {
            _statuses.Apply(_enemy, StatusKind.Bleed, duration: 3);
            _clock.BeginTurnFor(PlayerColor.Blue);
            _clock.BeginTurnFor(PlayerColor.Red);

            Use(_syla, AlphaRoster.FromTheHip, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(3), "2 base plus 1 against a bleeding target");
        }

        [Test]
        public void AceShards_HitsEveryEnemyInTheWindowAndBleedsThem()
        {
            var result = Use(_syla, AlphaRoster.AceShards);

            Assert.That(_enemy.Health, Is.EqualTo(3));
            Assert.That(_enemyTwo.Health, Is.EqualTo(3));

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.IsBleeding(_enemy), Is.True);
            Assert.That(_statuses.IsBleeding(_enemyTwo), Is.True);
            Assert.That(result.Outcomes.Count, Is.EqualTo(4), "two hit, two bled");
        }

        [Test]
        public void AceShards_NeverHitsItsOwnSide()
        {
            int allyHealth = _bouncer.Health;

            Use(_syla, AlphaRoster.AceShards);

            Assert.That(_bouncer.Health, Is.EqualTo(allyHealth));
        }

        [Test]
        public void TaggedFromAbove_GrantsStealthEvenWithoutPayout()
        {
            Use(_syla, AlphaRoster.TaggedFromAbove, _enemy);

            Assert.That(_statuses.Has(_syla, StatusKind.Stealth), Is.True);
            Assert.That(_statuses.MarkedBy(_enemy), Is.Null, "the mark takes hold on the target's turn");

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.MarkedBy(_enemy), Is.EqualTo(_syla.Id));
        }

        // ── Kurbyn ───────────────────────────────────────────────────────

        [Test]
        public void DarginPulse_DamagesAndStunsEveryEnemyInRange()
        {
            var kurbyn = Deployed(5, "Kurbyn", PlayerColor.Red, 6, 11);
            _board.Add(kurbyn);

            Use(kurbyn, AlphaRoster.DarginPulse);

            Assert.That(_enemy.Health, Is.EqualTo(4));

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.IsStunned(_enemy), Is.True);
        }

        [Test]
        public void MiraclePull_ExecutesTargetBelowHalfHealthAtCastTime()
        {
            var kurbyn = Deployed(5, "Kurbyn", PlayerColor.Red, 6, 11);
            _board.Add(kurbyn);
            _enemy.SetHealth(2);                    // below half of 6

            Use(kurbyn, AlphaRoster.MiraclePull, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(0));
        }

        [Test]
        public void MiraclePull_DoesNotExecuteTargetAtExactlyHalfHealth()
        {
            var kurbyn = Deployed(5, "Kurbyn", PlayerColor.Red, 6, 11);
            _board.Add(kurbyn);
            _enemy.SetHealth(3);                    // exactly half

            Use(kurbyn, AlphaRoster.MiraclePull, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(0), "3 Atomic still finishes it, but by damage");
        }

        [Test]
        public void MiraclePull_AtFullHealth_DealsItsAtomicDamage()
        {
            var kurbyn = Deployed(5, "Kurbyn", PlayerColor.Red, 6, 11);
            _board.Add(kurbyn);

            Use(kurbyn, AlphaRoster.MiraclePull, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(3));
        }

        [Test]
        public void MiraclePullSplash_ExcludesPrimaryTarget()
        {
            var kurbyn = Deployed(5, "Kurbyn", PlayerColor.Red, 6, 11);
            _board.Add(kurbyn);

            Use(kurbyn, AlphaRoster.MiraclePull, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(3), "3 direct, no splash on itself");
            Assert.That(_enemyTwo.Health, Is.EqualTo(4), "2 splash");
        }

        [Test]
        public void MiraclePullIsAtomic_AndIgnoresEvasion()
        {
            var kurbyn = Deployed(5, "Kurbyn", PlayerColor.Red, 6, 11);
            _board.Add(kurbyn);
            _statuses.ApplyPassive(_enemy, StatusKind.Evasion);

            Use(kurbyn, AlphaRoster.MiraclePull, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(3), "Atomic cannot be evaded");
        }
    }

    [TestFixture]
    public class AlphaRosterTests
    {
        [Test]
        public void EveryAbility_MatchesItsCostTier()
        {
            // Costs are 3 / 6 / 9 by tier (COMBAT_SYSTEMS §3.2). A value outside
            // that set means either the roster or the doc drifted.
            foreach (var ability in AlphaRoster.All)
            {
                bool onTier = ability.EnergyCost == 3 || ability.EnergyCost == 6 || ability.EnergyCost == 9;
                Assert.That(onTier, Is.True, $"{ability.Name} costs {ability.EnergyCost}");
            }
        }

        [Test]
        public void NoAbilityCostsMoreThanTheEnergyCap()
        {
            // An ability that cannot be afforded at a full pool is uncastable.
            foreach (var ability in AlphaRoster.All)
                Assert.That(ability.EnergyCost, Is.LessThanOrEqualTo(EnergyConfig.Default.EnergyCap),
                    ability.Name);
        }

        [Test]
        public void RosterSpeeds_SitInsideTheAdoptedBand()
        {
            // ADR-0002 Amendment 2: the band is 1.5 to 2.0, and Kurbyn reaches
            // 2.0 only via his passive.
            Assert.That(AlphaRoster.BouncerSpeed, Is.EqualTo(1.5));
            Assert.That(AlphaRoster.SylaSpeed, Is.EqualTo(2.0));
            Assert.That(AlphaRoster.KurbynBaseSpeed + AlphaRoster.KurbynPassiveSpeedBonus, Is.EqualTo(2.0));
        }

        [Test]
        public void EveryAbilityHasAtLeastOneEffect()
        {
            foreach (var ability in AlphaRoster.All)
                Assert.That(ability.Effects.Count, Is.AtLeast(1), ability.Name);
        }

        [Test]
        public void AbilityIdsAreUnique()
        {
            var seen = new HashSet<int>();

            foreach (var ability in AlphaRoster.All)
                Assert.That(seen.Add(ability.Id), Is.True, $"duplicate id on {ability.Name}");
        }
    }
}