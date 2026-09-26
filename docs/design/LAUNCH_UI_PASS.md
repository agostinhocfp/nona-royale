# Nona Royale — Launch UI pass (G6)

> Location in repo: `docs/design/LAUNCH_UI_PASS.md` · Project copy: `claude/LAUNCH_UI_PASS.md`
> Status: **Open, 2026-09-26.** G6a–G6e and G7a passed Play Mode and are committed (G7a `049edcd`). **G7b-1 (events in the player's words) written, waiting on Play Mode; G7b-2 (refusal wording) and G7b-3 (full rules on an ability card) follow. G8 (All In 1 Sprite Shader): the pack is committed (`f75fd43`); G8a and G8c are committed (`634406e`); the burn wasn't noticeable in Play Mode, and G8c's follow-up makes it readable and logs each run. G8b withdrawn; G8e needs another technique.** Open: G7 captures still owed (settings pages, glossary tab, trait and keyword cards, history hover card, doubles callout).
> Related: `GUI_PHASE.md` (E–J, G3–G5), `HUD_PASS.md` (H1–H4: the contextual tray, the folded rail, the quiet board — G6b builds on it and does not undo it), `MOBILE.md` (upright layout — every change here must keep M3/M6 intact), `ART_DIRECTION.md` §3 and §8, ADR-0008

## Verdict

The chrome is about 80% of a shipping UI. The frame, type and palette are right. What keeps it from launch is a handful of visible bugs, space spent in the wrong places (empty ability cards, a two-thirds-empty rail, tiny figures on a board whose yards take half its area), colour meaning slipping (red frames on every draft card, a lime HASTE chip), and dev-panel copy (instruction paragraphs on every screen).

## Flags

### P0 — visible bugs

1. **Blank chips.** The CPU style chip under BLUE/GREEN/VIOLET on setup is an empty gold box (the draft proves the styles exist: BANKER, BANKER, BRAWLER). The top slot of the history strip is also an empty box. Same failure shape twice; probably one cause in the kit.
2. **Comma decimals.** `SPD ×1,0`, `×1,5`, `speed ×1,0` on draft cards and the dossier. Culture-formatted floats on a Portuguese machine.
3. **Rail truncation.** `RED P…   home 2/3 …` on the active seat's header.
4. **Tag collisions on shared cells.** At the vault, `BALANCE` prints over `…LANCE`; three health labels crowd.
5. **History chip words clipped by the icon.** `UPKEEP` → `PKEEP`, `DEPLOY` → `EPLOY`. `ZD` is unreadable as a word.

### P1 — layout and information

6. **Ability cards are empty boxes.** Three ~370-unit cards, text only in the top-left corner, no effect line. Add the one-line effect (the dossier's copy), readiness on the frame rather than a word, and a selected state. (HUD_PASS H1 already shows the chosen ability's description above the aim line; G6b decides whether the resting cards carry it too.)
7. **The tray's operator block collapsed** to a shape and a red bar. No name, no health number, no statuses.
8. **The rail wastes two-thirds of its height,** and the seat order (VIOLET, RED, BLUE, GREEN) matches neither turn order nor the board. *Revised in G6b:* the empty height costs nothing — the board is fitted to the screen's height, and the rail's width is reserved either way — so filling it would only undo HUD_PASS H2's folding. The order is fixed; the height stays quiet.
9. **Draft cards are all framed in seat red at full strength.** Twelve red frames; red also means damage/reject. Rest = gold hairline, inspected = cyan, picked = seat colour.
10. **Pool shapes are drawn in seat red,** so they read as "RED owns these". Neutral ivory in the pool; seat colour only on a pick.
11. **Picked-by marker** is a ~10-unit diamond (Revú). Needs a seat chip; operators the picking seat already fielded should grey out.
12. **Tag chip palette.** HASTE is lime, off-palette; BALANCE/BURDEN/HOUSE tan; AURA dark. One mapping through `StatusPalette`.
13. **Row rhythm.** Ability rows spread to fill each card, so baselines differ card to card (Revú, Sanity, Lethe). Fixed pitch, top-aligned.
14. **Setup overuses cyan.** Table, four seats and squads all in the selected state; human and CPU seats look the same. Human = cyan fill, CPU = gold hairline plus style chip, empty = dim.
15. **BACK has DRAFT's size and weight** on setup. Demote to secondary.

### P1 — copy

16. **Instruction paragraphs on every screen** (four helper lines on setup, two on the draft header, a rules line in the draft footer), with widows ("table.", "one."). One short line per section at most, balanced wraps; rules move to tooltips or the Operators screen.
17. **The seed is a primary field.** Move it behind an ADVANCED disclosure; replays still need it.
18. **Draft header with one human:** "Choose a seat (1–4)" lets the human fill CPU squads. Noise unless it's hot-seat. *Decided 2026-09-26 (picker): own seat, plus an override.* A human picks only for their own seat by default; CPUs pick theirs; with two or more humans the seat chooser returns for them; a small toggle lets a player pick for CPU seats too (testing, setting up matchups).

### P2 — decisions, not bugs

19. **Board scale — withdrawn as written (2026-09-26).** The flag proposed shrinking the yard tables to enlarge the play space. That is wrong: the cross already spans the board's full width and height, the yards sit inside its corners, and the camera fits the board to the height between the top bar and the tray. Smaller yards free no room and nothing gets bigger. Cells are about 57 px at 1080p, so figures are not undersized there. The real levers, if it ever needs one: the tilted board camera (`BoardCamera.Tilted`, already built, about 1.45× the flat view's area; the default is TopDown), the figure's size within its cell, and the tray and top bar heights.
20. Title is flat; the board behind setup is tilted. Pick one for the menus. *Superseded 2026-09-26:* the designer asked for no board behind the menus at all ("it doesn't look premium"); see G6d.
21. Release build: drop "Development Build" and the "Prototype build · pieces and board drawn in code" footer. *2026-09-26: the "Prototype build" stamp stays for now (designer).*

## Plan

| # | Increment | Covers |
| --- | --- | --- |
| G6a | Bug sweep | 1–5 |
| G6b | Tray and rail | 6–8 |
| G6c | Draft and setup | 9–18 |
| — | Decided | 18: own seat, plus an override |

Each increment ends with a Play Mode check (desktop and upright) and a commit.

## Log

