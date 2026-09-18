// Assets/Tests/EditMode/Bots/BotTests.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Draft;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Bots
{
    [TestFixture]
    public class BotTests
    {
        private static readonly PlayerColor[] Four =
            { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet };

        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };

        /// <summary>Counts the draws made on it, so a test can see whose stream was used.</summary>
        private sealed class CountingRandom : IRandom
        {
            private readonly SeededRandom _inner;
            public CountingRandom(int seed) { _inner = new SeededRandom(seed); }
            public int Draws { get; private set; }
            public int NextInt(int min, int max) { Draws++; return _inner.NextInt(min, max); }
            public double NextDouble() { Draws++; return _inner.NextDouble(); }
        }

        private static PlayerColor[] SeatsFor(int count)
        {
            var seats = new PlayerColor[count];
            // Two seats sit opposite, as setup suggests.
            if (count == 2) return new[] { PlayerColor.Red, PlayerColor.Green };
            Array.Copy(Four, seats, count);
            return seats;
        }

        private static Dictionary<PlayerColor, IBot> BotsFor(
            IReadOnlyList<PlayerColor> seats, int offset, IRandom random, BotConfig config = null)
        {
            var bots = new Dictionary<PlayerColor, IBot>();
            for (int i = 0; i < seats.Count; i++)
                bots[seats[i]] = new BotBrain((BotPersonality)((i + offset) % 3), random, config);
            return bots;
        }

        private static OperatorState Op(MatchFactory.Match match, PlayerColor seat, string name)
        {
            foreach (var op in match.Operators)
                if (op.Owner == seat && op.Name == name) return op;
            throw new ArgumentException($"{seat} has no {name}");
        }

        /// <summary>The progress at which an operator of <paramref name="seat"/> stands on track cell <paramref name="index"/>.</summary>
        private static int ProgressAt(MatchFactory.Match match, PlayerColor seat, int index)
        {
            int circuit = match.Map.Profile.CircuitLength;
            return (index - match.Map.StartTrackIndex(seat) + circuit) % circuit;
        }

        private static MatchFactory.Match AlphaTwo()
        {
            var match = MatchFactory.CreateAlphaMatch(Two, seed: 11);
            match.Engine.Start();
            return match;
        }

        // ── Whole matches ────────────────────────────────────────────────

        [Test]
        public void Bots_FullSeededMatches_FinishWithoutARefusal()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                var seats = SeatsFor(2 + seed % 3);
                var match = seed % 4 == 0
                    ? MatchFactory.CreateAlphaMatch(seats, seed, openingDeployments: 2)
                    : MatchFactory.Create(seats, seed, openingDeployments: 2);

                var table = new BotTable(match, BotsFor(seats, seed, BotConfig.Default.RandomFor(seed)));
                var result = table.PlayToEnd();

                Assert.That(result.Finished, Is.True, $"seed {seed}: {result.StopReason}");
                Assert.That(result.Winner, Is.Not.Null, $"seed {seed}");
                Assert.That(result.Refusals, Is.EqualTo(0), $"seed {seed}");
            }
        }

        [Test]
        public void Bots_SameSeed_SameMatch()
        {
            BotRunResult Play()
            {
                var match = MatchFactory.Create(Four, 424242, openingDeployments: 2);
                return new BotTable(match, BotsFor(Four, 0, BotConfig.Default.RandomFor(424242))).PlayToEnd();
            }

            var first = Play();
            var second = Play();

            Assert.That(second.Winner, Is.EqualTo(first.Winner));
            Assert.That(second.Commands, Is.EqualTo(first.Commands));
            Assert.That(second.Rounds, Is.EqualTo(first.Rounds));
        }

        [Test]
        public void Bots_DrawOnlyFromTheirOwnStream()
        {
            // With no jitter a bot's choices use no randomness at all, so two
            // bot streams must give the same match: the dice are the match's alone.
            var still = new BotConfig(jitter: 0.0, draftJitter: 0.0);

            BotRunResult Play(int botSeed)
            {
                var match = MatchFactory.Create(Four, 777, openingDeployments: 2);
                return new BotTable(match, BotsFor(Four, 0, new SeededRandom(botSeed), still)).PlayToEnd();
            }

            var a = Play(1);
            var b = Play(99999);

            Assert.That(b.Winner, Is.EqualTo(a.Winner));
            Assert.That(b.Commands, Is.EqualTo(a.Commands));

            // And with jitter, the draws land on the stream the bot was given.
            var counting = new CountingRandom(5);
            var jittery = MatchFactory.Create(Two, 5, openingDeployments: 2);
            new BotTable(jittery, BotsFor(Two, 0, counting)).PlayToEnd();
            Assert.That(counting.Draws, Is.GreaterThan(0));
        }

        [Test]
        public void Bot_NeverTargetsItself()
        {
            int casts = 0;

            for (int seed = 20; seed < 28; seed++)
            {
                var match = MatchFactory.Create(Four, seed, openingDeployments: 2);
                var table = new BotTable(match, BotsFor(Four, seed, BotConfig.Default.RandomFor(seed)));
                table.Sent = (seat, command, events) =>
                {
                    if (!(command is UseAbilityCommand cast)) return;
                    casts++;
                    Assert.That(cast.TargetOperatorId, Is.Not.EqualTo(cast.CasterOperatorId), $"seed {seed}");
                };

                table.PlayToEnd();
            }

            Assert.That(casts, Is.GreaterThan(0), "the matches should have cast something");
        }

        [Test]
        public void Bot_DoesNotRepeatARefusedCommandInTheSameTurn()
        {
            // Two pieces already out, so the roll always offers more than one move.
            var match = MatchFactory.CreateAlphaMatch(Two, seed: 11, openingDeployments: 2);
            match.Engine.Start();
            var bot = new BotBrain(BotPersonality.Runner, new SeededRandom(3));

            var roll = bot.Next(match);
            Assert.That(roll, Is.InstanceOf<RollDiceCommand>());
            bot.Observe(roll, match.Engine.Execute(roll));

            var first = bot.Next(match);
            bot.Observe(first, new IGameEvent[] { new CommandRejected("test refusal") });

            var second = bot.Next(match);

            Assert.That(first, Is.Not.InstanceOf<EndTurnCommand>(), "the roll should offer a move");
            Assert.That(BotBrain.Key(second), Is.Not.EqualTo(BotBrain.Key(first)));
            Assert.That(bot.RefusalCount, Is.EqualTo(1));
        }

        // ── Movement preferences ─────────────────────────────────────────

        [Test]
        public void Brawler_PrefersTheKillingLanding()
        {
            var match = AlphaTwo();
            var mover = Op(match, PlayerColor.Red, "Bouncer");
            var victim = Op(match, PlayerColor.Blue, "Syla");

            mover.MoveTo(ProgressAt(match, PlayerColor.Red, 5));
            victim.MoveTo(ProgressAt(match, PlayerColor.Blue, 8));
            victim.SetHealth(1);

            var board = new BotBoard(match);
            var brawler = BotWeights.For(BotPersonality.Brawler);

            double kill = MoveScorer.ScoreLanding(board, brawler, mover, mover.Progress + 3, 3, 0);
            double run = MoveScorer.ScoreLanding(board, brawler, mover, mover.Progress + 5, 5, 0);

            Assert.That(kill, Is.GreaterThan(run));
        }

        [Test]
        public void Runner_StillTakesAKillingLanding()
        {
            var match = AlphaTwo();
            var mover = Op(match, PlayerColor.Red, "Bouncer");
            var victim = Op(match, PlayerColor.Blue, "Syla");

            mover.MoveTo(ProgressAt(match, PlayerColor.Red, 5));
            victim.MoveTo(ProgressAt(match, PlayerColor.Blue, 8));
            victim.SetHealth(1);

            var board = new BotBoard(match);
            var runner = BotWeights.For(BotPersonality.Runner);

            double kill = MoveScorer.ScoreLanding(board, runner, mover, mover.Progress + 3, 3, 0);
            double run = MoveScorer.ScoreLanding(board, runner, mover, mover.Progress + 5, 5, 0);

            Assert.That(kill, Is.GreaterThan(run));
        }

        [Test]
        public void Runner_EntersTheHomeColumnWhenOffered()
        {
            var match = AlphaTwo();
            var track = match.Map.Profile.TrackLength;
            var homeward = Op(match, PlayerColor.Red, "Bouncer");
            var other = Op(match, PlayerColor.Red, "Syla");

            homeward.MoveTo(track - 2);
            other.MoveTo(20);

            var board = new BotBoard(match);
            var runner = BotWeights.For(BotPersonality.Runner);

            double enter = MoveScorer.ScoreLanding(board, runner, homeward, track + 1, 3, 0);
            double stay = MoveScorer.ScoreLanding(board, runner, other, 23, 3, 0);

            Assert.That(match.Map.IsInHomeColumn(track + 1), Is.True);
            Assert.That(enter, Is.GreaterThan(stay));
        }

        [Test]
        public void Landing_JustAheadOfAnEnemy_ScoresBelowAQuietOne()
        {
            var match = AlphaTwo();
            var near = Op(match, PlayerColor.Red, "Bouncer");
            var far = Op(match, PlayerColor.Red, "Kurbyn");
            var enemy = Op(match, PlayerColor.Blue, "Bouncer");

            // The enemy stands on cell 30. One landing ends two cells ahead of it,
            // within its reach and its collision range; the other well behind it.
            enemy.MoveTo(ProgressAt(match, PlayerColor.Blue, 30));
            near.MoveTo(ProgressAt(match, PlayerColor.Red, 29));
            far.MoveTo(ProgressAt(match, PlayerColor.Red, 14));

            var board = new BotBoard(match);
            var weights = BotWeights.For(BotPersonality.Banker);

            double risky = MoveScorer.ScoreLanding(board, weights, near, near.Progress + 3, 3, 0);
            double quiet = MoveScorer.ScoreLanding(board, weights, far, far.Progress + 3, 3, 0);

            Assert.That(board.Threat(PlayerColor.Red, board.CellAt(PlayerColor.Red, near.Progress + 3)), Is.GreaterThan(0.0));
            Assert.That(risky, Is.LessThan(quiet));
        }

        // ── Casting preferences ──────────────────────────────────────────

        [Test]
        public void Runner_CastsForAKill_ButHoldsAPoke()
        {
            var match = AlphaTwo();
            var syla = Op(match, PlayerColor.Red, "Syla");
            var target = Op(match, PlayerColor.Blue, "Bouncer");
            var seat = match.Players[0];

            syla.MoveTo(ProgressAt(match, PlayerColor.Red, 10));
            target.MoveTo(ProgressAt(match, PlayerColor.Blue, 12));

            var board = new BotBoard(match);
            var runner = BotWeights.For(BotPersonality.Runner);
            var brawler = BotWeights.For(BotPersonality.Brawler);

            var poke = CastPlanner.Score(board, runner, seat, syla, Syla.FromTheHip, target, null, null);
            var brawlerPoke = CastPlanner.Score(board, brawler, seat, syla, Syla.FromTheHip, target, null, null);

            target.SetHealth(1);
            var finisher = CastPlanner.Score(new BotBoard(match), runner, seat, syla, Syla.FromTheHip, target, null, null);

            Assert.That(poke.Score, Is.LessThan(runner.CastThreshold), "a Runner holds a mere poke");
            Assert.That(brawlerPoke.Score, Is.GreaterThanOrEqualTo(brawler.CastThreshold), "a Brawler takes it");
            Assert.That(finisher.Score, Is.GreaterThanOrEqualTo(runner.CastThreshold), "a Runner takes the kill");
        }

        [Test]
        public void Casting_ScoresSelfDamageAgainstTheCaster()
        {
            var match = AlphaTwo();
            var bouncer = Op(match, PlayerColor.Red, "Bouncer");
            var target = Op(match, PlayerColor.Blue, "Syla");
            var seat = match.Players[0];

            bouncer.MoveTo(ProgressAt(match, PlayerColor.Red, 10));
            target.MoveTo(ProgressAt(match, PlayerColor.Blue, 11));
            bouncer.SetHealth(1);

            var weights = BotWeights.For(BotPersonality.Brawler);
            var mauling = CastPlanner.Score(new BotBoard(match), weights, seat, bouncer, Bouncer.AllInMauling, target, null, null);

            // One point of self-damage would neutralize a Bouncer on 1 health
            // (self-damage 2 → 1 on 2026-09-18).
            Assert.That(mauling.Score, Is.LessThan(0.0));
        }

        [Test]
        public void Banker_HoldsACheapCast_ButBreaksTheReserveForABigPlay()
        {
            var banker = BotWeights.For(BotPersonality.Banker);
            var brawler = BotWeights.For(BotPersonality.Brawler);

            // Saving for a 9-cost ability, holding 6: a 3-cost poke would leave 3.
            Assert.That(BotBrain.HoldsForReserve(banker, 6, 3, 9, 2.5), Is.True);
            Assert.That(BotBrain.HoldsForReserve(banker, 6, 3, 9, banker.ReserveOverride), Is.False, "a rescue or kill");
            Assert.That(BotBrain.HoldsForReserve(banker, 12, 3, 9, 2.5), Is.False, "enough left afterwards");
            Assert.That(BotBrain.HoldsForReserve(banker, 9, 9, 9, 2.5), Is.False, "the best ability itself");
            Assert.That(BotBrain.HoldsForReserve(brawler, 6, 3, 9, 0.1), Is.False, "only a Banker saves");
        }

        // ── Kian's push ──────────────────────────────────────────────────

        /// <summary>Red fields Kian; Blue fields three targets without evasion.</summary>
        private static MatchFactory.Match KianTable(int seed = 13)
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                { PlayerColor.Red, new[] { Kian.Definition, Bouncer.Definition, Syla.Definition } },
                { PlayerColor.Blue, new[] { Syla.Definition, Javi.Definition, Mimi.Definition } }
            };
            var match = MatchFactory.Create(Two, seed, squads);
            match.Engine.Start();
            return match;
        }

        [Test]
        public void Push_OffASafeCell_IntoALanding_IsWorthAKill()
        {
            var match = KianTable();
            var kian = Op(match, PlayerColor.Red, "Kian");
            var enemy = Op(match, PlayerColor.Blue, "Syla");
            const int safe = 26;   // a start cell: safe for everyone standing on it

            kian.MoveTo(ProgressAt(match, PlayerColor.Red, safe));
            enemy.MoveTo(ProgressAt(match, PlayerColor.Blue, safe));
            enemy.SetHealth(4);

            var board = new BotBoard(match);
            var w = BotWeights.For(BotPersonality.Brawler);

            // Sharing the cell, the enemy goes backwards: two cells, onto 24.
            int to = board.PredictPush(kian, enemy, 2);
            var landed = board.CellAt(PlayerColor.Blue, to);
            Assert.That(landed.Index, Is.EqualTo(safe - 2));
            Assert.That(match.Map.IsSafe(board.CellOf(enemy)), Is.True);
            Assert.That(match.Map.IsSafe(landed), Is.False);

            // Sonic Disrupter's 2 damage first, then a landing's 3 finishes 4 health.
            double setup = CastPlanner.PushValue(board, w, kian, enemy, 2, 2.0, new HashSet<int> { safe - 2 });
            double noDice = CastPlanner.PushValue(board, w, kian, enemy, 2, 2.0, new HashSet<int>());

            Assert.That(noDice, Is.GreaterThanOrEqualTo(w.PushExposure + 2 * w.PushProgress), "moved back and exposed");
            Assert.That(setup, Is.GreaterThan(noDice + w.Kill * w.PushSetup * 0.9), "the landing makes it a kill");
        }

        [Test]
        public void Push_ThatCarriesAnEnemyForward_ScoresNegative()
        {
            var match = KianTable();
            var kian = Op(match, PlayerColor.Red, "Kian");
            var enemy = Op(match, PlayerColor.Blue, "Syla");

            // The enemy stands two cells ahead of Kian, off any safe cell: a push sends it on its way.
            kian.MoveTo(ProgressAt(match, PlayerColor.Red, 30));
            enemy.MoveTo(ProgressAt(match, PlayerColor.Blue, 32));

            var board = new BotBoard(match);
            var w = BotWeights.For(BotPersonality.Brawler);

            Assert.That(board.PredictPush(kian, enemy, 2), Is.EqualTo(enemy.Progress + 2));
            Assert.That(CastPlanner.PushValue(board, w, kian, enemy, 2, 0.0, new HashSet<int>()), Is.LessThan(0.0));
        }

        [Test]
        public void PredictPush_MatchesTheEngine()
        {
            foreach (var (kianCell, enemyCell) in new[] { (26, 26), (30, 32), (30, 28), (0, 51), (10, 12) })
            {
                bool checkedOnce = false;

                // The first roll must grant enough energy to cast, so try seeds until one does.
                for (int seed = 1; seed <= 60 && !checkedOnce; seed++)
                {
                    var match = KianTable(seed);
                    var kian = Op(match, PlayerColor.Red, "Kian");
                    var enemy = Op(match, PlayerColor.Blue, "Mimi");
                    kian.MoveTo(ProgressAt(match, PlayerColor.Red, kianCell));
                    enemy.MoveTo(ProgressAt(match, PlayerColor.Blue, enemyCell));
                    enemy.SetHealth(enemy.MaxHealth);

                    int predicted = new BotBoard(match).PredictPush(kian, enemy, 2);

                    var engine = match.Engine;
                    engine.Execute(new RollDiceCommand());
                    if (match.Players[0].Energy < Kian.SonicDisrupter.EnergyCost) continue;

                    bool cast = false;
                    foreach (var e in engine.Execute(new UseAbilityCommand(kian.Id, Kian.SonicDisrupter.Id)))
                        if (e is EnergySpent) cast = true;

                    Assert.That(cast, Is.True, $"Kian {kianCell}, enemy {enemyCell}: the cast was refused");
                    Assert.That(enemy.Progress, Is.EqualTo(predicted), $"Kian {kianCell}, enemy {enemyCell}");
                    checkedOnce = true;
                }

                Assert.That(checkedOnce, Is.True, $"Kian {kianCell}, enemy {enemyCell}: never cast");
            }
        }

        // ── Drafting ─────────────────────────────────────────────────────

        [Test]
        public void CpuSeats_DraftLegalSquads_InBothModes()
        {
            foreach (DraftMode mode in new[] { DraftMode.AllPick, DraftMode.Snake })
            {
                for (int seed = 1; seed <= 6; seed++)
                {
                    var draft = DraftState.ForMatch(Four, mode, seed);
                    var bots = BotsFor(Four, seed, BotConfig.Default.RandomFor(seed));

                    int guard = 0;
                    while (!draft.IsComplete && guard++ < 100)
                    {
                        foreach (var seat in Four)
                        {
                            if (draft.CanPickAny(seat) != DraftRefusal.None) continue;

                            var pick = bots[seat].PickDraft(draft, seat);
                            Assert.That(pick, Is.Not.Null, $"{mode} seed {seed} {seat}");
                            Assert.That(draft.Pick(seat, pick), Is.EqualTo(DraftRefusal.None));
                        }
                    }

                    Assert.That(draft.IsComplete, Is.True, $"{mode} seed {seed}");

                    foreach (var seat in Four)
                    {
                        var names = new HashSet<string>();
                        foreach (var op in draft.PicksOf(seat))
                            Assert.That(names.Add(op.Name), Is.True, $"{mode} seed {seed} {seat} twice {op.Name}");
                    }

                    Assert.DoesNotThrow(() => MatchFactory.Create(Four, seed, draft.Squads()));
                }
            }
        }

        [Test]
        public void DraftPicker_ReturnsNull_WhenTheSeatCannotPick()
        {
            var draft = DraftState.ForMatch(Four, DraftMode.Snake, 1);
            var bot = new BotBrain(BotPersonality.Banker, new SeededRandom(1));

            Assert.That(bot.PickDraft(draft, PlayerColor.Blue), Is.Null, "not Blue's pick");
        }

        [Test]
        public void DraftPicker_FillsTheSustainGap()
        {
            var draft = DraftState.ForMatch(Two, DraftMode.AllPick, 3);
            draft.Pick(PlayerColor.Red, Roster.ByName("Syla"));
            draft.Pick(PlayerColor.Red, Roster.ByName("Kurbyn"));

            var weights = BotWeights.For(BotPersonality.Brawler);
            weights.DraftComposition = 100.0;

            var pick = DraftPicker.Choose(draft, PlayerColor.Red, weights, null);

            Assert.That(DraftPicker.HasSustain(Roster.ByName("Syla")), Is.False);
            Assert.That(DraftPicker.HasSustain(Roster.ByName("Kurbyn")), Is.False);
            Assert.That(DraftPicker.HasSustain(pick), Is.True, pick.Name);
        }

        [Test]
        public void Personalities_ValueTheRosterDifferently()
        {
            var runner = BotWeights.For(BotPersonality.Runner);
            var brawler = BotWeights.For(BotPersonality.Brawler);

            var javi = Roster.ByName("Javi");      // fast support
            var luka = Roster.ByName("Luka");      // burst duelist

            Assert.That(DraftPicker.Value(javi, runner) - DraftPicker.Value(luka, runner),
                Is.GreaterThan(DraftPicker.Value(javi, brawler) - DraftPicker.Value(luka, brawler)));
        }
    }
}