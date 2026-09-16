# Nona Royale — Lighting

> Location in repo: `docs/design/LIGHTING.md` · Project copy: `claude/LIGHTING.md`
> Status: **Open, 2026-09-16.** LT1 (room lighting, powered cells, bloom, the setting) is in the repo, awaiting Play Mode.
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
| LT2 | **Event light** (proposed) | A cool light on the selected operator, a flash on cast tells, a warm burst on a knockout, and a lit vault when a piece reaches HOME. |

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
