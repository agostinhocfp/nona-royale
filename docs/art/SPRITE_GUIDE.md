# Nona Royale — Board sprites with AI: roadmap and guide

> Location in repo: `docs/art/SPRITE_GUIDE.md` · Project copy: `claude/SPRITE_GUIDE.md`
> Written 2026-09-28, after the BS-series finished the Deco board in code (`docs/design/BOARD_SKIN.md`).
> **Supersedes §2–§5 of `claude/HANDOFF_board_assets.md`** (generating, cleaning, importing, checks). Its §1 contract still stands; §2 below restates it with what the code now actually does with each slot.
> Reference kit, in the repo: `docs/art/reference/` (crops of the target) and `docs/art/stencils/` (the exact shapes the code draws).

## 0. How this works

The board is finished in code. A painted sprite only **replaces** one piece of it:

1. Drop a PNG named exactly after its slot into `Assets/_Project/Art/Resources/Art/Board/`.
2. The importer sets it up (sprite, size, mipmaps, filtering), and the size warning tells you if it's off.
3. Restart Play Mode. On the **Deco** skin the painted piece replaces the code-drawn one. Move the file out and the code-drawn one comes back. **Nothing breaks either way.**

So every asset is an A/B test against code that already works. **Keep an asset only if it beats the code at phone size.**

**The one rule that decides everything:** the code owns geometry and light. Art paints a surface and nothing else. That means:

- no drop shadow;
- no text;
- no neighbouring cell or object;
- no perspective;
- no lighting from anywhere but the upper left.

The code draws every shadow, places and rotates everything, and tints the greyscale slots per seat.

## 1. Roadmap

In order of what it buys on screen. Each phase ends with a Play Mode screenshot beside `VISUAL_TARGET.png`.

| Phase | Slots | Why this order | Effort |
| --- | --- | --- | --- |
| **A. Set up once** | — | A style kit makes every later asset match the target: reference crops, a trained style (if your generator can), a prompt frame and the stencils. | 1–2 h |
| **B. The chair** | `yard_chair` | **The biggest remaining gap.** The target's tables are ringed with leather; ours show dark seat marks. Code-drawn chairs won't get there (D3). | 2–3 h |
| **C. The emblems** | `yard_table_emblem`, `board_start_emblem`, `board_medallion_emblem` | Small, high-visibility, and code only approximates them (a compass and a diamond). Painted Deco ornament is where AI is strongest. | 2–3 h for all three |
| **D. Felt grain** | `board_felt` (texture) | Four big tables show their felt; a real grain adds the "material" the target has. It's the one texture Deco still shows widely. | 1 h |
| **E. Optional A/B upgrades** | `yard_table_rim`, `board_medallion`, `board_corner_wedge`, `board_cell_home`, `board_cell_track` | Code already does these well. Try one only if you want more finish; **keep it only if it wins side by side.** | 1 h each |
| **F. Skip on Deco** | `board_marble`, `board_carpet` | On Deco the marble only shows in the gaps and the carpet is under the panels and frame. Worth it only for Classic. | — |
| **Parked** | `prop_candle`, `prop_chips`, `prop_plant`, `prop_instrument` | The simple target has no props (BS6 parked). The slots stay; leave them empty. | — |

**Not in this guide:** operators. They follow ART §5 and ADR-0009 (Blender renders), a separate pipeline.

## 2. The slots, as the code uses them today

**Stencil:** the exact shape the code expects, in `docs/art/stencils/`, white on black at 1024 px (see `STENCILS_OVERVIEW.png`). **Size:** the final PNG. Always generate bigger (1024+) and downscale.

