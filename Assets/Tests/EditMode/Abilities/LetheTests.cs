// Assets/Tests/EditMode/Abilities/LetheTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Abilities
{
    /// <summary>
    /// Lethe, operator #10 (COMBAT_SYSTEMS §10.10, 2026-09-17): Nano Cell, a
    /// bubble built from a shield and a stun; Catalyst, an aura that hastens
    /// allies within 2; and Eris' Exploit, a zone whose damage grows with the
    /// crowd it catches.
    /// </summary>
    /// <remarks>
    /// Wired from the services, like the other ability fixtures, except the
    /// Catalyst movement tests at the bottom, which need the engine: haste is
    /// paid there.
    /// </remarks>
    [TestFixture]
    public class LetheTests
    {
        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;
        private EnergyLedger _energy;
        private DamagePipeline _damage;
        private DeferredCellEffects _cellEffects;
        private DeferredOperatorEffects _operatorEffects;
        private AbilityResolver _abilities;

        private OperatorState _lethe;
        private OperatorState _ally;
        private OperatorState _javi;
        private OperatorState[] _foes;
        private PlayerState _red;
        private PlayerState _blue;
        private List<OperatorState> _board;

        /// <summary>A track cell far from everything, for operators a test does not want.</summary>
        private const int FarTrack = 33;

        /// <summary>The cell Eris' Exploit is aimed at: two ahead of Lethe, inside range 3.</summary>
        private const int ZoneTrack = 12;

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
            _abilities = new AbilityResolver(
                _map, _clock, _energy, _statuses, _targeting, _damage, _cellEffects, _operatorEffects);

            // TRACK cells, never progress; none is a start cell (1, 14, 27,
            // 40) except where a test says so. Lethe at 10, her ally at 12
            // (inside Nano Cell's range 4), Javi at 11 (inside Neural Purge's
            // range 5). The four foes start parked far away; each test places
            // the ones it wants.
            _lethe = AtTrack(1, "Lethe", PlayerColor.Red, Lethe.MaxHealth, 10);
            _ally = AtTrack(2, "Ally", PlayerColor.Red, 7, 12);
            _javi = AtTrack(3, "Javi", PlayerColor.Red, Javi.MaxHealth, 11);

            _foes = new OperatorState[4];
            for (int i = 0; i < _foes.Length; i++)
                _foes[i] = AtTrack(10 + i, $"Foe{i}", PlayerColor.Blue, 7, FarTrack + i);

            _red = new PlayerState(PlayerColor.Red, new[] { _lethe, _ally, _javi });
            _blue = new PlayerState(PlayerColor.Blue, _foes);
            _board = new List<OperatorState> { _lethe, _ally, _javi };
            _board.AddRange(_foes);

            _clock.BeginTurnFor(PlayerColor.Red);
            _red.BeginTurn();
            Fund(12);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private OperatorState AtTrack(int id, string name, PlayerColor owner, int hp, int track)
        {
            var op = new OperatorState(id, name, owner, hp, 1.0);
            Place(op, track);
            return op;
        }

        private void Place(OperatorState op, int track) => op.MoveTo(ProgressAtTrack(op.Owner, track));

        private int ProgressAtTrack(PlayerColor owner, int track)
        {
            int circuit = _map.Profile.CircuitLength;
            int start = _map.StartTrackIndex(owner);

            return ((track - start) % circuit + circuit) % circuit;
        }

        private CellRef Track(int track) => _map.CellAt(PlayerColor.Red, ProgressAtTrack(PlayerColor.Red, track));

        private void Fund(int amount)
        {
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6
            _red.BeginTurn();
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6, capped at 12
            if (amount < 12) _energy.Spend(_red, 12 - amount);
        }

        private AbilityResolution Bubble(OperatorState target) =>
            _abilities.Use(_lethe, Lethe.NanoCell, target, _red, _board);

        private AbilityResolution Seed(int track) =>
            _abilities.Use(_lethe, Lethe.ErisExploit, null, _red, _board, Track(track));

        private void AdvanceToRedsNextTurn()
        {
            _clock.BeginTurnFor(PlayerColor.Blue);
            _clock.BeginTurnFor(PlayerColor.Red);
        }

        private IReadOnlyList<CellEffectResolution> FireRed() => _cellEffects.Fire(PlayerColor.Red, _board);

        private static DamageInstance Hit(int amount, DamageType type) =>
            new DamageInstance(amount, type, 99, "test");

        /// <summary>Puts the first <paramref name="count"/> foes inside the zone's five cells.</summary>
        private void Crowd(int count)
        {
            int[] inside = { 11, 12, 13, 14 };
            for (int i = 0; i < count; i++) Place(_foes[i], inside[i]);
        }

        // ── The operator ─────────────────────────────────────────────────

        [Test]
        public void TheDesignersNumbers()
        {
            // 2026-09-17. A test so changing them is a deliberate act that also
            // updates §10.10.
            var op = Roster.ByName("Lethe");

            Assert.That(op.MaxHealth, Is.EqualTo(7));
            Assert.That(op.BaseSpeed, Is.EqualTo(1.0));
            Assert.That(op.Passive, Is.EqualTo(StatusKind.Hastened));
            Assert.That(op.PassiveMagnitude, Is.EqualTo(0.0), "haste is not speed");
            Assert.That(op.Abilities.Select(a => a.Id), Is.EqualTo(new[] { 1001, 1002 }));

            Assert.That(op.Aura.Name, Is.EqualTo("Catalyst"));
            Assert.That(op.Aura.Radius, Is.EqualTo(2));
            Assert.That(op.Aura.Side, Is.EqualTo(AuraSide.Allies));
            Assert.That(op.Aura.GrantsHaste, Is.True);
            Assert.That(op.Aura.SpeedModifier, Is.EqualTo(0.0), "Catalyst never touches the speed channel");

            var cell = Lethe.NanoCell;
            Assert.That((cell.EnergyCost, cell.CooldownTurns, cell.Range), Is.EqualTo((4, 4, 4)));

            var eris = Lethe.ErisExploit;
            Assert.That((eris.EnergyCost, eris.CooldownTurns, eris.Range), Is.EqualTo((6, 4, 3)));
            Assert.That(eris.Targeting, Is.EqualTo(AbilityTargeting.Cell));
            Assert.That(eris.Effects[0].Radius, Is.EqualTo(2));
        }

        [Test]
        public void HerPassiveHaste_AddsNoSpeed()
        {
            _statuses.ApplyPassive(_lethe, StatusKind.Hastened);

            Assert.That(_statuses.IsHastened(_lethe), Is.True);
            Assert.That(_statuses.SpeedModifier(_lethe), Is.EqualTo(0.0));
        }

        [Test]
        public void HerPassiveHaste_SurvivesACleanse()
        {
            _statuses.ApplyPassive(_lethe, StatusKind.Hastened);

            _statuses.ClearApplied(_lethe);

            Assert.That(_statuses.IsHastened(_lethe), Is.True);
        }

        // ── Nano Cell ────────────────────────────────────────────────────

        [Test]
        public void NanoCell_ShieldsAndStunsTheAlly_AtOnce()
        {
            var result = Bubble(_ally);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_statuses.ShieldPool(_ally), Is.EqualTo(Lethe.NanoCellPool));
            Assert.That(_statuses.IsStunned(_ally), Is.True,
                "cast in its own side's turn, the stun takes hold now — move first, then bubble");
            Assert.That(_red.Energy, Is.EqualTo(12 - 4));
        }

        [Test]
        public void NanoCell_AbsorbsNormalAndTech_WithoutRunningDry()
        {
            Bubble(_ally);

            // Fifty damage: more than any round of three enemy seats deals.
            for (int i = 0; i < 5; i++)
            {
                var normal = _damage.Apply(_ally, Hit(6, DamageType.Normal));
                var tech = _damage.Apply(_ally, Hit(4, DamageType.Tech));

                Assert.That(normal.Outcome, Is.EqualTo(DamageOutcome.Absorbed), $"normal #{i}");
                Assert.That(tech.Outcome, Is.EqualTo(DamageOutcome.Absorbed), $"tech #{i}");
            }

            Assert.That(_ally.Health, Is.EqualTo(7));
        }

        [Test]
        public void NanoCell_AtomicGoesStraightThrough()
        {
            Bubble(_ally);

            var result = _damage.Apply(_ally, Hit(3, DamageType.Atomic));

            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dealt));
            Assert.That(_ally.Health, Is.EqualTo(4));
        }

        [Test]
        public void NanoCell_LastsOneEnemyRound_AndTheAllysNextTurn()
        {
            Bubble(_ally);

            // End of the cast turn: still up, so the enemy's turn meets it.
            _statuses.ExpireCompleted(_ally);
            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.ShieldPool(_ally), Is.EqualTo(Lethe.NanoCellPool), "the enemy round is covered");

            // The ally's next turn: still inside, still stunned.
            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_statuses.IsStunned(_ally), Is.True, "the turn it pays with");

            // End of that turn: gone.
            _statuses.ExpireCompleted(_ally);
            AdvanceToRedsNextTurn();
            Assert.That(_statuses.IsStunned(_ally), Is.False);
            Assert.That(_statuses.ShieldPool(_ally), Is.EqualTo(0));
        }

        [Test]
        public void NanoCell_IsRefusedOnAnEnemy_AndCostsNothing()
        {
            Place(_foes[0], 12);

            var result = Bubble(_foes[0]);

            Assert.That(result.Approved, Is.False);
            Assert.That(_red.Energy, Is.EqualTo(12));
            Assert.That(_statuses.IsStunned(_foes[0]), Is.False);
        }

        [Test]
        public void NanoCell_CanBeCastOnHerself()
        {
            var result = Bubble(_lethe);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_statuses.ShieldPool(_lethe), Is.EqualTo(Lethe.NanoCellPool));
            Assert.That(_statuses.IsStunned(_lethe), Is.True);
        }

        [Test]
        public void NanoCell_TheBubbledAllyCannotCast()
        {
            Bubble(_javi);

            var result = _abilities.Use(_javi, Javi.NeuralPurge, _ally, _red, _board);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.CasterStunned));
        }

        [Test]
        public void NeuralPurge_PopsTheBubble_ShieldAndStunTogether()
        {
            // Stated in Lethe.cs so nobody "fixes" it: the cleanse is the answer
            // a blanket immunity needs.
            Bubble(_ally);

            var purge = _abilities.Use(_javi, Javi.NeuralPurge, _ally, _red, _board);

            Assert.That(purge.Approved, Is.True, purge.ToString());
            Assert.That(_statuses.ShieldPool(_ally), Is.EqualTo(0));
            Assert.That(_statuses.IsStunned(_ally), Is.False);
        }

        // ── Eris' Exploit ────────────────────────────────────────────────

        [Test]
        public void ErisExploit_StrikesOnTheCast_AndLeavesTheZoneStanding()
        {
            // Designer, 2026-09-18 (ADR-0007 Amendment 2): the first hit lands at once, the
            // second waits for her next upkeep.
            Crowd(3);

            var result = Seed(ZoneTrack);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_foes.Take(3).All(f => f.Health == 7 - 2), Is.True, "2 each, now");
            Assert.That(_cellEffects.ActiveZones(), Has.Member(Track(ZoneTrack)), "still telegraphed");
            Assert.That(FireRed(), Is.Empty, "the second hit waits for her next upkeep");
        }

        [Test] public void ErisExploit_OneCaught_TakesNothing() => EachTakesOnePerOther(1, 0);
        [Test] public void ErisExploit_TwoCaught_TakeOneEach() => EachTakesOnePerOther(2, 1);
        [Test] public void ErisExploit_ThreeCaught_TakeTwoEach() => EachTakesOnePerOther(3, 2);
        [Test] public void ErisExploit_FourCaught_TakeThreeEach() => EachTakesOnePerOther(4, 3);

        private void EachTakesOnePerOther(int caught, int each)
        {
            // The instant hit and the tick are the same arithmetic, so each
            // victim takes `each` twice if it stays put.
            Crowd(caught);
            Seed(ZoneTrack);

            for (int i = 0; i < caught; i++)
                Assert.That(_foes[i].Health, Is.EqualTo(7 - each), _foes[i].Name + ", on the cast");

            AdvanceToRedsNextTurn();

            var fired = FireRed().Single();

            Assert.That(fired.Caught.Count, Is.EqualTo(caught));
            Assert.That(fired.DamagePerTarget, Is.EqualTo(each));
            for (int i = 0; i < caught; i++)
                Assert.That(_foes[i].Health, Is.EqualTo(7 - 2 * each), _foes[i].Name);
        }

        [Test]
        public void ErisExploit_ALoneVictim_IsNotEvenTouched()
        {
            // Not a zero-damage hit: a zero instance would still spend an
            // evasion charge (§5.5). True of the instant hit as well (ADR-0007 Amendment 2).
            Crowd(1);

            var cast = Seed(ZoneTrack);

            Assert.That(cast.Outcomes.Any(o => o.Kind == EffectOutcomeKind.Damaged), Is.False,
                "nothing is struck on the cast either");
            Assert.That(_foes[0].Health, Is.EqualTo(7));

            AdvanceToRedsNextTurn();

            var fired = FireRed().Single();

            Assert.That(fired.Caught.Count, Is.EqualTo(1));
            Assert.That(fired.Damage, Is.Empty);
            Assert.That(fired.HitSomething, Is.True, "the view still learns who was inside");
        }

        [Test]
        public void ErisExploit_RadiusTwo_CountsNobodyThreeAway()
        {
            Crowd(2);
            Place(_foes[2], ZoneTrack + 3);   // one step past the edge
            Seed(ZoneTrack);
            AdvanceToRedsNextTurn();

            var fired = FireRed().Single();

            Assert.That(fired.Caught.Count, Is.EqualTo(2));
            Assert.That(_foes[2].Health, Is.EqualTo(7));
        }

        [Test]
        public void ErisExploit_HerOwnSideIsNeitherHurtNorCounted()
        {
            // Lethe (10), Javi (11) and the ally (12) all stand inside.
            Crowd(2);
            Seed(ZoneTrack);
            AdvanceToRedsNextTurn();

            var fired = FireRed().Single();

            Assert.That(fired.Caught.Count, Is.EqualTo(2));
            Assert.That(fired.DamagePerTarget, Is.EqualTo(1));
            Assert.That(_ally.Health, Is.EqualTo(7));
            Assert.That(_lethe.Health, Is.EqualTo(Lethe.MaxHealth));
        }

        [Test]
        public void ErisExploit_HitsOnCastAndOnce_ThenIsGone()
        {
            Crowd(3);
            Seed(ZoneTrack);
            Assert.That(_foes.Take(3).All(f => f.Health == 7 - 2), Is.True, "the instant hit");

            AdvanceToRedsNextTurn();
            Assert.That(FireRed().Single().Caught.Count, Is.EqualTo(3));

            AdvanceToRedsNextTurn();
            Assert.That(FireRed(), Is.Empty);
            Assert.That(_cellEffects.ActiveZones(), Is.Empty);

            Assert.That(_foes.Take(3).All(f => f.Health == 7 - 2 * 2), Is.True, "two hits of 2 in all");
        }

        [Test]
        public void ErisExploit_CountsTheCrowdAgainAtTheTick()
        {
            // The cast catches four (3 each); two scatter, so the tick catches
            // two (1 each). Scattering still pays, it just no longer escapes
            // the ability entirely.
            Crowd(4);
            Seed(ZoneTrack);

            Assert.That(_foes.Take(4).All(f => f.Health == 4), Is.True, "3 each on the cast");

            Place(_foes[2], FarTrack);
            Place(_foes[3], FarTrack + 1);
            AdvanceToRedsNextTurn();

            Assert.That(FireRed().Single().DamagePerTarget, Is.EqualTo(1));
            Assert.That(_foes[0].Health, Is.EqualTo(3));
            Assert.That(_foes[3].Health, Is.EqualTo(4), "gone before the tick");
        }

        [Test]
        public void ErisExploit_IsCreditedToLethe()
        {
            Crowd(2);
            Seed(ZoneTrack);
            AdvanceToRedsNextTurn();

            var fired = FireRed().Single();

            Assert.That(fired.SourceOperatorId, Is.EqualTo(_lethe.Id));
            Assert.That(fired.Owner, Is.EqualTo(PlayerColor.Red));
        }

        [Test]
        public void ErisExploit_IsOneHitPerVictim_SoAPlateMeetsItOnce()
        {
            // Four caught: 3 each. A 2-point plate takes 2 of the one hit and
            // 1 gets through — not three 1-point hits of which it eats two.
            // The instant hit is the one the plate meets (ADR-0007 Amendment 2).
            Crowd(4);
            _statuses.Apply(_foes[0], StatusKind.Shield, duration: 3, magnitude: 2);
            AdvanceToRedsNextTurn();          // the plate takes hold on Blue's turn
            Seed(ZoneTrack);

            Assert.That(_foes[0].Health, Is.EqualTo(6));
            Assert.That(_statuses.ShieldPool(_foes[0]), Is.EqualTo(0));
        }

        // ── Eris' Exploit beside a squadmate's devices ───────────────────

        private OperatorState AddNuetu(int track)
        {
            var nuetu = AtTrack(20, "Nuetu", PlayerColor.Red, Nuetu.MaxHealth, track);
            _board.Add(nuetu);
            return nuetu;
        }

        [Test]
        public void SquadmatesZones_OnOneCell_DoNotOverwriteEachOther()
        {
            // Keyed on the source too (2026-09-17): the Killzone Nuetu placed
            // survives Lethe casting on the same cell, and both go off.
            var nuetu = AddNuetu(11);
            Crowd(2);

            Assert.That(_abilities.Use(nuetu, Nuetu.Killzone, null, _red, _board, Track(ZoneTrack)).Approved, Is.True);
            Fund(12);
            Assert.That(Seed(ZoneTrack).Approved, Is.True);

            Assert.That(_cellEffects.Snapshot().Count, Is.EqualTo(2));

            AdvanceToRedsNextTurn();
            var fired = FireRed();

            Assert.That(fired.Count, Is.EqualTo(2));
            Assert.That(fired.Select(f => f.SourceOperatorId), Is.EquivalentTo(new[] { nuetu.Id, _lethe.Id }));
        }

        [Test]
        public void TheSameOperator_RecastingOnItsCell_StillReplaces()
        {
            var cell = Track(ZoneTrack);

            for (int i = 0; i < 2; i++)
                _cellEffects.Deploy(cell, PlayerColor.Red, _lethe.Id, 1, 1, 1, 2, DamageType.Normal,
                    null, 0, scalesWithCrowd: true);

            Assert.That(_cellEffects.Snapshot().Count, Is.EqualTo(1));
        }

        [Test]
        public void BioLinkRage_IgnoresLethesZone()
        {
            // "While one of HIS Killzones is live": a squadmate's zone is not his.
            var nuetu = AddNuetu(11);
            nuetu.SetHealth(3);
            Place(_foes[0], 12);

            Seed(ZoneTrack);
            var rage = _abilities.Use(nuetu, Nuetu.BioLinkRage, _foes[0], _red, _board);

            Assert.That(rage.Approved, Is.True, rage.ToString());
            Assert.That(nuetu.Health, Is.EqualTo(4), "the base heal only");
        }

        [Test]
        public void BioLinkRage_StillReadsHisOwnKillzone()
        {
            var nuetu = AddNuetu(11);
            nuetu.SetHealth(3);
            Place(_foes[0], 12);

            _abilities.Use(nuetu, Nuetu.Killzone, null, _red, _board, Track(ZoneTrack));
            var rage = _abilities.Use(nuetu, Nuetu.BioLinkRage, _foes[0], _red, _board);

            Assert.That(rage.Approved, Is.True, rage.ToString());
            Assert.That(nuetu.Health, Is.EqualTo(3 + 1 + 2), "Killzone's own heal, then the doubled rider");
        }

        // ── Catalyst: the aura rule ──────────────────────────────────────

        private AuraRules Auras(params (OperatorState op, AuraDefinition aura)[] sources) =>
            new AuraRules(_targeting, sources.ToDictionary(s => s.op.Id, s => s.aura));

        [Test]
        public void Catalyst_ReachesAlliesWithinTwo_AndNoOneElse()
        {
            var rules = Auras((_lethe, Lethe.Catalyst));

            Place(_foes[0], 11);
            Place(_foes[1], 12);

            Assert.That(rules.GrantsHaste(_javi, _board), Is.True, "ally one away");
            Assert.That(rules.GrantsHaste(_ally, _board), Is.True, "ally two away, the edge");
            Assert.That(rules.GrantsHaste(_foes[0], _board), Is.False, "an enemy beside her");
            Assert.That(rules.GrantsHaste(_lethe, _board), Is.False, "never herself — hers is the passive");

            Place(_ally, 13);
            Assert.That(rules.GrantsHaste(_ally, _board), Is.False, "three away: it fades");
        }

        [Test]
        public void Catalyst_FadesWhileSheIsOutOfPlay()
        {
            var rules = Auras((_lethe, Lethe.Catalyst));

            _lethe.MoveTo(PathMap.YardProgress);

            Assert.That(rules.GrantsHaste(_javi, _board), Is.False);
        }

        [Test]
        public void Catalyst_SurvivesHerStun()
        {
            var rules = Auras((_lethe, Lethe.Catalyst));

            _statuses.Apply(_lethe, StatusKind.Stun, 2);

            Assert.That(rules.GrantsHaste(_javi, _board), Is.True);
        }

        [Test]
        public void Catalyst_IsNotSpeed()
        {
            var rules = Auras((_lethe, Lethe.Catalyst));

            Assert.That(rules.SpeedModifierFor(_javi, _board), Is.EqualTo(0.0));
        }

        [Test]
        public void IntimidatingPresence_StillReachesOnlyEnemies()
        {
            var rules = Auras((_lethe, Bouncer.IntimidatingPresence));

            Place(_foes[0], 11);

            Assert.That(rules.SpeedModifierFor(_foes[0], _board), Is.EqualTo(-0.5));
            Assert.That(rules.SpeedModifierFor(_javi, _board), Is.EqualTo(0.0));
            Assert.That(rules.GrantsHaste(_foes[0], _board), Is.False);
        }

        [Test]
        public void SpeedAuras_ResolveBySign_WhateverTheListOrder()
        {
            // The old rule kept the first of two equal magnitudes, so a +0.5 and
            // a −0.5 came out as whichever operator had the lower id.
            var quickening = new AuraDefinition("Quickening", 3, +0.5, AuraSide.Allies);
            var slowing = new AuraDefinition("Slowing", 3, -0.5);

            Place(_foes[0], 13);   // an enemy of the ally, three away

            var rules = Auras((_lethe, quickening), (_foes[0], slowing));
            var forward = new List<OperatorState> { _lethe, _foes[0], _ally };
            var backward = new List<OperatorState> { _ally, _foes[0], _lethe };

            Assert.That(rules.SpeedModifierFor(_ally, forward), Is.EqualTo(0.0));
            Assert.That(rules.SpeedModifierFor(_ally, backward), Is.EqualTo(0.0));
        }

        [Test]
        public void SpeedAuras_OfOneSign_StillDoNotStack()
        {
            var slowing = new AuraDefinition("Slowing", 3, -0.5);
            var harder = new AuraDefinition("Harder", 3, -1.0);

            Place(_foes[0], 11);
            Place(_foes[1], 13);

            var rules = Auras((_foes[0], slowing), (_foes[1], harder));

            Assert.That(rules.SpeedModifierFor(_ally, _board), Is.EqualTo(-1.0));
        }

        [Test]
        public void AnAuraThatDoesNothing_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => new AuraDefinition("Nothing", 2, 0.0, AuraSide.Allies));
        }

        // ── Catalyst: through the engine ─────────────────────────────────

        private static CombatConfig Config => CombatConfig.Default;

        private static OperatorState Named(MatchFactory.Match match, string name) =>
            match.Operators.First(o => o.Name == name);

        /// <summary>
        /// A started solo match — Lethe, Bouncer, Mimi, all on the start cell —
        /// arranged by <paramref name="arrange"/> and rolled, whose roll
        /// satisfies <paramref name="wanted"/>.
        /// </summary>
        private static MatchFactory.Match RolledSquad(
            Func<DiceRoll, bool> wanted, Action<MatchFactory.Match> arrange, out DiceRoll roll)
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                [PlayerColor.Red] = new[] { Lethe.Definition, Bouncer.Definition, Mimi.Definition }
            };

            for (int seed = 1; seed < 2000; seed++)
            {
                var match = MatchFactory.Create(
                    new[] { PlayerColor.Red }, seed, squads, openingDeployments: 3);

                match.Engine.Start();
                arrange?.Invoke(match);

                var rolled = match.Engine.Execute(new RollDiceCommand())
                    .OfType<DiceRolled>().FirstOrDefault();

                if (rolled != null && wanted(rolled.Roll))
                {
                    roll = rolled.Roll;
                    return match;
                }
            }

            throw new InvalidOperationException("No seed produced the roll this test needs.");
        }

        private static int Travelled(MatchFactory.Match match, ICommand move)
        {
            var moved = match.Engine.Execute(move).OfType<OperatorMoved>().FirstOrDefault();

            Assert.That(moved, Is.Not.Null, "the move should have been accepted");
            return moved.To - moved.From;
        }

        [Test]
        public void Lethe_MovesWithHerOwnHaste()
        {
            var match = RolledSquad(r => !r.IsDouble, null, out var roll);
            var lethe = Named(match, "Lethe");

            Assert.That(Travelled(match, new MoveCommand(lethe.Id)),
                Is.EqualTo(roll.Total + Config.HasteCellsFor(roll.Total)));
        }

        [Test]
        public void AnAllyBesideHer_MovesWithHaste()
        {
            var match = RolledSquad(r => !r.IsDouble, null, out var roll);
            var bouncer = Named(match, "Bouncer");

            Assert.That(match.Engine.ActiveStatusesOn(bouncer), Has.Member(StatusKind.Hastened),
                "the HASTE tag is how the aura is taught");
            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)),
                Is.EqualTo(roll.Total + Config.HasteCellsFor(roll.Total)));
        }

        [Test]
        public void AnAllyThreeAway_MovesWithout()
        {
            var match = RolledSquad(r => !r.IsDouble,
                m => Named(m, "Bouncer").MoveTo(3), out var roll);
            var bouncer = Named(match, "Bouncer");

            Assert.That(match.Engine.ActiveStatusesOn(bouncer), Has.No.Member(StatusKind.Hastened));
            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)), Is.EqualTo(roll.Total));
        }

        [Test]
        public void TheBonusBelongsToWhereTheMoveStarted()
        {
            // Bouncer starts beside her and walks out of range: that move is
            // hastened, and afterwards the tag is gone.
            var match = RolledSquad(r => !r.IsDouble && r.Total >= 3, null, out var roll);
            var bouncer = Named(match, "Bouncer");

            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)),
                Is.EqualTo(roll.Total + Config.HasteCellsFor(roll.Total)));
            Assert.That(match.Engine.ActiveStatusesOn(bouncer), Has.No.Member(StatusKind.Hastened),
                "out of range now");
        }

        [Test]
        public void WhenSheLeavesFirst_TheAllyBehindGetsNothing()
        {
            // Split roll: Lethe spends one die and walks at least three cells,
            // then Bouncer spends the other from the start cell.
            var match = RolledSquad(r => !r.IsDouble && r.First >= 2 && r.Second >= 2, null, out var roll);
            var lethe = Named(match, "Lethe");
            var bouncer = Named(match, "Bouncer");

            Travelled(match, new MoveCommand(lethe.Id, roll.First));

            Assert.That(Travelled(match, new MoveCommand(bouncer.Id, roll.Second)), Is.EqualTo(roll.Second));
        }

        [Test]
        public void AHastedMoveOutOfRange_StillChargesTheTurnCap()
        {
            // Read-before-move, pinned. A high double: Bouncer walks d + 2 out of
            // her range, then Lethe walks d + 2 onto his cell. The re-roll finds
            // him beside her again, and only 1 of the cap's 3 is left — which
            // is only true if his first move was charged while he was leaving.
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                [PlayerColor.Red] = new[] { Lethe.Definition, Bouncer.Definition, Mimi.Definition }
            };

            for (int seed = 1; seed < 5000; seed++)
            {
                var match = MatchFactory.Create(new[] { PlayerColor.Red }, seed, squads, openingDeployments: 3);
                match.Engine.Start();

                var first = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;
                if (!first.IsDouble || first.First < 4) continue;

                var lethe = Named(match, "Lethe");
                var bouncer = Named(match, "Bouncer");

                Assert.That(Travelled(match, new MoveCommand(bouncer.Id, first.First)), Is.EqualTo(first.First + 2));
                Assert.That(Travelled(match, new MoveCommand(lethe.Id, first.Second)), Is.EqualTo(first.Second + 2));
                Assert.That(lethe.Progress, Is.EqualTo(bouncer.Progress), "she caught him up");

                var second = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;
                if (second.IsDouble || second.Total <= Config.HasteRollThreshold) continue;

                Assert.That(Travelled(match, new MoveCommand(bouncer.Id)),
                    Is.EqualTo(second.Total + (Config.HasteBonusCellCap - 2)));
                return;
            }

            Assert.Fail("No seed produced the rolls this test needs.");
        }

        [Test]
        public void CatalystAndAStatusHaste_PayOnce()
        {
            var match = RolledSquad(r => !r.IsDouble, null, out var roll);
            var bouncer = Named(match, "Bouncer");
            match.Statuses.Apply(bouncer, StatusKind.Hastened, Config.HasteDurationTurns);

            Assert.That(Travelled(match, new MoveCommand(bouncer.Id)),
                Is.EqualTo(roll.Total + Config.HasteCellsFor(roll.Total)));
        }
    }
}