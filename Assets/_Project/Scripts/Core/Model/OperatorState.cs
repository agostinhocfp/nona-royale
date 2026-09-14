// Assets/_Project/Scripts/Core/Model/OperatorState.cs
using System;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Core.Model
{
    /// <summary>
    /// One operator's mutable state during a match. Deliberately dumb: it holds
    /// values and validates them, and decides nothing. Whether a move is legal
    /// is <c>MovementResolver</c>'s call; whether damage kills is
    /// <c>DamagePipeline</c>'s. This type is the thing they write their answers
    /// onto.
    /// </summary>
    /// <remarks>
    /// Separating "what is true" from "what decides" is the split that makes the
    /// rules testable. The old codebase put both in one <c>MonoBehaviour</c> and
    /// could not test either (ADR-0004).
    /// </remarks>
    public sealed class OperatorState
    {
        public OperatorState(int id, string name, PlayerColor owner, int maxHealth, double baseSpeedMultiplier)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An operator needs a name.", nameof(name));
            if (owner == PlayerColor.None)
                throw new ArgumentException("An operator must belong to a seat.", nameof(owner));
            if (maxHealth < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (baseSpeedMultiplier <= 0)
                throw new ArgumentOutOfRangeException(nameof(baseSpeedMultiplier));

            Id = id;
            Name = name;
            Owner = owner;
            MaxHealth = maxHealth;
            BaseSpeedMultiplier = baseSpeedMultiplier;

            Health = maxHealth;
            Progress = PathMap.YardProgress;
        }

        public int Id { get; }
        public string Name { get; }
        public PlayerColor Owner { get; }
        public int MaxHealth { get; }

        /// <summary>
        /// Speed before auras and slows. The alpha roster uses 1.5 and 2.0; the
        /// value is not clamped here because the schema bounds live in
        /// <c>GameConfig</c> and roster validation is its own concern.
        /// </summary>
        public double BaseSpeedMultiplier { get; }

        public int Health { get; private set; }

        /// <summary>
        /// Cells travelled from this operator's own start cell.
        /// <see cref="PathMap.YardProgress"/> while undeployed.
        /// </summary>
        public int Progress { get; private set; }

        public bool IsInYard => Progress == PathMap.YardProgress;

        public void MoveTo(int progress)
        {
            if (progress < PathMap.YardProgress)
                throw new ArgumentOutOfRangeException(nameof(progress),
                    $"Progress cannot fall below {PathMap.YardProgress} (the yard); was {progress}.");

            Progress = progress;
        }

        /// <summary>Clamped at zero: nothing in the rules reads "how far below zero" (COMBAT_SYSTEMS §1.2).</summary>
        public void SetHealth(int health) => Health = Math.Max(0, health);

        /// <summary>
        /// Returns the operator to full health. Called on neutralize, which is
        /// the only healing route besides Bouncer's ability.
        /// </summary>
        public void RestoreHealth() => Health = MaxHealth;

        /// <summary>Restores health, never above maximum. Bouncer's All-In Mauling on an ally.</summary>
        public void Heal(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Health = Math.Min(MaxHealth, Health + amount);
        }

        public override string ToString() =>
            $"{Name}({Owner}) hp {Health}/{MaxHealth} @ {Progress}";
    }
}