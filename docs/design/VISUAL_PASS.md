# Nona Royale — Visual Pass

> Location in repo: `docs/design/VISUAL_PASS.md` · Project copy: `claude/VISUAL_PASS.md`
> Status: **Open, 2026-09-18.** V0 passed and is retired. V4 (the colour grade), V1a (the tilted camera), V1b (standing figures), V1c (the overlays) and V3a (the salon) are in; V2 and V3b (the title layout) remain. V3's room and title layout are chosen from mockups and not yet built.
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

## Risk: 2D lights under a perspective camera — **closed, 2026-09-18**

Forum reports (Unity 2021.1 onward) say URP 2D point and spot lights have no effect with a perspective camera; only the global light remains. If that held on Unity 6.6 / URP 17.6, the tilted view would have lost LT1's pools and cyan cells and all of LT2.

**It does not hold.** The designer ran V0 in Play Mode: the safe cells still glow cyan and still bloom, and LT2's event lights still show. That is the harder case of the two — the powered-cell lights are the only ones in the scene restricted with `targetSortingLayers`, and they are additive — so the warm pools, which are plain point lights on multiply targeting every layer, are covered by the same result. The vault's pool is visible in the screenshots. The tilt is viable, and V0 was deleted once V1a replaced it.

Two things the spike showed that were not being asked about:

- **The world-anchored HUD already projects correctly.** Health readouts and status tags landed on the right pieces while tilted, because they go through `WorldToScreenPoint`, which does not care which projection the camera uses. That was on V1's list and turned out to be free.
- **`AudioDirector.PanOf` gave up on a non-orthographic camera** and returned dead centre for everything, so the spike was silently mono. Fixed in V1a.

## Increments

| #  | Increment | What it delivers |
| -- | --------- | ---------------- |
| V0 | **Lighting spike** | `View/PerspectiveSpike` (editor and development builds only): F9 tilts the main camera into perspective over the current board, F10 cycles 30°/40°/48°, F9 restores it. Answers one question: do the 2D lights still work? |
| V1a | **Tilted camera** ✅ | `View/TiltFraming` and the Board camera setting (Top-down or Tilted, top-down the default). The perspective branch in `FrameCamera`; board clicks by ray against the board plane; the camera nudge shoved along screen axes; stereo panning measured on screen. Pieces still lie flat. |
| V1b | **Figures stand up** ✅ | `View/FigureTilt` and `View/BoardTilt`. A `SortingGroup` per piece with its order taken from world y; the figure leaning up out of the table about its feet; the hop rising on screen; the seat disc drawn as a true circle for the camera to foreshorten; and piece hit testing in screen space, without which a click on a standing figure selects the cell behind it. |
| V1c | **Overlays** ✅ | Everything that offsets from a world position along "up": `PieceHudLayer`'s health and tags, `FloatingText`'s damage numbers and `CastTell`'s falling diamond now read `BoardTilt.ScreenUp`. `CellLabelLayer`, `DiceRoller` and `FeedbackLayer` turned out to need nothing — see the log. Plus a ceiling on the piece readouts, which were colliding with the turn banner under the flat camera too. |
| V2 | **Table body** | The table's thickness and its gilt band, seen along the near edge; contact shadows under figures. |
| V3a | **The salon** ✅ | `RoomArt` and `RoomBackdrop`: a **flat** velvet wall with pelmet, gilt rail and skirting, a pair of chandeliers flanking the wordmark, and a ring of sconces on side columns. Behind every menu screen. The Board camera setting became match-only so the menus are always tilted, without which none of it is visible. The table stays **empty** (designer). |
| V3b | **The title layout** | The lockup to the top with the buttons in a bottom row and the scrim at 0.20, and a camera move from the room shot into the match framing. |
| V4 | **Colour grade** ✅ | URP Volume overrides beside the existing bloom: tonemapping, split toning, edge falloff (vignette), light grain. Off with Lighting effects. |
| V5 | **Surface sheen** | Normal maps for the board's procedural sprites, generated from the same shapes, so 2D lights pick out marble, gilt and felt. |

