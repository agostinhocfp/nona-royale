// Assets/Tests/EditMode/Unity/VoiceBlipsTests.cs
using System;
using NonaRoyale.Unity.Audio;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.Audio
{
    [TestFixture]
    public class VoiceBlipsTests
    {
        private static readonly string[] Names =
            { "Bouncer", "Syla", "Kurbyn", "Mimi", "Javi", "Kian", "Nuetu", "Sanity", "Luka", "Revú" };

        [Test]
        public void EveryOperatorAndSlot_BuildsAShortAudibleLine()
        {
            foreach (var name in Names)
            foreach (VoiceSlot slot in Enum.GetValues(typeof(VoiceSlot)))
            for (int v = 0; v < VoiceBlips.VariantsOf(slot); v++)
            {
                var b = VoiceBlips.Build(name, slot, v);
                float seconds = b.Length / (float)Synth.SampleRate;

                Assert.That(seconds, Is.InRange(0.15f, 1.2f), $"{name} {slot} {v}");
                Assert.That(Synth.Peak(b), Is.EqualTo(VoiceBlips.Peak).Within(0.001f), $"{name} {slot} {v}");
                foreach (float s in b) Assert.IsFalse(float.IsNaN(s), $"{name} {slot} {v}");
            }
        }

        [Test]
        public void ALine_IsTheSameEveryTime()
        {
            CollectionAssert.AreEqual(
                VoiceBlips.Build("Luka", VoiceSlot.Kill, 1),
                VoiceBlips.Build("Luka", VoiceSlot.Kill, 1));
        }

        [Test]
        public void Operators_SoundDifferent()
        {
            CollectionAssert.AreNotEqual(
                VoiceBlips.Build("Luka", VoiceSlot.Kill, 0),
                VoiceBlips.Build("Bouncer", VoiceSlot.Kill, 0));
        }

        [Test]
        public void Variants_Differ()
        {
            CollectionAssert.AreNotEqual(
                VoiceBlips.Build("Luka", VoiceSlot.Move, 0),
                VoiceBlips.Build("Luka", VoiceSlot.Move, 1));
        }

        [Test]
        public void ADeath_OutlastsAMove()
        {
            Assert.Greater(
                VoiceBlips.Build("Luka", VoiceSlot.Death, 0).Length,
                VoiceBlips.Build("Luka", VoiceSlot.Move, 0).Length);
        }

        [Test]
        public void AnUnknownName_GetsAStableSignature()
        {
            var a = VoiceBlips.SignatureOf("Fuse");
            var b = VoiceBlips.SignatureOf("Fuse");
            Assert.AreEqual(a.Root, b.Root);
            Assert.AreEqual(a.Wave, b.Wave);
            Assert.That(a.Root, Is.InRange(40f, 62f));
        }

        [Test]
        public void TheHouse_SpeaksLowerThanTheContractors()
        {
            Assert.Less(VoiceBlips.SignatureOf("Bouncer").Root, VoiceBlips.SignatureOf("Syla").Root);
            Assert.Less(VoiceBlips.SignatureOf("Sanity").Root, VoiceBlips.SignatureOf("Mimi").Root);
        }

        [Test]
        public void FileKey_KeepsAsciiLettersAndDigitsOnly()
        {
            Assert.AreEqual("Luka", VoiceBlips.FileKey("Luka"));
            Assert.AreEqual("Revu", VoiceBlips.FileKey("Revú"));
            Assert.AreEqual("ZeroDay", VoiceBlips.FileKey("Zero-Day"));
            Assert.AreEqual("", VoiceBlips.FileKey(null));
        }
    }
}