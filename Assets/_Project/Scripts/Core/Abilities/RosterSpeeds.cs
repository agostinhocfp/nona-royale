// Assets/_Project/Scripts/Core/Abilities/RosterSpeeds.cs
using System;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// The alpha roster's speed multipliers, as config rather than constants
    /// baked into match construction.
    /// </summary>
    /// <remarks>
    /// Exists so the simulation harness can sweep bands without editing the
    /// roster, and so a future balance pass changes data rather than code. The
    /// defaults are the adopted band (ADR-0002 Amendment 2); anything else is a
    /// measurement, not a shipping configuration.
    /// </remarks>
    public sealed class RosterSpeeds
    {
        public RosterSpeeds(double bouncer, double syla, double kurbynBase)
        {
            if (bouncer <= 0) throw new ArgumentOutOfRangeException(nameof(bouncer));
            if (syla <= 0) throw new ArgumentOutOfRangeException(nameof(syla));
            if (kurbynBase <= 0) throw new ArgumentOutOfRangeException(nameof(kurbynBase));

            Bouncer = bouncer;
            Syla = syla;
            KurbynBase = kurbynBase;
        }

        public double Bouncer { get; }
        public double Syla { get; }

        /// <summary>Before Evasive Protocol, which adds its bonus on top.</summary>
        public double KurbynBase { get; }

        public static RosterSpeeds Default =>
            new RosterSpeeds(AlphaRoster.BouncerSpeed, AlphaRoster.SylaSpeed, AlphaRoster.KurbynBaseSpeed);

        public override string ToString() =>
            $"{Bouncer:0.0}/{Syla:0.0}/{KurbynBase + AlphaRoster.KurbynPassiveSpeedBonus:0.0}";
    }
}