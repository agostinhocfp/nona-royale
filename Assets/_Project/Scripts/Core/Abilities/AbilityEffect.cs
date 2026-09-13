// Assets/_Project/Scripts/Core/Abilities/AbilityEffect.cs
using System;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// One thing an ability does. An ability is an ordered list of these, and
    /// order matters — Miracle Pull's execute check has to run before its damage
    /// so the threshold reads health at cast time.
    /// </summary>
    /// <remarks>
    /// A flat struct with factory methods rather than a class hierarchy: the
    /// whole point of going data-driven is that operators four through nine are
    /// content, and content is easier to write, diff and eventually load from a
    /// ScriptableObject when it is plain data.
    /// </remarks>
    public readonly struct AbilityEffect
    {
        private AbilityEffect(
            EffectKind kind, EffectScope scope, EffectAudience audience,
            int amount, DamageType damageType, int radius,
            StatusKind status, int duration, int stacks, double magnitude,
            int bonusIfBleeding, int executeNumerator, int executeDenominator)
        {
            Kind = kind;
            Scope = scope;
            Audience = audience;
            Amount = amount;
            DamageType = damageType;
            Radius = radius;
            Status = status;
            Duration = duration;
            Stacks = stacks;
            Magnitude = magnitude;
            BonusIfBleeding = bonusIfBleeding;
            ExecuteNumerator = executeNumerator;
            ExecuteDenominator = executeDenominator;
        }

        public EffectKind Kind { get; }
        public EffectScope Scope { get; }
        public EffectAudience Audience { get; }

        /// <summary>Damage or healing amount.</summary>
        public int Amount { get; }

        public DamageType DamageType { get; }

        /// <summary>Radius for the area scopes, in track steps. "Within N" covers 2N+1 cells.</summary>
        public int Radius { get; }

        public StatusKind Status { get; }
        public int Duration { get; }
        public int Stacks { get; }
        public double Magnitude { get; }

        /// <summary>Extra damage when the target already carries a bleed stack. Syla's From the Hip.</summary>
        public int BonusIfBleeding { get; }

        /// <summary>Execute threshold as a fraction — 1/2 for "below 50%".</summary>
        public int ExecuteNumerator { get; }
        public int ExecuteDenominator { get; }

        /// <summary>A copy with a different radius. For balance sweeps only.</summary>
        public AbilityEffect WithRadius(int radius) =>
            new AbilityEffect(Kind, Scope, Audience, Amount, DamageType, radius,
                Status, Duration, Stacks, Magnitude, BonusIfBleeding,
                ExecuteNumerator, ExecuteDenominator);

        public static AbilityEffect Damage(
            EffectScope scope, int amount, DamageType type,
            EffectAudience audience = EffectAudience.EnemyOnly,
            int radius = 0, int bonusIfBleeding = 0)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            return new AbilityEffect(EffectKind.Damage, scope, audience, amount, type, radius,
                default, 0, 0, 0, bonusIfBleeding, 0, 0);
        }

        /// <summary>
        /// Restore health, capped at maximum.
        /// </summary>
        /// <remarks>
        /// <paramref name="radius"/> is only read by the area scopes. A heal
        /// that reaches an area is still audience-scoped by the <i>cast mode</i>,
        /// not by who receives it — Nanite Infusion's splash heal is declared
        /// <see cref="EffectAudience.EnemyOnly"/> because it belongs to the
        /// hostile cast, even though every operator it touches is friendly.
        /// </remarks>
        public static AbilityEffect Heal(
            EffectScope scope, int amount,
            EffectAudience audience = EffectAudience.AllyOnly,
            int radius = 0)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            return new AbilityEffect(EffectKind.Heal, scope, audience, amount, default, radius,
                default, 0, 0, 0, 0, 0, 0);
        }

        /// <summary>
        /// Strip every applied status from the target. Passives survive.
        /// </summary>
        public static AbilityEffect Cleanse(EffectAudience audience = EffectAudience.AllyOnly) =>
            new AbilityEffect(EffectKind.RemoveStatuses, EffectScope.PrimaryTarget, audience,
                0, default, 0, default, 0, 0, 0, 0, 0, 0);

        public static AbilityEffect Status_(
            EffectScope scope, StatusKind status, int duration,
            EffectAudience audience = EffectAudience.EnemyOnly,
            int radius = 0, int stacks = 1, double magnitude = 0.0)
        {
            if (duration < 1) throw new ArgumentOutOfRangeException(nameof(duration));

            return new AbilityEffect(EffectKind.ApplyStatus, scope, audience, 0, default, radius,
                status, duration, stacks, magnitude, 0, 0, 0);
        }

        public static AbilityEffect Pull(EffectAudience audience = EffectAudience.Any) =>
            new AbilityEffect(EffectKind.PullToCaster, EffectScope.PrimaryTarget, audience,
                0, default, 0, default, 0, 0, 0, 0, 0, 0);

        /// <summary>
        /// Caster and target exchange board cells. Placement, not movement — it
        /// collides with nothing and triggers nothing (§7.4).
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="EffectAudience.Any"/>: Mimi's Translocation
        /// swaps with a friend or a foe, and the two modes are the same effect
        /// rather than two branches of one ability.
        ///
        /// A swap can be refused after targeting has already passed — see
        /// <see cref="EffectKind.SwapWithCaster"/>.
        /// </remarks>
        public static AbilityEffect Swap(EffectAudience audience = EffectAudience.Any) =>
            new AbilityEffect(EffectKind.SwapWithCaster, EffectScope.PrimaryTarget, audience,
                0, default, 0, default, 0, 0, 0, 0, 0, 0);

        /// <summary>
        /// Neutralize if health is below <paramref name="numerator"/>/<paramref name="denominator"/>
        /// of maximum at cast time; otherwise deal <paramref name="fallbackAmount"/> instead.
        /// </summary>
        public static AbilityEffect Execute(
            int numerator, int denominator, int fallbackAmount, DamageType fallbackType)
        {
            if (denominator < 1) throw new ArgumentOutOfRangeException(nameof(denominator));

            return new AbilityEffect(EffectKind.Execute, EffectScope.PrimaryTarget, EffectAudience.EnemyOnly,
                fallbackAmount, fallbackType, 0, default, 0, 0, 0, 0, numerator, denominator);
        }
    }
}