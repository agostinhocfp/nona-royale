// Assets/Tests/EditMode/Unity/DecoCentreTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The Deco skin's centre (board skin BS3) never covers a cell: the
    /// medallion leaves every home column's last cell whole (ART §10 ref 5),
    /// and the four corner wedges run between the cells to the inner corners.
    /// </summary>
    [TestFixture]
    public class DecoCentreTests
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

        /// <summary>Every cell's centre on the board: the track and all four home columns.</summary>
        private static List<Vector3> Cells(BoardProfile profile, BoardLayout layout)
        {
            var cells = new List<Vector3>();
            for (int i = 0; i < profile.CircuitLength; i++) cells.Add(layout.PositionOf(CellRef.Track(i)));
            foreach (var seat in Seats)
                for (int d = 0; d < profile.HomeColumnLength; d++)
                    cells.Add(layout.PositionOf(CellRef.HomeColumn(seat, d)));
            return cells;
        }

        private static bool InsideAnyCell(Vector3 point, List<Vector3> cells, float half)
        {
            foreach (var c in cells)
                if (Mathf.Abs(point.x - c.x) < half && Mathf.Abs(point.y - c.y) < half) return true;
            return false;
        }

        [Test]
        public void Medallion_LeavesEveryHomeColumnsLastCellWhole()
        {
            foreach (var profile in Profiles())
            {
                var layout = new BoardLayout(profile, 1.3f);
                float radius = BoardView.DecoMedallionDiameter(layout) * 0.5f;
                var centre = layout.HomeGoalPosition;

                foreach (var seat in Seats)
                {
                    var last = layout.PositionOf(CellRef.HomeColumn(seat, profile.HomeColumnLength - 1));
                    float innerEdge = Vector3.Distance(last, centre) - layout.CellSize * 0.5f;
                    Assert.Less(radius, innerEdge, $"{profile.Name} {seat}");
                }

                Assert.Less(BoardView.DecoMedallionDiameter(layout), layout.HomeGoalSize, profile.Name);
            }
        }

        [Test]
        public void Wedges_NeverEnterACell_OnAnyBoard()
        {
            foreach (var profile in Profiles())
            {
                var layout = new BoardLayout(profile, 1.3f);
                var cells = Cells(profile, layout);
                var centre = layout.HomeGoalPosition;
                float spacing = layout.Spacing;
                float half = layout.CellSize * 0.5f;

                for (int k = 0; k < 4; k++)
                {
                    for (float t = DecoBoardArt.WedgeFrom; t <= DecoBoardArt.WedgeTo; t += 0.01f)
                    {
                        float w = DecoBoardArt.WedgeHalfWidthAt(t);
                        foreach (float s in new[] { -w, 0f, w })
                        {
                            var local = new Vector3((t - s) / Mathf.Sqrt(2f), (t + s) / Mathf.Sqrt(2f), 0f) * spacing;

                            // A quarter turn per wedge, as BoardView turns them.
                            for (int q = 0; q < k; q++) local = new Vector3(-local.y, local.x, 0f);

                            Assert.IsFalse(InsideAnyCell(centre + local, cells, half),
                                $"{profile.Name}: wedge {k} at t={t:0.00}, s={s:0.00}");
                        }
                    }
                }
            }
        }

        [Test]
        public void Wedge_StartsUnderTheMedallion_AndStopsShortOfTheInnerCorner()
        {
            var layout = new BoardLayout(BoardProfile.Standard, 1f);
            Assert.Less(DecoBoardArt.WedgeFrom, BoardView.DecoMedallionDiameter(layout) * 0.5f);

            // The inner corner is 1.5 spacings out on each axis; the sprite covers exactly that square.
            Assert.Less(DecoBoardArt.WedgeTo, DecoBoardArt.WedgeSpan * Mathf.Sqrt(2f));
            Assert.Greater(DecoBoardArt.WedgeTo, 1.8f);
        }

        [Test]
        public void InWedge_IsFalseOffTheSpine_AndBeyondItsEnds()
        {
            Assert.IsTrue(DecoBoardArt.InWedge(DecoBoardArt.WedgeWidestAt, 0f));
            Assert.IsFalse(DecoBoardArt.InWedge(DecoBoardArt.WedgeWidestAt, DecoBoardArt.WedgeHalfWidth + 0.01f));
            Assert.IsFalse(DecoBoardArt.InWedge(DecoBoardArt.WedgeFrom - 0.01f, 0f));
            Assert.IsFalse(DecoBoardArt.InWedge(DecoBoardArt.WedgeTo + 0.01f, 0f));
        }
    }
}
