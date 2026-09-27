// Assets/_Project/Scripts/Unity/View/PoweredShine.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A glint that runs round the powered cells' inlays (LAUNCH_UI_PASS.md,
    /// G8f): each safe cell in turn catches a diagonal band of light, one
    /// after another round the board, on the powered lights' own period.
    /// </summary>
    /// <remarks>
    /// <b>The pack's shine, masked by the inlay.</b> Each inlay gets its own
    /// instance of <c>ShineLit</c> (<c>SHINE_ON</c> on the lit 2D shader), so
    /// the band lights only the inlay's pixels and the 2D lights still reach
    /// it. <c>_ShineLocation</c> is driven from here; outside a cell's sweep
    /// it is parked off the sprite.
    ///
    /// <b>Off with the lighting or under reduced motion</b>
    /// (<see cref="Active"/>, pushed by the composition root every frame, as
    /// <c>UiTween.ReducedMotion</c> is). It is the powered cells' light moving,
    /// so both switches that quiet that light quiet this too.
    ///
    /// <b>No material, no shine.</b> If <c>ShineLit</c> is missing or its
    /// shader can't run, <see cref="Add"/> leaves the inlay on its default
    /// material and nothing else changes.
    /// </remarks>
    public sealed class PoweredShine : MonoBehaviour
    {
        /// <summary>Whether glints run. The composition root sets it from Lighting effects and Reduced motion.</summary>
        public static bool Active { get; set; } = true;

        /// <summary>One lap of the board: the same period as the powered lights' breath.</summary>
        private const float Period = 3.4f;

        /// <summary>How long one cell's glint takes to cross it.</summary>
        private const float SweepSeconds = 0.7f;

        /// <summary>Where the band starts and ends, in the shader's location units: just off each side of the sprite.</summary>
        private const float From = -0.25f;
        private const float To = 1.25f;

        /// <summary>A location that puts the band nowhere on the sprite.</summary>
        private const float Parked = -2f;

        private readonly List<SpriteRenderer> _inlays = new List<SpriteRenderer>();
        private readonly List<Material> _materials = new List<Material>();
        private float _clock;
        private bool _parked;

        /// <summary>Gives <paramref name="inlay"/> the shine. Cells glint in the order they are added.</summary>
        public void Add(SpriteRenderer inlay)
        {
            if (inlay == null) return;

            var material = ShaderFx.Instance(ShaderFx.ShineLit);
            if (material == null) return;

            material.SetFloat(ShaderFx.ShineLocation, Parked);
            inlay.sharedMaterial = material;
            _inlays.Add(inlay);
            _materials.Add(material);
        }

        /// <summary>Forgets every inlay and destroys their instances. The board calls it before it rebuilds.</summary>
        public void Clear()
        {
            foreach (var material in _materials)
                if (material != null) Destroy(material);

            _inlays.Clear();
            _materials.Clear();
            _clock = 0f;
        }

        private void Update()
        {
            if (_materials.Count == 0) return;

            if (!Active)
            {
                if (!_parked) Park();
                return;
            }

            _parked = false;
            _clock = (_clock + Time.deltaTime) % Period;

            float step = Period / _materials.Count;
            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i] == null) continue;

                // Each cell's glint starts a step after the last one's.
                float t = (_clock - i * step + Period) % Period / SweepSeconds;
                float location = t < 1f ? Mathf.Lerp(From, To, t) : Parked;
                _materials[i].SetFloat(ShaderFx.ShineLocation, location);
            }
        }

        private void Park()
        {
            foreach (var material in _materials)
                if (material != null) material.SetFloat(ShaderFx.ShineLocation, Parked);
            _parked = true;
        }

        private void OnDestroy() => Clear();
    }
}