- 2026-09-26 — Audit written from screenshots.
- 2026-09-26 — **G6a written: the bug sweep (flags 1–5).** On HEAD `f287493`.
  - **1, blank chips — one cause, in `UiKit.Button`.** The caption was stretched with an 8-unit inset on all four sides, so a 26-unit chip had a 10-unit text box and the 28-unit LOG button a 12-unit one. A TMP label in Ellipsis mode whose first line does not fit its height draws nothing at all. The caption is now inset left and right only and takes the button's full height. Every short button benefits; tall ones are unchanged, since their text was centred either way.
  - **2, comma decimals.** New `UiKit.Multiplier(double)` formats with the invariant culture ("×1.5"). The four `{op.BaseSpeed:0.0}` sites (two draft cards, the draft detail, the dossier) use it. No other culture-sensitive number formatting was found in the view.
  - **3, rail header.** `SquadRail.SeatHeader` is two lines at 48 units: diamond, name, CPU tag and debt chip on the first; "home 2/3 · energy 12/12" on the second. The PLAYING word is gone: the gilt plate, the top bar and the turn pill already say it.
  - **4, tag collisions — two causes.** (a) Every seat's HOME is drawn at the one vault, but `Reposition` grouped pieces by `CellRef`, which carries the owner, so finished pieces of different seats were never fanned together. HOME cells now share one key. (b) Finished pieces show no readouts (`PieceHudLayer.SetRetired`): they are out of the fight, and their health and passives decide nothing. Separately, stacked status rows now step down a line each (`StackRowSpacing`), because a row is as wide as its words and one BALANCE tag is wider than the 68-unit label spacing.
  - **5, history chips.** The word has its own full-width line at the top; the silhouette sits left of the value beneath it. A cast with no damage or heal shows the energy it cost ("4e", cyan, summed from its `EnergySpent` events) instead of the ability's initials ("ZD"). `HistoryFeed.Initials` was removed as dead.
  - Files: `UiKit`, `SquadRail`, `PieceHudLayer`, `HistoryStrip`, `HistoryFeed`, `DraftScreen`, `OperatorDossier`, `MatchBootstrap`. No core change.
  - **Checked:** the view compiles against the 6000.6 DLLs with 0 warnings, verified live by breaking it on purpose. uGUI can't be previewed here; Play Mode is the check.
  - **Play Mode checklist:**
    - Setup: each CPU seat's chip reads its style (BANKER, BRAWLER…) and cycles on click.
    - History strip: the top button reads LOG (with the L key on desktop).
    - Draft cards, draft detail and the dossier read `×1.0` / `×1.5`: a point, not a comma.
    - Rail: the seat to play shows its full name and, under it, "home n/3 · energy n/12", nothing cut. Hover a CPU's spine: its header isn't cut either, style tag included.
    - Get two seats' pieces home: they fan at the vault instead of standing on one spot, and none shows a health label or tag.
    - Two pieces with statuses on one track cell: their tag rows sit on separate lines.
    - History chips: UPKEEP, DEPLOY, EFFECT and DEBT read in full on the standing strip and the upright band; a utility cast (Cryo Field, Deal Again) shows its cost as "4e" in cyan; a damaging cast still shows "-n".
    - Upright (phone or a portrait Game view): the history band's chips and the rail band still fit.
- 2026-09-26 — **G6a passed Play Mode.** Flag 19 withdrawn: shrinking the yards would not enlarge the board (see the flag).
- 2026-09-26 — **G6b written: the tray and the rail (flags 6–8).** On HEAD `327d226`.
  - **6, ability cards.** Wide, a card is the key and name, the meta, and the ability's `RulesText` line (two lines, ellipsis past that) — the same line the draft and the dossier print, never the flavour paragraph. Readiness moved onto the frame: a castable card has a faint cyan edge (55%), a chosen one keeps the cyan fill and double edge, one that can't be cast keeps the dimmed frame and puts the reason after its meta ("· ready in 2 turns", "· needs 5e, have 3"). The READY word is gone. The meta now uses `OperatorDossier.Meta` ("5e · r3 · cd 3"), so the tray, draft and dossier word it the same way. Upright cards drop the rules line; the aim line carries the armed ability's (MOBILE M3).
  - The Cast slot lost the flavour paragraph (four lines in a two-line box) and shows the aim in two lines over the button. `ActionTray.Height` is unchanged; the operator slot (120) now sets it.
  - **7, operator block.** The screenshot showed the card laid out at almost no width, its icon and health bar drawn under the first ability card and its name hidden. The cause wasn't found by reading: the card was a `Fixed` 300-unit child exactly like the dice box, which lays out correctly. The card is now a leaf slot holding the width, with the column stretched inside it, so the bar's answer comes from the slot's `LayoutElement` alone. **If the name and health still don't show between the dice and the cards, a Hierarchy look at `action_tray/content/operator` (its width and LayoutElement) is the next step.**
  - **8, rail order.** `SquadRail.InRailOrder`: turn order rotated to start at the first human seat. The match's list starts wherever the seed's first turn fell, which is what put VIOLET above RED. No human seat: unchanged.
  - Files: `ActionTray`, `SquadRail`. No core change.
  - **Checked:** the view compiles against the 6000.6 DLLs, 0 warnings. Play Mode is the check.
  - **Play Mode checklist:**
    - Select an operator: the tray shows its icon, name, where it is, health bar and number, statuses and trait chips between the dice and the cards, nothing under the cards.
    - Each card shows its rules line in two lines at most; long lines end in "…" without spilling past the card.
    - A castable card has a thin cyan edge; one on cooldown or short of energy is dimmed with the reason after its meta; the chosen card is cyan-filled with a double edge.
    - Choose a targeted ability: the Cast slot shows only the aim (two lines at most) and the Cast button.
    - The rail lists your seat first, then the others in turn order; start several matches and the order stays anchored on you.
    - Upright: the cards show name and meta only, and nothing overflows the block.
