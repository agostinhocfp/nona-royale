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
    /// Revú, operator #11 (COMBAT_SYSTEMS §10.11, 2026-09-17; debt rework
    /// 2026-09-24): Leech Round's debt, Sadist calling it in, and Equilibrium
    /// on the receiving end of real abilities. The ledger's collection,
    /// interest and burn rules are pinned in <c>DebtTests</c>.
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
            _energy.Spend(player, player.Energy);
            if (amount > 0) _energy.GrantBounty(player, amount);
            Assert.That(player.Energy, Is.EqualTo(amount), "precondition");
        }

        /// <summary>Sets a seat's debt to an exact figure, owed to Revú.</summary>
        private void SetDebt(PlayerState player, int amount)
        {
            _energy.WriteOffDebt(player);
            if (amount > 0) _energy.IncurDebt(player, amount, _revu.Id);
            Assert.That(player.Debt, Is.EqualTo(amount), "precondition");
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
            // Designer, 2026-09-24 (d03bb69): Sadist 7 → 6 energy. Before that,
            // 2026-09-21: Sadist 9 → 7 energy, cooldown 4 → 3, and a
            // floor of 2 on the primary figure.
            Assert.That((Revu.Sadist.EnergyCost, Revu.Sadist.CooldownTurns, Revu.Sadist.Range),
                Is.EqualTo((6, 3, 3)));
            Assert.That(Revu.SadistMinimumDamage, Is.EqualTo(2));
        }

        // ── Leech Round ──────────────────────────────────────────────────

        [Test]
        public void LeechRound_WoundsAndPutsTheTargetsSeatInDebt()
        {
            SetPool(_blue, 7);

            var result = Use(Revu.LeechRound, _target);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_target.Health, Is.EqualTo(5));
            Assert.That(_blue.Debt, Is.EqualTo(2));
            Assert.That(_blue.OwesTo(_revu.Id), Is.True, "owed to the caster");
            Assert.That(result.Outcomes.Single(o => o.Kind == EffectOutcomeKind.DebtIncurred).Amount, Is.EqualTo(2));
        }

        [Test]
        public void LeechRound_TakesNothingFromEitherPool_AtCastTime()
        {
            // The seat pays when its own turn ends (§3.3), and paid debt is
            // destroyed, never handed to Revú.
            SetPool(_blue, 7);

            Use(Revu.LeechRound, _target);

            Assert.That(_blue.Energy, Is.EqualTo(7), "nothing taken yet");
            Assert.That(_red.Energy, Is.EqualTo(12 - 3), "only the cost left Red's pool; nothing came back");
        }

        [Test]
        public void LeechRound_StacksDebt_UpToTheCap()
        {
            var added = new List<int>();

            for (int i = 0; i < 4; i++)
            {
                var result = Use(Revu.LeechRound, _target);
                added.Add(result.Outcomes.Single(o => o.Kind == EffectOutcomeKind.DebtIncurred).Amount);
                _abilities.ResetCooldowns(_revu);
                SetPool(_red, 12);
                _target.RestoreHealth();
            }

            Assert.That(added, Is.EqualTo(new[] { 2, 2, 2, 0 }), "the fourth finds the seat at the cap, and says so");
            Assert.That(_blue.Debt, Is.EqualTo(EnergyConfig.Default.DebtCap));
        }

        [Test]
        public void LeechRound_CannotBeAimedAtAnAlly()
        {
            var result = Use(Revu.LeechRound, _ally);

            Assert.That(result.Approved, Is.False);
            Assert.That(_red.Energy, Is.EqualTo(12));
        }

        [Test]
        public void TheDebtEffects_RefuseToGuess_WithoutTheSeats()
        {
            var blind = new AbilityResolver(
                _map, _clock, _energy, _statuses, _targeting, _damage, _cellEffects, _operatorEffects);

            Assert.Throws<InvalidOperationException>(() =>
                blind.Use(_revu, Revu.LeechRound, _target, _red, _board));
        }

        // ── Sadist ───────────────────────────────────────────────────────

        private int SadistOn(int debt)
        {
            SetDebt(_blue, debt);
            _target.RestoreHealth();
            Use(Revu.Sadist, _target);
            _abilities.ResetCooldowns(_revu);
            SetPool(_red, 12);
            return _target.MaxHealth - _target.Health;
        }

        [Test]
        public void Sadist_DealsTheWholeDebt_AtLeastTwo()
        {
            Assert.That(SadistOn(0), Is.EqualTo(2), "nothing owed: the floor (2026-09-21)");
            Assert.That(SadistOn(1), Is.EqualTo(2), "below the floor");
            Assert.That(SadistOn(3), Is.EqualTo(3));
            Assert.That(SadistOn(6), Is.EqualTo(6), "the cap");
        }

        [Test]
        public void Sadist_ClearsTheDebt_AndSaysHowMuch()
        {
            SetDebt(_blue, 5);

            var result = Use(Revu.Sadist, _target);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_blue.Debt, Is.EqualTo(0));
            Assert.That(_blue.Creditors, Is.Empty, "a settled seat owes nobody");
            Assert.That(result.Outcomes.Single(o => o.Kind == EffectOutcomeKind.DebtCalled).Amount, Is.EqualTo(5));
        }

        [Test]
        public void Sadist_ClearsTheDebt_EvenWhenEquilibriumHalvesTheHit()
        {
            // Aimed at Blue's own Revú: cost 7, so his 4 lands as 2. The loan is
            // collected all the same — a dodge or a halving is not a second life
            // for the debt.
            BringEnemyRevuTo(12);
            SetDebt(_blue, 4);

            Use(Revu.Sadist, _enemyRevu);

            Assert.That(_enemyRevu.Health, Is.EqualTo(Revu.MaxHealth - 2));
            Assert.That(_blue.Debt, Is.EqualTo(0));
        }

        [Test]
        public void Sadist_LeavesTheSplashVictimsSeatsDebtAlone()
        {
            SetDebt(_blue, 2);
            _energy.IncurDebt(_greenSeat, 3, _revu.Id);

            Use(Revu.Sadist, _target);

            Assert.That(_blue.Debt, Is.EqualTo(0), "the target's seat is settled");
            Assert.That(_greenSeat.Debt, Is.EqualTo(3), "Green was only standing nearby");
        }

        [Test]
        public void Sadist_WithNoDebt_StillDealsItsFloor()
        {
            // Designer, 2026-09-21. It used to strike nobody against a full
            // pool; now the blank case is a seat that owes nothing. The floor is
            // the primary figure's, and the splash still divides it.
            SetDebt(_blue, 0);

            var result = Use(Revu.Sadist, _target);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_target.Health, Is.EqualTo(5), "the floor of 2");
            Assert.That(_near.Health, Is.EqualTo(6), "half the floor, rounded down");
            Assert.That(_far.Health, Is.EqualTo(7), "four away");
        }

        [Test]
        public void Sadist_SplashesHalf_ToEnemiesWithinTwo_FromTheTargetsFigure()
        {
            SetDebt(_blue, 4);          // the target's seat: 4, so 2 splash
            SetPool(_blue, 12);         // what it holds does not matter any more

            Use(Revu.Sadist, _target);

            Assert.That(_target.Health, Is.EqualTo(3));
            Assert.That(_near.Health, Is.EqualTo(5), "Blue, one away");
            Assert.That(_green.Health, Is.EqualTo(5), "Green, one away the other side, owing nothing: the target's figure");
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
                    AbilityEffect.DebtDamage(Revu.SadistSplashRadius, Revu.SadistSplashDivisor, DamageType.Normal)
                });

            SetDebt(_blue, 1);   // 1 to the target, 0 splash

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
            // Blind Spot is 5, so Equilibrium neither doubles nor halves it.
            // The 3 is the strike's 2 plus Luka's heavy rider: Revú's maximum
            // of 8 is above the heavy line (§2.4).
            BringEnemyRevuTo(12);
            var luka = AtTrack(20, "Luka", PlayerColor.Red, Luka.MaxHealth, 9);
            _board.Add(luka);

            _abilities.Use(luka, Luka.BlindSpot, _enemyRevu, _red, _board);

            Assert.That(_enemyRevu.Health, Is.EqualTo(Revu.MaxHealth - 3));
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
        public void TheEngine_ReportsTheDebt()
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
                int pool = blue.Energy;

                var events = match.Engine.Execute(new UseAbilityCommand(revu.Id, Revu.LeechRound.Id, syla.Id, null));
                var debt = events.OfType<DebtIncurred>().Single();

                Assert.That(debt.Player, Is.EqualTo(PlayerColor.Blue));
                Assert.That(debt.Amount, Is.EqualTo(2));
                Assert.That(debt.Owed, Is.EqualTo(blue.Debt));
                Assert.That(debt.Source, Is.SameAs(revu));
                Assert.That(debt.Debtor, Is.SameAs(syla), "where the view shows the loan");
                Assert.That(blue.Energy, Is.EqualTo(pool), "nothing is taken at cast time");
                Assert.That(match.Statuses.ScalesCastDamage(revu), Is.True, "his passive reached the engine");
                return;
            }

            Assert.Fail("No seed gave Red three energy on the first roll.");
        }

        [Test]
        public void TheEngine_ReportsWhereSadistCollected()
        {
            // The view stamps the figure on the operator Sadist was aimed at,
            // so the event has to say who that was (§3.3).
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                [PlayerColor.Red] = new[] { Revu.Definition, Bouncer.Definition, Mimi.Definition },
                [PlayerColor.Blue] = new[] { Syla.Definition, Javi.Definition, Kian.Definition }
            };

            var match = MatchFactory.Create(new[] { PlayerColor.Red, PlayerColor.Blue }, 1, squads,
                openingDeployments: 3);
            match.Engine.Start();

            var revu = match.Operators.First(o => o.Name == "Revú");
            var syla = match.Operators.First(o => o.Name == "Syla");
            int circuit = match.Map.Profile.CircuitLength;
            revu.MoveTo((10 - match.Map.StartTrackIndex(PlayerColor.Red) + circuit) % circuit);
            syla.MoveTo((12 - match.Map.StartTrackIndex(PlayerColor.Blue) + circuit) % circuit);

            var red = match.Players.First(p => p.Color == PlayerColor.Red);
            var blue = match.Players.First(p => p.Color == PlayerColor.Blue);
            var ledger = new EnergyLedger(EnergyConfig.Default);
            ledger.IncurDebt(blue, 4, revu.Id);

            match.Engine.Execute(new RollDiceCommand());
            ledger.GrantBounty(red, EnergyConfig.Default.EnergyCap);

            var events = match.Engine.Execute(new UseAbilityCommand(revu.Id, Revu.Sadist.Id, syla.Id, null));
            var called = events.OfType<DebtCalled>().Single();

            Assert.That(called.Target, Is.SameAs(syla));
            Assert.That(called.Amount, Is.EqualTo(4));
            Assert.That(called.Source, Is.SameAs(revu));
            Assert.That(blue.Debt, Is.EqualTo(0));
        }
    }
}