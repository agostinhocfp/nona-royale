# Nona Royale — Board skin (BS-series)

> Location in repo: `docs/design/BOARD_SKIN.md` · Project copy: `claude/BOARD_SKIN.md`
> Status: **Open, 2026-09-27.** BS0–BS7b accepted (checkpoint: `docs/art/CHECKPOINT_BS5.png`). BS8 (the steel frame) and BS8b (one set of lines) written, waiting on Play Mode and commit with BS7b. After it, the board surface in code is complete; what is left is sprite art (`yard_chair` first).
> Design: `claude/HANDOFF_board_skin.md` (the code route). Sister: `claude/HANDOFF_board_assets.md` (the sprite route); its §1 table is the contract, copied below.
> Target: `docs/art/VISUAL_TARGET.png`, **the simple version since 2026-09-27** (steel frame, no props); the first, ornate one is `docs/art/VISUAL_TARGET_ORNATE.png`, for materials only. Acceptance for every increment is a Play Mode screenshot of the board beside it.
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

- 2026-09-28 — **BS11: a still board** (the designer's call: "remove the lighting effect going across the board every few seconds"; asked which of the two, the answer was the gilt sheen, and the safe-cell glint only every 60 s).
  - **The gilt sheen is gone.** BS5's slow band over the rims, the medallion and the wedges: `View/GiltSheen.cs` and `GiltSheenTests.cs` deleted (with their `.meta` files); `BoardView` no longer adds or clears it, `MatchBootstrap` no longer sets `GiltSheen.Active`. The rims, medallion and wedges are drawn exactly as before, on their default material.
  - **The safe-cell glint (G8f) runs once a minute**, both skins. One lap round the powered cells still takes 3.4 s (`PoweredShine.LapSeconds`, the powered lights' breath) with a 0.7 s sweep per cell; it now repeats every 60 s (`Period`) instead of back to back. The timing moved into a pure `PoweredShine.LocationAt(clock, index, count)`. Lighting effects off or Reduced motion still park it.
  - **Tests.** `PoweredShineTests` (new), 3: every cell glints exactly once, inside the lap; the rest of the minute is still; laps are a minute apart. Cloud compile: core 969/969, 0 warnings everywhere.
  - **Play Mode checklist:** no band crosses the rims, medallion or wedges; the safe cells glint in one quick lap right after the board builds, then nothing for a minute; Classic's safe cells behave the same.

- 2026-09-28 — **BS10: home columns as one strip** (the designer's call: "the home cells have a thick border around them; it causes a perception of misalignment").
  - **Cause.** Each home cell was a raised track tile (0.86 of a spacing) with its own gilt edge and drop shadow, centred in a lattice box a full spacing wide. Round every home cell sat its gilt edge, a dark 0.07 gap and the lattice line, and the shadow, down and right, made the gap uneven: a thick border that looked off-centre against the lattice.
  - **Now** every home cell fills its lattice box up to the lines' inner edges (`BoardView.DecoHomeCellSide` = spacing × (1 − `DecoLatticeWidth`), 0.96), flat, in the seat face, with no edge, bevel or shadow; its ring stays. A column reads as one seat-coloured strip ruled by the lattice, as the target draws it. Track, start and safe cells are unchanged.
  - **The mouth still glints** (G8f): the safe home cell's shine moved from its edge, which is gone, to its ring (or to the painted tile, when `board_cell_home` is filled).
  - **`board_cell_home`** is drawn at the same 0.96 when painted. SPRITE_GUIDE §2 and §4 updated (flat, square corners, no edge; ring at 28 % of the width) and `mask_board_cell_home.png` regenerated to match. `STENCILS_OVERVIEW.png` still shows the old home stencil.
  - **Tests.** `DecoLatticeTests` +2: a home face and its lattice line meet exactly, and a home face is wider than a track tile; no home face touches a wedge or a corner facet, on both boards. Cloud compile: core 969/969, 0 warnings everywhere.
  - **Play Mode checklist:** each home column is a continuous seat-coloured strip with thin gilt lines between its cells and a ring in each; no dark gaps or shadow edges inside it; the column's edges line up with the lattice beside it; the mouth cell's ring glints with the start cells.

- 2026-09-28 — **BS9: declutter** (the designer's call, after the first sprites: "remove the extra border around each table; the visual target has only one subtle line" and "make the cells a bit flatter").
  - **One line round each yard panel.** BS7b edged every panel with a dark band between two gilt hairlines; the target draws one thin gilt line just inside the edge, with its corner triangles. `DecoBoardArt.YardBand`, `YardBandInset`, `YardBandWidth` and `YardOuterLine` are gone, and so is `UiTheme.DecoYardEdge`; `YardInnerLine` (0.05) becomes `YardLine` (0.035) at `YardLineStrength` 0.6, so it stays under the corner ornaments. The panel's edge needs no line of its own: since BS8b it lies on the arms' gilt edges and the frame.
  - **Flatter cells** (about 40 % off BS7's values, which had already taken a third off BS5's): bevel 7 → 5 px; the face's bevel shades pulled toward the face (top/bottom/left/right 0.88/0.55/0.84/0.6 → 0.8/0.63/0.78/0.66); the trim's light and shade 0.18/0.12/0.34/0.27 → 0.11/0.07/0.2/0.16; the drop shadow 0.5 → 0.32, offset (0.04, −0.06) → (0.025, −0.04) tile sizes, falloff 1.3 → 1.2 tiles. Wedge shadows unchanged.
  - **Tests.** `DecoYardTests`: the table clears the one hairline and the corners; `TheBand_SitsBetweenTheTwoHairlines` becomes `TheHairline_IsOneSubtleLine_JustInsideTheEdge`. Cloud compile: core 969/969, 0 warnings everywhere.
  - **First painted slots accepted** (SPRITE_GUIDE phases B and C, AI-generated, cleaned per §5; masters in `docs/art/sources/`): `yard_chair` (grey, levelled to mean 128 for the seat tint); `yard_table_emblem` (grey render mapped to the board's gilt); `board_start_emblem` (the star and ring lifted out of a tile render, lightened to mean 205). Rejected: a framed marble track tile (repeats too loudly, undoes the lowered cells), a thick padded ring (crowds the chairs, which sit inside the table at 0.55 of its radius) and a pawn (not top-down; no slot).
  - **Play Mode checklist:** each yard shows one thin gilt line and its four corner triangles, no dark band; the cells read as inlaid tiles with a soft edge, not raised blocks; highlights and pieces still stand clear of them; Classic unchanged.

- 2026-09-27 — **BS8b: one set of lines** (the designer's catch, on the BS7 screenshot: "the edges of the cross are not perfectly aligned with the outer lines of the square board"). Three edges each stopped at their own place: the arms' tips at 7.62 spacings from the centre (the cross's outline, the cells' 7.5 plus `BoardArt.CrossPad`), the yard panels at 7.35 (inset 0.15 all round, BS7), and the frame's inner edge at 7.7 (BS8's 0.2 clearance); the arms' sides were at 1.62 and the panels' inner edges at 1.65. **Now they share their lines, as the target's do:**
  - the frame's inner edge is the arms' tips (`DecoFrameInner` = half the grid + `CrossPad`; `DecoFrameClearance` is gone);
  - each yard panel is the whole yard block, from its arm's edge to the frame (`DecoYardPanelSide` = `ArmLength`; `DecoYardInset` is gone), so its side is 6.0 and it is centred `CrossPad` further out on both axes than the yard (`DecoYardPanelCentre`). The table stays on the yard's centre, 0.12 off the panel's, too little to read.
  - `BoardArt.CrossPad` becomes public (one keyword and its comment in a Classic file; the value is unchanged).
  - The cross's gilt edge (−25) draws over the panel's dark band (−29), so where they meet there is one line, not two.
  - **Tests.** `DecoFrameTests` +1: the frame's inner edge is the cross's outline, and every panel's outer edges lie on it and its inner edges on the arms' edges, on both boards. `DecoYardTests` now measures from the panel's own centre and allows for the table's 0.12 offset. 54 EditMode tests pass offline; 3 need Unity. Cloud compile: core 969/969, 0 warnings everywhere.
  - **Play Mode checklist:** the arms' tips run straight into the frame; each yard's dark edge sits on its arm's gilt edge and on the frame, with no step or gap anywhere round the board; the frame's shadow falls lightly over the outermost row of cells.

- 2026-09-27 — **BS8 written: the steel frame** (the open item, gap row 9). On the uncommitted BS7b tree; the two go in one commit.
  - **On Deco, `DrawFloor` draws `DrawDecoFrame` where Classic draws its padded oxblood rail** (one branch in `BoardView.cs`, plumbing only; the rail stays on Classic).
  - **The frame:** four runs, like the rail, and for the same reason (G9c's vertex trap: a single frame sprite carries the empty table). Each run is one texture its own length, not tiled, at 120 texels a cell, every texel from one function of its place on the whole frame, so the runs join without seams and all four pivot on the board's centre. Brushed dark gunmetal (`UiTheme.DecoSteelShade` `#0E0D11` → `DecoSteelLight` `#8C8993`, baked), slightly raised in the middle; bevels on both edges, the outer one lit on the top and left runs, the inner one lit on the bottom and right, as the key light from the upper left falls; a dark groove down each run's middle with domed, slotted bolts on it every 2.2 cells, kept off the corners; outer corners chamfered at 45° (0.9 cells); a gilt hairline on the inner lip. A soft drop shadow onto the table, 55 % black, offset down and right.
  - **First pass too light**, caught in a mock-up: a 0.42 face read as pale grey steel against the target's near-black frame. Now 0.22, with the bevels at 0.62/0.06 outside and 0.52/0.08 inside.
  - **Geometry:** the outer edge is exactly where Classic's rail ends (`BoardView.DecoFrameOuter` = the table's edge plus the rail's overhang), so the camera frames the board the same on both skins. The inner edge stands 0.2 spacings past the arms' tips (`DecoFrameInner`, 7.7 on the standard board), over the table's margin, clear of every cell and yard panel. About 0.79 spacings wide on the standard board, twice the rail.
  - **No cyan plaque.** The target's cyan glass plaque on the top rail looks like UI, and the board carries no UI slots (ART §2.2). The always-on cyan the designer allowed stays available for later hardware.
  - **Tests.** `DecoFrameTests` (new), 3, on both boards: the outer edge lies past the table and inside the rail's reach; the inner edge clears every track cell and every yard panel; the frame is wide enough for its bolts and chamfer. All pass offline.
  - **Checks.** Cloud compile on the device's tree: core 969/969; Core, Unity (with and without `DEVELOPMENT_BUILD`), EditorTools and EditTests build with 0 warnings. 53 EditMode tests pass offline; 3 need Unity.
  - **Play Mode checklist:**
    - Deco: a dark steel frame with chamfered corners and bolts round the table; no oxblood rail. Classic: the rail, unchanged.
    - The board sits in the same place and size on screen on both skins (switch mid-match and watch the edges).
    - The frame's inner gilt line doesn't touch an arm's tip or a yard panel.
    - The four runs meet at the corners without a seam, flat and tilted.
    - Watch for: the steel too dark to read as metal, or the bolts too small at phone size (`FrameBoltSize`).

- 2026-09-27 — **BS7b: the yards' edge is dark, not the seat's colour.** On HEAD `2c36b7e`. The designer rejected BS7's seat-coloured band round each yard. **It was my misreading of the target, not a placeholder:** the target draws a dark line round each yard, and its seat colour is in the yard itself (a dark shade of the seat under the table) and in the chairs. Fixed: the band is near-black (`UiTheme.DecoYardEdge`, `#060405` at 95 %; `DecoYardBand` is gone), the gilt hairlines and corner triangles stay, and the panel's lacquer leans further toward its seat (`DecoYardWarmth` 0.08 → 0.14), so the seat colour moves from the edge into the yard. Two lines in `UiTheme`, one in `BoardView.Deco`; geometry and tests unchanged. Cloud compile on the device's tree: core 969/969, all assemblies 0 warnings.
  - **Play Mode checklist:** each yard is a dark seat-shaded square edged by a dark line and thin gilt, gilt triangles in its corners, no coloured border; the table's felt is the one strong seat colour in the yard.

- 2026-09-27 — **BS7 written: the yard panels, and the cells a little lower.** On HEAD `340fd12`.
  - **The cells lowered** (designer: "reduce the cells' elevation just a bit"), about a third off every cue: the trim's bevel light 0.26/0.18 → 0.18/0.12 and shade 0.5/0.4 → 0.34/0.27; the bevel 9 → 7 px of 128; the face's own bevel softened (0.95/0.45/0.88/0.52 → 0.88/0.55/0.84/0.6); the drop shadow 70 → 50 % (`UiTheme.DecoTileShadow`) at a shorter offset (6 %/9 % → 4 %/6 % of a tile). The gilt edge is unchanged, so the tiles still read as tiles.
  - **A panel under every table** (gap row 10, the simple target's yards). Each yard block, less 0.15 spacings on every side (`BoardView.DecoYardPanelSide`: 5.7 spacings on the standard board), is dark lacquer (`UiTheme.DecoYardFloor`, `#100C0F`, leaning 8 % toward its seat), edged by a band in the seat's colour at 75 % (`DecoYardBand`), between a gilt hairline outside it and one inside it, with a Deco triangle in each corner of the inner one: a solid faceted corner and a line across it. Drawn under everything on the table: fill at −31, band at −29, gilt at −28 (`DecoBoardArt.YardBand`, white for the tint, and `YardFrame`, gilt baked, 512 px, at 85 % through `UiTheme.DecoYardGilt` so the rim leads).
  - **Spacing, found in a mock-up:** with the band where it was first put, the table's rim touched it at the middle of each side. The band now sits 2 % in and the inner hairline 5 %, leaving about 0.13 spacings between the rim and the band.
  - **Tests.** `DecoYardTests` (new), 3, on the Standard and the Compact board: no panel overlaps a cell; every table sits inside its panel's band and clear of the corner ornaments; the band sits between the two hairlines. All pass offline.
  - **Checks.** Cloud compile on the device's tree (which includes `2658456`): core 969/969; Core, Unity (with and without `DEVELOPMENT_BUILD`), EditorTools and EditTests build with 0 warnings. 50 EditMode tests pass offline; 3 need Unity.
  - **Play Mode checklist:**
    - Deco: each table sits on a dark panel edged in its seat's colour, gilt corners in the four corners; the yards are no longer bare black. Classic: unchanged.
    - The cells sit a little lower than in `CHECKPOINT_BS5.png`, and still read as raised tiles.
    - Nothing in the panel reaches a track cell; the cross's gilt edge and the panel's hairline don't touch.
    - Seated figures and the seat marks still read over the panel's edge where they come close.
    - Watch for: the seat band too loud beside the felt (`DecoYardBand`); the panel's lacquer too warm (`DecoYardWarmth`).

- 2026-09-27 — **BS6 written: the tune pass** (the checkpoint's first pick). On the uncommitted BS5 tree; the two go in one commit.
  - **A gilt lattice between the tiles.** Per arm: the two lines between its three lanes, the line across its mouth between the last two home cells, and one line across every gap between rows, each 0.04 spacings wide down the middle of the 0.14 gap, in dark gilt (`UiTheme.DecoLattice`), with a light-gilt rivet (0.075) wherever a row line crosses a lane line (`UiTheme.DecoRivet`). The arm's outer edges are the cross's own gilt edge, so they aren't doubled. Drawn at the lanes' order (−24), under the tiles' shadows (−23), so each tile's shadow falls across the lattice as it should. `BoardView.DecoLatticeSegments` / `DecoLatticeRivets` give the geometry in spacings for any arm length: 32 lines and 40 rivets on the standard board.
  - **Calmer tables.** Felt 0.8 → 0.66 of the seat colour (`UiTheme.DecoFeltBrightness`); rim thinner (inner edge 0.855 → 0.87 of the radius, still inside the felt's 0.875 so no seam), its glint 0.4 → 0.25, and the whole rim dimmed through `UiTheme.DecoRimTint` (80 %), a tint rather than new art, so it tunes without rebuilding.
  - **Slimmer, darker wedges.** Waist 0.07 → 0.05, widest 0.2 → 0.13 (all four clearance tests still pass), tinted 82 % (`UiTheme.DecoWedgeTint`), so the four read as spikes rather than one gold X and the medallion stops looking small beside them.
  - **Corner facets.** Each inner corner gets two small triangles filling the corner of the centre square's empty corner cell, split by the wedge's diagonal, each in the colour of the arm it faces (60 % of the seat, `UiTheme.DecoFacetFace`), shaded lighter toward the corner, under the wedge and its shadow. The seat for each side is found from the layout (the home column pointing that way), not assumed. `DecoBoardArt.CornerFacet(upper)`, 64 px.
  - **Tests.** `DecoLatticeTests` (new), 4, on the Standard and the Compact board: every lattice line runs straight down a gap and never touches a cell; every rivet clears every cell; the standard board has the expected 32 lines and 40 rivets; the facets, in all four turns, never enter a cell. All pass offline. `DecoCentreTests` still pass with the slimmer wedges.
  - **Checks.** Cloud compile on the device's tree: core 969/969; Core, Unity (with and without `DEVELOPMENT_BUILD`), EditorTools and EditTests build with 0 warnings. 47 EditMode tests pass offline; 3 need Unity.
  - **Play Mode checklist:**
    - Deco: the cross reads as one latticed panel — a thin gilt line in every gap, rivets at the crossings, the tiles' shadows across it. Classic: unchanged.
    - The tables no longer out-shout the path: deeper felt, a thinner, dimmer rim.
    - The centre: four slim darker spikes, a coloured facet pair at each inner corner in the two neighbouring seats' colours (red and violet at the top right on the standard seating).
    - Highlights, reach and targets still sit clearly on top; the lattice never runs under a piece's feet in a way that reads as a cell edge.
    - Watch for: the lattice too bright against the tile edges (`DecoLattice`), rivets too prominent at phone size (`DecoRivetSize`), the felt now too dark (`DecoFeltBrightness`).

- 2026-09-27 — **The BS5 checkpoint** (designer's screenshot, `docs/art/CHECKPOINT_BS5.png`, Deco, flat, wide, beside `VISUAL_TARGET.png`). Read:
  - **Landed:** the path reads as tiles with gilt edges; the home columns read in their colours with their rings; the start cell's compass reads; figures read on every tile kind.
  - **Off:** the gaps between tiles are black where the target has a gilt lattice with rivets; the table rims and bright felt are the loudest thing on the board; the wedges read as one heavy gold X and make the medallion look small; the yards are empty black around the tables; the oxblood rail is still Classic's.
  - **No contract row flips to sprite-first.** Code is close enough on cells, centre and rims once tuned; the sprite-first rows stay as they were (chair, emblems).
  - **The medallion stays grid-sized** for now: the fault reads as the wedges' weight, not the medallion's size, and BS6 fixes that first.
  - **Order picked** (picker, recommended option): **BS6 the tune pass**, then **BS7 yard panels** (gap row 10), then **BS8 the steel frame** (the open item).

- 2026-09-27 — **BS5 written: weight and a moving light.** On HEAD `9e979f3`.
  - **Tile shadows.** Every Deco tile casts a soft drop shadow (`DecoBoardArt.TileShadow`, 64 px: the tile's chamfered square, solid to just inside its edge and fading over a margin, drawn 1.3× the tile), offset 6 % right and 9 % down of a tile, away from the key light, at 70 % black (`UiTheme.DecoTileShadow`). It lands on the floor between the cells, at the safe glows' order (−23), under the tile. Drawn for painted tiles too: the contract says art never bakes a shadow, so the code draws them all.
  - **Wedge shadows.** Each corner wedge casts a crisp copy of itself at 55 % black (`UiTheme.DecoWedgeShadow`), 0.03 right and 0.05 down, under the wedge (−15). The medallion and the tables already had theirs (BS3, and Classic's felt shadow).
  - **The gilt sheen** (`View/GiltSheen.cs`, new). A slow band of light crosses the table rims, the medallion and the four wedges together, once every 9 s, taking 1.8 s. One shared instance of `ShineLit` for all of them, so one `_ShineLocation` write a frame; the 2D lights still reach them. **Deliberately slow and rare,** so it is never read as `PoweredShine`'s quick glint, which means "powered". Off with Lighting effects or under Reduced motion (`GiltSheen.Active`, set beside `PoweredShine.Active` in `MatchBootstrap`), parked off every sprite when off. `BoardView.Build` clears it with `_shine` (plumbing only; Classic adds nothing to it).
  - **No light pools.** The plan had warm Light2D pools at the tables' corners with candle sprites; the simple target has no candles and its light is even, so they are dropped. Everything that gives weight is painted, so the board still reads finished with the lighting off.
  - **Tests.** `GiltSheenTests` (new), 4: the band crosses during the sweep, is parked for the rest of the period, repeats every period, and stays slow and rare. `DecoBoardArtTests` now checks every Deco sprite, the centre and table art included, at its own size.
  - **Checks.** Cloud compile on the device's tree: core 969/969; Core, Unity (with and without `DEVELOPMENT_BUILD`), EditorTools and EditTests build with 0 warnings. 43 EditMode tests pass offline; 3 need Unity.
  - **Cost.** One extra quad per tile (76 on the standard board) and four per centre; the sheen is one material and one float a frame. No tiled sprites, so the G9c vertex trap doesn't apply.
  - **Play Mode checklist:**
    - Deco: every tile sits on a soft shadow down and right; the wedges cast a crisp one; the board looks lifted off the floor. Classic: unchanged.
    - Watch 10 s: a slow sheen crosses the four rims, the medallion and the wedges together, then rests. The safe cells' quick glint still runs in turn, and the two never read as one thing.
    - Lighting effects off, or Reduced motion on: no sheen and no glint; the shadows stay.
    - Tilted camera and upright phone: the shadows don't smear or crawl.
    - Watch for: the shadows too heavy in the gaps (`UiTheme.DecoTileShadow`); the sheen too bright or too frequent (`GiltSheen.Period`, and the band in `ShineLit.mat`).
  - **The checkpoint (BOARD_SKIN plan, after BS5).** With BS1–BS5 in, the board surface is done in code. **Owed before any further board work:** a Play Mode screenshot of the Deco board (flat, wide) beside `VISUAL_TARGET.png`. That comparison decides which rows of the contract flip to sprite-first, whether the medallion stays grid-sized, and what the yard panels (gap row 10) and the steel frame (open item) need.

- 2026-09-27 — **BS4 written: the Deco tables.** On HEAD `8b84b23`.
  - **On Deco, `Build` draws `DrawDecoTable` for each seat instead of `DrawTable`;** Classic's tables are untouched (D4).
  - **Felt** in the seat's colour at 80 % (`UiTheme.DecoFeltBrightness`; Classic's is 62 %), so each table owns its jewel tone as the target's do. It reuses `BoardArt.Felt` (vignette, a highlight toward the light, fibre, or the painted `board_felt`). A filled `yard_table_felt` replaces it, tinted with the same colour and drawn at 90 % of the table so the rim covers its edge.
  - **The rim** (`DecoBoardArt.TableRim`, 512 px, colours baked): a stepped inner lip, a dark groove, a rounded gilt bead lit from the upper left with one glint at 128°, and a dark outer edge; inside it, a faint engraved gilt line on the felt at 79 % of the radius. `yard_table_rim` replaces it.
  - **The centre:** a gilt compass (the start cells' `DecoBoardArt.Compass`, tinted `UiTheme.DecoTableEmblem`, 1.25 spacings) in the home cells' engraved ring drawn at 1.5 spacings. `yard_table_emblem` replaces the compass.
  - **Chairs (D3):** with `yard_chair` filled, one chair per seat at `BoardLayout.YardSeat`, under the seated figure, turned so its seat faces the table (`BoardView.DecoChairRotation` = seat angle − 90°, for art drawn facing down) and tinted `UiTheme.DecoChairTint` (the seat colour, 15 % toward white). Empty slot: Classic's seat marks, as D3 says.
  - **Not drawn on Deco:** Classic's dotted ring, arc, dealer's spot and felt chips. The target's tables are quieter, and the simple target drops chips altogether.
  - **Tests.** `DecoTableTests` (new), 3: a turned chair faces the centre from all eight seats; the centre ring clears every chair and every chair stays inside the felt's gilt line, which stays inside the rim; chairs never overlap even with a squad of eight. All pass offline.
  - **Checks.** Cloud compile on the device's tree: core 969/969; Core, Unity (with and without `DEVELOPMENT_BUILD`), EditorTools and EditTests build with 0 warnings. 39 EditMode tests pass offline; 3 need Unity.
  - **Deviation from the target, for when the chair art is made:** the target's chairs are large curved armchairs *around the outside* of the rim, and its pawns stand on the felt. Ours sit under the seated figures at `YardSeat` (55 % of the radius, on the felt), because the figures sit there and moving them changes shared geometry (`BoardLayout.SeatRadius`, Classic too). The yard block leaves only half a spacing outside the rim, so a ring outside would not fit either. The `yard_chair` prompt should ask for a compact armchair seen from above, not the target's sofa arcs.
  - **Play Mode checklist:**
    - Deco: four tables in their seat colours with a heavy gilt rim and a gilt compass; no dotted ring, arc, spot or chips. Classic: unchanged.
    - Seated figures read on the brighter felt at phone size, in all four colours (Revú on red, ART §5.1); standing up leaves a readable seat mark.
    - The rim's glint sits at the upper left on all four tables, flat and tilted; the rim doesn't shimmer when the tilted camera settles.
    - The felt's gilt line never runs under a seated figure's face.
    - Watch for: the felt too bright against the tiles (`DecoFeltBrightness`); the rim too loud for the gold budget; the compass fighting a seated figure.

- 2026-09-27 — **The target is replaced by its simple version** (designer; picker, recommended option). `VISUAL_TARGET_UPDATED_SIMPLE.png` became `VISUAL_TARGET.png`; the first one is `VISUAL_TARGET_ORNATE.png`. The board, the tables and the chairs are the same; the frame is dark steel instead of gilt; the candles, plants, instrument and chips are gone. **Consequences:** BS6 (props) is **parked** and the four `prop_*` slots with it (kept in code, harmless when empty); BS5's warm light no longer hangs on candle sprites; the frame (open item) is steel, which stops it spending the gold budget. New in the simple target and not yet planned: each yard block is a dark panel edged in its seat's colour, with gilt corner ornaments. It joins the gap table as row 10. `ART_DIRECTION.md` §10 ref 5 and its status history updated.

- 2026-09-27 — **BS3 accepted** (designer: all green). **Its code landed inside `8b84b23`**, whose message names only BS2: the working tree held BS3 when it was committed. `DecoCentreTests` was left out of that commit and goes in with BS4.

- 2026-09-27 — **A stale `.git/index.lock`** was left by a read-only `git diff` from the device shell (the LFS filter isn't installed there, so git died holding the lock), the gotcha `HANDOFF_launch_ui.md` §2 warns of. It was moved to `Temp/stale_index.lock` (the device shell can't delete) before it could block a commit. Rule kept: never `git diff` from the device.

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
  | 8 | Props | Candles, chips, plants, instrument (ornate only; none in the simple target) | None beyond the felt's chips | BS6, parked |
  | 9 | Table and frame | Gilt frame, rails, tray (ornate); **dark steel frame** (simple target, from 2026-09-27) | Black carpet, the oxblood rail as a thin red line | BS8 |
  | 10 | Yard blocks (simple target) | A dark panel per yard, edged in its seat's colour, gilt corner ornaments | Bare carpet | BS7 |

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
