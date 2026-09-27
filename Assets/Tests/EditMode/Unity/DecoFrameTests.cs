// Assets/Tests/EditMode/Unity/DecoFrameTests.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The Deco skin's steel frame (board skin BS8) ends where Classic's rail
    /// ends, so framing is unchanged, and stops short of every cell and yard panel.
    /// </summary>
    [TestFixture]
    public class DecoFrameTests
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
        public void OuterEdge_IsClassicsRailEdge_SoTheCameraFramesTheSame()
        {
            foreach (var profile in Profiles())
            {
                var layout = new BoardLayout(profile, 1f);
                float tableHalf = layout.GridSize * 0.5f + BoardLayout.TableMargin;

                // The rail's outer edge: the table's edge plus the rail's overhang past it.
                Assert.Greater(BoardView.DecoFrameOuter(layout), tableHalf, profile.Name);
                Assert.Less(BoardView.DecoFrameOuter(layout), tableHalf + BoardArt.RailCells, profile.Name);
            }
        }

        [Test]
        public void InnerEdge_ClearsEveryCell_AndEveryYardPanel()
        {
            foreach (var profile in Profiles())
            {
                var layout = new BoardLayout(profile, 1f);
                var centre = layout.HomeGoalPosition;
                float inner = BoardView.DecoFrameInner(layout);
                float cellHalf = layout.CellSize * 0.5f;

                for (int i = 0; i < profile.CircuitLength; i++)
                {
                    var c = layout.PositionOf(CellRef.Track(i)) - centre;
                    Assert.Less(Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y)) + cellHalf, inner, $"{profile.Name} track {i}");
                }

                float panelHalf = BoardView.DecoYardPanelSide(layout) * 0.5f;
                foreach (var seat in Seats)
                {
                    var p = BoardView.DecoYardPanelCentre(layout, seat) - centre;
                    Assert.LessOrEqual(Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) + panelHalf, inner + 1e-4f, $"{profile.Name} {seat} panel");
                }
            }
        }

        [Test]
        public void TheCross_ThePanels_AndTheFrame_ShareTheirEdges()
        {
            foreach (var profile in Profiles())
            {
                var layout = new BoardLayout(profile, 1f);
                var centre = layout.HomeGoalPosition;
                float inner = BoardView.DecoFrameInner(layout);
                float half = BoardView.DecoYardPanelSide(layout) * 0.5f;
                float armEdge = 1.5f + BoardArt.CrossPad;

                // The frame's inner edge is the arms' tips: the cross's own outline.
                Assert.AreEqual(layout.GridSize * 0.5f + BoardArt.CrossPad, inner, 1e-4f, profile.Name);

                foreach (var seat in Seats)
                {
                    var p = BoardView.DecoYardPanelCentre(layout, seat) - centre;
                    Assert.AreEqual(inner, Mathf.Abs(p.x) + half, 1e-4f, $"{profile.Name} {seat}: outer edge on the frame");
                    Assert.AreEqual(armEdge, Mathf.Abs(p.x) - half, 1e-4f, $"{profile.Name} {seat}: inner edge on the arm");
                    Assert.AreEqual(inner, Mathf.Abs(p.y) + half, 1e-4f, $"{profile.Name} {seat}: outer edge on the frame");
                    Assert.AreEqual(armEdge, Mathf.Abs(p.y) - half, 1e-4f, $"{profile.Name} {seat}: inner edge on the arm");
                }
            }
        }

        [Test]
        public void TheFrame_HasRoomForItsBolts_AndItsChamfer()
        {
            var layout = new BoardLayout(BoardProfile.Standard, 1f);
            float width = BoardView.DecoFrameOuter(layout) - BoardView.DecoFrameInner(layout);

            Assert.Greater(width, DecoBoardArt.FrameBoltSize * 3f);
            Assert.Less(DecoBoardArt.FrameChamfer, width * 2f);
        }
    }
}