V0 passed, so neither fallback was needed: "top-down, deeper" and moving the board to URP's 3D renderer (which would have needed a new ADR against ADR-0010) are both off the table.

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
- 2026-09-18 — **V0 passed; V1a in: the tilted camera.** The pitch and the lens were
  re-measured before anything was built, and the measurement overturned the mockups'
  choice.
  - **The tilt's gain was overstated.** The mockup review claimed the tilted board is
    about twice the flat one on screen. That compared a *bounding box* against a square
    and used the spike's framing rather than a fitted one. Measured properly, as the
    board's own trapezoid with the same 12% of air the flat camera leaves: flat is
    828 × 828 px at 1080p with the HUD up, and the tilt at 40°/FOV 30 is 846,000 px²
    against 686,000 — **+23%, not +100%**, and the far edge comes out 1.40× narrower
    than the near one, so far cells are about 30% smaller.
  - **Below about 30° of pitch the tilt costs area** rather than gaining it: the board
    foreshortens and its near edge is not yet wide enough to pay for it.
  - **A long lens at a steep pitch beats a wide lens at a shallow one on both counts.**
    A wide lens balloons the near edge and the fit then shrinks the whole board to keep
    that edge clear of the tray; a long one keeps the trapezoid closer to a rectangle,
    so the same rectangle holds more board. Measured, at 1080p with the HUD up:

    | pitch | FOV | board area vs flat | near/far |
    | ----- | --- | ------------------ | -------- |
    | 52° | 12 | **1.45×** | **1.22** |
    | 48° | 20 | 1.36× | 1.34 |
    | 45° | 30 | 1.35× | 1.50 |
    | 40° | 30 | 1.23× | 1.40 |
    | 25° | 30 | 0.98× | 1.20 |

  - **Chosen (designer): 52° and a 12° field of view**, superseding the mockups' 40°/30°.
    Half again as much board as the flat view, with the mildest foreshortening of the
    options — which matters in a game where the player counts cells to plan a move. The
    cost is the least cinematic of the four. The menu pitch stays 58°.
    **Open:** the approved salon mockup was rendered at FOV 30, so the title room reads
    flatter at 12° and that one frame wants re-rendering before V3 is built.
  - **`View/TiltFraming`** (new, plain C#) solves the pose. With the rotation about x
    alone and the aim on the board plane the projection collapses to three lines, so the
    whole thing is arithmetic and testable outside the editor. The aim scans down the
    board, the distance comes from a bisection (fitting is monotone in distance), and the
    sideways aim is solved afterwards by a short fixed-point pass, because there is no
    lens shift. A 4:3 window is the case that needs both safety nets — the aim-independent
    width check and the re-verify back-off — and does not solve without them.
  - **`FrameCamera` has two branches** now; the flat one is unchanged. It also resets the
    clip planes, because the long lens pushes the near plane tens of units out and
    leaving that behind clips the board away on the way back to top-down.
  - **Board clicks are a ray against the board plane.** The old code unprojected the
    mouse and dropped z, which works only because the flat camera looks along z; under
    the tilt it returned a point in front of the lens. Right for both cameras now, so
    there is no mode to branch on.
  - **The camera nudge shoves along the camera's own right and up**, not world x and y,
    so a shake still means a shake of the picture. Identical under the flat camera.
  - **Board camera** is a `DisplaySettings` field on the Display page, remembered between
    sessions, defaulting to **Top-down**: the tilt is opted into until it has been played.
  - Checks: the view, tests and editor assemblies compile against the 6000.6 DLLs;
    **793 tests green** (24 new `TiltFramingTests`, 6 new `DisplaySettingsTests`). Eleven
    mutations of the framing maths were tried and all eleven fail a test.
  - **Play Mode watch-list (designer):**
    1. Settings → Display → **Board camera → TILTED**. The board should tilt at once,
       without resizing the window.
    2. Click a piece, then click a landing cell. Do clicks land where you point, at the
       near edge *and* the far edge?
    3. Switch back to TOP-DOWN. The board must come back exactly as it was — if the
       screen goes black, the clip planes are wrong.
    4. Knock something out: the screen shake should shake the picture, not dive into
       the table.
    5. Wear headphones and let a CPU turn play: sounds on the left should be on the left.
    6. Known and expected: the pieces still lie flat on the table, and far pieces may
       draw in front of near ones. That is V1b.
- 2026-09-18 — **V1b in: the figures stand up, and depth sorting exists at all.**
  - **The scope grew by one thing.** Standing a figure up breaks clicking: the figure is
    drawn well above the cell it stands on, so a click on its chest becomes a board point
    about a cell behind its feet. `BoardPointer.PieceAt` gained a screen-space path —
    the figure whose drawn rectangle is under the pointer wins, and the nearer of two
    overlapping figures wins — with the old world-circle test kept as the fallback, so
    clicking the seat disc still works and the flat camera is untouched. It had to ship
    with the lean rather than after it.
  - **The lean is `LeanFraction` (0.55) of the pitch back from upright**, which is where
    the mockups put the figures. The three candidates: flat on the table (the top-down
    view), bolt upright in the world (foreshortened to 79% of its height at 52°, head
    leaning away), or square to the camera (full height, but leaning 38° back and reading
    as floating). The chosen lean shows **99%** of the art and still reads as standing.
  - **Feet, not middles.** A piece's origin is the centre of its figure frame, so leaning
    about the origin swings the feet off the cell. The lean goes on a new `figure` child
    whose position cancels the swing, which leaves every sprite inside it at the local
    coordinates it already had — `SetPose`, the health bar and the pin needed no changes.
  - **The ground markings stay on the root** and are foreshortened by the camera, which
    is why the seat disc stops being a hand-faked 0.9 × 0.36 ellipse under the tilt and
    becomes a true circle. The flat view keeps the fake, because nothing foreshortens it
    there.
  - **Depth sorting did not exist.** Every `sortingOrder` in the view was a compile-time
    constant, and all of a piece's parts shared 0–6 on one layer, so two pieces on
    different rows drew in GameObject creation order. A `SortingGroup` per piece makes it
    one unit, with its order taken from world y over a band of 3–39. The cast tell, the
    feedback rings and the damage numbers moved from 14, 15 and 20 to 42, 43 and 48 to
    open that band; they were above the pieces either way, so nothing changed visually.
    The renderer's custom-axis transparency sort was considered and rejected: it only
    breaks ties *within* one order, so a near piece's base disc would still draw behind a
    far piece's body, and it would quietly change how the board art ties as well.
  - **Two bugs found while writing it.** The hop moved along world up, which under the
    tilt slides the piece up the table instead of off it — it now rises along the
    camera's up. And the piece's root scaled y by about 0.6 while leaving z at 1, which
    is harmless for flat sprites but shears a child rotated about x; z now takes the same
    scale as y.
  - **`View/BoardTilt`** is the seam: the composition root writes the pitch there every
    time it frames the camera, and the pieces, the pointer and the hop read it. Zero
    means the flat camera and every derived value is the identity, so top-down behaves
    exactly as it did before the tilt existed. V1c will read the same seam.
  - Checks: view, tests and editor assemblies compile; **812 tests green** (19 new
    `FigureTiltTests`). Twelve mutations of the tilt maths were tried and all twelve fail
    a test.
  - **Play Mode watch-list (designer):**
    1. With TILTED on, do the figures stand on the table rather than lie on it, and do
       their feet sit on the right cells?
    2. Click a figure's **body**, not its base. Does it select that figure?
    3. Move a piece so it passes another one. Does the nearer piece always draw in front?
    4. Hop a piece: does it rise off the table, or slide up it?
    5. Do the seat discs read as circles on the floor rather than as standing ellipses?
    6. Switch to TOP-DOWN: everything should look exactly as it did yesterday.
    7. Still expected, and V1c: health readouts, damage numbers and the feedback rings
       are placed as if "up" were up on screen, so they will sit oddly.
- 2026-09-18 — **V1c in: the overlays, and a collision that predates the tilt.**
  - **The planned scope was wider than the defect.** Six things were listed; three needed changing. Read rather than assumed:
    - **`PieceHudLayer`** offset its health `Vector3.up * worldOffset` and its status row `Vector3.down`. Under the tilt that slides the readouts *up the table* instead of *off* it. Now `BoardTilt.ScreenUp`.
    - **`FloatingText`** rose along world up, same fault: a damage number drifted across the board rather than toward the viewer.
    - **`CastTell.Drop`** started its diamond at `at + Vector3.up * 1.4 cells` and dropped it onto the cell. Under the tilt it fell along the table.
    - **`CellLabelLayer` needed nothing.** Its `Place` projects the cell's own world position through `WorldToScreenPoint` with no offset at all, and that is correct for any projection. The plan listed it on the assumption it carried an offset; it does not.
    - **`DiceRoller` needed nothing.** `CanvasPoint` is `RectTransformUtility.WorldToScreenPoint` on the vault's position — again projection-agnostic, the same reason V1a found the world-anchored HUD was already correct.
    - **`FeedbackLayer`'s rings needed nothing,** and changing them would have been a bug. They are true circles in the board plane that expand in place. The flat camera renders a circle; the tilted camera foreshortens it into the table by itself. That *is* the wanted behaviour — it is the seat disc's lesson from V1b in reverse, where the hand-faked 0.9 × 0.36 ellipse had to *become* a true circle.
  - **`BoardTilt` is the identity when flat,** so all three changes are no-ops for top-down. That was the point of putting the seam in during V1b.
  - **A collision found in a screenshot, not in the code.** Two pieces stacked at the head of the north arm spread their readouts sideways (`SetStack`, by a label's width) straight into the turn banner's pill, which prints as `RED10/10urn 7/7d 1 · Space to roll`. It needs a stack to show, which is why it survived every earlier Play Mode pass, and it has nothing to do with the tilt — the flat camera does it too.
    - `PieceHudLayer.SetCeiling` takes the canvas units spoken for at the top of the screen, and `MatchBootstrap` feeds it `TurnStrip.ReservedHeight + TurnBanner.ReservedHeight` from `FrameCamera`, beside the toasts', banner's and turn button's areas. New `TurnBanner.ReservedHeight` (44), stated as its parts rather than measured, because the pill is content-sized.
    - **Clamped, not flipped.** A top-row piece only needs to come down about its own height, so the label stays plainly attached; a flip would drop it onto its own status tags.
  - **Checked:** the view and core compile clean against the 6000.6 DLLs (178 files, 0 warnings, 0 errors), and no `Vector3.up` or `Vector3.down` survives in any of the five overlay files. No core changes, so the tests are unchanged.
  - **Play Mode checklist:**
    - **Tilted.** Health readouts and status tags sit above and below their pieces *on screen*, not shifted up the table. Damage numbers rise toward the viewer. A beacon or zone cast drops its diamond onto the cell from above on screen.
    - **Tilted.** Feedback rings still lie flat on the table and foreshorten with it — they should look like rings on a floor, not standing hoops.
    - **Both cameras.** Deal a match where two pieces start stacked at the head of the north arm (RED's own arm). The turn banner reads cleanly; the two readouts sit just under it rather than through it.
    - **Top-down.** Everything else is exactly as it was — the three changed offsets are identities at pitch 0.
    - Landing pips and the dice were not touched; if either looks wrong under the tilt it is a new finding, not a regression.
- 2026-09-18 — **V3a in: the salon, and the menus stop being flat.**
  - **Re-rendered first, as decision 4 says.** The approved room was rendered at `LENS=30`; the shipping camera is Unity `fieldOfView` 12. Those are not the same units — Blender's lens is a focal length, Unity's field of view is an angle — and at 16:9 the match is **96.3 mm**, so the approved frame was effectively a 37° vertical field against the 12° that ships. Frames at both are in the chat.
  - **The re-render overturned the doc's worry, in the room's favour.** This file expected the room to "read flatter at 12°". It does read flatter, but a long lens compresses depth, so background objects come out at nearly the same scale as foreground ones: the chandeliers and sconces are **larger and more legible** at the shipping lens, and the pair finally flanks the wordmark the way the design intended instead of clipping the frame's top edge.
  - **`tools/mockup/scene.py` could not have rendered it.** The fit search scanned distances 14–43.75, tuned for the 30 mm default; at 96 mm the board needs **87.5**, so no framing existed and the search would have failed silently. The range now scales with the lens (`k` is 1 at 30 mm, so every earlier render reproduces exactly). New knobs `FIGURES`, `ROOM_LIGHT` and `VELVET`, which is how the frames below were made.
  - **The velvet wall is flat, and the folds are not built.** At the approved light the salon is a black void with two chandeliers and four flames in it — the wall, pelmet and columns are simply absent. Lifting the room's fixtures ×2.6 and the cloth ×2.4 recovers all of it. But the folds **still** do not read: a flat maroon field with soft blotches. That is the third failure — room C's velvet "read as a flat maroon field", `rake_velvet` was added to fix precisely that, and now with the wall unambiguously lit they are still absent. `curtain()` builds it with 140 cuts and two raking spots exist only to shade them, and a plain panel gives the same image. **Designer: build the wall flat.**
  - **The table stays empty** (designer). This file assumed "V3 should draw a posed table"; the empty table reads fine in the re-render and does not look unfinished, and every stand-in figure is currently Luka.
  - **The Board camera setting is now match-only, and it had to be.** `FrameCamera` gated the tilt on `_display.Camera == BoardCamera.Tilted` for *every* screen, and that setting defaults to Top-down — so a new player's title screen was flat. The room is vertical: a straight-down camera sees the wall, the columns and the chandeliers edge-on and there is nothing left but the board. The gate now applies only when a match is on the table. That matches the setting's own rationale — V1a made the tilt opt-in because "the player counts cells to plan a move", and a menu has no cells to count — and `TiltFraming` already carried `MenuPitch` as its own constant, so the code was written as though the menu owned its pitch.
  - **Shelved, at the designer's request:** drawing the salon as a flat 2D backdrop behind the board instead of room geometry. It would work under both cameras and is cheap, but it cannot respond to perspective, so under the tilt it sits there as wallpaper while the board moves in depth — which is the thing the mockups were judged on. Kept here in case it is wanted.
  - **`RoomBackdrop` holds itself off when `BoardTilt.IsTilted` is false,** because a tilt that cannot be solved for the window still falls back to flat and the room has to go with it. Its geometry is written in the mockup's units (table half-side 8.3) and scaled into board units, so the render that was signed off and the scene that ships compare number for number. Brightness and the velvet's lift are **inspector fields**, not constants: they were set from Blender's approximation of the game's lights, and the value that matters is the one that looks right under the real URP lights and V4's grade.
  - **The light is painted, not cast** — chandeliers and sconces carry their own glow, the way G4's corner lamps did, so the room reads with Lighting effects off. Real `Light2D`s over the top are a follow-up.
  - **Checked:** the view and core compile clean against the 6000.6 DLLs (180 files, 0 warnings, 0 errors). No core changes, so the tests are unchanged. uGUI and the scene cannot be previewed here; Play Mode is the check.
  - **Play Mode checklist:**
    - On the title, with Board camera at **Top-down**: the room is there and the board is in perspective. That is the change — before this, Top-down gave a flat title with no room.
    - The velvet wall reads as a wall, lit from the pelmet and falling away toward the skirting, with no visible left or right edge.
    - Two chandeliers flank NONA rather than sitting behind it, and six sconces read as flames on dark columns framing the table.
    - Nothing of the room appears once a match is dealt, and it comes back behind the pause, setup, draft and end screens.
    - With Lighting effects **off** the room still reads — the glows are painted into the sprites.
    - `brightness` and `velvet` on the MatchBootstrap object: drag them in Play Mode and settle on values. 2.6 and 2.4 are Blender's answer, not URP's.
    - Switching Board camera between Top-down and Tilted mid-session: the match changes, the menus do not.
    - The first title does not hitch noticeably — the room's sprites are built once per board and cached.
