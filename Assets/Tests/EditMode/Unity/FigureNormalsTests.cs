// Assets/Tests/EditMode/Unity/FigureNormalsTests.cs
using System;
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The cel-facet normal maps (OPERATOR_LOOKBOOK.md, LB5d): the drawing's
    /// light planes face the key, its shade planes face away, metal turns
    /// further, the edge turns outward, and facing left reflects it all.
    /// </summary>
    [TestFixture]
    public class FigureNormalsTests
    {
        private static readonly FigureColour Ink = FigureColour.Hex("1C0E12");
        private static readonly FigureColour Cloth = FigureColour.Hex("303030");
        private static readonly FigureColour Brass = FigureColour.Hex("7C5A1E");
        private static readonly FigureColour Shade = FigureColour.Hex("7E7A94");
        private static readonly FigureColour Sheen = FigureColour.Hex("3A3548");

        /// <summary>x −1..1, y −1..1 at 32 texels per unit: 64×64.</summary>
        private static readonly FigureCanvas Canvas = new FigureCanvas(-1f, -1f, 64, 64, 32f, 1);

        /// <summary>A 1.2-unit square: the left half lit, the right half shaded, a brass disc in the lit half.</summary>
        private static FigureDrawing Drawing() =>
            new FigureDrawing(FigureShape.Rect(-0.6f, -0.6f, 0.6f, 0.6f), Cloth, Ink, 0.03f)
                .Block(FigureShape.Circle(-0.3f, -0.3f, 0.12f), Brass)
                .Shade(FigureShape.Rect(0f, -0.6f, 0.6f, 0.6f), Shade)
                .Light(FigureShape.Rect(-0.6f, -0.6f, 0f, 0.6f), Sheen)
                .Rim(0.04f, FigureColour.Hex("8C9DB0"), -10f);

        private static void At(byte[] pixels, float x, float y, out float nx, out float ny, out float nz)
        {
            int px = (int)((x - Canvas.Left) * Canvas.PixelsPerUnit);
            int py = (int)((y - Canvas.Bottom) * Canvas.PixelsPerUnit);
            FigureNormals.Decode(pixels, (py * Canvas.Width + px) * 4, out nx, out ny, out nz);
        }

        [Test]
        public void Outside_FacesTheCamera_AndEveryTexelIsAUnitNormal()
        {
            var pixels = FigureNormals.Render(Drawing(), Canvas, new[] { Brass }, false);

            Assert.AreEqual(Canvas.Width * Canvas.Height * 4, pixels.Length);

            At(pixels, -0.9f, 0.9f, out float x, out float y, out float z);
            Assert.AreEqual(0f, x, 0.01f);
            Assert.AreEqual(0f, y, 0.01f);
            Assert.AreEqual(1f, z, 0.01f);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                FigureNormals.Decode(pixels, i, out float a, out float b, out float c);
                Assert.AreEqual(1f, Math.Sqrt(a * a + b * b + c * c), 0.02f, $"texel {i / 4}");
                Assert.AreEqual(255, pixels[i + 3], "alpha is opaque, for the RG-or-AG unpack");
            }
        }

        [Test]
        public void TheLitPlane_FacesTheKey_AndTheShadedPlaneFacesAway()
        {
            var pixels = FigureNormals.Render(Drawing(), Canvas, null, false);

            At(pixels, -0.2f, 0.2f, out float lx, out float ly, out _);
            Assert.Less(lx, -0.1f, "lit: toward the figure's back, the key's side");
            Assert.Greater(ly, 0.1f, "lit: up, toward the key");

            At(pixels, 0.2f, 0.2f, out float sx, out float sy, out _);
            Assert.Greater(sx, 0.1f, "shaded: toward the figure's front, away from the key");
            Assert.Less(sy, 0f, "shaded: down");
        }

        [Test]
        public void FacingLeft_ReflectsTheFacets()
        {
            var right = FigureNormals.Render(Drawing(), Canvas, null, false);
            var left = FigureNormals.Render(Drawing().Mirrored(), Canvas, null, true);

            // The lit plane is now on the right of the sprite, and faces right.
            At(right, -0.2f, 0.2f, out float rx, out float ry, out _);
            At(left, 0.2f, 0.2f, out float lx, out float ly, out _);
            Assert.AreEqual(-rx, lx, 0.02f);
            Assert.AreEqual(ry, ly, 0.02f);
        }

        [Test]
        public void Metal_TurnsFurtherThanCloth()
        {
            var cloth = FigureNormals.Render(Drawing(), Canvas, null, false);
            var metal = FigureNormals.Render(Drawing(), Canvas, new[] { Brass }, false);

            At(cloth, -0.3f, -0.3f, out float cx, out _, out float cz);
            At(metal, -0.3f, -0.3f, out float mx, out _, out float mz);
            Assert.Less(mx, cx - 0.05f, "the brass disc turns further toward the key");
            Assert.Less(mz, cz, "and so faces the camera less");
        }

        [Test]
        public void TheEdge_TurnsOutward_AndTheMiddleDoesNot()
        {
            var plain = new FigureDrawing(FigureShape.Rect(-0.6f, -0.6f, 0.6f, 0.6f), Cloth, Ink, 0.03f)
                .Rim(0.04f, FigureColour.Hex("8C9DB0"), -10f);
            var pixels = FigureNormals.Render(plain, Canvas, null, false);

            At(pixels, -0.58f, 0f, out float lx, out _, out _);
            At(pixels, 0.58f, 0f, out float rx, out _, out _);
            At(pixels, 0f, 0.58f, out _, out float ty, out _);
            At(pixels, 0f, 0f, out float mx, out float my, out float mz);

            Assert.Less(lx, -0.3f, "the left edge faces left");
            Assert.Greater(rx, 0.3f, "the right edge faces right");
            Assert.Greater(ty, 0.3f, "the top edge faces up");
            Assert.AreEqual(0f, mx, 0.01f);
            Assert.AreEqual(0f, my, 0.01f);
            Assert.AreEqual(1f, mz, 0.01f);
        }

        [Test]
        public void EveryRigPart_HasNormals_TexelForTexel()
        {
            foreach (var rig in RigRoster.All)
            {
                var images = RigImages.Build(rig, OperatorLookBook.Palette);
                foreach (var facing in new[] { images.Right, images.Left })
                foreach (var part in facing.Parts)
                {
                    Assert.IsNotNull(part.StandingNormals, $"{rig.Name} {part.Part.Name}");
                    Assert.AreEqual(part.Standing.Pixels.Length, part.StandingNormals.Length, $"{rig.Name} {part.Part.Name}");

                    if (part.Seated == null) continue;
                    Assert.IsNotNull(part.SeatedNormals, $"{rig.Name} {part.Part.Name} seated");
                    Assert.AreEqual(part.Seated.Pixels.Length, part.SeatedNormals.Length, $"{rig.Name} {part.Part.Name} seated");
                    if (ReferenceEquals(part.Seated, part.Standing))
                        Assert.AreSame(part.StandingNormals, part.SeatedNormals, "uncut, the seated part shares its normals");
                }
            }
        }
    }
}
