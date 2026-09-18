# Nona Royale — Art Hookup (Stage 4)

> Location in repo: `docs/design/ART_HOOKUP.md` · Project copy: `claude/ART_HOOKUP.md`
> Status: **Open, 2026-09-17.** ART1 is written and builds; it waits for Play Mode and a commit.
> Related: `NEXT_PHASES.md` (Stage 4), `claude/STAGE4_HANDOFF.md`, ADR-0009 (2.5D production), ADR-0010 (URP 2D), `ART_PIPELINE.md`, `ART_DIRECTION.md` §5 and §6.1, `MOTION.md` (MO2)

## Goal

Real operator art on the board, starting with Luka, without breaking what the procedural figures already do:
- Luka shows in the yard and on the floor from real renders, readable at gameplay zoom and in a phone-sized Game view;
- every other operator still draws procedurally;
- hop, rise, shatter and idle still look right on a real sprite.

## Decisions (settled 2026-09-17)

1. **ADR-0009: adopt 2.5D production** (designer). Meshy model → Blender script → PNG sprites. The game stays 2D.
2. **Specs** (my call, as recommended in the Stage 4 hand-off): renders at 512×768 (standing) and 512×512 (seated, portrait), transparent, bilinear, no mipmaps, no normal maps on operators. Pixels per unit is 512, but it does not matter: the code scales by height.
3. **Loading by naming convention** (designer). `Assets/_Project/Art/Resources/Art/Operators/<key>_<pose>.png`, where the key is the operator's name, lowercased, accents stripped (`Revú` → `revu`). A ScriptableObject comes later only if per-operator tuning appears.
4. **Seat identity: a tinted base disc and ring under a rendered standing figure, plus the gold shape pin on the chest** (designer). The render is never tinted. A seated operator shows no disc; it sits at its own seat's table.
5. **First drop: Luka** (standing, seated, portrait). Rise, tell and knockout frames wait; the motion system already animates any sprite.
6. **Licensing:** Luka was made on a paid Meshy plan (designer, 2026-09-17), so the model is owned outright. Recorded in `docs/art/PROVENANCE.md`.
7. **Source layout** (designer): models in `art/source/characters/<name>/`, GLB, through LFS. Renders go to `art/renders/<name>/`, which is not committed.

## Increments

| #    | Increment | What it delivers |
| ---- | --------- | ---------------- |
| ART1 | Hookup + Luka | Art loading, fitted figures, seat disc, white hit flash, draft portrait, the import postprocessor, the Blender render script, Luka's first renders |

## ART1 — log (2026-09-17)

### Code

