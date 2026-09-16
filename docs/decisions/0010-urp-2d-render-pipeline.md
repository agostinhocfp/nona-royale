# ADR-0010 — Render pipeline: URP with the 2D Renderer

- **Status:** Accepted 2026-09-16 — the switch passed Play Mode: the game looks as it did on Built-in
- **Date:** 2026-09-16
- **Location:** `docs/decisions/0010-urp-2d-render-pipeline.md`
- **Relates to:** ADR-0001 (2D locked), ADR-0008 (uGUI, everything built in code), `ART_PIPELINE.md` §1 and §4, `ART_DIRECTION.md` (focal lighting, glossy reflections, glow on powered cells), `claude/STAGE4_HANDOFF.md`. ADR-0009 is reserved for Stage 4's 2.5D production decision.

## Context

`ART_PIPELINE.md` §1 has always said the game renders with **URP and the 2D Renderer**, because the art bible asks for things only a scriptable pipeline gives a 2D game: real 2D lights, normal-mapped sprites, and bloom. The project was never set up that way. It was created from a template on the **Built-in Render Pipeline**: no URP package, no pipeline asset in Graphics or Quality, and Gamma colour space.

This went unnoticed because nothing so far needed it: every sprite, texture and material is built in code, and the default sprite material renders the same either way. Real art and bought VFX are next (Stage 4), and both depend on the pipeline.

Unity marked the Built-in Render Pipeline **deprecated in Unity 6.5**. No removal date is set, but Unity guarantees it through Unity 6.7 LTS. The project runs Unity 6.6 (6000.6.0f1).

## Options considered

**(a) Stay on Built-in.** Nothing to do now. Lighting, normal maps and bloom would be faked with additive quads and custom shaders (the path §1 already rejects), and a migration would still be due before removal, with art by then authored against the wrong look.

**(b) URP with the Universal (3D) Renderer.** What Unity 6.6's Render Pipeline Converter targets. It lights sprites only through 3D lights and has no 2D lights, shadow casters or sprite normal-map path.

**(c) URP with the 2D Renderer.** What §1 specifies. It adds 2D lights (global, spot, freeform, sprite), normal and mask maps on sprites, 2D shadows and URP post-processing. Sprites get a lit material by default.

## Decision

**(c), switched now, before any art lands.** Settled with the designer on 2026-09-16:

1. **Package:** `com.unity.render-pipelines.universal` 17.6.0, the version that matches the editor.
2. **Assets** in `Assets/_Project/Settings/`: `NonaURP` (pipeline asset) and `NonaURP_Renderer` (2D Renderer data). `NonaURP` is the default in Graphics. Every quality level leaves its own pipeline asset empty, so all of them use `NonaURP`.
3. **Sprites use the lit material** (the 2D Renderer's Default Material Type: Lit), so lights and normal maps can arrive later without touching the pieces.
4. **Colour space: Linear**, as `ART_PIPELINE.md` §4 specifies. It changes only how see-through layers blend (glows, halos, shadows), so it happens once, now, before art is authored against the old look.
5. **The Render Pipeline Converter is not used.** It targets the 3D renderer only, and the project has no material, animation or post-processing assets for it to convert.

## Consequences

1. **A white global 2D light, built in code** (`View/SceneLighting`, created by `MatchBootstrap.Start`), keeps every lit sprite at its own colour, so the switch changes nothing on screen.
   - A global light already in the scene is used instead, so a designer-authored one wins.
   - The code never adds a second one; two global lights on the same layer and blend style is a URP error.
   - When focal lights arrive, the ambient drops below 1 so they have something to lift.
2. **`NonaRoyale.Unity.asmdef` references `Unity.RenderPipelines.Universal.Runtime` and `Unity.RenderPipelines.Universal.2D.Runtime`.** The view assembly now needs URP installed.
3. **Everything is still built in code** (ADR-0008 consequence 2). The only new hand-made assets are the pipeline and renderer assets. Unity also created `UniversalRenderPipelineGlobalSettings`, `DefaultVolumeProfile`, `URPProjectSettings`, `ShaderGraphSettings` and rendering-debugger axes in the Input Manager; all are committed.
4. **Anti-aliasing moves to the pipeline asset.** Unity cleared the quality level's MSAA (it was 2×), and `NonaURP` ships with MSAA off. Sprites are alpha-edged quads, so this is invisible today. Turn MSAA on in `NonaURP` if hard mesh edges appear.
5. **HDR is on** in `NonaURP` (the default), which bloom needs.
6. **Custom shaders must be URP shaders.** Built-in surface shaders don't render. Bought VFX must be URP-compatible or plain sprite sheets. TextMesh Pro's SDF shaders render under URP; the package also ships URP Shader Graph variants if lit text is ever wanted.
7. **Tests don't cover lighting.** Play Mode is the check: the board, pieces, HUD, dice, floating numbers and knockout shards look as they did before the switch.

## Revisit

If a platform target (a WebGL build or a low-end phone) struggles with the 2D Renderer's light passes, first cut post-processing and the light render-texture scale in `NonaURP_Renderer`. Going back to Built-in is not an option once lit art exists.
