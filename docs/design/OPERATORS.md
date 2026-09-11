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

The pool is **nine operators**; each player fields **three per match**. Three are built for the alpha. The remaining six are unwritten.

A new operator must be expressible in the existing combat rules. One that needs a genuinely new _mechanic_ gets an amendment to `COMBAT_SYSTEMS.md` — never a special case inside its own stat block. That rule is what stops the roster from becoming nine sets of bespoke exceptions.

---

## Archetypes

The base four are **Tank**, **Assassin**, **Controller**, **Support**.

Kurbyn is tagged **Brawler**, which is a fifth. That is unresolved: either Brawler joins the list as a defined archetype with its own design brief, or Kurbyn is re-tagged. Cosmetic, but it should not stay ambiguous once six more operators need slotting.

---

## The alpha three

### Bouncer — Tank

The house's muscle, in a tailored dinner jacket. He does not chase; he decides who gets past. His kit is built around a rope that drags people where he wants them and a grapple that hurts him as much as his target — a man who spends himself to control a doorway.

Mechanically he is the squad's roadblock: the one operator who can absorb repeated collisions and hold a contested cell, and the only source of healing in the alpha roster. He is also the slowest, which is the real price of his abilities — his costs are paid in the turns it takes to be standing next to someone, not in energy.

Read at the table: _the one you route around._

### Syla, The Blood Hound — Assassin

Obsidian derringers, a taste for wounded targets, and a drone network that marks people for the rest of the squad. She is fast, fragile, and rewards a player who is already winning a fight — several of her tools do more against a target that has already been hit.

Read at the table: _the one who finishes things._

### Kurbyn, DarkGrave — Brawler

A neural-prediction rig that lets him slip the first punch of any exchange, a pulse that scrambles motor function in a small radius, and a gravitic tether that implodes anything already dying. Built to be in the middle of a scrum and survive the first thing that hits him — by prediction, not armour.

Read at the table: _the one who is always already there._

---

## Design constraints on future operators

- **Silhouette-first.** Identifiable in pure black at board scale (`ART_DIRECTION.md` §5).
- **Powers are technology, never magic.** Every ability must have a plausible near-future device behind it — an emitter, a rig, a drone, a tether. This is locked (`ART_DIRECTION.md` §0).
- **A discreet tech tell.** Formalwear silhouette, with one visible device that hints at the ability.
- **Expressible in the existing rules**, per Roster scope above.
- **No operator carries a stat nobody can feel.** Energy Efficiency was cut for exactly this reason: one value on one operator, blank on two, and no rule ever attached to it.

---

## Open items

- [ ] Resolve **Brawler**: define it as a fifth archetype, or re-tag Kurbyn.
- [ ] Write the remaining **six operators**, mechanically and in flavour.
- [ ] Backstory and world placement for the alpha three — how they connect to the casino, to each other, and to whoever runs the house.
- [ ] Strengths, weaknesses, synergies, counters, playstyle notes per operator. Deferred to the balance pass; they are currently implied by the kits rather than stated.
- [ ] Voice and naming conventions for the roster — "Nona" is nine, and the pool is nine. Whether that is coincidence or canon is undecided.
