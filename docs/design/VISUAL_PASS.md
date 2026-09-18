# Nona Royale — Visual Pass

> Location in repo: `docs/design/VISUAL_PASS.md` · Project copy: `claude/VISUAL_PASS.md`
> Status: **Open, 2026-09-18.** V4 (the colour grade) is in. V3's room and title layout are chosen from mockups and not yet built. V0 (the lighting spike) is written and waits for the designer's check, which blocks V1.
> Related: `ART_DIRECTION.md` §3, §6, §6.1, ADR-0009 (figures rendered looking down about 25°), ADR-0010 (URP 2D Renderer), `LIGHTING.md`, `GUI_PHASE.md` (G2–G4)

## Goal

Make the first thing people see about the game look finished: the board, its perspective, the light, and what surrounds the table. Gameplay is out of scope for this thread.

## Decisions (2026-09-17, from pickers)

1. **Perspective: a tilted table.** The camera looks down the board at about **40°** from vertical in a match, and more steeply (about **58°**) on the title. The figures stand up and face the camera.
   - **The top-down view stays** as a fallback, not deleted (designer). The tilt ships as a view mode beside it.
   - If the tilt fails, the fallback is "top-down, deeper": the flat camera with depth cues.
2. **Room backdrop, menu screens first.** In a match the HUD covers almost all of the room (mockups 2 and 3 are nearly the same), so the room is built for the title, setup, draft and end screens. The match camera shows only the edges. The designer is "not sure" of the room yet; it gets its own mockup review before it is built.
3. **Lighting and finish: colour grade and surface sheen.** A URP colour grade (edge falloff, warm split-tone, light grain) off with Lighting effects; normal maps on the marble, gilt and felt so the lights catch them. Figure shadows and light shafts were not picked.
4. **Mockup first**, then code, for every step.

## Mockups (2026-09-17)

Rendered in Blender with the scripts in `tools/mockup/` from the G3 board texture, with Luka standing in for every operator and a numpy post pass for grade, bloom, falloff and grain. Sent in the chat as `tilt_mockups.png`.

1. **Title:** the table in a Deco room at 58°: back columns with sconces, velvet curtains, a tiered chandelier, dark carpet.
2. **Match, room, 40°**, with the 1080p HUD footprint (top 56, bottom 196, left 290, right 78).
3. **Match, void, 40°**, same footprint.

**Findings:**

- **The tilted board is bigger on screen.** Fitted into the HUD's free area, it comes out about 800×520 px at 1080p against about 500×500 px for the flat board, because the near edge uses the width the square board leaves empty.
- **The fit has to be computed.** Perspective makes the near edge the widest part; framing that only looks at the board's centre crops the near yards under the tray. The mockup searched camera distance and aim for the largest board whose eight table corners (top and bottom of the slab) sit inside the free rectangle, with the lens shifted to the rectangle's centre. The game should do the same.
- **Figures read well** as upright cards leaned back by about half the camera tilt.
- **The room is mostly a menu-screen asset** (see decision 2).

## V3 mockup review: the room and the title layout (2026-09-18)

Decision 2 said the room gets its own review before it is built. Four rooms were
rendered at the title's 58°, then the title screen's real lockup was laid over them
with `tools/mockup/title.py`, which reads its sizes off `View/TitleScreen` and
`View/UiTheme` so a mockup is judged as the screen and not as a bare room. Sent in
the chat as `v3_rooms.png`, `v3_lockup.png` and `v3_salon.png`.

**The four rooms:**

| | Room | Verdict |
| - | ---- | ------- |
| A | Parlour: five back columns, four curtain panels, a tiered chandelier | The ring of sconces is the best thing in the set — it reads as a room of tables going off into the dark. The columns are invisible silhouettes and the curtain hem zigzags. |
| B | Vault room: a Deco relief wall and a round brass vault door | **Failed as built.** The door read as a pale face, not brass, and the rest of the wall went black. The idea is on-theme; the execution needs another pass before it can be judged. |
| C | Curtain call: one velvet wall, one low chandelier | Strongest single image, and the cheapest real room. The chandelier is the best-built object. The velvet read as a flat maroon field. |
| D | Bare: a patterned floor and one far glow | Nearly free and not bad, but the procedural ring pattern read as horizontal banding, which looks like a bug rather than a Deco floor. |

