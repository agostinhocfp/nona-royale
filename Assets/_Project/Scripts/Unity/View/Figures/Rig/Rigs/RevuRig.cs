// Assets/_Project/Scripts/Unity/View/Figures/Rig/Rigs/RevuRig.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Revú on the rig, in three-quarter view (OPERATOR_LOOKBOOK.md, LB5d):
    /// the three-point barbed hook. The only red torso on the board.
    /// </summary>
    /// <remarks>
    /// Drawn turned toward the right of the screen: tall, thin, all length and
    /// no mass, with a slight forward lean and a long neck. The three points
    /// are the peaked near shoulder, the head carried forward on the neck, and
    /// the ledger case hanging low from the far hand on its chain; the line
    /// between them is concave, so it never reads as Syla's triangle.
    ///
    /// The oxblood double-breasted jacket is darker than the blood-velvet
    /// carpet (§6.1). A sliver of bone shirt and a black tie, brass at the
    /// cuffs, thin black gloves. The near hand rests at the lapel.
    ///
    /// <b>The head pass</b> (ART_PROMPTS, Revú): silver hair combed flat as
    /// one mass with a carved highlight, a long hollow face with one cheek
    /// shadow, heavy lids, a thin smile.
    ///
    /// <b>His own poses.</b> The cast lifts the ledger toward the target on
    /// its chain; the line down its spine is the tell. Seated, he is the only
    /// one with the ledger case open on the table; his activity is turning a
    /// page.
    /// </remarks>
    public static class RevuRig
    {
        public const string Torso = "torso";
        public const string HeadPart = "head";
        public const string Ledger = "ledger";
        public const string LedgerOpen = "ledger.table";

        public static readonly OperatorRig Rig = Build();

        private static OperatorRig Build()
        {
            var skeleton = new RigSkeleton()
                .Bone(RigBones.Root, null, 0.02f, 1.20f)
                .Bone(RigBones.Chest, RigBones.Root, 0.02f, 1.22f)
                .Bone(RigBones.Head, RigBones.Chest, 0.14f, 2.30f)
                .Bone(RigBones.UpperArmNear, RigBones.Chest, -0.30f, 2.22f)
                .Bone(RigBones.ForearmNear, RigBones.UpperArmNear, -0.36f, 1.72f)
                .Bone(RigBones.UpperArmFar, RigBones.Chest, 0.36f, 2.20f)
                .Bone(RigBones.ForearmFar, RigBones.UpperArmFar, 0.44f, 1.72f)
                .Bone(RigBones.ThighNear, RigBones.Root, -0.10f, 1.18f)
                .Bone(RigBones.ShinNear, RigBones.ThighNear, -0.11f, 0.62f)
                .Bone(RigBones.ThighFar, RigBones.Root, 0.14f, 1.18f)
                .Bone(RigBones.ShinFar, RigBones.ThighFar, 0.15f, 0.62f);

            var feet = new[]
            {
                new RigAnchor(RigBones.ShinNear, -0.11f, 0.00f),
                new RigAnchor(RigBones.ShinFar, 0.22f, 0.00f),
            };

            var poses = RigPoses.Template(RigBuild.Light);

            // The ledger lifted toward the target on its chain, the body inclined after it.
            poses[RigPoseNames.Cast] = new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Chest, -5f)
                .Turn(RigBones.Head, -3f)
                .Turn(RigBones.UpperArmFar, 34f)
                .Turn(RigBones.ForearmFar, -6f)
                .Turn(RigBones.UpperArmNear, -4f);

            // The ledger case open on the table, the far hand resting on it.
            var seated = RigPoses.Seated(RigPoseNames.Seated, 0.50f, 0.74f)
                .Turn(RigBones.Chest, -3f)
                .Turn(RigBones.Head, -8f)
                .Turn(RigBones.UpperArmFar, 18f)
                .Turn(RigBones.ForearmFar, 46f)
                .Hide(Ledger)
                .Show(LedgerOpen);
            poses[RigPoseNames.Seated] = seated;
            poses[RigPoseNames.SeatedB] = seated.Copy(RigPoseNames.SeatedB)
                .Turn(RigBones.Chest, -2f, 0f, 0.012f, 1.01f);

            // Turning a page: the near hand leaves the lapel and reaches across.
            poses[RigPoseNames.SeatedLook] = seated.Copy(RigPoseNames.SeatedLook)
                .Turn(RigBones.Head, -11f)
                .Turn(RigBones.UpperArmNear, 40f)
                .Turn(RigBones.ForearmNear, -20f);

            return new OperatorRig("Revú", skeleton, feet, Parts, poses).AimWith(RigBones.UpperArmFar);
        }

        private static IReadOnlyList<RigPart> Parts(LookBookPalette p)
        {
            float line = DecoMotifs.LineWeight;
            float rim = DecoMotifs.RimOnDark;
            const float noFloor = -10f;

            // ── Far arm: long, hanging, the ledger on its chain ──────────
            var farUpperShape = DecoMotifs.Limb(0.36f, 2.20f, 0.095f, 0.44f, 1.72f, 0.08f);
            var farUpperArm = new FigureDrawing(farUpperShape, p.RevuJacket, p.Ink, line)
                .Shade(farUpperShape.Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right | RimEdges.Top);

            var farForeShape = DecoMotifs.Limb(0.44f, 1.72f, 0.08f, 0.50f, 1.30f, 0.065f);
            var farGlove = FigureShape.Ellipse(0.51f, 1.21f, 0.055f, 0.08f);
            var farForearm = new FigureDrawing(farForeShape.Or(farGlove), p.RevuJacket, p.Ink, line)
                .Block(farGlove, p.Obsidian)
                .Block(FigureShape.Rect(0.40f, 1.28f, 0.60f, 1.34f).And(farForeShape), p.Brass)
                .Shade(farForeShape.Or(farGlove).Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right);

            var chain = FigureShape.Polyline(0.51f, 1.16f, 0.53f, 1.02f).Offset(0.014f);
            var case_ = FigureShape.Polygon(0.40f, 1.04f, 0.66f, 1.02f, 0.68f, 0.62f, 0.42f, 0.64f);
            var ledger = new FigureDrawing(case_.Or(chain), p.Obsidian, p.Ink, line)
                .Block(chain, p.Brass)
                .Light(FigureShape.Polygon(0.40f, 1.04f, 0.66f, 1.02f, 0.66f, 0.96f, 0.40f, 0.98f), p.PlateSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right)
                .Powered(FigureShape.Rect(0.515f, 0.66f, 0.555f, 0.96f), p.Powered);

            // Open on the table, lying along it.
            var openShape = FigureShape.Polygon(0.26f, 1.20f, 0.86f, 1.20f, 0.92f, 1.32f, 0.20f, 1.32f);
            var ledgerOpen = new FigureDrawing(openShape, p.Obsidian, p.Ink, line)
                .Block(FigureShape.Polygon(0.28f, 1.23f, 0.54f, 1.23f, 0.54f, 1.30f, 0.24f, 1.30f).Or(FigureShape.Polygon(0.58f, 1.23f, 0.84f, 1.23f, 0.88f, 1.30f, 0.58f, 1.30f)), p.Bone)
                .Shade(FigureShape.Rect(0.56f, 1.2f, 1.0f, 1.34f), p.Shade)
                .InkLine(FigureShape.Polyline(0.56f, 1.20f, 0.56f, 1.32f));

            // ── Legs: long and narrow, no rim ────────────────────────────
            FigureDrawing Shin(float x, float toe, bool near)
            {
                var leg = DecoMotifs.Limb(x, 0.62f, 0.095f, x, 0.14f, 0.08f);
                var shoe = FigureShape.Polygon(x - 0.10f, 0.14f, x + 0.07f, 0.14f, toe, 0.03f, toe, 0.00f, x - 0.11f, 0.00f);
                var d = new FigureDrawing(leg.Or(shoe), p.RevuTrousers, p.Ink, line)
                    .Block(shoe, p.Obsidian)
                    .Light(FigureShape.Rect(x - 0.095f, 0.16f, x - 0.05f, 0.62f).And(leg), p.PlateSheen);
                return near ? d : d.Shade(leg.Or(shoe).Offset(0.1f), p.Shade);
            }

            FigureDrawing Thigh(float x, bool near)
            {
                var shape = DecoMotifs.Limb(x, 1.20f, 0.11f, x - 0.01f, 0.62f, 0.095f);
                var d = new FigureDrawing(shape, p.RevuTrousers, p.Ink, line);
                return near ? d : d.Shade(shape.Offset(0.1f), p.Shade);
            }

            // ── Torso: peaked, narrow, the tails to the hip ──────────────
            var torsoShape = FigureShape.Polygon(
                -0.52f, 2.40f,     // the peaked near shoulder
                -0.14f, 2.28f,
                0.34f, 2.28f,
                0.50f, 2.20f,
                0.40f, 1.70f,
                0.34f, 1.06f,
                -0.20f, 1.08f,
                -0.30f, 1.70f,
                -0.40f, 2.14f);
            var shirt = FigureShape.Polygon(0.08f, 2.28f, 0.22f, 2.28f, 0.18f, 1.88f, 0.10f, 1.88f);
            var tie = FigureShape.Polygon(0.13f, 2.26f, 0.17f, 2.26f, 0.17f, 1.92f, 0.13f, 1.92f);
            var nearLapel = FigureShape.Polygon(0.08f, 2.28f, -0.26f, 2.22f, -0.06f, 2.06f, 0.10f, 1.76f);
            var buttons = FigureShape.Circle(-0.02f, 1.62f, 0.022f).Or(FigureShape.Circle(0.20f, 1.60f, 0.02f))
                .Or(FigureShape.Circle(-0.02f, 1.44f, 0.022f)).Or(FigureShape.Circle(0.20f, 1.42f, 0.02f));
            var sidePlane = FigureShape.HalfPlane(0.24f, 1.7f, -1f, 0.08f);

            var torso = new FigureDrawing(torsoShape, p.RevuJacket, p.Ink, line)
                .Block(shirt, p.Bone)
                .Block(tie, p.Obsidian)
                .Block(buttons, p.Brass)
                .Block(FigureShape.Circle(0.15f, 2.27f, 0.018f), p.Brass)       // the collar stud
                .Shade(sidePlane.Minus(shirt), p.Shade)
                .Light(nearLapel, p.Key)
                .Light(FigureShape.Polygon(-0.52f, 2.40f, -0.14f, 2.28f, -0.14f, 2.22f, -0.46f, 2.30f).And(torsoShape), p.Key)
                .Rim(rim, p.Rim, 1.1f, RimEdges.Top | RimEdges.Left | RimEdges.Right)
                .InkLine(FigureShape.Polyline(0.10f, 1.76f, 0.30f, 1.08f))
                .InkLine(FigureShape.Polyline(0.22f, 2.28f, 0.34f, 2.08f, 0.20f, 1.84f));

            // ── Head: long and hollow, carried forward on the long neck ──
            var neck = FigureShape.Polygon(0.04f, 2.20f, 0.24f, 2.20f, 0.30f, 2.50f, 0.12f, 2.52f);
            var skull = FigureShape.Ellipse(0.24f, 2.68f, 0.14f, 0.21f);
            var jaw = FigureShape.Polygon(0.16f, 2.56f, 0.38f, 2.58f, 0.34f, 2.46f, 0.20f, 2.44f);
            var nose = FigureShape.Polygon(0.36f, 2.72f, 0.43f, 2.62f, 0.37f, 2.58f);
            var headShape = FigureShape.Union(neck, skull, jaw, nose);
            var hair = skull.Offset(0.012f).And(FigureShape.Polygon(0.06f, 2.92f, 0.42f, 2.92f, 0.38f, 2.78f, 0.22f, 2.76f, 0.14f, 2.58f, 0.06f, 2.58f));

            var head = new FigureDrawing(headShape.Or(hair), p.RevuSkin, p.Ink, line)
                .Block(hair, p.RevuHair)
                .Shade(DecoMotifs.Crescent(0.24f, 2.66f, 0.19f, -0.10f, 0.05f).Or(nose).Minus(hair), p.Shade)
                .Shade(FigureShape.Polygon(0.26f, 2.60f, 0.34f, 2.58f, 0.30f, 2.50f), p.Shade)             // the hollow cheek
                .Shade(FigureShape.Polygon(0.04f, 2.20f, 0.24f, 2.20f, 0.26f, 2.36f, 0.08f, 2.38f), p.Shade)
                .Light(FigureShape.Polygon(0.10f, 2.82f, 0.20f, 2.88f, 0.34f, 2.89f, 0.16f, 2.80f).And(hair), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor)
                .InkLine(FigureShape.Polyline(0.30f, 2.70f, 0.38f, 2.70f))
                .InkLine(FigureShape.Polyline(0.29f, 2.52f, 0.34f, 2.51f, 0.37f, 2.53f));

            // ── Near arm: the hand at the lapel ──────────────────────────
            var nearUpperShape = DecoMotifs.Limb(-0.30f, 2.22f, 0.10f, -0.36f, 1.72f, 0.085f);
            var nearUpperArm = new FigureDrawing(nearUpperShape, p.RevuJacket, p.Ink, line)
                .Light(FigureShape.Rect(-0.42f, 1.76f, -0.35f, 2.18f).And(nearUpperShape), p.Key)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left | RimEdges.Top);

            var nearForeShape = DecoMotifs.Limb(-0.36f, 1.72f, 0.085f, -0.06f, 1.86f, 0.07f);
            var nearGlove = FigureShape.Ellipse(0.00f, 1.89f, 0.07f, 0.06f);
            var nearForearm = new FigureDrawing(nearForeShape.Or(nearGlove), p.RevuJacket, p.Ink, line)
                .Block(nearGlove, p.Obsidian)
                .Block(FigureShape.Polygon(-0.11f, 1.80f, -0.05f, 1.83f, -0.09f, 1.95f, -0.15f, 1.92f).And(nearForeShape), p.Brass)
                .Shade(FigureShape.HalfPlane(0f, 1.76f, 0f, 1f).And(nearForeShape), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left | RimEdges.Top);

            return new[]
            {
                new RigPart(Ledger, RigBones.ForearmFar, 0, ledger),
                new RigPart("forearm.far", RigBones.ForearmFar, 1, farForearm),
                new RigPart("upperArm.far", RigBones.UpperArmFar, 2, farUpperArm),
                new RigPart("shin.far", RigBones.ShinFar, 3, Shin(0.15f, 0.34f, false)),
                new RigPart("thigh.far", RigBones.ThighFar, 4, Thigh(0.15f, false)),
                new RigPart("shin.near", RigBones.ShinNear, 5, Shin(-0.11f, 0.08f, true)),
                new RigPart("thigh.near", RigBones.ThighNear, 6, Thigh(-0.10f, true)),
                new RigPart(Torso, RigBones.Chest, 7, torso),
                new RigPart(HeadPart, RigBones.Head, 8, head),
                new RigPart("upperArm.near", RigBones.UpperArmNear, 9, nearUpperArm),
                new RigPart("forearm.near", RigBones.ForearmNear, 10, nearForearm),
                new RigPart(LedgerOpen, RigBones.Root, 11, ledgerOpen, prop: true),
            };
        }
    }
}
