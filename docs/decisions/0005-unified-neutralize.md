# ADR-0005: Unified Neutralize Model — Capture Is Damage

> Location in repo: `docs/decisions/0005-unified-neutralize.md`
> Status: **Accepted**
> Date: 2026-09-11
> Related: ADR-0002 (board size), ADR-0003 (board topology), ADR-0004 (core architecture), `docs/design/COMBAT_SYSTEMS.md`, `docs/design/OPERATORS.md`

## Context

The design carried **two unrelated death systems** and had never reconciled them.

- **Ludo capture** (ADR-0003, inherited 1:1 from the reference board): land on an enemy, it goes home. Instant, unconditional, indifferent to the target.
- **HP and damage** (`OPERATORS.md`): operators have 3–12 HP and abilities that chip it away.

Neither doc defined how they coexisted, and the ambiguity blocked everything downstream. "Atomic damage pierces some defense" cannot be specified until _defense_ has a meaning, and defense has no meaning while a lucky roll erases a 12 HP tank as cheaply as a 6 HP assassin. `Shield` (ADR-0003's special space) was written as "requires 2 hits to capture instead of 1" — a capture-layer rule — while `Evasion` (Kurbyn's passive) was a damage-layer rule, and nothing connected them.

Two project constraints bear directly on the resolution:

- **The stated fun priority is 70% combat / 30% race** (ADR-0002). A model where positioning always beats combat inverts that.
- **ADR-0003 fixes the Ludo track 1:1 for _geometry_ only**, and explicitly reserves the layer above it for this project's originality. Changing what happens when two operators meet is inside that reservation; it costs nothing geometrically and requires no change to `PathManager`.

## Decision

**There is exactly one way an operator leaves the board: it is _neutralized_.** HP is the single currency in the game.

Landing on an enemy is an **attack delivered by movement**, not an execution. It goes through the same damage pipeline as an ability:

1. The mover deals `CollisionDamage` (3), type **Normal**, to the occupant.
2. If that neutralizes the occupant, it returns to the yard and the mover takes the cell.
3. If the occupant survives — including via evasion or shield — **it holds the cell and the mover is bounced back one step.**

Collision is one-directional (the mover takes nothing), always 1v1 (contested cells never hold more than one enemy), and never occurs on a safe cell, in a home column, against a friendly operator, or as a result of forced movement.

Neutralize is a **setback, not a removal**: the operator returns to its yard at full HP, with all statuses cleared and all track progress lost, and re-enters on a 6 like any other deployment. There is **no player elimination** in the MVP; the GDD's "until only one player is left" line is an artifact of an early pass and is not a mechanic.

Full rules, including the damage pipeline order and every consequent status interaction, live in `COMBAT_SYSTEMS.md`. This ADR records _why_.

## Rationale

- **It makes HP mean something.** Bouncer's 12 HP now buys him a role — he holds contested cells and survives being run into four times, while a 6 HP assassin dies to a collision plus a scratch. Under instant capture, the stat was decorative.
- **It turns cells into contested territory.** A failed collision leaves both operators adjacent and alive, which is exactly the pressure that makes abilities get used. That is the 70/30 priority expressed as a rule rather than an aspiration.
- **It collapses two systems into one.** Shield, Evasion, Atomic pierce, and "neutralize vs capture" all become answerable, because they are all questions about one pipeline. The alternative was maintaining two sets of mitigation rules that interact.
- **It survives the architecture.** One `DamagePipeline` in the core is the single choke point for every damage source; `CollisionResolver` calls into it exactly as `AbilityResolver` does. Under ADR-0004 that is one testable service rather than two parallel ones that will eventually disagree — the same failure mode as the duplicated energy formula that motivated ADR-0004.

## Alternatives considered

- **Capture is king** (landing neutralizes outright; HP gates abilities only). Rejected: positioning always beats combat, HP is decorative on the tank, and it contradicts the stated fun priority. It is Ludo with a combat minigame bolted on.
- **Hybrid** (landing captures unless the target is above an HP threshold). Rejected: two rules where one will do, and a threshold no player can reason about mid-turn.
- **Collision damage of 4.** Considered, deferred. 3 was chosen to start low and raise under measurement.

## Consequences

- **`CollisionDamage = 3` makes the race layer close to non-lethal on its own.** Nothing on the alpha roster dies to a single collision. Collision is a _softening_ mechanic that sets up ability kills. This is intended; if playtest reads as toothless, `CollisionDamage` is the first dial and ability costs are the last.
- **Safe cells now mean "safe from collision" and nothing more.** Abilities still reach an operator standing on S. If safe cells also blocked abilities they would become free parking and the combat layer would stall there.
- **Shield is rewritten** as "absorbs one instance of Normal damage" (`COMBAT_SYSTEMS.md` §5.6). The capture system its old wording described no longer exists. Special spaces remain deferred; the rule is defined so the space is buildable when it lands.
- **`Checkpoint`** (ADR-0003's deferred special space) directly contradicts "returns to yard" and will need an explicit exception when special spaces are implemented.
- **The yard setback is now the most expensive single rule in the game.** Simulation at 4 players on the Standard board measures it at **6.4 turns per match** — more than board length or damage numbers contribute per unit. Softening it (return to start cell: 3.0 turns; half progress: 1.7 turns) is the first lever if matches run long or losing feels unrecoverable. See ADR-0002 Amendment 2.
- **Board occupancy is low.** All three of a player's operators are on the board together only 10–15% of turns, because deploy friction and the yard setback compound. For a combat-first game this is the most concerning measured figure, and it is a consequence of this decision combined with ADR-0003's deploy-on-6 rule — not of either alone.

## Status history

- 2026-09-11 — Accepted. Capture and damage unified into a single neutralize model; collision defined as a Normal-damage attack with bounce-back; player elimination ruled out of the MVP.
