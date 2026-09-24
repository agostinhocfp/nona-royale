// Assets/_Project/Scripts/Core/Abilities/AuraDefinition.cs
using System;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>Which side an aura reaches.</summary>
    /// <remarks>
    /// <b>Values are explicit and append-only</b>, like every enum the core
    /// may one day serialise.
    /// </remarks>
    public enum AuraSide
    {
        /// <summary>Operators of every other seat. Bouncer's Intimidating Presence.</summary>
        Enemies = 0,

        /// <summary>
        /// The projecting operator's own squad, never the projector itself.
        /// Lethe's Catalyst.
        /// </summary>
        Allies = 1
    }

    /// <summary>
    /// A permanently-on effect an operator projects onto operators near it.
    /// Bouncer's Intimidating Presence slows enemies; Lethe's Catalyst hastens
    /// allies.
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
    ///
    /// <b>Two payloads, and they travel on different channels (2026-09-17).</b>
    /// <see cref="SpeedModifier"/> is a signed speed delta and joins the speed
    /// channel. <see cref="GrantsHaste"/> makes the recipient count as
    /// Hastened for the move being computed — flat extra cells, never speed
    /// (§5.9). Catalyst uses haste rather than a speed bonus precisely so it
    /// cannot push anybody past the 1.5 ceiling, which the speed channel does
    /// not enforce.
    /// </remarks>
    public sealed class AuraDefinition
    {
        public AuraDefinition(
            string name,
            int radius,
            double speedModifier,
            AuraSide side = AuraSide.Enemies,
            bool grantsHaste = false,
            string description = null,
            int trail = 0)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An aura needs a name.", nameof(name));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            if (trail < 0) throw new ArgumentOutOfRangeException(nameof(trail));
            if (speedModifier == 0.0 && !grantsHaste)
                throw new ArgumentException("An aura that neither changes speed nor grants haste does nothing.");

            Name = name;
            Radius = radius;
            Trail = trail;
            SpeedModifier = speedModifier;
            Side = side;
            GrantsHaste = grantsHaste;
            Description = description;
        }

        public string Name { get; }

        /// <summary>Track steps in either direction.</summary>
        public int Radius { get; }

        /// <summary>
        /// Track steps <b>behind</b> the projector, along the way everyone
        /// travels, that it also reaches — a slipstream (2026-09-24). Zero for
        /// none. Lethe's Catalyst: allies near her or following her.
        /// </summary>
        /// <remarks>
        /// Behind, not ahead, because a race strings a squad out behind its
        /// runner: measured, allies stood within two cells of her on only 27%
        /// of her turns (<c>LETHE_ANALYSIS.md</c>).
        /// </remarks>
        public int Trail { get; }

        /// <summary>Signed change to an affected operator's speed multiplier. Zero for none.</summary>
        public double SpeedModifier { get; }

        /// <summary>Who it reaches.</summary>
        public AuraSide Side { get; }

        /// <summary>Whether an operator it reaches counts as Hastened (§5.9).</summary>
        public bool GrantsHaste { get; }

        /// <summary>
        /// What the aura is, for a player reading the kit. Number-free, like an
        /// ability's description; the radius and the effect are the rules
        /// line's. Optional here, required of the roster by <c>KitTraitTests</c>.
        /// </summary>
        public string Description { get; }

        public override string ToString()
        {
            string payload = GrantsHaste ? "haste" : $"{SpeedModifier:+0.0;-0.0}";
            string side = Side == AuraSide.Allies ? "allies" : "enemies";
            string reach = Trail > 0 ? $"r{Radius}+{Trail} behind" : $"r{Radius}";
            return $"{Name} ({reach}, {payload}, {side})";
        }
    }
}