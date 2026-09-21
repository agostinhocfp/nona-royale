// Assets/_Project/Scripts/Unity/View/Figures/Rig/RigView.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A rig drawn on the board (OPERATOR_LOOKBOOK.md, LB5b): one
    /// <see cref="SpriteRenderer"/> per part, turned at its joint every frame,
    /// with a white silhouette over each for the hit flash.
    /// </summary>
    /// <remarks>
    /// <b>Where it hangs.</b> Under the piece's body child, in figure units,
    /// so <see cref="FigureLayout.Fit"/>'s scale and offset place it exactly
    /// as they place a render, and the lean, the pop and the hover act on it
    /// unchanged. Inside the piece's <c>SortingGroup</c>, so a near piece still
    /// covers a far one as a whole.
    ///
    /// <b>Draw order</b> is explicit: part <i>i</i> at
    /// <c>baseOrder + 2i</c>, its flash one above. Nothing relies on depth
    /// inside the group, which a tilted camera would make unreliable.
    ///
    /// <b>Facing</b> swaps every part's sprite and offset for the other
    /// facing's; the renderers stay. <b>Seated</b> swaps in the parts cut at
    /// the table line, and hides those under it.
    ///
    /// Plain C# owned by the piece, not a component: the piece already runs
    /// the frame, and this only needs telling what to show.
    /// </remarks>
    public sealed class RigView
    {
        private readonly RigArt _art;
        private readonly GameObject _root;
        private readonly Transform[] _joints;
        private readonly Transform[] _holders;
        private readonly SpriteRenderer[] _sprites;
        private readonly SpriteRenderer[] _flashes;
        private readonly BoneWorld[] _world;

        private bool _facesLeft;
        private bool _seated;
        private bool _powered;
        private float _alpha = 1f;
        private float _flash;
        private bool _active = true;

        public RigView(Transform parent, RigArt art, int baseOrder)
        {
            _art = art;
            _root = new GameObject("rig");
            _root.transform.SetParent(parent, false);

            var parts = art.Right.Parts;
            int count = parts.Count;
            _joints = new Transform[count];
            _holders = new Transform[count];
            _sprites = new SpriteRenderer[count];
            _flashes = new SpriteRenderer[count];
            _world = new BoneWorld[art.Right.Rig.Skeleton.Bones.Count];

            for (int i = 0; i < count; i++)
            {
                var joint = new GameObject(parts[i].Part.Name).transform;
                joint.SetParent(_root.transform, false);
                _joints[i] = joint;

                var holder = new GameObject("art");
                holder.transform.SetParent(joint, false);
                _holders[i] = holder.transform;
                _sprites[i] = holder.AddComponent<SpriteRenderer>();
                _sprites[i].sortingOrder = baseOrder + 2 * i;

                var flash = new GameObject("flash");
                flash.transform.SetParent(holder.transform, false);
                _flashes[i] = flash.AddComponent<SpriteRenderer>();
                _flashes[i].sortingOrder = baseOrder + 2 * i + 1;
                _flashes[i].enabled = false;
            }

            Assign();
        }

        public RigArt Art => _art;

        public RigFacingArt Current => _art.Facing(_facesLeft);

        public bool FacesLeft
        {
            get => _facesLeft;
            set
            {
                if (_facesLeft == value) return;
                _facesLeft = value;
                Assign();
            }
        }

        public bool Seated
        {
            get => _seated;
            set
            {
                if (_seated == value) return;
                _seated = value;
                Assign();
            }
        }

        /// <summary>
        /// The cast's cyan tell (LB5c): parts with a powered sprite show it,
        /// standing only. Everything else is untouched, so only the device lights.
        /// </summary>
        public bool Powered
        {
            get => _powered;
            set
            {
                if (_powered == value) return;
                _powered = value;
                Assign();
            }
        }

        public bool Active
        {
            get => _active;
            set
            {
                _active = value;
                if (_root != null) _root.SetActive(value);
            }
        }

        /// <summary>Places every part for the blend of two poses of the current facing's rig.</summary>
        public void Apply(RigPose a, RigPose b, float t)
        {
            if (!_active) return;

            var facing = Current;
            var skeleton = facing.Rig.Skeleton;
            skeleton.Evaluate(a, b, t, _world);

            // Which parts show switches at the halfway point, as RigPose.Lerp has it.
            var shown = t < 0.5f ? a : b;
            var parts = facing.Parts;

            for (int i = 0; i < parts.Count; i++)
            {
                var placed = _world[parts[i].BoneIndex];
                var joint = _joints[i];
                joint.localPosition = new Vector3(placed.PivotX, placed.PivotY, 0f);
                joint.localRotation = Quaternion.Euler(0f, 0f, placed.Degrees);
                joint.localScale = new Vector3(placed.Scale, placed.Scale, 1f);

                bool visible = _sprites[i].sprite != null && parts[i].Part.VisibleIn(shown);
                if (_sprites[i].enabled != visible) _sprites[i].enabled = visible;

                bool flashing = visible && _flash > 0f && _flashes[i].sprite != null;
                if (_flashes[i].enabled != flashing) _flashes[i].enabled = flashing;
            }
        }

        /// <summary>Where a rest-pose point on a bone is in the last applied pose, in figure units.</summary>
        public Vector2 Place(string bone, float x, float y)
        {
            var skeleton = Current.Rig.Skeleton;
            int index = skeleton.IndexOf(bone);
            if (index < 0) return new Vector2(x, y);

            var rest = skeleton.Bones[index];
            RigSkeleton.Transform(_world[index], rest.PivotX, rest.PivotY, x, y, out float wx, out float wy);
            return new Vector2(wx, wy);
        }

        /// <summary>Opacity of the whole figure: the evasive fade.</summary>
        public void SetAlpha(float alpha)
        {
            _alpha = alpha;
            var colour = new Color(1f, 1f, 1f, alpha);
            foreach (var sprite in _sprites) sprite.color = colour;
            Tint();
        }

        /// <summary>The white-out's strength, 0..1, already scaled for the evasive fade by the caller.</summary>
        public void Flash(float amount)
        {
            _flash = Mathf.Max(0f, amount);
            Tint();
        }

        /// <summary>The union of every drawn part's world bounds; false when nothing is drawn.</summary>
        public bool TryBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            if (!_active) return false;

            foreach (var sprite in _sprites)
            {
                if (!sprite.enabled || sprite.sprite == null) continue;

                if (!any) bounds = sprite.bounds;
                else bounds.Encapsulate(sprite.bounds);
                any = true;
            }

            return any;
        }

        public void Destroy()
        {
            if (_root == null) return;
            if (Application.isPlaying) Object.Destroy(_root);
            else Object.DestroyImmediate(_root);
        }

        private void Tint()
        {
            var colour = new Color(1f, 1f, 1f, _flash);
            foreach (var flash in _flashes)
            {
                flash.color = colour;
                if (_flash <= 0f && flash.enabled) flash.enabled = false;
            }
        }

        /// <summary>Puts the current facing's and pose's sprites on the renderers.</summary>
        private void Assign()
        {
            var parts = Current.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var sprite = _seated ? part.Seated : _powered && part.Powered != null ? part.Powered : part.Standing;
                var silhouette = _seated ? part.SeatedSilhouette : part.StandingSilhouette;
                var offset = _seated ? part.SeatedOffset : part.StandingOffset;

                _sprites[i].sprite = sprite;
                _flashes[i].sprite = silhouette;
                _holders[i].localPosition = new Vector3(offset.x, offset.y, 0f);
                if (sprite == null) _sprites[i].enabled = false;
            }
        }
    }
}
