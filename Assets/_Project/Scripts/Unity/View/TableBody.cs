// Assets/_Project/Scripts/Unity/View/TableBody.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The table's thickness, seen along its near edge (VISUAL_PASS.md, V2): a
    /// dark side face with a gilt band at its lip, hanging under the board so
    /// the table reads as a slab in a room rather than a pattern on the floor.
    /// </summary>
    /// <remarks>
    /// <b>One edge, because one edge is all there is to see.</b> The camera
    /// tilts about x alone, so the table's left and right sides are seen
    /// exactly edge-on and its far side is behind the surface. Building four
    /// would cost four times the geometry to show one.
    ///
    /// <b>It only exists under the tilt</b>, the same as
    /// <see cref="RoomBackdrop"/> and for the same reason: the face stands
    /// perpendicular to the board, which a straight-down camera sees edge-on.
    /// Unlike the room it belongs to a match as much as to a menu, so it reads
    /// <see cref="BoardTilt"/> itself rather than waiting to be told which
    /// screen is up.
    ///
    /// <b>Down is +z.</b> Screen up is <c>(0, cos p, -sin p)</c>
    /// (<see cref="FigureTilt.ScreenUp"/>), which the hop and the figures'
    /// lean both follow, so the direction away from the viewer's up - the way
    /// a table's thickness hangs - is +z. The sprites are authored lip-up, the
    /// way they are seen, and <see cref="Hang"/> flips them.
    ///
    /// <b>It hangs inside the framing's air.</b> <c>FrameCamera</c> fits the
    /// board's four corners with <c>FrameMargin</c>'s 12% of slack and knows
    /// nothing about a slab below the near edge, so <see cref="Thickness"/> is
    /// kept small enough to land inside that slack. Deepen it much and the lip
    /// goes under the action tray in a match, and off the bottom of the screen
    /// on the menus.
    /// </remarks>
    public sealed class TableBody : MonoBehaviour
    {
        /// <summary>How deep the slab is, in cell spacings. See the note on the framing's air.</summary>
        private const float Thickness = 0.5f;

        /// <summary>The gilt band's share of the slab, measured down from the lip.</summary>
        private const float BandShare = 0.28f;

        // Behind the board's own art, which BoardView draws from -33, and in
        // front of the room, which starts at -80.
        private const int FaceOrder = -36;
        private const int BandOrder = -35;

        private GameObject _root;
        private bool _applied;

        /// <summary>
        /// Builds the body around a board of this layout. Safe to call again;
        /// the previous one is destroyed, so a new profile re-sizes it.
        /// </summary>
        public void Build(BoardLayout layout)
        {
            Clear();
            if (layout == null) return;

            float spacing = layout.Spacing;
            var centre = layout.HomeGoalPosition;

            // The same square DrawFloor lays the carpet over.
            float side = (layout.GridSize + 2f * BoardLayout.TableMargin) * spacing;
            float depth = Thickness * spacing;
            float band = depth * BandShare;

            _root = new GameObject("table_body");
            _root.transform.SetParent(transform, false);

            // The near edge is the low-y side: the one the camera stands in
            // front of and looks up the board from.
            float edgeY = centre.y - side * 0.5f;

            Hang("table_edge", BoardArt.TableEdge, new Vector3(centre.x, edgeY, 0f), side, depth,
                UiTheme.TableEdge, FaceOrder);
            Hang("table_band", BoardArt.TableBand, new Vector3(centre.x, edgeY, 0f), side, band,
                UiTheme.TableBand, BandOrder);

            Apply();
        }

        public void Clear()
        {
            if (_root != null) Destroy(_root);
            _root = null;
            _applied = false;
        }

        /// <summary>
        /// Off under a flat camera. Driven here rather than by the composition
        /// root because the tilt is the only thing this depends on, and it can
        /// change mid-session - the Board camera setting flips it, and the
        /// title screen is flat on purpose (V3b).
        /// </summary>
        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (_root == null) return;

            bool on = BoardTilt.IsTilted;
            if (on == _applied && _root.activeSelf == on) return;

            _applied = on;
            _root.SetActive(on);
        }

        /// <summary>
        /// A sprite hung from the board's plane: stood a quarter turn about x
        /// so it faces the camera, then scaled to <paramref name="width"/> by
        /// <paramref name="height"/> world units with its lip at
        /// <paramref name="lip"/> and its body below.
        /// </summary>
        /// <remarks>
        /// The quarter turn maps the sprite's own up to world +z, which is
        /// down here, so the renderer is flipped: the art stays authored the
        /// way it is seen and the inversion is stated once, in one place.
        /// </remarks>
        private void Hang(string name, Sprite sprite, Vector3 lip,
            float width, float height, Color colour, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var size = sprite.bounds.size;
            float sx = size.x > 0f ? width / size.x : 1f;
            float sy = size.y > 0f ? height / size.y : 1f;
            go.transform.localScale = new Vector3(sx, sy, 1f);

            // The sprite is pivoted at its middle, so drop it by half its
            // height along what is now world down.
            go.transform.position = lip + new Vector3(0f, 0f, height * 0.5f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.flipY = true;
            renderer.sortingLayerName = SceneLighting.BoardLayer;
            renderer.sortingOrder = order;
        }
    }
}
