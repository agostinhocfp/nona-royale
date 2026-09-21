# Nona Royale — Combat Systems

> Location in repo: `docs/design/COMBAT_SYSTEMS.md`
> Status: **Accepted (alpha).** Every mechanic the roster invokes is defined and built — Mimi's Cryo Field, the last gap, landed 2026-09-16 (§5.14, §6.6, §10.4). Open items in §12 are balance dials and post-MVP scope, not gaps.
> Date: 2026-09-12 · synced to the code 2026-09-15 · Lethe added 2026-09-17
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
- Damage **persists across turns**. The one exception is slow regeneration (§5.11): +1 every third turn for any wounded operator standing in the open.
- HP is restored to full **only** on neutralize (§1.2). A wounded operator two cells from HOME is intended tension, not a problem to be smoothed.

Healing exists in two places and they are deliberately different. Bouncer's All-In Mauling heals an ally as the friendly half of a hostile ability; Javi's Nanite Infusion heals as its whole purpose. The tank's is incidental, the support's is a role. Both heal 2 since 2026-09-18 — the tank's was 3, above the support's, which said the opposite of that sentence. Javi's is still the role: longer reach, a cheaper cast, and it splashes.

The old `[Range(3, 9)]` cap on `Operator.maxHealth` is **dead**. The stat is unbounded in the core; presentation-layer sliders, if any, use 3–15. Since the +1 below, Bouncer at 10 sits just above that old cap, which is also no rule.

**Roster-wide +1 health (2026-09-16, designer).** Every operator gained one: the common figure is 7 (Mimi as well, since the 2026-09-17 buff in §10.4), Bouncer is 10 and Sanity 9 (10 until the 2026-09-20 pass, §10.8). The reason is match length. Knockouts drive it (§12), and more health means fewer knockouts. Shipped together with the regen amendment (§5.11).

- **What moved with it:** Luka's heavy line went 6 → 7 (§2.4), so "heavy" still means the two tanks and not most of the roster.
- **What did not:** damage, collision damage (3), the mark (2 a turn) and the execute threshold (a ratio, so it scales on its own). A 7-health operator now survives two collisions, and a full mark leaves it at 3 rather than 2.
- **Reasoning written against the old figures** (5, 6 and 9) still stands in §10 and in several code remarks, `CombatConfig.MarkDamagePerTurn` among them. Read those numbers as one lower than today.

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

**Who a knockout counts for** (added 2026-09-16, GUI increment I). The seat whose operator caused it: the mover in a collision, the caster of an ability, the source recorded on a bleed, mark, beacon or charge. A self-inflicted kill, a kill of one's own side, or a kill with no known source counts for nobody. The rule is the bounty's eligibility without the bounty's switch, so the tally does not move when the bounty is tuned. The engine reports it on `OperatorNeutralized.CreditedTo` and keeps per-seat totals (`KnockoutsScoredBy`, `OperatorsLostBy`) for the end screen. It is a statistic, not a rule: nothing reads it back.

**Passives are not status effects and survive neutralize.** Kurbyn's Evasive Protocol is who he is, not something applied to him; an operator returning to the yard is still itself. The registry keeps passives in a store the clear does not touch, which makes this structural rather than something every caller has to remember.

**There is no permanent death in the MVP.** Nothing removes an operator from a match for good. The GDD line about play continuing "until there's only one player left" is an artifact of the same early pass that produced the 3-energy-per-turn economy; player elimination is not a mechanic. Neutralize is a setback measured in turns, not a removal.

### 1.3 Re-entry

A neutralized operator re-enters exactly as it originally deployed (ADR-0003):

- **A roll containing a 6 may deploy one operator**, consuming that die. Deployment is optional.
- **Every die a deploy does not consume is movement**, spent under §6 — pooled onto one operator or dealt between two.
- **Each deploy consumes one die.** So a double 6 with only **one** operator waiting deploys that one and leaves the other 6 as the movement roll. Spending both dice to deploy a single operator would make a double 6 strictly worse than a single 6, which cannot be the intent.
- **Double 6 deploys two** operators and forfeits movement for that turn. It still grants the doubles re-roll (§6).
- The operator is placed on its colour's **start cell (S)**, which is a safe cell (§4.4) — so a deploy can never trigger a collision. It is also the operator's **spawn cell**: while it stands there it cannot be damaged, slowed or stunned (§4.4, third amendment, 2026-09-21).

`DeployRequirement = 6`.

---

## 2. Damage

### 2.1 The damage pipeline

All damage — from abilities, collisions, bleed and marks alike — passes through one choke point in the core. Order is fixed:

−1. **Sanctuary** (§4.4, third amendment, 2026-09-21). If the target stands on a safe cell, or on its own spawn cell, the instance is voided: emit `DamageSheltered`, **stop**. Every type, Atomic included; nothing is rescaled, rolled or consumed. It sits above everything so that no later layer can be the one that lets a hit through.
0. **Equilibrium** (§5.17, 2026-09-17). If the instance carries a cast cost and the target holds Equilibrium, its amount is rescaled first, for every damage type.
1. **Legality.** Targeting was already validated (§4). Damage against an illegal target never reaches the pipeline.
   - **1b. Tech ward.** If the instance is **Tech** and the target holds a tech ward (§5.12), the whole instance is blocked: emit `DamageAbsorbed`, **stop**. Nothing is consumed — not the evasion charge, not any shield pool.
2. **Evasion.** If the instance is **Normal or Tech** and the target has an unspent evasion charge this round, roll the seeded RNG at `EvasionChance`. On success: emit `DamageEvaded`, consume the charge, **stop**.
3. **Shield.** If the instance is **Normal or Tech** and the target holds a shield, the pool subtracts what it can (§5.6). If it ate the whole instance, emit `DamageAbsorbed`, **stop**; otherwise the rest continues to step 4, with the absorbed part reported as mitigated.
4. **Apply.** Subtract from HP, emit `DamageDealt`.
5. **Neutralize check.** HP ≤ 0 → §1.2, emit `OperatorNeutralized`.

**Atomic damage skips steps 1b, 2 and 3.** It does not skip step −1: sanctuary is not mitigation.

Evasion resolves before Shield deliberately: evasion is a reflex and should not burn a consumable the operator may need later.

**Every instance carries a cause** — "bleed", "mark", "collision", "ability", "critical", "execute", "self", "zero-day", "follow-up", "cryo-field", "watch" — and no rule reads it. It exists so the view can say what happened. Upkeep damage is why: bleed and mark ticks land in a phase where nothing else moves, so without a stated cause an operator simply loses health and, if that was its last, vanishes with nothing on screen accounting for it.

### 2.2 Normal, Tech and Atomic

- **Normal** is subject to every mitigation layer: Evasion, Shield, and anything added later.
- **Tech** is Normal in every respect, plus one counter: a **tech ward** (§5.12) blocks it outright, before evasion or a shield is consulted. The designer's stated direction is that Tech "can be amplified by specific abilities" — **nothing amplifies it yet**; the first amplifier is an amendment here and a step in the pipeline, placed before 1b so a ward blocks the amplified hit. _(Added 2026-09-15 with Luka.)_
  - **What deals Tech: damage from a guided or remote-operated device**, one that leaves the operator and does its work elsewhere — a field projected onto someone else, a homing charge, a drone. The operator's own blows, shots and self-centred emissions stay Normal, even when a device delivers them. _(Settled 2026-09-15.)_ The rule is what a jammer could plausibly stop, which is also what Hermes' Ring is (§5.12). **Borderline and left Normal:** Nuetu's Killzone, a deferred zone that reads as ordnance rather than a guided device. Mimi's Cryo Field is self-centred and is Normal under the rule — her own emission, not a device that leaves her. **Designer exception (2026-09-17):** Kian's Inversion Matrix and Sonic Disrupter are Tech, so his whole kit is one type (§10.6).
- **Atomic** ignores all of it.

Atomic does **not** bypass _targeting_ protection. Home columns and Stealth are not defenses — they are reachability rules, and Atomic damage that cannot legally be aimed at an operator simply never enters the pipeline. **Safe cells are both** (2026-09-21): a reachability rule for enemy single-targets (§4.4, first amendment) and a sanctuary the pipeline asks before anything else (§2.1 step −1). Atomic pierces neither.

> The one-sentence version, for the table: **Atomic can't be blocked, but it can't reach what it can't touch.**

**Sources of Atomic:** Velvet Rope, bleed ticks, Ace Shards' bleed, mark ticks, all of Miracle Pull, Vendetta. **Sources of Tech:** Cryo-Pulse, Zero-Day, Drone Strike, Inversion Matrix, Sonic Disrupter — one ability each from Mimi and Sanity, and all three of Kian's (the two emitters by designer call, 2026-09-17, §10.6). Everything else — including collision — is Normal.

**Atomic is the roster's answer to Evasion, and it is deliberately concentrated.** Three operators carry unblockable single-target damage: Bouncer with Velvet Rope at 6 energy, Kurbyn with Miracle Pull at 9, and Luka with Vendetta at 6 (added 2026-09-15 — the concentration is looser than it was, which is worth knowing before the next Atomic source is written). Syla's route through Evasive Protocol is indirect — Ace Shards applies bleed, bleed ticks Atomic, and From the Hip pays a bonus against a bleeding target — which makes her anti-evasion play a two-ability sequence rather than a single cast. Mimi and Javi carry none at all.

That concentration was fine while every player fielded all three of the alpha roster. **It is a live question now that squads are drafted three from the pool:** a legal draw can produce a squad with no way through an evasive target at all. Nothing in the draft checks for it.

### 2.3 Self-damage

