# ADR-0009 — 2.5D character production: model in 3D, render to 2D sprites

- **Status:** Accepted 2026-09-17 (designer, from a picker). The code side (ART1) passes the cloud build and tests; the look still needs a Play Mode check.
- **Date:** 2026-09-17
- **Location:** `docs/decisions/0009-2-5d-character-production.md`
- **Relates to:** ADR-0001 (2D locked; "2.5D noted, not adopted"), ADR-0010 (URP 2D), `ART_PIPELINE.md` §1, §2, §4, §5, §8 and §9, `ART_DIRECTION.md` §5 and §6.1, `docs/design/ART_HOOKUP.md` (the stage log), `tools/blender/render_operator.py`.

## Context

ADR-0001 locked the game to 2D and recorded one technique as available but not adopted: model characters in 3D and render them down to 2D sprites from the fixed board angle. It said adopting it would take its own ADR.

Operators need several consistent images each: seated, standing, a portrait, and later a rise, an ability tell and a knockout (`ART_PIPELINE.md` §6). Painting those by hand, or generating them one at a time with an image model, drifts between images and between operators. Luka's look was settled on 2026-09-16 (`ART_PROMPTS.md`), and the designer has since produced a textured, rigged Luka in Meshy (a Mixamo-style biped rig, exported as GLB with walking and running clips).

## Decision

**Adopt 2.5D production for operators.** The game stays 2D: Unity only ever sees PNG sprites. Only the way those sprites are made changes.

1. **Model:** Meshy, from the settled concept sheet, rigged by Meshy (Mixamo bone names), exported as **GLB** (one file with mesh, textures, rig and clips). Source files live in `art/source/characters/<name>/`, outside `Assets/`, so Unity never imports them. They go through Git LFS.
2. **Proportions: 6 heads tall.** Meshy follows the concept sheet, which is drawn at about 8 heads (Luka measured 8.3). The render script scales the head (default ×1.3) and shortens the legs (×0.83) and arms (×0.92), then bakes the result into the rest pose. Luka comes out at about 6.1 heads.
3. **Render:** Blender (4.0 or later), from a script (`tools/blender/render_operator.py`) rather than a hand-kept `.blend`, so every operator gets the same camera, light and line:
   - orthographic camera, 25° down (the board angle), yawed 30° to a three-quarter view;
   - EEVEE with a toon ramp (base texture × a three-step light ramp, as emission);
   - a warm key from the upper left, a cool rim from behind, a dark ambient;
   - a Freestyle outline in `#1C0E12` (silhouette, border and contour; creases are off, because they scratch painted cloth);
   - "Standard" view transform, transparent film, PNG with alpha.
4. **Poses are set by the script** from bone directions in the figure's own frame (forward, left and up, read from the hips and toes), so they carry over to any Mixamo-rigged operator:
   - `standing`: arms down, slightly out, elbows soft, face lifted to the camera. 512×768.
   - `seated`: waist up, leaning in, hands together at an unseen table, cropped just above the hips. 512×512. This matches the yard bust the procedural figure draws.
   - `portrait`: head and shoulders. 512×512.
   The script can save the posed scene (`--save-blend`) for hand tweaks.
5. **Sprites** are copied from `art/renders/<name>/` into `Assets/_Project/Art/Resources/Art/Operators/` as `<name>_standing.png`, `<name>_seated.png` and `<name>_portrait.png`. The game finds them by name (ART_HOOKUP decision 3). The renders folder itself is not committed; the GLB and the script reproduce it.

## Consequences

- **The game code does not care how a sprite was made.** `OperatorArtLibrary` loads by name, and the piece fits any render by its opaque rows into the procedural figure's frame (`FigureLayout`). Pivot, padding and resolution cannot misplace a figure. A hand-painted sprite would work the same way.
- **Every operator now needs a model.** The per-operator cost moves from painting several images to one Meshy model plus a script run. Rise, tell and knockout frames become extra script poses, not new paintings.
- **Blender becomes a project tool.** Its version is not pinned; the script handles the EEVEE engine rename (4.2 to 4.4 call it `BLENDER_EEVEE_NEXT`).
- **Licensing moves to Meshy's terms** (`ART_PIPELINE.md` §8). As of 2026-09-17, Meshy's help centre says: Free-plan output is **CC BY 4.0** (attribution required: "Model created with Meshy – CC BY 4.0 License"), and on paid plans the customer owns the output. Which plan made Luka is not yet recorded (`docs/art/PROVENANCE.md`).
- **Textures come from the concept sheet,** so painted marks on the clothes (folds, scratches) show up in every render. Clean them in the texture, not in the script.
- **ADR-0001 is amended, not reopened.** Its "3D is concept-only" rule now has one exception: producing operator sprites through this pipeline. Environment and board art stay painted 2D.

## Options considered

- **Keep painting or generating each image (ADR-0001 as it stood).** Cheapest per image, but consistency between poses and between operators depends on the tool's luck, and every new pose is another roll.
- **Real-time 3D characters in Unity.** Rejected by ADR-0001 for workload, and it would need a 3D camera and lighting rig on a 2D board.
- **A hand-kept `.blend` template instead of a script.** Easier to start in the Blender UI, but settings drift between operators and nothing records them. The script can still save a `.blend` for touch-ups.

## Status history

- 2026-09-17: Accepted. Luka rendered in the cloud with Blender 4.0.2 (EEVEE through a virtual display); renders written to `art/renders/luka/`.
