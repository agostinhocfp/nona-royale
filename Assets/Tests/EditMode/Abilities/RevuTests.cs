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
            Assert.That((Revu.Sadist.EnergyCost, Revu.Sadist.CooldownTurns, Revu.Sadist.Range),
                Is.EqualTo((9, 4, 3)));
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
            Assert.That(SadistOn(10), Is.EqualTo(0), "2 missing is not a point");
        }

        [Test]
        public void Sadist_AgainstAFullPool_StrikesNobody()
        {
            SetPool(_blue, 12);

            var result = Use(Revu.Sadist, _target);

            Assert.That(result.Approved, Is.True, "legal, and wasted");
            Assert.That(result.Outcomes.Any(o => o.Kind == EffectOutcomeKind.Damaged), Is.False,
                "no zero hits: they would spend evasion charges");
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
            SetPool(_blue, 9);   // 3 missing: 1 to the target, 0 splash

            var result = Use(Revu.Sadist, _target);

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
            // Cost 6 halves each 1 to... 1, not 0.
            BringEnemyRevuTo(12);
            var luka = AtTrack(20, "Luka", PlayerColor.Red, Luka.MaxHealth, 11);
            _board.Add(luka);

            Resolver(new FixedRoll(0.99)).Use(luka, Luka.Vendetta, _enemyRevu, _red, _board);

            Assert.That(_enemyRevu.Health, Is.EqualTo(Revu.MaxHealth - 3));
        }

        [Test]
        public void Equilibrium_AVendettaCritIsHalvedAfterItDoubles()
        {
            // Crit 1 × 2 = 2, then halved to 1; and Luka drains the 1 each blow removed.
            BringEnemyRevuTo(12);
            var luka = AtTrack(20, "Luka", PlayerColor.Red, Luka.MaxHealth, 11);
            luka.SetHealth(1);
            _board.Add(luka);

            Resolver(new FixedRoll(0.0)).Use(luka, Luka.Vendetta, _enemyRevu, _red, _board);

            Assert.That(_enemyRevu.Health, Is.EqualTo(Revu.MaxHealth - 3));
            Assert.That(luka.Health, Is.EqualTo(4), "lifesteal reads the halved hit");
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
        public void Equilibrium_ALaterDeviceIsNotACast()
        {
            // Eris' Exploit costs 6, but its ticks resolve at a later upkeep:
            // with two caught, each takes a clean 1.
            BringEnemyRevuTo(12);
            Place(_target, 13);
            Place(_near, 30);
            Place(_green, 31);
            var lethe = AtTrack(20, "Lethe", PlayerColor.Red, Lethe.MaxHealth, 10);
            _board.Add(lethe);

            _abilities.Use(lethe, Lethe.ErisExploit, null, _red, _board,
                _map.CellAt(PlayerColor.Red, (12 - _map.StartTrackIndex(PlayerColor.Red) + 52) % 52));
            _clock.BeginTurnFor(PlayerColor.Blue);
            _clock.BeginTurnFor(PlayerColor.Red);
            _cellEffects.Fire(PlayerColor.Red, _board);

            Assert.That(_enemyRevu.Health, Is.EqualTo(Revu.MaxHealth - 1));
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