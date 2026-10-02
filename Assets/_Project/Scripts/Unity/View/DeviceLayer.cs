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
    ///
    /// <b>A beacon's patch breathes</b> (CORE_GAMEPLAY.md, CG8): its area
    /// swells and fades over <see cref="BeaconPulsePeriod"/> seconds around
    /// its resting alpha, so a strike that is coming reads as live rather than
    /// as paint on the floor. Slow on purpose: it should be noticed, not
    /// flicker. The target mark stays steady, so the anchor cell is always
    /// readable, and under Reduced motion the patch holds still. Only Drone
    /// Strike paints a beacon today.
    ///
    /// <b>A blast zone's cells run with lava</b> (CORE_GAMEPLAY.md, CG10):
    /// <see cref="LavaTexture"/>'s slow molten veins under a thinned seat tint,
    /// so the cells read as ground that burns, and the ring and the faint tint
    /// still say whose it is. Armed it glows; once it has gone off and only
    /// lingers it cools to a dimmer flow, keeping ADR-0007's armed and
    /// lingering looks apart. Each cell starts at its own point in the loop
    /// and is turned and flipped its own way, so neighbours never pulse in
    /// step. A table is a trap, not a blast, and keeps its plain area. Only
    /// Killzone deploys a blast zone today. Reduced motion holds the lava
    /// still.
    ///
    /// <b>A crowd zone churns with haze instead</b> (CG13): Lethe's Eris'
    /// Exploit gets <see cref="HazeTexture"/>'s sinister green murk and her
    /// silver wisps, drifting slowly, laid and cross-faded exactly as the
    /// lava is, so the two zones on the board are never mistaken for each
    /// other.
    ///
    /// <b>A Cryo Field frosts the cells it reaches</b> (CG12): the area
    /// round Mimi the tick will bite, from <c>GameEngine.ActiveFields</c>, in
    /// <see cref="FrostTexture"/>'s pale crystals, breathing slowly. It moves
    /// with her whenever the board is redrawn. No ring and no seat tint: the
    /// field is centred on Mimi, so she is its mark. Reduced motion holds it
    /// still.
    ///
    /// <b>A Zero-Day charge lights its blast</b> (CG14): the cells its
    /// detonation will reach round the carrier, from
    /// <c>GameEngine.ActiveCharges</c>, in a faint magenta that pulses a
    /// little quicker than a beacon, like a timer. The carrier itself
    /// carries the blinking light (<see cref="ChargeLight"/>).
    ///
    /// <b>Fortuna's table is a table</b> (CG14): <see cref="FeltTexture"/>'s
    /// green felt with a gold rail on each cell it covers, and a short stack
    /// of chips in the owner's seat colour on its own cell, which is how the
    /// board says whose it is. Lit, not glowing, because it is an object.
    /// </remarks>
    public sealed class DeviceLayer : MonoBehaviour
    {
        private const int Order = 0;

        private const float BeaconAreaAlpha = 0.16f;
        private const float ArmedZoneAreaAlpha = 0.30f;
        private const float LingeringZoneAreaAlpha = 0.18f;

        /// <summary>The seat tint over lava: thinned, so it colours the lava rather than hiding it.</summary>
        private const float ArmedLavaTintAlpha = 0.12f;
        private const float LingeringLavaTintAlpha = 0.07f;

        /// <summary>The lava itself: brighter while the zone is armed, cooler once it only lingers.</summary>
        private const float ArmedLavaAlpha = 0.6f;
        private const float LingeringLavaAlpha = 0.34f;

        /// <summary>Under the tint (0) and over every board layer (−10 and below).</summary>
        private const int LavaOrder = -1;

        /// <summary>A Cryo Field's frost: at rest, how far it breathes either side, and how slowly.</summary>
        private const float FrostAlpha = 0.42f;
        private const float FrostBreathDepth = 0.18f;
        private const float FrostBreathPeriod = 4.5f;

        /// <summary>A Zero-Day blast: faint, ticking faster than a beacon's breath.</summary>
        private const float ChargeAreaAlpha = 0.15f;
        private const float ChargePulsePeriod = 1.3f;
        private const float ChargePulseDepth = 0.55f;

        /// <summary>The table: felt alpha, and its chip stack's size, rise per chip and offset toward a corner of its cell.</summary>
        private const float FeltAlpha = 0.95f;
        private const float ChipSize = 0.27f;
        private const float ChipRise = 0.045f;
        private const float ChipOffset = 0.2f;
        private const int ChipCount = 3;

        /// <summary>Seconds per breath of a beacon's patch.</summary>
        private const float BeaconPulsePeriod = 2.8f;

        /// <summary>How far the patch's alpha swings either side of its rest: 0.16 ± 60%, so 0.06 to 0.26.</summary>
        private const float BeaconPulseDepth = 0.6f;

        private readonly List<GameObject> _markers = new List<GameObject>();
        private readonly List<SpriteRenderer> _beaconAreas = new List<SpriteRenderer>();
        private readonly List<LavaCell> _lava = new List<LavaCell>();
        private readonly List<SpriteRenderer> _frost = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _chargeAreas = new List<SpriteRenderer>();

        /// <summary>
        /// One cell of a flowing surface, lava or haze: two frames
        /// cross-fading, its own start in the loop, its peak alpha, and the
        /// frames and loop it steps through.
        /// </summary>
        private sealed class LavaCell
        {
            public SpriteRenderer Under;
            public SpriteRenderer Over;
            public float Phase;
            public float Alpha;
            public IReadOnlyList<Sprite> Frames;
            public float LoopSeconds;
        }
        private BoardLayout _layout;

        /// <summary>Reduced motion: the patch holds its resting alpha.</summary>
        public bool Reduced { get; set; }

        public void Bind(BoardLayout layout) => _layout = layout;

        public void Clear()
        {
            foreach (var marker in _markers)
                if (marker != null) Destroy(marker);

            _markers.Clear();
            _beaconAreas.Clear();
            _lava.Clear();
            _frost.Clear();
            _chargeAreas.Clear();
        }

        private void Update()
        {
            if (_lava.Count > 0) FlowLava();
            if (_frost.Count > 0) BreatheFrost();
            if (_chargeAreas.Count > 0) TickCharges();
            if (_beaconAreas.Count == 0) return;

            float alpha = BeaconAreaAlpha *
                          LightPulse.Breath(Time.unscaledTimeAsDouble, BeaconPulsePeriod, BeaconPulseDepth, 0f, Reduced);

            foreach (var area in _beaconAreas)
            {
                if (area == null) continue;

                var colour = area.color;
                colour.a = alpha;
                area.color = colour;
            }
        }

        /// <summary>
        /// Replaces everything drawn with the given effects and, when given,
        /// the cells each standing Cryo Field reaches (CG12) and each pending
        /// Zero-Day's blast (CG14).
        /// </summary>
        public void Show(IReadOnlyList<CellEffectSnapshot> effects, IReadOnlyList<CellEffectSnapshot> fields = null,
            IReadOnlyList<CellEffectSnapshot> charges = null)
        {
            Clear();

            if (_layout == null) return;

            if (fields != null)
                foreach (var field in fields)
                    foreach (var cell in field.Covered)
                        SpawnFrost(cell);

            if (charges != null)
            {
                var magenta = StatusPalette.For(NonaRoyale.Core.Model.StatusKind.ZeroDayCharge);
                foreach (var charge in charges)
                    foreach (var cell in charge.Covered)
                        _chargeAreas.Add(Spawn(cell, Primitives.Square, _layout.CellSize * 0.9f, WithAlpha(magenta, ChargeAreaAlpha)));
            }

            if (effects == null) return;

            foreach (var effect in effects)
            {
                var seat = BoardLayout.ColourOf(effect.Owner);

                if (effect.IsTable)
                {
                    DrawTable(effect, seat);
                    continue;
                }

                bool haze = effect.IsZone && effect.IsCrowdZone;
                bool lava = effect.IsZone && !effect.IsTable && !haze;
                bool flows = lava || haze;

                float areaAlpha = !effect.IsZone ? BeaconAreaAlpha
                    : flows ? (effect.HasDetonated ? LingeringLavaTintAlpha : ArmedLavaTintAlpha)
                    : effect.HasDetonated ? LingeringZoneAreaAlpha
                    : ArmedZoneAreaAlpha;

                foreach (var cell in effect.Covered)
                {
                    var area = Spawn(cell, Primitives.Square, _layout.CellSize * 0.9f, WithAlpha(seat, areaAlpha));
                    if (!effect.IsZone) _beaconAreas.Add(area);
                    float flowAlpha = effect.HasDetonated ? LingeringLavaAlpha : ArmedLavaAlpha;
                    if (lava) SpawnFlow(cell, flowAlpha, LavaTexture.Frames, LavaTexture.LoopSeconds);
                    if (haze) SpawnFlow(cell, flowAlpha, HazeTexture.Frames, HazeTexture.LoopSeconds);
                }

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

        /// <summary>
        /// Lays a cell of a flowing surface, lava or haze: two stacked frames
        /// that cross-fade, turned a quarter-turn multiple and flipped by the
        /// cell's own hash, so a zone's cells are not stamped copies. Unlit
        /// where the material is there, so it glows the same under every light
        /// in the room.
        /// </summary>
        private void SpawnFlow(CellRef cell, float alpha, IReadOnlyList<Sprite> frames, float loopSeconds)
        {
            int hash = cell.GetHashCode() * 486187739;
            unchecked { hash ^= hash >> 15; }

            var unlit = ShaderFx.Source(ShaderFx.ChipUnlit);
            var tile = new LavaCell
            {
                Phase = ((hash & 0xFFFF) / 65535f) * frames.Count,
                Alpha = alpha,
                Frames = frames,
                LoopSeconds = loopSeconds,
            };

            for (int layer = 0; layer < 2; layer++)
            {
                var renderer = Spawn(cell, frames[0], _layout.CellSize * 0.88f, new Color(1f, 1f, 1f, 0f));
                renderer.sortingOrder = LavaOrder;
                renderer.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * ((hash >> 17) & 3));
                renderer.flipX = ((hash >> 19) & 1) == 1;
                renderer.flipY = ((hash >> 20) & 1) == 1;
                if (unlit != null) renderer.sharedMaterial = unlit;

                if (layer == 0) tile.Under = renderer;
                else tile.Over = renderer;
            }

            _lava.Add(tile);
        }

        /// <summary>
        /// Steps every flowing cell along its loop, cross-fading between
        /// neighbouring frames so the flow never ticks. Held on each cell's
        /// first frame under Reduced motion.
        /// </summary>
        private void FlowLava()
        {
            foreach (var tile in _lava)
            {
                if (tile.Under == null || tile.Over == null || tile.Frames == null) continue;

                var frames = tile.Frames;
                int count = frames.Count;
                float clock = Reduced ? 0f : Time.unscaledTime / tile.LoopSeconds * count;

                float position = clock + tile.Phase;
                int step = Mathf.FloorToInt(position);
                float blend = Reduced ? 0f : position - step;

                tile.Under.sprite = frames[((step % count) + count) % count];
                tile.Over.sprite = frames[(((step + 1) % count) + count) % count];
                tile.Under.color = new Color(1f, 1f, 1f, tile.Alpha * (1f - blend));
                tile.Over.color = new Color(1f, 1f, 1f, tile.Alpha * blend);
            }
        }

        /// <summary>Felt on every covered cell, and the owner's chip stack on the table's own cell.</summary>
        private void DrawTable(CellEffectSnapshot table, Color seat)
        {
            foreach (var cell in table.Covered)
            {
                var felt = Spawn(cell, FeltTexture.Sprite, _layout.CellSize * 0.9f, new Color(1f, 1f, 1f, FeltAlpha));
                felt.sortingOrder = LavaOrder;
            }

            var body = Color.Lerp(seat, Color.black, 0.2f);
            var corner = new Vector3(ChipOffset, ChipOffset, 0f) * _layout.CellSize;

            for (int i = 0; i < ChipCount; i++)
            {
                // Each chip a hair nearer the camera than the one under it, so the
                // stack draws bottom to top inside their shared sorting order.
                var lift = corner + new Vector3(0f, i * ChipRise * _layout.CellSize, -0.002f * (2 * i + 1));

                var chip = Spawn(table.Cell, Primitives.Disc, _layout.CellSize * ChipSize, WithAlpha(body, 1f));
                chip.transform.position += lift;
                chip.sortingOrder = Order;

                var rim = Spawn(table.Cell, Primitives.Ring, _layout.CellSize * ChipSize, WithAlpha(UiTheme.GoldBright, 0.85f));
                rim.transform.position += lift + new Vector3(0f, 0f, -0.001f);
                rim.sortingOrder = Order;
            }
        }

        /// <summary>Pulses the Zero-Day blasts together, a timer's tick; held at rest under Reduced motion.</summary>
        private void TickCharges()
        {
            float alpha = ChargeAreaAlpha *
                          LightPulse.Breath(Time.unscaledTimeAsDouble, ChargePulsePeriod, ChargePulseDepth, 0f, Reduced);

            foreach (var area in _chargeAreas)
            {
                if (area == null) continue;
                var colour = area.color;
                colour.a = alpha;
                area.color = colour;
            }
        }

        /// <summary>Lays a cell of frost, turned and flipped by the cell's hash so neighbours differ.</summary>
        private void SpawnFrost(CellRef cell)
        {
            int hash = cell.GetHashCode() * 668265263;
            unchecked { hash ^= hash >> 13; }

            var renderer = Spawn(cell, FrostTexture.Sprite, _layout.CellSize * 0.9f, new Color(1f, 1f, 1f, FrostAlpha));
            renderer.sortingOrder = LavaOrder;
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, 90f * ((hash >> 17) & 3));
            renderer.flipX = ((hash >> 19) & 1) == 1;
            renderer.flipY = ((hash >> 20) & 1) == 1;

            var unlit = ShaderFx.Source(ShaderFx.ChipUnlit);
            if (unlit != null) renderer.sharedMaterial = unlit;

            _frost.Add(renderer);
        }

        /// <summary>Breathes the frost's alpha, one shared slow breath; held at rest under Reduced motion.</summary>
        private void BreatheFrost()
        {
            float alpha = FrostAlpha *
                          LightPulse.Breath(Time.unscaledTimeAsDouble, FrostBreathPeriod, FrostBreathDepth, 0f, Reduced);

            foreach (var frost in _frost)
                if (frost != null) frost.color = new Color(1f, 1f, 1f, alpha);
        }

        private static Color WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }

        private SpriteRenderer Spawn(CellRef cell, Sprite sprite, float size, Color colour)
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
            return renderer;
        }
    }
}
