// Assets/Tests/EditMode/Teams/TeamRulesTests.cs
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

namespace NonaRoyale.Core.Tests.Teams
{
    /// <summary>
    /// What a crossed 1v1 changes about the rules (ADR-0012), one service at a
    /// time: Green is Red's partner, Blue and Violet are the opposition.
    /// </summary>
    /// <remarks>
    /// <b>Every test here has a free-for-all twin asserted in the same
    /// method.</b> "Red does not collide with Green" is only interesting
    /// alongside "and it still does under free-for-all" — a bug that made
    /// everyone friendly would otherwise pass the whole fixture.
    ///
    /// Positions are stated as <i>track indices</i> and converted to
    /// per-colour progress, for the reason <c>TargetingRulesTests</c> gives:
    /// progress is relative to a colour's own start, so two seats at the same
    /// progress are nowhere near each other.
    /// </remarks>
    [TestFixture]
    public class TeamRulesTests
    {
        private const int RedId = 1;
        private const int GreenId = 2;
        private const int BlueId = 3;

        private PathMap _map;
        private int _circuit;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _circuit = BoardProfile.Standard.CircuitLength;
        }

        // ── Collision (§4.5) ─────────────────────────────────────────────

        [Test]
        public void Partners_StackInsteadOfColliding()
        {
            // Red lands exactly where Green is standing, on an ordinary cell.
            int cell = FirstUnsafeTrackCell();

            Assert.That(CollisionHappened(TeamMap.CrossedPairs, PlayerColor.Green, cell), Is.False,
                "in a crossed 1v1 Green is Red's partner; landing on it is not an attack");

            Assert.That(CollisionHappened(TeamMap.FreeForAll, PlayerColor.Green, cell), Is.True,
                "under free-for-all Green is an opponent and the landing is a collision");
        }

        [Test]
        public void TheOppositionIsStillHitInATeamMatch()
        {
            int cell = FirstUnsafeTrackCell();

            Assert.That(CollisionHappened(TeamMap.CrossedPairs, PlayerColor.Blue, cell), Is.True);
            Assert.That(CollisionHappened(TeamMap.CrossedPairs, PlayerColor.Violet, cell), Is.True);
        }

        private bool CollisionHappened(TeamMap teams, PlayerColor occupantSeat, int trackCell)
        {
            var statuses = new StatusRegistry(new FakeClock(), CombatConfig.Default, teams);
            var damage = new DamagePipeline(statuses, new SeededRandom(1));
            var movement = new MovementResolver(_map, GameConfig.Default);
            var collisions = new CollisionResolver(_map, CombatConfig.Default, damage, movement, teams);

            var mover = At(RedId, PlayerColor.Red, trackCell - 3);
            var occupant = At(GreenId, occupantSeat, trackCell);

            var move = movement.ResolveMove(mover, 3);
            Assert.That(_map.CellAt(mover.Owner, move.To).Index, Is.EqualTo(trackCell),
                "fixture is wrong: the mover did not land on the occupied cell");

            var result = collisions.Resolve(mover, move, new[] { mover, occupant });
            return result.Occurred;
        }

        // ── Targeting (§4) ───────────────────────────────────────────────

        [Test]
        public void AnAreaSweepsTheOppositionAndSparesThePartner()
        {
            var team = Targeting(TeamMap.CrossedPairs);
            var ffa = Targeting(TeamMap.FreeForAll);

            var red = At(RedId, PlayerColor.Red, 0);
            var green = At(GreenId, PlayerColor.Green, 1);
            var blue = At(BlueId, PlayerColor.Blue, 2);
            var all = new[] { red, green, blue };

            var origin = _map.CellAt(red.Owner, red.Progress);

            Assert.That(team.EnemiesInArea(origin, 3, PlayerColor.Red, all).Select(o => o.Id),
                Is.EquivalentTo(new[] { BlueId }));

            Assert.That(ffa.EnemiesInArea(origin, 3, PlayerColor.Red, all).Select(o => o.Id),
                Is.EquivalentTo(new[] { GreenId, BlueId }));
        }

