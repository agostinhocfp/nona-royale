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

The pool is **nine operators**; each player fields **three per match**, distinct within a squad (`GDD.md` §2.2). **The pool is full and all nine are complete** — Mimi's Cryo Field, the last missing ability, landed 2026-09-16 (`COMBAT_SYSTEMS.md` §10.4). **Kian and Nuetu have no entry in this file yet**; their kits are in §10.6 and §10.7, and their identities are unwritten.

A new operator must be expressible in the existing combat rules. One that needs a genuinely new _mechanic_ gets an amendment to `COMBAT_SYSTEMS.md` — never a special case inside its own stat block. That rule is what stops the roster from becoming nine sets of bespoke exceptions, and it has now fired twice in earnest: Mimi's coordinate swap forced a sixth effect kind and a rewrite of §7.4, and Javi's cleanse forced a seventh — the first that removes from the status registry rather than adding to it.

**An operator is not finished until its mechanics exist.** Flavour can be written here at any time; §10 takes an operator only once every rule it needs is implemented and tested. Until then it lives under _In play, incomplete_ or _In design_ below, with what it is blocked on written down.

**The exception, retired.** Mimi and Javi spent a stretch in the draft pool with an ability missing, under banners in `COMBAT_SYSTEMS.md` §10.4 and §10.5 saying exactly what was absent — an operator the game can deal but the rules document does not describe is worse than an entry marked incomplete. Both are complete now (Javi on 2026-09-15, Mimi on 2026-09-16), and the rule stands for whoever comes next: an operator enters the pool and §10 together, or neither.

That the exception now covers two of five is a warning rather than a precedent. A third would mean the pool has become the place operators go to wait, which is the opposite of what it is for.

---

## Archetypes

The base four are **Tank**, **Assassin**, **Controller**, **Support**.

All four are now filled: Bouncer, Syla, Mimi and Javi. Kurbyn is tagged **Brawler**, which is a fifth. That is unresolved: either Brawler joins the list as a defined archetype with its own design brief, or Kurbyn is re-tagged. Cosmetic, but it should not stay ambiguous now the base four are occupied — every remaining operator is either a second of something or a new archetype.

---

## The house and the rest

Four operators work for the house: Bouncer on the door, Kurbyn sent after whoever gets past it, Javi keeping both of them upright, and Sanity keeping the building running. Four are outside contractors: Syla, Mimi, and — decided 2026-09-15, identities still unwritten — Kian and Nuetu. **Luka is neither**: nobody pays him and nobody sent him. He is the first operator with a grudge against the house, a third camp chosen on purpose (2026-09-15).

**The split is ratified (2026-09-15): four house, four contractors, one grudge.** It emerged rather than being designed, and it now stands on purpose. It gives the house a visual language — charcoal, oxblood and brass livery, worn differently by each — and leaves the contractors free to look like anything, which is real work being done at board scale.

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

A neural-prediction rig that lets him slip the first punch of any exchange and finishes reading a target's next move before the target makes it, a pulse that scrambles motor function in a small radius, and a gravitic tether that implodes anything already dying. Built to be in the middle of a scrum and survive the first thing that hits him — by prediction, not armour.

He dominated the first human sessions, and the response was to give one other operator a way through him rather than to take the prediction away. The rig is untouched; what changed is that it is no longer an answer to everything.

Read at the table: _the one who is always already there._

---

## In play, incomplete

### Mimi — Controller

A cryogenics rig and a coordinate-swap device. She freezes the ground out from under a target, hangs a field of cold on herself that bites anything that lingers, and when the fight arrives at her she simply trades places with whoever brought it.

She is the most fragile operator on the roster and moves at the same speed as the tank, so she cannot run from anything. What she has instead is reach — she can open an exchange from further out than any other operator can answer from, and she can leave one the same way. A player who treats her as a damage dealer will lose her in two hits; a player who treats her as a lever will move the whole board with her.

Her entire kit is one damage type, deliberately: she is the answer to an opponent who hides behind barriers, and she is helpless against one who simply doesn't get hit.

Read at the table: _the one who is never where you left her._

**Draftable and playable, complete since 2026-09-16.** Her kit is in `COMBAT_SYSTEMS.md` §10.4.

**All three blockers are cleared:**

- ~~**A swap effect.**~~ **Done.** It needed a sixth effect kind, and it forced the §7.4 amendment that had been overdue since Velvet Rope's clamp went undocumented. The arithmetic also exposed a case nobody had noticed — a placement running _forwards_ into a home column — and produced the rule that placement moving one operator clamps while placement moving two refuses.
- ~~**The Tech damage type.**~~ **Done (2026-09-15)**, with Luka. Her damage is now Tech, which Luka's Hermes' Ring blocks and nothing else does. Her identity as the answer to barriers is still unexpressed: Tech is Normal plus that one counter until something amplifies it (`COMBAT_SYSTEMS.md` §2.2).
- ~~**A field that damages on a duration.**~~ **Done (2026-09-16).** It arrived as the field shape of the operator-anchored deferred registry (`COMBAT_SYSTEMS.md` §6.6): a self-anchored `CryoField` status (§5.14) that bills enemies near her at each of her upkeeps, follows her when she moves, and ends when she does. The tick is 1 Normal — the design table left the amount blank, and it is flagged for tuning.

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

