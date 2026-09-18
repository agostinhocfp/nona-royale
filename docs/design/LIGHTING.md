# Nona Royale — Lighting

> Location in repo: `docs/design/LIGHTING.md` · Project copy: `claude/LIGHTING.md`
> Status: **Open.** LT1 (room lighting, powered cells, bloom, the setting) is committed (`de1abcf`). G3's pool haze (`46c1ce1`, `GUI_PHASE.md`) rides on its switches. LT2 (event light) is committed as `9514e90` (2026-09-17), in one commit with GUI G4. The bloom Volume also carries the colour grade since V4 (`VISUAL_PASS.md`).
> Related: ADR-0010 (URP with the 2D Renderer), `ART_DIRECTION.md` §2.1, §3, §6 and §6.1, `BoardView`, `MOTION.md` (Reduced motion)

## Goal

The room reads as a casino after hours: low ambient light, warm pools where play happens, the vault lit, and powered cells glowing in the cool register. It is done with URP's 2D lights and bloom, over today's procedural board, before any painted art. Everything can be switched off.

## Decisions (2026-09-16)

1. **Taken as recommended.** The designer picked the lighting pass as the first use of URP.
2. **All built in code** (`View/SceneLighting`), like the rest of the view. The tuning values are inspector fields read every frame, so they can be dragged live in Play Mode.
3. **Two registers** (ART_DIRECTION §2.1):
   - Room lights are warm and use the Multiply blend style (0).
   - Powered cells use the Additive blend style (1) in holo cyan, so they pass white and bloom.
4. **Readability beats richness** (ART_DIRECTION §6):
   - The board moves to its own **`Board` sorting layer**, behind Default.
   - The cyan lights reach only that layer, so a piece on a safe cell keeps its seat colour.
   - The warm lights reach every layer.
5. **Bloom threshold 1:** only what a light pushes past white glows. The HUD is a Screen Space Overlay canvas and is never bloomed.
6. **A "Lighting effects" setting**, on by default and remembered (`nr.settings.lighting`). Off is the flat room from before LT1: ambient 1, no pools, no bloom. It's also the cheap path for weak devices.
7. **Reduced motion** stills the swells (the vault's slow breath and the powered cells' hum).
8. **The painted glows stay.** `BoardView`'s arm, vault and safe-cell glow sprites are kept, and the real lights sit on top of them.

## Increments

| #   | Increment | What it delivers |
| --- | --------- | ---------------- |
| LT1 | **Room lighting** | Ambient 0.7; pools over the vault, the four arms and the four tables; cyan additive lights on safe cells; a bloom Volume; the Lighting effects setting; the `Board` sorting layer; `LightPulse` and its tests. |
| LT2 | **Event light** | A cool light on the selected operator, a flash on cast tells, a warm burst on a knockout, and a lit vault when a piece reaches HOME. |

## Log

