# ADR-0012: Sides, and the Crossed 1v1

> Location in repo: `docs/decisions/0012-team-sides.md`
> Status: **Accepted**
> Date: 2026-09-20
> Related: ADR-0003 (board topology → start offsets), ADR-0004 (core architecture, and its amendment on derived values), ADR-0005 (collision as an attack), `docs/design/COMBAT_SYSTEMS.md` §1.2, §4, §5.4, §8, §10.2, `docs/design/BOTS.md`

## Context

The game has always had four seats and no notion of a side. Every rule that
asks "friend or foe" asks it the same way:

```csharp
if (candidate.Owner == mover.Owner) continue;      // CollisionResolver
if (caster.Owner != target.Owner && ...)           // TargetingRules
if (killer.Owner == victim.Owner) return nothing;  // NeutralizeRules
bool sameSeat = source.Owner == op.Owner;          // AuraRules
return target.Owner == by;                         // StatusRegistry (stealth)
```

That is correct for a four-way free-for-all and it is the only mode there has
been. The requirement now is a **1v1 in which each player holds two seats**,
paired across the table: Red with Green, Blue with Violet.

The naive version is a flag — "team mode" — consulted at each of those sites.
It fails for the reason ADR-0004 was written about: the friend/foe test is
read in a dozen files, and a branch in a dozen files is a dozen places for the
two modes to drift. A subtler failure is worse: adding a branch to eleven of
the twelve sites leaves one rule quietly playing free-for-all, and the symptom
— a Red beacon that also strikes Green, once, in the one match where both were
in range — is nearly untestable after the fact.

There was also a question that had to be settled before any of this: **which
seats pair up.** A colour's start sits at `(int)colour * PlayerStartOffset`
around the loop (ADR-0003), so pairing neighbours (Red+Blue against
Green+Violet) hands each side one contiguous half of the board, and the match
becomes a standoff across a single seam. Pairing opposites puts each side's
two runners half a circuit apart at all times, so both players are always
present in both halves of the track.

## Decision

**A `TeamMap` is the one place the game answers "friend or foe", and
free-for-all is a team map like any other.**

### The type

`NonaRoyale.Core.Board.TeamMap` is immutable and holds nothing but which seat
is on which side. Its surface is three questions — `AreAllied`, `AreEnemies`,
`SeatsOn` — plus `LeadSeat`, which names a side with a `PlayerColor` for the
benefit of a view that knows how to render colours and not sides.

Two layouts ship:

- `TeamMap.FreeForAll` — each seat on a side of its own.
- `TeamMap.CrossedPairs` — Red+Green against Blue+Violet.

`AreEnemies` is **not** the negation of `AreAllied`. `PlayerColor.None` is
neither: outer-track cells carry `None` as their owner, and a cell has no side.

### There is no team-mode branch

Every comparison listed above became a call on the map. Under
`FreeForAll`, `AreAllied(a, b)` *is* `a == b`, so the four-way game runs on
exactly the code path it always ran on. That is the whole design: **the rules
do not know team play exists.** They know about sides, and free-for-all is the
map where every side has one seat.

The consequence worth stating plainly is that the four-way game cannot rot
while 1v1 is worked on, because there is nothing 1v1-shaped in the rules to
get it wrong.

### What the map is threaded into

Five services take it directly, as an optional constructor argument defaulting
to `FreeForAll`: `TargetingRules`, `StatusRegistry`, `CollisionResolver`,
`NeutralizeRules`, `WinConditions`. `MatchFactory` builds one map and hands it
to all five, so a match cannot hold two.

Everything else reaches it through a service it already had.
`TargetingRules` exposes `AreAllied`/`AreEnemies`, and `AuraRules`,
`AbilityResolver`, `DeferredCellEffects` and `DeferredOperatorEffects` all
hold a `TargetingRules` already — a second map passed alongside is a second
map that can disagree. `BotBoard` reads it off the `MatchFactory.Match` it
holds, for the same reason.

Defaulting the argument rather than adding it is deliberate: every existing
call site, every test and the whole simulation harness compile and behave
unchanged, so the change is verifiable by the suite staying green rather than
by reading the diff.

### What changes in a crossed match

Mechanically, everything follows from the map:

- **Collision (§4.5).** Partners stack. Landing on your own side is not an
  attack, so a clumsy roll costs the partnership nothing.
- **Areas, lines and splashes (§4.2).** An enemy sweep spares the partner; an
  allied splash — Javi's heal — reaches it.
- **Single-targeting (§4.4).** A partner on a safe cell can still be healed,
  plated or relocated, exactly as a squadmate always could; the camping rule
  does not fire on a partner, because it exists to stop aggression out of
  shelter.
- **Stealth (§5.4).** Hides from the opposition only, now meaning both enemy
  seats rather than the other three.
- **Auras (§5.9).** Lethe's Catalyst reaches the partner seat; Bouncer's drag
  does not slow it.
- **Ability audience.** `AbilityResolver.CastMode` decides ally-mode from the
  map, which is what makes every friendly mode on the roster reach across the
  partnership **without a single ability being rewritten.**
- **Tables (§7.7).** A side's tables let both its seats through.
- **Kill bounty and knockout credit (§1.2).** Killing a partner pays nothing
  and is credited to nobody — a player holding two seats must not be able to
  farm one with the other.
- **The win (§8).** A side wins when **all six** of its operators are home.

### What deliberately does not change

