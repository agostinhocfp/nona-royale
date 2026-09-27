# Nona Royale — Art Direction Bible

> Location in repo: `docs/art/ART_DIRECTION.md`
> Status: Draft v6 — title, tech-first genre and two-register model CONFIRMED & locked; **the rendering style amended 2026-09-27 from flat Dark Deco cel to Dark Deco, rendered, against the visual target `docs/art/VISUAL_TARGET.png` (§2.2, §10 reference 5)**; palette pending visual lock; 2D locked (ADR-0001).
> Related: `docs/art/ART_PIPELINE.md` (tooling + workflow), `docs/art/ART_PROMPTS.md` (the prompt pack, v4.1 — its style block predates the 2026-09-27 amendment), `docs/art/VISUAL_TARGET.png` (the visual target), ADR-0001 (2D rendering), ADR-0003 (board topology), ADR-0009 (2.5D character production), ADR-0011 (Bouncer's device), `docs/design/OPERATORS.md` (who the operators are).

This is the single source of truth for how Nona Royale looks. The pipeline doc says *how* art gets made (tools, Unity import, licensing); this says *what* it should look like. When the two ever conflict, this wins on aesthetics, the pipeline doc wins on process.

---

## 0. Resolved inconsistencies (read first)

- **Title.** Canonical is **Nona Royale** ("Nona" = nine, matching the original 9-operator pool). "Mystic Heist" is dead. **LOCKED 2026-07-10.**
- **Genre — grounded crime-noir, tech-enabled. LOCKED 2026-07-10.** The world is **grounded crime-noir Art Deco** (John Wick, Gatsby, Sin City). The governing analogy is **Marvel**: grounded-but-awesome, where **near-future tech is the enabling engine for all the spectacle** while the world stays rooted. Tech *justifies* the powers; it is the *how*. Crime-noir Deco is the *what and the mood*. Literal fantasy/arcane magic is **not** the default — but the door stays cracked for **rare, very light** magical touches, always drawn in the cool register (§2.1). Lethe is the one figure the allowance is currently parked on (`OPERATORS.md`), and it costs nothing until it is spent.
- **Rendering style — realism rejected; Dark Deco cel LOCKED 2026-09-19; amended 2026-09-27 to Dark Deco, rendered.** See §2.2. Realism failed on four counts, recorded here so it is not relitigated: fidelity raises the motion bar past what the project can animate; board scale (the squint test is 64 px) destroys realistic detail; a frame that is ~70% near-black turns a realistically-rendered figure into a smear, which is exactly what killed Luka run 1; and realism has no *spec* a solo developer can hit twelve times. **The 2026-09-27 amendment does not reopen realism.** The target is stylized and polished, not photographic, and the four reasons are why §2.2 still demands hard edges, geometric form and a 64 px check.
- **Two registers (warm Deco world / cool tech FX) → deliberate contrast, governed. LOCKED 2026-07-10.** Environments are period Art Deco (warm, gilded, physical, decaying). Tech/ability FX are cleaner near-future (cool, clean, luminous, holographic). The governing rule in §2.1 keeps it looking designed, not inconsistent.
- **Color palette was empty** except "accents not base colors." Filled in §3. **Tone is approved**; the **exact hex values are pending** a dedicated palette pass.
- **"Decay"** = material and moral decay (tarnish, cracks, stains, smoke), and by extension light **tech decay** (flickering displays, corroded devices) — never arcane corruption.
- **Bouncer's rope is dead. ADR-0011, 2026-09-19, amended the same day.** His device is a telescoping segmented mechanical **gauntlet** worn from elbow to fingertip over his own arm — strapped on, removable, not a prosthetic. The ability keeps the name *Velvet Rope*. A rope was the one device on the roster that predated the genre lock and never got revisited, and it argued against its own Atomic damage type.

---

## 1. One-line identity

Nona Royale is a stylized 2D board game set in a criminal underworld dressed as a high-society Art Deco casino — opulent, decaying, shot in deep noir shadow — where operators wield sleek near-future tech to fight, scheme, and race home.

## 2. Core aesthetic pillars

- **Clean lines.** Sharp, confident silhouettes, minimal noise. Reads instantly at board scale.
- **Dark glamour.** Deep shadow as base; metallic sheen, velvet, leather, gold as highlights emerging from it.
- **Art Deco elegance.** Geometric symmetry, bold repeating motifs, gilded ornament, stepped forms, tall diamonds, radial fans.
- **Sinister charm.** Alluring and dangerous at once — beauty with a threat under it.
- **Opulent decay.** Gold tarnishes, marble cracks, velvet stains, smoke hangs. Wealth past its peak.
- **Engineered edge.** Powers come from technology, not magic — a clean, modern, luminous layer that reads as *engineered* against the warm old-world grain.

### 2.1 Governing rule — the two registers

| | Environment / world | Tech / ability FX |
|---|---|---|
| Temperature | Warm (gold, amber, maroon) | Cool (cyan, white, pale blue) |
| Surface | Physical, textured, tarnished | Luminous, holographic, clean |
| Form | Ornate, gilded, Deco geometry | Precise, minimal, near-future |
| Feel | Old-world, decaying | Modern, intrusive, sharp |

The tech should feel like it *doesn't belong* in the gilded room — that friction is the aesthetic. Very light magical touches, when used, lean toward the tech register rather than the ornate one, so they don't muddy the noir.

### 2.2 Governing rule — the rendering style is Dark Deco, rendered

**Amended 2026-09-27 (designer). Supersedes the flat Dark Deco cel spec LOCKED 2026-09-19.** The visual target is **`docs/art/VISUAL_TARGET.png`** (§10 reference 5): the board as a crafted object with weight, light and polish, in the finish of *Hearthstone* and *Marvel Snap*, on Dark Deco's shape language. Everything drawn in this game — board, frame, props, UI chrome and operators — is judged against that image.

**What carries over from the cel spec.** The Dark Deco *shape language* stays: geometric Deco forms, strong silhouettes, a dark ground with gold and jewel tones emerging from it, and the two registers. What changes is the *finish*: surfaces are now modelled and lit, not three flat values.

Five rules, the spec as the target shows it:

1. **Modelled form, hard edges.** Volumes carry soft shading and gradients; a surface may have as many values as it needs to read as a solid object. Every silhouette and every major plane break stays a **crisp, hard edge**. Soft shading inside a shape, never a soft outline around it.
2. **One ink colour.** Silhouettes and major forms are outlined in `#1C0E12`. The weight may vary to separate planes; no crosshatching, no sketchy interior lines.
3. **Form is geometric.** Unchanged from the cel spec. Bodies and objects are built from primitives — wedge torso, blocked square shoulders, tapered limbs, simplified oval head; stepped frames, tall diamonds, radial fans. Anatomy and ornament are simplified *into* Deco shapes.
4. **Materials are told, at low contrast.** Marble veining, felt nap, velvet sheen, brushed and polished metal are allowed, and they are what give the target its weight. Metal and lacquer carry glossy specular highlights and **bevelled edges**. Texture stays low-contrast: it may never compete with a piece standing on it at 64 px.
5. **Light is placed, and it has depth.** A warm gold key from the upper left, warm local pools from lamps and candles, cool light only from tech. Contact shadows and soft drop shadows under raised objects (frames, tables, chips, pieces) are what make the board feel physical. Ambient stays dark.

**Relaxed with the amendment (designer, 2026-09-27).**

- **A little always-on cyan in the frame's hardware is allowed** (the target's top plaque and side tube): it gives the table life. It stays scarce, it never becomes a surface, and it never appears on a figure at rest (§5, devices dark at rest).
- **Props and dressing** (candles, chips, plants, brass instruments, frame fittings) are welcome **if they are well done**, and count toward the gold budget in §3.

