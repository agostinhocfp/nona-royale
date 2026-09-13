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
        Executed = 4,

        /// <summary>
        /// One end of a swap. Two of these are emitted per swap, one per
        /// operator — a view told about only one would draw a board that is
        /// wrong.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="Pulled"/> even though both are placement,
        /// because the view plays them differently: a pull drags one piece
        /// toward another, a swap exchanges two at once.
        /// </remarks>
        Swapped = 5,

        /// <summary>One status stripped by a cleanse. One of these per status removed.</summary>
        StatusRemoved = 6
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

        /// <summary>Where a pulled or swapped operator was placed.</summary>
        public int Progress { get; }

        public static EffectOutcome Damaged(OperatorState recipient, DamageResult damage) =>
            new EffectOutcome(EffectOutcomeKind.Damaged, recipient, damage, damage.AmountApplied, default, 0, 0);

        public static EffectOutcome Healed(OperatorState recipient, int amount) =>
            new EffectOutcome(EffectOutcomeKind.Healed, recipient, default, amount, default, 0, 0);

        public static EffectOutcome StatusApplied(OperatorState recipient, StatusKind status, int duration) =>
            new EffectOutcome(EffectOutcomeKind.StatusApplied, recipient, default, 0, status, duration, 0);

        public static EffectOutcome StatusRemoved(OperatorState recipient, StatusKind status) =>
            new EffectOutcome(EffectOutcomeKind.StatusRemoved, recipient, default, 0, status, 0, 0);

        public static EffectOutcome Pulled(OperatorState recipient, int progress) =>
            new EffectOutcome(EffectOutcomeKind.Pulled, recipient, default, 0, default, 0, progress);

        public static EffectOutcome Swapped(OperatorState recipient, int progress) =>
            new EffectOutcome(EffectOutcomeKind.Swapped, recipient, default, 0, default, 0, progress);

        public static EffectOutcome Executed(OperatorState recipient) =>
            new EffectOutcome(EffectOutcomeKind.Executed, recipient, default, 0, default, 0, 0);

        public override string ToString() => $"{Kind} -> {Recipient?.Name}";
    }
}