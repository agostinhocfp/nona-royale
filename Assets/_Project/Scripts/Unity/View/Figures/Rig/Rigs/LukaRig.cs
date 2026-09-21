// Assets/_Project/Scripts/Unity/View/Figures/Rig/Rigs/LukaRig.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Luka on the rig, in three-quarter view (OPERATOR_LOOKBOOK.md, LB5d):
    /// the four-point X, a thrown blade. The warm light torso, camel, which
    /// only he may carry.
    /// </summary>
    /// <remarks>
    /// <b>Drawn rigged from the start, and behind his render.</b> Luka has
    /// real renders for both poses (ART_HOOKUP.md), and a render always wins,
    /// so this rig draws only if they are removed. Its colours are matched to
    /// the render so the two can stand in for each other.
    ///
    /// Drawn turned toward the right of the screen in a fighter's ready
    /// stance: weight forward on the lead (far) foot, the rear (near) leg
    /// back, shoulders angled, head slightly down. Lean and wiry, never
    /// broad: a middleweight, visibly lighter than Bouncer. The four points
    /// are the two feet, the raised open far hand and the near elbow.
    ///
    /// The worn camel blazer hangs open over the dark shirt, sleeves pushed to
    /// the forearms; the knuckles are taped. The signet ring on his right
    /// hand, the near one, is his only technology and the tell.
    ///
    /// <b>The head pass</b> (ART_PROMPTS, Luka): the buzzcut as one dark
    /// shape, stubble as a flat tone on the jaw, a strong brow with the healed
    /// split scar cutting it, the head lowered.
    ///
    /// <b>His own poses.</b> The cast brings the ringed fist up at the target.
    /// Seated, at the table taping his hands; his activity is winding the
    /// tape.
    /// </remarks>
    public static class LukaRig
    {
        public const string Torso = "torso";
        public const string HeadPart = "head";

        public static readonly OperatorRig Rig = Build();

        private static OperatorRig Build()
        {
            var skeleton = new RigSkeleton()
                .Bone(RigBones.Root, null, 0.02f, 1.12f)
                .Bone(RigBones.Chest, RigBones.Root, 0.02f, 1.14f)
                .Bone(RigBones.Head, RigBones.Chest, 0.20f, 2.24f)
                .Bone(RigBones.UpperArmNear, RigBones.Chest, -0.30f, 2.10f)
                .Bone(RigBones.ForearmNear, RigBones.UpperArmNear, -0.40f, 1.64f)
                .Bone(RigBones.UpperArmFar, RigBones.Chest, 0.38f, 2.10f)
                .Bone(RigBones.ForearmFar, RigBones.UpperArmFar, 0.50f, 1.68f)
                .Bone(RigBones.ThighNear, RigBones.Root, -0.08f, 1.10f)
                .Bone(RigBones.ShinNear, RigBones.ThighNear, -0.26f, 0.58f)
                .Bone(RigBones.ThighFar, RigBones.Root, 0.14f, 1.10f)
                .Bone(RigBones.ShinFar, RigBones.ThighFar, 0.34f, 0.60f);

            var feet = new[]
            {
                new RigAnchor(RigBones.ShinNear, -0.36f, 0.00f),
                new RigAnchor(RigBones.ShinFar, 0.46f, 0.00f),
            };

            var poses = RigPoses.Template(RigBuild.Light);

            // The ringed fist brought up at the target.
            poses[RigPoseNames.Cast] = new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Chest, -5f)
                .Turn(RigBones.Head, 3f)
                .Turn(RigBones.UpperArmNear, 84f)
                .Turn(RigBones.ForearmNear, 16f)
                .Turn(RigBones.UpperArmFar, -10f)
                .Turn(RigBones.ForearmFar, -12f);

            // At the table, taping his hands: both forearms on the felt.
            var seated = RigPoses.Seated(RigPoseNames.Seated, 0.46f, 0.72f)
                .Turn(RigBones.ThighNear, 70f)
                .Turn(RigBones.ShinNear, -70f)
                .Turn(RigBones.ThighFar, 62f)
                .Turn(RigBones.ShinFar, -62f)
                .Turn(RigBones.Chest, -2f)
                .Turn(RigBones.Head, -10f)
                .Turn(RigBones.UpperArmNear, 40f)
                .Turn(RigBones.ForearmNear, 50f)
                .Turn(RigBones.UpperArmFar, 10f)
                .Turn(RigBones.ForearmFar, -30f);
            poses[RigPoseNames.Seated] = seated;
            poses[RigPoseNames.SeatedB] = seated.Copy(RigPoseNames.SeatedB)
                .Turn(RigBones.Chest, -1f, 0f, 0.012f, 1.01f);

            // Winding the tape: the near hand circles over the far one.
            poses[RigPoseNames.SeatedLook] = seated.Copy(RigPoseNames.SeatedLook)
                .Turn(RigBones.Head, -13f)
                .Turn(RigBones.UpperArmNear, 48f)
                .Turn(RigBones.ForearmNear, 70f);

            return new OperatorRig("Luka", skeleton, feet, Parts, poses);
        }

        private static IReadOnlyList<RigPart> Parts(LookBookPalette p)
        {
            float line = DecoMotifs.LineWeight;
            float rim = DecoMotifs.RimOnDark;
            const float noFloor = -10f;

            // ── Far arm: half-raised, the hand open ─────────────────────
            var farUpperShape = DecoMotifs.Limb(0.38f, 2.10f, 0.10f, 0.50f, 1.68f, 0.085f);
            var farUpperArm = new FigureDrawing(farUpperShape, p.LukaBlazer, p.Ink, line)
                .Shade(farUpperShape.Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right | RimEdges.Top);

            // The sleeve pushed up: camel to mid-forearm, then skin, the hand open and taped.
            var farForeShape = DecoMotifs.Limb(0.50f, 1.68f, 0.085f, 0.72f, 1.88f, 0.07f);
            var farHand = FigureShape.Polygon(0.72f, 1.84f, 0.84f, 1.94f, 0.86f, 2.04f, 0.78f, 2.04f, 0.70f, 1.96f);
            var farForearm = new FigureDrawing(farForeShape.Or(farHand), p.LukaSkin, p.Ink, line)
                .Block(FigureShape.Circle(0.50f, 1.68f, 0.13f).And(farForeShape.Offset(0.02f)), p.LukaBlazer)
                .Block(farHand.And(FigureShape.HalfPlane(0.78f, 1.95f, 0.6f, 0.8f)), p.Bone)
                .Shade(farForeShape.Or(farHand).Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right | RimEdges.Top);

            // ── Legs: the rear leg back, the lead leg forward ────────────
            FigureDrawing Shin(float kx, float ky, float ax, float toe, bool near)
            {
                var leg = DecoMotifs.Limb(kx, ky, 0.10f, ax, 0.14f, 0.08f);
                var shoe = FigureShape.Polygon(ax - 0.11f, 0.14f, ax + 0.07f, 0.14f, toe, 0.03f, toe, 0.00f, ax - 0.12f, 0.00f);
                var d = new FigureDrawing(leg.Or(shoe), p.LukaShirt, p.Ink, line)
                    .Block(shoe, p.Obsidian)
                    .Light(FigureShape.Polygon(kx - 0.10f, ky, kx - 0.05f, ky, ax - 0.03f, 0.16f, ax - 0.08f, 0.16f).And(leg), p.PlateSheen);
                return near ? d : d.Shade(leg.Or(shoe).Offset(0.1f), p.Shade);
            }

            FigureDrawing Thigh(float hx, float kx, float ky, bool near)
            {
                var shape = DecoMotifs.Limb(hx, 1.12f, 0.12f, kx, ky, 0.10f);
                var d = new FigureDrawing(shape, p.LukaShirt, p.Ink, line);
                return near ? d : d.Shade(shape.Offset(0.1f), p.Shade);
            }

            // ── Torso: lean, shoulders angled, the blazer open ───────────
            var torsoShape = FigureShape.Polygon(
                -0.40f, 2.20f,
                0.10f, 2.24f,
                0.44f, 2.20f,
                0.52f, 2.10f,
                0.40f, 1.60f,
                0.34f, 1.00f,
                -0.20f, 1.00f,
                -0.26f, 1.60f,
                -0.40f, 2.06f);
            var shirt = FigureShape.Polygon(0.00f, 2.22f, 0.30f, 2.20f, 0.24f, 1.36f, 0.06f, 1.40f);
            var lapelNear = FigureShape.Polygon(0.00f, 2.22f, -0.14f, 2.18f, 0.02f, 1.86f, 0.06f, 1.40f);
            var lapelFar = FigureShape.Polygon(0.30f, 2.20f, 0.40f, 2.12f, 0.30f, 1.88f, 0.24f, 1.40f);
            var belt = FigureShape.Rect(0.06f, 1.04f, 0.26f, 1.10f);
            var sidePlane = FigureShape.HalfPlane(0.30f, 1.6f, -1f, 0.08f);

            var torso = new FigureDrawing(torsoShape, p.LukaBlazer, p.Ink, line)
                .Block(shirt.Or(FigureShape.Polygon(0.06f, 1.40f, 0.24f, 1.36f, 0.26f, 1.00f, 0.04f, 1.00f)), p.LukaShirt)
                .Block(belt, p.Brass)
                .Shade(sidePlane.Minus(shirt).Minus(lapelNear), p.Shade)
                .Shade(FigureShape.Polygon(-0.20f, 1.30f, 0.02f, 1.20f, 0.00f, 1.02f, -0.20f, 1.02f), p.Shade)   // a crease across the hip
                .Light(lapelNear, p.Key)
                .Light(FigureShape.Polygon(-0.40f, 2.20f, 0.00f, 2.24f, -0.02f, 2.16f, -0.36f, 2.10f).And(torsoShape), p.Key)
                .Rim(rim, p.Rim, noFloor, RimEdges.Top | RimEdges.Right)
                .InkLine(FigureShape.Polyline(0.06f, 1.40f, 0.04f, 1.00f))
                .InkLine(FigureShape.Polyline(0.24f, 1.40f, 0.26f, 1.00f))
                .InkLine(FigureShape.Polyline(0.30f, 2.20f, 0.30f, 1.88f, 0.24f, 1.40f));

            // ── Head: the buzzcut, the scarred brow, lowered ─────────────
            var neck = FigureShape.Polygon(0.08f, 2.16f, 0.30f, 2.16f, 0.32f, 2.38f, 0.10f, 2.40f);
            var skull = FigureShape.Ellipse(0.28f, 2.56f, 0.15f, 0.20f);
            var jaw = FigureShape.Polygon(0.18f, 2.46f, 0.44f, 2.48f, 0.42f, 2.38f, 0.30f, 2.32f, 0.18f, 2.36f);
            var nose = FigureShape.Polygon(0.42f, 2.60f, 0.49f, 2.50f, 0.43f, 2.47f);
            var headShape = FigureShape.Union(neck, skull, jaw, nose);
            var hair = skull.Offset(0.01f).And(FigureShape.Polygon(0.10f, 2.80f, 0.44f, 2.80f, 0.40f, 2.68f, 0.24f, 2.64f, 0.16f, 2.48f, 0.10f, 2.48f));
            var stubble = jaw.And(FigureShape.HalfPlane(0f, 2.44f, 0f, 1f));
            // The brow, split by the healed scar.
            var brow = FigureShape.Polygon(0.32f, 2.62f, 0.45f, 2.62f, 0.45f, 2.57f, 0.33f, 2.56f)
                .Minus(FigureShape.Polyline(0.37f, 2.66f, 0.40f, 2.52f).Offset(0.012f));

            var head = new FigureDrawing(headShape.Or(hair), p.LukaSkin, p.Ink, line)
                .Block(hair, p.LukaShirt)
                .Shade(stubble, p.Shade)
                .Shade(DecoMotifs.Crescent(0.28f, 2.54f, 0.20f, -0.10f, 0.05f).Or(nose).Minus(hair), p.Shade)
                .Shade(FigureShape.Rect(0.08f, 2.16f, 0.32f, 2.30f).And(neck), p.Shade)
                .Shade(brow, p.Shade)
                .Light(FigureShape.Polygon(0.14f, 2.70f, 0.24f, 2.76f, 0.36f, 2.76f, 0.20f, 2.68f).And(hair), p.PlateSheen)
                .Rim(rim, p.Rim, noFloor)
                .InkLine(FigureShape.Polyline(0.37f, 2.66f, 0.40f, 2.52f))
                .InkLine(FigureShape.Polyline(0.36f, 2.42f, 0.44f, 2.42f));

            // ── Near arm: the ringed fist loose at his side ──────────────
            var nearUpperShape = DecoMotifs.Limb(-0.30f, 2.10f, 0.105f, -0.40f, 1.64f, 0.09f);
            var nearUpperArm = new FigureDrawing(nearUpperShape, p.LukaBlazer, p.Ink, line)
                .Shade(DecoMotifs.ShadowSide(-0.30f, 1.8f, 0.1f), p.Shade)
                .Light(FigureShape.Rect(-0.50f, 1.68f, -0.42f, 2.08f).And(nearUpperShape), p.Key)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left | RimEdges.Top);

            var nearForeShape = DecoMotifs.Limb(-0.40f, 1.64f, 0.09f, -0.44f, 1.24f, 0.075f);
            var nearFist = FigureShape.Ellipse(-0.45f, 1.14f, 0.09f, 0.10f);
            var ring = FigureShape.Rect(-0.55f, 1.10f, -0.42f, 1.19f);
            var nearForearm = new FigureDrawing(nearForeShape.Or(nearFist), p.LukaSkin, p.Ink, line)
                .Block(FigureShape.Circle(-0.40f, 1.64f, 0.14f).And(nearForeShape.Offset(0.02f)), p.LukaBlazer)
                .Block(nearFist.And(FigureShape.HalfPlane(0f, 1.12f, 0f, 1f)).Or(FigureShape.Rect(-0.54f, 1.18f, -0.36f, 1.24f).And(nearForeShape.Or(nearFist))), p.Bone)
                .Block(ring, p.Brass)
                .Shade(DecoMotifs.ShadowSide(-0.40f, 1.3f, 0f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left)
                .Powered(FigureShape.Rect(-0.53f, 1.115f, -0.43f, 1.175f).And(ring.Offset(0.01f)), p.Powered);

            return new[]
            {
                new RigPart("forearm.far", RigBones.ForearmFar, 0, farForearm),
                new RigPart("upperArm.far", RigBones.UpperArmFar, 1, farUpperArm),
                new RigPart("shin.far", RigBones.ShinFar, 2, Shin(0.34f, 0.60f, 0.38f, 0.58f, false)),
                new RigPart("thigh.far", RigBones.ThighFar, 3, Thigh(0.14f, 0.34f, 0.60f, false)),
                new RigPart("shin.near", RigBones.ShinNear, 4, Shin(-0.26f, 0.58f, -0.36f, -0.16f, true)),
                new RigPart("thigh.near", RigBones.ThighNear, 5, Thigh(-0.08f, -0.26f, 0.58f, true)),
                new RigPart(Torso, RigBones.Chest, 6, torso),
                new RigPart(HeadPart, RigBones.Head, 7, head),
                new RigPart("upperArm.near", RigBones.UpperArmNear, 8, nearUpperArm),
                new RigPart("forearm.near", RigBones.ForearmNear, 9, nearForearm),
            };
        }
    }
}
