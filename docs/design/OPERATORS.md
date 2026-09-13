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

The pool is **nine operators**; each player fields **three per match**, distinct within a squad (`GDD.md` §2.2). Three are complete. **Two are playable but unfinished.** The remaining four are unwritten.

A new operator must be expressible in the existing combat rules. One that needs a genuinely new _mechanic_ gets an amendment to `COMBAT_SYSTEMS.md` — never a special case inside its own stat block. That rule is what stops the roster from becoming nine sets of bespoke exceptions, and it has now fired twice in earnest: Mimi's coordinate swap forced a sixth effect kind and a rewrite of §7.4, and Javi's cleanse forced a seventh — the first that removes from the status registry rather than adding to it.

**An operator is not finished until its mechanics exist.** Flavour can be written here at any time; §10 takes an operator only once every rule it needs is implemented and tested. Until then it lives under _In play, incomplete_ or _In design_ below, with what it is blocked on written down.

**The exception, and why it is one.** Mimi and Javi are both in the draft pool with an ability missing, so a player can be dealt either — and an operator the game can deal but the rules document does not describe is worse than an entry marked incomplete. They therefore appear in `COMBAT_SYSTEMS.md` §10.4 and §10.5 under banners saying exactly what is absent. The rule still holds for everyone else: an operator enters the pool and §10 together, or neither.

That the exception now covers two of five is a warning rather than a precedent. A third would mean the pool has become the place operators go to wait, which is the opposite of what it is for.

---

## Archetypes

The base four are **Tank**, **Assassin**, **Controller**, **Support**.

All four are now filled: Bouncer, Syla, Mimi and Javi. Kurbyn is tagged **Brawler**, which is a fifth. That is unresolved: either Brawler joins the list as a defined archetype with its own design brief, or Kurbyn is re-tagged. Cosmetic, but it should not stay ambiguous now the base four are occupied — every remaining operator is either a second of something or a new archetype.

---

## The house and the rest

Three of the five written operators work for the house: Bouncer on the door, Kurbyn sent after whoever gets past it, and Javi keeping both of them upright. Syla and Mimi are outside contractors.

That split emerged rather than being designed, and it is worth ratifying or breaking on purpose. It currently gives the house a visual language — charcoal, oxblood and brass livery, worn differently by each — and leaves the contractors free to look like anything, which is real work being done at board scale. Four unwritten operators will tip it either way.

---

## The alpha three

### Bouncer — Tank

The house's muscle, in a tailored dinner jacket. He does not chase; he decides who gets past. His kit is built around a rope that drags people where he wants them and a grapple that hurts him as much as his target — a man who spends himself to control a doorway.

Mechanically he is the squad's roadblock: the one operator who can absorb repeated collisions and hold a contested cell. He is also the slowest, which is the real price of his abilities — his costs are paid in the turns it takes to be standing next to someone, not in energy.

His rope is also the roster's direct answer to an opponent who cannot be hit, which was not the original plan — it became one after the first human sessions, and it is the reason he reads as a counter-pick rather than a default.

He came down on every axis after human play: health, reach and damage together. If he now reads as weak, the reach is the first thing to restore, because it is the only one of the three that also governs what his aura can catch.

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

**Draftable and playable with two of three abilities.** Her live kit is in `COMBAT_SYSTEMS.md` §10.4, marked incomplete.

**One of three blockers is cleared:**

- ~~**A swap effect.**~~ **Done.** It needed a sixth effect kind, and it forced the §7.4 amendment that had been overdue since Velvet Rope's clamp went undocumented. The arithmetic also exposed a case nobody had noticed — a placement running _forwards_ into a home column — and produced the rule that placement moving one operator clamps while placement moving two refuses.
- **The Tech damage type.** Still deferred, still pending shields having a real source. Her damage ships as Normal, which behaves identically and expresses none of her identity. She remains the strongest argument for building both — now joined by Javi, who is the shield source it has been waiting for.
- **A field that damages on a duration.** Still missing. Auras are permanent and carry speed effects only, and no status damages an area at its holder's upkeep. **Cryo Field is not implemented and she plays without it.**

Her fiction is also the strongest argument in the tech-level question below. Everyone else has a rope, a rig or a drone; she has a singularity core and tachyon targeting.

### Javi — Support

House livery stripped to its working layer: bone-white shirt, cuffs buttoned tight, a high-fastened waistcoat with more pockets than a waistcoat should have, and no jacket at any point in the evening. Where Kurbyn sheds the uniform in the middle of a fight, Javi never put the outer half on — he expects to have his hands in someone.

