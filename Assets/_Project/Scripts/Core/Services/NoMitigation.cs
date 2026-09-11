// Assets/_Project/Scripts/Core/Services/NoMitigation.cs
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Mitigation that never mitigates. The default until statuses are
    /// implemented, and the right stand-in in tests that are about the pipeline
    /// rather than about evasion or shields.
    /// </summary>
    public sealed class NoMitigation : IDamageMitigation
    {
        public static NoMitigation Instance { get; } = new NoMitigation();

        public bool TryEvade(OperatorState target, IRandom random) => false;
        public bool TryAbsorb(OperatorState target) => false;
    }
}