**What the target does not decide.** It is a visual target, not a layout or a UI spec.

- **The grid is the code's**: 15×15, arms of 6, home columns of 6 (ADR-0002, ADR-0003, `BoardLayout`). Where the painting and the grid disagree, the grid wins. In particular the centre medallion must leave each home column's last cell full size.
- **Its pawns are placeholders.** Operators are figures, three per seat.
- **Its frame slots and tray are not UI.** The HUD is designed in `PRESENTATION.md` and `PHYSICAL_UI.md`, and it can change until launch.

**Characters.** The target shows no operators, so the rule for them is the finish, not a picture: an operator must look like it belongs on that table, lit the same way and polished to the same degree. Reference 4 (Bouncer) stays the reference for **shape, silhouette and value** only, not for finish. **This is the *look*; ADR-0009 is still the *production method*** (Meshy model, Blender render). Its toon ramp and outline settings now serve these five rules, and if a render breaks one of them, the render is wrong, not this section.

Where full production quality comes from is recorded in `MOTION.md` and ADR-0009: the 3D rig supplies interpolation for free, timing and secondary motion do most of the perceived work, and VFX in the cool register carry the rest. The target adds one more: **light and depth in the engine** (URP 2D lights, drop shadows, sheen), which the board already has the hooks for.

**Written against the cel spec, and due a pass** (open item below): `ART_PROMPTS.md` v4.1 (its style block and negative prompt fence off gradients and rendering), `OPERATOR_LOOKBOOK.md` (the heraldic vector approach is built on three flat values), `PHYSICAL_UI.md` ("cel skeuomorphism", three values per material), `STYLE.md` (round 1 recipes judged against the cel rules), and the Blender toon-ramp settings in `tools/blender/`.

