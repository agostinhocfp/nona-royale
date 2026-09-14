// Assets/_Project/Scripts/Core/Rng/IRandom.cs
namespace NonaRoyale.Core.Rng
{
    /// <summary>
    /// Every source of randomness in the core arrives through this interface.
    /// Nothing in Core ever calls <c>UnityEngine.Random</c> (it cannot — the
    /// assembly has no engine reference) and nothing constructs a
    /// <c>System.Random</c> on the spot.
    ///
    /// The point is reproducibility: the same seed plus the same commands must
    /// produce the same match, every time. That is what makes a rule testable
    /// without mocking, and what will let an online server later re-run a
    /// client's turn to verify it (ADR-0004).
    /// </summary>
    public interface IRandom
    {
        /// <summary>Uniform integer in <c>[minInclusive, maxExclusive)</c>.</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Uniform double in <c>[0.0, 1.0)</c>. Used for evasion rolls.</summary>
        double NextDouble();
    }
}