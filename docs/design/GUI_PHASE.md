# Nona Royale — GUI Phase

> Location in repo: `docs/design/GUI_PHASE.md`
> Status: **Closed at J, 2026-09-16; reopened 2026-09-17 for G3 (polish pass), written and awaiting Play Mode.** E through J are committed. K (stranger test, then the OnGUI cut) is parked until testers are available. What comes next is `NEXT_PHASES.md`.
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
| G   | **Skin pass** (G2: board and figures to the reference image)             | `UiTheme` tokens from ART_DIRECTION §3. Gold on black for static chrome, cyan for live states (§8). Deco frames from procedural sliced sprites. Board background and landing colours brought into the palette.                                                                          | —                                                               |
| G3  | **Polish pass** (quiet trim, richer surfaces)                          | HUD edges become one anti-aliased gilt hairline at about 50% alpha. Bright gold only for headings, the active seat's plate and ROLL. Corner fans smaller and dimmer, on floating cards only. Cyan live edges unchanged. Board: procedural material detail (marble veining, felt fibre, a gilt highlight streak, a faint Deco floor pattern), a single gilt rule on the table, a thinner cross-floor edge, stepped Deco corners, one cracked length of trim, a faint haze in the light pools. The table's corners stay dark. Painted textures come later through the same sprite seams. | —                                                               |
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
- 2026-09-15 — **F and F2 passed Play Mode** and were committed together.
- 2026-09-15 — **Increment G written: the skin pass.** Decided with the designer: the ART_DIRECTION §3 hexes are used as they stand (the lock is later, and is a one-file edit); the board goes "moderate" toward §6.1, still readable at rest for the stranger test; aim targets stay amber; theme tokens are a static class.
  - New files:
    - `UiTheme`: every colour and type size the view uses. The §3 swatches, HUD surfaces, text, game meaning (damage, heal, threat, reject), board, pieces and seats. `UiKit`'s colour fields moved here, and `BoardLayout.ColourOf` now reads `UiTheme.Seat`.
    - `DecoSprites`: the frame kit, drawn in code from signed distances, white so it tints. Chamfered fills and edges (panel, button, chip), sliced at one texel per canvas unit; a double rule; a corner fan; tall diamonds, filled and outlined; and for the board a tile inlay, a thin ring, a sunburst, a glow and a sliced table.
  - `UiKit`: `Frame` became `Dock` (a docked panel with a gold-and-brass double rule and a diamond at its middle). `Panel` is the floating chamfered card with corner fans. New `Heading` (spaced gold capitals), `Diamond`, `Divider`, `Sliced`, `Overlay` and `CornerFans`. Buttons are chamfered with a brass edge; a selected button fills cyan-deep with a cyan double edge. `Pulse` breathes a double edge and takes a colour.
  - HUD: section labels are gold headings. Energy pips are diamonds, cyan when lit and brass outlines when empty. Dice are ivory with a gold edge. Cast, when ready, is cyan. The turn button has corner fans: ROLL in gold, END TURN in cyan (the live step), MOVE FIRST dim. The rail marks the seat to play with a gilt plate and each seat with a diamond. Chips, tags, toasts, the turn pill and cell labels are chamfered; landing labels are cyan. The log overlay and the history hover card are framed panels.
  - Board (`BoardView`): a charcoal table with a double gilt frame, corner fans and a pool of warm light. Track cells are dark marble with a brass inlay. Safe cells are powered tiles (cyan-lifted marble, cyan inlay, faint cyan glow), and each start cell carries a wash of its seat colour. Home columns deepen toward HOME with a gold inlay. Yards are felt tables in the seat's colour with a seat rim and a gilt rim. HOME is a dark vault floor under a lit gold sunburst. `BoardLayout.Spacing` was added for the table margin.
  - Highlights: landings are cyan rings; reach became small cyan dots, so landings and reach tell apart by shape; cell targets and target rings are amber (`UiTheme.Threat`). Piece outlines are ink (§5).
  - Left as they were: `StatusPalette` (status colours are their own table), `DeviceLayer` (seat-tinted by design) and the dev panel.
  - No core changes. View compile-checked against the editor's DLLs; 429 passing in the stand-in run.
