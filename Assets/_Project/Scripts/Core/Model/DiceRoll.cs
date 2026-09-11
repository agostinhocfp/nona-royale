// Assets/_Project/Scripts/Core/Model/DiceRoll.cs
using System;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Model
{
    /// <summary>
    /// One roll of the dice. A value type so a test can state a roll outright
    /// (<c>new DiceRoll(6, 3)</c>) instead of hunting for a seed that produces
    /// it — most movement and deploy rules are about *which faces came up*, not
    /// about randomness at all.
    /// </summary>
    public readonly struct DiceRoll : IEquatable<DiceRoll>
    {
        public int First { get; }
        public int Second { get; }

        public DiceRoll(int first, int second)
        {
            if (first < 1) throw new ArgumentOutOfRangeException(nameof(first));
            if (second < 1) throw new ArgumentOutOfRangeException(nameof(second));

            First = first;
            Second = second;
        }

        public int Total => First + Second;

        /// <summary>Doubles grant an extra movement roll, never extra energy (COMBAT_SYSTEMS §3.1).</summary>
        public bool IsDouble => First == Second;

        public bool Contains(int face) => First == face || Second == face;

        /// <summary>How many dice show this face. Two on a double, which is what deploys two operators.</summary>
        public int CountOf(int face) =>
            (First == face ? 1 : 0) + (Second == face ? 1 : 0);

        /// <summary>The other die, given one that has been consumed by a deploy.</summary>
        public int OtherThan(int face)
        {
            if (First == face) return Second;
            if (Second == face) return First;

            throw new ArgumentException($"Roll {this} does not contain a {face}.", nameof(face));
        }

        public static DiceRoll Roll(IRandom random, GameConfig config)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (config == null) throw new ArgumentNullException(nameof(config));

            return new DiceRoll(
                random.NextInt(1, config.DiceSides + 1),
                random.NextInt(1, config.DiceSides + 1));
        }

        public bool Equals(DiceRoll other) => First == other.First && Second == other.Second;
        public override bool Equals(object obj) => obj is DiceRoll other && Equals(other);
        public override int GetHashCode() { unchecked { return (First * 397) ^ Second; } }
        public override string ToString() => $"[{First},{Second}]";
    }
}