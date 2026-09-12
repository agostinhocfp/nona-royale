# ADR-0004: Core Architecture — Pure-C# Core, Unity as Thin View

> Location in repo: `docs/decisions/0004-core-architecture.md`
> Status: **Accepted**
> Date: 2026-07-10
> Related: `CONVENTIONS.md`, ADR-0003 (board topology → `PathManager`), the existing `GameManager`/`PathManager`/`OperatorManager`/`PlayerEnergyManager` singletons (to be replaced), `docs/design/OPERATORS.md` (combat rules live in the core)

## Context

The original codebase had five `MonoBehaviour` singletons (`GameManager`, `PathManager`, `GridManager`, `OperatorManager`, `PlayerEnergyManager`) calling each other's `.Instance` directly. Consequences observed:

- **Untestable rules.** Energy math, movement, capture, and win conditions were tangled into `MonoBehaviour`s, so verifying them needs the Unity runtime. Example of the cost: the energy formula was duplicated in `PlayerEnergyManager` _and_ `GameManager` and the two **disagreed** (`sum <= 8` vs `sum < 9`) — exactly the class of bug a unit test catches in milliseconds and a runtime-only setup hides.
- **Hidden coupling & ordering.** `.Instance` access creates implicit initialization-order dependencies and makes any unit impossible to exercise in isolation.
- **No seam for online.** The stated goal is online multiplayer (hot-seat first, for iteration). A `MonoBehaviour`-centric design has nowhere clean to insert a network boundary later.

This project is a solo effort by a full-stack developer who values testing. The game is rules-heavy (dice, energy, abilities, capture, win conditions) — precisely the kind of logic that benefits most from being testable in isolation.

## Decision

**A pure-C# game core; Unity `MonoBehaviour`s are a thin presentation/input layer.**

### The core (pure C#)

- Lives in its own assembly (`NonaRoyale.Core`) via an **assembly definition that references _zero_ Unity types**. It cannot compile against `UnityEngine`. This is a hard wall, not a guideline — it's what guarantees testability and keeps rules engine-independent.
- Holds **all game state and all rules**: board/path model (`PathManager` logic per ADR-0003), turn state machine, dice→energy, movement, ability resolution, capture, status effects, win conditions.
- Is **deterministic**: given the same state + same command, it produces the same next state. Randomness (dice) enters through an injected RNG seed, so games are reproducible in tests.

### The boundary — commands in, events out

- The view sends **Commands** to the core (`RollDiceCommand`, `SelectOperatorCommand`, `MoveCommand`, `UseAbilityCommand`, `EndTurnCommand`). The core **validates** each against current state, mutates state if legal, and returns **Events** describing what happened (`DiceRolled`, `EnergyChanged`, `OperatorMoved`, `OperatorCaptured`, `TurnEnded`, `GameWon`).
- The view never mutates game state directly and never reads private core internals — it reacts to events. This is the same event instinct already in the old code (`OnEnergyChanged`, etc.), promoted to the whole architecture.

### The view (Unity)

- `MonoBehaviour`s render state and translate player input into Commands. They own `Transform`s, `SpriteRenderer`s (2D per ADR-0001), animation, audio, UI — **nothing rules-related**.
- Dependencies are **passed in**, not fetched from globals. No `.Instance` singletons in the new design; a single composition root wires the core to the view at startup.

## Why this shape serves hot-seat → online

- **Hot-seat now:** the local view sends everyone's commands to the one core instance. Simplest possible setup for gameplay iteration.
- **Online later, no rewrite:** commands are serializable messages and the core is deterministic. Whether a command arrives from a local input or a network socket is the _view's_ concern. The seam already exists — online becomes a transport swap plus authority/validation on a server-side core, not a re-architecture. (Full netcode is its own future ADR; this ADR only guarantees we're not blocked.)

## Testability rule (hard)

- The core assembly references **zero** Unity types; enforced by the asmdef.
- Every rule ships with **EditMode unit tests** that run without the engine: e.g. "dice total 9 → 3 energy", "operator on safe cell is not captured", "all three operators home → win". These run in milliseconds and are the regression net for fearless refactoring.
- `PlayMode` tests are reserved for genuinely runtime behavior (input, rendering, animation), not rules.

## Consequences

