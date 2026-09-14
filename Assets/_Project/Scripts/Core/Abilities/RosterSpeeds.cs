// Assets/_Project/Scripts/Core/Abilities/RosterSpeeds.cs
using System;

// Aliased because this type has properties named Bouncer and Syla. A simple
// name in a member binds to the enclosing type's member before it ever reaches
// a type of the same name, so `Bouncer.Speed` would not compile here.
using BouncerKit = NonaRoyale.Core.Abilities.Bouncer;
using SylaKit = NonaRoyale.Core.Abilities.Syla;
using KurbynKit = NonaRoyale.Core.Abilities.Kurbyn;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// The alpha three's speed multipliers, as config rather than constants
    /// baked into match construction.
    /// </summary>
    /// <remarks>
    /// Exists so the simulation harness can sweep bands without editing the
    /// roster, and so a future balance pass changes data rather than code. The
    /// defaults are the adopted band (ADR-0002 Amendment 4); anything else is a
    /// measurement, not a shipping configuration.
    ///
    /// <b>Three named fields, which will not survive a fifth operator.</b> This
    /// type predates one-file-per-operator and still assumes the roster is
    /// exactly Bouncer, Syla and Kurbyn. Mimi has a speed and no way to reach it
    /// from here. Replacing this with a per-operator lookup is the same change
    /// as squad selection and belongs with it — which is also why it stays in
    /// this folder rather than moving to <c>Core/Config</c> where it otherwise
    /// belongs. There is no point rehoming a type that is about to be rewritten.
    ///
    /// <b><see cref="ToString"/> reports the effective band, not the base.</b>
    /// It adds Kurbyn's passive bonus, and every sweep label in ADR-0002
    /// Amendments 2 through 4 came from here — which is why those labels
    /// overstated him by 0.5 for as long as the passive went unwired.
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
            new RosterSpeeds(BouncerKit.Speed, SylaKit.Speed, KurbynKit.BaseSpeed);

        public override string ToString() =>
            $"{Bouncer:0.0}/{Syla:0.0}/{KurbynBase + KurbynKit.PassiveSpeedBonus:0.0}";
    }
}