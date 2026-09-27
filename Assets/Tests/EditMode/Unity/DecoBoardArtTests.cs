// Assets/Tests/EditMode/Unity/DecoBoardArtTests.cs
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>The Deco skin's procedural cell art (board skin BS2).</summary>
    [TestFixture]
    public class DecoBoardArtTests
    {
        [Test]
        public void FaceTint_DividesOutTheFaceValue_SoTheFaceReadsAsAsked()
        {
            var face = new Color(0.36f, 0.18f, 0.072f, 0.5f);
            var tint = DecoBoardArt.FaceTint(face);

            Assert.AreEqual(face.r, tint.r * DecoBoardArt.FaceValue, 1e-5f);
            Assert.AreEqual(face.g, tint.g * DecoBoardArt.FaceValue, 1e-5f);
            Assert.AreEqual(face.b, tint.b * DecoBoardArt.FaceValue, 1e-5f);
            Assert.AreEqual(0.5f, tint.a);
        }

        [Test]
        public void FaceTint_ClampsAFaceBrighterThanTheSpriteCanShow()
        {
            var tint = DecoBoardArt.FaceTint(Color.white);
            Assert.AreEqual(1f, tint.r);
            Assert.AreEqual(1f, tint.g);
            Assert.AreEqual(1f, tint.b);
        }

        [Test]
        public void EverySprite_IsOneUnitAcross_AndMipmapped()
        {
            var sprites = new[]
            {
                (DecoBoardArt.TileBody, DecoBoardArt.Size), (DecoBoardArt.TrimGilt, DecoBoardArt.Size),
                (DecoBoardArt.TrimCyan, DecoBoardArt.Size), (DecoBoardArt.CellRing, DecoBoardArt.Size),
                (DecoBoardArt.Compass, DecoBoardArt.Size), (DecoBoardArt.TileShadow, 64),
                (DecoBoardArt.Medallion, DecoBoardArt.CentreSize), (DecoBoardArt.MedallionEmblem, DecoBoardArt.CentreSize),
                (DecoBoardArt.CornerWedge, DecoBoardArt.CentreSize), (DecoBoardArt.TableRim, DecoBoardArt.TableSize),
            };

            foreach (var (sprite, size) in sprites)
            {
                Assert.IsNotNull(sprite);
                Assert.AreEqual(1f, sprite.bounds.size.x, 1e-4f, sprite.name);
                Assert.AreEqual(size, sprite.texture.width, sprite.name);
                Assert.Greater(sprite.texture.mipmapCount, 1, sprite.name);
            }
        }
    }
}
