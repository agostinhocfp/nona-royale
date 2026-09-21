// Assets/Tests/EditMode/Unity/RigPieceTests.cs
using System.Linq;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// A rigged figure on a piece (OPERATOR_LOOKBOOK.md, LB5b): where it sits
    /// in the fallback order, its parts seated and standing, and that the
    /// pin and bar still draw over it.
    /// </summary>
    [TestFixture]
    public class RigPieceTests
    {
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            OperatorArtLibrary.ClearCache();
            OperatorLookBook.ClearCache();
            OperatorRigArt.ClearCache();
            OperatorLookBook.Enabled = true;
            OperatorRigArt.Enabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            OperatorArtLibrary.ClearCache();
            OperatorLookBook.ClearCache();
            OperatorRigArt.ClearCache();
            OperatorRigArt.Enabled = true;
        }

        [Test]
        public void Bouncer_IsDrawnAsARig_SeatedAndStanding()
        {
            Assume.That(OperatorArtLibrary.Rendered("Bouncer", FigurePose.Standing), Is.Null,
                "Bouncer has a real render now; point this test at a rigged operator without one.");

            var piece = Bind("Bouncer");

            // Seated: the rig, cut at the table; no seat disc, no body sprite.
            Assert.IsTrue(piece.Seated);
            Assert.IsTrue(piece.ShowsRig);
            Assert.IsTrue(piece.ShowsRenderedArt);
            Assert.IsNull(Child(piece, "body").sprite);
            Assert.IsFalse(Child(piece, "seat_base").enabled);
            Assert.IsTrue(Part(piece, BouncerRig.HeadPart).enabled);
            Assert.IsFalse(Part(piece, BouncerRig.Gauntlet).enabled, "he takes the gauntlet off at the table");
            Assert.IsTrue(Part(piece, BouncerRig.GauntletOnTable).enabled, "and sets it down on it");

            // Standing: every part that is not a prop, over the seat disc and ring.
            piece.Rise(Vector3.zero);

            Assert.IsFalse(piece.Seated);
            Assert.IsTrue(piece.ShowsRig);
            Assert.IsTrue(Child(piece, "seat_base").enabled);
            Assert.IsTrue(Child(piece, "seat_ring").enabled);
            Assert.IsTrue(Part(piece, "shin.near").enabled);
            Assert.IsTrue(Part(piece, BouncerRig.Gauntlet).enabled);
            Assert.IsFalse(Part(piece, BouncerRig.GauntletOnTable).enabled, "the table prop is off the table");
        }

        [Test]
        public void ThePinAndTheBar_DrawOverEveryPart()
        {
            var piece = Bind("Bouncer");
            piece.Rise(Vector3.zero);

            int highest = Rig(piece).GetComponentsInChildren<SpriteRenderer>(true).Max(r => r.sortingOrder);

            Assert.Greater(Child(piece, "pin").sortingOrder, highest);
            Assert.Greater(Child(piece, "health_back").sortingOrder, highest);
            Assert.Greater(Child(piece, "health_fill").sortingOrder, Child(piece, "health_back").sortingOrder);
        }

        [Test]
        public void ARealRender_BeatsTheRig()
        {
            OperatorArtLibrary.Register("Bouncer", FigurePose.Standing, Readable(8, 20));

            var piece = Bind("Bouncer");
            Assert.IsTrue(piece.ShowsRig, "no seated render, so the rig sits");

            piece.Rise(Vector3.zero);
            Assert.IsFalse(piece.ShowsRig);
            Assert.IsNotNull(Child(piece, "body").sprite);
        }

        [Test]
        public void RigsOff_DrawTheLookBookFigure()
        {
            OperatorRigArt.Enabled = false;

            var piece = Bind("Bouncer");

            Assert.IsFalse(piece.ShowsRig);
            Assert.IsTrue(piece.ShowsRenderedArt);
            Assert.IsNotNull(Child(piece, "body").sprite);
        }

        [Test]
        public void TheScreenBounds_AreTheParts()
        {
            var camera = new GameObject("camera").AddComponent<Camera>();
            camera.transform.SetParent(_host != null ? _host.transform : null, false);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            try
            {
                var piece = Bind("Bouncer");
                piece.Rise(Vector3.zero);

                Assert.IsTrue(piece.TryScreenBounds(camera, out var rect));
                Assert.Greater(rect.height, rect.width, "a standing figure is taller than it is wide");
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void ACast_TurnsTheFigureToItsAim()
        {
            var piece = Bind("Bouncer");
            piece.Rise(Vector3.zero);
            Assume.That(piece.FacesLeft, Is.False);

            piece.Cast(new Vector3(-3f, 1f, 0f), 0.5f);
            Assert.IsTrue(piece.FacesLeft);

            piece.Recoil(new Vector3(3f, 0f, 0f));
            Assert.IsFalse(piece.FacesLeft, "a hit turns it to the striker");
        }

        [Test]
        public void AKnockout_FoldsBeforeItShatters_AndReappearingEndsIt()
        {
            var piece = Bind("Bouncer");
            piece.Rise(Vector3.zero);

            piece.Shatter();
            Assert.IsTrue(piece.IsCollapsing);
            Assert.IsTrue(piece.IsKnockingOut, "the queue waits for the fall");
            Assert.IsFalse(piece.IsHidden, "still standing, going down");

            piece.Reappear(Vector3.one);
            Assert.IsFalse(piece.IsCollapsing);
            Assert.IsFalse(piece.IsKnockingOut);
            Assert.IsFalse(piece.IsHidden);
        }

        [Test]
        public void SeatedOrOff_ThereIsNoFold()
        {
            var piece = Bind("Bouncer");
            Assume.That(piece.Seated, Is.True);

            piece.Shatter();
            Assert.IsFalse(piece.IsCollapsing);
            Assert.IsTrue(piece.IsHidden);
        }

        private OperatorPiece Bind(string name)
        {
            _host = new GameObject("host");
            var go = new GameObject("piece");
            go.transform.SetParent(_host.transform, false);
            var piece = go.AddComponent<OperatorPiece>();
            piece.Bind(new OperatorState(1, name, PlayerColor.Red, 10, 1.0), 1f, 1f, new MotionSettings { ReducedMotion = true });
            return piece;
        }

        private static Transform Rig(OperatorPiece piece)
        {
            foreach (var t in piece.GetComponentsInChildren<Transform>(true))
                if (t.name == "rig") return t;

            Assert.Fail("No rig.");
            return null;
        }

        /// <summary>A part's renderer: the "art" child of the joint named after the part.</summary>
        private static SpriteRenderer Part(OperatorPiece piece, string part)
        {
            var joint = Rig(piece).Find(part);
            Assert.IsNotNull(joint, $"No part '{part}'.");
            return joint.Find("art").GetComponent<SpriteRenderer>();
        }

        private static SpriteRenderer Child(OperatorPiece piece, string name)
        {
            foreach (var renderer in piece.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.name == name) return renderer;

            Assert.Fail($"No child '{name}'.");
            return null;
        }

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
