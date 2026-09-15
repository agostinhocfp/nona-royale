# Nona Royale — Combat Systems

> Location in repo: `docs/design/COMBAT_SYSTEMS.md`
> Status: **Accepted (alpha).** Every mechanic the alpha three invoke is defined and built. §10.4 and §10.5 describe two operators who are draftable with an ability each still unbuilt; both carry banners. Open items in §12 are balance dials and post-MVP scope, not gaps.
> Date: 2026-09-12
> Related: `docs/design/OPERATORS.md` (roster), ADR-0002 (board size, incl. Amendment 5), ADR-0003 (topology), ADR-0004 (pure-C# core), `CONVENTIONS.md`, `docs/GDD.md`
> Supersedes: `docs/design/_HANDOFF_combat.md` and `docs/design/_HANDOFF_split_movement.md` (delete both)

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
- **Every die a deploy does not consume is movement**, spent under §6 — pooled onto one operator or dealt between two.
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
- Costs are free integers; passives are free. Per-ability costs in §10. **The 3 / 6 / 9 tier was abolished 2026-09-13** — three abilities had been priced off it on their own merits, and a rule overridden every time it binds makes its exceptions look like oversights. A cost is now argued against its peers in the operator file. The only remaining constraint is that a cost above the energy cap is unspendable and therefore invalid.

---

## 4. Targeting and range

### 4.1 Distance is measured along the track

Range is counted in **path steps along the circuit, in either direction**. Never Euclidean, never grid-adjacency.

Two cells can sit physically beside each other across the board's centre and be 26 steps apart — half the 52-cell circuit, the furthest any two cells can be. Allowing abilities to cross that gap would make the track — the only topology the game has — meaningless.

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
- **Safe cells and abilities — amended twice.** The original rule: safe cells do **not** block abilities; safe means safe from _collision_, nothing more — otherwise S becomes a free parking space and the combat layer stalls. Both amendments below deliberately moved off that position. The original reasoning stays on the page because the free-parking risk it named is real — the second amendment exists to pay for the first.
- **First amendment (2026-09-13): a safe cell refuses enemy single-targeting.** An operator standing on S cannot be picked out by an enemy single-target ability. The counterweight the original rule did not weigh: winning needs all three operators home, so an operator parked on a start cell is an operator not winning. Scoped to enemies exactly as stealth is (§5.4) — an ally can still be healed, plated, cleansed or repositioned while standing on one — and single-target only: areas, lines, beacons and zones all still reach the cell. A safe cell stops somebody picking you out; it does not stop a blast.
- **Second amendment (2026-09-14): the camping rule.** An operator standing on a safe cell may not **aim behind itself**. Refused with `AimedBehindFromSafeCell`: enemy single-targets, cell aims (Drone Strike, Killzone), and placements at allies — any ability containing a pull, swap, push or dash, today exactly Velvet Rope, Translocation and Collision, which closes the safe-cell taxi. Still legal: heals, plates and cleanses at allies behind, because support is not the aggression this rule exists to stop; and self-origin areas, which radiate backwards unconsulted, because presence is not an aim. **"Behind" is the direction of travel, never progress** — a forward track offset greater than half the circuit. Progress is per-colour and meaningless to compare across seats. On an even circuit the exact-opposite cell counts as _ahead_, so the rule never blocks more than what is strictly behind. This amendment is the first one's price: a shelter that is single-target-proof must not also be an artillery position.

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
- **A stunned operator is not a legal consumer of a die** (§6). If it is the only operator that could otherwise move, the roll is forfeit and the turn can be ended.

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

- **Effect:** the **first** instance of Normal damage against the holder **each round** is negated on a `EvasionChance = 0.3` seeded roll. Every subsequent instance that round lands automatically.
- **A failed roll still spends the charge.** The charge is the _attempt_, not the success. If a miss left it intact, the holder would keep rolling against every hit until one landed, and the per-round cap — the thing that bounds the worst case — would stop binding at all.
- The charge refreshes at the holder's upkeep. "Round" therefore means _since the holder's last turn began_, which is the window during which opponents actually attack it.
- A cleanse does **not** re-arm it (§5.8).
- Atomic pierces it (§2.2).
- **Evasion negates damage, never movement.** If a collision's damage is evaded, the target still survives and the mover still bounces back (§7.2).

The per-round cap is load-bearing. Uncapped, a roll across the half-dozen attacks a target sees in a match does not average out — it decides games, and it can eat a four-turn ultimate investment on a single roll.

**Splitting a roll raises the number of attacks a round has to absorb.** Two landings per roll means up to two collisions where there was one (§6, §7.1), and the cap bites on the first of them only. That makes the second landing of a split strictly more likely to connect than the first, which is a real tactic and an unmeasured one.

### 5.6 Shield

- **Effect:** absorbs one entire instance of Normal damage, including a collision, then expires.
- Atomic ignores it.
- Granted by the `Shield` special space (ADR-0003). **Special spaces are deferred and not in the MVP.**

**Absorbing a whole instance regardless of its size is a timing lottery**, and it is tolerable only while the sole source is a rare board space. Javi's Trauma Plate makes the shield castable, which means it has to become a **pool** with a per-ability value — predictable, and priceable. That change reworks the same pipeline branch the deterministic-evasion pass is rewriting, so the two land together. Until then Trauma Plate is unbuilt (§10.5).

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

### 5.10 ZeroDayCharge

- **Effect:** none. A pure marker — the visible half of Zero-Day's attached charge (§6.4), present so the attachment is something the rules can see and remove.
- **A cleanse strips it, and the charge never detonates.** The deferred effect checks for the marker at the owner's upkeep; a target still in play without it means the charge was answered, and it cancels silently (§5.8's rule — removing the status removes what the status carried — applied to a delayed effect for the first time).
- **Duration 2, derived, not chosen.** The earliest a charge can detonate is the owner's next upkeep; a 1-turn marker applied during the owner's own turn would expire at the end of the target's intervening turn — before that upkeep — and a charge that cancelled itself on schedule would read as a cleanse that never happened. Two turns keeps the marker alive through every legal detonation window, and its expiry is otherwise harmless: a charge that has fired is gone whatever the registry says.

---

## 6. Turn structure and resolution order

**Every die is consumed exactly once, by a deploy or by a move.** Dice spent on movement may be pooled onto one operator or dealt one to each of two — or spent on the same operator in two separate steps. A die is forfeit only when no legal consumer exists for it. Energy may be spent by any owned operator and consumes no dice.

