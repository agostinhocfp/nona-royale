# Nona Royale — Combat Systems

> Location in repo: `docs/design/COMBAT_SYSTEMS.md`
> Status: **Accepted (alpha)** — every mechanic the alpha roster invokes is defined. No TBDs remain in the rules; open items in §12 are balance dials and post-MVP scope, not gaps.
> Date: 2026-09-11
> Related: `docs/design/OPERATORS.md` (roster), ADR-0002 (board size), ADR-0003 (topology), ADR-0004 (pure-C# core), `CONVENTIONS.md`, `docs/GDD.md`
> Supersedes: `docs/design/_HANDOFF_combat.md` (delete it)

Combat rules are **core logic**. Everything in this document lives in `NonaRoyale.Core`, references zero Unity types, is deterministic under an injected seed, and is reached through commands and reported through events (ADR-0004). Every numeric value here is **named config data in the core**, never an inline literal (`CONVENTIONS.md` → "Config, not literals").

---

## 0. The unifying rule

**There is exactly one way an operator leaves the board: it is *neutralized*.**

Ludo capture and HP damage are not two systems. Landing on an enemy is an attack that happens to be delivered by movement; it goes through the same damage pipeline as an ability, and it kills only if it reduces the target to 0 HP. HP is the single currency in the game.

This is the decision the rest of the document falls out of. It is also where the project's originality lives: ADR-0003 fixes the Ludo track 1:1 for *geometry*, and explicitly reserves the layer on top for mechanics like this one.

---

## 1. Health, neutralize, re-entry

### 1.1 Health

- Each operator has `MaxHealth` (per `OPERATORS.md`) and a current HP.
- Damage **persists across turns**. There is no passive regeneration. The only healing in the alpha roster is Bouncer's All-In Mauling on a friendly target.
- HP is restored to full **only** on neutralize (§1.2). A wounded operator two cells from HOME is intended tension, not a problem to be smoothed.

The old `[Range(3, 9)]` cap on `Operator.maxHealth` is **dead** — Bouncer is 12. The stat is unbounded in the core; presentation-layer sliders, if any, use 3–15.

### 1.2 Neutralized

An operator is neutralized when its HP reaches 0 by any route (collision, ability, bleed, Miracle Pull's execute, or Bouncer's self-damage).

On neutralize:

| | |
|---|---|
| Position | Returns to its owner's **yard** |
| Health | Restored to `MaxHealth` |
| Status effects | All cleared (stun, slow, bleed, stealth, shield, mark) |
| Cooldowns | All reset to ready |
| Track progress | **Entirely lost** — it re-enters at its start cell |
| Energy | Unaffected. The pool is player-level (§3) and a yarded operator costs the player nothing economically |

**There is no permanent death in the MVP.** Nothing removes an operator from a match for good. The GDD line about play continuing "until there's only one player left" is an artifact of the same early pass that produced the 3-energy-per-turn economy; player elimination is not a mechanic. Neutralize is a setback measured in turns, not a removal.

### 1.3 Re-entry

A neutralized operator re-enters exactly as it originally deployed (ADR-0003):

- **A roll containing a 6 may deploy one operator**, consuming that die. Deployment is optional.
- **The other die is that turn's movement roll**, applied (× speed) to any one operator, including the one just deployed.
- **Double 6 deploys two** operators and forfeits movement for that turn. It still grants the doubles re-roll (§6).
- The operator is placed on its colour's **start cell (S)**, which is a safe cell (§4.4) — so a deploy can never trigger a collision.

`DeployRequirement = 6`.

---

## 2. Damage

### 2.1 The damage pipeline

All damage — from abilities, collisions, and bleed alike — passes through one choke point in the core. Order is fixed:

1. **Legality.** Targeting was already validated (§4). Damage against an illegal target never reaches the pipeline.
2. **Evasion.** If the instance is **Normal** and the target has an unspent evasion charge this round, roll the seeded RNG at `EvasionChance`. On success: emit `DamageEvaded`, consume the charge, **stop**.
3. **Shield.** If the instance is **Normal** and the target holds a shield, the shield absorbs the entire instance, is consumed, emits `DamageAbsorbed`, **stop**.
4. **Apply.** Subtract from HP, emit `DamageDealt`.
5. **Neutralize check.** HP ≤ 0 → §1.2, emit `OperatorNeutralized`.

**Atomic damage skips steps 2 and 3.**

Evasion resolves before Shield deliberately: evasion is a reflex and should not burn a consumable the operator may need later.

### 2.2 Normal vs Atomic

- **Normal** is subject to every mitigation layer: Evasion, Shield, and anything added later.
- **Atomic** ignores all of it.

Atomic does **not** bypass *targeting* protection. Safe cells, home columns, and Stealth are not defenses — they are reachability rules, and Atomic damage that cannot legally be aimed at an operator simply never enters the pipeline.

> The one-sentence version, for the table: **Atomic can't be blocked, but it can't reach what it can't touch.**

**Sources of Atomic:** Bleed ticks, Ace Shards' bleed, all of Miracle Pull. Everything else — including collision — is Normal.

### 2.3 Self-damage

Self-inflicted damage (Bouncer's All-In Mauling) is applied **directly to HP**, bypassing the pipeline entirely. It cannot be evaded, shielded, or evaded-then-refunded, and it *can* neutralize its own caster.

---

## 3. Energy

A single **shared pool per player**, not per operator. The tactical choice the pool creates — *which* of my three operators spends this energy — is the point.

`Energy` as a per-operator stat is removed from the schema, alongside `Energy Efficiency` (§11).

### 3.1 Generation

```
EnergyGranted = floor(DiceTotal / 2)     // range 1–6, mean 3.5
```

- Granted **once per turn, on the first roll only.** Doubles grant an extra movement roll but never a second energy grant — otherwise a double snowballs both axes at once.
- `EnergyCap = 12`. Energy above the cap is burned, not stored. The cap is what forces spending; it is also exactly one ultimate plus nothing, so hoarding for an ult is a real, visible commitment.

Over a ~15-turn match a player sees roughly 52 energy for the whole squad: about three ultimates plus a handful of basics, or a steadier drip of cheap abilities. The superseded tiered rule (≤4 → 1, 5–8 → 2, ≥9 → 3) generated ~34 and left operators standing around; it is dead (§11).

### 3.2 Spending

- Energy is spent from the pool by **any operator the player owns**, in any order, during the action phase.
- There is **no cap on abilities per turn.** Cooldowns and the 12-energy ceiling are the regulators. Banking to 12 and firing Velvet Rope into All-In Mauling in a single turn is a combo worth having.
- A stunned operator cannot spend (§5.1). An operator in a home column cannot spend (§4.3).
- Costs are 3 / 6 / 9 by tier; passives are free. Per-ability costs in §10.

---

## 4. Targeting and range

### 4.1 Distance is measured along the track

Range is counted in **path steps along the circuit, in either direction**. Never Euclidean, never grid-adjacency.

Two cells can sit physically beside each other across the board's centre and be 24 steps apart. Allowing abilities to cross that gap would make the track — the only topology the game has — meaningless.

Counting follows the track around corners exactly as movement does; the arm geometry is invisible to range.

### 4.2 Area of effect

"Within N" means **N steps in each direction** from the AOE's origin cell: a window of `2N + 1` cells, origin included.

- **Ace Shards** and **Dargin Pulse** originate on the caster.
- **Miracle Pull**'s splash originates on the primary target and **excludes** that target (it already took the direct hit).

### 4.3 Home columns are out of the fight

An operator in its home column **cannot be targeted and cannot target**. No abilities in, no abilities out, no AOE reach, no collisions.

This makes ADR-0003's home-entry safe cell redundant in the best way, and removes the entire class of "does AOE reach into home" questions.

### 4.4 Safe cells

- The start cell (S) and, formally, the first home-column cell (H) are safe (ADR-0003).
- **No collision occurs on a safe cell.** A mover landing on a safe cell occupied by an enemy simply shares it. Both operators occupy; nothing resolves.
- Safe cells do **not** block abilities. An operator standing on S can be shot, pulled, stunned, and bled. Safe means safe from *collision*, nothing more — otherwise S becomes a free parking space and the combat layer stalls.

### 4.5 Friendly stacking

Friendly operators may share any cell freely. There is no blocking mechanic in the MVP. (ADR-0003's `SharksTable` special space hints at a two-operators-on-a-cell mechanic; special spaces are deferred and it is not defined here.)

---

## 5. Status effects

Durations are counted in **the affected operator's own turns**. An effect applied outside the target's turn takes hold on the target's next turn and expires at the end of it (for duration 1). Timers are stored as **absolute turn indices resolved at application time**, not decrementing counters — this removes the classic off-by-one and makes the boundary case a one-line test.

Statuses do not stack unless stated. Re-application refreshes duration and takes the larger magnitude.

### 5.1 Stun

- **Effect:** the operator cannot move and cannot spend energy on its next turn.
- **Passives stay live.** A passive is who an operator is, not what it does.
- **Forced movement still works.** Velvet Rope pulls a stunned target normally — being moved is not the target's action.
- **Cooldowns still tick.** They are timers, not actions.

### 5.2 Slow

- **Effect:** speed multiplier **−0.5** for the duration.
- `MinSpeedMultiplier = 0.5`. The band is 1.5–2.0 (§6), so −1 would erase most of it and turn an aura into hard control. −0.5 costs a 2.0 operator a quarter of its movement and a 1.5 operator a third — felt, not crippling.
- Sources do not stack; the largest applies.

### 5.3 Bleed

- **Effect:** each stack deals `BleedDamagePerStack = 1` **Atomic** damage at the **upkeep of the bleeding operator's owner's turn**, then that stack is removed. Bleed is delayed damage, not a lingering condition.
- Stacks are additive.
- An operator with at least one unspent stack counts as *bleeding* for Syla's From the Hip bonus.
- Bleed can neutralize. An operator dying at upkeep never gets its turn.

### 5.4 Stealth

- **Effect:** the operator **cannot be selected as a single target by an enemy.** That is the whole of it.
- **Still affected by:** AOE, passive auras, collision damage, and bleed already applied. Stealth hides you from being *aimed at*, not from the room.
- **Visible on the board.** The piece is never hidden from the opponent. This is a hot-seat digital board game; concealing a piece would mean building fog-of-war to service one ability and a UI that lies about the state.
- **Does not break on attacking.** A 9-energy effect that dies the moment its owner acts is not an effect.
- **Allies may still target it.** Untargetability is scoped to enemies, so Stealth never locks an operator out of its own team's repositioning or healing.

### 5.5 Evasion

- **Effect:** the **first** instance of Normal damage against the holder **each round** is negated on a `EvasionChance = 0.5` seeded roll. Every subsequent instance that round lands automatically.
- The charge refreshes at the holder's upkeep. "Round" therefore means *since the holder's last turn began*, which is the window during which opponents actually attack it.
- Atomic pierces it (§2.2).
- **Evasion negates damage, never movement.** If a collision's damage is evaded, the target still survives and the mover still bounces back (§7.2).

The per-round cap is load-bearing. Uncapped, a coin flip in a match with roughly six attacks against a target does not average out — it decides games, and it can eat a four-turn ultimate investment on a single roll.

### 5.6 Shield

- **Effect:** absorbs one entire instance of Normal damage, including a collision, then expires.
- Atomic ignores it.
- Granted by the `Shield` special space (ADR-0003). **Special spaces are deferred and not in the MVP**; the rule is defined here so the space is buildable when it lands.

This replaces the old "requires 2 hits to capture instead of 1" wording, which described a capture system that no longer exists.

### 5.7 Mark

- **Effect:** bookkeeping only; applies no modifier. Set by Tagged From Above, read by its payout condition (§10.2).
- Cleared on neutralize, on expiry, or when the payout fires.

---

## 6. Turn structure and resolution order

Exactly one operator moves per roll. Energy may be spent by any owned operator.

| Phase | What resolves |
|---|---|
| **1. Upkeep** | Bleed ticks (Atomic). Cooldowns advance. Evasion charge refreshes. Neutralize checks from bleed resolve here. |
| **2. Roll** | Dice rolled from the injected RNG. Energy granted (first roll of the turn only, §3.1). |
| **3. Action** | Deploy (if a 6, §1.3) and/or move one operator; spend energy on abilities with any owned operator; any order the player chooses. Collisions resolve immediately on landing (§7). Doubles → return to phase 2 **without** an energy grant. |
| **4. End** | Status durations expire. Win check. |

Expiry sits at End and application takes hold at the target's next turn, so a 1-turn stun applied during an opponent's turn correctly blocks the target's action phase before expiring.

`MaxRollsPerTurn = 3` (the initial roll plus two doubles) bounds turn length. Tunable.

**Movement:** `cells = floor(DiceTotal × EffectiveSpeed)`, where `EffectiveSpeed` is the operator's multiplier after auras and slows, floored at `MinSpeedMultiplier`.

**Speed band: 1.5 – 2.0**, in half-steps. `SpeedMultiplierMin = 1.0` and `SpeedMultiplierMax = 2.5` are the legal schema bounds; the alpha roster uses 1.5 and 2.0 only. The band was set by simulation, not by feel — see ADR-0002 Amendment 2. Two constraints fix it:

- **Below 1.5 the squad is gated by its slowest operator** and every match pays for it. A 1.0 Bouncer adds roughly three turns to the whole match while the other two wait.
- **Above 2.0 a single move stops being readable.** At 2.5×, a double-6 moves 30 cells — over half the loop in one action, on a board the player is meant to be able to follow.

---

## 7. Collision

### 7.1 Trigger

A collision occurs when an operator's movement **ends** on a cell occupied by an enemy operator.

It does **not** occur on: a safe cell (§4.4), a home column (§4.3), a cell occupied only by friendly operators (§4.5), passing *through* an occupied cell mid-move, or any form of forced movement (§7.3).

### 7.2 Resolution

1. The mover deals `CollisionDamage = 3`, type **Normal**, to the occupant, through the standard pipeline (§2.1).
2. **If the occupant is neutralized:** it goes to the yard (§1.2) and the mover takes the cell.
3. **If the occupant survives** — including via evasion or shield — **the occupant holds the cell and the mover is bounced back one step** along its own path.

The mover never takes damage. Collision is one-directional.

**Bounce-back** is placement, not movement: it triggers nothing — no second collision, no special space, no home entry. The destination is always the cell one step back along the track, which always exists for a deployed operator (the only cell an operator can occupy immediately after deploying is S, which is safe and therefore cannot be contested).

Because collision can only happen on a non-safe cell, and non-safe cells never hold more than one enemy, **a collision is always exactly 1v1**.

### 7.3 What collision damage means at 3

Nothing on the roster dies to a single collision. A 6-HP operator dies to a collision plus any prior scratch; Bouncer absorbs four.

That is deliberate. Collision is a **softening** mechanic that sets up ability kills, not a kill mechanic itself — the 70/30 combat-over-race priority expressed as a number. The consequence to watch is that the race layer is now close to non-lethal on its own. If playtest reads as toothless, `CollisionDamage` is the first dial; ability costs are the last.

### 7.4 Forced movement

Pull, push, and teleport effects **never collide** and never trigger cell effects. They are placement.

An operator pulled toward a puller is placed on the **last track cell between them** — adjacent to the puller, on the side it came from. This preserves "pulled toward" whether the target was ahead or behind, and guarantees two operators never co-occupy a contested cell as a side effect of an ability.

---

## 8. Win condition

A player wins when **all three of their operators have reached HOME**. Reaching HOME removes an operator from play permanently for that match — it cannot be targeted, moved, or returned.

Home entry is automatic on the MVP (ADR-0003). The opt-out-to-pursue flag is post-MVP.

---

## 9. Core services, commands, events

### 9.1 Services

Noun-based, per `CONVENTIONS.md`. Each owns one rule family and nothing else.

| Service | Owns |
|---|---|
| `TurnStateMachine` | Phase order (§6), turn rotation, roll budget |
| `EnergyLedger` | Generation, cap, spend, refusal on insufficient funds (§3) |
| `MovementResolver` | Dice → cells, deploy, path advance, home entry (§1.3, §6) |
| `CollisionResolver` | Landing contest, bounce-back (§7) |
| `TargetingRules` | Range along track, AOE windows, legality: stealth, home column, safe (§4) |
| `AbilityResolver` | Cost, cooldown, target validation, effect emission (§10) |
| `DamagePipeline` | The single choke point of §2.1 |
| `StatusRegistry` | Apply, query, expire; absolute-index timers (§5) |
| `WinConditions` | §8 |

Randomness reaches exactly two places: `MovementResolver` (dice) and `DamagePipeline` (evasion). Both take the injected seedable RNG. Nothing else in combat is random.

### 9.2 Commands (view → core)

`RollDiceCommand` · `DeployCommand` · `MoveCommand` · `UseAbilityCommand` · `EndTurnCommand`

### 9.3 Events (core → view)

`DiceRolled` · `EnergyGranted` · `EnergySpent` · `OperatorDeployed` · `OperatorMoved` · `CollisionResolved` · `DamageDealt` · `DamageEvaded` · `DamageAbsorbed` · `StatusApplied` · `StatusExpired` · `OperatorNeutralized` · `OperatorReachedHome` · `TurnEnded` · `GameWon`

`DamageEvaded` and `DamageAbsorbed` are separate events rather than a flag on `DamageDealt` because the view needs to play three visibly different things.

---

## 10. Alpha roster, re-expressed

Every ability below is fully expressible in the rules above. Nothing is deferred.

### 10.1 Bouncer — Tank

**HP 12 · Speed 1.5× · Range in path steps**

| # | Ability | Type | Cost | CD | Range | Effect |
|---|---|---|---|---|---|---|
| 1 | **Velvet Rope** | Active | 6 | 2 | 3 | Pull target to the cell adjacent to Bouncer (§7.4). Enemy: **3 Normal**. Ally: pull only, no damage. |
| 2 | **Intimidating Presence** | Passive | — | — | 2 | Enemies within range: speed multiplier **−0.5** (floor 0.5, §5.2). |
| 3 | **All-In Mauling** | Active | 6 | — | 1 | Enemy: **3 Normal** to target **and 3 direct to Bouncer** (§2.3). Ally: **heal 3**. |

Intimidating Presence is an aura, not a status: it is evaluated when an affected operator's movement is calculated, so there is no duration to track and no application event.

Bouncer's kit is priced on **positioning, not energy** — the roster's slowest operator with range 3 and range 1, so the real cost is the turns it takes him to be standing next to anyone. That makes him the most pool-efficient operator in the squad, which is a legitimate reason to run him.

He sits at 1.5 rather than 1.0 for a measured reason: the match ends when the *last* operator gets home, so a 1.0 tank taxes every match by roughly three turns while his squadmates idle. At 1.5 he is still visibly the slow one (25% behind the others) without gating the game.

### 10.2 Syla, The Blood Hound — Assassin

**HP 6 · Speed 2.0×**

| # | Ability | Type | Cost | CD | Range | Effect |
|---|---|---|---|---|---|---|
| 1 | **From the Hip** | Active | 3 | 1 | 3 | **2 Normal**; **Slow 1 turn**; **+1 damage** if the target is bleeding (§5.3). |
| 2 | **Ace Shards** | Active | 6 | 3 | 3 (AOE, self-origin) | **3 Normal** to all enemies in the window; applies **1 Bleed** each. |
| 3 | **Tagged From Above** | Active (Ult) | 9 | 2 | 3 | **Mark** an enemy. If it is neutralized by Syla's side within **3 of Syla's turns**, the whole squad gains **speed multiplier +0.5 for one round**. Syla gains **Stealth** for the current turn + 1, regardless of payout. |

The mark's payout credits *any* neutralize by Syla's side, including a collision. The stealth is unconditional and does not break on attacking (§5.4).

The payout was **+3** in the original roster. Against literal multipliers that produced a 35-cell turn — three-quarters of the loop, from one ability. At +0.5 the squad moves at 2.5× for a round (~17 cells each), which is a real tempo swing that a player can still read.

### 10.3 Kurbyn, DarkGrave — Brawler

**HP 6 · Speed 1.5× base (2.0× with passive)**

| # | Ability | Type | Cost | CD | Range | Effect |
|---|---|---|---|---|---|---|
| 1 | **Dargin Pulse** | Active | 6 | 3 | 2 (AOE, self-origin) | **2 Normal** to all enemies in the window; **Stun 1 turn** (§5.1). |
| 2 | **Evasive Protocol** | Passive | — | — | self | First Normal damage instance each round: **50% negated** (§5.5). Speed multiplier **+0.5**. |
| 3 | **Miracle Pull** | Active (Ult) | 9 | 2 | 1 | **3 Atomic** to the target. **Execute:** if the target was below 50% HP **at cast time**, it is instead neutralized outright. **2 Atomic** to enemies within 3 of the target, excluding the target. |

The execute threshold is evaluated **before** the direct damage lands, on the target's HP at cast. Checking after would mean a full-health 6-HP target drops to 3 and survives at exactly 50%, which reads as a bug at the table. `current * 2 < max` — integer comparison, no fractional HP support required anywhere in the core.

---

## 11. Superseded and removed

| Thing | Status |
|---|---|
| Tiered energy (≤4 → 1, 5–8 → 2, ≥9 → 3) | **Dead.** Replaced by §3.1. |
| "3 energy points per turn to spend" (GDD) | **Dead.** Never closed against 9-cost ultimates. |
| `Energy Efficiency` stat | **Cut.** One value on one operator, blank on two, no rule ever attached. |
| `Energy` as a per-operator stat | **Cut.** The pool is player-level. |
| Shield as "2 hits to capture" | **Rewritten** as §5.6 — the capture system it described no longer exists. |
| Ludo capture (land → instant send-home) | **Replaced** by collision (§7). |
| `[Range(3, 9)]` on `Operator.maxHealth` | **Dead.** Bouncer is 12. |
| Player elimination ("until one player is left") | **Not a mechanic** in the MVP (§1.2). |
| `MeshRenderer` fallback on `Operator` | Already dead (ADR-0001). |

---

## 12. Open items

These are dials and scope, not holes. Nothing here blocks implementation.

All numbers below were measured, not estimated. See ADR-0002 Amendment 2 and `tools/sim/` for the harness. Current measured baseline, 4 players on the Standard board with the alpha roster: **16.7 turns mean, p90 21**, ~8 neutralizes and ~33 abilities per match.

**Balance**

1. **Board occupancy is the real problem, and it isn't a speed problem.** All three of a player's operators are simultaneously on the board for only **10–15% of turns**. Deploy friction plus neutralize-to-yard keeps the track sparse, which starves a game that is 70% combat. The two dials are the deploy gate (item 2) and the setback (item 3).
2. **Deploy gate.** Requiring a 6 costs ~1.9 turns per match. Allowing deploy on **a 6 or any double** recovers it (11/36 → 16/36) and raises occupancy. Not adopted — it changes ADR-0003 — but it is the cheapest available fix if the board reads as empty.
3. **The yard setback is the most expensive single rule in the game.** Measured at 4P/Standard: neutralize → yard costs 6.4 turns per match; → start cell costs 3.0; → half progress costs 1.7. If matches run long or losing feels unrecoverable, soften this *before* touching damage numbers.
4. `CollisionDamage = 3` — first dial if the race layer reads as toothless (§7.3). Dropping ability damage from 3/2 to 2/1 cuts neutralizes by half and shortens matches by 3.4 turns, so damage and pacing are the same dial.
5. `EvasionChance = 0.5` — the per-round cap bounds the worst case; the rate itself is free to move.
6. `EnergyCap = 12` against a `floor(total/2)` drip — governs how often ultimates appear. ~50 energy per match is currently burned at the cap, almost all of it pre-contact in the opening turns.

**Scope**

7. **Special spaces** are deferred (ADR-0003). Shield is defined (§5.6); Teleport, Slippery, Checkpoint, RollAgain, and SharksTable are not. Note that Checkpoint conflicts with §1.2's "return to yard" and needs an explicit exception when it lands.
8. **"Brawler" is a fifth archetype** (Kurbyn) outside the base four. Add it or re-tag — cosmetic, unblocking.
9. **Six of nine operators unwritten.** They must be expressible in the rules above; a new operator needing a new *mechanic* gets an amendment to this doc, not a special case in its own stat block.
10. Flavour fields across the roster (`OPERATORS.md`).

---

## 13. Test matrix

Per `CONVENTIONS.md`: every rule ships with EditMode tests, named by behaviour, deterministic under a seed. A rule without a test isn't done.

**Energy — `EnergyLedger`**
- `DiceTotalOfNine_GrantsFourEnergy`
- `DiceTotalOfTwo_GrantsOneEnergy`
- `EnergyAboveTwelve_IsBurnedNotStored`
- `DoublesReroll_GrantsNoAdditionalEnergy`
- `AbilityCostExceedingPool_IsRejected`
- `EnergySpentByOneOperator_ReducesTheSharedPool`

**Movement and deploy — `MovementResolver`**
- `RollContainingSix_DeploysAndLeavesOtherDieAsMovement`
- `DoubleSix_DeploysTwoAndForfeitsMovement`
- `DoubleSix_StillGrantsRerollF`
- `RollWithoutSix_CannotDeploy`
- `DeployIsOptional_PlayerMayDeclineAndMoveFullTotal`
- `SlowedOperatorAtOneTimesSpeed_FloorsAtHalfMultiplier`

**Collision — `CollisionResolver`**
- `LandingOnEnemy_DealsThreeNormalDamage`
- `SurvivingOccupant_HoldsCellAndMoverBouncesBack`
- `NeutralizedOccupant_YieldsCellToMover`
- `LandingOnEnemyOnSafeCell_DoesNotCollide`
- `PassingThroughOccupiedCell_DoesNotCollide`
- `LandingOnFriendlyOperator_DoesNotCollide`
- `BounceBack_DoesNotTriggerSecondCollision`
- `EvadedCollisionDamage_StillBouncesMoverBack`
- `PulledOperator_DoesNotCollideOnArrival`

**Damage — `DamagePipeline`**
- `AtomicDamage_IgnoresShield`
- `AtomicDamage_IgnoresEvasion`
- `NormalDamage_IsFullyAbsorbedByShieldThenShieldExpires`
- `EvasionResolvesBeforeShield_AndPreservesTheShield`
- `SecondNormalInstanceInSameRound_IgnoresEvasion`
- `EvasionCharge_RefreshesAtOwnersUpkeep`
- `SelfDamage_BypassesEvasionAndShield`
- `SelfDamage_CanNeutralizeItsOwnCaster`

**Targeting — `TargetingRules`**
- `RangeIsCountedAlongTrack_NotEuclidean`
- `RangeCountsInBothDirections`
- `OperatorInHomeColumn_CannotBeTargeted`
- `OperatorInHomeColumn_CannotTarget`
- `OperatorOnSafeCell_CanStillBeTargetedByAbilities`
- `AoeWithinThree_CoversSevenCells`
- `MiraclePullSplash_ExcludesPrimaryTarget`

**Status — `StatusRegistry`**
- `StunAppliedOnOpponentTurn_BlocksTargetsNextTurn`
- `StunnedOperator_CannotMoveOrSpendEnergy`
- `StunnedOperator_RetainsPassiveEffects`
- `StunnedOperator_CanStillBePulled`
- `StunnedOperator_CooldownsStillTick`
- `BleedTicks_AtBleedingOwnersUpkeep`
- `BleedStack_IsRemovedAfterTicking`
- `BleedStacks_AreAdditive`
- `BleedCanNeutralize_BeforeTargetActs`
- `StealthedOperator_CannotBeTargetedByEnemy`
- `StealthedOperator_IsStillHitByAoe`
- `StealthedOperator_IsStillHitByCollision`
- `StealthedOperator_CanStillBeTargetedByAllies`
- `StealthPersists_AfterTheOwnerAttacks`
- `SlowFromMultipleSources_DoesNotStack`

**Abilities — `AbilityResolver`**
- `AbilityOnCooldown_IsRejected`
- `CooldownOfTwo_MakesAbilityUnusableForTwoOwnerTurns`
- `VelvetRopeOnAlly_DealsNoDamage`
- `VelvetRope_PlacesTargetAdjacentOnTheSideItCameFrom`
- `MiraclePull_ExecutesTargetBelowHalfHealthAtCastTime`
- `MiraclePull_DoesNotExecuteTargetAtExactlyHalfHealth`
- `FromTheHip_DealsBonusDamageToBleedingTarget`
- `TaggedFromAbove_PaysOutWhenMarkedTargetIsNeutralizedByCollision`
- `TaggedFromAbove_GrantsStealthEvenWithoutPayout`

**Neutralize and win — `WinConditions`**
- `NeutralizedOperator_ReturnsToYardAtFullHealth`
- `NeutralizedOperator_LosesAllStatusEffects`
- `NeutralizedOperator_LosesAllTrackProgress`
- `NeutralizedOperator_DoesNotReduceOwnersEnergyPool`
- `AllThreeOperatorsHome_WinsGame`
- `OperatorAtHome_CannotBeTargeted`

---

## Status history

- 2026-09-11 — Accepted (alpha). Unified capture and damage into one neutralize model; defined Atomic pierce, Stun, Stealth, Evasion, Shield, Bleed, Mark; cut Energy Efficiency and per-operator energy; replaced the energy economy; pinned targeting, AOE, resolution order, and collision; re-expressed the three alpha operators with zero TBDs.
- 2026-09-11 — Amended after simulation (ADR-0002 Amendment 2). Speed band set to 1.5–2.0 and roster restated (Bouncer 1.5, Syla 2.0, Kurbyn 1.5+0.5); Slow rescaled to −0.5; Tagged From Above's payout cut from +3 to +0.5; §12 balance items replaced with measured figures. Damage, energy, targeting, status and collision rules unchanged.
