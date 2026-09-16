// Assets/Tests/EditMode/Unity/VoiceRulesTests.cs
using NonaRoyale.Unity.Audio;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.Audio
{
    [TestFixture]
    public class VoiceRulesTests
    {
        private double _roll;
        private VoiceRules _rules;

        [SetUp]
        public void SetUp()
        {
            _roll = 0.0; // every chance roll succeeds unless a test says otherwise
            _rules = new VoiceRules(() => _roll);
        }

        [Test]
        public void ALine_PlaysWhenNothingIsSpeaking()
        {
            Assert.AreEqual(VoiceVerdict.Play, _rules.Request(VoiceSlot.Cast, "Luka", 1.0, 0.0));
            Assert.IsTrue(_rules.Speaking(0.5));
            Assert.IsFalse(_rules.Speaking(1.0));
        }

        [Test]
        public void AHigherSlot_InterruptsTheLinePlaying()
        {
            _rules.Request(VoiceSlot.Deploy, "Luka", 1.0, 0.0);
            Assert.AreEqual(VoiceVerdict.Interrupt, _rules.Request(VoiceSlot.Cast, "Syla", 1.0, 0.2));
        }

        [Test]
        public void ChatterAtOrBelowThePlayingSlot_IsDropped()
        {
            _rules.Request(VoiceSlot.Cast, "Luka", 1.0, 0.0);
            Assert.AreEqual(VoiceVerdict.Drop, _rules.Request(VoiceSlot.Cast, "Syla", 1.0, 0.2));
            Assert.AreEqual(VoiceVerdict.Drop, _rules.Request(VoiceSlot.HitTaken, "Syla", 1.0, 0.2));
        }

        [Test]
        public void TheSameOperator_StaysQuietForTheCooldown()
        {
            _rules.Request(VoiceSlot.Deploy, "Luka", 0.5, 0.0);
            double justInside = VoiceRules.CooldownSeconds - 0.01;
            Assert.AreEqual(VoiceVerdict.Drop, _rules.Request(VoiceSlot.Cast, "Luka", 0.5, justInside));
            Assert.AreEqual(VoiceVerdict.Play, _rules.Request(VoiceSlot.Cast, "Luka", 0.5, VoiceRules.CooldownSeconds));
        }

        [Test]
        public void TheCooldown_IsPerOperator()
        {
            _rules.Request(VoiceSlot.Deploy, "Luka", 0.5, 0.0);
            Assert.AreEqual(VoiceVerdict.Play, _rules.Request(VoiceSlot.Deploy, "Syla", 0.5, 1.0));
        }

        [Test]
        public void Moments_IgnoreTheCooldown()
        {
            _rules.Request(VoiceSlot.Cast, "Luka", 0.5, 0.0);
            Assert.AreEqual(VoiceVerdict.Play, _rules.Request(VoiceSlot.Kill, "Luka", 0.5, 1.0));
        }

        [Test]
        public void Chatter_WaitsABreathAfterALineEnds()
        {
            _rules.Request(VoiceSlot.Deploy, "Luka", 1.0, 0.0);
            double inBreath = 1.0 + VoiceRules.BreathSeconds - 0.01;
            Assert.AreEqual(VoiceVerdict.Drop, _rules.Request(VoiceSlot.Deploy, "Syla", 0.5, inBreath));
            Assert.AreEqual(VoiceVerdict.Play, _rules.Request(VoiceSlot.Deploy, "Syla", 0.5, 1.0 + VoiceRules.BreathSeconds));
        }

        [Test]
        public void AMoment_DoesNotWaitForTheBreath()
        {
            _rules.Request(VoiceSlot.Deploy, "Luka", 1.0, 0.0);
            Assert.AreEqual(VoiceVerdict.Play, _rules.Request(VoiceSlot.Death, "Syla", 0.5, 1.05));
        }

        [Test]
        public void Moves_AreVoicedOnlyOnASuccessfulRoll()
        {
            _roll = VoiceRules.MoveChance;
            Assert.AreEqual(VoiceVerdict.Drop, _rules.Request(VoiceSlot.Move, "Luka", 0.5, 0.0));

            _roll = VoiceRules.MoveChance - 0.01;
            Assert.AreEqual(VoiceVerdict.Play, _rules.Request(VoiceSlot.Move, "Luka", 0.5, 0.0));
        }

        [Test]
        public void OnlyMoves_RollForIt()
        {
            _roll = 0.99;
            Assert.AreEqual(VoiceVerdict.Play, _rules.Request(VoiceSlot.Deploy, "Luka", 0.5, 0.0));
        }

        [Test]
        public void AKillBehindADeath_IsHeldAndPlaysWhenTheDeathEnds()
        {
            Assert.AreEqual(VoiceVerdict.Play, _rules.Request(VoiceSlot.Death, "Syla", 0.8, 0.0));
            Assert.AreEqual(VoiceVerdict.Hold, _rules.Request(VoiceSlot.Kill, "Luka", 0.6, 0.0));

            Assert.IsFalse(_rules.TryRelease(0.5, out _, out _), "still speaking");

            Assert.IsTrue(_rules.TryRelease(0.8, out var slot, out var speaker));
            Assert.AreEqual(VoiceSlot.Kill, slot);
            Assert.AreEqual("Luka", speaker);
            Assert.IsTrue(_rules.Speaking(1.3), "the released line counts as playing for its own length");
            Assert.IsFalse(_rules.TryRelease(1.5, out _, out _), "released once only");
        }

        [Test]
        public void AHeldMoment_ExpiresIfItWaitsTooLong()
        {
            _rules.Request(VoiceSlot.Victory, "Syla", 3.0, 0.0);
            Assert.AreEqual(VoiceVerdict.Hold, _rules.Request(VoiceSlot.Kill, "Luka", 0.6, 0.0));
            Assert.IsFalse(_rules.TryRelease(3.0, out _, out _));
        }

        [Test]
        public void TheMoreImportantHeldMoment_IsKept()
        {
            _rules.Request(VoiceSlot.Victory, "Syla", 1.0, 0.0);
            Assert.AreEqual(VoiceVerdict.Hold, _rules.Request(VoiceSlot.Death, "Bouncer", 0.5, 0.0));
            Assert.AreEqual(VoiceVerdict.Drop, _rules.Request(VoiceSlot.Kill, "Luka", 0.5, 0.1));
            Assert.AreEqual(VoiceVerdict.Hold, _rules.Request(VoiceSlot.Victory, "Kian", 0.5, 0.2));

            Assert.IsTrue(_rules.TryRelease(1.0, out var slot, out var speaker));
            Assert.AreEqual(VoiceSlot.Victory, slot);
            Assert.AreEqual("Kian", speaker);
        }

        [Test]
        public void Stop_ForgetsThePlayingAndHeldLines()
        {
            _rules.Request(VoiceSlot.Death, "Syla", 1.0, 0.0);
            _rules.Request(VoiceSlot.Kill, "Luka", 0.5, 0.0);
            _rules.Stop();

            Assert.IsFalse(_rules.Speaking(0.1));
            Assert.IsFalse(_rules.TryRelease(1.0, out _, out _));
            Assert.AreEqual(VoiceVerdict.Play, _rules.Request(VoiceSlot.Deploy, "Bouncer", 0.5, 0.1));
        }

        [Test]
        public void NoSpeakerOrNoLength_IsDropped()
        {
            Assert.AreEqual(VoiceVerdict.Drop, _rules.Request(VoiceSlot.Kill, null, 1.0, 0.0));
            Assert.AreEqual(VoiceVerdict.Drop, _rules.Request(VoiceSlot.Kill, "Luka", 0.0, 0.0));
            Assert.IsFalse(_rules.Speaking(0.0));
        }

        [Test]
        public void TheSlots_AreOrderedByPriority()
        {
            Assert.Less(VoiceSlot.Move, VoiceSlot.Deploy);
            Assert.Less(VoiceSlot.Deploy, VoiceSlot.HitTaken);
            Assert.Less(VoiceSlot.HitTaken, VoiceSlot.Cast);
            Assert.Less(VoiceSlot.Cast, VoiceSlot.Kill);
            Assert.Less(VoiceSlot.Kill, VoiceSlot.Death);
            Assert.Less(VoiceSlot.Death, VoiceSlot.Victory);
            Assert.IsFalse(VoiceRules.IsMoment(VoiceSlot.Cast));
            Assert.IsTrue(VoiceRules.IsMoment(VoiceSlot.Kill));
        }
    }
}