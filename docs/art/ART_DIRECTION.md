# Nona Royale — Art Direction Bible

> Location in repo: `docs/art/ART_DIRECTION.md`
> Status: Draft v4 — title, tech-first genre, and two-register CONFIRMED & locked; palette pending visual lock; 2D locked (ADR-0001). Restored to the repo 2026-09-15 from project knowledge (see Status history).
> Related: `docs/art/ART_PIPELINE.md` (tooling + workflow), ADR-0001 (2D rendering), ADR-0003 (board topology), `docs/design/OPERATORS.md` (who the operators are).

This is the single source of truth for how Nona Royale looks. The pipeline doc says *how* art gets made (tools, Unity import, licensing); this says *what* it should look like. When the two ever conflict, this wins on aesthetics, the pipeline doc wins on process.

---

## 0. Resolved inconsistencies (read first)

The raw vision notes carried contradictions (some introduced by old AI-merged text). These are the resolutions applied throughout. Forks marked **CONFIRM** were open questions; those now settled are marked **LOCKED** with the date.

- **Title.** Notes used three names — "Nona Royale," "Mystic Heist," unnamed setting text. Canonical is **Nona Royale** ("Nona" = nine, matching the 9-operator pool). "Mystic Heist" is dead. **LOCKED 2026-07-10.**
- **Genre — grounded crime-noir, tech-enabled. LOCKED 2026-07-10.** The world is **grounded crime-noir Art Deco** (John Wick, Gatsby, Sin City). The governing analogy is **Marvel**: grounded-but-awesome, where **near-future tech is the enabling engine for all the spectacle** (Cap's shield, Iron Man's suit) while the world stays rooted and embraces its own roots without apology. Tech *justifies* the powers and abilities; it is the *how*. Crime-noir Deco is the *what and the mood*. Literal fantasy/arcane magic is **not** the default — but the door stays cracked (as Marvel has Strange and Asgard) for **rare, very light** magical touches. **The door stays open, confirmed 2026-09-15:** tech is the default, and every operator so far has a device behind every ability; a magical touch is an exception to be argued for, and is always drawn in the cool register (§2.1). *Confirmed against the operator design: abilities are neural-prediction systems, gravitic tethers, drone tags, shard emitters, a signal-spoofing ring — clean tech.*
- **Two registers (warm Deco world / cool tech FX) → deliberate contrast, governed. LOCKED 2026-07-10.** Environments are period Art Deco (warm, gilded, physical, decaying). Tech/ability FX are cleaner near-future (cool, clean, luminous, holographic). This contrast is **intentional** — sleek tech intruding on old-world criminal glamour. The governing rule in §2.1 keeps it looking designed, not inconsistent.
- **Color palette was empty** except "accents not base colors." Filled in §3. **Tone is approved** (the concept SVGs in §10 nailed the intended mood); the **exact hex values are pending** a dedicated palette pass in a browser tool, since the old brainstorm palette is archaic and may not match the current vision.
- **"Decay"** = material and moral decay (tarnish, cracks, stains, smoke), and by extension light **tech decay** (flickering displays, corroded devices) — never arcane corruption.

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

Two visual registers coexist, and keeping them **distinct** is what makes the contrast read as intentional:

| | Environment / world | Tech / ability FX |
|---|---|---|
| Temperature | Warm (gold, amber, maroon) | Cool (cyan, white, pale blue) |
| Surface | Physical, textured, tarnished | Luminous, holographic, clean |
| Form | Ornate, gilded, Deco geometry | Precise, minimal, near-future |
| Feel | Old-world, decaying | Modern, intrusive, sharp |

The tech should feel like it *doesn't belong* in the gilded room — that friction is the aesthetic. Very light magical touches, when used, lean toward the tech register (luminous, cool) rather than the ornate one, so they don't muddy the noir.

## 3. Color palette

Grounded, rich, shadow-heavy. Accent tones are *accents*, never base colors — most of any frame is near-black neutral, with gold and one jewel tone doing the talking, and a **cool tech accent reserved for FX only.**

| Role | Name | Hex (proposal) | Use |
|------|------|------|-----|
| Base darkest | Obsidian | `#0A0709` | Backgrounds, deepest shadow |
| Base dark | Charcoal velvet | `#141013` | Floor fields, panels |
| Neutral | Gunmetal | `#1A1519` | Mid surfaces, stone |
| Primary metal | Gilt gold | `#C99A3C` -> hi `#F4D98B` | Trim, frames, ornament, key accents |
| Tarnish | Aged brass | `#7C5A1E` | Decay on gold, low-light metal |
| Jewel accent A | Blood velvet | `#5A1626` | Carpet, drapery, danger cues |
| Jewel accent B | Deco emerald | `#0F6E56` | Secondary accent (felt, glass) — sparing |
| Ink line | Deep maroon-black | `#1C0E12` | Character/tile outlines |
| **Tech accent** | Holo cyan | `#5FE0E8` -> hi `#C9FBFF` | **Ability FX, holographic UI states, powered tiles — FX only, never a surface color** |