## Beyond the alpha

### Sanity — Engineer

The house's maintenance man, and the first operator who looks like work rather than violence. A broad, hulking silhouette — an octagon where everyone else is a dart or a disc — in a heavy canvas apron over the livery, tool roll across the chest, and a charged prod slung at the hip like a sidearm. He does not hurry. He has never once hurried.

His kit is the fantasy of the immovable object. He is the toughest operator ever fielded and the slowest by half the band, and everything he carries is built to make standing still a threat: a prod that shorts out whatever he can reach, a magnetized grenade that snaps onto a victim and follows it home, and a winch-anchor that fires him along the track at a target — enemy or ally — raking everyone between. The grenade is the tell of the whole design: a delayed certainty you can see coming, and the counterplay is to cleanse it off before it goes off.

He is tagged **Engineer**, which is a sixth archetype and as undefined as Brawler — the same open question, now twice as pressing. Both should be settled together.

Read at the table: _the one the fight has to come to._

**Complete and in the pool.** His live kit is in `COMBAT_SYSTEMS.md` §10.8, including the two recorded designer overrides — the sub-band speed and the roster-maximum health — that his fantasy was bought with.

### Luka — Duelist _(archetype label provisional)_

Born in the Caucasus, and built like someone who was fighting before anyone taught him how: medium frame, athletic, a clean buzzcut, nothing on him that is there for show. He dresses for the room without dressing like it. He wears a black open-collar shirt under a worn, slightly creased camel linen blazer, unstructured, with the sleeves pushed to the forearm and the shirt cuffs turned over them, dark trousers and good shoes he can move in. His hands are wrapped in worn tape, and a healed split runs through one eyebrow. No livery, no crest. In a room where the house dresses dark, he is the light shape on the floor, which is also what keeps him readable on the board. The only thing on him that lights up is a heavy signet ring on his right hand.

**Where he comes from.** He came up in the fight nights the house runs under the floor: bare-knuckle bouts for high rollers who like to watch. He was the best earner that room ever had, which made him the house's to spend. On the night the book was heavy against him, he was told to lose, and he refused. He lost anyway. Something guided came down out of the rafters mid-round, and nobody in the room saw it but him.

He came back with two things: the ring, taken off the man who worked the rafters that night, and the names that were on the book. He is working through them one at a time. The house calls it a vendetta. To him it is a list.

**The device: one ring, two directions.** Everyone in the casino wears optics, whether lenses, implants or the house's own sensor web, and the ring is a signal spoofer tuned to all of them.

