// Assets/Tests/EditMode/Status/SpeedStackingTests.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NonaRoyale.Core.Tests.Abilities;   // FakeClock
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Status
{
    /// <summary>
    /// Speed penalties do not stack, across every join (COMBAT_SYSTEMS §5.2;
    /// designer, 2026-10-01: "No slow stacking").
    /// </summary>
    /// <remarks>
    /// <b>These are the tests that should have existed.</b> The rule was stated
    /// in §5.2 from the beginning and enforced in two of the three places it
    /// had to hold: <c>StatusRegistry</c> keeps the deeper of two slows of the
    /// same kind, and <c>AuraRules</c> keeps the strongest penalty across aura
    /// sources. The third join — <c>GameEngine.SpeedOf</c> folding the two
    /// channels together — used <c>+</c>, so a cast slow and an enemy aura
    /// stacked to −1.0 where the rule says −0.5.
    ///
    /// The whole suite passed before and after the fix, which is the reason to
    /// write these down: nothing anywhere put a slow and an aura on the same
    /// operator. §12 had carried the conflict as the project's top open rules
    /// item precisely because the scripted player triggers it constantly, so
    /// every figure measured before 2026-10-01 was measured with slows
    /// stacking.
    /// </remarks>
    [TestFixture]
    public class SpeedStackingTests
    {
        private PathMap _map;
        private int _circuit;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _circuit = _map.Profile.CircuitLength;
        }

        // ── The fold itself ──────────────────────────────────────────────

        [Test]
        public void TheDeeperPenaltyWins_AndTheBetterBonusWins()
        {
            var a = new SpeedChange(0.25, -1.5);
            var b = new SpeedChange(0.75, -0.5);

            var folded = SpeedChange.Strongest(a, b);

            Assert.That(folded.Penalty, Is.EqualTo(-1.5), "the deeper slow, not their sum");
            Assert.That(folded.Bonus, Is.EqualTo(0.75), "the better bonus, not their sum");
            Assert.That(folded.Total, Is.EqualTo(-0.75));
        }

        [Test]
        public void TheFold_IsOrderIndependent()
        {
            // The property that makes it safe to add a third channel later.
            var a = new SpeedChange(0.5, -1.0);
            var b = new SpeedChange(0.25, -1.5);

            Assert.That(SpeedChange.Strongest(a, b).Total,
                Is.EqualTo(SpeedChange.Strongest(b, a).Total));
        }

        [Test]
        public void TheFold_IsIdempotent_SoOneSourceTwiceIsOneSource()
        {
            var one = SpeedChange.Of(-0.5);

            Assert.That(SpeedChange.Strongest(one, one).Total, Is.EqualTo(-0.5),
                "the same slow counted twice is still one slow");
        }

        [Test]
        public void ADeltaSortsItselfBySign()
        {
            Assert.That(SpeedChange.Of(-0.5).Penalty, Is.EqualTo(-0.5));
            Assert.That(SpeedChange.Of(-0.5).Bonus, Is.EqualTo(0.0));
            Assert.That(SpeedChange.Of(0.5).Bonus, Is.EqualTo(0.5));
            Assert.That(SpeedChange.Of(0.5).Penalty, Is.EqualTo(0.0));
        }

        // ── The two channels, together ───────────────────────────────────

        [Test]
        public void ASlowInsideAnAura_IsOneSlow_NotTwo()
        {
            // The regression, stated as plainly as it can be. Bouncer's
            // Intimidating Presence is −0.5 and a Slow is −0.5; before
            // 2026-10-01 this operator moved at 0.5× and it should be 1.0−0.5.
            var statuses = Registry();
            var victim = At(2, PlayerColor.Blue, TrackNear(0, 1));
            var bouncer = At(1, PlayerColor.Red, TrackNear(0, 0));

            statuses.Apply(victim, StatusKind.Slow, duration: 2);
            Advance(statuses, victim);

            var folded = Fold(statuses, victim, bouncer);

            Assert.That(folded.Penalty, Is.EqualTo(-0.5), "the deeper of the two, which is either");
            Assert.That(folded.Total, Is.EqualTo(-0.5));
        }

        [Test]
        public void ADeepSlowInsideAnAura_KeepsTheDeepSlow()
        {
            // The aura must not be able to make it worse, and must not be
            // ignored either — it is simply the shallower of the two.
            var statuses = Registry();
            var victim = At(2, PlayerColor.Blue, TrackNear(0, 1));
            var bouncer = At(1, PlayerColor.Red, TrackNear(0, 0));

            statuses.Apply(victim, StatusKind.Slow, duration: 2, magnitude: -1.5);
            Advance(statuses, victim);

            Assert.That(Fold(statuses, victim, bouncer).Total, Is.EqualTo(-1.5));
        }

        [Test]
        public void EachChannelStillWorksAlone()
        {
            // The pairing that proves the tests above measure the fold rather
            // than a channel that stopped contributing.
            var statuses = Registry();
            var victim = At(2, PlayerColor.Blue, TrackNear(0, 1));
            var bouncer = At(1, PlayerColor.Red, TrackNear(0, 0));
            var farAway = At(3, PlayerColor.Red, TrackNear(0, Bouncer.IntimidatingPresenceRadius + 2));

            Assert.That(Fold(statuses, victim, bouncer).Total, Is.EqualTo(-0.5), "the aura alone");

            statuses.Apply(victim, StatusKind.Slow, duration: 2);
            Advance(statuses, victim);

            Assert.That(Fold(statuses, victim, farAway).Total, Is.EqualTo(-0.5), "the slow alone");
        }

        [Test]
        public void AnUntouchedOperator_HasNoChange()
        {
            var statuses = Registry();
            var victim = At(2, PlayerColor.Blue, TrackNear(0, 1));
            var farAway = At(3, PlayerColor.Red, TrackNear(0, Bouncer.IntimidatingPresenceRadius + 2));

            Assert.That(Fold(statuses, victim, farAway).Total, Is.EqualTo(0.0));
        }

        // ── What it is worth in cells ────────────────────────────────────

        [Test]
        public void ItIsWorthCells_ButOnlyToAnOperatorAboveOneTimes()
        {
            // Who the rule actually changes, and it is a short list. The speed
            // floor is MinSpeedMultiplier (0.5), so at base 1.0 a stacked pair
            // landed on the floor and so did a single slow: identical. Only an
            // operator above 1.0 could feel the difference, and today that is
            // Javi and Syla at 1.5 and nobody else.
            var movement = new MovementResolver(_map, GameConfig.Default);
            double floor = GameConfig.Default.MinSpeedMultiplier;

            Assert.That(movement.EffectiveSpeed(1.0, -1.0), Is.EqualTo(floor),
                "stacked, at 1.0 base: the floor swallowed it");
            Assert.That(movement.EffectiveSpeed(1.0, -0.5), Is.EqualTo(floor),
                "and one slow lands on the same floor, so nothing changed for a 1.0 operator");

            double stacked = movement.EffectiveSpeed(1.5, -1.0);
            double single = movement.EffectiveSpeed(1.5, -0.5);

            Assert.That(single, Is.GreaterThan(stacked), "a 1.5 operator does feel it");
            Assert.That(movement.CellsFor(6, single), Is.GreaterThan(movement.CellsFor(6, stacked)),
                "and it is worth whole cells, not a rounding");
        }

        [Test]
        public void TheSpeedBand_StillHoldsOnlyTwoOperatorsAboveOneTimes()
        {
            // The test above is only true while that list is short. If a third
            // operator goes above 1.0, or one of these two comes down to it,
            // this fails and the reasoning above has to be re-read.
            Assert.That(Javi.Speed, Is.EqualTo(1.5));
            Assert.That(Syla.Speed, Is.EqualTo(1.5));
            Assert.That(Bouncer.Speed, Is.EqualTo(1.0));
            Assert.That(Kurbyn.BaseSpeed, Is.EqualTo(1.0));
        }

        // ── Through the engine, which is where it was broken ─────────────

        [Test]
        public void TheEngine_FoldsTheChannels_RatherThanSummingThem()
        {
            // The regression at the only level that proves it. The two tests
            // above pass just as well on a summing engine, because they fold in
            // the fixture; this one reads the distance GameEngine actually
            // offers. Syla is 1.5, so she is one of the two operators the rule
            // can reach (see the band test below), and Bouncer carries the aura.
            var match = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red, PlayerColor.Blue }, seed: 7, openingDeployments: 3);

            match.Engine.Start();

            var bouncer = Named(match, PlayerColor.Red, "Bouncer");
            var syla = Named(match, PlayerColor.Blue, "Syla");

            // Red's turn is played out properly rather than abandoned: the
            // engine refuses an end-turn that still owes a roll. Done BEFORE
            // placing anyone, because playing it out moves a Red operator and
            // would walk the Bouncer off the cell this test needs him on.
            PlayOutTurn(match);

            // Syla one cell from Bouncer, so his aura reaches her, and slowed
            // on top. Progress is per-seat, so both are placed by track cell.
            int cell = UnsafeTrack(match, 6);
            Place(match, bouncer, cell);
            Place(match, syla, cell + 1);

            // Applied on Blue's own turn, so it takes hold at once (§5) instead
            // of waiting for a turn that this test would then have to reach.
            match.Statuses.Apply(syla, StatusKind.Slow, duration: 3);
            Assert.That(match.Statuses.Has(syla, StatusKind.Slow), Is.True, "precondition: slowed");
            // The engine owns its own AuraRules; this rebuilds the same
            // question to prove the aura really does reach her, because without
            // it this test would pass on a summing engine too.
            var reaching = new AuraRules(
                new TargetingRules(match.Map, match.Statuses),
                new Dictionary<int, AuraDefinition> { { bouncer.Id, Bouncer.IntimidatingPresence } });

            Assert.That(reaching.SpeedChangeFor(syla, match.Operators).Penalty, Is.EqualTo(-0.5),
                "precondition: inside the aura");

            var roll = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().First().Roll;
            var preview = match.Engine.PreviewLandings()
                .First(p => p.OperatorId == syla.Id && p.IsPooled);

            var movement = new MovementResolver(match.Map, GameConfig.Default);
            int folded = movement.CellsFor(roll.Total, movement.EffectiveSpeed(Syla.Speed, -0.5));
            int summed = movement.CellsFor(roll.Total, movement.EffectiveSpeed(Syla.Speed, -1.0));

            Assert.That(summed, Is.LessThan(folded), "precondition: this roll can tell them apart");
            Assert.That(preview.Cells, Is.EqualTo(folded),
                "one slow, the deeper of the two — not the sum of both channels");
        }

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Rolls, spends the whole roll on whoever can take it, and hands over.
        /// Borrowed from <c>RegenTests</c>: the engine will not end a turn that
        /// still owes a roll, so a test that wants the next seat has to play
        /// this one.
        /// </summary>
        private static void PlayOutTurn(MatchFactory.Match match)
        {
            var engine = match.Engine;

            engine.Execute(new RollDiceCommand());
            SpendRoll(engine);

            while (engine.CanRollAgain)
            {
                engine.Execute(new RollDiceCommand());
                SpendRoll(engine);
            }

            var handover = engine.Execute(new EndTurnCommand());
            Assert.That(handover.OfType<CommandRejected>(), Is.Empty, "the turn should hand over");
        }

        private static void SpendRoll(GameEngine engine)
        {
            foreach (var landing in engine.PreviewLandings())
            {
                if (!landing.IsPooled) continue;
                engine.Execute(new MoveCommand(landing.OperatorId));
                return;
            }
        }

        private static OperatorState Named(MatchFactory.Match match, PlayerColor seat, string name) =>
            match.Operators.First(o => o.Owner == seat && o.Name == name);

        /// <summary>Puts an operator on a track cell, whatever its seat's progress offset is.</summary>
        private static void Place(MatchFactory.Match match, OperatorState op, int trackCell)
        {
            int circuit = match.Map.Profile.CircuitLength;
            int wanted = ((trackCell % circuit) + circuit) % circuit;

            for (int p = 0; p < circuit; p++)
            {
                if (!match.Map.IsOnOuterTrack(p)) continue;
                if (match.Map.CellAt(op.Owner, p).Index != wanted) continue;
                op.MoveTo(p);
                return;
            }

            Assert.Fail($"Track {wanted} is unreachable for {op.Owner} — has the board changed?");
        }

        /// <summary>A track cell no seat starts on, so nothing is sheltered from the slow (§4.4).</summary>
        private static int UnsafeTrack(MatchFactory.Match match, int from)
        {
            int circuit = match.Map.Profile.CircuitLength;

            for (int i = from; i < from + circuit; i++)
            {
                int cell = (i % circuit + circuit) % circuit;
                if (!match.Map.IsSafe(CellRef.Track(cell)) && !match.Map.IsSafe(CellRef.Track((cell + 1) % circuit)))
                    return cell;
            }

            Assert.Fail("No adjacent pair of unsafe cells — has the board changed?");
            return -1;
        }

        private FakeClock _clock;

        private StatusRegistry Registry()
        {
            _clock = new FakeClock();
            return new StatusRegistry(_clock, CombatConfig.Default);
        }

        /// <summary>
        /// A status applied outside its holder's own turn only takes hold on
        /// that holder's next one (§5), so the clock has to reach it before the
        /// slow is readable.
        /// </summary>
        private void Advance(StatusRegistry statuses, OperatorState op)
        {
            _clock.BeginTurnFor(op.Owner);
            Assert.That(statuses.Has(op, StatusKind.Slow), Is.True, "precondition: the slow is live");
        }

        /// <summary>Both channels folded exactly as <c>GameEngine.SpeedOf</c> folds them.</summary>
        private SpeedChange Fold(StatusRegistry statuses, OperatorState victim, OperatorState auraSource)
        {
            var targeting = new TargetingRules(_map, statuses);
            var auras = new AuraRules(targeting,
                new Dictionary<int, AuraDefinition> { { auraSource.Id, Bouncer.IntimidatingPresence } });

            var board = new[] { auraSource, victim };

            return SpeedChange.Strongest(
                statuses.SpeedChangeFor(victim),
                auras.SpeedChangeFor(victim, board));
        }

        private OperatorState At(int id, PlayerColor seat, int trackCell)
        {
            var op = new OperatorState(id, $"{seat} {id}", seat, 10, 1.0);
            op.MoveTo(ProgressAtTrack(seat, trackCell));
            return op;
        }

        /// <summary>
        /// A track cell <paramref name="offset"/> steps past <paramref name="from"/>,
        /// wrapped. Offsets rather than literals, so the fixture survives a
        /// board profile change.
        /// </summary>
        private int TrackNear(int from, int offset) => ((from + offset) % _circuit + _circuit) % _circuit;

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
    }
}