- 2026-09-16 — **Stage opened** after ADR-0010 passed Play Mode (HEAD `feat(render): switch to URP with the 2D Renderer and Linear colour`).
- 2026-09-16 — **LT1 built: room lighting.**
  - **`View/SceneLighting`** (rewritten; ADR-0010's version only made the ambient light):
    - **Ambient:** a global light (blend style 0) at 0.7, warm white. A global light already in the scene is used instead and left alone.
    - **`Arrange(layout, map)`** places the room's point lights each time the board is drawn: once for the title's table, and again at every deal.
      - Vault: a warm light at HOME, reaching 0.42 of the board's half-width, intensity 0.6. It swells ±6% every 7 s.
      - Arms: four warm lights where `BoardView` paints its arm glows (0.6 of the half-width out), reaching 0.6 of it, intensity 0.34.
      - Tables: one warm light per yard, reaching 0.75 of a table's diameter, intensity 0.4.
      - Powered cells: a holo-cyan additive light on every safe track cell, reaching 1.1 cells, intensity 0.5 (0.35 after review, below). Each hums ±15% every 3.4 s on its own phase. They target only the `Board` sorting layer.
    - **Bloom:** a global Volume with a runtime profile (threshold 1, intensity 0.7, scatter 0.6, fast filtering). Post-processing is switched on the main camera only while effects are on.
    - **Every value is a public field,** read each frame. Edits made in Play Mode are lost when Play stops, unless the component is first added to the MatchBootstrap object in the scene (`Ensure` then uses it).
    - **Effects off:** the ambient goes to 1 and white, the point lights and the volume are disabled, and post-processing is turned off.
  - **`View/LightPulse`** (plain C#): the swell curve (exactly 1 under Reduced motion, or with no period or depth; depth capped at 1) and golden-ratio phases, so neighbouring lights never swell together.
  - **`BoardView`:** draws on the `Board` sorting layer when it exists and sits behind Default (`SceneLighting.BoardLayerId`). Otherwise it stays on Default, the powered lights are skipped, and one `[Lighting]` warning names the missing layer.
  - **`MatchBootstrap`:**
    - `lightingEffects`, loaded and saved like the other settings.
    - `Arrange` runs after both board builds.
    - `Effects` and `Reduced` are pushed every frame, before the pause check, because the switch lives on the pause menu.
  - **Settings:** `ISettingsHost.LightingEffects`, a "Lighting effects" row after Reduced motion, `SettingsStore.Lighting` and `SaveLighting`.
  - **`NonaRoyale.Unity.asmdef`** also references `Unity.RenderPipelines.Core.Runtime` (for `Volume`).
  - **Editor step (the designer):** add the `Board` sorting layer in Project Settings › Tags and Layers › Sorting Layers, **above Default** in the list (higher in the list is drawn further back). It lands in `ProjectSettings/TagManager.asset`.
  - **Checks:**
    - The view compiles with no warnings against URP 17.6.0.
    - `LightPulseTests` (6) in `NonaRoyale.Unity.EditTests`: **605 passing** in the cloud harness. Mutants caught: Reduced motion ignored, a zero period not guarded, the depth not capped, the phase ignored, negative indices, and a phase spread that repeats.
    - The lights and bloom are Unity rendering, so Play Mode is their test.
  - **Play Mode watch-list:**
    - No `[Lighting]` warning once the layer exists.
    - The corners of the table fall into shadow; the path, the tables and the vault stay readable; pieces keep their seat colours everywhere, including on safe cells.
    - Safe cells glow cyan and bloom a little; the vault's gold hub glows.
    - The HUD, dice and health labels are unchanged.
    - Settings › Lighting effects off gives back the flat room, instantly, and stays off after a restart.
    - Reduced motion stills the swells.
    - The frame rate holds, including on a phone-sized Game view.
- 2026-09-16 — **LT1 review.** The designer added the `Board` sorting layer; since the editor hides it, it was written into `ProjectSettings/TagManager.asset` with Unity closed (`Board`, uniqueID 2847561903, ahead of Default). Everything checks out. The powered-cell glow is a nice touch but a bit strong: `poweredIntensity` 0.5 → **0.35** (30% less).
- 2026-09-17 — **LT2 written: event light.** The designer took all four proposed lights (picker, all recommended), LT2 before the board's corner and texture work, as its own commit.
  - **New `View/EventLights`** (a component beside `SceneLighting`, bound after each `Arrange`):
    - **Selected operator:** a cyan additive pool under the selected piece, 0.9 cells, intensity 0.3, breathing ±12% every 2.6 s. It fades in and out at 2.5 per second on unscaled time and follows the piece, so it rides the hover lift and the walk. The floor only, so the figure keeps its seat colour.
    - **Cast flash:** a cyan additive flash (1.5 cells, peak 0.9, 0.45 s) on the caster as the tell starts, and a second on the target or aimed cell 45% of the way through the tell, when its line arrives. The floor only.
    - **Knockout burst:** a warm multiply flash (2.2 cells, peak 0.9, 0.6 s, `#FFB875`) on the fallen piece with the knockout beat. Every layer, so the burst touches the neighbouring figures too, and bloom catches its peak.
    - **Vault on HOME:** a warm multiply swell at the vault, with the room's vault colour and reach, adding up to 0.7 over 1.6 s. It fires as the settle step starts, which is when the walk has landed.
    - **Rules:** floor-only lights need the `Board` sorting layer and are skipped without it (as the powered cells are). With Lighting effects off nothing new is lit and the pool fades. Flashes run on scaled time times the animation speed, so pause and hit-stop hold them, and Fast plays them faster. Every value is an inspector field read each frame.
  - **`LightPulse.Flash(elapsed, duration, reduced)`:** a smoothstep rise over the first 8% and a squared fall; 0 outside the span, before a delay, or with no duration. Under Reduced motion the rise takes 30% and the peak is `ReducedFlashPeak` (0.5).
  - **`MatchBootstrap`:** `_eventLights` made at Start next to the room's lights; `Bind` after both `Arrange` calls; `Clear` in `StopPresentation`; the selected piece pushed every frame; `CastFlash` in the cast tell step (with its hold); `Knockout` beside `FeedbackLayer.Neutralized`; `OperatorReachedHome` in a batch sets `home`, and the settle step calls `VaultSwell` first.
  - **Checks:** the view compiles against the 6000.6 DLLs. Seven new `LightPulseTests` for `Flash` (13 in the file); 731 passing in the cloud harness. Every mutant of `Flash` fails a test: the end bound, both guards, the reduced peak, the reduced rise, the rise shape and the squared fall.
  - **Play Mode watch-list:**
    - Selecting a piece (on the floor or seated) lays a faint cyan pool under it that follows the hover lift and fades on deselect; the piece itself stays its seat colour.
    - A cast flashes on the caster, then on the target or cell as the tell's line arrives. A self-cast flashes once.
    - A knockout gives a short warm flash with the shatter and hit-stop, and the bloom doesn't blow out the frame.
    - An operator reaching HOME makes the vault swell briefly.
    - Lighting effects off: no event light at all. Reduced motion: softer flashes and a steady pool.
    - CPU turns on Fast and with Space held: the flashes keep up and none are left behind.
    - Main menu mid-flash, and a new deal: no stray lights.
- 2026-09-17 — **LT2 committed** as `9514e90`. The same commit carries GUI G4 (the board texture hookup, `GUI_PHASE.md`).
- 2026-09-18 — **The bloom Volume became the room's grade** (`VISUAL_PASS.md`, V4). `SceneLighting` adds neutral tonemapping, colour adjustments, split toning, a vignette and thin film grain to the same profile, under a `Colour grade (V4)` header. They switch with Lighting effects exactly as bloom does, and Reduced motion drops the grain, which cannot hold still.
