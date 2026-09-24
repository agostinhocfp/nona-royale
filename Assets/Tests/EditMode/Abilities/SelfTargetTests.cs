// Assets/Tests/EditMode/Abilities/SelfTargetTests.cs
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
    /// Self-targeting is per-ability opt-in (COMBAT_SYSTEMS §10, settled
    /// 2026-09-17): <see cref="AbilityDefinition.AllowsSelfTarget"/> decides
    /// whether the caster is a legal target of its own cast. Javi's three
    /// abilities and Lethe's Nano Cell declare it; nothing else does — blanket
    /// self-cast was rejected because All-In Mauling's friendly mode is a
    /// heal, and Bouncer self-sustaining was never intended.
    /// </summary>
    /// <remarks>
    /// Wired from the services, like <c>AbilityResolverTests</c>. The FakeClock
    /// lives in that fixture's file, same namespace.
    /// </remarks>
    [TestFixture]
    public class SelfTargetTests
    {
        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;
        private EnergyLedger _energy;
        private DamagePipeline _damage;
        private DeferredCellEffects _cellEffects;
        private AbilityResolver _abilities;

        private OperatorState _javi;
        private OperatorState _lethe;
        private OperatorState _bouncer;
        private OperatorState _foe;
        private OperatorState _foeTwo;
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
            _abilities = new AbilityResolver(
                _map, _clock, _energy, _statuses, _targeting, _damage, _cellEffects);

            // TRACK cells, never progress; none is a safe start cell (0, 13,
            // 26, 39 — indices divisible by PlayerStartOffset). The Red trio
            // clusters at 10–12 so every flagged ability reaches everyone it
            // should, and the foes sit at 8 (inside Nanite Infusion's 5) and
            // 15 (at its range-5 edge).
            _javi = AtTrack(1, "Javi", PlayerColor.Red, Javi.MaxHealth, 10);
            _lethe = AtTrack(2, "Lethe", PlayerColor.Red, Lethe.MaxHealth, 11);
            _bouncer = AtTrack(3, "Bouncer", PlayerColor.Red, Bouncer.MaxHealth, 12);
            _foe = AtTrack(4, "Foe", PlayerColor.Blue, 7, 8);
            _foeTwo = AtTrack(5, "Foe2", PlayerColor.Blue, 7, 15);

            _red = new PlayerState(PlayerColor.Red, new[] { _javi, _lethe, _bouncer });
            _blue = new PlayerState(PlayerColor.Blue, new[] { _foe, _foeTwo });
            _board = new List<OperatorState> { _javi, _lethe, _bouncer, _foe, _foeTwo };

            _clock.BeginTurnFor(PlayerColor.Red);
            _red.BeginTurn();
            Fund(12);
        }

        // ── Helpers ──────────────────────────────────────────────────────

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

        private void Fund(int amount)
        {
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6
            _red.BeginTurn();
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6, capped at 12
            if (amount < 12) _energy.Spend(_red, 12 - amount);
        }

        private AbilityResolution Use(OperatorState caster, AbilityDefinition ability, OperatorState target) =>
            _abilities.Use(caster, ability, target, _red, _board);

        private IReadOnlyList<OperatorState> Legal(OperatorState caster, AbilityDefinition ability) =>
            _abilities.LegalTargets(caster, ability, _board);

        // ── The four opted-in abilities, cast on their own caster ────────

        [Test]
        public void NaniteInfusion_OnHimself_Heals_AndTheHostileModeStaysOff()
        {
            _javi.SetHealth(4);

            var result = Use(_javi, Javi.NaniteInfusion, _javi);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_javi.Health, Is.EqualTo(6),
                "the friendly cast heals 2; the enemy-mode 2 damage is scoped away, not redirected");
            Assert.That(_red.Energy, Is.EqualTo(12 - 3));
        }

        [Test]
        public void TraumaPlate_OnHimself_Shields()
        {
            var result = Use(_javi, Javi.TraumaPlate, _javi);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_statuses.ShieldPool(_javi), Is.EqualTo(2));
            Assert.That(_red.Energy, Is.EqualTo(12 - 4));
        }

        [Test]
        public void NeuralPurge_OnHimself_Cleanses()
        {
            // Not a stun: a stunned caster cannot cast at all (§5.1).
            _statuses.Apply(_javi, StatusKind.Slow, 2);
            _statuses.Apply(_javi, StatusKind.Bleed, 2);

            var result = Use(_javi, Javi.NeuralPurge, _javi);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_statuses.Has(_javi, StatusKind.Slow), Is.False);
            Assert.That(_statuses.Has(_javi, StatusKind.Bleed), Is.False);
            Assert.That(_red.Energy, Is.EqualTo(12 - 6));
        }

        [Test]
        public void NanoCell_OnHerself_Shields_AndCostsOnlyEnergy()
        {
            var result = Use(_lethe, Lethe.NanoCell, _lethe);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_statuses.ShieldPool(_lethe), Is.EqualTo(Lethe.NanoCellPool));
            Assert.That(_statuses.IsStunned(_lethe), Is.False, "no stun since 2026-09-24");
            Assert.That(_red.Energy, Is.EqualTo(12 - 4));
        }

        // ── The abilities that did not opt in ────────────────────────────

        [Test]
        public void AllInMauling_OnHimself_IsRefused_AndCostsNothing()
        {
            // The case that motivated opt-in over blanket: its friendly mode is
            // a heal, and Bouncer self-sustaining was never intended.
            var result = Use(_bouncer, Bouncer.AllInMauling, _bouncer);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.IllegalTarget));
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.CannotTargetSelf));
            Assert.That(_red.Energy, Is.EqualTo(12));
            Assert.That(_bouncer.Health, Is.EqualTo(Bouncer.MaxHealth), "no friendly heal either");
        }

        [Test]
        public void VelvetRope_OnHimself_IsRefused_AndStaysReady()
        {
            var result = Use(_bouncer, Bouncer.VelvetRope, _bouncer);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.IllegalTarget));
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.CannotTargetSelf));
            Assert.That(_red.Energy, Is.EqualTo(12));
            Assert.That(_abilities.IsReady(_bouncer, Bouncer.VelvetRope), Is.True,
                "a refusal never starts the cooldown");
        }

        [Test]
        public void ASelfCast_NamesTheSelf_BeforeItNamesThePrice()
        {
            // Energy is checked last, so an unaffordable self-cast still reports
            // the illegal target — the problem the player cannot pay to fix.
            Fund(0);

            var result = Use(_bouncer, Bouncer.AllInMauling, _bouncer);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.IllegalTarget));
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.CannotTargetSelf));
        }

        // ── LegalTargets ─────────────────────────────────────────────────

        [Test]
        public void LegalTargets_ExcludesTheCaster_ForUnflaggedAbilities()
        {
            Assert.That(Legal(_bouncer, Bouncer.VelvetRope), Has.No.Member(_bouncer));
            Assert.That(Legal(_bouncer, Bouncer.AllInMauling), Has.No.Member(_bouncer));
        }

        [Test]
        public void LegalTargets_IncludesTheCaster_ForTheFlaggedFour()
        {
            Assert.That(Legal(_javi, Javi.NaniteInfusion), Has.Member(_javi));
            Assert.That(Legal(_javi, Javi.TraumaPlate), Has.Member(_javi));
            Assert.That(Legal(_javi, Javi.NeuralPurge), Has.Member(_javi));
            Assert.That(Legal(_lethe, Lethe.NanoCell), Has.Member(_lethe));
        }

        [Test]
        public void LegalTargets_ForAFlaggedAbility_StillListsEveryoneElseItAlwaysDid()
        {
            var legal = Legal(_javi, Javi.NaniteInfusion);

            Assert.That(legal, Has.Member(_lethe), "an ally");
            Assert.That(legal, Has.Member(_bouncer), "an ally");
            Assert.That(legal, Has.Member(_foe), "an enemy — the hostile mode still casts");
            Assert.That(legal, Has.Member(_foeTwo), "an enemy at the range-5 edge");
            Assert.That(legal.Count, Is.EqualTo(5), "the caster joins the list; nobody leaves it");
        }

        // ── Ordinary casts regress nothing ───────────────────────────────

        [Test]
        public void AFlaggedAbility_OnAnAlly_StillResolvesAsItDid()
        {
            _lethe.SetHealth(4);

            var heal = Use(_javi, Javi.NaniteInfusion, _lethe);
            Assert.That(heal.Approved, Is.True, heal.ToString());
            Assert.That(_lethe.Health, Is.EqualTo(6));

            var plate = Use(_javi, Javi.TraumaPlate, _bouncer);
            Assert.That(plate.Approved, Is.True, plate.ToString());
            Assert.That(_statuses.ShieldPool(_bouncer), Is.EqualTo(2));
        }

        [Test]
        public void AFlaggedAbility_OnAnEnemy_StillResolvesAsItDid()
        {
            var result = Use(_javi, Javi.NaniteInfusion, _foe);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_foe.Health, Is.EqualTo(5), "2 Normal, as before");
        }

        [Test]
        public void ARangeRetune_KeepsTheOptIn()
        {
            // MatchFactory.Retune rebuilds every ability from its parts, and its
            // own remark warns that a field forgotten there is silently dropped
            // from every swept match.
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                [PlayerColor.Red] = new[] { Javi.Definition, Lethe.Definition, Bouncer.Definition }
            };

            var match = MatchFactory.Create(
                new[] { PlayerColor.Red }, seed: 1, squads: squads, abilityRangeBonus: 2);

            var javi = match.Operators.First(o => o.Name == "Javi");
            var abilities = match.AbilitiesByOperator[javi.Id];

            Assert.That(abilities.Single(a => a.Id == 501).AllowsSelfTarget, Is.True);
            Assert.That(abilities.Single(a => a.Id == 502).AllowsSelfTarget, Is.True);
            Assert.That(abilities.Single(a => a.Id == 503).AllowsSelfTarget, Is.True);
            Assert.That(abilities.Single(a => a.Id == 501).Range, Is.EqualTo(5 + 2),
                "the retune itself still applied");

            var lethe = match.Operators.First(o => o.Name == "Lethe");
            Assert.That(match.AbilitiesByOperator[lethe.Id].Single(a => a.Id == 1001).AllowsSelfTarget, Is.True);

            var bouncer = match.Operators.First(o => o.Name == "Bouncer");
            Assert.That(match.AbilitiesByOperator[bouncer.Id].Single(a => a.Id == 102).AllowsSelfTarget,
                Is.False, "All-In Mauling did not opt in");
        }
    }
}
