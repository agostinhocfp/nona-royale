# Nona Royale — Conventions

> Location in repo: `CONVENTIONS.md`
> Status: Living doc. Lean by design — add rules when a real ambiguity forces one, not preemptively.
> Related: ADR-0004 (architecture this enforces), ADR-0001/0002/0003.

The one rule everything else serves: **rules live in the pure-C# core; Unity only renders and collects input.** If you're unsure where code goes, ask "does this decide what happens in the game, or just show it?" — deciding is core, showing is view.

## Assembly layout

- `NonaRoyale.Core` — pure C#. **References zero Unity types** (enforced by its asmdef). All state + rules.
- `NonaRoyale.Core.Tests` — EditMode unit tests for the core. No engine required.
- `NonaRoyale.Unity` — MonoBehaviours, rendering, input, audio, UI. References Core, never the reverse.
  A file that needs `using UnityEngine;` cannot be in Core. If you reach for that using inside Core, the code is in the wrong assembly.

## The core/view boundary

- View → Core: **Commands** (`RollDiceCommand`, `MoveCommand`, `UseAbilityCommand`, …). The core validates, mutates, returns **Events**.
- Core → View: **Events** (`OperatorMoved`, `EnergyChanged`, `OperatorCaptured`, …). The view subscribes and renders; it never mutates core state or reads private internals.
- No `MonoBehaviour` holds game-rule state. No core type knows a `Transform` exists.

## Dependencies

- **No singletons / no `.Instance`.** Dependencies are passed in (constructor for core types, serialized reference or injected for view types). One **composition root** wires everything at startup.
- Randomness enters the core through an **injected RNG (seedable)** — never `UnityEngine.Random` in Core, never `System.Random` created ad-hoc. Seeded RNG = reproducible tests and future online determinism.

## Config, not literals

- Board constants (ADR-0002: `CircuitLength = 48`, `HomeColumnLength = 6`, `PlayerStartOffset = 12`) and tunables (energy thresholds, ability costs) live as **named config data in the core**, not magic numbers inline. Changing 48→60 is a data edit.

## Naming (C# standard, no surprises)

- Types/methods/properties: `PascalCase`. Locals/params: `camelCase`. Private fields: `_camelCase`. Constants: `PascalCase`.
- Commands end in `Command`, events in past tense (`OperatorMoved`, not `MoveOperator`). Core services are noun-based (`MovementResolver`, `EnergyLedger`, `CaptureRules`).
- One public type per file; file name = type name.

## Testing

- Every core rule ships with EditMode tests. A rule without a test isn't done.
- Tests are **deterministic** — seed the RNG, assert on resulting state/events. No sleeps, no frame-waiting in core tests.
- Name tests by behavior: `DiceTotalOfNine_GrantsThreeEnergy`, `OperatorOnSafeCell_IsNotCaptured`, `AllThreeOperatorsHome_WinsGame`.
- `PlayMode` tests only for runtime concerns (input wiring, animation, rendering) — never for rules.

## Comments & docs

- Comment the **why**, not the what. A tricky rule (e.g. "below 50% HP" execute threshold) gets a one-line rationale; obvious code doesn't.
- When a file implements an ADR decision, reference it (`// per ADR-0003: counter-clockwise, outer lane only`).

## Git (lightweight, solo)

- Small, focused commits; present-tense summary (`add energy ledger`, `fix capture on safe cell`).
- Don't commit a rule change without its test in the same commit.
