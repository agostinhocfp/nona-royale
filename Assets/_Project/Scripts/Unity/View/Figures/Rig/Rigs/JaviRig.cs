// Assets/_Project/Scripts/Unity/View/Figures/Rig/Rigs/JaviRig.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Javi on the rig, in three-quarter view (OPERATOR_LOOKBOOK.md, LB5d):
    /// the upright cross. A dark waistcoat block with two white sleeves.
    /// </summary>
    /// <remarks>
    /// Drawn turned toward the right of the screen: narrow and tall, a little
    /// stooped from a career spent leaning over other men. The cross bar is
    /// his elbows, held out wide with the gloved hands up in front of him,
    /// ready, and the bandolier racked across his chest. The charcoal
    /// waistcoat covers the whole torso (the ratified amendment), so the
    /// white stays in the sleeves and the collar.
    ///
    /// The bandolier is a brass strap with short steel canisters racked in
    /// order, frosted at the seal; one is spent. Their charge lines are the
    /// tell. Grey surgical gloves, always on.
    ///
    /// <b>The head pass</b> (ART_PROMPTS, Javi): short greying hair as one
    /// mass, wire-rimmed glasses pushed up onto it, a deep line down the
    /// cheek, the head carried forward.
    ///
    /// <b>His own poses.</b> The cast flicks the near hand out at the target,
    /// as if sending a canister. Seated, forearms on the table, head down;
    /// his activity is re-racking a canister, not looking up.
    /// </remarks>
    public static class JaviRig
    {
        public const string Torso = "torso";
        public const string HeadPart = "head";

        public static readonly OperatorRig Rig = Build();

        private static OperatorRig Build()
        {
            var skeleton = new RigSkeleton()
                .Bone(RigBones.Root, null, 0.02f, 1.14f)
                .Bone(RigBones.Chest, RigBones.Root, 0.02f, 1.16f)
                .Bone(RigBones.Head, RigBones.Chest, 0.10f, 2.30f)
                .Bone(RigBones.UpperArmNear, RigBones.Chest, -0.26f, 2.14f)
                .Bone(RigBones.ForearmNear, RigBones.UpperArmNear, -0.56f, 1.86f)
                .Bone(RigBones.UpperArmFar, RigBones.Chest, 0.32f, 2.12f)
                .Bone(RigBones.ForearmFar, RigBones.UpperArmFar, 0.60f, 1.86f)
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

            // The near hand flicked out at the target, as if sending a canister.
            poses[RigPoseNames.Cast] = new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Chest, -4f)
                .Turn(RigBones.Head, 2f)
                .Turn(RigBones.UpperArmNear, 110f)
                .Turn(RigBones.ForearmNear, -150f)
                .Turn(RigBones.UpperArmFar, -6f);

            // Forearms on the felt, head down over the bandolier.
            var seated = RigPoses.Seated(RigPoseNames.Seated, 0.46f, 0.74f)
                .Turn(RigBones.Chest, -3f)
                .Turn(RigBones.Head, -10f)
                .Turn(RigBones.UpperArmNear, 88f)
                .Turn(RigBones.ForearmNear, -138f)
                .Turn(RigBones.UpperArmFar, -24f)
                .Turn(RigBones.ForearmFar, -128f);
            poses[RigPoseNames.Seated] = seated;
            poses[RigPoseNames.SeatedB] = seated.Copy(RigPoseNames.SeatedB)
                .Turn(RigBones.Chest, -2f, 0f, 0.012f, 1.01f);

            // Re-racking: the near hand lifts a canister toward the strap, eyes on it.
            poses[RigPoseNames.SeatedLook] = seated.Copy(RigPoseNames.SeatedLook)
                .Turn(RigBones.Head, -13f)
                .Turn(RigBones.UpperArmNear, 70f)
                .Turn(RigBones.ForearmNear, -100f);

            return new OperatorRig("Javi", skeleton, feet, Parts, poses);
        }

        private static IReadOnlyList<RigPart> Parts(LookBookPalette p)
        {
            float line = DecoMotifs.LineWeight;
            float rim = DecoMotifs.RimOnDark;
            const float noFloor = -10f;

            // ── Far arm: white sleeve, the gloved hand up ────────────────
            var farUpperShape = DecoMotifs.Limb(0.32f, 2.12f, 0.10f, 0.60f, 1.86f, 0.085f);
            var farUpperArm = new FigureDrawing(farUpperShape, p.Bone, p.Ink, line)
                .Shade(farUpperShape.Offset(0.1f), p.Shade)
                .Rim(DecoMotifs.RimOnLight, p.RimOnLight, noFloor, RimEdges.Right | RimEdges.Top);

            var farForeShape = DecoMotifs.Limb(0.60f, 1.86f, 0.085f, 0.46f, 2.10f, 0.07f);
            var farGlove = FigureShape.Ellipse(0.42f, 2.18f, 0.075f, 0.09f);
            var farForearm = new FigureDrawing(farForeShape.Or(farGlove), p.Bone, p.Ink, line)
                .Block(farGlove.Or(FigureShape.Circle(0.46f, 2.10f, 0.075f)), p.JaviGlove)
                .Shade(farForeShape.Or(farGlove).Offset(0.1f), p.Shade)
                .Rim(DecoMotifs.RimOnLight, p.RimOnLight, noFloor, RimEdges.Right | RimEdges.Top);

            // ── Legs: narrow and straight, black, no rim ─────────────────
            FigureDrawing Shin(float x, float toe, bool near)
            {
                var leg = DecoMotifs.Limb(x, 0.58f, 0.10f, x, 0.14f, 0.085f);
                var shoe = FigureShape.Polygon(x - 0.10f, 0.14f, x + 0.07f, 0.14f, toe, 0.03f, toe, 0.00f, x - 0.11f, 0.00f);
                var d = new FigureDrawing(leg.Or(shoe), p.Obsidian, p.Ink, line)
                    .Light(FigureShape.Rect(x - 0.10f, 0.16f, x - 0.05f, 0.58f).And(leg), p.PlateSheen);
                return near ? d : d.Shade(leg.Or(shoe).Offset(0.1f), p.Shade);
            }

            FigureDrawing Thigh(float x, bool near)
            {
                var shape = DecoMotifs.Limb(x, 1.14f, 0.12f, x - 0.01f, 0.58f, 0.10f);
                var d = new FigureDrawing(shape, p.Obsidian, p.Ink, line)
                    .Light(FigureShape.Rect(x - 0.12f, 0.60f, x - 0.06f, 1.12f).And(shape), p.PlateSheen);
                return near ? d : d.Shade(shape.Offset(0.1f), p.Shade);
            }

            // ── Torso: the waistcoat block and the bandolier ─────────────
            var torsoShape = FigureShape.Polygon(
                -0.26f, 2.24f,
                0.34f, 2.22f,
                0.32f, 1.60f,
                0.28f, 1.10f,
                -0.22f, 1.10f,
                -0.24f, 1.60f);

            var collar = FigureShape.Polygon(0.00f, 2.26f, 0.22f, 2.25f, 0.14f, 2.10f, 0.08f, 2.10f);
            var strap = FigureShape.Polygon(-0.26f, 1.86f, 0.34f, 1.82f, 0.34f, 1.88f, -0.26f, 1.92f);
            var sidePlane = FigureShape.HalfPlane(0.16f, 1.6f, -1f, 0.04f);

            var torso = new FigureDrawing(torsoShape, p.JaviWaistcoat, p.Ink, line)
                .Block(collar, p.Bone)
                .Block(strap, p.Brass)
                .Block(DecoMotifs.Chevron(1.36f, 0.28f, 0.10f, 0.035f).Translate(0.04f, 0f).And(torsoShape), p.PlateSheen);

            // Five canisters racked in order, foreshortened toward the far side; the fourth is spent.
            FigureShape charge = null;
            for (int i = 0; i < 5; i++)
            {
                float x = -0.18f + i * 0.105f - i * i * 0.004f;
                float w = 0.036f - i * 0.003f;
                float y = 1.89f - i * 0.008f;
                var can = FigureShape.Rect(x - w, y - 0.14f, x + w, y + 0.10f);
                bool spent = i == 3;
                torso.Block(can, spent ? p.Obsidian : p.JaviSteel);
                if (!spent)
                {
                    torso.Block(FigureShape.Rect(x - w, y + 0.05f, x + w, y + 0.10f), p.JaviFrost);
                    var line1 = FigureShape.Rect(x - w, y - 0.10f, x + w, y - 0.085f);
                    charge = charge == null ? line1 : charge.Or(line1);
                }
            }

            torso
                .Shade(sidePlane.Minus(collar), p.Shade)
                .Light(FigureShape.Polygon(-0.26f, 2.24f, -0.14f, 2.24f, -0.12f, 1.12f, -0.22f, 1.10f, -0.24f, 1.60f).And(torsoShape).Minus(strap), p.SuitSheen)
                .Rim(rim, p.Rim, 1.12f, RimEdges.Top | RimEdges.Right)
                .InkLine(FigureShape.Polyline(-0.18f, 1.52f, -0.04f, 1.52f))
                .InkLine(FigureShape.Polyline(0.04f, 1.52f, 0.18f, 1.52f))
                .InkLine(FigureShape.Polyline(0.12f, 2.10f, 0.12f, 1.12f))
                .Powered(charge, p.Powered);

            // ── Head: greying hair, glasses pushed up, carried forward ───
            var neck = FigureShape.Rect(0.02f, 2.20f, 0.22f, 2.40f);
            var skull = FigureShape.Ellipse(0.16f, 2.56f, 0.16f, 0.20f);
            var nose = FigureShape.Polygon(0.30f, 2.58f, 0.37f, 2.49f, 0.31f, 2.46f);
            var headShape = FigureShape.Union(neck, skull, nose);
            var hair = skull.Offset(0.015f).And(FigureShape.Polygon(-0.04f, 2.84f, 0.40f, 2.84f, 0.34f, 2.66f, 0.10f, 2.64f, 0.04f, 2.44f, -0.04f, 2.44f));
            var glasses = FigureShape.Polyline(0.12f, 2.70f, 0.32f, 2.70f).Offset(0.018f)
                .Or(FigureShape.Circle(0.28f, 2.70f, 0.035f));

            var head = new FigureDrawing(headShape.Or(hair), p.JaviSkin, p.Ink, line)
                .Block(hair, p.JaviHair)
                .Block(glasses, p.Obsidian)
                .Shade(DecoMotifs.Crescent(0.16f, 2.54f, 0.22f, -0.12f, 0.06f).Or(nose).Minus(hair), p.Shade)
                .Shade(FigureShape.Rect(0.02f, 2.20f, 0.22f, 2.34f).And(neck), p.Shade)
                .Light(FigureShape.Polygon(0.00f, 2.70f, 0.10f, 2.78f, 0.24f, 2.80f, 0.06f, 2.68f).And(hair), p.SuitSheen)
                .Light(DecoMotifs.Crescent(0.16f, 2.54f, 0.16f, 0.06f, -0.06f).Minus(hair).And(FigureShape.HalfPlane(0f, 2.56f, 0f, -1f)), p.Key)
                .Rim(rim, p.Rim, noFloor)
                .InkLine(FigureShape.Polyline(0.24f, 2.52f, 0.26f, 2.42f))
                .InkLine(FigureShape.Polyline(0.22f, 2.39f, 0.29f, 2.39f));

            // ── Near arm: white sleeve, the gloved hand up before him ────
            var nearUpperShape = DecoMotifs.Limb(-0.26f, 2.14f, 0.105f, -0.56f, 1.86f, 0.09f);
            var nearUpperArm = new FigureDrawing(nearUpperShape, p.Bone, p.Ink, line)
                .Shade(FigureShape.HalfPlane(-0.40f, 1.98f, 0.7f, 0.7f).Or(FigureShape.HalfPlane(0f, 1.96f, 0f, 1f)).And(nearUpperShape).Minus(FigureShape.HalfPlane(-0.40f, 2.06f, -0.7f, -0.7f)), p.Shade)
                .Rim(DecoMotifs.RimOnLight, p.RimOnLight, noFloor, RimEdges.Left | RimEdges.Top);

            var nearForeShape = DecoMotifs.Limb(-0.56f, 1.86f, 0.09f, -0.42f, 2.10f, 0.075f);
            var nearGlove = FigureShape.Ellipse(-0.38f, 2.18f, 0.08f, 0.095f);
            var nearForearm = new FigureDrawing(nearForeShape.Or(nearGlove), p.Bone, p.Ink, line)
                .Block(nearGlove.Or(FigureShape.Circle(-0.42f, 2.10f, 0.08f)), p.JaviGlove)
                .Shade(FigureShape.HalfPlane(-0.50f, 1.94f, -0.8f, 0.6f).And(nearForeShape).Minus(nearGlove), p.Shade)
                .Rim(DecoMotifs.RimOnLight, p.RimOnLight, noFloor, RimEdges.Left | RimEdges.Top)
                .InkLine(FigureShape.Polyline(-0.49f, 2.03f, -0.36f, 2.11f));

            return new[]
            {
                new RigPart("forearm.far", RigBones.ForearmFar, 0, farForearm),
                new RigPart("upperArm.far", RigBones.UpperArmFar, 1, farUpperArm),
                new RigPart("shin.far", RigBones.ShinFar, 2, Shin(0.17f, 0.34f, false)),
                new RigPart("thigh.far", RigBones.ThighFar, 3, Thigh(0.17f, false)),
                new RigPart("shin.near", RigBones.ShinNear, 4, Shin(-0.11f, 0.06f, true)),
                new RigPart("thigh.near", RigBones.ThighNear, 5, Thigh(-0.10f, true)),
                new RigPart(Torso, RigBones.Chest, 6, torso),
                new RigPart(HeadPart, RigBones.Head, 7, head),
                new RigPart("upperArm.near", RigBones.UpperArmNear, 8, nearUpperArm),
                new RigPart("forearm.near", RigBones.ForearmNear, 9, nearForearm),
            };
        }
    }
}
