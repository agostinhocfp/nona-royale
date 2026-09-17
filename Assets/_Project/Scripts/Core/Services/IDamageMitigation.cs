// Assets/_Project/Scripts/Core/Services/IDamageMitigation.cs
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// The mitigation layers the pipeline consults before applying Normal or
    /// Tech damage, in the fixed order of COMBAT_SYSTEMS §2.1: tech ward (Tech
    /// only), then evasion, then shield, then apply.
    /// </summary>
    /// <remarks>
    /// <b>The two layers are deliberately different shapes.</b> Evasion is
    /// terminal and returns a bool — it either negates the instance or it does
    /// not. The shield subtracts and returns an amount, because a shield with a
    /// per-ability pool has to be priceable, and absorbing one whole instance
    /// regardless of size is a timing lottery worth 1 against From the Hip and
    /// 3 against a collision (§5.6).
    ///
    /// That asymmetry is a decision, not an oversight. Deterministic evasion —
    /// a flat reduction replacing the roll — was proposed in
    /// <c>_HANDOFF_mitigation.md</c> and <b>not adopted</b>; the rate was moved
    /// instead. If it is ever revisited, <see cref="TryEvade"/> becomes an
    /// <c>int EvasionReduction(OperatorState)</c>, <see cref="IRandom"/> leaves
    /// this interface and the pipeline entirely, and every damage test's
    /// construction changes with it.
    ///
    /// Behind an interface so the order is fixed now and stays fixed. Adding a
    /// mitigation layer later is a change here, never a change to §2.1's order.
    /// </remarks>
    public interface IDamageMitigation
    {
        /// <summary>
        /// Attempts to evade. Consumes the round's charge on the attempt,
        /// whether or not it succeeds — the charge is the attempt, not the
        /// success (§5.5).
        /// </summary>
        /// <remarks>
        /// Resolved <i>before</i> the shield deliberately: evasion negates the
        /// whole instance, so resolving it first means a successful dodge never
        /// burns a pool the operator paid energy for and may still need.
        /// </remarks>
        bool TryEvade(OperatorState target, IRandom random);

        /// <summary>
        /// Offers <paramref name="amount"/> to the target's shield pool and
        /// returns how much the pool actually ate. Zero when there is no shield.
        /// </summary>
        /// <remarks>
        /// The pool is decremented by what it absorbs and removed when it is
        /// spent. Implementations must never return more than
        /// <paramref name="amount"/>, and never a negative — the pipeline
        /// clamps defensively, but a violation there is a bug here.
        /// </remarks>
        int AbsorbFrom(OperatorState target, int amount);

        /// <summary>
        /// Whether the target blocks Tech damage outright right now (§5.12).
        /// Consumes nothing — a ward is a duration, not a charge.
        /// </summary>
        /// <remarks>
        /// Asked first, and only for Tech. A blocked instance never reaches
        /// evasion or the shield, so a ward never costs its holder the round's
        /// evasion charge or any of a pool it paid for separately.
        /// </remarks>
        bool BlocksTech(OperatorState target);

        /// <summary>Whether the target carries Equilibrium (§5.17).</summary>
        bool ScalesCastDamage(OperatorState target);
    }
}