| Slot | Final px | Colour | What the code does with it | Stencil and safe zone |
| --- | --- | --- | --- | --- |
| `yard_chair` | 256 | **Grey**, tinted | One per seat, drawn 0.95 cells wide **under the seated figure**, turned to face the table. Tinted with the seat colour lifted 15 % to white. | `mask_yard_chair`: back along the **top**, arms down both sides, seat opening toward the **bottom** (the table). The grey middle is where the figure sits; the back and arms must read around it. |
| `yard_table_emblem` | 256 | Colour (gilt) | 1.25 cells wide at the table's centre, inside the code's engraved ring. | `mask_yard_table_emblem`: keep the ornament inside the circle. |
| `board_start_emblem` | 256 | **Grey**, tinted | 72 % of a start tile, tinted with the seat colour lifted 60 % to white, on a seat-coloured tile. | `mask_board_start_emblem`. Paint it **light** grey to white: it has to stand out on its own seat colour. |
| `board_medallion_emblem` | 512 | Colour (gilt) | Drawn **the same size as the medallion**, over it. | `mask_board_medallion_emblem`: stay inside 72 % of the radius, or it covers the rim. |
| `board_felt` | 1024, tileable | **Grey** (brightness only) | Baked into every table's felt; its average brightness is divided out, so only the grain shows. | None: a seamless square. Grain within about ±10 % of the mean. |
| `yard_table_rim` *(opt.)* | 1024 | Colour | The table's full diameter, dimmed to 80 %. | `mask_yard_table_rim`: ring from **87 %** of the radius to the edge; transparent centre. |
| `board_medallion` *(opt.)* | 512 | Colour | 1.05 cells: the grid leaves no more room. | `mask_board_medallion`: a disc to the edge. The outer ring (from 80 %) is the rim; the grey middle is the face under the emblem. |
| `board_corner_wedge` *(opt.)* | 256 | Colour | The square from the board's centre (bottom-left) to the inner corner (top-right), turned 4 times. It also casts its own shadow. | `mask_board_corner_wedge`: **stay inside the white kite.** The red blocks in the overview are cells: a wider wedge would cover them. |
| `board_cell_home` *(opt.)* | 256 | **Grey**, tinted | Every home cell, tinted with the **full** seat colour, filling its whole lattice box (0.96 of a spacing, BS10): the column reads as one strip ruled by the lattice. Replaces the face **and** its ring. | `mask_board_cell_home`: fill edge to edge, **square corners, no edge or bevel** (the lattice is the edge), and the ring at 28 % of the width painted in. |
| `board_cell_track` *(opt.)* | 256 | Colour | Every plain track tile, as painted (untinted). Replaces the tile and its gilt edge. | `mask_board_cell_track`: fill edge to edge; paint a thin gilt edge and a **soft** top-left bevel (the cells were lowered in BS7). |

**Grey slots:** paint in colour if that's easier, then desaturate and set the average to about **50 %** grey (§5, step 5). The code multiplies in the seat colour, so a dark grey gives a muddy seat and a light grey washes it out. The exception is `board_start_emblem`, which should be light.

## 3. Phase A — the style kit (once)

1. **Reference crops.** `docs/art/reference/` holds ten crops of the target:
   - `ref_chair`, `ref_table`, `ref_table_emblem`, `ref_yard_panel`, `ref_medallion`, `ref_centre`;
   - `ref_start_cell`, `ref_track_tiles`, `ref_home_column`, `ref_frame_corner`.

   They are upscaled, so they're soft. Use them as **style** references, never as the thing to trace.
2. **A trained style, if your generator has one** (Scenario calls it a custom model; others call it a style LoRA or a style reference set). Train on `VISUAL_TARGET.png` plus the ten crops. It is the single biggest win for consistency across 10+ assets. No training? Attach the whole target plus the one matching crop as the style reference on every generation.
3. **Stencils as structure.** Most generators accept a second image that fixes the composition. It goes by names like structure reference, image guidance, ControlNet (edge, depth or scribble) or composition reference. Feed it the matching `mask_*.png`:
   - **Moderate strength** (around 0.4–0.6): enough to hold the silhouette, not so much that it goes flat.
   - **Keep the stencil** as a layer in Photopea for cleaning (§5).
