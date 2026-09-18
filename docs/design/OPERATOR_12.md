# Operator #12 — Fortuna, The Dealer

> Status: **Built, 2026-09-18.** Shipped as written, under the name **Fortuna** (the designer's choice; Nona was the proposal). Every ruling below is implemented and tested; the measured result is at the end.
> Location in repo when adopted: `docs/design/OPERATOR_12.md`
> Related: `COMBAT_SYSTEMS.md` §3, §6, §6.1, §6.3, §9.2, §10, §12 · `OPERATORS.md` · ADR-0006, ADR-0007
> Ability ids reserved: **1201–1203** (operator 12 × 100 + slot). The passive takes none.

---

## 1. The pitch

**Eleven operators fight over the board. The twelfth owns the dice.**

Fortuna deals. Her kit never touches health except in passing: she converts dice into money, re-deals a die she does not like, sets a cell on the track where a run has to stop, and once a match she deals the house a double six. She is the first operator whose subject is **the roll** rather than the board, and she is the only operator on the roster who makes the race itself contestable.

She is not strong because her numbers are big. Her numbers are small. She is strong because every other operator spends a currency she manufactures.

---

## 2. Why her, and why now

Three holes, and one operator closes all three.

**The dice are untouched, and the game is a dice game.** Eleven operators and seventeen effect kinds, and not one of them reads, alters, buys or spends a die. Haste adds cells *after* the arithmetic; slows scale the multiplier; nothing goes near `_unspentDice`. The single largest blank space in the design is the mechanic on the box.

**Speed has no counter, only frictions.** The bots sweep has said the same thing for a week: Syla 32%, Kurbyn 31%, Javi 30% — the three operators at 1.5×. Every answer on the roster is a slow, and slows have a floor (§5.2), stack in two channels the doc admits are in conflict (§12), and shave a fraction off a move. Nothing on the roster makes *speed itself* a liability. A cell that stops a runner does: the faster you move, the more cells you cross, and the more likely one of them is hers. That is a counter in the sense the doc already endorses — **the Velvet Rope answer to Evasion, not the nerf answer** (§10.1).

**"Fortuna" means nine, and the pool is eleven.** `OPERATORS.md` leaves it open and warns that a quiet tenth is the worst of the options. The cleanest resolution available is that the title was never a count: **Fortuna is a person, and the Royale is her table.** Adopt her and the open item closes with a name instead of a rule.

Practical bonus: **twelve is free.** The draft grid holds three rows and widens to four columns for 10–12 cards (§12, `DRAFT.md`). A thirteenth operator needs a layout decision; she does not.

---

## 3. The block

**HP 7 · Speed 1.0× · Archetype: the Dealer · Neither house nor contractor — the house is hers**

| #   | Ability            | Type         | Cost | CD  | Range      | Effect                                                                                                                                                                             |
| --- | ------------------ | ------------ | ---- | --- | ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **The House Edge** | Passive      | —    | —   | self       | Once per turn, one unspent die that could have moved her is **cashed** instead: the seat gains **2 energy**, the die is consumed, nobody moves.                                     |
| 2   | **Deal Again**     | Active, self | 3    | 1   | —          | **Re-roll one unspent die.**                                                                                                                                                       |
| 3   | **The Table**      | Active       | 6    | 3   | 4 (cell)   | A **table** on an outer-track cell for two of her upkeeps. An enemy dice move that crosses or ends on it **stops on that cell** and takes **2 Normal**. Once per enemy operator.    |
| 4   | **Boxcars**        | Active (Ult) | 9    | 4   | self       | Both unspent dice **become sixes**. Counts as a rolled double for the extra roll (§6), inside `MaxRollsPerTurn`.                                                                    |

---

## 4. The House Edge — the third thing a die can be

§6 opens with a sentence that has been true since the core was written: **"Every die is consumed exactly once, by a deploy or by a move."** She adds the third consumer, and the whole operator follows from it.

**Why two energy is not a free tap.** A die is worth about 3.5 pips, and pips are the scarcest resource in the match. Per seat, per match: roughly 26 turns × ~8.4 pips (the roll plus the doubles chain) ≈ **220 pips**. A squad at 1.0× needs 174 cells to get three operators home, plus whatever knockouts take back — at 13.1 neutralizes a match, about 3.3 per seat, each costing a half-lap on average. **The race is already close to unaffordable, which is exactly why 1.5× operators win.** Cashing a die every turn forfeits ~90 pips over a match and loses the race outright. So the passive is self-limiting by the win condition, not by a cooldown, and the interesting cases are the ones where the die was worth little anyway:

- The roll is a **1** and the cell in front of your piece is not worth having.
- **Movement is compulsory** (§6.1) and the only legal move walks a wounded operator off a safe cell, onto a painted cell, or into a collision the opponent set up. Today that is a tax with no answer. She is the answer, once per turn, and she charges you a die for it.
- You are banking for an ultimate and the board is quiet.

That second case is the one I would ship her for. **Compulsory movement is the only rule in the game that can force a player into a mistake**, and it was adopted for a structural reason (a turn must always be endable), not because being forced is fun. The House Edge does not repeal it — it prices it.

**Rulings.** She must be able to move that die: a yarded, stunned or home Fortuna cashes nothing. Cashing is not a move, so it neither trips a watch (§6.7) nor collects haste. It happens in the action phase, and the cashed die is gone, so a turn can end — that is the escape valve working. Energy above the cap burns as usual (§3.1); cashing into a full pool is legal and stupid.

---

## 5. Deal Again — the cheapest interesting decision on the roster

Three energy for an expected +2.5 pips is a bad trade and that is the point: **you do not cast it for the average, you cast it for the specific.**

- A **6** deploys a dead operator (§1.3). That is a half-lap of lost progress bought back for three energy, and it is the single highest-value outcome any 3-cost ability on the roster can produce.
- An **exact** number lands a collision: three damage that costs no energy, from a roll that was going to fall short.
- A re-roll is the only way to refuse a specific number when refusing is what matters.

Priced beside Leech Round and Short Circuit (3, cooldown 1 and 2). Cooldown 1 so it is castable every turn, per §3.1's rule that an ability meant to be castable every turn must cost 3 or less.

**Rulings.** One die, chosen. **The doubles re-roll is decided when the dice leave the cup** — dealing again never creates one and never destroys one; that keeps the ult as the only way to buy a roll and stops a re-roll chain. A re-rolled die is an ordinary die: it deploys, it moves, it can be cashed. The re-roll draws the injected RNG (§9.1), so it shifts the stream — every seeded test written after her needs to know that, and no figure measured before her survives her.

---

## 6. The Table — the first ability that touches the path

Every existing effect reads a **landing** (§7.1) or a cell at an **upkeep** (§6.4–6.7). This one reads the cells in between, which is machinery the core has only ever used once, for Sanity's dash.

**The dilemma it poses is the good kind.** The table is visible from the moment it is dealt, like a beacon (ADR-0006), and the enemy has four answers, all of which cost something:

1. **Route around it** — spend the die on another operator, which is the slower operator or the wrong one.
2. **Stop short** — split the roll, which costs a whole cell whenever both dice are odd (§6.3) and doubles your landings, and a landing is a collision trigger.
3. **Bypass with placement** — a push, pull, swap or dash is not a dice move and the table never sees it (§7.4, and the watch precedent in §6.7). **This is the design's best feature: it makes eight abilities already on the roster newly valuable** rather than adding a counter of its own.
4. **Walk into it** — take two Normal and stop where she chose, which is usually beside something of hers.

**And it makes the finish contestable without touching the finish.** Home columns are out of the fight and must stay that way (§4.3). A table one cell short of the mouth does not reach into the column — it stops the leader on the approach. That is the endgame tension the game currently does not have, obtained legally.

**Rulings.** Outer track only; **a safe cell is refused** (a table on a start cell would shelter whoever it stopped, which is backwards) and so is a home column. The stop **is a landing**: the collision contest resolves normally (§7.1), which is how she throws a runner into your tank. Enemies only — allies and she herself cross freely, as with fields and zones (§6.6). It is a deployed device, so it outlives her (ADR-0006/0007). **Once per enemy operator**, so it can never hold anyone in place twice; the same operator crossing again on a later turn passes. `PreviewLandings` must show the truncated landing, or the view hides the decision the ability exists to create (§9.1).

Priced against Eris' Exploit (6, cooldown 4) and under Killzone (9, cooldown 6). Two damage because the theft of pips is the ability; the damage is a receipt.

---

## 7. Boxcars — the only ult that cannot kill anybody

Twelve pips, chosen rather than rolled, plus the roll a double is owed. Against an average 7-pip roll that is about **+12 pips of tempo** once the extra roll is counted — at 1.0× a fifth of the loop, at 1.5× nearly a third, or **two deploys** and a squad back from a wipe (§1.3).

Nine energy and cooldown 4 puts it beside Tagged From Above, Miracle Pull, Killzone and Sadist. It is worth less than Miracle Pull, which can delete an operator and take a half-lap with it, and that is the correct relationship: **she is never the reason someone dies, she is the reason someone arrives.**

**Rulings.** Both dice must be unspent, so it is cast at the top of the action phase and never salvages a half-spent roll. It grants the extra roll **only if the seat has rolls left** (`MaxRollsPerTurn = 3`) — at the cap it sets the faces and nothing more. No energy grant on the extra roll (§3.1, unchanged). A double six that deploys two operators still forfeits movement, exactly as a rolled one does.

---

## 8. Why this is S+ and why it is not broken

**S+ because she changes the draft, not the damage race.** Every squad that fields her gets an economy nobody else can buy: six to eight cashed dice a match is ten to sixteen extra energy, which is an ultimate that would not otherwise have been cast. She is the best partner the three Tech operators have ever had — Mimi at 18%, Kian at 23% and Sanity at 26% are the roster's expensive kits, and she funds them without a single buff to their numbers. She is also the best partner the 1.5× operators have, because her pips convert at their multiplier. **An operator that is first pick for both the top and the bottom of the meta is the definition of the tier.**

**Not broken, for four reasons the rules already enforce.**

- **The race polices the passive.** Cash more than a handful of dice and you cannot finish. No cooldown needed; the win condition is the cooldown.
- **She cannot defend herself.** Seven health, 1.0×, no shield, no ward, no escape, no cleanse — the only operator with three abilities and no answer to anybody walking up to her. Diving her is correct and cheap, and Kurbyn or Syla can do it on turn three.
- **Her bank is a target.** Revú's Leech Round destroys exactly what she manufactures, and Sadist is *worse* against the pool she is filling. The two money operators counter each other by construction — the loan shark and the dealer, which is also the fiction.
- **Nothing she does is hidden.** The table is visible, the pool is public, and 9 energy on a Fortuna seat reads as "boxcars is coming" the same way 9 on a Revú seat reads as Sadist.

**The honest risk is stalling.** A player who has given up the race can cash every turn and become a pure combat engine that out-casts three opponents. The cap at 12 and the burn rule blunt it, and losing is losing — but it is a strategy the game has never had, and it is the thing to watch in the first human session rather than in the sim.

---

## 9. Fiction, and the name

**Fortuna deals at the Royale.** Not staff, not a contractor, no grudge: the building is hers, the table is hers, and the eleven of them are playing on it. She is the Fate who spins the thread, dressed as a croupier — the oldest joke in the noir book, told straight: the dealer decides, everyone else calls it luck.

Adopting the name answers the open item in `OPERATORS.md` in the direction that needs no rule: **the title is a name, not a count.** If that is a step too far, the same kit ships as Fortuna or as a flat alias — **The Croupier** — and the naming item stays open. **Decided 2026-09-18: Fortuna.** The naming item in `OPERATORS.md` therefore stays open — the title still means nine while the pool holds twelve.

**Silhouette:** `Primitives.Polygon(4, 45f)` — a diamond, the card pip, on its point. It sits closest to Kurbyn's square (`Polygon(4, 0f)`) and is distinguished only by rotation; if that reads badly at board scale, `Star(8, 22.5f)` is a chip's notched edge and is unclaimed.

**Voice, six lines, dealer's patter — dry, never cruel** (the register `VOICE_LINES.md` sets):

- Move: "Place your bets."
- Cash a die: "Chips off the table."
- The Table: "Nobody walks past a game."
- Boxcars: "House throws."
- Kill: "Debt settled."
- Death: "The table's closed."

**Art:** charcoal and brass, the house livery worn as a waistcoat rather than a uniform, because she is not wearing anybody's colours — they are wearing hers. Long hands, a chip rack, no weapon anywhere on her. `ART_PROMPTS.md` gets its row when she is adopted.

---

## 10. What it costs to build

Larger than any operator since Sanity. Honest list, in order of risk:

1. **A new command, the first since the roster began** (§9.2): `CashDieCommand(face)`, with a `DieCashed` event. `GameEngine` owns `_unspentDice`, so the change is local — but §6.1's endability check and `PreviewLandings` both have to learn about it.
2. **One new effect kind, the eighteenth**: `DealDice` — amount plus an optional face; no face means re-roll. One kind covers Deal Again and Boxcars, and `AbilityResolver` emits an outcome `GameEngine` applies, the way deferred registries already work. If the project prefers a kind per behaviour, it is `RerollDice` and `SetDice` instead; I recommend the single kind.
3. **ADR-0007 Amendment 3 — interception.** Zones gain a mode that reads a move's traversed cells and truncates it. This is the real work: `MovementResolver` must return the interception, `GameEngine` must resolve the landing there, `DeferredCellEffects` must record which operators a table has already stopped, and a `MoveIntercepted` event has to exist for the view.
4. **Bots.** `CastPlanner` has no notion of a die, so an untaught Fortuna measures as the worst operator on the roster — the Predator's Read failure (0.00 casts) with three abilities instead of one. **She needs bot work in the same commit or the sweep will lie:** a cash rule (cash when the best landing for the die is worth less than 2 energy or is actively harmful), a re-roll rule (deploys and exact-reach kills), a Boxcars rule (bank at 9 when two or more operators are yarded, or when the squad's remaining journey is inside 12 pips), and a table placement rule (the cell most enemy pips must cross).
5. **GUI.** The dice tray becomes interactive for the first time: cash and re-roll need gestures and a confirmation, and the truncated landing needs a preview that reads as a wall. `GUI_PHASE.md` work, not core work.

