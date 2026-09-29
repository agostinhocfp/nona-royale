# Nona Royale — Chip pieces

> Location in repo: `docs/design/CHIP_PIECES.md` · Project copy: `claude/CHIP_PIECES.md`
> Status: **Adopted, ADR-0013 (2026-09-29).** CP1–CP4 built and accepted in Play Mode. Operators are portrait chips on the board; the other nine portraits come from `ART_PROMPTS.md` block 8.
> Related: `ART_PROMPTS.md` v4.2 asset block 7 (the chip frame and anchors), `OPERATORS.md`, `ART_DIRECTION.md` §3 and §5, `VISUAL_PASS.md` (the tilt), `OPERATOR_LOOKBOOK.md` (the rigs this sits beside).

## What and why

The designer is testing operators as flat casino-chip tokens with portrait art, instead of standing figures (2026-09-29). If the chips are adopted, board pieces need no Blender pass: they are sprites only. The first three portraits are Luka, Bouncer and Syla. **Mimi and the rest wait for this playtest.**

## Decisions (designer, 2026-09-29)

1. **Flat token.** The chip lies on the cell, face up, with a visible edge for thickness. Under the tilted camera it stays on the table rather than standing up.
2. **Emblem chips for the nine without art.** Same body and ring, the operator's shape in gold on black lacquer as the face, so the playtest judges the format and not a mixed board.
3. **A switch, not a replacement.** Display settings get **Pieces: Chips / Figures**, default **Chips**. The figures (renders, rigs, look book, pawn) are untouched.
4. **Board pieces only.** The HUD, the draft and the dossier keep what they show today.

## How it is built (CP1)

| File | What |
| --- | --- |
| `View/ChipSprites.cs` (new) | The shared parts drawn in code: greyscale body (tinted per seat), ivory edge inserts, gilt ring, plain disc. Mipmapped, 256 px, one unit across |
| `View/ChipArtLibrary.cs` (new) | Loads `Resources/Art/Chips/<name>_chip_unlit` and `_chip_lit`; the device anchors by name |
| `View/ChipView.cs` (new) | One chip: shadow, edge, body, inserts, face (portrait or emblem), lit face, ring, device glow |
| `Editor/ChipArtImporter.cs` (new) | Imports the chip folder (centre pivot, 512 max, one unit across) and **cuts every portrait to a circle** at import, so the generator's square files go in as they are |
| `View/OperatorPiece.cs` | A chip branch ahead of the figure fallbacks; `ApplyStyle` switches live |
| `View/DisplaySettings.cs`, `View/ISettingsHost.cs` | `PieceStyle`, the Pieces row, saved as `nr.display.pieces` |
| `Composition/MatchBootstrap.cs` | Binds pieces in the chosen style; applies a change every frame, like the board skin |
| `Art/Resources/Art/Chips/` | The six portraits, downscaled to 512 from `art/source/characters/` |

**What a chip does differently from a figure:**

- **Hop:** it grows toward the camera and back instead of lifting up the screen, and lands with a small thud. Reduced motion glides, as before.
- **Idle:** no breathing, no sway. A chip is an object.
- **Cast:** the lit portrait fades in over the unlit one and a cyan glow swells on the device anchor for the tell's length, then both go dark. The rim flare still plays.
- **Seat identity:** the chip's body is the seat colour, so the seat disc, its ring and the shape pin are hidden.
- **Unchanged:** the walk, the select glow and rim, the target ring, the hit flash (a white disc), the health bar, the knockout shards and burn, the haste streaks.

**Tuning knobs** (Play Mode):

| Constant | Where | Now | Does |
| --- | --- | --- | --- |
| `ChipDiameter` | `OperatorPiece` | 1.008 | Chip size: 0.76 of a cell for the frailest operator to 1.23 for the toughest (was 0.72) |
| `ChipHopGrow` | `OperatorPiece` | 0.14 | How much a chip grows at the top of a hop |
| `EdgeDrop` | `ChipView` | 0.08 | Thickness (0.055 before CP2) |
| `ShadowOffset`, `ShadowAlpha` | `ChipView` | (0.03, -0.11), 0.7 | The drop shadow (0.55 before CP2) |
| `ContactSize`, `ContactAlpha` | `ChipView` | 1.06, 0.75 | The tight shadow in the seam under the edge |
| `InkWidth`, `InkAlpha` | `ChipView` | 0.018, 0.95 | The ink outline, per side, in chip diameters |
| `FaceTint` | `ChipView` | (1, 0.98, 0.94) | Brightness and warmth of the unlit face and ring (0.97, 0.94, 0.89 in CP2) |
| `BodyTint` | `ChipView` | (0.88, 0.86, 0.82) | Brightness of the unlit body, edge and inserts |
| `HeroSize`, `UprightHeroSize` | `ActionTray` | 210, 124 | The hero portrait's canvas, wide and upright; the frame inside it is 95% as wide and 80% as tall |
| `FaceRadius`, `RingOuter` | `ChipSprites` | 0.78, 0.86 | Face size, ring width |
| `MaskRadius` | `ChipArtLibrary` | 0.48 | How much of the portrait is kept. **Changing it needs a reimport of the chips folder** |

