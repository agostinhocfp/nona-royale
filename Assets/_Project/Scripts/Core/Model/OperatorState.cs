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
        /// Speed before auras and slows. The adopted band is 1.0–1.5 (ADR-0002
        /// Amendment 4; this remark once said 1.5 and 2.0, which was the
        /// original band). Not clamped here because the schema bounds live in
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

        /// <summary>
        /// Consecutive owner-upkeeps this operator has spent regen-eligible:
        /// in play, below half health, and not on a safe cell (§5.8). Only
        /// <c>GameEngine.EvaluateRegen</c> writes it.
        /// </summary>
        /// <remarks>
        /// An ineligible upkeep <b>resets</b> rather than pauses — the pity
        /// deploy's semantics, for the pity deploy's reason: a paused streak
        /// leaks stale history, and stepping onto a safe cell should cost the
        /// clock, not stop it. No reset is needed on neutralize: a yarded
        /// operator is out of play at its next evaluation, which resets it.
        /// </remarks>
        public int TurnsTowardRegen { get; private set; }

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
        /// Returns the operator to full health. Called on neutralize. (Its
        /// remark once said neutralize and Bouncer were the only healing
        /// routes; Nanite Infusion's splash and passive regen §5.11 have since
        /// joined them — those go through <see cref="Heal"/>.)
        /// </summary>
        public void RestoreHealth() => Health = MaxHealth;

        /// <summary>Restores health, never above maximum.</summary>
        public void Heal(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Health = Math.Min(MaxHealth, Health + amount);
        }

        public void RecordTurnTowardRegen() => TurnsTowardRegen++;

        public void ResetRegenProgress() => TurnsTowardRegen = 0;

        public override string ToString() =>
            $"{Name}({Owner}) hp {Health}/{MaxHealth} @ {Progress}";
    }
}