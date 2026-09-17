// Assets/_Project/Scripts/Unity/View/EventLights.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Light that answers play (LIGHTING.md, LT2): a cool pool under the
    /// selected operator, a cyan flash on a cast, a warm burst on a knockout,
    /// and a swell of the vault's light when an operator reaches HOME.
    /// </summary>
    /// <remarks>
    /// <b>It shows, it decides nothing.</b> The composition root calls it
    /// from the presentation beats it already plays (the cast tell, the
    /// knockout, the settle) and tells it which piece is selected.
    ///
    /// <b>The two registers hold</b> (ART_DIRECTION §2.1, LIGHTING.md
    /// decision 4). The selection pool and the cast flash are cyan and
    /// additive, and reach only the <see cref="SceneLighting.BoardLayer"/>
    /// sorting layer, so a piece keeps its seat colour. Without that layer
    /// they are skipped, as LT1's powered cells are. The knockout burst and
    /// the vault swell are warm and multiply, like the room's pools, and
    /// reach every layer.
    ///
    /// <b>The room's switches apply.</b> With Lighting effects off nothing
    /// new is lit and the selection pool fades out. Under Reduced motion the
    /// flashes rise more slowly and peak at half (<see cref="LightPulse.Flash"/>),
    /// and the selection pool holds steady.
    ///
    /// <b>Clocks.</b> Flashes run on scaled time times the animation speed,
    /// like the presentation queue, so pause and hit-stop hold them. The
    /// selection pool fades on unscaled time, so it answers a click even
    /// while time is slowed.
    ///
    /// <b>Tuning.</b> Like <see cref="SceneLighting"/>, the fields are read
    /// every frame and can be dragged in Play Mode.
    /// </remarks>
    public sealed class EventLights : MonoBehaviour
    {
        private const int MultiplyStyle = 0;
        private const int AdditiveStyle = 1;

        [Header("Selected operator (cool, floor only)")]
        [Range(0f, 2f)] public float selectIntensity = 0.3f;
        [Tooltip("Outer radius, in cells.")]
        [Range(0.2f, 3f)] public float selectReach = 0.9f;
        [Tooltip("How fast the pool fades in and out, in intensity per second.")]
        public float selectFadeRate = 2.5f;
        public float selectBreathPeriod = 2.6f;
        [Range(0f, 0.5f)] public float selectBreathDepth = 0.12f;

        [Header("Cast flash (cool, floor only)")]
        [Range(0f, 3f)] public float castIntensity = 0.9f;
        [Tooltip("Outer radius, in cells.")]
        [Range(0.2f, 4f)] public float castReach = 1.5f;
        public float castSeconds = 0.45f;
        [Tooltip("When the flash at the aim starts, as a fraction of the cast tell.")]
        [Range(0f, 1f)] public float castAimDelay = 0.45f;

        [Header("Knockout burst (warm)")]
        [Range(0f, 3f)] public float knockoutIntensity = 0.9f;
        [Tooltip("Outer radius, in cells.")]
        [Range(0.2f, 5f)] public float knockoutReach = 2.2f;
        public float knockoutSeconds = 0.6f;
        public Color knockoutColour = new Color(1f, 0.72f, 0.46f);

        [Header("Vault on HOME (warm)")]
        [Tooltip("Added on top of the vault's resting light at the swell's peak.")]
        [Range(0f, 3f)] public float vaultBoost = 0.7f;
        public float vaultSeconds = 1.6f;

        private sealed class Flash
        {
            public Light2D Light;
            public double Elapsed;
            public float Duration;
            public float Peak;
        }

        private readonly List<Flash> _flashes = new List<Flash>();

        private SceneLighting _room;
        private MotionSettings _motion;
        private int? _boardLayer;
        private Transform _root;
        private float _cell = 1f;
        private float _extent = 1f;
        private Vector3 _vault;

        private Light2D _select;
        private float _selectLevel;

        /// <summary>The selected piece's transform, or null. The pool follows it.</summary>
        public Transform Selected { get; set; }

        /// <summary>Called whenever the board is drawn: the title's table, and every deal.</summary>
        public void Bind(SceneLighting room, BoardLayout layout, MotionSettings motion)
        {
            Clear();

            _room = room;
            _motion = motion;
            _boardLayer = SceneLighting.BoardLayerId;

            if (layout != null)
            {
                _cell = layout.CellSize;
                _extent = layout.Extent;
                _vault = layout.HomeGoalPosition;
            }

            if (_root == null)
            {
                _root = new GameObject("event_lights").transform;
                _root.SetParent(transform, false);
            }
        }

        /// <summary>Drops every flash and the selection pool, for a teardown or a new deal.</summary>
        public void Clear()
        {
            foreach (var flash in _flashes)
                if (flash.Light != null) Destroy(flash.Light.gameObject);

            _flashes.Clear();
            Selected = null;
            _selectLevel = 0f;
            if (_select != null) _select.enabled = false;
        }

        /// <summary>
        /// A cyan flash on the caster, and a second on its target or aimed
        /// cell part-way through the tell.
        /// </summary>
        /// <param name="tellSeconds">How long the cast tell holds, so the aim's flash lands with its line.</param>
        public void CastFlash(Vector3 caster, Vector3? aim, float tellSeconds)
        {
            if (!CanLight || _boardLayer == null) return;

            Spawn("cast_flash", caster, UiTheme.Cyan, AdditiveStyle, castReach * _cell, castSeconds, castIntensity, 0f, true);

            if (aim.HasValue)
            {
                Spawn("cast_flash_aim", aim.Value, UiTheme.Cyan, AdditiveStyle, castReach * _cell, castSeconds, castIntensity,
                    Mathf.Max(0f, tellSeconds) * castAimDelay, true);
            }
        }

        /// <summary>A warm burst where an operator fell.</summary>
        public void Knockout(Vector3 at)
        {
            if (!CanLight) return;
            Spawn("knockout_burst", at, knockoutColour, MultiplyStyle, knockoutReach * _cell, knockoutSeconds, knockoutIntensity, 0f, false);
        }

        /// <summary>The vault's light swells for a moment: an operator made it HOME.</summary>
        public void VaultSwell()
        {
            if (!CanLight) return;

            var colour = _room != null ? _room.vaultColour : new Color(1f, 0.83f, 0.56f);
            float reach = (_room != null ? _room.vaultReach : 0.42f) * _extent;
            Spawn("vault_swell", _vault, colour, MultiplyStyle, reach, vaultSeconds, vaultBoost, 0f, false);
        }

        private bool Effects => _room == null || _room.Effects;
        private bool Reduced => _room != null && _room.Reduced;
        private bool CanLight => Effects && _root != null;

        private void LateUpdate()
        {
            UpdateSelection();
            UpdateFlashes();
        }

        private void UpdateSelection()
        {
            bool wanted = Selected != null && Effects && _boardLayer != null;

            if (_select == null)
            {
                if (!wanted || _root == null) return;
                _select = MakeLight("select_pool", UiTheme.Cyan, AdditiveStyle, true);
            }

            float target = wanted ? selectIntensity : 0f;
            _selectLevel = Mathf.MoveTowards(_selectLevel, target, Mathf.Max(0.01f, selectFadeRate) * Time.unscaledDeltaTime);

            if (Selected != null) _select.transform.position = Selected.position;

            float breath = LightPulse.Breath(Time.unscaledTimeAsDouble, selectBreathPeriod, selectBreathDepth, 0f, Reduced);
            _select.intensity = _selectLevel * breath;
            _select.pointLightOuterRadius = Mathf.Max(0.01f, selectReach * _cell);
            _select.pointLightInnerRadius = _select.pointLightOuterRadius * 0.2f;
            _select.enabled = _selectLevel > 0.001f;
        }

        private void UpdateFlashes()
        {
            if (_flashes.Count == 0) return;

            float rate = _motion != null ? _motion.Rate : 1f;
            double step = Time.deltaTime * rate;
            bool effects = Effects;
            bool reduced = Reduced;

            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                var flash = _flashes[i];
                flash.Elapsed += step;

                if (flash.Light == null || flash.Elapsed >= flash.Duration)
                {
                    if (flash.Light != null) Destroy(flash.Light.gameObject);
                    _flashes.RemoveAt(i);
                    continue;
                }

                float level = flash.Peak * LightPulse.Flash(flash.Elapsed, flash.Duration, reduced);
                flash.Light.intensity = level;
                flash.Light.enabled = effects && level > 0.001f;
            }
        }

        private void Spawn(string name, Vector3 at, Color colour, int style, float reach, float seconds, float peak,
            float delay, bool floorOnly)
        {
            if (!(seconds > 0f) || !(peak > 0f)) return;

            var light = MakeLight(name, colour, style, floorOnly);
            light.transform.position = at;
            light.pointLightOuterRadius = Mathf.Max(0.01f, reach);
            light.pointLightInnerRadius = light.pointLightOuterRadius * 0.2f;
            light.intensity = 0f;
            light.enabled = false;

            // A delay is a head start below zero: the flash reads 0 until it passes.
            _flashes.Add(new Flash { Light = light, Elapsed = -delay, Duration = seconds, Peak = peak });
        }

        private Light2D MakeLight(string name, Color colour, int style, bool floorOnly)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.blendStyleIndex = style;
            light.falloffIntensity = 0.6f;
            light.color = colour;

            if (floorOnly && _boardLayer.HasValue)
                light.targetSortingLayers = new[] { _boardLayer.Value };

            return light;
        }
    }
}