- **Replaces the singletons.** `GameManager`/`PathManager`/`OperatorManager`/`PlayerEnergyManager` are re-expressed as core services + thin view components. The `MeshRenderer` fallback is already dead (ADR-0001).
- **Single source of truth for rules.** The duplicated/desynced energy formula collapses to one place in the core. Config constants (ADR-0002: `CircuitLength`, `HomeColumnLength`, etc.) live in the core as data, not literals.
- **Assembly layout:** `NonaRoyale.Core` (pure C#), `NonaRoyale.Core.Tests` (EditMode), `NonaRoyale.Unity` (MonoBehaviours/view). See `CONVENTIONS.md`.
- **Cost:** more upfront structure than "script on a GameObject." Accepted deliberately — the stated bar is "extremely well done, polished, runs well," and a tested core is how a solo dev hits it without the game becoming unmaintainable.
- Combat/ability rules (from `OPERATORS.md`) and the still-undefined mechanics (Atomic pierce, Stun, Stealth, Evasion, Energy Efficiency) are **core** logic and get defined in `COMBAT_SYSTEMS.md` against this architecture.

---

## Amendment (2026-09-12) — the boundary governs reads, not just writes

### Context

This ADR established that rules live in the core and the view renders state. That framing implicitly covered **writes**: the view must not mutate state, and the command/event boundary enforces it — the view has no handle on a service and could not mutate anything if it tried.

It said nothing about **reads the view performs by deriving rather than asking.**

The gap surfaced building the move preview. The view needs to show where an operator would land. It already receives the dice roll as an event and can read `BaseSpeedMultiplier` off an operator, so the multiplication is three lines in the view and needs no core change.

That version is wrong, and wrong in the way that matters least visibly: distance also depends on status modifiers and enemy auras, neither of which the view can see. The preview would agree with the rules in the ordinary case and **disagree exactly when a slow or an aura is in play** — the moment a player most relies on it.

Status badges were the same shape. `StatusApplied` and `StatusExpired` events could sustain a badge layer in the view with no core change — but passives are granted at match start without an event, and bleed stacks are consumed at upkeep without a `StatusExpired`, so the inferred layer drifts.

Neither would have been caught by a test. Both would have been caught by a player, eventually, as "the game lied to me."

### Decision

**The single-source rule applies to derived values, not only to state changes.**

> If a number appears on screen and the core can produce it, the core produces it. The view displays; it does not calculate.

Concretely, a derived value the view needs becomes a **read-only query on `GameEngine`** that shares the code path of the rule it previews, rather than a calculation in the view that mirrors it.

Two such queries exist:

- `PreviewLandings()` — shares `Move`'s arithmetic rather than restating it.
- `ActiveStatusesOn(op)` — surfaces `StatusRegistry.ActiveKinds`.

### Why

A derived value in the view is a **second implementation of a rule**, subject to the same failure this ADR was written about. The old codebase had the energy formula in two files with different thresholds; a preview that computes its own distance is that bug with a nicer excuse, because "it is only for display" is true right up until a player makes a decision on it.

Read-only queries cost the core almost nothing. They add no state, cannot mutate, and are covered by the tests already exercising the path they share.

### Consequences

- The view's public surface on `GameEngine` will grow as presentation gets richer. That is correct: each addition is a rule the view stopped guessing at.
- A query that does **not** share an existing code path is a warning sign — it means the rule it previews does not exist yet, and writing it in the query would put a rule in the wrong place.
- The view layer's own rules now live in `docs/design/PRESENTATION.md`, which this amendment establishes as their owner. Before it, view decisions had no home, which is how the derive-it-locally version nearly shipped.

---

## Alternatives considered

- **Keep MonoBehaviour-centric, just de-singleton (DI the managers).** Less churn, but rules stay entangled with Unity and remain slow/awkward to test. Rejected: doesn't fix the core problem (testability), only the symptom (globals).
- **Full ECS (DOTS).** Overkill for a turn-based board game; steep learning curve for a solo dev; wrong tool for this problem. Rejected.

## Status history

- 2026-09-12 — Amended. The single-source rule extended to derived values: the view displays numbers the core produces rather than recomputing them. `docs/design/PRESENTATION.md` established as the owner of view-layer rules.
- 2026-07-10 — Accepted. Pure-C# core + thin Unity view + command/event boundary; hard testability wall via asmdef; hot-seat now, online-capable seam preserved.