- 2026-09-15 — **G committed.** The designer liked it as a start and shared a reference image for the board: a dark room, a cross-shaped floor with a gilt edge and no visible cells, four felt tables with gilt rims, seated figures, a standing figure with a cyan glow and halo, a glowing vault door.
- 2026-09-15 — **Increment G2 written: the board to the reference.** Decided with the designer: the path stays as a faint whisper, enough to count squares, with the turn's cells lit on demand; the fourth seat becomes violet, renamed in the core; operators become figures wearing their shape as a pin, as a stand-in for the rendered character models.
  - **Core: `PlayerColor.Yellow` is now `Violet`** (value 3 unchanged, so saved scenes and offsets hold). Yellow read poorly against the gold trim and the amber aim colour. Tests, `PathMap` and the sim follow. 429 passing in the stand-in run; the sim compiles.
  - New `BoardArt`: shaded procedural sprites (grey luminance baked in, tinted by the renderer). The gilt table rim (bevel, light from the top left), felt (vignette and inner shadow), dotted ring, dealer's arc, soft shadow disc, seated bust and standing pawn with outlines, halo, vault plate, frame and dial, a polished boss, the cross floor (fill, edge, shadow; cached per grid) and the table's grain. `DecoSprites` shares its rasterisers and lost the table and sunburst sprites G used.
  - `BoardView` redrawn: square table with grain; cross floor with shadow, gilt edge, a pool of light and a gold lane in each arm; whispered cells (faint marble and inlay); powered safe cells (cyan glow); start cells inlaid in the seat's colour; home columns washed in the seat's colour, deepening toward HOME; four felt tables (all seats, played or not) with shadow, dotted ring, arc, dealer's spot, chips, seat marks and a gilt rim; the vault door with glow, frame, dial and boss. `Build` now takes the seats per table.
  - `BoardLayout`: the yard is the centre of the corner block (was a cell beside it); `TableDiameter`; `YardSeat(colour, seat)` places an operator at a fixed seat (west, north, east, south, then diagonals), so nobody shuffles when a squadmate stands up.
  - `OperatorPiece`: a bust while in the yard, a pawn on the floor, with the operator's shape as a gold pin; seat colour kept at full strength in the yard (the pose says "waiting"); selection is a cyan glow plus a halo over the head, deployable is a pulsing glow, targets keep the amber ring; the health bar sits above the head and hides while seated; figures are drawn 1.45x the old token size.
  - `MatchBootstrap`: yard pieces go to their seats; piece readouts sit 0.8 of a cell from the piece (was 0.55) to clear a standing head.
  - `UiTheme`: board tokens replaced for the new floor; `SeatViolet`; `PieceEmblem`, `FigureLift`.
  - Previewed before Play Mode by porting the sprite maths to numpy and composing the board (`board_preview.png` in the chat).
- 2026-09-15 — **G2 passed Play Mode and was committed** (designer: "such a great job with the board").
- 2026-09-15 — **Increment H written: the pause menu.**
  - New `PauseMenu` and `IPauseHost`. A full-screen scrim (catches the pointer) under a framed card. Main page: RESUME (cyan, Esc), RESTART, SETTINGS, QUIT, and the round and seat to play (or the result) under the title. Settings page: toggles for health above pieces (H), the event log (L) and the dev panel (Tab), each with its key and an ON/OFF chip, and BACK.
  - **Opening.** Esc when nothing is selected, or the new MENU button at the top bar's right end. With a selection, Esc still steps back first. While open, Esc goes back a page, then resumes.
  - **Pausing stops the clock** (`Time.timeScale` 0, restored on close and if the component is disabled). Board clicks, hover, game keys and the H, L, Tab and F2 toggles are ignored while open; the HUD's unscaled pulses keep breathing. The menu takes the top sibling back if an overlay opens above it.
  - **RESTART and QUIT ask twice**: the first press turns the button amber with a one-line warning. RESTART deals a new match with the next seed and the same settings (a same-seed replay repeats the dice; that stays a dev tool). QUIT stops Play Mode in the editor and quits a build; the title menu arrives with J.
  - `TurnStrip`: MENU button; legend now "Esc back / menu" and drops H and Tab (they live in settings); the match-over prompt points at Esc instead of the dev panel.
  - `MatchBootstrap` implements `IPauseHost`. No core changes; view compile-checked.
  - Known: the legacy OnGUI dev panel (Tab then F2) still draws over the scrim. It is deleted in K.
