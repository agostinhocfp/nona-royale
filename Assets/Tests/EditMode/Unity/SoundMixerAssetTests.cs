// Assets/Tests/EditMode/Unity/SoundMixerAssetTests.cs
using NonaRoyale.Unity.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;

namespace NonaRoyale.Unity.Tests.Audio
{
    /// <summary>
    /// The mixer asset has what <see cref="SoundMixer"/> looks for (AU1f).
    /// Needs the editor: it loads <c>Resources/Audio/Mixer.mixer</c>.
    /// </summary>
    [TestFixture]
    public class SoundMixerAssetTests
    {
        private AudioMixer _mixer;

        [SetUp]
        public void SetUp()
        {
            _mixer = Resources.Load<AudioMixer>(SoundMixer.ResourcePath);
        }

        [Test]
        public void TheMixer_LoadsByName()
        {
            Assert.IsNotNull(_mixer, $"No AudioMixer at Resources/{SoundMixer.ResourcePath}");
        }

        [Test]
        public void EveryBus_HasItsGroup()
        {
            Assume.That(_mixer, Is.Not.Null);

            foreach (var path in SoundMixer.GroupPaths)
                Assert.IsNotNull(SoundMixer.Find(_mixer, path), path);
        }

        [Test]
        public void Interface_SitsUnderEffects()
        {
            Assume.That(_mixer, Is.Not.Null);

            var matches = _mixer.FindMatchingGroups(SoundMixer.EffectsPath);
            bool found = false;
            foreach (var group in matches) found |= group.name == "Interface";
            Assert.IsTrue(found, "Interface should be a child of Effects, so clicks follow the Effects slider");
        }

        [Test]
        public void EveryFader_IsExposed()
        {
            Assume.That(_mixer, Is.Not.Null);

            foreach (var name in SoundMixer.Parameters)
                Assert.IsTrue(_mixer.GetFloat(name, out _), $"{name} is not exposed");
        }
    }
}