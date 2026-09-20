# Nona Royale — HUD Pass

> Location in repo: `docs/design/HUD_PASS.md` · Project copy: `claude/HUD_PASS.md`
> Status: **Closed, 2026-09-20.** All four increments in. Two of the six diagnosis items were wrong and are retracted in the log below; H4 shrank to one deletion because of it.
> Related: `GUI_PHASE.md` (the HUD this reworks), `VISUAL_PASS.md` (the board it sits on), `PRESENTATION.md`, ADR-0008

## Goal

Make the match HUD carry a turn instead of listing the game's data. The board is the game; everything else earns its pixels or loses them.

## The diagnosis (2026-09-20, from a 1920x1080 Play Mode capture)

The HUD is organised by **data category** — dice, operator, abilities, aim, squads, history — and every category owns permanent real estate whether or not it has anything to say. In the capture, three of the four tray sections read "nothing selected", and the history column shows an empty box and "R1".

Measured: `SquadRail` 290 + `HistoryStrip` 78 = **368 canvas units of width**, `ActionTray` 196 + `TurnStrip` + `TurnBanner` 44 = **about 308 of height**, reserved permanently. The board lands at roughly 710 square — 71% of screen height but only **37% of width**.

What the capture shows, in order of cost:

1. **Four things narrate one state.** Top bar "Move 5 + 5 — click a piece, then where it lands"; tray "Click one of your pieces, or its row on the left."; dice "Move a piece to spend these."; button "5 + 5 left".
2. **The player's seat does not outrank the CPUs.** Four identical blocks, twelve identical rows.
3. **Two competing primaries.** Gold MOVE FIRST floating over the tray band, gold Cast inside it. MOVE FIRST also reads as an instruction rather than a button.
4. **The keyboard legend is permanent chrome** — six shortcuts across the top, the loudest non-game text on screen, while the buttons already carry their own keys through `ModalCard.WithKey`.
5. **Status chips live in two places at once**, on the rail rows and floating over the board, at a saturation nothing else in a noir palette reaches.
6. **Eight health readouts float over the board at rest**, duplicating the rail.

## Decisions (2026-09-20, from pickers)

1. **The rail keeps your seat expanded and collapses the opponents to spines.** One line per CPU — colour, name, home count, three health pips — expanding while that CPU plays. Rejected: moving opponents to their felt tables, which reads spatially but puts HUD text on the play surface that V3 fought to keep clear.
2. **The tray becomes one contextual bar.** It shows the live step and nothing else. Rejected: keeping four sections and compressing them, which leaves the empty sections in place.
3. **The board goes quiet at rest.** Health on hover, on selection, and permanently once damaged. Status chips live on the board only and come off the rail rows.

## Rules

- **Stable geometry, contextual content.** A slot that has nothing to say renders nothing, but keeps its width, so the bar fills in rather than reflowing under the pointer.
- **The reserved band never changes size mid-turn.** `FrameCamera` fits the board inside what the HUD reserves, so a tray that grew on selection would resize the board while the player was aiming.
- **One statement per fact.** If the top bar says it, the tray does not.
- **The view computes nothing** (PRESENTATION §1). Readiness, legality and cost stay engine answers.

## Increments

| #  | Increment | What it delivers |
| -- | --------- | ---------------- |
| H1 | **The contextual bar** ✅ | `ActionTray` at 132 rather than 196, with the four section headings and all four empty-state strings gone. Slots keep their width and fill in: dice, the selected operator, its abilities, the aim and Cast. |
| H2 | **The rail** ✅ | Your seat in full rows; each CPU one line with colour, name, three health-tinted operator silhouettes and a home count, opening on hover and for its whole turn. 290 down to **220**. The status chips left the rail here rather than in H3, since the rows were being rewritten anyway. |
| H3 | **A quiet board** ✅ | `PieceHudLayer` shows health on hover, on selection and on damage only; a piece at full health says nothing. Board chips go through `StatusPalette.OnBoard`, off full chroma and brightness. Chips stay always-visible - they are transient and they are the point. The rail half landed in H2. |
| H4 | **The key legend goes** ✅ | Six shortcuts leave the top bar; every one of them was already printed on the control it drives. The history fold and the one-primary change were both dropped — see the retractions. |

## Log

