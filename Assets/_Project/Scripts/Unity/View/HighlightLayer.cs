// Assets/_Project/Scripts/Unity/View/HighlightLayer.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Draws the two things a player needs to see before committing: where a
    /// move would land, and which cells an ability reaches.
    /// </summary>
    /// <remarks>
    /// Both are rebuilt from scratch each time they change. The marker count is
    /// small — at most a squad's landings plus a range window — so pooling would
    /// be complexity without benefit, and a rebuild cannot drift out of sync
    /// with the state it is drawn from.
    /// </remarks>
    public sealed class HighlightLayer : MonoBehaviour
    {
        private readonly List<GameObject> _markers = new List<GameObject>();
        private BoardLayout _layout;

        public void Bind(BoardLayout layout) => _layout = layout;

        public void Clear()
        {
            foreach (var marker in _markers)
                if (marker != null) Destroy(marker);

            _markers.Clear();
        }

        /// <summary>Ghost markers on the cells this roll would carry each operator to.</summary>
        public void ShowLandings(IEnumerable<CellRef> cells)
        {
            foreach (var cell in cells)
                Spawn(cell, _layout.CellSize * 0.92f, new Color(0.95f, 0.92f, 0.70f, 0.35f), 1);
        }

        /// <summary>
        /// The cells an ability can reach, counted along the track in both
        /// directions — the only measure of distance the rules use (§4.1).
        /// </summary>
        public void ShowRange(PathMap map, CellRef origin, int range)
        {
            if (!origin.IsOnTrack || range <= 0) return;

            int circuit = map.Profile.CircuitLength;

            for (int step = 1; step <= range; step++)
            {
                Spawn(CellRef.Track(((origin.Index + step) % circuit + circuit) % circuit),
                    _layout.CellSize * 0.55f, new Color(0.45f, 0.75f, 0.95f, 0.55f), 1);

                Spawn(CellRef.Track(((origin.Index - step) % circuit + circuit) % circuit),
                    _layout.CellSize * 0.55f, new Color(0.45f, 0.75f, 0.95f, 0.55f), 1);
            }
        }

        private void Spawn(CellRef cell, float size, Color colour, int order)
        {
            if (_layout == null) return;

            var go = new GameObject($"hl_{cell}");
            go.transform.SetParent(transform, false);
            go.transform.position = _layout.PositionOf(cell);
            go.transform.localScale = Vector3.one * size;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Primitives.Ring;
            renderer.color = colour;
            renderer.sortingOrder = order;

            _markers.Add(go);
        }
    }
}