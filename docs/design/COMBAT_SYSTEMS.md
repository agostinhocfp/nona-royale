# Nona Royale — Combat Systems

> Location in repo: `docs/design/COMBAT_SYSTEMS.md`
> Status: **Accepted (alpha)** — every mechanic the alpha roster invokes is defined. No TBDs remain in the rules; open items in §12 are balance dials and post-MVP scope, not gaps.
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

- Each operator has `MaxHealth` (per `OPERATORS.md`) and a current HP.
- Damage **persists across turns**. There is no passive regeneration. The only healing in the alpha roster is Bouncer's All-In Mauling on a friendly target.
- HP is restored to full **only** on neutralize (§1.2). A wounded operator two cells from HOME is intended tension, not a problem to be smoothed.

The old `[Range(3, 9)]` cap on `Operator.maxHealth` is **dead** — Bouncer is 12. The stat is unbounded in the core; presentation-layer sliders, if any, use 3–15.

### 1.2 Neutralized

An operator is neutralized when its HP reaches 0 by any route (collision, ability, bleed, a mark tick, Miracle Pull's execute, or Bouncer's self-damage).

On neutralize:

|                |                                                                                                       |
| -------------- | ----------------------------------------------------------------------------------------------------- |
| Position       | Returns to its owner's **yard**                                                                       |
| Health         | Restored to `MaxHealth`                                                                               |
| Status effects | All cleared (stun, slow, bleed, stealth, shield, mark)                                                |
| Cooldowns      | All reset to ready                                                                                    |
| Track progress | **Entirely lost** — it re-enters at its start cell                                                    |
| Energy         | Unaffected. The pool is player-level (§3) and a yarded operator costs the player nothing economically |

**Passives are not status effects and survive neutralize.** Kurbyn's Evasive Protocol is who he is, not something applied to him; an operator returning to the yard is still itself. Whatever clears statuses must re-grant passives.

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

### 2.2 Normal vs Atomic

- **Normal** is subject to every mitigation layer: Evasion, Shield, and anything added later.
- **Atomic** ignores all of it.

Atomic does **not** bypass _targeting_ protection. Safe cells, home columns, and Stealth are not defenses — they are reachability rules, and Atomic damage that cannot legally be aimed at an operator simply never enters the pipeline.

> The one-sentence version, for the table: **Atomic can't be blocked, but it can't reach what it can't touch.**

**Sources of Atomic:** Velvet Rope, bleed ticks, Ace Shards' bleed, mark ticks, all of Miracle Pull. Everything else — including collision — is Normal.

**Atomic is the roster's answer to Evasion, and it is deliberately concentrated.** Only two operators carry unblockable single-target damage: Bouncer with Velvet Rope at 6 energy, and Kurbyn with Miracle Pull at 9. Syla's route through Evasive Protocol is indirect — Ace Shards applies bleed, bleed ticks Atomic, and From the Hip pays a bonus against a bleeding target — which makes her anti-evasion play a two-ability sequence rather than a single cast.

That concentration is fine while every player fields all three operators. It becomes a real question once squads are drafted 3 from 9: a squad without Bouncer has only the bleed line against an evasive target, and a roster that adds further Evasion holders without adding Atomic sources will make that worse.

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

**The drip is the real cooldown on anything costing 6 or more.** At a mean of 3.5 energy per turn, a 6-cost ability is naturally gated to roughly every second turn and a 9-cost ult to roughly every third, whatever its declared cooldown says. A stated cooldown only bites when it is _longer_ than what the economy already imposes — which means an ability whose identity is "castable every turn" has to cost 3 or less to actually be one. A `cooldownTurns: 0` on a 6-cost ability buys little against the drip alone, but it is not nothing at the cap, where a banked player can fire it on consecutive turns or twice in one (§10.1).

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
- Safe cells do **not** block abilities. An operator standing on S can be shot, pulled, stunned, marked, and bled. Safe means safe from _collision_, nothing more — otherwise S becomes a free parking space and the combat layer stalls.

### 4.5 Friendly stacking