- 2026-09-20 — **Pass opened.** Designer, on the match HUD: "turn it into a world-class functional, clean and modern powerhouse that still fits the game." Diagnosis above from the Play Mode capture; decisions 1-3 settled from pickers before any code.
- 2026-09-20 — **H1 in: the bar carries the turn.** Four labelled sections became four slots with no headings and no empty states; all four placeholder strings are gone, and so is the dice hint that repeated the top bar. An empty slot draws nothing but holds its width — reflowing would move controls out from under the pointer mid-turn, and changing height would resize the board through `FrameCamera` while the player was aiming. 196 down to 150, not the 132 first drafted: the chosen ability's `Description` was homeless once the abilities slot stopped explaining itself, so it sits above the aim line beside Cast, and that slot's 122 units set the number. Compiled clean.
- 2026-09-20 — **H2 in: the rail folds.**
  - **The cost was priced before it was paid.** A one-line spine drops six fields per operator across three CPU seats — 45 in all. Most of them are a second copy of the board: `PieceShape.For` is keyed on the operator's **name**, not the seat, so Kurbyn is a diamond in blue, green and violet alike, and piece size already carries health by design ("the tank reads as the tank without a bar over its head"). Identity, position and rough health are readable off the board. The status chips were drawn in both places. And three fields — the CPU personality, the seat energy, the state line — were already being truncated at 290, so folding gives them **more** room when a seat opens, not less.
  - **What it genuinely costs is the four-seat scan.** Reading every opponent in detail at once is gone; hover and the playing seat's auto-open buy it back one seat at a time. Recorded here because it is the one thing no amount of hover fixes, and the designer accepted it knowingly.
  - **The spine keeps the silhouettes, not dots.** Three `PieceShape` marks tinted by state — home fades out, a yard piece takes the waiting tint, a piece on the board runs from the seat colour toward `Danger` as it is hurt. A folded seat still answers who they have and how hurt.
  - **Folding never rebuilds.** Both forms are built and one is switched off, the same reasoning the hover wash already followed: a rebuild would destroy the very rect whose pointer-exit is owed, and the fold would stick. The relay sits on a wrapper holding both forms, so swapping them cannot pull the hover target out from under the pointer, and expanding grows the wrapper downward from a fixed top rather than under the cursor.
  - **220, not the 210 estimated.** At 210 the expanded rows could not hold an operator name — "Bouncer" at `FontBody` needs about 68 and the fixed columns left 68 exactly. The icon went 30 to 26 and the health column 64 to 52, which buys the name 84 at a 220-wide rail. The board gains 70 units of width rather than 80.
  - "ready to deploy" is now "ready": the row **is** the button, and it was the string the narrower rail cut.
  - **Play Mode checklist:**
    - The three CPU seats show one line each; your seat shows three full rows. Hovering a CPU line opens it; leaving folds it again, with no flicker at the boundary.
    - The seat whose turn it is stays open for the whole turn without hovering, and folds when the turn passes.
    - The spine's three marks are the same silhouettes as the pieces, dim for home, pale for the yard, reddening as one is hurt.
    - No operator name is cut in an expanded row.
    - Status chips appear on the board only. Nothing on the rail shows them.
- 2026-09-20 — **H3 in: the board goes quiet.**
  - **Health is on request or on damage.** Eight readouts over eight untouched pieces was most of the board's clutter, and every one repeated a number the rail already carried. `showHealth` is now `standing && (hurt || focused)`, where `hurt` reads `ShownHealth` rather than the engine's health so the label and the hit's number change together, the way MO2 already had it.
  - **Focus is driven every frame**, beside the `Visible` flag it sits next to. Selection changes in half a dozen places in the composition root; a readout wired to one of them would sit wrong until the next hit.
  - **Chips are muted, not hidden.** Health can wait to be asked for; a STUN or a MARKED cannot — they are transient and they are the reason to look. So they stay always-visible and lose about a fifth of their brightness instead: peaks go from 0.95 to 0.74, hues intact and still separable. `StatusPalette.OnBoard` mixes 22% toward `UiTheme.Text` and scales value to 0.78 — toward the warm off-white everything else on this board desaturates into, not toward grey.
  - **The tray's operator card keeps full-strength chips.** It shows one piece at a time, on a panel, which is where that palette was tuned.
  - **Play Mode checklist:**
    - A full-health piece shows no readout. Hovering it shows one; so does selecting it; both go away again.
    - A damaged piece keeps its readout with nothing hovered or selected.
    - Take a hit: the readout appears as the number lands, not a frame early or late.
    - Status chips are still on every piece that has one, and no longer the brightest thing on the board after the vault.
    - The tray's operator card chips are unchanged.
- 2026-09-20 — **H4 in, and two of the diagnosis items retracted.**
  - **The key legend is gone.** Six shortcuts, permanently right-aligned in the top bar, and every one of them already lives on the control it drives: `Space` and `E` are the turn button's own hint line, `1`–`3` are printed on the ability cards, `Enter` is on Cast, `Esc` is on MENU, and `L` is on the history strip's LOG button. The prompt takes the freed width, which is the right trade — it is the turn's one instruction and it is now the widest thing on the bar.
  - **Retracted: "two competing primaries".** `TurnButton` is already one state-driven primary — ROLL, MOVE FIRST, END TURN, with the key or the reason underneath — and Cast is cyan against its gold. That is ART_DIRECTION §8's own split: gold is the turn's ritual, cyan is the live step. Not a conflict, and nothing to fix.
  - **Retracted: "the history strip is dead space".** The empty-looking box at the top of it is the LOG button, which carries its own key; the "R1" below it is a real history chip. The strip is sparse at round one and fills with play, which is not the same failure as a tray section that is empty by construction. Folding it would have been a rewrite with no win.
  - **What that leaves of the original six:** the four narration strings (H1), the rail's flat hierarchy (H2), the duplicated chips and the eight floating readouts (H2 and H3), and the legend (H4). Two of six were me reading a screenshot instead of the code.
  - **Play Mode checklist:**
    - No key legend on the top bar; the prompt is centred and has room.
    - Every key still findable: ROLL and END TURN show theirs, the ability cards show 1-3, Cast shows Enter, MENU shows Esc, LOG shows L.
