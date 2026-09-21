// Assets/_Project/Scripts/Unity/View/Figures/Rig/Rigs/BouncerRig.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Bouncer on the rig, in three-quarter view (OPERATOR_LOOKBOOK.md, LB5a):
    /// the black slab split by the white V, the aged-brass gauntlet on his
    /// right arm, which faces the camera and so is the near arm.
    /// </summary>
    /// <remarks>
    /// Drawn turned toward the right of the screen. The front of the jacket
    /// faces the camera and the side plane recedes on the right, in the key's
    /// shadow; the V and the bow tie sit off-centre toward the facing side.
    ///
    /// <b>The head pass</b> (ART_PROMPTS, Bouncer): a shaved oval with one
    /// carved highlight, a heavy brow as one shadow shape over deep-set eyes,
    /// the flat broken nose in profile, an ear. No features beyond that.
    ///
    /// <b>His own poses.</b> The cast swings the gauntlet up and across toward
    /// the target, feet planted (Velvet Rope never moves his feet). Seated, he
    /// is the one operator who takes his device off: the gauntlet lies on the
    /// table and his bare right forearm rests beside it.
    /// </remarks>
    public static class BouncerRig
    {
        public const string Torso = "torso";
        public const string HeadPart = "head";
        public const string Gauntlet = "gauntlet";
        public const string BareForearm = "forearm.bare";
        public const string GauntletOnTable = "gauntlet.table";

        public static readonly OperatorRig Rig = Build();

        private static OperatorRig Build()
        {
            var skeleton = new RigSkeleton()
                .Bone(RigBones.Root, null, 0.02f, 1.02f)
                .Bone(RigBones.Chest, RigBones.Root, 0.02f, 1.04f)
                .Bone(RigBones.Head, RigBones.Chest, 0.14f, 2.34f)
                .Bone(RigBones.UpperArmNear, RigBones.Chest, -0.74f, 2.14f)
                .Bone(RigBones.ForearmNear, RigBones.UpperArmNear, -0.79f, 1.56f)
                .Bone(RigBones.UpperArmFar, RigBones.Chest, 0.70f, 2.12f)
                .Bone(RigBones.ForearmFar, RigBones.UpperArmFar, 0.75f, 1.56f)
                .Bone(RigBones.ThighNear, RigBones.Root, -0.31f, 1.00f)
                .Bone(RigBones.ShinNear, RigBones.ThighNear, -0.32f, 0.54f)
                .Bone(RigBones.ThighFar, RigBones.Root, 0.37f, 1.00f)
                .Bone(RigBones.ShinFar, RigBones.ThighFar, 0.38f, 0.54f);

            var feet = new[]
            {
                new RigAnchor(RigBones.ShinNear, -0.32f, 0.00f),
                new RigAnchor(RigBones.ShinFar, 0.44f, 0.00f),
            };

            var poses = RigPoses.Template(RigBuild.Heavy);

            // Velvet Rope: the gauntlet up and across toward the target, the
            // body leaning into it, the feet where they were.
            poses[RigPoseNames.Cast] = new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Chest, -4f)
                .Turn(RigBones.Head, -3f)
                .Turn(RigBones.UpperArmNear, 82f)
                .Turn(RigBones.ForearmNear, 8f)
                .Turn(RigBones.UpperArmFar, -6f);

            // At the table: the gauntlet off and set down, the bare forearm on the felt.
            var seated = RigPoses.Seated(RigPoseNames.Seated, 0.42f, 0.74f)
                .Turn(RigBones.Head, 3f)
                .Turn(RigBones.UpperArmNear, 30f)
                .Turn(RigBones.ForearmNear, 62f)
                .Hide(Gauntlet)
                .Show(BareForearm)
                .Show(GauntletOnTable);
            poses[RigPoseNames.Seated] = seated;
            // His activity (ART_PROMPTS, "Seated"): turned outward, watching the
            // room rather than playing. He sits back and lifts his chin; the
            // bare arm stays on the felt, so the upper arm gives back what
            // the chest takes.
            poses[RigPoseNames.SeatedLook] = seated.Copy(RigPoseNames.SeatedLook)
                .Turn(RigBones.Chest, 2.5f)
                .Turn(RigBones.Head, 11f)
                .Turn(RigBones.UpperArmNear, 27.5f)
                .Turn(RigBones.UpperArmFar, -3f);
            poses[RigPoseNames.SeatedB] = seated.Copy(RigPoseNames.SeatedB)
                .Turn(RigBones.Chest, 1f, 0f, 0.012f, 1.01f)
                .Turn(RigBones.Head, 1f);

            return new OperatorRig("Bouncer", skeleton, feet, Parts, poses);
        }

        private static IReadOnlyList<RigPart> Parts(LookBookPalette p)
        {
            float line = DecoMotifs.LineWeight;
            float rim = DecoMotifs.RimOnDark;
            const float noFloor = -10f;

            // ── Far arm (his left): behind the body, in the key's shadow ──
            var farUpper = FigureShape.Polygon(0.54f, 2.26f, 0.84f, 2.20f, 0.90f, 1.50f, 0.60f, 1.50f);
            var farUpperArm = new FigureDrawing(farUpper, p.BouncerSuit, p.Ink, line)
                .Shade(farUpper.Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right);

            var farSleeve = FigureShape.Union(
                FigureShape.Polygon(0.60f, 1.62f, 0.90f, 1.62f, 0.88f, 1.16f, 0.62f, 1.16f),
                FigureShape.Circle(0.75f, 1.58f, 0.15f));
            var farHand = FigureShape.Ellipse(0.76f, 1.03f, 0.12f, 0.13f);
            var farForearm = new FigureDrawing(farSleeve.Or(farHand), p.BouncerSuit, p.Ink, line)
                .Block(FigureShape.Rect(0.62f, 1.13f, 0.88f, 1.19f), p.Bone)
                .Block(farHand, p.BouncerSkin)
                .Shade(farSleeve.Or(farHand).Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right);

            // ── Legs: shins under the thighs so the knee shows no seam ──
            var nearShinShape = FigureShape.Union(
                FigureShape.Polygon(-0.58f, 0.58f, -0.06f, 0.58f, -0.08f, 0.08f, -0.60f, 0.08f),
                FigureShape.Circle(-0.32f, 0.56f, 0.25f));
            var nearShoe = FigureShape.Polygon(-0.64f, 0.10f, -0.02f, 0.10f, 0.05f, 0.00f, -0.64f, 0.00f);
            var nearShin = new FigureDrawing(nearShinShape.Or(nearShoe), p.BouncerSuit, p.Ink, line)
                .Block(nearShoe, p.Obsidian)
                .Shade(DecoMotifs.ShadowSide(-0.24f, 0.30f, 0f), p.Shade);

            var nearThighShape = FigureShape.Polygon(-0.60f, 1.10f, -0.02f, 1.10f, -0.06f, 0.50f, -0.58f, 0.50f);
            var nearThigh = new FigureDrawing(nearThighShape, p.BouncerSuit, p.Ink, line)
                .Shade(DecoMotifs.ShadowSide(-0.22f, 0.80f, 0f), p.Shade);

            var farShinShape = FigureShape.Union(
                FigureShape.Polygon(0.12f, 0.58f, 0.64f, 0.58f, 0.62f, 0.08f, 0.14f, 0.08f),
                FigureShape.Circle(0.38f, 0.56f, 0.25f));
            var farShoe = FigureShape.Polygon(0.12f, 0.10f, 0.66f, 0.10f, 0.80f, 0.00f, 0.12f, 0.00f);
            var farShin = new FigureDrawing(farShinShape.Or(farShoe), p.BouncerSuit, p.Ink, line)
                .Block(farShoe, p.Obsidian)
                .Shade(farShinShape.Or(farShoe).Offset(0.1f), p.Shade);

            var farThighShape = FigureShape.Polygon(0.08f, 1.10f, 0.66f, 1.10f, 0.64f, 0.50f, 0.12f, 0.50f);
            var farThigh = new FigureDrawing(farThighShape, p.BouncerSuit, p.Ink, line)
                .Shade(farThighShape.Offset(0.1f), p.Shade);

            // ── Torso: the slab, turned ──────────────────────────────────
            var torsoShape = FigureShape.Polygon(
                -0.12f, 2.40f,
                0.34f, 2.40f,
                0.62f, 2.30f,
                0.66f, 2.16f,
                0.64f, 1.20f,
                0.58f, 0.98f,
                0.26f, 0.86f,     // the jacket's front point
                -0.20f, 0.90f,
                -0.66f, 1.00f,
                -0.72f, 1.20f,
                -0.78f, 2.00f,
                -0.78f, 2.22f,    // stepped shoulder
                -0.62f, 2.22f,
                -0.62f, 2.30f);

            var shirt = FigureShape.Polygon(0.02f, 2.38f, 0.36f, 2.38f, 0.22f, 1.62f);
            var bowTie = FigureShape.Union(
                FigureShape.Polygon(0.19f, 2.30f, 0.09f, 2.35f, 0.09f, 2.24f),
                FigureShape.Polygon(0.19f, 2.30f, 0.29f, 2.35f, 0.29f, 2.24f),
                FigureShape.Circle(0.19f, 2.30f, 0.03f));
            var sidePlane = FigureShape.HalfPlane(0.42f, 1.6f, -1f, 0.05f);
            var nearLapel = FigureShape.Polygon(0.02f, 2.38f, -0.16f, 2.36f, 0.12f, 1.52f, 0.22f, 1.62f);

            var torso = new FigureDrawing(torsoShape, p.BouncerSuit, p.Ink, line)
                .Block(shirt, p.Bone)
                .Block(bowTie, p.Obsidian)
                .Block(FigureShape.Circle(-0.05f, 2.12f, 0.035f), p.Brass)      // the house pin
                .Shade(sidePlane, p.Shade)
                .Light(nearLapel.Minus(sidePlane), p.SuitSheen)
                .Light(FigureShape.Polygon(-0.62f, 2.30f, -0.12f, 2.40f, -0.12f, 2.32f, -0.60f, 2.24f), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Top)
                .InkLine(FigureShape.Polyline(-0.16f, 2.36f, 0.14f, 1.54f, 0.26f, 0.88f))
                .InkLine(FigureShape.Polyline(0.36f, 2.38f, 0.30f, 2.06f, 0.24f, 1.66f));

            // ── Head: shaved oval, heavy brow, the broken nose ───────────
            var neck = FigureShape.Rect(0.00f, 2.26f, 0.28f, 2.46f);
            var skull = FigureShape.Ellipse(0.16f, 2.64f, 0.19f, 0.235f);
            var nose = FigureShape.Polygon(0.31f, 2.66f, 0.40f, 2.56f, 0.33f, 2.51f);
            var headShape = FigureShape.Union(neck, skull, nose);
            var ear = FigureShape.Ellipse(0.02f, 2.60f, 0.045f, 0.07f);
            var brow = FigureShape.Polygon(0.20f, 2.67f, 0.36f, 2.69f, 0.36f, 2.62f, 0.22f, 2.61f);

            var head = new FigureDrawing(headShape, p.BouncerSkin, p.Ink, line)
                .Shade(DecoMotifs.Crescent(0.16f, 2.64f, 0.26f, -0.12f, 0.06f).Or(nose), p.Shade)
                .Shade(brow, p.Shade)
                .Shade(ear, p.Shade)
                .Light(DecoMotifs.Crescent(0.16f, 2.64f, 0.235f, 0.09f, -0.07f).And(FigureShape.HalfPlane(0f, 2.62f, 0f, -1f)), p.Key)
                .Rim(rim, p.Rim, noFloor)
                .InkLine(FigureShape.Polyline(0.02f, 2.53f, 0.06f, 2.66f));

            // ── Near arm (his right): in front of the body ───────────────
            var nearUpper = FigureShape.Polygon(
                -0.56f, 2.28f, -0.80f, 2.28f, -0.80f, 2.20f, -0.97f, 2.18f,
                -0.98f, 1.48f, -0.60f, 1.48f);
            var nearUpperArm = new FigureDrawing(nearUpper, p.BouncerSuit, p.Ink, line)
                .Shade(DecoMotifs.ShadowSide(-0.70f, 1.8f, 0f), p.Shade)
                .Light(FigureShape.Polygon(-0.97f, 2.18f, -0.84f, 2.20f, -0.86f, 1.56f, -0.97f, 1.56f), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left | RimEdges.Top);

            // The gauntlet, elbow to fingertip, strapped over his own arm.
            var gauntletShape = FigureShape.Union(
                FigureShape.Polygon(-1.00f, 1.64f, -0.58f, 1.64f, -0.60f, 1.00f, -0.68f, 0.86f, -0.96f, 0.86f, -1.02f, 1.02f),
                FigureShape.Circle(-0.79f, 1.58f, 0.19f));
            var gauntlet = new FigureDrawing(gauntletShape, p.Brass, p.Ink, line)
                .Block(FigureShape.Rect(-1.02f, 1.46f, -0.56f, 1.52f).And(gauntletShape), p.Obsidian)   // strap
                .Shade(DecoMotifs.ShadowSide(-0.76f, 1.3f, 0f), p.Shade)
                .Light(FigureShape.Polygon(-1.00f, 1.44f, -0.90f, 1.44f, -0.90f, 0.96f, -1.01f, 1.02f), p.Key)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left)
                .InkLine(FigureShape.Polyline(-1.00f, 1.30f, -0.59f, 1.30f))
                .InkLine(FigureShape.Polyline(-1.01f, 1.14f, -0.60f, 1.14f))
                .InkLine(FigureShape.Polyline(-1.00f, 0.98f, -0.62f, 0.98f))
                .Powered(FigureShape.Rect(-0.98f, 1.285f, -0.61f, 1.315f).Or(FigureShape.Rect(-0.99f, 1.125f, -0.62f, 1.155f)), p.Powered);

            // The same arm with the gauntlet off: sleeve, cuff, the bare scarred hand.
            var bareSleeve = FigureShape.Union(
                FigureShape.Polygon(-0.98f, 1.62f, -0.60f, 1.62f, -0.64f, 1.14f, -0.94f, 1.14f),
                FigureShape.Circle(-0.79f, 1.58f, 0.18f));
            var bareHand = FigureShape.Ellipse(-0.79f, 1.02f, 0.13f, 0.14f);
            var bareForearm = new FigureDrawing(bareSleeve.Or(bareHand), p.BouncerSuit, p.Ink, line)
                .Block(FigureShape.Rect(-0.95f, 1.11f, -0.63f, 1.17f), p.Bone)
                .Block(bareHand, p.BouncerSkin)
                .Shade(DecoMotifs.ShadowSide(-0.76f, 1.3f, 0f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left);

            // The gauntlet set down on the table, lying along it.
            var onTableShape = FigureShape.Polygon(0.30f, 1.18f, 0.86f, 1.18f, 0.94f, 1.36f, 0.30f, 1.38f);
            var onTable = new FigureDrawing(onTableShape, p.Brass, p.Ink, line)
                .Shade(FigureShape.Rect(0.2f, 1.1f, 1.0f, 1.25f), p.Shade)
                .InkLine(FigureShape.Polyline(0.48f, 1.18f, 0.48f, 1.38f))
                .InkLine(FigureShape.Polyline(0.66f, 1.18f, 0.67f, 1.37f));

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
                new RigPart("upperArm.near", RigBones.UpperArmNear, 8, nearUpperArm),
                new RigPart(Gauntlet, RigBones.ForearmNear, 9, gauntlet),
                new RigPart(BareForearm, RigBones.ForearmNear, 9, bareForearm, prop: true),
                new RigPart(GauntletOnTable, RigBones.Root, 10, onTable, prop: true),
            };
        }
    }
}
