# Nona Royale — Combat Systems

> Location in repo: `docs/design/COMBAT_SYSTEMS.md`
> Status: **Accepted (alpha).** Every mechanic the alpha three invoke is defined and built. §10.4 and §10.5 describe two operators who are draftable with an ability each still unbuilt; both carry banners. Open items in §12 are balance dials and post-MVP scope, not gaps.
> Date: 2026-09-12
> Related: `docs/design/OPERATORS.md` (roster), ADR-0002 (board size), ADR-0003 (topology), ADR-0004 (pure-C# core), `CONVENTIONS.md`, `docs/GDD.md`
> Supersedes: `docs/design/_HANDOFF_combat.md` (delete it)

Combat rules are **core logic**. Everything in this document lives in `NonaRoyale.Core`, references zero Unity types, is deterministic under an injected seed, and is reached through commands and reported through events (ADR-0004). Every numeric value here is **named config data in the core**, never an inline literal (`CONVENTIONS.md` → "Config, not literals").

---

## 0. The unifying rule

**There is exactly one way an operator leaves the board: it is _neutralized_.**

Ludo capture and HP damage are not two systems. Landing on an enemy is an attack that happens to be delivered by movement; it goes through the same damage pipeline as an ability, and it kills only if it reduces the target to 0 HP. HP is the single currency in the game.

This is the decision the rest of the document falls out of. It is also where the project's originality lives: ADR-0003 fixes the Ludo track 1:1 for _geometry_, and explicitly reserves the layer on top for mechanics like this one.

---

## 1. Health, neutralize, re-entry

### 1.1 Health

- Each operator has `MaxHealth` and a current HP.
- Damage **persists across turns**. There is no passive regeneration.
- HP is restored to full **only** on neutralize (§1.2). A wounded operator two cells from HOME is intended tension, not a problem to be smoothed.

Healing exists in two places and they are deliberately different. Bouncer's All-In Mauling heals an ally as the friendly half of a hostile ability; Javi's Nanite Infusion heals as its whole purpose. The tank's is incidental, the support's is a role.

The old `[Range(3, 9)]` cap on `Operator.maxHealth` is **dead**. The stat is unbounded in the core; presentation-layer sliders, if any, use 3–15. Note that the roster now happens to sit inside that old range again — Bouncer at 9 is the ceiling — which is coincidence, not a rule returning.

### 1.2 Neutralized

An operator is neutralized when its HP reaches 0 by any route (collision, ability, bleed, a mark tick, Miracle Pull's execute, or Bouncer's self-damage).

On neutralize:

|                |                                                                                                       |
| -------------- | ----------------------------------------------------------------------------------------------------- |
| Position       | Returns to its owner's **yard**                                                                       |
| Health         | Restored to `MaxHealth`                                                                               |
| Status effects | All cleared (stun, slow, bleed, stealth, shield, mark, haste)                                         |
| Cooldowns      | All reset to ready                                                                                    |
| Track progress | **Entirely lost** — it re-enters at its start cell                                                    |
| Energy         | Unaffected. The pool is player-level (§3) and a yarded operator costs the player nothing economically |

**Passives are not status effects and survive neutralize.** Kurbyn's Evasive Protocol is who he is, not something applied to him; an operator returning to the yard is still itself. The registry keeps passives in a store the clear does not touch, which makes this structural rather than something every caller has to remember.

**There is no permanent death in the MVP.** Nothing removes an operator from a match for good. The GDD line about play continuing "until there's only one player left" is an artifact of the same early pass that produced the 3-energy-per-turn economy; player elimination is not a mechanic. Neutralize is a setback measured in turns, not a removal.

### 1.3 Re-entry

A neutralized operator re-enters exactly as it originally deployed (ADR-0003):

- **A roll containing a 6 may deploy one operator**, consuming that die. Deployment is optional.
- **The other die is that turn's movement roll**, applied (× speed) to any one operator, including the one just deployed.
- **Each deploy consumes one die.** So a double 6 with only **one** operator waiting deploys that one and leaves the other 6 as the movement roll. Spending both dice to deploy a single operator would make a double 6 strictly worse than a single 6, which cannot be the intent.
- **Double 6 deploys two** operators and forfeits movement for that turn. It still grants the doubles re-roll (§6).
- The operator is placed on its colour's **start cell (S)**, which is a safe cell (§4.4) — so a deploy can never trigger a collision.

`DeployRequirement = 6`.

---

## 2. Damage

### 2.1 The damage pipeline

All damage — from abilities, collisions, bleed and marks alike — passes through one choke point in the core. Order is fixed:

1. **Legality.** Targeting was already validated (§4). Damage against an illegal target never reaches the pipeline.
2. **Evasion.** If the instance is **Normal** and the target has an unspent evasion charge this round, roll the seeded RNG at `EvasionChance`. On success: emit `DamageEvaded`, consume the charge, **stop**.
3. **Shield.** If the instance is **Normal** and the target holds a shield, the shield absorbs the entire instance, is consumed, emits `DamageAbsorbed`, **stop**.
4. **Apply.** Subtract from HP, emit `DamageDealt`.
5. **Neutralize check.** HP ≤ 0 → §1.2, emit `OperatorNeutralized`.

**Atomic damage skips steps 2 and 3.**

Evasion resolves before Shield deliberately: evasion is a reflex and should not burn a consumable the operator may need later.

**Every instance carries a cause** — "bleed", "mark", "collision", "ability", "execute", "self" — and no rule reads it. It exists so the view can say what happened. Upkeep damage is why: bleed and mark ticks land in a phase where nothing else moves, so without a stated cause an operator simply loses health and, if that was its last, vanishes with nothing on screen accounting for it.

### 2.2 Normal vs Atomic

- **Normal** is subject to every mitigation layer: Evasion, Shield, and anything added later.
- **Atomic** ignores all of it.

Atomic does **not** bypass _targeting_ protection. Safe cells, home columns, and Stealth are not defenses — they are reachability rules, and Atomic damage that cannot legally be aimed at an operator simply never enters the pipeline.

> The one-sentence version, for the table: **Atomic can't be blocked, but it can't reach what it can't touch.**

**Sources of Atomic:** Velvet Rope, bleed ticks, Ace Shards' bleed, mark ticks, all of Miracle Pull. Everything else — including collision — is Normal.

**Atomic is the roster's answer to Evasion, and it is deliberately concentrated.** Only two operators carry unblockable single-target damage: Bouncer with Velvet Rope at 6 energy, and Kurbyn with Miracle Pull at 9. Syla's route through Evasive Protocol is indirect — Ace Shards applies bleed, bleed ticks Atomic, and From the Hip pays a bonus against a bleeding target — which makes her anti-evasion play a two-ability sequence rather than a single cast. Mimi and Javi carry none at all.

That concentration was fine while every player fielded all three of the alpha roster. **It is a live question now that squads are drafted three from the pool:** a legal draw can produce a squad with no way through an evasive target at all. Nothing in the draft checks for it.

### 2.3 Self-damage

Self-inflicted damage (Bouncer's All-In Mauling) is applied **directly to HP**, bypassing the pipeline entirely. It cannot be evaded, shielded, or evaded-then-refunded, and it _can_ neutralize its own caster.

---

## 3. Energy

A single **shared pool per player**, not per operator. The tactical choice the pool creates — _which_ of my three operators spends this energy — is the point.

`Energy` as a per-operator stat is removed from the schema, alongside `Energy Efficiency` (§11).

### 3.1 Generation

```
EnergyGranted = floor(DiceTotal / 2)     // range 1–6, mean 3.5
```

- Granted **once per turn, on the first roll only.** Doubles grant an extra movement roll but never a second energy grant — otherwise a double snowballs both axes at once.
- `EnergyCap = 12`. Energy above the cap is burned, not stored. The cap is what forces spending; it is also exactly one ultimate plus nothing, so hoarding for an ult is a real, visible commitment.

Over a ~15-turn match a player sees roughly 52 energy for the whole squad: about three ultimates plus a handful of basics, or a steadier drip of cheap abilities. The superseded tiered rule (≤4 → 1, 5–8 → 2, ≥9 → 3) generated ~34 and left operators standing around; it is dead (§11).

**The drip is the real cooldown on anything costing 6 or more.** At a mean of 3.5 energy per turn, a 6-cost ability is naturally gated to roughly every second turn and a 9-cost ult to roughly every third, whatever its declared cooldown says. A stated cooldown only bites when it is _longer_ than what the economy already imposes — which means an ability whose identity is "castable every turn" has to cost 3 or less to actually be one, and a 3-cost ability is only limited at all if it carries a cooldown. Javi's Nanite Infusion at cooldown 2 and Mimi's Translocation at cooldown 4 are the two places on the roster where the declared cooldown, not the economy, is doing the work.

A `cooldownTurns: 0` on a 6-cost ability buys little against the drip alone, but it is not nothing at the cap, where a banked player can fire it on consecutive turns or twice in one (§10.1).

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
- **Miracle Pull**'s splash originates on the primary target and **excludes** that target — it already took the direct hit.
- **Cryo-Pulse** originates on the primary target and **includes** it. A splash that follows a direct hit excludes its target, or one ability strikes it twice; a field centred on a target it does not otherwise touch must include it, or the operator the player aimed at is the one enemy the ability misses. The two are separate scopes in the core, not a flag.
- **Nanite Infusion**'s splash originates on the primary target and finds the caster's **allies** — the only area in the game that looks for friends around an enemy.

### 4.3 Home columns are out of the fight

An operator in its home column **cannot be targeted and cannot target**. No abilities in, no abilities out, no AOE reach, no collisions.

This makes ADR-0003's home-entry safe cell redundant in the best way, and removes the entire class of "does AOE reach into home" questions. It is also one of the two bounds a swap is checked against (§7.4).

### 4.4 Safe cells

- The start cell (S) and, formally, the first home-column cell (H) are safe (ADR-0003).
- **No collision occurs on a safe cell.** A mover landing on a safe cell occupied by an enemy simply shares it. Both operators occupy; nothing resolves.
- Safe cells do **not** block abilities. An operator standing on S can be shot, pulled, stunned, marked, and bled. Safe means safe from _collision_, nothing more — otherwise S becomes a free parking space and the combat layer stalls.

### 4.5 Friendly stacking

Friendly operators may share any cell freely. There is no blocking mechanic in the MVP. (ADR-0003's `SharksTable` special space hints at a two-operators-on-a-cell mechanic; special spaces are deferred and it is not defined here.)

Because of this, an enemy _stack_ on a contested cell is reachable in ordinary play. What happens when a mover lands on one is §7.5.

---

## 5. Status effects

Durations are counted in **the affected operator's own turns**. An effect applied outside the target's turn takes hold on the target's next turn and expires at the end of it (for duration 1). Timers are stored as **absolute turn indices resolved at application time**, not decrementing counters — this removes the classic off-by-one and makes the boundary case a one-line test.

Statuses do not stack unless stated. Re-application refreshes duration and keeps the stronger magnitude, compared by absolute value because magnitudes are signed.

**Expiry sweeps the turn it is called in, not the turn after.** At End of turn _N_, everything whose last active turn is _N_ is removed. Queries made _during_ a turn are stricter — a status is still active throughout its final turn — because the two answer different questions at different moments. Getting this backwards reinstates exactly the off-by-one the absolute-index timers exist to remove.

**Speed is one summed channel.** Every status that affects movement carries its size as a signed magnitude — Slow −0.5, Hastened +0.5, Evasive Protocol +0.5 — and the registry sums them. That is what lets a passive carry a speed effect without a special case, and what makes a per-source slow strength expressible at all.

### 5.1 Stun

- **Effect:** the operator cannot move and cannot spend energy on its next turn.
- **Passives stay live.** A passive is who an operator is, not what it does.
- **Forced movement still works.** Velvet Rope pulls a stunned target normally, and Translocation swaps with one — being moved is not the target's action.
- **Cooldowns still tick.** They are timers, not actions.

### 5.2 Slow

- **Effect:** speed multiplier **−0.5** for the duration.
- `MinSpeedMultiplier = 0.5`. Sources do not stack; the largest applies.

The band is 1.0–1.5 (§6), so −0.5 costs a 1.5 operator a third of its movement and takes a 1.0 operator to the floor. **Slow is therefore sharper against the roster's slower operators than the original 1.5–2.0 band made it**, which is worth watching: a slowed Bouncer at 0.5× moves half of what the dice say. The penalty is a config dial (`SlowSpeedPenalty`) if that reads as hard control rather than friction.

> **This rule is contradicted by the code.** Slows from a status and from an aura are summed rather than resolved against each other. See §12, "Open, unresolved rules conflict".

### 5.3 Bleed

- **Effect:** each stack deals `BleedDamagePerStack = 1` **Atomic** damage at the **upkeep of the bleeding operator's owner's turn**, then that stack is removed. Bleed is delayed damage, not a lingering condition.
- Stacks are additive.
- An operator with at least one unspent stack counts as _bleeding_ for Syla's From the Hip bonus.
- Bleed can neutralize. An operator dying at upkeep never gets its turn.

### 5.4 Stealth

- **Effect:** the operator **cannot be selected as a single target by an enemy.** That is the whole of it.
- **Still affected by:** AOE, passive auras, collision damage, and bleed or marks already applied. Stealth hides you from being _aimed at_, not from the room.
- **Visible on the board.** The piece is never hidden from the opponent. This is a hot-seat digital board game; concealing a piece would mean building fog-of-war to service one ability and a UI that lies about the state.
- **Does not break on attacking.** A 9-energy effect that dies the moment its owner acts is not an effect.
- **Allies may still target it.** Untargetability is scoped to enemies, so Stealth never locks an operator out of its own team's repositioning or healing.

### 5.5 Evasion

- **Effect:** the **first** instance of Normal damage against the holder **each round** is negated on a `EvasionChance = 0.5` seeded roll. Every subsequent instance that round lands automatically.
- **A failed roll still spends the charge.** The charge is the _attempt_, not the success. If a miss left it intact, the holder would keep rolling against every hit until one landed, and the per-round cap — the thing that bounds the worst case — would stop binding at all.
- The charge refreshes at the holder's upkeep. "Round" therefore means _since the holder's last turn began_, which is the window during which opponents actually attack it.
- A cleanse does **not** re-arm it (§5.8).
- Atomic pierces it (§2.2).
- **Evasion negates damage, never movement.** If a collision's damage is evaded, the target still survives and the mover still bounces back (§7.2).

The per-round cap is load-bearing. Uncapped, a coin flip in a match with roughly six attacks against a target does not average out — it decides games, and it can eat a four-turn ultimate investment on a single roll.

### 5.6 Shield

- **Effect:** absorbs one entire instance of Normal damage, including a collision, then expires.
- Atomic ignores it.
- Granted by the `Shield` special space (ADR-0003). **Special spaces are deferred and not in the MVP.**

**Absorbing a whole instance regardless of its size is a timing lottery**, and it is tolerable only while the sole source is a rare board space. Javi's Carapace makes the shield castable, which means it has to become a **pool** with a per-ability value — predictable, and priceable. That change reworks the same pipeline branch the deterministic-evasion pass is rewriting, so the two land together. Until then Carapace is unbuilt (§10.5).

### 5.7 Mark

- **Effect:** `MarkDamagePerTurn = 2` **Atomic** damage at the **upkeep of the marked operator's owner's turn**, every turn the mark is active.
- **Ticking does not remove it.** This is the whole difference between a mark and bleed (§5.3): bleed is delayed damage that fires once and is gone, a mark is a lingering condition that bills every turn until its duration runs out. Applied by Tagged From Above for **2 turns**, so an undisturbed mark deals **4 total**.
- **Records who applied it.** The payout condition in §10.2 reads that source; the mark is the only status that carries one.
- Does not stack. Re-application refreshes the duration.
- Mark can neutralize. An operator dying at upkeep never gets its turn, exactly as with bleed.
- Cleared on neutralize, on expiry, when the payout fires, or by a cleanse (§5.8).

**The damage is deliberately sub-lethal.** Both 6-HP operators survive a full mark at 2 HP — inside collision range, inside Miracle Pull's execute window, and inside a From the Hip against a bleeding target. The mark's job is to _hand_ the kill to the marker's squad, which is the condition that pays out Tagged From Above (§10.2). At 3 per turn over 3 turns a mark would deal 9 and kill both 6-HP operators unassisted, which makes the ult's own payout condition self-fulfilling: 9 energy for a guaranteed kill, a guaranteed squad buff, and unconditional stealth, against a Miracle Pull at the same cost that needs the target already below half and needs Kurbyn within two cells. `MarkDamagePerTurn` is the first dial if the mark reads as toothless.

**A kill by a mark tick does satisfy "neutralized by Syla's side" (§10.2).** The damage is sourced from Syla and runs the standard pipeline. At 2 per turn it is rare, but the rule is stated rather than left to emerge from whatever the code happens to do.

### 5.8 Cleanse

Not a status — the absence of them. Javi's Neural Purge removes every **applied** status from an ally, and it is the first effect in the game that subtracts from the status registry rather than adding to it. That makes its edges rules rather than implementation details:

- **A cleansed mark is gone, and its payout goes with it.** The mark carries the source Tagged From Above reads (§5.7); removing the status removes the source. A 9-energy ultimate is answered for 6.
- **Statuses that have not taken hold yet go too.** A stun applied during an opponent's turn is not active until its target's next one, and a cleanse that could not reach it would be useless in the only window where it matters.
- **Passives survive**, as they survive neutralize (§1.2).
- **The evasion charge is untouched.** Neutralize clears it because the operator is leaving the board; a cleanse must not, or it would silently re-arm an ally's evasion mid-round as a benefit nobody asked the ability for.

### 5.9 Hastened

- **Effect:** speed multiplier **+`HasteSpeedBonus` (0.5)** for `HasteDurationTurns` (2) of the holder's own turns.
- Granted only by Tagged From Above's payout, to the marker's whole squad (§10.2).

**Two turns, not one.** The payout can fire on the marker's own turn — a collision or ability kill — by which point that turn's movement is usually already spent, so a 1-turn buff would routinely be worth nothing. At 2 it covers the remainder of the current turn and the whole of the next, whether it fired on the marker's turn or on an opponent's upkeep.

---

## 6. Turn structure and resolution order

Every die is consumed exactly once, by a deploy or by a move.\*\* Dice spent on movement may be pooled onto one operator or dealt one to each of two — or spent on the same operator in two separate steps. A die is forfeit only when no legal consumer exists for it. Energy may be spent by any owned operator and consumes no dice.

| Phase         | What resolves                                                                                                                                                                                                                                                                                                                                  |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **1. Upkeep** | Bleed ticks (Atomic), then mark ticks (Atomic). Cooldowns advance. Evasion charge refreshes. Neutralize checks from both damage sources resolve here, and a mark payout can fire here.                                                                                                                                                         |
| **2. Roll**   | Dice rolled from the injected RNG. Energy granted (first roll of the turn only, §3.1).                                                                                                                                                                                                                                                         |
| **3. Action** | Deploy (if an unspent 6, §1.3) and move until the roll is spent; spend energy on abilities with any owned operator; any order the player chooses. Deploying no longer has to precede moving. Collisions resolve immediately on landing (§7). Doubles → return to phase 2 **without** an energy grant, and only once the roll in hand is spent. |
| **4. End**    | Status durations expire. Win check.                                                                                                                                                                                                                                                                                                            |

**Movement is compulsory.** A turn cannot be ended, and a doubles re-roll cannot be taken, while any of the current player's operators could legally move with an unspent die. "Legally" excludes operators in the yard, stunned (§5.1), or already

> home, and excludes a die whose pips floor to zero cells at that operator's current speed — a die that can only move somebody nowhere has no consumer and is forfeit.
> Deploying stays optional (§1.3). A declined deploy leaves the 6 available as movement, so no die is stranded by declining.
> **Knowingly out of scope:** rolling is not compulsory. A player who never rolls forfeits both their energy grant and their movement, which is self-punishing enough that no rule is needed. Revisit if it ever becomes a real tactic.

                                                                                                                                 |

Bleed resolves before marks purely for determinism; nothing in the alpha roster distinguishes the order, but an unspecified order is a bug waiting for the first operator that cares.

Expiry sits at End and application takes hold at the target's next turn, so a 1-turn stun applied during an opponent's turn correctly blocks the target's action phase before expiring.

`MaxRollsPerTurn = 3` (the initial roll plus two doubles) bounds turn length. Tunable.

**Movement:** `cells = floor(Pips × EffectiveSpeed)`, where `Pips` is the sum of the dice being spent on this move — the whole unspent roll, or one die. `EffectiveSpeed` is the moving operator's multiplier after auras and slows, floored at `MinSpeedMultiplier`. **The floor applies per move, which is what splitting costs.** Two dice pooled lose at most one half-cell to it; spent separately they lose one each. The two coincide exactly when both dice are odd — 9 rolls in 36 — and at whole-number
speeds the loss is zero. **The larger cost is routing a die through a slower operator**, which forfeits that operator's whole speed deficit on those pips: a double six pooled onto a 1.5 operator moves 18, split between a 1.5 and a 1.0
operator moves 15.

**Speed band: 1.0 – 1.5**, in half-steps. `SpeedMultiplierMin = 1.0` and `SpeedMultiplierMax = 2.5` are the legal schema bounds; the roster uses 1.0 and 1.5 only. The band was set by simulation, not by feel — see ADR-0002 Amendment 4, which lowered it from the 1.5–2.0 adopted in Amendment 2. Two constraints fix it:

- **A slow operator no longer taxes the match** the way it did before opening deployments landed. The pacing cost that pushed Bouncer up to 1.5 in Amendment 2 was paid for elsewhere, which freed the tank to be genuinely the slow one again.
- **Above 1.5 a single move stops being readable.** The ceiling exists so a mean move stays near a fifth of the loop — the figure that actually governs whether a player can follow a piece across the board.

---

## 7. Collision

### 7.1 Trigger

A collision occurs when an operator's movement **ends** on a cell occupied by an enemy operator.

It does **not** occur on: a safe cell (§4.4), a home column (§4.3), a cell occupied only by friendly operators (§4.5), passing _through_ an occupied cell mid-move, or any form of forced movement (§7.4).

### 7.2 Resolution

1. The mover deals `CollisionDamage = 3`, type **Normal**, to the occupant, through the standard pipeline (§2.1).
2. **If the occupant is neutralized:** it goes to the yard (§1.2) and the mover takes the cell.
3. **If the occupant survives** — including via evasion or shield — **the occupant holds the cell and the mover is bounced back one step** along its own path.

The mover never takes damage. Collision is one-directional.

**Bounce-back** is placement, not movement: it triggers nothing — no second collision, no special space, no home entry. The destination is always the cell one step back along the track, which always exists for a deployed operator (the only cell an operator can occupy immediately after deploying is S, which is safe and therefore cannot be contested).

### 7.3 What collision damage means at 3

Nothing on the roster dies to a single collision. A 6-HP operator dies to a collision plus any prior scratch; Mimi at 5 dies to a collision plus two; Bouncer absorbs three.

That is deliberate. Collision is a **softening** mechanic that sets up ability kills, not a kill mechanic itself — the 70/30 combat-over-race priority expressed as a number. The consequence to watch is that the race layer is close to non-lethal on its own.

### 7.4 Forced movement

Pull, push, swap and teleport effects **never collide** and never trigger cell effects. They are placement, not movement.

**A pull** places its target on the **last track cell between it and the puller** — adjacent, on the side it came from. This preserves "pulled toward" whether the target was ahead or behind, and guarantees two operators never co-occupy a contested cell as a side effect of the pull itself.

**A swap** exchanges the caster's and the target's cells outright. It works on an ally or an enemy.

#### Placement is measured in cells, applied in progress

Each colour enters the circuit at its own start cell, so two operators can stand four cells apart while their progress values differ by forty. Every placement effect therefore computes the move as a **signed shift in cells** and applies that same shift to each operator's own progress. Without the conversion a placement would silently grant or cost most of a lap.

For a swap the target's shift is the **exact negation** of the caster's, not a second independent calculation. Two shortest-way-round calculations agree on direction at exactly half the circuit and would push both operators the same way.

#### Leaving the track

A placement can compute a destination that does not exist on the moved operator's own path. There are two such cases.

**Backwards, behind the start cell.** An operator's path does not extend behind its own start, so there is no cell there to occupy.

**Forwards, into the home column.** Progress at or beyond the track length is inside that operator's private home column, which no ability may reach into or out of (§4.3). A placement carrying an operator there would skip the remainder of the loop.

**A pull clamps at the start cell.** One operator moves, the start cell is safe (§4.4), and the clamp is the only legal placement available. A pull cannot reach the forwards case: it moves a target only _toward_ the puller, and the puller is itself on the circuit.

**A swap is refused, in both directions.** Clamping would not produce a swap at all — an operator whose new progress fell below zero would land on its own start cell rather than the caster's, which is a total progress wipe for the price of a cheap ability, and a harsher punishment than neutralizing it. Forwards is worse still. The ability is rejected before it is paid for and costs nothing.

> The general rule: **placement that moves one operator clamps; placement that moves two refuses.** A clamp that breaks the symmetry of a swap has stopped being the effect the player cast.

#### Consequences

- Placement onto an occupied cell is legal and resolves nothing. This is one of the three routes that produces a stacked cell (§7.5).
- A swap never leaves two operators contesting one cell by itself, since it is an exchange.
- Neither triggers a bounce-back, a special space, or home entry.
- A swapped or pulled operator keeps every status, all cooldowns and all health. Only its position changes.

### 7.5 Stacked occupants

**A contested cell can hold more than one enemy, and the mover strikes all of them.**

This corrects a claim §7.2 previously made — that a collision is always exactly 1v1, because a non-safe cell never holds more than one enemy. That is false, and it contradicts §4.5. Three ordinary sequences produce a stack:

- **Friendly stacking.** Two operators of the same colour may share any cell (§4.5). A third player landing there meets both.
- **Bounce-back.** A bounced mover is _placed_ one step back and triggers nothing (§7.2) — including no collision, so it may land on an occupied cell.
- **Forced movement.** A pull places its target without colliding, and a swap places two operators without colliding (§7.4). Either can drop an operator onto a cell an enemy already holds.

The rule:

1. The mover deals `CollisionDamage`, type **Normal**, to **every** enemy on the cell, each through the standard pipeline (§2.1).
2. **If every one of them is neutralized,** they all go to the yard and the mover takes the cell.
3. **If any survives** — by health, evasion or shield — **the survivors hold the cell and the mover is bounced back one step.**

The mover still takes nothing. Collision remains one-directional.

Striking the whole stack, rather than one occupant, was chosen over two alternatives. Picking a single victim needs a tie-break rule no player could predict at the table. Treating a stack as a Ludo-style blockade that cannot be landed on at all is a defensible game — but it is a _new mechanic_, not a clarification, and it would make stacking a purely defensive tool in a game whose stated priority is combat. Hitting everything makes a stack dangerous to stand in and dangerous to charge, which is the tension worth having.

> This rule was found by simulation, not by review. The invariant held for two years of design documents and failed in the first three hundred simulated matches.

---

## 8. Win condition

A player wins when **all three of their operators have reached HOME**. Reaching HOME removes an operator from play permanently for that match — it cannot be targeted, moved, or returned.

Home entry is automatic on the MVP (ADR-0003). The opt-out-to-pursue flag is post-MVP — see `_HANDOFF_opt_out_home_entry.md`.

---

## 9. Core services, commands, events

### 9.1 Services

Noun-based, per `CONVENTIONS.md`. Each owns one rule family and nothing else.

| Service             | Owns                                                                         |
| ------------------- | ---------------------------------------------------------------------------- |
| `TurnStateMachine`  | Phase order (§6), turn rotation, roll budget                                 |
| `EnergyLedger`      | Generation, cap, spend, refusal on insufficient funds (§3)                   |
| `MovementResolver`  | Dice → cells, deploy, path advance, home entry (§1.3, §6)                    |
| `CollisionResolver` | Landing contest, bounce-back (§7)                                            |
| `TargetingRules`    | Range along track, AOE windows, legality: stealth, home column, safe (§4)    |
| `AbilityResolver`   | Cost, cooldown, target validation, placement legality, effect emission (§10) |
| `DamagePipeline`    | The single choke point of §2.1                                               |
| `StatusRegistry`    | Apply, query, expire, cleanse; absolute-index timers (§5)                    |
| `AuraRules`         | Aura effects, evaluated on demand rather than stored (§10.1)                 |
| `NeutralizeRules`   | The §1.2 consequences, and the mark payout (§10.2)                           |
| `WinConditions`     | §8                                                                           |

**`StatusRegistry` reports damage, it never applies it.** The pipeline consults the registry for evasion and shields, so a registry that called the pipeline would close a dependency cycle. Bleed and mark ticks are therefore _queried_ — the registry says what the tick owes and the caller pushes it through the pipeline as Atomic. The registry decides what damage is owed, the pipeline decides how damage lands, and neither knows the other exists.

**An ability is a list of effects, and there are seven kinds:** Damage, Heal, ApplyStatus, PullToCaster, Execute, SwapWithCaster, RemoveStatuses. The resolver never branches on which ability is being cast. A new operator that cannot be expressed in those seven gets an amendment to this document and a new kind — never an `if`. Two have been added in earnest: the swap, for Mimi, and the cleanse, for Javi.

Randomness reaches exactly two places: `MovementResolver` (dice) and `DamagePipeline` (evasion). Both take the injected seedable RNG. Nothing else in combat is random.

### 9.2 Commands (view → core)

`RollDiceCommand` · `DeployCommand` · `MoveCommand` · `UseAbilityCommand` · `EndTurnCommand`

### 9.3 Events (core → view)

`DiceRolled` · `EnergyGranted` · `EnergySpent` · `OperatorDeployed` · `OperatorMoved` · `CollisionResolved` · `DamageDealt` · `DamageEvaded` · `DamageAbsorbed` · `HealApplied` · `StatusApplied` · `StatusExpired` · `OperatorNeutralized` · `OperatorReachedHome` · `TurnEnded` · `GameWon`

`DamageEvaded` and `DamageAbsorbed` are separate events rather than a flag on `DamageDealt` because the view needs to play three visibly different things. `DamageDealt` and `OperatorNeutralized` both carry a **cause** for the reason given in §2.1. What the view is required to do with all of it is `docs/design/PRESENTATION.md`.

---

## 10. The roster, re-expressed

The alpha three are complete and every ability they invoke is built. **§10.4 and §10.5 are not**: Mimi and Javi are in the draft pool with one ability each still unimplemented, and each section carries a banner saying which. An operator the game can deal but this document does not describe is worse than an entry marked incomplete — but the exception now covers two of five, and a third would mean the pool has become the place operators go to wait.

**Every ability carries a player-facing description** in the core, required by the constructor, and it contains no numbers. Cost, range, cooldown and damage all live on the same object; a figure repeated in prose is a second copy of a value that will be wrong the first time anyone tunes it.

**An ability with a hostile and a friendly mode picks its mode once, from who was targeted.** All-In Mauling's self-damage belongs to the _hostile_ cast: used on an ally it heals and costs the Bouncer nothing. The same rule governs Velvet Rope and Nanite Infusion. Deciding per _recipient_ rather than per _cast_ gives a nonsense answer for any effect aimed at the caster's own side, since the caster is always friendly to himself.

**An ability every one of whose effects is scoped away by the cast mode is refused**, and costs nothing. A cleanse aimed at an enemy was never a legal cast; without the check it would resolve, do nothing, and charge for it.

### 10.1 Bouncer — Tank

**HP 9 · Speed 1.0× · Range in path steps**

| #   | Ability                   | Type    | Cost | CD  | Range | Effect                                                                                               |
| --- | ------------------------- | ------- | ---- | --- | ----- | ---------------------------------------------------------------------------------------------------- |
| 1   | **Velvet Rope**           | Active  | 6    | 2   | 3     | Pull target to the cell adjacent to Bouncer (§7.4). Enemy: **3 Atomic**. Ally: pull only, no damage. |
| 2   | **Intimidating Presence** | Passive | —    | —   | 3     | Enemies within range: speed multiplier **−0.5** (floor 0.5, §5.2).                                   |
| 3   | **All-In Mauling**        | Active  | 6    | —   | 2     | Enemy: **2 Normal** to target **and 2 direct to Bouncer** (§2.3). Ally: **heal 3**.                  |

Intimidating Presence is an aura, not a status: it is evaluated when an affected operator's movement is calculated, so there is no duration to track and no application event.

Bouncer's kit is priced on **positioning, not energy** — the roster's slowest operator, so the real cost is the turns it takes him to be standing near anyone. That makes him the most pool-efficient operator in the squad, which is a legitimate reason to run him.

**He came down on all three axes at once after human play.** Health 12 → 9, Velvet Rope's range 4 → 3, All-In Mauling 3 → 2 damage with self-damage 1 → 2. Any one of those alone would have been measurable; together they are not separable, and if he now reads as weak the **reach is the first thing to restore** — it is the only one of the three that also governs what his aura can catch.

**Reach and aura are one kit.** Intimidating Presence at 3 equals Velvet Rope's reach again: everything he can rope is already slowed, and everything he ropes stays slowed once it arrives. They were briefly out of step when the rope went to 4 and back. If one moves, move the other.

**Velvet Rope is Atomic, which makes Bouncer the roster's direct counter to Evasion** (§2.2). This was a targeted answer to Kurbyn dominating early play, chosen over weakening Evasion itself: a counter preserves the rock-paper-scissors, a nerf flattens it. It is also the **only single-cast route through Evasive Protocol** — Syla's is a two-ability sequence — which is why shortening it is a larger change than the number suggests.

**The rope-into-Mauling one-turn kill is gone by design.** It was 3 Atomic plus 3, and six damage killed either 6-health operator from full for the price of a banked pool. At 3 plus 2 it leaves them at 1: a setup rather than an execution, and something the victim's owner gets a turn to answer. The zero cooldown still buys back-to-back casts at the cap, now for 4 self-damage against 9 health, which is most of what he can pay.

### 10.2 Syla, The Blood Hound — Assassin

**HP 6 · Speed 1.5×**

| #   | Ability               | Type         | Cost | CD  | Range                | Effect                                                                                                                                                                                                                                                  |
| --- | --------------------- | ------------ | ---- | --- | -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **From the Hip**      | Active       | 3    | 1   | 3                    | **1 Normal**; **Slow 1 turn**; **+1 damage** if the target is bleeding (§5.3).                                                                                                                                                                          |
| 2   | **Ace Shards**        | Active       | 6    | 3   | 3 (AOE, self-origin) | **3 Normal** to all enemies in the window; applies **1 Bleed** each.                                                                                                                                                                                    |
| 3   | **Tagged From Above** | Active (Ult) | 9    | 2   | 3                    | **Mark** an enemy for **2 turns**: **2 Atomic** at its upkeep each turn (§5.7). If it is neutralized by Syla's side **while marked**, the whole squad gains **Hastened** (§5.9). Syla gains **Stealth** for the current turn + 1, regardless of payout. |

**From the Hip is a control tool with a damage rider, not a damage ability.** At 1 base against 6 health it will not trade with anything on its own; the slow is the point, and the bleed bonus doubles it. That makes Syla's line explicitly sequential — Ace Shards first for the bleed, From the Hip after — rather than a cheap ability she can lead with. It was 2 base until the bleed profile proved strong enough that the base did not need to carry the ability.

The mark's payout credits _any_ neutralize by Syla's side, including a collision and including the mark's own ticks. The stealth is unconditional and does not break on attacking (§5.4).

**The payout window is the mark's own duration.** The ability previously specified "within 3 of Syla's turns", a second timer that duplicated the status's duration and counted against a different operator's turn index than the status registry does. One timer, stored on the mark, expiring at the marked operator's own upkeep like every other status.

**And it can be answered.** Javi's Neural Purge strips the mark for 6 energy, taking the payout with it (§5.8). A 9-cost ultimate that is cancelled by a 6-cost cleanse is a real counter-pick relationship, not an accident — but it is unmeasured, and it is the sharpest thing a drafted squad can do to her.

Two numbers here have been walked back under measurement. The squad buff was **+3** in the original roster; against literal multipliers that produced a 35-cell turn, three-quarters of the loop from one ability, and it is now +0.5. The mark was **pure bookkeeping** until 2026-09-12 — the ult cost 9 and did nothing whatsoever on the turn it was cast.

### 10.3 Kurbyn, DarkGrave — Brawler

**HP 6 · Speed 1.0× base (1.5× with passive)**

| #   | Ability              | Type         | Cost | CD  | Range                | Effect                                                                                                                                                                                              |
| --- | -------------------- | ------------ | ---- | --- | -------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Dargin Pulse**     | Active       | 6    | 3   | 2 (AOE, self-origin) | **2 Normal** to all enemies in the window; **Stun 1 turn** (§5.1).                                                                                                                                  |
| 2   | **Evasive Protocol** | Passive      | —    | —   | self                 | First Normal damage instance each round: **50% negated** (§5.5). Speed multiplier **+0.5**.                                                                                                         |
| 3   | **Miracle Pull**     | Active (Ult) | 9    | 2   | 2                    | **3 Atomic** to the target. **Execute:** if the target was below 50% HP **at cast time**, it is instead neutralized outright. **2 Atomic** to enemies within 3 of the target, excluding the target. |

The execute threshold is evaluated **before** the direct damage lands, on the target's HP at cast. Checking after would mean a full-health 6-HP target drops to 3 and survives at exactly 50%, which reads as a bug at the table. `current * 2 < max` — integer comparison, no fractional HP support required anywhere in the core.

Evasive Protocol carries the speed bonus, which makes Kurbyn's mobility **conditional on the passive being live** in a way no other operator's is. It is granted once at match start and, per §1.2, survives neutralize. A Kurbyn moving at 1.0 is a bug, not a balance state.

**Miracle Pull went from range 1 to 2** in the same pass that cut Bouncer. At range 1 the finisher needed him on the cell beside his target, which on a board where placement is mostly dice meant the ult was often unspendable at the moment it was worth spending. It also widens the splash's practical reach without touching its radius.

**Evasion made Kurbyn dominant in the first human sessions**, which is what prompted Velvet Rope becoming Atomic rather than any change here. Note that the tuning pass then shortened that counter and lengthened his ultimate — every change moved power the same way. Whether that is one correction or an overcorrection is a measurement, not an argument.

### 10.4 Mimi — Controller

**HP 5 · Speed 1.0× · Two of three abilities implemented**

> **Incomplete, and in the draft pool.** Cryo Field is designed and not built: it needs a status that damages an area at its holder's upkeep, and no such mechanic exists. She is draftable now, so this section describes what she actually does.
>
> Her damage is declared **Normal** and should be **Tech** (§12). That type does not exist and is blocked on shields having a real source; until it does, Tech and Normal behave identically, so the substitution changes no outcome and expresses none of her identity.

| #   | Ability           | Type   | Cost | CD  | Range                                          | Effect                                                                                                           |
| --- | ----------------- | ------ | ---- | --- | ---------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| 1   | **Cryo-Pulse**    | Active | 6    | 3   | 3 (AOE radius 2, target-origin, **inclusive**) | **2 Normal** to every enemy in the window, the target included; applies **1 Bleed** and **Slow 1 turn** to each. |
| 2   | **Cryo Field**    | Active | 6    | 3   | radius 2, 2 turns                              | _Not implemented._ Damage at her own upkeep to enemies near her.                                                 |
| 3   | **Translocation** | Active | 3    | 4   | 6                                              | **Swap** cells with the target, ally or enemy. Placement — collides with nothing, triggers nothing (§7.4).       |

**Five health is what prices her kit.** She is the only operator below 6, and she moves at the tank's speed, so she cannot run from anything. Ace Shards into a bleeding-bonus From the Hip kills her; so does Dargin Pulse into a collision — two-ability sequences every other operator survives.

**Cryo-Pulse is unmeasured and possibly stronger than its peer.** Against Dargin Pulse it is the same cost, radius and damage, but it originates on a target three cells away rather than on the caster, and applies two statuses rather than one. For a 1.0-speed operator, remote origin is most of the game. It was 4 energy in the first draft, which was off the tier and did more than either 6-cost area ability for two-thirds the price.

**Translocation's range 6 is the longest in the game**, and it is the only compensation a 5-health operator gets for being in a fight. Because progress moves one-for-one with cells, the range also bounds the swing: a swap shifts either operator by at most 6 cells of journey.

**Its cooldown is the whole limiter.** At 3 energy against a 3.5 drip both 3 and 4 gate to roughly every turn, so raising the cost would not have limited it — and 4 is off the tier besides. A cooldown longer than the economy imposes is exactly what §3.1 says a stated cooldown is for. This is also the cheapest denial tool in the design: swapping with an operator near its home mouth sends it backwards while you take its cell, which is a version of the play `_HANDOFF_opt_out_home_entry.md` prices at 3–6 energy as an entire new mechanic.

### 10.5 Javi — Support

**HP 6 · Speed 1.5× · Two of three abilities implemented**

> **Incomplete, and in the draft pool.** Carapace is designed and not built: a shield with a per-ability value needs the absorb layer reworked from a whole-instance bool to a pool (§5.6), which is the same pipeline branch the deterministic-evasion pass is rewriting. The two land together.

| #   | Ability             | Type   | Cost | CD  | Range | Effect                                                                                                           |
| --- | ------------------- | ------ | ---- | --- | ----- | ---------------------------------------------------------------------------------------------------------------- |
| 1   | **Nanite Infusion** | Active | 3    | 2   | 3     | Ally: **heal 2**. Enemy: **2 Normal**, and **heal 1** to every ally within 2 of the target, the caster included. |
| 2   | **Carapace**        | Active | 6    | 3   | 3     | _Not implemented._ Shield with a 2-point pool, 2 turns.                                                          |
| 3   | **Neural Purge**    | Active | 6    | 3   | 3     | Remove **every applied status** from an ally (§5.8). Passives untouched.                                         |

**He is the first operator who makes a target harder to kill**, which changes what the whole board is doing rather than adding to one side of it. Everything before him moved damage around; he removes it.

**Speed 1.5 with every ability at range 3.** A support who cannot reach the fight is a dead ability list, so he pays for his reach in fragility rather than in slowness.

**Heal 2, not 3.** Collision is 3, so a heal never fully undoes a hit — he blunts damage rather than erasing it, which is the difference between a support and an undo button. Cooldown 2 on a 3-cost ability is one of only two cooldowns on the roster that bind tighter than the economy (§3.1).

**The hostile mode is the interesting one.** Turned on an enemy it damages, and heals every ally within 2 of _that enemy_ — which is precisely where Ace Shards and Dargin Pulse punish a squad for standing. It pays for a commitment the rest of the roster charges for.

**Neural Purge was damage reduction until 2026-09-12**, and that design lost to Carapace on every axis: a flat 2-point shield absorbs more than a 50% cut, at half the cost and three times the range. A percentage also forces fractional health into a pipeline that has none — half of 3 is 1.5, and the rounding rule would have decided more than the design did. The cleanse gives him something no other operator has and no overlap with his own shield.

**He may be the operator that tips the game.** §12 records that neutralizing rewards the attacker with nothing, and suspects that suppresses combat in human play in a way the harness cannot detect, because the scripted player fights unconditionally. A dedicated healer makes kills materially harder to land. His measured strength depends entirely on which way that question goes, so settle it before trusting any figure about him.

---

## 11. Superseded and removed

| Thing                                                        | Status                                                                                                                          |
| ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------- |
| Tiered energy (≤4 → 1, 5–8 → 2, ≥9 → 3)                      | **Dead.** Replaced by §3.1.                                                                                                     |
| "3 energy points per turn to spend" (GDD)                    | **Dead.** Never closed against 9-cost ultimates.                                                                                |
| `Energy Efficiency` stat                                     | **Cut.** One value on one operator, blank on two, no rule ever attached.                                                        |
| `Energy` as a per-operator stat                              | **Cut.** The pool is player-level.                                                                                              |
| Shield as "2 hits to capture"                                | **Rewritten** as §5.6 — the capture system it described no longer exists.                                                       |
| Shield absorbing a whole instance whatever its size          | **Superseded in principle** by §5.6's pool, which is unbuilt. The current rule stands until it lands.                           |
| Mark as bookkeeping-only, applying no modifier               | **Rewritten** as §5.7 — it now deals damage over time.                                                                          |
| Tagged From Above's "within 3 of Syla's turns" payout window | **Replaced** by the mark's own duration (§5.7, §10.2).                                                                          |
| "A collision is always exactly 1v1" (§7.2)                   | **False.** Contradicted §4.5. Replaced by §7.5.                                                                                 |
| §7.4 covering pulls only                                     | **Extended** to swaps, with the cells-versus-progress conversion and the forwards case stated for the first time.               |
| The pull clamp as "owed a doc amendment"                     | **Discharged.** Stated in §7.4.                                                                                                 |
| Speed band 1.5–2.0 (Amendment 2)                             | **Lowered** to 1.0–1.5 by Amendment 4. Bouncer 1.5 → 1.0, Syla 2.0 → 1.5, Kurbyn 1.5+0.5 → 1.0+0.5.                             |
| Bouncer at 12 health                                         | **Lowered** to 9 (§10.1). He absorbed four collisions and shrugged off the sequence that kills everyone else.                   |
| Velvet Rope as 3 Normal at range 3                           | **Retuned** to 3 **Atomic** at range 4, then **back to range 3** (§10.1). Atomic stayed; the reach did not.                     |
| All-In Mauling at range 1 with 3 damage and 3 self           | **Retuned** to range 2, 2 damage, 2 self (§10.1).                                                                               |
| Miracle Pull at range 1                                      | **Widened** to 2 (§10.3). At 1 the ult was often unspendable when it was worth spending.                                        |
| Intimidating Presence at radius 2                            | **Widened** to 3, matching Velvet Rope's reach (§10.1).                                                                         |
| From the Hip at 2 base damage                                | **Lowered** to 1 (§10.2). The bleed profile carries the ability; the base does not.                                             |
| Cryo-Pulse at 4 energy                                       | **Repriced** to 6 (§10.4). Off the tier, and it did more than either 6-cost area ability.                                       |
| Javi's third ability as 50% damage reduction                 | **Replaced** by Neural Purge (§10.5). It duplicated Carapace, lost on cost, range and absorption, and needed fractional health. |
| An ability with no player-facing description                 | **Dead.** The constructor requires one and throws on an empty string (§10).                                                     |
| `CollisionDamage` as "the first dial"                        | **Withdrawn** (§12). Measured at 0.4 turns across a 2→6 range.                                                                  |
| Ludo capture (land → instant send-home)                      | **Replaced** by collision (§7).                                                                                                 |
| `[Range(3, 9)]` on `Operator.maxHealth`                      | **Dead**, and still dead — Bouncer at 9 is coincidence, not the rule returning.                                                 |
| Player elimination ("until one player is left")              | **Not a mechanic** in the MVP (§1.2).                                                                                           |
| `MeshRenderer` fallback on `Operator`                        | Already dead (ADR-0001).                                                                                                        |

---

## 12. Open items

These are dials and scope, not holes. Nothing here blocks implementation.

> **Every figure below is stale, and by more than it was.** They were measured before the mark gained damage, before Velvet Rope became Atomic, before the reach retunes, before Bouncer was cut on three axes, before Miracle Pull widened, and before two operators entered the draft pool. **Re-run `tools/sim/NonaRoyale.Sim` and replace these numbers before citing them.** Note also that adding an operator shifts the RNG stream, so figures either side of a roster change are not comparable even at the same seed.

**Last measured baseline** — Standard 48×1, band 1.0/1.5/1.5, `openingDeployments = 2`, alpha three, pre-roster-changes:

**19.6 turns, p90 24, 6.7 neutralizes, 43.3 abilities, 33% squad occupancy.**

**Nothing has ever measured a drafted squad.** The harness still fields the alpha three, so not one of Mimi's or Javi's four live abilities has run in a simulated match.

**Balance dials, ranked by effect per turn spent**

1. **Ability reach — the largest lever, and now partly spent and partly refunded.** Adding +1 to every range and radius bought **+47% neutralizes for under 1.5 turns** when measured. Since then Velvet Rope took +1 and gave it back, Intimidating Presence took +1 and kept it, and Miracle Pull took +1. The net is unmeasured.

2. **Opening deployments.** Adopted at 2. The only lever found that improves a problem at no cost elsewhere: occupancy roughly doubles and matches get _shorter_. `openingDeployments = 1` is retained for a more classic Ludo opening.

3. **Journey length.** The only thing that buys occupancy outright, and it costs pacing directly.

4. `EvasionChance = 0.5` — the per-round cap bounds the worst case; the rate itself is free to move. **Evasion drove Kurbyn's dominance in the first human sessions**, and the response was a targeted counter (§10.1) rather than touching this number — a counter that has since been shortened. If he is dominant again, restoring Velvet Rope's range comes before this dial.

5. `EnergyCap = 12` against a `floor(total/2)` drip — governs how often ultimates appear. Roughly 24 energy per match was burned at the cap, almost all of it pre-contact in the opening turns.

6. `MarkDamagePerTurn = 2` over a 2-turn duration — **unmeasured.** Set by reasoning: 4 total leaves a 6-HP target at 2, inside collision range and inside Miracle Pull's execute window, while 9 would kill unassisted and make the ult's payout self-fulfilling.

7. `SlowSpeedPenalty = 0.5` against the 1.0–1.5 band takes a 1.0 operator to the `MinSpeedMultiplier` floor (§5.2). Slow is harsher than it was under the earlier band and was not re-measured when the band moved. Intimidating Presence at radius 3, and Cryo-Pulse applying slow to a whole area, both make it land more often.

8. `HasteSpeedBonus = 0.5` and `HasteDurationTurns = 2` — **unmeasured.** The bonus was inherited from a cut made against the 1.5–2.0 band, where it was a ~25% bump; against 1.0–1.5 it is +33% to +50%.

**Struck**

- **`CollisionDamage` is not a dial.** Moving it from 2 to 6 changes match length by 0.4 turns and neutralizes by 1.1, because collisions occur only ~2.4 times a match on Standard. This section and ADR-0002 Amendment 2 both named it as the first lever if the race reads as toothless; that advice was wrong and is withdrawn.
- **The yard setback is not the most expensive rule.** The Python model showed it costing 6.4 turns per match at ~8 neutralizes. At the measured 6.7 across four players it fires under twice per player per match, and its contribution is far smaller than claimed.
- **Occupancy is no longer 10–15%.** It measured that under the old speed band. At the adopted band with opening deployments it is **33%**.

**Open, unresolved rules conflict**

- **Slows and auras stack, and §5.2 says they should not.** `GameEngine` sums two channels when computing effective speed — `StatusRegistry.SpeedModifier` and `AuraRules.SpeedModifierFor` — so From the Hip's slow and Bouncer's Intimidating Presence apply together. Within each channel the rule holds; across the two it does not.

  Both readings are defensible: an aura and a status are arguably different things, and a tank's presence compounding a wound is reasonable. But the doc says one thing and the code does another, which is the state this project exists to avoid. **Decide it.** The stakes rose again with Cryo-Pulse, which slows an entire area.

**Open, undecided**

- **The Tech damage type, and the shield source it waits on.** A four-type matrix — Normal, Force, Tech, Atomic, across Evasion and Shield — is the agreed model and is blocked on shields having a real source. Javi's Carapace is that source. Until both land, Mimi's whole identity is unexpressed and Force and Tech are indistinguishable from Atomic and Normal.
- **A draft has no balance constraint.** Nothing checks that a squad has an answer to Evasion, a way to heal, or reliable damage. With Atomic in two operators (§2.2), a legal draw can produce a squad with no way through Kurbyn.

**Open, unmeasured**

- **Neutralizing rewards the attacker with nothing**, which at four players makes killing a public good bought with private resources. Believed to suppress combat in human play in a way no simulation can detect, because the scripted player fights unconditionally. Javi makes this more pressing, not less. See `_HANDOFF_neutralize_rewards.md`.
- **Two-player matches are close to a pure race.** Pacing barely moves with seat count but combat scales hard: **0.6 neutralizes at two players against 4.0 at four**, measured under the older band. If 1v1 is meant to be a real mode it needs its own configuration, not just fewer seats.
- **Whether any of this is fun.** The harness reports pacing and throughput. It says nothing about whether the density reads as tension or as thinness. Only a human can.

**Scope**

1. **Special spaces** are deferred (ADR-0003). Shield is defined (§5.6); Teleport, Slippery, Checkpoint, RollAgain, and SharksTable are not. Checkpoint conflicts with §1.2's "return to yard" and needs an explicit exception when it lands.
2. **"Brawler" is a fifth archetype** (Kurbyn) outside the base four, and the base four are now filled. Add it or re-tag.
3. **Four of nine operators unwritten**, and two of the five written are incomplete. A new operator needing a new _mechanic_ gets an amendment to this doc, not a special case in its own stat block. Watch the Atomic concentration in §2.2.
4. **Opt-out of home entry** (ADR-0003, §8) — designed, deferred. Note the overlap with Translocation, which delivers a version of home denial for 3 energy; when opt-out lands, review the two together.
5. Flavour and world placement across the roster (`OPERATORS.md`).

---

## 13. Test matrix

Per `CONVENTIONS.md`: every rule ships with EditMode tests, named by behaviour, deterministic under a seed. A rule without a test isn't done.

**Energy — `EnergyLedger`**

- `DiceTotalOfNine_GrantsFourEnergy`
- `DiceTotalOfTwo_GrantsOneEnergy`
- `EnergyAboveTwelve_IsBurnedNotStored`
- `DoublesReroll_GrantsNoAdditionalEnergy`
- `EnergyIsGrantedAgain_OnTheOwnersNextTurn`
- `AbilityCostExceedingPool_IsRejected`
- `EnergySpentByOneOperator_ReducesTheSharedPool`

**Movement and deploy — `MovementResolver`**

- `RollContainingSix_DeploysAndLeavesOtherDieAsMovement`
- `DoubleSix_DeploysTwoAndForfeitsMovement`
- `DoubleSixWithOneOperatorWaiting_DeploysOneAndKeepsTheOtherSix`
- `RollWithoutSix_CannotDeploy`
- `DeployIsOptional_PlayerMayDeclineAndMoveFullTotal`
- `SlowedOperatorAtOneTimesSpeed_FloorsAtHalfMultiplier`
- `KurbynMovesAtHisPassiveSpeed_NotHisBaseSpeed`
- `TheTank_MovesAtItsBaseSpeed_WithNoPassiveToAdd`
- `KurbynRetainsEvasiveProtocol_AfterBeingNeutralized`

**Collision — `CollisionResolver`**

- `LandingOnEnemy_DealsThreeNormalDamage`
- `SurvivingOccupant_HoldsCellAndMoverBouncesBack`
- `NeutralizedOccupant_YieldsCellToMover`
- `LandingOnEnemyOnSafeCell_DoesNotCollide`
- `PassingThroughOccupiedCell_DoesNotCollide`
- `LandingOnFriendlyOperator_DoesNotCollide`
- `BounceBack_DoesNotTriggerSecondCollision`
- `LandingOnAStackOfEnemies_StrikesEveryOneOfThem`
- `AStackWithAnySurvivor_HoldsTheCell`
- `AStackWipedOut_YieldsTheCell`
- `EvadedCollisionDamage_StillBouncesMoverBack`
- `PulledOperator_DoesNotCollideOnArrival`

**Damage — `DamagePipeline`**

- `AtomicDamage_IgnoresShield`
- `AtomicDamage_IgnoresEvasion`
- `NormalDamage_IsFullyAbsorbedByShieldThenShieldExpires`
- `EvasionResolvesBeforeShield_AndPreservesTheShield`
- `SecondNormalInstanceInSameRound_IgnoresEvasion`
- `AFailedEvasionRoll_StillSpendsTheCharge`
- `EvasionCharge_RefreshesAtOwnersUpkeep`
- `SelfDamage_BypassesEvasionAndShield`
- `SelfDamage_CanNeutralizeItsOwnCaster`
- `EveryResult_CarriesTheCauseItWasGiven`

**Targeting — `TargetingRules`**

- `RangeIsCountedAlongTrack_NotEuclidean`
- `RangeCountsInBothDirections`
- `OperatorInHomeColumn_CannotBeTargeted`
- `OperatorInHomeColumn_CannotTarget`
- `OperatorOnSafeCell_CanStillBeTargetedByAbilities`
- `AoeWithinThree_CoversSevenCells`
- `MiraclePullSplash_ExcludesPrimaryTarget`
- `AnAlliesArea_FindsTheCasterWhenItStandsCloseEnough`

**Status — `StatusRegistry`**

- `StunAppliedOnOpponentTurn_BlocksTargetsNextTurn`
- `StunnedOperator_CannotMoveOrSpendEnergy`
- `StunnedOperator_RetainsPassiveEffects`
- `StunnedOperator_CanStillBePulled`
- `StunnedOperator_CooldownsStillTick`
- `APassiveSpeedBonus_ReachesTheSpeedModifier`
- `APassiveBonusAndASlow_ResolveAgainstEachOther`
- `AStrongerSlow_OverridesAWeakerOne`
- `ClearAll_StripsAppliedStatuses_ButKeepsPassives`
- `BleedTicks_AtBleedingOwnersUpkeep`
- `BleedStack_IsRemovedAfterTicking`
- `BleedStacks_AreAdditive`
- `BleedCanNeutralize_BeforeTargetActs`
- `MarkTicks_AtMarkedOwnersUpkeep`
- `MarkStack_SurvivesTicking`
- `MarkDamage_IsAtomic_AndIgnoresEvasion`
- `MarkCanNeutralize_BeforeTargetActs`
- `MarkExpiresAfterTwoTicks`
- `MarkRecordsItsSourceOperator`
- `StealthedOperator_CannotBeTargetedByEnemy`
- `StealthedOperator_IsStillHitByAoe`
- `StealthedOperator_IsStillHitByCollision`
- `StealthedOperator_IsStillTickedByAnExistingMark`
- `StealthedOperator_CanStillBeTargetedByAllies`
- `StealthPersists_AfterTheOwnerAttacks`
- `SlowFromMultipleSources_DoesNotStack`
- `ACleanse_StripsAppliedStatusesButNotPassives`
- `ACleanse_RemovesAStatusThatHasNotTakenHoldYet`
- `ACleanse_DoesNotReArmTheEvasionCharge`

**Abilities — `AbilityResolver`**

- `AbilityOnCooldown_IsRejected`
- `CooldownOfTwo_MakesAbilityUnusableForTwoOwnerTurns`
- `AnAbilityWithNoEffectForThisCastMode_IsRefusedAndCostsNothing`
- `VelvetRopeOnAlly_DealsNoDamage`
- `VelvetRope_PlacesTargetAdjacentOnTheSideItCameFrom`
- `VelvetRope_IsAtomic_AndIgnoresEvasion`
- `APullThatWouldGoBehindTheTargetsStartCell_ClampsThere`
- `AllInMaulingOnAlly_HealsAndCostsTheCasterNothing`
- `MiraclePull_ExecutesTargetBelowHalfHealthAtCastTime`
- `MiraclePull_DoesNotExecuteTargetAtExactlyHalfHealth`
- `FromTheHip_DealsBonusDamageToBleedingTarget`
- `TaggedFromAbove_PaysOutWhenMarkedTargetIsNeutralizedByCollision`
- `TaggedFromAbove_PaysOutWhenItsOwnMarkTickNeutralizesTheTarget`
- `TaggedFromAbove_DoesNotPayOutAfterTheMarkExpires`
- `TaggedFromAbove_GrantsStealthEvenWithoutPayout`
- `ACleansedMark_PaysOutNothingWhenTheTargetLaterFalls`
- `ASwap_ExchangesBothOperatorsCells`
- `ASwap_ShiftsEachOperatorByTheSameCellDistanceOnItsOwnPath`
- `ASwapThatWouldGoBehindAStartCell_IsRefused`
- `ASwapThatWouldEnterAHomeColumn_IsRefused`
- `ARefusedSwap_CostsNoEnergyAndNoCooldown`
- `ASwappedOperator_DoesNotCollideOnArrival`
- `CryoPulse_HitsThePrimaryTargetAsWellAsTheRingAroundIt`
- `NaniteInfusionOnEnemy_DamagesItAndHealsAlliesAroundIt`

**Roster — `Roster`**

- `AbilityIdsAreUnique`
- `EveryAbility_MatchesItsCostTier`
- `EveryAbility_HasADescription`
- `NoAbilityCostsMoreThanTheEnergyCap`
- `ADraftedSquad_HoldsDistinctOperators`
- `TheSameSeed_DraftsTheSameSquads`

**Neutralize and win — `WinConditions`**

- `NeutralizedOperator_ReturnsToYardAtFullHealth`
- `NeutralizedOperator_LosesAllStatusEffects`
- `NeutralizedOperator_RetainsItsPassives`
- `NeutralizedOperator_LosesAllTrackProgress`
- `NeutralizedOperator_DoesNotReduceOwnersEnergyPool`
- `AllThreeOperatorsHome_WinsGame`
- `OperatorAtHome_CannotBeTargeted`

---

## Status history

- 2026-09-11 — Accepted (alpha). Unified capture and damage into one neutralize model; defined Atomic pierce, Stun, Stealth, Evasion, Shield, Bleed, Mark; cut Energy Efficiency and per-operator energy; replaced the energy economy; pinned targeting, AOE, resolution order, and collision; re-expressed the three alpha operators with zero TBDs.
- 2026-09-11 — Amended after simulation (ADR-0002 Amendment 2). Speed band set to 1.5–2.0 and roster restated; Slow rescaled to −0.5; Tagged From Above's payout cut from +3 to +0.5; §12 balance items replaced with measured figures.
- 2026-09-11 — Amended during core implementation. Five rules the document did not cover were forced by writing the code and are now stated: deploy consuming one die each (§1.3); a failed evasion roll spending the charge (§5.5); expiry sweeping the turn it is called in (§5); the pull clamp at a target's own start cell (§7.4); and cast mode being chosen once from the target (§10). No existing rule changed.
- 2026-09-12 — Doc caught up to ADR-0002 Amendment 4. Speed band lowered to 1.0–1.5 and the roster restated; the band had been changed in the core and in the tests without the doc following. No rule changed here — a correction, not a decision.
- 2026-09-12 — Mark given damage over time. `MarkDamagePerTurn = 2` Atomic at the marked operator's upkeep, every turn, with Tagged From Above's duration cut 3 → 2 (§5.7, §6, §10.2). Mark was previously bookkeeping only, which left a 9-energy ultimate doing nothing on the turn it was cast. All-In Mauling retuned, and the energy drip documented as the real cooldown on anything costing 6 or more (§3.1).
- 2026-09-12 — Amended after the first simulation run against the live rules. §7.2's "a collision is always exactly 1v1" was false — it contradicted §4.5, and bounce-back and pulls reach the same state. Replaced by §7.5.
- 2026-09-12 — §12 rewritten against live-core measurements, replacing the Python model's figures. Reach identified as the largest balance lever; `CollisionDamage` struck as a dial; the yard setback demoted; occupancy corrected from 10–15% to 33%.
- 2026-09-12 — Roster corrections after the first human sessions; **code was authoritative and the doc had drifted behind it**. Velvet Rope became 3 Atomic at range 4, a targeted counter to Evasion chosen over weakening Evasion itself. Intimidating Presence 2 → 3. From the Hip 2 → 1 base damage. §2.2 gained a note on Atomic being concentrated in two operators.
- 2026-09-12 — **Placement extended to swaps.** `EffectKind` gains `SwapWithCaster`, the sixth kind and the first added since the core was written. §7.4 rewritten: placement is computed in cells and applied in progress, and the two ways a destination can leave an operator's own path are now stated — backwards behind the start cell, and forwards into the home column, which nobody had noticed until the swap arithmetic forced it. A pull clamps, a swap refuses. §4.2 gains the inclusive area scope, and §7.5 notes swaps as a third route to a stacked cell.
- 2026-09-12 — **Bouncer tuned down after human play**, on all three axes at once: 12 → 9 health, Velvet Rope range 4 → 3, All-In Mauling 3 → 2 damage with self-damage 1 → 2. Miracle Pull widened 1 → 2 (§10.3). The rope-into-Mauling one-turn kill is gone by design — six damage killed a 6-health operator from full, five leaves them at 1. §7.3 corrected from four collisions to three. Every change in the pass moved power the same way, and the rope is the only single-cast route through Evasive Protocol; if Kurbyn is dominant again, its range is the first thing to restore.
- 2026-09-12 — **Mimi and Javi added as §10.4 and §10.5**, both incomplete and both in the draft pool. `EffectKind` gains `RemoveStatuses`, the seventh kind and the first that subtracts from the status registry — §5.8 states its edges, including that a cleansed mark takes its payout with it. §5.9 documents Hastened, which had been granted by the payout without ever being defined. §5.6 records that the whole-instance shield is a timing lottery and becomes a pool when Carapace lands. Every ability now carries a required player-facing description (§10). §10's opening claim that nothing is deferred is retired: two of five operators have an unbuilt ability, and the banners say which.

- Up to two operator-moves per roll, and `MaxRollsPerTurn = 3`, so up to **six
  moves in a turn** against one to three before. Expect materially shorter
  matches.
- Landings per roll roughly double when players split, and a landing is the
  collision trigger (§7.1). `CollisionDamage` was restored as a dial in §12
  because that trigger started firing more often; this moves the same lever
  again, in the same direction.
- **Unmeasured.** `ScriptedPlayer` pools and never splits, by deliberate policy,
  so harness runs measure compulsory movement only. Splitting's effect on contact
  will not appear in any current figure.
- Measure against `CreateAlphaMatch`, and measure this **together with** the 52/6
  board (ADR-0002 Amdt 5), which pushed journey the other way. Read separately,
  each will be contaminated by the other.
