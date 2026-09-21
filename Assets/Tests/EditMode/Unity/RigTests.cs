// Assets/Tests/EditMode/Unity/RigTests.cs
using System.Collections.Generic;
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The rig core (OPERATOR_LOOKBOOK.md, LB5a): the skeleton maths, the
    /// shared rules over every rig in <see cref="RigRoster"/>, and facing.
    /// </summary>
    [TestFixture]
    public class RigTests
    {
        private readonly Dictionary<string, List<RigPartImage>> _parts = new Dictionary<string, List<RigPartImage>>();

        [OneTimeSetUp]
        public void RenderAll()
        {
            foreach (var rig in RigRoster.All)
                _parts[rig.Name] = RigComposer.RenderParts(rig, OperatorLookBook.Palette, 96f, 4);
        }

        private static IEnumerable<string> Names()
        {
            foreach (var rig in RigRoster.All) yield return rig.Name;
        }

        // ── The maths ───────────────────────────────────────────────────

        [Test]
        public void AChildTurnsWithItsParent()
        {
            var skeleton = new RigSkeleton()
                .Bone("root", null, 0f, 0f)
                .Bone("arm", "root", 1f, 0f);

            var world = skeleton.Evaluate(new RigPose("p").Turn("root", 90f));

            Assert.AreEqual(0f, world["arm"].PivotX, 1e-5f);
            Assert.AreEqual(1f, world["arm"].PivotY, 1e-5f);
            Assert.AreEqual(90f, world["arm"].Degrees, 1e-5f);
        }

        [Test]
        public void Untransform_UndoesTransform()
        {
            var placed = new BoneWorld(0.3f, 1.2f, 37f, 1.05f);
            RigSkeleton.Transform(placed, 0.1f, 1.0f, 0.6f, 0.4f, out float wx, out float wy);
            RigSkeleton.Untransform(placed, 0.1f, 1.0f, wx, wy, out float x, out float y);

            Assert.AreEqual(0.6f, x, 1e-4f);
            Assert.AreEqual(0.4f, y, 1e-4f);
        }

        [Test]
        public void ABoneBeforeItsParent_IsRefused()
        {
            Assert.Throws<System.ArgumentException>(() => new RigSkeleton().Bone("root", null, 0f, 0f).Bone("arm", "missing", 0f, 0f));
        }

        [Test]
        public void Lerp_BlendsTurns_AndSwitchesPartsHalfway()
        {
            var a = new RigPose("a").Turn("x", 0f);
            var b = new RigPose("b").Turn("x", 40f).Hide("gauntlet");

            Assert.AreEqual(20f, RigPose.Lerp(a, b, 0.5f).Get("x").Degrees, 1e-5f);
            Assert.IsFalse(RigPose.Lerp(a, b, 0.4f).Hides("gauntlet"));
            Assert.IsTrue(RigPose.Lerp(a, b, 0.6f).Hides("gauntlet"));
        }

        // ── Every rig ───────────────────────────────────────────────────

        [TestCaseSource(nameof(Names))]
        public void EveryPart_RidesABoneThatExists(string name)
        {
            var rig = RigRoster.Find(name);
            foreach (var part in rig.Parts(OperatorLookBook.Palette))
                Assert.IsTrue(rig.Skeleton.Has(part.Bone), $"{part.Name} rides '{part.Bone}'");
        }

        [TestCaseSource(nameof(Names))]
        public void EveryStripPose_Exists(string name)
        {
            var rig = RigRoster.Find(name);
            foreach (var pose in RigPoseNames.Strip) Assert.IsTrue(rig.HasPose(pose), pose);
        }

        [TestCaseSource(nameof(Names))]
        public void TheFeetStayPlanted_InIdleAndCast(string name)
        {
            Assert.LessOrEqual(RigChecks.FootDrift(RigRoster.Find(name)), RigChecks.FootTolerance);
        }

        [TestCaseSource(nameof(Names))]
        public void NoPose_OpensASeamAtAJoint(string name)
        {
            var seams = RigChecks.Seams(RigRoster.Find(name), _parts[name], RigComposer.PoseCanvas);
            Assert.IsEmpty(seams, string.Join(", ", seams));
        }

        [TestCaseSource(nameof(Names))]
        public void NoPose_CarriesCyanAtRest(string name)
        {
            Assert.AreEqual(0, RigChecks.CyanAtRest(RigRoster.Find(name), _parts[name], RigComposer.PoseCanvas));
        }

        [TestCaseSource(nameof(Names))]
        public void TheCast_BringsTheTell(string name)
        {
            var rig = RigRoster.Find(name);
            var powered = RigComposer.RenderParts(rig, OperatorLookBook.Palette, 96f, 4, powered: true);
            var image = RigComposer.Compose(rig, powered, rig.Pose(RigPoseNames.Cast), RigComposer.PoseCanvas);

            Assert.Greater(SilhouetteMetrics.Count(image, SilhouetteMetrics.IsCyan), 0);
        }

        [TestCaseSource(nameof(Names))]
        public void NoPose_IsClippedByThePoseCanvas(string name)
        {
            var rig = RigRoster.Find(name);
            foreach (var pose in RigPoseNames.Strip)
            {
                var image = RigComposer.Compose(rig, _parts[name], rig.Pose(pose), RigComposer.PoseCanvas);
                for (int y = 0; y < image.Height; y++)
                {
                    Assert.AreEqual(0, image.Alpha(0, y), $"{pose}: clipped at the left");
                    Assert.AreEqual(0, image.Alpha(image.Width - 1, y), $"{pose}: clipped at the right");
                }
            }
        }

        [TestCaseSource(nameof(Names))]
        public void FacingLeft_IsTheSameShapeReflected(string name)
        {
            var rig = RigRoster.Find(name);
            var left = rig.Mirrored();
            var leftParts = RigComposer.RenderParts(left, OperatorLookBook.Palette, 96f, 4);

            var right = RigComposer.Compose(rig, _parts[name], rig.Pose(RigPoseNames.Rest), RigComposer.PoseCanvas);
            var mirrored = RigComposer.Compose(left, leftParts, left.Pose(RigPoseNames.Rest), RigComposer.PoseCanvas);

            // The pose canvas is centred on x = 0, so a reflection is a column flip.
            int w = right.Width, differ = 0, solid = 0;
            for (int y = 0; y < right.Height; y++)
            for (int x = 0; x < w; x++)
            {
                bool a = right.Alpha(x, y) >= 128;
                bool b = mirrored.Alpha(w - 1 - x, y) >= 128;
                if (a) solid++;
                if (a != b) differ++;
            }

            Assert.Less(differ, solid * 0.02f);
        }

        [TestCaseSource(nameof(Names))]
        public void TheCastClip_StartsAtRest_AndPeaksAtTheCastPose(string name)
        {
            var rig = RigRoster.Find(name);
            var start = RigClips.Sample(rig, RigClips.Cast, 0f);
            var peak = RigClips.Sample(rig, RigClips.Cast, RigClips.Length(RigClips.Cast) * 0.4f);

            Assert.AreEqual(0f, start.Get(rig.AimBone).Degrees, 1e-4f);
            Assert.AreEqual(rig.Pose(RigPoseNames.Cast).Get(rig.AimBone).Degrees, peak.Get(rig.AimBone).Degrees, 1e-4f);
        }
    }
}
