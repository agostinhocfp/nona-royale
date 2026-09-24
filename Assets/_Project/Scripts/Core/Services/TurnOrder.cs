// Assets/_Project/Scripts/Core/Services/TurnOrder.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Who plays first. The seats keep their order around the table; the
    /// match starts at a seat drawn from the seed, and play goes on from there.
    /// </summary>
    /// <remarks>
    /// <b>A rotation, not a shuffle.</b> Seats play in the direction of travel
    /// (Red, Blue, Green, Violet, which is <see cref="PlayerColor"/> order), and a
    /// crossed table alternates sides around it (ADR-0012). Rotating keeps both;
    /// only the seat that opens changes. The first seat of the returned order
    /// also opens every round, which is what <c>TurnStateMachine.Round</c> assumes.
    ///
    /// <b>Its own draw, not the match RNG.</b> The draw never touches the match's
    /// stream, so the dice after it are the dice the seed always gave, as with
    /// the draft's and the bots' streams (DRAFT.md decision 9, BOTS.md decision 4).
    ///
    /// <b>A hash, not <c>new SeededRandom(seed ^ salt)</c>.</b> System.Random's
    /// first draws for neighbouring seeds are correlated, and a rematch's seed
    /// can sit next to the last one. The avalanche below breaks that, so a
    /// neighbouring seed opens on any seat with equal odds.
    ///
    /// <b>Reproducible.</b> The same seed and seats always give the same order,
    /// and a recipe records seats in turn order, so a replay needs nothing extra.
    /// </remarks>
    public static class TurnOrder
    {
        /// <summary>
        /// XORed into the match seed before hashing, so this draw is not the
        /// draft's or the bots'. Any fixed value works; this one is the fifth
        /// SHA-256 initial hash word, picked only because the others are taken.
        /// </summary>
        public const int SeedSalt = 0x510E527F;

        /// <summary>
        /// The seats in turn order for <paramref name="seed"/>: the input order,
        /// rotated so that <see cref="FirstSeatIndex"/> opens. The input is not changed.
        /// </summary>
        public static PlayerColor[] Opening(IReadOnlyList<PlayerColor> seats, int seed)
        {
            if (seats == null) throw new ArgumentNullException(nameof(seats));
            if (seats.Count == 0) throw new ArgumentException("A match needs at least one seat.", nameof(seats));

            int first = FirstSeatIndex(seats.Count, seed);

            var order = new PlayerColor[seats.Count];
            for (int i = 0; i < order.Length; i++)
                order[i] = seats[(first + i) % seats.Count];

            return order;
        }

        /// <summary>The index, into seats in table order, of the seat that plays first.</summary>
        public static int FirstSeatIndex(int seatCount, int seed)
        {
            if (seatCount <= 0) throw new ArgumentOutOfRangeException(nameof(seatCount));

            // lowbias32 (Chris Wellons): every input bit reaches every output bit.
            uint x = unchecked((uint)(seed ^ SeedSalt));
            x ^= x >> 16;
            x = unchecked(x * 0x7FEB352Du);
            x ^= x >> 15;
            x = unchecked(x * 0x846CA68Bu);
            x ^= x >> 16;

            return (int)(x % (uint)seatCount);
        }
    }
}
