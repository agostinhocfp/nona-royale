# Nona Royale — GUI Phase

> Location in repo: `docs/design/GUI_PHASE.md`
> Status: **In progress.** Started 2026-09-15. E is done; F is written and waiting for a Play Mode check.
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
- 2026-09-15 — **Increment E passed Play Mode** and was committed (`678c8db`).
- 2026-09-15 — **Increment F written.**
  - New files:
    - `UiKit`: shared widgets and provisional palette colours, which G will skin.
    - `SquadRail`: every seat's squad, with state, health and statuses. A click on a row selects or deploys, like a click on the piece.
    - `ActionTray`: dice, Roll and End turn, the operator card, ability cards showing "ready in N" or the energy needed, the aim hint, and Cast.
    - `LogPanel`: newest first, rejections tinted, L collapses it to a tab.
  - `TurnStrip` became the full-width top bar: seat, energy pips against the cap, round, prompt and key legend.
  - `ControlPanel` is now the dev panel. Tab shows it in place of the rail.
  - `MatchBootstrap` fits the board between all four reservations.
  - **`showPanel` renamed `showDevPanel`** (default off), so the value saved in the scene does not carry over.
  - Core: `EnergyCap`, `Round`, `TurnsUntilReady`, `Winner` and `CanRollAgain` on `GameEngine`, backed by `EnergyLedger.Cap`, `TurnStateMachine.Round` and `AbilityResolver.TurnsUntilReady`. Four new tests; 429 passing in the stand-in run.
- 2026-09-15 — **F, first Play Mode look.** The layout reads well. The bottom and top edges looked cut off because the Game view was set to a fixed 1920×1080 in a shorter window, and Unity crops a fixed resolution that does not fit. The top bar was entirely off screen. Use a 16:9 aspect or zoom out. Fixes from the same screenshot:
  - Tray sections and top-bar items grew past their widths. A layout group that force-expands its children reports itself as flexible, so fixed sections are now pinned with `UiKit.Fixed`.
  - Rail names were cut off ("Boun…"). The name now has its own line, with state and statuses on the second, and the health column is fixed.
  - Health labels of pieces sharing a cell printed on top of each other ("9/6/6"). `PieceHudLayer.SetStack` now spreads them by a label's width.
- 2026-09-15 — **Increment F2, from designer feedback on F.**
  - **The text log gave way to a history strip** (Hearthstone-style): a 78-unit column with one chip per action, where it used to be 360 units of text. Each chip shows the acting piece's shape, a word (MOVE, HIT, CAST, DEPLOY, UPKEEP, HOME, WIN) and one number, with a KO mark when someone fell. Hovering shows the full card. Turns are divided by seat colour and round.
  - **Toasts:** up to three short-lived lines at the top of the board for casts, hits, KOs, upkeep and refusals.
  - **The full log became an overlay** (L, or LOG at the top of the strip). `showLog` was renamed `showFullLog`, off by default.
  - **Turn banner:** "RED'S TURN" plus upkeep lines and a pulsing ROLL button at the start of every turn. Rolling closes it; Esc hides it. The tray's Roll button pulses whenever a roll is due.
  - **Ability card text is smaller** (names 15, details 13), and the operator name is 22.
  - New files: `HistoryFeed` (events into items, one per action), `HistoryStrip`, `HistoryChip` (hover), `EventToasts`, `TurnBanner`, `UiPulse`. `UiKit.Border` now returns its strips, and `UiKit.Pulse` was added.
  - No core changes.
- 2026-09-15 — **Ending a turn made obvious** (designer: "how do I end a turn?"). The tray's End turn button and E already did it, but the button sat greyed out with no reason while moves were owed.
  - The dice header now says what the dice are waiting for: roll to start, move to spend them, doubles, no legal move, or all spent.
  - Once End turn is the next thing to press, the button turns teal and pulses.
  - The top bar's prompt names the E key.
- 2026-09-15 — **Turn flow made subtle and clear** (designer: ending a turn still unclear, and the turn card too big).
  - **`TurnButton`: one button at the board's bottom-right corner**, in the card-game end-turn style. It reads ROLL or ROLL AGAIN (gold, breathing), then MOVE FIRST with the dice left (dim, disabled), then END TURN (teal, breathing). Roll and End turn left the tray, whose dice section now shows only the dice and a one-line hint.
  - **`TurnBanner` became a slim pill** under the top bar: a seat-colour dot, "RED's turn", the round and "Space to roll". It stays until the roll, then fades. It no longer catches the pointer, and Esc no longer touches it. Upkeep effects are left to the toasts.
