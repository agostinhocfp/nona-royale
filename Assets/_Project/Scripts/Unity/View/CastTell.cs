// Assets/_Project/Scripts/Unity/View/CastTell.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Announces a cast before its effects land (MOTION.md decision 3): a
    /// cyan sweep on the caster, then a line to the target, or a ring
    /// dropping onto the chosen cell. A cast with no aim sweeps wider.
    /// </summary>
    /// <remarks>
    /// <b>It is also how a CPU's target reads</b>, which is the pre-move
    /// flash BOT2 deferred: the tell plays for every accepted cast, whoever
    /// sent it.
    ///
    /// <b>It shows the aim the command carried, nothing more.</b> Positions
    /// come from the pieces and the layout; which cells an area covers is the
    /// engine's business and the effects that follow show it. Cyan is the
    /// tech register (ART_DIRECTION §8).
    /// </remarks>
    public sealed class CastTell : MonoBehaviour
    {
        private const float SweepSeconds = 0.3f;
        private const float ReachDelay = 0.12f;
        private const float ReachSeconds = 0.2f;
        private const float LingerSeconds = 0.18f;

        private const int Sorting = 14;

        private float _cell = 1f;
        private MotionSettings _motion;

        public void Bind(float cellSize, MotionSettings motion)
        {
            _cell = cellSize;
            _motion = motion;
        }

        /// <summary>
        /// How long a tell runs, in unhurried seconds (the clock the
        /// presentation queue holds on), so the queue can hold for it.
        /// </summary>
        public float Duration(bool aimed)
        {
            float k = _motion != null ? _motion.Tween(1f) : 1f;
            return (aimed ? ReachDelay + ReachSeconds + LingerSeconds : SweepSeconds) * k;
        }

        /// <summary>
        /// Plays the tell and returns how long it runs, in unhurried seconds
        /// (the same clock the presentation queue holds on).
        /// </summary>
        /// <param name="caster">The caster's position.</param>
        /// <param name="target">The target piece's position, for a targeted cast.</param>
        /// <param name="cell">The chosen cell's position, for a cell-targeted cast.</param>
        public float Play(Vector3 caster, Vector3? target, Vector3? cell)
        {
            float k = _motion != null ? _motion.Tween(1f) : 1f;
            var cyan = UiTheme.Cyan;
            bool aimed = target.HasValue || cell.HasValue;

            // The sweep: a glow and an opening ring on the caster.
            float sweepTo = aimed ? 1.7f : 2.8f;
            Fx(DecoSprites.Glow, UiTheme.WithAlpha(cyan, 0.7f),
                Pose(caster, 1.1f), Pose(caster, 1.6f, 0f), SweepSeconds * k);
            Fx(Primitives.Ring, cyan,
                Pose(caster, 0.6f), Pose(caster, sweepTo, 0f), SweepSeconds * k);

            if (!aimed) return Duration(false);

            float delay = ReachDelay * k;
            float reach = ReachSeconds * k;
            float linger = LingerSeconds * k;
            var at = target ?? cell.Value;

            if (target.HasValue) Line(caster, at, cyan, delay, reach, linger);
            else Drop(at, cyan, delay, reach, linger);

            // Both end on a ring closing onto the aim.
            Fx(Primitives.Ring, cyan, Pose(at, 2.2f, 0.2f), Pose(at, 0.95f), reach, delay);
            Fx(Primitives.Ring, cyan, Pose(at, 0.95f), Pose(at, 1.3f, 0f), linger, delay + reach, FxSprite.Ease.Linear);

            return Duration(true);
        }

        /// <summary>A beam that grows from the caster to the target, then fades where it lies.</summary>
        private void Line(Vector3 from, Vector3 to, Color colour, float delay, float reach, float linger)
        {
            var span = to - from;
            float length = span.magnitude;
            if (length < 0.001f) return;

            float angle = Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg;
            float thickness = 0.07f * _cell;
            var middle = (from + to) * 0.5f;

            // The left end stays on the caster while the beam grows.
            Fx(Primitives.Square, colour,
                new FxPose(from, new Vector3(0f, thickness, 1f), angle),
                new FxPose(middle, new Vector3(length, thickness, 1f), angle),
                reach, delay);
            Fx(Primitives.Square, colour,
                new FxPose(middle, new Vector3(length, thickness, 1f), angle),
                new FxPose(middle, new Vector3(length, thickness * 0.3f, 1f), angle, 0f),
                linger, delay + reach, FxSprite.Ease.Linear);
        }

        /// <summary>A diamond falling onto the chosen cell, for beacons and zones.</summary>
        private void Drop(Vector3 at, Color colour, float delay, float reach, float linger)
        {
            // A world diamond: the square polygon stood on its point, drawn a little tall.
            var diamond = Primitives.Polygon(4, 0f);
            var size = new Vector3(0.32f, 0.44f, 1f) * _cell;
            var above = at + Vector3.up * (1.4f * _cell);

            Fx(diamond, colour,
                new FxPose(above, size, 0f, 0.3f),
                new FxPose(at, size),
                reach, delay, FxSprite.Ease.In);
            Fx(diamond, colour,
                new FxPose(at, size),
                new FxPose(at, size * 1.7f, 0f, 0f),
                linger, delay + reach, FxSprite.Ease.Linear);
        }

        private FxPose Pose(Vector3 at, float cells, float alpha = 1f) =>
            new FxPose(at, Vector3.one * (cells * _cell), 0f, alpha);

        private void Fx(Sprite sprite, Color colour, FxPose from, FxPose to, float seconds,
            float delay = 0f, FxSprite.Ease ease = FxSprite.Ease.Out) =>
            FxSprite.Spawn(transform, sprite, colour, Sorting, from, to, seconds, _motion, delay, ease);
    }
}