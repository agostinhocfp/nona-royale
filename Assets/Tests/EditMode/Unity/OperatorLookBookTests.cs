// Assets/Tests/EditMode/Unity/OperatorLookBookTests.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The look book inside the editor (OPERATOR_LOOKBOOK.md, LB0): where it
    /// sits in the fallback order, and that a piece draws its figure the way it
    /// draws a render (LB1 rides the ART1 path).
    /// </summary>
    [TestFixture]
    public class OperatorLookBookTests
    {
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            OperatorArtLibrary.ClearCache();
            OperatorLookBook.ClearCache();
            OperatorLookBook.Enabled = true;

            // Bouncer has a rig since LB5b; these tests are about the look book under it.
            OperatorRigArt.Enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            OperatorArtLibrary.ClearCache();
            OperatorLookBook.ClearCache();
            OperatorLookBook.Enabled = true;
            OperatorRigArt.Enabled = true;
        }

        [Test]
        public void AnOperatorWithNoRender_GetsItsLookBookFigure()
        {
            // Bouncer has real renders since c14a2b4 (2026-09-23); hide the
            // standing one so the look book underneath is what gets drawn.
            OperatorArtLibrary.Register("Bouncer", FigurePose.Standing, null);
            Assert.That(OperatorArtLibrary.Rendered("Bouncer", FigurePose.Standing), Is.Null, "precondition");

            var art = OperatorArtLibrary.Figure("Bouncer", FigurePose.Standing);

            Assert.IsNotNull(art);
            Assert.IsNotNull(art.Silhouette, "the hit flash needs the white silhouette");
            Assert.AreEqual(0f, art.OpaqueBottom, 0.05f, "the feet sit on the sprite's pivot");
            Assert.Greater(art.OpaqueTop, 2.5f);
        }

        [Test]
        public void ARealRender_AlwaysWins()
        {
            var sprite = Readable(8, 20);
            OperatorArtLibrary.Register("Bouncer", FigurePose.Standing, sprite);

            Assert.AreSame(sprite, OperatorArtLibrary.Figure("Bouncer", FigurePose.Standing).Sprite);
        }

        [Test]
        public void Disabled_FallsBackToTheProceduralFigure()
        {
            OperatorLookBook.Enabled = false;
            Assert.IsNull(OperatorArtLibrary.Figure("Nuetu", FigurePose.Standing));
        }

        [Test]
        public void NoRecipe_IsNull()
        {
            Assert.IsFalse(OperatorLookBook.Has("Nobody"));
            Assert.IsNull(OperatorLookBook.Figure("Nobody", FigurePose.Standing));
            Assert.IsNull(OperatorArtLibrary.Figure("Nobody", FigurePose.Standing));
        }

        [Test]
        public void Figures_AreCached_AndTheSeatedCutIsShorter()
        {
            var standing = OperatorLookBook.Figure("Nuetu", FigurePose.Standing);
            var seated = OperatorLookBook.Figure("Nuetu", FigurePose.Seated);

            Assert.AreSame(standing, OperatorLookBook.Figure("Nuetu", FigurePose.Standing));
            Assert.Less(seated.OpaqueTop - seated.OpaqueBottom, standing.OpaqueTop - standing.OpaqueBottom);
            Assert.AreEqual(standing.OpaqueTop, seated.OpaqueTop, 0.02f, "the cut takes the legs, not the head");
        }

        [Test]
        public void Prewarm_GivesTheSameFigures_AsBuildingOnDemand()
        {
            OperatorLookBook.Prewarm(new[] { "Bouncer", "Nuetu", "Nobody" });

            var warmed = OperatorLookBook.Figure("Bouncer", FigurePose.Seated);
            Assert.IsNotNull(warmed);
            Assert.IsNotNull(OperatorLookBook.Figure("Nuetu", FigurePose.Standing));
            Assert.IsNull(OperatorLookBook.Figure("Nobody", FigurePose.Standing));
        }

        [Test]
        public void APiece_DrawsTheLookBookFigure_LikeARender()
        {
            var piece = Bind(new OperatorState(1, "Bouncer", PlayerColor.Red, 10, 1.0));

            // Seated at its own table: the look-book bust, no seat disc.
            Assert.IsTrue(piece.Seated);
            Assert.IsTrue(piece.ShowsRenderedArt);
            Assert.AreNotEqual(BoardArt.Bust, Child(piece, "body").sprite);
            Assert.IsFalse(Child(piece, "seat_base").enabled);

            // Standing: untinted, the seat on the disc and ring, no pawn outline.
            piece.Rise(Vector3.zero);

            var body = Child(piece, "body");
            Assert.IsTrue(piece.ShowsRenderedArt);
            Assert.AreEqual(1f, body.color.r, 1e-3f);
            Assert.AreEqual(1f, body.color.g, 1e-3f);
            Assert.AreEqual(1f, body.color.b, 1e-3f);
            Assert.IsFalse(Child(piece, "outline").enabled);
            Assert.IsTrue(Child(piece, "seat_base").enabled);
            Assert.IsTrue(Child(piece, "seat_ring").enabled);
        }

        private OperatorPiece Bind(OperatorState op)
        {
            // A parent, so the rise's ring effect is destroyed with the test.
            _host = new GameObject("host");
            var go = new GameObject("piece");
            go.transform.SetParent(_host.transform, false);
            var piece = go.AddComponent<OperatorPiece>();
            piece.Bind(op, 1f, 1f, new MotionSettings { ReducedMotion = true });
            return piece;
        }

        private static SpriteRenderer Child(OperatorPiece piece, string name)
        {
            foreach (var renderer in piece.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.name == name) return renderer;

            Assert.Fail($"No child '{name}'.");
            return null;
        }

        /// <summary>A readable, fully opaque sprite pivoted at the bottom, 10 pixels per unit.</summary>
        private static Sprite Readable(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), 10f);
        }
    }
}
