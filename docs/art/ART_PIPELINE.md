# Nona Royale — Art Pipeline

> Location in repo: `docs/art/ART_PIPELINE.md`
> Status: **Reconstructed skeleton, 2026-09-11.** The original was cited by `ART_DIRECTION.md`, ADR-0001 and the project tooling notes, but no copy survives in project knowledge. What follows is everything recoverable from those references plus proposals for the gaps. **Sections marked NEEDS DECISION are not settled** — confirm or overwrite them before generating art at volume.
> Related: `docs/art/ART_DIRECTION.md` (what it should look like — wins on aesthetics), ADR-0001 (2D locked), ADR-0003 (board geometry the art must match)

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
- **48-cell outer loop**, **6-cell home column** per colour, **72 total path positions**.
- Four corner **yards**, rendered as round felt gaming tables (`ART_DIRECTION.md` §6.1).
- A single lit **HOME vault** at the centre where the four home columns meet.
- **15×15 grid family.**

The board must also survive a **profile swap** — Sprint is 24 cells, Standard is 48, on identical topology. Build board art as **modular, tileable cells plus corner and arm pieces**, never as one painted 48-cell image. A single baked board image breaks the moment `CircuitLength` changes, and ADR-0002 treats that as a config edit.

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
  Board/       cell_normal_01 · cell_safe_start · cell_home_red · corner_ne · yard_blue
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

## 7. Tile states

States are **overlay layers, not redraws** (`ART_DIRECTION.md` §7). Base tile art stays static and reusable; an overlay sprite renders above it for idle / powered / objective states. Powered and active states use the cool cyan register.

Every animated tile needs a defined **idle** state so the board is calm at rest. Per §6.1, the board is atmospheric at rest and legible on demand — never light the whole grid.

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
- [ ] Fill the tile-type catalog (`ART_DIRECTION.md` §11) once special-space design is decided. The 2024 fire/ice/arcane brainstorm is off-canon and must not be used.
- [ ] Confirm whether the original ART_PIPELINE.md exists anywhere locally. If it does, reconcile it against this and keep whichever is more specific.
