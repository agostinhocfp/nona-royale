// Assets/Tests/EditMode/Unity/GiltSheenTests.cs
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>The Deco skin's slow gilt sheen (board skin BS5): one pass, then rest.</summary>
    [TestFixture]
    public class GiltSheenTests
    {
        [Test]
        public void Location_CrossesTheSprite_DuringTheSweep()
        {
            float start = GiltSheen.LocationAt(0f);
            float middle = GiltSheen.LocationAt(GiltSheen.SweepSeconds * 0.5f);
            float late = GiltSheen.LocationAt(GiltSheen.SweepSeconds * 0.99f);

            Assert.Less(start, 0f);
            Assert.That(middle, Is.InRange(0.4f, 0.6f));
            Assert.Greater(late, 1f);
        }

        [Test]
        public void Location_IsParkedOffTheSprite_ForTheRestOfThePeriod()
        {
            for (float t = GiltSheen.SweepSeconds + 0.01f; t < GiltSheen.Period; t += 0.25f)
                Assert.Less(GiltSheen.LocationAt(t), -1f, $"t={t}");
        }

        [Test]
        public void Location_Repeats_EveryPeriod()
        {
            Assert.AreEqual(GiltSheen.LocationAt(0.7f), GiltSheen.LocationAt(0.7f + GiltSheen.Period), 1e-4f);
        }

        [Test]
        public void TheSheenIsSlowAndRare_NotAGlint()
        {
            // PoweredShine's glint means "powered": the sheen must never be mistaken for it.
            Assert.GreaterOrEqual(GiltSheen.Period, 6f);
            Assert.GreaterOrEqual(GiltSheen.SweepSeconds, 1.2f);
        }
    }
}
