// Assets/_Project/Scripts/Unity/View/Figures/Rig/RigPose.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>One bone's part of a pose: a turn at its joint, a shift, and a scale.</summary>
    public readonly struct BoneTurn
    {
        public static readonly BoneTurn Rest = new BoneTurn(0f, 0f, 0f, 1f);

        /// <summary>Counter-clockwise, in degrees, relative to the parent bone.</summary>
        public readonly float Degrees;
        public readonly float Dx, Dy;
        public readonly float Scale;

        public BoneTurn(float degrees, float dx = 0f, float dy = 0f, float scale = 1f)
        {
            Degrees = degrees;
            Dx = dx;
            Dy = dy;
            Scale = scale;
        }

        public static BoneTurn Lerp(BoneTurn a, BoneTurn b, float t) => new BoneTurn(
            a.Degrees + (b.Degrees - a.Degrees) * t,
            a.Dx + (b.Dx - a.Dx) * t,
            a.Dy + (b.Dy - a.Dy) * t,
            a.Scale + (b.Scale - a.Scale) * t);
    }

    /// <summary>
    /// A pose as data (OPERATOR_LOOKBOOK.md, LB5): a turn per bone, which
    /// parts are hidden or shown, and, for the seated pose, the table line
    /// below which nothing is drawn. Bones a pose does not name stay at rest.
    /// </summary>
    public sealed class RigPose
    {
        private readonly Dictionary<string, BoneTurn> _turns = new Dictionary<string, BoneTurn>();
        private readonly HashSet<string> _hide = new HashSet<string>();
        private readonly HashSet<string> _show = new HashSet<string>();

        public string Name { get; }

        /// <summary>Figure-space height of the table's near edge, for a seated pose; null when standing.</summary>
        public float? TableLine { get; private set; }

        public RigPose(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

        public RigPose Turn(string bone, float degrees, float dx = 0f, float dy = 0f, float scale = 1f)
        {
            _turns[bone] = new BoneTurn(degrees, dx, dy, scale);
            return this;
        }

        /// <summary>Hides a part the pose does not use (Bouncer's gauntlet, off at the table).</summary>
        public RigPose Hide(string part)
        {
            _hide.Add(part);
            return this;
        }

        /// <summary>Shows a prop part, which is hidden in every pose that does not ask for it.</summary>
        public RigPose Show(string part)
        {
            _show.Add(part);
            return this;
        }

        public RigPose AtTable(float tableLine)
        {
            TableLine = tableLine;
            return this;
        }

        public BoneTurn Get(string bone) => _turns.TryGetValue(bone, out var turn) ? turn : BoneTurn.Rest;

        public bool Hides(string part) => _hide.Contains(part);

        public bool Shows(string part) => _show.Contains(part);

        public IEnumerable<string> Bones => _turns.Keys;

        /// <summary>A copy under a new name, to build a variation on (a recipe's own cast from the template's).</summary>
        public RigPose Copy(string name)
        {
            var copy = new RigPose(name) { TableLine = TableLine };
            foreach (var pair in _turns) copy._turns[pair.Key] = pair.Value;
            copy._hide.UnionWith(_hide);
            copy._show.UnionWith(_show);
            return copy;
        }

        /// <summary>
        /// Between two poses. Turns blend; parts and the table line switch at
        /// the halfway point, which is where a cut would put them.
        /// </summary>
        public static RigPose Lerp(RigPose a, RigPose b, float t)
        {
            var near = t < 0.5f ? a : b;
            var pose = new RigPose(a.Name + "→" + b.Name) { TableLine = near.TableLine };
            pose._hide.UnionWith(near._hide);
            pose._show.UnionWith(near._show);

            var names = new HashSet<string>(a._turns.Keys);
            names.UnionWith(b._turns.Keys);
            foreach (var bone in names) pose._turns[bone] = BoneTurn.Lerp(a.Get(bone), b.Get(bone), t);

            return pose;
        }

        /// <summary>The pose for the other facing: every turn and shift reflected.</summary>
        public RigPose Mirrored()
        {
            var copy = new RigPose(Name) { TableLine = TableLine };
            foreach (var pair in _turns)
                copy._turns[pair.Key] = new BoneTurn(-pair.Value.Degrees, -pair.Value.Dx, pair.Value.Dy, pair.Value.Scale);
            copy._hide.UnionWith(_hide);
            copy._show.UnionWith(_show);
            return copy;
        }
    }
}
