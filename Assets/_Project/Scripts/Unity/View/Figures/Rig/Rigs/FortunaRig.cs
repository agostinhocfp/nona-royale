// Assets/_Project/Scripts/Unity/View/Figures/Rig/Rigs/FortunaRig.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Fortuna on the rig, in three-quarter view (OPERATOR_LOOKBOOK.md, LB5d),
    /// drawn rigged from the start: the only gold-dominant figure on the board,
    /// and the only one whose metal is bright rather than tarnished.
    /// </summary>
    /// <remarks>
    /// Drawn turned toward the right of the screen: upright and still, the
    /// shoulders squared to a hard horizontal line, a narrow waist, the long
    /// straight skirt. The diamond is her outline: the hair's sculpted roll
    /// at the top, the elbows out at the waist where her hands rest (the near
    /// one on the dealer's shoe at her hip), the hem at the bottom. The
    /// diamond is still under review (ART_PROMPTS): the question is her piece
    /// pin against the eight-pointed chip notch, which this figure does not
    /// decide.
    ///
    /// The gold is where it is prescribed: the stepped collar piece, the
    /// stepped panels at the waist, the chain, the garters above the elbows
    /// and the cuffs. Polished, with a bright wedge on each piece. The
    /// charcoal is hers, not livery. The shoe's feed line is the tell.
    ///
    /// <b>The skirt walks</b> like Syla's gown: it rides the hips, and her
    /// knockout sinks into it rather than kneeling.
    ///
    /// <b>The head pass</b> (ART_PROMPTS, Fortuna): dark hair in a low
    /// sculpted roll as one mass with two carved highlights, a calm face,
    /// level eyes, a small dark mouth.
    ///
    /// <b>Her own poses.</b> The cast deals: the near hand flicks forward off
    /// the shoe toward the target. Seated, she is not at a yard table, she is
    /// dealing at it: both hands on the felt; her activity is sliding a card
    /// out.
    /// </remarks>
    public static class FortunaRig
    {
        public const string Torso = "torso";
        public const string HeadPart = "head";
        public const string Skirt = "skirt";

        public static readonly OperatorRig Rig = Build();

        private static OperatorRig Build()
        {
            var skeleton = new RigSkeleton()
                .Bone(RigBones.Root, null, 0.02f, 1.14f)
                .Bone(RigBones.Chest, RigBones.Root, 0.02f, 1.16f)
                .Bone(RigBones.Head, RigBones.Chest, 0.08f, 2.30f)
                .Bone(RigBones.UpperArmNear, RigBones.Chest, -0.34f, 2.14f)
                .Bone(RigBones.ForearmNear, RigBones.UpperArmNear, -0.50f, 1.70f)
                .Bone(RigBones.UpperArmFar, RigBones.Chest, 0.38f, 2.12f)
                .Bone(RigBones.ForearmFar, RigBones.UpperArmFar, 0.54f, 1.70f)
                .Bone(RigBones.ThighNear, RigBones.Root, -0.08f, 1.06f)
                .Bone(RigBones.ShinNear, RigBones.ThighNear, -0.09f, 0.56f)
                .Bone(RigBones.ThighFar, RigBones.Root, 0.12f, 1.06f)
                .Bone(RigBones.ShinFar, RigBones.ThighFar, 0.13f, 0.56f);

            var feet = new[]
            {
                new RigAnchor(RigBones.ShinNear, -0.09f, 0.00f),
                new RigAnchor(RigBones.ShinFar, 0.20f, 0.00f),
            };

            var poses = RigPoses.Template(RigBuild.Light);

            // The skirt cannot kneel: she sinks into it, drawn down about the hips.
            poses[RigPoseNames.Knockout] = new RigPose(RigPoseNames.Knockout)
                .Turn(RigBones.Root, 0f, -0.03f, -0.10f, 0.9f)
                .Turn(RigBones.Chest, 16f)
                .Turn(RigBones.Head, 14f)
                .Turn(RigBones.UpperArmNear, 14f)
                .Turn(RigBones.UpperArmFar, 20f);

            // Dealing: the near hand flicks forward off the shoe toward the target.
            poses[RigPoseNames.Cast] = new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Chest, -3f)
                .Turn(RigBones.UpperArmNear, 70f)
                .Turn(RigBones.ForearmNear, -58f)
                .Turn(RigBones.UpperArmFar, -4f);

            // Dealing at the table: both hands on the felt.
            var seated = RigPoses.Seated(RigPoseNames.Seated, 0.46f, 0.74f)
                .Turn(RigBones.Head, -6f)
                .Turn(RigBones.UpperArmNear, 44f)
                .Turn(RigBones.ForearmNear, -8f)
                .Turn(RigBones.UpperArmFar, 18f)
                .Turn(RigBones.ForearmFar, 30f);
            poses[RigPoseNames.Seated] = seated;
            poses[RigPoseNames.SeatedB] = seated.Copy(RigPoseNames.SeatedB)
                .Turn(RigBones.Chest, 1f, 0f, 0.01f, 1.01f);

            // Sliding a card out: the near hand draws forward along the felt.
            poses[RigPoseNames.SeatedLook] = seated.Copy(RigPoseNames.SeatedLook)
                .Turn(RigBones.Head, -9f)
                .Turn(RigBones.UpperArmNear, 58f)
                .Turn(RigBones.ForearmNear, -22f);

            return new OperatorRig("Fortuna", skeleton, feet, Parts, poses);
        }

        private static IReadOnlyList<RigPart> Parts(LookBookPalette p)
        {
            float line = DecoMotifs.LineWeight;
            float rim = DecoMotifs.RimOnDark;
            const float noFloor = -10f;

            // ── Far arm: hand at the far hip ─────────────────────────────
            var farUpperShape = DecoMotifs.Limb(0.38f, 2.12f, 0.10f, 0.54f, 1.70f, 0.085f);
            var farUpperArm = new FigureDrawing(farUpperShape, p.FortunaShirt, p.Ink, line)
                .Block(FigureShape.Rect(0.30f, 1.80f, 0.66f, 1.86f).And(farUpperShape), p.FortunaGold)
                .Shade(farUpperShape.Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right | RimEdges.Top);

            var farForeShape = DecoMotifs.Limb(0.54f, 1.70f, 0.08f, 0.40f, 1.40f, 0.065f);
            var farHand = FigureShape.Ellipse(0.36f, 1.33f, 0.07f, 0.08f);
            var farForearm = new FigureDrawing(farForeShape.Or(farHand), p.FortunaShirt, p.Ink, line)
                .Block(farHand, p.FortunaSkin)
                .Block(FigureShape.Circle(0.40f, 1.40f, 0.075f).And(farForeShape), p.FortunaGold)
                .Shade(farForeShape.Or(farHand).Offset(0.1f), p.Shade)
                .Rim(rim, p.Rim, noFloor, RimEdges.Right);

            // ── Legs: under the skirt, the shoe tips under the hem ───────
            FigureDrawing Shin(float x, float toe, bool near)
            {
                var leg = DecoMotifs.Limb(x, 0.56f, 0.09f, x, 0.12f, 0.065f);
                var shoe = FigureShape.Polygon(x - 0.08f, 0.12f, x + 0.06f, 0.12f, toe, 0.02f, toe, 0.00f, x - 0.09f, 0.00f);
                var d = new FigureDrawing(leg.Or(shoe), p.FortunaCloth, p.Ink, line)
                    .Block(shoe, p.Obsidian);
                return near ? d : d.Shade(leg.Or(shoe).Offset(0.1f), p.Shade);
            }

            FigureDrawing Thigh(float x, bool near)
            {
                var shape = DecoMotifs.Limb(x, 1.08f, 0.13f, x - 0.01f, 0.56f, 0.09f);
                var d = new FigureDrawing(shape, p.FortunaCloth, p.Ink, line);
                return near ? d : d.Shade(shape.Offset(0.1f), p.Shade);
            }

            // ── Skirt: long and straight, on the hips; the shoe at the near hip ──
            var skirtShape = FigureShape.Polygon(
                -0.20f, 1.68f,
                0.24f, 1.68f,
                0.30f, 1.20f,
                0.20f, 0.12f,
                -0.16f, 0.12f,
                -0.26f, 1.20f);
            var shoe = FigureShape.Polygon(-0.50f, 1.40f, -0.24f, 1.42f, -0.22f, 1.14f, -0.48f, 1.12f);
            var skirtPanel = FigureShape.Polygon(-0.06f, 1.68f, 0.14f, 1.68f, 0.14f, 1.20f, 0.10f, 1.10f, 0.10f, 0.40f, 0.14f, 0.30f, 0.14f, 0.12f,
                -0.08f, 0.12f, -0.08f, 0.30f, -0.04f, 0.40f, -0.04f, 1.10f, -0.08f, 1.20f).And(skirtShape);
            var hem = FigureShape.Rect(-0.30f, 0.12f, 0.30f, 0.22f).And(skirtShape);
            var skirt = new FigureDrawing(skirtShape.Or(shoe), p.FortunaCloth, p.Ink, line)
                .Block(skirtPanel.Or(hem), p.FortunaGold)
                .Block(shoe, p.Obsidian)
                .Shade(FigureShape.HalfPlane(0.08f, 1.0f, -1f, 0.06f).Minus(shoe), p.Shade)
                .Light(FigureShape.Rect(-0.08f, 0.12f, 0.00f, 1.68f).And(skirtPanel), p.FortunaGoldLight)
                .Light(FigureShape.Polygon(-0.20f, 1.68f, -0.10f, 1.68f, -0.10f, 0.14f, -0.16f, 0.12f, -0.26f, 1.20f).And(skirtShape).Minus(shoe).Minus(skirtPanel).Minus(hem), p.SuitSheen)
                .Light(FigureShape.Polygon(-0.50f, 1.40f, -0.24f, 1.42f, -0.24f, 1.37f, -0.50f, 1.35f), p.PlateSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left)
                .InkLine(FigureShape.Polyline(0.14f, 1.64f, 0.16f, 0.24f))
                .Powered(FigureShape.Rect(-0.49f, 1.16f, -0.46f, 1.38f), p.Powered);

            // ── Bodice: squared shoulders, the gold at collar and waist ──
            var bodice = FigureShape.Polygon(
                -0.46f, 2.26f,
                0.48f, 2.24f,
                0.46f, 2.10f,
                0.24f, 1.60f,
                -0.20f, 1.60f,
                -0.44f, 2.10f);
            var shirtV = FigureShape.Polygon(-0.04f, 2.26f, 0.24f, 2.25f, 0.12f, 1.88f);
            // A stepped yoke across the shoulders, and a stepped corset panel at the waist:
            // enough gold that she reads as the gold figure, not a dark one with trim.
            var collarPiece = FigureShape.Polygon(
                -0.46f, 2.30f, 0.48f, 2.28f, 0.46f, 2.12f, 0.34f, 2.12f, 0.30f, 2.00f, 0.20f, 2.00f, 0.16f, 1.90f,
                -0.02f, 1.90f, -0.06f, 2.00f, -0.20f, 2.00f, -0.26f, 2.12f, -0.44f, 2.12f).And(bodice.Offset(0.01f));
            var waistPanels = FigureShape.Polygon(
                -0.24f, 1.86f, -0.10f, 1.86f, -0.08f, 1.80f, 0.16f, 1.80f, 0.18f, 1.86f, 0.30f, 1.86f,
                0.26f, 1.60f, -0.20f, 1.60f).And(bodice.Offset(0.01f));
            var chain = FigureShape.Polyline(-0.18f, 1.62f, -0.02f, 1.52f, 0.14f, 1.50f, 0.26f, 1.58f).Offset(0.012f);
            var gold = FigureShape.Union(collarPiece, waistPanels, chain);
            var sidePlane = FigureShape.HalfPlane(0.18f, 1.9f, -1f, 0.1f);

            var torso = new FigureDrawing(bodice.Or(collarPiece).Or(chain), p.FortunaCloth, p.Ink, line)
                .Block(shirtV, p.FortunaShirt)
                .Block(gold, p.FortunaGold)
                .InkLine(FigureShape.Polyline(-0.20f, 1.72f, 0.28f, 1.72f))
                .InkLine(FigureShape.Polyline(-0.34f, 2.20f, 0.40f, 2.19f))
                .Shade(sidePlane.Minus(gold), p.Shade)
                .Shade(sidePlane.And(gold), p.Shade)
                .Light(FigureShape.Polygon(-0.46f, 2.30f, -0.04f, 2.30f, -0.10f, 2.20f, -0.44f, 2.20f).And(collarPiece), p.FortunaGoldLight)
                .Light(FigureShape.Polygon(-0.24f, 1.86f, -0.12f, 1.86f, -0.16f, 1.62f, -0.21f, 1.62f).And(waistPanels), p.FortunaGoldLight)
                .Light(FigureShape.Polygon(-0.44f, 2.12f, -0.30f, 2.12f, -0.18f, 1.62f, -0.20f, 1.60f).And(bodice).Minus(gold), p.SuitSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Top | RimEdges.Left);

            // ── Head: the sculpted roll ──────────────────────────────────
            var neck = FigureShape.Rect(0.00f, 2.24f, 0.18f, 2.42f);
            var skull = FigureShape.Ellipse(0.14f, 2.58f, 0.15f, 0.19f);
            var nose = FigureShape.Polygon(0.27f, 2.60f, 0.33f, 2.52f, 0.28f, 2.50f);
            var roll = FigureShape.Ellipse(-0.04f, 2.48f, 0.12f, 0.09f);
            var hairMass = skull.Offset(0.02f).And(FigureShape.Polygon(-0.10f, 2.82f, 0.34f, 2.82f, 0.32f, 2.68f, 0.14f, 2.66f, 0.06f, 2.44f, -0.10f, 2.44f));
            var hair = hairMass.Or(roll);
            var headShape = FigureShape.Union(neck, skull, nose, hair);

            var head = new FigureDrawing(headShape, p.FortunaSkin, p.Ink, line)
                .Block(hair, p.FortunaHair)
                .Block(FigureShape.Rect(0.18f, 2.46f, 0.24f, 2.48f), p.FortunaShirt)       // the small dark mouth
                .Shade(DecoMotifs.Crescent(0.14f, 2.56f, 0.20f, -0.10f, 0.05f).Or(nose).Minus(hair), p.Shade)
                .Shade(FigureShape.Rect(0.00f, 2.24f, 0.18f, 2.36f).And(neck), p.Shade)
                .Light(FigureShape.Polygon(-0.02f, 2.74f, 0.08f, 2.80f, 0.22f, 2.81f, 0.04f, 2.72f).And(hair), p.PlateSheen)
                .Light(FigureShape.Ellipse(-0.07f, 2.51f, 0.07f, 0.03f).And(roll), p.PlateSheen)
                .Rim(rim, p.Rim, noFloor)
                .InkLine(FigureShape.Polyline(0.20f, 2.60f, 0.27f, 2.60f));

            // ── Near arm: elbow out, the hand resting on the shoe ────────
            var nearUpperShape = DecoMotifs.Limb(-0.34f, 2.14f, 0.105f, -0.50f, 1.70f, 0.09f);
            var nearUpperArm = new FigureDrawing(nearUpperShape, p.FortunaShirt, p.Ink, line)
                .Block(FigureShape.Polygon(-0.60f, 1.86f, -0.30f, 1.90f, -0.30f, 1.82f, -0.60f, 1.78f).And(nearUpperShape), p.FortunaGold)
                .Light(FigureShape.Rect(-0.60f, 1.90f, -0.44f, 2.10f).And(nearUpperShape), p.PlateSheen)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left | RimEdges.Top);

            var nearForeShape = DecoMotifs.Limb(-0.50f, 1.70f, 0.09f, -0.38f, 1.46f, 0.07f);
            var nearHand = FigureShape.Polygon(-0.44f, 1.46f, -0.28f, 1.46f, -0.24f, 1.40f, -0.44f, 1.38f);
            var cuff = FigureShape.Circle(-0.39f, 1.48f, 0.08f).And(nearForeShape);
            var nearForearm = new FigureDrawing(nearForeShape.Or(nearHand), p.FortunaShirt, p.Ink, line)
                .Block(nearHand, p.FortunaSkin)
                .Block(cuff, p.FortunaGold)
                .Light(cuff.And(FigureShape.HalfPlane(-0.39f, 1.50f, 0.7f, -0.7f)), p.FortunaGoldLight)
                .Rim(rim, p.Rim, noFloor, RimEdges.Left | RimEdges.Top);

            return new[]
            {
                new RigPart("forearm.far", RigBones.ForearmFar, 0, farForearm),
                new RigPart("upperArm.far", RigBones.UpperArmFar, 1, farUpperArm),
                new RigPart("shin.far", RigBones.ShinFar, 2, Shin(0.13f, 0.28f, false)),
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