| Phase         | What resolves                                                                                                                                                                                                                                                                                                                                  |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **1. Upkeep** | Bleed ticks (Atomic), then mark ticks (Atomic). Cooldowns advance. Evasion charge refreshes. Neutralize checks from both damage sources resolve here, and a mark payout can fire here.                                                                                                                                                         |
| **2. Roll**   | Dice rolled from the injected RNG. Energy granted (first roll of the turn only, §3.1).                                                                                                                                                                                                                                                         |
| **3. Action** | Deploy (if an unspent 6, §1.3) and move until the roll is spent; spend energy on abilities with any owned operator; any order the player chooses. Deploying no longer has to precede moving. Collisions resolve immediately on landing (§7). Doubles → return to phase 2 **without** an energy grant, and only once the roll in hand is spent. |
| **4. End**    | Status durations expire. Win check.                                                                                                                                                                                                                                                                                                            |

### 6.1 Movement is compulsory

A turn cannot be ended, and a doubles re-roll cannot be taken, while any of the current player's operators could legally move with an unspent die. "Legally" excludes operators in the yard, stunned (§5.1), or already home.

It also excludes a roll whose **pooled** pips floor to zero cells for every operator that could move. Pooling is always available and never moves fewer cells than a single die, so if the pool cannot move anybody, nothing can — and a die that can only move somebody nowhere has no consumer and is forfeit. This is the one rule that guarantees a turn is always endable; without it a heavy slow could deadlock the match.

Deploying stays optional (§1.3). A declined deploy leaves the 6 available as movement, so no die is stranded by declining.

**Knowingly out of scope:** rolling is not compulsory. A player who never rolls forfeits both their energy grant and their movement, which is self-punishing enough that no rule is needed. Revisit if it ever becomes a real tactic.

### 6.2 Resolution notes

Bleed resolves before marks purely for determinism; nothing in the alpha roster distinguishes the order, but an unspecified order is a bug waiting for the first operator that cares.

Expiry sits at End and application takes hold at the target's next turn, so a 1-turn stun applied during an opponent's turn correctly blocks the target's action phase before expiring.

`MaxRollsPerTurn = 3` (the initial roll plus two doubles) bounds turn length. Tunable. Combined with splitting, that is up to **six operator-moves in a turn** against one to three before — see §12.

### 6.3 Movement arithmetic

**Movement:** `cells = floor(Pips × EffectiveSpeed)` at 1.0× and above; **below 1.0×, half cells round up** (amendment, 2026-09-15). `Pips` is the sum of the dice being spent on this move — the whole unspent roll, or one die. `EffectiveSpeed` is the moving operator's multiplier after auras and slows, floored at `MinSpeedMultiplier`.

**Amendment (2026-09-15): below 1.0×, the half cell rounds up.** Sanity is the first operator who lives under 1.0× permanently, and the floor taxed him twice — once by the multiplier, then again on every odd die: a 5 always moved 2, never 3. For a fast operator the floored half is a rounding tax on a long move; for the slowest operator ever fielded it is half of everything he has. So below 1.0× the half rounds up: a 5 moves 3, a 7 moves 4. The rule is general — anyone slowed to 0.5× gets the same grace — but 1.5× is deliberately untouched: at or above 1.0× a half-step multiplier still never gifts a cell, and the player can still halve the roll in their head. Same family as the 2026-09-14 "a spent die always moves at least one cell" clamp: slows may shrink a move, never erase one — and now, never tax the odd die twice.

**The rounding applies per move, which is what splitting costs.** Two dice pooled lose at most one half-cell to it; spent separately they lose one each. So splitting costs a **whole cell exactly when both dice are odd** — 9 rolls in 36 — and nothing otherwise. At whole-number speeds it costs nothing at all.

**The larger cost is routing a die through a slower operator**, which forfeits that operator's whole speed deficit on those pips: a double six pooled onto a 1.5 operator moves 18; split between a 1.5 and a 1.0 operator it moves 15. That is three cells, not one, and it is why the landing preview must show every option before one is chosen (§9.1).

**Speed band: 1.0 – 1.5**, in half-steps. `SpeedMultiplierMin = 1.0` and `SpeedMultiplierMax = 2.5` are the legal schema bounds; the roster uses 1.0 and 1.5 only. The band was set by simulation, not by feel — see ADR-0002 Amendment 4, which lowered it from the 1.5–2.0 adopted in Amendment 2. Two constraints fix it:

- **A slow operator no longer taxes the match** the way it did before opening deployments landed. The pacing cost that pushed Bouncer up to 1.5 in Amendment 2 was paid for elsewhere, which freed the tank to be genuinely the slow one again.
- **Above 1.5 a single move stops being readable.** The ceiling exists so a mean move stays near a fifth of the loop — the figure that actually governs whether a player can follow a piece across the board.

**The band has one recorded exception.** Sanity fields at 0.5 — below the floor, permanently on `MinSpeedMultiplier`, and therefore immune to every slow and aura in the game as a side effect. That is a deliberate designer override (2026-09-15), recorded with its reasoning at §10.8; it does not reopen the band for anyone else.

### 6.4 Operator-anchored deferred effects

A beacon is anchored to a **cell** (§9.1, `DeferredCellEffects`): it fires at the caster's next upkeep on whoever is standing there then. Zero-Day introduces the twin: a charge anchored to an **operator**. The rules, parallel to the beacon's wherever the two can be:

- **It follows the target.** The detonation cell is wherever the target stands at the owner's next upkeep — moved, pulled, swapped or dashed, the charge goes with it. A beacon is a bet on where somebody will be; an attached charge is a delayed certainty that somebody will be struck wherever they go.
- **Timing is the owner's next upkeep**, exactly as a beacon's, and it resolves in the same upkeep window.
- **The marked target is hit hardest.** Everyone in the blast takes the splash; the operator carrying the charge takes the splash plus a bonus. The bonus has no recipient if the carrier is already dead.
- **Death does not disarm it, on either side.** If the target is neutralized before the detonation, the charge goes off on the death cell — the blast still happens, only the bonus is lost. And the charge is keyed to the owning *seat's* upkeep, not to the operator who threw it: Sanity in his yard changes nothing, exactly as a deployed beacon outlives its caster (ADR-0006). A kill still credits the recorded source.
- **Re-attaching replaces.** A second charge from the same seat onto the same target refreshes the pending one rather than stacking — the same shape as re-painting a cell or re-applying a status.
- **It is telegraphed and it can be answered.** Attachment applies the ZeroDayCharge marker (§5.10) and emits its own event, so the view can show the grenade riding the target. A cleanse strips the marker and cancels the detonation outright — the counterplay is the marker, not the blast.
- **Kill credit follows the source.** Damage at detonation is attributed to the operator who attached the charge, through the same neutralize fold as everything else.

**Known limitation:** charges from two different seats on one target share one marker — the registry stores one entry per status kind per operator. The first detonation consumes it, and the second charge then reads as cleansed. A four-seat edge the design has not needed to answer; recorded rather than solved.

---

## 7. Collision

### 7.1 Trigger

A collision occurs when an operator's movement **ends** on a cell occupied by an enemy operator.

It does **not** occur on: a safe cell (§4.4), a home column (§4.3), a cell occupied only by friendly operators (§4.5), passing _through_ an occupied cell mid-move, or any form of forced movement (§7.4).

