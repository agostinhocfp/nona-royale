// Assets/_Project/Scripts/Core/Services/AbilityResolution.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Core.Services
{
    /// <summary>Whether an ability went off, and everything it did if it did.</summary>
    public sealed class AbilityResolution
    {
        private static readonly IReadOnlyList<EffectOutcome> Nothing = Array.Empty<EffectOutcome>();

        private AbilityResolution(bool approved, AbilityRefusal refusal,
            TargetingVerdict targetingVerdict, IReadOnlyList<EffectOutcome> outcomes)
        {
            Approved = approved;
            Refusal = refusal;
            TargetingVerdict = targetingVerdict;
            Outcomes = outcomes;
        }

        public bool Approved { get; }

        public AbilityRefusal Refusal { get; }

        /// <summary>Detail when <see cref="Refusal"/> is <see cref="AbilityRefusal.IllegalTarget"/>.</summary>
        public TargetingVerdict TargetingVerdict { get; }

        /// <summary>What happened, in order. Empty on a refusal — a refused ability costs nothing.</summary>
        public IReadOnlyList<EffectOutcome> Outcomes { get; }

        public static AbilityResolution Resolved(IReadOnlyList<EffectOutcome> outcomes) =>
            new AbilityResolution(true, AbilityRefusal.None, TargetingVerdict.Legal, outcomes);

        public static AbilityResolution Refused(
            AbilityRefusal refusal, TargetingVerdict verdict = TargetingVerdict.Legal) =>
            new AbilityResolution(false, refusal, verdict, Nothing);

        public override string ToString() =>
            Approved ? $"resolved ({Outcomes.Count} effects)" : $"refused: {Refusal}";
    }
}