# Nona Royale — Art provenance

> Location in repo: `docs/art/PROVENANCE.md`
> One row per shipped art asset or batch: where it came from and under what terms (`ART_PIPELINE.md` §8, ADR-0009). Audio has its own file (`docs/audio/PROVENANCE.md`).

## Operators

| Asset | Made with | Source | Date | Terms | Notes |
| ----- | --------- | ------ | ---- | ----- | ----- |
| `art/source/characters/luka/luka_walk.glb`, `luka_run.glb` | Meshy (image to 3D, 4k textures, auto-rig "biped", Walking and Running clips) | Luka concept sheet (`ART_PROMPTS.md` runs 1–4; crops in `Claude outputs/luka_*.png`) | 2026-09-17 | **Plan not yet recorded.** Meshy Free: CC BY 4.0, credit "Model created with Meshy – CC BY 4.0 License". Paid: owned by the customer. | Original names `Meshy_AI_Luka_4k_biped_Animation_{Walking,Running}_withSkin.glb` |
| `Assets/_Project/Art/Resources/Art/Operators/luka_{standing,seated,portrait}.png` | Blender 4.0.2, `tools/blender/render_operator.py` defaults (head 1.3, legs 0.83, arms 0.92, yaw 30, pitch 25) | `luka_walk.glb` | 2026-09-17 | As the model | Rendered in the cloud; re-render locally for final quality if EEVEE output differs |

## To record

- The image tool and plan behind Luka's concept sheet (`ART_PROMPTS.md`).
- Meshy plan at the time Luka was generated.
- Steam's AI-content disclosure, checked at submission time.
