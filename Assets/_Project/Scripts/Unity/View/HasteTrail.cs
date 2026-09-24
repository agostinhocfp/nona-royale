// Assets/_Project/Scripts/Unity/View/HasteTrail.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a hastened operator looks (designer, 2026-09-24): lime speed
    /// streaks trailing the piece against its direction of travel, and a few
    /// fading afterimages when it walks. It replaces the HASTE tag.
    /// </summary>
    /// <remarks>
    /// <b>Why not a tag.</b> Haste is permanent on Kurbyn and Lethe and
    /// constant around Lethe's Catalyst, so the tag sat under the same pieces
    /// all match and said nothing after the first turn — the reason Evasion
    /// was already drawn on the piece rather than tagged
    /// (<see cref="StatusPalette.IsDrawnOnPiece"/>).
    ///
    /// <b>Not Kurbyn's tell.</b> His is a single cyan afterimage offset toward
    /// his next position, for one frame on a cast. Haste is lime, several
    /// streaks, behind the piece, and continuous.
    ///
    /// <b>Engine answers only</b> (PRESENTATION §1): whether the piece is
    /// hastened comes from <c>ActiveStatusesOn</c>, and the direction of travel
    /// from the next cell on its own path, both handed in by the composition
    /// root. Reduced motion keeps the streaks still and drops the afterimages.
    /// </remarks>
    public sealed class HasteTrail : MonoBehaviour
    {
        private const int StreakCount = 3;

        /// <summary>Streak size and placement, in figure units (a figure is about one unit tall).</summary>
        private const float StreakLength = 0.34f;
        private const float StreakThickness = 0.035f;
        private const float StreakSpacing = 0.13f;
        private const float StreakBack = 0.36f;

        /// <summary>How much longer the streaks draw while the piece walks.</summary>
        private const float WalkStretch = 1.6f;

        /// <summary>Order inside the piece's sorting group: behind the outline (3) and the body (4).</summary>
        private const int StreakOrder = 2;

        private const float GhostEverySeconds = 0.06f;
        private const float GhostSeconds = 0.28f;
        private const float GhostAlpha = 0.35f;

        private OperatorPiece _piece;
        private Transform _frame;
        private MotionSettings _motion;
        private SpriteRenderer[] _streaks;
        private Color _colour;

        private bool _shown;
        private Vector3 _travel = Vector3.right;
        private float _time;
        private float _sinceGhost;

        private bool Reduced => _motion != null && _motion.ReducedMotion;

        public void Bind(OperatorPiece piece, Transform frame, MotionSettings motion)
        {
            _piece = piece;
            _frame = frame;
            _motion = motion;
            _colour = StatusPalette.OnBoard(NonaRoyale.Core.Model.StatusKind.Hastened);

            _streaks = new SpriteRenderer[StreakCount];
            for (int i = 0; i < StreakCount; i++)
            {
                var go = new GameObject($"haste_{i}");
                go.transform.SetParent(frame, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = Primitives.Square;
                renderer.sortingOrder = StreakOrder;
                renderer.enabled = false;
                _streaks[i] = renderer;
            }
        }

        /// <summary>
        /// Turns the trail on or off. <paramref name="travel"/> is the world
        /// direction from the piece's cell to the next one on its path; the
        /// last one given is kept when none is.
        /// </summary>
        public void Show(bool on, Vector3? travel)
        {
            if (travel.HasValue && travel.Value.sqrMagnitude > 1e-6f) _travel = travel.Value.normalized;
            if (_shown == on) return;

            _shown = on;
            _sinceGhost = 0f;
            if (_streaks == null) return;
            foreach (var streak in _streaks) streak.enabled = on;
        }

        private void Update()
        {
            if (!_shown || _streaks == null || _frame == null) return;

            _time += Time.deltaTime;
            bool walking = _piece != null && _piece.IsWalking;

            // Travel in the figure's own plane: the figure leans under the
            // tilt, and the streaks lean with it.
            var local = _frame.InverseTransformDirection(_travel);
            local.z = 0f;
            if (local.sqrMagnitude < 1e-6f) local = Vector3.right;
            local.Normalize();

            var across = new Vector3(-local.y, local.x, 0f);
            float angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            var centre = new Vector3(0f, _piece != null ? _piece.TrailHeight : 0.45f, 0f);

            for (int i = 0; i < StreakCount; i++)
            {
                float lane = i - (StreakCount - 1) * 0.5f;

                // A flicker per streak, out of step with its neighbours, so
                // the three read as rushing air rather than a drawn symbol.
                float pulse = Reduced ? 1f : 0.75f + 0.25f * Mathf.Sin(_time * 9f + i * 2.1f);
                float length = StreakLength * pulse * (walking ? WalkStretch : 1f) * (1f - 0.18f * Mathf.Abs(lane));
                float back = StreakBack + 0.06f * Mathf.Abs(lane);

                var streak = _streaks[i];
                streak.transform.localPosition = centre - local * (back + length * 0.5f) + across * (lane * StreakSpacing);
                streak.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                streak.transform.localScale = new Vector3(length, StreakThickness, 1f);

                float alpha = Reduced ? 0.55f : 0.45f + 0.25f * (0.5f + 0.5f * Mathf.Sin(_time * 13f + i));
                streak.color = UiTheme.WithAlpha(_colour, alpha);
            }

            if (walking && !Reduced) Ghosts();
        }

        /// <summary>Leaves a fading lime copy of the figure behind every few hundredths of a second.</summary>
        private void Ghosts()
        {
            _sinceGhost += Time.deltaTime;
            if (_sinceGhost < GhostEverySeconds) return;
            _sinceGhost = 0f;

            var sprite = _piece.GhostSprite;
            var frame = _piece.GhostFrame;
            if (sprite == null || frame == null) return;   // a rigged figure: the streaks carry it

            var go = new GameObject("haste_ghost");
            go.transform.SetParent(_piece.transform.parent, false);
            go.transform.SetPositionAndRotation(frame.position, frame.rotation);
            go.transform.localScale = frame.lossyScale;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = UiTheme.WithAlpha(_colour, GhostAlpha);
            renderer.sortingLayerID = _piece.GroupLayer;
            renderer.sortingOrder = _piece.GroupOrder - 1;

            go.AddComponent<FadingGhost>().Begin(GhostSeconds, GhostAlpha);
        }
    }

    /// <summary>A sprite that fades out and removes itself. Nothing tracks it.</summary>
    public sealed class FadingGhost : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private float _seconds;
        private float _from;
        private float _age;

        public void Begin(float seconds, float from)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _seconds = Mathf.Max(0.01f, seconds);
            _from = from;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _seconds);

            if (_renderer != null)
            {
                var colour = _renderer.color;
                colour.a = _from * (1f - t);
                _renderer.color = colour;
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
