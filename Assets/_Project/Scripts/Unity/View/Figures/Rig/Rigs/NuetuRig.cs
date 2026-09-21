// Assets/_Project/Scripts/Unity/View/Figures/Rig/Rigs/NuetuRig.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Nuetu on the rig, in three-quarter view (OPERATOR_LOOKBOOK.md, LB5d):
    /// the disc. The only all-light mass on the board, cool dove-grey, with
    /// black ablative plates strapped over the shirt.
    /// </summary>
    /// <remarks>
    /// Drawn turned toward the right of the screen, like every rig. The barrel
    /// of him is one circle; the jacket hangs open on it, so the white shirt
    /// shows as a band off-centre toward the facing side, with the stepped
    /// chest plate over it. The near pauldron rides the near upper arm, so it
    /// lifts with the arm on the cast.
    ///
    /// <b>The head pass</b> (ART_PROMPTS, Nuetu): a shaved round head, a wide
    /// flat face, a heavy jaw, one thickened ear, a permanent small grin. The
    /// tarnished bio-link collar sits at the base of the throat.
    ///
    /// <b>His own poses.</b> The cast swings the near arm forward and low, as
    /// if skimming one of his belt discs onto a cell. Seated, he leans back
    /// from the table; his activity is eating, a hand brought to his mouth.
    /// The piece shape is locked: the two belt discs stay on him in every pose.
    /// </remarks>
    public static class NuetuRig
    {
        public const string Torso = "torso";
        public const string HeadPart = "head";
        public const string Pauldron = "pauldron.near";

        public static readonly OperatorRig Rig = Build();

        private static OperatorRig Build()
        {
            var skeleton = new RigSkeleton()
                .Bone(RigBones.Root, null, 0.02f, 0.95f)
                .Bone(RigBones.Chest, RigBones.Root, 0.02f, 0.97f)
                .Bone(RigBones.Head, RigBones.Chest, 0.08f, 2.22f)
                .Bone(RigBones.UpperArmNear, RigBones.Chest, -0.50f, 1.96f)
                .Bone(RigBones.ForearmNear, RigBones.UpperArmNear, -0.58f, 1.46f)
                .Bone(RigBones.UpperArmFar, RigBones.Chest, 0.58f, 1.94f)
                .Bone(RigBones.ForearmFar, RigBones.UpperArmFar, 0.62f, 1.46f)
                .Bone(RigBones.ThighNear, RigBones.Root, -0.18f, 0.90f)
                .Bone(RigBones.ShinNear, RigBones.ThighNear, -0.20f, 0.46f)
                .Bone(RigBones.ThighFar, RigBones.Root, 0.24f, 0.90f)
                .Bone(RigBones.ShinFar, RigBones.ThighFar, 0.26f, 0.46f);

            var feet = new[]
            {
                new RigAnchor(RigBones.ShinNear, -0.20f, 0.00f),
                new RigAnchor(RigBones.ShinFar, 0.32f, 0.00f),
            };

            var poses = RigPoses.Template(RigBuild.Heavy);

            // The disc skimmed forward and low: the arm swings through, the
            // body turns into it, the feet stay where they were.
            poses[RigPoseNames.Cast] = new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Chest, -5f)
                .Turn(RigBones.Head, -2f)
                .Turn(RigBones.UpperArmNear, 62f)
                .Turn(RigBones.ForearmNear, 24f)
                .Turn(RigBones.UpperArmFar, -10f);

            // At the table: leaning back, the near forearm resting on the felt.
            var seated = RigPoses.Seated(RigPoseNames.Seated, 0.40f, 0.70f)
                .Turn(RigBones.Chest, 4f)
                .Turn(RigBones.Head, -2f)
                .Turn(RigBones.UpperArmNear, 24f)
                .Turn(RigBones.ForearmNear, 58f)
                .Turn(RigBones.UpperArmFar, -4f);
            poses[RigPoseNames.Seated] = seated;
            poses[RigPoseNames.SeatedB] = seated.Copy(RigPoseNames.SeatedB)
                .Turn(RigBones.Chest, 5f, 0f, 0.012f, 1.012f);

            // Eating: the near hand comes up to his mouth, the head dips to meet it.
            poses[RigPoseNames.SeatedLook] = seated.Copy(RigPoseNames.SeatedLook)
                .Turn(RigBones.Chest, 3f)
                .Turn(RigBones.Head, -7f)
                .Turn(RigBones.UpperArmNear, 38f)
                .Turn(RigBones.ForearmNear, 118f);

            return new OperatorRig("Nuetu", skeleton, feet, Parts, poses);
        }

        private static IReadOnlyList<RigPart> Parts(LookBookPalette p)
        {
            float line = DecoMotifs.LineWeight;
            float rim = DecoMotifs.RimOnLight;
            const float noFloor = -10f;

            // ── Far arm: behind the disc, in its shadow ──────────────────
            var farUpperShape = DecoMotifs.Limb(0.58f, 1.94f, 0.18f, 0.62f, 1.46f, 0.15f);
            var farUpperArm = new FigureDrawing(farUpperShape, p.NuetuGrey, p.Ink, line)
                .Shade(farUpperShape.Offset(0.1f), p.Shade)
                .Rim(rim, p.RimOnLight, noFloor, RimEdges.Right);

            var farSleeve = DecoMotifs.Limb(0.62f, 1.46f, 0.15f, 0.62f, 1.10f, 0.13f);
            var farFist = FigureShape.Ellipse(0.62f, 0.98f, 0.15f, 0.15f);
            var farForearm = new FigureDrawing(farSleeve.Or(farFist), p.NuetuGrey, p.Ink, line)
                .Block(FigureShape.Rect(0.46f, 1.04f, 0.80f, 1.10f).And(farSleeve), p.Bone)
                .Block(farFist, p.NuetuSkin)
                .Shade(farSleeve.Or(farFist).Offset(0.1f), p.Shade)
                .Rim(rim, p.RimOnLight, noFloor, RimEdges.Right);

            // ── Legs: short and thick, pale spats over black shoes ──────
            var nearShinShape = DecoMotifs.Limb(-0.20f, 0.46f, 0.24f, -0.20f, 0.26f, 0.21f);
            var nearShoe = FigureShape.Polygon(-0.46f, 0.12f, 0.06f, 0.12f, 0.14f, 0.00f, -0.46f, 0.00f);
            var nearSpat = FigureShape.Rect(-0.46f, 0.10f, 0.06f, 0.22f);
            var nearShin = new FigureDrawing(nearShinShape.Or(nearShoe), p.NuetuGrey, p.Ink, line)
                .Block(nearShoe, p.Obsidian)
                .Block(nearSpat.And(nearShinShape.Or(nearShoe)), p.Bone)
                .Shade(DecoMotifs.ShadowSide(-0.12f, 0.30f, 0f), p.Shade);

            var nearThighShape = DecoMotifs.Limb(-0.18f, 0.92f, 0.27f, -0.20f, 0.46f, 0.24f);
            var nearThigh = new FigureDrawing(nearThighShape, p.NuetuGrey, p.Ink, line)
                .Shade(DecoMotifs.ShadowSide(-0.10f, 0.70f, 0f), p.Shade);

            var farShinShape = DecoMotifs.Limb(0.26f, 0.46f, 0.24f, 0.26f, 0.26f, 0.21f);
            var farShoe = FigureShape.Polygon(0.04f, 0.12f, 0.52f, 0.12f, 0.64f, 0.00f, 0.04f, 0.00f);
            var farSpat = FigureShape.Rect(0.04f, 0.10f, 0.52f, 0.22f);
            var farShin = new FigureDrawing(farShinShape.Or(farShoe), p.NuetuGrey, p.Ink, line)
                .Block(farShoe, p.Obsidian)
                .Block(farSpat.And(farShinShape.Or(farShoe)), p.Bone)
                .Shade(farShinShape.Or(farShoe).Offset(0.1f), p.Shade);

            var farThighShape = DecoMotifs.Limb(0.24f, 0.92f, 0.27f, 0.26f, 0.46f, 0.24f);
            var farThigh = new FigureDrawing(farThighShape, p.NuetuGrey, p.Ink, line)
                .Shade(farThighShape.Offset(0.1f), p.Shade);

            // ── Torso: the disc, jacket open on it ───────────────────────
            var disc = FigureShape.Circle(0.02f, 1.50f, 0.86f);

            // The shirt: a band off-centre toward the facing side, open at the collar.
            var shirt = FigureShape.Polygon(-0.02f, 2.36f, 0.40f, 2.32f, 0.44f, 0.80f, 0.00f, 0.74f).And(disc);

            // The chest plate over it: a stepped Deco shield, turned.
            var plate = FigureShape.Polygon(
                -0.14f, 2.00f,
                0.46f, 1.96f,
                0.46f, 1.30f,
                0.34f, 1.16f,
                0.20f, 1.06f,
                0.06f, 1.16f,
                -0.08f, 1.30f,
                -0.14f, 1.50f);
            var harness = FigureShape.Polygon(-0.40f, 1.98f, -0.30f, 2.08f, -0.08f, 1.84f, -0.10f, 1.74f).And(disc);
            var farPauldron = FigureShape.Circle(0.62f, 1.98f, 0.24f).And(disc);

            // The Deco furniture: a fan set into the plate, in the suit's grey.
            var fan = DecoMotifs.Fan(0.18f, 1.12f, 5, 56f, 0.76f, 0.026f).And(plate.Offset(-0.045f));

            // Belt, and the two coaster discs that are his piece shape.
            var belt = FigureShape.Rect(-0.90f, 0.86f, 0.90f, 0.98f).And(disc);
            var nearCoaster = FigureShape.Circle(-0.26f, 0.90f, 0.13f);
            var farCoaster = FigureShape.Circle(0.58f, 0.94f, 0.10f).And(disc);

            var torsoShape = disc.Or(nearCoaster);

            // The cel sphere: an offset circle carves the shadow on the lower
            // right and the lit crescent on the upper left.
            var shadow = disc.Minus(FigureShape.Circle(-0.15f, 1.65f, 0.86f));
            var lit = disc.Minus(FigureShape.Circle(0.15f, 1.36f, 0.86f));
            var black = FigureShape.Union(plate, harness, farPauldron, belt, nearCoaster, farCoaster);

            var torso = new FigureDrawing(torsoShape, p.NuetuGrey, p.Ink, line)
                .Block(shirt, p.Bone)
                .Block(black, p.NuetuPlate)
                .Block(fan, p.NuetuGrey)
                .Shade(shadow, p.Shade)
                .Light(lit.Minus(shadow).Minus(black).Minus(shirt), p.Key)
                .Light(FigureShape.Polygon(-0.14f, 2.00f, 0.04f, 1.99f, 0.02f, 1.60f, -0.14f, 1.50f).And(plate), p.PlateSheen)
                .Light(FigureShape.Circle(-0.30f, 0.94f, 0.07f).And(nearCoaster), p.PlateSheen)
                .Rim(rim, p.RimOnLight, noFloor, RimEdges.Top | RimEdges.Left)
                .InkLine(FigureShape.Polyline(-0.02f, 2.36f, 0.00f, 0.74f))
                .Powered(FigureShape.Rect(0.17f, 1.22f, 0.20f, 1.94f).Or(FigureShape.Rect(-0.40f, 1.93f, -0.12f, 1.95f).And(harness.Offset(0.02f))), p.Powered);

            // ── Head: round, shaved, the heavy jaw and the thickened ear ─
            var neck = FigureShape.Rect(-0.08f, 2.12f, 0.26f, 2.34f);
            var skull = FigureShape.Circle(0.10f, 2.50f, 0.22f);
            var jaw = FigureShape.Polygon(-0.02f, 2.42f, 0.34f, 2.44f, 0.34f, 2.34f, 0.24f, 2.26f, 0.00f, 2.28f);
            var nose = FigureShape.Polygon(0.30f, 2.52f, 0.37f, 2.46f, 0.31f, 2.42f);
            var headShape = FigureShape.Union(neck, skull, jaw, nose);
            var ear = FigureShape.Ellipse(-0.06f, 2.48f, 0.07f, 0.09f);
            var brow = FigureShape.Polygon(0.16f, 2.56f, 0.32f, 2.57f, 0.32f, 2.51f, 0.18f, 2.51f);
            var collar = FigureShape.Rect(-0.10f, 2.14f, 0.28f, 2.22f);

            var head = new FigureDrawing(headShape, p.NuetuSkin, p.Ink, line)
                .Block(collar, p.Brass)
                .Shade(DecoMotifs.Crescent(0.10f, 2.50f, 0.26f, -0.12f, 0.07f).Or(nose), p.Shade)
                .Shade(FigureShape.Rect(-0.10f, 2.22f, 0.28f, 2.30f).And(neck), p.Shade)
                .Shade(brow, p.Shade)
                .Shade(ear, p.Shade)
                .Light(DecoMotifs.Crescent(0.10f, 2.50f, 0.22f, 0.08f, -0.07f).And(FigureShape.HalfPlane(0f, 2.48f, 0f, -1f)), p.Key)
                .Rim(DecoMotifs.RimOnDark, p.Rim, noFloor)
                .InkLine(FigureShape.Polyline(-0.06f, 2.42f, -0.02f, 2.54f))
                .InkLine(FigureShape.Polyline(0.20f, 2.35f, 0.29f, 2.36f, 0.33f, 2.39f));

            // ── Near arm: the pauldron rides it ──────────────────────────
            var nearUpperShape = DecoMotifs.Limb(-0.50f, 1.96f, 0.21f, -0.58f, 1.46f, 0.18f);
            var nearUpperArm = new FigureDrawing(nearUpperShape, p.NuetuGrey, p.Ink, line)
                .Shade(DecoMotifs.ShadowSide(-0.50f, 1.7f, 0.06f), p.Shade)
                .Light(FigureShape.Polygon(-0.74f, 2.00f, -0.62f, 2.02f, -0.68f, 1.46f, -0.78f, 1.46f).And(nearUpperShape), p.Key)
                .Rim(rim, p.RimOnLight, noFloor, RimEdges.Left | RimEdges.Top);

            // Round, following the disc: the plate caps the shoulder rather than squaring it.
            var pauldronShape = FigureShape.Ellipse(-0.50f, 2.00f, 0.29f, 0.24f)
                .And(FigureShape.HalfPlane(0f, 1.84f, 0f, -1f));
            var pauldron = new FigureDrawing(pauldronShape, p.NuetuPlate, p.Ink, line)
                .Light(FigureShape.Ellipse(-0.58f, 2.08f, 0.16f, 0.12f).And(pauldronShape), p.PlateSheen)
                .Rim(DecoMotifs.RimOnDark, p.Rim, noFloor, RimEdges.Left | RimEdges.Top)
                .InkLine(FigureShape.Polyline(-0.76f, 1.96f, -0.24f, 1.96f).And(pauldronShape.Offset(0.01f)))
                .Powered(FigureShape.Rect(-0.74f, 1.945f, -0.26f, 1.975f).And(pauldronShape), p.Powered);

            var nearSleeve = DecoMotifs.Limb(-0.58f, 1.46f, 0.18f, -0.58f, 1.10f, 0.15f);
            var nearFist = FigureShape.Ellipse(-0.58f, 0.96f, 0.17f, 0.16f);
            var nearForearm = new FigureDrawing(nearSleeve.Or(nearFist), p.NuetuGrey, p.Ink, line)
                .Block(FigureShape.Rect(-0.80f, 1.04f, -0.40f, 1.11f).And(nearSleeve), p.Bone)
                .Block(nearFist, p.NuetuSkin)
                .Shade(DecoMotifs.ShadowSide(-0.56f, 1.2f, 0f), p.Shade)
                .Light(FigureShape.Polygon(-0.78f, 1.50f, -0.68f, 1.50f, -0.68f, 1.12f, -0.76f, 1.12f).And(nearSleeve), p.Key)
                .Rim(rim, p.RimOnLight, noFloor, RimEdges.Left)
                .InkLine(FigureShape.Polyline(-0.69f, 0.94f, -0.49f, 0.94f));

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
                new RigPart(Pauldron, RigBones.UpperArmNear, 9, pauldron),
                new RigPart("forearm.near", RigBones.ForearmNear, 10, nearForearm),
            };
        }
    }
}
