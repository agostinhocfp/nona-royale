# Nona Royale — Operators

> Location in repo: `docs/design/OPERATORS.md`
> Status: **Living.** Roster identity and flavour. **Contains no mechanical values.**
> Related: `docs/design/COMBAT_SYSTEMS.md` §10 (the mechanical source of truth), `PROJECT_IDENTITY.md`, `art/ART_DIRECTION.md` §5

## What this document owns

Who the operators _are_: archetype, theme, how they read at the table, and what still needs writing about them.

**Every number lives in `COMBAT_SYSTEMS.md` §10** — health, speed multiplier, ability costs, cooldowns, ranges, damage, and effects. This file used to carry its own copies of those values, and they drifted out of sync the moment combat was rebalanced: it claimed a 1× Bouncer, a 3-energy ultimate, an Energy Efficiency stat that had been cut, and a Slow value sized against a speed band that no longer exists.

That divergence is the same failure that caused the project restart, one layer up. So there is now exactly one place a stat can be changed, and it is not here.

> **If you are implementing an operator, close this file and open `COMBAT_SYSTEMS.md` §10.**

---

## Roster scope

The pool is **nine operators**; each player fields **three per match**, distinct within a squad (`GDD.md` §2.2). Three are complete. One is playable but unfinished. The remaining five are unwritten.

A new operator must be expressible in the existing combat rules. One that needs a genuinely new _mechanic_ gets an amendment to `COMBAT_SYSTEMS.md` — never a special case inside its own stat block. That rule is what stops the roster from becoming nine sets of bespoke exceptions, and it has now fired once in earnest: Mimi's coordinate swap forced a sixth effect kind and a rewrite of §7.4.

**An operator is not finished until its mechanics exist.** Flavour can be written here at any time; §10 takes an operator only once every rule it needs is implemented and tested. Until then it lives under _In play, incomplete_ or _In design_ below, with what it is blocked on written down.

**The exception, and why it is one.** Mimi is in the draft pool with two of her three abilities, so a player can be dealt her — and an operator the game can deal but the rules document does not describe is worse than an entry marked incomplete. She therefore appears in `COMBAT_SYSTEMS.md` §10.4 under a banner saying exactly what is missing. The rule holds for everyone else: an operator enters the pool and §10 together, or neither.

---

## Archetypes

The base four are **Tank**, **Assassin**, **Controller**, **Support**.

Kurbyn is tagged **Brawler**, which is a fifth. That is unresolved: either Brawler joins the list as a defined archetype with its own design brief, or Kurbyn is re-tagged. Cosmetic, but it should not stay ambiguous once five more operators need slotting.

---

## The alpha three

### Bouncer — Tank

The house's muscle, in a tailored dinner jacket. He does not chase; he decides who gets past. His kit is built around a rope that drags people where he wants them and a grapple that hurts him as much as his target — a man who spends himself to control a doorway.

Mechanically he is the squad's roadblock: the one operator who can absorb repeated collisions and hold a contested cell, and the only source of healing in the alpha roster. He is also the slowest, which is the real price of his abilities — his costs are paid in the turns it takes to be standing next to someone, not in energy.

His rope is also the roster's direct answer to an opponent who cannot be hit, which was not the original plan — it became one after the first human sessions, and it is the reason he reads as a counter-pick rather than a default.

Read at the table: _the one you route around._

### Syla, The Blood Hound — Assassin

Obsidian derringers, a taste for wounded targets, and a drone network that marks people for the rest of the squad. She is fast, fragile, and rewards a player who is already winning a fight — several of her tools do more against a target that has already been hit.

That dependence is sharper than it looks. Her cheap shot barely trades on its own; it is a control tool that pays properly only into a target she has already opened up. A player who leads with her tends to lose her.

Read at the table: _the one who finishes things._

### Kurbyn, DarkGrave — Brawler

A neural-prediction rig that lets him slip the first punch of any exchange, a pulse that scrambles motor function in a small radius, and a gravitic tether that implodes anything already dying. Built to be in the middle of a scrum and survive the first thing that hits him — by prediction, not armour.

He dominated the first human sessions, and the response was to give one other operator a way through him rather than to take the prediction away. The rig is untouched; what changed is that it is no longer an answer to everything.

