// Assets/_Project/Scripts/Unity/View/Figures/Rig/Rigs/KianRig.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Kian on the rig, in three-quarter view (OPERATOR_LOOKBOOK.md, LB5d):
    /// the five-point spiked crown. The only green torso, and the only figure
    /// whose outline breaks upward.
    /// </summary>
    /// <remarks>
    /// Drawn turned toward the right of the screen: tall and narrow,
    /// round-shouldered and hunched, the head carried low and forward, long
    /// thin limbs, no mass at all. The crown is the rack on his back: four
    /// emitter rods rising past his shoulders, splayed, the two on the back
    /// side (the camera's left) taller and more visible, and his head the
    /// fifth point between them. The rod tips are the tell.
    ///
    /// The emerald smoking jacket is lit with the warm key, never a cool
    /// sheen, so it stays out of the cyan register (§3). A shawl collar,
    /// tarnished frogging, the black shirt to the throat, the disrupter disc
    /// at the sternum.
    ///
    /// <b>The head pass</b> (ART_PROMPTS, Kian): cropped receding dark hair,
    /// a long face, small round tinted spectacles in thin brass rims, eyes not
    /// quite on you.
    ///
    /// <b>His own poses.</b> The cast raises the near arm high, directing
    /// fire. Seated, he is turned half away from the table with an emitter
    /// rod across his knees; his activity is adjusting it.
    /// </remarks>
    public static class KianRig
    {
        public const string Torso = "torso";
        public const string HeadPart = "head";
        public const string Rack = "rack";
        public const string RodInHand = "rod.hand";

        public static readonly OperatorRig Rig = Build();

        private static OperatorRig Build()
        {
            var skeleton = new RigSkeleton()
                .Bone(RigBones.Root, null, 0.02f, 1.14f)
                .Bone(RigBones.Chest, RigBones.Root, 0.02f, 1.16f)
                .Bone(RigBones.Head, RigBones.Chest, 0.18f, 2.10f)
                .Bone(RigBones.UpperArmNear, RigBones.Chest, -0.30f, 2.00f)
                .Bone(RigBones.ForearmNear, RigBones.UpperArmNear, -0.34f, 1.50f)
                .Bone(RigBones.UpperArmFar, RigBones.Chest, 0.40f, 1.98f)
                .Bone(RigBones.ForearmFar, RigBones.UpperArmFar, 0.44f, 1.50f)
                .Bone(RigBones.ThighNear, RigBones.Root, -0.10f, 1.12f)
                .Bone(RigBones.ShinNear, RigBones.ThighNear, -0.11f, 0.58f)
                .Bone(RigBones.ThighFar, RigBones.Root, 0.16f, 1.12f)
                .Bone(RigBones.ShinFar, RigBones.ThighFar, 0.17f, 0.58f);

            var feet = new[]
            {
                new RigAnchor(RigBones.ShinNear, -0.11f, 0.00f),
                new RigAnchor(RigBones.ShinFar, 0.24f, 0.00f),
            };

            var poses = RigPoses.Template(RigBuild.Light);

            // The near arm raised high and forward, directing fire; the head comes up.
            poses[RigPoseNames.Cast] = new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Chest, 3f)
                .Turn(RigBones.Head, 8f)
                .Turn(RigBones.UpperArmNear, 128f)
                .Turn(RigBones.ForearmNear, -18f)
                .Turn(RigBones.UpperArmFar, -4f);

            // Turned half away from the table, a rod across his knees.
            var seated = RigPoses.Seated(RigPoseNames.Seated, 0.46f, 0.72f)
                .Turn(RigBones.Chest, 5f)
                .Turn(RigBones.Head, -12f)
                .Turn(RigBones.UpperArmNear, 22f)
                .Turn(RigBones.ForearmNear, 64f)
                .Turn(RigBones.UpperArmFar, 14f)
                .Turn(RigBones.ForearmFar, 58f)
                .Show(RodInHand);
            poses[RigPoseNames.Seated] = seated;
            poses[RigPoseNames.SeatedB] = seated.Copy(RigPoseNames.SeatedB)
                .Turn(RigBones.Chest, 6f, 0f, 0.012f, 1.01f);

            // Adjusting it: the near forearm lifts the rod to his eye.
            poses[RigPoseNames.SeatedLook] = seated.Copy(RigPoseNames.SeatedLook)
                .Turn(RigBones.Head, -4f)
                .Turn(RigBones.UpperArmNear, 30f)
                .Turn(RigBones.ForearmNear, 92f);

            return new OperatorRig("Kian", skeleton, feet, Parts, poses);
        }

        private static IReadOnlyList<RigPart> Parts(LookBookPalette p)
        {
            float line = DecoMotifs.LineWeight;
            float rim = DecoMotifs.RimOnDark;
            const float noFloor = -10f;

            // ── The rack on his back: four rods, the crown ──────────────
            var rods = FigureShape.Union(
                DecoMotifs.Limb(-0.20f, 1.96f, 0.035f, -0.34f, 2.80f, 0.03f),
                DecoMotifs.Limb(-0.32f, 1.94f, 0.035f, -0.72f, 2.62f, 0.03f),
                DecoMotifs.Limb(0.26f, 1.96f, 0.03f, 0.46f, 2.66f, 0.026f),
                DecoMotifs.Limb(0.34f, 1.94f, 0.03f, 0.70f, 2.46f, 0.026f));
            var tips = FigureShape.Union(
                FigureShape.Circle(-0.34f, 2.82f, 0.05f),
                FigureShape.Circle(-0.72f, 2.64f, 0.05f),
                FigureShape.Circle(0.46f, 2.68f, 0.042f),
                FigureShape.Circle(0.70f, 2.48f, 0.042f));
            var rack = new FigureDrawing(rods.Or(tips), p.KianShirt, p.Ink, line)
                .Light(FigureShape.HalfPlane(-0.30f, 2.2f, 1f, 0f), p.PlateSheen)
                .Rim(rim, p.Rim, 2.0f, RimEdges.Left | RimEdges.Top)
                .Powered(tips.Offset(-0.012f), p.Powered);

            // ── Far arm: long and thin, gloved ───────────────────────────
            var farUpperShape = DecoMotifs.Limb(0.40f, 1.98f, 0.085f, 0.44f, 1.50f, 0.075f);
            var farUpperArm = new FigureDrawing(farUpperShape, p.KianJacket, p.Ink, line)
                .Shade(farUpperShape.Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right);

            var farForeShape = DecoMotifs.Limb(0.44f, 1.50f, 0.075f, 0.46f, 1.12f, 0.065f);
            var farGlove = FigureShape.Ellipse(0.47f, 1.03f, 0.055f, 0.08f);
            var farForearm = new FigureDrawing(farForeShape.Or(farGlove), p.KianJacket, p.Ink, line)
                .Block(farGlove, p.KianShirt)
                .Shade(farForeShape.Or(farGlove).Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right);

            // ── Legs: long, narrow, no rim ───────────────────────────────
            FigureDrawing Shin(float x, float toe, bool near)
            {
                var leg = DecoMotifs.Limb(x, 0.58f, 0.09f, x, 0.14f, 0.075f);
                var shoe = FigureShape.Polygon(x - 0.10f, 0.14f, x + 0.07f, 0.14f, toe, 0.03f, toe, 0.00f, x - 0.11f, 0.00f);
                var d = new FigureDrawing(leg.Or(shoe), p.KianShirt, p.Ink, line)
                    .Block(shoe, p.Obsidian)
                    .Light(FigureShape.Rect(x - 0.09f, 0.16f, x - 0.045f, 0.58f).And(leg), p.PlateSheen);
                return near ? d : d.Shade(leg.Or(shoe).Offset(0.1f), p.Shade);
            }

            FigureDrawing Thigh(float x, bool near)
            {
                var shape = DecoMotifs.Limb(x, 1.14f, 0.11f, x - 0.01f, 0.58f, 0.09f);
                var d = new FigureDrawing(shape, p.KianShirt, p.Ink, line);
                return near ? d : d.Shade(shape.Offset(0.1f), p.Shade);
            }

            // ── Torso: round-shouldered, hunched, the emerald jacket ─────
            var torsoShape = FigureShape.Polygon(
                -0.20f, 2.12f,
                0.30f, 2.10f,
                0.50f, 1.98f,      // the far shoulder, rounded
                0.46f, 1.60f,
                0.34f, 1.06f,
                -0.22f, 1.06f,
                -0.30f, 1.60f,
                -0.40f, 1.96f);
            var shirt = FigureShape.Polygon(0.06f, 2.10f, 0.26f, 2.10f, 0.20f, 1.56f, 0.10f, 1.56f);
            var shawl = FigureShape.Polygon(-0.08f, 2.10f, 0.06f, 2.10f, 0.10f, 1.56f, 0.02f, 1.50f, -0.10f, 1.80f);
            var disc = FigureShape.Circle(0.15f, 1.84f, 0.07f);
            var holster = FigureShape.Rect(-0.20f, 1.62f, -0.04f, 1.80f);
            FigureShape frogging = null;
            for (int i = 0; i < 3; i++)
            {
                float y = 1.46f - i * 0.12f;
                var bar = FigureShape.Rect(-0.06f, y, 0.26f, y + 0.03f);
                frogging = frogging == null ? bar : frogging.Or(bar);
            }

            var sidePlane = FigureShape.HalfPlane(0.26f, 1.6f, -1f, 0.08f);

            var torso = new FigureDrawing(torsoShape, p.KianJacket, p.Ink, line)
                .Block(shirt, p.KianShirt)
                .Block(disc.Or(holster), p.Obsidian)
                .Block(frogging, p.Brass)
                .Shade(sidePlane.Minus(shirt).Minus(disc), p.Shade)
                // The warm key, not a cool sheen: screened cool, emerald drifts into cyan (§3).
                .Light(shawl, p.Key)
                .Light(FigureShape.Polygon(-0.40f, 1.96f, -0.20f, 2.12f, -0.14f, 2.10f, -0.30f, 1.90f).And(torsoShape), p.Key)
                .Rim(rim, p.Rim, 1.1f, RimEdges.Top | RimEdges.Right)
                .InkLine(FigureShape.Polyline(0.10f, 1.56f, 0.18f, 1.08f));

            // ── Head: low and forward, the round tinted spectacles ───────
            var neck = FigureShape.Polygon(0.06f, 2.04f, 0.26f, 2.04f, 0.30f, 2.24f, 0.10f, 2.26f);
            var skull = FigureShape.Ellipse(0.26f, 2.40f, 0.14f, 0.20f);
            var nose = FigureShape.Polygon(0.38f, 2.42f, 0.45f, 2.32f, 0.39f, 2.29f);
            var headShape = FigureShape.Union(neck, skull, nose);
            var hair = skull.Offset(0.012f).And(FigureShape.Polygon(0.08f, 2.62f, 0.34f, 2.62f, 0.28f, 2.54f, 0.18f, 2.46f, 0.12f, 2.30f, 0.08f, 2.30f));
            var rims = FigureShape.Circle(0.34f, 2.40f, 0.05f).Or(FigureShape.Polyline(0.20f, 2.42f, 0.30f, 2.41f).Offset(0.008f));

            var head = new FigureDrawing(headShape.Or(hair), p.KianSkin, p.Ink, line)
                .Block(hair, p.KianShirt)
                .Block(rims, p.Brass)
                .Block(FigureShape.Circle(0.34f, 2.40f, 0.032f), p.KianShirt)
                .Shade(DecoMotifs.Crescent(0.26f, 2.38f, 0.19f, -0.10f, 0.05f).Or(nose).Minus(hair).Minus(rims), p.Shade)
                .Shade(FigureShape.Polygon(0.06f, 2.04f, 0.26f, 2.04f, 0.28f, 2.16f, 0.08f, 2.18f), p.Shade)
                .Light(FigureShape.Polygon(0.12f, 2.52f, 0.20f, 2.58f, 0.30f, 2.58f, 0.16f, 2.50f).And(hair), p.PlateSheen)
                .Rim(rim, p.Rim, noFloor)
                .InkLine(FigureShape.Polyline(0.32f, 2.26f, 0.38f, 2.26f));

            // ── Near arm: long and thin, gloved ──────────────────────────
            var nearUpperShape = DecoMotifs.Limb(-0.30f, 2.00f, 0.09f, -0.34f, 1.50f, 0.08f);
            var nearUpperArm = new FigureDrawing(nearUpperShape, p.KianJacket, p.Ink, line)
                .Light(FigureShape.Rect(-0.40f, 1.54f, -0.34f, 1.96f).And(nearUpperShape), p.Key)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left | RimEdges.Top);

            var nearForeShape = DecoMotifs.Limb(-0.34f, 1.50f, 0.08f, -0.36f, 1.12f, 0.07f);
            var nearGlove = FigureShape.Ellipse(-0.37f, 1.03f, 0.06f, 0.085f);
            var nearForearm = new FigureDrawing(nearForeShape.Or(nearGlove), p.KianJacket, p.Ink, line)
                .Block(nearGlove, p.KianShirt)
                .Light(FigureShape.Rect(-0.41f, 1.14f, -0.36f, 1.48f).And(nearForeShape), p.Key)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left);

            // A rod across his knees at the table, held in the near hand.
            // Drawn upright at rest so it lies level once the seated forearm has turned onto the knees.
            var rodShape = DecoMotifs.Limb(-0.39f, 0.70f, 0.03f, -0.36f, 1.40f, 0.03f).Or(FigureShape.Circle(-0.36f, 1.42f, 0.045f));
            var rodInHand = new FigureDrawing(rodShape, p.KianShirt, p.Ink, line)
                .Light(FigureShape.HalfPlane(-0.37f, 1.0f, 1f, 0f), p.PlateSheen);

            return new[]
            {
                new RigPart(Rack, RigBones.Chest, 0, rack),
                new RigPart("forearm.far", RigBones.ForearmFar, 1, farForearm),
                new RigPart("upperArm.far", RigBones.UpperArmFar, 2, farUpperArm),
                new RigPart("shin.far", RigBones.ShinFar, 3, Shin(0.17f, 0.34f, false)),
                new RigPart("thigh.far", RigBones.ThighFar, 4, Thigh(0.17f, false)),
                new RigPart("shin.near", RigBones.ShinNear, 5, Shin(-0.11f, 0.06f, true)),
                new RigPart("thigh.near", RigBones.ThighNear, 6, Thigh(-0.10f, true)),
                new RigPart(Torso, RigBones.Chest, 7, torso),
                new RigPart(HeadPart, RigBones.Head, 8, head),
                new RigPart("upperArm.near", RigBones.UpperArmNear, 9, nearUpperArm),
                new RigPart("forearm.near", RigBones.ForearmNear, 10, nearForearm),
                new RigPart(RodInHand, RigBones.ForearmNear, 11, rodInHand, prop: true),
            };
        }
    }
}