        [Test]
        public void AnAlliedSplashReachesThePartnersOperators()
        {
            var team = Targeting(TeamMap.CrossedPairs);
            var ffa = Targeting(TeamMap.FreeForAll);

            var red = At(RedId, PlayerColor.Red, 0);
            var green = At(GreenId, PlayerColor.Green, 1);
            var blue = At(BlueId, PlayerColor.Blue, 2);
            var all = new[] { red, green, blue };

            var origin = _map.CellAt(red.Owner, red.Progress);

            Assert.That(team.AlliesInArea(origin, 3, PlayerColor.Red, all).Select(o => o.Id),
                Is.EquivalentTo(new[] { RedId, GreenId }),
                "Javi's splash heal reaches the partnership, not just the seat");

            Assert.That(ffa.AlliesInArea(origin, 3, PlayerColor.Red, all).Select(o => o.Id),
                Is.EquivalentTo(new[] { RedId }));
        }

        [Test]
        public void ALineAheadSkipsThePartnerStandingInIt()
        {
            var team = Targeting(TeamMap.CrossedPairs);
            var ffa = Targeting(TeamMap.FreeForAll);

            var red = At(RedId, PlayerColor.Red, 0);
            var green = At(GreenId, PlayerColor.Green, 2);
            var blue = At(BlueId, PlayerColor.Blue, 3);
            var all = new[] { red, green, blue };

            Assert.That(team.EnemiesInLineAhead(red, 4, all).Select(o => o.Id),
                Is.EquivalentTo(new[] { BlueId }));

            Assert.That(ffa.EnemiesInLineAhead(red, 4, all).Select(o => o.Id),
                Is.EquivalentTo(new[] { GreenId, BlueId }));
        }

        [Test]
        public void APartnerOnASafeCellCanStillBeSupported()
        {
            // §4.4's first amendment refuses *enemy* single-targeting on a safe
            // cell. A partner is not an enemy, so the heal still lands — the
            // same carve-out an operator's own squadmate already had.
            int safe = FirstSafeTrackCell();

            var team = Targeting(TeamMap.CrossedPairs);
            var ffa = Targeting(TeamMap.FreeForAll);

            var red = At(RedId, PlayerColor.Red, safe - 2);
            var green = At(GreenId, PlayerColor.Green, safe);

            Assert.That(team.CanSingleTarget(red, green, range: 6).IsLegal, Is.True);

            Assert.That(ffa.CanSingleTarget(red, green, range: 6).Verdict,
                Is.EqualTo(TargetingVerdict.OnASafeCell));
        }

        [Test]
        public void StealthHidesFromTheOppositionOnly()
        {
            var clock = new FakeClock();
            var statuses = new StatusRegistry(clock, CombatConfig.Default, TeamMap.CrossedPairs);
            var green = At(GreenId, PlayerColor.Green, 0);

            statuses.Apply(green, StatusKind.Stealth, 3);
            clock.BeginTurnFor(PlayerColor.Green);   // a status applied off-turn takes hold on the target's

            Assert.That(statuses.CanBeSingleTargetedBy(green, PlayerColor.Red), Is.True,
                "a partner is not fooled by its own side's stealth");
            Assert.That(statuses.CanBeSingleTargetedBy(green, PlayerColor.Blue), Is.False);
            Assert.That(statuses.CanBeSingleTargetedBy(green, PlayerColor.Violet), Is.False);
        }

        // ── Neutralize (§1.2, §10.2) ─────────────────────────────────────

        [Test]
        public void KillingYourPartnerPaysNoBountyAndEarnsNoCredit()
        {
            var fixture = new NeutralizeFixture(_map, TeamMap.CrossedPairs);

            var outcome = fixture.Neutralize.Apply(fixture.Green, fixture.Red.Id);

            Assert.That(outcome.PaidABounty, Is.False,
                "a player holding two seats must not be able to farm one with the other");
            Assert.That(outcome.CreditedTo, Is.Null);
        }

        [Test]
        public void KillingTheOppositionStillPays()
        {
            var fixture = new NeutralizeFixture(_map, TeamMap.CrossedPairs);

            var outcome = fixture.Neutralize.Apply(fixture.Blue, fixture.Red.Id);

            Assert.That(outcome.BountyPaidTo, Is.EqualTo(PlayerColor.Red));
            Assert.That(outcome.CreditedTo, Is.EqualTo(PlayerColor.Red));
        }

