// Assets/_Project/Scripts/Unity/View/Figures/Rig/RigSkeleton.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>The bones every rig names, so poses and templates can be shared across builds.</summary>
    /// <remarks>
    /// "Near" and "far" are as seen from the camera in the authored facing
    /// (towards the right of the screen): the near arm is drawn in front of
    /// the body, the far arm behind it (OPERATOR_LOOKBOOK.md, LB5).
    /// </remarks>
    public static class RigBones
    {
        public const string Root = "root";
        public const string Chest = "chest";
        public const string Head = "head";
        public const string UpperArmNear = "upperArm.near";
        public const string ForearmNear = "forearm.near";
        public const string UpperArmFar = "upperArm.far";
        public const string ForearmFar = "forearm.far";
        public const string ThighNear = "thigh.near";
        public const string ShinNear = "shin.near";
        public const string ThighFar = "thigh.far";
        public const string ShinFar = "shin.far";
    }

    /// <summary>One bone: a pivot in figure space, where it turns, and the bone it hangs from.</summary>
    public sealed class RigBone
    {
        public string Name { get; }

        /// <summary>The bone this one turns with; null for the root.</summary>
        public string Parent { get; }

        /// <summary>The joint, in figure space at rest.</summary>
        public float PivotX { get; }
        public float PivotY { get; }

        public RigBone(string name, string parent, float pivotX, float pivotY)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Parent = parent;
            PivotX = pivotX;
            PivotY = pivotY;
        }
    }

    /// <summary>A bone's placement in a pose: where its joint went, and how far it has turned in all.</summary>
    public readonly struct BoneWorld
    {
        public readonly float PivotX, PivotY;

        /// <summary>Total rotation, in degrees, counter-clockwise, including every parent's.</summary>
        public readonly float Degrees;

        /// <summary>Uniform scale about the pivot (the chest breathes); 1 at rest.</summary>
        public readonly float Scale;

        public BoneWorld(float pivotX, float pivotY, float degrees, float scale)
        {
            PivotX = pivotX;
            PivotY = pivotY;
            Degrees = degrees;
            Scale = scale;
        }
    }

    /// <summary>
    /// The bones of one operator, in figure space and three-quarter view
    /// (OPERATOR_LOOKBOOK.md, LB5). Rest positions are per operator, since a
    /// slab and a dart do not share joints; the names are shared, so poses are.
    /// </summary>
    public sealed class RigSkeleton
    {
        private readonly Dictionary<string, RigBone> _bones = new Dictionary<string, RigBone>();
        private readonly List<RigBone> _order = new List<RigBone>();

        /// <summary>Parents before children, in the order they were added.</summary>
        public IReadOnlyList<RigBone> Bones => _order;

        public RigSkeleton Bone(string name, string parent, float pivotX, float pivotY)
        {
            if (_bones.ContainsKey(name)) throw new ArgumentException($"Bone '{name}' is already in the skeleton.", nameof(name));
            if (parent != null && !_bones.ContainsKey(parent))
                throw new ArgumentException($"Bone '{name}' names parent '{parent}' before it exists.", nameof(parent));
            if (parent == null && _order.Count > 0) throw new ArgumentException("A skeleton has one root.", nameof(parent));

            var bone = new RigBone(name, parent, pivotX, pivotY);
            _bones[name] = bone;
            _order.Add(bone);
            return this;
        }

        public bool Has(string name) => name != null && _bones.ContainsKey(name);

        public RigBone this[string name] => _bones[name];

        /// <summary>Every bone placed for a pose: parents turn their children with them.</summary>
        public Dictionary<string, BoneWorld> Evaluate(RigPose pose)
        {
            var world = new Dictionary<string, BoneWorld>(_order.Count);

            foreach (var bone in _order)
            {
                var own = pose != null ? pose.Get(bone.Name) : BoneTurn.Rest;

                float px, py, degrees, scale;
                if (bone.Parent == null)
                {
                    px = bone.PivotX + own.Dx;
                    py = bone.PivotY + own.Dy;
                    degrees = own.Degrees;
                    scale = own.Scale;
                }
                else
                {
                    var parent = world[bone.Parent];
                    var p = _bones[bone.Parent];
                    Transform(parent, p.PivotX, p.PivotY, bone.PivotX, bone.PivotY, out px, out py);
                    px += own.Dx;
                    py += own.Dy;
                    degrees = parent.Degrees + own.Degrees;
                    scale = own.Scale;
                }

                world[bone.Name] = new BoneWorld(px, py, degrees, scale);
            }

            return world;
        }

        /// <summary>
        /// Where a point drawn at rest on a bone lands once the bone is placed:
        /// about the rest pivot, turned and scaled, then moved to the placed pivot.
        /// </summary>
        public static void Transform(BoneWorld placed, float restPivotX, float restPivotY, float x, float y, out float wx, out float wy)
        {
            double r = placed.Degrees * Math.PI / 180.0;
            float c = (float)Math.Cos(r), s = (float)Math.Sin(r);
            float lx = (x - restPivotX) * placed.Scale, ly = (y - restPivotY) * placed.Scale;
            wx = placed.PivotX + lx * c - ly * s;
            wy = placed.PivotY + lx * s + ly * c;
        }

        /// <summary>The reverse of <see cref="Transform"/>: from the placed figure back to the rest drawing.</summary>
        public static void Untransform(BoneWorld placed, float restPivotX, float restPivotY, float wx, float wy, out float x, out float y)
        {
            double r = -placed.Degrees * Math.PI / 180.0;
            float c = (float)Math.Cos(r), s = (float)Math.Sin(r);
            float dx = wx - placed.PivotX, dy = wy - placed.PivotY;
            float inv = placed.Scale > 0f ? 1f / placed.Scale : 1f;
            x = restPivotX + (dx * c - dy * s) * inv;
            y = restPivotY + (dx * s + dy * c) * inv;
        }

        /// <summary>The same skeleton reflected across x = 0, for the other facing.</summary>
        public RigSkeleton Mirrored()
        {
            var copy = new RigSkeleton();
            foreach (var bone in _order) copy.Bone(bone.Name, bone.Parent, -bone.PivotX, bone.PivotY);
            return copy;
        }
    }
}