Self-inflicted damage (Bouncer's All-In Mauling) is applied **directly to HP**, bypassing the pipeline entirely. It cannot be evaded, shielded, or evaded-then-refunded, and it _can_ neutralize its own caster.

### 2.4 Critical hits

_(Added 2026-09-15 with Luka's Vendetta — the first damage roll in the game.)_

- **A damage effect may carry a crit chance.** On a seeded roll under it, the hit's damage is **multiplied** — by the effect's multiplier, or by its heavy multiplier against a **heavy** target.
- **Heavy means maximum health above a threshold the effect states** (Luka's is 7, raised from 6 with the roster-wide +1 health on 2026-09-16: today the Bouncer and Sanity). Maximum, not current — heavy is who an operator is, not how hurt it is.
- **The roll is per effect and per recipient.** Three blows are three rolls.
- **The multiplier applies before the pipeline**, to everything the hit would have dealt, bonuses included. A critical is a bigger hit, not one that ignores defences; Vendetta's blows ignore defences because they are Atomic.
- **Self-damage never crits.** It bypasses the pipeline and is not an attack.
- **Cause `"critical"`**, so the view can label the number (`-2 CRIT`).
- **An effect with no crit chance never draws a random number**, so no existing ability's dice stream moved when this landed. A resolver built without a random source never crits — every fixture that predates this keeps its exact behaviour.

**One cast never neutralizes a target twice.** A cast's effects resolve before the engine yards anyone, so a later damage or execute effect in the same cast skips a recipient an earlier one already brought to zero. Vendetta is the first ability that lands several hits on one target, and without this a first-blow kill would report three neutralizes and pay three bounties.

### 2.5 Lifesteal

_(Added 2026-09-17 with Vendetta's drain, designer.)_

- **A damage effect may steal life.** After the hit resolves, the caster heals the health the hit **actually removed** (`DamageResult.AmountApplied`), up to its own maximum.
- **What was removed, not what the hit was worth.** Overkill heals nothing, a shield's share heals nothing, and an evaded or absorbed hit heals nothing. Against Atomic, which nothing mitigates, the drain equals the damage up to the target's remaining health.
- **Per hit.** Vendetta's three blows drain three times, and each is reported as its own heal. A blow that is not thrown (§2.4) drains nothing.
- **The heal reports what the caster gained**, so a Luka at full health shows no heal at all.
- **Only outgoing damage.** `WithLifesteal` refuses non-damage effects and damage aimed at the caster, and it composes with `WithCritical` in either order.

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

### 3.3 Draining and pool-scaled damage

_(Added 2026-09-17 with Revú.)_

- **A drain takes energy from the target's seat, and it is destroyed, not transferred** (designer). It takes what the pool holds, down to zero, and reports what it took, even 0. Leech Round drains 2 (§10.11). Transfers are left to Ghost's Siphon, if he is built.
- **Pool-scaled damage reads the target's seat at cast time.** Sadist deals `floor((cap − pool) ÷ 3)` to the target, "one damage for every 3 energy missing", and half of that, rounded down, to enemies within 2 of the target. The splash uses the target's figure, whatever the splash victims' own pools hold (designer). A share of 0 is not dealt at all.
- **A floor under the primary figure** (2026-09-21, designer). An effect may declare a minimum it always computes; Sadist's is **2**. Before it, a seat at cap paid nothing at all for the roster's dearest read, which made the ultimate a bet on the enemy's pool rather than a play. The floor is on the primary target only and the splash still divides the floored figure, so a full seat's neighbours take 1 rather than nothing. It is a floor on what the cast **computes**: Equilibrium and every mitigation layer still apply after it (§2.1), so a floored 2 against another Revú lands as 1.
- **Both need the seats.** `AbilityResolver` takes the match's players at composition. A resolver built without them refuses to run either effect instead of guessing.
- **`EnergyDrained` is its own event**, so the view can show which seat paid and what it has left.

### 3.4 Cashing a die

_(Added 2026-09-18 with Fortuna.)_

**Once a turn, a seat fielding Fortuna may cash one unspent die instead of moving it.** The die is consumed and the pool gains `EnergyConfig.CashedDieEnergy` (**2**), filling to the cap exactly as a kill bounty does; the remainder above the cap is never earned rather than burned, so `burned` keeps meaning what the drip generated and the cap destroyed (§3.1).

- **It is the third thing a die can be spent on** (§6), after a deploy and a move, and the first new consumer since the core was written.
- **The die must be one she could have moved.** A yarded, stunned, finished or home-column Fortuna cashes nothing, and neither does a die that her speed floors to no cells — a die with no legal consumer is forfeit, not money.
- **Two is priced against a die, not against an ability.** A die is worth about 3.5 pips, and a seat needs very nearly every pip it rolls to bring three operators home, so cashing is a loss on the race that only pays when the board makes that movement worthless or dangerous. It is the first dial if she reads as too strong (§10.12).
- **It answers compulsory movement** (§6.1) without repealing it: a turn whose only legal move is a bad one can be ended by selling the die. That is the rule's price, paid once a turn.
- **Its own command and its own event** — `CashDieCommand` and `DieCashed` (§9.2, §9.3) — so the view can show a die leaving the tray and the harness can count sold dice apart from the drip.

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
- **Safe cells and abilities — amended three times.** The original rule: safe cells do **not** block abilities; safe means safe from _collision_, nothing more — otherwise S becomes a free parking space and the combat layer stalls. All three amendments below deliberately moved off that position. The original reasoning stays on the page because the free-parking risk it named is real — the second amendment exists to pay for the first, and the third makes it sharper.
- **First amendment (2026-09-13): a safe cell refuses enemy single-targeting.** An operator standing on S cannot be picked out by an enemy single-target ability. The counterweight the original rule did not weigh: winning needs all three operators home, so an operator parked on a start cell is an operator not winning. Scoped to enemies exactly as stealth is (§5.4) — an ally can still be healed, plated, cleansed or repositioned while standing on one — and single-target only: areas, lines, beacons and zones all still reach the cell. A safe cell stops somebody picking you out; it does not stop a blast. _(For damage, superseded by the third amendment: the blast still arrives, and deals nothing.)_
- **Second amendment (2026-09-14): the camping rule.** An operator standing on a safe cell may not **aim behind itself**. Refused with `AimedBehindFromSafeCell`: enemy single-targets, cell aims (Drone Strike, Killzone), and placements at allies — any ability containing a pull, swap, push or dash, today exactly Velvet Rope, Translocation and Collision, which closes the safe-cell taxi. Still legal: heals, plates and cleanses at allies behind, because support is not the aggression this rule exists to stop; and self-origin areas, which radiate backwards unconsulted, because presence is not an aim. **"Behind" is the direction of travel, never progress** — a forward track offset greater than half the circuit. Progress is per-colour and meaningless to compare across seats. On an even circuit the exact-opposite cell counts as _ahead_, so the rule never blocks more than what is strictly behind. This amendment is the first one's price: a shelter that is single-target-proof must not also be an artillery position.
- **Third amendment (2026-09-21, designer): sanctuary.** Two rules, enforced in the core by `SanctuaryRules`:
  - **Safe ground is damage-proof.** An operator on any safe cell takes no damage — from abilities, areas, zones, beacons, fields, follow-ups, watches, charges, bleed and marks alike, of any type, Atomic included. The pipeline voids the instance before anything else (§2.1 step −1) and emits `DamageSheltered`; the view shows **SAFE**, deliberately not BLOCK, because nothing of the target's was spent. Reaching the cell is unchanged: areas still sweep it and riders still land — only the damage is voided. An execute below its threshold does not kill on safe ground; it falls back to its ordinary hit, which is voided too.
  - **The spawn cell is control-proof as well.** An operator on its **own colour's start cell** cannot be slowed or stunned. `StatusRegistry.Apply` refuses the application and reports it refused, so no `StatusApplied` is emitted; an aura's slow (§5.2) is suppressed there too. Another seat's start cell is safe ground but not a spawn cell: a visitor there is damage-proof and still slowable. The spawn cell is damage-proof in its own right, not only because start cells are safe today — a board whose starts stopped being safe would keep a deploying operator's protection.
  - **Not covered.** Self-inflicted damage (§2.3) is a price the caster chose, not damage received — All-In Mauling is not free on a start cell. Forced movement is not damage: a push, pull or swap can still take an operator off a safe cell, and that is the counterplay a damage-proof shelter needs (Sonic Disrupter's push from a shared safe cell is the sharpest). Statuses other than slow and stun still land anywhere — a mark on a sheltered operator stays and bills nothing until it steps off. A slow already held is not cleansed by standing on the spawn cell; only a new application is refused.
  - **The cost, stated.** This reverses the first amendment's "it does not stop a blast", and it makes parking stronger than any earlier rule did. The counterweights are the ones already on the page: winning needs all three operators home, the camping rule forbids aiming behind from the shelter, regeneration is off on safe cells (§5.11), and forced movement dislodges. **Measured the same day** (4000 matches, bots against bots, same seeds before and after; ±0.7 points on a win share). Damage fell about 9% (73.8 → 66.1 hits landed a match, 149.8 → 136.4 damage), knockouts 13.6 → 12.2, and matches got *shorter*, not longer — 26.9 → 26.0 turns a seat — because fewer knockouts means fewer yard setbacks. Safe-cell occupancy did not rise (22.9% → 22.0% of on-track operator-turns, own spawn 13.2% → 11.7%): the bots do not seek shelter deliberately, so this measures the rule's effect on combat, **not** on camping — that risk is still open and only human play will show it (§12). 3.85 hits a match are voided. Win shares: Javi 26% → 28% and Kian 26% → 24% are the only moves beyond noise, and the spread stayed 6 points (Kurbyn 29% high, Mimi/Nuetu/Fortuna/Revú 23% low). Casts barely moved; Ace Shards 1.53 → 1.19, Cryo Field 2.49 → 1.93 and Eris' Exploit 1.13 → 0.94 are the areas the bots now decline against sheltered targets.

### 4.5 Friendly stacking

Friendly operators may share any cell freely. There is no blocking mechanic in the MVP. (ADR-0003's `SharksTable` special space hints at a two-operators-on-a-cell mechanic; special spaces are deferred and it is not defined here.)

Because of this, an enemy _stack_ on a contested cell is reachable in ordinary play. What happens when a mover lands on one is §7.5.

---

## 5. Status effects

Durations are counted in **the affected operator's own turns**. An effect applied outside the target's turn takes hold on the target's next turn and expires at the end of it (for duration 1). Timers are stored as **absolute turn indices resolved at application time**, not decrementing counters — this removes the classic off-by-one and makes the boundary case a one-line test.

Statuses do not stack unless stated. Re-application refreshes duration and keeps the stronger magnitude, compared by absolute value because magnitudes are signed.

**Expiry sweeps the turn it is called in, not the turn after.** At End of turn _N_, everything whose last active turn is _N_ is removed. Queries made _during_ a turn are stricter — a status is still active throughout its final turn — because the two answer different questions at different moments. Getting this backwards reinstates exactly the off-by-one the absolute-index timers exist to remove.

**Speed is one summed channel.** Every status that affects speed carries its size as a signed magnitude — Slow −0.5, Evasive Protocol +0.5 — and the registry sums them. Hastened is not speed since 2026-09-16: it adds flat cells (§5.9). Burdened, its mirror, takes flat cells away (§5.16). That is what lets a passive carry a speed effect without a special case, and what makes a per-source slow strength expressible at all.

### 5.1 Stun

- **Effect:** the operator cannot move and cannot spend energy on its next turn.
- **Passives stay live.** A passive is who an operator is, not what it does.
- **Forced movement still works.** Velvet Rope pulls a stunned target normally, and Translocation swaps with one — being moved is not the target's action.
- **Cooldowns still tick.** They are timers, not actions.
- **Refused on the target's own spawn cell** (§4.4, third amendment). The application simply does not happen.
- **A stunned operator is not a legal consumer of a die** (§6). If it is the only operator that could otherwise move, the roll is forfeit and the turn can be ended.

### 5.2 Slow

- **Effect:** speed multiplier **−0.5** for the duration.
- `MinSpeedMultiplier = 0.5`. Sources do not stack; the largest applies.
- **Refused on the target's own spawn cell** (§4.4, third amendment), and an aura's slow does not reach an operator standing there either. A slow already held is kept.

The band is 1.0–1.5 (§6), so −0.5 costs a 1.5 operator a third of its movement and takes a 1.0 operator to the floor. **Slow is therefore sharper against the roster's slower operators than the original 1.5–2.0 band made it**, which is worth watching: a slowed Bouncer at 0.5× moves half of what the dice say. The penalty is a config dial (`SlowSpeedPenalty`) if that reads as hard control rather than friction.

> **This rule is contradicted by the code.** Slows from a status and from an aura are summed rather than resolved against each other. See §12, "Open, unresolved rules conflict".

### 5.3 Bleed

- **Effect:** each stack deals `BleedDamagePerStack = 1` **Atomic** damage at the **upkeep of the bleeding operator's owner's turn**, then that stack is removed. Bleed is delayed damage, not a lingering condition.
- Stacks are additive.
- An operator with at least one unspent stack counts as _bleeding_ for Syla's From the Hip bonus.
- Bleed can neutralize. An operator dying at upkeep never gets its turn.
- **On safe ground, bleed is spent and voided** (§4.4, third amendment). The stacks are consumed at upkeep as always and the Atomic instance is sheltered, emitting `DamageSheltered` — ending a turn on a safe cell shrugs a bleed off. Deferring it until the holder steps off was considered and not built: bleed is delayed damage, not a lingering condition, and a deferred bleed would be a second mark.

### 5.4 Stealth

- **Effect:** the operator **cannot be selected as a single target by an enemy.** That is the whole of it.
- **Still affected by:** AOE, passive auras, collision damage, and bleed or marks already applied. Stealth hides you from being _aimed at_, not from the room.
- **Visible on the board.** The piece is never hidden from the opponent. This is a hot-seat digital board game; concealing a piece would mean building fog-of-war to service one ability and a UI that lies about the state.
- **Does not break on attacking.** A 9-energy effect that dies the moment its owner acts is not an effect.
- **Allies may still target it.** Untargetability is scoped to enemies, so Stealth never locks an operator out of its own team's repositioning or healing.

### 5.5 Evasion

- **Effect:** the **first** instance of Normal damage against the holder **each round** is negated on a `EvasionChance = 0.12` seeded roll. Every subsequent instance that round lands automatically. The rate was 0.5, then 0.3 (2026-09-15), then 0.12 (2026-09-17, §10.3) — every tuning pass that left it standing failed to move Kurbyn.
- **A failed roll still spends the charge.** The charge is the _attempt_, not the success. If a miss left it intact, the holder would keep rolling against every hit until one landed, and the per-round cap — the thing that bounds the worst case — would stop binding at all.
- The charge refreshes at the holder's upkeep. "Round" therefore means _since the holder's last turn began_, which is the window during which opponents actually attack it.
- A cleanse does **not** re-arm it (§5.8).
- Atomic pierces it (§2.2).
- **Evasion negates damage, never movement.** If a collision's damage is evaded, the target still survives and the mover still bounces back (§7.2).

The per-round cap is load-bearing. Uncapped, a roll across the half-dozen attacks a target sees in a match does not average out — it decides games, and it can eat a four-turn ultimate investment on a single roll.

**Splitting a roll raises the number of attacks a round has to absorb.** Two landings per roll means up to two collisions where there was one (§6, §7.1), and the cap bites on the first of them only. That makes the second landing of a split strictly more likely to connect than the first, which is a real tactic and an unmeasured one.

### 5.6 Shield

- **Effect:** a **pool** of absorb, sized by whatever granted it. Each instance of Normal damage, a collision included, is reduced by what the pool can still take; the pool shrinks by that much. The shield is removed when the pool reaches zero or its duration ends, whichever comes first.
- **A partial absorb is still a hit.** The instance lands as `Dealt` with the absorbed part reported as `AmountMitigated`, so the view can play a glancing blow. Only an instance the pool eats entirely reports `Absorbed` (§9.3).
- **Re-application refreshes the duration and refills the pool** to the larger of the remaining and the new pool — sources do not stack, the strongest applies (§5.2). A spent-down plate re-cast is a fresh plate.
- **Evasion resolves first** (§2.1), so a dodged instance never spends a pool the holder may still need.
- Atomic ignores it.
- **Sources:** Javi's Trauma Plate (§10.5, 2-point pool on an ally), Nuetu's Ablative Plating (§10.7, 2-point pool on himself) and Lethe's Nano Cell (§10.10, a 99-point pool a round of enemy turns cannot empty, paired with a stun). The deferred `Shield` board space (ADR-0003) would use `ShieldPoolDefault`; special spaces are not in the MVP.
- A cleanse strips it, friendly or not (§5.8).

**Why a pool.** Absorbing a whole instance regardless of its size was a timing lottery, worth 1 against From the Hip and 3 against a collision, and unpriceable once a shield became castable. The pool landed on 2026-09-13 with Trauma Plate, as the shield half of the mitigation pass. The evasion half — replacing the roll with a flat reduction — was **declined**; the rate moved instead (§5.5, status history).

**The remaining pool is queryable** (`StatusRegistry.ShieldPool`), because a partial absorb emits no status event. A shield drawn as on/off tells a player a 1-point remnant is a fresh plate.

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

- **Effect:** **+1 extra cell if the roll totals 6 or less, +2 if it totals 7 or more** (`HasteRollThreshold`, `HasteCellsAtOrBelowThreshold`, `HasteCellsAboveThreshold`), for `HasteDurationTurns` (2) of the holder's own turns. Not a speed change: the cells are added after the move's own speed and rounding (§6.3).
- **The whole roll decides**, not the dice a move spends. Each hastened operator collects the bonus **once per roll**, on its first move with that roll. A second move from the same roll gets nothing extra. The roll's total counts even if one die went on a deploy or to another operator.
- **Capped at `HasteBonusCellCap` (3) extra cells per operator per turn.** It only binds on a doubles turn: a high double pays 2, and the re-roll can then add only 1. An operator can carry a lower cap of its own: Kurbyn's passive haste is capped at **2** (§10.3), read per operator with `HasteBonusCellCap` as the fallback (2026-09-17).
- A move the dice alone would not make (0 cells) gets no bonus, so haste never turns a refused move into a legal one.
- **Sources:** Tagged From Above's payout, to the marker's whole squad (§10.2); Lethe's passive, permanently (§10.10); and her Catalyst aura, to an ally within 2 of her when its move starts (§10.10). They do not stack: an operator is hastened or not, and collects one bonus per roll under one cap.

**Amendment (2026-09-16, designer): massive nerf, flat cells instead of speed.** Haste was +0.5 speed, capped at 3 extra cells a turn earlier the same day. Averaged over all 36 rolls, the capped version still paid **2.6 cells per roll at 1.0× and 2.8 at 1.5×**; the flat rule pays **1.6 at any speed**. It is part of the pass that nerfs the fast operators (§10.8): Syla and Kurbyn read strongest in the bots sweep and in human games, and the old bonus grew with the holder's speed.

**Why the whole roll and not each move.** A single die is always 6 or less, so reading the move's own pips would pay +1 per die: a split low roll (2 + 3) would earn 2 where pooling earns 1. Once per roll, set by the total, pays the same however the dice are spent.

**Bookkeeping, in `GameEngine`:**

- Paid-this-roll is per operator and clears on every roll. The turn budget clears at the start of the owner's turn.
- Each hastened operator has its own bonus and its own budget.
- A bounced move still collects the bonus (§7.2). Those cells were travelled, and the bounce is placement afterwards.
- The landing preview reads the same state (§9.1), so it shows the bonus on a first move and drops it once paid.

**Two turns, not one.** The payout can fire on the marker's own turn — a collision or ability kill — by which point that turn's movement is usually already spent, so a 1-turn buff would routinely be worth nothing. At 2 it covers the remainder of the current turn and the whole of the next, whether it fired on the marker's turn or on an opponent's upkeep.

### 5.10 ZeroDayCharge

- **Effect:** none. A pure marker — the visible half of Zero-Day's attached charge (§6.4), present so the attachment is something the rules can see and remove.
- **A cleanse strips it, and the charge never detonates.** The deferred effect checks for the marker at the owner's upkeep; a target still in play without it means the charge was answered, and it cancels silently (§5.8's rule — removing the status removes what the status carried — applied to a delayed effect for the first time).
- **Duration 2, derived, not chosen.** The earliest a charge can detonate is the owner's next upkeep; a 1-turn marker applied during the owner's own turn would expire at the end of the target's intervening turn — before that upkeep — and a charge that cancelled itself on schedule would read as a cleanse that never happened. Two turns keeps the marker alive through every legal detonation window, and its expiry is otherwise harmless: a charge that has fired is gone whatever the registry says.

---

### 5.11 Regeneration

Not a status, and not a heal anyone casts — a passive rule for every operator.

- **Effect:** `RegenAmount = 1` health at the operator's owner's upkeep, after `RegenEveryTurns = 3` **consecutive eligible** upkeeps. Reported as `OperatorRegenerated`.
- **Eligible means all three at once:** in play, **wounded** (below maximum health), and **not on a safe cell**. An ineligible upkeep resets the streak, so a heal needs three straight turns in the open.
- **Evaluated after the upkeep's damage**, so regeneration never shields an operator from a bleed or mark tick that same upkeep.
- Capped at `MaxHealth`, like every heal. `RegenEveryTurns = 0` disables it.

**Amendment (2026-09-16, designer): any wound, not below half, and now actually live.**

- **Why:** match length. Knockouts drive it (§12), and gated regen ticked about four times a match in the bots sweep, too little to matter.
- **Never ran before this date.** `CombatConfig`'s constructor took both regen values and never assigned them, so they read 0 and regen never fired. Now assigned and covered by `RegenTests`.
- **1 every 3 turns, not 1 every 2.** The designer picked the less drastic step; 1 every 2 was measured at −15% turns on its own.
- **The safe-cell exclusion stays**, for the free-parking reason below.
- **Shipped with the roster-wide +1 health** (§1.1). The measured before/after is in §12.

What follows is the argument for the gates as first shipped. The below-half gate is gone; the rest stands.

**It is a comeback spring, not a second health bar.** An always-on +1 every 2 turns was argued down before it shipped: against pools of 5–9 and damage of 1s and 2s it refunds the chip damage that taxes racing, it answers "why pay for Trauma Plate" with "don't", and on a safe cell it re-opens free parking. The below-half gate turns it into income for the losing side only; the safe-cell exclusion keeps the shelter offering nothing but shelter; every 3 keeps it slower than every damage clock in the game.

**Measured only in the bots sweep so far** (§12). The original bet is still testable: A/B `RegenEveryTurns` 0 against 3 in the policy sweep. If racers or bankers gain on the spender, or Javi's casts fall further, the gates are too loose — reach for 4 before touching the amount.

---

### 5.12 TechWard

_(Added 2026-09-15.)_

- **Effect:** Tech damage aimed at the holder is blocked outright (§2.1 step 1b, §2.2). Normal and Atomic pass through untouched.
- **Consumes nothing.** A blocked hit spends no evasion charge and no shield pool — the ward is a duration, not a charge.
- **Source:** Luka's Hermes' Ring, a self-cast for 3 turns: the cast turn and his next two, so it covers two full rounds of opponents' turns.
- **Cleansable,** like every applied status (§5.8).
- **Three sources, one per operator** (§2.2): Cryo-Pulse, Zero-Day and Drone Strike. Sanity and Kian keep Normal damage elsewhere in their kits, so against them the ward is a counter, not an immunity. **Mimi was the exception until 2026-09-16:** Cryo-Pulse was her only built damage — but Cryo Field is now built and is Normal (a self-centred emission, §2.2), so the ward no longer shuts her out.
- **Blocks damage, not riders.** Cryo-Pulse's bleed and slow and Zero-Day's slow still land on a warded holder. Bleed ticks are Atomic, so Cryo-Pulse still costs him 1 at his upkeep.
- **A warded holder still counts toward a divided payload.** Drone Strike splits its beam among everyone caught before any hit reaches the pipeline (§10.6), so the holder's share is blocked, not passed on: an ally under the beam with him takes half what it would alone. It follows from the pipeline and is kept deliberately, as a jammer eating its share; excluding warded operators from the split would be a new rule in `DeferredCellEffects`.

---

### 5.13 Hunted

_(Added 2026-09-15.)_

- **Effect:** none. A pure marker, exactly as §5.10 — the visible half of a pending follow-up strike (§6.5).
- **A cleanse strips it, and the strike never resolves.** Same mechanism and same silence as a cleansed charge.
- **Duration 2**, for §5.10's reason: the strike resolves at the same moment a charge detonates.
- **Its own kind, not a reused ZeroDayCharge,** so a target carrying both a charge and a follow-up keeps both — one entry per status kind per operator would otherwise let the first resolution cancel the other.

---

### 5.14 CryoField

_(Added 2026-09-16, with Mimi's Cryo Field — the mechanic the §10.4 banner waited on.)_

- **Effect:** while the status is active, every enemy within its radius of the holder's **current** cell takes the tick damage at each of the holder's owner-upkeeps (§6.6). The first status that damages an area, and the first whose victim is someone other than its carrier.
- **The marker is the field.** The ticking payload lives in `DeferredOperatorEffects` (§6.6) — the board-reading half — and this status is its visible, cleanseable tell, the ZeroDayCharge pattern again: a cleanse strips it and the field never ticks (§5.8's rule applied to a delayed effect once more).
- **Duration 3 reads as "two working turns"** here. A self-applied status counts the cast turn as its first (§5), and the field bills only at upkeeps — the cast turn's upkeep has already passed. Mimi's field is designed to tick at her next two upkeeps, so the registry duration is three of her turns.
- **It ends with the holder.** Neutralize strips it with every other applied status (§1.2), and a field centred on an operator in her yard centres nowhere — the follow-up precedent (§6.5), not the beacon one (ADR-0006): nothing was deployed, the field is her.
- **The tick is Normal** (a self-centred emission, §2.2) and goes through the pipeline: evasion and shields interact with it per §2.1.

---

### 5.15 Watched

_(Added 2026-09-16, with Kurbyn's Predator's Read — §6.7, §10.3. Dormant since 2026-09-17: the only ability that used it was removed (§11), and the machinery stays for the next operator who wants it.)_

- **Effect:** none. A pure marker, exactly as §5.10 and §5.13 — the visible half of a pending watch (§6.7): if the carrier moves **by dice** before the watch's owner-upkeep, the watch trips and strikes it, once.
- **A cleanse strips it, and the watch never trips.** Same mechanism and same silence as a cleansed charge or follow-up — Neural Purge answers Predator's Read for 6 against 3, the reverse of its trade against Zero-Day, and the rock-paper-scissors is the point either way.
- **Duration 2**, for §5.10's reason: the watch lapses at the same moment a charge detonates or a follow-up resolves, and the marker must outlive every counterplay window without outliving the lapse.
- **Its own kind, not a reused Hunted,** for §5.13's reason: one entry per status kind per operator, so a target carrying both a follow-up and a watch (Luka and Kurbyn can share a squad) keeps both. The fiction overlaps and the trigger is the exact opposite — Hunted punishes staying close, Watched punishes moving — so confusing the two on the board would misstate the counterplay, not just the badge.

### 5.16 Burdened

_(Added 2026-09-17, designer: Sanity's crawl, moved out of his speed.)_

- **Effect:** the holder's first move from each roll is **1 cell shorter if the roll totals 6 or less, 2 shorter if it totals 7 or more** (`BurdenCellsAtOrBelowThreshold`, `BurdenCellsAboveThreshold`, sharing `HasteRollThreshold`). Not a speed change: the cells come off after the move's own speed and rounding (§6.3).
- **Haste's rules, mirrored.** The whole roll decides; the burden is paid once per roll, on the first move. A second move from the same roll pays nothing.
- **A move never drops below one cell.** The §6.3 clamp applies after the burden, so a die of 1 or 2 spent alone on a high roll still moves 1. No per-turn cap: a penalty needs no ceiling.
- **Haste and burden cancel.** A hastened, burdened operator adds the bonus and takes the burden on the same move, once. Lethe's Catalyst or Tagged From Above's payout lifts Sanity to a plain 1.0 for as long as it lasts. The haste budget is still charged the full bonus.
- **Slows bite first.** A burdened operator at 1.0 is slowed to 0.5 like anyone (§5.2), and the burden comes off the slowed move: a slowed Sanity on a 7 moves 4 − 2 = 2.
- **A passive on Sanity**, so cleanses and neutralize leave his alone (§1.2). **Applied as an ordinary timed status by Bio-Link Rage** (§10.7, 2026-09-21), where it expires and a cleanse strips it like any other. The view tags it `BURDEN`, not "heavy", because "heavy" already means Luka's maximum-health line (§2.4).
- **Two sources:** Sanity's passive (§10.8) and Nuetu's Bio-Link Rage (§10.7). Shared exactly as Slow is; his is permanent and his own, Nuetu's is two turns and on somebody else.

### 5.17 Equilibrium

_(Added 2026-09-17: Revú's passive.)_

- **Effect:** damage an ability deals the holder at the moment it is used is rescaled by that ability's cost:
  - cost 3 or less (`EquilibriumCheapCostMax`): **double**;
  - cost 4–5: unchanged;
  - cost 6 or more (`EquilibriumDearCostMin`): **half, rounded down, but at least 1** (designer). A hit of 0 stays 0.
- **Instant hits only** (designer). Direct damage, execute fallbacks, Collision's path damage and Sadist all carry the cast's cost. **Which abilities sit in which band is a live consequence of every reprice**: after 2026-09-20 the dear band holds Velvet Rope, Miracle Pull, Drone Strike, Killzone, Sadist (7) and Collision, while Vendetta (5) and Eris' Exploit (4) have left it. Collisions, bleed and marks carry none, and neither does anything that lands later: beacons, zones, charges, follow-ups, fields and watches.
- **Every damage type, Atomic included, before every mitigation layer** (§2.1, step 0). It is a price on the caster's choice, not armour, so "Atomic ignores mitigation" does not reach it. A shield then meets the rescaled hit.
- **A critical doubles first, then Equilibrium rescales.** A dear cast's crit of 2 lands as 1, and lifesteal reads the rescaled hit. **Vendetta was the example until 2026-09-20**, when its cost went 6 → 5 and it left the dear band: crits against Revú are now whole, which is a real buff to Luka in that matchup and was not the stated point of the reprice (§10.9).
- **An execute still kills.** It is not an amount.
- **The web it builds:** cheap casts are the best way to kill him, but casting empties the pool Sadist reads. The honest answer is collisions: dice combat costs no energy and bypasses Equilibrium.
- **A passive**, so it survives cleanses and neutralize (§1.2). The view tags it `BALANCE`.

### 5.18 HouseEdge

_(Added 2026-09-18: Fortuna's passive.)_

- **Effect:** the holder's seat may cash one unspent die a turn for energy (§3.4).
- **A capability, not a modifier.** Every other passive changes a number the engine was going to compute anyway; this one adds a way to spend a die, which is why `GameEngine` reads it rather than the pipeline or the movement arithmetic.
- **A passive**, so it survives neutralize and no cleanse strips it (§1.2) — but it does nothing from a yard, a home column, or under a stun, because the die it cashes is a die she could have moved.
- The view tags it `HOUSE`.

---

## 6. Turn structure and resolution order

**Every die is consumed exactly once, by a deploy, by a move, or — with Fortuna on the seat — by being cashed (§3.4, amended 2026-09-18).** Dice spent on movement may be pooled onto one operator or dealt one to each of two — or spent on the same operator in two separate steps. A die is forfeit only when no legal consumer exists for it. Energy may be spent by any owned operator and consumes no dice.

| Phase         | What resolves                                                                                                                                                                                                                                                                                                                                  |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **1. Upkeep** | Bleed ticks (Atomic), then mark ticks (Atomic). Cooldowns advance. Evasion charge refreshes. Due beacons, charges and follow-ups resolve, standing fields bill their tick, and unsprung watches lapse (§6.4–§6.7). Neutralize checks from all of these resolve here, and a mark payout can fire here.                                                            |
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

**Movement:** `cells = floor(Pips × EffectiveSpeed)` at 1.0× and above; **below 1.0×, half cells round up** (amendment, 2026-09-15). `Pips` is the sum of the dice being spent on this move — the whole unspent roll, or one die. `EffectiveSpeed` is the moving operator's multiplier after auras and slows, floored at `MinSpeedMultiplier`.\r
\r
**The speed bonus is capped per turn (amendment, 2026-09-17, designer).** A move's bonus over a 1.0× move — `cells − Pips` — is capped at `SpeedBonusCellCap` (2) per operator per turn, charged on every move that collects it, from one budget across all of the turn's rolls (doubles included). The cap trims the bonus, never the pips, and a slowed operator at or below 1.0× has no bonus to trim. It is the ceiling the speed channel never had (§12): 1.5× still wins every die, but a turn never pays more than two extra cells. Bots sweep, 800 matches: the trim compressed the bottom of the table (Mimi 21% → 23%, Bouncer 21% → 24%) but left the 1.5× tier on top (Kurbyn 33%, Syla 30%, Javi 27%) — and at a cap of 1, measured and not adopted, Syla and Javi fell to 27% while Kurbyn still did not move (32%). His edge is the evasion, not the cells (§10.3). Matches run about a turn longer (26.6 → 27.8 turns per seat).

**Amendment (2026-09-15): below 1.0×, the half cell rounds up.** Sanity was the first operator who lived under 1.0× permanently (until 2026-09-17, below), and the floor taxed him twice — once by the multiplier, then again on every odd die: a 5 always moved 2, never 3. For a fast operator the floored half is a rounding tax on a long move; for the slowest operator ever fielded it is half of everything he has. So below 1.0× the half rounds up: a 5 moves 3, a 7 moves 4. The rule is general — anyone slowed to 0.5× gets the same grace — but 1.5× is deliberately untouched: at or above 1.0× a half-step multiplier still never gifts a cell, and the player can still halve the roll in their head. Same family as the 2026-09-14 "a spent die always moves at least one cell" clamp: slows may shrink a move, never erase one — and now, never tax the odd die twice.

**The rounding applies per move, which is what splitting costs.** Two dice pooled lose at most one half-cell to it; spent separately they lose one each. So splitting costs a **whole cell exactly when both dice are odd** — 9 rolls in 36 — and nothing otherwise. At whole-number speeds it costs nothing at all.

**The larger cost is routing a die through a slower operator**, which forfeits that operator's whole speed deficit on those pips: a double six pooled onto a 1.5 operator moves 18; split between a 1.5 and a 1.0 operator it moves 15. That is three cells, not one, and it is why the landing preview must show every option before one is chosen (§9.1).

**Haste adds flat cells after the formula (amendment, 2026-09-16).** A hastened operator's first move from a roll is `cells + min(1 or 2, budget left this turn)`: 1 if the roll totals 6 or less, 2 above, with a budget of `HasteBonusCellCap` (3) per operator per turn (§5.9). Haste does not touch `EffectiveSpeed`.

**Speed band: 1.0 – 1.5**, in half-steps. `SpeedMultiplierMin = 1.0` and `SpeedMultiplierMax = 2.5` are the legal schema bounds; the roster uses 1.0 and 1.5 only. The band was set by simulation, not by feel — see ADR-0002 Amendment 4, which lowered it from the 1.5–2.0 adopted in Amendment 2. Two constraints fix it:

- **A slow operator no longer taxes the match** the way it did before opening deployments landed. The pacing cost that pushed Bouncer up to 1.5 in Amendment 2 was paid for elsewhere, which freed the tank to be genuinely the slow one again.
- **Above 1.5 a single move stops being readable.** The ceiling exists so a mean move stays near a fifth of the loop — the figure that actually governs whether a player can follow a piece across the board.

**The band has no exceptions since 2026-09-17.** Sanity fielded at 0.5 as a recorded designer override (2026-09-15), immune to every slow and aura as a side effect. His crawl is now the Burdened passive (§5.16) at 1.0, and the roster test that named him as its one exception no longer does.

**A burden subtracts flat cells after the formula (amendment, 2026-09-17).** A burdened operator's first move from a roll is `max(1, cells + haste − (1 or 2))` (§5.16).

**Lethe sits inside the band at 1.0** (2026-09-17). Her mobility is permanent haste, which is flat cells and not speed, and her Catalyst aura grants haste rather than a speed bonus for the same reason: the speed channel has a floor and **no ceiling**, so a positive speed aura would have pushed 1.5 operators to 2.0 (§10.10). No shipped effect adds positive speed except Kurbyn's passive. The ceiling has been the per-turn speed bonus cap since 2026-09-17 (above).

### 6.4 Operator-anchored deferred effects

A beacon is anchored to a **cell** (§9.1, `DeferredCellEffects`): it fires at the caster's next upkeep on whoever is standing there then. Zero-Day introduces the twin: a charge anchored to an **operator**. The rules, parallel to the beacon's wherever the two can be:

- **It follows the target.** The detonation cell is wherever the target stands at the owner's next upkeep — moved, pulled, swapped or dashed, the charge goes with it. A beacon is a bet on where somebody will be; an attached charge is a delayed certainty that somebody will be struck wherever they go.
- **Timing is the owner's next upkeep**, exactly as a beacon's, and it resolves in the same upkeep window.
- **The marked target is hit hardest.** Everyone in the blast takes the splash; the operator carrying the charge takes the splash plus a bonus. The bonus has no recipient if the carrier is already dead.
- **Death does not disarm it, on either side.** If the target is neutralized before the detonation, the charge goes off on the death cell — the blast still happens, only the bonus is lost. And the charge is keyed to the owning _seat's_ upkeep, not to the operator who threw it: Sanity in his yard changes nothing, exactly as a deployed beacon outlives its caster (ADR-0006). A kill still credits the recorded source.
- **Re-attaching replaces.** A second charge from the same seat onto the same target refreshes the pending one rather than stacking — the same shape as re-painting a cell or re-applying a status.
- **It is telegraphed and it can be answered.** Attachment applies the ZeroDayCharge marker (§5.10) and emits its own event, so the view can show the grenade riding the target. A cleanse strips the marker and cancels the detonation outright — the counterplay is the marker, not the blast.
- **Kill credit follows the source.** Damage at detonation is attributed to the operator who attached the charge, through the same neutralize fold as everything else.

**Known limitation:** charges from two different seats on one target share one marker — the registry stores one entry per status kind per operator. The first detonation consumes it, and the second charge then reads as cleansed. A four-seat edge the design has not needed to answer; recorded rather than solved.

### 6.5 Follow-up strikes

_(Added 2026-09-15 with Luka's Blind Spot, then called L.)_

A follow-up is the charge's conditional sibling: the same registry (`DeferredOperatorEffects`), the same timing, the same marker-and-cleanse counterplay — but a single blow the target can **outrun**.

- **Timing is the caster's owner's next upkeep**, in the same window as charges and beacons, before the caster moves.
- **It lands only if the caster is within its reach of the target** (Blind Spot: 2), measured along the track in either direction (§4.1), between the two as they stand at that upkeep. Otherwise it is spent and reported as a miss.
- **The target alone.** No splash, no status.
- **Heavy targets take a bonus** (Blind Spot: +1 on top of 1 when maximum health is above 7). Settled at cast time — maximum health never changes.
- **Stealth does not stop it; safe ground voids it.** Stealth is a rule about aiming, and the aim was legal when the strike was set; what the target can do about it now is move. Since 2026-09-21 moving onto a safe cell is one such answer: the strike still resolves and the pipeline shelters it (§4.4, third amendment).
- **Unlike a charge, it does not outlive its caster.** Reach is measured from the caster, and a yarded operator has no distance to anyone — the strike misses. A target already neutralized is never in reach either.
- **Telegraphed and cleansable.** Setting it applies the Hunted marker (§5.13) and emits `FollowUpMarked`; a cleanse strips the marker and cancels it silently.
- **Re-setting replaces**, as with charges. A follow-up and a charge on the same target are separate entries with separate markers.
- **A miss still reports** (`FollowUpResolved` with `Landed = false`), naming the target — the escape is the counterplay working, and the player should see it.

### 6.6 Self-anchored fields

_(Added 2026-09-16 with Mimi's Cryo Field.)_

A field is the charge's second sibling: the same registry (`DeferredOperatorEffects`), the same marker-and-cleanse counterplay — but anchored to the **caster herself**, and **repeating**.

- **Timing is every owner-upkeep while the marker stands**, in the same window as beacons and charges, before the holder moves. A charge and a follow-up resolve once and are spent; a field bills each upkeep for its duration and is retired when the marker is — by expiry, by a cleanse, or by the holder's neutralize (§5.14).
- **It follows the holder.** Each tick is measured from her current cell, not from where she cast it. A beacon is a bet on a place and a charge follows its victim; a field is a bet on where *she* will be, which is to say a zoning tool.
- **Enemies only, within the radius.** Allies and the holder are never billed. Stealth does not protect, exactly as it does not against an area cast (§5.4): the field is not an aim, it is weather. Safe ground does — since 2026-09-21 the tick reaches an enemy on a safe cell and is voided there (§4.4, third amendment). Mitigation still applies — the tick is Normal, so evasion and shields interact per §2.1.
- **It does not outlive its holder.** The beacon precedent — a deployed device is not its operator (ADR-0006) — does not apply, because nothing was deployed. The follow-up's does: the effect is anchored to a body, and a yarded body is nowhere (§6.5). A kill still credits the recorded source through the same neutralize fold.
- **Re-projecting replaces**, as with charges and follow-ups.

### 6.7 Watches

_(Added 2026-09-16 with Kurbyn's Predator's Read.)_

A watch is the charge's third sibling: the same registry (`DeferredOperatorEffects`), the same marker-and-cleanse counterplay — but where a follow-up waits for the upkeep and asks where the target is, a watch asks only **what the target did**, and resolves the moment it does it.

- **The trigger is the target's first dice movement while the marker stands.** The strike lands the moment the move completes — after the landing's contest, before home credit — not at any upkeep. A move spent in two steps springs it on the first.
- **Placement never trips it** (§7.4). Pulls, pushes, swaps, dashes and bounce-backs relocate the target without spending its move, and a watch that answered those would punish the victim for somebody else's action. Being moved by somebody else is the escape hatch beside the obvious one: **standing still**. A target that never moves before the watch lapses takes nothing — the value of the cast was the movement it denied.
- **Once, and spent either way.** The trip consumes the watch and the marker; a second move in the same turn springs nothing. A strike the shield absorbs or evasion dodges is still spent.
- **The lapse is silent.** If no dice movement came before the owner's next upkeep, the entry retires and the marker is stripped, with no event — nothing happened, and the disappearing badge is the whole announcement. Contrast the follow-up, whose miss is reported (§6.5): there the target visibly escaped a blow; here there was never a blow to escape.
- **It outlives its caster, like a charge and unlike a follow-up.** The condition reads only the target's conduct, never the caster's position, so there is nothing for a yarded caster to be out of: the read was taken at cast time, and the answer was recorded with it (ADR-0006's deployed-device precedent). A kill still credits the recorded source.
- **It dies with its target.** Neutralize strips the marker with every other applied status (§1.2), and the marker is the source of truth for cancellation — a re-deployed target is clean, and the orphaned entry retires at the owner's next upkeep.
- **Telegraphed and cleanseable.** Setting a watch applies the Watched marker (§5.15) and emits `WatchMarked`; a cleanse strips the marker and the watch springs nothing — the trip finds no attachment and retires the entry silently.
- **Re-setting replaces**, as with every shape in the registry. With Predator's Read the case is unreachable in play — its cooldown outlasts its own marker — and the registry still answers it the same way.
- **Damage is whatever the effect declares** — Normal for Predator's Read — through the pipeline at trip time (§2.1). Stealth does not bear on it, as with the follow-up: the aim was legal when the watch was set. Safe ground voids the strike like any other damage (§4.4, third amendment).

### 6.8 Dealing dice

_(Added 2026-09-18 with Fortuna.)_

An ability may change the dice the casting seat is holding: re-roll them, or set them to a face. `EffectKind.DealDice` is the eighteenth kind and the first whose subject is the roll rather than the board.

- **The engine deals them, the resolver declares them.** The unspent dice are `GameEngine`'s state, so a cast records a `DiceDealt` outcome and the engine carries it out — the same division the deferred registries already use.
- **The dice must be in hand, and the check runs before payment.** An ability that deals two dice is refused on a half-spent roll, and the refusal costs nothing (the invariant every refusal upholds). `AbilityAvailability` gains `DiceNotHeld` so a tray can grey the ability out and a bot can stop proposing it.
- **A re-roll takes the lowest dice** (ruling, 2026-09-18). The command carries no die face, and the lowest is what a player wants re-dealt in every case the ability exists for: a one that walks nobody anywhere, or the die that is not the six. Stated as a ruling rather than offered as a choice the tray would have to carry.
- **A dealt double is not a rolled one.** The doubles re-roll is decided when the dice leave the cup, so re-rolling never creates or destroys one. Setting a double asks for the roll it is owed explicitly, and only inside `MaxRollsPerTurn` (§6.2); at the cap the faces change and nothing else does. The extra roll grants no energy, like any second roll (§3.1).
- **The roll total moves with the dice.** Haste and burden read the roll in hand (§5.9), so a dealt die adjusts the total by what changed rather than by what is left unspent.
- **The stream shifts.** A re-roll draws the match's own RNG (§9.1), so a seed reproduces the match with every re-dealt die in it — and no figure measured before her survives her.

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

### 7.7 Tables, and the first effect that shortens a move

_(Added 2026-09-18 with Fortuna's The Table. ADR-0007 Amendment 3.)_

A table is a cell effect that never resolves on a clock. It waits for traffic: **the first enemy dice move that crosses or ends on its cell stops there**, and the mover takes the table's hit.

- **It reads the cells a move passes through**, which nothing else in the game does except a dash's path (§7.6). The truncation happens before the move resolves, so the landing — and its collision contest (§7.1) — is the one the table chose.
- **The cell it starts on is not on its path.** An operator standing on a table is not stopped by it again, which is how a stopped piece leaves.
- **Once per enemy operator.** A table can never hold anybody in place twice; it is not spent by the first operator it stops, and re-dealing it on the same cell forgets who it had already stopped.
- **Placement never trips it** (§7.4), exactly as it never trips a watch (§6.7): pushes, pulls, swaps, dashes and bounce-backs are not dice movement. That is the counterplay the ability is priced around, and it makes eight abilities already on the roster newly valuable.
- **Enemies only.** Allies and the seat that dealt it cross freely.
- **It is billed on the attempted move, bounce or not.** The mover reached the table; a bounce-back is placement afterwards (§7.2), the same rule the haste bonus follows.
- **Outer track only, and never a safe cell.** A table on a start cell would shelter whoever it stopped, which is backwards; the cast is refused before payment, and the cell never appears in the legal-cell list a tray or a bot picks from. Home columns are out of reach, so **the finish stays out of the fight** (§4.3) — a table one cell short of the mouth stops the leader on the approach, which is the whole of its endgame reach.
- **A device, like every other cell effect.** It outlives the operator that dealt it (ADR-0006), ages on its owner's upkeeps, and a kill it lands credits the recorded source.
- **The landing preview must show the truncation** (§9.1), or the board shows a landing the engine will not give.

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
| `AuraRules`         | Aura effects, evaluated on demand rather than stored (§10.1, §10.10)         |
| `NeutralizeRules`   | The §1.2 consequences, the mark payout (§10.2), and the kill bounty          |
| `WinConditions`     | §8                                                                           |

**`MovementResolver` knows nothing about how many dice a move spends.** It takes a pip count, so a pooled two-die move and a single-die move are the same call with different arguments. Which dice are unspent, and whether the roll is owed, is `GameEngine`'s state — that is why splitting needed no change to the resolver at all.

**`StatusRegistry` reports damage, it never applies it.** The pipeline consults the registry for evasion and shields, so a registry that called the pipeline would close a dependency cycle. Bleed and mark ticks are therefore _queried_ — the registry says what the tick owes and the caller pushes it through the pipeline as Atomic. The registry decides what damage is owed, the pipeline decides how damage lands, and neither knows the other exists.

**An ability is a list of effects, and there are nineteen kinds:** Damage, Heal, ApplyStatus, PullToCaster, Execute, SwapWithCaster, RemoveStatuses, PushFromCaster, PaintCell, DeployZone, AttachCharge, DashToTarget, FollowUp, ProjectField, Watch, DrainEnergy, MissingEnergyDamage, DealDice, SetTable. The resolver never branches on which ability is being cast. A new operator that cannot be expressed in those fifteen gets an amendment to this document and a new kind — never an `if`. Ten have been added in earnest: the swap for Mimi, the cleanse for Javi, the push and the two cell-anchored deferred kinds for Kian and Nuetu, the operator-anchored charge and the dash for Sanity (§10.8), the follow-up for Luka (§10.9), the self-anchored field for Mimi's Cryo Field (§10.4, §6.6), and the watch for Kurbyn's Predator's Read (§10.3, §6.7). Luka's critical hits are a field on the Damage kind (§2.4), not a kind of their own. Lethe added none (§10.10): Eris' Exploit is a `DeployZone` with a crowd setting (ADR-0007 Amendment 1). Revú added two, the drain and the pool-scaled hit (§3.3, §10.11), the first effects that reach past an operator into a player. Fortuna added two more (§10.12): the dealt dice, the first effect whose subject is the roll (§6.8), and the table, the first that reads the cells a move passes through (§7.7).

**The engine reports every move a roll could make, not just one.** `PreviewLandings` returns, per operator, the pooled landing and one per distinct unspent face. A preview that showed only the pooled option would hide exactly the choice §6.3 prices, and the view must not compute any of it itself (`PRESENTATION.md` §1).

Randomness reaches exactly three places: `MovementResolver` (dice), `DamagePipeline` (evasion) and `AbilityResolver` (critical hits, §2.4 — only for an effect that can crit). All take the injected seedable RNG. Nothing else in combat is random.

### 9.2 Commands (view → core)

`RollDiceCommand` · `DeployCommand` · `MoveCommand` · `CashDieCommand` · `UseAbilityCommand` · `EndTurnCommand`

`CashDieCommand` is the first command added since the roster began (2026-09-18). It carries the operator selling and the face being sold, and it exists as a command rather than an ability because §6 is about dice and abilities spend energy (§3.4).

`MoveCommand` carries an optional die face. Null means pool every unspent die onto this operator; a face means spend that one die. `EndTurnCommand` is rejected while §6.1 is unsatisfied.

### 9.3 Events (core → view)

`CommandRejected` · `TurnBegan` · `DiceRolled` · `EnergyGranted` · `EnergySpent` · `OperatorDeployed` · `OperatorPityDeployed` · `OperatorMoved` · `CollisionResolved` · `DamageDealt` · `DamageEvaded` · `DamageAbsorbed` · `DamageSheltered` · `HealApplied` · `OperatorRegenerated` · `StatusApplied` · `StatusExpired` · `OperatorNeutralized` · `OperatorReachedHome` · `TurnEnded` · `GameWon` · `BeaconPlaced` · `BeaconFired` · `ZoneDeployed` · `ZoneTicked` · `ZeroDayAttached` · `ZeroDayDetonated` · `FollowUpMarked` · `FollowUpResolved` · `FieldProjected` · `FieldTicked` · `WatchMarked` · `WatchTripped` · `DieCashed` · `DiceDealt` · `TableDealt` · `MoveIntercepted`

**`OperatorMoved` carries the attempted landing as well as the final one** (`AttemptedTo`, `Bounced`), so a bounced move can be drawn reaching the contested cell before it is thrown back (§7.2, `PRESENTATION.md` §3). For placement the two are equal.

`DamageEvaded`, `DamageAbsorbed` and `DamageSheltered` are separate events rather than a flag on `DamageDealt` because the view needs to play visibly different things — a sheltered hit is the cell's doing, not the target's (§4.4). `DamageDealt` and `OperatorNeutralized` both carry a **cause** for the reason given in §2.1. What the view is required to do with all of it is `docs/design/PRESENTATION.md`.

---

## 10. The roster, re-expressed

Twelve operators are in the draft pool, and **all twelve are complete**. Fortuna arrived whole on 2026-09-18 (§10.12) and fills the draft grid exactly — three rows at four columns hold twelve, so a thirteenth is a layout decision (§12). Mimi's Cryo Field and Kurbyn's Predator's Read, the last two unbuilt abilities, landed 2026-09-16 (§10.4, §10.3). Lethe and Revú arrived whole on 2026-09-17 (§10.10, §10.11). Bouncer and Lethe field two abilities and an aura, Revú fields two abilities and a named passive, Kurbyn fields two actives and a two-status passive since 2026-09-17 (§10.3), and everyone else fields three abilities. An operator the game can deal but this document does not describe is worse than an entry marked incomplete; the pool no longer has one.

**The tables are copied from the roster files and the code wins any disagreement.** Numbers change there first (`Assets/_Project/Scripts/Core/Abilities/Roster/`), and a table that drifts is a second copy of a value that is now wrong. Last synced 2026-09-16.

**Health went up by 1 across the roster on 2026-09-16** (§1.1). The HP lines below show the new values. Reasoning in this section that cites 5, 6 or 9 health was written before it; read those as one lower than today.

**Every ability carries a player-facing description** in the core, required by the constructor, and it contains no numbers. Cost, range, cooldown and damage all live on the same object; a figure repeated in prose is a second copy of a value that will be wrong the first time anyone tunes it.

**An ability with a hostile and a friendly mode picks its mode once, from who was targeted.** All-In Mauling's self-damage belongs to the _hostile_ cast: used on an ally it heals and costs the Bouncer nothing. The same rule governs Velvet Rope and Nanite Infusion. Deciding per _recipient_ rather than per _cast_ gives a nonsense answer for any effect aimed at the caster's own side, since the caster is always friendly to himself.

**Self-targeting is per-ability opt-in, declared on the ability** (designer, 2026-09-17). A self-cast resolves as a friendly cast under the mode rule above — the caster is always friendly to himself — and blanket self-cast was rejected on exactly that ground: All-In Mauling's friendly mode is a heal, and Bouncer self-sustaining was never intended. So `AbilityDefinition` carries an `allowsSelfTarget` flag, the resolver excludes the caster from the target list unless the ability sets it and refuses a self-aimed cast without it (`TargetingVerdict.CannotTargetSelf`, costing nothing like every refusal), and the UI offers exactly what the list holds. Four defensive abilities opt in: Javi's Nanite Infusion, Trauma Plate and Neural Purge, and Lethe's Nano Cell, whose self-bubble pays the stun as its price. Nothing else — not Translocation, not All-In Mauling, not Velvet Rope. No effect-shape heuristics: the declaration is the rule.

**An ability every one of whose effects is scoped away by the cast mode is refused**, and costs nothing. A cleanse aimed at an enemy was never a legal cast; without the check it would resolve, do nothing, and charge for it.

### 10.1 Bouncer — Tank

**HP 10 · Speed 1.0× · Range in path steps**

| #   | Ability                   | Type    | Cost | CD  | Range | Effect                                                                                               |
| --- | ------------------------- | ------- | ---- | --- | ----- | ---------------------------------------------------------------------------------------------------- |
| 1   | **Velvet Rope**           | Active  | 6    | 2   | 3     | Pull target to the cell adjacent to Bouncer (§7.4). Enemy: **3 Atomic**. Ally: pull only, no damage. |
| 2   | **Intimidating Presence** | Passive | —    | —   | 3     | Enemies within range: speed multiplier **−0.5** (floor 0.5, §5.2).                                   |
| 3   | **All-In Mauling**        | Active  | 4    | 1   | 2     | Enemy: **3 Normal** to target **and 1 direct to Bouncer** (§2.3). Ally: **heal 2**.                  |

Intimidating Presence is an aura, not a status: it is evaluated when an affected operator's movement is calculated, so there is no duration to track and no application event.

Bouncer's kit is priced on **positioning, not energy** — the roster's slowest operator, so the real cost is the turns it takes him to be standing near anyone. That makes him the most pool-efficient operator in the squad, which is a legitimate reason to run him.

**He came down on all three axes at once after human play.** Health 12 → 9, Velvet Rope's range 4 → 3, All-In Mauling 3 → 2 damage with self-damage 1 → 2. Any one of those alone would have been measurable; together they are not separable, and if he now reads as weak the **reach is the first thing to restore** — it is the only one of the three that also governs what his aura can catch.

**Reach and aura are one kit.** Intimidating Presence at 3 equals Velvet Rope's reach again: everything he can rope is already slowed, and everything he ropes stays slowed once it arrives. They were briefly out of step when the rope went to 4 and back. If one moves, move the other.

**Velvet Rope is Atomic, which makes Bouncer the roster's direct counter to Evasion** (§2.2). This was a targeted answer to Kurbyn dominating early play, chosen over weakening Evasion itself: a counter preserves the rock-paper-scissors, a nerf flattens it. It is also the **only single-cast route through Evasive Protocol** — Syla's is a two-ability sequence — which is why shortening it is a larger change than the number suggests.

**All-In Mauling repriced 2026-09-18 (designer): 4 energy, cooldown 1, 3 out, 2 back, ally heal 2.** At 6 energy for 2 Normal it made no sense on three counts, all of them measurable against the rest of the sheet:

- **Velvet Rope beat it outright** at the same cost: 3 Atomic against 2 Normal, range 3 against 2, a pull instead of 2 self-damage. There was no board on which it was the better way to hurt somebody.
- **Three energy a point was the worst rate on the roster**, with blood on top. Nuetu's Bio-Link Rage is 3 energy for 3 at the same range and heals him 1.
- **Its ally heal was the largest in the game**, above the dedicated healer's (§1.1).

It is now the cheap brawl: shorter than the rope, Normal rather than Atomic so a plate or an evasion charge answers it, and it costs blood. **Cooldown 1 rather than 0** — at 4 energy the cap would otherwise buy three casts in a banked turn. Rope into maul still works; they share no cooldown.

**Measured, 800 matches per row.** Bots cast it 1.40 → 2.33 times a match, so the reprice did what it was meant to. It also made the game bloodier — standard sweep neutralizes 3.4 → 4.8 and turns 21.2 → 22.1; four-bot knockouts 13.9 → 14.9 — and **Bouncer's own win share fell 28% → 24%**, because a bot that casts it twice as often pays twice the blood.

**Self-damage 2 → 1 (2026-09-18, designer), the answer to that.** It was 1 against 12 health — twelve casts, flavour text — and rose to 2 when health fell to 9; at ten health and twice the casting rate, 2 a cast was giving his match away. At 1 it is ten casts, and it still bites the wounded Bouncer who was going to cast anyway.

**It recovers about a point, not four.** Bots: Bouncer 24% → 25%, casts 2.33 → 2.40, knockouts 14.9 → 14.7, turns per seat 28.3 → 28.1; standard sweep back to 21.6 turns and 4.3 neutralizes (from 22.1 and 4.8, against 21.2 and 3.4 before the reprice). So most of his drop was not the blood — a cheaper Normal hit cast twice as often is simply worth less to a bot than the rope. Judge the rest in human games rather than chasing it in the sweep.

**The rope-into-Mauling one-turn kill stays gone by design.** It was 3 Atomic plus 3, and six damage killed either 6-health operator from full for the price of a banked pool. At 3 plus 3 against 7 health it leaves them at 1: a setup rather than an execution, and something the victim's owner gets a turn to answer. The zero cooldown still buys back-to-back casts at the cap, now for 4 self-damage against 9 health, which is most of what he can pay.

### 10.2 Syla, The Blood Hound — Assassin

**HP 7 · Speed 1.5×**

| #   | Ability               | Type         | Cost | CD  | Range                | Effect                                                                                                                                                                                                                                                  |
| --- | --------------------- | ------------ | ---- | --- | -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **From the Hip**      | Active       | 3    | 1   | 3                    | **1 Normal**; **Slow 1 turn**; **+1 damage** if the target is bleeding (§5.3).                                                                                                                                                                          |
| 2   | **Ace Shards**        | Active       | 6    | 3   | 2 (AOE, self-origin) | **2 Normal** to all enemies in the window; applies **1 Bleed** each.                                                                                                                                                                                    |
| 3   | **Tagged From Above** | Active (Ult) | 9    | 4   | 3                    | **Mark** an enemy for **2 turns**: **2 Atomic** at its upkeep each turn (§5.7). If it is neutralized by Syla's side **while marked**, the whole squad gains **Hastened** (§5.9). Syla gains **Stealth** for the current turn + 1, regardless of payout. |

**From the Hip is a control tool with a damage rider, not a damage ability.** At 1 base against 6 health it will not trade with anything on its own; the slow is the point, and the bleed bonus doubles it. That makes Syla's line explicitly sequential — Ace Shards first for the bleed, From the Hip after — rather than a cheap ability she can lead with. It was 2 base until the bleed profile proved strong enough that the base did not need to carry the ability.

**Ace Shards 3 → 2 damage, range 3 → 2 (2026-09-17, designer).** She was second in the speed-cap sweep at 30%, on a kit whose area cast out-damaged everything else at its price. The bleed rider is untouched. Bots sweep: 30% → 24% — measured together with the Kurbyn rebuild below, so the figure is the package's, not the dial's alone.

The mark's payout credits _any_ neutralize by Syla's side, including a collision and including the mark's own ticks. The stealth is unconditional and does not break on attacking (§5.4).

**The payout window is the mark's own duration.** The ability previously specified "within 3 of Syla's turns", a second timer that duplicated the status's duration and counted against a different operator's turn index than the status registry does. One timer, stored on the mark, expiring at the marked operator's own upkeep like every other status.

**Cooldown 2 → 4 (2026-09-13)**, after human play read the ability as too frequent. At 2 the cooldown sat at roughly the cadence the energy drip already imposed, so it limited nothing; at 4 it is clearly in front of the drip, which is the lever §3.1 nominates.

**And it can be answered.** Javi's Neural Purge strips the mark for 6 energy, taking the payout with it (§5.8). A 9-cost ultimate that is cancelled by a 6-cost cleanse is a real counter-pick relationship, not an accident — but it is unmeasured, and it is the sharpest thing a drafted squad can do to her.

Two numbers here have been walked back under measurement. The squad buff was **+3** in the original roster; against literal multipliers that produced a 35-cell turn, two-thirds of the loop from one ability, and it is now +0.5. The mark was **pure bookkeeping** until 2026-09-12 — the ult cost 9 and did nothing whatsoever on the turn it was cast.

### 10.3 Kurbyn, DarkGrave — Brawler

**HP 7 · Speed 1.0× with permanent haste (+1 on a roll of 6 or less, +2 above, capped at 2 cells a turn) · two actives and the passive since 2026-09-17**

| #   | Ability              | Type         | Cost | CD  | Range                | Effect                                                                                                                                                                                              |
| --- | -------------------- | ------------ | ---- | --- | -------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Dargin Pulse**     | Active       | 6    | 3   | 2 (AOE, self-origin) | **2 Normal** to all enemies in the window; **Stun 1 turn** (§5.1).                                                                                                                                  |
| 2   | **Evasive Protocol** | Passive      | —    | —   | self                 | First Normal damage instance each round: negated on a **12%** roll (§5.5). Permanent **Hastened** — +1 cell on a roll of 6 or less, +2 above, capped at **2** cells a turn where the roster's cap is 3 (§5.9). |
| 3   | **Miracle Pull**     | Active (Ult) | 9    | 3   | 2                    | **3 Atomic** to the target. **Execute:** if the target was below 50% HP **at cast time**, it is instead neutralized outright. **2 Atomic** to enemies within 3 of the target, excluding the target. |

> **Predator's Read was removed 2026-09-17 (designer; §11).** The paragraph below is the reasoning that built it the day before, kept for the record. The watch machinery it introduced (§5.15, §6.7, `EffectKind.Watch`) stays in the core, dormant, for the next operator who wants it.

**Predator's Read fills the three-energy rung (2026-09-16, designer).** His cheapest cast was 6, so on a lean turn Kurbyn watched the fight rather than shaping it. The ability needed the roster's fifteenth effect kind (§9.1): the follow-up's machinery with the trigger inverted — a follow-up resolves at the upkeep and punishes a target that stayed; the watch resolves on the move and punishes a target that left. The fiction is his established one (the neural-prediction rig reads the target; if it moves, the answer is already on its way), and the damage is deliberately not the point: a target that stands still to dodge the 2 has spent its move, which under compulsory movement (§6.1) is often the worse half of the choice. Priced with Short Circuit and From the Hip, the other cheap control tools, and Normal so his own evasion's answers — a charge, a plate, a cleanse — all work against it. Unmeasured; adding him a third ability shifts the draft's dice stream, so no figure taken before compares with one after.

**Rebuilt 2026-09-17 (designer).** The paragraph after next describes the old passive and is superseded. Evasive Protocol is one fiction carried as two permanent statuses — Evasion at 12% and Hastened at the flat +1/+2 — and his base speed is a plain 1.0. The speed channel no longer carries him at all; a Kurbyn moving at his base plus haste cells is the rules, not a bug.

The execute threshold is evaluated **before** the direct damage lands, on the target's HP at cast. Checking after would mean a full-health 6-HP target drops to 3 and survives at exactly 50%, which reads as a bug at the table. `current * 2 < max` — integer comparison, no fractional HP support required anywhere in the core.

Evasive Protocol carries the speed bonus, which makes Kurbyn's mobility **conditional on the passive being live** in a way no other operator's is. It is granted once at match start and, per §1.2, survives neutralize. A Kurbyn moving at 1.0 is a bug, not a balance state.

**Miracle Pull went from range 1 to 2** in the same pass that cut Bouncer. At range 1 the finisher needed him on the cell beside his target, which on a board where placement is mostly dice meant the ult was often unspendable at the moment it was worth spending. It also widens the splash's practical reach without touching its radius.

**Evasion 50% → 30% (2026-09-15, designer).** Kurbyn is fast (1.5 with the passive) and evasive, and human games and the bots sweep (35% win share) agreed the pair was too much. The speed stays; the evasion roll came down.

**Evasion 30% → 12%, Predator's Read removed, the speed passive traded for haste (2026-09-17, designer).** The per-turn speed cap (§6.3) compressed the bottom of the table and left him untouched at 33%; a cap of 1 left him at 32%. Reading the roster table, the kit had quietly become three actives plus a fourth ability's worth of defence — and the evasion was meant to be a single ability. The roll came down to 12% (about 0.26 instances prevented per round against a single attacker), and the +0.5 speed became permanent flat haste capped at 2 cells a turn, so his traversal edge survives at half strength and his defence finally has a price tag. Bots sweep, 800 matches against the speed-cap baseline: **Kurbyn 33% → 28%**, Bouncer 24% → 28%, Syla 30% → 24% (her Ace Shards patch landed in the same tree — confounded), Revú 20% → 23%, the rest within a point; **Javi 27% → 31% is the new outlier**, up four without being touched. Turns per seat 27.8 → 27.5.

**Evasion made Kurbyn dominant in the first human sessions**, which is what prompted Velvet Rope becoming Atomic rather than any change here. Note that the tuning pass then shortened that counter and lengthened his ultimate — every change moved power the same way. Whether that is one correction or an overcorrection is a measurement, not an argument.

### 10.4 Mimi — Controller

**HP 7 · Speed 1.0× · Complete — all three abilities implemented since 2026-09-16**

> Her direct damage is **Tech** (2026-09-15, §2.2); Cryo Field is a self-centred emission and stays **Normal** under the same rule, so a warded Luka no longer shuts her out (§5.12). Her old identity as the anti-shield operator is still unexpressed — nothing amplifies Tech yet.

| #   | Ability           | Type   | Cost | CD  | Range                                          | Effect                                                                                                         |
| --- | ----------------- | ------ | ---- | --- | ---------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| 1   | **Cryo-Pulse**    | Active | 4    | 3   | 3 (AOE radius 2, target-origin, **inclusive**) | **2 Tech** to every enemy in the window, the target included; applies **1 Bleed** and **Slow 1 turn** to each. |
| 2   | **Cryo Field**    | Active | 4    | 3   | self (AOE radius 3, self-origin, **2 ticks**)  | **2 Normal** to every enemy within 3 of her **current** cell, at each of her next **two upkeeps** (§5.14, §6.6). Ends early if cleansed or if she is neutralized. |
| 3   | **Translocation** | Active | 3    | 4   | 6                                              | **Swap** cells with the target, ally or enemy. Placement — collides with nothing, triggers nothing (§7.4).     |

**Both 6-cost casts cut to 4 (designer, 2026-09-17).** She was last in the bots sweep at 16%. The price came down; the payloads did not change. Cryo Field's range 0 means it is centred on herself: it bills every enemy within 2 of her, five cells. The same pass taught the bots to cast Cryo Field. The planner had no scoring for a self-centred field, so it had been cast 0.00 times a match.

| Bots against bots, 800 matches | Before | Bots cast Cryo Field | + both at cost 4 |
| ------------------------------ | ------ | -------------------- | ---------------- |
| Mimi win share                 | 16%    | 17%                  | **20%**          |
| Cryo-Pulse casts per match     | —      | 2.06                 | 2.53             |
| Cryo Field casts per match     | 0.00   | 0.76                 | 1.38             |
| Turns per seat                 | 24.8   | 24.9                 | 25.1             |

- **The price is what moved her**, not the bot fix: +3 points from the cut, +1 from the bots learning the field.
- **Adopted row, everyone:** Kurbyn 32%, Javi 32%, Syla 31%, Sanity 26%, Nuetu 25%, Luka 24%, Revú 24%, Bouncer 22%, Lethe 20%, Mimi 20%, Kian 19%.
- **Lethe fell from 25% to 20%**, about three standard errors, more than noise usually explains. A cheaper Cryo Field punishes exactly the tight squad Catalyst asks for. Watch it before touching either.

**Buffed again the same day (designer, 2026-09-17, second pass).** Health 6 → 7 — the last operator below the roster's common figure — and Cryo Field's payload up: tick 1 → 2, radius 2 → 3 (five cells to seven). She had sunk back to 17% in the same-day sweep, after the Kian buff and the bots learning Predator's Read. The "before" below is that sweep, with the Miracle Pull cooldown nerf already in the tree, so the delta is this pass alone.

| Bots against bots, 800 matches | Before | After   |
| ------------------------------ | ------ | ------- |
| Mimi win share                 | 17%    | **21%** |
| Cryo Field casts per match     | 1.35   | 2.69    |
| Cryo-Pulse casts per match     | 2.44   | 2.50    |
| Turns per seat                 | 26.4   | 26.6    |

- **+4 points, about 2.7 standard errors** — her first move clearly out of the noise. She leaves last place; the bottom is now Mimi, Bouncer and Lethe clustered at 21%.
- **Everyone else, before → after:** Kurbyn 32% → 32%, Syla 30% → 31%, Javi 29% → 28%, Luka 26% → 25%, Sanity 25% → 25%, Nuetu 24% → 23%, Kian 24% → 23%, Revú 23% → 23%, Bouncer 23% → 21%, Lethe 22% → 21%. All within noise.
- EditMode expectations pinned to the old numbers moved with it (radius-edge fixtures and tick arithmetic in `CryoFieldTests`, the designer's-numbers assertions in `MimiBotTests`); 679 passing.

**Five health is what priced her kit until 2026-09-17.** She was the only operator below 6, and she moves at the tank's speed, so she cannot run from anything. Ace Shards into a bleeding-bonus From the Hip kills her; so does Dargin Pulse into a collision — two-ability sequences every other operator survives. _(Superseded 2026-09-17: she is 7 health now, the roster's common figure. Kept for the reasoning that priced the kit.)_

**Cryo Field's tick is 1 Normal, and the amount is a placeholder** — the design table left it blank and 1 is the smallest instrument in the game, which fits a zoning tool rather than a nuke. It is a named constant (`Mimi.CryoFieldTickDamage`) so tuning is a one-line edit. The timing likewise: a self-applied status counts the cast turn as its first (§5), so the marker's registry duration is 3 to yield the designed two ticks — cast during her action phase, it bills at her next two upkeeps and expires at the end of the second. **It follows her**: each tick is measured from where she stands then, which is the zoning the ability is for. It has never been simulated. _(Dated: the tick is 2 Normal since 2026-09-17 — the placeholder is retired — and the radius is now 3. The field has been simulated since the bots' `ProjectField` branch, this same day's sweeps included.)_

**Cryo-Pulse is unmeasured and possibly stronger than its peer.** Against Dargin Pulse it is the same cost, radius and damage, but it originates on a target three cells away rather than on the caster, and applies two statuses rather than one. For a 1.0-speed operator, remote origin is most of the game. It was 4 energy in the first draft and did more than either 6-cost area ability for two-thirds the price. The tier was a second objection at the time and is now abolished; the peer comparison was always the real one, so the price stands.

**Translocation's range 6 is the longest in the game**, and it is the only compensation a 5-health operator gets for being in a fight. Because progress moves one-for-one with cells, the range also bounds the swing: a swap shifts either operator by at most 6 cells of journey.

**Its cooldown is the whole limiter.** At 3 energy against a 3.5 drip both 3 and 4 gate to roughly every turn, so raising the cost would not have limited it whatever it was set to. A cooldown longer than the economy imposes is exactly what §3.1 says a stated cooldown is for. This is also the cheapest denial tool in the design: swapping with an operator near its home mouth sends it backwards while you take its cell, which is a version of the play `_HANDOFF_opt_out_home_entry.md` prices at 3–6 energy as an entire new mechanic.

### 10.5 Javi — Support

**HP 7 · Speed 1.5× · Complete — all three abilities implemented**

| #   | Ability             | Type   | Cost | CD  | Range | Effect                                                                                                             |
| --- | ------------------- | ------ | ---- | --- | ----- | ------------------------------------------------------------------------------------------------------------------ |
| 1   | **Nanite Infusion** | Active | 3    | 2   | 5     | Ally: **heal 2**. Enemy: **2 Normal**, and **heal 1** to every ally within 2 of the target, the caster included.   |
| 2   | **Trauma Plate**    | Active | 4    | 3   | 4     | Ally only: **Shield** with a **2-point pool** for **2 turns** (§5.6). A hostile cast is refused and costs nothing. |
| 3   | **Neural Purge**    | Active | 6    | 3   | 5     | Remove **every applied status** from an ally (§5.8). Passives untouched.                                           |

**He is the first operator who makes a target harder to kill**, which changes what the whole board is doing rather than adding to one side of it. Everything before him moved damage around; he removes it. He is also why the shield layer matters: a board space could never give a mitigation type enough uptime to mean anything.

**All three abilities may be aimed at himself** (§10's self-cast opt-in, 2026-09-17). His toolkit is defensive, and a healer who cannot treat himself is half one — heal, plate and cleanse all take him as a legal target. Nothing else on the roster self-casts.

**Ranges 5 / 4 / 5.** The kit was designed at range 3 across the board — a support who cannot reach the fight is a dead ability list, so he paid for reach in fragility rather than speed — and the 2026-09-15 balance pass (`e85d710`) raised Nanite Infusion and Neural Purge to 5 and Trauma Plate to 4. That is a deliberate designer change; `Javi.cs` records it. Note that §10.4 makes Mimi's range 6 her sole compensation for 5 health, and Neural Purge now sits one cell short of it — the gap to watch if either moves again.

**Heal 2, not 3.** Collision is 3, so a heal never fully undoes a hit — he blunts damage rather than erasing it, which is the difference between a support and an undo button. Cooldown 2 on a 3-cost ability is one of only two cooldowns on the roster that bind tighter than the economy (§3.1).

**The hostile mode is the interesting one.** Turned on an enemy it damages, and heals every ally within 2 of _that enemy_ — which is precisely where Ace Shards and Dargin Pulse punish a squad for standing. It pays for a commitment the rest of the roster charges for. The splash heal is declared enemy-audience despite landing on allies: audience picks the cast mode, not the recipient.

**Trauma Plate: a 2-point pool, cost walked 3 → 6 → 4.** The pool eats one small hit whole or takes the edge off a collision, never both. A 1-point pool was rejected: it cancels From the Hip outright and saves nobody from the collision that actually kills. The original cost 3 was too cheap beside a 6-energy Velvet Rope; 6 bought 2 points of absorb where the same 6 buys Atomic damage that ignores every defence. 4 is the compromise, **reasoned, not measured**.

**Cooldown 3 against duration 2, deliberately.** The sketched cooldown 1 gave permanent uptime — plates held on two operators forever, which is flat damage reduction on a squad, not a shield. At 3 the plate is up for two of every four of the holder's turns, so choosing _when_ is the skill.

**His two defensive abilities collide.** Neural Purge strips a friendly Trauma Plate along with everything else, because the cleanse is indiscriminate by design (§5.8). Casting them in the wrong order on one ally wastes one of them.

**Neural Purge was damage reduction until 2026-09-12**, and that design lost to Trauma Plate on every axis: a flat 2-point pool absorbs more than a 50% cut, predictably, and a percentage would have forced fractional health into a pipeline that has none. As a cleanse it is a specific answer to Syla's mark (the payout goes with it) and to Kurbyn's stun.

**He may tip a combat game into a race.** A dedicated healer with a castable shield makes kills materially harder to land, on a board whose stated priority is 70% combat. The kill bounty (§1.2) pushes the other way and landed at the same time. Neither direction has been measured with him in play.

### 10.6 Kian — Artillery

**HP 7 · Speed 1.0× · Complete — all three abilities implemented**

| #   | Ability              | Type   | Cost | CD  | Range                     | Effect                                                                                                                                                |
| --- | -------------------- | ------ | ---- | --- | ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Inversion Matrix** | Active | 3    | 3   | 4 (line ahead, no target) | **2 Tech** and **Stun 1 turn** to every enemy on the next 4 cells **ahead of him** along his own direction of travel.                                 |
| 2   | **Sonic Disrupter**  | Active | 3    | 3   | 3 (AOE, self-origin)      | **2 Tech**, **Slow 1 turn**, then **push 2 cells** away from him (§7.4), to every enemy within 3. The push never carries anyone into a home column.   |
| 3   | **Drone Strike**     | Active | 4    | 2   | unlimited (cell)          | Paint any outer-track cell (ADR-0006). At Kian's next upkeep a beam deals **4 Tech in total**, **divided** among the enemies within 1 of the cell.    |

**A sixth archetype, priced on being reached, not on reach.** At 7 health and 1.0 speed with no escape tool, anyone who closes on him has him. That is the entire cost of a kit that otherwise never needs to be near anything. He is the only operator with no single-target ability: he cannot pick one enemy and hit it.

**Inversion Matrix is the roster's first directional effect.** Every other area is symmetric, which makes a self-centred blast something you position for; a line points somewhere, and choosing where is a decision no other ability asks for. Damage 1 because the stun was what was being bought — until 2026-09-17 (below). Cost walked 3 → 4: at 3 it matched Nanite Infusion's price for something that can lock down two or three operators at once. It is one of the three abilities that ended the cost tier (§3.2).

**Sonic Disrupter's effect order is a rule:** damage, then slow, then push. Recipients are recomputed per effect, so pushing first would carry enemies out of the radius before the slow found them. **The push is away from him along the loop**, so an enemy ahead of him is thrown toward its own home — the first ability in the game that can help the player it is aimed at. Accepted: he is meant to be punishing and swingy, and which side of him to stand on is a real thing to get right. The forward clamp (§7.4) is not a balance dial; without it the ability finishes an opponent's lap. An enemy sharing his cell — reachable only on a safe cell — is thrown backwards, which breaks up exactly the free parking §4.4 worries about.

**Drone Strike's delay is the mechanic, not a cost.** He bets a round ahead on where somebody will be; they see the mark (`PRESENTATION.md`, ADR-0006) and decide whether moving off it is worth what moving costs. The divided beam makes it strongest against a lone operator and weakest against a crowd, so its counterplay is to bunch up — which every area ability on the roster punishes. That tension is why it earns a place rather than being a second area attack. Radius 1 gives the prediction a margin without making it forgiving. Unlimited range is his only free axis. Tech damage (§2.2, 2026-09-15), otherwise Normal, so a plate or an evasion charge blunts it. A warded Luka blocks his share, and it still counts toward the split (§5.12). **The cooldown is the limiter**: paint, fire, one idle turn. The device survives his death and still credits him.

**The 2026-09-15 balance pass (`245a60b`) shortened both of his long abilities:** Inversion Matrix's line 6 → 4, and Drone Strike's beam 6 → 4. At 6 a correct guess killed four of the roster's operators from full; at 4 it kills nobody from full — a 6-health operator is left at 2, inside collision range — and the beam sets the kill up instead of taking it.

**The 2026-09-17 buff (designer).** Kian was last in the bots sweep. Every ability got cheaper and both emitters hit harder:

- **Inversion Matrix** 4 → 3 energy, 1 Normal → **2 Tech**. The stun is still the point, but at 1 damage the line did nothing a stun alone didn't.
- **Sonic Disrupter** 4 → 3 energy, 2 Normal → **2 Tech**, radius 2 → **3** for all three effects (damage, slow, push — `Kian.SonicDisrupterRadius`). The order rule above still holds: pushed from inside 3, an enemy can land as far as 5 away, so the slow must come first.
- **Drone Strike** 6 → 4 energy. Cooldown unchanged at 2, so the limiter is still the cooldown, and now more often than energy.

**Both emitters are Tech by designer call, not by the §2.2 rule.** Under the rule a self-centred emission stays Normal (Mimi's Cryo Field does). Kian's are recorded as the exception: the Inversion Matrix is a set of deployed emitters and the Disrupter a device he carries, and the designer wants his whole kit on one type. Consequence: **a warded Luka takes nothing from Kian at all** — the only operator a single Hermes' Ring shuts out completely besides Mimi's Cryo-Pulse (§5.12). Riders still land on a warded target: the stun, the slow and the push.

Bots sweep, 800 matches, before → after (noise about ±1.5 points):

| Operator | Before | After |
| -------- | ------ | ----- |
| Kian     | 19%    | 23%   |
| Mimi     | 20%    | 18%   |
| Lethe    | 20%    | 23%   |
| Syla     | 31%    | 32%   |
| Kurbyn   | 32%    | 31%   |
| Javi     | 32%    | 30%   |

Casts per match: Drone Strike 4.58 → 5.87, Sonic Disrupter 2.72 → 3.70, Inversion Matrix 1.82 → 3.05. Mimi's drop is just outside noise: at 6 health she is the operator a wider 2-damage wave hurts most. Watch it.

### 10.7 Nuetu — Bruiser

**HP 7 · Speed 1.0× · Complete — all three abilities implemented**

| #   | Ability              | Type   | Cost | CD  | Range    | Effect                                                                                                                                                                                                                                     |
| --- | -------------------- | ------ | ---- | --- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1   | **Bio-Link Rage**    | Active | 3    | 2   | 2        | Enemy: **3 Normal**, **Burdened 2 turns** (§5.16), and Nuetu **heals 1** — **2** while one of his Killzones is live (ADR-0007). An ally target is refused and costs nothing.                                                              |
| 2   | **Ablative Plating** | Active | 3    | 4   | self     | **Shield** on himself with a **2-point pool** for **2 turns** (§5.6).                                                                                                                                                                      |
| 3   | **Killzone**         | Active | 9    | 4   | 2 (cell) | Deploy a zone on a cell (ADR-0007). At Nuetu's next upkeep it detonates: **1 Normal** and **Stun 2 turns** to every enemy within 1. Then **1 Normal** to each enemy standing in it at his next **two** upkeeps. Nuetu **heals 1** on cast. |

**He needed almost no new engine capability, which is the point.** Bio-Link Rage and Ablative Plating are existing vocabulary. Killzone brought lingering zones and the first board-reading rider (ADR-0007). A seventh archetype, on a taxonomy that still names four (§12).

**Health stopped being what prices him.** Walked 9 → 7 → 6: at 9 he tied the tank, and a second operator at tank health with a shield on top is a better tank, not a bruiser. At 6 he sits level with most of the roster, and with the plate up he is effectively 8 against Normal. **Reach is his real price**: range 2 on everything, no repositioning, no escape, at 1.0. Speed was the original price (a sketched 0.75) and was withdrawn: it sat below the band, and would have floored a single die of 1 to zero cells.

**He is the answer to mobility (designer, 2026-09-21).** Bio-Link Rage now leaves **Burdened** on what it hits for two turns, and Killzone's cooldown drops **6 → 4**. The brief was Kurbyn: permanent haste, 12% evasion, and two actives at range 2 that make him choose when to arrive and when to leave. Damage was never the answer to that — Burdened is, because it is haste's mirror and cancels against it cell for cell (§5.16), so a Kurbyn he has hold of is a plain 1.0 operator with no passive movement at all. It reaches Syla's payout and Lethe's Catalyst the same way, which is the point: he is the anti-mobility operator, not a Kurbyn-specific gadget, and a hard counter would be bad design in a four-seat game where nobody picks their opponent.

- **On the repeatable ability, not the zone.** Something that arrives whenever it likes is answered by pressure, not by a moment: 3 energy and a 2-turn cooldown against 2 turns of burden means he re-makes the choice every turn, and spreading it over two targets holds neither.
- **It is never a stun.** The floor is one cell (§5.16), so a burdened operator always moves; what it takes away is the extra.
- **Killzone at cooldown 4, from 6.** Once every seven turns the trap was almost never standing at the moment somebody chose to close, which is the only moment it answers. The cost stays at 9 and the payload is untouched.
- **Measured, and the sweep cannot see it properly** (4000 matches, paired seeds; plus 1500 head-to-head). Roster-wide he is flat at 24% and Kurbyn is flat at 28–29%; head to head, Nuetu's squad against Kurbyn's with identical fillers, his seat went **34.3% → 36.0%** — a point and a half, about 1.4 standard errors. The bots score a status at a flat worth and never route around a zone or price their own mobility, so denial is the one thing the harness is blind to (BOTS.md). **This one has to be judged at the table.**
- **Killzone's cast rate barely moved** (1.12 → 1.21), which is the third time the same lesson has come back this week: at 9 energy the cost is the limiter, not the cooldown. If it should be on the board more, the dial is 9 → 7.

**Bio-Link Rage is repeated pressure.** At 3 energy the cooldown is the whole regulator, and he can run it most turns — three damage and one health back, repeatedly, beats six once. Normal, not Atomic: a repeatable Atomic hit would make plates worthless against the operator who attacks most often. **Known limitation:** the heal is not lifesteal — it fires even when the damage is evaded or absorbed. **Open design choice (ADR-0007):** the rider reads whether a zone exists anywhere, not whether he stands in it; the positional version is a one-word change and is the stronger design.

**Ablative Plating is deliberately worse than Trauma Plate.** Cut from a 3-point pool over 4 turns, which beat Javi's plate on every axis. At pool 2 it protects as much as a plate does; cooldown 4 against duration 2 is 40% uptime where Javi gets 50%, and self-only is why it costs 3 to Javi's 4. Neural Purge strips it — the rock-paper-scissors the cleanse exists for.

**Killzone: only the detonation stuns, and that is a rule, not tuning.** Stun blocks movement (§5.1); a zone that stunned every tick would hold its victims until it expired. The stun is what the ability is priced on — three Normal over three rounds, telegraphed, is a rider — and the two dials are not independent: the stun is why a victim is still standing there for the later ticks. Damage is per target, the deliberate opposite of Drone Strike. **Cost 9, cooldown 4** (6 until 2026-09-21) — still the longest cooldown in the game, which is what lets the payload be this large. The damage was walked down twice in the same pass as his health; axes moved together are not separately measured.

### 10.8 Sanity — Engineer

**HP 9 · Speed 1.0×, Burdened (§5.16) · Complete — all three abilities implemented**

> **Burdened since 2026-09-17 (designer).** His speed is 1.0 and his crawl is a permanent passive: −1 cell on a roll of 6 or less, −2 above, once per roll. The two blockquotes below are the history of the 0.5 override it replaced.

> **He transgressed a precedent, knowingly, recorded here as a designer override (2026-09-15), not drift.** Speed 0.5 sits below the 1.0–1.5 band (ADR-0002 Amendment 4, §6.3) — the first operator outside it — signed off as the price of the "immovable object" fantasy. He shipped at health 12, tying the maximum the Bouncer cut had vacated, as a second override; a later balance pass the same day took him to **9**, level with the Bouncer.
>
> **The slow-immunity side effect is accepted, not overlooked.** At 0.5 he sits permanently on `MinSpeedMultiplier`, so no slow and no aura can move his speed at all — the floor swallows them (§5.2). That cuts both ways: his own Zero-Day slow is something he can never suffer in a mirror match.

| #   | Ability           | Type   | Cost | CD  | Range | Effect                                                                                                                                                                                                                                                            |
| --- | ----------------- | ------ | ---- | --- | ----- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Short Circuit** | Active | 3    | 1   | 1     | **1 Normal**; **Stun 1 turn** (§5.1).                                                                                                                                                                                                                             |
| 2   | **Zero-Day**      | Active | 4    | 3   | 3     | Attach a charge to an enemy (§6.4): at Sanity's next upkeep it detonates on the target's current cell — **1 Tech** to all enemies within 1, **+1** to the marked target, **Slow 1 turn** to everyone caught. Telegraphed on attach; cleanse-detachable (§5.10).   |
| 3   | **Collision**     | Active | 6    | 3   | 5     | **Dash** to the target, enemy or ally (§7.6): **1 Normal** to every enemy on the traversed cells. Enemy target: **3 Normal** and **Stun 1 turn**. Lands a cell behind the target.                                                                                 |

**Short Circuit is From the Hip's shape with the reach spent on a stun.** Same price, same 1 damage — but Syla's slow at range 3 becomes the strongest control status in the game at melee range, on the slowest operator ever fielded. The range is the whole cost of the ability: he has to be standing next to somebody, and at 0.5× that is the rarest thing on the board.

**Zero-Day is the first operator-anchored deferred effect (§6.4).** A beacon is a bet on where somebody will be; the charge is a delayed certainty that follows them there, and it pays for the certainty in counterplay rather than in damage — telegraphed on attachment, and a cleanse strips the marker and cancels it outright. Javi's Neural Purge answers a 4-energy ability for 6, which is the rock-paper-scissors the cleanse exists for (§5.8). The damage split inverts Drone Strike's: the beacon is strongest against a crowd that scatters its beam, the charge against the one operator it is riding. Tech type (§2.2, 2026-09-15): a homing grenade is the plainest guided device on the roster. Otherwise it is Normal, so a plate absorbs it and an evasion charge can dodge it, a warded Luka blocks it, and the cleanse cancels it. Atomic would make the counterplay one-dimensional, and Atomic is deliberately concentrated (§2.2).

**Collision is the mobility his speed denies him.** At 0.5 he moves three cells on a six; the dash moves him up to seven — six to the target and one past it — in either direction. The ally mode is the escape the rest of the kit refuses to give him, and it is why the ability carries the camping rule (§4.4, second amendment). Priced under Miracle Pull's 9: three Normal and a stun on the anchor plus a rake along the path is less than a possible execute, and the dash cuts both ways — it delivers the slowest operator in the game to exactly where the fight is, which is sometimes where he wanted to be.

**Costs 3 / 4 / 7 were the balance review's outcome (2026-09-15), argued against peers and unmeasured.** A basic priced like From the Hip, a delayed area priced under Drone Strike because it can be cleansed away, an ultimate priced under Miracle Pull because its damage is Normal and its target can be an ally. Adding him shifts the draft's dice stream besides, so nothing here can be compared to figures taken before him. They are now 3 / 4 / 6 (below).

**Pass of 2026-09-20 (designer): health 10 → 9, Collision range 6 → 5.** He had been the joint-highest health on the roster and the dash was the longest reach in the game; both were trimmed one notch after the live sessions, not after a sweep. The dash still moves him six cells (five to the target, one past it), which is still more than any roll he can spend. **Measured (4000 matches, paired seeds): 26% → 24%, the largest move in the pass.** Two notches at once on the operator the sanctuary sweep had just put above average is a real cut — if he reads as weak in play, the health is the one to restore first, because the range cut is what the camping rule was written for.

**Balance pass (2026-09-16, designer): buff Sanity, nerf the speedsters.** The bots sweep had Syla (36%) and Kurbyn (35%) clearly strongest and Kian and Sanity weakest (19%), and human games agreed. Syla and Kurbyn felt too fast, and Kurbyn's evasion on top of his speed was too much, which is why Evasive Protocol went 50% → 30% the day before (§10.3). This pass is the other half: the haste cap (§5.9) trims the speed Syla's payout hands her squad, and Sanity gets more reach and a cheaper, more frequent Collision.

| Ability   | Cost  | CD    | Range |
| --------- | ----- | ----- | ----- |
| Zero-Day  | 4     | 3     | 2 → 3 |
| Collision | 7 → 6 | 4 → 3 | 5 → 6 |

- **Zero-Day at range 3** matches From the Hip and Blind Spot. He no longer has to stand inside the fight to throw it.
- **Collision is no longer priced as an ultimate.** At 6 energy and cooldown 3 it has the same price and cooldown as Ace Shards, Cryo-Pulse, Vendetta and Neural Purge. Range 6 ties Translocation for the longest targeted reach on the roster (Drone Strike's is unlimited, but it aims at a cell). The dash now covers up to seven cells.

**Burdened instead of 0.5 (designer, 2026-09-17).** The designer's read: he was too slow to matter (21% in the bots sweep, after the Lethe and lifesteal changes). Two ideas were weighed, both about 5.4 cells a roll against 0.5's 3.75: **speed 0.75**, or **haste in reverse**. The burden won. It is a subtraction a player can do at a glance, where 0.75 breaks the half-step band and its rounding. It cancels cleanly against haste. And it hands his slow immunity back to the roster as counterplay. A **+1 to each ability's primary damage** (Short Circuit 2, Zero-Day's marked target +2, Collision 4) was also proposed, measured, and not adopted: on top of the burden it overshot.

| Bots against bots, 800 matches, 4 seats | Sanity win share | Turns per seat |
| --------------------------------------- | ---------------- | -------------- |
| Before (0.5, current damage)            | 21%              | 26.5           |
| +1 damage only                          | 24%              | 27.8           |
| **Burdened only (adopted)**             | **27%**          | **25.1**       |
| Burdened + 1 damage                     | 31%              | 25.8           |
| 0.75 + 1 damage                         | 35%              | 25.2           |

- **Adopted row, everyone:** Syla 32%, Kurbyn 30%, Sanity 27%, Javi 26%, Luka 25%, Nuetu 25%, Bouncer 25%, Lethe 22%, Mimi 19%, Kian 19%.
- **Matches got about 1.4 turns per seat shorter**, the largest single cut since the health pass. The slowest piece on the board is usually the one a match waits for.
- **Mimi and Kian slipped** (20% → 19%, 21% → 19%, inside noise), and they were already the weakest. Their buffs are the next pass.

**Measured: the matches changed more than Sanity did.** Bots sweep, 800 matches per row, same seeds. Sanity is not in the alpha squad, so the standard sweep can't see this change.

| Bots against bots, 4 seats    | Before | Collision change only | Both changes |
| ----------------------------- | ------ | --------------------- | ------------ |
| Turns per seat                | 32.5   | 35.0                  | 34.2         |
| Knockouts per match           | 19.9   | 22.4                  | 22.0         |
| Casts per match               | 66.1   | 73.4                  | 72.0         |
| Sanity win share              | 21%    | 22%                   | 22%          |
| Collision casts per match     | 4.81   | 7.17                  | 6.80         |
| Zero-Day casts per match      | 1.85   | 2.38                  | 2.62         |
| Short Circuit casts per match | 3.02   | 3.85                  | 3.50         |

- **Sanity's win share barely moved** (+1 point, about one standard error at 1,047 squads).
- **Collision drives the rest.** On its own it adds about 2.5 turns per seat and 2.5 knockouts per match, and it is cast about 50% more often. Adding Zero-Day's range takes back a little of that (35.0 → 34.2 turns): Zero-Day is cast more and Collision a little less, which fits the two competing for the same energy.
- **Other operators moved by 3 points at most, about two standard errors.** Nuetu 21% → 24% and Bouncer 26% → 27% went up; Mimi 20% → 18% and Luka 24% → 22% went down.
- **Read with care.** The bots cast whatever scores well, and a cheaper Collision scores well more often. Whether a longer match is a problem is a question for human games; the sim only says it happened.

### 10.9 Luka — Duelist

**HP 7 · Speed 1.0× · Complete — all three abilities implemented** _(added 2026-09-15)_

> **He arrived with three amendments:** the Tech type (§2.2), critical hits (§2.4) and the follow-up strike (§6.5), plus two statuses (§5.12, §5.13). The teleport is Collision's landing (§7.6) with no path damage, not a new mechanic.
>
> **Blind Spot was dropped as "L"** and named 2026-09-15. The teleport's device is Hermes' Ring turned outward: a signal spoofer that edits him out of every lens in the room (`OPERATORS.md`). **Hermes' Ring was dropped as a passive** and is built as a self-cast, because a passive with a cost, a duration and a cooldown is something the player triggers.

| #   | Ability          | Type         | Cost | CD  | Range | Effect                                                                                                                                                                                                                              |
| --- | ---------------- | ------------ | ---- | --- | ----- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Blind Spot**   | Active       | 5    | 3   | 3     | **Teleport** to the enemy (§7.6 landing, no path damage); **2 Normal**. **Follow-up** (§6.5): at Luka's next upkeep, if he is within **2** of the target, **1 Normal** — **2** if its max HP is above 6. Hunted marker; cleansable. |
| 2   | **Hermes' Ring** | Active, self | 3    | 4   | —     | **TechWard 3 turns** on Luka (§5.12): Tech damage blocked outright.                                                                                                                                                                 |
| 3   | **Vendetta**     | Active (Ult) | 5    | 2   | 3     | **3 × 1 Atomic** to the target; each blow rolls a **10% critical** (§2.4): ×2, or **×3** if the target's max HP is above 7. **Lifesteal** (§2.5): Luka heals what each blow removes.                                                 |

**"Heavy" is maximum health above 7** (6 until the roster-wide +1 of 2026-09-16) — today the Bouncer and Sanity. Both of his damage riders read it.

**Blind Spot costs 5, one above Zero-Day, because it does more to one target** (dropped at 4, raised by the designer 2026-09-15). Same cooldown; two damage now where Zero-Day deals two a round later, up to two more if the target stays, and up to four cells of free mobility. Zero-Day pays for its certainty with a cleanse and a blast that reaches others; L pays with the escape — the teleport leaves Luka adjacent, so the target has to spend its move getting more than two cells clear — and the extra point. Both hits are Normal; no type was specified.

**Hermes' Ring counters three abilities, one per operator** (§2.2, §5.12): Cryo-Pulse, Zero-Day and Drone Strike, at Ablative Plating's price. It fully shut out only Mimi until Cryo Field landed (2026-09-16) — a self-centred field is Normal, so it now bills a warded Luka through the ward. Three turns on a self-cast covers two full rounds of opponents' turns.

**Vendetta costs 6** (dropped at 9, lowered by the designer 2026-09-15): expected 3.3 damage, 3.6 against a heavy target. At least one blow crits about 27% of the time; the ceiling is 6, or 9 against a heavy target. At 9 it lost to Miracle Pull at the same price; at 6 it sits beside Velvet Rope, the other single-target Atomic cast — 3 certain damage and a pull against 3.3 expected and a swing. A blow that finds its target already down is not thrown (§2.4), so a first-blow kill pays one bounty.

**Vendetta costs 5 with a 2-turn cooldown (designer, 2026-09-20 and 2026-09-21).** The first commit's message said cooldown and its code changed the cost; both halves were intended, and the cooldown followed on 2026-09-21. The side effect of the cost is the one §5.17 now names: at 5 the ultimate is out of Equilibrium's dear band, so Revú no longer halves it and a heavy crit against him is whole. **Measured (4000 matches, paired seeds): the cooldown cut is worth 2.04 → 2.18 casts a match and no movement in his win share (26%)** — energy is what limits the ultimate, not the timer, which is the same result Revú's cooldown cut produced in 2026-09-17.

**Vendetta steals life (designer, 2026-09-17; §2.5).** Luka heals what each blow removes: 3.3 expected against a healthy target, up to 9 against a heavy one, capped at his 7 health, and nothing for overkill. Atomic makes the drain dependable, because nothing mitigates the hits it reads. The ultimate stops being pure burst and becomes the duelist's way back into a fight he is losing. It is also the first cast that heals its own caster out of an enemy. **Bots sweep, 800 matches:** Luka's win share rose from 23% to 26%, the largest move on the table, and nobody else moved more than 2 points. Turns per seat went from 26.9 to 26.5.

**All numbers are the designer's, as dropped, and unmeasured.** Adding him shifts the draft's dice stream, so no figure taken before him compares with one after.

### 10.10 Lethe — Catalyst

**HP 7 · Speed 1.0× (permanently Hastened) · Complete — both abilities and the aura implemented** _(added 2026-09-17)_

> **Bouncer's shape, pointed the other way:** two actives, with an aura in the middle slot. His aura slows enemies near him; hers hastens allies near her. **No new effect kind.** Nano Cell uses existing effects, Catalyst is an aura with a side and a haste flag, and Eris' Exploit is a `DeployZone` with a crowd setting (ADR-0007 Amendment 1).

| #   | Ability           | Type    | Cost | CD  | Range      | Effect                                                                                                                                                                                                                                  |
| --- | ----------------- | ------- | ---- | --- | ---------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Nano Cell**     | Active  | 4    | 4   | 4          | Ally (or herself): **heal 2**, **Shield, 99-point pool, 2 turns** and **Stun, 2 turns** (§5.1, §5.6). Normal and Tech are absorbed; Atomic goes through.                                                                              |
| 2   | **Catalyst**      | Passive | —    | —   | 2          | **Aura, allies only.** An ally within 2 when it starts a move counts as **Hastened** for that move (§5.9). Lethe herself is Hastened by her passive, permanently.                                                                         |
| 3   | **Eris' Exploit** | Active  | 4    | 3   | 3 (cell)   | **Zone, radius 2** (ADR-0007). **On the cast, and again at Lethe's next upkeep**, **each enemy inside takes 1 Normal for every other enemy inside**, as a single hit. One enemy inside takes nothing. Kills credit Lethe. |

**Her speed is haste, not a multiplier (designer, 2026-09-17).** The handoff specced "base 1.0, effective 1.5 through her own passive haste", which was Kurbyn's construction when haste was +0.5 speed. Haste has been flat cells since 2026-09-16, so the passive now gives her +1 cell on a roll of 6 or less and +2 above, once per roll, capped at 3 per turn: about 1.6 cells a roll. The passive carries no magnitude, and `StatusRegistry.SpeedModifier` skips Hastened, so her speed is 1.0 and she sits inside the band. A passive survives cleanses and neutralize (§1.2).

**Health 7, the common figure** (designer, 2026-09-17). The handoff's "above the six-health group" was written before the +1 of 2026-09-16. Her price is that her mobility is capped and conditional on the roll, and that her kit asks her squad to stand together.

**Nano Cell heals 2 on the way in (designer, 2026-09-21).** The bubble used to be worth nothing on an ally that was not about to be hit, and the stun was charged either way; the heal makes it a defensive cast worth making before the blow lands rather than after. It is ally-only, like the rest of the ability, and it resolves before the shield and the stun, so a bubbled ally is healed and then sealed. It also makes her the roster's second healer at 2 — the same figure as Javi's Nanite Infusion and Bouncer's ally mode (§1.1) — which is a role overlap to watch rather than an accident.

**Eris' Exploit costs 4 with a 3-turn cooldown since 2026-09-20 (designer)**, down from 6 and 4. It was the rarest cast on the roster at under one a match, and the zone rewards a clustered board that the bots never give it. At 4 it also left Equilibrium's dear band (§5.17), so its instant hit against Revú is no longer halved. **The reprice bought nothing in the sweep** — 0.94 → 0.93 casts a match — because price was never what stopped it: the bots do not cluster, so there is rarely a second enemy in the circle to bill. Her win share moved 24% → 25%, and most of that is the Nano Cell heal. If it is still rare in human play, the dial is the radius or the crowd rule, not the cost.

**Nano Cell is total immunity bought with total inaction.** A shield whose pool a round of enemy turns cannot empty, plus a stun. Its edges:

- **The stun takes hold at once.** Cast on an ally during her own seat's turn, a status is active immediately (§5), so an ally that has not moved yet loses this turn's move as well. Move first, then bubble. Duration 2 is the minimum that protects at all: it covers exactly one round of enemy turns and takes the ally's next turn.
- **Atomic ignores it** (§2.2): bleed, marks, Velvet Rope, Miracle Pull and Vendetta all go through.
- **It blocks the road.** A collision is Normal damage, so the bubble eats it, the target survives and the mover bounces (§7.2).
- **Neural Purge strips both halves** (§5.8). A Javi on her side can free the ally early; an enemy cannot, because a cleanse only reaches allies. A blanket immunity with no answer would be oppressive, so this is intended. Do not "fix" it.
- **"Cannot be stunned inside" is redundant**, not contradicted: the bubble already stuns. **Status immunity was proposed and dropped**, because it needed an immunity system with a carve-out on day one.
- **Cost 4, not the spec's 3.** At 3 it blanked a 9-energy Killzone or Drone Strike on one ally for a third of the price. At 4 it costs the same as Trauma Plate, and the stun pays the rest.
- **She may bubble herself.** Auras survive stun (§10.1), so a bubbled Lethe keeps hastening her squad from a cell that Normal and Tech damage cannot hurt. Nano Cell is one of the four abilities carrying §10's self-cast opt-in flag (2026-09-17); the self-bubble pays the stun as its price.

**Catalyst is haste, not speed, and it is local** (designer, 2026-09-17). A +0.5 speed aura would have put Syla and Javi at 2.0×, where a mean roll covers a quarter of the loop, because `MovementResolver.EffectiveSpeed` has a floor and no ceiling. Haste is flat cells under a per-turn cap, so nothing can overflow. It is also **not a copy of Tagged From Above's payout**: that one follows the squad anywhere for two turns, while Catalyst ends two cells from Lethe. Its rules:

- **Read where the move starts.** An ally that walks out of range keeps the cells of the move that took it out, and still pays for them against the turn cap. If Lethe moves away first, the ally behind gets nothing.
- **A yes or no, not a stack.** Catalyst on top of a payout, or on top of another haste, still pays one bonus per roll under one cap.
- **Visible.** `GameEngine.ActiveStatusesOn` lists Hastened on an ally inside the aura, so the HASTE tag appears and disappears as pieces move.
- **Not on herself.** Her own haste is the passive.

**Eris' Exploit is the anti-clustering ability.** N enemies inside take N−1 each per tick:

| Caught | Each, per hit | Each, both hits | Total |
| ------ | -------------- | ---------------- | ----- |
| 1      | 0              | 0                | 0     |
| 2      | 1              | 2                | 4     |
| 3      | 2              | 4                | 12    |
| 4      | 3              | 6                | 24    |

- **Three cell abilities, three shapes.** Drone Strike divides a fixed payload, so it is best against one. Killzone bills each victim in full, so it grows linearly with the crowd. Eris' Exploit bills each victim for the rest of the crowd, so it grows quadratically.
- **Radius 2, not the spec's 3.** Five cells limit the crowd by geometry. At seven cells, four victims were routine on stacked safe cells: 24 damage for 6 energy, against Ace Shards' 4 each at the same price. At radius 2 a crowd of four needs four enemies inside five cells, and since the +1 health, six damage kills only Mimi outright.
- **It strikes on the cast, then once more at her next upkeep** (designer, 2026-09-18; ADR-0007 Amendment 2). It was a plain zone — nothing at cast, two later ticks — and the crowd could simply scatter, which made a 6-energy cast worth nothing often enough that she sat at the bottom of the sweep. The first hit is now undodgeable; the second still is, so a crowd that breaks up has still been controlled, and the total against a crowd that stays put is unchanged.
- **The crowd is counted again at the tick**, not fixed at the cast.
- **Only the instant hit is a cast hit**, so only it carries the ability's cost and meets Revú's Equilibrium (§5.17). A deferred tick has no cost to read.
- **The bots cannot measure this change.** They never scatter out of a zone, so they always ate both ticks; the sweep reads 22.9% → 22.7% and 0.96 → 0.97 casts, which is noise. The change is worth what human opponents' dodging was worth, and nothing less.
- **One hit per victim**, so an evasion charge or a plate meets it once, as with a Killzone tick. **A lone victim is not hit at all.** A zero-damage instance would still spend its evasion charge.
- **Normal, credited to Lethe**, as Killzone is credited to Nuetu. Areas reach safe cells, where the damage is voided (§4.4).
- **Squadmates' devices no longer overwrite each other (2026-09-17).** Cell effects were keyed on cell and seat, so a Lethe casting on a squadmate Nuetu's Killzone cell replaced it. They are now keyed on cell, seat and source operator; the same operator re-casting on its own cell still replaces. Bio-Link Rage's rider now reads only Nuetu's own zones: "while one of his Killzones is live" (ADR-0007).
- **The name** borrows Greek myth, like Hermes' Ring and her own name. Kept as the designer wrote it.

**Measured, bots sweep, 800 matches, 4 seats (2026-09-17).** She is the tenth operator, so every seed's draft changed and no row compares one-to-one.

| Bots against bots          | Before Lethe | With Lethe |
| -------------------------- | ------------ | ---------- |
| Turns per seat             | 28.2         | 26.9       |
| Knockouts per match        | 13.6         | 12.4       |
| Casts per match            | 60.4         | 55.3       |
| Lethe win share            | —            | 24%        |
| Nano Cell casts per match  | —            | 1.17       |
| Eris' Exploit casts/match  | —            | 1.04       |

- **Lethe lands at 24%, neutral** (25% is an average seat). Every other operator is within 3 points of its previous share: Kurbyn 31% → 33%, Syla 32% → 30%, Javi 29% → 27%, Mimi 20% → 21%, Kian 20% → 22%, Sanity 22% → 21%.
- **Matches got about a turn shorter** and knockouts fell by about one. Catalyst's extra cells and Nano Cell's protection both point that way. The sim can't tell which.
- **The bots had to learn four things** (`BOTS.md`): a shield is as deep as its remaining pool, a stun on an ally is a cost, a cleanse also strips the ally's bubble, and a crowd zone scores nothing on a lone enemy.
- The "with" run was built just before the device-key fix above, which only matters when a Lethe and a Nuetu share a seat and a cell.

**All numbers are the designer's (2026-09-17)** and reasoned against peers. Only the sweep above has measured them.

### 10.11 Revú — Loan Shark

**HP 8 · Speed 1.0× · Complete — both abilities and the passive implemented** _(added 2026-09-17, from `OPERATOR_DRAFTS.md` §3; tuned the same day, below)_

> **The punishment web.** His ultimate scales with what the enemy has spent. His passive turns the cheap answers to him into fuel for that ultimate, and his basic ability drains the pool further. **Bouncer's shape:** two actives with a named passive in the middle slot, ids 1101 and 1102. The draft planned 1101–1103 under the operator-number scheme; Fuse and Ghost's planned ids are now Luka's and Lethe's.

| #   | Ability         | Type         | Cost | CD  | Range | Effect                                                                                                                                           |
| --- | --------------- | ------------ | ---- | --- | ----- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1   | **Leech Round** | Active       | 3    | 1   | 3     | **2 Normal**; **drain 2 energy** from the target's seat (§3.3), destroyed.                                                                        |
| 2   | **Equilibrium** | Passive      | —    | —   | self  | A cast's instant damage to him is **×2** at cost ≤ 3, **½ (at least 1)** at cost ≥ 6, unchanged at 4–5 (§5.17).                                   |
| 3   | **Sadist**      | Active (Ult) | 7    | 3   | 3     | **1 Normal per 3 energy** the target's seat is missing, **at least 2** (0 → 4, 6 → 2, 12 → 2); **half that** to enemies within 2 of the target (§3.3). |

**Health 8, not the draft's 4.** The draft's open question reasoned at 4, before the roster-wide +1 and the draft-health pass (`c0db409`) that set him to 7. The designer's tuning then took him to 8. At 8, a doubled From the Hip (2) or Short Circuit (2) still needs friends, and a doubled Bio-Link Rage (6) leaves him at 2. That is the web working: the cheapest abilities are the ones he fears most.

**Designer's rulings (2026-09-17):**
- Equilibrium reads **instant hits only**.
- Its halves are **at least 1**. Floored halves would have left Vendetta doing nothing to him.
- Sadist's figure comes **from the target's seat**, and the splash is half of it.
- Leech Round's energy is **destroyed**, not transferred.
- On top of those: Equilibrium applies to Atomic, and an execute still kills. Neither was asked; both are stated here so they are decisions rather than accidents.

**Readability, as the draft demanded:** "one damage for every 3 energy missing from their pool" is a figure the target's owner can compute before deciding to spend.

**Mirror match:** Sadist is a 7-cost cast, so Equilibrium still halves it against another Revú, to 2 at most and 1 at the floor. Leech Round is cheap, so its 2 lands as 4.

**Measured, bots sweep, 800 matches, 4 seats (2026-09-17).** He is the eleventh operator, so every seed's draft moved again.

| Bots against bots          | Before Revú | With Revú |
| -------------------------- | ----------- | --------- |
| Turns per seat             | 25.1        | 24.7      |
| Knockouts per match        | 11.5        | 11.1      |
| Revú win share             | —           | 21%       |
| Leech Round casts per match | —          | 1.53      |
| Sadist casts per match     | —           | 0.88      |

- **Revú lands at 21%, below average**, level with Bouncer and above Kian (20%) and Mimi (17%).
- **Everyone else, with Revú in the pool:** Kurbyn 33%, Javi 31%, Syla 30%, Luka 27%, Nuetu 26%, Sanity 25%, Lethe 24%.
- **The bots only half play the web.** They do score a cheap cast on him higher, but nothing in the planner holds energy back to blunt Sadist, and nothing chases collisions against him. Human play may rate him higher or lower.
- **Dials, if he stays low:** health 7 → 8, Leech Round's drain 2 → 3, or Sadist's cost 9 → 8. Each is a one-line change in `Revu.cs`.

**Repriced 2026-09-21 (designer): 9 → 7 energy, cooldown 4 → 3, and a floor of 2 on the primary figure (§3.3).** Two of the three dials the 2026-09-17 measurement had listed, plus the floor, which answers the flaw the sweeps never punished: against a seat at cap the ultimate computed zero and the cast was simply thrown away. At 7 it is reachable on the turn after a Leech Round rather than two turns later, which is what makes the drain-then-collect combo a plan instead of a coincidence. **Measured (4000 matches, paired seeds): Sadist 0.77 → 1.10 casts a match, and Revú 23% → 24%.** The cast frequency is the point; the win share moved less than the noise band.

**Tuned by the designer the same day (2026-09-17):** health 7 → 8; Leech Round damage 1 → 2 and cooldown 2 → 1, so it can be cast every turn; Sadist cooldown 5 → 4. Bots sweep, 800 matches:

| Bots against bots          | As built | Tuned |
| -------------------------- | -------- | ----- |
| Revú win share             | 21%      | 24%   |
| Leech Round casts per match | 1.53    | 2.87  |
| Sadist casts per match     | 0.88     | 0.88  |
| Turns per seat             | 24.7     | 24.8  |

- **Revú is now at 24%, inside noise of average.** Leech Round is cast almost twice as often. Sadist is unchanged, because energy limits it, not the cooldown.
- **Everyone else, tuned:** Kurbyn 33%, Syla 33%, Javi 29%, Luka 25%, Lethe 25%, Sanity 24%, Nuetu 23%, Bouncer 23%, Kian 19%, Mimi 16%.
- **The bottom is now Kian and Mimi alone**, both further down than before. Their buffs are the open item.

### 10.12 Fortuna — Dealer

**HP 7 · Speed 1.0× · Complete — three abilities and the House Edge** _(added 2026-09-18)_

| #   | Ability            | Type         | Cost | CD  | Range      | Effect                                                                                                                                                                  |
| --- | ------------------ | ------------ | ---- | --- | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1   | **The House Edge** | Passive      | —    | —   | self       | Once a turn, one unspent die she could have moved is **cashed**: the seat gains **2 energy** and the die is consumed (§3.4, §5.18).                                       |
| 2   | **Deal Again**     | Active, self | 3    | 1   | —          | **Re-roll the lowest unspent die** (§6.8).                                                                                                                              |
| 3   | **The Table**      | Active       | 5    | 3   | 4 (cell)   | A **table** on an outer-track cell for three of her turns. The first enemy dice move that crosses or ends on it **stops there** and takes **2 Normal**, once per operator (§7.7). |
| 4   | **Boxcars**        | Active (Ult) | 7    | 4   | —          | Both unspent dice **become sixes**, and the dealt double is owed its roll inside the turn's budget (§6.8).                                                               |

**Eleven operators fight over the board; she owns the dice.** Her kit reads health only in passing — two damage in the whole of it — and every other operator on the roster spends a currency she manufactures. She is the first operator whose subject is the roll, and the only one who makes the race itself contestable.

**The House Edge is the third thing a die can be spent on.** §6 has said since the core was written that every die is consumed by a deploy or a move; she is the other answer, and specifically the answer to compulsory movement (§6.1) — the one rule in the game that can force a player into a mistake. It does not repeal it, it prices it, once a turn. **The race polices it by itself:** cash more than a handful of dice a match and three operators do not get home, which is why the figure needed no cooldown.

**Deal Again is cast for the specific, not the average.** Three energy for an expected two and a half pips is a bad trade; a six that deploys an operator out of the yard is half a lap of lost progress bought back for three, and an exact number lands a collision that costs no energy at all. It re-deals the lowest die by ruling (§6.8).

**The Table is the roster's first structural counter to speed.** Every other answer to a fast operator is a slow, and slows have a floor (§5.2) and stack in two channels the doc admits are in conflict (§12). A table makes speed itself a liability: the faster you move, the more cells you cross, and the likelier one of them is hers. Its counterplay is four kinds of cost — route the die elsewhere, split and stop short, bypass with placement, or walk in — and the third is the good one, because it makes eight existing abilities newly valuable rather than adding a counter of its own. **And it makes the finish contestable without touching the finish** (§4.3, §7.7).

**Buffed 2026-09-21 (designer): The Table 6 → 5 energy and two turns → three, Boxcars 9 → 7.** She had been in the bottom half of every sweep since she landed, and the two casts that were overpriced were the two that do not touch health. The table at two turns covered exactly one round of everyone else's movement, so an opponent who routed around it once had paid for it once and was done; at three the avoidance is charged twice, which is where the routing cost the ability is built on actually lands — and at 5 it is no longer priced beside ultimates that kill. Boxcars at 9 was three quarters of the cap for an ultimate that kills nobody, reachable only after a hoard that cost her the race; at 7 it is a turn or two earlier. **The passive was deliberately left alone** — raising the cashed die to 3 is the dial that feeds the stall below, and the speed is the dial §10.12 already forbids. The health stays at 7: being defenceless is the price of owning the dice. **Measured (4000 matches, paired seeds): Fortuna 24% → 27%, The Table 2.31 → 2.92 casts a match and Boxcars 2.65 → 3.30; dice sold 5.14 → 5.36.** That is +3 points against a ±0.7 band — more than the "a little" the pass was aimed at, and it puts her third. Nobody else moved beyond noise and the spread is still 5 points (23–28), so it is not yet a problem; the trim, if she reads strong in play, is Boxcars back to 8, which is the half of the pass that moved the most casts.

**Boxcars is the only ultimate that cannot kill anybody.** Twelve pips chosen rather than rolled, plus the roll a double is owed: about twelve pips of tempo over an average roll once the extra roll is counted, or two deploys and a squad back from a wipe. Priced at nine beside the other ultimates and deliberately worth less than Miracle Pull, which deletes an operator and takes half a lap with it. She is never the reason somebody dies; she is the reason somebody arrives.

**Why she is not broken, in the rules rather than in the numbers.** The race polices the passive. She cannot defend herself — seven health, 1.0×, no shield, no ward, no escape, no cleanse, the only operator with three abilities and no answer to anybody walking up to her. Her bank is a target: Revú's Leech Round destroys exactly what she manufactures, and Sadist is worse against a full pool, so the two money operators counter each other by construction. And nothing she does is hidden.

**Do not raise her speed.** At 1.5× she banks money and races, and the passive's whole cost is the tempo she gives up.

**First bots sweep, 800 matches**, measured on the 2026-09-18 balance pass (twelve operators in the pool, so the draft distribution moved under it):

| Operator | Win share | | Operator | Win share |
| -------- | --------- | - | -------- | --------- |
| Kurbyn   | 28%       | | Luka     | 25%       |
| Kian     | 28%       | | Lethe    | 23%       |
| Sanity   | 27%       | | **Fortuna** | **23%** |
| Javi     | 27%       | | Nuetu    | 22%       |
| Bouncer  | 26%       | | Mimi     | 22%       |
| Syla     | 26%       | | Revú     | 22%       |

Casts per match: Deal Again 2.96, Boxcars 2.62, The Table 2.54. **Dice sold: 5.35 per match fielding her** — inside the four-to-eight band the design predicted, and the figure to watch: a Fortuna selling fewer than four is a bot problem, not a balance result. Turns per seat 26.7, neutralizes 13.4, refusals 0.

**She lands mid-field on her first measurement and nothing else moved much**, which is the result a utility operator should produce — and the field she joined is the tightest it has been (22% to 28%, where the sweep before the balance pass ran 19% to 32%).

**The stall nobody has seen yet.** A player who has given up the race can cash every turn and become a pure combat engine that out-casts three opponents. The cap and the burn rule blunt it and losing is losing, but it is a strategy the game has never had. **Watch it in the first human session**, not in the sim.

---

## 11. Superseded and removed

| Thing                                                        | Status                                                                                                                                                                                               |
| ------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| "Exactly one operator moves per roll" (§6)                   | **Dead.** Replaced by per-die consumption and compulsory movement (§6, §6.1).                                                                                                                        |
| Deploy having to precede movement in a roll                  | **Dead.** An artifact of computing the movement total up front from the whole roll; a deploy now simply removes a die (§6).                                                                          |
| `cells = floor(DiceTotal × EffectiveSpeed)`                  | **Restated** as `floor(Pips × EffectiveSpeed)`, where Pips is what this move spends (§6.3). Amended 2026-09-15: below 1.0× the half cell rounds up.                                                  |
| Tiered energy (≤4 → 1, 5–8 → 2, ≥9 → 3)                      | **Dead.** Replaced by §3.1.                                                                                                                                                                          |
| "3 energy points per turn to spend" (GDD)                    | **Dead.** Never closed against 9-cost ultimates.                                                                                                                                                     |
| `Energy Efficiency` stat                                     | **Cut.** One value on one operator, blank on two, no rule ever attached.                                                                                                                             |
| `Energy` as a per-operator stat                              | **Cut.** The pool is player-level.                                                                                                                                                                   |
| Shield as "2 hits to capture"                                | **Rewritten** as §5.6 — the capture system it described no longer exists.                                                                                                                            |
| Shield absorbing a whole instance whatever its size          | **Replaced** by §5.6's pool, landed 2026-09-13 with Trauma Plate. `TryAbsorb` became `AbsorbFrom`.                                                                                                   |
| Deterministic evasion (flat reduction of 1)                  | **Declined** 2026-09-13 (`_HANDOFF_mitigation.md`, retired). The roll stays; `EvasionChance` moved 0.5 → 0.3 instead (§5.5).                                                                         |
| "There is no passive regeneration" (§1.1)                    | **Replaced** by gated regeneration (§5.11).                                                                                                                                                          |
| Tagged From Above at cooldown 2                              | **Raised** to 4 (§10.2). At 2 it limited nothing the energy drip did not already.                                                                                                                    |
| Javi's abilities at range 3                                  | **Raised** to 5 / 4 / 5 in the 2026-09-15 balance pass (§10.5).                                                                                                                                      |
| Trauma Plate at 6 energy, range 3                            | **Repriced** to 4 and built (§10.5); range then raised with the rest of his kit.                                                                                                                     |
| Sanity at 12 health                                          | **Lowered** to 9 the day he shipped (§10.8). The speed override stands.                                                                                                                              |
| Drone Strike's beam at 6 total                               | **Lowered** to 4 (§10.6). A correct guess no longer kills anyone from full.                                                                                                                          |
| Inversion Matrix's line at 6 cells                           | **Shortened** to 4 (§10.6).                                                                                                                                                                          |
| Mark as bookkeeping-only, applying no modifier               | **Rewritten** as §5.7 — it now deals damage over time.                                                                                                                                               |
| Tagged From Above's "within 3 of Syla's turns" payout window | **Replaced** by the mark's own duration (§5.7, §10.2).                                                                                                                                               |
| "A collision is always exactly 1v1" (§7.2)                   | **False.** Contradicted §4.5. Replaced by §7.5.                                                                                                                                                      |
| §7.4 covering pulls only                                     | **Extended** to swaps, with the cells-versus-progress conversion and the forwards case stated for the first time.                                                                                    |
| The pull clamp as "owed a doc amendment"                     | **Discharged.** Stated in §7.4.                                                                                                                                                                      |
| Speed band 1.5–2.0 (Amendment 2)                             | **Lowered** to 1.0–1.5 by Amendment 4. Bouncer 1.5 → 1.0, Syla 2.0 → 1.5, Kurbyn 1.5+0.5 → 1.0+0.5.                                                                                                  |
| Speed band 1.0–1.5 as a roster-wide invariant                | **Amended** 2026-09-15: Sanity fields at 0.5, the first recorded exception (§10.8) — a designer override, bought with the slow-immunity side effect stated there. The band stands for everyone else. |
| Standard board 48/6                                          | **Replaced** by 52/6 (ADR-0002 Amendment 5). 48 cannot be drawn as a continuous Ludo cross; journey 54 → 58.                                                                                         |
| Bouncer at 12 health                                         | **Lowered** to 9 (§10.1). He absorbed four collisions and shrugged off the sequence that kills everyone else.                                                                                        |
| Velvet Rope as 3 Normal at range 3                           | **Retuned** to 3 **Atomic** at range 4, then **back to range 3** (§10.1). Atomic stayed; the reach did not.                                                                                          |
| All-In Mauling at range 1 with 3 damage and 3 self           | **Retuned** to range 2, 2 damage, 2 self, then repriced 2026-09-18 to 4 energy, cooldown 1, 3 damage, 1 self, heal 2 (§10.1).                                                                                                                                                    |
| Miracle Pull at range 1                                      | **Widened** to 2 (§10.3). At 1 the ult was often unspendable when it was worth spending.                                                                                                             |
| Miracle Pull at cooldown 2                                 | **Raised** to 3 (§10.3), with the rebuild below.                                                                                                                                                    |
| Predator's Read (Kurbyn's third active, id 303)            | **Removed** 2026-09-17 (§10.3): the kit read as three actives plus a passive's worth of defence, and the evasion was meant to be a single ability. The watch machinery (§5.15, §6.7, `EffectKind.Watch`) stays in the core, dormant. |
| Evasive Protocol at 30% with a +0.5 speed rider            | **Rebuilt** 2026-09-17 (§10.3): the roll is 12%, and the speed rider became permanent flat haste capped at 2 cells a turn.                                                                            |
| Intimidating Presence at radius 2                            | **Widened** to 3, matching Velvet Rope's reach (§10.1).                                                                                                                                              |
| From the Hip at 2 base damage                                | **Lowered** to 1 (§10.2). The bleed profile carries the ability; the base does not.                                                                                                                  |
| Cryo-Pulse at 4 energy                                       | **Repriced** to 6 (§10.4). It did more than either 6-cost area ability for two-thirds the price.                                                                                                     |
| The 3 / 6 / 9 cost tier                                      | **Abolished** 2026-09-13 (§3.2). Three abilities were priced off it deliberately; no price moved when it went.                                                                                       |
| Javi's third ability as 50% damage reduction                 | **Replaced** by Neural Purge (§10.5). It duplicated Trauma Plate, lost on cost, range and absorption, and needed fractional health.                                                                  |
| An ability with no player-facing description                 | **Dead.** The constructor requires one and throws on an empty string (§10).                                                                                                                          |
| `CollisionDamage` as "the first dial"                        | **Withdrawn** (§12). Measured at 0.4 turns across a 2→6 range — but see §12 on splitting, which attacks the premise.                                                                                 |
| Ludo capture (land → instant send-home)                      | **Replaced** by collision (§7).                                                                                                                                                                      |
| `[Range(3, 9)]` on `Operator.maxHealth`                      | **Dead**, and still dead — Bouncer at 9 is coincidence, not the rule returning.                                                                                                                      |
| Player elimination ("until one player is left")              | **Not a mechanic** in the MVP (§1.2).                                                                                                                                                                |
| `GameEngine.CheckAbility` answering Ready for a caster in a home column | **Fixed** 2026-09-18. It tested the yard and HOME but not the column, where the resolver has always refused (§4.3) — so a tray would have drawn the ability as castable and a bot proposed it. Found by Fortuna, whose self-cast abilities are the first a seat wants while an operator is on its last stretch. |
| `MeshRenderer` fallback on `Operator`                        | Already dead (ADR-0001).                                                                                                                                                                             |

---

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

- **The harness fields the alpha three.** None of Mimi's, Javi's, Kian's, Nuetu's or Sanity's abilities has run in a simulated match, and §2.2's concentration question — a legal draw with no answer to Evasion — has never been exercised.
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
6. `MarkDamagePerTurn = 2`, `HasteDurationTurns = 2`, the haste cells (+1 / +2 around a roll of 6) with `HasteBonusCellCap = 3`, `SlowSpeedPenalty = 0.5` — all still set by reasoning and unmeasured in isolation.
7. `EnergyCap = 12` against a `floor(total/2)` drip. **Burn is 12.1 per match at the shipping configuration**, up from 8.0 without the bounty. That is the economy's headroom, and it is the figure to watch if the bounty ever moves.

### Consequences of compulsory and split movement — split still unmeasured

- **Compulsory movement is in these numbers.** Split movement is not: `ScriptedPlayer` pools.
- **Up to six operator-moves in a turn** once players split — two per roll against one, and `MaxRollsPerTurn = 3`.
- **Landings per roll roughly double when players split**, and a landing is the collision trigger (§7.1). Collisions already rose to 5.1 from 2.4 under compulsory movement alone; splitting attacks the same number again.
- **Kill bounties become easier to farm.** Two collisions per roll is two chances at a bounty where there was one, and the bounty is now the sharpest dial in the table. Neither feature was designed against the other.
- **Evasion's per-round cap is worth less.** The cap bites on the first Normal instance only, so the second landing of a split is strictly more likely to connect (§5.5) — and at `EvasionChance = 0.3` the first one is likelier to connect too.

### Open, unresolved rules conflict

- **Across auras, resolved (2026-09-17): the strongest bonus plus the strongest penalty.** `AuraRules` used to take the largest aura by absolute value and keep the first one it met on a tie, so a +0.5 and a −0.5 would have resolved by operator id order. Each sign now resolves on its own and the two add, which gives the same result as before for every shipped aura (all are negative, or grant haste). The status-versus-aura question below is still open.
- **Slows and auras stack, and §5.2 says they should not.** `GameEngine` sums two channels when computing effective speed — `StatusRegistry.SpeedModifier` and `AuraRules.SpeedModifierFor` — so From the Hip's slow and Bouncer's Intimidating Presence apply together. Within each channel the rule holds; across the two it does not.
  Both readings are defensible: an aura and a status are arguably different things, and a tank's presence compounding a wound is reasonable. But the doc says one thing and the code does another, which is the state this project exists to avoid. **Decide it.** The stakes rose again with Cryo-Pulse, which slows an entire area, and again with §6.1, where a deep enough slow decides whether a turn can be ended at all.
- **The kill bounty is implemented and this document never states it as a rule.** §9.1 records that `NeutralizeRules` owns it and §12 ranks it first among dials, but no section of §1 or §3 says what it does. A reader could work through the whole document and not learn that killing pays.

### Open, undecided

- **Whether the 15–20 minute budget still stands.** It was set in ADR-0002 before anyone had played the game, and every board except the two smallest now exceeds it. It may be the budget that is wrong rather than the board — but that is a decision somebody has to take, not a number to tune toward silently.
- **The Force damage type, and what amplifies Tech.** A four-type matrix — Normal, Force, Tech, Atomic, across Evasion and Shield — was the agreed model. **Tech landed on 2026-09-15** (§2.2) as Normal plus a ward counter, with "can be amplified by specific abilities" as the stated direction and no amplifier yet. Force is unbuilt. Which operators deal Tech was settled 2026-09-15 (§2.2): Cryo-Pulse, Zero-Day, Drone Strike. **Unmeasured:** a warded Luka against a Mimi squad, and the split-soak on Drone Strike (§5.12).
- **A draft has no balance constraint.** Nothing checks that a squad has an answer to Evasion, a way to heal, or reliable damage. With Atomic in three operators (§2.2), a legal draw can produce a squad with no way through Kurbyn.
- **Luka's numbers** (§10.9). Blind Spot (then L) repriced 4 → 5 and Vendetta 9 → 6 by the designer on first review; reasoned, not measured.
- **A speed ceiling.** Settled 2026-09-17 as the per-turn speed bonus cap (§6.3): `EffectiveSpeed` still floors at `MinSpeedMultiplier` with no hard maximum, but no operator collects more than `SpeedBonusCellCap` (2) bonus cells a turn, which is the ceiling that matters. A positive speed aura or status would still need a look — the cap trims its payout, not its legality.
- **Drone Strike can split to zero.** Four damage over five or more caught enemies gives 0 each, and a zero-damage Normal hit still spends an evasion charge (§5.5). Rare, and left alone; Eris' Exploit skips the hit instead.
- **The draft grid holds twelve.** The draft screen keeps three rows and widens to four columns for 10–12 operators (`DRAFT.md`). A thirteenth operator (Fuse, Ghost and Revú are drafted) needs a layout decision, not a smaller card.
- **The draft picker reads Fortuna last** (2026-09-18). `DraftPicker.Value` is built from damage, burst and sustain, and she has none of the three; a tempo term (`DraftTempo`, one per dice ability and one for the House Edge) lifts her but not past the field. It costs nothing in the sweep, where squads are drafted at random, and it is wrong on the draft screen. Recorded as an honest failure rather than a weight bent until it passed — `FortunaBotTests` pins both the term and the ranking.
- **Cashing dice as a stall** (2026-09-18, §3.4). A seat that has given up the race can sell a die a turn and out-cast three opponents. The cap, the burn rule and the win condition all push against it, and the sim cannot see it because a bot never gives up racing. **A human session is the only instrument.**
- **Nona means nine.** Lethe is the tenth operator. Whether the title's number is canon is open (`OPERATORS.md`).
- **Whether the same operator may take both dice in two steps.** §6 allows it, and it is Ludo-standard. It is also strictly worse in cells and strictly better in landings, which makes it a deliberate two-collision play rather than a mistake.

### Open, unmeasured

- **Two-player is a different game, not a smaller one.** 1.0 neutralizes against four-player's 9.3 — a ratio near 1:9, and it has held through every rules change. Combat scales with the number of _pairs_ of players, so it is an emergent property of crowding rather than of any rule. **Nothing in this table will fix 1v1.**
- **Javi may move the free-rider question.** Neutralizing now pays the attacker energy, which was the answer to killing being a public good — but a dedicated healer with a castable shield makes kills materially harder to land, and so do Nuetu's plate and regeneration (§5.11). No measurement has ever included any of them.
- **Whether any of this is fun.** The harness reports pacing and throughput and says nothing about whether the density reads as tension or as thinness. Only a human can.

### Scope

1. **Special spaces** are deferred (ADR-0003). Shield is defined (§5.6); Teleport, Slippery, Checkpoint, RollAgain and SharksTable are not. Checkpoint conflicts with §1.2's "return to yard" and needs an explicit exception when it lands.
2. **"Brawler" is a fifth archetype** (Kurbyn) outside the base four, and the base four are now filled. Add it or re-tag.
3. **The taxonomy names four archetypes; the roster now has more than that** (tank, assassin, brawler, controller, support, artillery, bruiser, Sanity's engineer, Luka's duelist and Lethe's catalyst). Rewrite it or drop it rather than stretch it.
4. **Opt-out of home entry** (ADR-0003, §8) — designed, deferred. Note the overlap with Translocation, which delivers a version of home denial for 3 energy, and that splitting produces two landing decisions per roll where opt-out assumes one.
5. **Two walks per roll.** `PRESENTATION.md` §3 has pieces walking the track cell by cell; a split produces two, and on the same piece they must be sequenced rather than overlapped.
6. Flavour and world placement across the roster (`OPERATORS.md`).

### Unmeasured dials — reasoning on record

Carried from the pre-2026-09-14 version of this section, which the table above supersedes for ranking. The reasoning still applies until each is swept on its own.

- **Journey length is geometrically pinned.** The circuit must satisfy `8L + 4` to be drawable (ADR-0002 Amendment 5), so the nearest alternatives to 52 are 44 and 60. It is a board decision, not a free dial.
- **`MarkDamagePerTurn = 2`** over a 2-turn mark: 4 total leaves a 6-HP target at 2, inside collision range and Miracle Pull's execute window, while 9 would kill unassisted and make the ult's payout self-fulfilling.
- **`SlowSpeedPenalty = 0.5`** takes a 1.0 operator to the `MinSpeedMultiplier` floor (§5.2). It was not re-measured when the band moved, and Intimidating Presence and Cryo-Pulse make it land more often. It also interacts with §6.1: a heavily slowed squad can reach the state where a roll has no legal consumer.
- **`HasteDurationTurns = 2`; haste +1 on a roll of 6 or less, +2 above** (§5.9, 2026-09-16). Replaced `HasteSpeedBonus = 0.5`. Before/after at 800 matches (bots sweep): noise only. Syla 31% → 32%, Kurbyn 31% → 31%, turns per seat 28.2 → 28.2. The payout fires about once a match (Tagged From Above: 0.95 casts), so the sim cannot see it; only human games can.
- **`HasteBonusCellCap = 3`** (§5.9) — the designer's number (2026-09-16), kept when haste became flat cells; it now only binds on doubles turns. When it first landed it limited the old +0.5 speed to 3 cells per operator per turn. Before/after at 800 matches it changed nothing beyond noise: the standard table's adopted row is identical (20.3 turns, 5.5 neutralizes, 40% 3-up), and Syla's bot-vs-bot win share went 31% → 30%. The payout fires about once a match (Tagged From Above: 1.15 casts), so the sim cannot show a cap on it. Only human games can.\r
- **`SpeedBonusCellCap = 2`** (§6.3) — the designer's number (2026-09-17), adopted to even up the field. At 2 it binds on big moves only: a single 6 at 1.5×, or a pooled roll. Bots sweep, 800 matches before/after: Syla 31% → 30%, Javi 28% → 27%, Kurbyn 32% → 33% (noise), Bouncer 21% → 24%, Mimi 21% → 23%, Revú 23% → 20% (watch); turns per seat 26.6 → 27.8. A cap of 1 was measured and not adopted: Syla 27%, Javi 27%, and Kurbyn still 32% — the trim does not reach him at either setting, because his edge is the evasion.
- **`EvasionChance = 0.12`** (§5.5) — the designer's number (2026-09-17), the third setting after 0.5 and 0.3. The sweep that measured it is confounded with the rest of the Kurbyn rebuild (§10.3): 33% → 28% for the package, not the dial alone. If he still reads as unkillable in human games, the deterministic-evasion proposal (§11) is the next pass, not a fourth rate.
- **`RegenEveryTurns = 3`, `RegenAmount = 1`** (§5.11) — A/B 0 against 3 before trusting either. **Not live in code:** the `CombatConfig` constructor never assigns either field, so both read 0 and regeneration never fires. Found 2026-09-16; enabling it is a balance change and waits for a decision.
- **Sanity's Collision at 6 / 3 / 6** (§10.8, 2026-09-16) — the bots sweep puts about 1.7 turns per seat and 2 knockouts per match on the four-seat game. Watch match length in human games before touching anything else.

### Match length and knockouts (adopted in part, 2026-09-16)

**Adopted:** +1 health across the roster (§1.1) and regen at 1 every 3 turns for any wound, off safe cells (§5.11). The designer chose that over 0.5 per round as the less drastic step. The exploration that led there follows the result.

| Shipped vs before, 800 matches            | Before | After |
| ----------------------------------------- | ------ | ----- |
| Bots against bots: turns per seat         | 34.2   | 28.0  |
| Bots against bots: knockouts per match    | 22.0   | 13.4  |
| Bots against bots: casts per match        | 72.0   | 59.8  |
| Standard sweep (alpha three): turns       | 20.3   | 19.2  |
| Standard sweep (alpha three): p90         | 25     | 22    |
| Standard sweep (alpha three): neutralizes | 5.5    | 3.4   |

- **Matches are 18% shorter for the bots, and knockouts fall 39%.** That is a large cut in a game whose stated priority is 70% combat. Human games decide whether it went too far. If they feel toothless, take regen back first: `RegenEveryTurns = 0` switches it off.
- **Win shares moved by 3 points at most:** Kurbyn 33% → 31%, Syla 30% → 31%, Javi 28% → 29%, Bouncer 27% → 25%, Nuetu 24% → 24%, Luka 22% → 23%, Kian 20% → 21%, Mimi 18% → 21%, Sanity 22% → 20%. Sanity gives back most of his 2026-09-16 buff.
- **Bots now beat the scripted players 70% of the time**, up from 65–68%.
- **The standard sweep's baseline row moved.** Use the "after" row (19.2 turns, 3.4 neutralizes, 41% 3-up) when the `tools/sim` README tripwire is set.
- The exploratory rows below ran health +1 with Luka's heavy line still at 6. The shipped version raised it to 7, which is one reason the shipped win shares differ from row "Health +1".

#### How it was explored

The designer's read: games take too long, possibly because there are a lot of kills. Two ideas: the roster's health may be too low, or regenerate 0.5 health per round. **Nothing is adopted.** Measured with 4 bots, 800 matches, the same seeds, on the code after the Sanity pass. Lower health was run too, as a check on direction. Regen needed the §5.11 wiring fixed in a scratch copy. "0.5 per round" means +1 every 2 upkeeps for any wounded operator in play; "ungated" heals on safe cells too, "off safe cells" keeps §5.11's safe-cell exclusion.

| Scenario                        | Turns per seat | p90  | Knockouts | Re-walked cells | Regen ticks |
| ------------------------------- | -------------- | ---- | --------- | --------------- | ----------- |
| As shipped (no regen)           | 34.2           | 50.5 | 22.0      | 50%             | 0           |
| Regen as designed (+1/3, gated) | 33.9           | 48.5 | 21.5      | 50%             | 3.8         |
| Regen 0.5 per round, ungated    | 28.9           | 39.3 | 15.4      | 40%             | 36.8        |
| Regen 0.5 per round, off safe cells | 29.9       | 41.0 | 16.6      | 42%             | 30.2        |
| Health −1 everywhere            | 39.5           | 57.3 | 29.5      | 57%             | 0           |
| Health −2 everywhere (floor 2)  | 45.5           | 69.3 | 40.0      | 63%             | 0           |
| Health +1 everywhere            | 30.5           | 42.8 | 16.5      | 44%             | 0           |
| Health +2 everywhere            | 27.9           | 38.8 | 12.7      | 38%             | 0           |

"Re-walked cells" is the share of all forward movement that re-covers ground an operator lost to a knockout.

- **The designer's read holds: knockouts drive match length.** Across matches, turns and knockouts correlate at 0.94, and each knockout adds about one turn per seat. Half of all movement is re-walking lost ground.
- **Both ideas shorten matches.** More health or more healing means fewer knockouts and less re-walked ground. Lower health does the opposite: −1 adds 5 turns per seat, −2 adds 11.
- **Regeneration as designed barely matters.** About 4 ticks a match. It is also not live in code (above).
- **+1 health is the cleanest option measured:** −11% turns per seat, −25% knockouts, and every win share within 2 points (Bouncer 27% → 25%, Mimi 18% → 20%). It needs no new rule. The catch is that several §10 numbers were reasoned against 6 health: at 7, a 6-health operator survives two collisions instead of one, and the mark's 4 damage leaves it at 3 instead of 2. Those remarks would need a pass.
- **Regen off safe cells keeps most of the ungated effect** (−13% turns) without the free parking, but it costs Bouncer 4 points (27% → 23%) while Sanity and Javi gain 2.
- **Ungated 0.5 per round and +2 health go further:** −15% and −18% turns per seat, with the long tail cut by about a quarter. They differ in who they help:
  - +2 health lifts Kurbyn to 36% and Mimi from 18% to 23%, and drops Bouncer to 21% and Sanity to 20%. The tanks lose their relative edge. Kurbyn likely gains because a kill takes more hits, and his evasion gets more chances to cancel one.
  - Ungated regen moves every win share by 2 points or less: Bouncer 27% → 25%, Sanity and Luka 22% → 24%.
- **Ungated regen reopens what §5.11 closed:** it heals on safe cells (free parking), refunds chip damage such as bleed, and undercuts Javi's heal. The gates answered those, and the measurement shows the gates are also why regen does almost nothing.
- **The Sanity pass pushes the other way:** +1.7 turns per seat (§10.8).
- **Not measured yet:** lower collision damage; +1 health on the 5- and 6-health operators only.

### Struck

Historical. `CollisionDamage` has since been restored and demoted — see item 3 of the dials table above for its current standing.

- **`CollisionDamage` is not a dial** — _as measured._ Moving it from 2 to 6 changed match length by 0.4 turns and neutralizes by 1.1, because collisions occurred only ~2.4 times a match on Standard. This section and ADR-0002 Amendment 2 both named it as the first lever if the race reads as toothless; that advice was wrong on the figures available. **The strike rests entirely on collision frequency, and split movement raises it.** Re-measure before relying on this either way.
- **The yard setback is not the most expensive rule.** The Python model showed it costing 6.4 turns per match at ~8 neutralizes. At the measured 6.7 across four players it fires under twice per player per match, and its contribution is far smaller than claimed.
- **Occupancy is no longer 10–15%.** It measured that under the old speed band. At the adopted band with opening deployments it is **33%**.

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

**Regeneration — `RegenTests`** (§5.11)

- `TheDefaults_AreReallyAssigned`
- `ALightlyWoundedOperator_HealsOneAfterThreeOfItsTurns`
- `ADeeplyWoundedOperator_KeepsHealingEveryThirdTurn`
- `AnOperatorOnASafeCell_DoesNotHeal`
- `SteppingOntoASafeCell_RestartsTheClock`
- `AnUnwoundedOperator_NeverReportsARegen`

**Haste — `HasteCapTests`, `StatusRegistryTests`** (§5.9)

- `TheDesignersNumbers`
- `ALowRoll_AddsOneCell`, `AHighRoll_AddsTwoCells`
- `HasteNoLongerScalesWithSpeed`, `APassiveSpeedBonus_StillApplies`
- `ASplitRoll_PaysTheBonusOnce_AndTheWholeRollSetsIt`
- `ThePreview_DropsTheBonusOnceItIsPaid`
- `EachOperator_CollectsItsOwnBonus`
- `ADoublesTurn_StopsAtTheCap`
- `TheBudget_ResetsOnTheNextTurn`
- `Haste_IsNotSpeed`, `IsHastened_IsFalse_BeforeTheHasteTakesHold`
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
- `AllInMauling_SitsOutOneTurn`
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

**Self-targeting — `SelfTargetTests`** (§10, 2026-09-17)

- `NaniteInfusion_OnHimself_Heals_AndTheHostileModeStaysOff`
- `TraumaPlate_OnHimself_Shields`
- `NeuralPurge_OnHimself_Cleanses`
- `NanoCell_OnHerself_ShieldsAndStuns_TheStunIsThePrice`
- `AllInMauling_OnHimself_IsRefused_AndCostsNothing`
- `VelvetRope_OnHimself_IsRefused_AndStaysReady`
- `ASelfCast_NamesTheSelf_BeforeItNamesThePrice`
- `LegalTargets_ExcludesTheCaster_ForUnflaggedAbilities`
- `LegalTargets_IncludesTheCaster_ForTheFlaggedFour`
- `LegalTargets_ForAFlaggedAbility_StillListsEveryoneElseItAlwaysDid`
- `AFlaggedAbility_OnAnAlly_StillResolvesAsItDid`
- `AFlaggedAbility_OnAnEnemy_StillResolvesAsItDid`
- `ARangeRetune_KeepsTheOptIn`

**Roster — `Roster`**

- `AbilityIdsAreUnique`
- `EveryAbility_MatchesItsCostTier`
- `EveryAbility_HasADescription`
- `NoAbilityCostsMoreThanTheEnergyCap`
- `ADraftedSquad_HoldsDistinctOperators`
- `TheSameSeed_DraftsTheSameSquads`

**Luka — `LukaTests`, and Tech in `DamagePipelineTests`**

- `BlindSpot_TeleportsOneCellPastTheTarget_AndStrikesItForTwo`
- `BlindSpot_StrikesNobodyOnTheWay`
- `FollowUp_LandsForOne_WhenTheTargetStaysClose`
- `FollowUp_DealsTwo_ToAHeavyTarget`
- `FollowUp_Misses_WhenTheTargetGotClear`
- `FollowUp_Misses_WhenLukaIsNoLongerOnTheBoard`
- `FollowUp_CleanseCancelsIt`
- `FollowUp_AndZeroDay_OnOneTarget_BothResolve`
- `HermesRing_BlocksTech_ButNotNormalOrAtomic`
- `CryoPulse_IsTech_AndAWardedLukaTakesNoneOfIt`
- `TechSources_AreTheListTheDesignerChose` (renamed 2026-09-17; five abilities)
- `ZeroDay_IsTech_AndAWardedLukaTakesNoneOfTheBlast`
- `DroneStrike_IsTech_AndAWardedLukaStillCountsTowardTheSplit`
- `Vendetta_ACritTriples_AgainstAHeavyTarget`
- `Vendetta_StopsStriking_ATargetItAlreadyDowned`
- `TechWard_SpendsNeitherTheEvasionChargeNorThePool`

**Mimi's Cryo Field — `CryoFieldTests`**

- `CryoField_AppliesTheMarkerAndTelegraphs_AndNothingTicksYet`
- `CryoField_RequiresNoTarget`
- `CryoField_FirstTickLandsAtHerNextUpkeep_NotImmediately`
- `CryoField_TicksExactlyTwice_ThenExpires`
- `CryoField_FollowsHerBetweenUpkeeps`
- `CryoField_HarmsEnemiesOnly_AndOnlyWithinRadius`
- `CryoField_TickIsNormal_AShieldPoolAbsorbsIt`
- `CryoField_EndsWhenMimiIsNeutralized`
- `CryoField_CleanseStripsTheMarker_AndCancelsTheField`
- `CryoField_RefusedOnCooldown_AndReadyAgainAfterThreeOwnerTurns`
- `CryoField_RefusedWithoutEnergy_AndCostsNothing`

**Kurbyn's 2026-09-17 rebuild — `KurbynTests`** (§10.3)

- `TheDesignersNumbers`
- `TheKit_IsTwoActives_AndThePassive`
- `BothPassives_AreLiveFromMatchStart`
- `KurbynMovesAtHisBaseSpeed_PlusHaste` (`GameEngineTests`)
- `KurbynHaste_IsCappedAtTwo_NotThree` (`HasteCapTests`)
- `Kurbyn_IsOffTheSpeedChannel_Entirely` (`SpeedCapTests`)

The watch machinery (§6.7) is dormant: `PredatorsReadTests` and `WatchBotTests` were retired with the ability.

**Kian's 2026-09-17 buff — `AbilityResolverTests`** (§10.6)

- `InversionMatrix_HitsEnemiesAheadAndNotBehind` (now expects 2 per enemy)
- `SonicDisrupter_ReachesThreeCells_AndNoFurther`
- `Kian_Numbers_AreTheDesignersOf20260917`

**Fortuna — `FortunaTests`, `FortunaTableTests`, `FortunaBotTests`** (§3.4, §6.8, §7.7, §10.12)

- `Cash_TakesTheDieAndPaysTheHouse`, `Cash_IsOnceATurn`, `Cash_IsHersAlone`, `Cash_RefusedWhileStunned`
- `Cash_AtTheCap_StillSpendsTheDie`, `Cash_AnswersCompulsoryMovement`
- `DealAgain_ReDealsTheLowestDie`, `DealAgain_NeverCreatesTheDoublesRoll`, `DealAgain_RefusedWithNothingInHand_AndCostsNothing`
- `Boxcars_SetsBothDiceToSix_AndOwesARoll`, `Boxcars_RefusedOnAHalfSpentRoll_AndCostsNothing`, `Boxcars_IsNotOfferedOnceADieIsSpent`, `Boxcars_AtTheRollCap_SetsTheFacesAndNothingMore`
- `ItStopsTheFirstTableOnThePath_NotTheFurthest`, `ItStopsEachOperatorOnce`, `ItBillsThroughThePipeline_AndCreditsTheSourceSeat`
- `AlliesAndTheHouseCrossFreely`, `ItBillsNothingAtAnUpkeep`, `ItRetiresAfterItsLastTurn`, `ARedealForgetsWhoItStopped`
- `ItStopsARunAndBillsIt`, `ThePreviewShowsTheShortenedLanding`, `SomebodyStandingOnItLeavesFreely`
- `Table_RefusedOnASafeCell_AndCostsNothing`, `Table_IsDealtAndDrawn`
- Bots: `CashingIsWorthNothingWhenTheDieCouldDoSomething`, `CashingWinsWhenEveryLandingIsWorse`, `ADeployIsNeverSold`, `TheBrainSellsTheDie_AndTheEngineTakesIt`, `ARedealIsWorthMoreWithAWorseDieInHand`, `BoxcarsIsWorthThePipsAndTheRollItBuys`, `ATableIsWorthMoreInFrontOfARunner`, `TheDraftPaysForHerTempo`, `TheDraftStillReadsHerLast_AndThatIsRecorded`

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
- 2026-09-15 — **Synced to the code; the code was authoritative.** §5.6 rewritten for the shield pool that landed on 2026-09-13 (partial absorbs are `Dealt` with `AmountMitigated`; re-casting refills; the evasion half of the mitigation pass declined — `_HANDOFF_mitigation.md` retired). §5.11 added for gated regeneration, which shipped with a code remark citing §5.8 and no section of its own; §1.1 no longer says there is none. §10.5 marked complete with the code's numbers (ranges raised to 5 / 4 / 5, reason unrecorded). **§10.6 Kian and §10.7 Nuetu written** — both were in the draft pool with no section. §10.2 Tagged From Above cooldown 2 → 4 and §10.3 Evasive Protocol 50% → 30% corrected in the tables. §10.8 Sanity at 9 health. §9.3 gains the events it had not listed and notes `OperatorMoved.AttemptedTo`. §12's duplicated second half merged: its unique reasoning kept under "Unmeasured dials", its superseded figures dropped. **Stale code remarks noted, not edited:** `Javi.cs` (range 3), `Kian.cs` (a 6-point beam), `Sanity.cs` (health 12).
- 2026-09-15 — **Balance pass confirmed as deliberate, and the code's remarks brought in line** with it: Javi's ranges 5 / 4 / 5, Sanity at 9 health (only the speed override stands), Kian's Inversion Matrix line 6 → 4 and Drone Strike beam 6 → 4. §10.5, §10.6, §10.8 and §11 updated to match; no rule or number changed here.
- 2026-09-15 — **Luka added as §10.9**, complete, with three amendments. **§2.2: the Tech damage type** — Normal plus one counter, a ward that blocks it outright before evasion and the shield (§2.1 step 1b), with amplifiers the stated direction and none built; **Mimi's Cryo-Pulse is now Tech.** **§2.4: critical hits** — a per-effect, per-recipient roll that multiplies damage, with a heavier multiplier against targets whose maximum health is above a stated threshold; an effect without a chance never draws, so no existing dice stream moved. §2.4 also states that one cast never neutralizes a target twice. **§6.5: follow-up strikes** — `EffectKind.FollowUp`, the thirteenth kind, in the same registry as charges: resolves at the caster's next upkeep only if the caster is within reach, target alone, heavy bonus, a miss reported, and — unlike a charge — lost with its caster. `StatusKind` gains `TechWard` (§5.12) and `Hunted` (§5.13). §2.1 step 3 corrected to the shield pool (it still described the whole-instance shield). Atomic is now in three operators (§2.2, §12). §9.1 randomness reaches three places; §9.3 gains `FollowUpMarked` and `FollowUpResolved`. Luka's L and Vendetta flagged for the balance pass; all his numbers are the designer's, unmeasured.
- 2026-09-15 — **Luka repriced on first review** (designer): L 4 → 5, one above Zero-Day, which it out-damages on one target; Vendetta 9 → 6, out of Miracle Pull's price and beside Velvet Rope's. §2.2, §10.9 and §12 updated. Unmeasured.
- 2026-09-15 — **Tech sources settled** (designer): damage from a guided or remote-operated device is Tech (§2.2). **Zero-Day and Drone Strike switch Normal → Tech**, joining Cryo-Pulse; Killzone is recorded as borderline and stays Normal. §5.12 states three consequences: the ward blocks damage but not riders, a warded holder still counts toward Drone Strike's split, and Mimi alone is fully shut out by the ward. A roster test pins the source list. §10.4, §10.6, §10.8, §10.9, §12 and §13 updated. Unmeasured.
- 2026-09-15 — **"L" named Blind Spot** (designer). The device behind it and Hermes' Ring is one signal-spoofing ring: turned outward it hides him from every lens (the teleport), turned inward it jams guided tech (the ward). The fiction lives in `OPERATORS.md`. Ability id 901 unchanged. No rule or number changed.
- 2026-09-16 — **Haste bonus capped at 3 cells** (designer balance note). §5.9 and §6.3 amended. Hastened still adds +0.5 speed, but at most 3 extra cells per operator per turn. The cap is per turn, not per move, so splitting a roll cannot collect it twice. It is a new config value, `CombatConfig.HasteBonusCellCap`, and `GameEngine` keeps the per-turn budget. `Move`, `PreviewLandings` and `HasLegalMove` now share one distance helper. `MatchFactory.Match` now exposes `Statuses` for tests. Sim before/after is noise-level (§12). Tests 496 → 512.
- 2026-09-16 — **Sanity balance pass** (designer, landed in the haste-cap commit). Zero-Day range 2 → 3. Collision cost 7 → 6, cooldown 4 → 3, range 5 → 6, so it is no longer priced as an ultimate. §10.8 updated with a before/after bots sweep: Sanity's win share 21% → 22%, but bot matches run about 1.7 turns per seat longer with 2 more knockouts, and Collision is the cause. `Sanity.cs` remarks brought in line. §12 records the watch item, and that regeneration is not live in code.
- 2026-09-16 — **Designer's reasoning recorded** for the haste cap and the Sanity pass (§5.9, §10.8), and for the 2026-09-15 evasion cut (§10.3): buff the weakest, nerf the fast operators that the bots sweep and human games both had on top. §12 gains the match-length measurement behind the designer's ideas (health may be too low; 0.5 regen per round): knockouts drive length, and +1 health, +2 health or 0.5 regen shorten matches by 11–18%, while lower health lengthens them. +1 health moves win shares least. Nothing adopted.
- 2026-09-16 — **Match-length pass adopted** (designer): +1 health across the roster (common 7, Mimi 6, Bouncer and Sanity 10) and regen at +1 every 3 turns for any wound, off safe cells (§1.1, §5.11). Regen had never run: `CombatConfig` did not assign its fields, now fixed. Luka's heavy line 6 → 7 so it still means the tanks. Bots: 34.2 → 28.0 turns per seat, 22.0 → 13.4 knockouts, win shares within 3 points (§12). Standard sweep: 20.3 → 19.2 turns. §10 HP lines updated; older reasoning that cites 5, 6 or 9 health is flagged, not rewritten. Tests 512 → 518.
- 2026-09-16 — **Mimi completed: Cryo Field built.** The last unbuilt ability, and with it the mechanic its banner waited on: a status that damages an area at its holder's upkeep. `StatusKind` gains `CryoField` (§5.14); `EffectKind` gains `ProjectField`, the fourteenth kind; `DeferredOperatorEffects` gains a third, **repeating** shape — the field (§6.6), anchored to the caster herself, billing enemies near her current cell at each of her owner-upkeeps while the marker stands. Tick 1 Normal, radius 2, two ticks: the design row's "2 turns" reads as registry duration 3 because a self-applied status counts the cast turn as its first (§5). The tick amount was blank in the design table and is a named constant flagged for tuning (`Mimi.CryoFieldTickDamage`). Being Normal and self-centred, the field goes through Hermes' Ring — a warded Luka is no longer immune to Mimi (§2.2, §5.12). The field ends with her: neutralize strips the marker (§1.2), the follow-up precedent rather than the beacon one. §9.3 gains `FieldProjected` and `FieldTicked`; the top banner, §10's header claim and §10.4's banner are retired; `OPERATORS.md`'s "Finish Mimi" is checked off. Tests +11 (`CryoFieldTests`).
- 2026-09-16 — **Kurbyn completed: Predator's Read built** (id 303 — cost 3, cooldown 2, range 3). `StatusKind` gains `Watched` (§5.15); `EffectKind` gains `Watch`, the fifteenth kind; `DeferredOperatorEffects` gains a fourth shape — the watch (§6.7), the follow-up's mirror: anchored to the victim like a charge, but its moment is the target's first **dice movement**, not the upkeep, and its condition is the target's conduct rather than the caster's proximity. Placement never trips it (§7.4); standing still is the other escape hatch; the lapse is silent and the strike is once, spent landed or absorbed. It outlives its caster (the charge precedent, ADR-0006 — the condition never reads his position) and dies with its target (neutralize strips the marker, §1.2). Balance intent: his cheapest cast was 6; this fills the 3-energy rung with Short Circuit and From the Hip (§10.3). `GameEngine` gains the registry on its move path (the only trigger a watch has); §9.1 lists fifteen kinds; §9.3 gains `WatchMarked` and `WatchTripped`; §2.1's cause list gains "watch" and the "cryo-field" it had never recorded. `OPERATORS.md`'s Kurbyn paragraph gains the read; `Roster.cs`'s "the pool is even" remark was stale from the moment the Cryo Field commit wrote it — Kurbyn still had two abilities — and is true as of this change. Tests +16 (`PredatorsReadTests`), 533 → 549.
- 2026-09-16 — **Haste nerfed to flat cells** (designer): +1 extra cell when the roll totals 6 or less, +2 above, once per roll per operator, still capped at 3 per operator per turn (§5.9, §6.3). Replaces +0.5 speed, which averaged 2.6–2.8 cells per roll even under the cap; the new rule averages 1.6 at any speed. `CombatConfig.HasteSpeedBonus` removed; `StatusRegistry.HasteBonus` became `IsHastened`, and Hastened no longer enters the speed sum. `MovementResolver.CellsWithCappedBonus` removed as dead. Syla's payout copy updated. Bots sweep: noise only (§12).
- 2026-09-17 — **Lethe added as §10.10**, complete (designer's numbers after a review of the handoff). HP 7, speed 1.0 with a permanent Hastened passive (flat cells, not the handoff's +0.5 speed, which the 2026-09-16 haste nerf retired). **Nano Cell** (1001; 4 / 4 / 4) combines a 99-point shield and a stun, each for 2 turns. **Catalyst** is an ally-only aura that grants haste within 2, not speed, so the missing ceiling is never tested. **Eris' Exploit** (1002; 6 / 4 / 3) is a radius-2 zone that bills each enemy N−1 per tick for two ticks. `AuraDefinition` gains a side and a haste flag; `AuraRules` resolves speed auras as strongest bonus plus strongest penalty, and answers `GrantsHaste`; `GameEngine` reads aura haste where a move starts and lists it in `ActiveStatusesOn`. ADR-0007 Amendment 1: crowd zones and zones without a status. Cell effects are keyed on source as well as cell and seat, and Bio-Link Rage's rider reads only Nuetu's own zones. Bots learned real shield pools, ally stuns and crowd zones. Bots sweep: Lethe 24%, turns per seat 28.2 → 26.9. §5.6, §5.9, §6.3, §9.1, §10, §12 amended.
- 2026-09-17 — **Lifesteal added (§2.5); Vendetta drains** (designer). A damage effect can heal its caster for the health the hit actually removed, per hit, capped at the caster's maximum. Luka heals up to 3.3 expected per Vendetta. The bots value the drain by what Luka is missing. §10.9's heavy line corrected to "above 7".
- 2026-09-17 — **Sanity burdened** (designer). New status Burdened (§5.16): −1 cell on a roll of 6 or less, −2 above, once per roll, never below 1 cell, cancelling against haste. Sanity moves to speed 1.0 with Burdened as his passive, so the §6.3 band has no exceptions and he is no longer immune to slows. `CombatConfig` gains `BurdenCellsAtOrBelowThreshold`, `BurdenCellsAboveThreshold` and `BurdenCellsFor`. The bots draft haste and burden passives at their average worth. A +1 damage package was measured and not adopted. Bots sweep: Sanity 21% → 27%, turns per seat 26.5 → 25.1. §5, §6.3, §10.8 amended.
- 2026-09-17 — **Revú added as §10.11**, complete, from `OPERATOR_DRAFTS.md` §3, with the designer's rulings. **§3.3:** energy drain (destroyed) and pool-scaled damage (the target's seat, splash half); `EffectKind` gains `DrainEnergy` and `MissingEnergyDamage`, 17 in all, and `AbilityResolver` takes the players. **§5.17 Equilibrium:** a cast's instant damage is ×2 at cost ≤ 3 and ½ (at least 1) at cost ≥ 6, for every type, as pipeline step 0 (§2.1). `DamageInstance` carries `CastCost`, and `IDamageMitigation` gains `ScalesCastDamage`. The `EnergyDrained` event is new. Bots read Equilibrium and score drains and Sadist. The draft card shows a named passive in the free slot. Bots sweep: Revú 21%, turns per seat 25.1 → 24.7. §2.1, §3, §5, §9.1, §10 amended.
- 2026-09-17 — **Revú tuned** (designer): health 7 → 8, Leech Round 1 → 2 damage and cooldown 2 → 1, Sadist cooldown 5 → 4. Bots sweep: Revú 21% → 24%, Leech Round 1.53 → 2.87 casts per match. §10.11 amended.
- 2026-09-17 — **Mimi repriced** (designer): Cryo-Pulse and Cryo Field 6 → 4. The bots learned to cast Cryo Field (a `ProjectField` scoring branch; it had been cast 0.00 times). Bots sweep: Mimi 16% → 20%, Lethe 25% → 20% (watch). §10.4 amended.
- 2026-09-17 — **Kian buffed** (designer): Inversion Matrix 4 → 3 energy and 1 Normal → 2 Tech; Sonic Disrupter 4 → 3 energy, 2 Normal → 2 Tech, radius 2 → 3; Drone Strike 6 → 4 energy. The two emitters are Tech by designer exception to §2.2, so a warded Luka blocks all of Kian's damage. Bots sweep: Kian 19% → 23%, Mimi 20% → 18% (watch). §2.2, §10.6 and §13 amended.
- 2026-09-17 — **Self-targeting settled: per-ability opt-in** (designer). §10's cast-mode rule made every friendly mode reachable by its own caster, and blanket self-cast was rejected on that ground — All-In Mauling's friendly mode is a heal, and Bouncer self-sustaining was never intended. `AbilityDefinition` gains `allowsSelfTarget`; the resolver excludes the caster from `LegalTargets` unless it is set and refuses a self-aimed cast without it (`TargetingVerdict.CannotTargetSelf`, costing nothing); the UI filter that had been settling the question by omission is removed, so the board offers the caster's own piece exactly when the ability declares it. Four defensive abilities opt in: Javi's Nanite Infusion, Trauma Plate and Neural Purge, and Lethe's Nano Cell, whose self-bubble pays the stun as its price. Bots keep their no-self-cast policy. §10, §10.5, §10.10 amended. Tests +13 (`SelfTargetTests`).
- 2026-09-17 — **Mimi buffed** (designer): health 6 → 7 (the roster's common figure at last), Cryo Field tick 1 → 2 and radius 2 → 3. Bots sweep, before/after on the same tree: Mimi 17% → 21% (+4, about 2.7 standard errors — out of the noise); Cryo Field 1.35 → 2.69 casts per match; no other operator moved more than 2 points. §1.1 and §10.4 amended; the EditMode expectations pinned to the old numbers (radius-edge fixtures and tick arithmetic in `CryoFieldTests`, the designer's-numbers assertions in `MimiBotTests`) moved with it, 679 passing.
- 2026-09-17 — **Speed bonus capped per turn** (designer): at most `CombatConfig.SpeedBonusCellCap` (2) extra cells from a speed above 1.0× per operator per turn, charged on every move that collects it — the ceiling the speed channel never had (§6.3, §12). `GameEngine` keeps a second per-turn budget beside haste's; `CellsFor` gained a speed-bonus out parameter so `Move` charges it and `PreviewLandings` and `HasLegalMove` agree. Bots sweep: the bottom compressed (Mimi 21% → 23%, Bouncer 21% → 24%) but the 1.5× tier stayed on top (Kurbyn 33%, Syla 30%, Javi 27%); a cap of 1 was measured and not adopted (§12). §6.3, §12 amended. Tests +10 (`SpeedCapTests`); `HasteCapTests`' speedster expectations and the tallied-match seed moved to the new rule. 679 → 689 passing.
- 2026-09-17 — **Syla's Ace Shards patched down** (designer): damage 3 → 2, range 3 → 2, the bleed rider untouched (§10.2). She was second in the speed-cap sweep at 30%. Measured together with the Kurbyn rebuild below: Syla 30% → 24%, confounded by design. The `AbilityResolverTests` expectations moved with it.
- 2026-09-17 — **Kurbyn rebuilt** (designer): Predator's Read removed (id 303; the watch machinery — §5.15, §6.7, `EffectKind.Watch` — stays in the core, dormant), evasion 30% → 12% (`EvasionChance`; he is the only holder, so the global dial is his number), base speed a plain 1.0, and the +0.5 passive speed replaced by permanent flat haste (+1 on a roll of 6 or less, +2 above) capped at **2** cells a turn — the first per-operator haste cap, read as `OperatorState.HasteCellCap` with `HasteBonusCellCap` (3) the fallback. `OperatorDefinition` gains a second passive slot, `MatchFactory` applies both, and the draft card and `DraftPicker.EffectiveSpeed` read both. Miracle Pull's cooldown went 2 → 3 in the same pass. Bots sweep, 800 matches against the speed-cap baseline: Kurbyn 33% → 28%, Bouncer 24% → 28%, Javi 27% → 31% (the new outlier — watch), Syla 30% → 24% (her patch is in the same tree), Revú 20% → 23%, the rest within a point; turns per seat 27.8 → 27.5; personalities back to 25/25/25. §5.5, §5.9, §9.1, §10.2, §10.3, §11, §12, §13 amended. Tests: `PredatorsReadTests` (16) and `WatchBotTests` (9) retired with the ability; +3 (`KurbynTests`), +1 (`HasteCapTests`' cap-2 case); stale expectations moved in `GameEngineTests`, `BurdenTests`, `HasteCapTests`, `SpeedCapTests` and — for Syla's patch — `AbilityResolverTests`. 689 → 668 passing.
- 2026-09-18 — **All-In Mauling repriced** (designer): 6 energy, cooldown 0, 2 damage, ally heal 3 → **4 energy, cooldown 1, 3 damage, ally heal 2**; self-damage stays 2 (§10.1, §1.1, §11). At the old price Velvet Rope dominated it at equal cost, it was the worst damage-per-energy on the roster, and its ally heal was larger than the dedicated healer's. ADR-0002's open item on its cost is closed with the cheaper cost and a real cooldown. Bots cast it 1.40 → 2.33 a match; standard-sweep neutralizes 3.4 → 4.8, and Bouncer's own bot win share fell 28% → 24% — watch it. Tests 668 → 669.
- 2026-09-18 — **All-In Mauling's self-damage 2 → 1** (designer), the follow-up to the reprice above. At ten health and roughly 2.4 casts a match, 2 a cast cost Bouncer his match in the sweep. Bots: Bouncer 24% → 25%, knockouts 14.9 → 14.7. Most of the reprice's 4-point drop was not the blood, so the rest waits for human games (§10.1).
- 2026-09-18 — **Eris' Exploit strikes on the cast** (designer; ADR-0007 Amendment 2). Its first hit lands at cast time and the zone still ticks once at Lethe's next upkeep, so the total against a static crowd is unchanged while the scatter no longer voids the cast. `AbilityEffect.StrikesOnCast` and `AbilityResolver.StrikeZoneNow`; the instant hit carries the cast's cost, so Equilibrium halves it against Revú and the later tick is still clean. §10.10 updated. Bots sweep 1600 matches: 22.9% → 22.7%, casts 0.96 → 0.97 — the bots never dodge zones, so the sweep is blind to this.
- 2026-09-18 — **Lineup read, 1600 matches, bots against bots** (±1.0 point): Javi 28.9%, Syla 27.7%, Kurbyn 27.5%, Bouncer 25.0%, Kian 24.8%, Sanity 24.5%, Nuetu 23.9%, Luka 23.6%, Mimi 23.4%, Lethe 22.9%, Revú 22.8%. Turns per seat 28.3, knockouts 14.7. The spread is 6 points, the tightest the roster has been; only Javi, Syla and Kurbyn (high) and Lethe and Revú (low) are outside noise. Rarest casts: Neural Purge 0.15, Trauma Plate 0.41, Miracle Pull 0.55, Eris' Exploit 0.96, Sadist 0.98, Nano Cell 1.17.
- 2026-09-18 — **Fortuna added as §10.12**, complete, the twelfth operator and the first whose subject is the dice. **§3.4: cashing a die** — once a turn a seat fielding her sells an unspent die she could have moved for 2 energy, the third thing a die can be spent on (§6) and the priced answer to compulsory movement (§6.1). **§6.8: dealing dice** — `EffectKind.DealDice`, the eighteenth kind; the engine deals, the resolver declares; a re-roll takes the lowest die, a dealt double is not a rolled one, and the dice precondition refuses before payment (`AbilityAvailability.DiceNotHeld`). **§7.7: tables** — `EffectKind.SetTable`, the nineteenth kind and ADR-0007 Amendment 3: the first effect that reads the cells a move passes through and the only one that can shorten a move, once per enemy operator, never on a safe cell, placement never trips it. `StatusKind` gains `HouseEdge` (§5.18). New command `CashDieCommand`, the first since the roster began, and four events (§9.2, §9.3). **Fixed in passing:** `CheckAbility` answered Ready for a caster in a home column (§11). Bots sweep on the same day's balance pass: Fortuna 23%, 5.35 dice sold a match, Deal Again 2.96 / Boxcars 2.62 / The Table 2.54 casts, refusals 0. Two open items recorded: the draft picker reads her last, and cashing as a stall (§12).
- 2026-09-21 — **Sanctuary: §4.4 third amendment** (designer). Operators on a safe cell take no damage of any type, Atomic included; on their own spawn cell they also cannot be slowed or stunned. New `SanctuaryRules`, one per match, shared by `DamagePipeline` (step −1, §2.1, new outcome `Sheltered` and event `DamageSheltered`), `StatusRegistry.Apply` (now returns whether it applied; refused statuses emit nothing) and `AuraRules` (no aura slow on the spawn cell). An execute below threshold on safe ground falls back to its voided hit; bleed resolving on safe ground is spent and voided; self-damage and forced movement are untouched. `GameEngine` gains `IsSheltered` and `Resists`; the bots read them, and a push's landing is valued off the shelter it leaves. The view shows SAFE. §1.3, §2.1, §2.2, §4.4, §5.1–§5.3, §6.5–§6.7, §9.3, §10.10 amended. Measured (4000 matches, paired seeds): damage −9%, knockouts 13.6 → 12.2, turns per seat 26.9 → 26.0, 3.85 hits voided a match; Javi 26% → 28% and Kian 26% → 24% are the only win-share moves beyond noise; safe-cell occupancy unchanged, because the bots never camp deliberately — the camping risk is open until human play (§4.4, §12). Tests +20 (`SanctuaryTests`); `BurdenTests`, `FortunaTests` and a Kian push case in `BotTests` moved off the spawn cell or onto the new rule.
- 2026-09-20 — **Balance pass across the roster** (designer, live sessions; committed as four `balance(...)` commits and documented here after the fact). **Sanity:** health 10 → 9, Collision range 6 → 5 (§1.1, §10.8). **Luka:** Vendetta 6 → 5 energy (§10.9) — the commit message says cooldown, the code changed the cost, and the code is what shipped. **Lethe:** Eris' Exploit 6 → 4 energy, cooldown 4 → 3 (§10.10). Two of these have a consequence nobody asked for and §5.17 now records: Vendetta and Eris' Exploit both left Equilibrium's **dear** band, so Revú no longer halves either. Stale expectations moved in `LetheTests`, `LukaTests` and `RevuTests` (the Vendetta crit and zone cases were rewritten against the new bands, and the crit-then-halve ordering is pinned on a test double, since no roster ability is dear and crits any more).
- 2026-09-21 — **Lethe's Nano Cell heals 2** (designer). The bubble was worth nothing on an ally that was not about to be hit and charged the stun anyway; it now heals on the way in, ally-only, resolving before the shield and the stun (§10.10). She is the roster's second 2-point healer, which is a role overlap to watch.
- 2026-09-21 — **Sadist repriced and floored** (designer): 9 → 7 energy, cooldown 4 → 3, and a **minimum of 2** on the primary figure (§3.3, §10.11). `AbilityEffect` gains `MinimumDamage`, a floor on what an effect **computes** for the kinds that read their amount off the board — today only `MissingEnergyDamage`; mitigation and Equilibrium still apply after it, and the splash divides the floored figure. Against a seat at cap the cast used to compute zero and strike nobody. The bots read the floor (`CastPlanner`). `RevuTests` and `RevuBotTests` moved with it; the zero-splash rule is pinned on a floorless test double, because Sadist can no longer produce one.
- 2026-09-21 — **The three balance passes measured together** (4000 matches, bots against bots, paired seeds, against the sanctuary build; ±0.7 points). Turns per seat 26.0 → 25.8, knockouts 12.2 → 11.9. **Win shares: Kurbyn 28%, Javi 27%, Syla 26%, Luka 26%, Bouncer 25%, Lethe 25%, Kian 24%, Sanity 24%, Revú 24%, Fortuna 24%, Nuetu 24%, Mimi 23%** — a 5-point spread, the tightest the roster has been, and every personality on 25%. Only Sanity moved beyond noise (26% → 24%). Casts: Sadist 0.77 → 1.10, Nano Cell 1.07 → 1.29, Eris' Exploit unchanged at 0.93. The bots still never camp and never cluster, so Eris' Exploit and the sanctuary rule are both under-read here (§4.4, §12).
- 2026-09-21 — **Fortuna buffed** (designer): The Table 6 → 5 energy and its lifetime two turns → three, Boxcars 9 → 7 (§10.12). She had been in the bottom half since she landed, and the two overpriced casts were the two that never touch health; the passive and the speed were deliberately left alone, because the cashed die is the dial that feeds the stall (§12) and the speed is the one §10.12 forbids. Bots sweep, 4000 matches, paired seeds: **Fortuna 24% → 27%** (third), The Table 2.31 → 2.92 casts a match, Boxcars 2.65 → 3.30, dice sold 5.14 → 5.36, turns per seat 25.8 → 26.1. Nobody else moved beyond noise; the spread stayed 5 points. Watch item: +3 is more than the pass intended, and Boxcars back to 8 is the trim.
- 2026-09-21 — **Vendetta's cooldown 3 → 2** (designer), the other half of the 2026-09-20 pass, whose commit message and code had disagreed (§10.9). Bots sweep, 4000 matches: 2.04 → 2.18 casts a match, Luka flat at 26%; energy is the ultimate's real limiter.
- 2026-09-21 — **Nuetu becomes the answer to mobility** (designer). Bio-Link Rage applies **Burdened for 2 turns** to what it hits, and Killzone's cooldown goes **6 → 4** (§5.16, §10.7). The brief was a counter to Kurbyn: Burdened is haste's mirror and cancels it cell for cell, so his permanent passive stops paying while Nuetu keeps hold of him, and the same answer reaches Syla's payout and Lethe's Catalyst. Burdened gains a second source and becomes a timed, cleansable status as well as Sanity's passive. `CastPlanner.StatusWorth` gains Burdened at 1.0 — it had been scoring the new rider at nothing. Sweeps: roster-wide flat (Nuetu 24%, Kurbyn 28–29%), head to head with identical fillers **34.3% → 36.0%** over 1500 matches; the bots never price their own mobility, so this is the change the harness is least able to measure. Killzone 1.12 → 1.21 casts — the cooldown was not what limited it. Tests +5 (`NuetuTests`).
