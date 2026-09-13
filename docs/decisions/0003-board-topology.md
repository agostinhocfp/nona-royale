# ADR-0003: Board Topology — Standard Ludo Track (52 outer + 24 home)

> Location in repo: `docs/decisions/0003-board-topology.md`
> Status: **Accepted** · Amended 2026-09-12 (sizes corrected to the drawable cross; ADR-0002 Amendment 6)
> Date: 2026-07-10
> Related: ADR-0002 (board size, amended alongside this), `docs/GDD.md`, `PathMap.cs`, `BoardLayout.cs`, `docs/art/ART_PIPELINE.md` (board art must match this geometry)

## Context

The board geometry existed only as scattered, partly-contradictory hints in code and the GDD (76 vs 96 spaces; `SpriteRenderer`/`MeshRenderer` hedging; safe/spawn logic that didn't match any coherent layout). Before writing a line of the rebuilt path model, the full topology was worked out from scratch against a reference Parcheesi/Ludo board and hand-marked corrections. This ADR is the canonical record of that model so no future work re-derives (and re-drifts) it.

**The board is a standard Ludo/Parcheesi track, 1:1.** The project's originality lives in the _mechanics layered on top_ (operator combat, abilities, energy, special spaces), not in the board movement itself. When any question about movement geometry arises, the answer is "whatever standard Ludo does," scaled to the sizes below.

## Decision — the canonical model

### Shape

- A four-arm **cross**. Four corner **yards** (circular holding areas) hold each player's operators before deployment.
- Each arm is **three lanes wide**: two outer travel lanes and a colored **center lane** that is that color's home column.
- A single centered **HOME goal** sits where the four home columns meet.
- The four **inner corners are never track.** They belong to the central HOME area, in this board and in classic Ludo alike.

### Sizes (canonical constants — ADR-0002 Amendment 6)

The loop threads each arm as two flanking lanes of length L plus the tip cell of the centre lane it must cross. Four arms, so the family is:

> `CircuitLength = 8L + 4` · `HomeColumnLength = L` · `GridSize = 2L + 3`

- **Outer track: 52 cells**, forming one continuous single-file loop.
- **Home column: 6 cells per color**, the arm's colored center lane, running inward to HOME.
- **Total path positions: 52 + (4 × 6) = 76.**
- **Grid: 15×15** — classic Ludo, and L = 6.

**A circuit outside that family cannot be drawn as a continuous cross.** 48 was the adopted size for four amendments and implies an arm of 5.5; see the amendment below.

### Movement

- **Deploy:** roll a **6** to move one operator from its yard onto its **start (S) cell**. Each deploy consumes one die, so a **double 6 deploys two** operators.
- **Direction:** travel is **counter-clockwise** around the outer track.
- **Track threading (1:1 Ludo):** per arm — outward along one flanking lane, across the centre-lane tip, back inward along the other lane, then a diagonal step past the inner corner to the next arm.
- **Home entry:** when an operator returns to **its own** arm, it turns off the outer track into **its own colored home column** at the home mouth. Entry is **automatic** in the MVP.
- **Finish:** up the home column to HOME. All of a player's operators reaching HOME = win (`COMBAT_SYSTEMS.md` §8).

### Safe spots — two per color

1. **Start / track safe (S):** the deploy cell, on an outer travel lane. It is safe.
2. **Home-entry safe (H):** the **first cell of the home column**. Redundant in practice — `COMBAT_SYSTEMS` §4.3 puts the whole home column out of the fight — but retained as the geometric rule.

- **Corner cells exist as track but are never safe.**

### Placement (rotational rule)

- The **H** cell is the first home-column cell for each color.
- The **S** cell sits at a fixed offset from that color's home mouth, rotated identically for all four colors. Encode it as a **derived offset from the home mouth**, not four independent literals, so it stays consistent if sizes change.
- Start cells are offset **13 apart** around the 52-loop (one quarter per color).

## Future mechanics (noted, NOT in MVP)

- **Opt-out of home entry:** a player may choose _not_ to turn into home, staying on the track to pursue a leading opponent — enabling swing plays. Explicitly deferred; MVP uses automatic entry. Designed, with its open questions, in `docs/design/_HANDOFF_opt_out_home_entry.md`.
- Special/rare spaces, abilities, energy-gated combat, status effects — layered on top of this track later. None change the base geometry above.

## Consequences

- `PathMap` encodes one canonical 76-position path (52 outer + 24 home) with per-color start offsets and the two safe-cell rules. Sizes are **config constants**, not literals.
- **The core holds no geometry.** `PathMap` knows that cell 14 follows cell 13 and nothing about where either one is; everything spatial lives in `BoardLayout` (ADR-0004). That is why the board could be drawn as a ring first and a cross later without touching a rule or a test — and it is also why an undrawable board survived four amendments of simulation. **Drawability is enforced where the board is drawn, not in the core.**
- Board **art must match this geometry** (`ART_PIPELINE.md`): 3-lane arms, center home columns, corner yards, centered HOME.
- The old 76/96 numbers are dead as a _circuit_ count. Note the coincidence: total path positions are now 76, which is the number the original GDD carried for a different quantity entirely. Do not read one as the other.

---

## Amendment (2026-09-12) — the sizes were undrawable

Everything above the sizes is unchanged. The geometry model was right from the first day; the numbers attached to it were not.

**48 outer cells cannot form a continuous four-arm cross.** The family is `8L + 4` — 28, 36, 44, 52, 60 — and 48 implies an arm of 5.5. `BoardLayout` had been absorbing the mismatch by handing each arm's tip cell to the home column and letting the loop hop over it, which put four visible two-cell gaps in the track and left every home column one cell short of HOME.

Corrected here:

|                      | Was   | Now                                    |
| -------------------- | ----- | -------------------------------------- |
| Outer track          | 48    | **52**                                 |
| Total path positions | 72    | **76**                                 |
| Start offset         | 12    | **13**                                 |
| Grid                 | 15×15 | 15×15 (unchanged — 48 never fitted it) |

The home column stays at 6 and the grid stays at 15×15, which is the tell: **52/6 on 15×15 is classic Ludo**, and this ADR's first line has always said the board is standard Ludo 1:1. The sizes were the one part that had drifted away from it.

The threading description is also corrected. It previously read "down each arm's inner lane, around the arm tip, and back up its outer lane," which describes a loop that never crosses the centre lane — and the tip cell of the centre lane is precisely what makes the arithmetic work.

Rationale, measurements and the fallback board are in **ADR-0002 Amendment 6**.

## Status history

- 2026-09-12 — Amended. Outer track 48 → 52, total positions 72 → 76, start offset 12 → 13, and the drawable-cross family (`8L + 4`) recorded as the constraint. The threading description corrected to cross the centre-lane tip. No change to the shape, the safe-cell rules, deploy, home entry, or the placement rule.
- 2026-07-10 — Accepted. Full model derived and confirmed cell-by-cell against a marked-up reference board.
