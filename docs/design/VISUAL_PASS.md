# Nona Royale — Visual Pass

> Location in repo: `docs/design/VISUAL_PASS.md` · Project copy: `claude/VISUAL_PASS.md`
> Status: **Closed, 2026-09-20.** V0 passed and is retired; V1a, V1b, V1c, V2, V3a, V3b, V4 and V5 are all in and all passed Play Mode. The title is the one screen that is **flat**, and the salon stands the right way up.
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
| V2 | **Table body** ✅ | `View/TableBody`: the slab's near-edge face and the gilt band at its lip, hung under the board and off under a flat camera. Plus a contact shadow under each standing figure in `OperatorPiece`, which stays on the table through a hop. |
| V3a | **The salon** ✅ | `RoomArt` and `RoomBackdrop`: a **flat** velvet wall with pelmet, gilt rail and skirting, and a ring of sconces on side columns. Behind the setup, draft and end screens; the title is flat and shows no room (V3b). The chandeliers were cut on 2026-09-19. The Board camera setting became match-only so those menus are always tilted, without which none of it is visible. The table stays **empty** (designer). |
| V3b | **The title layout** ✅ | The lockup to the top, the buttons in one bottom row, the scrim at 0.20, the tagline gone and the build line moved to a corner stamp. The board between them is **flat**, and `TitleScreen.ReservedTop`/`ReservedBottom` keep it there. The camera move from the room shot into the match framing was not built. |
| V4 | **Colour grade** ✅ | URP Volume overrides beside the existing bloom: tonemapping, split toning, edge falloff (vignette), light grain. Off with Lighting effects. |
| V5 | **Surface sheen** ✅ | **Painted, not normal-mapped** — `Light2D.normalMapQuality` is read-only in this URP and every light here is made at runtime. A `CrossSheen` part on the cross: one broad band of the room's light raking across the floor, with the marble's own veins taking more of it than the stone does. Gilt and felt already carried painted light and were left alone. |

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
- 2026-09-19 — **V3b in: the title layout, and the title alone goes back to flat.**
  - **Designer, on seeing the salon behind the lockup: minimalist, and flat.** So the title is the one screen that opts out of the tilt. `FrameCamera` asks `_title.IsShowing` and clears the tilt for it; `RoomBackdrop` already hid itself whenever `BoardTilt.IsTilted` was false, so the room leaves without being told and the setup, draft and end screens keep it. That is a real narrowing of V3a — the salon now shows on three screens rather than four, and never on the one it was composed for.
  - **`ModalCard.IsShowing`, new, and the reason is a tenth of a second.** `Close` fades over 0.12s and only deactivates the scrim when the fade lands, so `IsOpen` — and with it `CurrentScreen` — stays on the title through the fade. Framing off that would have left the camera flat for a beat after PLAY and then snapped. `IsShowing` is open-and-not-closing, so the tilt comes back as the card starts to go.
  - **The bands are not the card.** V3's review found the centred 520-wide column buries the board whatever is behind it. The main page now composes into two rects hung off the scrim — the wordmark pinned to the top, one horizontal row of three equal buttons pinned to the bottom — and the settings pages still build into `ModalCard`'s column. A page swap therefore fades the card alone and the wordmark holds still, which it did not before. The cost is that `Show`'s rise and cascade do not reach the bands, so `Open` gives them their own slide.
  - **The board is framed between the bands, not under them.** `TitleScreen.ReservedTop` (250) and `ReservedBottom` (160) are read by `FrameCamera` exactly as `TurnStrip.ReservedHeight` and `ActionTray.ReservedHeight` are in a match. They are stated as their parts — inset, band height, margin — and the armed QUIT's warning is counted whether or not it is showing, so the board does not resize when QUIT is pressed. At 1080 that leaves the board about 56% of the screen's height, centred a little low, under the wordmark.
  - **Dropped:** the tagline ("Nine operators. One vault.", and the roster is eleven — the review's own note, now moot). **Scrim to 0.20** from 0.45, per the review.
  - **The "Prototype build" line became a corner stamp** (designer): 12 pt, `TextDim`, pinned bottom-right, outside both bands and built once rather than composed. So it holds still across the pages and does not ride up when the armed QUIT pushes the row. It costs the framing nothing — the board is square and centred, so the corner is space a wide window was never going to use.
  - **`tools/mockup/title.py` is now stale.** It draws the old centred lockup with the tagline, at the old scrim. It did its job in the V3 review; it is not worth keeping in step with the screen.
  - **Checked:** the editor recompiled `NonaRoyale.Unity` on its own seven seconds after the last edit landed — `Temp/pipeline_recompile_status.json` reads `failed: false, errors: []`, no `CS` diagnostics in `Logs/Editor.log`, and `Library/ScriptAssemblies/NonaRoyale.Unity.dll` carries `IsShowing`, `ReservedTop`/`ReservedBottom`, `AddStamp` and the `title_stamp`/`title_lockup`/`title_menu` literals. No core changes, so the tests are unchanged. uGUI cannot be previewed from a shell; Play Mode is the check.
  - **`tools/view-compile.cmd`, new**, for when the editor is closed: it runs Unity's own Roslyn over the response file the build graph already wrote. It anchors itself to the project root (Explorer starts a double-clicked script in its own folder, which made the first version exit instantly), tees to `Temp/view-compile.log`, and holds the window open when double-clicked.
  - **Play Mode checklist:**
    - The title is flat and has no room: NONA ROYALE across the top, the board square and whole in the middle, PLAY / SETTINGS / QUIT in one row along the bottom. No tagline; the build line sits small in the bottom-right corner, clear of the row on any window you can resize to.
    - PLAY: the camera tilts back into the salon as the title fades, not a beat after it.
    - SETTINGS, then Sound and Display: the wordmark does not move or flicker between pages; the settings card fades and settles under it as before. Esc walks back.
    - QUIT: the right-hand button becomes CONFIRM QUIT with the warning above the row, and **the board does not resize**. Esc disarms it.
    - Return to the title from a match (MAIN MENU): flat again, no tilted frame on the way in.
    - Resize the window narrow and short: the row stays on screen and the board still sits between the bands.
    - Setup, draft and the end card are unchanged — tilted, with the salon behind them.
- 2026-09-19 — **The chandeliers are cut** (designer: "not a fan … don't think it fits, at least not this design").
  - `RoomBackdrop.Chandeliers` and `RoomArt.BuildChandelier` are gone. The room is now the velvet wall with its pelmet, rail and skirting, and the six sconces on the side columns — which the V3 review already called "the best thing in the set".
  - **The radial glow survived them.** The sconces were drawing their spill from `RoomArt.Chandelier[ChandelierGlow]`, so it is its own sprite now, `RoomArt.Halo`, and the chandelier body is not built at all.
  - **The pair only existed to dodge the wordmark** — V3's first salon pass put one dead centre and it landed on NONA ROYALE. That constraint died with V3b: the title is flat and shows no room, so the chandeliers were an ornament on three screens that never see the composition they were placed for.
  - **The upper wall is bare now.** The wall's own gradient is lit from the top, which now reads as light off the pelmet rather than off a fixture. Worth a look in Play Mode before deciding whether anything replaces them.
  - **Checked:** in the same clean editor recompile as V3b above; `BuildChandelier`, `ChandelierBody` and `ChandelierGlow` are absent from the built assembly and `BuildHalo` is in it.
- 2026-09-19 — **V2 in: the table has a side.**
  - **One edge, because one edge is all there is to see.** The camera tilts about x alone, so the table's left and right sides project exactly edge-on and its far side is behind the surface. `View/TableBody` builds the near edge and nothing else: a dark face from `BoardArt.TableEdge` and a gilt band at its lip from `BoardArt.TableBand`, both hung from the board's low-y edge, at sorting orders −36 and −35 — behind `BoardView`'s own art, which starts at −33, and in front of the room at −80.
  - **It drives its own visibility.** Like the room it is vertical geometry and a straight-down camera sees it edge-on, so it is off when `BoardTilt.IsTilted` is false. Unlike the room it belongs to a match as much as to a menu, so it reads the tilt itself in `LateUpdate` rather than being told which screen is up. That covers the Board camera setting, a tilt that cannot be solved for the window, and the flat title.
  - **The slab hangs inside the framing's air.** `FrameCamera` fits the board's four corners with `FrameMargin`'s 12% of slack and knows nothing about anything below the near edge. `Thickness` is 0.5 spacings, which projects to about 0.4 spacings of screen height at the match pitch and lands inside that slack. Deepen it much and the lip goes under the action tray in a match and off the bottom edge on the menus — the fit would have to learn about the slab first.
  - **Contact shadows are the figure's, not the render's.** A new `contact_shadow` on each piece at order −1, inside the piece's `SortingGroup`, so it stays under its own figure while a nearer figure's shadow still draws over it. It spreads and pales at the top of a hop and snaps tight and dark on the landing, and it is **un-hopped** every frame: the root carries `ScreenUp * _lift`, so the lift is taken back out through `InverseTransformVector`. A contact shadow that sails up with the piece is the one thing it must not do.
  - **Off under the flat camera**, standing figures only. The top-down view is exactly as it was, which is the rule V1 set and kept.
  - **Finding, since proven and fixed (see the entry below): the salon was built upside down.** Screen up is `(0, cos p, −sin p)` — `FigureTilt.ScreenUp`, which the hop and the figures' lean both follow — so **up off the board is −z**. `RoomBackdrop.Stand` stands every part of the room along **+z**, which is down. If that is right, the wall's pelmet and gilt rail render at the *bottom* of the wall where it meets the table, and the skirting at the top of the frame. It cannot be told apart from a correct wall in a shell, and a flat velvet gradient inverted still reads as a wall at a glance, which is how it could have passed V3a's Play Mode check. **One look in the Scene view settles it.** Not touched here: it is shipped, approved work and the fix is a one-line sign change either way.
  - **Noticed in passing:** `seat_base` and `seat_ring` are children of the piece root, so they ride `ScreenUp * _lift` and fly with the figure through a hop. The contact shadow does not, deliberately. Left alone so one new thing lands at a time, but it is the same one-line fix.
  - **Checked:** the editor recompiled clean — `failed: false, errors: []`, no `CS` diagnostics, and `TableBody`, `BuildEdge`, `BuildBand`, `ShowContact` and the `table_body` / `table_edge` / `table_band` / `contact_shadow` literals are all in the built assembly.
  - **Play Mode checklist:**
    - Deal a match with Board camera on **Tilted**: the table has a visible near edge with a gilt lip, and the lip is *below* the surface, not above it. If it is above, the sign in `TableBody.Hang` is wrong — and so is the room's.
    - The edge does not disappear under the action tray, and does not get cut off the bottom on setup, draft and the end card.
    - Board camera on **Top-down**, and on the title: no edge at all, and the board is exactly as it was.
    - Walk a piece: its shadow stays on the table, spreads and pales at the top of each hop, and snaps tight on each landing. It never leaves the table with the figure.
    - Two pieces stacked: the near one's shadow draws over the far one's figure, not under it.
    - An evasive piece's shadow fades with it; with **Reduced motion** on, the shadow simply sits there.
    - `UiTheme.TableEdge` and `UiTheme.TableBand` were set by eye against the void. If the slab reads as a hole or as a shelf, those are the two values to try first.
- 2026-09-19 — **The salon was upside down. Turned round.**
  - **Proven, not eyeballed.** `TiltFraming.Solve` and `Project` only ever handle points on the board plane, so nothing in the codebase projects a z. Extending the same camera model gives `eye.y = dy·cos p − wz·sin p` and `eye.z = d + dy·sin p + wz·cos p`, so raising `wz` lowers the numerator *and* raises the denominator: **+z is strictly down the screen, at every distance and every pitch in range.** For the wall at the menu pitch, its head at z = 21 lands at v = −0.13 against its foot's +0.16. The pelmet and the gilt rail were rendering along the wall's bottom edge, where it meets the table, and the skirting ran across the top of the frame.
  - **Screen up is `(0, cos p, −sin p)`** — `FigureTilt.ScreenUp`, which the hop rises along and the figures' lean follows. So up off the board is **−z**, and `RoomBackdrop.Stand` was standing every part of the room along +z.
  - **How it survived V3a.** A flat velvet gradient reads as a wall either way up, the room draws at sorting orders −80…−69 behind everything, and the wall sits at y ≈ 2.1× the board's extent, so an inverted wall still fills the top of the frame. The Play Mode note that passed it — "reads as a wall, lit from the pelmet and falling away toward the skirting" — is exactly what an inverted wall also looks like.
  - **The sign is turned in one place**, `Stand`, with `flipY` alongside it so each sprite's own up still reads as up. Every geometry constant still reads as the mockup wrote it, z counting up from the floor. If the room looked right before and looks wrong now, that one line is the revert.
  - **`TableBody` was written against the corrected convention**, so the table's lip hangs below its surface rather than above it. The two agree.
  - **Play Mode checklist:**
    - On setup, draft or the end card: the pelmet and the gilt rail run along the **top** of the velvet wall and the skirting along its **bottom**, where the cloth meets the floor.
    - The sconces sit above their columns' feet, and each flame sits above its cup.
    - `brightness` and `velvet` were dragged into place against the inverted room — expect to re-tune them.
- 2026-09-20 — **V5 in, and not the way it was specified: the sheen is painted.**
  - **Normal maps are not reachable from code here.** `Light2D.normalMapQuality` is a get-only property over a private `[SerializeField]` that defaults to `Disabled`, with no public or internal setter anywhere in the URP package. Every light in the game is made at runtime by `SceneLighting.AddRig` with `AddComponent<Light2D>()`, so all of them come up with normal maps off and a normal map on a sprite would have done nothing at all. The alternatives were to write the private field through reflection or `JsonUtility`, which is one package upgrade from silently reverting, or to move the light rig into an authored prefab, which is a real refactor of the lighting setup. **Designer took the third road: paint it**, which is what this project does everywhere else — G4's corner lamps, the sconces, the chandeliers that were, the table rim's streak.
  - **It also survives the switch.** Lighting effects is a player-facing setting. A normal-mapped sheen vanishes when it is off; a painted one does not, and "the room still reads finished with Lighting effects off" is already the rule `BoardView` states.
  - **Marble was the only real gap.** The table rim already had a bead, a directional light and a glint streak; the felt already had a vignette, a highlight toward the light, grain and a nap; the table band from V2 has its own roll. The cross floor was flat stone with a faint inlay. So V5 is one new part, `BoardArt.CrossSheen`, drawn at order −27 between the inlay and the arm pools.
  - **The veins take the light first**, which is the whole difference between a polished marble and a matte one. `Veining` is now shared by the stone and its sheen, so the highlight lands on the veins that are actually there rather than on a second, unrelated pattern. The band rakes at 34°, off the diagonal so it never lines up with a lane and reads as a seam, and off centre so it is not wasted under the vault's own glow.
  - **Within G3's few percent, measured not guessed:** peak alpha is 0.024 on bare stone and 0.07 on a full vein. G3's rule is that nothing competes with the lit cells or sits loud under a piece.
  - **With a painted marble it degrades to an even rake.** The fill would carry the texture's own veins, which these do not register with, so the sheen part is never painted. No painted board textures exist in the project today.
  - **Play Mode checklist:**
    - The floor reads as polished stone, not as a bright shape: a soft band low and left of centre, brightest where veins cross it.
    - It does not line up with any lane, does not stop at the gilt trim, and is invisible under a piece.
    - With Lighting effects **off** it is still there.
    - `UiTheme.FloorSheen` is the one value to tune; `SheenDegrees`, `SheenCentre` and `SheenWidth` in `BoardArt` move and shape the band.
- 2026-09-20 — **Pass closed. Play Mode passed on all five outstanding changes.**
  - The designer ran the checklists for V3b, V2, the room's flip and V5 together and reported all pass. That settles the two things a shell could not: the table's gilt lip hangs **below** its surface, and the salon's pelmet and rail now run along the **top** of the velvet wall. The sign convention in `TableBody.Hang` and `RoomBackdrop.Stand` is confirmed correct — up off the board is −z.
  - **Eight increments in, none open.** V0 was a spike and is deleted. Both fallbacks it existed to test — "top-down, deeper", and moving the board to URP's 3D renderer against ADR-0010 — were never needed.
  - **What the pass left behind, for whoever picks the board up next:**
    - `seat_base` and `seat_ring` are children of the piece root, so they ride `ScreenUp * _lift` and fly with the figure through a hop. V2's contact shadow deliberately does not. One line, same mechanism, not done because one new thing at a time.
    - `tools/mockup/title.py` and `tools/mockup/scene.py` still draw the pre-V3b lockup with its tagline, and a room with chandeliers in it. They did their job in the V3 review; nothing depends on them now.
    - `brightness` and `velvet` on the MatchBootstrap object were dragged into place against the inverted room. They pass as they are, but they were never re-tuned against the corrected one.
    - V5's sheen is painted because `Light2D.normalMapQuality` is read-only in this URP. If the light rig ever moves to an authored prefab, real normal maps become available and this decision is worth revisiting.
