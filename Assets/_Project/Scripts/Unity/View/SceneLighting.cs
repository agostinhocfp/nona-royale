// Assets/_Project/Scripts/Unity/View/SceneLighting.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The room's light under URP (ADR-0010; LIGHTING.md, LT1): a low
    /// ambient, warm pools over the vault, the arms and the four tables, a
    /// cool glow on every powered cell, and bloom on whatever the lights push
    /// past white.
    /// </summary>
    /// <remarks>
    /// <b>Why a global light at all.</b> The 2D Renderer gives every sprite
    /// the lit material (ADR-0010 decision 3). A lit sprite takes its colour
    /// from the 2D lights on its sorting layer: once any 2D light exists, a
    /// layer without a global light starts from black.
    ///
    /// <b>Focal light into shadow</b> (ART_DIRECTION §6). The ambient sits
    /// below 1, and the pools lift the places play happens back to about
    /// full, so the dead corners of the table fall away. The pools sit where
    /// the board already paints its fake ones (<see cref="BoardView"/>'s arm
    /// and vault glows), so the real light agrees with the painted one.
    ///
    /// <b>Two registers</b> (ART_DIRECTION §2.1). Room lights are warm and
    /// multiply (blend style 0). Powered cells use the additive style
    /// (blend style 1) in holo cyan: the colour is added on top, so the cell
    /// goes past white and blooms. Cyan stays an effect, never a surface.
    ///
    /// <b>Powered light stays on the floor.</b> Added cyan would wash out a
    /// piece standing on a safe cell (contrast discipline, ART_DIRECTION §6),
    /// so those lights reach only the <see cref="BoardLayer"/> sorting layer,
    /// where <see cref="BoardView"/> draws. The layer is made once in the
    /// editor (Project Settings › Tags and Layers › Sorting Layers, above
    /// Default). Without it, the board stays on Default and the powered
    /// lights are skipped, with one warning.
    ///
    /// <b>Haze</b> (GUI increment G3). A very faint cloud drifts in the vault's
    /// and each arm's pool: a sprite on the board layer, so the pools' lights
    /// warm it and the dark swallows it. It is part of the effects, so it is
    /// gone with Lighting effects off, and it holds still under Reduced
    /// motion.
    ///
    /// <b>Bloom</b> is a global Volume built in code, with its threshold at
    /// 1: only what a light pushes past white glows, never the flat HUD
    /// (a Screen Space Overlay canvas is drawn after post-processing).
    ///
    /// <b>The Lighting effects setting</b> (<see cref="Effects"/>) turns
    /// all of it off: ambient back to 1, the pools and bloom disabled. That
    /// is the look the game had on Built-in, and the cheap path for weak
    /// devices. <b>Reduced motion</b> (<see cref="Reduced"/>) stills the
    /// swells and the haze.
    ///
    /// <b>Tuning.</b> The fields below are read every frame, so they can be
    /// dragged in the inspector during Play Mode. Those edits are lost when
    /// Play stops unless this component is added to the MatchBootstrap
    /// object in the scene, where the bootstrap finds and uses it.
    ///
    /// <b>Built in code</b> (no scene wiring). A global light already in the
    /// scene is used as the ambient and left alone (the setting doesn't
    /// change it), and a second one is never added: two global lights on one
    /// layer and blend style is a URP error.
    /// </remarks>
    public sealed class SceneLighting : MonoBehaviour
    {
        private const int MultiplyStyle = 0;
        private const int AdditiveStyle = 1;

        /// <summary>The sorting layer the board is drawn on, behind Default.</summary>
        public const string BoardLayer = "Board";

        [Header("Ambient")]
        [Tooltip("The room's base light. 1 is flat, as on Built-in.")]
        [Range(0f, 1.5f)] public float ambientIntensity = 0.7f;
        public Color ambientColour = new Color(1f, 0.96f, 0.9f);

        [Header("Vault (HOME)")]
        [Range(0f, 2f)] public float vaultIntensity = 0.6f;
        [Tooltip("Outer radius, as a fraction of the board's half-width.")]
        [Range(0.05f, 1.5f)] public float vaultReach = 0.42f;
        public Color vaultColour = new Color(1f, 0.83f, 0.56f);
        [Tooltip("Seconds per swell; 0 holds it steady.")]
        public float vaultBreathPeriod = 7f;
        [Range(0f, 0.5f)] public float vaultBreathDepth = 0.06f;

        [Header("Arms (the path)")]
        [Range(0f, 2f)] public float armIntensity = 0.34f;
        [Tooltip("Outer radius, as a fraction of the board's half-width.")]
        [Range(0.05f, 1.5f)] public float armReach = 0.6f;
        public Color armColour = new Color(1f, 0.9f, 0.74f);

        [Header("Tables (yards)")]
        [Range(0f, 2f)] public float tableIntensity = 0.4f;
        [Tooltip("Outer radius, as a fraction of a table's diameter.")]
        [Range(0.1f, 2f)] public float tableReach = 0.75f;
        public Color tableColour = new Color(1f, 0.88f, 0.7f);

        [Header("Powered cells (cool register)")]
        [Range(0f, 3f)] public float poweredIntensity = 0.35f; // 0.5 before the designer's review (-30%)
        [Tooltip("Outer radius, in cells.")]
        [Range(0.2f, 4f)] public float poweredReach = 1.1f;
        public float poweredBreathPeriod = 3.4f;
        [Range(0f, 0.5f)] public float poweredBreathDepth = 0.15f;

        [Header("Haze (G3)")]
        [Tooltip("Multiplies the haze's alpha from UiTheme. 0 hides it.")]
        [Range(0f, 3f)] public float hazeStrength = 1f;
        [Tooltip("Seconds for one slow drift loop.")]
        public float hazeDriftPeriod = 46f;
        [Tooltip("How far the haze wanders, as a fraction of its size.")]
        [Range(0f, 0.2f)] public float hazeDrift = 0.05f;

        [Header("Bloom")]
        [Tooltip("Brightness where glow starts. 1 means only what a light pushes past white.")]
        [Range(0f, 3f)] public float bloomThreshold = 1f;
        [Range(0f, 3f)] public float bloomIntensity = 0.7f;
        [Range(0f, 1f)] public float bloomScatter = 0.6f;

        /// <summary>The Lighting effects setting: pools, glow and bloom, or the flat room.</summary>
        public bool Effects { get; set; } = true;

        /// <summary>Reduced motion: the swells hold still.</summary>
        public bool Reduced { get; set; }

        /// <summary>The global light in use, found or built.</summary>
        public Light2D Ambient { get; private set; }

        private enum Kind { Vault, Arm, Table, Powered, Haze }

        private sealed class Rig
        {
            public Light2D Light;
            public SpriteRenderer Haze;
            public Vector3 Home;
            public Kind Kind;
            public float Phase;
            public float Scale; // world units the reach is a fraction of; a haze's size
        }

        /// <summary>Above every board sprite, still under the pieces (Default layer, order 1 and up).</summary>
        private const int HazeOrder = -5;

        private static bool s_warned;

        private readonly List<Rig> _rigs = new List<Rig>();
        private bool _ownsAmbient;
        private Transform _room;
        private Volume _volume;
        private VolumeProfile _profile;
        private Bloom _bloom;
        private Camera _camera;
        private bool? _postProcessing;

        /// <summary>Finds the scene's global light, or adds one, and builds the bloom volume. Safe to call again.</summary>
        public void Build()
        {
            if (Ambient == null) Ambient = FindOrAddAmbient();
            if (_volume == null) BuildVolume();
        }

        /// <summary>Places the room's lights for this board. Called whenever the board is drawn.</summary>
        public void Arrange(BoardLayout layout, PathMap map)
        {
            Build();
            ClearRoom();
            if (layout == null) return;

            _room = new GameObject("room_lights").transform;
            _room.SetParent(transform, false);

            float extent = layout.Extent;
            var centre = layout.HomeGoalPosition;

            int index = 0;
            AddRig(Kind.Vault, "vault_light", centre, extent, MultiplyStyle, index++);
            foreach (var direction in new[] { Vector3.up, Vector3.left, Vector3.down, Vector3.right })
                AddRig(Kind.Arm, "arm_light", centre + direction * extent * 0.6f, extent, MultiplyStyle, index++);

            foreach (var seat in new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet })
            {
                AddRig(Kind.Table, $"table_light_{seat}", layout.PositionOf(CellRef.Yard(seat)),
                    layout.TableDiameter, MultiplyStyle, index++);
            }

            int? board = BoardLayerId;

            AddHaze(centre, extent * 0.75f, board, index++);
            foreach (var direction in new[] { Vector3.up, Vector3.left, Vector3.down, Vector3.right })
                AddHaze(centre + direction * extent * 0.6f, extent * 0.6f, board, index++);

            if (map != null && board == null)
            {
                if (!s_warned)
                {
                    Debug.LogWarning($"[Lighting] No '{BoardLayer}' sorting layer behind Default, so powered cells get no light. " +
                                     "Add it in Project Settings › Tags and Layers › Sorting Layers, above Default.");
                    s_warned = true;
                }
            }
            else if (map != null)
            {
                for (int i = 0; i < map.Profile.CircuitLength; i++)
                {
                    var cell = CellRef.Track(i);
                    if (!map.IsSafe(cell)) continue;

                    var light = AddRig(Kind.Powered, $"powered_light_{i}", layout.PositionOf(cell), layout.CellSize,
                        AdditiveStyle, index++);
                    light.targetSortingLayers = new[] { board.Value };
                }
            }

            Apply();
        }

        private void LateUpdate() => Apply();

        private void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
        }

        // ── Per frame ────────────────────────────────────────────────────

        private void Apply()
        {
            bool on = Effects;

            if (_ownsAmbient && Ambient != null)
            {
                Ambient.color = on ? ambientColour : Color.white;
                Ambient.intensity = on ? ambientIntensity : 1f;
            }

            double time = Time.timeAsDouble;
            foreach (var rig in _rigs)
            {
                if (rig.Kind == Kind.Haze)
                {
                    if (rig.Haze == null) continue;
                    bool shown = on && hazeStrength > 0f;
                    if (rig.Haze.enabled != shown) rig.Haze.enabled = shown;
                    if (shown) Drift(rig, time);
                    continue;
                }

                if (rig.Light == null) continue;
                if (rig.Light.enabled != on) rig.Light.enabled = on;
                if (on) Shape(rig, time);
            }

            if (_volume != null)
            {
                _volume.enabled = on;
                _bloom.threshold.Override(bloomThreshold);
                _bloom.intensity.Override(bloomIntensity);
                _bloom.scatter.Override(bloomScatter);
            }

            SetPostProcessing(on);
        }

        private void Shape(Rig rig, double time)
        {
            var light = rig.Light;
            float outer;
            float inner;
            float breath;

            switch (rig.Kind)
            {
                case Kind.Vault:
                    outer = rig.Scale * vaultReach;
                    inner = outer * 0.25f;
                    light.color = vaultColour;
                    breath = LightPulse.Breath(time, vaultBreathPeriod, vaultBreathDepth, rig.Phase, Reduced);
                    light.intensity = vaultIntensity * breath;
                    break;

                case Kind.Arm:
                    outer = rig.Scale * armReach;
                    inner = outer * 0.35f;
                    light.color = armColour;
                    light.intensity = armIntensity;
                    break;

                case Kind.Table:
                    outer = rig.Scale * tableReach;
                    inner = outer * 0.4f;
                    light.color = tableColour;
                    light.intensity = tableIntensity;
                    break;

                default:
                    outer = rig.Scale * poweredReach;
                    inner = outer * 0.15f;
                    light.color = UiTheme.Cyan;
                    breath = LightPulse.Breath(time, poweredBreathPeriod, poweredBreathDepth, rig.Phase, Reduced);
                    light.intensity = poweredIntensity * breath;
                    break;
            }

            light.pointLightOuterRadius = Mathf.Max(0.01f, outer);
            light.pointLightInnerRadius = Mathf.Clamp(inner, 0f, light.pointLightOuterRadius);
        }

        /// <summary>
        /// A slow loop around the haze's home, a slower turn and a gentle
        /// swell. Under Reduced motion it sits at home, unturned, at its
        /// base strength.
        /// </summary>
        private void Drift(Rig rig, double time)
        {
            var colour = UiTheme.Haze;
            colour.a = Mathf.Clamp01(colour.a * hazeStrength);

            var puff = rig.Haze.transform;

            if (Reduced || !(hazeDriftPeriod > 0f))
            {
                puff.position = rig.Home;
                puff.localRotation = Quaternion.identity;
                rig.Haze.color = colour;
                return;
            }

            double cycle = time / hazeDriftPeriod + rig.Phase;
            float angle = (float)(2.0 * System.Math.PI * cycle);
            float reach = rig.Scale * hazeDrift;

            // A Lissajous loop, so the path never visibly repeats.
            puff.position = rig.Home + new Vector3(Mathf.Sin(angle), Mathf.Sin(angle * 0.5f + 1.3f), 0f) * reach;
            puff.localRotation = Quaternion.Euler(0f, 0f, (float)((cycle * 0.25 % 1.0) * 360.0));

            colour.a *= LightPulse.Breath(time, hazeDriftPeriod * 0.37f, 0.25f, rig.Phase, false);
            rig.Haze.color = colour;
        }

        /// <summary>Post-processing is a per-camera switch; bloom needs it, the flat room doesn't.</summary>
        private void SetPostProcessing(bool on)
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                _postProcessing = null;
                if (_camera == null) return;
            }

            if (_postProcessing == on) return;

            _camera.GetUniversalAdditionalCameraData().renderPostProcessing = on;
            _postProcessing = on;
        }

        // ── Building ─────────────────────────────────────────────────────

        private Light2D FindOrAddAmbient()
        {
            foreach (var light in FindObjectsByType<Light2D>())
            {
                if (light.lightType != Light2D.LightType.Global || light.blendStyleIndex != MultiplyStyle) continue;

                _ownsAmbient = false;
                return light;
            }

            var go = new GameObject("ambient_light");
            go.transform.SetParent(transform, false);

            // Awake runs inside AddComponent and targets every sorting layer.
            var ambientLight = go.AddComponent<Light2D>();
            ambientLight.lightType = Light2D.LightType.Global;
            ambientLight.blendStyleIndex = MultiplyStyle;
            _ownsAmbient = true;
            return ambientLight;
        }

        private void BuildVolume()
        {
            var go = new GameObject("post_volume");
            go.transform.SetParent(transform, false);

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "RoomBloom";
            _bloom = _profile.Add<Bloom>(true);
            _bloom.highQualityFiltering.Override(false);

            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.sharedProfile = _profile;
        }

        /// <summary>
        /// The <see cref="BoardLayer"/> sorting layer's id, or null when it is
        /// missing or not behind Default (where it would hide the pieces).
        /// </summary>
        public static int? BoardLayerId
        {
            get
            {
                foreach (var layer in SortingLayer.layers)
                {
                    if (layer.name != BoardLayer) continue;

                    bool behind = SortingLayer.GetLayerValueFromID(layer.id) <
                                  SortingLayer.GetLayerValueFromName("Default");
                    return behind ? layer.id : (int?)null;
                }

                return null;
            }
        }

        private Light2D AddRig(Kind kind, string name, Vector3 at, float scale, int style, int index)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_room, false);
            go.transform.position = at;

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.blendStyleIndex = style;
            light.falloffIntensity = 0.6f;

            _rigs.Add(new Rig { Light = light, Kind = kind, Phase = LightPulse.Phase(index), Scale = scale });
            return light;
        }

        private void AddHaze(Vector3 at, float size, int? layer, int index)
        {
            var go = new GameObject("haze");
            go.transform.SetParent(_room, false);
            go.transform.position = at;
            go.transform.localScale = Vector3.one * size;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = BoardArt.Haze;
            renderer.color = UiTheme.Haze;
            renderer.sortingOrder = HazeOrder;
            if (layer.HasValue) renderer.sortingLayerID = layer.Value;

            _rigs.Add(new Rig { Haze = renderer, Home = at, Kind = Kind.Haze, Phase = LightPulse.Phase(index), Scale = size });
        }

        private void ClearRoom()
        {
            _rigs.Clear();
            if (_room != null) Destroy(_room.gameObject);
            _room = null;
        }
    }
}