        /// <summary>
        /// The documented exception (ADR-0012): Tagged From Above pays the
        /// marker's <i>seat</i>, not its side, so the ability is worth the
        /// same at either table.
        /// </summary>
        /// <remarks>
        /// Asserted rather than left implicit because it is the one place in
        /// the core where a colour comparison survives on purpose. Without
        /// this test, somebody tidying the last <c>Owner !=</c> in
        /// <c>NeutralizeRules</c> into a map call would silently double the
        /// ability in a 1v1 and nothing would complain.
        /// </remarks>
        [Test]
        public void AMarkPayoutHastensTheMarkersSeatOnly_AtEitherTable()
        {
            var team = new NeutralizeFixture(_map, TeamMap.CrossedPairs);
            team.Statuses.Apply(team.Blue, StatusKind.Mark, duration: 3, sourceOperatorId: team.Red.Id);
            team.Clock.BeginTurnFor(PlayerColor.Blue);   // the mark takes hold on the target's turn

            Assert.That(team.Neutralize.Apply(team.Blue, team.Violet.Id).Hastened.Select(o => o.Id),
                Is.EquivalentTo(new[] { team.Red.Id }),
                "the partner seat spent nothing on the mark and collects nothing");

            var ffa = new NeutralizeFixture(_map, TeamMap.FreeForAll);
            ffa.Statuses.Apply(ffa.Blue, StatusKind.Mark, duration: 3, sourceOperatorId: ffa.Red.Id);
            ffa.Clock.BeginTurnFor(PlayerColor.Blue);

            Assert.That(ffa.Neutralize.Apply(ffa.Blue, ffa.Violet.Id).Hastened.Select(o => o.Id),
                Is.EquivalentTo(new[] { ffa.Red.Id }));
        }

        [Test]
        public void AMarkPayoutStillCoversTheMarkersWholeSquad()
        {
            // Seat-scoped is not operator-scoped: the whole squad of the seat
            // that cast it is hastened, which is what §10.2 has always said.
            var team = new NeutralizeFixture(_map, TeamMap.CrossedPairs, squadSize: 3);
            team.Statuses.Apply(team.Blue, StatusKind.Mark, duration: 3, sourceOperatorId: team.Red.Id);
            team.Clock.BeginTurnFor(PlayerColor.Blue);

            var hastened = team.Neutralize.Apply(team.Blue, team.Violet.Id).Hastened;

            Assert.That(hastened.Count, Is.EqualTo(3));
            Assert.That(hastened.All(o => o.Owner == PlayerColor.Red), Is.True);
        }

        // ── Auras (§5.9) ─────────────────────────────────────────────────

        [Test]
        public void AnEnemyAuraDoesNotDragThePartner()
        {
            var drag = new AuraDefinition("drag", radius: 3, speedModifier: -0.5, side: AuraSide.Enemies);

            Assert.That(AuraSpeedOnGreen(TeamMap.CrossedPairs, drag), Is.EqualTo(0.0),
                "Bouncer does not slow his own side");
            Assert.That(AuraSpeedOnGreen(TeamMap.FreeForAll, drag), Is.EqualTo(-0.5));
        }

        [Test]
        public void AnAlliedAuraReachesThePartner()
        {
            var lift = new AuraDefinition("lift", radius: 3, speedModifier: 0.5, side: AuraSide.Allies);

            Assert.That(AuraSpeedOnGreen(TeamMap.CrossedPairs, lift), Is.EqualTo(0.5));
            Assert.That(AuraSpeedOnGreen(TeamMap.FreeForAll, lift), Is.EqualTo(0.0));
        }

