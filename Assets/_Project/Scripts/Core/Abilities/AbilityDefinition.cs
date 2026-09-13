// Assets/_Project/Scripts/Core/Abilities/AbilityDefinition.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// One ability, as data. Cost, cooldown, range, an ordered effect list, and
    /// the line a player reads before pressing it — no behaviour, no subclass
    /// per operator.
    /// </summary>
    public sealed class AbilityDefinition
    {
        public AbilityDefinition(
            int id,
            string name,
            string description,
            int energyCost,
            int cooldownTurns,
            int range,
            IEnumerable<AbilityEffect> effects,
            bool requiresTarget = true)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An ability needs a name.", nameof(name));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException(
                    $"{name} needs a description — a player has to know what it does before spending on it.",
                    nameof(description));
            if (energyCost < 0) throw new ArgumentOutOfRangeException(nameof(energyCost));
            if (cooldownTurns < 0) throw new ArgumentOutOfRangeException(nameof(cooldownTurns));
            if (range < 0) throw new ArgumentOutOfRangeException(nameof(range));
            if (effects == null) throw new ArgumentNullException(nameof(effects));

            Id = id;
            Name = name;
            Description = description;
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

        /// <summary>
        /// What this does, for a player deciding whether to press it. One or two
        /// sentences.
        /// </summary>
        /// <remarks>
        /// <b>No numbers, ever.</b> Cost, range, cooldown and damage all live on
        /// this object already and the view reads them from here — a figure
        /// repeated in prose is a second copy of a value three lines above it,
        /// and it will be wrong the first time anyone tunes the ability. That is
        /// not hypothetical: Slow's magnitude, All-In Mauling's range, the speed
        /// band and the whole of <c>OPERATORS.md</c> have each drifted from the
        /// values they described.
        ///
        /// <b>Required, not optional.</b> A nullable description is one half the
        /// roster will not have. The constructor throwing is the only thing that
        /// makes a new operator arrive with one.
        ///
        /// <b>Not the XML doc comments.</b> Those explain the design to the next
        /// developer — why the self-damage bypasses the pipeline, what was walked
        /// back and from where. This is for someone choosing between two buttons.
        /// Merging the two audiences would serve neither.
        /// </remarks>
        public string Description { get; }

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