**In code:** these values live in `Assets/_Project/Scripts/Unity/View/UiTheme.cs` (GUI increment G, 2026-09-15). Locking the palette means editing that file; nothing else in the view holds a colour.

Rough mix per frame: ~70% dark neutrals, ~20% gold, ~10% one jewel tone. The tech cyan appears only when something is *powered/active* — its rarity is what gives it meaning.

## 4. Moodboard anchors

Bioshock (Rapture Deco — for environment ornament and mood, NOT for the tech register), John Wick (tailored lethal elegance), Baz Luhrmann's *Gatsby* (gilded excess), Persona 5 (UI confidence, angular energy; its clean stylized power FX are a good tech-register reference), Sin City / Arkham (noir contrast, ink shadow), casino/poker table motifs. For the near-future tech FX specifically, lean cleaner/holographic — Persona 5's UI energy over Bioshock's brass.

## 5. Characters (operators)

- **Proportions:** slightly exaggerated, comic-book stylization — not cartoonish.
- **Outlines:** medium-weight deep maroon-black (`#1C0E12`), consistent across the cast.
- **Stance:** elegance or power even when idle — every operator holds itself with intent.
- **Clothing:** tailored fitted formalwear, lightly embellished; may carry a **discreet piece of tech** (an earpiece, a wrist device, a lapel module) that hints at their ability without breaking the formalwear silhouette.
- **Expressions:** subtle but readable.
- **Silhouette-first:** identifiable in pure black silhouette at board scale.
- **Ability tells:** when powered, an operator's tech reads in the cool register (§2.1) — a device lights, a holographic cue appears — distinct from the warm environment.

## 6. Environments & tiles

Vault-beneath-a-casino hybrid: gold under shadow, danger behind elegance.

- **Floor tiles:** inlaid marble, velvet runners, patterned gold trim.
- **Backgrounds:** soft gradients, layered silhouettes of ornate structures receding into dark.
- **Materials:** gold, glass, velvet, obsidian — each with hints of decay.
- **Modularity:** grid-based, interchangeable, symmetric, seamless looping; minimal seams on snap-to-grid.
- **Depth layering:** foreground tile -> light/FX overlay -> background silhouette. This gives the claustrophobic, smoke-filled, vault-like air and the 2D-reads-as-3D feel.

### Geometry & material rules
- Motifs: hexes, tall diamonds, sharp arches, stepped corners.
- Symmetry with a subtle deliberate break (one cracked length of gold trim).
- Reflective surfaces glow in darkness but never overpower clarity.

### Light & shadow
- Low ambient; **focal lighting** on key tiles, detail falling into shadow for mystery.
- Reflections and silhouettes in glossy floor panels.

### Contrast discipline (readability)
- Tile silhouettes must contrast with operators on them — e.g. **no blood-velvet tiles under a red operator.** Piece readability beats scene richness, always.
- **Powered/tech tiles use the cool cyan register** so they pop against warm gold surroundings — this doubles as gameplay clarity (you can see which tiles are active).

### 6.1 World-as-place rules (derived from the concept scene)

The board should read as an **actual casino floor**, not a diagram in costume. Two rules follow:

- **Atmospheric at rest, functional on demand.** The world is a moody, low-light place when nothing is happening — the path is a *whisper* of gold inlay in dark marble, not bright drawn cells; the yards are round felt gaming tables, not labelled circles; HOME is a lit vault. Legibility is delivered *on demand*: the cells relevant to the current turn (movable destinations, valid targets) light up in the cool tech register on your turn / on hover, then fade back. Never light the whole grid at rest — it kills the atmosphere and the premium feel.
- **Operators are people who sit and stand.** Before deployment, operators are **seated** around their table (the yard). On deploy they **rise** and step onto the floor. This means each operator needs at minimum a seated pose, a standing pose, and a rise transition — a small but real content/animation requirement, not just two static sprites.

## 7. Tile states (animated / overlay)

States are **overlay layers**, not redraws — base tile art stays static and reusable:
- Idle (base, warm register).
- Trap: on / off (on = cool tech glow).
- Treasure/objective: glow / looted.
- Special-space active (ties to special-space mechanics on the Ludo board — see board topology ADR); active = cool register.

Every animated tile needs a defined **idle state** so the board isn't busy at rest.

## 8. UI integration

- Frame everything in Art Deco: filigree, windowed borders, corner fans/accents — the gilded case around a vault door.
- Dramatic contrast for clarity — gold on black is the default legibility pairing.
- **Live/interactive UI states** (selection, cooldowns, powered abilities) use the **cool tech register** — holographic highlights over the gilded frame. Static chrome stays warm/gilded; active states go cool. This mirrors the world's two-register rule at the UI level.
- **Icon language:** sharp corners, tall diamonds, angular fans. No soft/rounded shapes.

## 9. What to feed the art tool

