// Assets/Tests/EditMode/Board/BoardProfileTests.cs
using System;
using NonaRoyale.Core.Board;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Board
{
    [TestFixture]
    public class BoardProfileTests
    {
        [Test]
        public void StandardProfile_MatchesTheCanonicalConstants()
        {
            // ADR-0002: CircuitLength 48, HomeColumnLength 6,
            // PlayerStartOffset 12, 72 total path positions.
            var board = BoardProfile.Standard;

            Assert.That(board.CircuitLength, Is.EqualTo(48));
            Assert.That(board.HomeColumnLength, Is.EqualTo(6));
            Assert.That(board.PlayerStartOffset, Is.EqualTo(12));
            Assert.That(board.Journey, Is.EqualTo(54));
            Assert.That(board.TotalPathPositions, Is.EqualTo(72));
        }

        [Test]
        public void SprintProfile_IsTheSameTopologyWithFewerCells()
        {
            var board = BoardProfile.Sprint;

            Assert.That(board.CircuitLength, Is.EqualTo(24));
            Assert.That(board.HomeColumnLength, Is.EqualTo(3));
            Assert.That(board.PlayerStartOffset, Is.EqualTo(6));
            Assert.That(board.Journey, Is.EqualTo(27));
        }

        [Test]
        public void CircuitNotDivisibleByFour_IsRejected()
        {
            // An indivisible circuit would space the four starts unevenly and
            // silently hand one seat a shorter journey.
            Assert.Throws<ArgumentException>(() => new BoardProfile("Bad", 50, 6));
        }

        [Test]
        public void CircuitBelowTheMinimum_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoardProfile("Tiny", 4, 1));
        }

        [Test]
        public void EmptyHomeColumn_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoardProfile("NoHome", 48, 0));
        }

        [Test]
        public void StartOffsets_DivideTheCircuitIntoFourEqualQuarters()
        {
            var board = BoardProfile.Standard;

            Assert.That(board.PlayerStartOffset * BoardProfile.PlayerCount,
                Is.EqualTo(board.CircuitLength));
        }

        [Test]
        public void FromCircuitLength_DerivesTheHomeColumnByHalvingTheArm()
        {
            foreach (var circuit in new[] { 24, 32, 40, 48, 56 })
            {
                var board = BoardProfile.FromCircuitLength("derived", circuit);

                Assert.That(board.HomeColumnLength, Is.EqualTo(board.PlayerStartOffset / 2),
                    $"circuit {circuit}");
                Assert.That(board.CircuitLength, Is.EqualTo(circuit));
            }
        }

        [Test]
        public void FromCircuitLength_MatchesTheNamedProfilesItCanReach()
        {
            Assert.That(BoardProfile.FromCircuitLength("x", 48).HomeColumnLength,
                Is.EqualTo(BoardProfile.Standard.HomeColumnLength));
            Assert.That(BoardProfile.FromCircuitLength("x", 24).HomeColumnLength,
                Is.EqualTo(BoardProfile.Sprint.HomeColumnLength));
        }

        [Test]
        public void FromCircuitLength_WhereTheArmDoesNotHalve_IsRejected()
        {
            // 36 -> arm 9, 52 -> arm 13, 60 -> arm 15. All odd.
            foreach (var circuit in new[] { 36, 52, 60 })
            {
                Assert.Throws<ArgumentException>(
                    () => BoardProfile.FromCircuitLength("odd arm", circuit));
            }
        }

        [Test]
        public void BoardsTheShortcutCannotDerive_AreStillLegalViaTheConstructor()
        {
            // The crux. FromCircuitLength rejecting 36 and 60 must not be read
            // as those boards being invalid: ADR-0002 names them, the sim
            // measured them, and classic Ludo is 52/6. Only the shortcut is
            // narrow, never the board model.
            Assert.That(new BoardProfile("Fallback", 36, 5).Journey, Is.EqualTo(41));
            Assert.That(BoardProfile.Long.CircuitLength, Is.EqualTo(60));
            Assert.That(new BoardProfile("Classic Ludo", 52, 6).TotalPathPositions,
                Is.EqualTo(76));
        }

        [Test]
        public void FromCircuitLength_StillValidatesTheCircuitItself()
        {
            Assert.Throws<ArgumentException>(() => BoardProfile.FromCircuitLength("bad", 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => BoardProfile.FromCircuitLength("tiny", 4));
            Assert.Throws<ArgumentException>(() => BoardProfile.FromCircuitLength("  ", 48));
        }

        [Test]
        public void HomeColumnLength_IsNotDerivedFromCircuitLength()
        {
            // ADR-0002 states HomeColumnLength = PlayerStartOffset / 2, as if
            // one integer defined a board. Under integer division that happens
            // to hold for 24, 48 and even 60 — but not for 36, which the ADR
            // lists as a 5-cell column while the rule would give 4.
            //
            // So both values stay explicit config. This test exists so nobody
            // "tidies" them into a derivation later and quietly shortens the
            // 36 profile.
            var thirtySix = new BoardProfile("Fallback", 36, 5);

            Assert.That(thirtySix.PlayerStartOffset, Is.EqualTo(9));
            Assert.That(thirtySix.HomeColumnLength, Is.EqualTo(5));
            Assert.That(thirtySix.HomeColumnLength,
                Is.Not.EqualTo(thirtySix.PlayerStartOffset / 2));
        }
    }
}