## CP2 — Making them pop without size (designer, 2026-09-29)

- **The face, the emblem, the ring and the cast glow are unlit** (`Resources/Art/Fx/ChipUnlit.mat`, URP's `Sprite-Unlit-Default`, loaded through `ShaderFx`), multiplied by `FaceTint`, the room's warm white. The body stays lit, so the chip still sits in the room. Before this, the whole chip took the 0.7 ambient, the vignette and the dark gaps between the light pools. If the material is missing, the chip stays lit and logs one warning.
- **An ink outline** (`UiTheme.Ink`) round the chip and its edge, always on. The selection rim now follows the outline, outside it.
- **Lift:** the edge is thicker and carries the inserts down its side, a tight contact shadow sits in the seam and fades out on a hop, and the drop shadow is darker.
- Not done, for after a screenshot: a gloss sweep on the face, stronger seat colour on the body, and a slow lift on the chips that can act (with CO2).

## CP3 — Hero portrait, dice dock, more light (designer, 2026-09-29)

- **Hero portrait.** The selected operator opens the action tray as a large chip (`View/HeroPortrait.cs`): the same portrait, seat-coloured body, inserts, ink and gilt ring as its chip on the board, in uGUI. Wide it is 172 across in a 172 slot and rises 34 units above the tray's top edge; upright it is 100 across and rises over the aim line only, never the board. An operator with no portrait shows its emblem. The shape icon on the operator card is gone: the portrait says who it is.
- **Powered while armed.** With an ability armed, the portrait shows the lit twin and a cyan glow on the device.
- **The dice moved.** Wide: to a small dock floating over the board's bottom-left corner, above the portrait, mirroring the turn button in the bottom-right; the dice hint moves with them. Upright: to the screen's bottom-left corner, in the tray's bottom row with Cast between the dice and the turn button; the hint is dropped there (the top bar says it). The coach's tips start above the portrait and the dock, wide (`ActionTray.LeftStackClearance`).
- **More light.** The whole chip is now unlit except its shadows and ink: the face and ring at `FaceTint`, raised to about full, and the body, edge and inserts at `BodyTint`, a step lower so the portrait stays the brightest thing on the chip.
- **Play Mode checks:** the portrait against the board and the rail, wide and upright; the dock against the pieces in the bottom-left cells and against the dice roll's landing; a coach tip shown while an operator is selected; armed and unarmed.

## CP4 — The painted hero frame (designer, 2026-09-29)

- **The designer's vision** (`art/source/ui/hero_frame_sheet.png`): the portrait in a Deco frame of black lacquer, oxblood insets, ivory clasps top and bottom, side pods and a gold inner ring, with cyan arcs when powered. CP3's hero was a large seat-coloured chip; the frame replaces it.
- **Cut from the sheet by `tools/art/hero_frame.py`** into `Resources/Art/Hero/hero_frame.png` (background removed by flooding the dark background in from the border, the ring's inside cleared) and `hero_frame_lit.png` (the lit frame's cyan arcs alone, aligned, with a soft glow). Both 640×640, centred on the gold ring; the portrait fills the ring's inside (`HeroPortrait.InnerRadius`, 199/640). Rerun the script on a new sheet, after measuring its crop boxes again.
- **The face is the chip portrait**, so the tray and the board still show one object; the emblem sits on black lacquer where there is no portrait. While an ability is armed: the lit portrait, the frame's arcs and a device glow. Without the frame file, CP3's chip hero is drawn instead.
- **The oxblood insets stay oxblood**, as painted, not the seat colour. The seat shows on the board's chip and the health bar. If the frame should carry the seat, the insets can be split into a tinted layer by the same script.
- **Fix: the black squares behind the portraits.** The six chip PNGs were saved RGB, and a texture with no alpha channel can't be cut, so the importer's circle never took. The files are now RGBA and cut, and the importer gives any RGB file an alpha channel (from grey) before cutting it, so a generator's RGB output works as dropped in. `ChipArtImporter` also imports `Art/Hero/`.

## Adding an operator's chip

1. Generate and approve the portrait under `ART_PROMPTS.md` asset block 7 (unlit, then the lit twin as a masked edit).
2. Keep the originals in `art/source/characters/<name>/`, and drop copies (512 px is enough) into `Assets/_Project/Art/Resources/Art/Chips/` with the same names.
3. Measure the device anchor and add it to `ChipArtLibrary.Anchors`. Without one, the glow plays at the face's centre.
4. Record both files in `docs/art/PROVENANCE.md`.

## Play Mode checks

- [ ] The three portrait chips and the nine emblem chips in all four seat colours, at gameplay zoom and in a phone-sized Game view.
- [ ] A cast by Luka, Bouncer and Syla: the glow lands on the ring, the fist and the hand drone.
- [ ] Walk, bounce, knockout and redeploy, with Reduced motion on and off.
- [ ] Selection rim, target ring and hit flash read on a chip.
- [ ] Tilted camera: the chips lie flat and sort front to back.
- [ ] Both board skins.
- [ ] Switch Pieces mid-match: every piece redraws at once, and back.
- [ ] The yard: seated chips sit readably at the tables.

## Log

- 2026-09-29 — **CP1.** Chip pieces behind the Pieces switch (default Chips): code-drawn chip body, inserts and ring; portrait faces for Luka, Bouncer and Syla with lit twins and device glows; emblem faces for the other nine; a chip importer that cuts portraits to a circle; EditMode tests for the lookup, the anchors, the setting and the switch. Compiles against the editor DLLs (runtime, editor and EditMode test assemblies); not yet run in Unity.
- 2026-09-29 — **CP1b, chips 40% larger** (designer, after the first look: "they need some size"). `ChipDiameter` 0.72 → 1.008; the hit radius follows the chip. The toughest operators' chips now overlap neighbouring cells slightly.
- 2026-09-29 — **CP2, chips stand out without size** (designer): unlit face, emblem, ring and cast glow at the room's warm white; an ink outline round the chip and its edge; a thicker edge with the inserts carried down it, a contact shadow and a darker drop shadow. Compiles (runtime, editor and EditMode test assemblies); not yet run in Unity.
- 2026-09-29 — **CP3, hero portrait and dice dock** (designer): the selected operator's chip as a hero portrait in the tray, lit while armed; the dice moved to a dock at the board's bottom-left (wide) or the screen's bottom-left (upright); the whole chip unlit except shadows and ink, a little brighter. Compiles (runtime, editor and EditMode test assemblies); not yet run in Unity.
- 2026-09-29 — **CP4, the painted hero frame** (designer's sheet): the hero portrait moves into the Deco frame cut from the sheet by `tools/art/hero_frame.py`, with its cyan arcs while armed; the chip portraits are re-saved RGBA and the importer now handles RGB files, which fixes the black squares behind the faces. Compiles (runtime and editor assemblies); not yet run in Unity.
- 2026-09-29 — **Adopted (ADR-0013).** Chips are the board pieces; ADR-0009 is superseded for them; the rigs leave the 2D build once `archive/rigs-2d` is tagged; no ability icons in the tray.
- 2026-09-29 — **Kurbyn's chip.** Unlit and lit portraits in `art/source/characters/kurbyn/` and, 512 px and cut, in `Resources/Art/Chips/`; the lit file is the designer's masked edit composited back onto the unlit one inside a feathered mask round the rig, so nothing outside the device changes when it lights. Anchor (0.473, 0.283) in `ChipArtLibrary`; the anchor test covers him.
- 2026-09-29 — **Javi's chip.** The designer's prompt (mixed skin tone, long midface, flared cheekbones wider than the jaw) with his eyes on the canister in his hand; the lit twin lights the six full canisters and the one in hand, the spent ones stay dark, composited back onto the unlit image (the edit had also redrawn his hair and face slightly). Anchor (0.744, 0.378) on the hand canister.
- 2026-09-29 — **Sanity's chip.** Bald, clean-shaven, the prod on his shoulder and the tool roll across an oiled umber apron; his head is about a third of the circle, a deliberate exception to the 45–55 % rule so the largest man reads by mass. Lit twin composited so only the prod's head changes; anchor (0.23, 0.223).
- 2026-09-29 — **Mimi's chip.** The designer's prompt (late teens, warm skin, epicanthic folds, friendly) with the plated cryo harness, two frosted emitters and the forearm coordinate plate; the lit twin lights the harness, both emitters and the plate, composited so nothing else changes. Anchor (0.68, 0.534) on the emitter nearest her face.
- 2026-09-29 — **Fortuna's chip.** Second pass, gold-dominant as the cast needed: broad stepped gold collar, gold-panelled lapels and waistcoat, watch chain and wide cuffs on black; turned blonde by the designer, since the rest of the cast is dark-haired. A card raised in her right hand, the dealer's shoe under her left. The lit twin draws a cyan feed line along the shoe and a faint cyan on her fingers, composited so nothing else changes. Anchor (0.644, 0.825) on the shoe. The two chip tests that used Mimi as the operator with no portrait now use a name outside the cast (they failed once Mimi's chip landed).