**A split roll produces two landings, and therefore up to two collisions** (§6). This is the single largest downstream consequence of split movement, and it is unmeasured — see §12.

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

### 7.6 Dash

A dash moves the **caster** along the track to a chosen target and through whatever lies between — the first placement effect that repositions the caster without swapping (§7.4), added for Collision (§10.8).

- **Direction is the shortest way round to the target**, either way along the loop. A tie — a target exactly half the circuit away — resolves **forwards**, as does the degenerate case of a target on the caster's own cell: a dash that cannot pick a direction picks the direction of travel.
- **The path rakes, it does not contest.** Every enemy standing on a traversed cell — from one step out to the target's own cell inclusive — takes the dash's path damage through the standard pipeline. The caster passes through occupants without colliding (§7.1: a collision is a landing, and the dash's only landing is its final placement). Allies on the path are untouched, and the target itself is not path damage's business — what happens to the target is declared as ordinary effects on the ability, filtered by cast mode like anything else.
- **The landing is one step past the target** along the dash direction — "a cell behind it". If that cell is occupied, the caster lands **one step short** instead, on his own side of the target. The fallback is not refused and not searched further: a second occupant there simply stacks, which placement already allows (§7.4, consequences).
- **It is placement throughout.** No collision, no cell effects, no bounce-back, no home entry — and the camping rule applies, because the ability contains a placement (§4.4, second amendment): a sheltered engineer may not dash to an ally behind himself.
- **One operator moves, so it clamps** (§7.4's general rule). A landing computed past the home mouth clamps to the last track cell; one behind the start clamps to the start cell. The dash never carries anyone into a home column, its own included.

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
| `MovementResolver`  | Pips → cells, deploy, path advance, home entry (§1.3, §6)                    |
| `CollisionResolver` | Landing contest, bounce-back (§7)                                            |
| `TargetingRules`    | Range along track, AOE windows, legality: stealth, home column, safe (§4)    |
| `AbilityResolver`   | Cost, cooldown, target validation, placement legality, effect emission (§10) |
| `DamagePipeline`    | The single choke point of §2.1                                               |
| `StatusRegistry`    | Apply, query, expire, cleanse; absolute-index timers (§5)                    |
| `AuraRules`         | Aura effects, evaluated on demand rather than stored (§10.1)                 |
| `NeutralizeRules`   | The §1.2 consequences, the mark payout (§10.2), and the kill bounty          |
| `WinConditions`     | §8                                                                           |

**`MovementResolver` knows nothing about how many dice a move spends.** It takes a pip count, so a pooled two-die move and a single-die move are the same call with different arguments. Which dice are unspent, and whether the roll is owed, is `GameEngine`'s state — that is why splitting needed no change to the resolver at all.

**`StatusRegistry` reports damage, it never applies it.** The pipeline consults the registry for evasion and shields, so a registry that called the pipeline would close a dependency cycle. Bleed and mark ticks are therefore _queried_ — the registry says what the tick owes and the caller pushes it through the pipeline as Atomic. The registry decides what damage is owed, the pipeline decides how damage lands, and neither knows the other exists.

**An ability is a list of effects, and there are twelve kinds:** Damage, Heal, ApplyStatus, PullToCaster, Execute, SwapWithCaster, RemoveStatuses, PushFromCaster, PaintCell, DeployZone, AttachCharge, DashToTarget. The resolver never branches on which ability is being cast. A new operator that cannot be expressed in those twelve gets an amendment to this document and a new kind — never an `if`. Seven have been added in earnest: the swap for Mimi, the cleanse for Javi, the push and the two cell-anchored deferred kinds for Kian and Nuetu, and the operator-anchored charge and the dash for Sanity (§10.8).

**The engine reports every move a roll could make, not just one.** `PreviewLandings` returns, per operator, the pooled landing and one per distinct unspent face. A preview that showed only the pooled option would hide exactly the choice §6.3 prices, and the view must not compute any of it itself (`PRESENTATION.md` §1).

Randomness reaches exactly two places: `MovementResolver` (dice) and `DamagePipeline` (evasion). Both take the injected seedable RNG. Nothing else in combat is random.

### 9.2 Commands (view → core)

`RollDiceCommand` · `DeployCommand` · `MoveCommand` · `UseAbilityCommand` · `EndTurnCommand`

`MoveCommand` carries an optional die face. Null means pool every unspent die onto this operator; a face means spend that one die. `EndTurnCommand` is rejected while §6.1 is unsatisfied.

### 9.3 Events (core → view)

`DiceRolled` · `EnergyGranted` · `EnergySpent` · `OperatorDeployed` · `OperatorMoved` · `CollisionResolved` · `DamageDealt` · `DamageEvaded` · `DamageAbsorbed` · `HealApplied` · `StatusApplied` · `StatusExpired` · `OperatorNeutralized` · `OperatorReachedHome` · `TurnEnded` · `GameWon` · `BeaconPlaced` · `BeaconFired` · `ZoneDeployed` · `ZoneTicked` · `ZeroDayAttached` · `ZeroDayDetonated`

`DamageEvaded` and `DamageAbsorbed` are separate events rather than a flag on `DamageDealt` because the view needs to play three visibly different things. `DamageDealt` and `OperatorNeutralized` both carry a **cause** for the reason given in §2.1. What the view is required to do with all of it is `docs/design/PRESENTATION.md`.

---

## 10. The roster, re-expressed

The alpha three are complete and every ability they invoke is built. **§10.4 and §10.5 are not**: Mimi and Javi are in the draft pool with one ability each still unimplemented, and each section carries a banner saying which. An operator the game can deal but this document does not describe is worse than an entry marked incomplete — but the exception now covers two of six, and a third would mean the pool has become the place operators go to wait. Sanity (§10.8) is complete: all three abilities are implemented, and the two mechanics they needed are §6.4 and §7.6.

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

Two numbers here have been walked back under measurement. The squad buff was **+3** in the original roster; against literal multipliers that produced a 35-cell turn, two-thirds of the loop from one ability, and it is now +0.5. The mark was **pure bookkeeping** until 2026-09-12 — the ult cost 9 and did nothing whatsoever on the turn it was cast.

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

**Cryo-Pulse is unmeasured and possibly stronger than its peer.** Against Dargin Pulse it is the same cost, radius and damage, but it originates on a target three cells away rather than on the caster, and applies two statuses rather than one. For a 1.0-speed operator, remote origin is most of the game. It was 4 energy in the first draft and did more than either 6-cost area ability for two-thirds the price. The tier was a second objection at the time and is now abolished; the peer comparison was always the real one, so the price stands.

**Translocation's range 6 is the longest in the game**, and it is the only compensation a 5-health operator gets for being in a fight. Because progress moves one-for-one with cells, the range also bounds the swing: a swap shifts either operator by at most 6 cells of journey.

**Its cooldown is the whole limiter.** At 3 energy against a 3.5 drip both 3 and 4 gate to roughly every turn, so raising the cost would not have limited it whatever it was set to. A cooldown longer than the economy imposes is exactly what §3.1 says a stated cooldown is for. This is also the cheapest denial tool in the design: swapping with an operator near its home mouth sends it backwards while you take its cell, which is a version of the play `_HANDOFF_opt_out_home_entry.md` prices at 3–6 energy as an entire new mechanic.

### 10.5 Javi — Support

**HP 6 · Speed 1.5× · Two of three abilities implemented**

> **Incomplete, and in the draft pool.** Trauma Plate is designed and not built: a shield with a per-ability value needs the absorb layer reworked from a whole-instance bool to a pool (§5.6), which is the same pipeline branch the deterministic-evasion pass is rewriting. The two land together.

| #   | Ability             | Type   | Cost | CD  | Range | Effect                                                                                                           |
| --- | ------------------- | ------ | ---- | --- | ----- | ---------------------------------------------------------------------------------------------------------------- |
| 1   | **Nanite Infusion** | Active | 3    | 2   | 3     | Ally: **heal 2**. Enemy: **2 Normal**, and **heal 1** to every ally within 2 of the target, the caster included. |
| 2   | **Trauma Plate**    | Active | 6    | 3   | 3     | _Not implemented._ Shield with a 2-point pool, 2 turns.                                                          |
| 3   | **Neural Purge**    | Active | 6    | 3   | 3     | Remove **every applied status** from an ally (§5.8). Passives untouched.                                         |

**He is the first operator who makes a target harder to kill**, which changes what the whole board is doing rather than adding to one side of it. Everything before him moved damage around; he removes it.

**Speed 1.5 with every ability at range 3.** A support who cannot reach the fight is a dead ability list, so he pays for his reach in fragility rather than in slowness.

**Heal 2, not 3.** Collision is 3, so a heal never fully undoes a hit — he blunts damage rather than erasing it, which is the difference between a support and an undo button. Cooldown 2 on a 3-cost ability is one of only two cooldowns on the roster that bind tighter than the economy (§3.1).

**The hostile mode is the interesting one.** Turned on an enemy it damages, and heals every ally within 2 of _that enemy_ — which is precisely where Ace Shards and Dargin Pulse punish a squad for standing. It pays for a commitment the rest of the roster charges for.

**Neural Purge was damage reduction until 2026-09-12**, and that design lost to Trauma Plate on every axis: a flat 2-point pool absorbs more than a 50% cut, and does it predictably.

**He may be the operator that tips the game.** §12 records that neutralizing rewards the attacker with nothing, and suspects that suppresses combat in human play in a way the harness cannot detect, because the scripted player fights unconditionally. A dedicated healer makes kills materially harder to land. His measured strength depends entirely on which way that question goes, so settle it before trusting any figure about him.

### 10.8 Sanity — Engineer

**HP 12 · Speed 0.5× · Complete — all three abilities implemented**

> **He transgresses two precedents, knowingly, and both are recorded here as designer overrides (2026-09-15), not drift.** Speed 0.5 sits below the 1.0–1.5 band (ADR-0002 Amendment 4, §6.3) — the first operator outside it. Health 12 ties the roster maximum the Bouncer cut (2026-09-12) had just vacated. Both were signed off as the price of the "immovable object" fantasy: the toughest operator ever fielded, and the slowest by half the band.
>
> **The slow-immunity side effect is accepted, not overlooked.** At 0.5 he sits permanently on `MinSpeedMultiplier`, so no slow and no aura can move his speed at all — the floor swallows them (§5.2). That cuts both ways: his own Zero-Day slow is something he can never suffer in a mirror match.

| #   | Ability           | Type   | Cost | CD  | Range | Effect                                                                                                                                                                            |
| --- | ----------------- | ------ | ---- | --- | ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Short Circuit** | Active | 3    | 1   | 1     | **1 Normal**; **Stun 1 turn** (§5.1).                                                                                                                                             |
| 2   | **Zero-Day**      | Active | 4    | 3   | 2     | Attach a charge to an enemy (§6.4): at Sanity's next upkeep it detonates on the target's current cell — **1 Normal** to all enemies within 1, **+1** to the marked target, **Slow 1 turn** to everyone caught. Telegraphed on attach; cleanse-detachable (§5.10). |
| 3   | **Collision**     | Active | 7    | 4   | 5     | **Dash** to the target, enemy or ally (§7.6): **1 Normal** to every enemy on the traversed cells. Enemy target: **3 Normal** and **Stun 1 turn**. Lands a cell behind the target. |

**Short Circuit is From the Hip's shape with the reach spent on a stun.** Same price, same 1 damage — but Syla's slow at range 3 becomes the strongest control status in the game at melee range, on the slowest operator ever fielded. The range is the whole cost of the ability: he has to be standing next to somebody, and at 0.5× that is the rarest thing on the board.

**Zero-Day is the first operator-anchored deferred effect (§6.4).** A beacon is a bet on where somebody will be; the charge is a delayed certainty that follows them there, and it pays for the certainty in counterplay rather than in damage — telegraphed on attachment, and a cleanse strips the marker and cancels it outright. Javi's Neural Purge answers a 4-energy ability for 6, which is the rock-paper-scissors the cleanse exists for (§5.8). The damage split inverts Drone Strike's: the beacon is strongest against a crowd that scatters its beam, the charge against the one operator it is riding. Normal type, so a plate absorbs it and an evasion charge can dodge it, on top of the cleanse — Atomic would make the counterplay one-dimensional, and Atomic is deliberately concentrated (§2.2).

**Collision is the mobility his speed denies him, priced as an ultimate.** At 0.5 he moves three cells on a six; the dash moves him up to six — five to the target and one past it — in either direction. The ally mode is the escape the rest of the kit refuses to give him, and it is why the ability carries the camping rule (§4.4, second amendment). Priced under Miracle Pull's 9: three Normal and a stun on the anchor plus a rake along the path is less than a possible execute, and the dash cuts both ways — it delivers the slowest operator in the game to exactly where the fight is, which is sometimes where he wanted to be.

**Costs 3 / 4 / 7 are the balance review's outcome (2026-09-15), argued against peers and unmeasured.** A basic priced like From the Hip, a delayed area priced under Drone Strike because it can be cleansed away, an ultimate priced under Miracle Pull because its damage is Normal and its target can be an ally. Adding him shifts the draft's dice stream besides, so nothing here can be compared to figures taken before him.

---

## 11. Superseded and removed

| Thing                                                        | Status                                                                                                                              |
| ------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------- |
| "Exactly one operator moves per roll" (§6)                   | **Dead.** Replaced by per-die consumption and compulsory movement (§6, §6.1).                                                       |
| Deploy having to precede movement in a roll                  | **Dead.** An artifact of computing the movement total up front from the whole roll; a deploy now simply removes a die (§6).         |
| `cells = floor(DiceTotal × EffectiveSpeed)`                  | **Restated** as `floor(Pips × EffectiveSpeed)`, where Pips is what this move spends (§6.3). Amended 2026-09-15: below 1.0× the half cell rounds up. |
| Tiered energy (≤4 → 1, 5–8 → 2, ≥9 → 3)                      | **Dead.** Replaced by §3.1.                                                                                                         |
| "3 energy points per turn to spend" (GDD)                    | **Dead.** Never closed against 9-cost ultimates.                                                                                    |
| `Energy Efficiency` stat                                     | **Cut.** One value on one operator, blank on two, no rule ever attached.                                                            |
| `Energy` as a per-operator stat                              | **Cut.** The pool is player-level.                                                                                                  |
| Shield as "2 hits to capture"                                | **Rewritten** as §5.6 — the capture system it described no longer exists.                                                           |
| Shield absorbing a whole instance whatever its size          | **Superseded in principle** by §5.6's pool, which is unbuilt. The current rule stands until it lands.                               |
| Mark as bookkeeping-only, applying no modifier               | **Rewritten** as §5.7 — it now deals damage over time.                                                                              |
| Tagged From Above's "within 3 of Syla's turns" payout window | **Replaced** by the mark's own duration (§5.7, §10.2).                                                                              |
| "A collision is always exactly 1v1" (§7.2)                   | **False.** Contradicted §4.5. Replaced by §7.5.                                                                                     |
| §7.4 covering pulls only                                     | **Extended** to swaps, with the cells-versus-progress conversion and the forwards case stated for the first time.                   |
| The pull clamp as "owed a doc amendment"                     | **Discharged.** Stated in §7.4.                                                                                                     |
| Speed band 1.5–2.0 (Amendment 2)                             | **Lowered** to 1.0–1.5 by Amendment 4. Bouncer 1.5 → 1.0, Syla 2.0 → 1.5, Kurbyn 1.5+0.5 → 1.0+0.5.                                 |
| Speed band 1.0–1.5 as a roster-wide invariant                | **Amended** 2026-09-15: Sanity fields at 0.5, the first recorded exception (§10.8) — a designer override, bought with the slow-immunity side effect stated there. The band stands for everyone else. |
| Standard board 48/6                                          | **Replaced** by 52/6 (ADR-0002 Amendment 5). 48 cannot be drawn as a continuous Ludo cross; journey 54 → 58.                        |
| Bouncer at 12 health                                         | **Lowered** to 9 (§10.1). He absorbed four collisions and shrugged off the sequence that kills everyone else.                       |
| Velvet Rope as 3 Normal at range 3                           | **Retuned** to 3 **Atomic** at range 4, then **back to range 3** (§10.1). Atomic stayed; the reach did not.                         |
| All-In Mauling at range 1 with 3 damage and 3 self           | **Retuned** to range 2, 2 damage, 2 self (§10.1).                                                                                   |
| Miracle Pull at range 1                                      | **Widened** to 2 (§10.3). At 1 the ult was often unspendable when it was worth spending.                                            |
| Intimidating Presence at radius 2                            | **Widened** to 3, matching Velvet Rope's reach (§10.1).                                                                             |
| From the Hip at 2 base damage                                | **Lowered** to 1 (§10.2). The bleed profile carries the ability; the base does not.                                                 |
| Cryo-Pulse at 4 energy                                       | **Repriced** to 6 (§10.4). It did more than either 6-cost area ability for two-thirds the price.                                    |
| The 3 / 6 / 9 cost tier                                      | **Abolished** 2026-09-13 (§3.2). Three abilities were priced off it deliberately; no price moved when it went.                      |
| Javi's third ability as 50% damage reduction                 | **Replaced** by Neural Purge (§10.5). It duplicated Trauma Plate, lost on cost, range and absorption, and needed fractional health. |
| An ability with no player-facing description                 | **Dead.** The constructor requires one and throws on an empty string (§10).                                                         |
| `CollisionDamage` as "the first dial"                        | **Withdrawn** (§12). Measured at 0.4 turns across a 2→6 range — but see §12 on splitting, which attacks the premise.                |
| Ludo capture (land → instant send-home)                      | **Replaced** by collision (§7).                                                                                                     |
| `[Range(3, 9)]` on `Operator.maxHealth`                      | **Dead**, and still dead — Bouncer at 9 is coincidence, not the rule returning.                                                     |
| Player elimination ("until one player is left")              | **Not a mechanic** in the MVP (§1.2).                                                                                               |
| `MeshRenderer` fallback on `Operator`                        | Already dead (ADR-0001).                                                                                                            |

---

## 12. Open items

## 12. Open items

These are dials and scope, not holes. Nothing here blocks implementation.

All figures below are measured against the live core by `tools/sim/NonaRoyale.Sim`, **800 matches per row**, four players, `openingDeployments = 2`, alpha three via `CreateAlphaMatch`. They supersede every earlier figure in this document and in ADR-0002 Amendments 2 through 6.

Spending wins at roughly 66/34 under a naive spender, on both alpha and drafted squads, 2000 matches.

14/09/2026 New and open: the cost-tier signal. Three comparisons suggest large abilities convert energy better than small ones, and nothing in the design intends that.

**Baseline — Standard 52/6, band 1.0/1.5/1.5, `NeutralizeEnergyBounty = 3`:**

**22.6 turns · p90 29 · 9.3 neutralizes · 53.5 abilities · 5.1 collisions · 12.1 energy burned · 33% squad occupancy.**

### Why this is not the 21.3 / 6.8 that ADR-0002 Amendment 6 records

It is the same board and the same band, and the neutralize count is 37% higher. That is not noise and it is not a fault: **the roster changed underneath it.** `EvasionChance` fell 0.5 → 0.3, Bouncer lost three health, Miracle Pull's range widened, and movement became compulsory (§6.1). Every one of those makes a kill easier to land, and evasion alone accounts for most of it.

Recorded because the comparison looked alarming for an hour: **a figure is only comparable to another figure taken on the same rules**, and the rules here move faster than the measurements do. Quote the configuration, not just the number.

### Nothing has measured a drafted squad, or a split roll

- **The harness fields the alpha three.** Not one of Mimi's or Javi's four live abilities has run in a simulated match, and §2.2's concentration question — a legal draw with no answer to Evasion — has never been exercised.
- **`ScriptedPlayer` pools every roll and never splits**, by deliberate policy: a split policy is a tactical judgement the harness would be making on the player's behalf. So these figures measure compulsory movement and nothing about splitting. Everything under "Consequences of split movement" below remains unmeasured.

### The match is over its budget, and the board is why

**22.6 turns, p90 29**, against ADR-0002's stated 15–20 minutes. Only two boards in the drawable family come in under it, and both are smaller than classic Ludo.

| Board    | Journey | Turns    | p90    | Neutralizes | Kills/turn | Mean move, share of loop |
| -------- | ------- | -------- | ------ | ----------- | ---------- | ------------------------ |
| 28/3     | 31      | 15.3     | 17     | 6.6         | 0.43       | **33%**                  |
| 36/4     | 40      | 17.3     | 22     | 7.9         | **0.46**   | 26%                      |
| **44/5** | 49      | **20.8** | **25** | 8.7         | 0.42       | **21%**                  |
| 52/6     | 58      | 22.6     | 29     | 9.3         | 0.41       | 18%                      |
| 60/7     | 67      | 26.4     | 32     | 9.5         | 0.36       | 16%                      |

**Combat density barely moves with board size; pacing moves a lot.** Kills per turn sit between 0.41 and 0.46 across a board that doubles in journey — so a shorter board is very nearly free combat, and a longer one is very nearly free waiting.

**44/5 costs 0.6 neutralizes and saves 1.8 turns and four off the p90**, and is fractionally _denser_ than Standard. It also hits ADR-0002 Amendment 4's readability target — a mean move covering roughly a fifth of the loop — exactly, where Standard is under it at 18% and 36/4 is over it at 26%. 28/3 sits at the 33% hard limit and is a development board regardless.

That makes 44/5 the only lever measured that buys pacing without giving up combat. **It is a board decision, not a dial**, and ADR-0002 owns it.

### Balance dials, ranked by neutralizes gained per turn of match length spent

| Lever                          | Neutralizes | Turns | Kills per turn |
| ------------------------------ | ----------- | ----- | -------------- |
| `NeutralizeEnergyBounty` 0 → 3 | +1.3        | +0.9  | **1.44**       |
| `NeutralizeEnergyBounty` 3 → 6 | +1.6        | +2.1  | 0.76           |
| Speed band 1.25/1.75 → 1.0/1.5 | +3.8        | +5.2  | 0.73           |
| `CollisionDamage` 2 → 6        | +2.4        | +4.8  | 0.50           |
| Board 44/5 → 52/6              | +0.6        | +1.8  | 0.33           |
| Board 52/6 → 60/7              | +0.2        | +3.8  | 0.05           |

1. **`NeutralizeEnergyBounty = 3` is the best-value dial measured, and 6 is not.** The first three energy buy nearly twice the combat per turn that the second three do. **Keep 3.**
   The reason is in the burn column: **8.0 → 12.1 → 18.7.** The bounty itself never burns (§1.2), but the energy it adds raises the pool, so the _turn grant_ spills more often. At 6 the economy is simply overflowing, and the marginal reward is being destroyed by a rule it does not touch. **Burn is the tell for a bounty that has gone too far**, not the neutralize count.
2. **Ability reach — historically the largest lever, and currently unmeasurable as one.** +1 to every range and radius bought +47% neutralizes for under 1.5 turns when last swept globally. Since then Velvet Rope took +1 and gave it back, Intimidating Presence took +1 and kept it, and Miracle Pull took +1. **The net has never been measured and the sweep no longer exists in the default run.**
3. **`CollisionDamage` — struck, restored, and now demoted.** It has changed status three times. At 2 → 6 it now costs **4.8 turns** for 2.4 neutralizes, which makes it the worst combat lever except board length. It was struck when collisions fired 2.4 times a match; they now fire 5.1.
   The general rule, which matters more than the dial: **a dial's potency is a function of how often its trigger fires.** Anything struck here must be re-measured after a structural change rather than trusted. Note also that at 6 it is the only configuration measured that fails to finish every match — 99% completion against 100% everywhere else.
4. **Opening deployments.** Adopted at 2, and still the only lever that ever improved a problem at no cost elsewhere.
5. `EvasionChance = 0.3` — **lowered from 0.5 by reasoning, and this run is its first measurement.** It is inside the 9.3 baseline and cannot be separated from the rest of the tuning pass without its own sweep.
6. `MarkDamagePerTurn = 2`, `HasteSpeedBonus = 0.5`, `HasteDurationTurns = 2`, `SlowSpeedPenalty = 0.5` — all still set by reasoning and unmeasured in isolation.
7. `EnergyCap = 12` against a `floor(total/2)` drip. **Burn is 12.1 per match at the shipping configuration**, up from 8.0 without the bounty. That is the economy's headroom, and it is the figure to watch if the bounty ever moves.

### Consequences of compulsory and split movement — split still unmeasured

- **Compulsory movement is in these numbers.** Split movement is not: `ScriptedPlayer` pools.
- **Up to six operator-moves in a turn** once players split — two per roll against one, and `MaxRollsPerTurn = 3`.
- **Landings per roll roughly double when players split**, and a landing is the collision trigger (§7.1). Collisions already rose to 5.1 from 2.4 under compulsory movement alone; splitting attacks the same number again.
- **Kill bounties become easier to farm.** Two collisions per roll is two chances at a bounty where there was one, and the bounty is now the sharpest dial in the table. Neither feature was designed against the other.
- **Evasion's per-round cap is worth less.** The cap bites on the first Normal instance only, so the second landing of a split is strictly more likely to connect (§5.5) — and at `EvasionChance = 0.3` the first one is likelier to connect too.

### Open, unresolved rules conflict

- **Slows and auras stack, and §5.2 says they should not.** `GameEngine` sums two channels when computing effective speed — `StatusRegistry.SpeedModifier` and `AuraRules.SpeedModifierFor` — so From the Hip's slow and Bouncer's Intimidating Presence apply together. Within each channel the rule holds; across the two it does not.
  Both readings are defensible: an aura and a status are arguably different things, and a tank's presence compounding a wound is reasonable. But the doc says one thing and the code does another, which is the state this project exists to avoid. **Decide it.** The stakes rose again with Cryo-Pulse, which slows an entire area, and again with §6.1, where a deep enough slow decides whether a turn can be ended at all.
- **The kill bounty is implemented and this document never states it as a rule.** §9.1 records that `NeutralizeRules` owns it and §12 ranks it first among dials, but no section of §1 or §3 says what it does. A reader could work through the whole document and not learn that killing pays.

### Open, undecided

- **Whether the 15–20 minute budget still stands.** It was set in ADR-0002 before anyone had played the game, and every board except the two smallest now exceeds it. It may be the budget that is wrong rather than the board — but that is a decision somebody has to take, not a number to tune toward silently.
- **The Tech damage type, and the shield source it waits on.** Blocked on Trauma Plate. Until both land, Mimi's identity is unexpressed.
- **A draft has no balance constraint.** Nothing checks that a squad has an answer to Evasion, a way to heal, or reliable damage. With Atomic in two operators (§2.2), a legal draw can produce a squad with no way through Kurbyn.
- **Whether the same operator may take both dice in two steps.** §6 allows it, and it is Ludo-standard. It is also strictly worse in cells and strictly better in landings, which makes it a deliberate two-collision play rather than a mistake.

### Open, unmeasured

- **Two-player is a different game, not a smaller one.** 1.0 neutralizes against four-player's 9.3 — a ratio near 1:9, and it has held through every rules change. Combat scales with the number of _pairs_ of players, so it is an emergent property of crowding rather than of any rule. **Nothing in this table will fix 1v1.**
- **Javi may move the free-rider question.** Neutralizing now pays the attacker energy, which was the answer to killing being a public good — but a dedicated healer makes kills materially harder to land, and no measurement has ever included one.
- **Whether any of this is fun.** The harness reports pacing and throughput and says nothing about whether the density reads as tension or as thinness. Only a human can.

### Scope

1. **Special spaces** are deferred (ADR-0003). Shield is defined (§5.6); Teleport, Slippery, Checkpoint, RollAgain and SharksTable are not. Checkpoint conflicts with §1.2's "return to yard" and needs an explicit exception when it lands.
2. **"Brawler" is a fifth archetype** (Kurbyn) outside the base four, and the base four are now filled. Add it or re-tag.
3. **Four of nine operators unwritten**, and two of the five written are incomplete.
4. **Opt-out of home entry** (ADR-0003, §8) — designed, deferred. Note the overlap with Translocation, which delivers a version of home denial for 3 energy, and that splitting produces two landing decisions per roll where opt-out assumes one.
5. **Two walks per roll.** `PRESENTATION.md` §3 has pieces walking the track cell by cell; a split produces two, and on the same piece they must be sequenced rather than overlapped.
6. Flavour and world placement across the roster (`OPERATORS.md`).

### Balance dials, ranked by effect per turn spent

1. **Ability reach — the largest lever, and now partly spent and partly refunded.** Adding +1 to every range and radius bought **+47% neutralizes for under 1.5 turns** when measured. Since then Velvet Rope took +1 and gave it back, Intimidating Presence took +1 and kept it, and Miracle Pull took +1. The net is unmeasured.

2. **Opening deployments.** Adopted at 2. The only lever found that improves a problem at no cost elsewhere: occupancy roughly doubles and matches get _shorter_. `openingDeployments = 1` is retained for a more classic Ludo opening.

3. **Journey length.** The only thing that buys occupancy outright, and it costs pacing directly. **Now geometrically pinned** — the circuit must satisfy `8L + 4` to be drawable as a cross (ADR-0002 Amdt 5), so the nearest alternatives to 52 are 44 and 60. If pacing needs adjusting, this is no longer a free dial; use opening deployments or the speed band instead.

4. `EvasionChance = 0.3` — **lowered from 0.5, adopted by reasoning and unmeasured.** Prevents 0.65 per round against the current Normal spread (mean 2.17), where 0.5 prevented 1.08. The per-round cap bounds the worst case; the rate itself is free to move. Note this dial was pulled ahead of restoring Velvet Rope's range, which this item had ranked first — that reordering is unexplained and the range is still shortened.

5. `EnergyCap = 12` against a `floor(total/2)` drip — governs how often ultimates appear. Roughly 24 energy per match was burned at the cap, almost all of it pre-contact in the opening turns.

6. `MarkDamagePerTurn = 2` over a 2-turn duration — **unmeasured.** Set by reasoning: 4 total leaves a 6-HP target at 2, inside collision range and inside Miracle Pull's execute window, while 9 would kill unassisted and make the ult's payout self-fulfilling.

7. `SlowSpeedPenalty = 0.5` against the 1.0–1.5 band takes a 1.0 operator to the `MinSpeedMultiplier` floor (§5.2). Slow is harsher than it was under the earlier band and was not re-measured when the band moved. Intimidating Presence at radius 3, and Cryo-Pulse applying slow to a whole area, both make it land more often. Note it now also interacts with §6.1: a heavily slowed squad can reach the state where a roll has no legal consumer at all.

8. `HasteSpeedBonus = 0.5` and `HasteDurationTurns = 2` — **unmeasured.** The bonus was inherited from a cut made against the 1.5–2.0 band, where it was a ~25% bump; against 1.0–1.5 it is +33% to +50%.

### Struck

- **`CollisionDamage` is not a dial** — _as measured._ Moving it from 2 to 6 changed match length by 0.4 turns and neutralizes by 1.1, because collisions occurred only ~2.4 times a match on Standard. This section and ADR-0002 Amendment 2 both named it as the first lever if the race reads as toothless; that advice was wrong on the figures available. **The strike rests entirely on collision frequency, and split movement raises it.** Re-measure before relying on this either way.
- **The yard setback is not the most expensive rule.** The Python model showed it costing 6.4 turns per match at ~8 neutralizes. At the measured 6.7 across four players it fires under twice per player per match, and its contribution is far smaller than claimed.
- **Occupancy is no longer 10–15%.** It measured that under the old speed band. At the adopted band with opening deployments it is **33%**.

### Open, unresolved rules conflict

- **Slows and auras stack, and §5.2 says they should not.** `GameEngine` sums two channels when computing effective speed — `StatusRegistry.SpeedModifier` and `AuraRules.SpeedModifierFor` — so From the Hip's slow and Bouncer's Intimidating Presence apply together. Within each channel the rule holds; across the two it does not.

  Both readings are defensible: an aura and a status are arguably different things, and a tank's presence compounding a wound is reasonable. But the doc says one thing and the code does another, which is the state this project exists to avoid. **Decide it.** The stakes rose again with Cryo-Pulse, which slows an entire area, and again with §6.1, where a deep enough slow decides whether a turn can be ended at all.

### Open, undecided

- **The Tech damage type, and the shield source it waits on.** A four-type matrix — Normal, Force, Tech, Atomic, across Evasion and Shield — is the agreed model and is blocked on shields having a real source. Javi's Trauma Plate is that source. Until both land, Mimi's whole identity is unexpressed and Force and Tech are indistinguishable from Atomic and Normal.
- **A draft has no balance constraint.** Nothing checks that a squad has an answer to Evasion, a way to heal, or reliable damage. With Atomic in two operators (§2.2), a legal draw can produce a squad with no way through Kurbyn.
- **Whether the same operator may take both dice in two steps.** §6 allows it, and it is Ludo-standard. It is also strictly worse in cells and strictly better in landings, which makes it a deliberate two-collision play rather than a mistake. Undecided whether that is a feature worth keeping or an exploit worth naming.

### Open, unmeasured

- **Neutralizing rewards the attacker with nothing**, which at four players makes killing a public good bought with private resources. Believed to suppress combat in human play in a way no simulation can detect, because the scripted player fights unconditionally. Javi makes this more pressing, not less. See `_HANDOFF_neutralize_rewards.md`.
- **Two-player matches are close to a pure race.** Pacing barely moves with seat count but combat scales hard: **0.6 neutralizes at two players against 4.0 at four**, measured under the older band. If 1v1 is meant to be a real mode it needs its own configuration, not just fewer seats.
- **Whether any of this is fun.** The harness reports pacing and throughput. It says nothing about whether the density reads as tension or as thinness. Only a human can.

### Scope

1. **Special spaces** are deferred (ADR-0003). Shield is defined (§5.6); Teleport, Slippery, Checkpoint, RollAgain, and SharksTable are not. Checkpoint conflicts with §1.2's "return to yard" and needs an explicit exception when it lands.
2. **"Brawler" is a fifth archetype** (Kurbyn) outside the base four, and the base four are now filled. Add it or re-tag.
3. **Four of nine operators unwritten**, and two of the five written are incomplete. A new operator needing a new _mechanic_ gets an amendment to this doc, not a special case in its own stat block. Watch the Atomic concentration in §2.2.
4. **Opt-out of home entry** (ADR-0003, §8) — designed, deferred. Note the overlap with Translocation, which delivers a version of home denial for 3 energy; when opt-out lands, review the two together. Note also that opt-out is a per-move decision at the moment of landing, and splitting produces two such moments per roll — the two want one consistent answer to "when is the player asked about a move, and what can they decline".
5. **Two walks per roll.** `PRESENTATION.md` §3 has pieces walking the track cell by cell. A split produces two walks; on two different pieces they read fine, on the same piece twice they arrive back to back and must be sequenced rather than overlapped.
6. Flavour and world placement across the roster (`OPERATORS.md`).

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

**Compulsory and split movement — `GameEngine`**

- `EndingATurnWhileADieCanStillBeSpent_IsRejected`
- `EndingATurnWithNoLegalMove_IsAllowed`
- `EndingATurnWithEveryOperatorStunned_IsAllowed`
- `ADoublesReroll_IsRefusedWhileTheRollInHandIsSpendable`
- `ARollCanBeSplitBetweenTwoOperators`
- `PoolingSpendsEveryUnspentDie`
- `TheSameOperatorMaySpendBothDiceInTwoSteps`
- `DeployingAfterMoving_IsAllowedWhileASixIsUnspent`
- `TwoOddDiceSpentSeparately_ArriveOneCellShortOfPooling`
- `TwoEvenDiceSpentSeparately_ArriveWherePoolingWouldHave`
- `ADieThatFloorsToZeroCells_DoesNotBlockEndTurn`
- `MovingAnOperatorThatIsAlreadyHome_IsRejected`
- `PreviewLandings_ReportsThePooledOptionAndOnePerDistinctFace`
- `PreviewLandings_OnADouble_ReportsOneSplitOptionNotTwo`

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
- `ASplitRoll_CanProduceTwoCollisionsInOneTurn`

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
- 2026-09-12 — **Mimi and Javi added as §10.4 and §10.5**, both incomplete and both in the draft pool. `EffectKind` gains `RemoveStatuses`, the seventh kind and the first that subtracts from the status registry — §5.8 states its edges, including that a cleansed mark takes its payout with it. §5.9 documents Hastened, which had been granted by the payout without ever being defined. §5.6 records that the whole-instance shield is a timing lottery and becomes a pool when Trauma Plate lands. Every ability now carries a required player-facing description (§10). §10's opening claim that nothing is deferred is retired: two of five operators have an unbuilt ability, and the banners say which.
- 2026-09-12 — **Board corrected to 52/6** (ADR-0002 Amendment 5). 48 cannot be drawn as a continuous Ludo cross — the loop must cross each arm tip, which requires `CircuitLength = 8L + 4` — so the track had a two-cell gap at all four tips and every home column stopped one cell short of HOME. Journey 54 → 58, `PlayerStartOffset` 12 → 13. §4.1's maximum track distance corrected 24 → 26. Journey length is no longer a free balance dial (§12).
- 2026-09-12 — **Mandatory rolls and split movement.** §6's "exactly one operator moves per roll" replaced by per-die consumption, which generalises §1.3's existing one-die-per-deploy rule rather than inventing a second scheme. Movement is now compulsory while a legal consumer exists (§6.1), and a doubles re-roll waits on the roll in hand. Deploy no longer has to precede movement — that rule was an artifact of computing the movement total up front. The movement formula is restated per move (§6.3), which is where splitting's price lives: a whole cell when both dice are odd, and considerably more when a die is routed through a slower operator. `MoveCommand` gains a die selector and `PreviewLandings` reports every option per operator, because a price the player cannot see before choosing is not a decision. **Unmeasured**, and the harness will not measure it: `ScriptedPlayer` pools and never splits by deliberate policy. §12 records the consequences, including that the `CollisionDamage` strike rests on a collision frequency this change attacks.
- 2026-09-12 — Javi's shield named **Trauma Plate** and its values settled at 6 energy, cooldown 3, range 3, 2-point pool, 2 turns (§10.5). Still unbuilt: it needs the absorb layer reworked from a whole-instance bool to a pool, which is the same interface the deterministic-evasion pass is rewriting. Both are specified together in `_HANDOFF_mitigation.md`, which supersedes `_HANDOFF_evasion.md` — delete that file.
  **Deterministic evasion was proposed and declined (2026-09-13).** `_HANDOFF_mitigation.md` argued for replacing the roll with a flat reduction of 1 — zero variance, 1.00 prevented per round against 0.65 here. Declined on cost, not on merit: the rate is a one-line config edit, where going deterministic changes `IDamageMitigation`, removes `IRandom` from the pipeline, and rewrites five test fixtures. Lowering the rate does not address the variance the proposal was aimed at; if evasion still reads as arbitrary in human play, that pass is the answer and this number is not.
- 2026-09-15 — **Sanity added as §10.8**, complete, with two recorded designer overrides: speed 0.5 below the 1.0–1.5 band (the first exception, slow-immunity side effect accepted) and health 12 tying the roster maximum the Bouncer cut had vacated. `EffectKind` gains `AttachCharge` and `DashToTarget`, the eleventh and twelfth kinds; `StatusKind` gains `ZeroDayCharge`, the first marker status (§5.10 — no gameplay effect, duration 2 derived from the detonation window, a cleanse cancels the charge). §6.4 states operator-anchored deferred effects: follows the target, owner's-next-upkeep timing, a bonus for the marked target, death-cell detonation, keyed to the seat so the caster's own death changes nothing, telegraphed and cleanse-detachable. §7.6 states the dash: shortest-way-round with ties forwards, the path rakes without contesting, landing one step past the target with a one-short fallback, placement throughout so one operator clamps. §4.4's camping rule now names the dash alongside pull, swap and push. §9.1's effect-kind enumeration was stale at "seven" — it had already missed the push and the two cell-anchored kinds — and now lists all twelve; §9.3's event list gains the beacon, zone and Zero-Day events it had never recorded. Costs 3 / 4 / 7 are the balance review's outcome, argued against peers and unmeasured.