Read at the table: _the one who is always already there._

---

## In play, incomplete

### Mimi — Controller

A cryogenics rig and a coordinate-swap device. She freezes the ground out from under a target, hangs a field of cold on herself that bites anything that lingers, and when the fight arrives at her she simply trades places with whoever brought it.

She is the most fragile operator on the roster and moves at the same speed as the tank, so she cannot run from anything. What she has instead is reach — she can open an exchange from further out than any other operator can answer from, and she can leave one the same way. A player who treats her as a damage dealer will lose her in two hits; a player who treats her as a lever will move the whole board with her.

Her entire kit is one damage type, deliberately: she is the answer to an opponent who hides behind barriers, and she is helpless against one who simply doesn't get hit.

Read at the table: _the one who is never where you left her._

**She is draftable and playable with two of three abilities.** Her live kit is in `COMBAT_SYSTEMS.md` §10.4, marked incomplete.

**One of three blockers is cleared:**

- ~~**A swap effect.**~~ **Done.** It needed a sixth effect kind, and it forced the §7.4 amendment that had been overdue since Velvet Rope's clamp went undocumented. The arithmetic also exposed a case nobody had noticed — a placement running _forwards_ into a home column — and produced the rule that placement moving one operator clamps while placement moving two refuses.
- **The Tech damage type.** Still deferred, still pending shields having a real source. Her damage ships as Normal, which behaves identically and expresses none of her identity. She remains the strongest argument for building both.
- **A field that damages on a duration.** Still missing. Auras are permanent and carry speed effects only, and no status damages an area at its holder's upkeep. **Cryo Field is not implemented and she plays without it.**

Her fiction is also the strongest argument in the tech-level question below. Everyone else has a rope, a rig or a drone; she has a singularity core and tachyon targeting.

---

## Design constraints on future operators

- **Silhouette-first.** Identifiable in pure black at board scale (`ART_DIRECTION.md` §5).
- **Powers are technology, never magic.** Every ability must have a plausible near-future device behind it — an emitter, a rig, a drone, a tether. This is locked (`ART_DIRECTION.md` §0).
- **A discreet tech tell.** Formalwear silhouette, with one visible device that hints at the ability.
- **Expressible in the existing rules**, per Roster scope above.
- **No operator carries a stat nobody can feel.** Energy Efficiency was cut for exactly this reason: one value on one operator, blank on two, and no rule ever attached to it.
- **One damage type is an identity, not a default.** A kit built entirely around a single type says what that operator answers and what answers it. It only works while the types stay scarce — the moment most abilities pierce most defences, the defences stop existing and so does the identity.
- **Watch where the counters are concentrated.** Only two operators carry unblockable single-target damage, which is fine while every squad fields all three of the alpha roster and becomes a drafting question at three-from-nine (`COMBAT_SYSTEMS.md` §2.2). Adding another operator who is hard to hit, without adding another way through, makes that worse.

---

## Open items

- [ ] Resolve **Brawler**: define it as a fifth archetype, or re-tag Kurbyn.
- [ ] **Finish Mimi** — the Tech damage type, and an area-damage-over-time status for Cryo Field. The swap landed; she plays without the field.
- [ ] **Measure Mimi.** She is in the pool and nothing has ever dealt her: the harness still fields the alpha three, so neither of her live abilities has run in a simulated match. Two open questions wait on it — whether Cryo-Pulse outclasses its peer at the same cost, and whether Translocation is as strong a denial tool as it looks.
- [ ] Settle the **tech level**. Bouncer has a rope and Kurbyn has a neural-prediction rig; Mimi has a singularity core and tachyon targeting. Those are not the same world. Either the ceiling moves up for everyone or Mimi's fiction comes down to meet the others — but nine operators should not be spread across three centuries by accident.
- [ ] Write the remaining **five operators**, mechanically and in flavour.
- [ ] Backstory and world placement for the alpha three — how they connect to the casino, to each other, and to whoever runs the house.
- [ ] Strengths, weaknesses, synergies, counters, playstyle notes per operator. Deferred to the balance pass; they are currently implied by the kits rather than stated.
- [ ] Voice and naming conventions for the roster — "Nona" is nine, and the pool is nine. Whether that is coincidence or canon is undecided.
