// Assets/Tests/EditMode/Abilities/LukaTests.cs
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
    /// Luka's kit: Blind Spot's teleport and conditional follow-up (§6.5), Hermes'
    /// Ring against the Tech damage type (§2.2, §5.12), and Vendetta's
    /// critical blows (§2.4).
    /// </summary>
    [TestFixture]
    public class LukaTests
    {
        /// <summary>Always rolls the same value, so a critical is a decision rather than a seed hunt.</summary>
        private sealed class FixedRoll : IRandom
        {
            private readonly double _value;
            public FixedRoll(double value) { _value = value; }
            public int NextInt(int min, int max) => min;
            public double NextDouble() => _value;
        }

        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;
        private EnergyLedger _energy;
        private DamagePipeline _damage;
        private DeferredCellEffects _cellEffects;
        private DeferredOperatorEffects _operatorEffects;
        private AbilityResolver _abilities;
        private NeutralizeRules _neutralize;

        private OperatorState _luka;
        private OperatorState _ally;
        private OperatorState _sanity;
        private OperatorState _target;
        private OperatorState _bystander;
        private OperatorState _heavy;
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
            _damage = new DamagePipeline(_statuses, new SeededRandom(1));
            _cellEffects = new DeferredCellEffects(_clock, _targeting, _damage, _statuses);
            _operatorEffects = new DeferredOperatorEffects(_clock, _targeting, _damage, _statuses);

            // No random source: nothing here crits unless a test builds a
            // resolver that can (WithRoll).
            _abilities = new AbilityResolver(
                _map, _clock, _energy, _statuses, _targeting, _damage, _cellEffects, _operatorEffects);

            // Positions are TRACK cells, never progress, and none is a start
            // cell (multiples of 13), so nothing here is safe. Luka at 15; an
            // ally behind him at 14; the target two ahead at 17 with an enemy
            // on the way at 16; a heavy enemy three behind at 12; Sanity at
            // 19, inside Zero-Day's range of the target.
            _luka = AtTrack(1, "Luka", PlayerColor.Red, Luka.MaxHealth, 15);
            _ally = AtTrack(2, "Ally", PlayerColor.Red, 6, 14);
            _sanity = AtTrack(3, "Sanity", PlayerColor.Red, Sanity.MaxHealth, 19);
            _target = AtTrack(4, "Target", PlayerColor.Blue, 6, 17);
            _bystander = AtTrack(5, "Bystander", PlayerColor.Blue, 6, 16);
            _heavy = AtTrack(6, "Heavy", PlayerColor.Blue, 9, 12);

            _red = new PlayerState(PlayerColor.Red, new[] { _luka, _ally, _sanity });
            _blue = new PlayerState(PlayerColor.Blue, new[] { _target, _bystander, _heavy });
            _board = new List<OperatorState> { _luka, _ally, _sanity, _target, _bystander, _heavy };

            _neutralize = new NeutralizeRules(
                _statuses, _abilities, _energy, _board,
                new PlayerState[] { _red, _blue }, CombatConfig.Default, _operatorEffects);

            _clock.BeginTurnFor(PlayerColor.Red);
            _red.BeginTurn();
            Fund(12);
        }

        private OperatorState AtTrack(int id, string name, PlayerColor owner, int hp, int track)
        {
            var op = new OperatorState(id, name, owner, hp, 1.0);
            op.MoveTo(ProgressAtTrack(owner, track));
            return op;
        }

        private int ProgressAtTrack(PlayerColor owner, int track)
        {
            int circuit = _map.Profile.CircuitLength;
            int start = _map.StartTrackIndex(owner);

            return ((track - start) % circuit + circuit) % circuit;
        }

        private int TrackOf(OperatorState op) => _map.CellAt(op.Owner, op.Progress).Index;

        /// <summary>Tops Red's pool up to a known figure without going through the dice.</summary>
        private void Fund(int amount)
        {
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6
            _red.BeginTurn();
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6, capped at 12
            if (amount < 12) _energy.Spend(_red, 12 - amount);
        }

        private AbilityResolver WithRoll(double roll) =>
            new AbilityResolver(
                _map, _clock, _energy, _statuses, _targeting, _damage,
                _cellEffects, _operatorEffects, new FixedRoll(roll));

        private AbilityResolution CastBlindSpot(OperatorState target) =>
            _abilities.Use(_luka, Luka.BlindSpot, target, _red, _board);

        /// <summary>Past the target's turn to Red's next upkeep, when a follow-up comes due.</summary>
        private void AdvanceToCasterUpkeep()
        {
            _clock.BeginTurnFor(PlayerColor.Blue);
            _clock.BeginTurnFor(PlayerColor.Red);
        }

        private IReadOnlyList<OperatorEffectResolution> Fire() =>
            _operatorEffects.Fire(PlayerColor.Red, _board);

        // ── Blind Spot: the teleport and the first strike ─────────────────────────

        [Test]
        public void BlindSpot_TeleportsOneCellPastTheTarget_AndStrikesItForTwo()
        {
            var result = CastBlindSpot(_target);

            Assert.That(result.Approved, Is.True);
            Assert.That(TrackOf(_luka), Is.EqualTo(18), "Collision's landing, reused (§7.6)");
            Assert.That(_target.Health, Is.EqualTo(4));
        }

        [Test]
        public void BlindSpot_StrikesNobodyOnTheWay()
        {
            // A teleport with no path damage must not report a zero hit on
            // every enemy it passes.
            var result = CastBlindSpot(_target);

            Assert.That(_bystander.Health, Is.EqualTo(6));
            Assert.That(result.Outcomes.Any(o =>
                o.Kind == EffectOutcomeKind.Damaged && ReferenceEquals(o.Recipient, _bystander)), Is.False);
        }

        [Test]
        public void BlindSpot_TeleportsBackwards_ToATargetBehindHim()
        {
            CastBlindSpot(_heavy);

            Assert.That(TrackOf(_luka), Is.EqualTo(11), "one past the target in the dash direction");
        }

        [Test]
        public void BlindSpot_MarksTheTarget_AndNothingElseResolvesYet()
        {
            var result = CastBlindSpot(_target);

            Assert.That(result.Outcomes.Any(o => o.Kind == EffectOutcomeKind.FollowUpMarked), Is.True,
                "the telegraph the counterplay depends on");
            Assert.That(_operatorEffects.HasFollowUpOn(_target, PlayerColor.Red), Is.True);
            Assert.That(_operatorEffects.HasChargeOn(_target, PlayerColor.Red), Is.False,
                "a follow-up is not a charge");

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.Has(_target, StatusKind.Hunted), Is.True,
                "the marker takes hold on the target's next turn, like any debuff");
        }

        [Test]
        public void BlindSpot_AimedAtAnAlly_IsRefusedAsWrongSide()
        {
            int before = _red.Energy;

            var result = CastBlindSpot(_ally);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.WrongSide));
            Assert.That(_red.Energy, Is.EqualTo(before), "a refusal costs nothing");
            Assert.That(TrackOf(_luka), Is.EqualTo(15), "and moves nobody");
        }

        // ── Blind Spot: the follow-up ─────────────────────────────────────────────

        [Test]
        public void FollowUp_LandsForOne_WhenTheTargetStaysClose()
        {
            CastBlindSpot(_target);

            AdvanceToCasterUpkeep();
            var fired = Fire();

            Assert.That(fired.Count, Is.EqualTo(1));
            Assert.That(fired[0].HitSomething, Is.True);
            Assert.That(fired[0].Cause, Is.EqualTo(DeferredOperatorEffects.FollowUpCause));
            Assert.That(fired[0].MarkedTargetBonus, Is.EqualTo(0), "not a heavy target");
            Assert.That(_target.Health, Is.EqualTo(3), "two from the cast, one from the follow-up");
            Assert.That(_bystander.Health, Is.EqualTo(6), "the target alone, never a blast");
        }

        [Test]
        public void FollowUp_DealsTwo_ToAHeavyTarget()
        {
            // Heavy is maximum health above 6 — the Bouncer and Sanity today.
            CastBlindSpot(_heavy);
            Assert.That(_heavy.Health, Is.EqualTo(7), "precondition: the cast's two");

            AdvanceToCasterUpkeep();
            var fired = Fire();

            Assert.That(fired[0].MarkedTargetBonus, Is.EqualTo(1));
            Assert.That(_heavy.Health, Is.EqualTo(5));
        }

        [Test]
        public void FollowUp_ReachesTwoCellsAway_InEitherDirection()
        {
            CastBlindSpot(_target);                                      // Luka lands on 18
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 20)); // two ahead of him

            AdvanceToCasterUpkeep();
            var fired = Fire();

            Assert.That(fired[0].HitSomething, Is.True, "exactly at reach still lands");
            Assert.That(_target.Health, Is.EqualTo(3));
        }

        [Test]
        public void FollowUp_Misses_WhenTheTargetGotClear()
        {
            // The counterplay: three cells from Luka is out of reach. A miss
            // still reports, so the escape is visible.
            CastBlindSpot(_target);                                      // Luka lands on 18
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 21));

            AdvanceToCasterUpkeep();
            var fired = Fire();

            Assert.That(fired.Count, Is.EqualTo(1), "a miss is reported, not swallowed");
            Assert.That(fired[0].HitSomething, Is.False);
            Assert.That(fired[0].Target, Is.SameAs(_target), "the view has to say who got away");
            Assert.That(fired[0].MarkedTargetBonus, Is.EqualTo(0));
            Assert.That(_target.Health, Is.EqualTo(4), "only the cast's two");
            Assert.That(_statuses.Has(_target, StatusKind.Hunted), Is.False, "spent either way");
        }

        [Test]
        public void FollowUp_Misses_WhenLukaIsNoLongerOnTheBoard()
        {
            // Unlike a charge, the strike is Luka's own blow: measured from
            // him, so a yarded Luka cannot land it.
            CastBlindSpot(_target);

            _damage.Apply(_luka, new DamageInstance(Luka.MaxHealth, DamageType.Atomic, _target.Id, "ability"));
            _neutralize.Apply(_luka, _target.Id);
            Assert.That(_luka.IsInYard, Is.True, "precondition");

            AdvanceToCasterUpkeep();
            var fired = Fire();

            Assert.That(fired.Count, Is.EqualTo(1));
            Assert.That(fired[0].HitSomething, Is.False);
            Assert.That(_target.Health, Is.EqualTo(4));
        }

        [Test]
        public void FollowUp_CleanseCancelsIt()
        {
            CastBlindSpot(_target);

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.Has(_target, StatusKind.Hunted), Is.True, "precondition");

            _statuses.ClearApplied(_target);

            _clock.BeginTurnFor(PlayerColor.Red);
            var fired = Fire();

            Assert.That(fired, Is.Empty, "a cleansed follow-up never resolves");
            Assert.That(_operatorEffects.HasFollowUpOn(_target, PlayerColor.Red), Is.False);
            Assert.That(_target.Health, Is.EqualTo(4));
        }

        [Test]
        public void FollowUp_AndZeroDay_OnOneTarget_BothResolve()
        {
            // Different markers, different entries: neither reads the other's
            // resolution as a cleanse.
            CastBlindSpot(_target);                                      // 6 → 4, Luka on 18
            var zeroDay = _abilities.Use(_sanity, Sanity.ZeroDay, _target, _red, _board);
            Assert.That(zeroDay.Approved, Is.True, "precondition");

            AdvanceToCasterUpkeep();
            var fired = Fire();

            Assert.That(fired.Count, Is.EqualTo(2));
            Assert.That(fired.Any(r => r.Cause == DeferredOperatorEffects.FollowUpCause && r.HitSomething), Is.True);
            Assert.That(fired.Any(r => r.Cause == DeferredOperatorEffects.ChargeCause), Is.True);
            Assert.That(_target.Health, Is.EqualTo(1), "4, less the charge's two and the follow-up's one");
            Assert.That(_statuses.Has(_target, StatusKind.Hunted), Is.False);
            Assert.That(_statuses.Has(_target, StatusKind.ZeroDayCharge), Is.False);
        }

        // ── Hermes' Ring and Tech ────────────────────────────────────────

        [Test]
        public void HermesRing_BlocksTech_ButNotNormalOrAtomic()
        {
            var result = _abilities.Use(_luka, Luka.HermesRing, null, _red, _board);
            Assert.That(result.Approved, Is.True);
            Assert.That(_statuses.BlocksTech(_luka), Is.True, "a self-buff takes hold at once");

            var tech = _damage.Apply(_luka, new DamageInstance(2, DamageType.Tech, _target.Id, "ability"));
            Assert.That(tech.Outcome, Is.EqualTo(DamageOutcome.Absorbed));
            Assert.That(_luka.Health, Is.EqualTo(Luka.MaxHealth));

            _damage.Apply(_luka, new DamageInstance(1, DamageType.Normal, _target.Id, "ability"));
            _damage.Apply(_luka, new DamageInstance(1, DamageType.Atomic, _target.Id, "ability"));
            Assert.That(_luka.Health, Is.EqualTo(Luka.MaxHealth - 2));
        }

        [Test]
        public void TurnsUntilReady_CountsDownToTheTurnTheAbilityReturns()
        {
            Assert.That(_abilities.TurnsUntilReady(_luka, Luka.HermesRing), Is.EqualTo(0), "never cast");

            _abilities.Use(_luka, Luka.HermesRing, null, _red, _board);   // cooldown 4
            Assert.That(_abilities.TurnsUntilReady(_luka, Luka.HermesRing), Is.EqualTo(5),
                "sits out four turns, ready on the fifth");

            for (int expected = 4; expected >= 0; expected--)
            {
                _clock.BeginTurnFor(PlayerColor.Blue);
                _clock.BeginTurnFor(PlayerColor.Red);

                Assert.That(_abilities.TurnsUntilReady(_luka, Luka.HermesRing), Is.EqualTo(expected));
                Assert.That(_abilities.IsReady(_luka, Luka.HermesRing), Is.EqualTo(expected == 0),
                    "agrees with IsReady");
            }
        }

        [Test]
        public void HermesRing_CoversTwoFullRounds_ThenExpires()
        {
            _abilities.Use(_luka, Luka.HermesRing, null, _red, _board);

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.BlocksTech(_luka), Is.True, "first round of opponents");

            _clock.BeginTurnFor(PlayerColor.Red);
            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.BlocksTech(_luka), Is.True, "second round of opponents");

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_statuses.ExpireCompleted(_luka), Does.Contain(StatusKind.TechWard),
                "gone at the end of his second turn after the cast");
        }

        [Test]
        public void CryoPulse_IsTech_AndAWardedLukaTakesNoneOfIt()
        {
            // The ward blocks the damage, not the statuses riding with it.
            var mimi = AtTrack(7, "Mimi", PlayerColor.Blue, Mimi.MaxHealth, 18);
            var blue = new PlayerState(PlayerColor.Blue, new[] { mimi });
            var board = new List<OperatorState>(_board) { mimi };

            _abilities.Use(_luka, Luka.HermesRing, null, _red, board);

            _clock.BeginTurnFor(PlayerColor.Blue);
            blue.BeginTurn();
            _energy.GrantForTurn(blue, new DiceRoll(6, 6));

            var result = _abilities.Use(mimi, Mimi.CryoPulse, _luka, blue, board);

            Assert.That(result.Approved, Is.True, "precondition");
            Assert.That(_luka.Health, Is.EqualTo(Luka.MaxHealth), "the ward ate the Tech hit");
            Assert.That(_ally.Health, Is.EqualTo(4), "an unwarded ally in the field did not");

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_statuses.IsBleeding(_luka), Is.True, "the bleed still lands");
        }

        [Test]
        public void TechSources_AreTheListTheDesignerChose()
        {
            // The ring is worth exactly this list (§2.2). A change to it is a
            // design decision, so it has to show up as a failing test. Kian's
            // Inversion Matrix and Sonic Disrupter joined on 2026-09-17.
            var tech = Roster.AllAbilities
                .Where(a => a.Effects.Any(e => e.DamageType == DamageType.Tech))
                .Select(a => a.Name)
                .OrderBy(n => n)
                .ToList();

            Assert.That(tech, Is.EqualTo(new[]
                { "Cryo-Pulse", "Drone Strike", "Inversion Matrix", "Sonic Disrupter", "Zero-Day" }));
        }

        [Test]
        public void ZeroDay_IsTech_AndAWardedLukaTakesNoneOfTheBlast()
        {
            // An enemy engineer rides a charge on Luka himself: neither the
            // splash nor the marked-target bonus gets through the ward. The
            // unwarded ally beside him takes the splash, and the slow still
            // lands on both, as Cryo-Pulse's bleed does.
            var engineer = AtTrack(7, "Engineer", PlayerColor.Blue, Sanity.MaxHealth, 16);
            var blue = new PlayerState(PlayerColor.Blue, new[] { engineer });
            var board = new List<OperatorState>(_board) { engineer };

            _abilities.Use(_luka, Luka.HermesRing, null, _red, board);

            _clock.BeginTurnFor(PlayerColor.Blue);
            blue.BeginTurn();
            _energy.GrantForTurn(blue, new DiceRoll(6, 6));

            var attach = _abilities.Use(engineer, Sanity.ZeroDay, _luka, blue, board);
            Assert.That(attach.Approved, Is.True, "precondition");

            _clock.BeginTurnFor(PlayerColor.Red);
            _clock.BeginTurnFor(PlayerColor.Blue);
            var fired = _operatorEffects.Fire(PlayerColor.Blue, board);

            Assert.That(fired.Count, Is.EqualTo(1));
            Assert.That(fired[0].Caught, Does.Contain(_luka), "he is in the blast");
            Assert.That(_luka.Health, Is.EqualTo(Luka.MaxHealth), "the ward ate splash and bonus");
            Assert.That(_ally.Health, Is.EqualTo(5), "an unwarded ally took the splash");

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_statuses.Has(_luka, StatusKind.Slow), Is.True, "the slow still lands");
        }

        [Test]
        public void DroneStrike_IsTech_AndAWardedLukaStillCountsTowardTheSplit()
        {
            // The beam divides before any hit reaches the pipeline, so Luka's
            // share is blocked rather than passed on to the ally beside him.
            var kian = AtTrack(7, "Kian", PlayerColor.Blue, Kian.MaxHealth, 30);
            var blue = new PlayerState(PlayerColor.Blue, new[] { kian });
            var board = new List<OperatorState>(_board) { kian };

            _abilities.Use(_luka, Luka.HermesRing, null, _red, board);

            _clock.BeginTurnFor(PlayerColor.Blue);
            blue.BeginTurn();
            _energy.GrantForTurn(blue, new DiceRoll(6, 6));

            var paint = _abilities.Use(kian, Kian.DroneStrike, null, blue, board, CellRef.Track(15));
            Assert.That(paint.Approved, Is.True, "precondition");

            _clock.BeginTurnFor(PlayerColor.Red);
            _clock.BeginTurnFor(PlayerColor.Blue);
            var fired = _cellEffects.Fire(PlayerColor.Blue, board);

            Assert.That(fired.Count, Is.EqualTo(1));
            Assert.That(fired[0].Caught.Count, Is.EqualTo(2), "Luka on the square, his ally beside it");
            Assert.That(fired[0].DamagePerTarget, Is.EqualTo(2), "four split two ways, ward or no ward");
            Assert.That(_luka.Health, Is.EqualTo(Luka.MaxHealth), "his share is blocked");
            Assert.That(_ally.Health, Is.EqualTo(4), "and not passed on");
        }

        // ── Vendetta ─────────────────────────────────────────────────────

        [Test]
        public void Vendetta_WithoutACrit_IsThreeAtomicBlowsOfOne_ThroughEveryDefence()
        {
            _statuses.Apply(_target, StatusKind.Shield, duration: 2, magnitude: 2);
            _statuses.ApplyPassive(_target, StatusKind.Evasion);

            var result = WithRoll(0.99).Use(_luka, Luka.Vendetta, _target, _red, _board);

            var hits = result.Outcomes.Where(o => o.Kind == EffectOutcomeKind.Damaged).ToList();
            Assert.That(hits.Count, Is.EqualTo(3));
            Assert.That(hits.All(h => h.Damage.Cause == AbilityResolver.AbilityCause), Is.True);
            Assert.That(_target.Health, Is.EqualTo(3));
        }

        [Test]
        public void Vendetta_WithoutARandomSource_NeverCrits()
        {
            _abilities.Use(_luka, Luka.Vendetta, _target, _red, _board);

            Assert.That(_target.Health, Is.EqualTo(3));
        }

        [Test]
        public void Vendetta_ARollOfExactlyTheChance_IsNotACrit()
        {
            WithRoll(0.1).Use(_luka, Luka.Vendetta, _target, _red, _board);

            Assert.That(_target.Health, Is.EqualTo(3), "under the chance crits; equal to it does not");
        }

        [Test]
        public void Vendetta_ACritDoublesTheBlow()
        {
            _target.SetHealth(6);
            var result = WithRoll(0.0).Use(_luka, Luka.Vendetta, _target, _red, _board);

            var first = result.Outcomes.First(o => o.Kind == EffectOutcomeKind.Damaged);
            Assert.That(first.Damage.AmountApplied, Is.EqualTo(2));
            Assert.That(first.Damage.Cause, Is.EqualTo(AbilityResolver.CriticalCause));
        }

        [Test]
        public void Vendetta_ACritTriples_AgainstAHeavyTarget()
        {
            var result = WithRoll(0.0).Use(_luka, Luka.Vendetta, _heavy, _red, _board);

            var hits = result.Outcomes.Where(o => o.Kind == EffectOutcomeKind.Damaged).ToList();
            Assert.That(hits[0].Damage.AmountApplied, Is.EqualTo(3));
            Assert.That(_heavy.Health, Is.EqualTo(0), "three triples fell a nine");
        }

        [Test]
        public void Vendetta_StopsStriking_ATargetItAlreadyDowned()
        {
            // One kill is one neutralize and one bounty. The engine yards the
            // target only after the cast, so the resolver must not strike a
            // downed target again.
            _target.SetHealth(2);

            var result = WithRoll(0.0).Use(_luka, Luka.Vendetta, _target, _red, _board);

            var hits = result.Outcomes.Where(o => o.Kind == EffectOutcomeKind.Damaged).ToList();
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].Damage.Outcome, Is.EqualTo(DamageOutcome.Neutralized));
        }

        // ── Vendetta: lifesteal (§2.5, 2026-09-17) ───────────────────────

        private static int HealedBy(AbilityResolution result, OperatorState who) =>
            result.Outcomes.Where(o => o.Kind == EffectOutcomeKind.Healed && ReferenceEquals(o.Recipient, who))
                .Sum(o => o.Amount);

        [Test]
        public void Vendetta_EveryBlowFeedsLukaWhatItTook()
        {
            _luka.SetHealth(2);

            var result = _abilities.Use(_luka, Luka.Vendetta, _target, _red, _board);

            Assert.That(_target.Health, Is.EqualTo(3));
            Assert.That(_luka.Health, Is.EqualTo(5), "three blows of 1, three points back");
            Assert.That(result.Outcomes.Count(o => o.Kind == EffectOutcomeKind.Healed), Is.EqualTo(3),
                "one heal per blow, so the view can pulse each one");
        }

        [Test]
        public void Vendetta_ACritDrainsTheWholeCrit()
        {
            _luka.SetHealth(1);

            var result = WithRoll(0.0).Use(_luka, Luka.Vendetta, _heavy, _red, _board);

            Assert.That(_heavy.Health, Is.EqualTo(0));
            Assert.That(_luka.Health, Is.EqualTo(Luka.MaxHealth), "nine drained, capped at seven");
            Assert.That(HealedBy(result, _luka), Is.EqualTo(Luka.MaxHealth - 1), "reports what he gained, not what he drained");
        }

        [Test]
        public void Vendetta_OverkillDrainsNothing()
        {
            // A 2-health target and a crit of 2 on the first blow: 2 removed,
            // and the other two blows are never thrown.
            _luka.SetHealth(1);
            _target.SetHealth(1);

            var result = WithRoll(0.0).Use(_luka, Luka.Vendetta, _target, _red, _board);

            Assert.That(_luka.Health, Is.EqualTo(2), "the blow removed 1 health, not the 2 it was worth");
            Assert.That(HealedBy(result, _luka), Is.EqualTo(1));
        }

        [Test]
        public void Vendetta_AtFullHealth_ReportsNoHeal()
        {
            var result = _abilities.Use(_luka, Luka.Vendetta, _target, _red, _board);

            Assert.That(_luka.Health, Is.EqualTo(Luka.MaxHealth));
            Assert.That(result.Outcomes.Any(o => o.Kind == EffectOutcomeKind.Healed), Is.False);
        }

        [Test]
        public void Lifesteal_DrainsOnlyWhatGetsThrough()
        {
            // A Normal drain against a plate: 3 dealt into a 2-point pool, 1
            // removed, 1 healed. Vendetta is Atomic, so this pins the rule
            // for whoever gets a mitigable drain next.
            var drain = new AbilityDefinition(
                id: 99901, name: "Test Drain", description: "Test double.",
                energyCost: 0, cooldownTurns: 0, range: 3,
                effects: new[]
                {
                    AbilityEffect.Damage(EffectScope.PrimaryTarget, 3, DamageType.Normal).WithLifesteal()
                });
            _luka.SetHealth(2);
            // Plated on Blue's own turn, so the plate is up when Red strikes.
            _clock.BeginTurnFor(PlayerColor.Blue);
            _statuses.Apply(_target, StatusKind.Shield, duration: 2, magnitude: 2);
            _clock.BeginTurnFor(PlayerColor.Red);

            _abilities.Use(_luka, drain, _target, _red, _board);

            Assert.That(_target.Health, Is.EqualTo(5));
            Assert.That(_luka.Health, Is.EqualTo(3));
        }

        [Test]
        public void Lifesteal_IsRefusedOnAnythingButOutgoingDamage()
        {
            Assert.Throws<System.InvalidOperationException>(() =>
                AbilityEffect.Heal(EffectScope.Caster, 1).WithLifesteal());
            Assert.Throws<System.InvalidOperationException>(() =>
                AbilityEffect.Damage(EffectScope.Caster, 1, DamageType.Normal).WithLifesteal());
        }

        [Test]
        public void Lifesteal_SurvivesTheCriticalCopy_InEitherOrder()
        {
            var a = AbilityEffect.Damage(EffectScope.PrimaryTarget, 1, DamageType.Atomic)
                .WithLifesteal().WithCritical(0.1, 2);
            var b = AbilityEffect.Damage(EffectScope.PrimaryTarget, 1, DamageType.Atomic)
                .WithCritical(0.1, 2).WithLifesteal();

            Assert.That(a.Lifesteal && b.Lifesteal, Is.True);
            Assert.That(a.CritChance, Is.EqualTo(0.1));
            Assert.That(b.CritChance, Is.EqualTo(0.1));
            Assert.That(Luka.Vendetta.Effects.All(e => e.Lifesteal), Is.True);
        }

        // ── Roster ───────────────────────────────────────────────────────

        [Test]
        public void Luka_IsInThePool_WithTheDroppedNumbers()
        {
            var luka = Roster.ByName("Luka");

            Assert.That(luka.MaxHealth, Is.EqualTo(7), "6 until the roster-wide +1 of 2026-09-16");
            Assert.That(luka.BaseSpeed, Is.EqualTo(1.0));

            Assert.That(Luka.BlindSpot.EnergyCost, Is.EqualTo(5));
            Assert.That(Luka.BlindSpot.CooldownTurns, Is.EqualTo(3));
            Assert.That(Luka.BlindSpot.Range, Is.EqualTo(3));

            Assert.That(Luka.HermesRing.EnergyCost, Is.EqualTo(3));
            Assert.That(Luka.HermesRing.CooldownTurns, Is.EqualTo(4));
            Assert.That(Luka.HermesRing.RequiresTarget, Is.False);

            Assert.That(Luka.Vendetta.EnergyCost, Is.EqualTo(6));
            Assert.That(Luka.Vendetta.CooldownTurns, Is.EqualTo(3));
            Assert.That(Luka.Vendetta.Range, Is.EqualTo(3));
            Assert.That(Luka.Vendetta.Effects.Count, Is.EqualTo(3));
        }
    }
}
