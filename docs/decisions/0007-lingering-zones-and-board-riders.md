# ADR-0007: Lingering Zones and Board-Reading Riders

> Location in repo: `docs/decisions/0007-lingering-zones-and-board-riders.md`
> Status: **Accepted**, with one open design choice (see the end) · **Amendment 1, 2026-09-17** (crowd zones)
> Date: 2026-09-14 (decision, landed with Nuetu) · recorded 2026-09-15
> Related: ADR-0006 (cell-targeted casting), `docs/design/COMBAT_SYSTEMS.md` §5.1, §9.1

> **Recorded after the fact.** The code cites this number as "the recurring upkeep billing rider", and until now no file stood behind it. This record was reconstructed from the remarks in `EffectKind.DeployZone`, `AbilityEffect.DeployZone` and `AbilityEffect.BonusInOwnZone`, `DeferredCellEffects` and `Nuetu.cs`. It adds no rationale the code does not already state.

## Context

ADR-0006 gave the core cell-anchored effects that **fire once and are deleted**. Nuetu's **Killzone** needed two things that did not exist:

1. **An effect that holds its ground.** The grenade goes off a round after it is placed, and the ground stays hostile afterwards: it bills whoever is standing there on further turns of its owner.
2. **An ability whose numbers depend on the board.** Bio-Link Rage heals more while one of Nuetu's zones is live. Syla's `BonusIfBleeding` is the nearest precedent, and it reads a status on the effect's own recipient. This reads the board, from inside another ability.

## Decision

**Lingering zones are a second shape of the same deferred cell entry, and an effect may carry a board-reading bonus.**

1. **One entry type, different settings.** A zone is a `DeferredCellEffects` entry with a lifetime of several owner turns, a detonation payload, a lingering payload and an optional status. A beacon is the same entry with one resolution and a split payload. The two share everything that is hard: the anchoring, the owner-relative clock, the area query and the kill plumbing.
2. **The effect kind is `DeployZone`.** It detonates at the owner's next upkeep, then bills again for a set number of the owner's turns, each time on the owner's upkeep (ADR-0006's clock).
3. **Zone damage is per target, not divided.** This is the deliberate opposite of a beacon: a beam of fixed energy is at its best against a lone target, and ground that grinds is at its best against a crowd. Two cell abilities that behaved alike would not have been worth two.
4. **Only the detonation applies the status.** Stun blocks movement (§5.1), so a zone that stunned on every tick would hold its victims inside itself until it expired, removing them from the game with no answer on the roster. The grenade crushes once; what lingers only grinds.
5. **Beacons and zones are distinct events and distinct queries.** Beacons produce `BeaconFired`; zones produce `ZoneTicked`, which carries whether this resolution is the detonation. `ActiveZones()` sits beside `ActiveBeacons()`, and the view draws zones differently from beacons. The two read differently: one beam resolving once, against ground doing its work for the third round running.
6. **The rider is a field on the effect, `BonusInOwnZone`,** read through `DeferredCellEffects.HasActiveZoneFor(owner)`. It is a field because nothing in the vocabulary can express "this effect, but only sometimes".

## Options considered

- **A separate registry for zones.** Rejected: it would have duplicated the hard part (anchoring, clocks, area queries, kill credit) to avoid duplicating the easy part (duration, split or not, status or not).
- **Re-applying the status on every tick.** Rejected on rule grounds, not tuning grounds (decision 4).
- **Splitting zone damage like a beacon.** Rejected: it would make the two cell abilities the same ability at different prices.
- **A second, conditional effect instead of a field.** Rejected for now: the vocabulary has no condition primitive, and adding one for a single rider is a larger change than the rider.

## Consequences

- **This is the first number in the game that one ability changes on another.** Balance reasoning about Bio-Link Rage now depends on whether a Killzone is live.
- **`Nuetu.cs` required no new engine capability beyond this.** The rest of his kit is existing vocabulary, which is what `AbilityResolver`'s contract predicted operators would eventually look like.
- **Closed 2026-09-15: zones were not drawn,** for the same reason given in ADR-0006. `DeviceLayer` now draws them: an armed zone as a heavier area with a ring, a zone that has gone off as a fainter area with a thin ring (`CellEffectSnapshot.HasDetonated`).
- **Zone numbers are reasoned, not measured.** They were walked down several times in one pass, together with Nuetu's health; `Nuetu.cs` records the history. Axes moved together are not independently measured.

## Amendment 1 (2026-09-17): crowd zones, zones without a status, and per-source keys

Lethe's **Eris' Exploit** is a zone whose damage depends on how many enemies it catches. It needed three changes. None of them is a new effect kind.

1. **A third damage shape.** `AbilityEffect.CrowdZone` builds a `DeployZone` with `ScalesWithCrowd` set: each victim takes the payload once for every *other* victim, as one hit. N victims take N(N−1) between them per tick. Beacons divide the payload, zones bill it in full to each victim, and crowd zones multiply it by the rest of the crowd. A crowd of one is not hit at all: a zero-damage instance would still spend an evasion charge.
2. **The status is optional.** `DeferredCellEffects.Deploy` takes a nullable status. An effect records "no status" as `Duration = 0`, which `AbilityEffect.CarriesStatus` now names. `Status` defaults to `Stun`, so any caller that reads `Status` on a non-status effect must check `CarriesStatus` first. The bots' draft picker was counting Eris' Exploit as a stun until it did.
3. **Entries are keyed on cell, seat and source operator.** Under the old key (cell and seat), a squadmate casting on a cell replaced the device already there. With Lethe and Nuetu on one seat, that meant a Killzone could be overwritten by an Eris' Exploit. A Kian beacon could be overwritten the same way, before Lethe existed. The same operator re-casting on its own cell still replaces, which is decision 1's "sources do not stack" for a single source. `HasBeaconOn` still asks about the seat as a whole.

**The rider reads only the caster's own zones.** `HasActiveZoneFor(owner, sourceOperatorId)` takes the caster, because Bio-Link Rage's rider is "while one of *his* Killzones is live", and a squadmate's crowd zone is not his. This narrows decision 6. It does not settle the open choice below, which is about *where* Nuetu stands, not *whose* zone it is.

## Open design choice

**Does the rider ask whether a zone exists anywhere, or whether Nuetu is standing in it?**

- **Current: anywhere** (`HasActiveZoneFor`). The rider is an unconditional bonus for as long as the zone lasts.
- **Alternative: positional** (`ZoneCoversOperator`). The rider becomes a reason to walk into his own grenade, which both `Nuetu.cs` and `DeferredCellEffects` call the stronger design.

Switching is a one-word change in `AbilityResolver`. The choice is a design decision, not an implementation one, and it has not been taken. When it is, amend this record.
