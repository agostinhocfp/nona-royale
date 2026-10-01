// Assets/Tests/EditMode/Abilities/JaviTests.cs
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
    /// Nanite Infusion's runoff heal and the scope it rides on
    /// (<see cref="EffectScope.AlliesAroundCaster"/>, COMBAT_SYSTEMS §10.5,
    /// designer 2026-10-01): the hostile cast wounds the target and heals Javi
    /// and every ally within <see cref="Javi.RunoffRadius"/> of <b>Javi</b>.
    /// </summary>
    /// <remarks>
    /// <b>The first tests this clause has ever had, and the reason it needed
    /// them.</b> Before 2026-10-01 the splash heal was anchored on the primary
    /// target at radius 2. Moving it onto the caster at radius 5 changed who it
    /// heals in every cast in the game, and the whole suite stayed green —
    /// <c>SelfTargetTests</c> covers the ability's damage and its self-cast, and
    /// nothing anywhere asserted which operators the runoff reached. The most
    /// cast ability in Javi's kit had an untested half.
    ///
    /// Wired from the services, like <c>SelfTargetTests</c>. The FakeClock lives
    /// in <c>AbilityResolverTests</c>, same namespace.
    /// </remarks>
    [TestFixture]
    public class JaviTests
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
        private OperatorState _near;
        private OperatorState _edge;
        private OperatorState _beyond;
        private OperatorState _foe;
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

            // TRACK cells, never progress, and none is a safe start cell
            // (multiples of 13). Javi at 20; allies at 22, at 25 — exactly
            // RunoffRadius away — and at 26, one cell past it. The foe sits at
            // 16, four back: inside Javi's cast range of 5, and far enough from
            // the two outer allies that the OLD target-anchored radius of 2
            // would not have reached them. That gap is what the tests measure.
            _javi = AtTrack(1, "Javi", PlayerColor.Red, Javi.MaxHealth, 20);
            _near = AtTrack(2, "Near", PlayerColor.Red, 8, 22);
            _edge = AtTrack(3, "Edge", PlayerColor.Red, 8, 25);
            _beyond = AtTrack(4, "Beyond", PlayerColor.Red, 8, 26);
            _foe = AtTrack(5, "Foe", PlayerColor.Blue, 8, 16);

            _red = new PlayerState(PlayerColor.Red, new[] { _javi, _near, _edge, _beyond });
            _blue = new PlayerState(PlayerColor.Blue, new[] { _foe });
            _board = new List<OperatorState> { _javi, _near, _edge, _beyond, _foe };

            _clock.BeginTurnFor(PlayerColor.Red);
            _red.BeginTurn();
            Fund(12);
        }

        // ── Helpers ──────────────────────────────────────────────────────

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

        private void Fund(int amount)
        {
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6
            _red.BeginTurn();
            _energy.GrantForTurn(_red, new DiceRoll(6, 6));   // +6, capped at 12
            if (amount < 12) _energy.Spend(_red, 12 - amount);
        }

        /// <summary>Everyone starts one below full, so a heal of 1 is visible and nothing is capped.</summary>
        private void WoundTheSquad()
        {
            foreach (var op in new[] { _javi, _near, _edge, _beyond })
                op.SetHealth(op.MaxHealth - 1);
        }

        private AbilityResolution CastAtTheFoe() =>
            _abilities.Use(_javi, Javi.NaniteInfusion, _foe, _red, _board);

        // ── The runoff heal's scope ──────────────────────────────────────

        [Test]
        public void TheRunoffRadius_IsFive()
        {
            // The designer's number (2026-10-01). A test so that changing it is
            // a deliberate act that also updates §10.5.
            Assert.That(Javi.RunoffRadius, Is.EqualTo(5));
        }

        [Test]
        public void TheHostileCast_HealsJaviHimself()
        {
            // He stands at distance zero from his own cell, so this is
            // unconditional: every hostile cast returns him a point, with no
            // positioning asked and no second ability spent.
            WoundTheSquad();

            var result = CastAtTheFoe();

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_javi.Health, Is.EqualTo(Javi.MaxHealth), "the caster is always in his own radius");
        }

        [Test]
        public void TheHostileCast_HealsAlliesWithinFiveOfJavi()
        {
            WoundTheSquad();

            CastAtTheFoe();

            Assert.That(_near.Health, Is.EqualTo(8), "two cells from Javi");
            Assert.That(_edge.Health, Is.EqualTo(8), "exactly five from Javi — the edge still counts");
        }

        [Test]
        public void TheHostileCast_LeavesAnAllySixCellsOut()
        {
            // The pairing that keeps the radius honest. Without this the test
            // above would pass just as well on a heal that reached everyone.
            WoundTheSquad();

            CastAtTheFoe();

            Assert.That(_beyond.Health, Is.EqualTo(7), "six cells from Javi is outside the runoff");
        }

        [Test]
        public void TheRunoff_IsMeasuredFromJavi_NotFromTheTarget()
        {
            // The change itself, stated as a single assertion. The foe at 16 is
            // nine cells from the ally at 25, so under the old
            // AlliesAroundPrimaryTarget radius 2 that ally healed for nothing.
            // It heals now because the anchor moved, not because the number did.
            WoundTheSquad();

            CastAtTheFoe();

            Assert.That(_edge.Health, Is.EqualTo(8));
            Assert.That(_foe.Health, Is.EqualTo(6), "and the target still takes its 2 Normal");
        }

        [Test]
        public void TheRunoff_HealsNobodyAlreadyAtFullHealth()
        {
            // A heal on a healthy squad is not an error and not an outcome. This
            // pins that the clause reports nothing rather than reporting zeroes.
            var result = CastAtTheFoe();

            Assert.That(result.Approved, Is.True);
            Assert.That(_javi.Health, Is.EqualTo(Javi.MaxHealth));
            Assert.That(_near.Health, Is.EqualTo(8));
            Assert.That(result.Outcomes.Any(o =>
                o.Kind == EffectOutcomeKind.Healed && ReferenceEquals(o.Recipient, _near)), Is.False,
                "nothing to heal, so nothing is reported");
        }

        [Test]
        public void TheRunoff_NeverHealsTheEnemyItWasAimedAt()
        {
            // The scope is allies of the caster, so a wounded foe standing on
            // Javi's own doorstep is still only a recipient of the damage.
            _foe.SetHealth(4);

            CastAtTheFoe();

            Assert.That(_foe.Health, Is.EqualTo(2), "2 Normal, and not a point of healing");
        }

        [Test]
        public void TheFriendlyCast_StillHealsOnlyItsTarget()
        {
            // The runoff clause is EnemyOnly, so it belongs to the hostile mode
            // and is scoped away entirely when Javi aims at a friend. The ally
            // beside him must not pick up a second point.
            WoundTheSquad();

            var result = _abilities.Use(_javi, Javi.NaniteInfusion, _near, _red, _board);

            Assert.That(result.Approved, Is.True, result.ToString());
            Assert.That(_near.Health, Is.EqualTo(8), "the friendly heal of 2, capped at maximum");
            Assert.That(_javi.Health, Is.EqualTo(Javi.MaxHealth - 1), "no runoff on a friendly cast");
            Assert.That(_edge.Health, Is.EqualTo(7), "nor for anyone else standing near him");
        }
    }
}