- 2026-09-16 — **H committed.**
- 2026-09-16 — **Increment I written: match setup and end.** Decided with the designer: setup shows on Play with a Skip Setup flag, and the pause menu's RESTART becomes NEW MATCH (opens setup); seats are four toggles, any two to four; knockout credit goes into the core.
  - **Core.** `NeutralizeOutcome.CreditedTo` (the killer's seat; null for self, friendly or unknown kills; independent of the bounty setting). `OperatorNeutralized.CreditedTo`. `GameEngine.KnockoutsScoredBy(seat)` and `OperatorsLostBy(seat)`, tallied at both places a neutralize is reported. COMBAT_SYSTEMS §1.2 records the rule. Five new tests (three service, two engine including a whole scripted match that checks tallies against announced events); 434 passing in the stand-in run; the sim compiles.
  - New `ModalCard` (scrim plus framed card, rebuilt on change, keeps the top sibling), `MatchSettings` and `IMatchFlowHost`, `SetupScreen`, `EndScreen`.
  - **Setup:** seat tiles (diamond, name, PLAYING or EMPTY; the last two refuse to switch off and say why), ALPHA THREE or DRAFTED with a line on each, the seed with SHUFFLE, DEAL (Enter), and BACK TO THE MATCH (Esc) when there is one or QUIT when there is not. It edits a copy; backing out changes nothing. Shown over the empty table, which is drawn before the first deal.
  - **End:** "RED WINS" in the seat colour, round and seed, a tally table (home, knockouts, lost) with the winner's row plated, REMATCH (Enter, next seed), NEW MATCH, VIEW BOARD (Esc; Esc again brings the screen back), QUIT. Opens 1.2 s after the win, on unscaled time.
  - `MatchBootstrap`: implements `IMatchFlowHost`; Start draws the empty table and opens setup unless `skipSetup`; seats come from the setup (the `players` field seeds the first deal); one modal check gates input; Esc/Enter routed to the open card; the end screen is queued once per match.
  - `PauseMenu`: NEW MATCH replaces RESTART (no confirmation; setup has its own way back). `IPauseHost.Restart` became `OpenSetup`. `TurnStrip`: match-over prompt says Esc shows the results.
  - Known: the legacy OnGUI dev panel still draws over the cards until K.
- 2026-09-16 — **I committed** (designer: "looks great").
- 2026-09-16 — **Increment J written: the title menu and the app flow.** Decided with the designer: in-match QUIT becomes MAIN MENU, and only the title quits; display settings are remembered between sessions.
  - New `TitleScreen` (with `ITitleHost`): no card, a light scrim over the empty room, a warm glow, the NONA wordmark, ROYALE between two diamond-tipped rules, the tagline "Nine operators. One vault.", then PLAY (cyan, Enter), SETTINGS and QUIT (asks twice). Its settings page is the pause menu's.
  - New `ISettingsHost`, `SettingsRows` (the shared rows) and `SettingsStore` (PlayerPrefs; missing keys fall back to the inspector). `UiKit.ToggleRow` replaces the pause menu's private toggle. `IPauseHost` now extends `ISettingsHost`.
  - **App flow.** `MatchBootstrap.CurrentScreen` (Title, Setup, Match, Paused, Results) is read from the open cards, so a card that closes itself cannot desynchronise it; Esc and Enter route by it. Start shows the title (Skip Setup still deals straight in). `ShowTitle` closes every card, tears the match down (pieces, marks, devices, labels, history, toasts) and redraws the empty room. Settings are loaded at Start and saved whenever a flag changes, whatever changed it.
  - **The in-match HUD moved to `HudRoot.MatchLayer`**, a full-canvas child hidden on the title; the cards stay on the canvas root. `HistoryStrip` now uses `lossyScale` for its hover card, since its parent is no longer the canvas. The camera frames the room with no HUD reservations when there is no match.
  - Setup: BACK always shows (to the match, or to the title); `IMatchFlowHost.Quit` became `CancelSetup` and `MainMenu`. Pause: QUIT became MAIN MENU (asks twice, "This match is lost"). End: QUIT became MAIN MENU (no confirmation; the match is over). `ModalCard` gained `Framed`, `ColumnSlot`, `Root`.
  - No core changes. View compile-checked; 434 tests unchanged.
  - Known: the legacy OnGUI dev panel still draws over the cards until K.
- 2026-09-16 — **J committed** (designer: "you nailed the menu").
- 2026-09-16 — **Informal test at home: the GUI passed easily.** One note: the dice roll could be more obvious, either by catching the eye or by sitting more centrally. Carried to `NEXT_PHASES.md` Stage 3 (dice moment).
- 2026-09-16 — **Phase closed at J.** K is parked: the formal stranger test has no date, and the OnGUI dev panel stays behind Tab until it passes (ADR-0008 consequence 6). Next work, in order: All Pick Draft, CPU opponents, motion and feedback, art hookup (ADR-0009), audio (`NEXT_PHASES.md`).
- 2026-09-17 — **Increment G3 planned: the polish pass.** Designer: the in-match borders are "screaming gold"; the HUD should be slick and effective, and the board should look more high-end. The phase reopens for this one increment.
  - **Decided with the designer** (picker):
    - Board: procedural polish now; tileable painted textures (marble, felt, carpet) later, through the same sprite seams.
    - HUD edge: a single gilt hairline at about 50% alpha replaces the gold-and-brass double rule.
    - Table corners: left dark. Corner decor is deferred; the notes are in the hand-off.
  - **Why.** G and G2 run past ART_DIRECTION §3's rough 20% gold per frame. The richness moves from trim into surfaces and light.
  - **HUD.**
    - Bright gold (`#F4D98B`) is kept for section headings, the active seat's plate and the ROLL state.
    - Corner fans shrink and drop to about 40% alpha, on floating cards only; docks lose them.
    - Cyan live-state edges are unchanged; against quieter gold they read more clearly.
  - **Board.**
    - Materials: low-contrast marble veining, felt fibre noise, gilt with one highlight streak, a Deco chevron or fan pattern in the cross floor at 5–8% contrast.
    - Trim: a thinner cross-floor edge, stepped Deco corners where the arms meet (§6), a single gilt rule on the table instead of the double one, and one cracked length of trim (§6, the deliberate break).
    - Atmosphere: a very faint drifting haze in the warm pools, stilled under Reduced motion and absent with Lighting effects off.
  - **Rules.** Nothing decorative sits under a piece or competes with the lit cells (§6.1). The room with Lighting effects off must still look finished. Display-only graphics never catch the pointer (ADR-0008 consequence 9).
  - **Technical note.** At the 1080 reference with match 0.5, a 1-unit line falls under a pixel on 1366×768 and shimmers. The hairline should be an anti-aliased signed-distance edge at about 1.5 units.
- 2026-09-17 — **Increment G3 written** (on HEAD `6c9e75e`; the uncommitted ART1 follow-ups in `OperatorPiece`, `ART_HOOKUP.md`, `PROVENANCE.md` and `render_operator.py` are not part of it).
  - **HUD.**
    - `UiTheme.Line` is now gold at 50% (was brass at 95%) and serves every resting edge; `LineBright` is gone. New `FanAlpha` (0.4).
    - `DecoSprites`: `HairlineWidth` 1.5 units. `PanelEdge` is one hairline (was a 2-unit and a 1-unit rule). New `RuleAlong` and `RuleUp`: anti-aliased straight hairlines in a 4-unit box, since a solid quad that thin snaps to whole pixels. `FanSize` 22 → 16, `FanInset` 7 → 5; the fan is drawn at its size rather than scaled.
    - `UiKit.Dock`: one hairline on the inner edge, no diamond. `Panel`: hairline frame, fans at `FanAlpha`. `Divider`: hairline and a smaller diamond, both at `Line` strength.
    - `TurnButton`: fans at `FanAlpha`, and none while the button waits (MOVE FIRST, a CPU seat). `SetupScreen`: the CPU style chip's edge is `Line`, not full gold.
    - Kept at full gold: headings, the active seat's plate on the rail, the winner's row, ROLL and its pulse, the dice. Cyan edges unchanged.
  - **Board** (`BoardArt`, `BoardView`, `UiTheme`).
    - The cross fill is shaded marble: a soft cloud and two sets of noise-bent veins, luminance 0.8–1 (`CrossFloor` lifted to `#1B161E` to match). Noise is a fixed integer hash (`Hash`, `ValueNoise`, `Fbm`), so the room is the same every run.
    - A new cross part, the sunburst: 48 rays from the vault (alternate rays start further out) and rings every two cells, gold at 4%, kept off the edge and out of the vault's light. `Cross` returns four sprites, indexed by `CrossFill`, `CrossEdge`, `CrossShadow`, `CrossPattern`.
    - The cross edge is 1.4 texels (was 2.2) and steps in three notches at each inner corner. The boxes reach half a cell back into the floor, so their joins draw no edge.
    - The deliberate break: on the south arm's west edge, a little under halfway out, the trim tarnishes to half for about a cell and a half, with a slanted gap at its middle and a short hairline crack in the marble beside it, clear of the cells.
    - Felt: per-texel grain and a faint one-way nap. Rims: body dimmed to leave room for one highlight streak at 128°; `TableRim` warmed to 60% of the way to bright gold.
    - The table gets one gilt rule 0.3 cells in from its edge (`UiTheme.TableRule`, gold at 32%, `BoardArt.TableRule`), plain at the corners. The old table had no frame since G2, so this adds one rather than replacing a double.
    - Lanes dimmed (brass 55% → 42%). Sorting orders shifted down by three to fit the rule and the pattern; still all below zero.
    - The commented-out first version of `BoardView` at the top of the file was removed.
  - **Haze** (`SceneLighting`). Five puffs of `BoardArt.Haze` (fbm cloud with a soft edge) over the vault and arm pools, on the board layer at order −5, warm white at 3.5%. They loop slowly (46 s), turn slower still and swell ±25%. Reduced motion holds them at home; Lighting effects off hides them. Inspector: `hazeStrength`, `hazeDriftPeriod`, `hazeDrift`.
  - **Checked:** the view compiles against the 6000.6 DLLs. No core changes, so the tests are unchanged. The board was previewed in numpy (linear blending, approximate room lights) before and after, and the pattern, veins and haze were toned down once from that preview. The HUD can't be previewed here.
  - **Play Mode checklist:**
    - Docked panels (top bar, rail, tray, history strip) show one thin gilt line on their inner edge and no diamond; nothing flickers at 1366×768 or when the Game view is resized.
    - Floating cards (pause, setup, end, title settings, log, history hover) have a single hairline frame and small, dim fans.
    - The rail's active seat plate, headings and ROLL still read as the brightest gold on screen.
    - Board: the veining and sunburst are visible up close and do not fight the lit landings or the pieces; the stepped corners and the cracked length (south arm, west side) are there; the table rule sits inside the view.
    - Haze drifts in the pools; it holds still with Reduced motion and disappears with Lighting effects off. The room still looks finished with effects off.
    - The first board build does not hitch noticeably (the cross is now shaded with noise; it is built once per grid and cached).
