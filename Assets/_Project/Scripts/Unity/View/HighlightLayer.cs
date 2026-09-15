// Assets/_Project/Scripts/Unity/View/HighlightLayer.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Draws the things a player needs to see before committing: where a move
    /// would land, which cells an ability reaches, and which cells a
    /// cell-targeted cast would accept.
    /// </summary>
    /// <remarks>
    /// All are rebuilt from scratch each time they change. The marker count is
    /// small — at most a squad's landings plus a range window or one track's
    /// worth of targets — so pooling would be complexity without benefit, and a
    /// rebuild cannot drift out of sync with the state it is drawn from.
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

        /// <summary>
        /// Ghost markers on the cells this roll could carry each operator to.
        /// </summary>
        /// <remarks>
        /// <b>Two weights, because a split roll offers two answers per
        /// operator.</b> The bold marker is where the whole roll lands the piece;
        /// the faint smaller ones are where each single die would. Drawing them
        /// identically would be worse than drawing one — a player would read a
        /// cluster of equal ghosts as a range rather than a menu.
        ///
        /// <paramref name="perDie"/> is optional so the single-die case (one
        /// die left, nothing to choose between) stays a one-argument call.
        /// </remarks>
        public void ShowLandings(IEnumerable<CellRef> pooled, IEnumerable<CellRef> perDie = null)
        {
            if (pooled != null)
            {
                foreach (var cell in pooled)
                    Spawn(cell, _layout.CellSize * 0.92f, UiTheme.WithAlpha(UiTheme.CyanBright, 0.6f), 1);
            }

            if (perDie == null) return;

            foreach (var cell in perDie)
                Spawn(cell, _layout.CellSize * 0.58f, UiTheme.WithAlpha(UiTheme.Cyan, 0.3f), 1);
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
                    _layout.CellSize * 0.22f, ReachColour, 1, Primitives.Disc);

                Spawn(CellRef.Track(((origin.Index - step) % circuit + circuit) % circuit),
                    _layout.CellSize * 0.22f, ReachColour, 1, Primitives.Disc);
            }
        }

        /// <summary>
        /// The cells a cell-targeted cast would accept, with the chosen one
        /// drawn bold (ADR-0006).
        /// </summary>
        /// <remarks>
        /// Legality arrives from the engine already decided — range, home
        /// columns and the camping rule included — so this draws exactly the
        /// clickable set and nothing a cast would refuse. Amber rather than
        /// the reach cyan: these are not "cells in reach", they are "cells a
        /// strike will land on", and a player mid-aim should not have to
        /// remember which meaning the colour carries this time.
        /// </remarks>
        public void ShowCellTargets(IEnumerable<CellRef> legal, CellRef? chosen)
        {
            if (legal == null) return;

            foreach (var cell in legal)
                Spawn(cell, _layout.CellSize * 0.66f, UiTheme.WithAlpha(UiTheme.Threat, 0.55f), 1);

            if (chosen != null)
                Spawn(chosen.Value, _layout.CellSize * 0.98f, UiTheme.WithAlpha(UiTheme.Threat, 0.95f), 2);
        }

        /// <summary>
        /// Reach is drawn as dots, not rings: a selected operator can show its
        /// landings and its ability's reach at once, and both are cyan (live).
        /// Shape tells them apart.
        /// </summary>
        private static Color ReachColour => UiTheme.WithAlpha(UiTheme.Cyan, 0.55f);

        private void Spawn(CellRef cell, float size, Color colour, int order, Sprite sprite = null)
        {
            if (_layout == null) return;

            var go = new GameObject($"hl_{cell}");
            go.transform.SetParent(transform, false);
            go.transform.position = _layout.PositionOf(cell);
            go.transform.localScale = Vector3.one * size;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite ?? Primitives.Ring;
            renderer.color = colour;
            renderer.sortingOrder = order;

            _markers.Add(go);
        }
    }
}