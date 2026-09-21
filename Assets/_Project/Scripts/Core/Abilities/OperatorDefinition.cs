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
    /// <b>Speed is the base only.</b> Until 2026-09-17 Kurbyn's Evasive
    /// Protocol added its bonus as the passive's magnitude, which is what made
    /// it reach the engine at all (ADR-0002 Amendment 5) — and reading
    /// <see cref="BaseSpeed"/> while expecting his effective speed was the bug
    /// that went unnoticed for weeks. The bonus is gone: his mobility is flat
    /// haste cells now, so the base is the whole speed again.
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
            string passiveName = null,
            StatusKind? passive2 = null,
            double passive2Magnitude = 0.0,
            int? hasteCellCap = null,
            string passiveDescription = null)
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
            Passive2 = passive2;
            Passive2Magnitude = passive2Magnitude;
            HasteCellCap = hasteCellCap;
            PassiveDescription = passiveDescription;
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

        /// <summary>The passive's magnitude — a speed bonus for Evasive Protocol, before 2026-09-17.</summary>
        public double PassiveMagnitude { get; }

        /// <summary>
        /// A second permanent status granted at match start, or null. The slot
        /// exists because Kurbyn carries two (2026-09-17): his evasion and his
        /// haste are one fiction, Evasive Protocol, but two statuses.
        /// </summary>
        public StatusKind? Passive2 { get; }

        /// <summary>The second passive's magnitude.</summary>
        public double Passive2Magnitude { get; }

        /// <summary>
        /// A per-operator override of <c>CombatConfig.HasteBonusCellCap</c>
        /// (§5.9), or null for the global cap. Kurbyn's haste is capped at 2
        /// (2026-09-17, designer) where the roster's is 3.
        /// </summary>
        public int? HasteCellCap { get; }

        /// <summary>
        /// The passive's name when it fills a kit slot (Revú's Equilibrium), or
        /// null when it is an attribute rather than a named ability (Kurbyn's
        /// evasion, Lethe's haste, Sanity's burden). The draft card shows a
        /// named passive in the line after the abilities.
        /// </summary>
        public string PassiveName { get; }

        /// <summary>
        /// What the passive is, for a player reading the kit: the flavour line
        /// under its generated rules line, as <see cref="AbilityDefinition.Description"/>
        /// is for an ability. One entry covers a named two-status passive
        /// (Kurbyn's Evasive Protocol).
        /// </summary>
        /// <remarks>
        /// <b>No numbers, ever</b>, for the same reason as an ability's: the
        /// rules line already carries them from the data (OPERATOR_GUIDE.md D2).
        ///
        /// <b>Optional here, required of the roster.</b> Test and sweep squads
        /// build operators with passives and no reader, so the constructor
        /// accepts null; <c>KitTraitTests</c> fails any operator in
        /// <c>Roster.All</c> whose passive has none.
        /// </remarks>
        public string PassiveDescription { get; }

        public override string ToString() =>
            $"{Name} (hp {MaxHealth}, speed {BaseSpeed:0.0}, {Abilities.Count} abilities)";
    }
}