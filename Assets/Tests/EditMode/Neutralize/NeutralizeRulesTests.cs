// Assets/Tests/EditMode/Neutralize/NeutralizeRulesTests.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;
using NonaRoyale.Core.Tests.Abilities;   // FakeClock
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Neutralize
{
    /// <summary>
    /// The consequences of a neutralize, at service level: what it pays, what it
    /// refuses to pay, and what it hands the marker's squad.
    /// </summary>
    /// <remarks>
    /// Built by hand rather than through <c>GameEngine</c> because the bounty's
    /// interesting cases are all about <i>who</i> killed whom, and arranging a
    /// specific killer through the dice is far more work than it is worth. The
    /// engine's job — that it reports what happened — is tested where it
    /// belongs, in <c>GameEngineTests</c>.
    ///
    /// <b>The cell-effect registry is here only to satisfy the resolver.</b>
    /// Nothing in this fixture paints a beacon; a beacon <i>kill</i> is an
    /// ordinary neutralize with a different cause label, and where that matters
    /// is the turn machine's upkeep ordering, which is where it is tested.
    /// </remarks>
    [TestFixture]
    public class NeutralizeRulesTests
    {
        private const int Bounty = 3;

        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private EnergyLedger _energy;
        private NeutralizeRules _neutralize;

        private OperatorState _killer;
        private OperatorState _ally;
        private OperatorState _victim;
        private PlayerState _red;
        private PlayerState _blue;
        private List<OperatorState> _operators;

        [SetUp]
        public void SetUp()
        {
            var combat = new CombatConfig(neutralizeEnergyBounty: Bounty);

            _map = new PathMap(BoardProfile.Standard);
            _clock = new FakeClock();
            _statuses = new StatusRegistry(_clock, combat);
            _energy = new EnergyLedger(EnergyConfig.Default);

            var targeting = new TargetingRules(_map, _statuses);
            var damage = new DamagePipeline(_statuses, new SeededRandom(1));
            var cellEffects = new DeferredCellEffects(_clock, targeting, damage, _statuses);
            var abilities = new AbilityResolver(_map, _clock, _energy, _statuses, targeting, damage, cellEffects);

            _killer = new OperatorState(1, "Syla", PlayerColor.Red, 6, 1.5);
            _ally = new OperatorState(2, "Bouncer", PlayerColor.Red, 12, 1.0);
            _victim = new OperatorState(3, "Kurbyn", PlayerColor.Blue, 6, 1.0);

            _killer.MoveTo(10);
            _ally.MoveTo(11);
            _victim.MoveTo(12);

            _red = new PlayerState(PlayerColor.Red, new[] { _killer, _ally });
            _blue = new PlayerState(PlayerColor.Blue, new[] { _victim });
            _operators = new List<OperatorState> { _killer, _ally, _victim };

            _neutralize = new NeutralizeRules(
                _statuses, abilities, _energy, _operators, new[] { _red, _blue }, combat);

            _clock.BeginTurnFor(PlayerColor.Red);
        }

        /// <summary>
        /// Tops a pool up to a known figure through the ledger.
        /// </summary>
        /// <remarks>
        /// <c>PlayerState.SetEnergy</c> is internal to the core, which is
        /// correct — the ledger owns the pool (§3). Two capped grants reach the
        /// ceiling, and the remainder is spent back down.
        /// </remarks>
        private void Fund(PlayerState player, int amount)
        {
            int cap = EnergyConfig.Default.EnergyCap;

            _energy.GrantForTurn(player, new DiceRoll(6, 6));
            player.BeginTurn();
            _energy.GrantForTurn(player, new DiceRoll(6, 6));

            if (amount < cap) _energy.Spend(player, cap - amount);
        }

        // ── The bounty ───────────────────────────────────────────────────

        [Test]
        public void AKill_CreditsTheAttackersPool()
        {
            Assert.That(_red.Energy, Is.EqualTo(0), "precondition: an empty pool");

            var outcome = _neutralize.Apply(_victim, _killer.Id);

            Assert.That(outcome.PaidABounty, Is.True);
            Assert.That(outcome.BountyPaidTo, Is.EqualTo(PlayerColor.Red));
            Assert.That(_red.Energy, Is.EqualTo(Bounty));
        }

        [Test]
        public void AKill_LeavesTheVictimsPoolAlone()
        {
            // Energy is player-level, so a yarded operator costs its owner
            // nothing economically (§1.2). The bounty changed what the attacker
            // gets, not what the victim loses.
            Fund(_blue, 7);

            _neutralize.Apply(_victim, _killer.Id);

            Assert.That(_blue.Energy, Is.EqualTo(7));
        }

        [Test]
        public void AnOperatorThatKillsItself_PaysNothing()
        {
            // All-In Mauling's self-damage is the only route (§2.3). Without
            // this the Bouncer could farm his own pool.
            var outcome = _neutralize.Apply(_ally, _ally.Id);

            Assert.That(outcome.PaidABounty, Is.False);
            Assert.That(_red.Energy, Is.EqualTo(0));
        }

        [Test]
        public void AKillOfYourOwnSide_PaysNothing()
        {
            var outcome = _neutralize.Apply(_ally, _killer.Id);   // both Red

            Assert.That(outcome.PaidABounty, Is.False);
            Assert.That(_red.Energy, Is.EqualTo(0));
        }

        [Test]
        public void AKillWithNoKnownKiller_PaysNothing()
        {
            var outcome = _neutralize.Apply(_victim);

            Assert.That(outcome.PaidABounty, Is.False);
            Assert.That(_red.Energy, Is.EqualTo(0));
        }

        [Test]
        public void ABankedPlayer_CollectsNothingAndBurnsNothing()
        {
            // Holding a full pool is a strategy. Its cost is already the
            // abilities not cast; the system does not add one, and the burn
            // figure stays a measurement of the economy rather than of a reward
            // (§3.1).
            Fund(_red, EnergyConfig.Default.EnergyCap);

            var outcome = _neutralize.Apply(_victim, _killer.Id);

            Assert.That(_red.Energy, Is.EqualTo(EnergyConfig.Default.EnergyCap));
            Assert.That(outcome.Bounty.Stored, Is.EqualTo(0));
            Assert.That(outcome.Bounty.Burned, Is.EqualTo(0), "a bounty never burns");
        }

        [Test]
        public void ABountyIsCappedRatherThanRefused()
        {
            // One short of the cap: the kill pays one and not three.
            Fund(_red, EnergyConfig.Default.EnergyCap - 1);

            var outcome = _neutralize.Apply(_victim, _killer.Id);

            Assert.That(_red.Energy, Is.EqualTo(EnergyConfig.Default.EnergyCap));
            Assert.That(outcome.Bounty.Stored, Is.EqualTo(1));
            Assert.That(outcome.Bounty.Burned, Is.EqualTo(0));
        }

        [Test]
        public void ABountyOfZero_DisablesTheRewardEntirely()
        {
            // The configuration every figure in COMBAT_SYSTEMS §12 was measured
            // under, and the one a sweep re-runs to compare against them.
            var combat = new CombatConfig(neutralizeEnergyBounty: 0);
            var targeting = new TargetingRules(_map, _statuses);
            var damage = new DamagePipeline(_statuses, new SeededRandom(1));
            var cellEffects = new DeferredCellEffects(_clock, targeting, damage, _statuses);
            var abilities = new AbilityResolver(_map, _clock, _energy, _statuses, targeting, damage, cellEffects);

            var unrewarded = new NeutralizeRules(
                _statuses, abilities, _energy, _operators, new[] { _red, _blue }, combat);

            Assert.That(unrewarded.Apply(_victim, _killer.Id).PaidABounty, Is.False);
            Assert.That(_red.Energy, Is.EqualTo(0));
        }

        // ── Knockout credit (GUI increment I) ────────────────────────────

        [Test]
        public void AKill_IsCreditedToTheKillersSeat()
        {
            Assert.That(_neutralize.Apply(_victim, _killer.Id).CreditedTo, Is.EqualTo(PlayerColor.Red));
        }

        [Test]
        public void SelfFriendlyAndUnknownKills_AreCreditedToNobody()
        {
            Assert.That(_neutralize.Apply(_ally, _ally.Id).CreditedTo, Is.Null, "self");
            Assert.That(_neutralize.Apply(_ally, _killer.Id).CreditedTo, Is.Null, "own side");
            Assert.That(_neutralize.Apply(_victim).CreditedTo, Is.Null, "no known killer");
        }

        [Test]
        public void Credit_DoesNotDependOnTheBounty()
        {
            // Match stats must not change when the bounty is tuned to zero.
            var combat = new CombatConfig(neutralizeEnergyBounty: 0);
            var targeting = new TargetingRules(_map, _statuses);
            var damage = new DamagePipeline(_statuses, new SeededRandom(1));
            var cellEffects = new DeferredCellEffects(_clock, targeting, damage, _statuses);
            var abilities = new AbilityResolver(_map, _clock, _energy, _statuses, targeting, damage, cellEffects);

            var unrewarded = new NeutralizeRules(
                _statuses, abilities, _energy, _operators, new[] { _red, _blue }, combat);

            var outcome = unrewarded.Apply(_victim, _killer.Id);

            Assert.That(outcome.PaidABounty, Is.False);
            Assert.That(outcome.CreditedTo, Is.EqualTo(PlayerColor.Red));
        }

        // ── The §1.2 consequences ────────────────────────────────────────

        [Test]
        public void ANeutralizedOperator_ReturnsToItsYardAtFullHealth()
        {
            _victim.SetHealth(0);

            _neutralize.Apply(_victim, _killer.Id);

            Assert.That(_victim.IsInYard, Is.True);
            Assert.That(_victim.Health, Is.EqualTo(_victim.MaxHealth));
        }

        // ── The mark payout ──────────────────────────────────────────────

        [Test]
        public void AMarkedVictim_HastensTheMarkersWholeSquad()
        {
            _statuses.Apply(_victim, StatusKind.Mark, duration: 2, sourceOperatorId: _killer.Id);
            _clock.BeginTurnFor(PlayerColor.Blue);      // the mark takes hold on the target's turn

            var outcome = _neutralize.Apply(_victim, _killer.Id);

            Assert.That(outcome.Hastened.Count, Is.EqualTo(2), "both Red operators");
            Assert.That(outcome.Hastened.All(o => o.Owner == PlayerColor.Red), Is.True);
        }

        [Test]
        public void AnUnmarkedVictim_HastensNobody()
        {
            var outcome = _neutralize.Apply(_victim, _killer.Id);

            Assert.That(outcome.Hastened, Is.Empty);
        }

        // ── CPR’s save (§10.5, 2026-10-01) ─────────────────────────────

        /// <summary>
        /// Takes the victim to zero the way the pipeline would, so the save is
        /// answering the same state a real knockout answers.
        /// </summary>
        private void DropToZero(OperatorState op) => op.SetHealth(0);

        /// <summary>
        /// Primes an operator and leaves the clock on its own seat.
        /// </summary>
        /// <remarks>
        /// The clock move is the point. A status applied outside its holder’s
        /// own turn only takes hold on that holder’s next one (§5), and this
        /// fixture’s victim belongs to the seat that is <i>not</i> to play. In a
        /// real match that case cannot arise: CPR is ally-only, so the seat
        /// priming an operator is always the seat that owns it, and the save is
        /// live from the moment it is cast.
        /// </remarks>
        private void Prime(OperatorState op)
        {
            _clock.BeginTurnFor(op.Owner);
            _statuses.Apply(op, StatusKind.Resuscitation, Javi.CprTurns);
            Assert.That(_statuses.Has(op, StatusKind.Resuscitation), Is.True, "precondition: primed");
        }

        [Test]
        public void APrimedVictim_StaysOnItsCell_AtOneHealth()
        {
            // The whole point: progress is what a knockout costs, and the save
            // refuses that rather than the damage.
            Prime(_victim);
            DropToZero(_victim);

            var outcome = _neutralize.Apply(_victim, _killer.Id);

            Assert.That(outcome.Saved, Is.True);
            Assert.That(_victim.IsInYard, Is.False, "it never went to the yard");
            Assert.That(_victim.Progress, Is.EqualTo(12), "and it did not move");
            Assert.That(_victim.Health, Is.EqualTo(1), "barely standing, and not healed");
        }

        [Test]
        public void TheSave_IsSpent_SoASecondBlowFinishesTheJob()
        {
            // A reprieve, not a second life.
            Prime(_victim);
            DropToZero(_victim);
            _neutralize.Apply(_victim, _killer.Id);

            DropToZero(_victim);
            var second = _neutralize.Apply(_victim, _killer.Id);

            Assert.That(second.Saved, Is.False);
            Assert.That(_victim.IsInYard, Is.True);
        }

        [Test]
        public void TheSave_PaysNoBounty_AndCreditsNobody()
        {
            Fund(_red, 0);
            Prime(_victim);
            DropToZero(_victim);

            var outcome = _neutralize.Apply(_victim, _killer.Id);

            Assert.That(outcome.PaidABounty, Is.False, "nobody died, so nothing is owed");
            Assert.That(outcome.CreditedTo, Is.Null, "and the knockout does not count");
            Assert.That(_red.Energy, Is.EqualTo(0));
        }

        [Test]
        public void TheSave_DeniesTheMarkPayout_AndLeavesNobodyHastened()
        {
            // Tagged From Above pays on a kill. A rescue is not a kill, and the
            // mark comes off with everything else, so it cannot pay later either.
            Prime(_victim);                          // leaves the clock on Blue
            _statuses.Apply(_victim, StatusKind.Mark, duration: 2, sourceOperatorId: _killer.Id);
            Assert.That(_statuses.Has(_victim, StatusKind.Mark), Is.True,
                "precondition: applied on its own turn, so it holds at once");
            DropToZero(_victim);

            var outcome = _neutralize.Apply(_victim, _killer.Id);

            Assert.That(outcome.Saved, Is.True);
            Assert.That(outcome.Hastened, Is.Empty);
            Assert.That(_statuses.Has(_victim, StatusKind.Mark), Is.False, "washed off by the rescue");
        }

        [Test]
        public void TheSave_WashesOffEverythingElse()
        {
            // It carries the game’s only cleanse since CPR replaced Neural
            // Purge — at the moment of rescue, never on demand.
            Prime(_victim);
            _statuses.Apply(_victim, StatusKind.Bleed, 3);
            _statuses.Apply(_victim, StatusKind.Slow, 3);
            _statuses.Apply(_victim, StatusKind.ZeroDayCharge, 3, sourceOperatorId: _killer.Id);
            DropToZero(_victim);

            _neutralize.Apply(_victim, _killer.Id);

            Assert.That(_statuses.Has(_victim, StatusKind.Bleed), Is.False);
            Assert.That(_statuses.Has(_victim, StatusKind.Slow), Is.False);
            Assert.That(_statuses.Has(_victim, StatusKind.ZeroDayCharge), Is.False);
            Assert.That(_statuses.Has(_victim, StatusKind.Resuscitation), Is.False, "and itself");
        }

        [Test]
        public void AnUnprimedVictim_IsYardedExactlyAsBefore()
        {
            // The pairing that proves the tests above measure the save rather
            // than a neutralize that stopped working.
            DropToZero(_victim);

            var outcome = _neutralize.Apply(_victim, _killer.Id);

            Assert.That(outcome.Saved, Is.False);
            Assert.That(_victim.IsInYard, Is.True);
            Assert.That(_victim.Health, Is.EqualTo(_victim.MaxHealth), "a yarded operator comes back whole");
        }

        [Test]
        public void TheSave_AnswersASelfInflictedZero_WithNoKiller()
        {
            // All-In Mauling’s recoil and a bleed tick both reach here with no
            // creditable killer. The save is about the victim, not the source.
            Prime(_victim);
            DropToZero(_victim);

            var outcome = _neutralize.Apply(_victim);

            Assert.That(outcome.Saved, Is.True);
            Assert.That(_victim.IsInYard, Is.False);
        }
    }
}