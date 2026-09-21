// Assets/Tests/EditMode/Abilities/RevuTests.cs
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
    /// Revú, operator #11 (COMBAT_SYSTEMS §10.11, 2026-09-17): Leech Round's
    /// drain, Sadist's pool-scaled hit, and Equilibrium on the receiving end
    /// of real abilities.
    /// </summary>
    [TestFixture]
    public class RevuTests
    {
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

        private OperatorState _revu;
        private OperatorState _ally;
        private OperatorState _target;
        private OperatorState _near;
        private OperatorState _far;
        private OperatorState _green;
        private OperatorState _enemyRevu;
        private PlayerState _red;
        private PlayerState _blue;
        private PlayerState _greenSeat;
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

            // TRACK cells, never progress; no start cells (1, 14, 27, 40).
            // Revú at 10, his ally at 11; the target at 12 (range 3), a Blue
            // squadmate at 13 (inside the splash), another at 16 (outside it),
            // a Green operator at 11 (inside the splash, another seat), and a
            // Blue Revú far away for the Equilibrium tests.
            _revu = AtTrack(1, "Revú", PlayerColor.Red, Revu.MaxHealth, 10);
            _ally = AtTrack(2, "Ally", PlayerColor.Red, 7, 11);
            _target = AtTrack(3, "Target", PlayerColor.Blue, 7, 12);
            _near = AtTrack(4, "Near", PlayerColor.Blue, 7, 13);
            _far = AtTrack(5, "Far", PlayerColor.Blue, 7, 16);
            _green = AtTrack(6, "Green", PlayerColor.Green, 7, 11);
            _enemyRevu = AtTrack(7, "Revú", PlayerColor.Blue, Revu.MaxHealth, 33);

            _red = new PlayerState(PlayerColor.Red, new[] { _revu, _ally });
            _blue = new PlayerState(PlayerColor.Blue, new[] { _target, _near, _far, _enemyRevu });
            _greenSeat = new PlayerState(PlayerColor.Green, new[] { _green });
            _board = new List<OperatorState> { _revu, _ally, _target, _near, _far, _green, _enemyRevu };

            _statuses.ApplyPassive(_enemyRevu, StatusKind.Equilibrium);

            _abilities = Resolver(null);

            _clock.BeginTurnFor(PlayerColor.Red);
            _red.BeginTurn();
            Fund(_red, 12);
        }

        private AbilityResolver Resolver(IRandom random) => new AbilityResolver(
            _map, _clock, _energy, _statuses, _targeting, _damage, _cellEffects, _operatorEffects,
            random, new[] { _red, _blue, _greenSeat });

        private OperatorState AtTrack(int id, string name, PlayerColor owner, int hp, int track)
        {
            var op = new OperatorState(id, name, owner, hp, 1.0);
            Place(op, track);
            return op;
        }

        private void Place(OperatorState op, int track)
        {
            int circuit = _map.Profile.CircuitLength;
            op.MoveTo(((track - _map.StartTrackIndex(op.Owner)) % circuit + circuit) % circuit);
        }

        private void Fund(PlayerState player, int amount)
        {
            _energy.GrantForTurn(player, new DiceRoll(6, 6));
            player.BeginTurn();
            _energy.GrantForTurn(player, new DiceRoll(6, 6));
            if (amount < 12) _energy.Spend(player, 12 - amount);
        }

        /// <summary>Sets a pool to an exact figure.</summary>
        private void SetPool(PlayerState player, int amount)
        {
            _energy.Drain(player, player.Energy);
            if (amount > 0) _energy.GrantBounty(player, amount);
            Assert.That(player.Energy, Is.EqualTo(amount), "precondition");
        }

        private AbilityResolution Use(AbilityDefinition ability, OperatorState target) =>
            _abilities.Use(_revu, ability, target, _red, _board);

        // ── The operator ─────────────────────────────────────────────────

        [Test]
        public void TheDraftsNumbers()
        {
            var op = Roster.ByName("Revú");

            // Designer, 2026-09-17: health 7 → 8, Leech Round 1 → 2 damage and
            // cooldown 2 → 1, Sadist cooldown 5 → 4.
            Assert.That(op.MaxHealth, Is.EqualTo(8));
            Assert.That(op.BaseSpeed, Is.EqualTo(1.0));
            Assert.That(op.Passive, Is.EqualTo(StatusKind.Equilibrium));
            Assert.That(op.PassiveName, Is.EqualTo("Equilibrium"));
            Assert.That(op.Abilities.Select(a => a.Id), Is.EqualTo(new[] { 1101, 1102 }));

            Assert.That((Revu.LeechRound.EnergyCost, Revu.LeechRound.CooldownTurns, Revu.LeechRound.Range),
                Is.EqualTo((3, 1, 3)));
            // Designer, 2026-09-21: Sadist 9 → 7 energy, cooldown 4 → 3, and a
            // floor of 2 on the primary figure.
            Assert.That((Revu.Sadist.EnergyCost, Revu.Sadist.CooldownTurns, Revu.Sadist.Range),
                Is.EqualTo((7, 3, 3)));
            Assert.That(Revu.SadistMinimumDamage, Is.EqualTo(2));
        }

        // ── Leech Round ──────────────────────────────────────────────────

        [Test]
        public void LeechRound_WoundsAndDrainsTheTargetsSeat()
        {
            SetPool(_blue, 7);

            var result = Use(Revu.LeechRound, _target);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_target.Health, Is.EqualTo(5));
            Assert.That(_blue.Energy, Is.EqualTo(5));
            Assert.That(result.Outcomes.Single(o => o.Kind == EffectOutcomeKind.EnergyDrained).Amount, Is.EqualTo(2));
        }

        [Test]
        public void LeechRound_DestroysTheEnergy_ItDoesNotTransferIt()
        {
            SetPool(_blue, 7);

            Use(Revu.LeechRound, _target);

            Assert.That(_red.Energy, Is.EqualTo(12 - 3), "only the cost left Red's pool; nothing came back");
        }

        [Test]
        public void LeechRound_TakesOnlyWhatIsThere()
        {
            SetPool(_blue, 1);

            var result = Use(Revu.LeechRound, _target);

            Assert.That(_blue.Energy, Is.EqualTo(0));
            Assert.That(result.Outcomes.Single(o => o.Kind == EffectOutcomeKind.EnergyDrained).Amount, Is.EqualTo(1));
        }

        [Test]
        public void LeechRound_OnADryPool_StillSaysSo()
        {
            SetPool(_blue, 0);

            var result = Use(Revu.LeechRound, _target);

            Assert.That(result.Outcomes.Single(o => o.Kind == EffectOutcomeKind.EnergyDrained).Amount, Is.EqualTo(0));
        }

        [Test]
        public void LeechRound_CannotBeAimedAtAnAlly()
        {
            var result = Use(Revu.LeechRound, _ally);

            Assert.That(result.Approved, Is.False);
            Assert.That(_red.Energy, Is.EqualTo(12));
        }

        [Test]
        public void TheEnergyEffects_RefuseToGuess_WithoutTheSeats()
        {
            var blind = new AbilityResolver(
                _map, _clock, _energy, _statuses, _targeting, _damage, _cellEffects, _operatorEffects);

            Assert.Throws<InvalidOperationException>(() =>
                blind.Use(_revu, Revu.LeechRound, _target, _red, _board));
        }

        // ── Sadist ───────────────────────────────────────────────────────

        private int SadistOn(int pool)
        {
            SetPool(_blue, pool);
            _target.RestoreHealth();
            Use(Revu.Sadist, _target);
            _abilities.ResetCooldowns(_revu);
            SetPool(_red, 12);
            return _target.MaxHealth - _target.Health;
        }

        [Test]
        public void Sadist_OneDamageForEveryThreeMissing()
        {
            Assert.That(SadistOn(0), Is.EqualTo(4), "an empty pool");
            Assert.That(SadistOn(5), Is.EqualTo(2), "7 missing rounds down to 2");
            Assert.That(SadistOn(6), Is.EqualTo(2));
            Assert.That(SadistOn(10), Is.EqualTo(2), "2 missing is not a point, but the floor is (2026-09-21)");
        }

        [Test]
        public void Sadist_AgainstAFullPool_StillDealsItsFloor()
        {
            // Designer, 2026-09-21. It used to strike nobody: a seat at cap paid
            // nothing for the roster's dearest read. The floor is the primary
            // figure's, and the splash still divides it.
            SetPool(_blue, 12);

            var result = Use(Revu.Sadist, _target);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_target.Health, Is.EqualTo(5), "the floor of 2");
            Assert.That(_near.Health, Is.EqualTo(6), "half the floor, rounded down");
            Assert.That(_far.Health, Is.EqualTo(7), "four away");
        }

        [Test]
        public void Sadist_SplashesHalf_ToEnemiesWithinTwo_FromTheTargetsFigure()
        {
            SetPool(_blue, 0);         // the target's seat: 4, so 2 splash
            SetPool(_greenSeat, 12);   // Green's own pool is full, and does not matter

            Use(Revu.Sadist, _target);

            Assert.That(_target.Health, Is.EqualTo(3));
            Assert.That(_near.Health, Is.EqualTo(5), "Blue, one away");
            Assert.That(_green.Health, Is.EqualTo(5), "Green, one away the other side: the target's figure");
            Assert.That(_far.Health, Is.EqualTo(7), "four away");
            Assert.That(_ally.Health, Is.EqualTo(7), "his own side");
            Assert.That(_revu.Health, Is.EqualTo(Revu.MaxHealth));
        }

        [Test]
        public void Sadist_ASplashOfZero_IsNotDealt()
        {
            // Unreachable through Sadist since the floor (a primary of 2 always
            // splashes 1), so the rule is pinned on a floorless double. Zero
            // hits are not dealt at all: they would spend evasion charges.
            var floorless = new AbilityDefinition(
                id: 99961, name: "Test Collection", description: "Test double: Sadist without its floor.",
                energyCost: 7, cooldownTurns: 0, range: 3,
                effects: new[]
                {
                    AbilityEffect.MissingEnergyDamage(
                        Revu.SadistEnergyPerDamage, Revu.SadistSplashRadius, Revu.SadistSplashDivisor,
                        DamageType.Normal)
                });

            SetPool(_blue, 9);   // 3 missing: 1 to the target, 0 splash

            var result = Use(floorless, _target);

            Assert.That(_target.Health, Is.EqualTo(6));
            Assert.That(result.Outcomes.Count(o => o.Kind == EffectOutcomeKind.Damaged), Is.EqualTo(1));
        }

        // ── Equilibrium, on the receiving end ────────────────────────────

        private void BringEnemyRevuTo(int track) => Place(_enemyRevu, track);

        [Test]
        public void Equilibrium_ACheapCastHitsHimDouble()
        {
            // Leech Round is 3: its 2 becomes 4. The mirror is fair game.
            BringEnemyRevuTo(12);

            Use(Revu.LeechRound, _enemyRevu);

            Assert.That(_enemyRevu.Health, Is.EqualTo(Revu.MaxHealth - 4));
        }

        [Test]
        public void Equilibrium_AMidCostCastHitsHimClean()
        {
            // Blind Spot is 5: its 2 lands as 2.
            BringEnemyRevuTo(12);
            var luka = AtTrack(20, "Luka", PlayerColor.Red, Luka.MaxHealth, 9);
            _board.Add(luka);

            _abilities.Use(luka, Luka.BlindSpot, _enemyRevu, _red, _board);

            Assert.That(_enemyRevu.Health, Is.EqualTo(Revu.MaxHealth - 2));
        }

        [Test]
        public void Equilibrium_VendettaStillHurts_OnePerBlow()
        {
            // Cost 5 since 2026-09-20, so it is neither cheap nor dear and each
            // blow lands its plain 1. At cost 6 the halving floored each 1 at 1
            // and the total was the same, which is why this number did not move.
            BringEnemyRevuTo(12);
            var luka = AtTrack(20, "Luka", PlayerColor.Red, Luka.MaxHealth, 11);
            _board.Add(luka);

            Resolver(new FixedRoll(0.99)).Use(luka, Luka.Vendetta, _enemyRevu, _red, _board);

            Assert.That(_enemyRevu.Health, Is.EqualTo(Revu.MaxHealth - 3));
        }

        [Test]
        public void ACritAgainstRevu_IsWholeNow_AndDrains()
        {
            // Vendetta left the dear band at 5 (2026-09-20), so nothing halves
            // it any more: Revú is heavy at 8, so each crit is 1 × 3, and three
            // blows finish him from full. Luka drains what each blow removed.
            BringEnemyRevuTo(12);
            var luka = AtTrack(20, "Luka", PlayerColor.Red, Luka.MaxHealth, 11);
            luka.SetHealth(1);
            _board.Add(luka);

            Resolver(new FixedRoll(0.0)).Use(luka, Luka.Vendetta, _enemyRevu, _red, _board);

            Assert.That(_enemyRevu.Health, Is.EqualTo(0), "three heavy crits against 8");
            Assert.That(luka.Health, Is.EqualTo(Luka.MaxHealth), "lifesteal, capped");
        }

        [Test]
        public void ACritOnADearCast_IsHalvedAfterItDoubles()
        {
            // The ordering rule (§2.4, §5.17) with no roster ability left to
            // show it: Vendetta and Eris' Exploit both left the dear band in the
            // 2026-09-20 pass, so it is pinned on a double at cost 6.
            var dearCrit = new AbilityDefinition(
                id: 99962, name: "Test Grudge", description: "Test double: a dear cast that can crit.",
                energyCost: 6, cooldownTurns: 0, range: 3,
                effects: new[]
                {
                    AbilityEffect.Damage(EffectScope.PrimaryTarget, 2, DamageType.Atomic, EffectAudience.EnemyOnly)
                        .WithCritical(chance: 1.0, multiplier: 2, heavyMultiplier: 2, heavyAboveMaxHealth: 0)
                });

            BringEnemyRevuTo(12);
            var luka = AtTrack(20, "Luka", PlayerColor.Red, Luka.MaxHealth, 11);
            _board.Add(luka);

            Resolver(new FixedRoll(0.0)).Use(luka, dearCrit, _enemyRevu, _red, _board);

            Assert.That(_enemyRevu.Health, Is.EqualTo(Revu.MaxHealth - 2), "2 × 2 crit, then halved");
        }

        [Test]
        public void Equilibrium_HalvesAtomicToo()
        {
            // Velvet Rope is 6: 3 Atomic becomes 1.
            BringEnemyRevuTo(12);
            var bouncer = AtTrack(20, "Bouncer", PlayerColor.Red, Bouncer.MaxHealth, 11);
            _board.Add(bouncer);

            _abilities.Use(bouncer, Bouncer.VelvetRope, _enemyRevu, _red, _board);

            Assert.That(_enemyRevu.Health, Is.EqualTo(Revu.MaxHealth - 1));
        }

        [Test]
        public void Equilibrium_AnExecuteStillKills()
        {
            BringEnemyRevuTo(12);
            _enemyRevu.SetHealth(2);
            var kurbyn = AtTrack(20, "Kurbyn", PlayerColor.Red, Kurbyn.MaxHealth, 11);
            _board.Add(kurbyn);

            var result = _abilities.Use(kurbyn, Kurbyn.MiraclePull, _enemyRevu, _red, _board);

            Assert.That(result.Outcomes.Any(o => o.Kind == EffectOutcomeKind.Executed && o.Recipient == _enemyRevu), Is.True);
        }

        [Test]
        public void Equilibrium_ScalesTheZonesInstantHit_ButNotItsLaterTick()
        {
            // Eris' Exploit strikes on the cast since 2026-09-18 (ADR-0007
            // Amendment 2), so its first hit is a cast hit and Equilibrium reads
            // its cost; its later tick resolves at an upkeep and carries no cost
            // at all. Since the 2026-09-20 reprice the cost is 4 — neither cheap
            // nor dear — so the instant hit is clean too, and the two are equal.
            // Three caught: 2 each, so Revú takes 2 now and 2 at the tick.
            BringEnemyRevuTo(12);
            Place(_target, 13);
            Place(_near, 11);
            Place(_green, 31);
            var lethe = AtTrack(20, "Lethe", PlayerColor.Red, Lethe.MaxHealth, 10);
            _board.Add(lethe);

            _abilities.Use(lethe, Lethe.ErisExploit, null, _red, _board,
                _map.CellAt(PlayerColor.Red, (12 - _map.StartTrackIndex(PlayerColor.Red) + 52) % 52));

            Assert.That(_enemyRevu.Health, Is.EqualTo(Revu.MaxHealth - 2), "the instant hit, unscaled at cost 4");
            Assert.That(_target.Health, Is.EqualTo(5), "everyone else takes the same 2");

            _clock.BeginTurnFor(PlayerColor.Blue);
            _clock.BeginTurnFor(PlayerColor.Red);
            _cellEffects.Fire(PlayerColor.Red, _board);

            Assert.That(_enemyRevu.Health, Is.EqualTo(Revu.MaxHealth - 4), "the tick is not a cast: a clean 2");
        }

        [Test]
        public void Equilibrium_DoesNotTouchOtherOperators()
        {
            Use(Revu.LeechRound, _target);

            Assert.That(_target.Health, Is.EqualTo(5));
        }

        // ── Through the engine ───────────────────────────────────────────

        [Test]
        public void TheEngine_ReportsTheDrain()
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                [PlayerColor.Red] = new[] { Revu.Definition, Bouncer.Definition, Mimi.Definition },
                [PlayerColor.Blue] = new[] { Syla.Definition, Javi.Definition, Kian.Definition }
            };

            for (int seed = 1; seed < 400; seed++)
            {
                var match = MatchFactory.Create(new[] { PlayerColor.Red, PlayerColor.Blue }, seed, squads,
                    openingDeployments: 3);
                match.Engine.Start();

                var revu = match.Operators.First(o => o.Name == "Revú");
                var syla = match.Operators.First(o => o.Name == "Syla");
                int circuit = match.Map.Profile.CircuitLength;
                revu.MoveTo((10 - match.Map.StartTrackIndex(PlayerColor.Red) + circuit) % circuit);
                syla.MoveTo((12 - match.Map.StartTrackIndex(PlayerColor.Blue) + circuit) % circuit);

                match.Engine.Execute(new RollDiceCommand());
                if (match.Engine.CurrentPlayer.Energy < 3) continue;

                var blue = match.Players.First(p => p.Color == PlayerColor.Blue);
                int before = blue.Energy;

                var events = match.Engine.Execute(new UseAbilityCommand(revu.Id, Revu.LeechRound.Id, syla.Id, null));
                var drained = events.OfType<EnergyDrained>().Single();

                Assert.That(drained.Player, Is.EqualTo(PlayerColor.Blue));
                Assert.That(drained.Amount, Is.EqualTo(Math.Min(2, before)));
                Assert.That(drained.Remaining, Is.EqualTo(blue.Energy));
                Assert.That(drained.Source, Is.SameAs(revu));
                Assert.That(match.Statuses.ScalesCastDamage(revu), Is.True, "his passive reached the engine");
                return;
            }

            Assert.Fail("No seed gave Red three energy on the first roll.");
        }
    }
}