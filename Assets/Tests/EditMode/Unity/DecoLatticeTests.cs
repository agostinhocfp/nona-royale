// Assets/Tests/EditMode/Unity/DecoLatticeTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The Deco skin's lattice, rivets and corner facets (board skin BS6)
    /// run between the cells and never cover one.
    /// </summary>
    [TestFixture]
    public class DecoLatticeTests
    {
        private static readonly PlayerColor[] Seats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
        };

        private static IEnumerable<BoardProfile> Profiles()
        {
            yield return BoardProfile.Standard;
            yield return BoardProfile.Cross("Compact", 3, laps: 2);
        }

        /// <summary>Every cell's centre, in spacings from the board's centre.</summary>
        private static List<Vector2> Cells(BoardProfile profile)
        {
            var layout = new BoardLayout(profile, 1f);
            var centre = layout.HomeGoalPosition;
            var cells = new List<Vector2>();
            for (int i = 0; i < profile.CircuitLength; i++) cells.Add(layout.PositionOf(CellRef.Track(i)) - centre);
            foreach (var seat in Seats)
                for (int d = 0; d < profile.HomeColumnLength; d++)
                    cells.Add(layout.PositionOf(CellRef.HomeColumn(seat, d)) - centre);
            return cells;
        }

        /// <summary>True when a square of half-size <paramref name="reach"/> about the point overlaps a cell.</summary>
        private static bool TouchesACell(Vector2 point, float reach, List<Vector2> cells)
        {
            const float cellHalf = 0.43f;
            foreach (var c in cells)
                if (Mathf.Abs(point.x - c.x) < cellHalf + reach && Mathf.Abs(point.y - c.y) < cellHalf + reach)
                    return true;
            return false;
        }

        [Test]
        public void LatticeLines_RunDownTheGaps_AndNeverCrossACell()
        {
            foreach (var profile in Profiles())
            {
                var cells = Cells(profile);
                int arm = new BoardLayout(profile, 1f).ArmLength;

                foreach (var (from, to) in BoardView.DecoLatticeSegments(arm))
                {
                    Assert.IsTrue(from.x == to.x || from.y == to.y, "lines are straight along an axis");

                    for (float t = 0f; t <= 1f; t += 0.01f)
                    {
                        var p = Vector2.Lerp(from, to, t);
                        Assert.IsFalse(TouchesACell(p, BoardView.DecoLatticeWidth * 0.5f, cells), $"{profile.Name}: {from}→{to} at {p}");
                    }
                }
            }
        }

        [Test]
        public void Rivets_SitOnTheLattice_AndNeverTouchACell()
        {
            foreach (var profile in Profiles())
            {
                var cells = Cells(profile);
                int arm = new BoardLayout(profile, 1f).ArmLength;

                foreach (var rivet in BoardView.DecoLatticeRivets(arm))
                    Assert.IsFalse(TouchesACell(rivet, BoardView.DecoRivetSize * 0.5f, cells), $"{profile.Name}: rivet at {rivet}");
            }
        }

        [Test]
        public void Lattice_HasTheExpectedLines_PerArm()
        {
            // Two between the lanes, one across the mouth, one per gap between rows.
            Assert.AreEqual(4 * (3 + 5), BoardView.DecoLatticeSegments(6).Count);
            Assert.AreEqual(4 * 2 * 5, BoardView.DecoLatticeRivets(6).Count);
        }

        [Test]
        public void CornerFacets_NeverEnterACell()
        {
            foreach (var profile in Profiles())
            {
                var cells = Cells(profile);
                for (float x = 0.5f; x <= 1.5f; x += 0.01f)
                    for (float y = 0.5f; y <= 1.5f; y += 0.01f)
                        foreach (bool upper in new[] { true, false })
                        {
                            if (!DecoBoardArt.InFacet(x, y, upper)) continue;

                            // All four corners: the facet art turns with its wedge.
                            var p = new Vector2(x, y);
                            for (int k = 0; k < 4; k++)
                            {
                                Assert.IsFalse(TouchesACell(p, 0f, cells), $"{profile.Name}: facet at {p}");
                                p = new Vector2(-p.y, p.x);
                            }
                        }
            }
        }
    }
}
