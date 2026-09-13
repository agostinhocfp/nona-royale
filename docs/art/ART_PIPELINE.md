# Nona Royale — Art Pipeline

> Location in repo: `docs/art/ART_PIPELINE.md`
> Status: **Reconstructed skeleton, 2026-09-11.** Amended 2026-09-12 (board geometry corrected). The original was cited by `ART_DIRECTION.md`, ADR-0001 and the project tooling notes, but no copy survives in project knowledge. What follows is everything recoverable from those references plus proposals for the gaps. **Sections marked NEEDS DECISION are not settled** — confirm or overwrite them before generating art at volume.
> Related: `docs/art/ART_DIRECTION.md` (what it should look like — wins on aesthetics), ADR-0001 (2D locked), ADR-0003 (board geometry the art must match), `docs/design/PRESENTATION.md` (what the view must show)

Division of labour: **ART_DIRECTION says what it should look like. This says how it gets made.** Where they conflict, ART_DIRECTION wins on aesthetics and this doc wins on process.

---

## 1. Rendering target

2D, locked (ADR-0001). Sprites, painted board, orthographic top-down. `SpriteRenderer` only — the `MeshRenderer` fallback in the old code is dead.

Render pipeline is **URP with the 2D renderer** (Universal 2D template). This matters for the art bible's focal lighting, glossy reflections, and the cool-register glow on powered tiles: URP gives real 2D lights, per-sprite normal maps and bloom. Built-in would mean faking all of it with additive quads.

**Consequence for asset production:** any sprite meant to receive lighting needs a **normal map** alongside its albedo. Decide per asset class rather than blanket — see §4.

**3D is concept-vision only** (ADR-0001). A 3D render may be produced as a one-off "here's the world" beauty shot to align on mood. It never enters the production pipeline. The cheapest correct path for such a shot is a 3D-_looking_ painted image from the 2D tools using a board schematic as reference, not an actual 3D toolchain.

**2.5D is available but not adopted.** Modelling assets in 3D and rendering them down to 2D sprites from the fixed top-down angle is the standard indie route to dimensional, consistently-lit characters — including the seated → rise → standing frames the art bible requires. Adopting it changes art _production_ only, not the game, and would be its own ADR.

---

## 2. Tools

| Role               | Tool            | Notes                                                                                            |
| ------------------ | --------------- | ------------------------------------------------------------------------------------------------ |
| AI art — primary   | **Scenario**    | Custom model trained on locked references, so the whole set inherits one identity                |
| AI art — secondary | **Leonardo.Ai** | Fallback and variation                                                                           |
| 3D                 | **Meshy**       | Only if 2.5D is ever adopted, or for a one-off concept render                                    |
| Board layout       | **GeoGebra**    | Geometry worked out there, then recreated in Unity                                               |
| Raster editing     | NEEDS DECISION  | Photoshop / Affinity / Krita / Aseprite — whichever, pick one so source files are openable later |

### Generating with Scenario

Train the custom model on locked references **before** batch-generating. Generate **environment art and tech-FX overlays as separate batches** — warm register and cool register must not cross-contaminate (`ART_DIRECTION.md` §2.1).

Style-bible prompt seed (`ART_DIRECTION.md` §9): _top-down Art Deco casino-vault, obsidian and velvet base, gilded geometric inlay, focal light into deep shadow, tarnished gold and cracked marble, noir — with sleek cool-cyan holographic tech accents on powered elements._

The complete brief for any artist or tool is: `ART_DIRECTION.md` + the three concept references in its §10. Reference 3 sets tone, 1 fixes geometry, 2 fixes UI.

---

## 3. Board geometry the art must match

Non-negotiable, from ADR-0002 / ADR-0003. Art that contradicts this is wrong regardless of how good it looks:

