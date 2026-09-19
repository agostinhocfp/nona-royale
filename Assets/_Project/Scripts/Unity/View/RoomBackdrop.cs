// Assets/_Project/Scripts/Unity/View/RoomBackdrop.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The salon behind the setup, draft and end screens: a velvet wall with
    /// its trim, and a ring of sconces on side columns framing the table
    /// (VISUAL_PASS.md, V3).
    /// </summary>
    /// <remarks>
    /// <b>It only exists under the tilt.</b> Every part of the room stands
    /// vertically in world space, and a straight-down camera sees a vertical
    /// plane edge-on - the wall and the columns would vanish, and nothing
    /// would be left but the board. That is why the Board camera setting became
    /// match-only in V3: it no longer gives the menus two looks.
    /// <see cref="Visible"/> is guarded by <see cref="BoardTilt.IsTilted"/> on
    /// top of that, which covers both a tilt that cannot be solved for the
    /// window and the title, which is deliberately flat and so shows no room at
    /// all (V3b).
    ///
    /// <b>Geometry is the approved mockup's, scaled.</b> The numbers below are
    /// in `tools/mockup/scene.py`'s units, where the table's half-side is 8.3,
    /// and are multiplied into board units by <see cref="_scale"/>. Keeping the
    /// mockup's frame of reference means the render that was signed off and the
    /// scene that ships can be compared number for number. They are written the
    /// way the mockup states them - z counting up from the floor - and
    /// <see cref="Stand"/> turns that into world z, which runs the other way.
    ///
    /// <b>The light is painted, not cast.</b> The sconces carry their own halo
    /// and flame (<see cref="RoomArt"/>), so the room still reads with Lighting
    /// effects off, the way G4's corner lamps did. Real <c>Light2D</c>s over
    /// the top are a follow-up.
    ///
    /// <b>The chandeliers are gone</b> (designer, 2026-09-19). The pair was
    /// placed so as not to land on the wordmark, and the title they were
    /// composed for no longer shows the room at all.
    ///
    /// <b>Brightness is an inspector field, not a constant.</b> It was set from
    /// a Blender approximation of the game's lights; the value that matters is
    /// the one that looks right under the real URP lights and V4's grade, and
    /// this is read every time the room is built so it can be dragged.
    /// </remarks>
    public sealed class RoomBackdrop : MonoBehaviour
    {
        /// <summary>The mockup's table half-side, the unit its geometry is written in.</summary>
        private const float MockHalfSide = 8.3f;

        // ── Geometry, in mockup units (scene.py, ROOM=salon) ────────────

        private const float WallY = 17.5f, WallHalfWidth = 24f, WallHeight = 21f;
        private const float PelmetZ = 20.2f, PelmetHeight = 1.1f, SkirtingZ = 0.28f, SkirtingHeight = 0.56f;
        private const float ColumnX = 15.5f, ColumnHeight = 14f, ColumnWidth = 1.35f;
        private const float SconceZ = 6.4f, SconceSize = 1.5f;
        private static readonly float[] ColumnYs = { 12.5f, 4.5f, -3.5f };

        [Header("Room (VISUAL_PASS.md, V3)")]
        [Tooltip("Multiplies the room's painted light. 1 is the light the salon was approved at; " +
                 "the mockup wanted about 2.6 to make the wall read, but that was Blender's " +
                 "approximation of the lights, not URP's. Tune it in Play Mode.")]
        [Range(0f, 4f)] public float brightness = 2.6f;

        [Tooltip("Lifts the velvet off near-black. Below about 2 the wall stops reading as a wall.")]
        [Range(0.5f, 4f)] public float velvet = 2.4f;

        private GameObject _root;
        private float _scale;
        private bool _wanted;
        private bool _applied;

        /// <summary>
        /// Whether the room should be on screen. Set by the composition root
        /// from the current screen; the room is a menu backdrop and never
        /// competes with a match on the table.
        /// </summary>
        public bool Visible
        {
            get => _wanted;
            set
            {
                _wanted = value;
                Apply();
            }
        }

        /// <summary>
        /// Builds the room around a board of this layout. Safe to call again;
        /// the previous room is destroyed, so a new profile re-sizes it.
        /// </summary>
        public void Build(BoardLayout layout)
        {
            Clear();
            if (layout == null) return;

            _scale = layout.Extent / MockHalfSide;

            _root = new GameObject("room");
            _root.transform.SetParent(transform, false);

            Wall();
            Columns();

            Apply();
        }

        public void Clear()
        {
            if (_root != null) Destroy(_root);
            _root = null;
        }

        private void Apply()
        {
            if (_root == null) return;

            // A tilt that could not be solved leaves the camera flat, and a
            // vertical room is invisible to it - worse, its sprites would still
            // be drawn as edge-on slivers. Off is the honest state.
            // Driven every frame from the current screen, so only act on a
            // change: BoardTilt can flip under it when the setting does.
            bool on = _wanted && BoardTilt.IsTilted;
            if (on == _applied && _root.activeSelf == on) return;

            _applied = on;
            _root.SetActive(on);
        }

        // ── Parts ───────────────────────────────────────────────────────

        private void Wall()
        {
            float y = WallY * _scale;

            Stand("wall", RoomArt.Wall, new Vector3(0f, y, 0f),
                WallHalfWidth * 2f * _scale, WallHeight * _scale, Lit(UiTheme.BloodVelvet, velvet), -80);

            // The trim sits a touch in front of the cloth. Sorting decides what
            // draws over what, but the offset keeps them from z-fighting if the
            // renderer is ever given a depth sort.
            float front = y - 0.05f * _scale;

            Stand("pelmet", RoomArt.Pelmet, new Vector3(0f, front, PelmetZ * _scale),
                WallHalfWidth * 2f * _scale, PelmetHeight * 2.4f * _scale, Lit(UiTheme.Gunmetal, 1.6f), -78);

            Stand("rail", RoomArt.Rail, new Vector3(0f, front, (PelmetZ - 0.75f) * _scale),
                WallHalfWidth * 2f * _scale, 0.5f * _scale, Lit(UiTheme.Gold, 1f), -77);

            Stand("skirting", RoomArt.Skirting, new Vector3(0f, front, SkirtingZ * _scale),
                WallHalfWidth * 2f * _scale, SkirtingHeight * 2.2f * _scale, Lit(UiTheme.Gunmetal, 1.1f), -76);
        }

        private void Columns()
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float cy in ColumnYs)
                {
                    var foot = new Vector3(sx * ColumnX * _scale, cy * _scale, 0f);

                    var column = Stand("column", RoomArt.Column, foot,
                        ColumnWidth * _scale, ColumnHeight * _scale, Lit(UiTheme.Obsidian, 2.2f), -72);
                    // The lit edge is painted on the left; the eastern pair
                    // wants it on the right, facing the table.
                    if (sx > 0f) column.flipX = true;

                    var at = foot + new Vector3(0f, -0.35f * _scale, SconceZ * _scale);
                    float size = SconceSize * _scale;

                    Stand("sconce_glow", RoomArt.Halo, at,
                        size * 5.5f, size * 5.5f, Lit(UiTheme.WithAlpha(UiTheme.GoldBright, 0.13f), 1f), -71);
                    Stand("sconce", RoomArt.Sconce, at, size, size * 1.3f, Lit(UiTheme.Brass, 1.3f), -70);
                    Stand("flame", RoomArt.Flame, at + new Vector3(0f, 0f, size * 0.7f),
                        size * 0.5f, size * 0.8f, Lit(UiTheme.WithAlpha(UiTheme.GoldBright, 0.95f), 1f), -69);
                }
            }
        }

        // ── Placement ───────────────────────────────────────────────────

        /// <summary>
        /// A sprite stood up in the room: rotated a quarter turn about x so it
        /// faces -y, back toward the camera, then scaled to
        /// <paramref name="width"/> by <paramref name="height"/> world units
        /// with its foot at <paramref name="foot"/>, whose z counts **up** from
        /// the floor.
        /// </summary>
        /// <remarks>
        /// <b>Up off the board is -z</b> (corrected 2026-09-19). Screen up is
        /// <c>(0, cos p, -sin p)</c> - <see cref="FigureTilt.ScreenUp"/>, which
        /// the hop and the figures' lean both follow - so a part placed at +z
        /// hangs below the board instead of standing over it. Until this was
        /// turned round the whole salon was built upside down: putting the
        /// wall's head at world z = 21 puts it at screen v = -0.13 against its
        /// foot's +0.16, so the pelmet and the gilt rail rendered along the
        /// wall's *bottom* edge and the skirting ran across the top of the
        /// frame. A flat velvet gradient reads as a wall either way up, which
        /// is how it passed V3a's Play Mode check.
        ///
        /// The sign is turned here and nowhere else, so the geometry above
        /// still reads as the mockup wrote it. <see cref="SpriteRenderer.flipY"/>
        /// goes with it, so each sprite's own up still reads as up.
        /// </remarks>
        private SpriteRenderer Stand(string name, Sprite sprite, Vector3 foot,
            float width, float height, Color colour, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var size = sprite.bounds.size;
            float sx = size.x > 0f ? width / size.x : 1f;
            float sy = size.y > 0f ? height / size.y : 1f;
            go.transform.localScale = new Vector3(sx, sy, 1f);

            // The sprite is pivoted at its middle, so lift it by half its
            // height above its foot - and then negate, because up is -z.
            go.transform.position = new Vector3(foot.x, foot.y, -(foot.z + height * 0.5f));

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.flipY = true;
            renderer.sortingLayerName = SceneLighting.BoardLayer;
            renderer.sortingOrder = order;
            return renderer;
        }

        /// <summary>A colour scaled by the room's brightness, alpha untouched.</summary>
        private Color Lit(Color colour, float lift)
        {
            float k = brightness * lift;
            return new Color(colour.r * k, colour.g * k, colour.b * k, colour.a);
        }
    }
}