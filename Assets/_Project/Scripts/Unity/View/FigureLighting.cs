// Assets/_Project/Scripts/Unity/View/FigureLighting.cs
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Turns a 2D light's normal-map reading on or off (OPERATOR_LOOKBOOK.md,
    /// LB5d), so the rigged figures' cel-facet normal maps catch it.
    /// </summary>
    /// <remarks>
    /// <b>Which lights</b> (designer, 2026-09-21): the room's warm pools
    /// (vault, arms, tables) and the knockout burst. The cyan floor lights
    /// reach only the board, which has no normal maps, and a global light
    /// cannot read them, so neither changes.
    ///
    /// <b>The height.</b> URP lights a normal-mapped surface as if the light
    /// sat above the table at this distance. A sprite with no normal map
    /// (the board) is flat, so it now dims toward the pool's edge by
    /// height ÷ √(distance² + height²). Set as a fraction of the light's outer
    /// radius: at 1.2 the edge keeps 77% of the pool's light while a facet
    /// turned toward the light still gains on one turned away. Lower is more
    /// dramatic on the figures and darker at the board's pool edges.
    ///
    /// <b>Set by reflection.</b> URP 17 exposes <c>normalMapQuality</c> and
    /// <c>normalMapDistance</c> read-only; they are meant to be set in the
    /// inspector, and every light here is built in code (ADR-0008). The
    /// serialized fields behind them, <c>m_NormalMapQuality</c> and
    /// <c>m_NormalMapDistance</c>, are written directly. If a URP upgrade
    /// renames them, this logs one warning and the figures simply go unlit
    /// by normals, as before LB5d; nothing breaks.
    /// </remarks>
    public static class FigureLighting
    {
        /// <summary>Normal-map height as a fraction of the light's outer radius.</summary>
        public const float DefaultHeight = 1.2f;

        private static readonly FieldInfo QualityField =
            typeof(Light2D).GetField("m_NormalMapQuality", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo DistanceField =
            typeof(Light2D).GetField("m_NormalMapDistance", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool s_warned;

        /// <summary>Whether this URP lets the normal-map settings be written from code.</summary>
        public static bool Supported =>
            QualityField != null && QualityField.FieldType == typeof(Light2D.NormalMapQuality) &&
            DistanceField != null && DistanceField.FieldType == typeof(float);

        public static void UseNormals(Light2D light, bool on, float heightFraction)
        {
            if (light == null) return;

            if (!Supported)
            {
                if (!s_warned)
                {
                    Debug.LogWarning("[Lighting] This URP version hides Light2D's normal-map fields, so the figures' normal maps go unused.");
                    s_warned = true;
                }

                return;
            }

            var quality = on ? Light2D.NormalMapQuality.Accurate : Light2D.NormalMapQuality.Disabled;
            if (light.normalMapQuality != quality) QualityField.SetValue(light, quality);

            if (!on) return;

            float height = light.pointLightOuterRadius * (heightFraction > 0.01f ? heightFraction : DefaultHeight);
            if (!Mathf.Approximately(light.normalMapDistance, height)) DistanceField.SetValue(light, height);
        }
    }
}
