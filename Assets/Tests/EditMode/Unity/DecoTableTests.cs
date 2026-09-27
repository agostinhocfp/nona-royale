// Assets/Tests/EditMode/Unity/DecoTableTests.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The Deco skin's yard tables (board skin BS4): chairs face the table,
    /// and the centre emblem, the chairs and the felt's gilt line never crowd
    /// one another.
    /// </summary>
    [TestFixture]
    public class DecoTableTests
    {
        [Test]
        public void ChairRotation_TurnsTheSeatToFaceTheTable_FromEverySeat()
        {
            for (int seat = 0; seat < 8; seat++)
            {
                float seatAngle = BoardLayout.SeatAngle(seat);
                float turn = BoardView.DecoChairRotation(seatAngle) * Mathf.Deg2Rad;

                // The chair art faces down: its seat looks along (0, -1). Turned, that must point at the centre.
                var facing = new Vector2(Mathf.Sin(turn), -Mathf.Cos(turn));
                var toCentre = -new Vector2(Mathf.Cos(seatAngle * Mathf.Deg2Rad), Mathf.Sin(seatAngle * Mathf.Deg2Rad));

                Assert.AreEqual(toCentre.x, facing.x, 1e-4f, $"seat {seat}");
                Assert.AreEqual(toCentre.y, facing.y, 1e-4f, $"seat {seat}");
            }
        }

        [Test]
        public void EmblemRing_ClearsEveryChair_AndChairsStayInsideTheFeltLine()
        {
            var layout = new BoardLayout(BoardProfile.Standard, 1f);
            float radius = layout.TableDiameter * 0.5f;
            float seatRadius = radius * BoardLayout.SeatRadius;
            float chairHalf = BoardView.DecoChairSize * 0.5f;

            Assert.Less(BoardView.DecoTableEmblemRing * 0.5f, seatRadius - chairHalf);
            Assert.Less(BoardView.DecoTableEmblemSize, BoardView.DecoTableEmblemRing);
            Assert.Less(seatRadius + chairHalf, radius * DecoBoardArt.FeltLine);
            Assert.Less(DecoBoardArt.FeltLine, DecoBoardArt.RimInner);
        }

        [Test]
        public void Chairs_NeverOverlap_WithAFullSquadOfEight()
        {
            var layout = new BoardLayout(BoardProfile.Standard, 1f);
            for (int a = 0; a < 8; a++)
                for (int b = a + 1; b < 8; b++)
                {
                    float gap = Vector3.Distance(layout.YardSeat(PlayerColor.Red, a), layout.YardSeat(PlayerColor.Red, b));
                    Assert.Greater(gap, BoardView.DecoChairSize * 0.9f, $"seats {a} and {b}");
                }
        }
    }
}
