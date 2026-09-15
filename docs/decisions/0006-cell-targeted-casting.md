# ADR-0006: Cell-Targeted Casting and Deferred Cell Effects

> Location in repo: `docs/decisions/0006-cell-targeted-casting.md`
> Status: **Accepted**
> Date: 2026-09-14 (decision, landed with Kian) · recorded 2026-09-15
> Related: ADR-0004 (core architecture), ADR-0007 (lingering zones), `docs/design/COMBAT_SYSTEMS.md` §4.4, §9.1, `docs/design/PRESENTATION.md` §1

> **Recorded after the fact.** The decision was taken and built on 2026-09-14, and the code has cited this number since then without a file behind it. This record was reconstructed from the remarks in `AbilityTargeting`, `EffectKind`, `DeferredCellEffects`, `TargetingRules.CanTargetCell` and `Kian.cs`. It adds no rationale the code does not already state.

## Context

Until Kian, every ability was aimed at an operator or at nothing. `AbilityDefinition.RequiresTarget` was a bool, and every effect resolved on the turn it was cast, against recipients the resolver already held.

Kian's third ability, **Drone Strike**, fits neither assumption. It paints a *cell*, which can be empty, and nothing happens until later. Whoever is standing there when it resolves takes the hit. The ability is a bet on where somebody will be, not a delayed hit on somebody chosen now. That is its whole design: remove either property and nothing is left.

Three questions had to be answered:

1. How does an ability declare that it takes a cell?
2. Where does a pending effect live, and on whose clock does it resolve?
3. Is this Drone Strike's private machinery, or a system?

## Decision

**Abilities can target a cell, and cell-anchored deferred effects are a core system, not one ability's special case.**

1. **Targeting is a three-state enum, `AbilityTargeting { Operator, None, Cell }`, replacing the bool.** `RequiresTarget` and `RequiresCell` survive as computed properties, so existing readers did not change. `UseAbilityCommand` carries an optional `TargetCell`.
2. **A legal cell is on the shared outer track and within range, measured along the track.** Occupancy is irrelevant: an empty cell is legal. Home columns and yards are excluded (§4.3). The safe-cell camping rule applies to cell aims unconditionally (§4.4, second amendment). `TargetingRules.CanTargetCell` owns this.
3. **The engine answers "which cells?"** `GameEngine.LegalCellsFor` is the cell twin of `LegalTargetsFor`, so the picker highlights exactly what a cast would accept (PRESENTATION §1).
4. **Pending effects live in `DeferredCellEffects`**, a service that stores each entry against its cell and its owner.
   - **It resolves on the owner's upkeep, one full round after placement,** so every opponent moves before it lands.
   - **It is keyed on cell *and* owner.** Two seats can hold effects on the same cell, and each resolves independently.
   - **Re-painting a cell you already hold replaces the old paint** rather than stacking, the same shape as re-applying a status.
   - **Turn indices are absolute,** as with status durations and cooldowns. Nothing decrements, so nothing drifts.
5. **A placed effect outlives its operator.** It resolves whether the operator that placed it is stunned, in a home column, home, or neutralized in its yard, and a kill still credits the recorded source.
6. **Placed effects are always visible.** `ActiveBeacons()` is a standing query, not an optional one. The placement event announces a beacon once; the query is how the board keeps showing it.
7. **Upkeep reports resolutions before neutralizations.** The beam has to land on screen before the piece it finished disappears. A beam that hits nobody still reports, so a missed bet is visible as a miss.
8. **The effect kind is `PaintCell`.** Its damage is a *total*, divided among the enemies caught within its radius. It is recorded with the `PrimaryTarget` scope, which the resolver ignores, because scopes resolve to operators and this effect has none when it is cast.

## Options considered

- **A second bool beside `RequiresTarget`.** Rejected: it allows "targets an operator and a cell", a combination that means nothing. Three states are three states.
- **Drone Strike's own machinery inside `AbilityResolver`.** Rejected in favour of a service, on the expectation that mines, zones and timed hazards follow. ADR-0007 was the first to follow, the same day.
- **Resolve on the victim's upkeep, as bleed and marks do.** Rejected. Bleed and marks are carried by the victim, so the victim's clock is natural. A beacon is a device its owner placed. The owner's clock is also the only arrangement that gives every seat the same warning, wherever it sits in the turn order.
- **Key pending effects on the cell alone.** Rejected: a player could wipe an opponent's spent energy by painting the same square.
- **Stacking repeated paints.** Rejected: two beams on one cell is a different mechanic, and nobody designed it.
- **Disarm the effect when its operator is neutralized.** Rejected: a device is not its operator, and letting a kill refund the energy spent on it makes the ability worse than it reads.
- **Let the view infer beacons from placement and resolution events.** Rejected, for the same reason `StatusRegistry.ActiveKinds` exists: inferred state drifts. An invisible delayed strike is a trap, not a prediction.

## Consequences

- **`DeferredCellEffects` resolves damage and statuses itself,** as `CollisionResolver` does. `StatusRegistry` only reports what a tick owes, because the damage pipeline consults the registry and a call back would close a cycle. Nothing consults this service, so it has no cycle to avoid.
- **The view gained a cell picker** (commit `8b6a8f2`): board clicks snap to the nearest legal cell, and a click anywhere else clears the choice. The panel-rect guard and, later, the EventSystem guard exist so that a click on a button cannot re-aim a strike (ADR-0008 consequence 4).
- **The effect vocabulary grew by one kind**, and `COMBAT_SYSTEMS` §9.1's "never an `if`" rule held: the resolver does not branch on Drone Strike.
- **Unlimited range needs no special case.** The largest possible track distance is half the circuit, so the unlimited-range sentinel is simply never exceeded. The distance is still reported.
- **Open, and a PRESENTATION §2 gap: the view does not draw beacons yet.** `GameEngine.ActiveBeacons()` exists, but nothing in the Unity assembly calls it (checked 2026-09-15). Decision 6 is therefore met in the core and not on screen. Until the view draws them, a beacon is visible only in the event log, which is the trap this record rejects. It does not affect the stranger test, which uses the alpha three.
- **Open: `ClearFor(owner)` has no caller.** It exists on the service, but nothing in the engine calls it. Whether a seat's devices should ever be cleared (for example, when its last operator gets home) is undecided.
