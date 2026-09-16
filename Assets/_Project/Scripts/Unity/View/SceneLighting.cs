// Assets/_Project/Scripts/Unity/View/SceneLighting.cs
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The scene's 2D lighting under URP (ADR-0010). For now, one white
    /// global light, so lit sprites look exactly as they did unlit.
    /// </summary>
    /// <remarks>
    /// <b>Why a light at all.</b> The 2D Renderer gives every sprite the lit
    /// material (ADR-0010 decision 3). A lit sprite takes its colour from
    /// the 2D lights on its sorting layer: once any 2D light exists, a layer
    /// without a global light starts from black, so everything outside that
    /// light goes dark.
    ///
    /// <b>Built in code, like everything else</b> (no scene wiring). A global
    /// light already in the scene wins: the designer can author one, tint
    /// it, and this class leaves it alone. Two global lights on one layer
    /// and blend style is a URP error, so it never adds a second.
    ///
    /// <b>Later.</b> Focal pools of light, the cool glow on powered cells
    /// and normal-mapped floors go on top. The ambient will then drop below
    /// 1 so those lights have something to lift.
    /// </remarks>
    public sealed class SceneLighting : MonoBehaviour
    {
        /// <summary>Full, neutral light: a lit sprite renders at its own colour.</summary>
        public const float AmbientIntensity = 1f;

        public static readonly Color AmbientColor = Color.white;

        /// <summary>The global light in use, found or built.</summary>
        public Light2D Ambient { get; private set; }

        /// <summary>Finds the scene's global light, or adds one. Safe to call again.</summary>
        public void Build()
        {
            if (Ambient != null) return;

            foreach (var light in FindObjectsByType<Light2D>())
            {
                if (light.lightType != Light2D.LightType.Global || light.blendStyleIndex != 0) continue;

                Ambient = light;
                return;
            }

            var go = new GameObject("ambient_light");
            go.transform.SetParent(transform, false);

            // Awake runs inside AddComponent and targets every sorting layer.
            var ambient = go.AddComponent<Light2D>();
            ambient.lightType = Light2D.LightType.Global;
            ambient.blendStyleIndex = 0;
            ambient.color = AmbientColor;
            ambient.intensity = AmbientIntensity;
            Ambient = ambient;
        }
    }
}