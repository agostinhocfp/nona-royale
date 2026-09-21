// Assets/_Project/Scripts/Unity/View/Figures/Rig/RigChecks.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The rig's rules, shared by the tests and the judging window
    /// (OPERATOR_LOOKBOOK.md, LB5): the feet stay planted, no joint opens a
    /// seam, nothing is cyan at rest in any pose. Plain C#.
    /// </summary>
    public static class RigChecks
    {
        /// <summary>How far a planted foot may drift, in figure units: well under a texel on the board.</summary>
        public const float FootTolerance = 0.01f;

        /// <summary>The worst distance any foot moves from rest across the planted poses.</summary>
        public static float FootDrift(OperatorRig rig)
        {
            var rest = Feet(rig, rig.Pose(RigPoseNames.Rest));
            float worst = 0f;

            foreach (var name in RigPoseNames.Planted)
            {
                var feet = Feet(rig, rig.Pose(name));
                for (int i = 0; i < feet.Count; i++)
                {
                    float dx = feet[i].x - rest[i].x, dy = feet[i].y - rest[i].y;
                    worst = Math.Max(worst, (float)Math.Sqrt(dx * dx + dy * dy));
                }
            }

            return worst;
        }

        /// <summary>
        /// Every joint, in every pose, that shows a gap: a non-root pivot above
        /// the table line where the composed figure is not solid.
        /// </summary>
        public static List<string> Seams(OperatorRig rig, IReadOnlyList<RigPartImage> parts, FigureCanvas canvas)
        {
            var seams = new List<string>();

            foreach (var name in RigPoseNames.Strip)
            {
                var pose = rig.Pose(name);
                var image = RigComposer.Compose(rig, parts, pose, canvas);
                var world = rig.Skeleton.Evaluate(pose);

                foreach (var bone in rig.Skeleton.Bones)
                {
                    if (bone.Parent == null) continue;

                    var placed = world[bone.Name];
                    if (pose.TableLine.HasValue && placed.PivotY < pose.TableLine.Value + 0.02f) continue;

                    int x = (int)((placed.PivotX - canvas.Left) * canvas.PixelsPerUnit);
                    int y = (int)((placed.PivotY - canvas.Bottom) * canvas.PixelsPerUnit);
                    bool inside = x >= 0 && y >= 0 && x < image.Width && y < image.Height;
                    if (!inside || image.Alpha(x, y) < 250) seams.Add($"{name}: {bone.Name}");
                }
            }

            return seams;
        }

        /// <summary>Cyan texels at rest summed over every pose in the strip.</summary>
        public static int CyanAtRest(OperatorRig rig, IReadOnlyList<RigPartImage> parts, FigureCanvas canvas)
        {
            int count = 0;
            foreach (var name in RigPoseNames.Strip)
                count += SilhouetteMetrics.Count(RigComposer.Compose(rig, parts, rig.Pose(name), canvas), SilhouetteMetrics.IsCyan);

            return count;
        }

        private static List<(float x, float y)> Feet(OperatorRig rig, RigPose pose)
        {
            var world = rig.Skeleton.Evaluate(pose);
            var feet = new List<(float, float)>();

            foreach (var foot in rig.Feet)
            {
                var bone = rig.Skeleton[foot.Bone];
                RigSkeleton.Transform(world[foot.Bone], bone.PivotX, bone.PivotY, foot.X, foot.Y, out float x, out float y);
                feet.Add((x, y));
            }

            return feet;
        }
    }
}
