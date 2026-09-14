// Assets/_Project/Scripts/Core/Rng/SeededRandom.cs
using System;

namespace NonaRoyale.Core.Rng
{
    /// <summary>
    /// The production <see cref="IRandom"/>. Deliberately thin: it exists to be
    /// seeded and to be swappable in tests, not to be clever.
    /// </summary>
    /// <remarks>
    /// <see cref="Seed"/> is kept so a match can report the seed it was played
    /// under. A bug reproduced from a seed is a bug you can write a test for.
    /// </remarks>
    public sealed class SeededRandom : IRandom
    {
        private readonly Random _random;

        public SeededRandom(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Seed { get; }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                    $"Range must be non-empty; got [{minInclusive}, {maxExclusive}).");

            return _random.Next(minInclusive, maxExclusive);
        }

        public double NextDouble() => _random.NextDouble();
    }
}