Friendly operators may share any cell freely. There is no blocking mechanic in the MVP. (ADR-0003's `SharksTable` special space hints at a two-operators-on-a-cell mechanic; special spaces are deferred and it is not defined here.)

Because of this, an enemy _stack_ on a contested cell is reachable in ordinary play. What happens when a mover lands on one is §7.5.

---

## 5. Status effects

Durations are counted in **the affected operator's own turns**. An effect applied outside the target's turn takes hold on the target's next turn and expires at the end of it (for duration 1). Timers are stored as **absolute turn indices resolved at application time**, not decrementing counters — this removes the classic off-by-one and makes the boundary case a one-line test.

Statuses do not stack unless stated. Re-application refreshes duration and takes the larger magnitude.

**Expiry sweeps the turn it is called in, not the turn after.** At End of turn _N_, everything whose last active turn is _N_ is removed. Queries made _during_ a turn are stricter — a status is still active throughout its final turn — because the two answer different questions at different moments. Getting this backwards reinstates exactly the off-by-one the absolute-index timers exist to remove.

### 5.1 Stun

- **Effect:** the operator cannot move and cannot spend energy on its next turn.
- **Passives stay live.** A passive is who an operator is, not what it does.
- **Forced movement still works.** Velvet Rope pulls a stunned target normally — being moved is not the target's action.
- **Cooldowns still tick.** They are timers, not actions.

### 5.2 Slow

- **Effect:** speed multiplier **−0.5** for the duration.
- `MinSpeedMultiplier = 0.5`. Sources do not stack; the largest applies.

The band is 1.0–1.5 (§6), so −0.5 costs a 1.5 operator a third of its movement and takes a 1.0 operator to the floor. **Slow is therefore sharper against the roster's slower operators than the original 1.5–2.0 band made it**, which is worth watching: a slowed Bouncer at 0.5× moves half of what the dice say. The penalty is a config dial (`SlowSpeedPenalty`) if that reads as hard control rather than friction.

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
- Atomic pierces it (§2.2).
- **Evasion negates damage, never movement.** If a collision's damage is evaded, the target still survives and the mover still bounces back (§7.2).

The per-round cap is load-bearing. Uncapped, a coin flip in a match with roughly six attacks against a target does not average out — it decides games, and it can eat a four-turn ultimate investment on a single roll.

### 5.6 Shield

- **Effect:** absorbs one entire instance of Normal damage, including a collision, then expires.
- Atomic ignores it.
- Granted by the `Shield` special space (ADR-0003). **Special spaces are deferred and not in the MVP**; the rule is defined here so the space is buildable when it lands.

This replaces the old "requires 2 hits to capture instead of 1" wording, which described a capture system that no longer exists.

### 5.7 Mark

- **Effect:** `MarkDamagePerTurn = 2` **Atomic** damage at the **upkeep of the marked operator's owner's turn**, every turn the mark is active.
- **Ticking does not remove it.** This is the whole difference between a mark and bleed (§5.3): bleed is delayed damage that fires once and is gone, a mark is a lingering condition that bills every turn until its duration runs out. Applied by Tagged From Above for **2 turns**, so an undisturbed mark deals **4 total**.
- **Records who applied it.** The payout condition in §10.2 reads that source; the mark is the only status that carries one.
- Does not stack. Re-application refreshes the duration.
- Mark can neutralize. An operator dying at upkeep never gets its turn, exactly as with bleed.
- Cleared on neutralize, on expiry, or when the payout fires.

**The damage is deliberately sub-lethal.** Both 6-HP operators survive a full mark at 2 HP — inside collision range, inside Miracle Pull's execute window, and inside a From the Hip against a bleeding target. The mark's job is to _hand_ the kill to the marker's squad, which is the condition that pays out Tagged From Above (§10.2). At 3 per turn over 3 turns a mark would deal 9 and kill both 6-HP operators unassisted, which makes the ult's own payout condition self-fulfilling: 9 energy for a guaranteed kill, a guaranteed squad buff, and unconditional stealth, against a Miracle Pull at the same cost that needs the target already below half and needs Kurbyn adjacent. `MarkDamagePerTurn` is the first dial if the mark reads as toothless; per ADR-0005's precedent on `CollisionDamage`, it starts low and rises under measurement.

**A kill by a mark tick does satisfy "neutralized by Syla's side" (§10.2).** The damage is sourced from Syla and runs the standard pipeline. At 2 per turn it is rare, but the rule is stated rather than left to emerge from whatever the code happens to do.

---

## 6. Turn structure and resolution order

Exactly one operator moves per roll. Energy may be spent by any owned operator.

| Phase         | What resolves                                                                                                                                                                                                                             |
| ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **1. Upkeep** | Bleed ticks (Atomic), then mark ticks (Atomic). Cooldowns advance. Evasion charge refreshes. Neutralize checks from both damage sources resolve here.                                                                                     |
| **2. Roll**   | Dice rolled from the injected RNG. Energy granted (first roll of the turn only, §3.1).                                                                                                                                                    |
| **3. Action** | Deploy (if a 6, §1.3) and/or move one operator; spend energy on abilities with any owned operator; any order the player chooses. Collisions resolve immediately on landing (§7). Doubles → return to phase 2 **without** an energy grant. |
| **4. End**    | Status durations expire. Win check.                                                                                                                                                                                                       |

Bleed resolves before marks purely for determinism; nothing in the alpha roster distinguishes the order, but an unspecified order is a bug waiting for the first operator that cares.

Expiry sits at End and application takes hold at the target's next turn, so a 1-turn stun applied during an opponent's turn correctly blocks the target's action phase before expiring.

`MaxRollsPerTurn = 3` (the initial roll plus two doubles) bounds turn length. Tunable.

**Movement:** `cells = floor(DiceTotal × EffectiveSpeed)`, where `EffectiveSpeed` is the operator's multiplier after auras and slows, floored at `MinSpeedMultiplier`.

**Speed band: 1.0 – 1.5**, in half-steps. `SpeedMultiplierMin = 1.0` and `SpeedMultiplierMax = 2.5` are the legal schema bounds; the alpha roster uses 1.0 and 1.5 only. The band was set by simulation, not by feel — see ADR-0002 Amendment 4, which lowered it from the 1.5–2.0 adopted in Amendment 2. Two constraints fix it:

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

Nothing on the roster dies to a single collision. A 6-HP operator dies to a collision plus any prior scratch; Bouncer absorbs four.

That is deliberate. Collision is a **softening** mechanic that sets up ability kills, not a kill mechanic itself — the 70/30 combat-over-race priority expressed as a number. The consequence to watch is that the race layer is now close to non-lethal on its own.

### 7.4 Forced movement

Pull, push, and teleport effects **never collide** and never trigger cell effects. They are placement.

An operator pulled toward a puller is placed on the **last track cell between them** — adjacent to the puller, on the side it came from. This preserves "pulled toward" whether the target was ahead or behind, and guarantees two operators never co-occupy a contested cell as a side effect of an ability.

**A pull that would carry an operator behind its own start cell clamps there.** An operator's path does not extend backwards past its start, so there is nowhere further to place it; the start cell is safe, so the clamp costs nothing and triggers nothing. The alternative — wrapping the placement around the loop — would silently hand the target most of a lap.

### 7.5 Stacked occupants

**A contested cell can hold more than one enemy, and the mover strikes all of them.**

This corrects a claim §7.2 previously made — that a collision is always exactly 1v1, because a non-safe cell never holds more than one enemy. That is false, and it contradicts §4.5. Three ordinary sequences produce a stack:

- **Friendly stacking.** Two operators of the same colour may share any cell (§4.5). A third player landing there meets both.
- **Bounce-back.** A bounced mover is _placed_ one step back and triggers nothing (§7.2) — including no collision, so it may land on an occupied cell.
- **Forced movement.** A pull places its target without colliding (§7.4), which can drop it onto a cell an enemy already holds.

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

| Service             | Owns                                                                      |
| ------------------- | ------------------------------------------------------------------------- |
| `TurnStateMachine`  | Phase order (§6), turn rotation, roll budget                              |
| `EnergyLedger`      | Generation, cap, spend, refusal on insufficient funds (§3)                |
| `MovementResolver`  | Dice → cells, deploy, path advance, home entry (§1.3, §6)                 |
| `CollisionResolver` | Landing contest, bounce-back (§7)                                         |
| `TargetingRules`    | Range along track, AOE windows, legality: stealth, home column, safe (§4) |
| `AbilityResolver`   | Cost, cooldown, target validation, effect emission (§10)                  |
| `DamagePipeline`    | The single choke point of §2.1                                            |
| `StatusRegistry`    | Apply, query, expire; absolute-index timers (§5)                          |
| `AuraRules`         | Aura effects, evaluated on demand rather than stored (§10.1)              |
| `NeutralizeRules`   | The §1.2 consequences, and the mark payout (§10.2)                        |
| `WinConditions`     | §8                                                                        |

**`StatusRegistry` reports damage, it never applies it.** The pipeline consults the registry for evasion and shields, so a registry that called the pipeline would close a dependency cycle. Bleed and mark ticks are therefore _queried_ — the registry says what the tick owes and the caller pushes it through the pipeline as Atomic. The registry decides what damage is owed, the pipeline decides how damage lands, and neither knows the other exists.

Randomness reaches exactly two places: `MovementResolver` (dice) and `DamagePipeline` (evasion). Both take the injected seedable RNG. Nothing else in combat is random.

### 9.2 Commands (view → core)

`RollDiceCommand` · `DeployCommand` · `MoveCommand` · `UseAbilityCommand` · `EndTurnCommand`

### 9.3 Events (core → view)

`DiceRolled` · `EnergyGranted` · `EnergySpent` · `OperatorDeployed` · `OperatorMoved` · `CollisionResolved` · `DamageDealt` · `DamageEvaded` · `DamageAbsorbed` · `HealApplied` · `StatusApplied` · `StatusExpired` · `OperatorNeutralized` · `OperatorReachedHome` · `TurnEnded` · `GameWon`

`DamageEvaded` and `DamageAbsorbed` are separate events rather than a flag on `DamageDealt` because the view needs to play three visibly different things. What the view is required to do with them is `docs/design/PRESENTATION.md`.

---

## 10. Alpha roster, re-expressed

Every ability below is fully expressible in the rules above. Nothing is deferred.

**An ability with a hostile and a friendly mode picks its mode once, from who was targeted.** All-In Mauling's self-damage belongs to the _hostile_ cast: used on an ally it heals and costs the Bouncer nothing. The same rule governs Velvet Rope, which pulls either way but only damages an enemy. Deciding per _recipient_ rather than per _cast_ gives a nonsense answer for any effect aimed at the caster's own side, since the caster is always friendly to himself.

### 10.1 Bouncer — Tank

**HP 12 · Speed 1.0× · Range in path steps**

| #   | Ability                   | Type    | Cost | CD  | Range | Effect                                                                                               |
| --- | ------------------------- | ------- | ---- | --- | ----- | ---------------------------------------------------------------------------------------------------- |
| 1   | **Velvet Rope**           | Active  | 6    | 2   | 4     | Pull target to the cell adjacent to Bouncer (§7.4). Enemy: **3 Atomic**. Ally: pull only, no damage. |
| 2   | **Intimidating Presence** | Passive | —    | —   | 3     | Enemies within range: speed multiplier **−0.5** (floor 0.5, §5.2).                                   |
| 3   | **All-In Mauling**        | Active  | 6    | —   | 2     | Enemy: **3 Normal** to target **and 1 direct to Bouncer** (§2.3). Ally: **heal 3**.                  |

Intimidating Presence is an aura, not a status: it is evaluated when an affected operator's movement is calculated, so there is no duration to track and no application event.

Bouncer's kit is priced on **positioning, not energy** — the roster's slowest operator, so the real cost is the turns it takes him to be standing near anyone. That makes him the most pool-efficient operator in the squad, which is a legitimate reason to run him.

He sits at **1.0**, which is what his design brief always wanted. Amendment 2 pushed him to 1.5 for a pacing reason — a slow tank taxes every match, because the match ends when the _last_ operator gets home — and Amendment 4 reverted it once opening deployments paid that cost elsewhere. A tank that moves like everyone else is not a tank.

**His reach is the compensation for that speed.** Velvet Rope at 4 is the longest single-target range in the game — Syla caps at 3, Kurbyn at 2 — and that is deliberate for an operator whose stated cost is positioning. It also makes the rope a **soft denial tool**: pulling an operator four cells back from its home mouth is a swing play the board otherwise has no answer to.

**Intimidating Presence at 3 matches the rope's previous reach**, so anything Bouncer could rope before is already slowed, and anything he ropes now is slowed the moment it arrives. Reach and aura are one kit, not two.

**Velvet Rope is Atomic, which makes Bouncer the roster's direct counter to Evasion** (§2.2). This was a targeted answer to Kurbyn dominating early play, chosen over weakening Evasion itself: a counter preserves the rock-paper-scissors, a nerf flattens it.

**Velvet Rope and All-In Mauling are a combo, not alternatives.** On the same operator at the same cost they look redundant — the rope has more range, is unblockable, pulls, and costs no health. The point is that you cast both: the rope pulls the target adjacent and hits for 3 Atomic, Mauling follows at range 2 for 3 more. **Six damage in one turn kills either 6-health operator from full.** It needs the full 12-energy bank, so it comes round about every third turn — ultimate cadence, from the one operator with no ultimate.

That is also what qualifies §3.1's rule about zero cooldowns. Against the drip alone Mauling's is close to inert; at the cap it buys back-to-back turns, and it is what allows Mauling to fire twice in a single turn. Both matter only to a player who banks.

### 10.2 Syla, The Blood Hound — Assassin

**HP 6 · Speed 1.5×**

| #   | Ability               | Type         | Cost | CD  | Range                | Effect                                                                                                                                                                                                                                                                      |
| --- | --------------------- | ------------ | ---- | --- | -------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **From the Hip**      | Active       | 3    | 1   | 3                    | **1 Normal**; **Slow 1 turn**; **+1 damage** if the target is bleeding (§5.3).                                                                                                                                                                                              |
| 2   | **Ace Shards**        | Active       | 6    | 3   | 3 (AOE, self-origin) | **3 Normal** to all enemies in the window; applies **1 Bleed** each.                                                                                                                                                                                                        |
| 3   | **Tagged From Above** | Active (Ult) | 9    | 2   | 3                    | **Mark** an enemy for **2 turns**: **2 Atomic** at its upkeep each turn (§5.7). If it is neutralized by Syla's side **while marked**, the whole squad gains **speed multiplier +0.5 for one round**. Syla gains **Stealth** for the current turn + 1, regardless of payout. |

**From the Hip is a control tool with a damage rider, not a damage ability.** At 1 base against 6 health it will not trade with anything on its own; the slow is the point, and the bleed bonus doubles it. That makes Syla's line explicitly sequential — Ace Shards first for the bleed, From the Hip after — rather than a cheap ability she can lead with. It was 2 base until the bleed profile proved strong enough that the base did not need to carry the ability.

The mark's payout credits _any_ neutralize by Syla's side, including a collision and including the mark's own ticks. The stealth is unconditional and does not break on attacking (§5.4).

**The payout window is the mark's own duration.** The ability previously specified "within 3 of Syla's turns", a second timer that duplicated the status's duration and counted against a different operator's turn index than the status registry does. One timer, stored on the mark, expiring at the marked operator's own upkeep like every other status.

Two numbers here have been walked back under measurement. The squad buff was **+3** in the original roster; against literal multipliers that produced a 35-cell turn, three-quarters of the loop from one ability, and it is now +0.5. The mark was **pure bookkeeping** until 2026-09-12 — the ult cost 9 and did nothing whatsoever on the turn it was cast, deferring its entire value to a condition the player still had to satisfy by other means. The mark's damage is what gives the ability presence on the board the turn it is spent, and it is what starts the clock on its own payout condition.

### 10.3 Kurbyn, DarkGrave — Brawler

**HP 6 · Speed 1.0× base (1.5× with passive)**

| #   | Ability              | Type         | Cost | CD  | Range                | Effect                                                                                                                                                                                              |
| --- | -------------------- | ------------ | ---- | --- | -------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Dargin Pulse**     | Active       | 6    | 3   | 2 (AOE, self-origin) | **2 Normal** to all enemies in the window; **Stun 1 turn** (§5.1).                                                                                                                                  |
| 2   | **Evasive Protocol** | Passive      | —    | —   | self                 | First Normal damage instance each round: **50% negated** (§5.5). Speed multiplier **+0.5**.                                                                                                         |
| 3   | **Miracle Pull**     | Active (Ult) | 9    | 2   | 1                    | **3 Atomic** to the target. **Execute:** if the target was below 50% HP **at cast time**, it is instead neutralized outright. **2 Atomic** to enemies within 3 of the target, excluding the target. |

The execute threshold is evaluated **before** the direct damage lands, on the target's HP at cast. Checking after would mean a full-health 6-HP target drops to 3 and survives at exactly 50%, which reads as a bug at the table. `current * 2 < max` — integer comparison, no fractional HP support required anywhere in the core.

Evasive Protocol carries the speed bonus, which makes Kurbyn's mobility **conditional on the passive being live** in a way no other operator's is. It is granted once at match start and, per §1.2, must survive neutralize. A Kurbyn moving at 1.0 is a bug, not a balance state.

**Evasion made Kurbyn dominant in the first human sessions**, which is what prompted Velvet Rope becoming Atomic rather than any change here. The passive is untouched; what changed is that one operator can now reliably go through it.

---

## 11. Superseded and removed

| Thing                                                        | Status                                                                                              |
| ------------------------------------------------------------ | --------------------------------------------------------------------------------------------------- |
| Tiered energy (≤4 → 1, 5–8 → 2, ≥9 → 3)                      | **Dead.** Replaced by §3.1.                                                                         |
| "3 energy points per turn to spend" (GDD)                    | **Dead.** Never closed against 9-cost ultimates.                                                    |
| `Energy Efficiency` stat                                     | **Cut.** One value on one operator, blank on two, no rule ever attached.                            |
| `Energy` as a per-operator stat                              | **Cut.** The pool is player-level.                                                                  |
| Shield as "2 hits to capture"                                | **Rewritten** as §5.6 — the capture system it described no longer exists.                           |
| Mark as bookkeeping-only, applying no modifier               | **Rewritten** as §5.7 — it now deals damage over time.                                              |
| Tagged From Above's "within 3 of Syla's turns" payout window | **Replaced** by the mark's own duration (§5.7, §10.2).                                              |
| "A collision is always exactly 1v1" (§7.2)                   | **False.** Contradicted §4.5. Replaced by §7.5.                                                     |
| Speed band 1.5–2.0 (Amendment 2)                             | **Lowered** to 1.0–1.5 by Amendment 4. Bouncer 1.5 → 1.0, Syla 2.0 → 1.5, Kurbyn 1.5+0.5 → 1.0+0.5. |
| All-In Mauling at range 1 with 3 self-damage                 | **Retuned** to range 2 with 1 self-damage (§10.1).                                                  |
| Velvet Rope as 3 Normal at range 3                           | **Retuned** to 3 **Atomic** at range 4 (§2.2, §10.1) — the roster's answer to Evasion.              |
| Intimidating Presence at radius 2                            | **Widened** to 3, matching Velvet Rope's previous reach (§10.1).                                    |
| From the Hip at 2 base damage                                | **Lowered** to 1 (§10.2). The bleed profile carries the ability; the base does not.                 |
| `CollisionDamage` as "the first dial"                        | **Withdrawn** (§12). Measured at 0.4 turns across a 2→6 range.                                      |
| Ludo capture (land → instant send-home)                      | **Replaced** by collision (§7).                                                                     |
| `[Range(3, 9)]` on `Operator.maxHealth`                      | **Dead.** Bouncer is 12.                                                                            |
| Player elimination ("until one player is left")              | **Not a mechanic** in the MVP (§1.2).                                                               |
| `MeshRenderer` fallback on `Operator`                        | Already dead (ADR-0001).                                                                            |

---

## 12. Open items

These are dials and scope, not holes. Nothing here blocks implementation.

> **Every figure below is stale.** They were measured against the live core, but _before_ the mark gained damage, Velvet Rope became Atomic at range 4, Intimidating Presence widened to 3, From the Hip halved, and All-In Mauling was retuned. Four of those five raise lethality. **Re-run `tools/sim/NonaRoyale.Sim` and replace these numbers before citing them.**

**Last measured baseline** — Standard 48×1, band 1.0/1.5/1.5, `openingDeployments = 2`, pre-roster-changes:

**19.6 turns, p90 24, 6.7 neutralizes, 43.3 abilities, 33% squad occupancy.**

**Balance dials, ranked by effect per turn spent**

1. **Ability reach — the largest lever, and now partly spent.** Adding +1 to every range and radius bought **+47% neutralizes for under 1.5 turns** when measured. Velvet Rope has since taken +1 on its own and Intimidating Presence +1, so some of that lever is already cashed. How much is unmeasured. A further global +1 remains available but should not be spent on reasoning alone.

2. **Opening deployments.** Adopted at 2. The only lever found that improves a problem at no cost elsewhere: occupancy roughly doubles and matches get _shorter_. `openingDeployments = 1` is retained for a more classic Ludo opening.

3. **Journey length.** The only thing that buys occupancy outright, and it costs pacing directly.

4. `EvasionChance = 0.5` — the per-round cap bounds the worst case; the rate itself is free to move. **Evasion drove Kurbyn's dominance in the first human sessions**, and the response was a targeted counter (§10.1) rather than touching this number. If he is still dominant after the counter, this is the next dial.

5. `EnergyCap = 12` against a `floor(total/2)` drip — governs how often ultimates appear, and gates the Velvet Rope → All-In Mauling combo to roughly every third turn (§10.1). Roughly 24 energy per match was burned at the cap, almost all of it pre-contact in the opening turns.

6. `MarkDamagePerTurn = 2` over a 2-turn duration — **unmeasured.** Set by reasoning, not simulation: 4 total leaves a 6-HP target at 2, inside collision range and inside Miracle Pull's execute window, while 9 (3 over 3 turns) would kill unassisted and make the ult's payout self-fulfilling. Both the per-turn damage and the duration are independent dials.

7. `SlowSpeedPenalty = 0.5` against the lowered 1.0–1.5 band takes a 1.0 operator to the `MinSpeedMultiplier` floor (§5.2). Slow is meaningfully harsher than it was under the earlier band and was not re-measured when the band moved. Intimidating Presence widening to radius 3 makes this land more often.

**Struck**

- **`CollisionDamage` is not a dial.** Moving it from 2 to 6 changes match length by 0.4 turns and neutralizes by 1.1, because collisions occur only ~2.4 times a match on Standard. This section and ADR-0002 Amendment 2 both named it as the first lever if the race reads as toothless; that advice was wrong and is withdrawn.
- **The yard setback is not the most expensive rule.** The Python model showed it costing 6.4 turns per match at ~8 neutralizes. At the measured 6.7 across four players it fires under twice per player per match, and its contribution is far smaller than claimed.
- **Occupancy is no longer 10–15%.** It measured that under the old speed band. At the adopted band with opening deployments it is **33%** — the highest measured anywhere, and no longer the headline problem.

**Open, unresolved rules conflict**

- **Slows and auras stack, and §5.2 says they should not.** `GameEngine` sums two channels when computing effective speed — `StatusRegistry.SpeedModifier` and `AuraRules.SpeedModifierFor` — so From the Hip's slow and Bouncer's Intimidating Presence apply together. §5.2 states that slow sources do not stack and the largest applies. Within each channel that holds; across the two it does not.

  Both readings are defensible: an aura and a status are arguably different things, and a tank's presence compounding a wound is reasonable. But the doc says one thing and the code does another, which is the state this project exists to avoid. **Decide it.** The stakes rose when the aura widened to radius 3 and Syla's slow became her primary contribution. _(Formerly decision-log D-007, which is abandoned.)_

**Open, unmeasured**

- **Neutralizing rewards the attacker with nothing**, which at four players makes killing a public good bought with private resources. Believed to suppress combat in human play in a way no simulation can detect, because the scripted player fights unconditionally. See `_HANDOFF_neutralize_rewards.md`.
- **Two-player matches are close to a pure race.** Pacing barely moves with seat count but combat scales hard: **0.6 neutralizes at two players against 4.0 at four**, measured under the older band. If 1v1 is meant to be a real mode it needs its own configuration, not just fewer seats.
- **Whether any of this is fun.** The harness reports pacing and throughput. It says nothing about whether the density reads as tension or as thinness. Only a human can.

**Scope**

1. **Special spaces** are deferred (ADR-0003). Shield is defined (§5.6); Teleport, Slippery, Checkpoint, RollAgain, and SharksTable are not. Note that Checkpoint conflicts with §1.2's "return to yard" and needs an explicit exception when it lands.
2. **"Brawler" is a fifth archetype** (Kurbyn) outside the base four. Add it or re-tag — cosmetic, unblocking.
3. **Six of nine operators unwritten.** They must be expressible in the rules above; a new operator needing a new _mechanic_ gets an amendment to this doc, not a special case in its own stat block. Watch the Atomic concentration noted in §2.2 when adding Evasion holders.
4. **Opt-out of home entry** (ADR-0003, §8) — designed, deferred, and the lever that targets occupancy. See `_HANDOFF_opt_out_home_entry.md`.
5. Flavour fields across the roster (`OPERATORS.md`).

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

**Abilities — `AbilityResolver`**

- `AbilityOnCooldown_IsRejected`
- `CooldownOfTwo_MakesAbilityUnusableForTwoOwnerTurns`
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
- 2026-09-11 — Amended after simulation (ADR-0002 Amendment 2). Speed band set to 1.5–2.0 and roster restated (Bouncer 1.5, Syla 2.0, Kurbyn 1.5+0.5); Slow rescaled to −0.5; Tagged From Above's payout cut from +3 to +0.5; §12 balance items replaced with measured figures. Damage, energy, targeting, status and collision rules unchanged.
- 2026-09-11 — Amended during core implementation. Five rules the document did not cover were forced by writing the code and are now stated: deploy consuming one die each (§1.3); a failed evasion roll spending the charge (§5.5); expiry sweeping the turn it is called in (§5); the pull clamp at a target's own start cell (§7.4); and cast mode being chosen once from the target (§10). No existing rule changed.
- 2026-09-12 — Doc caught up to ADR-0002 Amendment 4. Speed band lowered to 1.0–1.5 and the roster restated (Bouncer 1.0, Syla 1.5, Kurbyn 1.0+0.5) across §5.2, §6, §10.1, §10.2 and §10.3; the band had been changed in the core and in the tests without the doc following. Slow's interaction with the lower band noted as unmeasured (§12, `SlowSpeedPenalty`). No rule changed here — this is a correction, not a decision.
- 2026-09-12 — Mark given damage over time. `MarkDamagePerTurn = 2` Atomic at the marked operator's upkeep, every turn, with Tagged From Above's duration cut 3 → 2 (§5.7, §6, §10.2). Mark was previously bookkeeping only, which left a 9-energy ultimate doing nothing on the turn it was cast. The ult's separate "within 3 of Syla's turns" payout window is replaced by the mark's own duration. All-In Mauling retuned to range 2 with 1 self-damage (§10.1), and the energy drip documented as the real cooldown on anything costing 6 or more (§3.1).
- 2026-09-12 — Amended after the first simulation run against the live rules. §7.2's "a collision is always exactly 1v1" was false — it contradicted §4.5, and bounce-back and pulls reach the same state. Replaced by §7.5: the mover strikes every enemy on the cell and takes it only if all of them fall.
- 2026-09-12 — §12 rewritten against live-core measurements, replacing the Python model's figures. Reach identified as the largest balance lever; `CollisionDamage` struck as a dial; the yard setback demoted; occupancy corrected from 10–15% to 33%. Gained the slow/aura stacking conflict, rehoused from the abandoned decision log.
- 2026-09-12 — Roster corrections after the first human sessions; **code was authoritative and the doc had drifted behind it**. Velvet Rope becomes **3 Atomic at range 4** (§2.2, §10.1), a targeted counter to Evasion chosen over weakening Evasion itself after Kurbyn dominated early play. Intimidating Presence radius **2 → 3**, matching the rope's previous reach. From the Hip **2 → 1** base damage (§10.2), making it a control tool with a damage rider. §3.1's claim that a zero cooldown on a 6-cost ability buys nothing softened: at the cap it enables the Velvet Rope → All-In Mauling combo, six damage in one turn. §2.2 gained a note on Atomic being concentrated in two operators, which becomes a drafting question at 3-from-9. §12 flagged wholly stale: the figures predate five roster changes, four of which raise lethality.
  2026-09-12 — Placement extended to swaps. EffectKind gains SwapWithCaster, the sixth kind and the first added since the core was written. §7.4 rewritten: placement is computed in cells and applied in progress, and the two ways a destination can leave an operator's own path are now stated — backwards behind the start cell, and forwards into the home column, which nobody had noticed until the swap arithmetic forced it. A pull clamps, a swap refuses; the rule is that placement moving one operator clamps and placement moving two refuses, because a clamp that breaks a swap's symmetry has stopped being the effect the player cast. §4.2 gains the inclusive area scope, and §7.5 notes swaps as a third route to a stacked cell. Mimi is written but stays out of §10 — two of her three abilities are castable, and Cryo Field still needs a status that damages an area at its holder's upkeep.

tune bouncer down after human play

He was too strong across all three axes, so all three came down: 12 -> 9
health, Velvet Rope range 4 -> 3, All-In Mauling 3 -> 2 damage with self
1 -> 2. Miracle Pull gains range 1 -> 2.

The rope-into-mauling one-turn kill is gone by design. Six damage killed
either 6-health operator from full for the price of a banked pool; five
leaves them at 1, which is a setup the victim gets a turn to answer.
Self-damage at 1 against 12 health was flavour text - at 2 against 9 it
is four casts, and a wounded Bouncer has to decide whether he can afford
the exchange.

Intimidating Presence stays at 3 and now equals the rope's reach again.
Reach and aura are one kit: if one moves, move the other.

All four changes push power the same way, and the rope is the only
single-cast route through Evasive Protocol - Syla's is a two-ability
sequence. If Kurbyn reads as dominant again, the rope's range is the
first thing to restore.

COMBAT_SYSTEMS 7.3, 10.1, 10.3, 11 and 12 updated.
