// Assets/_Project/Scripts/Unity/View/DeviceLayer.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Services;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Draws every pending beacon and zone: the cells it will strike, tinted in
    /// the owner's colour, and a mark on the anchor cell.
    /// </summary>
    /// <remarks>
    /// <b>This is what ADR-0006 decision 6 asks for.</b> A beacon exists so
    /// opponents can see it and choose whether to step off. Until this layer,
    /// both devices were visible only in the event log, which made them the
    /// hidden trap the ADR rejects.
    ///
    /// <b>Drawn from <c>GameEngine.ActiveCellEffects</c> after every batch of
    /// events, never from placement or resolution events</b> (PRESENTATION
    /// §1). The area comes from the core too, so the drawn cells are exactly
    /// the cells that will be struck.
    ///
    /// <b>The two devices look different</b> (ADR-0007):
    /// - a beacon is a target mark (small ring and centre dot) over a faint
    ///   area: one beam, coming. Not <c>Primitives.Cross</c>, which is Javi's
    ///   silhouette and would read as a piece;
    /// - an armed zone is a heavier area with a ring: it will go off;
    /// - a zone that has gone off drops to a fainter area with a thin ring: it
    ///   still bills whoever stands in it, but its big moment is over.
    ///
    /// <b>Sorting order 0</b>, between the board cells (−1, −2) and the move
    /// and aim highlights (1, 2), so a player aiming a cast still sees the aim
    /// on top. Pieces sit above all of it.
    ///
    /// Rebuilt from scratch on every change, like <see cref="HighlightLayer"/>:
    /// there are a handful of devices at most, and a rebuild cannot drift.
    /// </remarks>
    public sealed class DeviceLayer : MonoBehaviour
    {
        private const int Order = 0;

        private const float BeaconAreaAlpha = 0.16f;
        private const float ArmedZoneAreaAlpha = 0.30f;
        private const float LingeringZoneAreaAlpha = 0.18f;

        private readonly List<GameObject> _markers = new List<GameObject>();
        private BoardLayout _layout;

        public void Bind(BoardLayout layout) => _layout = layout;

        public void Clear()
        {
            foreach (var marker in _markers)
                if (marker != null) Destroy(marker);

            _markers.Clear();
        }

        /// <summary>Replaces everything drawn with the given effects.</summary>
        public void Show(IReadOnlyList<CellEffectSnapshot> effects)
        {
            Clear();

            if (_layout == null || effects == null) return;

            foreach (var effect in effects)
            {
                var seat = BoardLayout.ColourOf(effect.Owner);

                float areaAlpha = !effect.IsZone ? BeaconAreaAlpha
                    : effect.HasDetonated ? LingeringZoneAreaAlpha
                    : ArmedZoneAreaAlpha;

                foreach (var cell in effect.Covered)
                    Spawn(cell, Primitives.Square, _layout.CellSize * 0.9f, WithAlpha(seat, areaAlpha));

                if (!effect.IsZone)
                {
                    Spawn(effect.Cell, Primitives.Ring, _layout.CellSize * 0.7f, WithAlpha(seat, 0.9f));
                    Spawn(effect.Cell, Primitives.Disc, _layout.CellSize * 0.18f, WithAlpha(seat, 0.9f));
                }
                else
                {
                    float ringAlpha = effect.HasDetonated ? 0.45f : 0.9f;
                    float ringSize = effect.HasDetonated ? 0.8f : 1.0f;

                    Spawn(effect.Cell, Primitives.Ring, _layout.CellSize * ringSize, WithAlpha(seat, ringAlpha));
                }
            }
        }

        private static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }

        private void Spawn(CellRef cell, Sprite sprite, float size, Color colour)
        {
            var go = new GameObject($"device_{cell}");
            go.transform.SetParent(transform, false);
            go.transform.position = _layout.PositionOf(cell);
            go.transform.localScale = Vector3.one * size;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.sortingOrder = Order;

            _markers.Add(go);
        }
    }
}
