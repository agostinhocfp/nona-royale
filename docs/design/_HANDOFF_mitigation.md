# Handoff — the mitigation pass

> Location in repo: `docs/design/_HANDOFF_mitigation.md`
> **Replaces `_HANDOFF_evasion.md`.** Delete that file; it described half of this change.
> Status: **Not started.** Delete this once both halves land and `COMBAT_SYSTEMS` §5.5 and §5.6 are rewritten.
> Related: `COMBAT_SYSTEMS.md` §2.1, §2.2, §5.5, §5.6, §10.5 · ADR-0002 Amendment 5

Paste this whole file into a fresh session as context. It is written to be read cold.

---

## Why these are one change, not two

Two separate needs arrived at the same interface from opposite directions.

**Evasion is too swingy.** §5.5's own rationale admits it: a coin flip _"can eat a four-turn ultimate investment on one roll."_ The per-round cap bounded the frequency and did nothing about the variance.

**Trauma Plate needs a shield with a value.** Javi's second ability is a shield with a **2-point pool** rather than the current whole-instance absorb. §5.6 says why the existing rule cannot survive a castable shield: absorbing one entire instance regardless of size is a timing lottery, worth 1 against From the Hip and 3 against a collision, and unpriceable.

Both mean the same thing to the code: **`IDamageMitigation` stops returning bools and starts returning amounts.** Doing them in two passes means rewriting the same two branches of `DamagePipeline.Apply` twice, and the second pass would collide with the first.

## The interface, now

```csharp
public interface IDamageMitigation
{
    bool TryEvade(OperatorState target, IRandom random);
    bool TryAbsorb(OperatorState target);
}
```

Both are terminal: `DamagePipeline.Apply` returns immediately on either, with `DamageOutcome.Evaded` or `Absorbed`, zero applied, health unchanged. The instance never reaches step 4.

## The interface, after

Something like:

```csharp
int EvasionReduction(OperatorState target);          // no IRandom — nothing is rolled
int AbsorbFrom(OperatorState target, int amount);    // returns what the pool actually ate
```

Both subtract rather than stop. The instance always continues to step 4; it just arrives smaller.

Consequences, in order of how likely they are to be missed:

1. **`DamagePipeline` may no longer need `IRandom` at all.** Evasion is its only use. Dropping it makes dice the sole RNG consumer in combat — a real win for the harness — but it is a constructor change reaching `MatchFactory`, `TurnStateMachineTests`, `NeutralizeRulesTests`, `AbilityResolverTests` and `SwapEffectTests`. Name the cost; don't discover it.

2. **`DamageResult` needs a mitigated amount.** It currently carries `AmountApplied`, `RemainingHealth`, `TargetOperatorId` and `Cause`. Add something like `AmountMitigated` so the view can play a glancing blow. `Cause` and the `TargetSurvived` helper stay exactly as they are.

3. **`DamageOutcome.Evaded` and `.Absorbed` survive, for the zero case only.** When mitigation meets or exceeds the instance, the target takes nothing, and "reduced to zero" and "dodged" look identical to a player. `GameEngine.EmitDamage` already switches on the outcome to play three different things, and `FeedbackLayer` draws MISS and BLOCK off them — preserving both means the view needs no change for that path.

4. **Shield expiry changes shape.** Today `TryAbsorb` removes the status. With a pool it decrements, and removes only when the pool hits zero or the duration ends. That is a `StatusRegistry` change: the shield entry's `Magnitude` is the obvious home for the remaining pool, since it is already a `double` on `Entry` and already written by `Apply`.

## The numbers

**Evasion → flat reduction of 1.**

Normal damage on the roster is 1 (From the Hip), 2 (Dargin Pulse), 2 (Cryo-Pulse), 2 (All-In Mauling), 3 (Ace Shards), and collision at 3. Mean **2.17**.

| Scheme                           | Prevented per round | Variance |
| -------------------------------- | ------------------- | -------- |
| Today: 50% to erase one instance | 1.08                | enormous |
| Flat **2**                       | 2.00                | none     |
| Flat **1**                       | 1.00                | none     |

**Adopt 1.** A flat 2 is a _buff_ — it prevents nearly twice what the current coin flip does while looking like a nerf. This is the single most important line in this handoff, and the mean moved since it was first calculated (From the Hip dropped to 1, All-In Mauling to 2), so recompute rather than trusting a remembered figure.

**Keep the per-round charge.** `_evasionSpentThisRound` still limits it to the first Normal instance per round, still re-arms at the holder's upkeep, and a cleanse still must not re-arm it (§5.8). Without the cap Kurbyn reduces _every_ hit, which is far stronger than what he has now.

