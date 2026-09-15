// Assets/Tests/EditMode/Board/PathMapTests.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Board
{
    /// <summary>
    /// The coordinate scheme: progress to cell, safe cells, and distance.
    /// </summary>
    /// <remarks>
    /// <b>Every cell and progress value here is derived from the profile.</b>
    /// These tests were written against the 48-cell board with literal cells
    /// (start offset 12, journey 54) and went red when Standard became 52/6
    /// (ADR-0002 Amendment 6). Stating them as offsets, circuit and journey
    /// makes them survive the next board change, the same fix
    /// <c>TargetingRulesTests</c> received. The canonical numbers themselves
    /// are asserted once, in <c>BoardProfileTests</c>.
    /// </remarks>
    [TestFixture]
    public class PathMapTests
    {
        private PathMap _map;

        private static BoardProfile Board => BoardProfile.Standard;
        private static int Circuit => Board.CircuitLength;
        private static int Offset => Board.PlayerStartOffset;
        private static int TrackLength => Board.TrackLength;
        private static int Journey => Board.Journey;

        [SetUp]
        public void SetUp() => _map = new PathMap(Board);

        // ── Start cells ──────────────────────────────────────────────────

        [Test]
        public void StartCells_AreOneQuarterOfTheLoopApart()
        {
            Assert.That(_map.StartTrackIndex(PlayerColor.Red), Is.EqualTo(0));
            Assert.That(_map.StartTrackIndex(PlayerColor.Blue), Is.EqualTo(Offset));
            Assert.That(_map.StartTrackIndex(PlayerColor.Green), Is.EqualTo(2 * Offset));
            Assert.That(_map.StartTrackIndex(PlayerColor.Yellow), Is.EqualTo(3 * Offset));
            Assert.That(4 * Offset, Is.EqualTo(Circuit), "four equal quarters");
        }

        [Test]
        public void StartCellSpacing_FollowsTheBoardProfile()
        {
            var sprint = new PathMap(BoardProfile.Sprint);

            Assert.That(sprint.StartTrackIndex(PlayerColor.Green),
                Is.EqualTo(2 * BoardProfile.Sprint.PlayerStartOffset));
            Assert.That(BoardProfile.Sprint.PlayerStartOffset, Is.Not.EqualTo(Offset),
                "precondition: the two profiles space their starts differently");
        }

        [Test]
        public void NoneColor_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => _map.StartTrackIndex(PlayerColor.None));
        }

        // ── Progress to cell ─────────────────────────────────────────────

        [Test]
        public void ProgressZero_IsTheColoursOwnStartCell()
        {
            Assert.That(_map.CellAt(PlayerColor.Red, 0), Is.EqualTo(CellRef.Track(0)));
            Assert.That(_map.CellAt(PlayerColor.Blue, 0), Is.EqualTo(CellRef.Track(Offset)));
        }

        [Test]
        public void ProgressWrapsAroundTheLoop()
        {
            // Blue starts at Offset; this many steps later it has passed the end
            // of the loop and stands on track 4.
            int progress = Circuit - Offset + 4;

            Assert.That(_map.CellAt(PlayerColor.Blue, progress), Is.EqualTo(CellRef.Track(4)));
        }

        [Test]
        public void LastTrackCell_IsOneStepBeforeTheColoursOwnStart()
        {
            // A full lap lands the operator back where it began, so the final
            // track cell is the one immediately behind its start.
            Assert.That(_map.CellAt(PlayerColor.Red, Circuit - 1), Is.EqualTo(CellRef.Track(Circuit - 1)));
            Assert.That(_map.CellAt(PlayerColor.Blue, Circuit - 1), Is.EqualTo(CellRef.Track(Offset - 1)));
        }

        [Test]
        public void ProgressAtCircuitLength_EntersTheHomeColumn()
        {
            Assert.That(_map.CellAt(PlayerColor.Red, TrackLength),
                Is.EqualTo(CellRef.HomeColumn(PlayerColor.Red, 0)));
        }

        [Test]
        public void LastHomeColumnCell_IsOneStepShortOfHome()
        {
            Assert.That(_map.CellAt(PlayerColor.Red, Journey - 1),
                Is.EqualTo(CellRef.HomeColumn(PlayerColor.Red, Board.HomeColumnLength - 1)));
        }

        [Test]
        public void ProgressAtJourney_IsHome()
        {
            Assert.That(_map.CellAt(PlayerColor.Red, Journey), Is.EqualTo(CellRef.Home(PlayerColor.Red)));
        }

        [Test]
        public void OvershootingHome_Finishes_RatherThanBouncing()
        {
            // Home entry is automatic on the MVP and needs no exact roll
            // (ADR-0003), so a move past HOME still finishes.
            Assert.That(_map.CellAt(PlayerColor.Red, Journey + 10), Is.EqualTo(CellRef.Home(PlayerColor.Red)));
        }

        [Test]
        public void NegativeProgress_IsTheYard()
        {
            Assert.That(_map.CellAt(PlayerColor.Green, PathMap.YardProgress),
                Is.EqualTo(CellRef.Yard(PlayerColor.Green)));
        }

        [Test]
        public void ProgressBelowTheYard_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _map.CellAt(PlayerColor.Red, -2));
        }

        // ── Cell identity ────────────────────────────────────────────────

        [Test]
        public void DifferentColoursOnTheSameTrackCell_AreTheSamePlace()
        {
            // This is what makes a collision detectable at all. Red one quarter
            // round and Blue on its start are standing on the same track cell.
            var red = _map.CellAt(PlayerColor.Red, Offset);
            var blue = _map.CellAt(PlayerColor.Blue, 0);

            Assert.That(red, Is.EqualTo(blue));
        }

        [Test]
        public void HomeColumnsOfDifferentColours_AreDifferentPlaces()
        {
            var red = _map.CellAt(PlayerColor.Red, TrackLength);
            var blue = _map.CellAt(PlayerColor.Blue, TrackLength);

            Assert.That(red, Is.Not.EqualTo(blue));
        }

        [Test]
        public void EveryProgressValue_MapsToADistinctCellForOneColour()
        {
            // Guards the whole coordinate scheme: if any two progress values
            // aliased to one cell, an operator could occupy two places at once
            // or skip a cell silently.
            var seen = new HashSet<CellRef>();

            for (int progress = 0; progress < Journey; progress++)
            {
                Assert.That(seen.Add(_map.CellAt(PlayerColor.Yellow, progress)), Is.True,
                    $"Progress {progress} aliased an earlier cell.");
            }

            Assert.That(seen.Count, Is.EqualTo(Journey));
        }

        // ── Safe cells ───────────────────────────────────────────────────

        [Test]
        public void AllFourStartCells_AreSafe()
        {
            foreach (var color in new[]
                     { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow })
            {
                Assert.That(_map.IsSafe(CellRef.Track(_map.StartTrackIndex(color))), Is.True,
                    $"{color}'s start cell should be safe.");
            }
        }

        [Test]
        public void AStartCell_IsSafeForEveryoneStandingOnIt_NotJustItsOwner()
        {
            // Standard Ludo. A safe cell is a property of the cell, not of who
            // owns it — otherwise deploying onto an occupied start could trigger
            // a collision, which COMBAT_SYSTEMS §1.3 rules out.
            var blueStart = _map.CellAt(PlayerColor.Blue, 0);
            var redAtSameCell = _map.CellAt(PlayerColor.Red, Offset);

            Assert.That(_map.IsSafe(redAtSameCell), Is.True);
            Assert.That(redAtSameCell, Is.EqualTo(blueStart));
        }

        [Test]
        public void OrdinaryTrackCells_AreNotSafe()
        {
            Assert.That(_map.IsSafe(CellRef.Track(1)), Is.False);
            Assert.That(_map.IsSafe(CellRef.Track(Offset - 1)), Is.False);
            Assert.That(_map.IsSafe(CellRef.Track(Circuit - 1)), Is.False);
        }

        [Test]
        public void HomeColumnMouth_IsSafe_ButDeeperCellsAreNot()
        {
            Assert.That(_map.IsSafe(CellRef.HomeColumn(PlayerColor.Red, 0)), Is.True);
            Assert.That(_map.IsSafe(CellRef.HomeColumn(PlayerColor.Red, 1)), Is.False);
        }

        [Test]
        public void YardAndHome_AreNotSafeCells()
        {
            // They are off the board, not protected squares on it.
            Assert.That(_map.IsSafe(CellRef.Yard(PlayerColor.Red)), Is.False);
            Assert.That(_map.IsSafe(CellRef.Home(PlayerColor.Red)), Is.False);
        }

        // ── Distance ─────────────────────────────────────────────────────

        [Test]
        public void TrackDistance_IsCountedInBothDirections()
        {
            // Cells 2 and (Circuit - 2) are four apart the short way, across the
            // seam at track 0, and Circuit - 4 apart the long way.
            Assert.That(_map.TrackDistance(CellRef.Track(2), CellRef.Track(Circuit - 2)), Is.EqualTo(4));
        }

        [Test]
        public void TrackDistance_IsSymmetric()
        {
            Assert.That(_map.TrackDistance(CellRef.Track(Circuit - 2), CellRef.Track(2)), Is.EqualTo(4));
        }

        [Test]
        public void TrackDistance_AcrossTheBoardCentre_IsMeasuredAlongTheLoop()
        {
            // Red's and Green's starts sit opposite each other across the centre
            // of the cross — close in space, half the loop apart in play. Range
            // must use the loop (COMBAT_SYSTEMS §4.1).
            var redStart = CellRef.Track(_map.StartTrackIndex(PlayerColor.Red));
            var greenStart = CellRef.Track(_map.StartTrackIndex(PlayerColor.Green));

            Assert.That(_map.TrackDistance(redStart, greenStart), Is.EqualTo(Circuit / 2));
        }

        [Test]
        public void TrackDistance_ToItself_IsZero()
        {
            Assert.That(_map.TrackDistance(CellRef.Track(7), CellRef.Track(7)), Is.EqualTo(0));
        }

        [Test]
        public void TrackDistance_NeverExceedsHalfTheLoop()
        {
            for (int a = 0; a < Circuit; a++)
            {
                for (int b = 0; b < Circuit; b++)
                {
                    Assert.That(_map.TrackDistance(CellRef.Track(a), CellRef.Track(b)),
                        Is.LessThanOrEqualTo(Circuit / 2));
                }
            }
        }

        [Test]
        public void TrackDistance_ToAHomeColumnCell_IsUnreachable()
        {
            // Not "far away" — unreachable. An operator in its home column is
            // out of the fight entirely (COMBAT_SYSTEMS §4.3).
            Assert.That(_map.TrackDistance(CellRef.Track(0), CellRef.HomeColumn(PlayerColor.Red, 0)),
                Is.Null);
        }

        [Test]
        public void TrackDistance_ToTheYardOrHome_IsUnreachable()
        {
            Assert.That(_map.TrackDistance(CellRef.Track(0), CellRef.Yard(PlayerColor.Red)), Is.Null);
            Assert.That(_map.TrackDistance(CellRef.Track(0), CellRef.Home(PlayerColor.Red)), Is.Null);
        }

        // ── Phase predicates ─────────────────────────────────────────────

        [Test]
        public void ProgressPredicates_PartitionTheJourney()
        {
            Assert.That(_map.IsOnOuterTrack(PathMap.YardProgress), Is.False);
            Assert.That(_map.IsOnOuterTrack(0), Is.True);
            Assert.That(_map.IsOnOuterTrack(TrackLength - 1), Is.True);
            Assert.That(_map.IsOnOuterTrack(TrackLength), Is.False);

            Assert.That(_map.IsInHomeColumn(TrackLength - 1), Is.False);
            Assert.That(_map.IsInHomeColumn(TrackLength), Is.True);
            Assert.That(_map.IsInHomeColumn(Journey - 1), Is.True);
            Assert.That(_map.IsInHomeColumn(Journey), Is.False);

            Assert.That(_map.HasFinished(Journey - 1), Is.False);
            Assert.That(_map.HasFinished(Journey), Is.True);
        }
    }
}
