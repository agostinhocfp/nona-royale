# ADR-0011 — Bouncer's device: a segmented mechanical gauntlet, not a rope

- **Status:** Accepted 2026-09-19 (designer). **Amended 2026-09-19** after the first render: the hip spool is dropped and the device is a worn gauntlet from elbow to fingertip. Fiction and art only; **no code change**.
- **Date:** 2026-09-19
- **Location:** `docs/decisions/0011-bouncer-segmented-arm.md`
- **Relates to:** `COMBAT_SYSTEMS.md` §10.1 (Velvet Rope, Intimidating Presence), §2.2 (Atomic and Evasion), `OPERATORS.md` (Bouncer), `ART_DIRECTION.md` §0, §5 and §5.1, `ART_PROMPTS.md` v4.1, `MOTION.md` decision 3 (cast tells), ADR-0001 (tech-first genre).

## Context

Bouncer's first ability has always been fictionally a rope. Two problems.

**It is off-genre.** `ART_DIRECTION.md` §0 locks the setting as grounded crime-noir where **near-future tech is the enabling engine for all the spectacle**, and the design constraint in `OPERATORS.md` says every ability should have a plausible near-future device behind it. Every other operator complies: a neural-prediction rig, a cryogenics rig, a nanite bandolier, a signal-spoofing ring, a charged prod, emitter rods. Bouncer had a length of rope. He was the one operator whose device predated the genre lock and never got revisited.

**It does not justify its own mechanics.** §10.1 makes Velvet Rope **Atomic**, which makes Bouncer the roster's direct counter to Evasion and the **only single-cast route through Evasive Protocol** — the answer to an opponent who cannot be hit. A rope is the most dodgeable object that could possibly have been chosen for that job. The mechanic has been carrying fiction that argues against it.

## Decision

**Bouncer's device is a telescoping segmented mechanical gauntlet** — interlocking brass-cased steel segments that extend with snake-like grace and pinpoint precision.

1. **It is worn from elbow to fingertip, and it is not his arm.** A heavy segmented brass vambrace over his right forearm, ending in a segmented brass hand, strapped on over his own arm and removable. The telescoping length lives inside the forearm housing and slides out of it, segment from segment.

   **This is the amendment.** The original decision put the coiled segments in a flat gilded spool at the right hip with a brass cuff as the grip. The first render showed the spool reads as nothing at board scale — a saucer-sized disc is three pixels of gold — while a full brass forearm is legible at 64 px as *the device*, instantly. The designer removed the spool on sight, and they were right.

2. **Worn, not installed** — the distinction the amendment preserves, and it is load-bearing. A prosthetic limb pushes the tech ceiling into cybernetics while the tech-level question in `OPERATORS.md` is still open, and it costs the character: "he decides with his body, and the machine is what he reaches with when a body is not enough." A gauntlet he straps on is a tool in the same class as Javi's gloves and Sanity's prod, and it looks identical to what was rendered. **He can take it off**, which is a free beat for the seated pose.

3. **The ability keeps the name Velvet Rope.** The doorman's line made literal as a brass serpent, named by the same house technicians who called Luka's ring *Hermes*. Consequence: the ability id, the `Bouncer.cs` string, the bot tests and every measured sweep row stay untouched.

4. **Aged brass, never polished gilt.** The gauntlet is a large metal mass on a figure who is not Fortuna, and `ART_DIRECTION.md` §5.1 gives her the gold-dominant slot. It is rendered in tarnished aged brass (`#7C5A1E`), a working machine rather than jewellery. The first render came back bright gilt; that is the one thing to fix on it.

5. **The deployed arm is drawn at exactly the ability's range**, and the segments **bunch and compress** rather than reading as infinitely extensible. A visibly long machine invites players to expect more reach than the stats give; the length is a visible promise, so it has to be the true one.

6. **Reach and art are now coupled.** §10.1 already couples reach to the Intimidating Presence radius ("if one moves, move the other") and names reach as the first thing to restore if he reads weak. Add the art to that list.

