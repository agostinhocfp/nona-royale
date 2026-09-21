// Assets/_Project/Scripts/Unity/View/Figures/Rig/OperatorRig.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// One body part of a rig (OPERATOR_LOOKBOOK.md, LB5): a flat drawing in
    /// figure space at rest, riding one bone, drawn at one depth.
    /// </summary>
    public sealed class RigPart
    {
        public string Name { get; }
        public string Bone { get; }

        /// <summary>Back to front: lower draws first.</summary>
        public int Order { get; }

        /// <summary>A prop is hidden unless a pose shows it (the gauntlet set down on the table).</summary>
        public bool Prop { get; }

        public FigureDrawing Drawing { get; }

        public RigPart(string name, string bone, int order, FigureDrawing drawing, bool prop = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Bone = bone ?? throw new ArgumentNullException(nameof(bone));
            Order = order;
            Drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
            Prop = prop;
        }

        public bool VisibleIn(RigPose pose) =>
            pose == null ? !Prop : Prop ? pose.Shows(Name) : !pose.Hides(Name);

        public RigPart Mirrored() => new RigPart(Name, Bone, Order, Drawing.Mirrored(), Prop);
    }

    /// <summary>A point on the floor that a pose must not move: a sole, carried by its shin.</summary>
    public readonly struct RigAnchor
    {
        public readonly string Bone;
        public readonly float X, Y;

        public RigAnchor(string bone, float x, float y)
        {
            Bone = bone;
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// One operator's rig (OPERATOR_LOOKBOOK.md, LB5): the skeleton, the parts
    /// as a pure function of the palette, and the poses, which start from the
    /// build's shared template and are overridden only where the operator is
    /// its own (the cast, the seated activity).
    /// </summary>
    /// <remarks>
    /// Authored facing the right of the screen. <see cref="Mirrored"/> faces
    /// it left by reflecting the geometry, so the rasteriser still lights it
    /// from the upper left (§2.2 rule 5).
    /// </remarks>
    public sealed class OperatorRig
    {
        private readonly Func<LookBookPalette, IReadOnlyList<RigPart>> _parts;
        private readonly Dictionary<string, RigPose> _poses;

        public string Name { get; }
        public string Key { get; }
        public RigSkeleton Skeleton { get; }
        public IReadOnlyList<RigAnchor> Feet { get; }

        /// <summary>True for the left-facing copy.</summary>
        public bool FacesLeft { get; }

        public OperatorRig(string name, RigSkeleton skeleton, IReadOnlyList<RigAnchor> feet,
            Func<LookBookPalette, IReadOnlyList<RigPart>> parts, IDictionary<string, RigPose> poses)
            : this(name, skeleton, feet, parts, new Dictionary<string, RigPose>(poses), false)
        {
        }

        private OperatorRig(string name, RigSkeleton skeleton, IReadOnlyList<RigAnchor> feet,
            Func<LookBookPalette, IReadOnlyList<RigPart>> parts, Dictionary<string, RigPose> poses, bool facesLeft)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Key = OperatorArtNames.Key(name);
            Skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
            Feet = feet ?? Array.Empty<RigAnchor>();
            _parts = parts ?? throw new ArgumentNullException(nameof(parts));
            _poses = poses;
            FacesLeft = facesLeft;

            if (!_poses.ContainsKey(RigPoseNames.Rest)) _poses[RigPoseNames.Rest] = new RigPose(RigPoseNames.Rest);
        }

        public IEnumerable<string> PoseNames => _poses.Keys;

        public RigPose Pose(string name) => _poses.TryGetValue(name, out var pose) ? pose : _poses[RigPoseNames.Rest];

        public bool HasPose(string name) => _poses.ContainsKey(name);

        /// <summary>The parts, back to front.</summary>
        public IReadOnlyList<RigPart> Parts(LookBookPalette palette)
        {
            var parts = new List<RigPart>(_parts(palette));
            parts.Sort((a, b) => a.Order.CompareTo(b.Order));
            return parts;
        }

        /// <summary>The same rig facing the other way, lit from the same side.</summary>
        public OperatorRig Mirrored()
        {
            var poses = new Dictionary<string, RigPose>();
            foreach (var pair in _poses) poses[pair.Key] = pair.Value.Mirrored();

            var feet = new List<RigAnchor>();
            foreach (var foot in Feet) feet.Add(new RigAnchor(foot.Bone, -foot.X, foot.Y));

            var parts = _parts;
            return new OperatorRig(Name, Skeleton.Mirrored(), feet, palette =>
            {
                var mirrored = new List<RigPart>();
                foreach (var part in parts(palette)) mirrored.Add(part.Mirrored());
                return mirrored;
            }, poses, !FacesLeft);
        }

        public override string ToString() => Name;
    }
}
