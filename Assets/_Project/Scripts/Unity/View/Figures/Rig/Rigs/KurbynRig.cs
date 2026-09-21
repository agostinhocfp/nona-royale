// Assets/_Project/Scripts/Unity/View/Figures/Rig/Rigs/KurbynRig.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Kurbyn, DarkGrave, on the rig, in three-quarter view
    /// (OPERATOR_LOOKBOOK.md, LB5d): the coiled four-point. Mid-dark,
    /// broken by bare forearms.
    /// </summary>
    /// <remarks>
    /// Drawn turned toward the right of the screen and drawn coiled at rest:
    /// the torso leans into the facing side, wide through the back, narrow at
    /// the hip; the stance is wide and low with the knees bent outward over
    /// the feet (designer, 2026-09-21: the knees never point at each other);
    /// both fists are up in front of him. The four points are the two elbows
    /// and the two feet. Wider and lower than any other forward-leaning
    /// figure: a crouched spring, not a thrown blade.
    ///
    /// The bare pale forearms are his only light. The waistcoat hangs open,
    /// the brass braces show, the tie is pulled loose. The neural rig clips
    /// flat behind his right ear and runs down the nape; its two filaments
    /// are the tell.
    ///
    /// <b>The head pass</b> (ART_PROMPTS, Kurbyn): black hair cropped and
    /// pushed back as one shape with one carved highlight, a hard jaw, one
    /// brow shadow over the fixed stare.
    ///
    /// <b>His own poses.</b> The cast snaps the near fist out straight at the
    /// target. Seated, he rocks the chair back on two legs and watches
    /// someone else's table; his activity is tipping further back.
    /// </remarks>
    public static class KurbynRig
    {
        public const string Torso = "torso";
        public const string HeadPart = "head";

        public static readonly OperatorRig Rig = Build();

        private static OperatorRig Build()
        {
            var skeleton = new RigSkeleton()
                .Bone(RigBones.Root, null, 0.02f, 1.02f)
                .Bone(RigBones.Chest, RigBones.Root, 0.02f, 1.04f)
                .Bone(RigBones.Head, RigBones.Chest, 0.18f, 2.16f)
                .Bone(RigBones.UpperArmNear, RigBones.Chest, -0.40f, 2.02f)
                .Bone(RigBones.ForearmNear, RigBones.UpperArmNear, -0.54f, 1.54f)
                .Bone(RigBones.UpperArmFar, RigBones.Chest, 0.50f, 2.04f)
                .Bone(RigBones.ForearmFar, RigBones.UpperArmFar, 0.56f, 1.58f)
                .Bone(RigBones.ThighNear, RigBones.Root, -0.14f, 1.02f)
                .Bone(RigBones.ShinNear, RigBones.ThighNear, -0.44f, 0.56f)
                .Bone(RigBones.ThighFar, RigBones.Root, 0.18f, 1.02f)
                .Bone(RigBones.ShinFar, RigBones.ThighFar, 0.48f, 0.56f);

            var feet = new[]
            {
                new RigAnchor(RigBones.ShinNear, -0.44f, 0.00f),
                new RigAnchor(RigBones.ShinFar, 0.56f, 0.00f),
            };

            var poses = RigPoses.Template(RigBuild.Light);

            // The near fist snapped out straight at the target, the body behind it.
            poses[RigPoseNames.Cast] = new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Chest, -6f)
                .Turn(RigBones.Head, -2f)
                .Turn(RigBones.UpperArmNear, 88f)
                .Turn(RigBones.ForearmNear, -48f)
                .Turn(RigBones.UpperArmFar, -12f);

            // The chair back on two legs, eyes on someone else's table.
            // The wide stance splays the far thigh out, so it turns less to
            // stay under the table.
            var seated = RigPoses.Seated(RigPoseNames.Seated, 0.44f, 0.72f)
                .Turn(RigBones.ThighFar, 48f)
                .Turn(RigBones.ShinFar, -48f)
                .Turn(RigBones.Chest, 7f)
                .Turn(RigBones.Head, -4f)
                .Turn(RigBones.UpperArmNear, 18f)
                .Turn(RigBones.ForearmNear, 20f)
                .Turn(RigBones.UpperArmFar, 10f)
                .Turn(RigBones.ForearmFar, 16f);
            poses[RigPoseNames.Seated] = seated;
            poses[RigPoseNames.SeatedB] = seated.Copy(RigPoseNames.SeatedB)
                .Turn(RigBones.Chest, 8f, 0f, 0.012f, 1.01f);

            // Tipping further back, the head turned to follow someone across the room.
            poses[RigPoseNames.SeatedLook] = seated.Copy(RigPoseNames.SeatedLook)
                .Turn(RigBones.Chest, 13f)
                .Turn(RigBones.Head, -10f)
                .Turn(RigBones.UpperArmNear, 8f)
                .Turn(RigBones.ForearmNear, 10f);

            return new OperatorRig("Kurbyn", skeleton, feet, Parts, poses);
        }

        private static IReadOnlyList<RigPart> Parts(LookBookPalette p)
        {
            float line = DecoMotifs.LineWeight;
            float rim = DecoMotifs.RimOnDark;
            const float noFloor = -10f;

            // ── Far arm: rolled sleeve, bare forearm, fist up ───────────
            var farUpperShape = DecoMotifs.Limb(0.50f, 2.04f, 0.14f, 0.56f, 1.58f, 0.12f);
            var farUpperArm = new FigureDrawing(farUpperShape, p.KurbynCloth, p.Ink, line)
                .Shade(farUpperShape.Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right | RimEdges.Top);

            var farForeShape = DecoMotifs.Limb(0.56f, 1.58f, 0.11f, 0.66f, 1.84f, 0.09f);
            var farFist = FigureShape.Ellipse(0.70f, 1.92f, 0.10f, 0.09f);
            var farForearm = new FigureDrawing(farForeShape.Or(farFist), p.KurbynSkin, p.Ink, line)
                .Block(FigureShape.Circle(0.56f, 1.58f, 0.13f).And(farForeShape.Offset(0.02f)), p.KurbynCloth)
                .Shade(farForeShape.Or(farFist).Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right | RimEdges.Top);

            // ── Legs: wide and low, the knees out over the feet ─────────
            // No rim: on trousers this narrow a rim down both sides turns them
            // into two lit wires at board scale (as in the front-view recipe).
            FigureDrawing Shin(float kx, float ax, float toe, bool near)
            {
                var leg = DecoMotifs.Limb(kx, 0.56f, 0.13f, ax, 0.14f, 0.10f);
                var shoe = FigureShape.Polygon(ax - 0.14f, 0.14f, ax + 0.08f, 0.14f, toe, 0.03f, toe, 0.00f, ax - 0.15f, 0.00f);
                var d = new FigureDrawing(leg.Or(shoe), p.Obsidian, p.Ink, line)
                    .Light(FigureShape.Polygon(kx - 0.13f, 0.56f, kx - 0.05f, 0.58f, ax - 0.04f, 0.16f, ax - 0.10f, 0.16f).And(leg), p.PlateSheen);
                return near ? d : d.Shade(leg.Or(shoe).Offset(0.1f), p.Shade);
            }

            FigureDrawing Thigh(float hx, float kx, bool near)
            {
                var shape = DecoMotifs.Limb(hx, 1.04f, 0.16f, kx, 0.56f, 0.13f);
                var d = new FigureDrawing(shape, p.Obsidian, p.Ink, line)
                    .Light(FigureShape.Polygon(hx - 0.16f, 1.02f, hx - 0.08f, 1.04f, kx - 0.05f, 0.58f, kx - 0.13f, 0.56f).And(shape), p.PlateSheen);
                return near ? d : d.Shade(shape.Offset(0.1f), p.Shade);
            }

            // ── Torso: wide back, narrow hip, leaning into the facing side ──
            var torsoShape = FigureShape.Polygon(
                -0.18f, 2.26f,
                0.44f, 2.24f,
                0.66f, 2.12f,      // the far shoulder, rolled forward
                0.54f, 1.66f,
                0.32f, 1.04f,      // narrow hip
                -0.18f, 1.00f,
                -0.30f, 1.60f,
                -0.58f, 2.06f);    // the near shoulder, the back wide behind it

            var shirtFront = FigureShape.Polygon(0.10f, 2.24f, 0.36f, 2.22f, 0.28f, 1.04f, 0.06f, 1.02f);
            var nearPanel = FigureShape.Polygon(-0.14f, 2.18f, 0.10f, 2.22f, 0.04f, 1.02f, -0.20f, 1.02f, -0.30f, 1.60f);
            var farPanel = FigureShape.Polygon(0.36f, 2.20f, 0.52f, 2.10f, 0.50f, 1.66f, 0.30f, 1.06f, 0.26f, 1.40f);
            var braces = FigureShape.Rect(0.12f, 1.06f, 0.16f, 2.20f).Or(FigureShape.Rect(0.28f, 1.08f, 0.31f, 2.18f)).And(shirtFront);
            var tie = FigureShape.Polygon(0.20f, 2.20f, 0.28f, 2.18f, 0.30f, 1.66f, 0.22f, 1.60f, 0.18f, 1.66f);
            var belt = FigureShape.Rect(-0.24f, 1.02f, 0.34f, 1.10f).And(torsoShape);
            var sidePlane = FigureShape.HalfPlane(0.36f, 1.6f, -1f, 0.1f);

            var torso = new FigureDrawing(torsoShape, p.KurbynCloth, p.Ink, line)
                .Block(nearPanel.Or(farPanel).Or(belt), p.Obsidian)
                .Block(braces, p.Brass)
                .Block(tie, p.Obsidian)
                .Shade(sidePlane.Minus(nearPanel), p.Shade)
                .Light(FigureShape.Polygon(-0.58f, 2.06f, -0.18f, 2.26f, -0.10f, 2.26f, -0.10f, 2.16f, -0.48f, 1.98f).And(torsoShape), p.SuitSheen)
                .Light(FigureShape.Polygon(-0.14f, 2.18f, -0.04f, 2.20f, -0.12f, 1.04f, -0.20f, 1.02f, -0.30f, 1.60f).And(nearPanel), p.PlateSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Top | RimEdges.Right)
                .InkLine(FigureShape.Polyline(0.36f, 2.20f, 0.30f, 1.06f));

            // ── Head: hair pushed back, hard jaw, the rig at the nape ────
            var neck = FigureShape.Polygon(0.06f, 2.14f, 0.34f, 2.14f, 0.34f, 2.36f, 0.08f, 2.40f);
            var skull = FigureShape.Ellipse(0.24f, 2.52f, 0.17f, 0.21f);
            var jaw = FigureShape.Polygon(0.16f, 2.44f, 0.42f, 2.44f, 0.40f, 2.34f, 0.28f, 2.28f, 0.16f, 2.32f);
            var nose = FigureShape.Polygon(0.39f, 2.56f, 0.46f, 2.48f, 0.40f, 2.45f);
            var headShape = FigureShape.Union(neck, skull, jaw, nose);
            var hair = skull.Offset(0.02f).And(FigureShape.Polygon(0.00f, 2.80f, 0.46f, 2.80f, 0.42f, 2.62f, 0.20f, 2.58f, 0.10f, 2.36f, 0.00f, 2.36f));
            var nape = FigureShape.Polygon(0.06f, 2.46f, 0.12f, 2.48f, 0.12f, 2.20f, 0.06f, 2.18f);
            var brow = FigureShape.Polygon(0.30f, 2.58f, 0.42f, 2.58f, 0.42f, 2.53f, 0.31f, 2.52f);

            var head = new FigureDrawing(headShape.Or(hair), p.KurbynSkin, p.Ink, line)
                .Block(hair, p.Obsidian)
                .Block(nape, p.Obsidian)
                .Shade(DecoMotifs.Crescent(0.24f, 2.50f, 0.24f, -0.12f, 0.06f).Or(nose).Minus(hair), p.Shade)
                .Shade(FigureShape.Rect(0.06f, 2.14f, 0.34f, 2.30f).And(neck).Minus(nape), p.Shade)
                .Shade(brow, p.Shade)
                .Light(FigureShape.Polygon(0.10f, 2.70f, 0.22f, 2.76f, 0.34f, 2.76f, 0.16f, 2.68f).And(hair), p.PlateSheen)
                .Light(DecoMotifs.Crescent(0.24f, 2.50f, 0.17f, 0.06f, -0.06f).Minus(hair).And(FigureShape.HalfPlane(0f, 2.50f, 0f, -1f)), p.Key)
                .Rim(rim, p.Rim, noFloor)
                .InkLine(FigureShape.Polyline(0.30f, 2.38f, 0.40f, 2.38f))
                .Powered(FigureShape.Rect(0.08f, 2.22f, 0.10f, 2.44f).Or(FigureShape.Rect(0.11f, 2.20f, 0.12f, 2.40f)), p.Powered);

            // ── Near arm: in front, the fist up ──────────────────────────
            var nearUpperShape = DecoMotifs.Limb(-0.40f, 2.02f, 0.15f, -0.54f, 1.54f, 0.12f);
            var nearUpperArm = new FigureDrawing(nearUpperShape, p.KurbynCloth, p.Ink, line)
                .Shade(DecoMotifs.ShadowSide(-0.44f, 1.8f, 0.2f), p.Shade)
                .Light(FigureShape.Polygon(-0.58f, 2.08f, -0.48f, 2.14f, -0.60f, 1.56f, -0.68f, 1.56f).And(nearUpperShape), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left | RimEdges.Top);

            // Forearm rolled bare from the elbow, angled forward and up; the fist before the chest.
            var nearForeShape = DecoMotifs.Limb(-0.54f, 1.54f, 0.12f, -0.14f, 1.72f, 0.10f);
            var nearFist = FigureShape.Ellipse(-0.06f, 1.76f, 0.12f, 0.11f);
            var nearForearm = new FigureDrawing(nearForeShape.Or(nearFist), p.KurbynSkin, p.Ink, line)
                .Block(FigureShape.Circle(-0.54f, 1.54f, 0.14f).And(nearForeShape.Offset(0.02f)), p.KurbynCloth)
                .Shade(FigureShape.Polygon(-0.54f, 1.44f, -0.06f, 1.62f, -0.02f, 1.66f, -0.60f, 1.44f).Or(FigureShape.HalfPlane(0f, 1.60f, 0f, 1f)).And(nearForeShape.Or(nearFist)), p.Shade)
                .Light(FigureShape.Polygon(-0.60f, 1.64f, -0.16f, 1.82f, -0.14f, 1.78f, -0.56f, 1.60f).And(nearForeShape), p.Key)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left | RimEdges.Top)
                .InkLine(FigureShape.Polyline(-0.10f, 1.68f, -0.04f, 1.84f));

            return new[]
            {
                new RigPart("forearm.far", RigBones.ForearmFar, 0, farForearm),
                new RigPart("upperArm.far", RigBones.UpperArmFar, 1, farUpperArm),
                new RigPart("shin.far", RigBones.ShinFar, 2, Shin(0.48f, 0.50f, 0.72f, false)),
                new RigPart("thigh.far", RigBones.ThighFar, 3, Thigh(0.18f, 0.48f, false)),
                new RigPart("shin.near", RigBones.ShinNear, 4, Shin(-0.44f, -0.46f, -0.22f, true)),
                new RigPart("thigh.near", RigBones.ThighNear, 5, Thigh(-0.14f, -0.44f, true)),
                new RigPart(Torso, RigBones.Chest, 6, torso),
                new RigPart(HeadPart, RigBones.Head, 7, head),
                new RigPart("upperArm.near", RigBones.UpperArmNear, 8, nearUpperArm),
                new RigPart("forearm.near", RigBones.ForearmNear, 9, nearForearm),
            };
        }
    }
}
