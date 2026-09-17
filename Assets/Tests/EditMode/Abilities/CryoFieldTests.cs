// Assets/Tests/EditMode/Abilities/CryoFieldTests.cs
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
    /// Mimi's Cryo Field: the first self-anchored field (§6.6), and the first
    /// status that damages an area at its holder's upkeep (§5.14). It follows
    /// her between upkeeps, ticks exactly twice, and ends when she does.
    /// </summary>
    [TestFixture]
    public class CryoFieldTests
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
        private NeutralizeRules _neutralize;

        private OperatorState _mimi;
        private OperatorState _ally;
        private OperatorState _near;
        private OperatorState _far;
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
            _abilities = new AbilityResolver(
                _map, _clock, _energy, _statuses, _targeting, _damage, _cellEffects, _operatorEffects);

            // Positions are stated as TRACK cells, never progress. Mimi at 10,
            // her ally at 11 (inside the field, friendly), an enemy at 13 (at
            // the field's edge, radius 3) and one at 14 (one step past it).
            _mimi = AtTrack(1, "Mimi", PlayerColor.Red, Mimi.MaxHealth, 10);
            _ally = AtTrack(2, "Ally", PlayerColor.Red, 6, 11);
            _near = AtTrack(3, "Near", PlayerColor.Blue, 6, 13);
            _far = AtTrack(4, "Far", PlayerColor.Blue, 6, 14);

            _red = new PlayerState(PlayerColor.Red, new[] { _mimi, _ally });
            _blue = new PlayerState(PlayerColor.Blue, new[] { _near, _far });
            _board = new List<OperatorState> { _mimi, _ally, _near, _far };

            // Wired exactly as MatchFactory wires it: neutralize is how a
            // marker gets stripped when the holder goes down (§1.2, §5.14).
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

        /// <summary>Tops the pool up to a known figure without going through the dice.</summary>
        private void Fund(int amount)
        {
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6
            _red.BeginTurn();
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6, capped at 12
            if (amount < 12) _energy.Spend(_red, 12 - amount);
        }

        private AbilityResolution Cast() =>
            _abilities.Use(_mimi, Mimi.CryoField, null, _red, _board);

        /// <summary>Passes the intervening opponent turn and starts Mimi's next one.</summary>
        private void AdvanceToHerNextUpkeep()
        {
            _clock.BeginTurnFor(PlayerColor.Blue);
            _clock.BeginTurnFor(PlayerColor.Red);
        }

        /// <summary>Her end-of-turn sweep — where status durations expire (§5).</summary>
        private void EndHerTurn() => _statuses.ExpireCompleted(_mimi);

        private IReadOnlyList<OperatorEffectResolution> Fire() =>
            _operatorEffects.Fire(PlayerColor.Red, _board);

        [Test]
        public void CryoField_AppliesTheMarkerAndTelegraphs_AndNothingTicksYet()
        {
            // The delay is the mechanic, and the telegraph is the counterplay:
            // an event announces the field and the badge keeps it on the board,
            // but nobody is bitten by the cast itself.
            var result = Cast();

            Assert.That(result.Approved, Is.True);
            Assert.That(result.Outcomes.Any(o => o.Kind == EffectOutcomeKind.FieldProjected), Is.True,
                "the telegraph the zoning is balanced around");
            Assert.That(_statuses.Has(_mimi, StatusKind.CryoField), Is.True,
                "self-applied on her own turn, so it takes hold immediately (§5)");
            Assert.That(_operatorEffects.HasFieldOn(_mimi, PlayerColor.Red), Is.True);
            Assert.That(_near.Health, Is.EqualTo(6), "nothing ticks at cast time");
        }

        [Test]
        public void CryoField_RequiresNoTarget()
        {
            // A self-origin field asks the player for nothing — no operator, no
            // cell (§10.4).
            Assert.That(Mimi.CryoField.Targeting, Is.EqualTo(AbilityTargeting.None));
            Assert.That(Mimi.CryoField.RequiresTarget, Is.False);
            Assert.That(Mimi.CryoField.RequiresCell, Is.False);
            Assert.That(_abilities.LegalTargets(_mimi, Mimi.CryoField, _board), Is.Empty);

            Assert.That(Cast().Approved, Is.True, "a null target is the cast");
        }

        [Test]
        public void CryoField_FirstTickLandsAtHerNextUpkeep_NotImmediately()
        {
            Cast();

            AdvanceToHerNextUpkeep();
            var fired = Fire();

            Assert.That(fired.Count, Is.EqualTo(1));
            Assert.That(fired[0].Cause, Is.EqualTo(DeferredOperatorEffects.FieldCause));
            Assert.That(fired[0].Cell, Is.EqualTo(CellRef.Track(10)), "centred on where she stands");
            Assert.That(_near.Health, Is.EqualTo(4), "2 Normal at the field's edge");
            Assert.That(_far.Health, Is.EqualTo(6), "one step past the radius");
        }

        [Test]
        public void CryoField_TicksExactlyTwice_ThenExpires()
        {
            // The design row says 2 turns; the registry counts a self-applied
            // status's cast turn as its first, so the marker lasts three of her
            // turns and bills at the two upkeeps inside that span (§5, §6.6).
            Cast();

            AdvanceToHerNextUpkeep();
            Fire();
            Assert.That(_near.Health, Is.EqualTo(4), "first tick");
            EndHerTurn();
            Assert.That(_statuses.Has(_mimi, StatusKind.CryoField), Is.True,
                "still standing after her first ticked turn");

            AdvanceToHerNextUpkeep();
            Fire();
            Assert.That(_near.Health, Is.EqualTo(2), "second tick");
            EndHerTurn();
            Assert.That(_statuses.Has(_mimi, StatusKind.CryoField), Is.False,
                "the marker expires at the end of the second ticked turn");

            AdvanceToHerNextUpkeep();
            var fired = Fire();
            Assert.That(fired, Is.Empty, "a third upkeep finds no field");
            Assert.That(_operatorEffects.HasFieldOn(_mimi, PlayerColor.Red), Is.False,
                "and the pending entry is retired with it");
            Assert.That(_near.Health, Is.EqualTo(2), "no third tick");
        }

        [Test]
        public void CryoField_FollowsHerBetweenUpkeeps()
        {
            // "Enemies near her" means where she stands at each upkeep, not
            // where she cast it — the field is anchored to a body, not a cell
            // (§6.6), which is the zoning the ability is for.
            Cast();
            _mimi.MoveTo(ProgressAtTrack(PlayerColor.Red, 20));
            _far.MoveTo(ProgressAtTrack(PlayerColor.Blue, 21));

            AdvanceToHerNextUpkeep();
            var fired = Fire();

            Assert.That(fired[0].Cell, Is.EqualTo(CellRef.Track(20)), "the field moved with her");
            Assert.That(_far.Health, Is.EqualTo(4), "near her new cell");
            Assert.That(_near.Health, Is.EqualTo(6), "left behind, out of the cold");
        }

        [Test]
        public void CryoField_HarmsEnemiesOnly_AndOnlyWithinRadius()
        {
            Cast();

            AdvanceToHerNextUpkeep();
            Fire();

            Assert.That(_near.Health, Is.EqualTo(4), "an enemy at the radius edge is caught");
            Assert.That(_far.Health, Is.EqualTo(6), "an enemy past it is not");
            Assert.That(_ally.Health, Is.EqualTo(6), "the field is an enemy weapon");
            Assert.That(_mimi.Health, Is.EqualTo(Mimi.MaxHealth), "and never bites its carrier");
        }

        [Test]
        public void CryoField_TickIsNormal_AShieldPoolAbsorbsIt()
        {
            // §2.2: a self-centred emission is Normal, not Tech — so the
            // ordinary mitigation layers apply (§2.1). An Atomic tick would
            // have gone straight through the plate.
            _statuses.Apply(_near, StatusKind.Shield, duration: 3, magnitude: 2,
                sourceOperatorId: _ally.Id);

            Cast();
            AdvanceToHerNextUpkeep();
            var fired = Fire();

            Assert.That(fired[0].Damage.Count, Is.EqualTo(1));
            Assert.That(fired[0].Damage[0].Outcome, Is.EqualTo(DamageOutcome.Absorbed));
            Assert.That(_near.Health, Is.EqualTo(6), "the pool ate the whole tick");
            Assert.That(_statuses.ShieldPool(_near), Is.EqualTo(0), "and paid both points for it");
        }

        [Test]
        public void CryoField_EndsWhenMimiIsNeutralized()
        {
            // A field is anchored to her body, not deployed like a beacon:
            // neutralize strips the marker with every other applied status
            // (§1.2), and a field centred on an operator in her yard centres on
            // nowhere. The follow-up precedent, not the beacon one (§6.5).
            Cast();

            var killed = _damage.Apply(_mimi, new DamageInstance(
                Mimi.MaxHealth, DamageType.Normal, _near.Id, "ability"));
            Assert.That(killed.Outcome, Is.EqualTo(DamageOutcome.Neutralized), "precondition");
            _neutralize.Apply(_mimi, _near.Id);
            Assert.That(_mimi.IsInYard, Is.True, "precondition");

            AdvanceToHerNextUpkeep();
            var fired = Fire();

            Assert.That(fired, Is.Empty, "the field died with her");
            Assert.That(_operatorEffects.HasFieldOn(_mimi, PlayerColor.Red), Is.False);
            Assert.That(_near.Health, Is.EqualTo(6));
        }

        [Test]
        public void CryoField_CleanseStripsTheMarker_AndCancelsTheField()
        {
            // The marker is the field: stripping it — an indiscriminate
            // friendly cleanse, invoked through the same registry call Neural
            // Purge makes — ends the field outright (§5.8, §5.14).
            Cast();
            Assert.That(_statuses.Has(_mimi, StatusKind.CryoField), Is.True, "precondition");

            _statuses.ClearApplied(_mimi);

            AdvanceToHerNextUpkeep();
            var fired = Fire();

            Assert.That(fired, Is.Empty, "a cleansed field never ticks");
            Assert.That(_operatorEffects.HasFieldOn(_mimi, PlayerColor.Red), Is.False);
            Assert.That(_near.Health, Is.EqualTo(6));
        }

        [Test]
        public void CryoField_RefusedOnCooldown_AndReadyAgainAfterThreeOwnerTurns()
        {
            Cast();

            var second = Cast();
            Assert.That(second.Refusal, Is.EqualTo(AbilityRefusal.OnCooldown));

            // Cooldown 3: unusable for her next three turns, back on the one
            // after — by which point the field it raised has long expired.
            for (int turn = 0; turn < 3; turn++)
            {
                _clock.BeginTurnFor(PlayerColor.Red);
                Assert.That(_abilities.IsReady(_mimi, Mimi.CryoField), Is.False,
                    $"blocked turn {turn + 1} of 3");
            }

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(_abilities.IsReady(_mimi, Mimi.CryoField), Is.True);
        }

        [Test]
        public void CryoField_RefusedWithoutEnergy_AndCostsNothing()
        {
            _energy.Spend(_red, _red.Energy);   // broke

            var result = Cast();

            Assert.That(result.Approved, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.InsufficientEnergy));
            Assert.That(_statuses.Has(_mimi, StatusKind.CryoField), Is.False,
                "a refused ability projects nothing");
            Assert.That(_operatorEffects.HasFieldOn(_mimi, PlayerColor.Red), Is.False);
            Assert.That(_abilities.IsReady(_mimi, Mimi.CryoField), Is.True,
                "and takes no cooldown");
        }
    }
}
