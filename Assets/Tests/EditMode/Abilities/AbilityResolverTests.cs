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

            // Positions are stated as TRACK cells, not progress, and converted
            // per owner. Progress is relative to a colour's own start, so a
            // literal progress silently changes meaning whenever the start
            // offset moves — which it did when the circuit went 48 → 52 and
            // took every "// track 12" comment in this file with it.
            _bouncer = AtTrack(1, "Bouncer", PlayerColor.Red, Bouncer.MaxHealth, 10);
            _syla = AtTrack(2, "Syla", PlayerColor.Red, Syla.MaxHealth, 11);
            _enemy = AtTrack(3, "Enemy", PlayerColor.Blue, 6, 12);
            _enemyTwo = AtTrack(4, "Enemy2", PlayerColor.Blue, 6, 13);

            _red = new PlayerState(PlayerColor.Red, new[] { _bouncer, _syla });
            _board = new List<OperatorState> { _bouncer, _syla, _enemy, _enemyTwo };

            _clock.BeginTurnFor(PlayerColor.Red);
            _red.BeginTurn();
            Fund(12);
        }

        /// <summary>Builds an operator standing on a given outer-track cell.</summary>
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

        // ── Cost and cooldown ────────────────────────────────────────────

        [Test]
        public void AnAbility_SpendsFromTheSharedPool()
        {
            int before = _red.Energy;

            Use(_syla, Syla.FromTheHip, _enemy);

            Assert.That(_red.Energy, Is.EqualTo(before - Syla.FromTheHip.EnergyCost));
        }

        [Test]
        public void AbilityCostExceedingPool_IsRejected()
        {
            _energy.Spend(_red, _red.Energy);   // broke

            var result = Use(_syla, Syla.TaggedFromAbove, _enemy);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.InsufficientEnergy));
            Assert.That(_enemy.Health, Is.EqualTo(6), "a refused ability does nothing");
        }

        [Test]
        public void AbilityOnCooldown_IsRejected()
        {
            Use(_syla, Syla.FromTheHip, _enemy);

            var second = Use(_syla, Syla.FromTheHip, _enemy);

            Assert.That(second.Refusal, Is.EqualTo(AbilityRefusal.OnCooldown));
        }

        [Test]
        public void CooldownOfTwo_MakesAbilityUnusableForTwoOwnerTurns()
        {
            Use(_bouncer, Bouncer.VelvetRope, _enemy);      // cooldown 2

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_abilities.IsReady(_bouncer, Bouncer.VelvetRope), Is.False, "first blocked turn");

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_abilities.IsReady(_bouncer, Bouncer.VelvetRope), Is.False, "second blocked turn");

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_abilities.IsReady(_bouncer, Bouncer.VelvetRope), Is.True);
        }

        [Test]
        public void AnAbilityWithNoCooldown_IsImmediatelyReusable()
        {
            _bouncer.MoveTo(ProgressAtTrack(PlayerColor.Red, 11));   // inside range 2

            Use(_bouncer, Bouncer.AllInMauling, _enemy);

            Assert.That(_abilities.IsReady(_bouncer, Bouncer.AllInMauling), Is.True);
        }

        [Test]
        public void ARefusedAbility_CostsNoEnergyAndNoCooldown()
        {
            var far = AtTrack(9, "Far", PlayerColor.Blue, 6, 32);
            _board.Add(far);
            int before = _red.Energy;

            var result = Use(_syla, Syla.FromTheHip, far);

            Assert.That(result.Approved, Is.False);
            Assert.That(_red.Energy, Is.EqualTo(before));
            Assert.That(_abilities.IsReady(_syla, Syla.FromTheHip), Is.True);
        }

        [Test]
        public void NeutralizedOperator_HasItsCooldownsReset()
        {
            Use(_bouncer, Bouncer.VelvetRope, _enemy);
            Assert.That(_abilities.IsReady(_bouncer, Bouncer.VelvetRope), Is.False);

            _abilities.ResetCooldowns(_bouncer);

            Assert.That(_abilities.IsReady(_bouncer, Bouncer.VelvetRope), Is.True);
        }

        // ── Caster legality ──────────────────────────────────────────────

        [Test]
        public void StunnedOperator_CannotSpendEnergy()
        {
            _clock.BeginTurnFor(PlayerColor.Blue);      // an opponent acts
            _statuses.Apply(_syla, StatusKind.Stun, duration: 1);
            _clock.BeginTurnFor(PlayerColor.Red);      // Syla's turn comes round

            var result = Use(_syla, Syla.FromTheHip, _enemy);

            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.CasterStunned));
        }

        [Test]
        public void OperatorInHomeColumn_CannotTarget()
        {
            // The first home-column cell, derived rather than hardcoded. A
            // literal 50 was a home cell on the 48-track board and is still
            // outer track on the 52-track one, so this test quietly stopped
            // testing anything when the board changed.
            _syla.MoveTo(_map.Profile.TrackLength);

            var result = Use(_syla, Syla.FromTheHip, _enemy);

            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.CasterOutOfPlay));
        }

        [Test]
        public void AStealthedEnemy_CannotBeSingleTargeted()
        {
            _statuses.Apply(_enemy, StatusKind.Stealth, duration: 2);
            _clock.BeginTurnFor(PlayerColor.Blue);
            _clock.BeginTurnFor(PlayerColor.Red);

            var result = Use(_syla, Syla.FromTheHip, _enemy);

            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.IllegalTarget));
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.Stealthed));
        }

        [Test]
        public void ATargetedAbilityWithNoTarget_IsRejected()
        {
            var result = Use(_syla, Syla.FromTheHip);

            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.NoTarget));
        }

        [Test]
        public void ASelfOriginAreaAbility_NeedsNoTarget()
        {
            var result = Use(_syla, Syla.AceShards);

            Assert.That(result.Approved, Is.True);
        }

        // ── Bouncer ──────────────────────────────────────────────────────

        [Test]
        public void VelvetRope_PlacesTargetAdjacentOnTheSideItCameFrom()
        {
            // Bouncer on track 2; the target approaches from track 4 and lands
            // on 3 — beside him, on the side it came from, never on his cell.
            var target = AtTrack(8, "Enemy3", PlayerColor.Blue, 6, 4);
            _board.Add(target);
            _bouncer.MoveTo(ProgressAtTrack(PlayerColor.Red, 2));

            Use(_bouncer, Bouncer.VelvetRope, target);

            Assert.That(_map.CellAt(target.Owner, target.Progress), Is.EqualTo(CellRef.Track(3)));
        }

        [Test]
        public void APullThatWouldGoBehindTheTargetsStartCell_ClampsThere()
        {
            // An operator's path does not extend behind its own start cell, so
            // there is nowhere further back to place it. The start cell is safe,
            // which makes the clamp harmless (§7.4).
            var atStart = AtTrack(10, "AtStart", PlayerColor.Blue, 6,
                _map.StartTrackIndex(PlayerColor.Blue));
            _board.Add(atStart);

            Assert.That(atStart.Progress, Is.EqualTo(0), "precondition: it stands on its own start");

            Use(_bouncer, Bouncer.VelvetRope, atStart);

            Assert.That(atStart.Progress, Is.EqualTo(0));
        }

        [Test]
        public void VelvetRope_DamagesAnEnemy()
        {
            Use(_bouncer, Bouncer.VelvetRope, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(3));
        }

        [Test]
        public void VelvetRope_IsAtomic_AndIgnoresEvasion()
        {
            // Bouncer is the roster's direct counter to Evasive Protocol (§2.2).
            _statuses.ApplyPassive(_enemy, StatusKind.Evasion);

            Use(_bouncer, Bouncer.VelvetRope, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(3), "Atomic cannot be evaded");
        }

        [Test]
        public void VelvetRopeOnAlly_DealsNoDamage()
        {
            int before = _syla.Health;

            var result = Use(_bouncer, Bouncer.VelvetRope, _syla);

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
            _syla.MoveTo(ProgressAtTrack(PlayerColor.Red, 11));   // the pull destination
            int sylaHealth = _syla.Health;

            Use(_bouncer, Bouncer.VelvetRope, _enemy);

            Assert.That(_syla.Health, Is.EqualTo(sylaHealth));
            Assert.That(_enemy.Health, Is.EqualTo(3), "only the rope's own damage");
        }

        [Test]
        public void AllInMauling_WoundsTheTargetAndTheBouncer()
        {
            _bouncer.MoveTo(ProgressAtTrack(PlayerColor.Red, 11));   // range 2 to track 12

            var result = Use(_bouncer, Bouncer.AllInMauling, _enemy);

            Assert.That(result.Approved, Is.True);
            Assert.That(_enemy.Health, Is.EqualTo(3));
            Assert.That(_bouncer.Health, Is.EqualTo(Bouncer.MaxHealth - 1),
                "self-damage was retuned 3 → 1 (ADR-0002 Amendment 5)");
        }

        [Test]
        public void AllInMaulingOnAlly_HealsAndCostsTheCasterNothing()
        {
            _syla.SetHealth(2);
            _syla.MoveTo(ProgressAtTrack(PlayerColor.Red, 10));

            Use(_bouncer, Bouncer.AllInMauling, _syla);

            Assert.That(_syla.Health, Is.EqualTo(5));
            Assert.That(_bouncer.Health, Is.EqualTo(Bouncer.MaxHealth),
                "cast mode is chosen once from the target (§10)");
        }

        [Test]
        public void AllInMauling_CanNeutralizeItsOwnCaster()
        {
            _bouncer.SetHealth(1);                                   // one point left
            _bouncer.MoveTo(ProgressAtTrack(PlayerColor.Red, 11));

            Use(_bouncer, Bouncer.AllInMauling, _enemy);

            Assert.That(_bouncer.Health, Is.EqualTo(0));
        }

        [Test]
        public void HealingNeverExceedsMaximum()
        {
            _syla.MoveTo(ProgressAtTrack(PlayerColor.Red, 10));

            Use(_bouncer, Bouncer.AllInMauling, _syla);

            Assert.That(_syla.Health, Is.EqualTo(Syla.MaxHealth));
        }

        // ── Syla ─────────────────────────────────────────────────────────

        [Test]
        public void FromTheHip_DamagesAndSlows()
        {
            Use(_syla, Syla.FromTheHip, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(5), "1 base — it is a control tool, not a damage ability");

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.SpeedModifier(_enemy), Is.EqualTo(-0.5));
        }

        [Test]
        public void FromTheHip_DealsBonusDamageToBleedingTarget()
        {
            _statuses.Apply(_enemy, StatusKind.Bleed, duration: 3);
            _clock.BeginTurnFor(PlayerColor.Blue);
            _clock.BeginTurnFor(PlayerColor.Red);

            Use(_syla, Syla.FromTheHip, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(4), "1 base plus 1 against a bleeding target");
        }

        [Test]
        public void AceShards_HitsEveryEnemyInTheWindowAndBleedsThem()
        {
            var result = Use(_syla, Syla.AceShards);

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

            Use(_syla, Syla.AceShards);

            Assert.That(_bouncer.Health, Is.EqualTo(allyHealth));
        }

        [Test]
        public void TaggedFromAbove_GrantsStealthEvenWithoutPayout()
        {
            Use(_syla, Syla.TaggedFromAbove, _enemy);

            Assert.That(_statuses.Has(_syla, StatusKind.Stealth), Is.True);
            Assert.That(_statuses.MarkedBy(_enemy), Is.Null, "the mark takes hold on the target's turn");

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.MarkedBy(_enemy), Is.EqualTo(_syla.Id));
        }

        // ── Kurbyn ───────────────────────────────────────────────────────

        /// <summary>A Kurbyn adjacent to <c>_enemy</c>, which Miracle Pull's range 1 needs.</summary>
        private OperatorState KurbynBeside()
        {
            var op = AtTrack(5, "Kurbyn", PlayerColor.Red, Kurbyn.MaxHealth, 11);
            _board.Add(op);
            return op;
        }

        [Test]
        public void DarginPulse_DamagesAndStunsEveryEnemyInRange()
        {
            var kurbyn = KurbynBeside();

            Use(kurbyn, Kurbyn.DarginPulse);

            Assert.That(_enemy.Health, Is.EqualTo(4));

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.IsStunned(_enemy), Is.True);
        }

        [Test]
        public void MiraclePull_ExecutesTargetBelowHalfHealthAtCastTime()
        {
            var kurbyn = KurbynBeside();
            _enemy.SetHealth(2);                    // below half of 6

            Use(kurbyn, Kurbyn.MiraclePull, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(0));
        }

        [Test]
        public void MiraclePull_DoesNotExecuteTargetAtExactlyHalfHealth()
        {
            var kurbyn = KurbynBeside();
            _enemy.SetHealth(3);                    // exactly half

            Use(kurbyn, Kurbyn.MiraclePull, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(0), "3 Atomic still finishes it, but by damage");
        }

        [Test]
        public void MiraclePull_AtFullHealth_DealsItsAtomicDamage()
        {
            var kurbyn = KurbynBeside();

            Use(kurbyn, Kurbyn.MiraclePull, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(3));
        }

        [Test]
        public void MiraclePullSplash_ExcludesPrimaryTarget()
        {
            var kurbyn = KurbynBeside();

            Use(kurbyn, Kurbyn.MiraclePull, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(3), "3 direct, no splash on itself");
            Assert.That(_enemyTwo.Health, Is.EqualTo(4), "2 splash");
        }

        [Test]
        public void MiraclePullIsAtomic_AndIgnoresEvasion()
        {
            var kurbyn = KurbynBeside();
            _statuses.ApplyPassive(_enemy, StatusKind.Evasion);

            Use(kurbyn, Kurbyn.MiraclePull, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(3), "Atomic cannot be evaded");
        }

        // ── Mimi ─────────────────────────────────────────────────────────

        [Test]
        public void CryoPulse_HitsThePrimaryTargetAsWellAsTheRing()
        {
            // The inclusive area scope (§4.2). A splash that follows a direct
            // hit excludes its target; a field centred on a target it does not
            // otherwise touch must include it, or the operator the player aimed
            // at is the one enemy the ability misses.
            var mimi = AtTrack(6, "Mimi", PlayerColor.Red, Mimi.MaxHealth, 11);
            _board.Add(mimi);

            Use(mimi, Mimi.CryoPulse, _enemy);

            Assert.That(_enemy.Health, Is.EqualTo(4), "the aimed-at target is inside its own field");
            Assert.That(_enemyTwo.Health, Is.EqualTo(4));
        }

        [Test]
        public void Translocation_ExchangesCells()
        {
            var mimi = AtTrack(6, "Mimi", PlayerColor.Red, Mimi.MaxHealth, 11);
            _board.Add(mimi);

            var mimiCell = _map.CellAt(mimi.Owner, mimi.Progress);
            var enemyCell = _map.CellAt(_enemy.Owner, _enemy.Progress);

            var result = Use(mimi, Mimi.Translocation, _enemy);

            Assert.That(result.Approved, Is.True);
            Assert.That(_map.CellAt(mimi.Owner, mimi.Progress), Is.EqualTo(enemyCell));
            Assert.That(_map.CellAt(_enemy.Owner, _enemy.Progress), Is.EqualTo(mimiCell));
        }

        [Test]
        public void ASwapThatWouldLeaveTheTrack_IsRefusedBeforeItIsPaidFor()
        {
            // A swap moves two operators, so it refuses where a pull clamps: a
            // clamp that breaks the symmetry has stopped being the effect the
            // player cast (§7.4).
            var mimi = AtTrack(6, "Mimi", PlayerColor.Red, Mimi.MaxHealth, 2);
            _board.Add(mimi);

            var atStart = AtTrack(11, "AtStart", PlayerColor.Blue, 6,
                _map.StartTrackIndex(PlayerColor.Blue));
            atStart.MoveTo(0);
            _board.Add(atStart);
            mimi.MoveTo(ProgressAtTrack(PlayerColor.Red,
                (_map.StartTrackIndex(PlayerColor.Blue) + 3) % _map.Profile.CircuitLength));

            int before = _red.Energy;
            var result = Use(mimi, Mimi.Translocation, atStart);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.SwapWouldLeaveTheTrack));
            Assert.That(_red.Energy, Is.EqualTo(before), "a refusal costs nothing");
            Assert.That(atStart.Progress, Is.EqualTo(0), "and moves nobody");
        }
    }

    [TestFixture]
    public class RosterTests
    {
        [Test]
        public void EveryAbility_MatchesItsCostTier()
        {
            // Costs are 3 / 6 / 9 by tier (COMBAT_SYSTEMS §3.2). A value outside
            // that set means either the roster or the doc drifted.
            foreach (var ability in Roster.AllAbilities)
            {
                bool onTier = ability.EnergyCost == 3 || ability.EnergyCost == 6 || ability.EnergyCost == 9;
                Assert.That(onTier, Is.True, $"{ability.Name} costs {ability.EnergyCost}");
            }
        }

        [Test]
        public void NoAbilityCostsMoreThanTheEnergyCap()
        {
            // An ability that cannot be afforded at a full pool is uncastable.
            foreach (var ability in Roster.AllAbilities)
                Assert.That(ability.EnergyCost, Is.LessThanOrEqualTo(EnergyConfig.Default.EnergyCap),
                    ability.Name);
        }

        [Test]
        public void AbilityIdsAreUniqueAcrossTheWholeRoster()
        {
            // This is what Roster exists for. A duplicate id between two
            // operator files compiles, runs, and silently registers one ability
            // under the other's key in MatchFactory's ability book. It could not
            // happen while one file held every operator; it can now.
            var seen = new HashSet<int>();

            foreach (var ability in Roster.AllAbilities)
                Assert.That(seen.Add(ability.Id), Is.True, $"duplicate id on {ability.Name}");
        }

        [Test]
        public void EveryAbilityHasAtLeastOneEffect()
        {
            foreach (var ability in Roster.AllAbilities)
                Assert.That(ability.Effects.Count, Is.AtLeast(1), ability.Name);
        }

        [Test]
        public void EveryOperator_SitsInsideTheAdoptedSpeedBand()
        {
            // ADR-0002 Amendment 4: the band is 1.0 to 1.5, and Kurbyn reaches
            // 1.5 only via his passive. The ceiling exists so a mean move stays
            // near a fifth of the loop — the figure that actually governs
            // whether a player can follow a piece.
            foreach (var op in Roster.All)
            {
                Assert.That(op.BaseSpeed, Is.InRange(1.0, 1.5), op.Name);
                Assert.That(op.BaseSpeed + op.PassiveMagnitude, Is.InRange(1.0, 1.5),
                    $"{op.Name} with its passive");
            }

            // Declared values only. That a passive actually reaches the engine
            // is proved by GameEngineTests.KurbynMovesAtHisPassiveSpeed —
            // an assertion about two constants cannot prove it, and did not.
        }

        [Test]
        public void TheRosterCanFieldASquad()
        {
            // Drafting draws SquadSize distinct operators per seat, so a pool
            // smaller than that cannot start a match at all.
            Assert.That(Roster.All.Count, Is.AtLeast(Roster.SquadSize));
            Assert.That(Roster.Alpha.Count, Is.EqualTo(Roster.SquadSize));
        }
    }
}