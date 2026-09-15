# Nona Royale — GUI Phase

> Location in repo: `docs/design/GUI_PHASE.md`
> Status: **In progress.** Started 2026-09-15. Increment E is written and waiting for a Play Mode check.
> Related: ADR-0008 (uGUI; its removal order stays binding), `PRESENTATION.md` (what the view may do and must show), `ART_DIRECTION.md` §3 and §8 (palette, UI registers), `STRANGER_TEST.md` (the gate before `OnGUI` is deleted)

## Goal

Take the GUI from a working dev panel to 80–100% of a shippable game UI, then move on to the next major feature. The work covers **in-match play and the match flow** (decided 2026-09-15).

**Not in scope:** art. Everything is still drawn from code (PRESENTATION §6). Operator sprites come in the art pass, possibly from 3D models rendered to 2D; that pipeline is `ART_PIPELINE.md` §1 and would get its own ADR. The procedural shapes stay as the fallback and as the silhouette spec.

**The stranger test has no date.** It runs when the new layout is in, and it still gates deleting the `OnGUI` panel (ADR-0008 consequence 6).

## Increments

Each increment ends with a Play Mode check and a commit.

| #   | Increment                 | What it delivers                                                                                                                                                                                                                                                                       | Engine queries                                                  |
| --- | ------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------- |
| E   | **Board-first input**     | Click a piece to select, click a landing to move, click a yard piece to deploy, click to aim. Pip labels on landings. Selection, target and deploy rings. Hover lift. Right-click or Esc steps back. Keys: Space, E, 1–3, Enter. (PRESENTATION §4.1)                                  | `CanDeploy`, `IsHome` (added)                                   |
| F   | **In-match layout**       | Top bar: seat, energy with its cap, round. Bottom tray: dice (spent and unspent), selected-operator card (shape, health, statuses), ability buttons (cost, range, cooldown turns left, reason), cast. Side rail: every seat's squad as waiting, on board or home. Collapsible log. The dev panel is retired behind a toggle. | `EnergyCap`, `CooldownRemaining`, round number                  |
| G   | **Skin pass**             | `UiTheme` tokens from ART_DIRECTION §3. Gold on black for static chrome, cyan for live states (§8). Deco frames from procedural sliced sprites. Board background and landing colours brought into the palette.                                                                          | —                                                               |
| H   | **Pause**                 | Esc with nothing selected: resume, restart, settings (health labels, log), quit to menu.                                                                                                                                                                                               | —                                                               |
| I   | **Match setup and end**   | Setup: seats 2–4, alpha or drafted squads, seed. End: winner, turns, neutralizes per seat, rematch or menu.                                                                                                                                                                             | `Winner`                                                        |
| J   | **Title menu**            | Play, settings, quit. One scene, with screens as canvas states behind a small app-flow state machine.                                                                                                                                                                                  | —                                                               |
| K   | **Stranger test, then cut** | Run STRANGER_TEST on the new layout. After a pass, delete `OnGUI` in one commit.                                                                                                                                                                                                     | —                                                               |

## Rules that hold throughout

- **The view computes nothing** (PRESENTATION §1). A number or a mark the engine can produce comes from a query. A missing query is added beside `PreviewLandings`, with a test.
- **One intent, every input.** Board clicks, keys, the dev panel and the new tray all call the same `IControlPanelHost` intents.
- **Display-only graphics never catch the pointer** (ADR-0008 consequence 9).
- **Compile-checked before Play Mode.** The view layer builds in the cloud workspace against the editor's own `UnityEngine` DLLs, and the core runs its tests there as well. Play Mode is still the acceptance test.

## Log

- 2026-09-15 — Phase opened. Scope and increments agreed.
- 2026-09-15 — **Increment E written.**
  - New files: `BoardPointer` (hit testing), `CellLabelLayer` (pip labels), `PieceMark` (mark flags).
  - Changed: `OperatorPiece` (rings, hover lift, deploy pulse), `MatchBootstrap` (board input, keys, marks, selection dropped on seat change), `ControlPanel` (Select button, numbered abilities, key legend), `HudRoot` (`FindAnyObjectByType`).
  - `IControlPanelHost` renamed `SelectedCaster`/`ToggleCaster` to `SelectedOperator`/`ToggleOperator`: one selection now serves moving and casting.
  - Core: `GameEngine.CanDeploy` and `IsHome`; `Deploy` now shares its checks with `CanDeploy`. Three new tests; 425 passing in the stand-in run.
  - Nuetu's piece is the disc, on purpose.
