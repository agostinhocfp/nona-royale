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
        /// <summary>
        /// A <see cref="Range"/> that reaches anywhere on the board. Kian's Drone
        /// Strike.
        /// </summary>
        /// <remarks>
        /// <b>It needs no special case in targeting.</b> The largest possible
        /// track distance is half the circuit, so a comparison against this value
        /// is simply never exceeded — no branch, no sentinel check.
        ///
        /// <b>It does need one in arithmetic.</b> <c>MatchFactory.Retune</c> adds
        /// a bonus to every range for the harness; adding to this overflows to a
        /// negative range, which validates as illegal and silently makes the
        /// ability uncastable in every swept match. That guard is in Retune.
        ///
        /// The view should print it as a symbol rather than the number.
        /// </remarks>
        public const int UnlimitedRange = int.MaxValue;

        public AbilityDefinition(
            int id,
            string name,
            string description,
            int energyCost,
            int cooldownTurns,
            int range,
            IEnumerable<AbilityEffect> effects,
            AbilityTargeting targeting = AbilityTargeting.Operator,
            bool allowsSelfTarget = false)
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
            Targeting = targeting;
            AllowsSelfTarget = allowsSelfTarget;
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

        /// <summary>
        /// Range in track steps, either direction, or
        /// <see cref="UnlimitedRange"/>.
        /// </summary>
        public int Range { get; }

        /// <summary>What the player must pick before this can be cast.</summary>
        public AbilityTargeting Targeting { get; }

        /// <summary>
        /// Whether the caster is a legal target of its own cast (COMBAT_SYSTEMS
        /// §10, settled 2026-09-17).
        /// </summary>
        /// <remarks>
        /// <b>Opt-in, per ability, never a default.</b> Self-targeting resolves
        /// as a friendly cast under §10's mode rule, and blanket self-cast was
        /// rejected precisely because of what that rule does to All-In Mauling:
        /// its friendly mode is a heal, and Bouncer self-sustaining was never
        /// intended. So the ability declares it. Defensive abilities do —
        /// Javi's three and Lethe's Nano Cell, whose self-bubble pays the stun
        /// as its price — and nothing else on the roster does. The refusal and
        /// the target-list exclusion both live in <c>AbilityResolver</c>, not in
        /// the view (PRESENTATION §1), and a refused self-cast costs nothing,
        /// like every other refusal.
        /// </remarks>
        public bool AllowsSelfTarget { get; }

        /// <summary>
        /// True only for abilities aimed at one operator.
        /// </summary>
        /// <remarks>
        /// Kept as a computed property rather than replaced everywhere: every
        /// caller that asks this is asking exactly the question it still answers,
        /// and a cell-targeted ability is correctly not an operator-targeted one.
        /// A caller that needs to tell <i>cell</i> from <i>nothing</i> reads
        /// <see cref="Targeting"/>.
        /// </remarks>
        public bool RequiresTarget => Targeting == AbilityTargeting.Operator;

        /// <summary>True for an ability the player aims at a board cell.</summary>
        public bool RequiresCell => Targeting == AbilityTargeting.Cell;

        /// <summary>
        /// True when this reaches anywhere on the board. Callers that iterate or
        /// do arithmetic with <see cref="Range"/> must check this first —
        /// <see cref="UnlimitedRange"/> is <c>int.MaxValue</c>, so a range ring
        /// drawn from it is not large, it is fatal.
        /// </summary>
        public bool HasUnlimitedRange => Range == UnlimitedRange;

        /// <summary>
        /// True when any effect relocates somebody — a pull, a swap, a push, or
        /// a dash.
        /// </summary>
        /// <remarks>
        /// Exists for the safe-cell camping rule (§4.4, second amendment),
        /// which forbids aiming a placement at an ally behind a caster
        /// standing on a safe cell — the safe-cell taxi. Expressed as a
        /// question about effect kinds rather than a list of ability ids
        /// because content must never branch into logic (<see cref="Roster"/>):
        /// today this is exactly Velvet Rope, Translocation and Collision, and
        /// any future ally-mover inherits the rule without anyone remembering
        /// to add it.
        ///
        /// <b>A dash moves the caster, not the target — and it still counts.</b>
        /// The rule's shape is "no repositioning plays backwards out of a
        /// shelter", and a sheltered caster dashing to an ally behind itself is
        /// the same taxi with the seats exchanged: the camper spends the ability
        /// and the camp is still handed down the track.
        /// </remarks>
        public bool ContainsPlacement
        {
            get
            {
                foreach (var effect in Effects)
                {
                    if (effect.Kind == EffectKind.PullToCaster ||
                        effect.Kind == EffectKind.SwapWithCaster ||
                        effect.Kind == EffectKind.PushFromCaster ||
                        effect.Kind == EffectKind.DashToTarget)
                        return true;
                }

                return false;
            }
        }

        public IReadOnlyList<AbilityEffect> Effects { get; }

        public override string ToString()
        {
            string range = Range == UnlimitedRange ? "any" : Range.ToString();
            return $"{Name} ({EnergyCost}e, cd {CooldownTurns}, range {range})";
        }
    }
}