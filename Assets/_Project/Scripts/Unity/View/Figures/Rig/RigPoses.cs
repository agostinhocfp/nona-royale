// Assets/_Project/Scripts/Unity/View/Figures/Rig/RigPoses.cs
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>The poses every rig has, by name.</summary>
    public static class RigPoseNames
    {
        public const string Rest = "rest";
        public const string IdleA = "idle.a";
        public const string IdleB = "idle.b";
        public const string StepA = "step.a";
        public const string StepB = "step.b";
        public const string Cast = "cast";
        public const string Hit = "hit";
        public const string Knockout = "knockout";
        public const string Seated = "seated";
        public const string SeatedB = "seated.b";

        /// <summary>The seated activity's other beat: leaning back to take in the room (LB5c).</summary>
        public const string SeatedLook = "seated.look";

        /// <summary>The order the judging window shows them in.</summary>
        public static readonly string[] Strip = { Rest, IdleA, IdleB, StepA, StepB, Cast, Hit, Knockout, Seated, SeatedB, SeatedLook };

        /// <summary>Poses the feet must not move in.</summary>
        public static readonly string[] Planted = { Rest, IdleA, IdleB, Cast };
    }

    /// <summary>How a body moves: heavy figures move less and lower, light ones more.</summary>
    public enum RigBuild
    {
        /// <summary>The slab, the disc, the octagon: Bouncer, Nuetu, Sanity.</summary>
        Heavy,

        /// <summary>The dart, the column, the blade: everyone else.</summary>
        Light,
    }

    /// <summary>
    /// The shared pose templates (OPERATOR_LOOKBOOK.md, LB5): the idle, the
    /// step, the hit and the knockout by build, plus a default cast and seated
    /// pose for a recipe to override. Angles are counter-clockwise, so with a
    /// figure facing right a positive turn swings a hanging limb forward.
    /// </summary>
    /// <remarks>
    /// If a recipe needs more than its cast and its seated activity of its
    /// own, the template is wrong, not the recipe.
    /// </remarks>
    public static class RigPoses
    {
        public static Dictionary<string, RigPose> Template(RigBuild build)
        {
            float k = build == RigBuild.Heavy ? 0.7f : 1f;
            var poses = new Dictionary<string, RigPose>();

            void Add(RigPose pose) => poses[pose.Name] = pose;

            Add(new RigPose(RigPoseNames.Rest));

            // Idle: a slow breath and a small weight shift. The feet never move.
            Add(new RigPose(RigPoseNames.IdleA)
                .Turn(RigBones.Chest, -1.2f * k)
                .Turn(RigBones.Head, 1.0f * k)
                .Turn(RigBones.UpperArmNear, 1.5f * k)
                .Turn(RigBones.UpperArmFar, -1.5f * k));

            Add(new RigPose(RigPoseNames.IdleB)
                .Turn(RigBones.Chest, 0.8f * k, 0f, 0.015f, 1.012f)
                .Turn(RigBones.Head, -1.5f * k)
                .Turn(RigBones.UpperArmNear, -1.0f * k)
                .Turn(RigBones.UpperArmFar, 1.0f * k));

            // The step: near leg forward, far leg back, arms countering.
            Add(new RigPose(RigPoseNames.StepA)
                .Turn(RigBones.Root, 0f, 0f, 0.02f)
                .Turn(RigBones.Chest, -2f * k)
                .Turn(RigBones.ThighNear, 16f * k)
                .Turn(RigBones.ShinNear, -14f * k)
                .Turn(RigBones.ThighFar, -14f * k)
                .Turn(RigBones.ShinFar, -8f * k)
                .Turn(RigBones.UpperArmNear, -10f * k)
                .Turn(RigBones.UpperArmFar, 10f * k));

            Add(new RigPose(RigPoseNames.StepB)
                .Turn(RigBones.Root, 0f, 0f, 0.02f)
                .Turn(RigBones.Chest, -2f * k)
                .Turn(RigBones.ThighNear, -14f * k)
                .Turn(RigBones.ShinNear, -8f * k)
                .Turn(RigBones.ThighFar, 16f * k)
                .Turn(RigBones.ShinFar, -14f * k)
                .Turn(RigBones.UpperArmNear, 10f * k)
                .Turn(RigBones.UpperArmFar, -10f * k));

            // The hit: rocked back, away from whoever struck. Strong enough to
            // read at board scale in the 60 ms it peaks for (LB5c: the first
            // 9° barely showed).
            Add(new RigPose(RigPoseNames.Hit)
                .Turn(RigBones.Root, 0f, -0.08f * k)
                .Turn(RigBones.Chest, 14f * k)
                .Turn(RigBones.Head, 10f * k)
                .Turn(RigBones.UpperArmNear, -18f * k)
                .Turn(RigBones.UpperArmFar, 16f * k));

            // The knockout: down on the knees and folding, just before the shatter.
            Add(new RigPose(RigPoseNames.Knockout)
                .Turn(RigBones.Root, 0f, -0.06f, -0.38f)
                .Turn(RigBones.Chest, 24f)
                .Turn(RigBones.Head, 16f)
                .Turn(RigBones.ThighNear, 70f)
                .Turn(RigBones.ShinNear, -75f)
                .Turn(RigBones.ThighFar, 60f)
                .Turn(RigBones.ShinFar, -70f)
                .Turn(RigBones.UpperArmNear, 10f)
                .Turn(RigBones.UpperArmFar, 20f));

            // Defaults a recipe replaces with its own.
            Add(new RigPose(RigPoseNames.Cast)
                .Turn(RigBones.Chest, -3f)
                .Turn(RigBones.UpperArmNear, 70f)
                .Turn(RigBones.ForearmNear, 10f));

            Add(Seated(RigPoseNames.Seated, 0.42f, 0.72f));
            Add(Seated(RigPoseNames.SeatedB, 0.42f, 0.72f).Turn(RigBones.Chest, 1f, 0f, 0.012f, 1.01f));

            // The seated activity's default: a lean back, the chin up. A recipe
            // replaces it with its own (ART_PROMPTS, each block's "Seated").
            Add(Seated(RigPoseNames.SeatedLook, 0.42f, 0.72f)
                .Turn(RigBones.Chest, 2f)
                .Turn(RigBones.Head, 5f)
                .Turn(RigBones.UpperArmNear, -2f)
                .Turn(RigBones.UpperArmFar, -2f));

            return poses;
        }

        /// <summary>
        /// Sat at the table: the body lowered by <paramref name="drop"/>, the
        /// thighs forward, and nothing drawn below <paramref name="tableLine"/>.
        /// </summary>
        public static RigPose Seated(string name, float drop, float tableLine) =>
            new RigPose(name)
                .Turn(RigBones.Root, 0f, 0f, -drop)
                .Turn(RigBones.ThighNear, 85f)
                .Turn(RigBones.ShinNear, -85f)
                .Turn(RigBones.ThighFar, 85f)
                .Turn(RigBones.ShinFar, -85f)
                .AtTable(tableLine);
    }
}
