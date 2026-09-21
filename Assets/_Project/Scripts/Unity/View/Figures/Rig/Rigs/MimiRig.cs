// Assets/_Project/Scripts/Unity/View/Figures/Rig/Rigs/MimiRig.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Mimi on the rig, in three-quarter view (OPERATOR_LOOKBOOK.md, LB5d):
    /// the small dart. Near-black and narrow, with bright pale hardware.
    /// </summary>
    /// <remarks>
    /// Drawn turned toward the right of the screen: everything narrow, nothing
    /// with mass. Almost all of her is one dark shape on purpose; the only
    /// light things are the frosted rig and her face and hands, and at board
    /// scale you see the rig, not her.
    ///
    /// <b>The rig</b> is a pack on her back, which shows past her near
    /// shoulder because the back is the side turned toward the camera's left,
    /// caps over both shoulders, straps straight down the front, and two short
    /// emitters angled up and out: the dart's fins. The coordinate plate is on
    /// her left forearm, the far one. Their charge lines are the tell.
    ///
    /// <b>The coat walks</b> the way Syla's gown does: the skirt of the
    /// coat-dress rides the hips, so a lean never swings it.
    ///
    /// <b>The head pass</b> (ART_PROMPTS, Mimi): black hair scraped back into
    /// a small hard knot at the back of the head, a pale sharp face, the high
    /// black collar.
    ///
    /// <b>Her own poses.</b> The cast points the near hand at the target
    /// while the emitters and the plate light. Seated, the rig is dark and
    /// her hands are flat on the felt; her activity is checking the plate.
    /// </remarks>
    public static class MimiRig
    {
        public const string Torso = "torso";
        public const string HeadPart = "head";
        public const string Skirt = "skirt";
        public const string Pack = "pack";

        public static readonly OperatorRig Rig = Build();

        private static OperatorRig Build()
        {
            var skeleton = new RigSkeleton()
                .Bone(RigBones.Root, null, 0.00f, 1.00f)
                .Bone(RigBones.Chest, RigBones.Root, 0.00f, 1.02f)
                .Bone(RigBones.Head, RigBones.Chest, 0.06f, 2.28f)
                .Bone(RigBones.UpperArmNear, RigBones.Chest, -0.24f, 2.16f)
                .Bone(RigBones.ForearmNear, RigBones.UpperArmNear, -0.28f, 1.74f)
                .Bone(RigBones.UpperArmFar, RigBones.Chest, 0.26f, 2.14f)
                .Bone(RigBones.ForearmFar, RigBones.UpperArmFar, 0.29f, 1.74f)
                .Bone(RigBones.ThighNear, RigBones.Root, -0.08f, 0.98f)
                .Bone(RigBones.ShinNear, RigBones.ThighNear, -0.09f, 0.50f)
                .Bone(RigBones.ThighFar, RigBones.Root, 0.12f, 0.98f)
                .Bone(RigBones.ShinFar, RigBones.ThighFar, 0.13f, 0.50f);

            var feet = new[]
            {
                new RigAnchor(RigBones.ShinNear, -0.09f, 0.00f),
                new RigAnchor(RigBones.ShinFar, 0.18f, 0.00f),
            };

            var poses = RigPoses.Template(RigBuild.Light);

            // The near hand pointed at the target, the far forearm lifted to read the plate.
            poses[RigPoseNames.Cast] = new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Chest, -2f)
                .Turn(RigBones.Head, 2f)
                .Turn(RigBones.UpperArmNear, 82f)
                .Turn(RigBones.ForearmNear, 6f)
                .Turn(RigBones.UpperArmFar, 20f)
                .Turn(RigBones.ForearmFar, 70f);

            // Hands flat on the felt, the rig dark.
            var seated = RigPoses.Seated(RigPoseNames.Seated, 0.44f, 0.72f)
                .Turn(RigBones.Head, -3f)
                .Turn(RigBones.UpperArmNear, 30f)
                .Turn(RigBones.ForearmNear, 56f)
                .Turn(RigBones.UpperArmFar, 18f)
                .Turn(RigBones.ForearmFar, 60f);
            poses[RigPoseNames.Seated] = seated;
            poses[RigPoseNames.SeatedB] = seated.Copy(RigPoseNames.SeatedB)
                .Turn(RigBones.Chest, 1f, 0f, 0.01f, 1.01f);

            // Checking the plate: the far forearm lifted, her eyes on it.
            poses[RigPoseNames.SeatedLook] = seated.Copy(RigPoseNames.SeatedLook)
                .Turn(RigBones.Head, -9f)
                .Turn(RigBones.UpperArmFar, 26f)
                .Turn(RigBones.ForearmFar, 96f);

            return new OperatorRig("Mimi", skeleton, feet, Parts, poses);
        }

        private static IReadOnlyList<RigPart> Parts(LookBookPalette p)
        {
            float line = DecoMotifs.LineWeight;
            float rim = DecoMotifs.RimOnDark;
            const float noFloor = -10f;

            // ── Far arm: the coordinate plate on the forearm ────────────
            var farUpperShape = DecoMotifs.Limb(0.26f, 2.14f, 0.09f, 0.29f, 1.74f, 0.075f);
            var farUpperArm = new FigureDrawing(farUpperShape, p.MimiCoat, p.Ink, line)
                .Shade(farUpperShape.Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right);

            var farForeShape = DecoMotifs.Limb(0.29f, 1.74f, 0.075f, 0.30f, 1.38f, 0.065f);
            var farHand = FigureShape.Ellipse(0.31f, 1.29f, 0.055f, 0.08f);
            var plate = FigureShape.Polygon(0.23f, 1.46f, 0.37f, 1.47f, 0.37f, 1.64f, 0.23f, 1.62f);
            var farForearm = new FigureDrawing(farForeShape.Or(farHand).Or(plate), p.MimiCoat, p.Ink, line)
                .Block(farHand, p.MimiSkin)
                .Block(plate, p.MimiRig)
                .Shade(farForeShape.Or(farHand).Offset(0.1f).Minus(plate), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right)
                .Powered(plate.Offset(-0.025f), p.Powered);

            // ── Legs: black leggings, flat boots, no rim ────────────────
            FigureDrawing Shin(float x, float toe, bool near)
            {
                var leg = DecoMotifs.Limb(x, 0.50f, 0.075f, x, 0.12f, 0.065f);
                var boot = FigureShape.Polygon(x - 0.08f, 0.22f, x + 0.07f, 0.22f, x + 0.07f, 0.10f, toe, 0.03f, toe, 0.00f, x - 0.09f, 0.00f);
                var d = new FigureDrawing(leg.Or(boot), p.MimiCoat, p.Ink, line)
                    .Block(boot, p.Obsidian)
                    .Light(FigureShape.Rect(x - 0.075f, 0.24f, x - 0.04f, 0.50f).And(leg), p.PlateSheen);
                return near ? d : d.Shade(leg.Or(boot).Offset(0.1f), p.Shade);
            }

            FigureDrawing Thigh(float x, bool near)
            {
                var shape = DecoMotifs.Limb(x, 1.00f, 0.09f, x - 0.01f, 0.50f, 0.075f);
                var d = new FigureDrawing(shape, p.MimiCoat, p.Ink, line);
                return near ? d : d.Shade(shape.Offset(0.1f), p.Shade);
            }

            // ── The pack on her back: behind everything but the far arm ──
            var packShape = FigureShape.Union(
                FigureShape.Polygon(-0.44f, 2.40f, -0.14f, 2.42f, -0.12f, 1.92f, -0.40f, 1.90f),
                DecoMotifs.Limb(-0.30f, 2.34f, 0.045f, -0.56f, 2.68f, 0.04f),
                DecoMotifs.Limb(0.16f, 2.36f, 0.04f, 0.36f, 2.62f, 0.035f));
            var tips = FigureShape.Circle(-0.56f, 2.68f, 0.055f).Or(FigureShape.Circle(0.36f, 2.62f, 0.045f));
            var pack = new FigureDrawing(packShape.Or(tips), p.MimiRig, p.Ink, line)
                .Block(DecoMotifs.Limb(-0.30f, 2.34f, 0.045f, -0.56f, 2.68f, 0.04f).Or(DecoMotifs.Limb(0.16f, 2.36f, 0.04f, 0.36f, 2.62f, 0.035f)), p.MimiSteel)
                .Block(tips, p.MimiRig)
                .Shade(FigureShape.HalfPlane(-0.26f, 2.1f, -1f, 0f).And(packShape.Minus(tips)), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left | RimEdges.Top)
                .InkLine(FigureShape.Polyline(-0.42f, 2.10f, -0.14f, 2.12f))
                .Powered(FigureShape.Polyline(-0.32f, 2.36f, -0.54f, 2.66f).Offset(0.012f)
                    .Or(FigureShape.Polyline(0.18f, 2.38f, 0.35f, 2.60f).Offset(0.010f)), p.Powered);

            // ── Skirt of the coat-dress: rides the hips ──────────────────
            var skirtShape = FigureShape.Polygon(-0.22f, 1.72f, 0.24f, 1.72f, 0.36f, 0.92f, -0.32f, 0.92f);
            var skirt = new FigureDrawing(skirtShape, p.MimiCoat, p.Ink, line)
                .Block(DecoMotifs.Chevron(1.12f, 0.30f, 0.10f, 0.035f).Translate(0.02f, 0f).And(skirtShape), p.PlateSheen)
                .Shade(FigureShape.HalfPlane(0.10f, 1.3f, -1f, 0.06f), p.Shade)
                .Light(FigureShape.Polygon(-0.22f, 1.72f, -0.12f, 1.72f, -0.20f, 0.94f, -0.32f, 0.92f).And(skirtShape), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left)
                .InkLine(FigureShape.Polyline(0.08f, 1.70f, 0.10f, 0.94f));

            // ── Bodice: narrow, the caps and straps over it ──────────────
            var bodice = FigureShape.Polygon(-0.26f, 2.26f, 0.30f, 2.24f, 0.22f, 1.62f, -0.20f, 1.62f);
            var caps = FigureShape.Ellipse(-0.26f, 2.22f, 0.12f, 0.08f).Or(FigureShape.Ellipse(0.28f, 2.20f, 0.08f, 0.07f));
            var straps = FigureShape.Rect(-0.20f, 1.94f, -0.13f, 2.22f).Or(FigureShape.Rect(0.16f, 1.94f, 0.21f, 2.20f));
            var collar = FigureShape.Polygon(-0.04f, 2.24f, 0.18f, 2.24f, 0.16f, 2.44f, -0.02f, 2.44f);

            var torso = new FigureDrawing(bodice.Or(caps).Or(collar), p.MimiCoat, p.Ink, line)
                .Block(caps.Or(straps), p.MimiRig)
                .Shade(FigureShape.HalfPlane(0.10f, 1.9f, -1f, 0.06f).Minus(caps).Minus(straps), p.Shade)
                .Light(FigureShape.Polygon(-0.26f, 2.24f, -0.16f, 2.24f, -0.12f, 1.64f, -0.20f, 1.62f).Minus(straps).Minus(caps).And(bodice), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Top | RimEdges.Left);

            // ── Head: the hard knot, the pale sharp face ─────────────────
            var skull = FigureShape.Ellipse(0.10f, 2.58f, 0.15f, 0.18f);
            var nose = FigureShape.Polygon(0.23f, 2.60f, 0.29f, 2.52f, 0.23f, 2.50f);
            var knot = FigureShape.Circle(-0.08f, 2.70f, 0.07f);
            var neck = FigureShape.Rect(0.00f, 2.28f, 0.14f, 2.44f);
            var headShape = FigureShape.Union(skull, nose, knot, neck);
            var hair = skull.Offset(0.012f).And(FigureShape.Polygon(-0.10f, 2.80f, 0.30f, 2.80f, 0.26f, 2.66f, 0.10f, 2.62f, 0.02f, 2.46f, -0.10f, 2.46f)).Or(knot);

            var head = new FigureDrawing(headShape, p.MimiSkin, p.Ink, line)
                .Block(hair, p.MimiCoat)
                .Shade(DecoMotifs.Crescent(0.10f, 2.56f, 0.20f, -0.10f, 0.05f).Or(nose).Minus(hair), p.Shade)
                .Shade(FigureShape.Rect(0.00f, 2.28f, 0.14f, 2.40f).And(neck), p.Shade)
                .Light(FigureShape.Polygon(-0.04f, 2.70f, 0.06f, 2.76f, 0.18f, 2.77f, 0.02f, 2.68f).And(hair), p.PlateSheen)
                .Rim(rim, p.Rim, noFloor)
                .InkLine(FigureShape.Polyline(0.16f, 2.60f, 0.22f, 2.61f))
                .InkLine(FigureShape.Polyline(0.17f, 2.46f, 0.22f, 2.46f));

            // ── Near arm: narrow sleeve, held close ──────────────────────
            var nearUpperShape = DecoMotifs.Limb(-0.24f, 2.16f, 0.095f, -0.28f, 1.74f, 0.08f);
            var nearUpperArm = new FigureDrawing(nearUpperShape, p.MimiCoat, p.Ink, line)
                .Light(FigureShape.Rect(-0.34f, 1.78f, -0.28f, 2.12f).And(nearUpperShape), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left);

            var nearForeShape = DecoMotifs.Limb(-0.28f, 1.74f, 0.08f, -0.30f, 1.38f, 0.07f);
            var nearHand = FigureShape.Ellipse(-0.31f, 1.29f, 0.06f, 0.085f);
            var nearForearm = new FigureDrawing(nearForeShape.Or(nearHand), p.MimiCoat, p.Ink, line)
                .Block(nearHand, p.MimiSkin)
                .Light(FigureShape.Rect(-0.36f, 1.40f, -0.31f, 1.72f).And(nearForeShape), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left);

            return new[]
            {
                new RigPart("forearm.far", RigBones.ForearmFar, 0, farForearm),
                new RigPart("upperArm.far", RigBones.UpperArmFar, 1, farUpperArm),
                new RigPart(Pack, RigBones.Chest, 2, pack),
                new RigPart("shin.far", RigBones.ShinFar, 3, Shin(0.13f, 0.26f, false)),
                new RigPart("thigh.far", RigBones.ThighFar, 4, Thigh(0.13f, false)),
                new RigPart("shin.near", RigBones.ShinNear, 5, Shin(-0.09f, 0.04f, true)),
                new RigPart("thigh.near", RigBones.ThighNear, 6, Thigh(-0.08f, true)),
                new RigPart(Skirt, RigBones.Root, 7, skirt),
                new RigPart(Torso, RigBones.Chest, 8, torso),
                new RigPart(HeadPart, RigBones.Head, 9, head),
                new RigPart("upperArm.near", RigBones.UpperArmNear, 10, nearUpperArm),
                new RigPart("forearm.near", RigBones.ForearmNear, 11, nearForearm),
            };
        }
    }
}
