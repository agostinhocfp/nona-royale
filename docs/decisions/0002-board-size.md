# ADR-0002: Board Size — 52-Space Circuit

> Location in repo: `docs/decisions/0002-board-size.md`
> Status: **Accepted** — 52/6, the classic Ludo cross. 48 was adopted for four amendments and could not be drawn (Amendment 6).

- 2026-09-13 — Amendment 6 revisited. The whole drawable family measured: combat density is nearly flat across it (0.41–0.46 kills per turn from 28 cells to 60), so board size is a pacing dial and almost nothing else. **44/5 confirmed as the in-budget candidate** — 20.8 turns against Standard's 22.6, four off the p90, fractionally denser, and landing Amendment 4's readability target at 21% of the loop. Not adopted: a smaller-than-Ludo board is a felt property and wants a session. Amendment 6's own re-baseline superseded — the roster changed under it, chiefly `EvasionChance` 0.5 → 0.3.

- 2026-09-12 — Amended. 48 shown to be undrawable as a four-arm cross: the loop needs `8L + 4` cells and 48 implies an arm of 5.5, which `BoardLayout` had been absorbing with four two-cell gaps in the track and home columns one cell short. Board corrected to **52/6, journey 58** — classic Ludo, which ADR-0003 always said this board was. `% 8 == 0` struck as a false invariant. Every absolute figure re-baselined; the match now runs 21.3 turns against a 15–20 minute budget, and 44/5 recorded as the in-budget fallback the old constraint had hidden.

> Date: 2026-07-10 · Amended 2026-07-10 (home-column length + constants) · Amended 2026-09-11 (pacing model corrected, speed band, board profiles) · Amended 2026-09-12 (laps and shipping profiles; Compact withdrawn and band lowered; mark damage, haste payout, and a measurement fault)
> Related: `docs/GDD.md`, ADR-0003 (board topology), ADR-0005 (unified neutralize), `docs/design/COMBAT_SYSTEMS.md`, `PathManager.cs`

## Context

The board size was never deliberately chosen. The codebase and GDD had drifted into three different numbers (GDD: 76; `PathManager`: 76-space circuit; `PathSpace`: 96 total). Board size couples directly to dice, energy curve, and win condition, and determines game length. With ~7 avg roll per turn and the old ~81-space journey, a full 3-operator win implied ~30–40+ turns per player — far too long.

Design targets driving this decision:

- **Game length:** quick, ~15–20 minutes for a full game (all 3 operators home).
- **Fun priority:** combat/abilities over the race, ~70/30.
- **Platform:** readable on mobile as well as desktop/tablet.

These align: a shorter, tighter loop hits the time budget, stays readable on a phone, and — critically for the combat priority — forces operators into the same neighborhoods so abilities get used. A long sparse loop spreads players out into a lonely race.

## Decision

Adopt a **48-space main circuit**, with home columns per player (length set in the amendment below).

Rationale for 48:

- Divides cleanly by 4 players and by the 2-dice system.
- Full game ≈ 12–15 turns/player ≈ 15–20 min.
- Tight enough to force encounters; readable on mobile.

## Amendment (2026-07-10) — home-column length and canonical constants

