// Assets/Tests/EditMode/Unity/MusicPlaylistTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Unity.Audio;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.Audio
{
    [TestFixture]
    public class MusicPlaylistTests
    {
        [Test]
        public void NeverTheSameTrackTwiceInARow()
        {
            var playlist = new MusicPlaylist();
            var random = new Random(7);
            int previous = -1;

            for (int i = 0; i < 2000; i++)
            {
                int pick = playlist.Next(5, random);
                Assert.That(pick, Is.Not.EqualTo(previous), $"pick {i}");
                previous = pick;
            }
        }

        [Test]
        public void EveryTrackPlays_BeforeAnyPlaysTwice()
        {
            var playlist = new MusicPlaylist();
            var random = new Random(11);

            for (int bag = 0; bag < 50; bag++)
            {
                var picks = Enumerable.Range(0, 5).Select(_ => playlist.Next(5, random)).ToList();
                CollectionAssert.AreEquivalent(Enumerable.Range(0, 5), picks, $"bag {bag}");
            }
        }

        [Test]
        public void TwoTracks_Alternate()
        {
            var playlist = new MusicPlaylist();
            var random = new Random(3);
            var picks = Enumerable.Range(0, 20).Select(_ => playlist.Next(2, random)).ToList();

            for (int i = 1; i < picks.Count; i++) Assert.That(picks[i], Is.Not.EqualTo(picks[i - 1]));
        }

        [Test]
        public void OneTrack_IsAlwaysThatTrack()
        {
            var playlist = new MusicPlaylist();
            var random = new Random(1);

            for (int i = 0; i < 5; i++) Assert.That(playlist.Next(1, random), Is.EqualTo(0));
        }

        [Test]
        public void ACountChange_StartsAFreshBag_InRange()
        {
            var playlist = new MusicPlaylist();
            var random = new Random(5);

            for (int i = 0; i < 3; i++) playlist.Next(5, random);

            var seen = new HashSet<int>();
            for (int i = 0; i < 3; i++) seen.Add(playlist.Next(3, random));

            Assert.That(seen, Is.EquivalentTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void BadArguments_AreRejected()
        {
            var playlist = new MusicPlaylist();
            Assert.Throws<ArgumentOutOfRangeException>(() => playlist.Next(0, new Random(1)));
            Assert.Throws<ArgumentNullException>(() => playlist.Next(3, null));
        }
    }
}