**Findings:**

- **A lit floor kills the room.** The first pass used a large area lamp as the
  figures' key. At the title's grazing angle it washed the carpet into a milky grey
  haze and the room stopped reading as dark. The key and the rim are spots now, aimed
  at the table, and the carpet's sheen is off — sheen at a grazing angle turns a dark
  carpet into a retroreflective sheet. This is the one lighting lesson to carry into
  Unity: light the table, not the room.
- **The centred lockup buries the board.** `TitleScreen` stacks the wordmark and all
  three buttons in a 520-wide column at the centre of the screen, over a 0.45 scrim.
  Laid over any of the four rooms, it covers the board almost entirely — the same
  problem the match HUD has. Whatever room is built behind it would be about 85%
  invisible, which is reason enough to question building one at all.
- **Velvet only reads from the side.** The folds are displaced in depth, so they all
  face the table equally and light from the front leaves them flat. Two spots raking
  *along* the cloth are what make the folds shade.
- **The room's hero object and the wordmark want the same place.** A single chandelier
  dead centre lands exactly where NONA ROYALE goes. A symmetrical *pair*, flanking the
  wordmark, keeps the Deco symmetry and leaves the middle of the upper frame free.

**Chosen (designer, from pickers):**

1. **The room is C plus A's sconces** — `ROOM=salon` in `scene.py`: a velvet wall with a
   pelmet and a skirting at the hem, a pair of chandeliers flanking the wordmark, and a
   ring of sconces on side columns framing the table.
2. **The title layout is the wordmark on top and the buttons in a row along the bottom**,
   with the board whole in the middle and a lighter scrim (0.20, not 0.45). This is the
   only layout in which the board is fully visible, and it is what makes the room worth
   building. `FIT=title_top` is the band it leaves: x 0.08–0.92, y 0.20–0.76.

**Noticed in passing, for the designer:**

- The tagline reads "Nine operators. One vault." and the roster is eleven.
- `TitleScreen` shows the table **empty**. Every mockup above has figures standing on it,
  and that is a good part of why they look alive. V3 should draw a posed table behind the
  menu screens.

## Risk: 2D lights under a perspective camera

Forum reports (Unity 2021.1 onward) say URP 2D point and spot lights have no effect with a perspective camera; only the global light remains. If that holds on Unity 6.6 / URP 17.6, the tilted view would lose LT1's pools and cyan cells and all of LT2. It has to be checked in the editor before V1 is built.

## Increments

| #  | Increment | What it delivers |
| -- | --------- | ---------------- |
| V0 | **Lighting spike** | `View/PerspectiveSpike` (editor and development builds only): F9 tilts the main camera into perspective over the current board, F10 cycles 30°/40°/48°, F9 restores it. Answers one question: do the 2D lights still work? |
| V1 | **Tilted view mode** (if V0 passes) | A view-mode setting (Top-down or Tilted, top-down kept). A camera rig with the pitch and the fitted framing; board clicks by ray against the board plane; HUD overlays placed through the tilted camera; figures standing and facing the camera, sorted by depth. |
| V2 | **Table body** | The table's thickness and its gilt band, seen along the near edge; contact shadows under figures. |
| V3 | **Room for the menu screens** | The chosen salon (velvet wall, a pair of chandeliers, a sconce ring on side columns) behind the title, setup, draft and end screens; the title's lockup moved to the top with the buttons in a bottom row and the scrim at 0.20; a posed table instead of an empty one; and a camera move from the room shot into the match framing. Mockups reviewed and chosen 2026-09-18. |
| V4 | **Colour grade** ✅ | URP Volume overrides beside the existing bloom: tonemapping, split toning, edge falloff (vignette), light grain. Off with Lighting effects. |
| V5 | **Surface sheen** | Normal maps for the board's procedural sprites, generated from the same shapes, so 2D lights pick out marble, gilt and felt. |