        private double AuraSpeedOnGreen(TeamMap teams, AuraDefinition aura)
        {
            var targeting = Targeting(teams);
            var red = At(RedId, PlayerColor.Red, 0);
            var green = At(GreenId, PlayerColor.Green, 1);

            var rules = new AuraRules(targeting, new Dictionary<int, AuraDefinition> { { RedId, aura } });
            return rules.SpeedModifierFor(green, new[] { red, green });
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private TargetingRules Targeting(TeamMap teams) =>
            new TargetingRules(_map, new StatusRegistry(new FakeClock(), CombatConfig.Default, teams), teams);

        /// <summary>An operator of <paramref name="seat"/> standing on the given track cell.</summary>
        private OperatorState At(int id, PlayerColor seat, int trackCell)
        {
            int cell = ((trackCell % _circuit) + _circuit) % _circuit;
            var op = new OperatorState(id, $"{seat} op", seat, 12, 1.0);
            op.MoveTo(ProgressAtTrack(seat, cell));
            return op;
        }

        private int ProgressAtTrack(PlayerColor seat, int trackCell)
        {
            for (int p = 0; p < _circuit; p++)
            {
                if (!_map.IsOnOuterTrack(p)) continue;
                if (_map.CellAt(seat, p).Index == trackCell) return p;
            }

            Assert.Fail($"Track {trackCell} is unreachable for {seat} — has the board changed?");
            return -1;
        }

        /// <summary>
        /// A cell no seat starts on, so a collision there is not suppressed by
        /// §4.4. Found rather than hardcoded: start cells move with the board
        /// profile, and a literal would silently pick a safe cell the day the
        /// circuit length changes.
        /// </summary>
        private int FirstUnsafeTrackCell()
        {
            for (int index = 4; index < _circuit; index++)
                if (!_map.IsSafe(CellRef.Track(index))) return index;

            Assert.Fail("Every cell on the circuit is safe — has the board changed?");
            return -1;
        }

        private int FirstSafeTrackCell()
        {
            for (int index = 4; index < _circuit; index++)
                if (_map.IsSafe(CellRef.Track(index))) return index;

            Assert.Fail("No safe cell on the circuit — has the board changed?");
            return -1;
        }

        /// <summary>
        /// Four seats, one operator each, wired for a neutralize. Built by hand
        /// for the reason <c>NeutralizeRulesTests</c> gives: arranging a
        /// specific killer through the dice costs far more than it buys.
        /// </summary>
        private sealed class NeutralizeFixture
        {
            public NeutralizeFixture(PathMap map, TeamMap teams, int squadSize = 1)
            {
                var combat = new CombatConfig(neutralizeEnergyBounty: 3);

                Clock = new FakeClock();
                var clock = Clock;

                Statuses = new StatusRegistry(clock, combat, teams);

                var energy = new EnergyLedger(EnergyConfig.Default);
                var targeting = new TargetingRules(map, Statuses, teams);
                var damage = new DamagePipeline(Statuses, new SeededRandom(1));
                var cellEffects = new DeferredCellEffects(clock, targeting, damage, Statuses);
                var abilities = new AbilityResolver(
                    map, clock, energy, Statuses, targeting, damage, cellEffects);

                Red = new OperatorState(1, "Red op", PlayerColor.Red, 6, 1.0);
                Blue = new OperatorState(2, "Blue op", PlayerColor.Blue, 6, 1.0);
                Green = new OperatorState(3, "Green op", PlayerColor.Green, 6, 1.0);
                Violet = new OperatorState(4, "Violet op", PlayerColor.Violet, 6, 1.0);

                var operators = new List<OperatorState> { Red, Blue, Green, Violet };

                // Filler squadmates, so a payout that covers a seat's squad has
                // a squad to cover. Ids continue the block above; the four
                // named operators stay the first of their seats.
                var squads = new Dictionary<PlayerColor, List<OperatorState>>
                {
                    { PlayerColor.Red, new List<OperatorState> { Red } },
                    { PlayerColor.Blue, new List<OperatorState> { Blue } },
                    { PlayerColor.Green, new List<OperatorState> { Green } },
                    { PlayerColor.Violet, new List<OperatorState> { Violet } }
                };

                int nextId = 5;
                foreach (var pair in squads)
                {
                    while (pair.Value.Count < squadSize)
                    {
                        var extra = new OperatorState(nextId++, $"{pair.Key} filler", pair.Key, 6, 1.0);
                        pair.Value.Add(extra);
                        operators.Add(extra);
                    }
                }

                foreach (var op in operators) op.MoveTo(4);

                var players = new[]
                {
                    new PlayerState(PlayerColor.Red, squads[PlayerColor.Red]),
                    new PlayerState(PlayerColor.Blue, squads[PlayerColor.Blue]),
                    new PlayerState(PlayerColor.Green, squads[PlayerColor.Green]),
                    new PlayerState(PlayerColor.Violet, squads[PlayerColor.Violet])
                };

                Neutralize = new NeutralizeRules(
                    Statuses, abilities, energy, operators, players, combat, null, teams);

                clock.BeginTurnFor(PlayerColor.Red);
            }

            public FakeClock Clock { get; }
            public StatusRegistry Statuses { get; }
            public NeutralizeRules Neutralize { get; }
            public OperatorState Red { get; }
            public OperatorState Blue { get; }
            public OperatorState Green { get; }
            public OperatorState Violet { get; }
        }
    }
}
