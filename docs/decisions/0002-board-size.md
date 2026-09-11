# ADR-0002: Board Size — 48-Space Circuit

> Location in repo: `docs/decisions/0002-board-size.md`
> Status: **Accepted** — 48 confirmed by simulation; no longer provisional.
> Date: 2026-07-10 · Amended 2026-07-10 (home-column length + constants) · Amended 2026-09-11 (pacing model corrected, speed band, board profiles)
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

Constraints: `CircuitLength % 4 == 0`; `PlayerStartOffset = CircuitLength / 4`; `HomeColumnLength = PlayerStartOffset / 2`. One integer defines a board.

**Sprint exists to iterate on combat, not to be played.** A _longer_ board is the wrong test bed for combat rules — it spreads operators apart and produces fewer interactions per match, which is the exact failure mode this ADR was written to avoid. Sprint needs no new art: same topology, fewer cells.

### The measured finding that should worry us most

**All three of a player's operators are on the board together only 10–15% of turns.** Deploy friction and the yard setback compound, and the track stays sparse. For a game whose stated priority is 70% combat, that is a more serious number than any pacing figure in this ADR, and it is not a board-size problem — see ADR-0005 Consequences and `COMBAT_SYSTEMS.md` §12 items 1–3.

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

- 2026-07-10 — Accepted (provisional). 48 chosen; 60 held open as tested fallback.
- 2026-07-10 — Amended. HomeColumnLength = 6 (was HomeStretchLength = 4); canonical constants corrected to total 72 path positions.
- 2026-09-11 — Amended and promoted to Accepted. Original pacing model shown to be incomplete on four terms; 48 confirmed by simulation at 16.7 turns mean; speed band set to 1.5–2.0; 60 withdrawn as fallback in favour of 40/36; board profiles introduced; revisit trigger replaced with a ranked lever list.
