// Assets/_Project/Scripts/Core/Services/DamageResult.cs
namespace NonaRoyale.Core.Services
{
    /// <summary>What the pipeline did with one damage instance.</summary>
    public readonly struct DamageResult
    {
        /// <remarks>
        /// <b><paramref name="amountMitigated"/> is appended, not inserted.</b>
        /// Putting it beside <paramref name="amountApplied"/> where it belongs
        /// would renumber every positional argument at every call site, in a
        /// file other sessions are editing. Last-and-optional means existing
        /// callers compile untouched and only the pipeline passes it.
        /// </remarks>
        public DamageResult(
            DamageOutcome outcome, int amountApplied, int remainingHealth,
            int targetOperatorId, string cause = "unknown", int amountMitigated = 0)
        {
            Outcome = outcome;
            AmountApplied = amountApplied;
            RemainingHealth = remainingHealth;
            TargetOperatorId = targetOperatorId;
            Cause = cause;
            AmountMitigated = amountMitigated;
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

        /// <summary>Health actually lost. Zero when evaded or fully absorbed.</summary>
        public int AmountApplied { get; }

        /// <summary>
        /// How much a mitigation layer prevented. The whole instance on
        /// <see cref="DamageOutcome.Evaded"/>, the pool's share on a partial
        /// absorb, zero otherwise.
        /// </summary>
        /// <remarks>
        /// <b>For presentation, like <see cref="Cause"/>, and read by no rule.</b>
        /// Under a whole-instance shield a blocked hit was visibly all-or-nothing.
        /// Under a pool the common case is a 3-damage hit landing for 1, and
        /// without this the view has no way to draw the difference between that
        /// and a 1-damage hit landing in full — the board would under-report
        /// what the defending player's 4 energy actually bought.
        /// </remarks>
        public int AmountMitigated { get; }

        public int RemainingHealth { get; }
        public int TargetOperatorId { get; }

        /// <summary>
        /// True if the target is still on the board. The single question
        /// collision resolution asks: a surviving occupant holds its cell and
        /// bounces the mover back, however it survived (COMBAT_SYSTEMS §7.2).
        /// </summary>
        public bool TargetSurvived => Outcome != DamageOutcome.Neutralized;

        public override string ToString() =>
            AmountMitigated > 0
                ? $"{Outcome} ({AmountApplied}, -{AmountMitigated} mitigated, hp {RemainingHealth})"
                : $"{Outcome} ({AmountApplied}, hp {RemainingHealth})";
    }
}