Tests, roughly the shape `LetheTests`/`RevuTests` took: cash legality and the once-a-turn rule; cash satisfies §6.1; a cashed die grants no haste and trips no watch; re-roll leaves the doubles grant alone; Boxcars refused with a spent die; Boxcars at the roll cap; interception on a crossing, on a landing, refused on a safe cell, ignored for allies and for placement, once per operator, and the stop resolving a collision.

---

## 11. Rulings I have taken, for the designer to overturn

1. **Two energy flat**, not scaled to the die's face. `floor(face/2)` matches the grant formula and is prettier, but it pays 0 for a 1, which is the die you most want to cash.
2. **Once per turn**, not once per roll. Per roll would triple on a doubles chain.
3. **The table stops each operator once**, not once in total. If she reads as oppressive, "the table closes after the first interception" is the first nerf and it is a one-line change.
4. **Boxcars honours the double.** Setting a double and then denying the roll it is owed is a special case, and special cases are what this project avoids.
5. **Cashing is not moving.** No haste, no watch trip, no collision.

---

## 12. Dials, in order

**If she is too strong:** cash 2 → 1 energy · the table closes on its first interception · Deal Again 3 → 4 · the table's duration 2 ticks → 1 · Boxcars loses the extra roll.
**If she is too weak:** the table's radius 0 → 1 · Deal Again re-rolls both dice · cash twice a turn when she is standing still · health 7 → 8.
**Do not touch her speed.** At 1.5× she banks money *and* races, and the passive's whole cost is the tempo she gives up.

