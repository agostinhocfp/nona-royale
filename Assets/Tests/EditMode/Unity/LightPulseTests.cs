// Assets/Tests/EditMode/Unity/LightPulseTests.cs
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.View
{
    [TestFixture]
    public class LightPulseTests
    {
        [Test]
        public void ReducedMotion_HoldsTheLightSteady()
        {
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(1f, LightPulse.Breath(i * 0.37, 3f, 0.2f, 0.1f, reduced: true));
        }

        [Test]
        public void NoPeriodOrDepth_HoldsTheLightSteady()
        {
            Assert.AreEqual(1f, LightPulse.Breath(1.3, 0f, 0.2f, 0f, false));
            Assert.AreEqual(1f, LightPulse.Breath(1.3, -2f, 0.2f, 0f, false));
            Assert.AreEqual(1f, LightPulse.Breath(1.3, 3f, 0f, 0f, false));
            Assert.AreEqual(1f, LightPulse.Breath(1.3, float.NaN, 0.2f, 0f, false));
        }

        [Test]
        public void Breath_SwingsWithinItsDepth_AndReachesBothEnds()
        {
            float low = 2f, high = 0f;
            for (int i = 0; i < 400; i++)
            {
                float value = LightPulse.Breath(i * 0.01, 4f, 0.2f, 0f, false);
                Assert.That(value, Is.InRange(0.8f - 1e-4f, 1.2f + 1e-4f));
                low = System.Math.Min(low, value);
                high = System.Math.Max(high, value);
            }

            Assert.AreEqual(0.8f, low, 1e-3f);
            Assert.AreEqual(1.2f, high, 1e-3f);
        }

        [Test]
        public void Breath_RepeatsEveryPeriod_AndPhaseShiftsIt()
        {
            Assert.AreEqual(LightPulse.Breath(0.7, 3f, 0.1f, 0f, false), LightPulse.Breath(3.7, 3f, 0.1f, 0f, false), 1e-5f);
            Assert.AreEqual(LightPulse.Breath(0.75, 3f, 0.1f, 0f, false), LightPulse.Breath(0f, 3f, 0.1f, 0.25f, false), 1e-5f);
        }

        [Test]
        public void Breath_NeverGoesDark()
        {
            for (int i = 0; i < 100; i++)
                Assert.GreaterOrEqual(LightPulse.Breath(i * 0.05, 1f, 5f, 0f, false), 0f);
        }

        [Test]
        public void Phases_AreSpreadInsideOneCycle()
        {
            float last = -1f;
            for (int i = 0; i < 20; i++)
            {
                float phase = LightPulse.Phase(i);
                Assert.That(phase, Is.InRange(0f, 0.99999f));
                Assert.Greater(System.Math.Abs(phase - last), 0.05f, $"light {i} swells with the one before");
                last = phase;
            }

            for (int i = 0; i < 20; i++)
                for (int j = i + 1; j < 20; j++)
                    Assert.Greater(System.Math.Abs(LightPulse.Phase(i) - LightPulse.Phase(j)), 0.005f, $"lights {i} and {j}");

            Assert.AreEqual(LightPulse.Phase(3), LightPulse.Phase(-3));
        }
    }
}