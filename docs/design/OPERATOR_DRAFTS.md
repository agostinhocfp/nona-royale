# Operator Drafts — Handoff Notes

> Location in repo: `docs/design/OPERATOR_DRAFTS.md`
> Status: **Draft.** Fuse and Ghost are not implemented. **Revú was built on 2026-09-17** (`COMBAT_SYSTEMS.md` §10.11), with the designer's rulings; his section below is kept as the design record, and the code wins any disagreement. Numbers are reasoned against
> peers, not simulated. Each operator below shifts the draft RNG when added, so
> no figure measured before them survives contact.
> Conventions: costs in energy (cap 12, ~3.5/turn drip), ranges in track steps,
> durations in rounds, CD in owner turns. "New" flags machinery that does not
> exist in the core today and needs a COMBAT_SYSTEMS amendment before code.

---

## 1. Fuse

**5 HP · speed 1.5 · the dead man's switch**

The inversion operator: trivially easy to kill, and killing him is the
mistake. The 4 HP is not a weakness to compensate — it is the trigger.

| Slot    | Name              | Cost | CD  | Range | Effect                                                                                                                                                                                                     |
| ------- | ----------------- | ---- | --- | ----- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Passive | Dead Man's Switch | —    | —   | —     | When Fuse is neutralized, he detonates: **3 Atomic, radius 1**, around his death cell. **New: on-death trigger.** The callback point already exists (NeutralizeRules death-cell hook, built for Zero-Day). |
| 1       | Hot Swap          | 3    | 2   | 2     | Swap places with an **ally**. Existing mechanic (Translocation shape), aimed the other way: the tank steps out, Fuse steps in.                                                                             |
| 2       | Cooked Round      | 4    | 3   | 2     | Attach a charge to **himself**: at his next upkeep he detonates, radius 1 — and he counts as caught. Reuses the operator-anchored deferred machinery (§6.4) pointed inward.                                |
| 3       | Kamikaze Protocol | 9    | 5   | self  | Voluntary detonation, immediately: **4 Atomic, radius 2**, and Fuse is neutralized. An ultimate that spends an operator. **New: self-neutralize effect.**                                                  |

**Counterplay:** stun him and walk away (threat radius is 1–2); push him
(Sonic Disrupter); out-range him (Kian); or refuse to kill him and race — an
ignored 4-HP unit is a wasted slot. His own team pays too: the ultimate costs
a squad slot and the redeploy crawl.

**Open questions:** does Dead Man's Switch hit allies caught in radius 1?
Does it fire on _every_ neutralize cause (execute, collision, bleed)? Redeploy
timing after Kamikaze Protocol — normal pity-deploy coverage or a penalty?

---

## 2. Ghost

**5 HP · speed 1.5 · the energy saboteur**

Zero damage — a roster first. He doesn't beat the enemy squad, he defunds it.
Rebuilt away from pure denial (locks are misery) into **taxation**: every
effect leaves the enemy a choice with a price.

| Slot | Name          | Cost | CD  | Range | Effect                                                                                                                                                                            |
| ---- | ------------- | ---- | --- | ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | Siphon        | 3    | 2   | 3     | Steal **2 energy** from the enemy pool into yours. **New: energy-transfer effect.** Visible and telegraphed — the EnergyChanged events already exist, so the theft is in the log. |
| 2    | Brownout      | 4    | 3   | 3     | Enemy abilities cost **+2** until their next turn ends. A tax, not a lock — they can still cast, they wince. **New: cost-modifier status.**                                       |
| 3    | Grand Larceny | 9    | 5   | —     | **Swap energy pools with the enemy.** The 9 is paid _before_ the swap — running your own pool to zero first is the skill test. **New: pool-swap effect.**                         |

**Counterplay is economic:** spend fluidly, never hoard — there is nothing to
steal from an empty pool. Physically: he operates at range 3, never fights,
and any diver who reaches him ends the audit (4 HP).

**Honest flag:** this is a racing game and movement costs no energy — Ghost
never touches the win condition directly. His warp is pace degradation, not
lockout. Watch for opponents going passive in playtests.

