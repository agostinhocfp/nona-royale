# ADR-0011 — Bouncer's device: a segmented mechanical gauntlet, not a rope

- **Status:** Accepted 2026-09-19 (designer).
  - **Amended 2026-09-19** after the first render: the hip spool is dropped, and the device is a gauntlet worn from elbow to fingertip.
  - **Amended 2026-09-29:** gunmetal steel, not aged brass (decision 4).
  - **Amended 2026-10-03:** the original concept is archived to match today's character, the chip piece with the steel gauntlet. The live ability is described under **Today's Apophis**, and everything it replaced is under **Archive**.
  - **Amended 2026-10-03, again:** the ability is renamed **Apophis**; it was Velvet Rope (decision 3).
  - Fiction, art and names only; **no rules change.** Id 101 never moved, and the rules fingerprint carries no names.
- **Date:** 2026-09-19 (amended 2026-09-29 and 2026-10-03)
- **Location:** `docs/decisions/0011-bouncer-segmented-arm.md`
- **Relates to:**
  - `COMBAT_SYSTEMS.md` §10.1 (Apophis, Intimidating Presence) and §2.2 (Atomic and Evasion).
  - `OPERATORS.md` (Bouncer); `ART_DIRECTION.md` §0, §3, §5 and §5.1; `ART_PROMPTS.md` v4.2+.
  - `MOTION.md` decision 3 (cast tells); `AUDIO.md` decision 1 (sound slugs).
  - ADR-0001 (tech-first genre), ADR-0013 (chip pieces), ADR-0014 (operator glow colours).

## Context

Bouncer's first ability was always fictionally a rope. That caused two problems.

**It is off-genre.** `ART_DIRECTION.md` §0 locks the setting as grounded crime-noir where **near-future tech is the enabling engine for all the spectacle**. The design constraint in `OPERATORS.md` says every ability should have a plausible near-future device behind it, and every other operator complies: a neural-prediction rig, a cryogenics rig, a nanite bandolier, a signal-spoofing ring, a charged prod, emitter rods. Bouncer had a length of rope. He was the one operator whose device predated the genre lock and never got revisited.

**It does not justify its own mechanics.** §10.1 makes the ability (then Velvet Rope, now Apophis) **Atomic**. That makes Bouncer the roster's direct counter to Evasion and the **only single-cast route through Evasive Protocol**: the answer to an opponent who cannot be hit. A rope is the most dodgeable object that could possibly have been chosen for that job, so the mechanic was carrying fiction that argued against it.

## Decision

**Bouncer's device is a telescoping segmented mechanical gauntlet**: interlocking steel segments that extend with snake-like grace and pinpoint precision.

1. **It is worn from elbow to fingertip, and it is not his arm.** It is a heavy segmented vambrace over his right forearm, ending in a segmented glove, strapped on over his own arm and removable. The telescoping length lives inside the forearm housing and slides out of it, segment from segment.

2. **Worn, not installed.** This distinction is load-bearing.
   - A prosthetic limb would push the tech ceiling into cybernetics while the tech-level question in `OPERATORS.md` is still open.
   - It would also cost the character: "he decides with his body, and the machine is what he reaches with when a body is not enough."
   - A gauntlet he straps on is a tool in the same class as Javi's gloves and Sanity's prod. **He can take it off**, which gives the seated pose a free beat.

3. **The ability is named Apophis** (designer, 2026-10-03; it was Velvet Rope, which is now in the Archive).
   - Apophis is the serpent of chaos that lies across the sun's road through the underworld every night and tries to stop it passing. That is a doorman: the thing at the gate that decides whether you get through. It is also the segmented gauntlet, uncoiling across the floor like a snake.
   - The house technicians name their machines after gods, as with Luka's *Hermes'* Ring and Lethe's *Eris'* Exploit. The possessive follows the same form: Apophis'.
   - **Id 101 never moved.** Replays, bot weights and sweep rows are keyed by id, and the rules fingerprint carries no names, so it stays `a48228fb`.
   - The code followed the name: `Bouncer.Apophis`, the sound slug `Bouncer_Apophis` (no recorded file used the old one), `SignatureRecipes.ApophisTell` and `ApophisImpact`, and the test names.

