// Assets/_Project/Scripts/Core/Draft/DraftConfig.cs
using System;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Draft
{
    /// <summary>
    /// The draft's tunables (DRAFT.md decisions 1, 6 and 9).
    /// </summary>
    /// <remarks>
    /// <b>The clocks are seconds of real time.</b> The draft runs before any
    /// match exists, so there is no turn to count in. The view feeds the clock
    /// unscaled time, and stops feeding it while a card covers the screen.
    ///
    /// <b>30 s is flat at every seat count.</b> Four seats share one pointer for
    /// twelve picks, so at a full table the clock may run out often. The timeout
    /// fill covers that. It is a number to tune after a playtest, which is why
    /// it lives here.
    /// </remarks>
    public sealed class DraftConfig
    {
        /// <summary>
        /// XORed into the match seed to seed the draft's own RNG. Any fixed
        /// value works; this one is the 32-bit golden-ratio constant, chosen
        /// only so it is not a round number that a match seed might share.
        /// </summary>
        public const int DefaultSeedSalt = unchecked((int)0x9E3779B9);

        public DraftConfig(
            double allPickSeconds = 30.0,
            double snakePickSeconds = 10.0,
            int seedSalt = DefaultSeedSalt)
        {
            if (!(allPickSeconds > 0.0)) throw new ArgumentOutOfRangeException(nameof(allPickSeconds));
            if (!(snakePickSeconds > 0.0)) throw new ArgumentOutOfRangeException(nameof(snakePickSeconds));

            AllPickSeconds = allPickSeconds;
            SnakePickSeconds = snakePickSeconds;
            SeedSalt = seedSalt;
        }

        /// <summary>ALL PICK: one clock for the whole draft.</summary>
        public double AllPickSeconds { get; }

        /// <summary>SNAKE: the clock for each pick, restarted after every pick.</summary>
        public double SnakePickSeconds { get; }

        /// <summary>See <see cref="DefaultSeedSalt"/>.</summary>
        public int SeedSalt { get; }

        /// <summary>
        /// The draft's RNG for a match seed. It is kept apart from the match's
        /// RNG so the dice do not depend on how many picks were random.
        /// </summary>
        public IRandom RandomFor(int matchSeed) => new SeededRandom(matchSeed ^ SeedSalt);

        public double SecondsFor(DraftMode mode) =>
            mode == DraftMode.Snake ? SnakePickSeconds : AllPickSeconds;

        public static DraftConfig Default => new DraftConfig();
    }
}