Style-bible reference for Scenario/Leonardo (per `ART_PIPELINE.md`): *top-down Art Deco casino-vault, obsidian and velvet base, gilded geometric inlay, focal light into deep shadow, tarnished gold and cracked marble, noir — with sleek cool-cyan holographic tech accents on powered elements.* Train the custom model on locked references before batch-generating so the whole set inherits one identity. Generate **environment art and tech-FX overlays as separate batches** (warm vs. cool registers) so they don't cross-contaminate.

---

## 10. Concept reference set

Three concept visuals were produced this session to anchor the look. They are **composition and mood references, not final art** — SVG schematics that pin *where things go and in what register*, to be handed (with this doc) to the art tool for real rendering.

1. **Rendered board** — the Ludo board in the Nona Royale palette: obsidian + gold Art Deco table, jewel-tone velvet yards, gilded home columns, cyan-powered safe cells, lit central HOME medallion. The canonical *board composition* reference. **Drawn against the dead 48-cell circuit** — re-check it against 52 before use (`ART_PIPELINE.md` §3).
2. **In-game HUD mock** — the full screen: gilded top HUD (turn banner, energy pips, round counter), the board in play with operators as pieces and one cyan-lit active operator, bottom ability tray (Deco diamonds) + dice tray + selected-operator card. The canonical *screen composition / UI hierarchy* reference.
3. **Casino-floor world scene** — the north-star "the board is a place" illustration: top-down casino floor, four round felt tables as yards with seated operators, path whispered into the marble as faint inlay, lit vault at center, one operator risen and tech-lit as if just deployed. The canonical *world feel* reference.

When briefing Scenario/Leonardo (or any artist), the complete brief is: this doc + these three references. Reference #3 is the tone-setter; #1 fixes the geometry; #2 fixes the UI.

## Open items to lock
- [x] Title **Nona Royale** — LOCKED. Purge "Mystic Heist" from any remaining assets/docs.
- [x] Two-register model (warm Deco / cool tech FX) — LOCKED.
- [x] Tech-first: powers are clean near-future tech by default, with the door open for rare, very light magic drawn in the cool register (§0) — LOCKED; door confirmed open 2026-09-15.
- [ ] Lock the §3 hex palette, especially the **holo cyan** tech accent. *(Pending your visual sign-off — the one CONFIRM item that needs a designer's eye, not a rubber stamp.)*
- [x] 2D vs 3D — LOCKED as 2D (ADR-0001).
- [ ] **Fill the tile-type catalog** — see §11. Structure is set; the specific gameplay types are pending the current (post-brainstorm) design.
- [ ] **Re-check concept reference 1 against the 52-cell board** (§10).


## 11. Tile catalog (structure set; contents pending)

The board is built from modular tiles. Every tile has two orthogonal aspects, and both follow the two-register rule (§2.1):

- **Gameplay type** — what the cell *does* in the rules. These map to the safe-cell rules and the special-space mechanics in the board topology ADR (ADR-0003), not invented here. Known so far from that ADR: **Normal**, **Safe (start/track)**, **Safe (home-entry)**, **Home-column**, and the deferred **Special/rare** spaces (shield, teleport, slippery, checkpoint, etc.).
- **Art state** — the overlay layers a tile can show (§7): idle, active/powered, and any type-specific states (e.g. objective glow/looted). Base tile art stays static; states are overlays. Powered/active states use the cool tech register.

Plus **structural / art-only elements** that are not gameplay cells: floor fields, walls/panels, the central vault door (HOME), decorative trim. These carry the world but hold no rules.

> **Contents deliberately left blank.** An earlier (2024) brainstorm proposed specific types — fire traps, ice traps, "arcane" treasure tiles — but those are **stale and off-canon**: they used fantasy/elemental framing that conflicts with the locked grounded-tech genre, and newer design files supersede them (currently unlocated). Do **not** treat those old types as canon. Fill this catalog from the *current* special-space design once it's recovered/redecided, so the two registers and ADR-0003 mechanics drive the tile list — not year-old fantasy terms.

---

## Status history

- 2026-07-10 — Title, genre and two-register model locked (§0).
- 2026-09-15 — **Restored to the repo** from project knowledge. `ART_PIPELINE.md`, `PROJECT_IDENTITY.md`, `OPERATORS.md`, `PRESENTATION.md` and ADR-0001 had all been citing a file that was missing from `docs/art/`. Content unchanged except:
  - The pipeline path is corrected to `docs/art/`.
  - §10 reference 1 is flagged as drawn against the dead 48-cell circuit, with a matching open item.
  - Luka's ring is added to the §0 list of devices.
  - **The magic rule is reconciled** (designer). §0 kept the door open for rare, very light magic, but the open-items list said "no literal magic". The door stays open and tech stays the default; the open item and `OPERATORS.md` now say the same thing.
- 2026-09-15 — The §3 proposals were put into `UiTheme.cs` as the working palette for the GUI skin pass. They are still proposals; the lock is a visual sign-off in Play Mode.
