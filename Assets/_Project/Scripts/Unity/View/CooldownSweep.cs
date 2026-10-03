// Assets/_Project/Scripts/Unity/View/CooldownSweep.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The cooldown sweep on an ability card: a translucent veil covering the
    /// share of the cooldown still to run, whose leading edge (the hand)
    /// turns clockwise from twelve o'clock like a radar as the operator's
    /// turns pass. The veil is brightest along the hand and fades into its
    /// plain wash over <see cref="GlowDegrees"/>.
    /// </summary>
    /// <remarks>
    /// <b>Time here is turns.</b> A cooldown counts the caster's own turns
    /// (§5), so the veil does not creep in real time. It holds still between
    /// turns and sweeps when a turn passes: each of the caster's turns turns
    /// the hand by one step, and the last step clears the card. Right after
    /// a cast the veil is whole. Its size is
    /// <see cref="CooldownSweepGeometry.Fraction"/>: turns left over the
    /// cooldown plus the turn it was cast in, so a cooldown of 2 reads full,
    /// two thirds, one third, clear.
    ///
    /// <b>A card is rebuilt on every change</b>, and a new sweep is a new
    /// component. The share last shown for each operator's ability lives in
    /// the tray's <c>shown</c> map, so the new sweep starts where the old one
    /// stood and animates from there. A share that grows (a cast) appears at
    /// once with a quick fade, since a hand turning backwards would read as
    /// time running in reverse; one that shrinks sweeps.
    ///
    /// <b>Drawn as a mesh, clipped to the card's chamfer.</b> The card face is
    /// <see cref="DecoSprites.ButtonFill"/>, a box with its corners cut at
    /// <see cref="CooldownSweepGeometry.ButtonCut"/>. A filled
    /// <see cref="Image"/> would stretch the sliced sprite and bend the
    /// corners, so the veil is a triangle fan from the centre to the card's
    /// outline (<see cref="CooldownSweepGeometry.Outline"/>), and the hand a
    /// hairline quad on top. The geometry is plain maths in
    /// <see cref="CooldownSweepGeometry"/>, so the tests need no UI assembly.
    ///
    /// <b>Under the text, over the face.</b> The sweep is the card's first
    /// child, so its words stay legible on top of it. It ignores layout and
    /// the pointer, so a hold or a hover on the card still reads it.
    /// </remarks>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CooldownSweep : MaskableGraphic
    {
        /// <summary>A sweep's base length; it runs <see cref="SecondsPerCircle"/> longer per whole circle it turns.</summary>
        private const float StepSeconds = 0.35f;

        private const float SecondsPerCircle = 0.9f;

        /// <summary>How fast a veil that grew (a cast) fades in.</summary>
        private const float GrowSeconds = 0.15f;

        /// <summary>How far from the hand the veil's bright edge fades out, in degrees.</summary>
        private const float GlowDegrees = 32f;

        private const float HandWidth = 1.5f;

        private static readonly List<float> Angles = new List<float>(128);

        private Dictionary<long, float> _shown;
        private long _key;
        private float _target;
        private float _current;
        private float _from;
        private float _time;
        private float _seconds;
        private float _alpha = 1f;
        private bool _growing;

        /// <summary>
        /// Adds a sweep to <paramref name="card"/>, behind its content, showing
        /// <paramref name="turnsLeft"/> of a cooldown of
        /// <paramref name="cooldown"/> turns. <paramref name="shown"/> carries
        /// the share last shown under <paramref name="key"/> across rebuilds.
        /// </summary>
        public static CooldownSweep Attach(RectTransform card, int turnsLeft, int cooldown,
            Dictionary<long, float> shown, long key)
        {
            var rect = UiKit.Rect("cooldown_sweep", card);
            UiKit.Stretch(rect);
            rect.SetAsFirstSibling();
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var sweep = rect.gameObject.AddComponent<CooldownSweep>();
            sweep.raycastTarget = false;
            sweep.color = UiTheme.CooldownVeil;
            sweep.Begin(CooldownSweepGeometry.Fraction(turnsLeft, cooldown), shown, key);
            return sweep;
        }

        private void Begin(float target, Dictionary<long, float> shown, long key)
        {
            _shown = shown;
            _key = key;
            _target = target;

            bool known = shown != null && shown.TryGetValue(key, out _current);
            if (!known) _current = target;

            _from = _current;
            _time = 0f;

            if (target > _current + 1e-4f)
            {
                // A cast: the veil appears whole and fades in, never winds back.
                _growing = true;
                _current = target;
                _alpha = 0f;
                _seconds = GrowSeconds;
            }
            else
            {
                float span = _current - target;
                _seconds = span > 1e-4f ? StepSeconds + span * SecondsPerCircle : 0f;
                if (UiTween.ReducedMotion) _seconds *= MotionSettings.ReducedTween;
            }

            Remember(_current);
        }

        private void Update()
        {
            if (_seconds <= 0f) return;

            _time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_time / _seconds);
            float eased = UiEasing.Evaluate(UiEase.OutCubic, t);

            if (_growing) _alpha = eased;
            else _current = Mathf.Lerp(_from, _target, eased);

            if (t >= 1f)
            {
                _seconds = 0f;
                _alpha = 1f;
                _current = _target;
            }

            Remember(_current);
            SetVerticesDirty();
        }

        private void Remember(float value)
        {
            if (_shown != null) _shown[_key] = value;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_current <= 1e-4f || _alpha <= 0f) return;

            var rect = rectTransform.rect;
            var centre = rect.center;
            var half = rect.size * 0.5f;
            if (half.x <= 0f || half.y <= 0f) return;

            float hand = CooldownSweepGeometry.HandAngle(_current);
            CooldownSweepGeometry.FanAngles(hand, half, CooldownSweepGeometry.ButtonCut, Angles);

            // The veil: a fan from the centre, brightest along the hand.
            var veil = color;
            veil.a *= _alpha;
            var glow = UiTheme.CooldownGlow;
            glow.a *= _alpha;

            vh.AddVert(centre, veil, Vector4.zero);
            for (int i = 0; i < Angles.Count; i++)
            {
                float fromHand = Angles[i] - hand;
                float lit = 1f - Mathf.Clamp01(fromHand / GlowDegrees);
                var tint = Color.Lerp(veil, glow, lit * lit);
                vh.AddVert(centre + CooldownSweepGeometry.Outline(Angles[i], half, CooldownSweepGeometry.ButtonCut), tint, Vector4.zero);
                if (i > 0) vh.AddTriangle(0, i, i + 1);
            }

            // The hand, a hairline from the centre to the outline.
            var handColour = UiTheme.CooldownHand;
            handColour.a *= _alpha;
            var tip = centre + CooldownSweepGeometry.Outline(hand, half, CooldownSweepGeometry.ButtonCut);
            var along = (tip - centre).normalized;
            var across = new Vector2(along.y, -along.x) * (HandWidth * 0.5f);

            int start = vh.currentVertCount;
            vh.AddVert(centre - across, handColour, Vector4.zero);
            vh.AddVert(centre + across, handColour, Vector4.zero);
            vh.AddVert(tip + across, handColour, Vector4.zero);
            vh.AddVert(tip - across, handColour, Vector4.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
