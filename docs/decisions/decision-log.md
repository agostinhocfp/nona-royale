# Nona Royale — Decision Log

> Location in repo: `docs/decisions/decision-log.md`
> Related: `docs/decisions/` (ADRs), `docs/design/COMBAT_SYSTEMS.md`, `docs/design/balance-log.md`

**What this is.** ADRs record decisions large enough to have alternatives and
consequences worth arguing. This log records everything below that bar: the
small rulings, the deliberate deferrals, and the questions that surfaced while
implementing an ADR and have not been answered yet. An ADR is a document; an
entry here is a line.

**The rule.** Nothing gets resolved in a code comment alone. If a choice was
made and someone could reasonably have made the other one, it lands here on the
day it was made. An entry that grows alternatives and consequences graduates
into an ADR.

Status values: **Settled** · **Open** · **Deferred** · **Superseded**

---

## Settled

### D-001 — Mark deals damage over time

**2026-09-12 · COMBAT_SYSTEMS §5.7**

Mark was bookkeeping only, which left Tagged From Above costing 9 energy and
doing nothing on the turn it was cast. It now deals `MarkDamagePerTurn` Atomic
at the marked operator's upkeep, every turn it is active.

Ticking does **not** consume it. That is the whole distinction from Bleed: bleed
is delayed damage that fires once and is gone, a mark is a lingering condition
that bills until its duration ends. The two methods are named `ConsumeBleed` and
`MarkTickDamage` so the difference is visible at the call site.

### D-002 — The payout window is the mark's own duration

**2026-09-12 · COMBAT_SYSTEMS §10.2**

Tagged From Above previously specified a separate "within 3 of Syla's turns"
window. That was a second timer duplicating the status's duration, and it
counted against a different operator's turn index than `StatusRegistry` uses —
the registry resolves durations against the _target owner's_ turn count.

One timer, stored on the mark, expiring at the marked operator's own upkeep like
every other status. The doc's "3 of Syla's turns" wording is superseded.

### D-003 — Magnitude is the single signed speed channel

**2026-09-12 · `StatusRegistry`**

`Entry.Magnitude` was written by `Apply()` and read by nothing; `SpeedModifier`
hardcoded Slow and pulled its size from config. Kurbyn's `+0.5` had nowhere to
live, which is why `KurbynPassiveSpeedBonus` was a constant no engine code read.

Magnitudes are now signed speed deltas summed across kinds. Slow stores −0.5,
Hastened +0.5, Evasive Protocol +0.5. A zero magnitude means "use this kind's
default", so `AlphaRoster` needs no dependency on config while an ability that
wants a different size can state one.

Consequence: re-application compares by **absolute value**. With signed
magnitudes the old `Math.Max` would have let a weak slow override a strong one.

### D-004 — Passives live in their own store

**2026-09-12 · COMBAT_SYSTEMS §1.2, §5.1**

`ClearAll` removed an operator's whole dictionary, taking permanent passives with
it. Kurbyn lost Evasive Protocol — evasion _and_ speed — the first time he died,
silently and permanently.

`StatusRegistry` now keeps `_passives` separate. `ClearAll` drops only applied
statuses; `ActiveEntry` checks applied first, then passives. "Passives survive
neutralize" is structural rather than something every future caller has to
remember to undo.

Consequence: suppressing a passive means applying a status of the same kind,
which shadows it. There is no "remove passive" path, deliberately.

### D-005 — Haste lasts 2 turns, not 1

**2026-09-12 · `CombatConfig.HasteDurationTurns`**

The payout can fire on the marker's own turn (a collision or ability kill), by
which point that turn's movement is usually spent — a 1-turn buff would
routinely be worth nothing. At 2 it covers the remainder of the current turn and
the whole of the next, whether it fired on the marker's turn or on an opponent's
upkeep. This is what §10.2's "one round" resolves to.

### D-006 — The payout lives in `NeutralizeRules`

**2026-09-12 · `NeutralizeRules.PayOutMark`**

Four things neutralize: collision, ability, bleed tick, mark tick. Reading the
mark at the one place every death funnels through means no call site can forget
it. It runs _before_ `ClearAll`, because neutralize strips the mark it needs.

`Apply` now returns the hastened operators so callers can emit events; the
service owns state, not presentation.

---

## Open