4. **Gunmetal steel** (amended 2026-09-29; it was "aged brass, never polished gilt").
   - The gauntlet is dark gunmetal steel: `#3B3F45`, highlight `#A7B0BA`. It is the one exception to the operator aged-brass rule (`ART_DIRECTION.md` §3).
   - The reason: in the chip portraits a big brass fist read as Thanos.
   - His house pin stays dull brass. The livery is brass; the machine is steel.

5. **Dark at rest.** The seams carry no light on any resting image: chip, standing or seated. The light arrives with the cast, in the house cyan (`ART_DIRECTION.md` §5, ADR-0014).

## Today's Apophis (2026-10-03)

Since ADR-0013, every operator is a chip lying on the table, and the 2D rigs are gone (2026-10-01). The ability is presented like this:

- **At rest:** the chip portrait (`art/source/characters/bouncer/bouncer_chip_unlit.png`) shows the steel gauntlet with dark seams. Under the Figures option, his look-book figure wears it in the same gunmetal (`BouncerLook`, `UiTheme.LookBouncerSteel`).
- **The tell:** the shared cast tell that every ability uses (`CastTell`: the sweep on the caster and the line to the target). The chip lights its portrait (`bouncer_chip_lit.png`: cyan in the gauntlet's seams), and the rim flares cyan on the caster and amber on the target.
- **The pull:** the target is placed on the cell adjacent to him (§7.4). Placement, not a walk: the piece settles there, with no path drawn. **His chip never moves.**
- **The sound:** steel plates unlocking down the arm, faster as they go, over a hydraulic hiss (`SignatureRecipes.ApophisTell`), then the clamp latching and the target hauled across the felt (`ApophisImpact`). A recorded file named `Bouncer_Apophis_Tell` or `Bouncer_Apophis_Impact` replaces either one (`SFX_PROMPTS.md`).

## Consequences

- **No rules churn.** The id holds, so no roster line, no bot weight and no sweep row is invalidated, and the fingerprint does not move. The rename touched the display name, one property, the sound slug, test names and text.
- **The Atomic damage type now has a reason.** A machine that corrects mid-flight beats a man who dodges. §10.1's counter-pick relationship reads as designed rather than arbitrary, and that was the strongest argument for the change.
- **The tech ceiling does not move.** A worn mechanical tool sits beside Bouncer's peers and well below Mimi's singularity core.
- **His silhouette is unchanged.** The gauntlet stays inside the arm's outline, so the wide low slab that makes him readable survives.
- **`ART_PROMPTS.md` carries a per-operator negative for him.** Image generators see "mechanical arm" and produce a chrome cyborg. His negative block adds `cyborg, robot arm, full prosthetic arm, missing arm, mech suit, exposed wires, exposed cables, hoses, tentacle`, and the character block says **worn over his own arm, strapped, removable**.
- **The name is a god's, not a description.** A new player learns what Apophis does from its card and its pull, as they do with Hermes' Ring; Long Arm would have said it outright. The designer chose the name with that trade in view.

## Archive (2026-10-03)

These are the concepts and the name the ability has had, kept for the record. None is live, and nothing in the game draws, plays or relies on them. The fourth is kept on purpose for the planned 3D version (about three years out), where a figure can do what a chip cannot.

1. **The rope** (to 2026-09-19). A length of velvet rope from the doorman's stanchions, thrown and hauled. Retired for the two reasons in Context.
2. **The hip spool** (first draft of this ADR, 2026-09-19). The coiled segments lived in a flat gilded spool at his right hip, with a brass cuff as the grip. The first render showed that a saucer-sized disc is three pixels of gold at board scale, while a whole forearm reads at 64 px as *the device*. The designer removed it on sight.
3. **The aged-brass gauntlet** (2026-09-19 to 2026-09-29). Tarnished brass (`#7C5A1E`), "a working machine rather than jewellery". The first render came back bright gilt, and the chips then showed that any big gold or brass fist reads as Thanos. Replaced by gunmetal steel (decision 4).
4. **The figure-era tell, and the art coupled to reach** (decisions 5 to 7 of the 2026-09-19 text). Built for standing figures and the 2D rigs:
   - The gauntlet's segments separate and pour out across the floor in a fast, low S-curve, with cyan light running in the seams.
   - The hand clamps on the target and hit-stop fires.
   - The whole assembly snaps back, dragging the target to the adjacent cell, while his feet never move.
   - The deployed arm was drawn at exactly the ability's range, with the segments bunching rather than reading as endless. So the art was coupled to reach: if the range moved, the drawn length moved with it.
   - **Never built.** The rigs it would have hung on were deleted (tag `archive/rigs-2d`, 2026-10-01), and a chip lying flat has no arm to extend.
   - The reach coupling that survives is the rules' own: Intimidating Presence's radius equals Apophis' reach (§10.1).
   - **Revive it for the 3D build:** the figure, the S-curve and the drawn length at the true range are all still the right reading of *the one you route around*, and the serpent uncoiling is now the name too.
5. **The name Velvet Rope** (to 2026-10-03). The velvet rope at a club door, the doorman's line made literal. It was right for a rope, and the 2026-09-19 text kept it for the gauntlet as "a steel serpent", to save an id, a roster edit and a sweep of the bot tests. Neither cost was real: the id was never tied to the name. Once the rope was gone from every picture, the name described an object that no longer existed. The sound slug `Bouncer_VelvetRope` went with it.

## Options considered

- **Keep the rope.** Cheapest, and wrong for the two reasons in Context. It was never argued for; it was inherited.
- **Keep the name Velvet Rope for the gauntlet.** The 2026-09-19 decision, now in the Archive. It saved nothing in the end, and it named the one object the ADR had removed.
- **A descriptive name** (Long Arm, Collared, Velvet Glove and Serpent's Reach were proposed on 2026-10-03). Long Arm says the mechanic outright. The designer chose Apophis instead: a god's name, in the house technicians' habit, that reads as both the doorman and the serpent.
- **A hip spool plus a forearm cuff.** The original decision, now in the Archive. Correct in principle, illegible in practice.
- **A full prosthetic arm.** What the first render actually produced. Rejected: it moves the tech ceiling and costs the character, for a picture that is identical to the gauntlet.
- **A back-mounted spine deploying over the shoulder.** More dramatic at rest, but it adds a hump to the one silhouette on the board whose entire job is to be a clean wide slab.

## Status history

- 2026-09-19: **Accepted**, from the designer's note that "a rope is lame in a sci setting". The Atomic justification was found while writing this ADR and is the stronger reason of the two.
- 2026-09-19: **Amended after the first render.** The hip spool was dropped, and the device became a worn elbow-to-fingertip gauntlet. Worn-not-installed was kept as the governing reading, and the brass was specified as tarnished rather than gilt.
- 2026-09-29: **Gunmetal steel** (designer, chip portraits): a big brass fist read as Thanos. `ART_DIRECTION.md` §3 gained the operator-steel row.
- 2026-10-03: **The original concept is archived to match today's character** (designer). The rope, the spool, the brass and the figure-era tell move to the Archive. The live presentation is written down as built.
  - The follow-up edits this ADR asked for are done: `COMBAT_SYSTEMS.md` §10.1 no longer uses "rope" as the object, and neither do `Bouncer.cs`'s comments or the operator guide's counterplay line.
  - Bouncer's look-book figure, `SFX_PROMPTS.md` and the synthesized signature's notes now say steel.
- 2026-10-03: **Renamed Apophis** (designer: "The Rope concept doesn't make sense anymore, it's a gauntlet"). Decision 3 rewritten, the name Velvet Rope archived.
  - Code: the display name, `Bouncer.Apophis`, the slug and signature recipes, the guide's lines, comments and test names. Id 101 and the fingerprint `a48228fb` are unchanged; the core harness (1022) and the audio tests (46) pass.
  - Docs: `COMBAT_SYSTEMS.md` (outside its dated log), `OPERATORS.md`, `ART_DIRECTION.md`, `ART_PIPELINE.md`, `AUDIO.md` (outside its log), `SFX_PROMPTS.md`, `CORE_GAMEPLAY.md` and `NEXT_PHASES.md`. Dated log entries, ADR-0002 and `OPERATOR_12.md` keep the old name as history.