- **New `View/FigureLayout.cs`** (plain C#): the procedural pawn and bust frames, and `Fit()`, which fits a render into a frame.
  - The render's lowest opaque row sits on the frame's feet, and the render is scaled to the frame's height. Pivot, padding and resolution cannot misplace it.
  - The pin sits at a fraction of the fitted height (standing 0.70, seated 0.62), the halo 0.125 below the top, the bar 0.175 above it. These are the pawn's own gaps.
  - `FootAnchorOffset()` keeps the feet still while the figure squashes, stretches or breathes about its centre.
- **New `View/OperatorArtNames.cs`** (plain C#): `Key(name)`.
- **New `View/OperatorArtLibrary.cs`**: `Figure(name, pose)` and `Portrait(name)`, cached, null when missing.
  - It reads each render's pixels once, to find the opaque rows and to build a white silhouette for the hit flash.
  - An unreadable or packed texture still draws, fitted by its rectangle, with no flash, and logs one warning.
  - `Register` and `ClearCache` exist for tests and tools.
- **`View/OperatorPiece.cs`:**
  - The body is now a child renderer (`body`), so a render can be scaled and moved inside the frame. With no art it sits at the origin at scale 1, exactly where the root's renderer was.
  - `SetPose` picks a render per pose and falls back per pose, so Luka with only a standing render still sits as a bust.
  - For a render: body untinted, outline hidden, pin 0.12 (not 0.18) at the chest, halo and bar from the fitted top, seat disc and ring at the feet.
  - The hit flash on a render is a white silhouette child (`flash`), faded from 0.75. Lerping an untinted sprite toward white shows nothing.
  - **Behaviour change for every piece:** squash, the rise's stretch and breathing now keep the feet on the floor (before, they scaled about the centre, so the feet moved a few percent). The pop and the hover lift still grow the whole figure. `transform.position` is still the figure's centre, give or take that anchor, so floaters, sounds, hit testing and the health label are unaffected.
  - New `ShowsRenderedArt`. Tuning constants: `ArtPinSize`, `ArtStandingHeightScale`, `ArtSeatedHeightScale`, `ArtFlashStrength`, `SeatBase*`.
- **`View/DraftScreen.cs`:** a roster card shows the portrait when there is one, drawn as painted, with the shape as an 18 px seat-tinted pin in its corner. Seat slots (24 px) and the end screen (20 px) keep the shapes: a portrait is unreadable at that size.
- **New `Scripts/Editor/OperatorArtImporter.cs`** in a new editor-only assembly, `NonaRoyale.EditorTools`. On the first import of a texture in the operators folder, it sets: Sprite, Single, bottom-centre pivot, Full Rect mesh, PPU 512, alpha is transparency, mipmaps (Kaiser; off until the fix below), **Read/Write on**, **uncompressed**, bilinear, clamp, max 1024. Later Inspector changes survive a re-import.
  - The cost: about 1.2 MB per 512×768 image, twice. Fine for Luka; revisit before all eleven operators ship three images each (a shader-based flash would remove the need to read pixels).
- `BoardArt.PawnHead` is no longer read by the piece (`FigureLayout.Pawn` holds the same numbers); a test pins them together.

### Tools

- **New `tools/blender/render_operator.py`** (ADR-0009): import, proportions, three poses, toon, light, outline, render. Run from the repo root:
  ```
  "C:\Program Files\Blender Foundation\Blender 4.x\blender.exe" --background --python tools\blender\render_operator.py -- --model art\source\characters\luka\luka_walk.glb --name luka
  ```
  - Options: `--head`, `--legs`, `--arms`, `--yaw`, `--pitch`, `--line`, `--only`, `--save-blend`, `--preview`, `--out`.
  - It only renders the skinned meshes; the glTF importer's bone-display icosphere is hidden.
  - It needs EEVEE, so it needs a GPU or a virtual display. In the cloud it ran on Blender 4.0.2 under `xvfb-run` (about 45 s for three images on the CPU).

### Luka

- **Source:** `art/source/characters/luka/luka_walk.glb` and `luka_run.glb` (renamed from Meshy's `Meshy_AI_Luka_4k_biped_Animation_Walking_withSkin.glb` and `…_Running_withSkin.glb`). The same rest pose in each; one mesh (117,626 vertices), 3 textures (base, normal, metallic-roughness), 28 joints.
- **Proportions measured:** 1.70 m tall, head 0.205 m (chin at 1.495) → **8.3 heads**. The script's defaults bring him to 1.64 m and about **6.1 heads**.
- **Renders** in `art/renders/luka/`: standing, seated (hands together at the table), portrait. Faces lifted to the camera; the rest pose tips the head about 12° down.
- Painted marks on the jacket (from the concept sheet's texture) show in every render. Clean them in the texture if they read as dirt at board zoom.

### Seated size (2026-09-17, after the designer's first look)

- The seated figure read too small at the table. The seated render is waist up with the arms forward, so fitted to the bust's height its head was about 30% smaller than the bust's. `OperatorPiece` now fits each pose with its own height scale: standing 1.0, seated **1.4**, growing upward from the table line. Tune `ArtSeatedHeightScale` if 1.4 overlaps the table or its neighbours.

### Mipmaps (2026-09-17)

- The importer turned mipmaps off, which was wrong: a 768-pixel render is drawn about 90 to 180 pixels tall (1080p to 4K), and shrinking it that far without mipmaps shimmers in motion. `OperatorArtImporter` now turns them on (Kaiser filter), and the flash silhouette is mipmapped too. PNGs imported before this need **Generate Mipmaps** ticked in the Inspector (or their `.meta` deleted).

### Meshy reference sheet (2026-09-17, designer's pick)

- `render_operator.py --reference` renders the corrected body (the same head, leg and arm scales) in an A-pose, texture only (no toon ramp, no outline), on the concept crops' grey, at eye level, 1024×1024, one scale for every view: `<out>/reference/<name>_ref_{front,side,back,threequarter}.png`.
- Luka's sheet is in `art/renders/luka/reference/`. Upload it to Meshy (multi-view image to 3D) to regenerate him at 6 heads natively, then rig and export as before and re-render the sprites with `--head 1 --legs 1 --arms 1`.
- The jacket marks are in these images too. Painting them out here, in 2D, before the upload is easier than cleaning the texture afterwards.

### Checks

- The view and the editor assembly compile against the 6000.6 DLLs, with no new warnings.
- Tests: `FigureLayoutTests` (10), `OperatorArtNamesTests` (5). Cloud total: 724 green (653 core, the rest audio and view).
- `OperatorArtTests` (5, Unity-only, compile-checked here): the frames match `BoardArt`; a missing figure is null and cached; measuring finds the opaque rows and builds a white silhouette; standing art replaces the pawn while seated falls back to the bust; with no art, the pawn draws exactly as before.
- Mutation check on `FigureLayout` and `OperatorArtNames`: 11 of 11 killed (one survivor, the trailing-underscore trim, was killed by a test added after).
- **Not checked: anything on screen.** Play Mode is the only look check.

### Play Mode checklist (ART1)

Do these in order: Unity has to compile the importer **before** the PNGs arrive, or they import with default settings.

1. Pull the code, focus Unity, and wait for the compile. `NonaRoyale.EditorTools` appears in the Project.
2. Copy `art/renders/luka/luka_standing.png`, `luka_seated.png` and `luka_portrait.png` into `Assets/_Project/Art/Resources/Art/Operators/`. The Console shows three `[OperatorArt] Import settings applied` lines. If not, delete the three `.meta` files and let Unity re-import them.
3. Draft screen: Luka's card shows the portrait with a small shape pin; other cards are unchanged.
4. Match with Luka:
   - In the yard he sits as a render at the table. Is the height right against the other busts?
   - Deploy: he rises from seated to standing with the ring; a seat-coloured disc sits under his feet.
   - Standing on the floor: readable at gameplay zoom and in a phone-sized Game view (e.g. 390×844)? Pin, halo (select him) and health bar sit sensibly?
   - Walk: the hop and landing squash keep his feet on the cell.
   - Hit him: a white flash. Knock him out: shatter, then he sits again in the yard.
   - Evasion (if a source is handy): the whole figure fades, disc included.
   - Other operators look exactly as before (their feet may now stay a touch steadier on landing).
5. Tuning, if needed: `ArtStandingHeightScale`, `ArtSeatedHeightScale`, `ArtPinSize`, `SeatBaseWidth`/`Depth` in `OperatorPiece`; `--yaw`, `--pitch`, `--head`, `--legs` on the script.

### Open

- Whether the pin on a painted chest reads well, or should move to the disc.
- `art/source/…/Meshy_AI_Luka_4k_biped.zip` duplicates the GLBs and adds a third clip (Boom Dance); it is ignored by git.
- `ART_PIPELINE.md` §2 raster editor is still undecided.