## 3. Color palette

Grounded, rich, shadow-heavy. Accent tones are *accents*, never base colors.

| Role | Name | Hex (proposal) | Use |
|------|------|------|-----|
| Base darkest | Obsidian | `#0A0709` | Backgrounds, deepest shadow |
| Base dark | Charcoal velvet | `#141013` | Floor fields, panels |
| Neutral | Gunmetal | `#1A1519` | Mid surfaces, stone |
| Primary metal | Gilt gold | `#C99A3C` -> hi `#F4D98B` | Trim, frames, ornament, key accents |
| Tarnish | Aged brass | `#7C5A1E` | Decay on gold, low-light metal, **all operator hardware** |
| Jewel accent A | Blood velvet | `#5A1626` | Carpet, drapery, danger cues |
| Jewel accent B | Deco emerald | `#0F6E56` | Secondary accent (felt, glass) — sparing |
| Ink line | Deep maroon-black | `#1C0E12` | Character/tile outlines |
| **Tech accent** | Holo cyan | `#5FE0E8` -> hi `#C9FBFF` | **Ability FX, holographic UI states, powered tiles — FX only, never a surface color.** A little always-on cyan in frame hardware is allowed (§2.2, 2026-09-27) |

**In code:** these values live in `Assets/_Project/Scripts/Unity/View/UiTheme.cs`. Locking the palette means editing that file; nothing else in the view holds a colour.

Rough mix per frame: ~70% dark neutrals, ~20% gold, ~10% one jewel tone. The tech cyan appears only when something is *powered/active*, plus the small allowance in the frame — its rarity is what gives it meaning. The visual target keeps this mix: a near-black cross and frame, gilt edges and fittings, and one jewel tone per seat.

**Two rules the first operator renders forced (2026-09-19):**

- **Operator metal is aged brass, never gilt.** Bouncer's gauntlet and any other sizeable metal on a figure use `#7C5A1E` with tarnish. The first renders came back in bright polished gold, which put a large gilt mass on operators who are not Fortuna and quietly spent her whole distinction. **Fortuna is the only figure whose metal is gilt, and the only one permitted to exceed the gold budget** — being over-gilded is how she reads as the room's owner rather than its staff.
- **No decorative gold trim on contractors.** Syla's first pass carried gold edging on the cape and pods. It came off. Her frame is black and her accent is cyan.

**Jewel tones as garments are allowed on operators**, because a figure occupies a small fraction of a frame: Kian wears Deco emerald and Revú wears oxblood, and each is the only operator who does. Revú's oxblood must be clearly **darker** than `board_carpet.png` (§6.1 forbids a red operator on blood velvet).

## 4. Moodboard anchors