## 13. Sim protocol

Same as the Sanity re-baseline: 800 matches, bots sweep, before and after, with the bot rules above in place. Expect the cast counts to be the honest signal rather than her win share — a Fortuna who cashes fewer than four dice a match is a bot problem, not a balance result. Watch three numbers: turns per seat (she should lengthen the match slightly), energy burned per match (she should raise it — if burn explodes, the cash rate is wrong), and Mimi's and Kian's win share in squads that field her (the funding thesis, which is the reason to ship her).

---

## 14. Considered and declined

- **The Rake** — a marker that forfeits the lower die of an enemy seat's next roll. The sharpest possible "the house takes its cut", and cut for two reasons: cross-turn dice denial is the least answerable thing in the game, and taxation is the lane `OPERATOR_DRAFTS.md` §2 already reserves for Ghost.
- **Reaching into the home column** — pulling a leader back out of the mouth. The biggest structural hole in the game (the endgame is uncontested), and it contradicts §4.3 twice over and is miserable to be on the end of. The table's approach-side stop delivers the tension at a tenth of the cost.
- **A Tech amplifier** — the open item from §2.2, still waiting. It is a good operator, and it is a *support* operator: it lifts three kits and invents nothing. She funds the same three by touching the currency instead, which is the more interesting version of the same fix. The amplifier should still exist one day.
- **All In** — an ult that burns the whole pool for damage scaled to what it burned. Dramatic, and it collides with Sadist's arithmetic and with the no-kill identity that makes Boxcars safe to print.
- **Zero damage** — she could carry none at all. Left at two on the table so `OPERATOR_DRAFTS.md` §2 keeps "zero damage, a roster first" for Ghost.