### D-007 — Slow and Intimidating Presence stack, contradicting §5.2

**Pre-existing. Not introduced by the 2026-09-12 work.**

§5.2 says slow sources do not stack; the largest applies. `AuraRules` honours
that among auras, and `StatusRegistry` honours it among statuses. But
`GameEngine.Move` **sums** the two channels:

```
_statuses.SpeedModifier(op) + _auras.SpeedModifierFor(op, _operators)
```

So a Syla slowed by From the Hip while standing in Bouncer's aura takes −1.0 and
drops to the `MinSpeedMultiplier` floor. Two half-slows become a hard stop, which
is exactly the outcome §5.2 exists to prevent.

Three ways out, none obviously right:

1. Combine the two channels by strongest-negative rather than sum. Breaks the
   moment Hastened and a slow coexist, since one channel now carries both signs.
2. Split the registry's output into "strongest slow" and "summed bonuses", and
   have the caller combine slow-with-aura by max and add bonuses after. Correct,
   but leaks the combination rule into `GameEngine`.
3. Amend §5.2 to say aura and status slows _do_ stack, and accept that a
   coordinated Bouncer-plus-Syla play floors a target. Cheapest, and arguably
   the more interesting rule, but it is a design change, not a bug fix.

Needs a ruling before the harness is trusted, because it changes effective
speeds under exactly the conditions the sim generates most often.

### D-008 — A marked operator's self-kill pays out the enemy

**2026-09-12 · `NeutralizeRules.PayOutMark`**

Any death of a marked operator pays out, whatever killed it. Bouncer's All-In
Mauling is the only self-kill route, so a Bouncer who mauls himself to death
while marked hands the enemy squad a free haste.

Checking the killer instead needs the killer's id threaded through
`DamageResult`, which carries only the target's. Accepted as-is for now on the
grounds that it is rare and that the fix is the same one D-009 needs.

### D-009 — Bleed attributes its damage to its victim

**Pre-existing.**

`TurnStateMachine` builds bleed's `DamageInstance` with `op.Id` as the source —
the bleeding operator, not whoever applied the stack. `Entry.SourceOperatorId` is
already stored for bleed, so the data exists; nothing reads it.

Harmless today because nothing consumes the source. It stops being harmless now
that D-006 has shipped: a marked target finished by Syla's own Ace Shards bleed
credits itself, and the payout does not fire when it should.

Fix is a `BleedSource(op)` query alongside `ConsumeBleed`, plus carrying the
source through `DamageResult` — which also closes D-008. One commit, both.

### D-010 — Upkeep payouts emit no `StatusApplied` event

**2026-09-12 · `TurnStateMachine.BeginTurn`**

`TurnStateMachine` calls `_neutralize.Apply` directly and discards the returned
hastened operators. A mark tick that kills applies the haste correctly but the
view is never told, so that one path animates nothing.

State is right; presentation is incomplete. Fixing it means widening
`UpkeepReport`.

### D-011 — All-In Mauling's zero cooldown is inert

**2026-09-12 · COMBAT_SYSTEMS §3.1, §10.1**

At mean 3.5 energy per turn, a 6-cost ability is already gated to roughly every
second turn by the economy. A declared cooldown only bites when it is _longer_
than that, so `cooldownTurns: 0` on a 6-cost ability buys nothing — and the
ability is otherwise a strictly worse Velvet Rope (same cost, same damage, one
less range, no pull).

Either the cost drops to 3, which makes it genuinely castable every turn and
hands the limiting job to the self-damage where the name implies it belongs, or
the zero cooldown is dropped as the fiction it currently is. Range 1 → 2 and
self-damage 3 → 1 have already landed; the cost has not been decided.

---

## Superseded

| Decision                                             | Superseded by        | Date       |
| ---------------------------------------------------- | -------------------- | ---------- |
| Mark applies no modifier; bookkeeping only           | D-001                | 2026-09-12 |
| Payout window "within 3 of Syla's turns"             | D-002                | 2026-09-12 |
| `SpeedModifier` reads Slow only, size from config    | D-003                | 2026-09-12 |
| Passives re-granted by whoever rebuilds the operator | D-004                | 2026-09-12 |
| Speed band 1.5–2.0 (ADR-0002 Amendment 2)            | ADR-0002 Amendment 4 | —          |
