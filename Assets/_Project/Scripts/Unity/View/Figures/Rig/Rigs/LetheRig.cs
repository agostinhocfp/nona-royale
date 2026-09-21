// Assets/_Project/Scripts/Unity/View/Figures/Rig/Rigs/LetheRig.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Lethe on the rig, in three-quarter view (OPERATOR_LOOKBOOK.md, LB5d),
    /// drawn rigged from the start: the six-pointed spark, point up. The only
    /// true mid-grey figure on the board.
    /// </summary>
    /// <remarks>
    /// Drawn turned toward the right of the screen: very tall, very upright,
    /// very still, a narrow body under a wide hard shoulder line, the hands
    /// hanging at her sides. The six points are the three highest blades of
    /// the headdress, the two ends of the shoulder line, and the hem. The
    /// headdress is the tallest element on any figure.
    ///
    /// The smoke-grey column gown is neither light nor dark. Its silver is
    /// tarnished pewter, never gold (it must not compete with Fortuna): the
    /// headdress, the beading at the shoulders and the hem, the lattice collar
    /// across the collarbones, the nanite cells at the hips. The lattice's
    /// points and the cells are the tell.
    ///
    /// <b>The gown walks</b> like Syla's: the skirt rides the hips and falls
    /// to the floor, so she glides with only the toes showing, and her
    /// knockout sinks into it.
    ///
    /// <b>The head pass</b> (ART_PROMPTS, Lethe): dark hair pulled entirely
    /// flat beneath the headdress, a calm symmetrical face, level eyes, a
    /// small closed mouth.
    ///
    /// <b>Her own poses.</b> The cast raises the near hand toward the target,
    /// palm out: she decides where it stands. Seated, hands flat on the table,
    /// looking at the middle of the board; her activity is the smallest on the
    /// board, a lowering of the head.
    /// </remarks>
    public static class LetheRig
    {
        public const string Torso = "torso";
        public const string HeadPart = "head";
        public const string Skirt = "skirt";

        public static readonly OperatorRig Rig = Build();

        private static OperatorRig Build()
        {
            var skeleton = new RigSkeleton()
                .Bone(RigBones.Root, null, 0.02f, 1.10f)
                .Bone(RigBones.Chest, RigBones.Root, 0.02f, 1.12f)
                .Bone(RigBones.Head, RigBones.Chest, 0.06f, 2.30f)
                .Bone(RigBones.UpperArmNear, RigBones.Chest, -0.38f, 2.20f)
                .Bone(RigBones.ForearmNear, RigBones.UpperArmNear, -0.42f, 1.70f)
                .Bone(RigBones.UpperArmFar, RigBones.Chest, 0.42f, 2.18f)
                .Bone(RigBones.ForearmFar, RigBones.UpperArmFar, 0.46f, 1.70f)
                .Bone(RigBones.ThighNear, RigBones.Root, -0.08f, 1.04f)
                .Bone(RigBones.ShinNear, RigBones.ThighNear, -0.09f, 0.54f)
                .Bone(RigBones.ThighFar, RigBones.Root, 0.12f, 1.04f)
                .Bone(RigBones.ShinFar, RigBones.ThighFar, 0.13f, 0.54f);

            var feet = new[]
            {
                new RigAnchor(RigBones.ShinNear, -0.09f, 0.00f),
                new RigAnchor(RigBones.ShinFar, 0.20f, 0.00f),
            };

            var poses = RigPoses.Template(RigBuild.Light);

            // She does not fall: she sinks into the gown, the head bowing.
            poses[RigPoseNames.Knockout] = new RigPose(RigPoseNames.Knockout)
                .Turn(RigBones.Root, 0f, 0f, -0.10f, 0.9f)
                .Turn(RigBones.Chest, 10f)
                .Turn(RigBones.Head, 20f)
                .Turn(RigBones.UpperArmNear, 10f)
                .Turn(RigBones.UpperArmFar, 14f);

            // The near hand raised toward the target, palm out.
            poses[RigPoseNames.Cast] = new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Head, 2f)
                .Turn(RigBones.UpperArmNear, 92f)
                .Turn(RigBones.ForearmNear, -8f);

            // Hands flat on the table, looking at the middle of the board.
            var seated = RigPoses.Seated(RigPoseNames.Seated, 0.46f, 0.74f)
                .Turn(RigBones.Head, -2f)
                .Turn(RigBones.UpperArmNear, 36f)
                .Turn(RigBones.ForearmNear, 54f)
                .Turn(RigBones.UpperArmFar, 18f)
                .Turn(RigBones.ForearmFar, 54f);
            poses[RigPoseNames.Seated] = seated;
            poses[RigPoseNames.SeatedB] = seated.Copy(RigPoseNames.SeatedB)
                .Turn(RigBones.Chest, 0.5f, 0f, 0.008f, 1.006f);

            // The smallest activity on the board: the head lowers, and nothing else moves.
            poses[RigPoseNames.SeatedLook] = seated.Copy(RigPoseNames.SeatedLook)
                .Turn(RigBones.Head, -7f);

            return new OperatorRig("Lethe", skeleton, feet, Parts, poses);
        }

        private static IReadOnlyList<RigPart> Parts(LookBookPalette p)
        {
            float line = DecoMotifs.LineWeight;
            float rim = DecoMotifs.RimOnLight;
            const float noFloor = -10f;

            // ── Far arm: gloved grey, hanging ────────────────────────────
            var farUpperShape = DecoMotifs.Limb(0.42f, 2.18f, 0.095f, 0.46f, 1.70f, 0.08f);
            var farUpperArm = new FigureDrawing(farUpperShape, p.LetheGlove, p.Ink, line)
                .Shade(farUpperShape.Offset(0.1f), p.Shade)
                .Rim(rim, p.RimOnLight, noFloor, RimEdges.Right);

            var farForeShape = DecoMotifs.Limb(0.46f, 1.70f, 0.08f, 0.48f, 1.30f, 0.065f);
            var farHand = FigureShape.Ellipse(0.49f, 1.21f, 0.06f, 0.09f);
            var farForearm = new FigureDrawing(farForeShape.Or(farHand), p.LetheGlove, p.Ink, line)
                .Shade(farForeShape.Or(farHand).Offset(0.1f), p.Shade)
                .Rim(rim, p.RimOnLight, noFloor, RimEdges.Right);

            // ── Legs: under the gown; only the toes ever show ────────────
            FigureDrawing Shin(float x, float toe, bool near)
            {
                var leg = DecoMotifs.Limb(x, 0.54f, 0.09f, x, 0.10f, 0.06f);
                var shoe = FigureShape.Polygon(x - 0.08f, 0.10f, x + 0.06f, 0.10f, toe, 0.02f, toe, 0.00f, x - 0.09f, 0.00f);
                var d = new FigureDrawing(leg.Or(shoe), p.LetheGown, p.Ink, line)
                    .Block(shoe, p.LetheGlove);
                return near ? d : d.Shade(leg.Or(shoe).Offset(0.1f), p.Shade);
            }

            FigureDrawing Thigh(float x, bool near)
            {
                var shape = DecoMotifs.Limb(x, 1.06f, 0.13f, x - 0.01f, 0.54f, 0.09f);
                var d = new FigureDrawing(shape, p.LetheGown, p.Ink, line);
                return near ? d : d.Shade(shape.Offset(0.1f), p.Shade);
            }

            // ── Skirt: the column to the floor, the cells at the hips ────
            var skirtShape = FigureShape.Polygon(
                -0.22f, 1.72f,
                0.26f, 1.72f,
                0.30f, 1.12f,
                0.24f, 0.04f,
                -0.20f, 0.04f,
                -0.26f, 1.12f);
            var cells = FigureShape.Rect(-0.34f, 1.18f, -0.24f, 1.40f).Or(FigureShape.Rect(0.26f, 1.20f, 0.34f, 1.40f));
            var hemBeads = FigureShape.Rect(-0.22f, 0.10f, 0.26f, 0.16f).And(skirtShape);
            var skirt = new FigureDrawing(skirtShape.Or(cells), p.LetheGown, p.Ink, line)
                .Block(cells.Or(hemBeads), p.LetheSilver)
                .Block(FigureShape.Rect(-0.20f, 0.12f, -0.16f, 0.14f).Or(FigureShape.Rect(-0.06f, 0.12f, -0.02f, 0.14f))
                    .Or(FigureShape.Rect(0.08f, 0.12f, 0.12f, 0.14f)), p.LetheGlove)
                .Shade(FigureShape.HalfPlane(0.08f, 1.0f, -1f, 0.04f), p.Shade)
                .Light(FigureShape.Polygon(-0.22f, 1.72f, -0.12f, 1.72f, -0.10f, 0.06f, -0.20f, 0.04f, -0.26f, 1.12f).And(skirtShape).Minus(cells), p.Key)
                .Rim(rim, p.RimOnLight, noFloor, RimEdges.Left)
                .InkLine(FigureShape.Polyline(0.08f, 1.68f, 0.10f, 0.06f))
                .Powered(FigureShape.Rect(-0.31f, 1.22f, -0.28f, 1.36f).Or(FigureShape.Rect(0.29f, 1.24f, 0.31f, 1.36f)), p.Powered);

            // ── Bodice: the wide hard shoulder line, the lattice collar ──
            var bodice = FigureShape.Polygon(
                -0.54f, 2.32f,
                0.56f, 2.30f,
                0.54f, 2.20f,
                0.26f, 1.66f,
                -0.22f, 1.66f,
                -0.52f, 2.20f);
            var shoulderBeads = FigureShape.Polygon(-0.54f, 2.32f, -0.30f, 2.32f, -0.32f, 2.24f, -0.52f, 2.22f)
                .Or(FigureShape.Polygon(0.34f, 2.31f, 0.56f, 2.30f, 0.54f, 2.21f, 0.34f, 2.23f));
            var skin = FigureShape.Polygon(-0.20f, 2.32f, 0.30f, 2.31f, 0.20f, 2.10f, -0.10f, 2.10f);
            var lattice = FigureShape.Union(
                FigureShape.Polyline(-0.20f, 2.30f, 0.04f, 2.12f, 0.30f, 2.30f),
                FigureShape.Polyline(-0.14f, 2.30f, 0.04f, 2.18f, 0.24f, 2.30f),
                FigureShape.Polyline(-0.20f, 2.20f, -0.04f, 2.26f, 0.04f, 2.12f, 0.14f, 2.26f, 0.26f, 2.20f)).Offset(0.008f);
            var points = FigureShape.Union(
                FigureShape.Circle(0.04f, 2.12f, 0.018f),
                FigureShape.Circle(-0.10f, 2.24f, 0.014f),
                FigureShape.Circle(0.18f, 2.24f, 0.014f));
            var sidePlane = FigureShape.HalfPlane(0.16f, 1.9f, -1f, 0.08f);

            var torso = new FigureDrawing(bodice, p.LetheGown, p.Ink, line)
                .Block(skin, p.LetheSkin)
                .Block(shoulderBeads.Or(lattice.And(skin.Offset(0.01f))), p.LetheSilver)
                .Shade(sidePlane.Minus(shoulderBeads), p.Shade)
                .Light(FigureShape.Polygon(-0.54f, 2.32f, -0.20f, 2.32f, -0.10f, 1.68f, -0.22f, 1.66f, -0.52f, 2.20f).And(bodice).Minus(skin).Minus(shoulderBeads), p.Key)
                .Rim(rim, p.RimOnLight, noFloor, RimEdges.Top | RimEdges.Left)
                .Powered(points, p.Powered);

            // ── Head: hair flat under the headdress, the silver blades ───
            var neck = FigureShape.Rect(-0.02f, 2.26f, 0.14f, 2.44f);
            var skull = FigureShape.Ellipse(0.10f, 2.58f, 0.14f, 0.18f);
            var nose = FigureShape.Polygon(0.22f, 2.60f, 0.28f, 2.52f, 0.23f, 2.50f);
            var hair = skull.Offset(0.012f).And(FigureShape.Polygon(-0.06f, 2.80f, 0.30f, 2.80f, 0.26f, 2.68f, 0.12f, 2.66f, 0.04f, 2.46f, -0.06f, 2.46f));

            // The spark: five blades fanning up and out from the temples, the middle three highest.
            float cx = 0.08f, cy = 2.70f;
            var blades = FigureShape.Union(
                DecoMotifs.Ray(cx, cy, 90f, 0.62f, 0.035f),
                DecoMotifs.Ray(cx, cy, 64f, 0.52f, 0.032f),
                DecoMotifs.Ray(cx, cy, 116f, 0.52f, 0.032f),
                DecoMotifs.Ray(cx, cy, 36f, 0.36f, 0.028f),
                DecoMotifs.Ray(cx, cy, 144f, 0.36f, 0.028f));
            var band = FigureShape.Ellipse(cx, cy, 0.15f, 0.05f);
            var headdress = blades.Or(band);
            var headShape = FigureShape.Union(neck, skull, nose, hair, headdress);

            var head = new FigureDrawing(headShape, p.LetheSkin, p.Ink, line)
                .Block(hair, p.LetheHair)
                .Block(headdress, p.LetheSilver)
                .Block(FigureShape.Rect(0.16f, 2.47f, 0.21f, 2.48f), p.LetheHair)          // the small closed mouth
                .Shade(DecoMotifs.Crescent(0.10f, 2.56f, 0.19f, -0.10f, 0.05f).Or(nose).Minus(hair).Minus(headdress), p.Shade)
                .Shade(FigureShape.Rect(-0.02f, 2.26f, 0.14f, 2.38f).And(neck), p.Shade)
                .Shade(headdress.And(FigureShape.HalfPlane(cx, cy, -1f, 0f)), p.Shade)
                .Rim(rim, p.RimOnLight, noFloor)
                .InkLine(FigureShape.Polyline(0.16f, 2.60f, 0.22f, 2.60f));

            // ── Near arm: gloved grey, hanging still ─────────────────────
            var nearUpperShape = DecoMotifs.Limb(-0.38f, 2.20f, 0.10f, -0.42f, 1.70f, 0.085f);
            var nearUpperArm = new FigureDrawing(nearUpperShape, p.LetheGlove, p.Ink, line)
                .Light(FigureShape.Rect(-0.48f, 1.74f, -0.42f, 2.16f).And(nearUpperShape), p.Key)
                .Rim(rim, p.RimOnLight, noFloor, RimEdges.Left | RimEdges.Top);

            var nearForeShape = DecoMotifs.Limb(-0.42f, 1.70f, 0.085f, -0.44f, 1.30f, 0.07f);
            var nearHand = FigureShape.Ellipse(-0.45f, 1.21f, 0.065f, 0.095f);
            var nearForearm = new FigureDrawing(nearForeShape.Or(nearHand), p.LetheGlove, p.Ink, line)
                .Light(FigureShape.Rect(-0.50f, 1.24f, -0.45f, 1.66f).And(nearForeShape.Or(nearHand)), p.Key)
                .Rim(rim, p.RimOnLight, noFloor, RimEdges.Left);

            return new[]
            {
                new RigPart("forearm.far", RigBones.ForearmFar, 0, farForearm),
                new RigPart("upperArm.far", RigBones.UpperArmFar, 1, farUpperArm),
                new RigPart("shin.far", RigBones.ShinFar, 2, Shin(0.13f, 0.26f, false)),
                new RigPart("thigh.far", RigBones.ThighFar, 3, Thigh(0.13f, false)),
                new RigPart("shin.near", RigBones.ShinNear, 4, Shin(-0.09f, 0.04f, true)),
                new RigPart("thigh.near", RigBones.ThighNear, 5, Thigh(-0.08f, true)),
                new RigPart(Skirt, RigBones.Root, 6, skirt),
                new RigPart(Torso, RigBones.Chest, 7, torso),
                new RigPart(HeadPart, RigBones.Head, 8, head),
                new RigPart("upperArm.near", RigBones.UpperArmNear, 9, nearUpperArm),
                new RigPart("forearm.near", RigBones.ForearmNear, 10, nearForearm),
            };
        }
    }
}
