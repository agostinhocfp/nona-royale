# Nona Royale — HUD Pass

> Location in repo: `docs/design/HUD_PASS.md` · Project copy: `claude/HUD_PASS.md`
> Status: **Open, 2026-09-20.** Opened the day the visual pass closed. H1 and H2 are in; H3 and H4 remain.
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
| H3 | **A quiet board** | `PieceHudLayer` shows health on hover, on selection and on damage only, and the board's status chips come down to a saturation the palette can hold. The rail half of this landed in H2. |
| H4 | **One primary, one instruction** | The redundant strings go, the keyboard legend leaves the top bar, the history folds into it, and one primary slot carries the live step's action. |

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
