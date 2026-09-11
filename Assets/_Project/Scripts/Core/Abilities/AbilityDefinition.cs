// Assets/_Project/Scripts/Core/Abilities/AbilityDefinition.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// One ability, as data. Cost, cooldown, range and an ordered effect list —
    /// no behaviour, no subclass per operator.
    /// </summary>
    public sealed class AbilityDefinition
    {
        public AbilityDefinition(
            int id,
            string name,
            int energyCost,
            int cooldownTurns,
            int range,
            IEnumerable<AbilityEffect> effects,
            bool requiresTarget = true)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An ability needs a name.", nameof(name));
            if (energyCost < 0) throw new ArgumentOutOfRangeException(nameof(energyCost));
            if (cooldownTurns < 0) throw new ArgumentOutOfRangeException(nameof(cooldownTurns));
            if (range < 0) throw new ArgumentOutOfRangeException(nameof(range));
            if (effects == null) throw new ArgumentNullException(nameof(effects));

            Id = id;
            Name = name;
            EnergyCost = energyCost;
            CooldownTurns = cooldownTurns;
            Range = range;
            RequiresTarget = requiresTarget;
            Effects = new List<AbilityEffect>(effects);

            if (Effects.Count == 0)
                throw new ArgumentException($"{name} does nothing.", nameof(effects));
        }

        public int Id { get; }
        public string Name { get; }

        /// <summary>Cost from the player's shared pool. Passives are free and never resolved here.</summary>
        public int EnergyCost { get; }

        /// <summary>Turns of the caster's owner during which it is unusable after being used.</summary>
        public int CooldownTurns { get; }

        /// <summary>Range in track steps, either direction.</summary>
        public int Range { get; }

        /// <summary>False for self-origin area abilities, which need no chosen target.</summary>
        public bool RequiresTarget { get; }

        public IReadOnlyList<AbilityEffect> Effects { get; }

        public override string ToString() => $"{Name} ({EnergyCost}e, cd {CooldownTurns}, range {Range})";
    }
}