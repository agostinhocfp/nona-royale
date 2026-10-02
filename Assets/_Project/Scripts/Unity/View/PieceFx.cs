// Assets/_Project/Scripts/Unity/View/PieceFx.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The piece effects drawn by the All In 1 Sprite Shader pack
    /// (CORE_GAMEPLAY.md, CG16): greyscale for a piece in the yard or stunned,
    /// a magenta hologram with a glitch on a Zero-Day carrier, and a heat
    /// shimmer for Stealth.
    /// </summary>
    /// <remarks>
    /// <b>Copies on top, not the piece's own material,</b> for the greyscale
    /// and the hologram. Each is a copy of the portrait (a chip's face and
    /// emblem, or a rendered figure's body) drawn a hair nearer the camera
    /// with the effect's material. The piece keeps its own material, so the
    /// two effects stack with each other and with everything already on the
    /// piece, and a copy's alpha is the effect's strength: a greyscale copy at
    /// 0.7 is the portrait 70% desaturated. A chip's body and ring are never
    /// copied, so the seat colour always reads, and nor is a procedural pawn,
    /// which is all seat colour.
    ///
    /// <b>The shimmer is the exception.</b> A heat haze has to move the piece
    /// itself, so while Stealth lasts every part of the chip (or the figure)
    /// draws with the shimmer material, and gets its own back when it ends. A
    /// copy laid over it is not distorted, so a stealthed piece that is also
    /// stunned or carrying a charge shows the copy steady over the shimmer.
    /// That pairing is rare, and only the copy looks steady.
    ///
    /// <b>Lit or unlit, like what it copies.</b> A chip's parts are unlit
    /// (<see cref="ShaderFx.ChipUnlit"/>), so they get the unlit materials, on
    /// the pack's plain shader. A rendered figure takes the room's light, so
    /// it gets the lit ones, on the 2D renderer shader.
    ///
    /// <b>Missing is not an error</b> (<see cref="ShaderFx"/>): without its
    /// material an effect is just not drawn, and the shimmer answers false so
    /// the caller shows <see cref="StealthShimmer"/>'s violet copies instead.
    /// Reduced motion does the same for the shimmer, holds the hologram's
    /// stripes still and drops its glitch.
    ///
    /// <b>The shader's own clock.</b> The shimmer and the hologram run on
    /// real time, like the lava and the haze, so they keep moving while the
    /// game is paused.
    /// </remarks>
    public sealed class PieceFx : MonoBehaviour
    {
        /// <summary>How far a seated operator in the yard is desaturated: benched, still recognisable.</summary>
        public const float YardGrey = 0.7f;

        /// <summary>How far a stunned operator is desaturated, under its stars.</summary>
        public const float StunGrey = 0.55f;

        /// <summary>The hologram copy's opacity; the stripes then vary it.</summary>
        private const float HoloAlpha = 0.85f;

        /// <summary>How quickly the greyscale eases in and out, per second.</summary>
        private const float GreyRate = 5f;

        /// <summary>Toward the camera, in world units, so a copy at its source's order draws on top (the chip stack's rule).</summary>
        private const float GreyLift = 0.002f;
        private const float HoloLift = 0.004f;

        private sealed class Skin
        {
            public SpriteRenderer Source;
            public Material Original;
            public bool Unlit;
            public bool Shimmering;
            public SpriteRenderer Grey;
            public SpriteRenderer Holo;
        }

        private OperatorPiece _piece;
        private MotionSettings _motion;

        private readonly Dictionary<SpriteRenderer, Skin> _skins = new Dictionary<SpriteRenderer, Skin>();
        private readonly List<SpriteRenderer> _faces = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _parts = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _gone = new List<SpriteRenderer>();

        private float _grey;
        private float _greyTarget;
        private bool _greyable;
        private bool _holo;
        private bool _shimmer;

        private bool Reduced => _motion != null && _motion.ReducedMotion;

        public void Bind(OperatorPiece piece, MotionSettings motion)
        {
            _piece = piece;
            _motion = motion;
        }

        /// <summary>Desaturates the portrait toward <paramref name="strength"/> (0 is full colour); eased, instant under Reduced motion.</summary>
        public void SetGrey(float strength) => _greyTarget = Mathf.Clamp01(strength);

        /// <summary>Lays the Zero-Day hologram over the portrait, or takes it off.</summary>
        public void SetHologram(bool on) => _holo = on;

        /// <summary>
        /// Turns the Stealth shimmer on or off. Answers whether the shader
        /// draws it: false when it is off, when Reduced motion is on, or when
        /// the material is missing, and the caller then draws its fallback.
        /// </summary>
        public bool SetShimmer(bool on)
        {
            bool drawn = on && !Reduced && Shared(ShaderFx.ShimmerUnlit, false) != null && Shared(ShaderFx.ShimmerLit, false) != null;
            _shimmer = drawn;
            if (!drawn) RestoreAll();
            return drawn;
        }

        private void LateUpdate()
        {
            if (_piece == null) return;

            _greyable = _piece.FxParts(_faces, _parts);
            Forget();

            _grey = Reduced
                ? _greyTarget
                : Mathf.MoveTowards(_grey, _greyTarget, Time.deltaTime * GreyRate);

            float grey = _greyable ? _grey : 0f;

            foreach (var source in _faces)
            {
                var skin = SkinFor(source);
                if (skin == null) continue;

                skin.Grey = Overlay(skin, skin.Grey, grey, GreyLift, GreyMaterial(skin.Unlit), "fx_grey");
                skin.Holo = Overlay(skin, skin.Holo, _holo ? HoloAlpha : 0f, HoloLift, HoloMaterial(skin.Unlit), "fx_holo");
            }

            foreach (var source in _parts)
            {
                var skin = SkinFor(source);
                if (skin == null) continue;

                if (_shimmer && !skin.Shimmering)
                {
                    var shimmer = Shared(skin.Unlit ? ShaderFx.ShimmerUnlit : ShaderFx.ShimmerLit, false);
                    if (shimmer == null) continue;

                    skin.Original = source.sharedMaterial;
                    source.sharedMaterial = shimmer;
                    skin.Shimmering = true;
                }
                else if (!_shimmer && skin.Shimmering)
                {
                    Restore(skin);
                }
            }
        }

        /// <summary>
        /// Keeps a copy of <paramref name="skin"/>'s source in step with it at
        /// <paramref name="strength"/>: its sprite, flips, colour and order,
        /// and its place a hair nearer the camera. Built on first need; hidden
        /// at zero strength.
        /// </summary>
        private static SpriteRenderer Overlay(Skin skin, SpriteRenderer overlay, float strength, float lift, Material material, string name)
        {
            var source = skin.Source;
            bool show = strength > 0.001f && material != null && source.enabled && source.sprite != null
                        && source.gameObject.activeInHierarchy;

            if (!show)
            {
                if (overlay != null && overlay.enabled) overlay.enabled = false;
                return overlay;
            }

            if (overlay == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(source.transform, false);
                overlay = go.AddComponent<SpriteRenderer>();
            }

            overlay.enabled = true;
            if (overlay.sharedMaterial != material) overlay.sharedMaterial = material;
            overlay.sprite = source.sprite;
            overlay.flipX = source.flipX;
            overlay.flipY = source.flipY;
            overlay.sortingLayerID = source.sortingLayerID;
            overlay.sortingOrder = source.sortingOrder;
            overlay.maskInteraction = source.maskInteraction;

            var colour = source.color;
            colour.a *= strength;
            overlay.color = colour;

            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = Vector3.one;
            overlay.transform.position = source.transform.position + new Vector3(0f, 0f, -lift);
            return overlay;
        }

        /// <summary>The skin for a source, made on first sight, when its material is still its own.</summary>
        private Skin SkinFor(SpriteRenderer source)
        {
            if (source == null) return null;
            if (_skins.TryGetValue(source, out var skin)) return skin;

            var unlit = ShaderFx.Source(ShaderFx.ChipUnlit);
            skin = new Skin
            {
                Source = source,
                Original = source.sharedMaterial,
                Unlit = unlit != null && source.sharedMaterial == unlit,
            };
            _skins[source] = skin;
            return skin;
        }

        /// <summary>
        /// Drops the skins of sources the piece no longer draws from (the
        /// style changed, or a part was destroyed): their copies go and their
        /// material comes back.
        /// </summary>
        private void Forget()
        {
            _gone.Clear();
            foreach (var pair in _skins)
                if (pair.Key == null || (!_faces.Contains(pair.Key) && !_parts.Contains(pair.Key)))
                    _gone.Add(pair.Key);

            foreach (var source in _gone)
            {
                var skin = _skins[source];
                Restore(skin);
                if (skin.Grey != null) Destroy(skin.Grey.gameObject);
                if (skin.Holo != null) Destroy(skin.Holo.gameObject);
                _skins.Remove(source);
            }
        }

        private void RestoreAll()
        {
            foreach (var skin in _skins.Values) Restore(skin);
        }

        private static void Restore(Skin skin)
        {
            if (!skin.Shimmering) return;
            if (skin.Source != null) skin.Source.sharedMaterial = skin.Original;
            skin.Shimmering = false;
        }

        private void OnDestroy()
        {
            RestoreAll();
        }

        // ── Shared materials ─────────────────────────────────────────────

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly HashSet<string> Unavailable = new HashSet<string>();

        private static Material GreyMaterial(bool unlit) => Shared(unlit ? ShaderFx.GreyUnlit : ShaderFx.GreyLit, false);

        private Material HoloMaterial(bool unlit) => Shared(unlit ? ShaderFx.HoloUnlit : ShaderFx.HoloLit, Reduced);

        /// <summary>
        /// One instance per effect, lighting and motion setting, shared by
        /// every piece so they batch. The hologram's is tinted in the Zero-Day
        /// magenta, and its still variant has no glitch and no scrolling.
        /// </summary>
        private static Material Shared(string name, bool still)
        {
            string key = still ? name + "/still" : name;
            if (Materials.TryGetValue(key, out var material)) return material;
            if (Unavailable.Contains(key)) return null;

            material = ShaderFx.Instance(name);
            if (material == null)
            {
                Unavailable.Add(key);
                return null;
            }

            if (name == ShaderFx.HoloUnlit || name == ShaderFx.HoloLit)
            {
                material.SetColor(ShaderFx.HologramStripeColor, StatusPalette.For(StatusKind.ZeroDayCharge));
                if (still)
                {
                    material.SetFloat(ShaderFx.HologramStripesSpeed, 0f);
                    material.SetFloat(ShaderFx.GlitchAmount, 0f);
                }
            }

            material.name = key + " (fx)";
            Materials[key] = material;
            return material;
        }

        /// <summary>Clears the shared materials when play starts without a domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShared()
        {
            Materials.Clear();
            Unavailable.Clear();
        }
    }
}
