# Nona Royale — All Pick Draft (Stage 1)

> Location in repo: `docs/design/DRAFT.md` · Project copy: `claude/DRAFT.md`
> Status: **Open, 2026-09-16.** DR1 (core) built; DR2 (screen) next.
> Related: `NEXT_PHASES.md` (Stage 1), `GDD.md` §2.2, `PRESENTATION.md` §4.3, ADR-0004 (core has no Unity types), ADR-0008 (uGUI)

## Goal

Before the match, the seats choose their three operators from the full roster in view of everyone. Setup's squad row becomes **ALL PICK / SNAKE / RANDOM / ALPHA THREE**. When the draft completes, the match deals with those squads.

## Decisions (settled 2026-09-16)

1. **Two draft modes.** This changes `NEXT_PHASES.md`, which planned snake as the only order.
   - **ALL PICK (default).** One shared **30 s** clock for the whole draft. Any seat may pick at any time, in any order. When the clock runs out, every empty slot is filled at random and the match deals.
   - **SNAKE (mode #2).** Picks go in snake order: R, B, G, V, then V, G, B, R, then forward again. Each pick has a **10 s** clock. When it runs out, that pick is made at random for the seat, and the next pick's clock starts.
   - Both durations are config (`DraftConfig`), not literals.
2. **Uniqueness follows the GDD rule.** A seat's three operators are distinct. Two seats may field the same operator. There is no global-unique option.
3. **Picks are visible as they happen.** This is hot-seat on one device, so there is no pass-the-device screen.
4. **Input for ALL PICK: an active seat.** Click a seat column, or press 1–4, to make that seat the picker. Clicking a card then fills that seat's next empty slot.
5. **Helpers.**
   - ALL PICK:
     - **Clearing a slot:** click a filled slot to empty it. This replaces UNDO in ALL PICK.
     - **RANDOM:** fills the active seat's next empty slot.
     - **START:** available once every slot is full (Enter). It deals without waiting for the clock.
     - **FILL & START:** fills the rest at random, then deals.
   - SNAKE:
     - **RANDOM** picks for the current seat.
     - **RANDOM REST** finishes the draft.
     - **UNDO** (Backspace) reverts one step. The reverted pick gets a fresh clock.
     - **START** is available once the draft is complete.
   - Both modes: **BACK** (Esc) returns to setup, with a warning if any picks exist. While that warning is open, the clock is frozen.
6. **The clock is 30 s flat at every seat count.** Four seats share one pointer for twelve picks, so the clock may expire often at big tables. The timeout fill covers that. Tune it after a playtest.
7. **REMATCH keeps the drafted squads** under ALL PICK, SNAKE and ALPHA THREE, and uses the next seed. RANDOM still re-rolls, as it does today. NEW MATCH goes back through setup and the draft.
8. **Role lines live in the view.** An `OperatorCopy` table keyed by name, with a fallback, holds them (same pattern as `PieceShape`). Display text stays out of the rules.
9. **Random picks draw from a draft RNG**, `new SeededRandom(seed ^ DraftConfig.SeedSalt)`, never from the match RNG. The dice stream therefore doesn't depend on how many picks were random.
   - Consequence: explicit squads skip `Roster.DraftRandom`. The same seed gives different dice under ALL PICK than under RANDOM. That's expected.
   - Timeout picks depend on wall-clock timing, so a draft is reproducible only from the same picks, not from the seed alone.

## Increments

| #   | Increment             | What it delivers                                                                                                                                                                                                                                                                                                                                             |
| --- | --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| DR1 | **Core draft model**  | `Core/Draft/`: `DraftMode`, `DraftRefusal`, `DraftConfig`, `DraftState`. Slots per seat, picks and refusals, clearing (ALL PICK), one-step undo (SNAKE), random pick and fill from the draft RNG, the clock (`Tick`), and `Squads()` for `MatchFactory.Create`. EditMode tests. GDD §2.2 closed.                                                            |
| DR2 | **Draft screen**      | `MatchSettings.Drafted` replaced by a squad-mode enum (moved here from DR1: without the screen, ALL PICK has nowhere to go). `View/DraftScreen.cs` (full canvas): 3×3 roster cards (shape, name, role, health, speed, abilities with cost, range and cooldown; hover expands the descriptions), seat columns, clock, and the controls above. `OperatorCopy`. `AppScreen.Draft`. REMATCH keeps squads. The end screen shows each seat's squad. PRESENTATION §4.3. |

## Rules for this stage

- The core owns the rules **and the clock**. The view calls `Tick(Time.unscaledDeltaTime)` only while the draft screen is up and no card covers it. It computes nothing.
- The core has no Unity types and no LINQ.
- A refused pick returns a `DraftRefusal`; it never throws. Misuse, such as a seat that isn't in the draft or a negative tick, does throw.
- Every rule ships with EditMode tests.

## Log

- 2026-09-16 — **Stage opened.** Checked HEAD: `docs(plan): close GUI phase at J and plan the next five stages`. Found that J committed `TitleScreen.cs` and `ISettingsHost.cs` without their `.meta` files; handed over a fix commit.
- 2026-09-16 — **Decisions 1–9 settled** with the designer. The main change from the plan: the default mode is a free, simultaneous 30 s pick, and snake becomes a second mode with a 10 s clock per pick. The unique-picks toggle is dropped.
- 2026-09-16 — **DR1 built: the core draft model.**
  - New `Core/Draft/`: `DraftMode`, `DraftRefusal`, `DraftConfig` (30 s / 10 s / seed salt), `DraftState`.
  - The core owns the clock. `Tick(elapsed)` applies the timeout rules.
  - ALL PICK: the clock keeps running after every slot is full, so the table can still swap slots until START or zero. At zero, the empty slots are filled and the draft closes (`IsTimeUp`).
  - SNAKE: a complete draft has no clock and waits for START. UNDO restores the reverted pick's full 10 s.
  - Random picks record `WasRandom`, so the screen can badge them.
  - Operators are matched by name, and `Squads()` returns copies.
  - 41 new tests (`Draft/DraftStateTests.cs`). **475 passing.** View and sim compile. A mutation check (breaking the snake reversal and the in-squad duplicate rule) was caught by 3 tests.
  - `MatchSettings` is unchanged; the squad-mode enum moves to DR2.
  - GDD §2.2 closed: draft modes recorded, the open-question entry removed. Two stale lines fixed along the way: "four operators exist" became nine (Mimi incomplete), and the out-of-match flow is marked as built.
