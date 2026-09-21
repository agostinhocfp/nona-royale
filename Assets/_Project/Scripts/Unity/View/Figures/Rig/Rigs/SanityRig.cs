// Assets/_Project/Scripts/Unity/View/Figures/Rig/Rigs/SanityRig.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Sanity on the rig, in three-quarter view (OPERATOR_LOOKBOOK.md, LB5d):
    /// the octagon. A large mid-brown mass, the umber apron, with a brass line
    /// across it.
    /// </summary>
    /// <remarks>
    /// Drawn turned toward the right of the screen. The apron is one chamfered
    /// octagon from the shoulders to just above the boots, its front face to
    /// the camera and its side plane receding in shadow on the right, so he
    /// stays a block and never the slab Bouncer is. The charcoal livery shows
    /// at the shoulders and in the sleeves; the forearms are bare; the tool
    /// roll is the brass line, running across him with two buckles.
    ///
    /// <b>The prod</b> is in his near hand, hanging along his leg at rest
    /// ("slung at his right hip like a sidearm"), so the cast only has to lift
    /// the arm to point it. The arc across its head is the tell.
    ///
    /// <b>The head pass</b> (ART_PROMPTS, Sanity): a small heavy shaved head
    /// sunk into a thick neck, a flat patient face, one brow shadow.
    ///
    /// <b>His own poses.</b> The cast raises the prod level at the target,
    /// feet planted. Seated, he doesn't fit the chair: elbows wide on the
    /// table. His activity is cleaning the prod, the near forearm working and
    /// the head bent over it.
    /// </remarks>
    public static class SanityRig
    {
        public const string Torso = "torso";
        public const string HeadPart = "head";
        public const string Prod = "prod";

        public static readonly OperatorRig Rig = Build();

        private static OperatorRig Build()
        {
            var skeleton = new RigSkeleton()
                .Bone(RigBones.Root, null, 0.00f, 0.90f)
                .Bone(RigBones.Chest, RigBones.Root, 0.00f, 0.92f)
                .Bone(RigBones.Head, RigBones.Chest, 0.06f, 2.30f)
                .Bone(RigBones.UpperArmNear, RigBones.Chest, -0.84f, 1.90f)
                .Bone(RigBones.ForearmNear, RigBones.UpperArmNear, -0.94f, 1.46f)
                .Bone(RigBones.UpperArmFar, RigBones.Chest, 0.70f, 1.90f)
                .Bone(RigBones.ForearmFar, RigBones.UpperArmFar, 0.78f, 1.46f)
                .Bone(RigBones.ThighNear, RigBones.Root, -0.28f, 0.84f)
                .Bone(RigBones.ShinNear, RigBones.ThighNear, -0.30f, 0.42f)
                .Bone(RigBones.ThighFar, RigBones.Root, 0.30f, 0.84f)
                .Bone(RigBones.ShinFar, RigBones.ThighFar, 0.32f, 0.42f);

            var feet = new[]
            {
                new RigAnchor(RigBones.ShinNear, -0.30f, 0.00f),
                new RigAnchor(RigBones.ShinFar, 0.40f, 0.00f),
            };

            var poses = RigPoses.Template(RigBuild.Heavy);

            // The prod raised level at the target, the body set behind it.
            poses[RigPoseNames.Cast] = new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Chest, -3f)
                .Turn(RigBones.Head, -2f)
                .Turn(RigBones.UpperArmNear, 74f)
                .Turn(RigBones.ForearmNear, 14f)
                .Turn(RigBones.UpperArmFar, -6f);

            // Elbows wide on the table: both forearms laid on the felt.
            var seated = RigPoses.Seated(RigPoseNames.Seated, 0.40f, 0.70f)
                .Turn(RigBones.Head, -2f)
                .Turn(RigBones.UpperArmNear, 34f)
                .Turn(RigBones.ForearmNear, 56f)
                .Turn(RigBones.UpperArmFar, 8f)
                .Turn(RigBones.ForearmFar, 42f);
            poses[RigPoseNames.Seated] = seated;
            poses[RigPoseNames.SeatedB] = seated.Copy(RigPoseNames.SeatedB)
                .Turn(RigBones.Chest, 1f, 0f, 0.012f, 1.01f);

            // Cleaning the prod: the near forearm working, the head bent over it.
            poses[RigPoseNames.SeatedLook] = seated.Copy(RigPoseNames.SeatedLook)
                .Turn(RigBones.Chest, -3f)
                .Turn(RigBones.Head, -9f)
                .Turn(RigBones.UpperArmNear, 30f)
                .Turn(RigBones.ForearmNear, 76f)
                .Turn(RigBones.ForearmFar, 50f);

            return new OperatorRig("Sanity", skeleton, feet, Parts, poses);
        }

        private static IReadOnlyList<RigPart> Parts(LookBookPalette p)
        {
            float line = DecoMotifs.LineWeight;
            float rim = DecoMotifs.RimOnDark;
            const float noFloor = -10f;

            // ── Far arm: behind the mass, in its shadow ──────────────────
            var farUpperShape = DecoMotifs.Limb(0.70f, 1.90f, 0.22f, 0.78f, 1.46f, 0.19f);
            var farUpperArm = new FigureDrawing(farUpperShape, p.SanityLivery, p.Ink, line)
                .Shade(farUpperShape.Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right);

            var farForeShape = DecoMotifs.Limb(0.78f, 1.46f, 0.19f, 0.78f, 1.04f, 0.16f);
            var farHand = FigureShape.Ellipse(0.78f, 0.92f, 0.17f, 0.16f);
            var farForearm = new FigureDrawing(farForeShape.Or(farHand), p.SanitySkin, p.Ink, line)
                .Block(FigureShape.Rect(0.54f, 1.36f, 1.04f, 1.50f).And(farForeShape), p.SanityLivery)
                .Shade(farForeShape.Or(farHand).Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right);

            // ── Legs: all but the boots under the apron ─────────────────
            var nearShinShape = DecoMotifs.Limb(-0.28f, 0.42f, 0.20f, -0.28f, 0.24f, 0.18f);
            var nearBoot = FigureShape.Polygon(-0.50f, 0.26f, -0.08f, 0.26f, -0.04f, 0.10f, 0.00f, 0.00f, -0.52f, 0.00f);
            var nearShin = new FigureDrawing(nearShinShape.Or(nearBoot), p.SanityLivery, p.Ink, line)
                .Block(nearBoot, p.Obsidian)
                .Light(FigureShape.Polygon(-0.48f, 0.24f, -0.34f, 0.24f, -0.34f, 0.14f, -0.50f, 0.12f), p.PlateSheen)
                .InkLine(FigureShape.Polyline(-0.51f, 0.08f, -0.02f, 0.08f));

            var nearThighShape = DecoMotifs.Limb(-0.28f, 0.84f, 0.27f, -0.30f, 0.42f, 0.24f);
            var nearThigh = new FigureDrawing(nearThighShape, p.SanityLivery, p.Ink, line)
                .Shade(DecoMotifs.ShadowSide(-0.20f, 0.70f, 0f), p.Shade);

            var farShinShape = DecoMotifs.Limb(0.32f, 0.42f, 0.20f, 0.32f, 0.24f, 0.18f);
            var farBoot = FigureShape.Polygon(0.10f, 0.26f, 0.52f, 0.26f, 0.58f, 0.10f, 0.66f, 0.00f, 0.08f, 0.00f);
            var farShin = new FigureDrawing(farShinShape.Or(farBoot), p.SanityLivery, p.Ink, line)
                .Block(farBoot, p.Obsidian)
                .Shade(farShinShape.Or(farBoot).Offset(0.1f), p.Shade)
                .InkLine(FigureShape.Polyline(0.08f, 0.08f, 0.62f, 0.08f));

            var farThighShape = DecoMotifs.Limb(0.30f, 0.84f, 0.27f, 0.32f, 0.42f, 0.24f);
            var farThigh = new FigureDrawing(farThighShape, p.SanityLivery, p.Ink, line)
                .Shade(farThighShape.Offset(0.1f), p.Shade);

            // ── Torso: the octagon, turned ───────────────────────────────
            // Down to the boots, the arms inside its outline: one block, no gaps.
            var mass = FigureShape.Polygon(
                -0.38f, 2.40f,
                0.26f, 2.40f,
                1.02f, 1.76f,
                1.02f, 0.92f,
                0.40f, 0.28f,
                -0.52f, 0.28f,
                -1.24f, 0.92f,
                -1.24f, 1.72f);

            // The bib's top edge: the livery shows above it at the shoulders.
            var bib = FigureShape.Polygon(-0.30f, 2.40f, 0.36f, 2.40f, 0.46f, 1.60f, -0.44f, 1.60f);
            var livery = mass.And(FigureShape.HalfPlane(0f, 1.62f, 0f, -1f)).Minus(bib);

            // The tool roll: the brass line across him, high on the near side.
            var roll = FigureShape.Polygon(-1.24f, 1.86f, 1.02f, 1.64f, 1.02f, 1.52f, -1.24f, 1.74f).And(mass);
            var buckles = FigureShape.Rect(-0.52f, 1.70f, -0.40f, 1.86f).Or(FigureShape.Rect(0.36f, 1.60f, 0.48f, 1.76f));
            var tools = FigureShape.Union(
                FigureShape.Rect(-0.26f, 1.90f, -0.20f, 2.04f),
                FigureShape.Rect(-0.12f, 1.88f, -0.06f, 2.00f),
                FigureShape.Rect(0.04f, 1.86f, 0.10f, 2.02f));

            // The Deco furniture: a stepped hem across the apron.
            var hem = DecoMotifs.Chevron(0.68f, 0.84f, 0.12f, 0.04f).Translate(-0.10f, 0f).And(mass);
            var sidePlane = FigureShape.HalfPlane(0.56f, 1.4f, -1f, 0.04f);

            var torso = new FigureDrawing(mass, p.SanityApron, p.Ink, line)
                .Block(livery, p.SanityLivery)
                .Block(tools, p.SanitySteel)
                .Block(roll, p.Brass)
                .Block(buckles, p.Obsidian)
                .Block(hem, p.SanityLivery)
                .Shade(sidePlane, p.Shade)
                .Light(FigureShape.Polygon(-0.38f, 2.40f, -1.24f, 1.72f, -1.24f, 1.60f, -0.38f, 2.28f, 0.26f, 2.30f, 0.26f, 2.40f).Minus(roll), p.SuitSheen)
                .Light(FigureShape.Polygon(-0.50f, 1.56f, -0.36f, 1.56f, -0.36f, 0.32f, -0.50f, 0.40f).And(mass), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Top | RimEdges.Right)
                .InkLine(FigureShape.Polyline(0.56f, 2.10f, 0.54f, 0.36f));

            // ── Head: small, heavy, sunk into the neck ───────────────────
            var neck = FigureShape.Polygon(-0.16f, 2.20f, 0.32f, 2.20f, 0.30f, 2.46f, -0.14f, 2.46f);
            var skull = FigureShape.Ellipse(0.10f, 2.58f, 0.21f, 0.23f);
            var nose = FigureShape.Polygon(0.29f, 2.62f, 0.36f, 2.54f, 0.30f, 2.50f);
            var headShape = FigureShape.Union(neck, skull, nose);
            var ear = FigureShape.Ellipse(-0.04f, 2.56f, 0.045f, 0.07f);
            var brow = FigureShape.Polygon(0.14f, 2.64f, 0.31f, 2.66f, 0.31f, 2.60f, 0.16f, 2.59f);

            var head = new FigureDrawing(headShape, p.SanitySkin, p.Ink, line)
                .Shade(DecoMotifs.Crescent(0.10f, 2.58f, 0.25f, -0.12f, 0.07f).Or(nose), p.Shade)
                .Shade(FigureShape.Rect(-0.16f, 2.20f, 0.32f, 2.36f).And(neck), p.Shade)
                .Shade(brow, p.Shade)
                .Shade(ear, p.Shade)
                .Light(DecoMotifs.Crescent(0.10f, 2.58f, 0.21f, 0.08f, -0.07f).And(FigureShape.HalfPlane(0f, 2.56f, 0f, -1f)), p.Key)
                .Rim(rim, p.Rim, noFloor)
                .InkLine(FigureShape.Polyline(-0.06f, 2.50f, -0.02f, 2.62f))
                .InkLine(FigureShape.Polyline(0.18f, 2.47f, 0.28f, 2.47f));

            // ── Near arm: sleeve, bare forearm, the prod in hand ─────────
            var nearUpperShape = DecoMotifs.Limb(-0.84f, 1.90f, 0.24f, -0.94f, 1.46f, 0.21f);
            var nearUpperArm = new FigureDrawing(nearUpperShape, p.SanityLivery, p.Ink, line)
                .Shade(DecoMotifs.ShadowSide(-0.88f, 1.7f, 0.06f), p.Shade)
                .Light(FigureShape.Polygon(-1.10f, 1.96f, -0.98f, 2.02f, -1.06f, 1.50f, -1.14f, 1.50f).And(nearUpperShape), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left | RimEdges.Top);

            var nearForeShape = DecoMotifs.Limb(-0.94f, 1.46f, 0.21f, -0.96f, 1.04f, 0.18f);
            var nearHand = FigureShape.Ellipse(-0.96f, 0.90f, 0.19f, 0.17f);
            var nearForearm = new FigureDrawing(nearForeShape.Or(nearHand), p.SanitySkin, p.Ink, line)
                .Block(FigureShape.Rect(-1.26f, 1.36f, -0.66f, 1.52f).And(nearForeShape), p.SanityLivery)
                .Shade(DecoMotifs.ShadowSide(-0.90f, 1.2f, 0f), p.Shade)
                .Light(FigureShape.Polygon(-1.16f, 1.34f, -1.06f, 1.34f, -1.08f, 1.00f, -1.16f, 1.00f).And(nearForeShape), p.Key)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left)
                .InkLine(FigureShape.Polyline(-1.08f, 0.88f, -0.84f, 0.88f));

            // Held along the leg at rest: steel shaft, brass grip in the fist, the arc at its head.
            var prodShape = FigureShape.Union(
                FigureShape.Rect(-1.01f, 0.18f, -0.91f, 0.92f),
                FigureShape.Rect(-1.05f, 0.14f, -0.87f, 0.24f));
            var prod = new FigureDrawing(prodShape, p.SanitySteel, p.Ink, line)
                .Block(FigureShape.Rect(-1.01f, 0.70f, -0.91f, 0.92f), p.Brass)
                .Shade(DecoMotifs.ShadowSide(-0.96f, 0.5f, 0f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left)
                .Powered(FigureShape.Rect(-1.04f, 0.17f, -0.88f, 0.21f), p.Powered);

            return new[]
            {
                new RigPart("forearm.far", RigBones.ForearmFar, 0, farForearm),
                new RigPart("upperArm.far", RigBones.UpperArmFar, 1, farUpperArm),
                new RigPart("shin.far", RigBones.ShinFar, 2, farShin),
                new RigPart("thigh.far", RigBones.ThighFar, 3, farThigh),
                new RigPart("shin.near", RigBones.ShinNear, 4, nearShin),
                new RigPart("thigh.near", RigBones.ThighNear, 5, nearThigh),
                new RigPart(Torso, RigBones.Chest, 6, torso),
                new RigPart(HeadPart, RigBones.Head, 7, head),
                new RigPart(Prod, RigBones.ForearmNear, 8, prod),
                new RigPart("upperArm.near", RigBones.UpperArmNear, 9, nearUpperArm),
                new RigPart("forearm.near", RigBones.ForearmNear, 10, nearForearm),
            };
        }
    }
}
