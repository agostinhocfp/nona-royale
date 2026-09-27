# Nona Royale — Board skin (BS-series)

> Location in repo: `docs/design/BOARD_SKIN.md` · Project copy: `claude/BOARD_SKIN.md`
> Status: **Open, 2026-09-27.** BS0, BS1 and BS2 accepted. BS3 (the Deco centre) written, waiting on Play Mode and commit.
> Design: `claude/HANDOFF_board_skin.md` (the code route). Sister: `claude/HANDOFF_board_assets.md` (the sprite route); its §1 table is the contract, copied below.
> Target: `docs/art/VISUAL_TARGET.png`. Acceptance for every increment is a Play Mode screenshot of the board beside it.
> Baseline: `docs/art/BASELINE_BS0.png` (Classic, flat, 2560×1440, 2026-09-27).
> Related: `ART_DIRECTION.md` v6 §2.2, §3, §6.1, §10 ref 5 · `LAUNCH_UI_PASS.md` · ADR-0010 (URP 2D, linear)

## Standing rules for the series

- The code owns geometry; art only paints surfaces. No BS-increment touches `Core/`.
- **Two skins, side by side** (D4). Settings → Display → **Board skin** switches CLASSIC and DECO, live, remembered, CLASSIC by default. Classic's drawing code in `BoardView.cs` is never edited by a BS-increment; Deco lives in `BoardView.Deco.cs` and `DecoBoardArt.cs`, element by element. An element Deco doesn't draw yet is drawn as Classic.
- Every increment must read in the **Deco skin**, **flat and tilted**, **wide and upright**, **Lighting effects on and off**, and under **Reduced motion**, and must leave **Classic pixel-identical**.
- A missing sprite is never an error: the board keeps its procedural surface, slot by slot.
- **The Case series is dropped** (designer, 2026-09-27; reverted in git). On Classic the padded oxblood rail (G9c) stays. The Deco skin's frame has no owner yet (open item below).

## Decisions

| # | Question | Recommended | Answer |
| --- | --- | --- | --- |
| D1 | Track path: a whisper at rest (ART §6.1, 2026-09-15) or gilt-edged tiles like the target? | Tiles, at a lower contrast than the target, so highlights still pop | **Tiles, softer** (as recommended), 2026-09-27 |
| D2 | Where the vault's win moment goes if the centre becomes a medallion | The medallion is the vault face; the win glow and the dial stay, restyled | **Medallion, drop the vault** (not the recommendation), 2026-09-27. The win moment moves off the centre |
| D3 | Chairs drawn in code or as a sprite | Sprite (`yard_chair`); seat marks stay until it exists | **Sprite** (as recommended), 2026-09-27 |
| D4 | How the new skin lives beside the current board | A switch on the Display page, CLASSIC by default, redrawing live; Classic's code never edited | **Switch in Display** (as recommended), 2026-09-27. Nothing of the G-series board or UI is undone |

**Open:** the Deco skin's frame. The target's gilt frame, side rails and tray were the Case series' (`CASE.md`), which is dropped. Recommended when the board itself is done: a BS7 that frames the Deco table in world space (G9c's rule), without HUD slots.

D1 and D2 are recorded in `ART_DIRECTION.md` §6.1 and §11, and the §6.1 open item is closed (2026-09-27).

**What D2 means for BS3.** On the Deco skin the medallion and the four corner wedges replace all of `DrawVault` (glow, plate, frame, dial, boss). Classic keeps its vault (D4), so nothing is removed. The win moment leaves the centre, and **where it goes is not board scope** (HUD or pieces): it is handed to the launch-UI series. Until that lands, a win has no centre glow. `EventLights.VaultSwell()` is the per-arrival light (LT2), not the vault art: BS3 keeps it, lighting the medallion, unless the designer says otherwise.

## The contract

Copied from `HANDOFF_board_assets.md` §1 on 2026-09-27. **The code mirror is `BoardSprites.Slots`**, and `BoardSpritesTests.Slots_MatchTheContract_NameSizeAndTint` types this table out, so a slot changed on one side fails until the other matches. Change the handoff, this table, `BoardSprites.Slots` and the test together.

Files go in `Assets/_Project/Art/Resources/Art/Board/`, as PNG, named exactly after the slot.