4. **The prompt frame** (put the slot's subject in the brackets):

   ```
   [subject], strict top-down orthographic view, single isolated object centred on a plain flat neutral grey background, no text, no numbers, no cast shadow, soft key light from the upper left, polished stylized game art in the finish of Hearthstone and Marvel Snap, Art Deco, noir casino, near-black lacquer and aged gilt gold, [material/colour]
   ```

   **Negative:**

   ```
   perspective, tilted camera, three-quarter view, cast shadow, drop shadow, multiple objects, border, frame, background scene, text, letters, watermark, signature, photorealistic, photo, anime, pixel art, blurry, noisy, cluttered
   ```

5. **Settings that hold across assets:**
   - square (1:1), the largest your plan allows (1024 or more);
   - 4–8 candidates per prompt;
   - lock the **seed** of a winner and change one word at a time to iterate;
   - keep the same style strength across the whole set.

## 4. The prompt pack

Each entry: the subject for the frame, extra negatives, and what to pick for.

**B. `yard_chair`** (stencil `mask_yard_chair`, reference `ref_chair`)
- Subject: *"one compact Art Deco club armchair seen from directly above, rounded padded backrest along the top edge, two padded armrests down the sides, open seat facing the bottom edge, tufted leather in neutral mid-grey, thin aged-brass piping"*.
- Negative add: *"sofa, bench, curved sectional, person, cushion pile, table"*.
- **Pick for:** the back and arms reading as a U at 64 px; an even grey with no strong colour cast; an open bottom edge. The target's chairs are long curved sofas. **Don't copy that:** ours sit one per seat under a figure (BOARD_SKIN BS4).

**C1. `yard_table_emblem`** (stencil `mask_yard_table_emblem`, reference `ref_table_emblem`)
- Subject: *"an Art Deco compass star emblem inside a thin diamond outline, eight points with four long, aged gilt metal with bevelled facets, flat on a transparent background"*.
- **Pick for:** crisp facets, perfect symmetry (§5 fixes small misses), not too thin to read at 40 px.

**C2. `board_start_emblem`** (stencil `mask_board_start_emblem`, reference `ref_start_cell`)
- Subject: *"an Art Deco eight-point compass rose emblem with a small ring round its centre, pale ivory metal"*.
- Then desaturate to light grey (§5).
- **Pick for:** a bold silhouette, because it sits on a small seat-coloured tile. Fewer, thicker points beat fine detail.

**C3. `board_medallion_emblem`** (stencil `mask_board_medallion_emblem`, reference `ref_medallion`)
- Subject: *"an Art Deco gilt diamond emblem with a stylized sunburst star inside it, bevelled aged gold, flat on transparent"*.
- **Pick for:** reading on black at about 60 px. It's the centre of the board.

**D. `board_felt`** (no stencil)
- Subject: *"seamless tileable texture of casino table felt, fine short nap, even neutral grey, very subtle fibre, flat lighting, no pattern, no logo"*.
- Negative add: *"seams, vignette, stains, folds, pattern, border"*.
- **Pick for:** even grain with no blotches. Blotches become visible stains on all four tables.

**E. Optional:**
- **`yard_table_rim`:** *"a heavy round bevelled aged-gilt ring, top-down, polished bead, one soft highlight at upper left, empty transparent centre"*. Stencil `mask_yard_table_rim`.
- **`board_medallion`:** *"a round black lacquer disc with a heavy bevelled aged-gilt rim, soft specular at upper left"*. Stencil `mask_board_medallion`.
- **`board_corner_wedge`:** *"a slim Art Deco gilt spike ornament pointing to the upper right corner, faceted down its spine"*. Stencil `mask_board_corner_wedge`; **clip it to the kite** in §5.
- **`board_cell_home`:** *"a flat square of neutral grey lacquer, edge to edge, no border, thin engraved gilt ring centred"*. Stencil `mask_board_cell_home`, then grey to 50 %.
- **`board_cell_track`:** *"one square board-game tile of near-black polished marble, very thin bevelled aged-gilt edge, subtle"*. Stencil `mask_board_cell_track`. **Pick the quietest** candidate: highlights must pop over it.

## 5. Cleaning (Photopea is enough)

For every asset, in order:

1. **Open** the candidate and the matching stencil. Put the stencil on top as a layer at 40 % opacity, in the Difference blend mode, to see mismatches.
2. **Remove the background** (Photopea: Select › Magic Cut or Remove BG, or your generator's own). Zoom to 400 % and **check the edge over black and over white**: grey or white halo pixels show up on the dark board. Contract the selection by 1–2 px if needed.
3. **Fit to the stencil.** Scale and centre until the silhouette matches, then **clip with the stencil** (Ctrl-click the stencil to select it, invert, delete). This is compulsory for `board_corner_wedge`, `yard_table_rim` and the emblems' safe circles.
4. **Symmetry, for emblems.** Cut the image in half, duplicate, flip horizontally and align; do the same vertically for four-way symmetry. AI emblems are never quite symmetric, and asymmetry reads as cheap.
5. **Grey slots:**
   - **All of them:** Image › Adjustments › Desaturate.
   - **`yard_chair` and `board_cell_home`:** then Levels until the **Histogram** panel's mean sits near **128** (about 50 %).
   - **`board_start_emblem`:** aim for 190–220 instead.
   - **`board_felt`:** its mean doesn't matter, since the code divides it out; only keep the grain within about ±10 %.
6. **Remove any painted shadow** under or beside the object. The code adds its own; a baked one doubles up and points the wrong way when the camera tilts.
7. **Resize last**, to the final size in §2, with Bicubic Sharper (or Lanczos). Keep square. Transparent corners stay transparent.
8. **Export PNG** named **exactly** after the slot, e.g. `yard_chair.png`. Keep your 1024 master under `docs/art/sources/` for later edits.

**Tileable textures** (`board_felt`):
1. Filter › Other › Offset by half the width and height.
2. Heal any seam that now crosses the middle.
3. Offset back and check again.
4. Blur very slightly if the grain crawls at small sizes.

## 6. Import and test

1. Drop the PNG into `Assets/_Project/Art/Resources/Art/Board/`. The console should say `[BoardSprites] Import settings applied …` (plus a size warning if it's not the contract size; it still works).
2. **Restart Play Mode.** Sprites are cached per session.
3. Settings › Display › **Board skin: DECO**.
4. Check, at your normal window and then with the Game view at phone size:
   - it reads at a glance, and at a squint at 64 px;
   - **all four seats**, for tinted slots;
   - pieces on or beside it still read (ART §5.1: Revú on red, Luka on gilt);
   - the turn's highlights still pop over it;
   - flat **and** tilted camera; Lighting effects **on and off**.
5. **A/B against the code:** move the PNG to `Assets/_Project/Art/BoardDrafts/` (outside `Resources`, so it doesn't load), restart Play Mode, and compare two screenshots. Keep the winner.
6. Commit the PNG **with its `.meta`**.

## 7. Provenance and rights

- One row per shipped asset in `docs/art/PROVENANCE.md`: slot, tool and model, plan tier, date, prompt (short), seed.
- Check that your generator plan grants **commercial use** of outputs. Some free tiers don't.
- Steam asks about AI-generated content at submission. The provenance rows are what you answer from.

## 8. Reviewing with me

For each slot, send:
- **the 3–4 best candidates at 1024**, before cleaning: I'll pick against the target at 64 px and say what to fix;
- then the **cleaned PNG** and a **Play Mode screenshot** (Deco, flat, wide) beside `VISUAL_TARGET.png`.

I log each accepted asset in `BOARD_SKIN.md` and its status in the asset handoff's §6 table. If a slot needs the code to change (a size, a tint, a new safe zone), I do it then.

## 9. Troubleshooting

| You see | Cause | Fix |
| --- | --- | --- |
| No change after dropping a file in | Play Mode wasn't restarted, or the name is off by a letter | Restart Play Mode; the console warns "not a contract slot" on a wrong name |
| Console: "is not imported as a Sprite" | The file was imported before as a plain texture | Delete its `.meta`; Unity re-imports it with the right settings |
| Tinted chair or tile too dark or muddy | Grey mean well under 50 % | Levels up to a mean near 128 |
| Tinted slot looks washed out | Grey mean too high | Levels down |
| White or grey fringe round an emblem | Halo from background removal | Contract the selection 1–2 px and delete |
| The wedge covers the edge of a cell | Art wider than the kite | Clip with `mask_board_corner_wedge` |
| The chair faces the wrong way on some seats | Drawn facing up or sideways | Redraw or rotate so the seat opening faces the **bottom** edge; the code turns it per seat |
| Emblem covers the medallion's rim | Emblem bigger than 72 % of the radius | Clip with `mask_board_medallion_emblem` |
| Visible seams or stains repeating on the felt | Not seamless, or blotchy grain | Offset-and-heal (§5); pick a flatter candidate |
| Looks great full size, mush on a phone | Too much fine detail | Pick bolder shapes; test at 64 px before cleaning |
