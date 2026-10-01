// Assets/Tests/EditMode/Unity/AudioLevelsTests.cs
using NonaRoyale.Unity.Audio;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.Audio
{
    [TestFixture]
    public class AudioLevelsTests
    {
        [Test]
        public void MusicDefault_IsTheLevelTheDesignerAskedFor()
        {
            Assert.AreEqual(0.36f, AudioLevels.DefaultMusic, 1e-4f);
            Assert.AreEqual(AudioLevels.DefaultMusic, new AudioLevels().Music);
            Assert.AreEqual(36, AudioLevels.Percent(new AudioLevels().Music));
        }

        [Test]
        public void Decibels_IsZeroAtFullGain_AndTheFloorAtSilence()
        {
            Assert.AreEqual(0f, AudioLevels.Decibels(1f), 1e-4f);
            Assert.AreEqual(-6.0206f, AudioLevels.Decibels(0.5f), 1e-3f);
            Assert.AreEqual(-80f, AudioLevels.Decibels(0.0001f), 1e-4f);
            Assert.AreEqual(-80f, AudioLevels.Decibels(0f));
            Assert.AreEqual(-80f, AudioLevels.Decibels(-1f));
            Assert.AreEqual(-80f, AudioLevels.Decibels(float.NaN));
            Assert.AreEqual(0f, AudioLevels.Decibels(4f), "a gain above 1 never boosts");
        }

        [Test]
        public void Decibels_RisesWithTheSlider()
        {
            float last = float.NegativeInfinity;
            for (int i = 0; i <= 100; i++)
            {
                float db = AudioLevels.Decibels(AudioLevels.Curve(i / 100f));
                Assert.GreaterOrEqual(db, last, $"slider {i}%");
                last = db;
            }
        }

        [Test]
        public void Mute_SilencesMasterOnly()
        {
            var levels = new AudioLevels { Master = 1f, Music = 1f, Muted = true };

            Assert.AreEqual(0f, levels.MasterGain);
            Assert.AreEqual(0f, levels.Gain(AudioBus.Music));
            Assert.AreEqual(1f, levels.BusGain(AudioBus.Music), "the bus fader keeps its place");
        }

        [Test]
        public void MuteMusic_SilencesTheMusicBusOnly()
        {
            var levels = new AudioLevels { Master = 1f, Music = 1f, Sfx = 1f, Voice = 1f, MusicMuted = true };

            Assert.AreEqual(0f, levels.BusGain(AudioBus.Music));
            Assert.AreEqual(0f, levels.Gain(AudioBus.Music));
            Assert.AreEqual(-80f, AudioLevels.Decibels(levels.BusGain(AudioBus.Music)), "the mixer's music fader bottoms out");
            Assert.AreEqual(1f, levels.Gain(AudioBus.Sfx));
            Assert.AreEqual(1f, levels.Gain(AudioBus.Ui));
            Assert.AreEqual(1f, levels.Gain(AudioBus.Voice));
            Assert.AreEqual(1f, levels.MasterGain);
            Assert.AreEqual(1f, levels.Music, "the slider keeps its place");
        }

        [Test]
        public void MuteMusic_IsCopiedAndCompared()
        {
            var muted = new AudioLevels { MusicMuted = true };
            var copy = new AudioLevels();
            Assert.IsFalse(copy.SameAs(muted));

            copy.CopyFrom(muted);

            Assert.IsTrue(copy.MusicMuted);
            Assert.IsTrue(copy.SameAs(muted));
        }

        [Test]
        public void BusGain_UsesItsOwnSlider_AndClicksFollowEffects()
        {
            var levels = new AudioLevels { Master = 0.5f, Music = 0.2f, Sfx = 0.4f, Voice = 0.6f };

            Assert.AreEqual(0.04f, levels.BusGain(AudioBus.Music), 1e-5f);
            Assert.AreEqual(0.16f, levels.BusGain(AudioBus.Sfx), 1e-5f);
            Assert.AreEqual(0.16f, levels.BusGain(AudioBus.Ui), 1e-5f);
            Assert.AreEqual(0.36f, levels.BusGain(AudioBus.Voice), 1e-5f);
            Assert.AreEqual(0.25f * 0.36f, levels.Gain(AudioBus.Voice), 1e-5f);
        }

        [Test]
        public void Reset_RestoresEveryDefault_AndUnmutes()
        {
            var levels = new AudioLevels { Master = 0.1f, Music = 0.9f, Sfx = 0f, Voice = 0.3f, Muted = true, MusicMuted = true };
            Assert.IsFalse(levels.IsDefault);

            levels.Reset();

            Assert.IsTrue(levels.IsDefault);
            Assert.IsTrue(levels.SameAs(new AudioLevels()));
        }

        [Test]
        public void IsDefault_NoticesEachValue()
        {
            Assert.IsFalse(new AudioLevels { Master = 0.5f }.IsDefault);
            Assert.IsFalse(new AudioLevels { Music = 0.5f }.IsDefault);
            Assert.IsFalse(new AudioLevels { Sfx = 0.5f }.IsDefault);
            Assert.IsFalse(new AudioLevels { Voice = 0.5f }.IsDefault);
            Assert.IsFalse(new AudioLevels { Muted = true }.IsDefault);
            Assert.IsFalse(new AudioLevels { MusicMuted = true }.IsDefault);
        }

        [Test]
        public void MigrateMusic_MovesAnUntouchedOldDefault()
        {
            Assert.AreEqual(AudioLevels.DefaultMusic, AudioLevels.MigrateMusic(2, 0.42f));
            Assert.AreEqual(AudioLevels.DefaultMusic, AudioLevels.MigrateMusic(1, 0.6f),
                "a save that skipped AU1f can still hold the first default");
            Assert.AreEqual(AudioLevels.DefaultMusic, AudioLevels.MigrateMusic(0, 0.6001f));
        }

        [Test]
        public void MigrateMusic_KeepsWhatThePlayerChose()
        {
            Assert.AreEqual(0.75f, AudioLevels.MigrateMusic(1, 0.75f));
            Assert.AreEqual(0.3f, AudioLevels.MigrateMusic(2, 0.3f));
            Assert.AreEqual(0.42f, AudioLevels.MigrateMusic(AudioLevels.Version, 0.42f),
                "42% saved under the current version was picked on purpose");
        }
    }
}