The original draft carried `HomeStretchLength = 4` under a since-corrected layout in which the home stretch was a minor tail near the corner. Once the true topology was worked out (ADR-0003: standard Ludo cross, home column = the arm's colored center lane), that value was wrong — "4 is too little." The home column is a **major structural element** whose length is geometrically tied to the arm.

Resolved:

- Rename `HomeStretchLength` → **`HomeColumnLength = 6`**, geometrically derived from the 12-cell arm to match classic Ludo (arm and home column share length; keeps the board square/symmetric).
- Canonical constants (single source of truth; **config values, not literals**):
  - `CircuitLength = 48`
  - `HomeColumnLength = 6`
  - `PlayerStartOffset = 12` (start cells one quarter apart)
  - Total path positions = 48 + (4 × 6) = **72**

This resolves the old 76/96 drift completely.

---

## Amendment 2 (2026-09-11) — the pacing model was wrong; 48 is right anyway

### What was wrong

The original decision sized the board on this reasoning: ~7 cells per turn, ~54-cell journey, three operators, therefore 12–15 turns per player. **Every term in that chain was incomplete.**

1. **It omitted deployment.** Operators start in the yard and deploy only on a 6 (ADR-0003) — 11/36 per turn. Getting three operators onto the board costs roughly three turns before the race meaningfully starts, and **every neutralize pays that cost again**.
2. **It omitted the speed multiplier.** Movement is `dice × speedMultiplier` and the alpha roster ranged 1× to 3×, so "~7 cells per turn" described only a 1× operator. At 1× uniformly the board runs **33.6 turns**, not 12–15.
3. **It omitted the setback.** The unified neutralize model (ADR-0005) returns an operator to the yard with all progress lost. Combat adds **6.4 turns** to a 4-player match, which the original estimate did not account for at all.
4. **It counted moves as 1 per turn.** Doubles grant re-rolls, giving ~1.19 moves per turn.

The original 12–15 figure was therefore not achievable at any speed band, and the "revisit trigger" it set (long/sparse → test 60) pointed the wrong way: 60 measures at **29.8 turns**.

### How this was resolved

By **simulation, not argument**. A headless Monte Carlo harness (`tools/sim/`) plays full matches against the `COMBAT_SYSTEMS.md` rules with scripted policies, 10,000 matches per configuration, seeded. This is the first cash-in on ADR-0004's deterministic pure-C# core: every number below is measured.

### Measured — speed band, 4 players, Standard board

| Band (Bouncer / Syla / Kurbyn)      | Mean turns | p90    | Longest single move |
| ----------------------------------- | ---------- | ------ | ------------------- |
| 1.0 / 1.0 / 1.0                     | 33.6       | 44     | 12                  |
| 1.0 / 1.5 / 1.5                     | 24.1       | 30     | 18                  |
| 1.0 / 2.0 / 2.0                     | 19.5       | 24     | 24                  |
| **1.5 / 2.0 / 2.0**                 | **16.7**   | **21** | 24                  |
| 1.0 / 2.0 / 3.0 (roster as written) | 17.8       | 22     | 36                  |

### Measured — board length, 4 players, adopted band

| Circuit / home | Mean turns                                             |
| -------------- | ------------------------------------------------------ |
| 24 / 3         | 13.0                                                   |
| 32 / 4         | 16.5                                                   |
| 36 / 5         | 18.5                                                   |
| 40 / 5         | 20.2                                                   |
| **48 / 6**     | **24.0** at 1.0/1.5/1.5 · **16.7** at the adopted band |
| 60 / 7         | 29.8                                                   |

Board length scales close to linearly, which confirms the Amendment 1 claim that `CircuitLength` is genuinely config and not a literal. That claim had never been executed until now.

### Resolved

- **`CircuitLength = 48` is confirmed.** Status moves from provisional to accepted. At the adopted speed band it measures 16.7 turns mean, p90 21 — about 67 player-turns at 4 players, which fits the 15–20 minute target at a realistic digital turn length.
- **`SpeedMultiplier` band is 1.5 – 2.0**, half-steps, with schema bounds `SpeedMultiplierMin = 1.0` / `SpeedMultiplierMax = 2.5`. Alpha roster: Bouncer 1.5, Syla 2.0, Kurbyn 1.5 base + 0.5 passive.
  - Below 1.5, the squad is gated by its slowest operator: a 1.0 tank taxes every match by ~3 turns while his squadmates idle.
  - Above 2.0, a single move stops being readable: at 2.5× a double-6 covers 30 cells, over half the loop in one action.
- **Slow rescales to −0.5** and Tagged From Above's payout from +3 to +0.5, since both were sized against a band that no longer exists (`COMBAT_SYSTEMS.md`).
- **60 is withdrawn as the fallback.** It measures 29.8 turns. If the board ever needs to move it moves _down_: **40** (20.2 turns) or **36** (18.5) — both still divisible by 4.
- **The revisit trigger is replaced.** Old: "long/sparse → test 60." New: _if matches run long, the levers in priority order are the yard setback (6.4 turns), the deploy gate (1.9 turns), ability damage (3.4 turns), and only then board length._ Board length is the blunt instrument, not the first resort.

### Board profiles

`CircuitLength` alone no longer defines a board. Three named profiles are config data in the core:

| Profile      | Circuit | Home column | Journey | Measured   | Purpose                                                                                    |
| ------------ | ------- | ----------- | ------- | ---------- | ------------------------------------------------------------------------------------------ |
| **Sprint**   | 24      | 3           | 27      | 13.0 turns | Combat iteration. Everyone permanently in range; ~4× the ability-resolution reps per hour. |
| **Standard** | 48      | 6           | 54      | 16.7 turns | The shipping board.                                                                        |
| **Long**     | 60      | 7           | 67      | 29.8 turns | Retained for measurement only. Not a shipping candidate.                                   |

Constraints: `CircuitLength % 4 == 0` and `PlayerStartOffset = CircuitLength / 4`. Both are genuine invariants — an indivisible circuit spaces the four starts unevenly and silently hands one seat a shorter journey.

**Correction (2026-09-11).** This section previously added `HomeColumnLength = PlayerStartOffset / 2` as a third invariant and claimed one integer defines a board. That is false, and it is false against this ADR's own table: 36 gives an arm of 9, which does not halve, yet 36/5 is listed above as the primary fallback. Classic Ludo fails it too — a 52-cell circuit with a 6-cell column.

The halving rule is a good guide for _choosing_ a home column: it makes the column the same length as the arm, which is what keeps the cross looking square. It is not a law. **Both numbers are explicit config**, and the core exposes `BoardProfile.FromCircuitLength` as a convenience for the circuits where the rule does apply (24, 32, 40, 48, 56). It refuses 36, 52 and 60 — meaning "this shortcut cannot derive that board", never "that board is invalid". Those are built with the constructor, stating both numbers.

**Sprint exists to iterate on combat, not to be played.** A _longer_ board is the wrong test bed for combat rules — it spreads operators apart and produces fewer interactions per match, which is the exact failure mode this ADR was written to avoid. Sprint needs no new art: same topology, fewer cells.

### The measured finding that should worry us most

**All three of a player's operators are on the board together only 10–15% of turns.** Deploy friction and the yard setback compound, and the track stays sparse. For a game whose stated priority is 70% combat, that is a more serious number than any pacing figure in this ADR, and it is not a board-size problem — see ADR-0005 Consequences and `COMBAT_SYSTEMS.md` §12 items 1–3.

---

## Amendment 3 (2026-09-12) — laps, two shipping boards, and opening deployments

### What changed

Amendment 2's figures came from a Python _model_ of the rules. The model has been replaced by a harness driving `NonaRoyale.Core` itself (`tools/sim/NonaRoyale.Sim`), so every number below is produced by the code that ships.

Pacing held up almost exactly — 16.8 turns measured against 16.7 modelled. **Combat did not.** The model predicted ~8 neutralizes per match; the real rules produce **4.0**. The model flattened every ability into damage at range 3, ignoring cooldowns, range 1 on two of the strongest abilities, and stun and slow consuming a cast without killing anything. The game is materially less lethal than Amendment 2 implied.

### The diagnosis, corrected

Amendment 2 blamed low occupancy on the yard setback. **That is wrong.** At 4.0 neutralizes across four players — one per player — the setback barely fires.

The actual cause: **an operator crosses the whole board in about four of its own moves.** At 2.0× a roll of 7 covers 14 cells against a 54-cell journey. It deploys after ~3 turns and is home ~4 turns later. It exists for a quarter of the match, mostly alone.

So the lever is journey length relative to speed — how many turns an operator is _on the board_ — not damage, not the deploy gate, not collision.

### Laps

`BoardProfile` gains a **`Laps`** value. An operator completes `Laps` circuits before turning into its home column: `Journey = CircuitLength × Laps + HomeColumnLength`.

This decouples two things the ADR had treated as one. A shorter loop travelled more times gives the same journey in half the space, which is where encounters come from — and it does so without making any single move less readable, which is what compressing the speed band would have cost.

Two operators on the same cell with different lap counts **do** collide. The lap count is bookkeeping on the operator, not on the board.

### Measured — loop size against laps, 4 players, adopted band, 800 matches each

| Profile    | Journey | Turns | p90 | Neutralizes | Abilities | Collisions | 3-up |
| ---------- | ------- | ----- | --- | ----------- | --------- | ---------- | ---- |
| **48 × 1** | 54      | 16.8  | 20  | 4.0         | 31.4      | 2.3        | 12%  |
| **24 × 2** | 51      | 20.1  | 26  | **9.6**     | **42.0**  | **5.5**    | 11%  |
| 16 × 3     | 50      | 24.4  | 33  | 15.7        | 52.5      | 8.4        | 11%  |
| 24 × 3     | 75      | 38.0  | 52  | 24.1        | 84.1      | 12.4       | 16%  |
| 48 × 2     | 102     | 37.6  | 49  | 15.6        | 80.4      | 7.6        | 22%  |

**Halving the loop and doubling the laps gives 2.4× the combat for 3.3 turns, at an identical journey.** No combat value changed.

### Density and occupancy are different problems

This ADR and `COMBAT_SYSTEMS` §12 have treated them as one figure. They are not:

- **Loop size buys encounters.** It leaves occupancy flat at 11–12%.
- **Journey length buys occupancy**, and only it does — 48 × 2 reaches 22%, at 37.6 turns.
- **Opening deployments buy occupancy for free**, which nothing else does (below).

### Opening deployments

`openingDeployments` puts operators on their start cells at match start, skipping the deploy roll for those.

| Board      | Opening | Turns    | Neutralizes | 3-up    |
| ---------- | ------- | -------- | ----------- | ------- |
| 48 × 1     | 0       | 16.8     | 4.0         | 12%     |
| 48 × 1     | 2       | 13.9     | 3.0         | **27%** |
| 24 × 2     | 0       | 20.1     | 9.6         | 11%     |
| **24 × 2** | **2**   | **16.7** | **8.2**     | **25%** |

**This is the only lever measured that improves a problem while costing nothing elsewhere** — occupancy more than doubles and matches get _shorter_.

It also reframes the deploy rule usefully. Deploy-on-a-6 currently acts as an opening tax: the first three turns are spent waiting to play. With two operators already out, the yard becomes almost entirely a **consequence of losing a fight**, which is where that friction belongs.

The cost is that it removes most of the classic Ludo opening scramble. If that scramble is wanted, `openingDeployments = 1` still buys 15% occupancy and two turns.

### Resolved — two shipping profiles

| Profile      | Circuit | Laps | Home | Journey | Opening | Measured                                   |
| ------------ | ------- | ---- | ---- | ------- | ------- | ------------------------------------------ |
| **Standard** | 48      | 1    | 6    | 54      | 2       | 13.9 turns, 27% occupancy, 3.0 neutralizes |
| **Compact**  | 24      | 2    | 3    | 51      | 2       | 16.7 turns, 25% occupancy, 8.2 neutralizes |

_Superseded by Amendment 4: Compact was withdrawn after a human session, having shipped with the very readability defect its speed band was meant to prevent._ Both were adopted here on the reasoning that they are the same game at two densities, and that which one players prefer is a question simulation cannot answer — which turned out to be true, and answered against Compact. `Sprint` (24 × 1) is retained as a development board; `Long` (60 × 1) for measurement only.

### The lever ranking is replaced again

Amendment 2 ranked the levers as yard setback, deploy gate, ability damage, then board length. Measured against the live rules, in order of effect per turn spent:

1. **Ability reach.** +1 to every range and radius: **+36% neutralizes for 0.4 turns**. Nothing else is close. It confirms the diagnosis — abilities were missing because they could not reach, not because they were weak or expensive.
2. **Loop size with laps.** 2.4× combat for 3.3 turns.
3. **Opening deployments.** Occupancy for free.
4. **Journey length.** Occupancy, at real pacing cost.
5. **`CollisionDamage` is not a dial at all.** From 2 to 6 moves match length by 0.4 turns and neutralizes by 1.1, because collisions only occur ~2.4 times a match. **Amendment 2 and `COMBAT_SYSTEMS` §12 both name it as the first lever if the race reads as toothless. That advice is struck.**

### Also measured

**Player count barely affects pacing** — 16.5 / 16.4 / 16.8 turns at 2 / 3 / 4 seats — but combat scales hard with it: **0.6 neutralizes at two players against 4.0 at four.** A two-player match is close to a pure race. If 1v1 is meant to be a real mode it needs its own configuration, not just fewer seats.

### Still not measured

Whether any of this is fun. The harness reports pacing and throughput; it says nothing about whether 2.4× the combat reads as tension or as noise. That remains a human playtest.

---

## Amendment 4 (2026-09-12) — Compact withdrawn; the speed band lowered

### What a human saw that the harness could not

Compact 24×2 was adopted in Amendment 3 on the strength of 2.4× the combat at an identical journey. The first session playing it reported that operators _jump around the board_ — pieces cross too much ground per move to follow.

The arithmetic confirms it immediately, using a figure this ADR had never tracked:

> **Mean move as a share of the loop** = `7 × mean speed ÷ CircuitLength`

| Configuration                      | Share of loop per move |
| ---------------------------------- | ---------------------- |
| Standard, adopted band 1.5/2.0/2.0 | 27%                    |
| **Compact 24×2, adopted band**     | **53%**                |
| Standard, band 1.0/1.5/1.5         | 19%                    |

Amendment 2 capped the speed band at 2.0 precisely because "above 2.0 a single move stops being readable." **That constraint was then never re-applied when the loop halved.** A 2.0 multiplier on a 24-cell loop is proportionally a 4.0 multiplier on a 48-cell one. Compact shipped with the exact defect its own band was set to prevent.

### The real governing number

Raw multiplier was the wrong thing to track. What a player can follow is **how much of the board a move crosses**, and that depends on the loop as much as the speed. Recorded here as the constraint to check whenever either changes:

> **Target: a mean move covers roughly a fifth of the loop. Past a third it stops being readable.**

### Measured — Standard board, slower bands, 4 players, opening 2, 600 matches

| Band                    | Turns    | p90    | Neutralizes | Abilities | 3-up    | Move %  |
| ----------------------- | -------- | ------ | ----------- | --------- | ------- | ------- |
| 1.0 / 1.0 / 1.0         | 37.7     | 49     | 13.8        | 79.0      | 21%     | 15%     |
| 1.0 / 1.25 / 1.25       | 28.3     | 36     | 10.0        | 60.5      | 26%     | 17%     |
| **1.0 / 1.5 / 1.5**     | **22.7** | **28** | **7.7**     | **49.0**  | **30%** | **19%** |
| 1.25 / 1.5 / 1.5        | 20.0     | 24     | 5.9         | 42.4      | 26%     | 21%     |
| adopted 1.5 / 2.0 / 2.0 | 13.9     | 17     | 3.0         | 29.0      | 27%     | 27%     |
| _Compact 24×2, adopted_ | _16.8_   | _22_   | _8.3_       | _38.2_    | _25%_   | _53%_   |

**The Standard board at 1.0/1.5/1.5 delivers Compact's combat at a third of its move size** — 7.7 neutralizes against 8.3, at 19% of the loop against 53%. It also produces the highest squad occupancy measured anywhere, 30%.

The density was never about board size. It was about how much of the board a move covers, and shrinking the loop was the worst available way to buy it, because it moved the numerator and the denominator in opposite directions.

### Resolved

- **Compact 24×2 is withdrawn** as a shipping profile. Laps remain implemented in `BoardProfile` and are retained for measurement; nothing about them was wrong, and a lapped board at a proportionate speed band stays a legitimate future option.
- **Standard 48×1 is the shipping board**, sole.
- **The speed band becomes 1.0 – 1.5.** Bouncer 1.0, Syla 1.5, Kurbyn 1.0 base + 0.5 passive = 1.5. Schema bounds stay 1.0–2.5 so measurement is unconstrained.
- **`openingDeployments = 2` is unchanged.**
- Expected match: **22.7 turns, p90 28, 7.7 neutralizes, 30% occupancy.**

### This reverses Amendment 2, and the reason it does is worth keeping

Amendment 2 set the band floor at 1.5 because a 1.0 tank taxes every match by roughly three turns while its squadmates idle. That was correct when it was written and nothing else had changed.

It stopped being correct once two other levers landed. Opening deployments absorbed part of the tax, and the slower band turns the rest of it into combat and occupancy rather than dead time. It also restores Bouncer's designed identity — `COMBAT_SYSTEMS` §10.1 calls him the squad's roadblock, and raising him to 1.5 for pacing reasons had quietly contradicted that.

### The cost, stated plainly

Matches go from 13.9 turns to 22.7, with a p90 of 28. At four players that is roughly 91 player-turns against 56. **Whether that is too long is the one thing simulation cannot answer**, and it is now the open question ahead of the next playtest. If it runs long, the levers in order are reach (already unspent), then `openingDeployments = 3`, then the band back toward 1.25.

---

---

## Amendment 5 (2026-09-12) — mark damage, the haste payout, and a measurement fault

### What this amendment is not

Amendments 2 through 4 adopted values because a harness measured them. **This one does not.** Every value below was reasoned at a desk and none has been simulated, because the harness cannot currently be trusted — see the fault below. They are adopted provisionally so the work can land; they are not confirmed, and the revisit trigger is a harness run rather than a playtest.

Recorded as its own amendment rather than folded into `COMBAT_SYSTEMS` §12 precisely so the distinction survives. A reasoned number and a measured number look identical in a config file six months later.

### The fault: Kurbyn's passive never reached the engine

`AlphaRoster.KurbynPassiveSpeedBonus = 0.5` was read by nothing that affects a match. Three things had to line up, and all three were wrong:

- `MatchFactory` granted the passive as `ApplyPassive(op, StatusKind.Evasion)` with no magnitude, which defaults to `0.0`.
- `StatusRegistry.SpeedModifier` returned only the slow penalty, hardcoded, and never read `Entry.Magnitude` at all — the field was written by `Apply()` and consumed by no one.
- The test that covered it asserted `KurbynBaseSpeed + KurbynPassiveSpeedBonus == 1.5`, which is arithmetic on two constants and passes whether or not the engine applies either.

Kurbyn therefore moved at his base speed in every simulated match this ADR has ever quoted.

### What that does to the tables above

The harness labels its rows with the _effective_ band while constructing the _base_:

- `Program.cs` labels a row `"1.0 / 1.5 / 1.5"` and builds `new RosterSpeeds(1.0, 1.5, 1.0)`.
- `Dynamic.cs` does the same, and computes its `Move %` column as `(Bouncer + Syla + KurbynBase + KurbynPassiveSpeedBonus) / 3`.
- `RosterSpeeds.ToString()` prints `KurbynBase + KurbynPassiveSpeedBonus`.

So every band label in Amendments 2, 3 and 4 overstates Kurbyn by 0.5, and every `Move %` figure was computed against a mean speed no match ran at.

**Amendment 4's adopted row is the one that matters.** Labelled 1.0 / 1.5 / 1.5, it measured 1.0 / 1.5 / **1.0**. Mean speed 1.167, not 1.333; mean move 17% of the loop, not the 19% reported. The configuration this ADR currently calls the shipping band has never actually been measured, and when it is, it will run **shorter than 22.7 turns** — a third of every squad just became 50% faster.

### What survives and what does not

**Survives.** The fault was constant across every row of every sweep, so relative comparisons hold. Compact 24×2's withdrawal stands — that was driven by loop size, and its 53% move share is wrong in the same direction as everything else but nowhere near enough to save it. The lever ranking stands. `CollisionDamage` remains struck as a dial.

**Does not survive.** Every absolute figure: turn counts, p90s, neutralize counts, occupancy. Specifically, Amendment 4's expected match of _22.7 turns, p90 28, 7.7 neutralizes, 30% occupancy_ is withdrawn pending a re-run, and the baseline tripwire in `tools/sim/README` is invalidated — it was already one amendment stale, still quoting the Amendment 3 row.

### Adopted, provisionally and unmeasured

| Dial                       | Value | Reasoning                                                                                                                                                                                                                                                           |
| -------------------------- | ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `MarkDamagePerTurn`        | 2     | Over a 2-turn mark this totals 4, leaving a 6-HP operator at 2 — inside collision, execute and From the Hip range. At 3 over 3 turns a mark deals 9 and kills both 6-HP operators unassisted, which makes Tagged From Above's own payout condition self-fulfilling. |
| `HasteSpeedBonus`          | 0.5   | Inherited from Amendment 2's +3 → +0.5 cut, which was measured against the 1.5–2.0 band where it was a ~25% bump. Against 1.0–1.5 it is +33% to +50%. Unchanged for now because changing it and the mark at once would make neither separable.                      |
| `HasteDurationTurns`       | 2     | Not a balance figure. The payout can fire on the marker's own turn, by which point that turn's movement is spent, so a 1-turn buff would routinely be worth nothing.                                                                                                |
| All-In Mauling range       | 1 → 2 | A range-1 ability on the roster's slowest operator was unusable by construction.                                                                                                                                                                                    |
| All-In Mauling self-damage | 3 → 1 | Arguably overshoots: at 12 HP it now takes eleven casts to self-neutralize, so the cost is close to flavour. 2 was the recommendation.                                                                                                                              |

**Closed 2026-09-18** (designer): the cost dropped to 4 and the zero cooldown became 1, with damage 2 → 3, the ally heal 3 → 2 and, later that day, the self-damage 2 → 1 (COMBAT_SYSTEMS §10.1). The original note follows.

**Was open:** All-In Mauling's energy cost. At 6 against a mean drip of 3.5 per turn the economy already gates it to roughly every second turn, so its `cooldownTurns: 0` — the only thing distinguishing it from Velvet Rope, which is the same cost and damage at longer range with a pull — buys nothing. Either the cost drops to 3 or the zero cooldown should be dropped as fiction.

### Resolved

- **Kurbyn's passive carries its speed as the status entry's magnitude**, and `SpeedModifier` sums signed magnitudes across every active status and passive. This also gives Slow a per-source size for the first time and is what makes the haste payout expressible at all.
- **Passives are stored separately from applied statuses**, so `ClearAll` on neutralize no longer deletes them. Before this, Kurbyn would have lost Evasive Protocol permanently the first time he died — had the passive been working.
- **Every band label in Amendments 2 through 4 is to be read as base speeds**, with Kurbyn 0.5 lower than stated.
- **The values in the table above are provisional.** They ship so the mark is playable; they are not confirmed.

### Revisit trigger

A harness run, in this order, each sweep separable:

1. 1. ~~**Re-baseline** on the fixed core and regenerate the `tools/sim/README` tripwire row.~~ **Done** — but twice, because the first re-baseline measured a board that was then found undrawable. The current figures are in Amendment 6. The `tools/sim/README` tripwire row still needs regenerating.
2. **Rule on slow stacking.** `GameEngine` sums the status and aura speed channels, so Slow plus Intimidating Presence reaches the `MinSpeedMultiplier` floor — two half-slows becoming a hard stop, which `COMBAT_SYSTEMS` §5.2 exists to prevent. The scripted player triggers this constantly, so it distorts any measurement taken before it is settled.
3. **Sweep `MarkDamagePerTurn` at 1 / 2 / 3 against durations 2 / 3.** The figure to watch is not match length but _what share of marked targets die to the mark itself_ rather than to a follow-up. A high share means the payout is self-fulfilling regardless of what pacing says.
4. **Re-check `HasteSpeedBonus`** against the corrected band.
5. **Re-check `SlowSpeedPenalty`**, which was measured against 1.5–2.0 and never re-run when Amendment 4 lowered the band. At −0.5 against a 1.0 operator it now reaches the floor on its own.

---

---

## Amendment 6 (2026-09-12) — 48 cannot be drawn, and never could

### The fault

**48 is not a circuit a four-arm Ludo cross can have.**

A cross's loop threads each arm as two flanking lanes of length L plus the tip cell of the centre lane it has to cross. Four arms, so:

> `CircuitLength = 4 × (2L + 1) = 8L + 4`, on a grid of `2L + 3`.

48 needs L = 5.5. The drawable circuits are **28, 36, 44, 52, 60** — and 52 on a 15×15 grid is classic Ludo, which is what this project said it was copying 1:1 from the beginning (ADR-0003).

`BoardLayout` had been absorbing the mismatch by handing each arm's tip cell to the home column and letting the loop hop over it. That put **four visible two-cell gaps in the track** and left **every home column one cell short of HOME**. It was caught by the only code that enumerates intermediate cells — the view's move animation, which walks a piece one cell at a time and so made a piece visibly leap at each arm tip.

Nothing in the core was wrong. `PathMap` knows that cell 14 follows cell 13 and nothing about where either one is (ADR-0004), which is exactly why the rules, the tests and every measurement were unaffected by a board that could not be drawn. **The separation worked; it also meant the defect could survive four amendments of simulation.**

### Resolved

- **`CircuitLength = 52`, `HomeColumnLength = 6`, journey 58, on a 15×15 grid.** Total path positions `52 + (4 × 6) = 76`. `PlayerStartOffset = 13`.
- **`CircuitLength % 8 == 0` is dead.** It was never a real invariant — it was a coincidence of the boards being considered. The genuine constraint is the cross family above.
- **The constraint lives in `BoardLayout`, not in `BoardProfile`.** The core tolerates any circuit divisible by four, because the rules do not care about arm geometry and nothing in the core should start caring. A board has to be drawable only where it is drawn, and the harness legitimately measures boards that will never be rendered. `BoardProfile.Cross(name, armLength)` is the constructor for anything intended to ship.
- **Profiles restated:**

| Profile      | Circuit | Home | Laps | Journey | Grid  | Note                                          |
| ------------ | ------- | ---- | ---- | ------- | ----- | --------------------------------------------- |
| **Sprint**   | 28      | 3    | 1    | 31      | 9×9   | was 24/3                                      |
| **Standard** | 52      | 6    | 1    | 58      | 15×15 | was 48/6 — the shipping board                 |
| **Long**     | 60      | 7    | 1    | 67      | 17×17 | already a valid cross; unchanged              |
| _Compact_    | 28      | 3    | 2    | 59      | 9×9   | was 24×2. Withdrawn in Amendment 4 regardless |

`BoardProfile.FromCircuitLength` is kept for the harness and marked superseded. It is the halving rule that produced 48/6 in the first place.

### Re-baselined — 4 players, adopted band, opening 2, 800 matches

|             | Sprint 28/3 | **Standard 52/6** | Long 60/7 |
| ----------- | ----------- | ----------------- | --------- |
| Journey     | 31          | **58**            | 67        |
| Turns       | 12.7        | **21.3**          | 23.7      |
| p90         | 16          | **26**            | 28        |
| Neutralizes | 4.5         | **6.8**           | 7.2       |
| Abilities   | 29.0        | **46.9**          | 51.9      |
| Occupancy   | 32%         | **34%**           | 35%       |

These supersede every absolute figure in Amendments 2 through 5, and close the withdrawal Amendment 5 opened.

### The cost, and it is not small

**Standard went from 19.7 turns to 21.3, p90 23 to 26.** The 15–20 minute budget this ADR was written to protect is now **overdrawn**, and it was spent by a correctness fix rather than a design choice — nothing was bought with it.

Amendment 4 already stated the cost plainly and named the levers if it ran long: reach, then `openingDeployments = 3`, then the band back toward 1.25. `COMBAT_SYSTEMS` §12 has since established that every remaining lever trades at roughly **one turn per 1.5 neutralizes**, so all three buy pacing by giving up combat.

**The geometry offers a fourth that the old family could not.** L = 5 gives **44/5, journey 49, on a 13×13 grid** — an estimated 18.4 turns, inside budget, and a legitimate Ludo cross. It was unreachable before because 44 fails `% 8 == 0`, the constraint this amendment strikes.

Not adopted. It is a smaller board than classic Ludo and the readability constraint from Amendment 4 has to be re-checked against it — a mean move covers 21% of a 44-cell loop against 18% of 52, still inside the "roughly a fifth" target but worth measuring rather than asserting. **Recorded as the first thing to try if 21.3 turns reads long in a human session.**

### Revisit — 44/5 measured, and it holds up

The trigger was "measure 44/5 before touching any combat dial." Done, across the whole drawable family at 800 matches per row, four players, `openingDeployments = 2`, alpha three.

| Board         | Journey | Turns    | p90    | Neutralizes | Kills/turn | Mean move, share of loop |
| ------------- | ------- | -------- | ------ | ----------- | ---------- | ------------------------ |
| 28/3          | 31      | 15.3     | 17     | 6.6         | 0.43       | **33%**                  |
| 36/4          | 40      | 17.3     | 22     | 7.9         | **0.46**   | 26%                      |
| **44/5**      | 49      | **20.8** | **25** | 8.7         | 0.42       | **21%**                  |
| Standard 52/6 | 58      | 22.6     | 29     | 9.3         | 0.41       | 18%                      |
| 60/7          | 67      | 26.4     | 32     | 9.5         | 0.36       | 16%                      |

**Combat density is nearly flat across a board that doubles in journey** — 0.41 to 0.46 kills per turn end to end. Board size buys and sells pacing almost purely. That is a stronger version of Amendment 3's finding that journey length is the blunt instrument, and it is why the table above is a pacing decision rather than a balance one.

**44/5 costs 0.6 neutralizes and saves 1.8 turns and four off the p90**, and is fractionally denser than Standard by kills per turn. It also lands the Amendment 4 readability constraint exactly: a mean move covers **21%** of a 44-cell loop against the stated target of roughly a fifth. Standard is under at 18%; 36/4 is over at 26%; 28/3 sits on the 33% hard limit.

**Not adopted here.** Amendment 4 exists because a human noticed pieces jumping when a harness said the board was fine, and the same standard applies in reverse: 44/5 is a smaller board than classic Ludo, which this project has copied 1:1 since ADR-0003, and that is a felt property rather than a measured one. The measurement removes every reason not to try it; a session decides.

**If the budget is what gives instead**, say so explicitly rather than letting the board drift. The 15–20 minute target was set in this ADR's Context before anyone had played the game, and every board except the two smallest now exceeds it. It is a candidate for being wrong.

### The re-baseline in this amendment is already superseded

The table above this section records **21.3 turns, 6.8 neutralizes** for Standard. The current figure is **22.6 and 9.3**, on the same board and the same band.

The difference is not noise and not a fault — **the roster changed underneath it**. `EvasionChance` fell 0.5 → 0.3, Bouncer lost three health, Miracle Pull's range widened, and movement became compulsory. Every one of those makes a kill easier to land.

Recorded because the gap looked alarming before it was traced: **a figure is only comparable to another taken on the same rules**, and in this project the rules move faster than the measurements. `COMBAT_SYSTEMS` §12 now carries the current numbers and the configuration they were taken under; treat the table above as historical.

### Revisit trigger

A human session on Standard 52/6, with one question: does it run long? If yes, 44/5 is measured, drawable, and ready. If it does not feel long, amend the budget in Context rather than leaving a target nothing meets.

## Alternatives considered

- **36 circuit:** punchier/faster but cramped and swingy. Rejected as too chaotic for a first cut. _(Amendment 2: measures 18.5 turns; now the primary fallback if 48 runs long.)_
- **60 circuit:** more breathing room, but pushes past 20 min and gets tight on a phone. **Kept as the primary tested fallback.** _(Amendment 2: withdrawn — measures 29.8 turns.)_
- **Home column 5:** shorter run-in, breaks perfect Ludo symmetry slightly. Reasonable fallback if 6 tests slow.
- **76 / 96 (status quo):** too long; rejected.

## Consequences

- Config-driven lengths let us swap profiles without code changes — now demonstrated rather than asserted.
- ~~The avg-7-per-turn estimate ignores doubles (faster) and captures (slower); real pacing unknown until playtested.~~ _Superseded by Amendment 2: pacing is measured._
- **Revisit trigger:** human playtest. Simulation covers pacing and throughput; it says nothing about whether the game is _fun_, whether turns feel long, or whether the decisions are interesting. Those still need hands on it.

## Status history

- 2026-09-12 — Amended. Mark given damage over time and Tagged From Above's payout implemented; values adopted by reasoning rather than measurement, and marked as such. A measurement fault found: Kurbyn's passive speed bonus never reached the engine, so every band label in Amendments 2 through 4 overstates him by 0.5 and every absolute figure is withdrawn pending a re-baseline.
- 2026-09-12 — Amended again after the first human session. Compact 24×2 withdrawn: it moved pieces across 53% of the loop per turn. Speed band lowered to 1.0–1.5, which delivers the same combat at 19%. "Mean move as a share of the loop" recorded as the constraint that actually governs readability.
- 2026-09-12 — Amended. Harness ported onto the live core; pacing confirmed, combat figures corrected sharply downward. Laps introduced; two shipping profiles (Standard 48×1, Compact 24×2) adopted; opening deployments adopted; lever ranking replaced and `CollisionDamage` struck as a dial.
- 2026-09-11 — Corrected during core implementation. The `HomeColumnLength = PlayerStartOffset / 2` invariant and the "one integer defines a board" claim were wrong for 36 and 52; home column length is explicit config with the halving rule demoted to a guideline.
- 2026-07-10 — Accepted (provisional). 48 chosen; 60 held open as tested fallback.
- 2026-07-10 — Amended. HomeColumnLength = 6 (was HomeStretchLength = 4); canonical constants corrected to total 72 path positions.
- 2026-09-11 — Amended and promoted to Accepted. Original pacing model shown to be incomplete on four terms; 48 confirmed by simulation at 16.7 turns mean; speed band set to 1.5–2.0; 60 withdrawn as fallback in favour of 40/36; board profiles introduced; revisit trigger replaced with a ranked lever list.
