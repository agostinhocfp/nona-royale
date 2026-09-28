// Assets/Tests/EditMode/Unity/DecoYardTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The Deco skin's yard panels (board skin BS7) frame each table and never
    /// reach a cell.
    /// </summary>
    [TestFixture]
    public class DecoYardTests
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

        [Test]
        public void Panels_NeverOverlapACell()
        {
            foreach (var profile in Profiles())
            {
                var layout = new BoardLayout(profile, 1.3f);
                float panelHalf = BoardView.DecoYardPanelSide(layout) * 0.5f;
                float cellHalf = layout.CellSize * 0.5f;

                var cells = new List<Vector3>();
                for (int i = 0; i < profile.CircuitLength; i++) cells.Add(layout.PositionOf(CellRef.Track(i)));
                foreach (var seat in Seats)
                    for (int d = 0; d < profile.HomeColumnLength; d++)
                        cells.Add(layout.PositionOf(CellRef.HomeColumn(seat, d)));

                foreach (var seat in Seats)
                {
                    var panel = BoardView.DecoYardPanelCentre(layout, seat);
                    foreach (var cell in cells)
                    {
                        bool overlaps = Mathf.Abs(panel.x - cell.x) < panelHalf + cellHalf &&
                                        Mathf.Abs(panel.y - cell.y) < panelHalf + cellHalf;
                        Assert.IsFalse(overlaps, $"{profile.Name}: {seat}'s panel reaches the cell at {cell}");
                    }
                }
            }
        }

        [Test]
        public void EveryTable_SitsInsideItsPanel_ClearOfTheCornerOrnaments()
        {
            foreach (var profile in Profiles())
            {
                var layout = new BoardLayout(profile, 1f);
                float side = BoardView.DecoYardPanelSide(layout);
                float radius = layout.TableDiameter * 0.5f;

                // The table sits on the yard's centre, CrossPad off the panel's, toward the arms (BS8b).
                float offset = BoardArt.CrossPad;

                // The hairline, and how far the corner ornament reaches in along the diagonal.
                float line = side * (0.5f - DecoBoardArt.YardLine) - offset;
                Assert.Less(radius, line, profile.Name);

                float cornerInner = side * (0.5f - DecoBoardArt.YardLine - DecoBoardArt.YardCornerReach * 0.5f) - offset;
                Assert.Less(radius, cornerInner * Mathf.Sqrt(2f), profile.Name);
            }
        }

        [Test]
        public void TheHairline_IsOneSubtleLine_JustInsideTheEdge()
        {
            // BS9: one line, as the target draws, close to the panel's edge and under full strength.
            Assert.Greater(DecoBoardArt.YardLine, 0f);
            Assert.Less(DecoBoardArt.YardLine, 0.05f);
            Assert.Greater(DecoBoardArt.YardLineStrength, 0f);
            Assert.Less(DecoBoardArt.YardLineStrength, 1f);
        }
    }
}