| Slot | What | Size (px) | Colour | Route | Owner in code |
| --- | --- | --- | --- | --- | --- |
| `board_marble` | Cross floor texture, tileable | 1024 | Colour | Existing slot | `BoardTextures` / `BoardTextureImporter` |
| `board_felt` | Table felt texture, tileable | 1024 | Greyscale | Existing slot | `BoardTextures` / `BoardTextureImporter` |
| `board_carpet` | Carpet around the cross, tileable | 1024 | Colour | Existing slot | `BoardTextures` / `BoardTextureImporter` |
| `board_cell_track` | One track cell: bevelled dark tile, thin gilt edge | 256 | Colour | Code-first | `BoardSprites.CellTrack` |
| `board_cell_home` | One home-column cell with the gilt ring | 256 | Greyscale, tinted | Code-first | `BoardSprites.CellHome` |
| `board_start_emblem` | Compass emblem for a start cell | 256 | Greyscale, tinted | Sprite-first | `BoardSprites.StartEmblem` |
| `board_medallion` | The centre disc | 512 | Colour | Code-first | `BoardSprites.Medallion` |
| `board_medallion_emblem` | Gilt diamond emblem on the medallion | 512 | Colour | Sprite-first | `BoardSprites.MedallionEmblem` |
| `board_corner_wedge` | One gilt wedge for an inner corner (points up-right) | 256 | Colour | Code-first | `BoardSprites.CornerWedge` |
| `yard_table_felt` | Round table top, felt only | 1024 | Greyscale, tinted | Code-first | `BoardSprites.TableFelt` |
| `yard_table_rim` | The gilt rim ring | 1024 | Colour | Code-first | `BoardSprites.TableRim` |
| `yard_table_emblem` | Compass emblem at the table's centre | 256 | Colour | Sprite-first | `BoardSprites.TableEmblem` |
| `yard_chair` | One armchair from above, seat toward the bottom | 256 | Greyscale, tinted | Sprite-first | `BoardSprites.Chair` |
| `prop_candle` | Brass candle cup, lit | 256 | Colour | Sprite-first | `BoardSprites.Candle` |
| `prop_chips` | Short stack of casino chips | 256 | Colour | Sprite-first | `BoardSprites.Chips` |
| `prop_plant` | Potted palm or fern, from above | 512 | Colour | Sprite-first | `BoardSprites.Plant` |
| `prop_instrument` | Brass spyglass or instrument | 512 | Colour | Sprite-first | `BoardSprites.Instrument` |

**Route changes** (a code-first row flipping to sprite-first after a checkpoint) are logged here and in the handoff's status table.

## Log

- 2026-09-27 — **BS3 written: the Deco centre (D2).** On the uncommitted BS1 + BS2 tree.
  - **On Deco, `Build` draws `DrawDecoCentre` instead of `DrawVault`;** Classic keeps its vault untouched (D4). No glow on Deco: the win moment leaves the centre (D2). `EventLights.VaultSwell`, the arrival light, is a light and not board art, so it still swells at the centre.
  - **The medallion** (`DecoBoardArt.Medallion`, 256 px): black lacquer (`UiTheme.DecoMedallionFace`, `#0D0A0C`) with a soft specular toward the upper left, a faint engraved ring, a gilt hairline and groove, and a heavy bevelled gilt rim lit from the upper left. **Its size is set by the grid, not the target:** `BoardView.DecoMedallionDiameter` = 92 % of the gap between the last home cells' inner edges, 1.05 spacings on every board, against the target's ~2.5. The target's medallion squeezes those cells to half width (ART §10 ref 5, a known departure), and ART §2.2 says the grid wins. A soft contact shadow sits under it.
  - **The emblem** (`DecoBoardArt.MedallionEmblem`): a gilt diamond outline, a four-faceted core lit from the upper left, rays on the axes between them and short ones on the diagonals.
  - **Four corner wedges** (`DecoBoardArt.CornerWedge`): long gilt kites from under the medallion toward the cross's four inner corners, faceted down the spine (lit side toward the light, a dark crease in the middle). They carry the target's star. **Their shape comes from the cells they pass:** 0.71 spacings out they slip between the corners of two last home cells, where only 0.1 is free either side, so they are narrow there (half-width 0.07), swell to 0.2 in the empty corner of the centre square at 1.3, and end in a point at 2.05, short of the inner corner at 2.12. Minimum clearance to any cell: 0.02 spacings.
  - **Slots honoured:** `board_medallion`, `board_medallion_emblem` and `board_corner_wedge` replace their procedural piece when filled. The wedge art points up and right and covers the 1.5-spacing square from the board's centre to the inner corner; the code turns it four times.
  - **Sorting:** shadow at −15, wedges at −14, disc at −13, emblem at −12 (the vault's orders, which Deco doesn't use), all over the cells and under every piece and highlight.
  - **Tests.** `DecoCentreTests` (new), 4, on the Standard and the Compact board: the medallion clears every home column's last cell and stays inside `HomeGoalSize`; the wedges never enter any cell (walked along both edges and the spine, all four turns); the wedge starts under the medallion and stops inside its square; `InWedge` is false off the spine and past both ends. All four pass offline as well as in Unity.
  - **Checks.** Cloud compile on the device's tree: core 969/969; Core, Unity (with and without `DEVELOPMENT_BUILD`), EditorTools and EditTests build with 0 warnings.
  - **Play Mode checklist:**
    - Deco: the centre is the medallion and four gilt wedges; no vault, no gold glow. Classic: the vault, unchanged.
    - **The last cell of every home column is whole and clear;** a piece on it reads, and so does one arriving HOME (it stands on the medallion).
    - The wedges don't touch any tile, flat and tilted, wide and upright.
    - An operator arriving HOME: the arrival light still swells at the centre. A win: no centre glow (expected, D2).
    - Switch skins mid-match: the vault and the medallion swap with the path.
    - Watch for: the wedges too loud against the 70/20/10 gold budget; the medallion reading too small against the target (see the open question below).
  - **Open question for the designer:** if the medallion reads too small, the alternative is a target-sized medallion drawn **under** the tiles, so the four last home cells sit on its rim and stay whole. It's a bigger change to the look, so it waits for your screenshot.

