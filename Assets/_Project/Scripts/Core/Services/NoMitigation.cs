// Assets/_Project/Scripts/Core/Services/NoMitigation.cs
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Mitigation that never mitigates. The right stand-in for tests that are
    /// about the pipeline rather than about evasion or shields.
    /// </summary>
    /// <remarks>
    /// <b>It is no longer a placeholder.</b> It was written as the default until
    /// statuses existed; <c>StatusRegistry</c> has implemented
    /// <see cref="IDamageMitigation"/> for some time, and this survives as a
    /// deliberate no-op rather than as scaffolding waiting to be replaced.
    ///
    /// <b>Zero is the whole contract.</b> <see cref="AbsorbFrom"/> returns what
    /// a pool actually ate, so nothing absorbed is 0 — not the amount offered.
    /// Returning the amount would mean this type silently negated every Normal
    /// instance in the game.
    /// </remarks>
    public sealed class NoMitigation : IDamageMitigation
    {
        public static NoMitigation Instance { get; } = new NoMitigation();

        public bool TryEvade(OperatorState target, IRandom random) => false;

        public int AbsorbFrom(OperatorState target, int amount) => 0;

        public bool BlocksTech(OperatorState target) => false;
    }
}