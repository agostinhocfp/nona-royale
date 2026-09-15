// Assets/Tests/EditMode/Abilities/ZeroDayTests.cs
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
    /// Sanity's grenade: the first operator-anchored deferred effect (§6.4).
    /// It follows the target, detonates at the caster's next upkeep on the
    /// target's current cell — or its death cell — and a cleanse cancels it
    /// by stripping the marker (§5.10).
    /// </summary>
    [TestFixture]
    public class ZeroDayTests
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

        private OperatorState _sanity;
        private OperatorState _ally;
        private OperatorState _target;
        private OperatorState _bystander;
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

            // Positions are stated as TRACK cells, never progress. Caster at
            // 10, target at 12 (inside range 2, on nobody's start cell), a
            // second enemy at 13 — one step past the target, inside the blast.
            // 13 is Blue's own start and therefore safe, which is deliberate:
            // a safe cell stops a single target being picked out, not a blast
            // (§4.4, first amendment).
            _sanity = AtTrack(1, "Sanity", PlayerColor.Red, Sanity.MaxHealth, 10);
            _ally = AtTrack(2, "Ally", PlayerColor.Red, 6, 11);
            _target = AtTrack(3, "Target", PlayerColor.Blue, 6, 12);
            _bystander = AtTrack(4, "Bystander", PlayerColor.Blue, 6, 13);

            _red = new PlayerState(PlayerColor.Red, new[] { _sanity, _ally });
            _blue = new PlayerState(PlayerColor.Blue, new[] { _target, _bystander });
            _board = new List<OperatorState> { _sanity, _ally, _target, _bystander };

            // Wired exactly as MatchFactory wires it: the neutralize path is
            // how an attached charge learns its carrier's death cell (§6.4).
            _neutralize = new NeutralizeRules(
                _statuses, _abilities, _energy, _board,
                new PlayerState[] { _red, _blue }, CombatConfig.Default, _operatorEffects);

            _clock.BeginTurnFor(PlayerColor.Red);
            _red.BeginTurn();
            Fund(12);
        }

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

        private AbilityResolution Cast(OperatorState target) =>
            _abilities.Use(_sanity, Sanity.ZeroDay, target, _red, _board);

        /// <summary>
        /// Advances past the target's turn to the caster's next upkeep — the
        /// moment the charge comes due, one full round after it was thrown.
        /// The marker only takes hold on the target's own turn (§5), so no
        /// detonation check may run before this.
        /// </summary>
        private void AdvanceToCasterUpkeep()
        {
            _clock.BeginTurnFor(PlayerColor.Blue);
            _clock.BeginTurnFor(PlayerColor.Red);
        }

        private IReadOnlyList<OperatorEffectResolution> Fire() =>
            _operatorEffects.Fire(PlayerColor.Red, _board);

        [Test]
        public void ZeroDay_AttachesAMarkerAndTelegraphsIt_AndNothingResolvesYet()
        {
            // The delay is the mechanic, and the telegraph is the counterplay:
            // an event announces the attachment and the marker keeps it on the
            // board, but nobody is struck by the throw itself.
            var result = Cast(_target);

            Assert.That(result.Approved, Is.True);
            Assert.That(result.Outcomes.Any(o => o.Kind == EffectOutcomeKind.ChargeAttached), Is.True,
                "the telegraph the ability is balanced around");
            Assert.That(_operatorEffects.HasChargeOn(_target, PlayerColor.Red), Is.True);
            Assert.That(_target.Health, Is.EqualTo(6), "nothing detonates at cast time");

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.Has(_target, StatusKind.ZeroDayCharge), Is.True,
                "the marker takes hold on the target's next turn, like any debuff");
        }

        [Test]
        public void ZeroDay_FollowsAMovedTarget_AndDetonatesOnItsNewCell()
        {
            // The opposite of a beacon: the bet is not on where the target will
            // be, because the charge goes with it.
            Cast(_target);
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 20));

            AdvanceToCasterUpkeep();
            var fired = Fire();

            Assert.That(fired.Count, Is.EqualTo(1));
            Assert.That(fired[0].Cell, Is.EqualTo(CellRef.Track(20)),
                "the detonation finds the target wherever it went");
            Assert.That(_target.Health, Is.EqualTo(4), "1 splash plus 1 for the marked target");
            Assert.That(_bystander.Health, Is.EqualTo(6), "left behind, outside the blast");
        }

        [Test]
        public void ZeroDay_DealsTwoToTheMarkedTarget_AndOneToTheRing()
        {
            Cast(_target);

            AdvanceToCasterUpkeep();
            var fired = Fire();

            Assert.That(fired[0].DamagePerTarget, Is.EqualTo(1));
            Assert.That(fired[0].MarkedTargetBonus, Is.EqualTo(1));
            Assert.That(_target.Health, Is.EqualTo(4), "splash plus the marked bonus");
            Assert.That(_bystander.Health, Is.EqualTo(5), "splash only, safe cell or not");
            Assert.That(_ally.Health, Is.EqualTo(6), "the blast is an enemy weapon");
        }

        [Test]
        public void ZeroDay_SlowsEveryoneItCatches()
        {
            Cast(_target);

            AdvanceToCasterUpkeep();
            Fire();

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.SpeedModifier(_target), Is.EqualTo(-0.5));
            Assert.That(_statuses.SpeedModifier(_bystander), Is.EqualTo(-0.5));
        }

        [Test]
        public void ZeroDay_DetonatesOnTheDeathCell_WhenTheTargetDiesFirst()
        {
            // An attached device outlives its carrier exactly as it outlives
            // its caster: the grenade goes off where the target fell, with no
            // living recipient for the bonus (§6.4).
            Cast(_target);

            var killed = _damage.Apply(_target, new DamageInstance(6, DamageType.Normal, _ally.Id, "ability"));
            Assert.That(killed.Outcome, Is.EqualTo(DamageOutcome.Neutralized), "precondition");
            _neutralize.Apply(_target, _ally.Id);
            Assert.That(_target.IsInYard, Is.True, "precondition");

            AdvanceToCasterUpkeep();
            var fired = Fire();

            Assert.That(fired.Count, Is.EqualTo(1));
            Assert.That(fired[0].Cell, Is.EqualTo(CellRef.Track(12)), "the death cell");
            Assert.That(fired[0].MarkedTargetBonus, Is.EqualTo(0), "no living recipient for the bonus");
            Assert.That(fired[0].Caught.Contains(_target), Is.False, "the dead are not caught");
            Assert.That(_bystander.Health, Is.EqualTo(5), "the ring still takes the splash");
        }

        [Test]
        public void ZeroDay_CleanseCancelsTheDetonation()
        {
            // The marker is the attachment: stripping it — Javi's Neural Purge,
            // invoked here through the same registry call it makes — cancels
            // the grenade outright (§5.10).
            Cast(_target);

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.Has(_target, StatusKind.ZeroDayCharge), Is.True, "precondition");

            _statuses.ClearApplied(_target);

            _clock.BeginTurnFor(PlayerColor.Red);
            var fired = Fire();

            Assert.That(fired, Is.Empty, "a cleansed charge never goes off");
            Assert.That(_operatorEffects.HasChargeOn(_target, PlayerColor.Red), Is.False);
            Assert.That(_target.Health, Is.EqualTo(6));
            Assert.That(_bystander.Health, Is.EqualTo(6));
        }

        [Test]
        public void ZeroDay_SurvivesTheCastersDeath_AndStillCreditsIt()
        {
            // A deployed device is not its operator (ADR-0006), and letting a
            // kill refund the spent energy would make the ability worse than
            // it reads.
            Cast(_target);

            var killed = _damage.Apply(_sanity, new DamageInstance(
                Sanity.MaxHealth, DamageType.Normal, _target.Id, "ability"));
            Assert.That(killed.Outcome, Is.EqualTo(DamageOutcome.Neutralized), "precondition");
            _neutralize.Apply(_sanity, _target.Id);

            AdvanceToCasterUpkeep();
            var fired = Fire();

            Assert.That(fired.Count, Is.EqualTo(1));
            Assert.That(fired[0].SourceOperatorId, Is.EqualTo(_sanity.Id));
            Assert.That(_target.Health, Is.EqualTo(4));
        }

        [Test]
        public void ZeroDay_DetonationConsumesTheMarker()
        {
            // A spent grenade does not keep drawing a badge, and the target it
            // leaves behind must not read as still carrying one.
            Cast(_target);

            AdvanceToCasterUpkeep();
            Fire();

            Assert.That(_statuses.Has(_target, StatusKind.ZeroDayCharge), Is.False);
        }

        [Test]
        public void ZeroDay_AimedAtAnAlly_IsRefusedAsWrongSide()
        {
            int before = _red.Energy;

            var result = Cast(_ally);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.WrongSide));
            Assert.That(_red.Energy, Is.EqualTo(before), "a refusal costs nothing");
        }
    }
}