- 2026-09-26 — **G6b passed Play Mode** (designer: "much has been improved"). The operator card now shows in full, so the leaf slot did its job. Two captures (Lethe and Syla selected, in the yard) showed what was left, and **G6b's follow-up** fixes it in `ActionTray`:
  - **Cards came out unequal** — Nano Cell 150 wide beside a 600-wide Eris' Exploit, Syla's three all different. A card's preferred width is its longest line unwrapped, and the row lerps between preferred widths before it shares out the rest. Cards now declare no preferred width (`EqualShare`), so each gets the same share.
  - **Three slots always.** A two-ability kit (Lethe) leaves the third slot empty instead of stretching its cards across it, so a card keeps its place and width from one operator to the next (H1's stable geometry).
  - **The health number never showed.** Same failure as G6a's blank chips: an 18-unit body-text label in an 18-unit row, in Ellipsis mode, draws nothing. It is now Overflow. The other short rows were checked (rail heads, draft role/name/foot, pause footer, dossier role) and fit.
  - **"· out of play" on every card** of an operator in the yard. Operator-wide reasons (out of play, stunned) are no longer repeated per card; the operator card says it once ("waiting in the yard", the STUN tag), and now also says "in the home column, out of the fight" where it used to say "on the board".
  - **Dimmed cards kept bright keywords.** A card that can't be cast now fades as a whole (a `CanvasGroup` at 45% on its text), so the cyan cost and the coloured keywords fade with the plain words.
  - Noted, not changed: the operator card's health bar is `Lerp(Danger, seat colour)`, so RED's full bar reads as the danger colour. Worth a look when the bars get a proper palette.
  - **Play Mode checklist:**
    - Syla, Lethe, Fortuna in turn: the three card slots keep the same width and place; Lethe's third slot is empty.
    - The operator card shows the health number beside the bar ("8/8").
    - An operator in the yard: its cards are dimmed with no "out of play"; its card reads "waiting in the yard". A stunned operator: STUN tag, no per-card "stunned".
    - A dimmed card's cost and keywords are as faded as its words; a castable card is at full strength with the cyan edge.
    - An operator in its home column reads "in the home column, out of the fight".
- 2026-09-26 — **G6b's follow-up passed Play Mode.** Designer, on the empty third slot: "we agreed to show all abilities, even passives." They are shown, as the gold chips on the operator card (OPERATOR_GUIDE §5a, 2026-09-21); the card row holds castable abilities only. Kept as is.
- 2026-09-26 — **G6c written: the draft and setup (flags 9–18).** On HEAD `e829cd2` plus the uncommitted follow-up.
  - **9, card frames.** At rest every card has the gold hairline. The card being read (hovered, or tapped upright) takes a cyan edge, repainted in place (`ApplyFocusEdges`) because hover only rebuilds the detail panel. A card the picking seat already holds takes that seat's edge, on top of its faded IN SQUAD state. The picking seat's colour no longer frames all twelve.
  - **10, shapes.** Pool shapes and portrait pins are neutral ivory (`PoolShape` = `UiTheme.Text`); refused cards keep the grey.
  - **11, holders.** Seat chips with the seat's initial on its colour (`HolderChips`), 11 pt wide and 9 pt upright, replacing the 10-unit diamonds.
  - **12, tag palette.** The draft card's status chips are gone. A status colour means "this is on the piece now" (lime HASTE is also the board's haste trail), and the kit lines already name every passive and aura with its `passive` or `aura · r2` tag. `StatusPalette` is unchanged: in the match its hues are meaning, not decoration.
  - **13, row rhythm.** The cause: a row whose layout group force-expands its children reports itself as flexible, so the card shared its spare height between the ability lines. Every card row now pins `flexibleHeight` to 0; the kit is top-aligned and three-line and four-line cards share baselines.
  - **18, picking (decided: own seat, plus an override).** The pointer already never picked for CPU seats; what was wrong was the prompt. The seat-choosing hint and the number keys only appear with two or more controllable seats; with one, the hint reads "Click an operator to take it. Click a filled slot to clear it." **PICK FOR CPUS** (footer, off at the start of every draft) lets the pointer choose, pick for and clear CPU seats, and pauses the CPUs' own picking while it is on (`Controllable`, `TogglePickForCpus`). Turning it off hands the pointer back to the first human seat and the CPU seats back to their brains. The subtitle reads "Picking for BLUE (CPU)" while the pointer is on a CPU seat.
  - **16, draft copy.** The footer's rules note is gone (the override's button takes its place when there is a CPU seat); the rules belong on setup.
  - **14, setup seats.** Only a human seat takes the cyan selected state. A CPU seat keeps the resting frame, "CPU" in plain text and its style chip; an empty seat is an inset tile with dim text.
  - **15, BACK.** New `ModalCard.SecondaryChoice`: 280 × 42, body type, centred under the primary. Setup's BACK uses it; the other cards are untouched.
  - **16, setup copy.** One line per section wide (two upright): "Every seat for itself. First squad home wins." · "Click a seat to change who plays it. The chip sets a CPU's style." · "Everyone picks at once, 30 seconds. Empty slots fill at random." The draft's clock lengths now come from `DraftConfig.Default`, so a tuning change can't leave the card promising the old number.
  - **17, seed.** Behind an ADVANCED row (SHOW/HIDE, remembered for the session): the seed, SHUFFLE, and one line on what it fixes.
  - `PRESENTATION.md` §4.3 updated to match (card look, picking rule, seed).
  - Files: `DraftScreen`, `SetupScreen`, `ModalCard`, `PRESENTATION.md`. No core change.
  - **Checked:** the view compiles against the 6000.6 DLLs, 0 warnings, and the Unity EditMode tests compile against it. uGUI can't be previewed here; Play Mode is the check.
  - **Play Mode checklist:**
    - Draft, one human, three CPUs: every card has the gold hairline and an ivory shape; hovering a card turns its edge cyan and the previous one back; no red frames.
    - A card RED holds shows RED's edge, IN SQUAD, faded. Cards held by other seats show their initials as coloured chips, top right.
    - Sanity (four kit lines) and Nuetu (three) side by side: the lines sit at the same heights from the top; the extra space is at the bottom.
    - No coloured HASTE/BURDEN/BALANCE/HOUSE/AURA chips on the cards; the kit lines show the passives and auras.
    - The header hint has no "Choose a seat / 1–4"; the seat rows show no number key; CPU rows can't be clicked.
    - PICK FOR CPUS: OFF by default. ON: the CPUs stop picking, clicking a CPU row makes it the picker ("Picking for BLUE (CPU)"), cards pick for it, its slots clear. OFF again: the pointer returns to RED and the CPUs resume.
    - Two humans: the seat hint and number keys come back.
    - Setup: RED (human) cyan; CPU seats in the plain frame with a readable style chip; an empty seat dims. BACK is a smaller button under DRAFT. Every note is one line. ADVANCED shows/hides the seed and SHUFFLE.
    - Upright: the draft footer fits PICK CPUS; setup notes wrap to two lines without clipping; BACK fits.
