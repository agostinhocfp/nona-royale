# Nona Royale — Launch UI pass (G6)

> Location in repo: `docs/design/LAUNCH_UI_PASS.md` · Project copy: `claude/LAUNCH_UI_PASS.md`
> Status: **Open, 2026-09-26.** Audit from four desktop screenshots (title, setup, draft, in-match at round 36). **G6a (bug sweep) written and compile-checked, waiting on Play Mode.** G6b and G6c next; two decisions open (18, 19).
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
8. **The rail wastes two-thirds of its height,** and the seat order (VIOLET, RED, BLUE, GREEN) matches neither turn order nor the board.
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
18. **Draft header with one human:** "Choose a seat (1–4)" lets the human fill CPU squads. Noise unless it's hot-seat.

### P2 — decisions, not bugs

19. **Board scale.** Figures stand ~50 units tall at 1080p; the four yards take about half the board. `TableDiameter` is view-only, so shrinking the yards and refitting the camera would enlarge the play space without touching locked geometry.
20. Title is flat; the board behind setup is tilted. Pick one for the menus.
21. Release build: drop "Development Build" and the "Prototype build · pieces and board drawn in code" footer.

## Plan

| # | Increment | Covers |
| --- | --- | --- |
| G6a | Bug sweep | 1–5 |
| G6b | Tray and rail | 6–8 |
| G6c | Draft and setup | 9–18 |
| — | Decisions first | 18 (hot-seat or not), 19 (board scale) |

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
