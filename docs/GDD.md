# Nona Royale — Game Design Document

> Location in repo: `docs/GDD.md`
> Status: **Living.** Replaces `GDD_Core_Systems.md`, `GDD_GameplayMechanics.md` and `GDD_OperatorProfiles.md`, all of which are deleted.
> Related: everything. This doc's job is to point at the right one.

## What this document is, and what it deliberately is not

The previous GDD tried to describe the whole game in one place. It ended up carrying a board size that disagreed with the code (76 vs 96 vs 48), an energy economy that could not afford its own ultimates, a win condition that contradicted the actual one, and a player-elimination rule that was never a mechanic. None of that was malice — it is what happens when one document restates what another document owns.

**So this GDD owns very little on purpose.** It covers match setup, modes, and the shape of a session. Everything else is a pointer.

**There are no numeric values anywhere in this document.** If you need a number, it lives in the doc that owns it. Adding one here is how the last GDD died.

---

## 1. The game

See `PROJECT_IDENTITY.md`. One line: a competitive digital board game where each player commands three operators on a Ludo/Parcheesi track, using dice-driven movement and energy-gated tech abilities to fight, scheme, and race home.

The board is standard Ludo geometry. **The originality is the combat layer built on top of it.**

---

## 2. Match setup

### 2.1 Players

2–4 players. Local hot-seat for the MVP; the architecture preserves a clean seam for online later (ADR-0004), but netcode is out of scope and gets its own ADR.

### 2.2 Draft

Each player fields **three operators**, chosen before the match from a pool of **nine**. Three are built for the alpha (`OPERATORS.md`); the remaining six are future work.

Draft rules — whether picks are simultaneous or in turn order, and whether two players may field the same operator — are **open**. For the alpha, assume simultaneous selection with duplicates allowed; it is the simplest thing that works and nothing depends on it yet.

### 2.3 Board

One of three configured profiles (ADR-0002). **Standard** is the shipping board; **Sprint** exists to iterate on combat and is not meant to be played as a product; **Long** is retained for measurement only.

### 2.4 Modes

- **Free-for-all** — every player for themselves.
- **2v2** — two teams of two. Team rules (shared energy? allied targeting across partners?) are **open and unspecified**. `COMBAT_SYSTEMS.md` defines "ally" as an operator you own; extending that to a partner's operators is a real design question, not a rename.
- **Ranked / competitive** — post-launch, explicitly not MVP.

---

## 3. A turn, at a glance

Upkeep → roll → act → end. The authoritative phase order, what resolves in each phase, and every interaction between them is `COMBAT_SYSTEMS.md` §6. It is not repeated here.

---

## 4. Winning

A player wins when all three of their operators reach HOME. `COMBAT_SYSTEMS.md` §8.

There is **no player elimination**. Neutralize is a setback, not a removal (ADR-0005). The old GDD's "play continues until only one player is left" was an artifact of an early pass and is not a mechanic.

---

## 5. Where everything actually lives

| You want to know…                                         | Read                           |
| --------------------------------------------------------- | ------------------------------ |
| What the game is, and for whom                            | `PROJECT_IDENTITY.md`          |
| How big the board is and why                              | ADR-0002                       |
| How the track is laid out, deploy, safe cells, home entry | ADR-0003                       |
| How damage, energy, status, targeting and collision work  | `design/COMBAT_SYSTEMS.md`     |
| Operator stats, ability costs, ranges, effects            | `design/COMBAT_SYSTEMS.md` §10 |
| Who the operators are — theme, archetype, personality     | `design/OPERATORS.md`          |
| Why capture became damage                                 | ADR-0005                       |
| How the code is organised, and where a given file goes    | ADR-0004, `CONVENTIONS.md`     |
| Why the game is 2D                                        | ADR-0001                       |
| What it should look like                                  | `art/ART_DIRECTION.md`         |
| How art actually gets produced                            | `art/ART_PIPELINE.md`          |

---

## 6. Out of scope for the MVP

Recorded so they are not silently forgotten, and so nobody mistakes them for planned work.

- **Special spaces.** Deferred (ADR-0003). `Shield` has a defined rule so the space is buildable when it lands; Teleport, Slippery, Checkpoint, RollAgain and SharksTable do not. Checkpoint conflicts with the neutralize rule and needs an explicit exception.
- **A card / gadget / trap deck.** An early design proposed a small deck of consumables layered over the board. It was never scoped, never costed against the energy economy, and nothing in the current rules references it. Treat it as an idea, not a feature.
- **Opt-out of home entry** — staying on the track to pursue a leader instead of turning into your home column. A good mechanic, explicitly post-MVP (ADR-0003).
- **Online multiplayer.** The seam exists; the netcode does not.
- **Progression, cosmetics, meta systems.** Nothing designed, nothing assumed.

---

## 7. Open questions

Small, and none of them block implementation.

- Draft rules (simultaneous vs. turn order; duplicates allowed?).
- 2v2 team semantics — what "ally" means across two players.
- Whether Sprint is ever exposed to players or stays a development tool.
- Out-of-match flow: main menu, match creation, post-match screen. Undesigned.