- **`docs/art/VISUAL_TARGET.png`** — the visual target (§10 reference 5). Above every anchor below.
- **Hearthstone / Marvel Snap** — the finish anchors (added 2026-09-27): objects with physical weight, bevelled gold, glossy speculars, warm local light, polish on every surface, and depth from drop shadows. The standard the board and the UI chrome are held to.
- **Batman: The Animated Series / Arkham** — the shape-language anchor. Dark Deco: ink-and-black shape language, characters as a strong silhouette on a dark ground. No longer the finish reference (§2.2).
- **Hades** — proof that dark, gold-heavy, jewel-tone stylization reads perfectly at small figure scale with modest frame counts.
- **Persona 5** — UI confidence and angular energy; also the clearest demonstration of 3D-toon characters animating smoothly without ever looking 3D. Its clean stylized power FX are the tech-register reference.
- **Darkest Dungeon** — a turn-based game that got enormous stylistic impact out of near-zero animation budget.
- **Lackadaisy** — line quality for period formalwear.
- **Bioshock** — Rapture Deco, for environment ornament and mood, **NOT** for the tech register.
- **Baz Luhrmann's *Gatsby*** (gilded excess), **John Wick** (tailored lethal elegance), **Sin City** (noir contrast), casino/poker table motifs.

## 5. Characters (operators)

- **Proportions:** 6 heads tall, noticeably large head and hands, short sturdy legs, thick limbs, no thin fragile details. Slightly exaggerated comic-book stylization — not cartoonish. **Generators do not deliver this** (five runs of evidence); proportions are fixed by construction in Blender under ADR-0009.
- **Rendering:** the five rules of §2.2, judged against reference 5 for finish and reference 4 for shape, silhouette and value.
- **Outlines:** deep maroon-black (`#1C0E12`), consistent across the cast.
- **Stance:** elegance or power even when idle — every operator holds itself with intent.
- **Clothing:** tailored fitted formalwear, lightly embellished; carries **one discreet piece of tech** that hints at the ability without breaking the formalwear silhouette. A device that has to break the silhouette is the wrong device (ADR-0011 rejected a prosthetic arm on exactly this rule).
- **Expressions:** subtle but readable.
- **Silhouette-first:** identifiable in pure black silhouette at board scale.
- **Ability tells:** when powered, an operator's tech reads in the cool register (§2.1) — a device lights, a holographic cue appears — distinct from the warm environment.
- **Devices are DARK at rest (added 2026-09-19).** Cyan means *powered*, and §3 says its rarity is what gives it meaning. The standing and seated board sprites carry **no cyan at all**; the light arrives with the cast tell and leaves with it. Character sheets and portraits may show the device lit, because their job is to establish where the light goes — generate both states and ship the unlit one to the board. Every figure in the first batch came back glowing while standing idle, so this has to be stated in the prompt rather than assumed. The 2026-09-27 frame-cyan allowance does not extend to figures.

### 5.1 The value ledger — assigned, not discovered

**Added 2026-09-19.** The single largest readability risk in this game is twelve dark figures on a near-black floor. §6.1 already says piece readability beats scene richness; that only holds if **every operator has a different value solution, assigned in advance**. The ledger stands under the 2026-09-27 amendment: modelled shading changes how a figure is lit, not which value mass it owns.

| # | Operator | Value solution | Silhouette | Dominant accent |
|---|---|---|---|---|
| 1 | Luka | warm light torso (camel) | four-point X, forward wedge | cyan signet ring |
| 2 | Syla | light core in a dark frame | downward triangle | cyan drone slits |
| 3 | Bouncer | black mass split by a hard white V | wide low slab | aged-brass gauntlet |
| 4 | Kurbyn | mid-dark, broken by bare forearms | coiled four-point | brass + nape cyan |
| 5 | Javi | dark waistcoat block, two white sleeves | upright cross | frosted canister seals |
| 6 | Sanity | large mid-brown mass | octagon | brass + prod cyan |
| 7 | Mimi | near-black and small, bright pale hardware | small dart | frosted white rig |
| 8 | Fortuna | the only gold-dominant figure | diamond (under review) | gilt gold |
| 9 | Revú | the only red torso | three-point barbed hook | oxblood |
| 10 | Kian | the only green torso, outline breaks upward | five-point spiked crown | emerald + cyan rod tips |
| 11 | Nuetu | the only all-light mass, cool dove-grey | disc | black plates on grey |
| 12 | Lethe | the only true mid-grey figure | six-point spark | tarnished silver |