- Four-arm cross, **three lanes per arm** — two outer travel lanes flanking a coloured centre home column.
- **52-cell outer loop**, **6-cell home column** per colour, **76 total path positions**.
- Four corner **yards**, rendered as round felt gaming tables (`ART_DIRECTION.md` §6.1).
- A single lit **HOME vault** at the centre where the four home columns meet.
- **15×15 grid.**
- The four **inner corners are never track.** They belong to the central HOME area.

### The board is a family, not a size

A cross's loop threads each arm as two flanking lanes of length L plus the tip cell of the centre lane it crosses, so the only drawable boards are:

> `CircuitLength = 8L + 4` · `HomeColumnLength = L` · `GridSize = 2L + 3`

| L   | Circuit | Home | Grid  | Profile                           |
| --- | ------- | ---- | ----- | --------------------------------- |
| 3   | 28      | 3    | 9×9   | Sprint                            |
| 4   | 36      | 4    | 11×11 | —                                 |
| 5   | 44      | 5    | 13×13 | fallback if sessions run long     |
| 6   | 52      | 6    | 15×15 | **Standard — the shipping board** |
| 7   | 60      | 7    | 17×17 | Long, measurement only            |

**This changes what "modular" has to mean.** It was already true that board art must be tileable cells plus corner and arm pieces rather than one painted image. It is now stronger: **the grid itself changes size between profiles**, so an arm piece has to work at three, five, six or seven cells of lane without redrawing. Build the arm as a repeatable lane cell plus a distinct tip piece; do not bake an arm.

> **This section said 48 cells and 72 positions until 2026-09-12.** 48 is not a circuit a four-arm cross can have — it implies an arm of 5.5 — and the layout code had been hiding the mismatch by letting the loop hop over each arm tip. Any board art, schematic or reference produced against 48 is wrong by four cells and should be re-checked before use. See ADR-0002 Amendment 6.

---

## 4. Asset specifications — NEEDS DECISION

These numbers are proposals, not decisions. Settle them before batch generation, because changing pixels-per-unit after a hundred assets exist is a re-import of everything.

| Setting                | Proposal                                               | Why                                                                                                                                  |
| ---------------------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------ |
| Pixels Per Unit        | **100**                                                | Unity default; 1 board cell = 1 world unit keeps grid maths trivial                                                                  |
| Cell source resolution | **256×256**                                            | Downsamples cleanly; leaves headroom for a future higher-DPI pass                                                                    |
| Operator sprite        | **256×384**                                            | Room for a standing pose above a 1-cell footprint                                                                                    |
| Filter mode            | Bilinear                                               | Painted art, not pixel art. Point/no-filter would be wrong here                                                                      |
| Compression            | None in editor, platform default in builds             |                                                                                                                                      |
| Sprite atlases         | One per class: `Board`, `Operators`, `UI`, `FX`        | Keeps draw calls down without one giant atlas that rebuilds constantly                                                               |
| Normal maps            | Board and environment **yes**; operators and UI **no** | Lighting does atmospheric work on the floor; pieces stay readable and near-unlit (`ART_DIRECTION.md` §6, readability beats richness) |
| Colour space           | **Linear**                                             | Required for URP lighting to behave                                                                                                  |

---

## 5. Naming and foldering

```
Assets/_Project/Art/
  Board/       cell_normal_01 · cell_safe_start · cell_home_red · corner_ne · arm_tip · yard_blue
  Operators/   bouncer_idle · bouncer_seated · bouncer_rise_01 · syla_idle …
  UI/          frame_deco_corner · icon_ability_velvetrope · hud_energy_pip
  FX/          fx_powered_glow · fx_stun_ring · fx_bleed_tick
  Materials/
```

Rules: `lowercase_snake_case`; class prefix first (`cell_`, `fx_`, `icon_`); numbered frames zero-padded to two digits; **no spaces, no version suffixes in filenames** — that is what git is for.

## 6. Required character content

