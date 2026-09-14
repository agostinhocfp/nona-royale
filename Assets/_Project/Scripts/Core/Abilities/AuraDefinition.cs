// Assets/_Project/Scripts/Core/Abilities/AuraDefinition.cs
using System;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// A permanently-on effect an operator projects onto enemies near it.
    /// Bouncer's Intimidating Presence is the only one on the alpha roster.
    /// </summary>
    /// <remarks>
    /// <b>An aura is not a status.</b> It has no duration, no application event
    /// and nothing to expire — it is simply true while both operators stand
    /// within range, and it is evaluated at the moment movement is calculated
    /// (COMBAT_SYSTEMS §10.1). Modelling it as a status would mean applying and
    /// removing it every time anyone moved.
    ///
    /// It also survives stun, because a passive is who an operator is rather
    /// than what it does.
    /// </remarks>
    public sealed class AuraDefinition
    {
        public AuraDefinition(string name, int radius, double speedModifier)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An aura needs a name.", nameof(name));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));

            Name = name;
            Radius = radius;
            SpeedModifier = speedModifier;
        }

        public string Name { get; }

        /// <summary>Track steps in either direction.</summary>
        public int Radius { get; }

        /// <summary>Signed change to an affected enemy's speed multiplier.</summary>
        public double SpeedModifier { get; }

        public override string ToString() => $"{Name} (r{Radius}, {SpeedModifier:+0.0;-0.0})";
    }
}