**Four standing rules fall out of it:**

- **The light torso is Luka's.** No other operator carries a warm light torso. Syla's ivory is a core inside a black frame, and Nuetu's dove-grey is cool and reads as a mass rather than a garment. This is why Javi wears a charcoal waistcoat over his bone-white shirt.
- **The gilt is Fortuna's.** Every other operator's metal is aged brass (§3). This rule was written because the first Bouncer render spent her distinction in one pass.
- **Two operators read by shape rather than value.** Mimi is solved by being the smallest figure with the brightest hardware; Lethe by being the only mid-grey and the only silhouette with radiating upper points.
- **The board must be checked under the pieces, not beside them.** Revú on blood-velvet carpet and Luka on lit gold inlay are the two known collisions. Both are Play Mode checks.

## 6. Environments & tiles

Vault-beneath-a-casino hybrid: gold under shadow, danger behind elegance.

- **Floor tiles:** inlaid marble, velvet runners, patterned gold trim.
- **Backgrounds:** soft gradients, layered silhouettes of ornate structures receding into dark.
- **Materials:** gold, glass, velvet, obsidian — each with hints of decay.
- **Modularity:** grid-based, interchangeable, symmetric, seamless looping; minimal seams on snap-to-grid.
- **Depth layering:** foreground tile -> light/FX overlay -> background silhouette.

### Geometry & material rules
- Motifs: hexes, tall diamonds, sharp arches, stepped corners.
- Symmetry with a subtle deliberate break (one cracked length of gold trim).
- Reflective surfaces glow in darkness but never overpower clarity.

### Light & shadow
- Low ambient; **focal lighting** on key tiles, detail falling into shadow for mystery.
- Reflections and silhouettes in glossy floor panels.

### Contrast discipline (readability)
- Tile silhouettes must contrast with operators on them — e.g. **no blood-velvet tiles under a red operator.** Piece readability beats scene richness, always. See §5.1.
- **Powered/tech tiles use the cool cyan register** so they pop against warm gold surroundings — this doubles as gameplay clarity.

### 6.1 World-as-place rules

- **Atmospheric at rest, functional on demand.** The world is a moody, low-light place when nothing is happening — the path is a run of raised dark tiles with thin gilt edges, held at a lower contrast than the visual target so the turn's highlights still read over them; the home columns take their seat colour with a gilt ring per cell; the yards are round felt gaming tables; HOME is a black medallion with a gilt emblem, not a vault door. Legibility is delivered *on demand*: the cells relevant to the current turn light up in the cool tech register on your turn or on hover, then fade back. Never light the whole grid at rest. *(Decided 2026-09-27, `BOARD_SKIN.md` D1 and D2: tiles at lower contrast replace the 2026-09-15 "whisper", and the medallion replaces the vault.)*
- **Operators are people who sit and stand.** Before deployment, operators are **seated** around their table (the yard). On deploy they **rise** and step onto the floor. Each operator needs at minimum a seated pose, a standing pose, and a rise transition. Each operator's seated activity is specified per-operator in `ART_PROMPTS.md`; it is characterization, not filler.

## 7. Tile states (animated / overlay)

States are **overlay layers**, not redraws — base tile art stays static and reusable: idle (base, warm register); trap on/off (on = cool tech glow); treasure/objective glow/looted; special-space active (cool register). Every animated tile needs a defined **idle state** so the board isn't busy at rest.

## 8. UI integration

- Frame everything in Art Deco: filigree, windowed borders, corner fans/accents — the gilded case around a vault door.
- Dramatic contrast for clarity — gold on black is the default legibility pairing.
- **Live/interactive UI states** (selection, cooldowns, powered abilities) use the **cool tech register**. Static chrome stays warm/gilded; active states go cool.
- **Icon language:** sharp corners, tall diamonds, angular fans. No soft/rounded shapes.
- **Finish (2026-09-27):** chrome is held to the visual target — bevelled gilt, depth and drop shadows — within the designer's standing call of borderless controls with gold kept for frames.

## 9. What to feed the art tool