- **Turn order.** The seat rotation stays Red → Blue → Green → Violet, so the
  two players alternate and each acts once per seat per round. Every timer in
  the game — status durations, cooldowns, bleed, beacons, zones, the deploy
  drought — is indexed on a seat's own turn count, and reordering the rotation
  would shift all of them at once and invalidate the tuning behind them.
- **Energy.** One pool per seat. The tactical question the pool creates —
  which of my three spends this — is the point of it (§3), and a shared pool
  would double a side's spending power against a pool granted twice as often.
- **Command authority.** A seat commands its own operators on its own turn.
  Holding two seats is not holding one bigger seat.
- **Squads.** Three operators per seat, drafted per seat.
- **Tagged From Above's payout (§10.2).** It hastens the marker's **seat**,
  three operators, at either table.

### The one deliberate exception

The mark payout is the single friend/foe test in the core that does **not**
go through the map, and it is worth saying why out loud, because a reader who
finds the last surviving `Owner !=` in `NeutralizeRules` will otherwise assume
it was missed.

§10.2 hands the kill to the squad that spent the cast, and a squad is three
operators. Routed through the map it would hasten six in a crossed match,
which makes the ability worth roughly twice as much at one table as at the
other — a partner seat that paid nothing toward the mark would collect from
it. Every other rule here reads sides because *reachability and ownership*
are properties of a side; a payout is a reward for a cast, and the cast was
paid for out of one seat's pool.

`TeamRulesTests.AMarkPayoutHastensTheMarkersSeatOnly_AtEitherTable` pins it in
both modes, so tidying that comparison into a map call fails a test rather
than silently doubling an ability.

## Presentation

`WinConditions` gained `WinningSeats` and `IsFinalStretch`, and `GameWon`
gained `Seats`. `IsFinalStretch` moved out of `GameEngine`: counted per seat,
a crossed match would cue the showdown music while the partner seat still had
three operators in its yard. That is ADR-0004's amendment doing its job — the
view displays what the core computes, so when the win condition changes shape
the thing that previews it has to move with it.

The end screen names the side: "RED & GREEN WIN", tinted by the seat the side
is named after. A partnership has no colour of its own, and inventing one
would fight the seat palette everywhere else on that screen.

## Setup

`MatchSettings` gained a `TableMode` — `FreeForAll` or `CrossedPairs` — and
the setup screen a TABLE row above the seats. Choosing a crossed table fills
all four seats and locks a seat's kind to its partner's: one player holds both,
so "human here, CPU there" describes a table nobody is choosing on purpose.
A seat tile then reads `HUMAN · GREEN`, because four tiles reading HUMAN and
CPU do not say which two are partners.

## Consequences

- **The friend/foe test now has one home.** A future mode — 2v2 down the
  table, a free-for-all with a temporary alliance, an asymmetric side — is a
  different `TeamMap`, not a different rules path. `TeamMap.AdjacentPairs`
  exists, unshipped, so that the crossed layout is visibly a choice.
- **A rule that reintroduces a colour comparison is a bug**, and an invisible
  one. `TeamMapTests` asserts that `FreeForAll` is exactly seat equality,
  which is the guard rail: if that ever stopped being true the four-way game
  would change without a single four-way test failing.
- **Tests pair every team assertion with its free-for-all twin** in the same
  method. "Red does not collide with Green" is only interesting alongside "and
  it still does under free-for-all"; a bug that made everyone friendly would
  otherwise pass the whole fixture.
- **The bots follow the map** (`BotBoard`, `CastPlanner`), so a CPU side does
  not shell its own partner. Their *tuning* is still four-way tuning; how a
  bot should value covering a partner is not a question this ADR answers.
- **An ability may be scoped to a seat on purpose**, as the mark payout is.
  "Sides are the default" is not "sides everywhere": the map is there so that
  a rule reading colours is a visible, argued choice rather than an oversight.
- **Cost.** Every service that decides friend or foe now takes a constructor
  argument it did not take before. That is the price of there being exactly
  one answer, and it is the same trade ADR-0004 made about the energy formula.

## Alternatives considered

- **A `bool teamMode` consulted at each site.** Rejected: twelve branches in
  twelve files, and the failure mode of missing one is a rule that silently
  plays the other game.
- **A `Side` property on `PlayerState` or `PlayerColor`.** Rejected: a seat's
  side is a property of the *match*, not of the colour — Red and Green are
  enemies in a four-way and partners in 1v1 — so this is either a global or a
  second copy that can disagree with the first.
- **Merging a side into one `PlayerState` with six operators and one pool.**
  Rejected: it collapses the seat, and with it four starts, four home columns,
  four energy pools and every turn-indexed timer. The board is built for four
  seats (ADR-0003); a 1v1 is two players at four seats, not two seats.
- **Adjacent pairing (Red+Blue against Green+Violet).** Rejected on the
  geometry: it gives each side a contiguous half of the loop and reduces the
  match to a standoff across one seam.
- **Winning when either seat gets all three home.** Rejected: it makes the
  second seat a pure escort and ends matches roughly twice as early. It is a
  coherent different game, and if it is ever wanted it is a rule on top of
  this ADR, not a replacement for it.

## Status history

- 2026-09-20 — Accepted. `TeamMap` as the single friend/foe authority;
  free-for-all expressed as a map; crossed pairing for 1v1; all six home to
  win; turn order, energy, command authority and Tagged From Above's payout
  unchanged.
