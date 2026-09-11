// Assets/Tests/EditMode/Board/PathMapTests.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Board
{
    [TestFixture]
    public class PathMapTests
    {
        private PathMap _map;

        [SetUp]
        public void SetUp() => _map = new PathMap(BoardProfile.Standard);

        // ── Start cells ──────────────────────────────────────────────────

        [Test]
        public void StartCells_AreOneQuarterOfTheLoopApart()
        {
            Assert.That(_map.StartTrackIndex(PlayerColor.Red), Is.EqualTo(0));
            Assert.That(_map.StartTrackIndex(PlayerColor.Blue), Is.EqualTo(12));
            Assert.That(_map.StartTrackIndex(PlayerColor.Green), Is.EqualTo(24));
            Assert.That(_map.StartTrackIndex(PlayerColor.Yellow), Is.EqualTo(36));
        }

        [Test]
        public void StartCellSpacing_FollowsTheBoardProfile()
        {
            var sprint = new PathMap(BoardProfile.Sprint);

            Assert.That(sprint.StartTrackIndex(PlayerColor.Green), Is.EqualTo(12));
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
            Assert.That(_map.CellAt(PlayerColor.Blue, 0), Is.EqualTo(CellRef.Track(12)));
        }

        [Test]
        public void ProgressWrapsAroundTheLoop()
        {
            // Blue starts at 12; forty steps later it is at (12 + 40) % 48 = 4.
            Assert.That(_map.CellAt(PlayerColor.Blue, 40), Is.EqualTo(CellRef.Track(4)));
        }

        [Test]
        public void LastTrackCell_IsOneStepBeforeTheColoursOwnStart()
        {
            // A full lap lands the operator back where it began, so the final
            // track cell is the one immediately behind its start.
            Assert.That(_map.CellAt(PlayerColor.Red, 47), Is.EqualTo(CellRef.Track(47)));
            Assert.That(_map.CellAt(PlayerColor.Blue, 47), Is.EqualTo(CellRef.Track(11)));
        }

        [Test]
        public void ProgressAtCircuitLength_EntersTheHomeColumn()
        {
            Assert.That(_map.CellAt(PlayerColor.Red, 48),
                Is.EqualTo(CellRef.HomeColumn(PlayerColor.Red, 0)));
        }

        [Test]
        public void LastHomeColumnCell_IsOneStepShortOfHome()
        {
            Assert.That(_map.CellAt(PlayerColor.Red, 53),
                Is.EqualTo(CellRef.HomeColumn(PlayerColor.Red, 5)));
        }

        [Test]
        public void ProgressAtJourney_IsHome()
        {
            Assert.That(_map.CellAt(PlayerColor.Red, 54), Is.EqualTo(CellRef.Home(PlayerColor.Red)));
        }

        [Test]
        public void OvershootingHome_Finishes_RatherThanBouncing()
        {
            // Home entry is automatic on the MVP and needs no exact roll
            // (ADR-0003), so a move past HOME still finishes.
            Assert.That(_map.CellAt(PlayerColor.Red, 99), Is.EqualTo(CellRef.Home(PlayerColor.Red)));
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
            // This is what makes a collision detectable at all. Red at progress
            // 12 and Blue at progress 0 are both standing on track cell 12.
            var red = _map.CellAt(PlayerColor.Red, 12);
            var blue = _map.CellAt(PlayerColor.Blue, 0);

            Assert.That(red, Is.EqualTo(blue));
        }

        [Test]
        public void HomeColumnsOfDifferentColours_AreDifferentPlaces()
        {
            var red = _map.CellAt(PlayerColor.Red, 48);
            var blue = _map.CellAt(PlayerColor.Blue, 48);

            Assert.That(red, Is.Not.EqualTo(blue));
        }

        [Test]
        public void EveryProgressValue_MapsToADistinctCellForOneColour()
        {
            // Guards the whole coordinate scheme: if any two progress values
            // aliased to one cell, an operator could occupy two places at once
            // or skip a cell silently.
            var seen = new HashSet<CellRef>();

            for (int progress = 0; progress < BoardProfile.Standard.Journey; progress++)
            {
                Assert.That(seen.Add(_map.CellAt(PlayerColor.Yellow, progress)), Is.True,
                    $"Progress {progress} aliased an earlier cell.");
            }

            Assert.That(seen.Count, Is.EqualTo(54));
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
            var redAtSameCell = _map.CellAt(PlayerColor.Red, 12);

            Assert.That(_map.IsSafe(redAtSameCell), Is.True);
            Assert.That(redAtSameCell, Is.EqualTo(blueStart));
        }

        [Test]
        public void OrdinaryTrackCells_AreNotSafe()
        {
            Assert.That(_map.IsSafe(CellRef.Track(1)), Is.False);
            Assert.That(_map.IsSafe(CellRef.Track(11)), Is.False);
            Assert.That(_map.IsSafe(CellRef.Track(47)), Is.False);
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
            // Cells 2 and 46 are 44 apart the long way and 4 the short way.
            Assert.That(_map.TrackDistance(CellRef.Track(2), CellRef.Track(46)), Is.EqualTo(4));
        }

        [Test]
        public void TrackDistance_IsSymmetric()
        {
            Assert.That(_map.TrackDistance(CellRef.Track(46), CellRef.Track(2)), Is.EqualTo(4));
        }

        [Test]
        public void TrackDistance_AcrossTheBoardCentre_IsMeasuredAlongTheLoop()
        {
            // Cells 0 and 24 sit physically opposite each other across the
            // centre of the cross — close in space, a quarter of the loop apart
            // in play. Range must use the loop (COMBAT_SYSTEMS §4.1).
            Assert.That(_map.TrackDistance(CellRef.Track(0), CellRef.Track(24)), Is.EqualTo(24));
        }

        [Test]
        public void TrackDistance_ToItself_IsZero()
        {
            Assert.That(_map.TrackDistance(CellRef.Track(7), CellRef.Track(7)), Is.EqualTo(0));
        }

        [Test]
        public void TrackDistance_NeverExceedsHalfTheLoop()
        {
            for (int a = 0; a < 48; a++)
            {
                for (int b = 0; b < 48; b++)
                {
                    Assert.That(_map.TrackDistance(CellRef.Track(a), CellRef.Track(b)),
                        Is.LessThanOrEqualTo(24));
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
            Assert.That(_map.IsOnOuterTrack(47), Is.True);
            Assert.That(_map.IsOnOuterTrack(48), Is.False);

            Assert.That(_map.IsInHomeColumn(47), Is.False);
            Assert.That(_map.IsInHomeColumn(48), Is.True);
            Assert.That(_map.IsInHomeColumn(53), Is.True);
            Assert.That(_map.IsInHomeColumn(54), Is.False);

            Assert.That(_map.HasFinished(53), Is.False);
            Assert.That(_map.HasFinished(54), Is.True);
        }
    }
}