**Open questions:** does Siphon credit past the 12 cap (burn) or waste?
Does Brownout stack with itself? Pool swap when the enemy pool is empty —
legal and funny, or refused?

---

## 3. Revú

> **Implemented 2026-09-17** as `Revu.cs`, ids 1101–1102, then tuned the same day: health 8, Leech Round 2 damage with cooldown 1, Sadist cooldown 4. Rulings: Equilibrium reads instant hits only, its halves are at least 1, and it applies to Atomic; Sadist reads the target's seat and splashes half; Leech Round's energy is destroyed. The "4 HP" reasoning below predates the draft-health pass.

**7 HP · speed 1.0 · the loan shark**

The punishment web. His ultimate scales with what the enemy has _spent_, his
passive makes the cheap answers to him feed that ultimate, and his basic
drains the pool further. Every door the enemy tries has a price on it.

| Slot | Name                  | Cost | CD  | Range | Effect                                                                                                                                                                                                                                                                                                              |
| ---- | --------------------- | ---- | --- | ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | Leech Round           | 3    | 2   | 3     | **1 Normal + drain 2 energy** from the enemy pool. From the Hip's price tag; the drain is the rider. **New: energy-drain effect** (shared machinery with Ghost's Siphon).                                                                                                                                           |
| 2    | Equilibrium (passive) | —    | —   | —     | Incoming **ability** damage scales with the casting ability's cost: cost ≤ 3 deals **double**, cost ≥ 6 deals **half** (rounds down), 4–5 lands clean. Collisions, bleeds and beacons are not casts and bypass it entirely. **New: incoming-damage modifier; DamageInstance must carry the source ability's cost.** |
| 3    | Sadist                | 9    | 5   | 3     | Damage = **floor((12 − enemy pool) ÷ 3)** — empty pool hits for **4**, pool of 6 for 2, full pool for 0. Enemies within **2** of the target take half (round down).                                                                                                                                                 |

**The web:** cheap casts are the best way to kill him (Equilibrium doubles
them) — but casting spends energy, which feeds Sadist. Hoarding blunts Sadist
but means never casting while Leech Round drains with impunity. The honest
answer: **collisions** — dice combat costs no energy and bypasses Equilibrium.
A 4-HP operator whose hard counter is the game's basic movement mechanic.

**Readability rule:** "one damage for every 3 energy missing from their
pool" — the enemy can compute Sadist in their head, which is the constraint
that fixed the speed band and applies here too.

**Open questions:** 4 HP keeps Equilibrium as execution-bait; 8–9 HP would
make it a real wall instead — 4 HP is the recommendation (the web does the
talking). Leech Round into Sadist is a two-turn combo worth ~3 damage + drain
— confirm that reads as setup, not delete-button. Mirror match: Sadist at
max roll one-shots another Revú (4 vs 4). Poetic or unacceptable?

---

## Implementation notes (all three)

- **Ability ids (stale):** Luka took 901–903 and Lethe took 1001–1002, so Fuse and Ghost need new numbers when they are built. The plan was Fuse #9 (901–903), Ghost #10 (1001–1003), Revú #11
  (1101–1103), per the operatorNumber × 100 + slot scheme.
- **New machinery total:** on-death trigger; self-neutralize; energy
  drain/transfer; cost-modifier status; pool swap; pool-scaled damage;
  incoming-damage modifier. Zero-Day's operator-anchored charges and the
  NeutralizeRules death-cell callback cover part of Fuse already.
- **Descriptions:** none written here on purpose — `AbilityDefinition`
  enforces the no-digits rule, so descriptions get written at implementation
  time, not in handoff.
- **Silhouettes:** Fuse must read as the smallest, scariest piece on the
  board (`PieceShape.SmallestHealth` drops to 4 — the comment in the file has
  been waiting for exactly this). Ghost and Revú need cases too; the default
  disc is Nuetu's now.
- **Sim:** no figure in this file is measured. First sweep after
  implementation should be cast counts and match lengths with each operator
  in the pool, same protocol as the Sanity re-baseline.
