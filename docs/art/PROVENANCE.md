# Nona Royale — Art provenance

> Location in repo: `docs/art/PROVENANCE.md`
> One row per shipped art asset or batch: where it came from and under what terms (`ART_PIPELINE.md` §8, ADR-0009). Audio has its own file (`docs/audio/PROVENANCE.md`).

## Operators

| Asset | Made with | Source | Date | Terms | Notes |
| ----- | --------- | ------ | ---- | ----- | ----- |
| `art/source/characters/luka/luka_walk.glb`, `luka_run.glb` | Meshy (image to 3D, 4k textures, auto-rig "biped", Walking and Running clips) | Luka concept sheet (`ART_PROMPTS.md` runs 1–4; crops in `Claude outputs/luka_*.png`) | 2026-09-17 | **Paid Meshy plan** (designer, 2026-09-17): the output is owned by the customer, no attribution required. | Original names `Meshy_AI_Luka_4k_biped_Animation_{Walking,Running}_withSkin.glb` |
| ~~`Assets/_Project/Art/Resources/Art/Operators/luka_{standing,seated,portrait}.png`~~ (replaced 2026-09-24, see below) | Blender 4.0.2, `tools/blender/render_operator.py` defaults (head 1.3, legs 0.83, arms 0.92, yaw 30, pitch 25) | `luka_walk.glb` | 2026-09-17 | As the model | Rendered in the cloud; re-render locally for final quality if EEVEE output differs |
| `art/source/characters/luka/Meshy_AI_Bandaged_Resolve_biped/` (FBX + textures) | Meshy (image to 3D, auto-rig "biped", Charged Slash clip) | The Dark Deco Chibi Noir Luka sheet (T-pose), designer, 2026-09-24 | 2026-09-24 | **Plan not yet recorded** — Free-plan output is CC BY 4.0 and needs attribution | Meshy's rig and clip are unused: the bind pose imports broken and the skinning tears (`claude/LUKA_CHIBI_PILOT.md`) |
| `art/source/characters/luka/luka_chibi_rig.blend` | Blender 4.2 (`bpy`), cloud session: new Mixamo-named rig with fists, automatic weights; trousers lifted to charcoal in the base texture (packed) | The Meshy model above | 2026-09-24 | As the model | Scripts in the session only until they move to `tools/blender/` |
| `Assets/_Project/Art/Resources/Art/Operators/luka_{standing,seated,portrait}.png` | Blender 4.2 EEVEE from `luka_chibi_rig.blend`: toon ramp, warm key, cool rim, Freestyle `#1C0E12` at 2 px; standing yaw 30 / pitch 25 in a boxer's guard, seated taping his hands, portrait yaw 24 / pitch 6 | `luka_chibi_rig.blend` | 2026-09-24 | As the model | Replaces the 2026-09-17 renders; same file names and import settings (`.meta` unchanged) |

## Fonts

| Asset | Made with | Source | Date | Terms | Notes |
| ----- | --------- | ------ | ---- | ----- | ----- |
| `Assets/_Project/Art/Resources/Art/Fonts/Cinzel.ttf` | Natanael Gama / NDISCOVER (Google Fonts) | `google/fonts` repo, `ofl/cinzel/Cinzel[wght].ttf` (upstream `NDISCOVER/Cinzel` @ `8271e16`) | 2026-09-17 | SIL Open Font License 1.1 | Variable weight 400–900; Unity uses the default instance. The display face for titles, the wordmark and the draft clock (UI_MOTION.md U3). **Its figures are not tabular** — `1` is 344/1000 against `0` at 552 — so the clock sets its own mono-spacing (GUI_PHASE.md G5) |
| `Assets/_Project/Art/Resources/Art/Fonts/Archivo.ttf`, `Archivo-SemiBold.ttf` | Omnibus-Type (Google Fonts), instanced and feature-frozen | `google/fonts` repo, `ofl/archivo/Archivo[wdth,wght].ttf` | 2026-09-18 | SIL Open Font License 1.1 | The data face (GUI_PHASE.md G5). Static instances of the variable source at wght 400 and 600, wdth 100 (fontTools `instantiateVariableFont`), then `tnum` and `lnum` applied to the outlines with `opentype-feature-freezer` so every digit is one advance — 568/1000 Regular, 579 SemiBold — because TMP in ugui 2.6 has no `tnum` to switch on. Name tables rewritten to `Archivo` / `Regular` and `Archivo` / `SemiBold`; the OFL permits both the instancing and the rename |

## To record

- The image tool and plan behind Luka's concept sheet (`ART_PROMPTS.md`).
- Steam's AI-content disclosure, checked at submission time.