Style-bible reference for Scenario/Leonardo: *top-down Art Deco casino-vault board, near-black marble and velvet base, bevelled gilt frames and inlay, warm candlelight pools into deep shadow, glossy speculars on metal and lacquer, soft drop shadows, tarnished gold and cracked marble, polished stylized finish like Hearthstone or Marvel Snap — with sparing cool-cyan holographic tech accents on powered elements.* Attach `VISUAL_TARGET.png` as the style reference. Generate **environment art and tech-FX overlays as separate batches** so the warm and cool registers don't cross-contaminate.

**For characters, `ART_PROMPTS.md` is the operative document**, not this section, but its v4.1 style block was written for the cel spec and must be rewritten against §2.2 before the next batch. Two things it records still hold: text cannot fix proportions or pose, and the generator's job is therefore the **concept sheet and the portrait** — board figures come out of Blender under ADR-0009.

---

## 10. Concept and rendering reference set

1. **Rendered board** — the Ludo board in the Nona Royale palette. A *board composition* reference, superseded for finish by reference 5. **Drawn against the dead 48-cell circuit** — re-check it against 52 before use.
2. **In-game HUD mock** — the canonical *screen composition / UI hierarchy* reference.
3. **Casino-floor world scene** — the north-star "the board is a place" illustration, and the canonical *world feel* reference.
4. **Bouncer, standing (2026-09-19)** — the reference for **shape, silhouette and value** on a figure. It was the canonical rendering reference under the cel spec; since 2026-09-27 its flat finish is not the target. Two known faults in it, corrected in `ART_PROMPTS.md` and not to be copied: the gauntlet is polished gilt rather than aged brass, and the device is lit while he stands idle.
5. **`docs/art/VISUAL_TARGET.png` (2026-09-27) — THE VISUAL TARGET.** An AI-generated board at 1254×1254, chosen by the designer after several rounds. Its grid was checked cell by cell against the game: arms of 6, home columns of 6 starting one cell in from a plain tip, and all four start cells in the right place. **Hold every render — board, frame, props, UI chrome and operators — up against this one for finish, light and weight.** Known departures, not to be copied: four pawns per seat, the centre medallion squeezing the last cell of the left and right home columns to half width, the tray and side slots that look like UI, and the lopsided frame.

When briefing a generator or an artist, the complete brief is: this doc + reference 5 for finish + reference 4 for figure shape and value + references 1–3 for composition.

## Open items to lock
- [x] Title **Nona Royale** — LOCKED.
- [x] Two-register model — LOCKED.
- [x] Tech-first, with the door open for rare, very light magic — LOCKED; currently parked on Lethe.
- [x] **Rendering style** — LOCKED 2026-09-19 as Dark Deco cel; **amended 2026-09-27 to Dark Deco, rendered** (§2.2).
- [x] **The roster value ledger** — assigned 2026-09-19 (§5.1), all twelve operators.
- [x] **Produce a rendering reference** — Bouncer, 2026-09-19 (§10 reference 4).
- [x] **A visual target** — `VISUAL_TARGET.png`, 2026-09-27 (§10 reference 5).
- [ ] **Bring the cel-era docs up to §2.2:** `ART_PROMPTS.md` (style block, negative prompt), `OPERATOR_LOOKBOOK.md`, `PHYSICAL_UI.md`, `STYLE.md`, and the Blender toon-ramp settings in `tools/blender/`.
- [ ] **A character rendered to the target.** The first operator render at the new finish becomes the figure reference that replaces reference 4's finish.
- [x] **§6.1 "whisper" against the target** — decided 2026-09-27 (`BOARD_SKIN.md` D1): gilt-edged tiles at a lower contrast than the target. The centre is a medallion with no vault (D2).
- [ ] Lock the §3 hex palette, especially the **holo cyan** tech accent. *(Pending a visual sign-off in Play Mode.)*
- [ ] **Fill the tile-type catalog** — see §11.
- [ ] **Re-check concept reference 1 against the 52-cell board.**
- [ ] **Test Fortuna's silhouette at piece size** against the unclaimed eight-pointed chip notch. `OPERATORS.md` flags her diamond as the weakest distinction on the board.

