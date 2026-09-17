// Assets/_Project/Scripts/Core/Abilities/OperatorDefinition.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// One operator, as data: stats, kit, and whatever passive it carries.
    /// </summary>
    /// <remarks>
    /// <b>Why this exists.</b> Operator files are static classes, and a static
    /// class cannot implement an interface — so there was no way to hold three
    /// arbitrary operators in a list, and <c>MatchFactory</c> hardcoded Bouncer,
    /// Syla and Kurbyn by name. Drafting a squad from a pool needs a uniform
    /// shape, and this is it.
    ///
    /// <b>Passives are declared here, not applied here.</b> An aura is evaluated
    /// on demand by <c>AuraRules</c>; a permanent status is granted once at
    /// composition by <c>MatchFactory</c>. Both are properties of the operator
    /// and neither is an ability — Intimidating Presence and Evasive Protocol
    /// are never "used" (§10.1, §10.3).
    ///
    /// <b>Speed is the base only.</b> Kurbyn's Evasive Protocol adds its bonus
    /// as the passive's magnitude, which is what makes it reach the engine at
    /// all (ADR-0002 Amendment 5). Reading <see cref="BaseSpeed"/> and expecting
    /// his effective speed is the bug that went unnoticed for weeks.
    /// </remarks>
    public sealed class OperatorDefinition
    {
        public OperatorDefinition(
            string name,
            int maxHealth,
            double baseSpeed,
            IReadOnlyList<AbilityDefinition> abilities,
            AuraDefinition aura = null,
            StatusKind? passive = null,
            double passiveMagnitude = 0.0,
            string passiveName = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An operator needs a name.", nameof(name));
            if (maxHealth < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (baseSpeed <= 0)
                throw new ArgumentOutOfRangeException(nameof(baseSpeed));
            if (abilities == null)
                throw new ArgumentNullException(nameof(abilities));

            Name = name;
            MaxHealth = maxHealth;
            BaseSpeed = baseSpeed;
            Abilities = abilities;
            Aura = aura;
            Passive = passive;
            PassiveMagnitude = passiveMagnitude;
            PassiveName = passiveName;
        }

        public string Name { get; }
        public int MaxHealth { get; }

        /// <summary>Before any passive bonus. See the remarks above.</summary>
        public double BaseSpeed { get; }

        public IReadOnlyList<AbilityDefinition> Abilities { get; }

        /// <summary>The aura this operator projects onto nearby enemies, or null.</summary>
        public AuraDefinition Aura { get; }

        /// <summary>A permanent status granted at match start, or null.</summary>
        public StatusKind? Passive { get; }

        /// <summary>The passive's magnitude — a speed bonus for Evasive Protocol.</summary>
        public double PassiveMagnitude { get; }

        /// <summary>
        /// The passive's name when it fills a kit slot (Revú's Equilibrium), or
        /// null when it is an attribute rather than a named ability (Kurbyn's
        /// evasion, Lethe's haste, Sanity's burden). The draft card shows a
        /// named passive in the line after the abilities.
        /// </summary>
        public string PassiveName { get; }

        public override string ToString() =>
            $"{Name} (hp {MaxHealth}, speed {BaseSpeed:0.0}, {Abilities.Count} abilities)";
    }
}