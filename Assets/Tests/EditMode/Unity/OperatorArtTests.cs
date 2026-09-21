// Assets/Tests/EditMode/Unity/OperatorArtTests.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The art hookup inside the editor (ART_HOOKUP.md, ART1): a render
    /// registered for one pose replaces that pose only, and a piece without
    /// art draws exactly as before.
    /// </summary>
    [TestFixture]
    public class OperatorArtTests
    {
        private const string Name = "Testoperator";
        private GameObject _host;

        [SetUp]
        public void SetUp() => OperatorArtLibrary.ClearCache();

        [TearDown]
        public void TearDown()
        {
            OperatorArtLibrary.ClearCache();
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [Test]
        public void ProceduralFrames_MatchBoardArt()
        {
            Assert.AreEqual(BoardArt.PawnPin.y, FigureLayout.Pawn.PinY, 1e-5f);
            Assert.AreEqual(BoardArt.PawnHead.y, FigureLayout.Pawn.HeadY, 1e-5f);
            Assert.AreEqual(BoardArt.BustPin.y, FigureLayout.Bust.PinY, 1e-5f);
        }

        [Test]
        public void MissingArt_IsNull_AndRemembered()
        {
            Assert.IsNull(OperatorArtLibrary.Figure("Nobody", FigurePose.Standing));
            Assert.IsNull(OperatorArtLibrary.Figure("Nobody", FigurePose.Standing));
            Assert.IsNull(OperatorArtLibrary.Portrait("Nobody"));
        }

        [Test]
        public void Measure_FindsTheOpaqueRows_AndBuildsAWhiteSilhouette()
        {
            // 8×20, pivot at the bottom, opaque from row 3 to row 16.
            var sprite = Figure(8, 20, 3, 16);
            OperatorArtLibrary.Register(Name, FigurePose.Standing, sprite);

            var art = OperatorArtLibrary.Figure(Name, FigurePose.Standing);
            Assert.IsNotNull(art);
            Assert.AreEqual(3f / 10f, art.OpaqueBottom, 1e-5f);   // ppu 10
            Assert.AreEqual(17f / 10f, art.OpaqueTop, 1e-5f);
            Assert.IsNotNull(art.Silhouette);

            // The mask is uploaded and released from CPU memory, so it is read
            // back through the GPU rather than with GetPixel.
            var mask = ReadBack(art.Silhouette.texture);
            var inside = mask.GetPixel(4, 10);
            Assert.AreEqual(1f, inside.r, 1e-2f);
            Assert.AreEqual(1f, inside.a, 1e-2f);
            Assert.AreEqual(0f, mask.GetPixel(4, 1).a, 1e-2f);
            Object.DestroyImmediate(mask);
        }

        [Test]
        public void StandingArt_ReplacesThePawn_AndSeatedStillDrawsTheBust()
        {
            OperatorArtLibrary.Register(Name, FigurePose.Standing, Figure(8, 20, 0, 19));
            var op = new OperatorState(1, Name, PlayerColor.Red, 8, 1.0);

            var piece = Bind(op);

            // In the yard: no seated render, so the procedural bust.
            Assert.IsTrue(piece.Seated);
            Assert.IsFalse(piece.ShowsRenderedArt);
            Assert.AreEqual(BoardArt.Bust, Child(piece, "body").sprite);
            Assert.IsTrue(Child(piece, "outline").enabled);
            Assert.IsFalse(Child(piece, "seat_base").enabled);

            // Deployed: the render, untinted, on a seat disc, with no outline.
            piece.Rise(Vector3.zero);
            Assert.IsFalse(piece.Seated);
            Assert.IsTrue(piece.ShowsRenderedArt);

            var body = Child(piece, "body");
            Assert.AreNotEqual(BoardArt.Pawn, body.sprite);
            Assert.AreEqual(Color.white.r, body.color.r, 1e-3f);
            Assert.AreEqual(Color.white.b, body.color.b, 1e-3f);
            Assert.IsFalse(Child(piece, "outline").enabled);
            Assert.IsTrue(Child(piece, "seat_base").enabled);
            Assert.IsTrue(Child(piece, "seat_ring").enabled);
        }

        [Test]
        public void ADestroyedCachedSprite_IsBuiltAgain()
        {
            // What the editor does to script-made textures on a scene change.
            var pawn = BoardArt.Pawn;
            Object.DestroyImmediate(pawn.texture);
            Object.DestroyImmediate(pawn);

            Assert.IsTrue(BoardArt.Pawn != null);
            Assert.IsTrue(Primitives.Disc != null);
        }

        [Test]
        public void NoArt_DrawsTheProceduralFigures_AsBefore()
        {
            var op = new OperatorState(1, "Nobody", PlayerColor.Blue, 8, 1.0);
            var piece = Bind(op);
            piece.Rise(Vector3.zero);

            var body = Child(piece, "body");
            Assert.IsFalse(piece.ShowsRenderedArt);
            Assert.AreEqual(BoardArt.Pawn, body.sprite);
            Assert.AreEqual(Vector3.zero, body.transform.localPosition);
            Assert.AreEqual(Vector3.one, body.transform.localScale);
            Assert.IsTrue(Child(piece, "outline").enabled);
            Assert.IsFalse(Child(piece, "seat_base").enabled);
            Assert.AreEqual((Vector2)BoardArt.PawnPin, (Vector2)Child(piece, "pin").transform.localPosition);
            Assert.AreEqual(FigureLayout.Pawn.BarY, Child(piece, "health_back").transform.localPosition.y, 1e-5f);
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

        /// <summary>A readable copy of a texture that may not be readable, via a render texture.</summary>
        private static Texture2D ReadBack(Texture source)
        {
            var target = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;

            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;

                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                copy.Apply();
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>A readable sprite, opaque between two rows, pivoted at the bottom, 10 pixels per unit.</summary>
        private static Sprite Figure(int width, int height, int fromRow, int toRow)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = y >= fromRow && y <= toRow
                    ? new Color32(200, 120, 60, 255)
                    : new Color32(0, 0, 0, 0);

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 10f);
        }
    }
}