- 2026-09-27 — **BS2 accepted** (designer): every Play Mode check passes.

- 2026-09-27 — **BS2 written: the skin switch, and the Deco cells.** On HEAD `64f6a21` plus BS1.
  - **The switch (D4).** `BoardSkin` (Classic, Deco) joins `DisplaySettings` beside the camera: default CLASSIC, cycled, labelled, compared, copied, reset and sanitised like the camera, and saved as `nr.display.boardSkin`. The Display page gains a **Board skin** row after Board camera, hinted "in development" while on DECO. `BoardView.Skin` is set before both builds in `MatchBootstrap`, and `BoardView.ApplySkin`, called every frame after the settings are saved, redraws the board from the last build's map, layout and seats when the skin changes, mid-match included (the board holds no state; `PoweredShine` is cleared and refilled by the rebuild). A skin change also counts as a display change, so `ApplyDisplay` re-applies the same resolution and VSync, as a camera change already did.
  - **`BoardView` edits are plumbing only:** `partial`, the skin property, the remembered build arguments, `ApplySkin`, and one branch in `Build`: `DrawDecoTrack` on Deco, `DrawTrack` otherwise. Every Classic drawing method is byte-identical.
  - **`View/BoardView.Deco.cs` (new).** The Deco path (D1): every track cell a raised tile, faced near-black (`UiTheme.DecoTrackFace`, `#1E191C`, held under the target's contrast), with a lit top-left bevel, a shaded bottom-right one and a gilt hairline with a glint at its top-left corner. Home columns: tiles at half their seat colour with an engraved gilt ring each. Start cells: tiles at 62 % of their seat colour with a pale-gold eight-point compass. Other safe cells: a dark tile with a **cyan** hairline, over the kept cyan glow. Every safe cell's trim glints (G8f), as the inlays did. The floor, lanes, tables, vault and rail stay Classic's until BS3–BS5.
  - **Slots honoured.** A filled `board_cell_track` replaces the face (its painted gilt edge stands; the procedural trim is drawn over it only on safe cells, to glint); `board_cell_home` is tinted with the seat and replaces face and ring; `board_start_emblem` is tinted `DecoEmblemTint` (the seat lifted 60 % to white) and replaces the compass. All scale through `BoardSprites.UnitScale`.
  - **`View/DecoBoardArt.cs` (new).** The procedural art, 128 px, mipmapped, trilinear, anisotropic 4 (the rest of the procedural board art has no mipmaps; cells are small and slanted). The face is greyscale, painted at 0.72 with a faint top-lit gradient and tinted through `FaceTint`. **The bevel is baked light and shade in the trim**, not a tinted bevel in the face: a first pass tinted the bevel with the face, and on a near-black tile a multiplied highlight is invisible, so the bevel vanished (seen in a cloud mock-up before any Play Mode). The trim comes in two, `TrimGilt` and `TrimCyan`, colours baked from `UiTheme.DecoGilt`/`DecoGiltLight` and `Cyan`/`CyanBright`, drawn at `UiTheme.DecoTrim` (white, 85 %). Transparent pixels keep their shape's colour, so the mips don't fringe dark.
  - **`UiTheme` gains the Deco block:** `DecoTrackFace`, `DecoGilt`, `DecoGiltLight`, `DecoTrim`, `DecoHomeFace` (0.5), `DecoStartFace` (0.62), `DecoHomeRing`, `DecoEmblem`, `DecoSeatFace`, `DecoEmblemTint`. No existing value changed.
  - **Sorting unchanged.** Face at `CellOrder` (−22), trim, ring and compass at `InlayOrder` (−21), all on the board layer. Highlights, reach, targets and auras draw on Default, above the whole board layer, so they always land over the tiles.
  - **Tests.** `DisplaySettingsTests` +5 (skin default, cycle and label, counts as a change, copy/reset, sanitise). `DecoBoardArtTests` (new), 3: `FaceTint` divides out the face value, clamps, and every sprite is one unit across at 128 px with mipmaps.
  - **Checks.** Cloud compile on the device's tree: core tests 969/969; Core, Unity (with and without `DEVELOPMENT_BUILD`), EditorTools and EditTests build with 0 warnings. 32 EditMode tests pass offline; the 3 that create textures or sprites need Unity's Test Runner.
  - **Play Mode checklist:**
    - Settings → Display: a **Board skin** row after Board camera reads CLASSIC. Classic looks exactly like `BASELINE_BS0.png`.
    - Switch to DECO **mid-match**: the path redraws at once as tiles; pieces, highlights, the vault, tables and rail don't move or flicker beyond one frame. Switch back: Classic, identical. Quit and relaunch: the choice is remembered. Restore defaults returns CLASSIC.
    - Deco, your turn: landing ghosts (cyan), reach dots, cast targets (amber) and auras all read clearly over the tiles; a piece on a track tile, a home tile and a start tile reads at phone size; Luka on the gilt edge and Revú over a red home column (ART §5.1).
    - Safe cells still glint in turn (Lighting effects on, Reduced motion off), and stop with either switched.
    - Tilted camera: the hairlines don't crawl or shimmer as the camera settles. Upright (phone shape): the tiles still read.
    - Lighting effects off: the tiles still read as raised (the bevel is painted, not lit).
    - **The acceptance screenshot:** Deco, flat, wide, beside `VISUAL_TARGET.png`, and one beside `BASELINE_BS0.png`.
    - Watch for: the tiles too bright or the gilt too loud against the highlights (`UiTheme.DecoTrackFace`, `DecoTrim`); home columns too saturated (`DecoHomeFace`); the compass too small (`DecoEmblemSize` in `BoardView.Deco.cs`).

- 2026-09-27 — **The Case series is dropped** (designer; reverted in git in the other chat). **D4: the new skin is a switch** beside the current board, so none of the G-series work is undone. The standing rules above are rewritten around the two skins; the "wait for C2" rule is gone.

- 2026-09-27 — **BS1 accepted** (designer): the board is identical before and after, and `BoardSpritesTests` is 10/10 in the Test Runner.

- 2026-09-27 — **BS0: the baseline.** `docs/art/BASELINE_BS0.png`: Classic, flat camera, wide (2560×1440 at 0.67 scale), round 1. Read against `VISUAL_TARGET.png`, element by element:

  | # | Element | Target | Baseline (Classic) | Increment |
  | --- | --- | --- | --- | --- |
  | 0 | **Overall value and gold** | Mid-dark board, ~20 % gilt, everything edged | Near-black; gilt only on the four table rims, the cross's edge and the vault. **The widest gap.** | All |
  | 1 | Track cells | Raised dark tiles, gilt edges, gilt lattice | Faint outline squares, a warm inlay at ~16 %; reads as a sketch of a grid | BS2 |
  | 2 | Home columns | Solid seat colour, a gilt ring per cell | A translucent wash; red reads, blue, green and violet are faint | BS2 |
  | 3 | Start and safe cells | Seat tile with a compass | Cyan glow squares; start cells under the pieces | BS2 |
  | 4 | Centre | Black medallion, gilt diamond, four gilt wedges | An octagonal vault with a hot gold glow, the brightest thing on the board | BS3 |
  | 5 | Tables | Seat felt, heavy gilt rim, emblem, a ring of armchairs | Tinted felt, dotted ring, arc, spot and a strong rim. **The closest element to the target.** No chairs; seat marks invisible | BS4 |
  | 6 | Weight | Drop shadows under everything raised | The cross's shadow vanishes into the black table; nothing else casts one | BS5 |
  | 7 | Light | Candle pools at the corners, gilt catching light | One hot pool at the vault; the arm pools barely show | BS5 |
  | 8 | Props | Candles, chips, plants, instrument | None beyond the felt's chips | BS6 |
  | 9 | Table and frame | Gilt frame, rails, tray | Black carpet, the oxblood rail as a thin red line | Open (D4 note) |

- 2026-09-27 — **BS1 written: the sprite slots.** On HEAD `64f6a21`. Nothing on screen changes: nothing reads a slot yet, and the folder is empty.
  - **`View/BoardSprites.cs` (new).** The contract in code: one constant per slot and `Slots`, a list of `BoardSlot` (name, painted size, tinted or not). `Get(slot)` loads `Resources/Art/Board/<slot>` as a `Sprite`, caches it (a cached null means "looked, empty"; a sprite the editor destroyed is looked up again), and returns null for an empty slot, so the caller keeps its procedural surface. `Has`, `Filled`, `TryGetSlot`, `IsSlot`. A name outside the contract **throws**, because a misspelt slot in code would otherwise look empty forever. `UnitScale(sprite)` returns the scale that draws a sprite one unit across from its bounds, so BS2+ never depend on pixels-per-unit being right. If a file is in the folder but not imported as a sprite, one warning says so instead of the slot silently staying procedural. The cache clears on entering Play Mode (`SubsystemRegistration`), so "restart Play Mode to see a new file" holds even if domain reload is turned off later.
  - **`Editor/BoardSpriteImporter.cs` (new).** For a contract slot directly in the Board folder, on first import: Sprite (single), centre pivot, full-rect mesh, alpha is transparency, mipmaps (Kaiser), bilinear, **anisotropic 4** (the tilted camera sees the board at a slant), clamp, high-quality compression, max 2048, **no Read/Write**. **Pixels-per-unit = the image's width on every import**, not only the first, because it is the contract, not a taste. A size other than the contract's square warns and still loads. A file whose name is not a slot (and not one of the three textures) warns as a likely typo and is left alone.
  - **`Editor/BoardTextureImporter.cs` (changed).** It used to claim **every** texture under the Board folder, so the first sprite dropped in would have been imported as a readable, uncompressed, repeating Default texture and `Resources.Load<Sprite>` would have returned null. It now owns only `board_marble`, `board_felt` and `board_carpet` (`Owns`), and only directly in the folder. The folder is empty today, so no existing asset is re-imported differently. Stays CRLF.
  - **`Tests/EditMode/Unity/BoardSpritesTests.cs` (new), 10 tests.** The contract table typed out and compared to `Slots` (name, size, tint); every public constant is a slot and every slot has a constant; names unique, lowercase `board_`/`yard_`/`prop_`, never one of the three textures; sizes are powers of two; `TryGetSlot` finds exactly the slots; unknown names throw; `Register`/`ClearCache`; `UnitScale` is one unit across at PPU 16, 64 and 100.
  - **Checks.** Cloud compile (the rebuilt `check.sh`: Unity's own editor rsp flags and defines, refs remapped to the staged DLLs, sources re-globbed so new files are included): core tests 969/969; `NonaRoyale.Core`, `NonaRoyale.Unity` with and without `DEVELOPMENT_BUILD`, `NonaRoyale.EditorTools` and `NonaRoyale.Unity.EditTests` all build with 0 warnings. The 8 pure `BoardSpritesTests` pass offline; the 2 that create a `Sprite` need Unity's Test Runner.
  - **Note: the C2 slice is not in the working tree.** At 14:56 the tree went back to HEAD. *Resolved the same day: the designer dropped the Case series and reverted it on purpose; `Temp/c1x`, `Temp/c2x` and `Temp/cc_c2x_backup.tgz` are leftovers, safe to delete.*
  - **Play Mode checklist:**
    - The editor compiles with no new console errors or warnings.
    - A match on Classic and on Case looks exactly as before (nothing reads a slot yet).
    - Test Runner → EditMode → `BoardSpritesTests`: 10/10 green.
    - Importer smoke test (one minute, optional): drop any square PNG named `yard_chair.png` into `Art/Resources/Art/Board/`. The console says `[BoardSprites] Import settings applied … (tinted slot: paint it grey)` (plus a size warning if it isn't 256), and the Inspector shows Sprite (2D and UI), Pixels Per Unit = its width, Read/Write off, Aniso 4, Clamp. Drop a copy named `yard_chairs.png`: a "not a contract slot" warning. Delete both, with their `.meta` files, before committing.

- 2026-09-27 — **BS0: D1–D3 answered** (table above): tiles at lower contrast; medallion with the vault dropped; chairs as a sprite. `ART_DIRECTION.md` §6.1, §11, the open items and the status history updated.
