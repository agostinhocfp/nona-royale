# Nona Royale — UI Motion and Type

> Location in repo: `docs/design/UI_MOTION.md`
> Status: **Open.** U1–U4 written 2026-09-17, Play Mode pending. U5 is parked until after the stranger test (`STRANGER_TEST.md`). U6 written 2026-10-03, Play Mode pending.
> Related: `GUI_PHASE.md` (the skin this animates), `MOTION.md` (board-side motion, MO1–MO2), `ART_DIRECTION.md` §8 (UI registers), ADR-0008 (uGUI rules)

## Goal

Take the HUD from "the skin is right" to "it feels like a modern card game": screens arrive and leave instead of snapping, controls answer the pointer, numbers glide rather than jump, panels sit visibly above the board, and titles get a display face. Everything stays code-drawn and procedural, like the rest of the view (PRESENTATION §6).

## Increments

Each increment ends with a Play Mode check and a commit.

| #  | Increment                             | What it delivers |
| -- | ------------------------------------- | ---------------- |
| U1 | **Motion foundation and transitions** | `UiTween`: one component per tween, unscaled time, Reduced-motion aware; the easing math in plain C# (`UiEasing`) so the harness pins the curves. Modal cards, the pause menu and the draft screen fade and rise in; page swaps cross-fade. |
| U2 | **Micro-interactions**                | Buttons dip on press; slider knobs grow on hover and drag; menu rows cascade in; toasts pop; the turn banner drops in; the end-screen tally counts up row by row; newly lit energy pips cascade; health bars glide. |
| U3 | **Typography**                        | Cinzel (OFL) as the display face on titles, the wordmark and the draft clock (`UiFonts`, loaded from Resources, degrades to the default face with one warning). `FloatingText` moves from the legacy `TextMesh` to TMP, with a birth pop. **The other half — a data face for everything numeric — landed as GUI increment G5 (`GUI_PHASE.md`), which also fixed the clock: Cinzel's figures are not tabular.** |
| U4 | **Elevation**                         | Floating panels cast a two-part shadow and take a whisper of sheen across the top (`UiTheme` tokens, `DecoSprites.PanelSheen`, `UiKit.Elevate`). |
| U5 | **Deeper juice**                      | Parked until after the stranger test; scoped from what testers react to. |
| U6 | **Cooldown sweep**                    | An ability card on cooldown carries a radar sweep: a faint cyan veil over the share still to run, clipped to the card's chamfer, whose hand turns clockwise one step each of the caster's turns and clears the card on the turn it is ready (`CooldownSweep`, designer 2026-10-03). |

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
- 2026-10-03 — **U6: the cooldown sweep** (designer: "a semi-transparent clockwise overlay that sweeps across the icon like a radar to represent time passing").
  - The ability cards carry words, not icons (block 6 of `ART_PROMPTS.md` was retired from the tray), so the sweep covers the card face, under its text, so the words stay legible.
  - **Time is turns.** A cooldown counts the caster's own turns, so the veil holds still between them and sweeps when one passes. Its share is turns left over cooldown + 1, because the turn of the cast counts (`AbilityResolver.PutOnCooldown`): a cooldown of 2 reads full, two thirds, one third, clear. A continuous real-time radar was rejected, because it would suggest time passes in seconds.
  - **Drawing:** `CooldownSweep`, a `MaskableGraphic` that builds a triangle fan from the card's centre to its chamfered outline (cut 6, `DecoSprites.ButtonFill`), with the corners added exactly, and a 1.5-unit hand on top. A filled `Image` would have stretched the sliced sprite and bent the corners. The veil is brightest along the hand and fades into the plain wash over 32°.
  - **Colours:** `UiTheme.CooldownVeil` (cyan 0.10), `CooldownGlow` (cyan 0.28), `CooldownHand` (cyan 0.80). They are cyan because ART_DIRECTION §8 lists cooldowns among the live states in the cool register.
  - **Motion:** a shrinking share sweeps over 0.35 s plus 0.9 s per whole circle, easing out (shorter under reduced motion). A growing share (a cast) appears whole with a 0.15 s fade, because a hand turning backwards would read as time reversing. The tray remembers the last share shown per operator and ability (`ActionTray._sweepShown`), so a rebuilt card picks up where the old sweep stood. An operator you return to after its turn has passed sweeps the step you missed.
  - `CooldownSweepGeometry` keeps the maths free of the UI assembly. `CooldownSweepTests` has 11 tests (share, hand angle, outline on and inside the chamfer, the corners, fan order, keys), all passing in the cloud harness. Core harness 1022 passing. The view compiles with and without `DEVELOPMENT_BUILD`.

## Play Mode checklist

- Title, settings pages, pause menu, draft and end screens fade and rise in; page swaps cross-fade; menu rows cascade.
- Buttons dip on press; slider knobs grow on hover and while dragging.
- Toasts pop; the turn banner drops in; the end-screen tally counts up; energy pips cascade; health bars glide on hits and heals.
- Floating panels cast a soft shadow and carry the sheen; edge hairlines stay crisp.
- Cinzel shows on titles, the wordmark and the draft clock; damage numbers match the HUD face.
- Reduced motion (settings): everything above shortens, and nothing overshoots.
- `FloatingText.FontSize` (3.8) was matched on paper to the old TextMesh — check a damage number against the board and tune.
- **U6:** cast an ability with cooldown 2. The card is veiled whole at once, the words stay readable, and the chamfered corners stay clean. On the caster's next turn the hand sweeps clockwise to two thirds left, then one third, then the card clears as it becomes ready. Other seats' turns move nothing. Selecting another operator and coming back neither replays nor skips a step. Check the veil's strength against the dimmed card upright on a phone; tune `CooldownVeil`/`CooldownGlow` if it is too faint or too loud.
