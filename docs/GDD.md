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

**Two players is a different game, not a smaller one.** Combat scales with the number of _pairs_ of players, so a two-seat match is close to a pure race no matter how the rules are tuned (`COMBAT_SYSTEMS.md` §12). If 1v1 is meant to ship it needs its own design, not a seat count.

### 2.2 Draft

Each player fields **three operators**, chosen before the match from a pool of **nine**. All nine are in the roster. One is incomplete: Mimi has two of her three abilities, because Cryo Field is not written yet (`OPERATORS.md`).

**Each seat fields three distinct operators.** Two players may field the same operator; one player may not field the same operator twice. Global uniqueness — no two players sharing an operator — is _not_ the rule and cannot be: four seats at three operators each would need twelve, and the pool is nine.

**How picks are made is settled** (2026-09-16, `DRAFT.md`). Setup offers four squad modes:

- **All Pick** (default): one shared 30-second clock. Any seat picks at any time, in view of everyone. When the clock runs out, every empty slot is filled at random.
- **Snake**: turn order reversing each round (R, B, G, V, V, G, B, R, …), 10 seconds per pick; a pick that times out is made at random.
- **Random**: every squad is drawn at random.
- **Alpha Three**: every seat fields Bouncer, Syla and Kurbyn (the measurement squad).

Random picks draw from a draft RNG derived from the match seed, never from the match's own RNG.

### 2.3 Board

One of three configured profiles (ADR-0002). **Standard** is the shipping board; **Sprint** was built to iterate on combat rather than to be played; **Long** is retained for measurement only.

**Sprint's status is under review.** Measured against the corrected board it runs a denser fight per minute than Standard and sits inside the session-length budget that Standard now overruns. Whether that makes it a product rather than a tool is an open question, and a real one — see ADR-0002 Amendment 6.

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
| What the view must show, and what it may not compute      | `design/PRESENTATION.md`       |
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
- **Opt-out of home entry** — staying on the track to pursue a leader instead of turning into your home column. A good mechanic, explicitly post-MVP (ADR-0003), designed with its open questions in `design/_HANDOFF_opt_out_home_entry.md`.
- **A reward for neutralizing.** Killing an opponent currently pays the attacker nothing, which at four players makes it a public good bought with private resources. Designed, not adopted — `design/_HANDOFF_neutralize_rewards.md`.
- **Mandatory and split movement** — using the whole roll, and splitting it between two operators. Both decided in principle, neither built; `design/_HANDOFF_split_movement.md`.
- **Online multiplayer.** The seam exists; the netcode does not.
- **Progression, cosmetics, meta systems.** Nothing designed, nothing assumed.

---

## 7. Open questions

Small, and none of them block implementation.

- 2v2 team semantics — what "ally" means across two players.
- Whether Sprint is exposed to players or stays a development tool (§2.3). Measurement has made this a live question rather than a rhetorical one.
- Whether a session that runs past the length budget is a problem or simply what this game is. ADR-0002 Amendment 6.
- ~~Out-of-match flow: main menu, match creation, post-match screen.~~ Designed and built in the GUI phase (`GUI_PHASE.md`, increments H–J).