His hands are the tell. Thin grey gloves, always on, and a bandolier of nanite canisters worn across the chest like ammunition, each frosted at the seal and racked in order. He is the only operator whose kit is visibly _consumable_: you can read how long the night has been from how many canisters are left.

He is the first operator who makes an enemy harder to kill rather than easier, which changes what the whole board is doing. He is also the first whose best play is often to stand next to something dangerous, because his nanites spill onto whoever is near the _target_ rather than near him — which is exactly where the roster's two area abilities punish a squad for standing.

Read at the table: _the one you have to kill twice._

**Draftable and playable with two of three abilities.** His live kit is in `COMBAT_SYSTEMS.md` §10.5, marked incomplete.

**One blocker, and it is the roster's most consequential:**

- **A shield with a per-ability value.** Shields currently absorb one whole instance whatever its size, granted by a board space that is itself deferred — which could never generate enough uptime for a mitigation type to mean anything. **Trauma Plate is not implemented and he plays without it.** It lands with the mitigation pass already rewriting the same interface for deterministic evasion; doing both in parallel would collide.

**And one thing that is not a blocker but should be settled before he is measured.** §12 records that neutralizing rewards the attacker with nothing, and suspects that suppresses combat in human play in a way the harness cannot detect, because the scripted player fights unconditionally. A dedicated healer makes kills materially harder to land. His measured strength depends entirely on which way that question goes.

---

## Design constraints on future operators

- **Silhouette-first.** Identifiable in pure black at board scale (`ART_DIRECTION.md` §5). Five shapes are spoken for; a sixth has to stay legible beside all of them at a third of a cell.
- **Powers are technology, never magic.** Every ability must have a plausible near-future device behind it — an emitter, a rig, a drone, a tether. This is locked (`ART_DIRECTION.md` §0).
- **A discreet tech tell.** Formalwear silhouette, with one visible device that hints at the ability.
- **Expressible in the existing rules**, per Roster scope above.
- **No operator carries a stat nobody can feel.** Energy Efficiency was cut for exactly this reason: one value on one operator, blank on two, and no rule ever attached to it.
- **One damage type is an identity, not a default.** A kit built entirely around a single type says what that operator answers and what answers it. It only works while the types stay scarce — the moment most abilities pierce most defences, the defences stop existing and so does the identity.
- **Watch where the counters are concentrated.** Only two operators carry unblockable single-target damage, which is fine while every squad fields all three of the alpha roster and becomes a drafting question at three-from-nine (`COMBAT_SYSTEMS.md` §2.2). Adding another operator who is hard to hit, without adding another way through, makes that worse.
- **Every ability needs a line a player can read.** Not a restatement of its numbers — the view already shows those — but what the ability is _for_. The constructor refuses an ability without one, which is the only reason every operator has them.

---

## Open items

- [ ] Resolve **Brawler**: define it as a fifth archetype, or re-tag Kurbyn. More pressing now the base four are filled.
- [ ] **Finish Mimi** — the Tech damage type, and an area-damage-over-time status for Cryo Field.
- [ ] **Finish Javi** — a shield with a per-ability value, which lands with the mitigation pass.
- [ ] **Measure both.** They are in the pool and nothing has ever dealt them: the harness still fields the alpha three, so not one of their four live abilities has run in a simulated match. Three questions wait on it — whether Cryo-Pulse outclasses its peer at the same cost, whether Translocation is as strong a denial tool as it looks, and whether a healer turns a combat game into a race.
- [ ] **Ratify or break the house split.** Three of five work for the house. It emerged rather than being designed, and four unwritten operators will tip it either way.
- [ ] Settle the **tech level**. Bouncer has a rope and Kurbyn has a neural-prediction rig; Mimi has a singularity core and tachyon targeting. Those are not the same world. Either the ceiling moves up for everyone or Mimi's fiction comes down to meet the others — but nine operators should not be spread across three centuries by accident.
- [ ] Write the remaining **four operators**, mechanically and in flavour.
- [ ] Backstory and world placement — how the five connect to the casino, to each other, and to whoever runs the house.
- [ ] Strengths, weaknesses, synergies, counters, playstyle notes per operator. Deferred to the balance pass; they are currently implied by the kits rather than stated.
- [ ] Voice and naming conventions for the roster — "Nona" is nine, and the pool is nine. Whether that is coincidence or canon is undecided.