## 11. Tile catalog (structure set; contents pending)

Every tile has two orthogonal aspects, and both follow §2.1:

- **Gameplay type** — what the cell *does*, per ADR-0003: **Normal**, **Safe (start/track)**, **Safe (home-entry)**, **Home-column**, and the deferred **Special/rare** spaces.
- **Art state** — the overlay layers a tile can show (§7). Base tile art stays static; powered states use the cool tech register.

Plus **structural / art-only elements** that are not gameplay cells: floor fields, walls/panels, the central medallion (HOME), decorative trim.

> **Contents deliberately left blank.** An earlier (2024) brainstorm proposed fire traps, ice traps and "arcane" treasure tiles. Those are **stale and off-canon** — fantasy/elemental framing that conflicts with the locked grounded-tech genre. Fill this catalog from the *current* special-space design.

---

## Status history

- 2026-07-10 — Title, genre and two-register model locked (§0).
- 2026-09-15 — **Restored to the repo** from project knowledge; pipeline path corrected; §10 reference 1 flagged against the dead 48-cell circuit; Luka's ring added to §0; the magic rule reconciled with `OPERATORS.md`.
- 2026-09-15 — The §3 proposals were put into `UiTheme.cs` as the working palette. Still proposals; the lock is a visual sign-off in Play Mode.
- 2026-09-19 — **The rendering style is named and locked (§2.2): Dark Deco cel.** Realism considered and rejected, with the four reasons recorded in §0.
- 2026-09-19 — **New §5.1, the roster value ledger.** All twelve operators assigned a value solution, a silhouette and a dominant accent.
- 2026-09-19 — §0 records ADR-0011: Bouncer's rope replaced by a segmented mechanical arm; §5's discreet-device rule is what rejected the prosthetic version.
- 2026-09-19 — §4 reordered with Batman: The Animated Series promoted to primary anchor; §9 defers to `ART_PROMPTS.md` for characters.
- 2026-09-19 — **First render batch (Luka, Bouncer, Syla) reviewed.** The style landed; three amendments came out of it. §10 gains **reference 4**, the first true rendering reference. §3 gains the **aged-brass rule for operator hardware** and the **no-gold-trim-on-contractors** rule, after bright gilt on Bouncer's gauntlet and gold edging on Syla's cape spent Fortuna's distinction. §5 gains **devices are dark at rest** — every figure in the batch was glowing while standing idle, which is the opposite of what §3 says cyan is for. ADR-0011 was amended in the same pass: the hip spool is dropped for a worn elbow-to-fingertip gauntlet.
- 2026-09-27 — **Visual target locked; §2.2 amended from flat cel to Dark Deco, rendered** (designer). `docs/art/VISUAL_TARGET.png` becomes §10 reference 5 and the top anchor in §4, with Hearthstone and Marvel Snap added as finish anchors. The five rules are rewritten around it: modelled form with hard edges, one ink colour, geometric form (unchanged), low-contrast materials with bevels and speculars, and placed light with drop shadows. The shape language, the two registers, the palette and its 70/20/10 mix, the value ledger and devices-dark-at-rest all stand. Two relaxations, both the designer's: a little always-on cyan in frame hardware, and props that are well done. Reference 4 keeps shape and value only. Realism stays rejected (§0). Five cel-era docs and the Blender toon settings are listed for a pass, and the §6.1 "whisper" rule is opened against the target. **Also restores v5 to the repo:** `docs/art/ART_DIRECTION.md` had stayed at the 2026-09-15 v4 while the project copy moved to v5 on 2026-09-19, so the repo never had §2.2, §5.1 or the aged-brass rules that `STYLE.md`, `PHYSICAL_UI.md` and `OPERATOR_LOOKBOOK.md` cite.
- 2026-09-27 — **§6.1 decided against the target** (designer, `BOARD_SKIN.md` D1–D3). The path at rest is gilt-edged tiles at a lower contrast than the target, not a whisper; highlights must still pop over them. HOME is a black medallion with a gilt emblem and the vault door is dropped, so the win moment moves off the centre. The yard chairs are a sprite (`yard_chair`). §11's "central vault door" becomes the medallion.
