// Assets/Tests/EditMode/Unity/OperatorGlowTests.cs
using NonaRoyale.Unity.View;
using NUnit.Framework;
using UnityEngine;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// Each operator's device glow (ADR-0014, 2026-09-30): the house glows
    /// the board's cyan, everyone else their own colour, and no glow can be
    /// read as a seat or as the threat rim.
    /// </summary>
    [TestFixture]
    public class OperatorGlowTests
    {
        private static readonly string[] House = { "Bouncer", "Kurbyn", "Javi", "Sanity", "Luka" };
        private static readonly string[] Own = { "Syla", "Mimi", "Kian", "Nuetu", "Fortuna", "Revú", "Lethe" };

        /// <summary>Below this saturation a glow reads as white light, not as a hue.</summary>
        private const float Pale = 0.3f;

        /// <summary>How far, in degrees of hue, a saturated glow keeps from a seat and from the threat rim.</summary>
        private const float Clearance = 20f;

        [Test]
        public void TheHouseAndLuka_GlowTheBoardsCyan()
        {
            foreach (var name in House)
            {
                Assert.AreEqual(UiTheme.Cyan, OperatorGlow.For(name), name);
                Assert.IsTrue(OperatorGlow.IsHouse(name), name);
            }
        }

        [Test]
        public void AnUnknownOperator_GlowsCyan()
        {
            Assert.AreEqual(OperatorGlow.House, OperatorGlow.For("Nobody"));
        }

        [Test]
        public void EveryoneElse_GlowsTheirOwnColour()
        {
            for (int i = 0; i < Own.Length; i++)
            {
                Assert.IsFalse(OperatorGlow.IsHouse(Own[i]), Own[i]);
                Assert.AreNotEqual(UiTheme.Cyan, OperatorGlow.For(Own[i]), Own[i]);

                for (int j = i + 1; j < Own.Length; j++)
                    Assert.AreNotEqual(OperatorGlow.For(Own[i]), OperatorGlow.For(Own[j]), $"{Own[i]} and {Own[j]}");
            }
        }

        [Test]
        public void NoGlow_SitsOnTheThreatRimsHue()
        {
            foreach (var name in Own)
            {
                var glow = OperatorGlow.For(name);
                if (Saturation(glow) < Pale) continue;
                Assert.Greater(HueGap(glow, UiTheme.Threat), Clearance, name);
            }
        }

        [Test]
        public void NoGlow_SitsOnASeatsHue_ButNuetusRed()
        {
            var seats = new[] { UiTheme.SeatRed, UiTheme.SeatBlue, UiTheme.SeatGreen, UiTheme.SeatViolet };

            foreach (var name in Own)
            {
                var glow = OperatorGlow.For(name);
                if (Saturation(glow) < Pale) continue;

                foreach (var seat in seats)
                {
                    // The designer's choice, recorded in ADR-0014: his red is his heritage's.
                    if (name == "Nuetu" && seat == UiTheme.SeatRed) continue;
                    Assert.Greater(HueGap(glow, seat), Clearance, $"{name} against a seat");
                }
            }
        }

        private static float Saturation(Color colour)
        {
            Color.RGBToHSV(colour, out _, out float s, out _);
            return s;
        }

        private static float HueGap(Color a, Color b)
        {
            Color.RGBToHSV(a, out float ha, out _, out _);
            Color.RGBToHSV(b, out float hb, out _, out _);
            float gap = Mathf.Abs(ha - hb) * 360f;
            return Mathf.Min(gap, 360f - gap);
        }
    }
}
