# Nona Royale — UI Motion and Type

> Location in repo: `docs/design/UI_MOTION.md`
> Status: **Open.** U1–U4 written 2026-09-17, Play Mode pending. U5 is parked until after the stranger test (`STRANGER_TEST.md`).
> Related: `GUI_PHASE.md` (the skin this animates), `MOTION.md` (board-side motion, MO1–MO2), `ART_DIRECTION.md` §8 (UI registers), ADR-0008 (uGUI rules)

## Goal

Take the HUD from "the skin is right" to "it feels like a modern card game": screens arrive and leave instead of snapping, controls answer the pointer, numbers glide rather than jump, panels sit visibly above the board, and titles get a display face. Everything stays code-drawn and procedural, like the rest of the view (PRESENTATION §6).

## Increments

Each increment ends with a Play Mode check and a commit.

| #  | Increment                             | What it delivers |
| -- | ------------------------------------- | ---------------- |
| U1 | **Motion foundation and transitions** | `UiTween`: one component per tween, unscaled time, Reduced-motion aware; the easing math in plain C# (`UiEasing`) so the harness pins the curves. Modal cards, the pause menu and the draft screen fade and rise in; page swaps cross-fade. |
| U2 | **Micro-interactions**                | Buttons dip on press; slider knobs grow on hover and drag; menu rows cascade in; toasts pop; the turn banner drops in; the end-screen tally counts up row by row; newly lit energy pips cascade; health bars glide. |
| U3 | **Typography**                        | Cinzel (OFL) as the display face on titles, the wordmark and the draft clock (`UiFonts`, loaded from Resources, degrades to the default face with one warning). `FloatingText` moves from the legacy `TextMesh` to TMP, with a birth pop. |
| U4 | **Elevation**                         | Floating panels cast a two-part shadow and take a whisper of sheen across the top (`UiTheme` tokens, `DecoSprites.PanelSheen`, `UiKit.Elevate`). |
| U5 | **Deeper juice**                      | Parked until after the stranger test; scoped from what testers react to. |

## Rules that hold throughout

- **Tweens run on unscaled time.** A paused game (timeScale 0) must not freeze the pause menu's own motion.
- **Reduced motion is one flag** (MO2). The composition root sets `UiTween.ReducedMotion` in `SyncMotion`; every tween runs shorter and overshooting eases fall back to smooth ones.
- **Layout groups own positions.** Slide tweens are for things a layout does not place (cards, pills); inside a layout, fade and scale only.
- **A tween is a component on the thing it moves** — a rebuild that destroys the thing kills the tween with it. No manager, no singleton (same shape as `UiPopIn`).
- **The math is plain C#.** `UiEasing` has no Unity types, so the stand-in harness pins the curves; `UiTween` is the Unity shell around it.

## Log

- 2026-09-17 — **U1–U4 written.**
  - New files: `UiTween`, `UiEasing` (six harness tests), `UiButtonFeel`, `UiSliderFeel`, `UiFonts`; `Cinzel.ttf` under `Art/Resources/Art/Fonts` (provenance in `docs/art/PROVENANCE.md`).
  - Screens: `ModalCard` and `PauseMenu` fade the scrim, rise the card and cascade the rows; page swaps cross-fade (`PlayPageTransition`). `DraftScreen` fades and rises on open.
  - Micro: kit buttons dip to 0.96 on press (`UiButtonFeel`); slider knobs grow on hover and drag (`UiSliderFeel`); toasts pop (`UiPopIn`); the turn banner drops in from under the top bar; the end screen counts its tally up row by row; newly lit energy pips pop in sequence; health bars in the squad rail and the operator card glide between fractions (`UiKit.TweenBar`, 0.35 s, remembered per operator across rebuilds).
  - Type: Cinzel on screen titles, the NONA ROYALE wordmark, the draft title and clock. `FloatingText` is TMP on the default face, bold, with a 1.35× birth pop.
  - Elevation: `UiTheme.PanelShadowNear`/`Far` (with offsets) and `PanelSheen`; `UiKit.Panel` elevates and sheens every floating panel.
  - 67 passing in the stand-in run (the csc harness: `dotnet restore` is broken machine-wide, SDK 10.0.401, so the harness compiles straight with csc — see `Temp/audio-tests`).
- 2026-09-17 — **Presence pass, after the first Play Mode look** ("not seeing much different"). The first tuning was too quiet to perceive: 0.18 s fades, black shadows at 0.30/0.16 on a near-black theme, a 0.045 sheen. Strengthened: screen fades 0.28 s with a 28-unit rise, cascade 0.055 s per row, page swaps 0.18 s, panel shadows 0.55/0.32 at −8/−24, sheen 0.10, button dip 0.93, slider knob ×1.3/×1.5. The display face now also covers every `UiKit.Heading` (DICE, OPERATOR, ABILITIES, …), not just titles — the one change visible on every screen at rest.

## Play Mode checklist

- Title, settings pages, pause menu, draft and end screens fade and rise in; page swaps cross-fade; menu rows cascade.
- Buttons dip on press; slider knobs grow on hover and while dragging.
- Toasts pop; the turn banner drops in; the end-screen tally counts up; energy pips cascade; health bars glide on hits and heals.
- Floating panels cast a soft shadow and carry the sheen; edge hairlines stay crisp.
- Cinzel shows on titles, the wordmark and the draft clock; damage numbers match the HUD face.
- Reduced motion (settings): everything above shortens, and nothing overshoots.
- `FloatingText.FontSize` (3.8) was matched on paper to the old TextMesh — check a damage number against the board and tune.
