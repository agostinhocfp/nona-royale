// Assets/Tests/EditMode/Unity/RigBoardTests.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The rig on the board, in plain C# (OPERATOR_LOOKBOOK.md, LB5b): the
    /// per-frame evaluation, the clip timings fitted to MOTION.md, the
    /// animator's choice of clip, facing, and the parts cut at the table line.
    /// </summary>
    [TestFixture]
    public class RigBoardTests
    {
        private readonly Dictionary<string, RigImages> _images = new Dictionary<string, RigImages>();

        [OneTimeSetUp]
        public void BuildAll()
        {
            foreach (var rig in RigRoster.All)
                _images[rig.Name] = RigImages.Build(rig, OperatorLookBook.Palette);
        }

        private static IEnumerable<string> Names()
        {
            foreach (var rig in RigRoster.All) yield return rig.Name;
        }

        private static OperatorRig Rig(string name) => RigRoster.Find(name);

        // ── Evaluation ──────────────────────────────────────────────────

        [TestCaseSource(nameof(Names))]
        public void EvaluatingIntoAnArray_MatchesTheBlendedPose(string name)
        {
            var rig = Rig(name);
            var a = rig.Pose(RigPoseNames.StepA);
            var b = rig.Pose(RigPoseNames.Cast);
            var expected = rig.Skeleton.Evaluate(RigPose.Lerp(a, b, 0.3f));
            var into = new BoneWorld[rig.Skeleton.Bones.Count];

            rig.Skeleton.Evaluate(a, b, 0.3f, into);

            for (int i = 0; i < into.Length; i++)
            {
                var want = expected[rig.Skeleton.Bones[i].Name];
                Assert.AreEqual(want.PivotX, into[i].PivotX, 1e-5f, rig.Skeleton.Bones[i].Name);
                Assert.AreEqual(want.PivotY, into[i].PivotY, 1e-5f, rig.Skeleton.Bones[i].Name);
                Assert.AreEqual(want.Degrees, into[i].Degrees, 1e-4f, rig.Skeleton.Bones[i].Name);
                Assert.AreEqual(want.Scale, into[i].Scale, 1e-5f, rig.Skeleton.Bones[i].Name);
            }
        }

        [Test]
        public void EvaluatingIntoTooSmallAnArray_IsRefused()
        {
            var rig = RigRoster.All[0];
            Assert.Throws<ArgumentException>(() => rig.Skeleton.Evaluate(null, null, 0f, new BoneWorld[1]));
        }

        // ── Timings (MOTION.md) ─────────────────────────────────────────

        [Test]
        public void TheClips_KeepMotionsClocks()
        {
            // One step per cell at the hop's 8 cells a second.
            Assert.AreEqual(2f / 8f, RigClips.Length(RigClips.Step), 1e-5f);

            // MO2's breathing, sin(t · 1.8), and sway, sin(t · 0.9).
            Assert.AreEqual(2f * Math.PI / 1.8, RigClips.Length(RigClips.Idle), 1e-4);
            Assert.AreEqual(2f * Math.PI / 0.9, RigClips.Length(RigClips.Seated), 1e-4);

            // The cast is at its peak by the cast tell's reach (0.12 s) and holds past its linger (0.5 s).
            float length = RigClips.Length(RigClips.Cast);
            Assert.LessOrEqual(0.18f * length, 0.15f);
            Assert.GreaterOrEqual(0.62f * length, 0.48f);
        }

        [TestCaseSource(nameof(Names))]
        public void AWalk_ArrivesOnEveryCell_AtRest(string name)
        {
            var rig = Rig(name);
            var rest = rig.Skeleton.Evaluate(rig.Pose(RigPoseNames.Rest));

            for (int cells = 0; cells <= 4; cells++)
            {
                RigClips.Blend(rig, RigClips.Step, RigClips.StepPhase(cells), out var a, out var b, out float t);
                var world = rig.Skeleton.Evaluate(RigPose.Lerp(a, b, t));

                foreach (var bone in rig.Skeleton.Bones)
                    Assert.AreEqual(rest[bone.Name].Degrees, world[bone.Name].Degrees, 1e-3f, $"cell {cells}, {bone.Name}");
            }
        }

        [TestCaseSource(nameof(Names))]
        public void Midway_ThroughACell_TheLegsAreApart(string name)
        {
            var rig = Rig(name);
            RigClips.Blend(rig, RigClips.Step, RigClips.StepPhase(0.5f), out var a, out var b, out float t);
            var world = rig.Skeleton.Evaluate(RigPose.Lerp(a, b, t));

            Assert.Greater(Math.Abs(world[RigBones.ThighNear].Degrees - world[RigBones.ThighFar].Degrees), 10f);
        }

        // ── The animator ────────────────────────────────────────────────

        [TestCaseSource(nameof(Names))]
        public void Seated_PlaysTheSeatedLoop_WhateverElseIsGoingOn(string name)
        {
            var rig = Rig(name);
            var animator = new RigAnimator { Seated = true, WalkCells = 0.5f };
            animator.Rise(1f);

            animator.Sample(rig, RigAnimator.Crouch(rig), out var a, out _, out _);

            Assert.IsTrue(a.TableLine.HasValue, "the seated loop draws at the table");
        }

        [TestCaseSource(nameof(Names))]
        public void ARise_StandsUpFromACrouch_ThenIdles(string name)
        {
            var rig = Rig(name);
            var crouch = RigAnimator.Crouch(rig);
            var animator = new RigAnimator();

            animator.Rise(0.4f);
            animator.Sample(rig, crouch, out var a, out var b, out float t);
            Assert.AreSame(crouch, a);
            Assert.AreEqual(0f, t, 1e-5f, "it starts in the crouch");

            animator.Advance(0f, 0.2f);
            animator.Sample(rig, crouch, out _, out _, out float mid);
            Assert.Greater(mid, 0.5f);

            animator.Advance(0f, 0.25f);
            Assert.IsFalse(animator.Rising);
            animator.Sample(rig, crouch, out a, out b, out _);
            Assert.AreSame(rig.Pose(RigPoseNames.IdleA), a, "done rising, it breathes");
        }

        [TestCaseSource(nameof(Names))]
        public void Walking_PlaysTheStep_FromTheCellsCovered(string name)
        {
            var rig = Rig(name);
            var animator = new RigAnimator { WalkCells = 0.5f };

            animator.Sample(rig, null, out var a, out var b, out float t);
            RigClips.Blend(rig, RigClips.Step, RigClips.StepPhase(0.5f), out var wantA, out var wantB, out float wantT);

            Assert.AreSame(wantA, a);
            Assert.AreSame(wantB, b);
            Assert.AreEqual(wantT, t, 1e-5f);
        }

        [TestCaseSource(nameof(Names))]
        public void ReducedMotion_HoldsTheRestPose_ButStillSteps(string name)
        {
            var rig = Rig(name);
            var animator = new RigAnimator(1.3f) { ReducedMotion = true };

            animator.Sample(rig, null, out var a, out var b, out _);
            Assert.AreSame(rig.Pose(RigPoseNames.Rest), a);
            Assert.AreSame(a, b);

            animator.WalkCells = 0.5f;
            animator.Sample(rig, null, out a, out b, out _);
            Assert.AreNotSame(a, b, "the walk is how it moves, not decoration");
        }

        [Test]
        public void Facing_TurnsOnlyPastTheDeadZone()
        {
            Assert.IsTrue(RigAnimator.FaceLeft(1f, 0f, false, 0.05f));
            Assert.IsFalse(RigAnimator.FaceLeft(0f, 1f, true, 0.05f));
            Assert.IsTrue(RigAnimator.FaceLeft(0f, 0.04f, true, 0.05f), "straight above: keeps looking left");
            Assert.IsFalse(RigAnimator.FaceLeft(0f, -0.04f, false, 0.05f), "straight below: keeps looking right");
        }

        // ── The images ──────────────────────────────────────────────────

        [TestCaseSource(nameof(Names))]
        public void BothFacings_HaveEveryPart(string name)
        {
            var images = _images[name];
            Assert.AreEqual(images.Right.Parts.Count, images.Left.Parts.Count);
            Assert.IsTrue(images.Left.Rig.FacesLeft);
            Assert.IsFalse(images.Right.Rig.FacesLeft);

            foreach (var facing in new[] { images.Right, images.Left })
            foreach (var part in facing.Parts)
            {
                Assert.IsNotNull(part.Standing, part.Part.Name);
                Assert.IsFalse(part.Standing.IsEmpty, part.Part.Name);
                Assert.GreaterOrEqual(part.BoneIndex, 0, part.Part.Name);
            }
        }

        [TestCaseSource(nameof(Names))]
        public void TheExtents_StandOnTheFeet_AndSitAtTheTable(string name)
        {
            var images = _images[name];
            var seated = Rig(name).Pose(RigPoseNames.Seated);

            Assert.AreEqual(0f, images.RestBottom, 0.05f, "the feet are the figure's origin");
            Assert.Greater(images.RestTop, 2.5f);
            Assert.AreEqual(seated.TableLine.Value, images.SeatedBottom, 0.03f);
            Assert.Less(images.SeatedTop, images.RestTop, "seated, the head is lower");
            Assert.Greater(images.SeatedTop, images.SeatedBottom + 1f);
        }

        [TestCaseSource(nameof(Names))]
        public void Seated_NothingIsDrawnUnderTheTable(string name)
        {
            foreach (var facing in new[] { _images[name].Right, _images[name].Left })
            {
                var rig = facing.Rig;
                var pose = rig.Pose(RigPoseNames.Seated);
                var world = rig.Skeleton.Evaluate(pose);
                float line = pose.TableLine.Value;
                int drawn = 0;

                foreach (var part in facing.Parts)
                {
                    if (part.Seated == null || !part.Part.VisibleIn(pose)) continue;

                    var bone = rig.Skeleton[part.Part.Bone];
                    var image = part.Seated;
                    var canvas = image.Canvas;

                    for (int y = 0; y < image.Height; y++)
                    for (int x = 0; x < image.Width; x++)
                    {
                        if (image.Alpha(x, y) < 128) continue;

                        float rx = canvas.Left + (x + 0.5f) / canvas.PixelsPerUnit;
                        float ry = canvas.Bottom + (y + 0.5f) / canvas.PixelsPerUnit;
                        RigSkeleton.Transform(world[part.Part.Bone], bone.PivotX, bone.PivotY, rx, ry, out _, out float wy);
                        Assert.GreaterOrEqual(wy, line - 1.5f / canvas.PixelsPerUnit,
                            $"{(rig.FacesLeft ? "left" : "right")} {part.Part.Name} draws under the table");
                        drawn++;
                    }
                }

                Assert.Greater(drawn, 0);
            }
        }

        [TestCaseSource(nameof(Names))]
        public void Seated_TheHeadIsTheStandingImage_AndTheShinsAreAtMostAKnee(string name)
        {
            var parts = _images[name].Right.Parts;
            RigPartImages Find(string bone)
            {
                foreach (var part in parts)
                    if (part.Part.Bone == bone) return part;
                return null;
            }

            var head = Find(RigBones.Head);
            Assert.AreSame(head.Standing, head.Seated, "wholly above the table, it is not rasterised twice");

            // The shins hang under the table; at most the top of a knee shows over it.
            foreach (var bone in new[] { RigBones.ShinNear, RigBones.ShinFar })
            {
                var shin = Find(bone);
                if (shin?.Seated == null) continue;

                int standing = shin.Standing.OpaqueTopRow - shin.Standing.OpaqueBottomRow;
                int seated = shin.Seated.OpaqueTopRow - shin.Seated.OpaqueBottomRow;
                Assert.Less(seated, standing / 3, $"{bone} is under the table");
            }
        }

        // ── Event poses (LB5c) ──────────────────────────────────────────

        [TestCaseSource(nameof(Names))]
        public void TheCast_LightsTheDevice_OnlyForItsHold(string name)
        {
            var rig = Rig(name);
            var animator = new RigAnimator();
            float length = RigClips.CastLengthFor(0.5f);

            animator.Cast(RigAnimator.Aimed(rig, 0f), length);
            Assert.IsFalse(animator.Powered, "not lit while it winds up");

            animator.Advance(0f, length * 0.3f);
            Assert.IsTrue(animator.Powered, "lit through the hold");

            animator.WalkCells = 0.5f;
            Assert.IsFalse(animator.Powered, "a walk takes over, and the tell goes out");
            animator.WalkCells = null;

            animator.Advance(0f, length * 0.4f);
            Assert.IsFalse(animator.Powered, "out as the arm comes back");
            Assert.IsTrue(animator.Casting);

            animator.Advance(0f, length);
            Assert.IsFalse(animator.Casting);
        }

        [Test]
        public void TheCastsHold_EndsWithTheTell()
        {
            float length = RigClips.CastLengthFor(0.5f);
            Assert.AreEqual(0.5f, length * RigClips.CastRelease, 1e-4f);
            Assert.IsTrue(RigClips.CastPowered(0.3f));
            Assert.IsFalse(RigClips.CastPowered(0.1f));
            Assert.IsFalse(RigClips.CastPowered(0.7f));
        }

        [TestCaseSource(nameof(Names))]
        public void AnAimedCast_RaisesTheArm_TowardATargetAbove_InEitherFacing(string name)
        {
            foreach (var rig in new[] { Rig(name), Rig(name).Mirrored() })
            {
                Assert.AreEqual(Rig(name).AimBone, rig.AimBone, "facing keeps the casting arm");
                float level = rig.Pose(RigPoseNames.Cast).Get(rig.AimBone).Degrees;
                float up = RigAnimator.Aimed(rig, 20f).Get(rig.AimBone).Degrees;
                float steep = RigAnimator.Aimed(rig, 80f).Get(rig.AimBone).Degrees;
                float sign = rig.FacesLeft ? -1f : 1f;

                Assert.AreEqual(20f, (up - level) * sign, 1e-3f, rig.FacesLeft ? "left" : "right");
                Assert.AreEqual(RigAnimator.MaxAim, (steep - level) * sign, 1e-3f, "clamped");
                Assert.AreSame(rig.Pose(RigPoseNames.Cast), RigAnimator.Aimed(rig, 0f), "level: the recipe's own cast");
            }
        }

        [Test]
        public void Elevation_IsTheAngleAboveTheLevel_EitherSide()
        {
            Assert.AreEqual(45f, RigAnimator.Elevation(0f, 0f, 1f, 1f), 1e-3f);
            Assert.AreEqual(45f, RigAnimator.Elevation(0f, 0f, -1f, 1f), 1e-3f);
            Assert.AreEqual(-45f, RigAnimator.Elevation(0f, 0f, 1f, -1f), 1e-3f);
            Assert.AreEqual(0f, RigAnimator.Elevation(2f, 3f, 2f, 3f), 1e-3f);
        }

        [TestCaseSource(nameof(Names))]
        public void TheKnockout_Folds_ThenHolds_OverEverythingButTheTable(string name)
        {
            var rig = Rig(name);
            var animator = new RigAnimator { WalkCells = 1.5f };
            animator.Hit(0.3f);
            animator.Knockout(0.25f);

            animator.Sample(rig, null, out var a, out var b, out _);
            Assert.AreSame(rig.Pose(RigPoseNames.Rest), a, "the fold starts from the figure standing");
            Assert.AreSame(rig.Pose(RigPoseNames.Knockout), b);
            Assert.IsFalse(animator.Folded);

            animator.Advance(0f, 0.3f);
            Assert.IsTrue(animator.Folded, "time for the shatter");
            animator.Sample(rig, null, out _, out b, out float t);
            Assert.AreSame(rig.Pose(RigPoseNames.Knockout), b);
            Assert.AreEqual(1f, t, 1e-5f, "it stays down");

            animator.Clear();
            Assert.IsFalse(animator.KnockedOut);
        }

        [TestCaseSource(nameof(Names))]
        public void TheHit_RocksBack_ThenReturns(string name)
        {
            var rig = Rig(name);
            var animator = new RigAnimator(0f) { ReducedMotion = true };
            animator.Hit(0.3f);

            animator.Advance(0f, 0.3f * RigClips.HitPeak);
            animator.Sample(rig, null, out var a, out var b, out float t);
            Assert.AreSame(rig.Pose(RigPoseNames.Hit), t >= 0.5f ? b : a, "at its peak");

            animator.Advance(0f, 0.3f);
            Assert.IsFalse(animator.Recoiling);
            animator.Sample(rig, null, out a, out _, out _);
            Assert.AreSame(rig.Pose(RigPoseNames.Rest), a, "back at rest");
        }

        [TestCaseSource(nameof(Names))]
        public void SeatedLoop_Breathes_ThenTakesInTheRoom(string name)
        {
            var rig = Rig(name);

            RigClips.Blend(rig, RigClips.Seated, 0.25f, out var a, out var b, out _);
            Assert.AreSame(rig.Pose(RigPoseNames.SeatedB), b, "first half: a breath");

            RigClips.Blend(rig, RigClips.Seated, 0.75f, out a, out b, out _);
            Assert.AreSame(rig.Pose(RigPoseNames.SeatedLook), a, "second half: the activity, held");
            Assert.AreSame(a, b);
            Assert.IsTrue(a.TableLine.HasValue, "still at the table");
        }

        [TestCaseSource(nameof(Names))]
        public void OnlyTheDevice_HasAPoweredImage_AndItIsCyan(string name)
        {
            int powered = 0;
            foreach (var part in _images[name].Right.Parts)
            {
                if (part.Powered == null) continue;

                powered++;
                Assert.Greater(SilhouetteMetrics.Count(part.Powered, SilhouetteMetrics.IsCyan), 0, part.Part.Name);
                Assert.AreEqual(0, SilhouetteMetrics.Count(part.Standing, SilhouetteMetrics.IsCyan), part.Part.Name + " at rest");
                Assert.AreEqual(part.Standing.Width, part.Powered.Width, "the same canvas, so the same offset");
                Assert.AreEqual(part.Standing.Height, part.Powered.Height);
            }

            Assert.Greater(powered, 0, "the cast has something to light");
        }
    }
}