Each operator needs more than two static sprites. From `ART_DIRECTION.md` §6.1, operators are **seated at their yard table before deployment** and **rise to step onto the floor**:

- seated pose
- standing / idle pose
- rise transition
- ability tell (the cool-register powered state)
- neutralized / return-to-yard transition

That is a real content requirement per operator, across nine operators eventually. It is the single largest art cost in the project and should be budgeted as such.

**Four operators exist so far** — three complete and one playable but unfinished (`OPERATORS.md`). None has any art. The prototype distinguishes them by procedural silhouette and by size taken from health, which is the same constraint `ART_DIRECTION.md` §5 sets for the real thing, arrived at crudely. **A shape that is hard to read in the prototype is information for this pass**, not a placeholder to ignore.

## 7. Tile and status states

States are **overlay layers, not redraws** (`ART_DIRECTION.md` §7). Base tile art stays static and reusable; an overlay sprite renders above it for idle / powered / objective states. Powered and active states use the cool cyan register.

Every animated tile needs a defined **idle** state so the board is calm at rest. Per §6.1, the board is atmospheric at rest and legible on demand — never light the whole grid.

**Status badges are a separate, growing set.** `PRESENTATION.md` §2 requires every active status to be visible on its operator at all times, and the roster has been adding statuses as operators land — stun, slow, bleed, stealth, evasion, shield, mark, haste, with more implied by unbuilt operators. Two production consequences:

- **Design the badge set as a system**, not as individual icons. A new status must be addable without a redraw of the others.
- **Damage over time reads differently from a hit.** Bleed and mark ticks resolve at upkeep, where nothing moves and nobody acted, so their feedback carries the cause and a distinct register (`PRESENTATION.md` §2). That is a real FX requirement, not a nicety.

## 8. Licensing — NEEDS DECISION

The original doc covered this and the content is lost. Settle and record before anything ships:

- Commercial-use terms for Scenario and Leonardo output under the current plans, including whether a paid tier is required for commercial rights.
- Whether AI-generated assets are acceptable for **Steam** publishing and what disclosure Valve currently requires.
- Provenance record: for each asset, which tool and which model produced it. A one-line manifest per batch is enough, and it is much cheaper to keep now than to reconstruct later.
- Terms for any purchased Asset Store or font licence, kept with the repo.

## 9. Unity import workflow

1. Export from the art tool to PNG (transparent where relevant) at source resolution.
2. Drop into the correct `Art/` subfolder — foldering drives atlas membership.
3. Set Texture Type **Sprite (2D and UI)**, PPU per §4, Mesh Type **Tight** for irregular shapes.
4. Assign to the class atlas.
5. For lit assets, import the normal map as **Normal map** type and attach via the sprite's secondary texture.
6. Check it at board scale in the Match scene before generating more in that style. Readability at gameplay zoom is the acceptance test, not how it looks at 100%.

---

## Open items

- [ ] Confirm or replace every **NEEDS DECISION** above — §2 raster editor, §4 asset specs, §8 licensing.
- [ ] Lock the `ART_DIRECTION.md` §3 hex palette, especially the holo-cyan tech accent. Still the one item needing a designer's eye.
- [ ] **Re-check any existing board schematic or reference against 52 cells** (§3). Anything drawn to the old 48 is wrong by four cells, and the arm-tip cell it was missing is structural rather than decorative.
- [ ] Decide whether art targets **one grid size or the family** (§3). If Standard is the only board that ever ships, arm pieces can be sized for L=6; if Sprint or the 44-cell fallback might, they cannot.
- [ ] Fill the tile-type catalog (`ART_DIRECTION.md` §11) once special-space design is decided. The 2024 fire/ice/arcane brainstorm is off-canon and must not be used.
- [ ] Design the **status badge set as a system** (§7), given the list is still growing.
- [ ] Confirm whether the original ART_PIPELINE.md exists anywhere locally. If it does, reconcile it against this and keep whichever is more specific.
