// Assets/_Project/Scripts/Unity/View/UiPulse.cs
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Breathes the alpha of a few graphics, to say "this is the next thing to
    /// press" (GUI increment F2).
    /// </summary>
    /// <remarks>
    /// Unscaled time, so a pause (increment H) that stops the clock does not
    /// freeze the cue.
    /// </remarks>
    public sealed class UiPulse : MonoBehaviour
    {
        public Graphic[] Targets;
        public float Speed = 4f;
        public float Min = 0.2f;
        public float Max = 1f;

        private void Update()
        {
            if (Targets == null) return;

            float alpha = Mathf.Lerp(Min, Max, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Speed));

            foreach (var target in Targets)
            {
                if (target == null) continue;

                var colour = target.color;
                colour.a = alpha;
                target.color = colour;
            }
        }
    }
}