---

## 15. What shipping it actually taught (2026-09-18)

Built whole in one pass: core, bots, tests, view and docs. 692 tests pass, every new rule mutation-checked, one survivor recorded (the "a cashed die must be one she could have moved" guard, which the speed floor makes unreachable in play).

**What the design got right.** The cash rate needed no cooldown — the race polices it, exactly as argued, and the bots sell 5.39 dice a match without ever selling a six that would deploy. The table's counterplay works out of existing abilities rather than new ones. Boxcars reads as tempo and never as a kill.

**What the build changed.**

- **A re-roll takes the lowest die, by ruling.** The proposal left the choice open; carrying a die face on the ability command would have meant a tray gesture for a decision that has one right answer in every case the ability exists for.
- **The planner's first table scoring asked the wrong question** — it looked for enemies standing on the cell, the test every other cell ability uses, and cast it 0.61 times a match. Asking what is *behind* the cell took it to 2.47. The lesson is the one the Cryo Field branch already taught: an ability the planner has no correct question for measures as a bad ability.
- **The draft picker still reads her last** (6.6 against 8.9–17.9) and a weight was not bent to hide it. It is in `COMBAT_SYSTEMS.md` §12 with a test that pins the ranking.
- **She found a pre-existing bug.** `CheckAbility` answered Ready for a caster standing in a home column, where the resolver has always refused. 111 refusals in the first sweep, none after.

**The measured result**, on the designer's 2026-09-18 balance pass: Fortuna **23%** win share in a field running 22% to 28% — the tightest the roster has been. Deal Again 2.96 casts a match, Boxcars 2.62, The Table 2.54, **dice sold 5.35**, refusals 0, turns per seat 26.7. Mid-field on her first measurement, which is what a utility operator should produce; the dials, in both directions, are in §12 above and in §10.12.

**A note on the base.** This was built against the tree as it stood on 2026-09-17 and re-based onto the 2026-09-18 balance pass afterwards, so every figure here is measured on the newer one. The two conflicts worth recording: `CellsFor` gained a speed-cap parameter, and `ADR-0007 Amendment 2` was taken by Eris' Exploit striking on the cast, so the table is **Amendment 3**.
