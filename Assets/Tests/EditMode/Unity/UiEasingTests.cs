// Assets/Tests/EditMode/Unity/UiEasingTests.cs
using NUnit.Framework;
using NonaRoyale.Unity.View;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// Pins the HUD easing curves (UI_MOTION.md increment U1). The math lives
    /// in <see cref="UiEasing"/>, plain C# with no Unity types, precisely so
    /// these run in the harness.
    /// </summary>
    public sealed class UiEasingTests
    {
        [Test]
        public void Every_curve_anchors_at_zero_and_one()
        {
            foreach (UiEase ease in System.Enum.GetValues(typeof(UiEase)))
            {
                Assert.That(UiEasing.Evaluate(ease, 0f), Is.EqualTo(0f).Within(1e-6f), ease.ToString());
                Assert.That(UiEasing.Evaluate(ease, 1f), Is.EqualTo(1f).Within(1e-6f), ease.ToString());
            }
        }

        [Test]
        public void Ease_out_curves_run_ahead_of_linear()
        {
            // Half-way in time, an ease-out is more than half-way there.
            Assert.That(UiEasing.Evaluate(UiEase.OutQuad, 0.5f), Is.GreaterThan(0.5f));
            Assert.That(UiEasing.Evaluate(UiEase.OutCubic, 0.5f), Is.GreaterThan(0.5f));
        }

        [Test]
        public void Ease_in_runs_behind_linear()
        {
            Assert.That(UiEasing.Evaluate(UiEase.InQuad, 0.5f), Is.LessThan(0.5f));
        }

        [Test]
        public void OutBack_overshoots_past_one_then_settles()
        {
            float peak = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float v = UiEasing.Evaluate(UiEase.OutBack, i / 100f);
                if (v > peak) peak = v;
            }

            Assert.That(peak, Is.GreaterThan(1.05f));
            Assert.That(UiEasing.Evaluate(UiEase.OutBack, 1f), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Out_curves_rise_monotonically()
        {
            foreach (var ease in new[] { UiEase.Linear, UiEase.OutQuad, UiEase.OutCubic, UiEase.InQuad })
            {
                float last = 0f;
                for (int i = 1; i <= 100; i++)
                {
                    float v = UiEasing.Evaluate(ease, i / 100f);
                    Assert.That(v, Is.GreaterThanOrEqualTo(last), ease.ToString());
                    last = v;
                }
            }
        }

        [Test]
        public void Inputs_clamp_to_the_zero_one_range()
        {
            Assert.That(UiEasing.Evaluate(UiEase.OutCubic, -0.5f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(UiEasing.Evaluate(UiEase.OutCubic, 1.5f), Is.EqualTo(1f).Within(1e-6f));
        }
    }
}