- **Turned outward (Blind Spot),** it edits him out of every lens in the room for the second it takes to cross the floor. Nobody sees him move. That is why the move plays as a teleport, and why it touches nobody on the way. He surfaces beside the target and stays in its blind spot, which is the second strike: get clear, or take another one.
- **Turned inward (Hermes' Ring),** it jams anything guided or remote-operated that is aimed at him. That is the Tech rule (`COMBAT_SYSTEMS.md` §2.2), and it is exactly what beat him. He wears the answer to the thing that took his fight.

The name is the house technicians': Hermes, patron of thieves, for a ring that lets its wearer walk through a room unseen.

**The ring is his whole tech tell.** Everything else is his hands. Vendetta is not a device. It is a fighter who has stopped holding back, and he knows where big men break, which is where the heavier crits against heavy targets come from.

**The world fact this commits to:** people in the casino wear optics. That sits comfortably beside Kurbyn's neural-prediction rig and well below Mimi's singularity core, and it adds a data point to the tech-level question below.

Read at the table: _the one you have to outrun, not outlast._

The whole kit is a duel. Blind Spot puts him next to his target, and the threat of a second strike next turn forces the target to spend its own move getting clear, or pay for staying. He is the third operator with unblockable damage, and the first with a random swing.

**Silhouette:** a four-pointed star turned to an X, like a thrown blade: a piece aimed at one person. It is distinct from Kian's five points and Javi's upright cross. The weapon it suggests is figurative; his are his fists.

**Art notes** (`ART_PIPELINE.md` §6):

- **Look settled 2026-09-16** from image-generator runs (`ART_PROMPTS.md`): the camel blazer, black shirt and trousers, hand tape and eyebrow scar. Board figures still need the 6-head proportions and the ready stance; the character sheet is the design reference, not a board asset.
- **Keep the light jacket his.** No other operator should carry a light torso, or he loses the one shape that makes him stand out.
- **Seated:** at the yard table, taping his hands.
- **Rise:** the jacket comes off the chair.
- **Ability tell:** the ring flares in the cool cyan register, and his figure drops out in horizontal scanlines before resolving beside the target. The ward is the same flare held steady, a faint ring of interference around him.
- **Neutralized:** the scanline dropout played in reverse, back to the table.

**Complete and in the pool.** His live kit is in `COMBAT_SYSTEMS.md` §10.9. Look, fiction, device and the Blind Spot name were settled 2026-09-15. Still open, and not his to settle alone: the **Duelist** tag is a seventh archetype, beside Brawler and Engineer.

---

## Design constraints on future operators

- **Silhouette-first.** Identifiable in pure black at board scale (`ART_DIRECTION.md` §5). Five shapes are spoken for; a sixth has to stay legible beside all of them at a third of a cell.
- **Powers are technology by default.** Every ability should have a plausible near-future device behind it: an emitter, a rig, a drone, a tether, a ring. `ART_DIRECTION.md` §0 keeps the door open for rare, very light magical touches, always drawn in the cool register. Treat magic as an exception to argue for, never a shortcut past designing the device. Locked in that form (confirmed 2026-09-15).
- **A discreet tech tell.** Formalwear silhouette, with one visible device that hints at the ability.
- **Expressible in the existing rules**, per Roster scope above.
- **No operator carries a stat nobody can feel.** Energy Efficiency was cut for exactly this reason: one value on one operator, blank on two, and no rule ever attached to it.
- **One damage type is an identity, not a default.** A kit built entirely around a single type says what that operator answers and what answers it. It only works while the types stay scarce — the moment most abilities pierce most defences, the defences stop existing and so does the identity.
- **Watch where the counters are concentrated.** Three operators now carry unblockable single-target damage (Luka joined Bouncer and Kurbyn), which is fine while every squad fields all three of the alpha roster and becomes a drafting question at three-from-nine (`COMBAT_SYSTEMS.md` §2.2). Adding another operator who is hard to hit, without adding another way through, makes that worse.
- **Every ability needs a line a player can read.** Not a restatement of its numbers — the view already shows those — but what the ability is _for_. The constructor refuses an ability without one, which is the only reason every operator has them.

---

## Open items

- [ ] Resolve **Brawler**: define it as a fifth archetype, or re-tag Kurbyn. More pressing now the base four are filled.
- [x] ~~**Finish Mimi** — an area-damage-over-time status for Cryo Field.~~ Done 2026-09-16: the field shape of `DeferredOperatorEffects` (§6.6) with a `CryoField` marker (§5.14), 1 Normal per tick flagged for designer tuning. (Tech landed 2026-09-15.)
- [x] ~~**Write Luka**~~: look, fiction, device and the Blind Spot name are done (2026-09-15). Whether "Duelist" is an archetype is still open, together with Brawler and Engineer.
- [ ] **Write Kian and Nuetu** — deferred (2026-09-15): backstory waits until later in development. Both are complete in `COMBAT_SYSTEMS.md` §10.6 and §10.7, both are contractors, and Nuetu's piece is the disc.
- [ ] **Javi's entry is stale.** It still says Trauma Plate is unbuilt; §10.5 has him complete.
- [x] ~~**`ART_DIRECTION.md` missing from the repo.**~~ Restored to `docs/art/` from project knowledge (2026-09-15), with its magic rule reconciled to the constraint above.
- [ ] **Finish Javi** — a shield with a per-ability value, which lands with the mitigation pass.
- [ ] **Measure both.** They are in the pool and nothing has ever dealt them: the harness still fields the alpha three, so not one of their four live abilities has run in a simulated match. Three questions wait on it — whether Cryo-Pulse outclasses its peer at the same cost, whether Translocation is as strong a denial tool as it looks, and whether a healer turns a combat game into a race.
- [x] ~~**Ratify or break the house split.**~~ Ratified 2026-09-15: four house, four contractors (Syla, Mimi, Kian, Nuetu), and Luka on his own.
- [ ] Settle the **tech level**. Bouncer has a rope and Kurbyn has a neural-prediction rig; Mimi has a singularity core and tachyon targeting. Those are not the same world. Either the ceiling moves up for everyone or Mimi's fiction comes down to meet the others — but nine operators should not be spread across three centuries by accident.
- [ ] Write the remaining **four operators**, mechanically and in flavour.
- [ ] Backstory and world placement — how the five connect to the casino, to each other, and to whoever runs the house.
- [ ] Strengths, weaknesses, synergies, counters, playstyle notes per operator. Deferred to the balance pass; they are currently implied by the kits rather than stated.
- [ ] Voice and naming conventions for the roster — "Nona" is nine, and the pool is nine. Whether that is coincidence or canon is undecided.