**Trauma Plate: 2-point pool, and the rest is unsettled.** The proposal on the table is **cost 3 · cooldown 3 · range 3 · 2-point pool · 2 turns**. The originally sketched cost 3 / cooldown 1 / range 6 has three problems worth resolving before building:

- **Cooldown 1 against duration 2 means permanent uptime.** He can hold shields on two operators forever and near-cover all three. A shield that is always up is flat damage reduction on a squad, which is a different mechanic.
- **Range 6 is Mimi's**, and §10.4 states it is the sole compensation for her 5 health. It also contradicts §10.5's own line that Javi pays for reach in fragility rather than distance.
- **A 1-point pool cancels From the Hip outright** and halves three of the four 2-damage abilities, while saving nobody from the collision that actually kills them — it blunts everything that does not matter and nothing that does.

## Files to ask for

Already available and known-good: `IDamageMitigation.cs`, `DamageResult.cs`, `DamagePipeline.cs`, `StatusRegistry.cs`, `GameEngine.cs`, `TurnStateMachineTests.cs`, `SwapEffectTests.cs`, `FeedbackLayer.cs`.

**Still needed:** whatever holds `DamageInstance` and `DamageOutcome`, and `DamagePipelineTests.cs` if it exists — §13 names nine damage tests.

## Tests that break or need writing

From §13:

- `EvasionResolvesBeforeShield_AndPreservesTheShield` — **re-derive the ordering, do not assume it holds.** Today evasion may erase the instance entirely, so it must resolve first to avoid burning a shield unnecessarily. Under two subtractions the instance usually survives to reach the shield anyway, and the argument for the order changes.
- `SecondNormalInstanceInSameRound_IgnoresEvasion` — still valid, different assertion.
- `NormalDamage_IsFullyAbsorbedByShieldThenShieldExpires` — no longer describes the rule. Split into a partial absorb and a pool-exhausted case.
- `AtomicDamage_IgnoresShield` / `_IgnoresEvasion` — unchanged in intent; the mechanism moves from "never rolls" to "never subtracts".
- `Upkeep_ReArmsTheEvasionCharge` and `MarkDamage_IsAtomic_AndIgnoresEvasion` in `TurnStateMachineTests` both construct against the current signatures. Rewrite.

New: reduction exceeding the instance still reports `Evaded`; a pool absorbing part of an instance leaves the rest; a pool surviving one hit and dying to the next.

## Docs to update in the same commit

- **§5.5** — rewrite. The probability and the negation both go.
- **§5.6** — rewrite. The pool replaces the whole-instance absorb, and the "superseded in principle" row in §11 becomes a real supersession.
- **§2.1 step 2 and 3** — both become subtractions; the "stop" is removed from each.
- **§10.5** — Trauma Plate moves out of its _not implemented_ row, and the banner comes off Javi's section.
- **§11** — rows for probabilistic evasion and for whole-instance shields.
- **§12** — `EvasionChance` is replaced by the new dial, tagged unmeasured.
- **ADR-0002** — this warrants an **Amendment 6**, matching how 5 reads: values adopted by reasoning, explicitly not measured, with a harness run as the revisit trigger.
- **`OPERATORS.md`** — Javi's blocker clears, and he moves out of _In play, incomplete_. Mimi stays: this unblocks the Tech type as a side effect, but Cryo Field still has no mechanic.

## Two rules to settle before writing code

**Does mitigation apply to Atomic?** No — §2.2 says Atomic ignores every layer, and that does not change. State it, because a subtraction is easier to apply universally by accident than a branch was.

**What happens to Kurbyn's fiction?** "Evasive Protocol" reads as dodging and will now read as damage reduction. Either rename the passive or keep the name and let the animation sell a glancing blow. A naming decision, not a mechanical one, but make it deliberately.

## Context you need but should not re-derive

- **The harness baseline is invalid** and withdrawn (ADR-0002 Amendment 5). Do not tune against any number in §12.
- **Do not re-baseline before this lands.** It moves every figure again.
- **The slow/aura stacking conflict is open** (§12) and distorts any sweep taken before it is ruled on. Unrelated to mitigation; do not fix it here.
- **This change unblocks the Tech damage type.** A four-type matrix — Normal, Force, Tech, Atomic across Evasion and Shield — has been agreed and is blocked on shields having a real source. Trauma Plate is that source. **Do not add the types in this pass**; just know that the next person can.
