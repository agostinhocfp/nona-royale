// Assets/Tests/EditMode/Draft/DraftStateTests.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Draft;
using NonaRoyale.Core.Rng;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Draft
{
    [TestFixture]
    public class DraftStateTests
    {
        private static readonly PlayerColor[] Four =
            { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet };

        private static readonly PlayerColor[] Two = { PlayerColor.Red, PlayerColor.Blue };

        /// <summary>Always draws the lowest index, so a random pick is the first available operator.</summary>
        private sealed class FirstRandom : IRandom
        {
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0.0;
        }

        private static DraftState NewDraft(IReadOnlyList<PlayerColor> seats, DraftMode mode) =>
            new DraftState(seats, mode, new FirstRandom());

        private static OperatorDefinition Op(int i) => Roster.All[i];

        // ── Order ────────────────────────────────────────────────────────

        [Test]
        public void SnakeOrder_FourSeats_SecondRoundReverses()
        {
            var draft = NewDraft(Four, DraftMode.Snake);

            var expected = new[]
            {
                PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet,
                PlayerColor.Violet, PlayerColor.Green, PlayerColor.Blue, PlayerColor.Red,
                PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
            };

            Assert.That(draft.TotalPicks, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
                Assert.That(draft.SeatForPick(i), Is.EqualTo(expected[i]), $"pick {i}");
        }

        [Test]
        public void SnakeOrder_TwoSeats_AlternatesByRound()
        {
            var draft = NewDraft(Two, DraftMode.Snake);

            var expected = new[]
            {
                PlayerColor.Red, PlayerColor.Blue,
                PlayerColor.Blue, PlayerColor.Red,
                PlayerColor.Red, PlayerColor.Blue
            };

            for (int i = 0; i < expected.Length; i++)
                Assert.That(draft.SeatForPick(i), Is.EqualTo(expected[i]), $"pick {i}");
        }

        [Test]
        public void SnakePicks_AdvanceTheCurrentSeat()
        {
            var draft = NewDraft(Four, DraftMode.Snake);

            for (int i = 0; i < draft.TotalPicks; i++)
            {
                Assert.That(draft.CurrentSeat, Is.EqualTo(draft.SeatForPick(i)));
                Assert.That(draft.RandomPick(draft.CurrentSeat), Is.EqualTo(DraftRefusal.None));
            }

            Assert.That(draft.CurrentSeat, Is.EqualTo(PlayerColor.None));
        }

        [Test]
        public void AllPick_HasNoTurnOrder()
        {
            var draft = NewDraft(Four, DraftMode.AllPick);

            Assert.That(draft.CurrentSeat, Is.EqualTo(PlayerColor.None));
            Assert.That(draft.SeatForPick(0), Is.EqualTo(PlayerColor.None));
        }

        [Test]
        public void AllPick_AnySeatMayPickInAnyOrder()
        {
            var draft = NewDraft(Four, DraftMode.AllPick);

            Assert.That(draft.Pick(PlayerColor.Violet, Op(0)), Is.EqualTo(DraftRefusal.None));
            Assert.That(draft.Pick(PlayerColor.Violet, Op(1)), Is.EqualTo(DraftRefusal.None));
            Assert.That(draft.Pick(PlayerColor.Green, Op(2)), Is.EqualTo(DraftRefusal.None));
            Assert.That(draft.Pick(PlayerColor.Red, Op(3)), Is.EqualTo(DraftRefusal.None));

            Assert.That(draft.FilledCount(PlayerColor.Violet), Is.EqualTo(2));
            Assert.That(draft.FilledCount(PlayerColor.Blue), Is.EqualTo(0));
            Assert.That(draft.PickCount, Is.EqualTo(4));
        }

        [Test]
        public void Snake_OutOfTurnPick_IsRefused()
        {
            var draft = NewDraft(Four, DraftMode.Snake);

            Assert.That(draft.CanPick(PlayerColor.Blue, Op(0)), Is.EqualTo(DraftRefusal.NotYourTurn));
            Assert.That(draft.Pick(PlayerColor.Blue, Op(0)), Is.EqualTo(DraftRefusal.NotYourTurn));
            Assert.That(draft.RandomPick(PlayerColor.Blue), Is.EqualTo(DraftRefusal.NotYourTurn));
            Assert.That(draft.PickCount, Is.EqualTo(0));
        }

        // ── Uniqueness ───────────────────────────────────────────────────

        [Test]
        public void Pick_SameOperatorTwiceForOneSeat_IsRefused()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);

            draft.Pick(PlayerColor.Red, Op(4));

            Assert.That(draft.CanPick(PlayerColor.Red, Op(4)), Is.EqualTo(DraftRefusal.AlreadyInSquad));
            Assert.That(draft.Pick(PlayerColor.Red, Op(4)), Is.EqualTo(DraftRefusal.AlreadyInSquad));
            Assert.That(draft.FilledCount(PlayerColor.Red), Is.EqualTo(1));
        }

        [Test]
        public void Pick_SameOperatorAcrossSeats_IsAllowed()
        {
            // GDD §2.2: four seats need twelve picks and the pool is nine.
            var draft = NewDraft(Four, DraftMode.AllPick);

            foreach (var seat in Four)
                Assert.That(draft.Pick(seat, Op(0)), Is.EqualTo(DraftRefusal.None), seat.ToString());
        }

        [Test]
        public void Pick_FourthOperatorForOneSeat_IsRefused()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);

            for (int i = 0; i < Roster.SquadSize; i++) draft.Pick(PlayerColor.Red, Op(i));

            Assert.That(draft.Pick(PlayerColor.Red, Op(5)), Is.EqualTo(DraftRefusal.SquadFull));
            Assert.That(draft.RandomPick(PlayerColor.Red), Is.EqualTo(DraftRefusal.SquadFull));
            Assert.That(draft.Available(PlayerColor.Red), Is.Empty);
        }

        [Test]
        public void Pick_OperatorOutsidePool_IsRefused()
        {
            var pool = new[] { Op(0), Op(1), Op(2), Op(3) };
            var draft = new DraftState(Two, DraftMode.AllPick, new FirstRandom(), pool: pool);

            Assert.That(draft.Pick(PlayerColor.Red, Op(8)), Is.EqualTo(DraftRefusal.NotInPool));
        }

        [Test]
        public void Available_ExcludesOnlyTheSeatsOwnPicks()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);

            draft.Pick(PlayerColor.Red, Op(0));
            draft.Pick(PlayerColor.Blue, Op(1));

            var red = draft.Available(PlayerColor.Red);
            Assert.That(red.Count, Is.EqualTo(Roster.All.Count - 1));
            Assert.That(red, Has.No.Member(Op(0)));
            Assert.That(red, Has.Member(Op(1)));
        }

        [Test]
        public void SeatsHolding_ListsEverySeatFieldingTheOperator()
        {
            var draft = NewDraft(Four, DraftMode.AllPick);

            draft.Pick(PlayerColor.Violet, Op(2));
            draft.Pick(PlayerColor.Blue, Op(2));
            draft.Pick(PlayerColor.Red, Op(3));

            Assert.That(draft.SeatsHolding(Op(2)), Is.EqualTo(new[] { PlayerColor.Blue, PlayerColor.Violet }));
            Assert.That(draft.SeatsHolding(Op(5)), Is.Empty);

            draft.Clear(PlayerColor.Blue, 0);
            Assert.That(draft.SeatsHolding(Op(2)), Is.EqualTo(new[] { PlayerColor.Violet }));
        }

        // ── Completion and squads ────────────────────────────────────────

        [Test]
        public void Complete_AfterThreePicksPerSeat()
        {
            var draft = NewDraft(Four, DraftMode.AllPick);

            foreach (var seat in Four)
                for (int i = 0; i < Roster.SquadSize; i++)
                {
                    Assert.That(draft.IsComplete, Is.False);
                    draft.Pick(seat, Op(i));
                }

            Assert.That(draft.IsComplete, Is.True);
            Assert.That(draft.PickCount, Is.EqualTo(12));
            Assert.That(draft.CanPickAny(PlayerColor.Red), Is.EqualTo(DraftRefusal.Complete));
        }

        [Test]
        public void Squads_BeforeComplete_Throw()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);
            draft.Pick(PlayerColor.Red, Op(0));

            Assert.Throws<InvalidOperationException>(() => draft.Squads());
        }

        [Test]
        public void Squads_FeedMatchFactory()
        {
            var draft = NewDraft(Four, DraftMode.AllPick);

            draft.Pick(PlayerColor.Red, Roster.ByName("Luka"));
            draft.Pick(PlayerColor.Red, Roster.ByName("Mimi"));
            draft.Pick(PlayerColor.Red, Roster.ByName("Javi"));
            draft.FillRandom();

            var squads = draft.Squads();
            var match = MatchFactory.Create(Four, 7, squads);

            Assert.That(match.Operators.Count, Is.EqualTo(12));

            foreach (var seat in Four)
            {
                var expected = squads[seat];
                var actual = new List<string>();
                foreach (var op in match.Operators)
                    if (op.Owner == seat) actual.Add(op.Name);

                Assert.That(actual.Count, Is.EqualTo(Roster.SquadSize), seat.ToString());
                for (int i = 0; i < expected.Count; i++)
                    Assert.That(actual[i], Is.EqualTo(expected[i].Name), $"{seat} slot {i}");
            }

            Assert.That(squads[PlayerColor.Red][0].Name, Is.EqualTo("Luka"));
        }

        [Test]
        public void Squads_AreSnapshots()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);
            draft.FillRandom();

            var red = draft.Squads()[PlayerColor.Red];
            draft.Clear(PlayerColor.Red, 0);

            // A match dealt from the squads must not change when a slot is cleared afterwards.
            Assert.That(red[0], Is.Not.Null);
            Assert.That(red.Count, Is.EqualTo(Roster.SquadSize));
        }

        // ── Randomness ───────────────────────────────────────────────────

        [Test]
        public void RandomPick_IsDeterministicForSeed()
        {
            var a = DraftState.ForMatch(Four, DraftMode.Snake, 20260916);
            var b = DraftState.ForMatch(Four, DraftMode.Snake, 20260916);

            a.FillRandom();
            b.FillRandom();

            foreach (var seat in Four)
                for (int slot = 0; slot < Roster.SquadSize; slot++)
                    Assert.That(a.SlotOf(seat, slot).Name, Is.EqualTo(b.SlotOf(seat, slot).Name));
        }

        [Test]
        public void RandomFills_NeverDuplicateWithinASeat()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var draft = DraftState.ForMatch(Four, seed % 2 == 0 ? DraftMode.AllPick : DraftMode.Snake, seed);
                draft.FillRandom();

                foreach (var seat in Four)
                {
                    var names = new HashSet<string>();
                    foreach (var op in draft.PicksOf(seat))
                        Assert.That(names.Add(op.Name), Is.True, $"seed {seed}, {seat} drew {op.Name} twice");
                }
            }
        }

        [Test]
        public void RandomPicks_AreMarked()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);

            draft.Pick(PlayerColor.Red, Op(5));
            draft.RandomPick(PlayerColor.Red);

            Assert.That(draft.WasRandom(PlayerColor.Red, 0), Is.False);
            Assert.That(draft.WasRandom(PlayerColor.Red, 1), Is.True);
            Assert.That(draft.SlotOf(PlayerColor.Red, 1), Is.SameAs(Op(0)));
        }

        [Test]
        public void DraftRng_IsSaltedAwayFromTheMatchRng()
        {
            // Same seed, different stream: random picks must not replay the dice.
            const int seed = 12345;
            var draftRng = DraftConfig.Default.RandomFor(seed);
            var matchRng = new SeededRandom(seed);

            bool anyDifference = false;
            for (int i = 0; i < 50; i++)
                if (draftRng.NextInt(0, 1000) != matchRng.NextInt(0, 1000)) anyDifference = true;

            Assert.That(anyDifference, Is.True);
        }

        // ── Undo (snake) ─────────────────────────────────────────────────

        [Test]
        public void Undo_RestoresTurnAndPool()
        {
            var draft = NewDraft(Four, DraftMode.Snake);

            draft.Pick(PlayerColor.Red, Op(2));
            Assert.That(draft.CurrentSeat, Is.EqualTo(PlayerColor.Blue));

            Assert.That(draft.Undo(), Is.EqualTo(DraftRefusal.None));

            Assert.That(draft.CurrentSeat, Is.EqualTo(PlayerColor.Red));
            Assert.That(draft.PickCount, Is.EqualTo(0));
            Assert.That(draft.SlotOf(PlayerColor.Red, 0), Is.Null);
            Assert.That(draft.Available(PlayerColor.Red), Has.Member(Op(2)));
        }

        [Test]
        public void Undo_IsOneStepOnly()
        {
            var draft = NewDraft(Four, DraftMode.Snake);

            draft.Pick(PlayerColor.Red, Op(0));
            draft.Pick(PlayerColor.Blue, Op(1));

            Assert.That(draft.Undo(), Is.EqualTo(DraftRefusal.None));
            Assert.That(draft.CanUndo(), Is.EqualTo(DraftRefusal.NothingToRevert));
            Assert.That(draft.Undo(), Is.EqualTo(DraftRefusal.NothingToRevert));
            Assert.That(draft.SlotOf(PlayerColor.Red, 0), Is.SameAs(Op(0)));
        }

        [Test]
        public void LastPickSeat_NamesWhoUndoWouldRevert()
        {
            var snake = NewDraft(Four, DraftMode.Snake);
            Assert.That(snake.LastPickSeat, Is.EqualTo(PlayerColor.None));

            snake.Pick(PlayerColor.Red, Op(0));
            snake.RandomPick(PlayerColor.Blue);
            Assert.That(snake.LastPickSeat, Is.EqualTo(PlayerColor.Blue));

            snake.Undo();
            Assert.That(snake.LastPickSeat, Is.EqualTo(PlayerColor.None));

            var allPick = NewDraft(Four, DraftMode.AllPick);
            allPick.Pick(PlayerColor.Green, Op(0));
            Assert.That(allPick.LastPickSeat, Is.EqualTo(PlayerColor.None), "no undo in ALL PICK");
        }

        [Test]
        public void Undo_BeforeAnyPick_HasNothingToRevert()
        {
            var draft = NewDraft(Four, DraftMode.Snake);

            Assert.That(draft.Undo(), Is.EqualTo(DraftRefusal.NothingToRevert));
        }

        [Test]
        public void Undo_AfterTheLastPick_ReopensTheDraft()
        {
            var draft = NewDraft(Two, DraftMode.Snake);
            draft.FillRandom();
            Assert.That(draft.IsComplete, Is.True);

            Assert.That(draft.Undo(), Is.EqualTo(DraftRefusal.None));

            Assert.That(draft.IsComplete, Is.False);
            Assert.That(draft.CurrentSeat, Is.EqualTo(PlayerColor.Blue));
            Assert.That(draft.SecondsLeft, Is.EqualTo(DraftConfig.Default.SnakePickSeconds));
        }

        [Test]
        public void Undo_InAllPick_IsWrongMode()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);
            draft.Pick(PlayerColor.Red, Op(0));

            Assert.That(draft.Undo(), Is.EqualTo(DraftRefusal.WrongMode));
        }

        // ── Clear (all pick) ─────────────────────────────────────────────

        [Test]
        public void Clear_EmptiesTheSlot_AndTheNextPickFillsTheGap()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);

            draft.Pick(PlayerColor.Red, Op(0));
            draft.Pick(PlayerColor.Red, Op(1));
            draft.Pick(PlayerColor.Red, Op(2));

            Assert.That(draft.Clear(PlayerColor.Red, 0), Is.EqualTo(DraftRefusal.None));
            Assert.That(draft.PickCount, Is.EqualTo(2));
            Assert.That(draft.NextEmptySlot(PlayerColor.Red), Is.EqualTo(0));
            Assert.That(draft.PicksOf(PlayerColor.Red), Is.EqualTo(new[] { Op(1), Op(2) }));

            draft.Pick(PlayerColor.Red, Op(7));

            Assert.That(draft.SlotOf(PlayerColor.Red, 0), Is.SameAs(Op(7)));
        }

        [Test]
        public void Clear_AnEmptySlot_HasNothingToRevert()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);

            Assert.That(draft.Clear(PlayerColor.Red, 1), Is.EqualTo(DraftRefusal.NothingToRevert));
        }

        [Test]
        public void Clear_InSnake_IsWrongMode()
        {
            var draft = NewDraft(Two, DraftMode.Snake);
            draft.Pick(PlayerColor.Red, Op(0));

            Assert.That(draft.Clear(PlayerColor.Red, 0), Is.EqualTo(DraftRefusal.WrongMode));
        }

        [Test]
        public void Clear_WhenFullButClockRunning_ReopensTheDraft()
        {
            // START is optional in ALL PICK: until the clock runs out, a full table can still swap.
            var draft = NewDraft(Two, DraftMode.AllPick);
            draft.FillRandom();
            draft.Tick(5.0);

            Assert.That(draft.IsComplete, Is.True);
            Assert.That(draft.SecondsLeft, Is.EqualTo(25.0));

            Assert.That(draft.Clear(PlayerColor.Blue, 2), Is.EqualTo(DraftRefusal.None));
            Assert.That(draft.IsComplete, Is.False);
            Assert.That(draft.Pick(PlayerColor.Blue, Op(8)), Is.EqualTo(DraftRefusal.None));
        }

        // ── The clock ────────────────────────────────────────────────────

        [Test]
        public void AllPick_ClockStartsAtItsConfiguredLength()
        {
            var config = new DraftConfig(allPickSeconds: 45.0);
            var draft = new DraftState(Two, DraftMode.AllPick, new FirstRandom(), config);

            Assert.That(draft.SecondsLeft, Is.EqualTo(45.0));
        }

        [Test]
        public void AllPick_Timeout_KeepsPicksAndFillsTheRest()
        {
            var draft = NewDraft(Four, DraftMode.AllPick);

            draft.Pick(PlayerColor.Green, Op(6));
            draft.Pick(PlayerColor.Green, Op(7));

            Assert.That(draft.Tick(29.0), Is.EqualTo(0));
            Assert.That(draft.IsTimeUp, Is.False);

            Assert.That(draft.Tick(1.5), Is.EqualTo(10));

            Assert.That(draft.IsTimeUp, Is.True);
            Assert.That(draft.IsComplete, Is.True);
            Assert.That(draft.SecondsLeft, Is.EqualTo(0.0));
            Assert.That(draft.SlotOf(PlayerColor.Green, 0), Is.SameAs(Op(6)));
            Assert.That(draft.WasRandom(PlayerColor.Green, 1), Is.False);
            Assert.That(draft.WasRandom(PlayerColor.Green, 2), Is.True);
            Assert.That(draft.WasRandom(PlayerColor.Red, 0), Is.True);
        }

        [Test]
        public void AllPick_AfterTimeUp_TheDraftIsClosed()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);
            draft.Tick(30.0);

            Assert.That(draft.IsTimeUp, Is.True);
            Assert.That(draft.Clear(PlayerColor.Red, 0), Is.EqualTo(DraftRefusal.Complete));
            Assert.That(draft.Tick(10.0), Is.EqualTo(0));
            Assert.DoesNotThrow(() => draft.Squads());
        }

        [Test]
        public void AllPick_ClockRunsOutOnAFullTable_WithoutChangingIt()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);
            foreach (var seat in Two)
                for (int i = 0; i < Roster.SquadSize; i++) draft.Pick(seat, Op(i + 3));

            Assert.That(draft.Tick(60.0), Is.EqualTo(0));
            Assert.That(draft.IsTimeUp, Is.True);
            Assert.That(draft.WasRandom(PlayerColor.Blue, 2), Is.False);
        }

        [Test]
        public void Snake_Timeout_PicksForTheCurrentSeat_AndRestartsTheClock()
        {
            var draft = NewDraft(Four, DraftMode.Snake);

            Assert.That(draft.SecondsLeft, Is.EqualTo(10.0));
            Assert.That(draft.Tick(4.0), Is.EqualTo(0));
            Assert.That(draft.SecondsLeft, Is.EqualTo(6.0));

            Assert.That(draft.Tick(7.0), Is.EqualTo(1));

            Assert.That(draft.SlotOf(PlayerColor.Red, 0), Is.SameAs(Op(0)));
            Assert.That(draft.WasRandom(PlayerColor.Red, 0), Is.True);
            Assert.That(draft.CurrentSeat, Is.EqualTo(PlayerColor.Blue));
            Assert.That(draft.SecondsLeft, Is.EqualTo(9.0));
        }

        [Test]
        public void Snake_LongTick_MakesSeveralPicks()
        {
            var draft = NewDraft(Four, DraftMode.Snake);

            Assert.That(draft.Tick(35.0), Is.EqualTo(3));
            Assert.That(draft.CurrentSeat, Is.EqualTo(PlayerColor.Violet));
            Assert.That(draft.SecondsLeft, Is.EqualTo(5.0));

            Assert.That(draft.Tick(1000.0), Is.EqualTo(9));
            Assert.That(draft.IsComplete, Is.True);
            Assert.That(draft.SecondsLeft, Is.EqualTo(0.0));
        }

        [Test]
        public void Snake_PickRestartsTheClock()
        {
            var draft = NewDraft(Four, DraftMode.Snake);

            draft.Tick(8.0);
            draft.Pick(PlayerColor.Red, Op(3));

            Assert.That(draft.SecondsLeft, Is.EqualTo(10.0));
        }

        [Test]
        public void Snake_CompleteDraft_HasNoClock()
        {
            var draft = NewDraft(Two, DraftMode.Snake);
            draft.FillRandom();

            Assert.That(draft.SecondsLeft, Is.EqualTo(0.0));
            Assert.That(draft.Tick(50.0), Is.EqualTo(0));
            Assert.That(draft.IsTimeUp, Is.False);
        }

        [Test]
        public void Tick_RejectsNegativeTime()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);

            Assert.Throws<ArgumentOutOfRangeException>(() => draft.Tick(-0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => draft.Tick(double.NaN));
        }

        // ── Construction ─────────────────────────────────────────────────

        [Test]
        public void Construction_RejectsBadSeats()
        {
            var rng = new FirstRandom();

            Assert.Throws<ArgumentException>(() =>
                new DraftState(new PlayerColor[0], DraftMode.AllPick, rng));
            Assert.Throws<ArgumentException>(() =>
                new DraftState(new[] { PlayerColor.Red, PlayerColor.Red }, DraftMode.AllPick, rng));
            Assert.Throws<ArgumentException>(() =>
                new DraftState(new[] { PlayerColor.Red, PlayerColor.None }, DraftMode.AllPick, rng));
        }

        [Test]
        public void Construction_RejectsAPoolSmallerThanASquad()
        {
            Assert.Throws<ArgumentException>(() =>
                new DraftState(Two, DraftMode.AllPick, new FirstRandom(), pool: new[] { Op(0), Op(1) }));
        }

        [Test]
        public void Queries_RejectASeatOutsideTheDraft()
        {
            var draft = NewDraft(Two, DraftMode.AllPick);

            Assert.Throws<ArgumentException>(() => draft.CanPick(PlayerColor.Green, Op(0)));
            Assert.Throws<ArgumentException>(() => draft.FilledCount(PlayerColor.Violet));
        }

        [Test]
        public void Config_RejectsAClockThatIsNotPositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DraftConfig(allPickSeconds: 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DraftConfig(snakePickSeconds: -1.0));
        }
    }
}