7. **Cast tell:** the gauntlet's segments separate and pour out across the floor in a fast low S-curve with holo cyan light running in the seams, the hand clamps on the target, hit-stop fires, and the whole assembly snaps back dragging the target to the adjacent cell. **His feet never move.** The slowest operator on the board with the fastest tool on the board is a better reading of *the one you route around* than a rope ever was.

8. **Dark at rest.** The seams carry no cyan on the standing and seated sprites; the light arrives with the cast (`ART_DIRECTION.md` §5).

## Consequences

- **Zero code churn.** Because the name holds, nothing in `Core` changes: no ability id, no roster line, no test, no bot weight, and no sweep row is invalidated.
- **The Atomic damage type now has a reason.** A machine that corrects mid-flight beats a man who dodges. §10.1's counter-pick relationship reads as designed rather than arbitrary, and that was the strongest argument for the change.
- **Board readability improved over the original decision.** The amendment is not a compromise — the gauntlet is the better device at sprite scale, and the spool was a mistake in the first draft of this ADR.
- **Animation gets cheaper.** A rope needs a physics sim or a hand-faked curve. A segmented arm is a chain of transforms walked along the path cells, and the segments are a short bone chain parented to the existing rig — buildable with the existing `FxSprite` and `CastTell` machinery, no new systems.
- **The tech ceiling does not move.** A worn mechanical tool sits beside Bouncer's peers and well below Mimi's singularity core. This decision deliberately adds no data point to the open tech-level question — which is exactly what the worn-not-installed reading buys.
- **His silhouette is unchanged.** The gauntlet stays inside the arm's outline, so the wide low slab that makes him readable at board scale survives.
- **`ART_PROMPTS.md` needs a per-operator negative for him.** Image generators see "mechanical arm" and produce a chrome cyborg; his negative block adds `cyborg, robot arm, full prosthetic arm, missing arm, mech suit, exposed wires, exposed cables, hoses, tentacle`. Note that the first render still came back reading as a prosthetic, so the prompt now says **worn over his own arm, strapped, removable** in the character block itself rather than relying on the negative alone.

## Follow-up edits (not done by this ADR)

`COMBAT_SYSTEMS.md` is the mechanical source of truth and carries the old fiction in prose. Four places say "rope" as an object rather than as the ability's name:

- §10.1, the paragraph beginning "Reach and aura are one kit" — "the rope went to 4 and back".
- §10.1, the Velvet Rope / Evasion paragraph — the object is described as a rope.
- The retune table row, "Velvet Rope as 3 Normal at range 3".
- The All-In Mauling reprice comparison — "rope into maul" reads as the object.

None of these change a number. The ability's **name** may keep appearing everywhere; only the noun for the physical object changes.

## Options considered

- **Keep the rope.** Cheapest, and wrong for the two reasons in Context. It was never argued for; it was inherited.
- **Rename the ability** to something technical and demote "Velvet Rope" to a nickname. Costs an ability id, a roster edit and a sweep of the bot tests, and loses the best name in the game in exchange for precision nobody asked for.
- **A hip spool plus a forearm cuff** — the original decision, superseded. Correct in principle, illegible in practice.
- **A full prosthetic arm.** What the first render actually produced. Rejected: it moves the tech ceiling and costs the character, for a picture that is identical to the gauntlet.
- **A back-mounted spine deploying over the shoulder.** More dramatic at rest, but it adds a hump to the one silhouette on the board whose entire job is to be a clean wide slab.

## Status history

- 2026-09-19: Accepted, from the designer's note that "a rope is lame in a sci setting". The Atomic justification was found while writing this ADR and is the stronger reason of the two.
- 2026-09-19: **Amended after the first render.** Hip spool dropped; the device is a worn elbow-to-fingertip gauntlet. Worn-not-installed is kept as the governing reading, and the brass is specified as tarnished rather than gilt.
