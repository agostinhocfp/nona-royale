# Nona Royale — Presentation

> Location in repo: `docs/design/PRESENTATION.md`
> Status: **Living.** The view layer's rules — what it may do, what it must show, and how it behaves.
> Related: ADR-0004 (core architecture), `docs/design/COMBAT_SYSTEMS.md` (the rules being displayed), `docs/art/ART_DIRECTION.md` (what it looks like), `docs/art/ART_PIPELINE.md` (how art is made)

## Why this document exists

`COMBAT_SYSTEMS` owns the rules. `ART_DIRECTION` owns the look. `ART_PIPELINE` owns production. **Nothing owned what the view is allowed to do**, and view decisions were accumulating with nowhere to live.

That gap is how "it's only a preview, just compute it here" gets in. This document closes it.

It is not about aesthetics. It is about the constraints that keep the presentation layer honest about a game whose rules live somewhere else.

---

## 1. The view computes nothing

**It renders state and sends commands. That is the whole contract** (ADR-0004).

The dangerous case is not writing state — that boundary is obvious and enforced by the engine's API. It is the view _deriving_ a value the core could have told it.

> **Rule: if a number appears on screen and the core can produce it, the core produces it.**

A worked example, because it nearly went the other way. The move preview needs to know where an operator would land. The view already knows the dice roll and the operator's base speed, so it could do the multiplication itself — no core change, less code.

It would also be wrong, because distance depends on status modifiers and enemy auras the view cannot see. The preview would disagree with the rules **exactly when a slow or an aura is in play**, which is the moment a player most relies on it. So `GameEngine.PreviewLandings()` shares `Move`'s arithmetic and the view displays what it returns.

Same reasoning for status badges. The view receives `StatusApplied` and `StatusExpired` and could maintain badge state from them with no core change. But passives are granted at match start without an event, and bleed stacks are consumed at upkeep without a `StatusExpired` — so an inferred badge layer drifts. `StatusRegistry.ActiveKinds` exists instead.

**A board that lies is worse than a board that shows less.**

---

## 2. What must be visible, and when

**Always, without asking:**

|                               | Why                                                               |
| ----------------------------- | ----------------------------------------------------------------- |
| Health, per operator          | Every combat decision is a threshold question                     |
| Active statuses, per operator | Six statuses exist; one that cannot be seen may as well not apply |
| Whose turn, and their energy  | The pool is shared, so it is a squad-level decision               |
| Which operators are deployed  | Occupancy is the number the design keeps fighting for             |
| Safe cells                    | The only cells carrying a rule a player must see without asking   |

**On demand, because showing it always would be noise:**

|                                | Trigger                                     |
| ------------------------------ | ------------------------------------------- |
| Where a move would land        | After rolling                               |
| Which cells an ability reaches | When an ability is selected                 |
| What just happened, in words   | The event log, always present but scannable |

**Never shown:** the outcome of an action before it is committed. Landings are shown; whether a landing is contested is not. Showing a player the result of a fight before they choose it removes the choice.

---

## 3. Placement is not animated as movement

Pieces walk the track cell by cell. **Pulls and bounce-backs do not.**

Both arrive as `OperatorMoved`, and both are _placement_ rather than movement (COMBAT_SYSTEMS §7.4, §7.2) — neither triggers anything along the way. Animating them as a walk would show a journey the rules say never happened, and would teach a player to expect collisions that cannot occur.

The implementation test is simple: a move reporting equal or lower progress is settled directly; only forward travel is walked.

**Open.** A bounce-back settles to the bounce cell without showing the contested landing first, because the event carries the final progress rather than the attempted one. The collision is legible in the log; the motion is not. Fixing it means the event carrying both.

---

## 4. Commitment needs a look first

Actions that spend a scarce resource get a **select, then commit** pattern rather than firing on one click.

Abilities: selecting highlights the cells the ability reaches; a second action casts. Reach turned out to be the binding constraint on the entire combat layer (ADR-0002 Amendment 4), so committing energy without seeing range was asking players to estimate the number the game is most sensitive to.

The cost is one extra click per ability use, in a match already running ~19.6 turns. **Watch whether it drags.**

Deploy and move fire on one click. Neither spends energy, and both are already previewed.

---

## 5. Unknown state still draws

Anything that maps game state to a visual carries a default case.

The status palette gives an unrecognised `StatusKind` a grey badge. A status kind added to the core later appears on the board without anyone remembering to update the view — it just appears unlabelled rather than invisibly.

**A silently omitted status is a bug that looks like a working board.** An unfamiliar grey mark is a question a player asks out loud.

---

## 6. Prototype conventions

The current build exists to answer whether the game is fun, and everything in it is disposable.

- **No art assets, no prefabs.** Sprites are generated at runtime; the board, pieces and controls are built in code. One script on one empty GameObject.
- **`OnGUI` for controls.** Ugly and immediate. Replaced by a real UI when there is something worth dressing.
- **Silhouette carries identity.** Colour is taken by the seat, so operators are told apart by shape and size — the same constraint `ART_DIRECTION` §5 sets for the real art, arrived at early and crudely. A shape that is hard to read here is information for the art pass.
- **The board is drawn from `PathMap`, never hard-coded.** `BoardLayout` is the only place that knows where a cell is; the core knows only that cell 14 follows cell 13. That separation is what let the board change from a ring to a cross without touching a rule or a test.

---

## 7. Open items

- **Bounce-back motion** (§3) — the attempted landing is not shown.
- **A mark payout resolved at upkeep changes state without announcing it.** `TurnStateMachine` applies the neutralize itself, so `GameEngine.BeginTurn` reports rather than resolves, and the hastened allies are never emitted as `StatusApplied`. The badges appear on the next refresh with no event explaining them. _(Formerly decision-log D-010, which is abandoned.)_
- **No lap indicator.** Not needed while only single-lap boards ship, but `BoardProfile.Laps` is implemented and any lapped board makes two operators on the same cell visually identical and positionally unrelated.
- **No indication of whose operator is whose beyond colour** at a glance across the table — fine for hot-seat, untested for anything else.
- **`OnGUI` does not scale with resolution.** Fine on one machine, not a build.