If V0 fails: decide between "top-down, deeper" and moving the board to URP's 3D renderer, which would need a new ADR against ADR-0010.

## Log

- 2026-09-17 — **Pass opened.** Designer: "work on the board, lighting, decoration, perspective … anything visual that will solidify the first thing people see." Decisions 1–4 above.
- 2026-09-17 — **Mockups rendered** (three frames above) and approved: tilt at 40°, the room on the menu screens first, the top-down view kept as a fallback.
- 2026-09-17 — **V0 written: `View/PerspectiveSpike`.**
  - It creates itself at scene load (`RuntimeInitializeOnLoadMethod`), in the editor and development builds only (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`). No scene wiring, no other file touched.
  - F9 saves the camera, switches it to perspective (FOV 30) and places it every `LateUpdate` at order 10000, after the framing and the camera nudge. The camera is tilted about x and aimed slightly below the board's centre, at a distance that fits the board's width. F10 cycles the tilt. F9 again restores the saved camera.
  - It logs the 2D lights it finds (local, global, enabled) on each toggle.
  - Known while tilted: pieces lie flat and board clicks miss. The spike only shows the light.
  - Compiles with and without the editor define.
  - **The check (designer):**
    1. Deal a match and press F9.
    2. Do the warm pools still show over the vault, arms and tables?
    3. Do the safe cells still glow cyan, and bloom?
    4. Select a piece, then cast and knock something out (select before F9 if clicks miss): do the LT2 lights show?
    5. Press F10 twice and look at 30° and 48° too.
    6. Screenshots of 40° with Lighting effects on and off would help.
- 2026-09-18 — **V4 in: the colour grade.** `View/SceneLighting` now builds the whole grade into the one global Volume it already made for bloom, so nothing new is wired and nothing new is spent: the Volume was already there.
  - **Neutral tonemapping**, not ACES. ACES crushes the room's near-black surfaces into flat black and pulls the gold orange; Neutral keeps the marble's veining readable and the gilt gold.
  - **Colour adjustments**: `+0.25` stops exposure, `+8` contrast, `−4` saturation, a warm `(1, 0.98, 0.94)` filter. The desaturation is what stops the felt reading as green plastic.
  - **Split toning**: shadows to the room's aubergine `(0.17, 0.07, 0.13)`, highlights to lamplight `(1, 0.95, 0.86)`, balance `−15` so the shadows take more of the frame.
  - **Vignette** `0.32` at `0.45` smoothness — the edge falloff the mockups were graded with. It darkens the corners the HUD sits in, which flatters the docks.
  - **Film grain** Thin1 at `0.12`, response `0.8`. It is there to keep the near-black floor from banding on 8-bit displays, not to be seen.
  - **Off with Lighting effects.** The overrides live inside `Apply()`'s existing `if (_volume != null)` block and the Volume is disabled with the pools and bloom, so the cheap path is unchanged.
  - **Reduced motion drops the grain** to 0. URP's grain scrolls every frame and has no still setting, so stilling it means turning it off.
  - Every field is read each frame and sits under a `Colour grade (V4)` header, so the grade can be dragged in the inspector during Play Mode like the rest of the lighting.
  - **The check (designer):** deal a match and look at the table's dark quarters — the corners should fall off without going muddy, the gold should stay gold, and turning Lighting effects off should snap the frame back to the flat look. If the room reads too dim, `gradeExposure` is the one dial to move.
- 2026-09-18 — **V3 mockups reviewed and the room chosen.** Four rooms rendered at the
  title framing and judged with the real lockup over them (see the V3 review section
  above). The designer chose the salon and the top-wordmark layout. Two new mockup
  tools: `tools/mockup/title.py` (the lockup, read off `TitleScreen` and `UiTheme`,
  with `LAYOUT=center|left|top` and `LOCKUP=0` for a room-only frame) and
  `tools/mockup/sheet.py` (contact sheets). `scene.py` gains `ROOM`, `CARPET`,
  `FIT=title_top`, and spots in place of the area key and rim. Not yet built in Unity.