- 2026-09-26 — **G6c passed Play Mode.**
- 2026-09-26 — **G6d written: the menu backdrop (flag 20).** Designer: remove the board from the title and setup screens; "it doesn't look premium". The "Prototype build" stamp stays for now.
  - **New `View/MenuBackdrop.cs`:** a full-canvas UI layer under everything (`HudRoot.BackdropLayer`, first child of the canvas, edge to edge rather than inset to the safe area). Obsidian base; a warm pool centred on the bottom edge (`DecoSprites.Glow`, GoldBright 10%); a half sunburst rising out of it (36 soft rays, every other at half strength, gold 7%, fading out before the top where the title's glow sits); a vignette (black 85% at the corners); one gilt hairline 28 units in (14 upright) with the corner fans. The sunburst and the vignette are rasterised once and cached. Static. Previewed in numpy before writing; concentric arcs were tried in the preview and dropped (they read as a radar).
  - **When:** `MatchBootstrap.Update` shows it whenever there is no match, so the title, setup, draft and guide sit on it; set directly too at Start, in `ShowTitle` and at the deal, so no frame shows the board. A card over a match (pause, NEW MATCH, results) keeps the match behind it. The empty room is still built underneath, because the first deal wants it; the backdrop is opaque over it.
  - **Title recomposed.** With no board to frame, the bands close up around the centre: the wordmark's bottom edge 56 above centre (70 upright), its glow behind it, the menu row's top 64 below (40 upright), bottom edge held so the armed QUIT's warning grows into the gap. The settings pages move the wordmark back to the top edge, clear of their centred card, gliding there (everything hangs off the centre so the slide needs no conversion; placed again if the screen height changes). The title's scrim is 0 — the backdrop is the scene.
  - **Scrims per opening.** `ModalCard` re-reads `ScrimAlpha` on every `Show`, so a card can differ over a match and over the backdrop. Setup: 0.72 over a match, 0.30 over the backdrop.
  - `TitleScreen.ReservedTop`/`ReservedBottom` still feed the title's camera framing; the board they framed is now covered, so they only keep the room's framing stable.
  - Files: `MenuBackdrop` (new; Unity makes its `.meta` on focus), `HudRoot`, `MatchBootstrap`, `ModalCard`, `SetupScreen`, `TitleScreen`. No core change.
  - **Checked:** the view compiles against the 6000.6 DLLs, 0 warnings. Play Mode is the check.
  - **Play Mode checklist:**
    - Launch: the title shows the dark room, the warm pool and rays from the bottom, the gilt frame, and no board, from the first frame.
    - The wordmark sits just above centre, the four buttons just below; arming QUIT shows "Leaves the game." above the row without moving it.
    - SETTINGS: the wordmark glides up to the top edge and the card sits clear of it; BACK glides it down again. Sound and Display pages the same.
    - PLAY: setup sits on the backdrop with a light scrim; the draft sits on it too; no board anywhere.
    - DEAL: the board and HUD appear, the backdrop is gone. Pause → NEW MATCH: setup shows the match behind it, darkened as before.
    - MAIN MENU from a match: the backdrop returns with no frame of board.
    - Upright: the frame hugs the screen at 14 units, the wordmark and the stacked menu both fit, the backdrop reaches under the camera cut-out.
- 2026-09-26 — **G6d passed Play Mode** (designer: "awesome"). Asked what the style is called: an Art Deco sunburst (sunray motif) in a low-key, vignetted noir light — "Deco noir".
- 2026-09-26 — **G6e written: grain, haze and a beam on the backdrop,** all in `MenuBackdrop`, so the gradients stop reading as digital. The asset packs were considered first: the MagusVFX and sound packs are for play, not a static backdrop; All In 1 Sprite Shader's Shine could have made the sweep, but the backdrop is UI under URP and the three effects are a few dozen lines in code, so nothing was bought for this.
  - **Grain:** a 128-texel white-noise tile (`BoardArt.Hash`, point-filtered, repeating) on a `RawImage` over everything but the frame, 1.5 canvas units a texel, alpha = noise³ × 6%. A UI layer can only lift a near-black, so the specks are cubed to keep the average lift near 1%; a flat 3.5% layer was tried in the preview and washed the blacks out. Re-dealt 12 times a second by jumping the tile's offset, like film.
  - **Haze:** three puffs of `BoardArt.Haze` over the pool (warm white at 6%), on the board lighting's loop — a 46 s Lissajous drift of 5% of their size, a slow turn and a ±25% breath (`SceneLighting.Drift`, G3). 60% size upright.
  - **Beam:** a soft wedge of GoldBright (4° spread, 6% peak) pivoting on the sunburst's origin. Every 20 s it sweeps from 75° one side to 75° the other over 7 s, eased, its light rising and falling with the sweep so neither end snaps. Dark for the rest of the cycle. It lights the haze as it passes, which is the point.
  - **Reduced motion:** the grain holds one frame, the haze sits at home at its base strength, the beam does not run. Unscaled time throughout.
  - Layer order, bottom up: base, pool, haze, rays, beam, vignette, grain, frame. Sizes follow the screen (`Place` runs again when the layout version or the canvas size changes).
  - Previewed in numpy before writing (grain level, beam strength).
  - Files: `MenuBackdrop`. No core change.
  - **Checked:** the view compiles against the 6000.6 DLLs, 0 warnings. Play Mode is the check.
  - **Play Mode checklist:**
    - Up close the backdrop has a fine, living grain; at arm's length the blacks still read black, not grey.
    - Haze drifts slowly in the warm pool; nothing about it pulses fast.
    - About every 20 s a soft beam sweeps across the sunburst from one side to the other and fades at both ends; between sweeps it is gone.
    - Settings → Reduced motion on: grain frozen, haze still, no beam. Off again: all three resume.
    - The wordmark and buttons are still the brightest things on screen; nothing distracts from PLAY.
    - Upright: the haze fits the screen, the beam reaches the top, the grain stays fine.
- 2026-09-26 — **G6e in Play Mode: the grain came out as static and was removed** (designer: keep it clean and minimalist unless it's barely noticeable). The cause is the colour space: the project renders in linear (ADR-0010), where a 6% white over near-black lands several times brighter than it reads in an sRGB preview. The numpy previews blended in sRGB, so every low-alpha layer here is stronger on screen than it looked in them; the haze is too, and is the first thing to halve if it reads as smudges. Removed rather than tuned down: the grain constants, texture, layer and re-deal are gone from `MenuBackdrop`; haze and beam stay. **Rule for future previews: blend in linear, or treat sRGB previews as a lower bound on brightness.**
- 2026-09-26 — **G7 opened: audit the remaining screens** (designer's pick over onboarding, accessibility and transitions): pause and its settings pages, results, the operator guide, the pop-up cards, the full log, and the in-match moments. Waiting on screenshots.
- 2026-09-26 — **G7 tooling: a dev win on Ctrl+Shift+Numpad 0,** so the results screen can be reached without playing a match out.
  - **Core:** `GameEngine.DevForceWin()` sends every operator on the current player's side HOME (both partners at a crossed table, via the new `WinConditions.SideSeats`), reporting each with `OperatorReachedHome`, then closes the turn through the same tail as a real end of turn (`CloseTurn`, extracted from `EndTurn`). The win is the ordinary win check's, so `GameWon` names the side exactly as a real finish would. It is not a command: not in the command set, no `Executed`, invisible to `ReplayRecorder` (a replay of a match ended this way stops at the cheat). Refused before the first turn and after the match. Compulsory rolling and movement are deliberately not checked.
  - **View:** `MatchBootstrap` reads the chord (either Ctrl, either Shift, Numpad 0) while no card is open and hands the engine's events to `Handle` like any command's, so the end screen, history and toasts take their normal path; a `[DEV]` line goes in the log. The key and `DevWin` are inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, compiled out of release builds.
  - **Tests:** `DevForceWinTests` (8): wins for the seat to play; reports every arrival and one `TurnEnded`; works mid-turn after a roll; leaves other seats where they were; wins for both partners at a crossed table; refused after the match and before the first turn; raises no `Executed`. Core total 931, all passing. The view compiles with and without `DEVELOPMENT_BUILD`.
  - **Play Mode checklist:** in a match, Ctrl+Shift+Numpad 0 walks the seat to play's pieces home and opens the results after the usual beat, naming that seat; with a card open it does nothing; in a crossed match it names both partners.
- 2026-09-26 — **The dev win passed** (a results screen was reached with it). **G7 audit** from seven captures at 2560×1440: the event log, an armed ability, the operator guide, the pause menu over a CPU turn, the results, a cast toast and a refusal toast.
  - **P0.** (1) The pause menu has no title: "Paused" in 34-pt Cinzel in a 44-unit box, Ellipsis mode, draws nothing — the same blank-line failure as G6a's chips and G6b's health number, now four times. (2) The top bar truncates a CPU seat: "GREEN (CPU · BANK…". (3) Every floating card (pause, results, guide) washes to grey toward its bottom edge.
  - **P1.** (4) The event log is raw engine text: "Kian 8 -> 9", "Red +1 energy (burned 0)", "rolled [1,1]", "Kurbyn deploys to Track[39]". (5) Refusals toast the engine's wording at the player: "a second roll needs doubles and a remaining roll in the budget". (6) The turn pill says "Space to roll" on a CPU's turn. (7) Space with the dice spent sends a roll and earns that refusal. (8) Results: rows in turn order (winner second), the seed in the subtitle and the rematch note, an explainer line under the table, knockouts in amber that reads as the winner's gold, and no word for how it went for the player. (9) Long rules lines are cut on the ability cards (Blind Spot, Vendetta end in "…").
  - **P2.** (10) The guide underlines every keyword in its own colour, which is busy on a reading page. (11) The pause card's "Esc resumes" footer is near-invisible and repeats RESUME's key. (12) The results screen shows no one: the winning squad's figures belong there once the art lands.
- 2026-09-26 — **G7a written: flags 1–3, 6–8, 11.**
  - **1, blank single lines, fixed at the source.** `UiKit.Label` gives every single-line label vertical slack: top and bottom margins of −0.3 × the type size, so a line may overhang its box instead of vanishing. Width still truncates with an ellipsis and a centred line stays centred. The one top-aligned single-line label (the history chip's word) is now midline. The per-site Overflow fixes from G6a and G6b stay.
  - **2.** The top bar reads "GREEN CPU to play"; the style is on the rail's header.
  - **3, the grey wash: the U4 sheen was upside down.** `DecoSprites.BuildSheen` treated texture row 0 as the top; it is the bottom, so every floating card has been lit from below since U4. Turned the right way up, and `UiTheme.PanelSheen` cut from 10% to 4% white, because in linear colour space 10% over a near-black card is a grey wash, not a whisper.
  - **6.** `TurnBanner.Show` takes `cpu`: a CPU's pill says "hold Space to hurry".
  - **7.** Space only sends a roll when one is on offer (the opening roll, or a doubles re-roll with nothing owed — `RollOnOffer`, the same engine answers the tray reads) or while the board is busy and the roll may be due when it settles. E is unchanged: its refusal ("you must use your roll") is the useful kind.
  - **8, results.** Rows in standing order (winning side, then home, knockouts, fewest lost, table order). The subtitle is "VICTORY · Round 3" or "DEFEAT · Round 3" when every human seat is on one side, and just the round otherwise (watch mode, hot-seat across sides); the seed is gone from it. The explainer line under the table is gone. Knockouts are plain text. The rematch note reads "Rematch: same table, same squads." (or "squads drawn again").
  - **11.** The pause footer is gone.
  - Files: `UiKit`, `HistoryStrip`, `DecoSprites`, `UiTheme`, `TurnStrip`, `TurnBanner`, `EndScreen`, `PauseMenu`, `MatchBootstrap`. No core change.
  - **Checked:** the view compiles against the 6000.6 DLLs, 0 warnings; the Unity EditMode tests compile against it.
  - **Play Mode checklist:**
    - Pause: PAUSED shows above the round line; no footer under MAIN MENU.
    - Pause, results, guide, setup and the draft's panels: lit faintly from the top, no grey toward the bottom.
    - A CPU's turn: the top bar reads "GREEN CPU to play" in full; the pill says "hold Space to hurry".
    - Dice spent: Space does nothing and no refusal toast appears; E still ends the turn. The opening roll and a doubles re-roll still work on Space, including pressed while the dice are still tumbling.
    - Results (Ctrl+Shift+Numpad 0 on your own turn): VICTORY under the banner, your row first; on a CPU's turn, DEFEAT with the winner first. No seed, no explainer line; knockouts in plain text.
    - Look for any single-line text that now sits a little off its box vertically — the margin change touches every label, so this is the thing to watch.
- 2026-09-26 — **G7b proposed: words for the player.** Flags 4, 5 and 9 share one gap: the game has no player-facing text for its events and refusals, only the engine's `ToString` and rejection strings. Proposal: a core `EventText` (next to `RulesText`, tested the same way) that writes each event and each refusal in plain language with seat-coloured names; the full log rebuilt from the history feed's items grouped by round, newest first, in those words; the toasts and history cards using the same lines; and a hover or tap on an ability card showing its whole rules line. Core strings change, so the tests that pin refusal wording change with them.
- 2026-09-26 — **G7a passed Play Mode** and was committed as `049edcd`.
- 2026-09-26 — **G8 opened: All In 1 Sprite Shader** (Seaside Studios, 4.68, bought by the designer). Imported to `Assets/Plugins/AllIn1SpriteShader/`.
  - **Trimmed** (designer's pick): `Demo/` (9.4 MB) and `2DRendererDemo.unitypackage` (5.3 MB) deleted; shaders, scripts, textures and `Documentation.pdf` kept. 17 MB, 106 files. The repo is private, so the pack is committed (licence terms in `docs/art/PROVENANCE.md`, new "Third-party packages" section). PNG, PSD and PDF go through LFS per `.gitattributes`.
  - **What fits us.** Pieces and board are SpriteRenderers under 2D lights with the default sprite material (no material is set anywhere in the view). `AllIn1SpriteShader/AllIn1Urp2dRenderer` has Universal2D and NormalsRendering passes, so it keeps them lit; the plain `AllIn1SpriteShader` is unlit and would drop them out of the lighting. `AllIn1SpriteShader/AllIn1SpriteShaderUiMask` is the uGUI variant (RectMask2D clip, `UNITY_UI_CLIP_RECT`). Keywords and properties we need: `HITEFFECT_ON` / `_HitEffectColor`, `_HitEffectGlow`, `_HitEffectBlend`; `FADE_ON` / `_FadeAmount` (−0.1..1), `_FadeBurnColor`, `_FadeBurnWidth`, `_FadeBurnGlow`, `_FadeTex`; `OUTBASE_ON` / `_OutlineColor`, `_OutlineWidth`, `_OutlineAlpha`, `_OutlineGlow`; `SHINE_ON` / `_ShineLocation`, `_ShineWidth`, `_ShineRotate`, `_ShineGlow`; `GLOW_ON`, `GREYSCALE_ON`, `FLICKER_ON`.
  - **Build trap: stripped keywords.** Every effect is `shader_feature_local`, so a build only carries the variants some material asset uses. A keyword switched on from code works in the editor and silently does nothing in a player. So each effect is a material asset under `Resources/Fx/` with its keyword already on, and code instantiates it (instances keep keywords). The `ShaderFx` wrapper loads by name and falls back to today's code-drawn effect when a material is missing.
  - **Withdrawn: "player builds will fail."** `AllIn1Shader.cs` line 3 has `using UnityEditor;` outside `#if UNITY_EDITOR` in a runtime assembly, and I first called it a build breaker. It is not: the pack's runtime scripts compile with no `UnityEditor.dll` referenced (the UnityEngine modules declare the namespace), checked with `dotnet build` against the 6000.6 player DLLs. No vendor code is patched.
  - **Plan, one increment each, each with a compile check, log entry and commit:** G8a `ShaderFx` and the Fx materials; G8b hit flash (`_HitEffectBlend`, a true flash to white — today's is a tint, which can only darken); G8c burn-dissolve knockout under the existing shatter; G8d seat-colour outline on the selected piece; G8e wordmark shine on the title; G8f powered cells and cast tells.
- 2026-09-26 — **G8 plan corrected after reading the code.**
  - **G8b withdrawn.** The log said today's hit flash "is a tint, which can only darken". Wrong: a painted figure gets a white silhouette over it at 75% (`_flashOverlay`), and a rig flashes each part the same way (`RigView.Flash`). Only the code-drawn pawn lerps its colour toward white, which brightens it too. There is nothing for `_HitEffectBlend` to fix.
  - **G8e can't use the pack as planned.** The wordmark is TextMeshPro text (`TitleScreen.Wordmark`), and TMP draws with its own SDF shaders. A sprite shader can't draw it. A shine needs another technique, such as a soft band masked by the text. It stays parked until it's picked up.
  - The designer's picks: G8a and G8c next; the materials are written as YAML `.mat` files, not made in the editor.
- 2026-09-26 — **G8a written: `ShaderFx` and the first Fx material.** On HEAD `f75fd43`.
  - **`View/ShaderFx.cs` (new):** loads a material by name from `Resources/Art/Fx/`, caches it, and hands out instances (`Instance`), which keep their keywords. If the material is missing, or its shader reports `isSupported == false`, it answers null and logs one warning for that name. The caller then keeps its code-drawn effect. The cache clears on play without a domain reload. It also holds the property IDs and the `_FadeAmount` ends (−0.1 means nothing dissolved and no edge; 1 means gone).
  - **`Art/Resources/Art/Fx/BurnLit.mat` (new, hand-written YAML, serializedVersion 8):** `AllIn1Urp2dRenderer` (GUID `202ffec9…`) with `FADE_ON` in `m_ValidKeywords`. `_FadeTex` is the pack's `seamlessNoise`, `_FadeBurnTex` is its `white`, burn width 0.06, transition 0.06, glow 1.6, alpha blend (5/10), no depth write. The folder and the material carry their own `.meta` files, so the GUIDs are fixed from the first commit.
  - **Checked while reading:** the pack's first SubShader is limited to URP 12–17.2, but the second requires URP 17.3 or later and we're on 17.6.0, so that one runs. The fragment pass multiplies by the vertex colour, so `SpriteRenderer.color` and the evasion alpha still apply. `SpriteRenderer` sets `_MainTex` itself, whatever the material.
- 2026-09-26 — **G8c written: the burn-dissolve knockout.**
  - **`View/FxBurn.cs` (new):** at the shatter, a copy of the figure as it stood burns away over the shatter's own 0.45 s (scaled by motion speed). It has a glowing edge in the seat colour, under the shards, which are unchanged. The copy clones each drawn part (sprite, colour, flips, order, mask interaction, world pose) under its own `SortingGroup` at the piece's layer and order. All parts share one material instance, destroyed with the copy. It runs on scaled time times `MotionSettings.Rate`, so pause freezes it. A dissolve isn't movement, so Reduced motion keeps it.
  - **Why a copy.** The piece still hides at the shatter and comes back at `Reappear` exactly as before. It never carries an effect material into its next life, and the presentation queue's wait (`IsKnockingOut`, the shard clock) is unchanged, because the burn lasts exactly as long.
  - **What burns** (`OperatorPiece.BurnParts`): every part of a rig (the new `RigView.Parts`). A painted figure's body. The code-drawn pawn's body and outline. A hit flash's silhouette if it is still on. The pin, health bar and ground markings disappear at once, as before. The rig's fold (LB5c) still runs first, so a standing rig burns folded.
  - **Fallback:** no `BurnLit` material, or a shader that can't run, means `FxBurn.Spawn` makes nothing and the knockout is exactly G7a's.
  - Files: `ShaderFx` (new), `FxBurn` (new), `OperatorPiece`, `RigView`, `Art/Resources/Art/Fx.meta`, `Fx/BurnLit.mat` and its `.meta`. Unity writes the two new scripts' `.meta` files on focus. No core change.
  - **Checked:** the view (all 146 files) compiles against the 6000.6 UnityEngine modules and the project's URP, TMP and UI reference assemblies, with 0 warnings and 0 errors. I verified this live by breaking the build on purpose. It also compiles with `DEVELOPMENT_BUILD`. `view-compile.cmd` can't check this increment until Unity has run once, because its response file doesn't list the two new files yet. I can't preview the shader here; Play Mode is the check.
  - **Play Mode checklist:**
    - On focus, the console shows no `[ShaderFx]` warning and no shader errors. `Fx/BurnLit` shows the All In 1 inspector with Fade on. If Unity rewrote the `.mat` on import, commit its version.
    - Knock out a painted operator: its figure burns away from a thin glowing edge in its seat colour while the shards fly. It is gone by the time they land and comes back in its yard with the usual pop, with no leftover edge and no tint.
    - Knock out the code-drawn pawn (an operator without art): the pawn and its outline burn together.
    - Knock out a rigged operator standing on the track: it folds, then its parts burn together as one figure. No part drops out early, and none burns in a different place.
    - The burning figure is lit like the living one. There's no jump in brightness at the first frame. If there is, `_LitAmount` or the glow is the knob.
    - Pause during a knockout: the burn freezes with the shards. Fast motion: it keeps pace with them. Reduced motion: shards shorter, burn still there.
    - An evasive (faded) operator knocked out: the copy starts at its faded alpha.
    - Remove or rename `BurnLit.mat` and knock out an operator: one warning, and the knockout is the old shatter alone. Put it back.
    - Build a player (Development Build is fine) and repeat one knockout. The burn must show there too. This is the keyword-stripping check.
- 2026-09-26 — **G8a–c committed (`634406e`). In Play Mode the burn wasn't noticeable** (designer: "not sure I noticed anything new").
  - **What the log says.** `Logs/Editor.log` has no `[ShaderFx]` warning, so `BurnLit` loaded and its shader runs. It can't say whether a burn spawned, because nothing logged one. Every operator in that match was a rig (the `[Rig]` lines: kurbyn, revu, kian, sanity, syla, javi, nuetu). So each knockout burned a folded figure about 60 px tall. It lasted 0.45 s, the same as the shards, with nine shards flying out over it. The first 10% of that time was spent between `_FadeAmount` −0.1 and 0, where nothing shows. The effect probably ran, and at that size and speed it couldn't be seen.
- 2026-09-26 — **G8c's follow-up: a burn you can see, and proof that it ran.**
  - **Longer than the shards.** The burn lasts 0.8 s (`OperatorPiece.BurnSeconds`, shortened by Reduced motion's tween like everything else). The knockout waits for whichever is longer, the shards or the burn (`_shardClock`), so the piece returns to its yard only once the burn is done. Each knockout gets 0.35 s longer. That's the cost, and a knockout is the biggest beat in a turn.
  - **Eased in, from 0.** `_FadeAmount` runs 0 → 1 on t², so the figure holds while the shards cover it, then burns faster as they clear. No frames are lost below 0.
  - **A hotter edge.** The edge colour is the seat colour 35% of the way to white. In `BurnLit.mat`: burn width 0.06 → 0.1, transition 0.06 → 0.04, glow 1.6 → 2.2.
  - **A log line per knockout** (editor and development builds only): `[FxBurn] <operator>: <n> parts over 0.80 s`.
  - Files: `OperatorPiece`, `FxBurn`, `Fx/BurnLit.mat`. No core change.
  - **Checked:** the view compiles against the 6000.6 modules with 0 warnings, both with and without `DEVELOPMENT_BUILD`.
  - **Play Mode checklist:**
    - Every knockout prints one `[FxBurn]` line in the console. If none appears, the burn never spawned. Report that; it's a different bug from "too subtle".
    - At normal speed, after the shards clear, the folded figure visibly eats away from a bright seat-coloured edge. It's gone before the piece pops back in its yard.
    - To see it in detail, pause right as a hit lands (Ctrl+Shift+P) and step frame by frame with Ctrl+Alt+P. There should be no black silhouette, no pink, and no part burning somewhere other than where it stood.
    - A knockout now feels a beat longer. If it drags, `BurnSeconds` is the knob. Above 0.45 s the burn still outlasts the shards; below about 0.6 s the ease-in leaves little burn visible after they clear.
- 2026-09-26 — **Dev tooling: Ctrl+Shift+Numpad 9 knocks out everyone on the track** (designer's request and pick: every seat's operators on the outer track, back to their yards). Written to test G8c's burn on demand.
  - **Core:** `GameEngine.DevKnockOutTrack()` sends every operator on the outer track (`PathMap.IsOnOuterTrack`) through the ordinary `Neutralize` path. It uses the new `GameEngine.DevCause` ("dev") and no killer. So each one goes to its yard at full health (the game's respawn), with statuses and cooldowns cleared. A mark still pays out and a riding charge still learns its death cell. There is no bounty and no credit, but the match stats count the loss, so a match tested this way shows those losses on its results screen. Operators in a home column or already home are out of the fight and are left alone. The turn is not ended. Like `DevForceWin` it is not a command: no `Executed`, invisible to a replay. It is refused before the first turn, after the match, and with nobody on the track.
  - **View:** `MatchBootstrap` reads the chord (either Ctrl, either Shift, Numpad 9) where it reads the dev win, while nothing is paused. It hands the events to `Handle`, so the shatter, the burn, the sound, the voice lines, the history, the refusal toast and the return to the yard all take their normal path. A `[DEV]` line goes in the log. The two chords now share `DevChord(KeyCode)`. Everything is inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. I checked that a release compile doesn't reference `DevKnockOutTrack` and a development compile does.
  - **Tests:** `DevKnockOutTrackTests` (8):
    - everyone on the track goes to the yard at full health, reported with `DevCause`;
    - yard and home-column operators are untouched;
    - no bounty and no credit, but the loss is counted;
    - the turn goes on;
    - it works mid-turn after a roll;
    - it is refused with nobody on the track;
    - it is refused before the first turn and after the match;
    - it raises no `Executed`.
    Core total 939, all passing.
  - Files: `GameEngine`, `GameEvents` (the cause list's doc comment), `MatchBootstrap`, `DevKnockOutTrackTests` (new; Unity writes its `.meta`). `FxBurn` and `OperatorPiece` are unchanged in this step.
  - **Checked:** the view compiles against the fresh core with 0 warnings, both with and without `DEVELOPMENT_BUILD`.
  - **Play Mode checklist:**
    - In a match with pieces out, Ctrl+Shift+Numpad 9 knocks out every piece on the track at once. Each one burns (one `[FxBurn]` console line each), the shards fly, and each piece reappears in its own yard. The turn pill and the dice don't change.
    - The history shows each knockout, and no seat collects a bounty.
    - With nobody on the track, pressing it shows a refusal toast and nothing else happens.
    - With the pause menu or a card open, it does nothing.
- 2026-09-26 — **G7b go (designer's pick over G8d and G8f). Split into three increments:** G7b-1 events in the player's words (flag 4); G7b-2 refusals in the player's words (flag 5), which rewrites the engine's reason strings and the tests that pin them; G7b-3 the whole rules line on hover or tap of an ability card (flag 9).
- 2026-09-26 — **G7b-1 written: every event in the player's words, in the log, the history cards and the toasts.**
  - **Core: `Text/EventText.cs` (new).** One line per event, as a `RulesLine`, for all 41 event types. It never uses the events' own `ToString`, which stays as the developer's text for the dev panel and the tests that read it. There are no board coordinates and no progress numbers. For example:
    - "Kian moves 5 cells", not "Kian 8 -> 13";
    - "RED gains 2 energy", not "Red +2 energy (burned 0)";
    - "Kurbyn deploys", not "Kurbyn deploys to Track[39]".

    Each damage cause is named: from Bleed, in a collision, from a table, a critical hit, and so on. Knockouts use the guide's word ("is neutralized", "neutralized outright" for an execute). Seats are written as the screen writes them ("RED"). `TurnBegan` and `TurnEnded` have no line; the log shows them as headings (`EventText.TurnHeading`). Refusals get one shared wording: "Can't: …" (`EventText.Refusal`).
  - **`RunKind.Named` (new) in `RulesLine`:** a name carries its seat (`RulesRun.Seat`), so the view draws every operator and seat name in that seat's colour. Two operators with the same name on different seats (two Fortunas, for example) now read apart. `RulesMarkup` draws it with `UiTheme.Readable` of the seat colour, the way the rail and the toasts already tint seat names.
  - **Tests: `EventTextTests` (12).**
    - Every `IGameEvent` type in the core has words (checked by reflection), so a new event without them fails.
    - Eight whole bot matches (2 and 4 seats) push every event they emit through the writer. None of the output may be unwritten, contain "->", "Track[" or "burned 0", or be empty.
    - Wording tests cover names as seat runs, seats, cells, dice, damage, knockouts, statuses, turn headings, a side's win and refusals.
    - Core total 951, all passing. `RulesMarkupTests` gains a named-run test (Unity test assembly; it compiles here, and Unity runs it).
  - **View: the event log is rebuilt** (`LogPanel` rewritten, new `View/MatchLog.cs`).
    - It reads `MatchLog`, which is fed where the history strip is (`ShowHistory`, when a batch settles), so a line never runs ahead of the board.
    - Newest first, grouped by round under a gold "ROUND n" heading. Inside a round, the newest turn comes first. Each turn reads top to bottom under "RED's turn" in the seat's colour. The turn in play is at full strength, older turns are dimmed, and refusals are tinted.
    - Sixteen turns are shown, forty-eight kept.
    - A CPU's refusals stay out, as they do from the toasts.
    - The raw engine lines, and the `[DEV]`, `[CPU]` and `[clock]` notes, stay in `_log` for the dev panel only.
  - **History cards and toasts:**
    - `HistoryFeed.Describe` writes each card line from `EventText`. The hand-written move and energy lines, and the fallback to `ToString`, are gone.
    - Seat names on cards, dividers and toasts are upper-case (`EventText.SeatName`).
    - A refusal toast uses the log's wording.
  - Files: `RulesLine`, `EventText` (new), `EventTextTests` (new), `RulesMarkup`, `RulesMarkupTests`, `MatchLog` (new), `LogPanel`, `HistoryFeed`, `HistoryStrip`, `EventToasts`, `MatchBootstrap`. Unity writes the three new files' `.meta` files.
  - **Checked:** the view compiles against the fresh core, with and without `DEVELOPMENT_BUILD`, 0 warnings. The Unity edit-mode tests compile against it. A 4-seat bot match was read through end to end: the lines read as plain English and names disambiguate by colour.
  - **Play Mode checklist:**
    - L opens the log. At the top is "ROUND n", then the current seat's turn in its colour, then its lines as they happened. Earlier turns sit below, dimmer, and earlier rounds below those.
    - No line in the log shows "->", "Track[", "burned 0" or a bracketed roll like "[1,1]". Rolls read "rolled 3 and 5" or "rolled double 1s: doubles, roll again".
    - Operator names are in their seat's colour everywhere: log, history hover card, toasts. Two operators with the same name on different seats show different colours.
    - Hover a history chip: the card's lines are the same words as the log's, with values bright and statuses in their board colours.
    - Make an illegal move (end the turn before rolling, say): the toast and the log both read "Can't: …" in the refusal colour. A CPU's refusals appear in neither.
    - Start a new match: the log is empty. Upright: the log still fills the board area and scrolls.
    - Look for text that overlaps vertically in the log. Its headings opt out of the single-line slack (G7a), so this is the thing to watch.
