// Assets/_Project/Scripts/Unity/Audio/MusicPlaylist.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>
    /// Picks the next track when a cue has several (AUDIO.md AU4): a shuffle
    /// bag, so every track plays once before any plays twice, and never the
    /// same track twice in a row, across bags included.
    /// </summary>
    /// <remarks>
    /// Plain C#, on the caller's random, so it can be tested without Unity.
    /// The bag outlives a cue change: leaving a match and starting another
    /// carries on through the same bag, so back-to-back matches don't both
    /// open on the same track.
    /// </remarks>
    public sealed class MusicPlaylist
    {
        private readonly List<int> _bag = new List<int>();
        private int _count;
        private int _last = -1;

        /// <summary>The index of the track played last, or -1 before the first pick.</summary>
        public int Last => _last;

        /// <summary>The next track out of <paramref name="count"/>.</summary>
        public int Next(int count, Random random)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (random == null) throw new ArgumentNullException(nameof(random));

            if (count == 1)
            {
                _last = 0;
                return 0;
            }

            // A different set of tracks (a file added or missing) starts a fresh bag.
            if (count != _count)
            {
                _count = count;
                _bag.Clear();
                if (_last >= count) _last = -1;
            }

            if (_bag.Count == 0) Refill(random);

            int pick = _bag[_bag.Count - 1];
            _bag.RemoveAt(_bag.Count - 1);
            _last = pick;
            return pick;
        }

        private void Refill(Random random)
        {
            for (int i = 0; i < _count; i++) _bag.Add(i);

            // Fisher–Yates; picks come off the end of the list.
            for (int i = _bag.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }

            // The new bag must not open on the track the old one closed on.
            int top = _bag.Count - 1;
            if (_bag[top] == _last)
            {
                int swap = random.Next(top);
                (_bag[top], _bag[swap]) = (_bag[swap], _bag[top]);
            }
        }
    }
}
