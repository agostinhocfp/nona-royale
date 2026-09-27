// Assets/_Project/Scripts/Unity/View/GiltSheen.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A slow band of light that passes over the Deco skin's large gilt, the
    /// table rims, the medallion and the corner wedges, all at once, every few
    /// seconds (board skin BS5).
    /// </summary>
    /// <remarks>
    /// <b>The room's light moving across the metal,</b> not a signal. It is
    /// slow and rare on purpose: <see cref="PoweredShine"/>'s quick glint round
    /// the safe cells means "powered", and this must never be read as that.
    ///
    /// <b>One material for all of it.</b> Every renderer shares a single
    /// instance of <c>ShineLit</c> (<c>SHINE_ON</c> on the lit 2D shader), so
    /// one <c>_ShineLocation</c> write a frame moves the band on every piece of
    /// gilt together, and the 2D lights still reach them. The location is in
    /// each sprite's own UV space, so the band crosses a rim and a wedge in the
    /// same time whatever their size.
    ///
    /// <b>Off with the lighting or under reduced motion</b> (<see cref="Active"/>,
    /// pushed by the composition root every frame, like
    /// <see cref="PoweredShine.Active"/>). With it off the band is parked off
    /// every sprite and the gilt is exactly as painted.
    ///
    /// <b>No material, no sheen.</b> If <c>ShineLit</c> is missing or its
    /// shader can't run, <see cref="Add"/> leaves the renderer on its default
    /// material and nothing else changes.
    /// </remarks>
    public sealed class GiltSheen : MonoBehaviour
    {
        /// <summary>Whether the sheen runs. The composition root sets it from Lighting effects and Reduced motion.</summary>
        public static bool Active { get; set; } = true;

        /// <summary>Seconds between passes.</summary>
        public const float Period = 9f;

        /// <summary>How long one pass takes to cross a sprite.</summary>
        public const float SweepSeconds = 1.8f;

        /// <summary>Where the band starts and ends, in the shader's location units: just off each side of the sprite.</summary>
        private const float From = -0.25f;
        private const float To = 1.25f;

        /// <summary>A location that puts the band nowhere on the sprite.</summary>
        private const float Parked = -2f;

        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        private Material _material;
        private float _clock;
        private bool _parked;

        /// <summary>Gives <paramref name="renderer"/> the sheen.</summary>
        public void Add(SpriteRenderer renderer)
        {
            if (renderer == null) return;

            if (_material == null)
            {
                _material = ShaderFx.Instance(ShaderFx.ShineLit);
                if (_material == null) return;
                _material.SetFloat(ShaderFx.ShineLocation, Parked);
                _parked = true;
            }

            renderer.sharedMaterial = _material;
            _renderers.Add(renderer);
        }

        /// <summary>Forgets every renderer and destroys the material. The board calls it before it rebuilds.</summary>
        public void Clear()
        {
            if (_material != null) Destroy(_material);

            _material = null;
            _renderers.Clear();
            _clock = 0f;
            _parked = false;
        }

        /// <summary>
        /// Where the band is <paramref name="clock"/> seconds into a period:
        /// crossing for the first <see cref="SweepSeconds"/>, parked after.
        /// </summary>
        public static float LocationAt(float clock)
        {
            float t = (clock % Period + Period) % Period / SweepSeconds;
            return t < 1f ? Mathf.Lerp(From, To, t) : Parked;
        }

        private void Update()
        {
            if (_material == null || _renderers.Count == 0) return;

            if (!Active)
            {
                if (!_parked)
                {
                    _material.SetFloat(ShaderFx.ShineLocation, Parked);
                    _parked = true;
                }
                return;
            }

            _parked = false;
            _clock = (_clock + Time.deltaTime) % Period;
            _material.SetFloat(ShaderFx.ShineLocation, LocationAt(_clock));
        }

        private void OnDestroy() => Clear();
    }
}
