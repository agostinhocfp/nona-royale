// Assets/_Project/Scripts/Unity/View/ShaderFx.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The All In 1 Sprite Shader effects (LAUNCH_UI_PASS.md, G8), as saved
    /// materials loaded by name.
    /// </summary>
    /// <remarks>
    /// <b>Why saved materials.</b> Every effect in the pack is a
    /// <c>shader_feature_local</c> keyword, and a player build keeps only the
    /// variants some material asset uses. A keyword switched on from code works
    /// in the editor and does nothing in a build. So each effect is a material
    /// under <c>Art/Resources/Art/Fx/</c> with its keyword already on, and code
    /// works on an instance of it, which keeps the keywords.
    ///
    /// <b>Missing is not an error.</b> A missing material, or one whose shader
    /// can't run here, answers null and the caller keeps its code-drawn effect.
    /// One warning per name, so a lost asset shows in the console without
    /// flooding it.
    ///
    /// <b>Which shader.</b> Board sprites use <c>AllIn1Urp2dRenderer</c>: it has
    /// the Universal2D and NormalsRendering passes, so the 2D lights still reach
    /// them. The plain <c>AllIn1SpriteShader</c> is unlit and would drop a board
    /// sprite out of the lighting. uGUI would use <c>AllIn1SpriteShaderUiMask</c>.
    /// </remarks>
    public static class ShaderFx
    {
        /// <summary>The materials' folder under a <c>Resources</c> root.</summary>
        public const string Folder = "Art/Fx/";

        /// <summary>Burn-dissolve for lit sprites: <c>FADE_ON</c> on <c>AllIn1Urp2dRenderer</c> (G8c).</summary>
        public const string BurnLit = "BurnLit";

        public static readonly int FadeAmount = Shader.PropertyToID("_FadeAmount");
        public static readonly int FadeBurnColor = Shader.PropertyToID("_FadeBurnColor");

        /// <summary>
        /// <c>_FadeAmount</c> with nothing dissolved and no burning edge. The
        /// pack's range starts below 0 because at 0 the edge still shows faintly.
        /// </summary>
        public const float FadeNone = -0.1f;

        /// <summary><c>_FadeAmount</c> with the sprite gone.</summary>
        public const float FadeAll = 1f;

        private static readonly Dictionary<string, Material> Loaded = new Dictionary<string, Material>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        /// <summary>The saved material itself. Never modify it: use <see cref="Instance"/>.</summary>
        public static Material Source(string name)
        {
            if (Loaded.TryGetValue(name, out var material)) return material;
            if (Missing.Contains(name)) return null;

            material = Resources.Load<Material>(Folder + name);

            if (material == null || material.shader == null || !material.shader.isSupported)
            {
                Missing.Add(name);
                Debug.LogWarning(material == null
                    ? $"[ShaderFx] No material at Resources/{Folder}{name}; the code-drawn effect runs instead."
                    : $"[ShaderFx] {name}'s shader can't run here; the code-drawn effect runs instead.");
                return null;
            }

            Loaded[name] = material;
            return material;
        }

        /// <summary>
        /// A fresh copy of the saved material, keywords included, or null when
        /// the effect is unavailable. The caller owns it and destroys it.
        /// </summary>
        public static Material Instance(string name)
        {
            var source = Source(name);
            if (source == null) return null;

            return new Material(source) { name = source.name + " (fx)" };
        }

        /// <summary>Clears the cache when play starts without a domain reload.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            Loaded.Clear();
            Missing.Clear();
        }
    }
}
