// Assets/_Project/Scripts/Core/Services/EffectOutcome.cs
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    public enum EffectOutcomeKind
    {
        Damaged = 0,
        Healed = 1,
        StatusApplied = 2,
        Pulled = 3,
        Executed = 4
    }

    /// <summary>
    /// One thing an ability actually did, in the order it happened. The view
    /// turns this list into events and animations; the tests read it to assert
    /// on behaviour rather than on internal state.
    /// </summary>
    public readonly struct EffectOutcome
    {
        private EffectOutcome(
            EffectOutcomeKind kind, OperatorState recipient, DamageResult damage,
            int amount, StatusKind status, int duration, int progress)
        {
            Kind = kind;
            Recipient = recipient;
            Damage = damage;
            Amount = amount;
            Status = status;
            Duration = duration;
            Progress = progress;
        }

        public EffectOutcomeKind Kind { get; }
        public OperatorState Recipient { get; }

        /// <summary>Meaningful for <see cref="EffectOutcomeKind.Damaged"/>.</summary>
        public DamageResult Damage { get; }

        /// <summary>Healing amount, for <see cref="EffectOutcomeKind.Healed"/>.</summary>
        public int Amount { get; }

        public StatusKind Status { get; }
        public int Duration { get; }

        /// <summary>Where a pulled operator was placed.</summary>
        public int Progress { get; }

        public static EffectOutcome Damaged(OperatorState recipient, DamageResult damage) =>
            new EffectOutcome(EffectOutcomeKind.Damaged, recipient, damage, damage.AmountApplied, default, 0, 0);

        public static EffectOutcome Healed(OperatorState recipient, int amount) =>
            new EffectOutcome(EffectOutcomeKind.Healed, recipient, default, amount, default, 0, 0);

        public static EffectOutcome StatusApplied(OperatorState recipient, StatusKind status, int duration) =>
            new EffectOutcome(EffectOutcomeKind.StatusApplied, recipient, default, 0, status, duration, 0);

        public static EffectOutcome Pulled(OperatorState recipient, int progress) =>
            new EffectOutcome(EffectOutcomeKind.Pulled, recipient, default, 0, default, 0, progress);

        public static EffectOutcome Executed(OperatorState recipient) =>
            new EffectOutcome(EffectOutcomeKind.Executed, recipient, default, 0, default, 0, 0);

        public override string ToString() => $"{Kind} -> {Recipient?.Name}";
    }
}