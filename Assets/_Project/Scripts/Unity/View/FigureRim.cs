// Assets/_Project/Scripts/Unity/View/FigureRim.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A coloured rim round a standing figure (LAUNCH_UI_PASS.md, G8d): its
    /// white silhouettes drawn eight times, nudged outward, just behind it.
    /// </summary>
    /// <remarks>
    /// <b>Why not the shader pack's outline.</b> <c>OUTBASE_ON</c> traces
    /// each sprite's alpha inside its own quad, so a tightly cropped part
    /// loses its outline at the quad's edge, and a rig of a dozen parts would
    /// draw a dozen outlines, seams included. Copies behind the figure have
    /// neither problem: the figure covers every copy but the ring round its
    /// outside, whatever it is made of.
    ///
    /// <b>What it copies.</b> Each <see cref="Source"/> pairs the renderer
    /// whose sprite is a white silhouette (a rig part's flash, a painted
    /// figure's flash overlay, the code-drawn pawn itself) with the renderer
    /// that says whether that part is showing. The copies are children of
    /// the silhouette's transform, so they follow every pose, facing and
    /// squash without being told; <see cref="Sync"/> only copies the sprite,
    /// the flips and the visibility each frame, since a facing or a seat
    /// swaps sprites on the same renderers.
    ///
    /// Plain C# owned by the piece.
    /// </remarks>
    internal sealed class FigureRim
    {
        /// <summary>A part to rim: where its silhouette comes from, and whether it is shown.</summary>
        internal readonly struct Source
        {
            public Source(SpriteRenderer silhouette, SpriteRenderer shown)
            {
                Silhouette = silhouette;
                Shown = shown;
            }

            public SpriteRenderer Silhouette { get; }
            public SpriteRenderer Shown { get; }
        }

        private static readonly Vector2[] Directions =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
        };

        private readonly List<Source> _sources = new List<Source>();
        private readonly List<SpriteRenderer[]> _copies = new List<SpriteRenderer[]>();
        private Color _colour;

        /// <summary>Whether copies exist.</summary>
        public bool Built => _copies.Count > 0;

        /// <summary>
        /// Makes the copies: for each source, eight renderers at
        /// <paramref name="width"/> (in <paramref name="frame"/>'s units) from
        /// it, at <paramref name="order"/> inside the piece's sorting group.
        /// </summary>
        /// <param name="frame">The transform the width is measured in: the piece's root.</param>
        /// <returns>False when the frame has no size yet (a hidden piece), so the caller tries again later.</returns>
        public bool Build(IEnumerable<Source> sources, Transform frame, float width, int order, Color colour)
        {
            Clear();
            _colour = colour;

            float frameScale = Mathf.Abs(frame.lossyScale.x);
            if (frameScale <= 0f) return false;

            foreach (var source in sources)
            {
                if (source.Silhouette == null || source.Shown == null) continue;

                // The copies sit in the silhouette's own frame; convert the width into it.
                float local = width * frameScale / Mathf.Max(1e-5f, Mathf.Abs(source.Silhouette.transform.lossyScale.x));

                var set = new SpriteRenderer[Directions.Length];
                for (int i = 0; i < Directions.Length; i++)
                {
                    var go = new GameObject("rim");
                    go.transform.SetParent(source.Silhouette.transform, false);
                    go.transform.localPosition = new Vector3(Directions[i].x * local, Directions[i].y * local, 0f);

                    var copy = go.AddComponent<SpriteRenderer>();
                    copy.sortingLayerID = source.Silhouette.sortingLayerID;
                    copy.sortingOrder = order;
                    copy.color = colour;
                    copy.enabled = false;
                    set[i] = copy;
                }

                _sources.Add(source);
                _copies.Add(set);
            }

            return true;
        }

        /// <summary>Copies each source's sprite, flips and visibility, in <paramref name="colour"/> at <paramref name="alpha"/>.</summary>
        public void Sync(Color colour, float alpha)
        {
            _colour = new Color(colour.r, colour.g, colour.b, colour.a * alpha);

            for (int s = 0; s < _sources.Count; s++)
            {
                var source = _sources[s];
                bool on = source.Shown != null && source.Shown.enabled && source.Silhouette != null &&
                          source.Silhouette.sprite != null;

                foreach (var copy in _copies[s])
                {
                    if (copy == null) continue;
                    copy.enabled = on;
                    if (!on) continue;

                    copy.sprite = source.Silhouette.sprite;
                    copy.flipX = source.Silhouette.flipX;
                    copy.flipY = source.Silhouette.flipY;
                    copy.color = _colour;
                }
            }
        }

        /// <summary>Hides every copy without destroying them.</summary>
        public void Hide()
        {
            foreach (var set in _copies)
            foreach (var copy in set)
                if (copy != null && copy.enabled) copy.enabled = false;
        }

        /// <summary>Destroys the copies. The next <see cref="Build"/> makes them afresh.</summary>
        public void Clear()
        {
            foreach (var set in _copies)
            foreach (var copy in set)
                if (copy != null) Object.Destroy(copy.gameObject);

            _copies.Clear();
            _sources.Clear();
        }
    }
}
