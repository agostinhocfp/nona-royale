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

|                               | Why                                                                                                                            |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Health, per operator          | Every combat decision is a threshold question                                                                                  |
| Active statuses, per operator | A status that cannot be seen may as well not apply                                                                             |
| Why health changed            | Upkeep damage lands in a phase where nothing else moves; a bar dropping with no cause is the board declining to explain itself |
| Whose turn, and their energy  | The pool is shared, so it is a squad-level decision                                                                            |
| Which operators are deployed  | Occupancy is the number the design keeps fighting for                                                                          |
| Safe cells                    | The only cells carrying a rule a player must see without asking                                                                |

**On demand, because showing it always would be noise:**

|                                | Trigger                                                        |
| ------------------------------ | -------------------------------------------------------------- |
| Where a move would land        | After rolling                                                  |
| Which cells an ability reaches | When an ability is selected                                    |
| Whether an ability can be cast | Always in the tray, from the engine, with the reason it cannot |
| What just happened, in words   | The event log, always present but scannable                    |

**Never shown:** the outcome of an action before it is committed. Landings are shown; whether a landing is contested is not. Showing a player the result of a fight before they choose it removes the choice.

**The hardest case is the one with no visible agent.** A bleed or mark tick resolves at upkeep, when nothing moves and nobody acted — so an operator loses health, and sometimes vanishes to its yard, with nothing on screen accounting for it. Every damage and neutralize event therefore carries its cause, and over-time damage is drawn differently from a hit. A collision or a cast needs no such label: the player watched it happen.

---

## 3. Placement is not animated as movement

Pieces walk the track cell by cell. **Pulls, swaps and bounce-backs do not.**

All three arrive as `OperatorMoved`, and all three are _placement_ rather than movement (COMBAT_SYSTEMS §7.4, §7.2) — none triggers anything along the way. Animating them as a walk would show a journey the rules say never happened, and would teach a player to expect collisions that cannot occur.

The implementation test is simple: a move reporting equal or lower progress is settled directly; only forward travel is walked.

**Walking one cell at a time is load-bearing, not decorative.** It is the only code anywhere that enumerates the cells between two progress values, which makes it the only thing that can expose a discontinuous layout — and it is how the arm-tip gap in the old 48-cell board was caught, after four amendments of simulation had missed it (ADR-0002 Amendment 6). Do not optimise it into a lerp.

**Open.** A bounce-back settles to the bounce cell without showing the contested landing first, because the event carries the final progress rather than the attempted one. The collision is legible in the log; the motion is not. Fixing it means the event carrying both.

---

## 4. Commitment needs a look first

Actions that spend a scarce resource get a **select, then commit** pattern rather than firing on one click.

Abilities: selecting highlights the cells the ability reaches; a second action casts. Reach turned out to be the binding constraint on the entire combat layer (ADR-0002 Amendment 4), so committing energy without seeing range was asking players to estimate the number the game is most sensitive to.

The tray also greys out what cannot be cast, with the reason — cooling down, not enough energy, stunned. That readiness comes from the engine, per §1; before it existed, a player learned an ability was on cooldown by pressing it and reading the rejection.

The cost is one extra click per ability use, in a match already running past its length budget. **Watch whether it drags.**

Deploy and move fire on one click. Neither spends energy, and both are already previewed.

---

## 5. Unknown state still draws

Anything that maps game state to a visual carries a default case.

The status palette gives an unrecognised `StatusKind` a grey badge. A status kind added to the core later appears on the board without anyone remembering to update the view — it just appears unlabelled rather than invisibly.

**A silently omitted status is a bug that looks like a working board.** An unfamiliar grey mark is a question a player asks out loud.

The same applies to operators. A silhouette is chosen by name with a fallback shape, so an operator added to the roster is drawn rather than dropped — but a fallback is not an identity, and any operator that reaches the pool wants its own shape before it reaches a player.

---

## 6. Prototype conventions

The current build exists to answer whether the game is fun, and everything in it is disposable.

- **No art assets, no prefabs.** Sprites are generated at runtime; the board, pieces and controls are built in code. One script on one empty GameObject.
- **`OnGUI` for controls.** Ugly and immediate. Replaced by a real UI when there is something worth dressing.
- **Silhouette carries identity.** Colour is taken by the seat, so operators are told apart by shape and size — the same constraint `ART_DIRECTION` §5 sets for the real art, arrived at early and crudely. A shape that is hard to read here is information for the art pass. Size tracks health against the frailest operator on the roster, not a round number, or the smallest piece stops reading as small.
- **The board is drawn from `PathMap`, never hard-coded.** `BoardLayout` is the only place that knows where a cell is; the core knows only that cell 14 follows cell 13. That separation is what let the board change from a ring to a cross without touching a rule or a test.
- **Drawability is enforced here, not in the core.** `BoardLayout` refuses a profile it cannot render as a continuous cross. The core tolerates any circuit divisible by four, because the rules do not care about arm geometry and nothing in the core should start caring — and the harness legitimately measures boards that will never be drawn. The cost of that separation is that an undrawable board can survive a long time in simulation, which is exactly what happened.

---

## 7. Open items

- **Bounce-back motion** (§3) — the attempted landing is not shown.
- **No lap indicator.** Not needed while only single-lap boards ship, but `BoardProfile.Laps` is implemented and any lapped board makes two operators on the same cell visually identical and positionally unrelated.
- **No indication of whose operator is whose beyond colour** at a glance across the table — fine for hot-seat, untested for anything else.
- **`OnGUI` does not scale with resolution.** The controls panel is a fixed pixel width, so the camera has to size and offset around it; that is handled, but the panel itself does not reflow. Fine on one machine, not a build.
- **Mimi draws as a fallback-adjacent shape with no art brief behind it** (§5). She is in the pool; the silhouette was chosen to be distinct, not to be right.
