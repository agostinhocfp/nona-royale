// Assets/Tests/EditMode/Unity/PoweredShineTests.cs
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The safe cells' glint (G8f) runs one lap round the board, then rests
    /// until the next minute (board skin BS11).
    /// </summary>
    [TestFixture]
    public class PoweredShineTests
    {
        private const int Cells = 12;

        [Test]
        public void EveryCell_GlintsOnce_InsideTheLap()
        {
            for (int i = 0; i < Cells; i++)
            {
                int seen = 0;
                bool inBand = false;
                for (float t = 0f; t < PoweredShine.Period; t += 0.02f)
                {
                    bool now = PoweredShine.LocationAt(t, i, Cells) > -1f;
                    if (now && !inBand) seen++;
                    if (now) Assert.Less(t, PoweredShine.LapSeconds + PoweredShine.SweepSeconds, $"cell {i} at {t}");
                    inBand = now;
                }

                Assert.AreEqual(1, seen, $"cell {i}");
            }
        }

        [Test]
        public void TheRestOfThePeriod_IsStill()
        {
            for (float t = PoweredShine.LapSeconds + PoweredShine.SweepSeconds; t < PoweredShine.Period; t += 0.25f)
                for (int i = 0; i < Cells; i++)
                    Assert.Less(PoweredShine.LocationAt(t, i, Cells), -1f, $"cell {i} at {t}");
        }

        [Test]
        public void Laps_AreAMinuteApart()
        {
            Assert.AreEqual(60f, PoweredShine.Period);
            Assert.AreEqual(PoweredShine.LocationAt(0.3f, 0, Cells), PoweredShine.LocationAt(0.3f + PoweredShine.Period, 0, Cells), 1e-4f);
        }
    }
}
