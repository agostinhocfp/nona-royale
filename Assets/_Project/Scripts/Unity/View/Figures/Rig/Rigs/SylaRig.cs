// Assets/_Project/Scripts/Unity/View/Figures/Rig/Rigs/SylaRig.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Syla, The Blood Hound, on the rig, in three-quarter view
    /// (OPERATOR_LOOKBOOK.md, LB5d): the downward triangle, a light core in a
    /// dark frame. The first rig on the Light build.
    /// </summary>
    /// <remarks>
    /// <b>No cape on the board figure</b> (designer, 2026-09-21), as in her
    /// front-view recipe. The frame is her black: the drone housings on her
    /// shoulders, the widest and hardest line on her; the opera gloves down
    /// both arms; the hip cradles; the bob.
    ///
    /// <b>The gown walks.</b> The skirt rides the hips, not the chest, so a
    /// turn of the body never swings it like a plank, and it falls to just
    /// above the floor, so her step shows as the shoes moving under the hem,
    /// which is how a column gown walks. The legs underneath are drawn in
    /// the gown's ivory behind it, so a kneel or a knockout never shows a gap.
    ///
    /// <b>The head pass</b> (ART_PROMPTS, Syla): the sharp Art Deco bob as one
    /// black mass cut hard at the jaw, with two carved highlights; a pale
    /// face, one shadow under the bob, a small oxblood mouth.
    ///
    /// <b>Her own poses.</b> The cast points the near hand at the target, the
    /// way she sends a drone; the slits in the housings are the tell. Seated,
    /// a drone rests in her near palm on the table, and her activity is turning
    /// it over.
    /// </remarks>
    public static class SylaRig
    {
        public const string Torso = "torso";
        public const string HeadPart = "head";
        public const string Skirt = "skirt";
        public const string DroneInHand = "drone.hand";

        public static readonly OperatorRig Rig = Build();

        private static OperatorRig Build()
        {
            var skeleton = new RigSkeleton()
                .Bone(RigBones.Root, null, 0.02f, 1.10f)
                .Bone(RigBones.Chest, RigBones.Root, 0.02f, 1.12f)
                .Bone(RigBones.Head, RigBones.Chest, 0.04f, 2.34f)
                .Bone(RigBones.UpperArmNear, RigBones.Chest, -0.36f, 2.18f)
                .Bone(RigBones.ForearmNear, RigBones.UpperArmNear, -0.42f, 1.66f)
                .Bone(RigBones.UpperArmFar, RigBones.Chest, 0.38f, 2.16f)
                .Bone(RigBones.ForearmFar, RigBones.UpperArmFar, 0.42f, 1.66f)
                .Bone(RigBones.ThighNear, RigBones.Root, -0.10f, 1.02f)
                .Bone(RigBones.ShinNear, RigBones.ThighNear, -0.11f, 0.54f)
                .Bone(RigBones.ThighFar, RigBones.Root, 0.14f, 1.02f)
                .Bone(RigBones.ShinFar, RigBones.ThighFar, 0.15f, 0.54f);

            var feet = new[]
            {
                new RigAnchor(RigBones.ShinNear, -0.11f, 0.00f),
                new RigAnchor(RigBones.ShinFar, 0.22f, 0.00f),
            };

            var poses = RigPoses.Template(RigBuild.Light);

            // A column gown cannot kneel: she sinks into it and folds at the
            // waist, the whole figure drawn down about the hips.
            poses[RigPoseNames.Knockout] = new RigPose(RigPoseNames.Knockout)
                .Turn(RigBones.Root, 0f, -0.03f, -0.10f, 0.9f)
                .Turn(RigBones.Chest, 18f)
                .Turn(RigBones.Head, 16f)
                .Turn(RigBones.UpperArmNear, 16f)
                .Turn(RigBones.ForearmNear, 24f)
                .Turn(RigBones.UpperArmFar, 22f);

            // Sending a drone: the near hand pointed at the target, chin level.
            poses[RigPoseNames.Cast] = new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Chest, -2f)
                .Turn(RigBones.Head, 2f)
                .Turn(RigBones.UpperArmNear, 84f)
                .Turn(RigBones.ForearmNear, 4f)
                .Turn(RigBones.UpperArmFar, -4f);

            // At the table: a drone resting in her near palm on the felt.
            var seated = RigPoses.Seated(RigPoseNames.Seated, 0.46f, 0.74f)
                .Turn(RigBones.Head, -4f)
                .Turn(RigBones.UpperArmNear, 28f)
                .Turn(RigBones.ForearmNear, 62f)
                .Turn(RigBones.UpperArmFar, 6f)
                .Show(DroneInHand);
            poses[RigPoseNames.Seated] = seated;
            poses[RigPoseNames.SeatedB] = seated.Copy(RigPoseNames.SeatedB)
                .Turn(RigBones.Chest, 1f, 0f, 0.012f, 1.01f);

            // Turning it over: the forearm lifts and rolls the drone, her eyes on it.
            poses[RigPoseNames.SeatedLook] = seated.Copy(RigPoseNames.SeatedLook)
                .Turn(RigBones.Head, -8f)
                .Turn(RigBones.UpperArmNear, 30f)
                .Turn(RigBones.ForearmNear, 88f);

            return new OperatorRig("Syla", skeleton, feet, Parts, poses);
        }

        private static IReadOnlyList<RigPart> Parts(LookBookPalette p)
        {
            float line = DecoMotifs.LineWeight;
            float rim = DecoMotifs.RimOnDark;
            const float noFloor = -10f;

            // ── Far arm: gloved, behind the gown ────────────────────────
            var farUpperShape = DecoMotifs.Limb(0.38f, 2.16f, 0.12f, 0.42f, 1.66f, 0.10f);
            var farUpperArm = new FigureDrawing(farUpperShape, p.SylaCape, p.Ink, line)
                .Block(FigureShape.Rect(0.20f, 2.02f, 0.56f, 2.30f).And(farUpperShape), p.SylaSkin)
                .Shade(farUpperShape.Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right);

            var farForeShape = DecoMotifs.Limb(0.42f, 1.66f, 0.10f, 0.44f, 1.28f, 0.085f);
            var farHand = FigureShape.Ellipse(0.45f, 1.17f, 0.08f, 0.11f);
            var farForearm = new FigureDrawing(farForeShape.Or(farHand), p.SylaCape, p.Ink, line)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right);

            // ── Legs: ivory under the gown, the shoes under the hem ──────
            FigureDrawing Shin(float x, float toe, bool near)
            {
                var leg = DecoMotifs.Limb(x, 0.54f, 0.10f, x, 0.12f, 0.07f);
                var shoe = FigureShape.Polygon(x - 0.09f, 0.14f, x + 0.06f, 0.14f, toe, 0.02f, toe, 0.00f, x - 0.10f, 0.00f);
                var d = new FigureDrawing(leg.Or(shoe), p.SylaGown, p.Ink, line)
                    .Block(shoe, p.SylaCape);
                return near ? d : d.Shade(leg.Or(shoe).Offset(0.1f), p.Shade);
            }

            var nearThigh = new FigureDrawing(DecoMotifs.Limb(-0.10f, 1.04f, 0.14f, -0.11f, 0.54f, 0.10f), p.SylaGown, p.Ink, line);
            var farThighShape = DecoMotifs.Limb(0.14f, 1.04f, 0.14f, 0.15f, 0.54f, 0.10f);
            var farThigh = new FigureDrawing(farThighShape, p.SylaGown, p.Ink, line)
                .Shade(farThighShape.Offset(0.1f), p.Shade);

            // ── Skirt: rides the hips, so a turn of the body never swings it ──
            // Full at the hip and narrowing hard to the ankle: the triangle's point.
            var skirtShape = FigureShape.Polygon(
                -0.24f, 1.66f,
                0.28f, 1.66f,
                0.42f, 1.24f,      // hip
                0.26f, 0.50f,
                0.15f, 0.12f,
                -0.13f, 0.12f,
                -0.24f, 0.50f,
                -0.40f, 1.24f);
            var cradles = FigureShape.Rect(-0.48f, 1.30f, -0.32f, 1.56f).Or(FigureShape.Rect(0.36f, 1.32f, 0.46f, 1.56f));
            var skirtPlane = FigureShape.HalfPlane(0.12f, 1.0f, -1f, 0.04f);

            var skirt = new FigureDrawing(skirtShape.Or(cradles), p.SylaGown, p.Ink, line)
                .Block(DecoMotifs.Chevron(0.62f, 0.30f, 0.10f, 0.05f).Translate(0.02f, 0f).And(skirtShape), p.SylaCape)
                .Block(cradles, p.Obsidian)
                .Shade(skirtPlane.Minus(cradles), p.Shade)
                .Light(FigureShape.Polygon(-0.24f, 1.66f, -0.14f, 1.66f, -0.08f, 0.14f, -0.14f, 0.12f, -0.24f, 0.50f, -0.40f, 1.24f).And(skirtShape), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left)
                .InkLine(FigureShape.Polyline(0.12f, 1.60f, 0.14f, 0.14f))
                .Powered(FigureShape.Rect(-0.42f, 1.36f, -0.39f, 1.50f), p.Powered);

            // ── Bodice: the bare shoulders and the drones ────────────────
            var bodice = FigureShape.Polygon(
                -0.30f, 2.18f,
                0.34f, 2.18f,
                0.24f, 1.50f,
                -0.20f, 1.50f,
                -0.22f, 1.72f);
            var shoulders = FigureShape.Polygon(
                -0.44f, 2.30f, -0.30f, 2.38f, 0.34f, 2.38f, 0.46f, 2.30f, 0.46f, 2.16f, -0.44f, 2.16f);

            // The drones: stepped wedges on the shoulders, the triangle's wide top.
            var nearDrone = FigureShape.Polygon(
                -0.84f, 2.44f, -0.30f, 2.46f, -0.30f, 2.24f, -0.46f, 2.12f, -0.60f, 2.20f, -0.84f, 2.28f);
            var farDrone = FigureShape.Polygon(
                0.30f, 2.44f, 0.70f, 2.42f, 0.70f, 2.28f, 0.54f, 2.20f, 0.44f, 2.14f, 0.30f, 2.24f);

            var torsoShape = FigureShape.Union(bodice, shoulders, nearDrone, farDrone);
            var black = FigureShape.Union(nearDrone, farDrone);
            var sidePlane = FigureShape.HalfPlane(0.12f, 1.8f, -1f, 0.04f);

            var torso = new FigureDrawing(torsoShape, p.SylaGown, p.Ink, line)
                .Block(shoulders.Minus(bodice), p.SylaSkin)
                .Block(black, p.Obsidian)
                .Shade(sidePlane.Minus(black), p.Shade)
                .Shade(farDrone.And(FigureShape.HalfPlane(0f, 2.36f, 0f, 1f)), p.Shade)
                .Light(FigureShape.Polygon(-0.30f, 2.16f, -0.18f, 2.16f, -0.12f, 1.50f, -0.20f, 1.50f, -0.22f, 1.72f).And(bodice), p.SuitSheen)
                .Light(FigureShape.Polygon(-0.84f, 2.44f, -0.30f, 2.46f, -0.30f, 2.39f, -0.84f, 2.37f), p.PlateSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Top | RimEdges.Left)
                .InkLine(FigureShape.Polyline(-0.28f, 2.17f, 0.32f, 2.17f))
                .InkLine(FigureShape.Polyline(0.12f, 2.16f, 0.12f, 1.54f))
                .Powered(FigureShape.Rect(-0.76f, 2.31f, -0.38f, 2.34f).Or(FigureShape.Rect(0.36f, 2.31f, 0.64f, 2.34f)), p.Powered);

            // ── Head: the bob cut hard at the jaw ────────────────────────
            // Drawn a size up about the neck, so the head holds its own
            // against the drone housings at board scale.
            FigureShape H(FigureShape shape) => shape.Scale(1.15f, 0.04f, 2.34f);

            var neck = FigureShape.Rect(-0.04f, 2.28f, 0.12f, 2.46f);
            var face = FigureShape.Ellipse(0.10f, 2.56f, 0.13f, 0.17f);
            var nose = FigureShape.Polygon(0.21f, 2.60f, 0.26f, 2.53f, 0.21f, 2.51f);
            var bobMass = FigureShape.Ellipse(0.02f, 2.64f, 0.21f, 0.22f).And(FigureShape.HalfPlane(0f, 2.44f, 0f, -1f));
            var faceWindow = FigureShape.Polygon(0.06f, 2.68f, 0.30f, 2.70f, 0.30f, 2.36f, 0.08f, 2.40f);
            var bob = bobMass.Minus(faceWindow.And(face.Offset(0.02f)));
            var headShape = FigureShape.Union(neck, face, nose, bobMass);

            var head = new FigureDrawing(H(headShape), p.SylaSkin, p.Ink, line)
                .Block(H(bob), p.SylaCape)
                .Block(H(FigureShape.Rect(0.12f, 2.46f, 0.19f, 2.48f)), p.RevuJacket)                   // the oxblood mouth
                .Shade(H(face.And(FigureShape.HalfPlane(0f, 2.62f, 0f, -1f)).And(FigureShape.Circle(-0.02f, 2.66f, 0.18f))), p.Shade)
                .Shade(H(FigureShape.Rect(-0.04f, 2.28f, 0.12f, 2.40f).And(neck)), p.Shade)
                .Light(H(FigureShape.Polygon(-0.14f, 2.74f, -0.02f, 2.84f, 0.08f, 2.84f, -0.06f, 2.74f).And(bob)), p.SuitSheen)
                .Light(H(FigureShape.Polygon(-0.19f, 2.60f, -0.14f, 2.72f, -0.10f, 2.70f, -0.15f, 2.52f).And(bob)), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor)
                .InkLine(H(FigureShape.Polyline(0.13f, 2.60f, 0.20f, 2.60f)));

            // ── Near arm: gloved, in front ───────────────────────────────
            var nearUpperShape = DecoMotifs.Limb(-0.36f, 2.18f, 0.13f, -0.42f, 1.66f, 0.11f);
            var nearUpperArm = new FigureDrawing(nearUpperShape, p.SylaCape, p.Ink, line)
                .Block(FigureShape.Rect(-0.60f, 2.02f, -0.16f, 2.32f).And(nearUpperShape), p.SylaSkin)
                .Light(FigureShape.Rect(-0.52f, 1.70f, -0.44f, 2.00f).And(nearUpperShape), p.PlateSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left | RimEdges.Top);

            var nearForeShape = DecoMotifs.Limb(-0.42f, 1.66f, 0.11f, -0.44f, 1.28f, 0.09f);
            var nearHand = FigureShape.Ellipse(-0.44f, 1.16f, 0.085f, 0.115f);
            var nearForearm = new FigureDrawing(nearForeShape.Or(nearHand), p.SylaCape, p.Ink, line)
                .Light(FigureShape.Rect(-0.53f, 1.30f, -0.47f, 1.62f).And(nearForeShape), p.PlateSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left);

            // A drone in her palm, for the table only.
            var droneShape = FigureShape.Polygon(-0.60f, 1.14f, -0.30f, 1.14f, -0.26f, 1.06f, -0.34f, 1.00f, -0.58f, 1.00f);
            var droneInHand = new FigureDrawing(droneShape, p.Obsidian, p.Ink, line)
                .Light(FigureShape.Rect(-0.60f, 1.10f, -0.30f, 1.14f).And(droneShape), p.PlateSheen)
                .InkLine(FigureShape.Polyline(-0.54f, 1.06f, -0.34f, 1.06f));

            return new[]
            {
                new RigPart("forearm.far", RigBones.ForearmFar, 0, farForearm),
                new RigPart("upperArm.far", RigBones.UpperArmFar, 1, farUpperArm),
                new RigPart("shin.far", RigBones.ShinFar, 2, Shin(0.15f, 0.30f, false)),
                new RigPart("thigh.far", RigBones.ThighFar, 3, farThigh),
                new RigPart("shin.near", RigBones.ShinNear, 4, Shin(-0.11f, 0.04f, true)),
                new RigPart("thigh.near", RigBones.ThighNear, 5, nearThigh),
                new RigPart(Skirt, RigBones.Root, 6, skirt),
                new RigPart(Torso, RigBones.Chest, 7, torso),
                new RigPart(HeadPart, RigBones.Head, 8, head),
                new RigPart("upperArm.near", RigBones.UpperArmNear, 9, nearUpperArm),
                new RigPart("forearm.near", RigBones.ForearmNear, 10, nearForearm),
                new RigPart(DroneInHand, RigBones.ForearmNear, 11, droneInHand, prop: true),
            };
        }
    }
}
