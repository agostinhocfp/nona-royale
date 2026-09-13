// Assets/_Project/Scripts/Core/Services/DamageResult.cs
namespace NonaRoyale.Core.Services
{
    /// <summary>What the pipeline did with one damage instance.</summary>
    public readonly struct DamageResult
    {
        public DamageResult(
     DamageOutcome outcome, int amountApplied, int remainingHealth,
     int targetOperatorId, string cause = "unknown")

        {
            Outcome = outcome;
            AmountApplied = amountApplied;
            RemainingHealth = remainingHealth;
            TargetOperatorId = targetOperatorId;
            Cause = cause;

        }

        /// <summary>
        /// What dealt it — "bleed", "mark", "collision", "ability", "self".
        /// </summary>
        /// <remarks>
        /// Carried for presentation, never read by a rule. Upkeep damage in
        /// particular arrives during a phase where nothing else moves, so a health
        /// bar dropping with no stated cause is the board declining to explain
        /// itself (`PRESENTATION.md` §2).
        /// </remarks>
        public string Cause { get; }


        public DamageOutcome Outcome { get; }

        /// <summary>Health actually lost. Zero when evaded or absorbed.</summary>
        public int AmountApplied { get; }

        public int RemainingHealth { get; }
        public int TargetOperatorId { get; }

        /// <summary>
        /// True if the target is still on the board. The single question
        /// collision resolution asks: a surviving occupant holds its cell and
        /// bounces the mover back, however it survived (COMBAT_SYSTEMS §7.2).
        /// </summary>
        public bool TargetSurvived => Outcome != DamageOutcome.Neutralized;

        public override string ToString() => $"{Outcome} ({AmountApplied}, hp {RemainingHealth})";
    }
}