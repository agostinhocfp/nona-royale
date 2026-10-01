// Assets/Tests/EditMode/Unity/ChipPieceTests.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// Operators drawn as chips (2026-09-29): the portrait lookup, the
    /// device anchors, and the piece switching between figures and chips.
    /// </summary>
    [TestFixture]
    public class ChipPieceTests
    {
        /// <summary>An operator with no chip portrait and no anchor: no one in the cast, so the art pass never paints it.</summary>
        private const string Unpainted = "Nobody";

        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            ChipArtLibrary.ClearCache();
            OperatorArtLibrary.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            ChipArtLibrary.ClearCache();
            OperatorArtLibrary.ClearCache();
        }

        [Test]
        public void PathFor_UsesTheSourceFileNames_UnderArtChips()
        {
            Assert.AreEqual("Art/Chips/luka_chip_unlit", ChipArtLibrary.PathFor("Luka", lit: false));
            Assert.AreEqual("Art/Chips/luka_chip_lit", ChipArtLibrary.PathFor("Luka", lit: true));
            Assert.AreEqual("Art/Chips/revu_chip_unlit", ChipArtLibrary.PathFor("Revú", lit: false));
        }

        [Test]
        public void FaceUnits_PutsTheImageCentreAtZero_AndFlipsY()
        {
            Assert.AreEqual(Vector2.zero, ChipArtLibrary.FaceUnits(new Vector2(0.5f, 0.5f)));

            var upperRight = ChipArtLibrary.FaceUnits(new Vector2(0.84f, 0.33f));
            Assert.AreEqual(0.34f, upperRight.x, 1e-5f);
            Assert.AreEqual(0.17f, upperRight.y, 1e-5f);
        }

        [Test]
        public void AllTwelveChips_HaveAMeasuredAnchor_InsideTheFace()
        {
            foreach (var name in new[] { "Luka", "Bouncer", "Syla", "Kurbyn", "Javi", "Sanity", "Mimi", "Fortuna", "Revú", "Nuetu", "Lethe", "Kian" })
            {
                Assert.IsTrue(ChipArtLibrary.HasAnchor(name), name);

                // Inside the circle the importer keeps, or the glow lands off the face.
                Assert.Less(ChipArtLibrary.DeviceFor(name).magnitude, ChipArtLibrary.MaskRadius, name);
            }
        }

        [Test]
        public void AnOperatorWithNoAnchor_FlaresAtTheCentre()
        {
            // A name no operator carries, so the test outlives the art pass.
            Assert.IsFalse(ChipArtLibrary.HasAnchor(Unpainted));
            Assert.AreEqual(Vector2.zero, ChipArtLibrary.DeviceFor(Unpainted));
        }

        [Test]
        public void TheFaceSitsUnderTheRing()
        {
            Assert.Less(ChipSprites.FaceRadius, ChipSprites.RingOuter);
            Assert.Less(ChipSprites.RingOuter, 1f);
        }

        [Test]
        public void AChipPiece_DrawsAChip_WithNoPin()
        {
            var piece = Bind("Mimi", PieceStyle.Chips);

            Assert.IsTrue(piece.ShowsChip);
            Assert.IsFalse(Child(piece, "pin").enabled);
            Assert.IsNotNull(Child(piece, "body").transform.Find("chip"));
        }

        [Test]
        public void ApplyStyle_SwitchesBetweenFiguresAndChips()
        {
            var piece = Bind("Mimi", PieceStyle.Figures);
            Assert.IsFalse(piece.ShowsChip);
            Assert.IsTrue(Child(piece, "pin").enabled);

            piece.ApplyStyle(PieceStyle.Chips);
            Assert.IsTrue(piece.ShowsChip);
            Assert.AreEqual(PieceStyle.Chips, piece.Style);
            Assert.IsFalse(Child(piece, "pin").enabled);

            piece.ApplyStyle(PieceStyle.Figures);
            Assert.IsFalse(piece.ShowsChip);
            Assert.IsTrue(Child(piece, "pin").enabled);
        }

        [Test]
        public void AChipWithNoPortrait_ShowsItsEmblem()
        {
            var piece = Bind(Unpainted, PieceStyle.Chips);

            Assert.IsTrue(Child(piece, "chip_emblem").enabled);
            Assert.AreSame(PieceShape.For(Unpainted), Child(piece, "chip_emblem").sprite);
        }

        private OperatorPiece Bind(string name, PieceStyle style)
        {
            _host = new GameObject("host");
            var go = new GameObject("piece");
            go.transform.SetParent(_host.transform, false);
            var piece = go.AddComponent<OperatorPiece>();
            piece.Bind(new OperatorState(1, name, PlayerColor.Red, 8, 1.0), 1f, 1f,
                new MotionSettings { ReducedMotion = true }, style);
            return piece;
        }

        private static SpriteRenderer Child(OperatorPiece piece, string name)
        {
            foreach (var renderer in piece.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.name == name) return renderer;

            Assert.Fail($"No child '{name}'.");
            return null;
        }
    }
}
