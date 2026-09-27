// Assets/_Project/Scripts/Unity/View/UiGradient.cs
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Shades a graphic from one colour at its top to another at its foot,
    /// through its vertex colours (G9b): the lacquer on the match bars.
    /// </summary>
    /// <remarks>
    /// The colours multiply the graphic's own, so give the graphic white and
    /// the gradient the real colours. On a plain quad the four corners carry
    /// the whole gradient; on a sliced sprite every row of vertices takes its
    /// own share, so the ramp stays straight either way.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UiGradient : BaseMeshEffect
    {
        private Color _top = Color.white;
        private Color _foot = Color.white;

        /// <summary>Shades <paramref name="graphic"/> from <paramref name="top"/> down to <paramref name="foot"/>.</summary>
        public static UiGradient Attach(Graphic graphic, Color top, Color foot)
        {
            var gradient = graphic.gameObject.AddComponent<UiGradient>();
            gradient._top = top;
            gradient._foot = foot;
            graphic.SetVerticesDirty();
            return gradient;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            var rect = graphic.rectTransform.rect;
            float height = Mathf.Max(rect.height, 1f);
            var vertex = new UIVertex();

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float t = Mathf.Clamp01((vertex.position.y - rect.yMin) / height);
                vertex.color = (Color)vertex.color * Color.Lerp(_foot, _top, t);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
