// Assets/Tests/EditMode/Unity/SfxRecipesTests.cs
using System;
using NonaRoyale.Unity.Audio;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.Audio
{
    [TestFixture]
    public class SfxRecipesTests
    {
        [Test]
        public void EveryCueAndVariant_BuildsAShortCleanSound()
        {
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            for (int v = 0; v < SfxRecipes.VariantsOf(cue); v++)
            {
                var b = SfxRecipes.Build(cue, v);
                float seconds = b.Length / (float)Synth.SampleRate;

                Assert.That(seconds, Is.InRange(0.04f, 1.2f), $"{cue} {v}");
                Assert.That(Synth.Peak(b), Is.InRange(0.5f, 0.96f), $"{cue} {v}");
                foreach (float s in b) Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s), $"{cue} {v}");

                // Ends in silence: nothing is cut off mid-ring, so no click at the end.
                Assert.That(MathF.Abs(b[b.Length - 1]), Is.LessThan(0.002f), $"{cue} {v}");
            }
        }

        [Test]
        public void ACue_IsTheSameEveryTime()
        {
            CollectionAssert.AreEqual(SfxRecipes.Build(SoundCue.Step, 2), SfxRecipes.Build(SoundCue.Step, 2));
        }

        [Test]
        public void Variants_Differ()
        {
            CollectionAssert.AreNotEqual(SfxRecipes.Build(SoundCue.UiClick, 0), SfxRecipes.Build(SoundCue.UiClick, 1));
        }

        [Test]
        public void Resonate_StaysBoundedAcrossAWideGlide()
        {
            var b = Synth.Buffer(0.5f);
            Synth.Resonate(b, 0f, 0.5f, 1f, 0.01f, 0.2f, 40f, 30000f, 20f, new SynthRandom(3));

            foreach (float s in b) Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s));
            Assert.That(Synth.Peak(b), Is.InRange(0.01f, 4f));
        }

        [Test]
        public void Resonate_CutShort_FadesInsteadOfClicking()
        {
            // A band that would still be loud at its end gets the declick fade.
            var b = Synth.Buffer(0.3f);
            Synth.Resonate(b, 0f, 0.2f, 1f, 0.001f, 100f, 500f, 500f, 2f, new SynthRandom(5));

            Assert.That(Synth.Peak(b), Is.GreaterThan(0.1f));
            Assert.That(MathF.Abs(b[Synth.Samples(0.2f) - 1]), Is.LessThan(0.01f));
        }

        [Test]
        public void Resonate_PassesItsCentreAndRejectsFarAway()
        {
            // A narrow band at 1 kHz: a 1 kHz sine should come through far
            // stronger than a 100 Hz one, measured by driving the filter with
            // noise and comparing the correlation with each tone.
            var b = Synth.Buffer(1f);
            Synth.Resonate(b, 0f, 1f, 1f, 0.001f, 100f, 1000f, 1000f, 8f, new SynthRandom(9));

            Assert.That(Energy(b, 1000f), Is.GreaterThan(Energy(b, 100f) * 20f));
        }

        [Test]
        public void Darken_KeepsLowsAndCutsHighs()
        {
            var low = Sine(100f);
            var high = Sine(8000f);
            Synth.Darken(low, 1000f);
            Synth.Darken(high, 1000f);

            // Steady state only: skip the filter's first 0.1 s.
            Assert.That(Synth.Peak(low[4410..]), Is.GreaterThan(0.95f));
            Assert.That(Synth.Peak(high[4410..]), Is.LessThan(0.03f));
        }

        private static float[] Sine(float hz)
        {
            var b = Synth.Buffer(0.5f);
            for (int i = 0; i < b.Length; i++) b[i] = MathF.Sin(2f * MathF.PI * hz * i / Synth.SampleRate);
            return b;
        }

        /// <summary>Energy of the buffer at one frequency (a single Goertzel-style bin).</summary>
        private static float Energy(float[] b, float hz)
        {
            double re = 0, im = 0;
            for (int i = 0; i < b.Length; i++)
            {
                double phase = 2.0 * Math.PI * hz * i / Synth.SampleRate;
                re += b[i] * Math.Cos(phase);
                im += b[i] * Math.Sin(phase);
            }

            return (float)(re * re + im * im);
        }
    }
}