# Nona Royale — Core Gameplay Pass

> Location in repo: `docs/design/CORE_GAMEPLAY.md` · Project copy: `claude/CORE_GAMEPLAY.md`
> Status: **Open, 2026-09-30.** CG1 and CG2 in; the designer's list is still being written.
> Related: `HUD_PASS.md` (the tray's stable geometry, H1), `PRESENTATION.md` §4 (board-first input), `CAST_ONBOARDING.md` (CO3, hold a card to read it), `MOBILE.md` (the upright tray)

## Goal

The designer's notes from playing the match, taken one at a time. This pass changes how the match plays and reads, not how it looks.

## Increments

| # | What | Status |
|---|---|---|
| CG1 | The tray shows an operator on other seats' turns | In, 2026-09-30 |
| CG2 | Clicking any seat's operator shows its kit | In, 2026-09-30 |

### CG1 — The tray stays up off-turn

**Designer (2026-09-30):** "Operator and Ability trays should still be showing even when it's not the player's turn. Perfect time to look at one's own abilities."

The tray never hid; it went blank, because it only ever drew the selected operator and the selection is cleared when the turn passes. Now:

- **The tray draws the host's `ShownOperator`:** the selection, else the operator being looked at (CG2), else, on a CPU's turn, one of the player's own (the first human seat's operator in play, else its first not yet home).
- **When the turn passes, the player's selected operator stays in view** instead of vanishing, read-only while the CPUs play.
- **On the player's next turn it is still shown.** Arming one of its cards selects it (`ToggleAbility` promotes the look to a selection), so the tray works without a click on the piece first. A board click never selects it implicitly, so clicking a landing to move another operator still works as before.
- **Hot seat:** when the turn passes to another human, a look at the previous human's operator is dropped, so the next player starts on their own board. A look at a CPU's operator stays.

### CG2 — Look at anyone's kit

**Designer (2026-09-30):** "Clicking on other player's ops should show that operator's abilities."

- **A click on a piece the player cannot command shows it in the tray**: an enemy's at any time, the player's own during a CPU's turn. A second click on it puts it away. On the player's own turn, looking at an enemy lets go of their selection (the tray shows one operator, and a selection behind it would keep drawing moves nobody sees).
- **A landing still wins.** With nothing selected, clicking an enemy that one of your operators can land on is a collision move, as before; only a piece nobody can land on is looked at.
- **Clicks look even while the board is busy or a CPU is playing.** Commands still wait; a look commands nothing, so it needs no wait.
- **The squad rail does the same:** a row or chip the player cannot command shows that operator on a click. The dossier icon inside a row still opens the dossier.
- **Hover lifts any piece** while nothing is armed, since a click on any piece now does something. With an ability armed it lifts only your own pieces and legal targets, as before.

**Read-only cards.** An operator the player cannot command right now is drawn with its own seat's state:

- Cooldowns, stun and out-of-play are its own, as the engine reports them.
- **Energy is its seat's**, not the seat to play's ("needs 5e, have 3" is that seat's pool), and dice are not checked at all: `CheckAbility` weighs both against the current player, which is wrong for anyone else.
- A card its seat could cast is drawn at full strength with no cyan edge and is not a button. A tap or a hold opens the peek, as on any card that cannot be cast (CO3); on a mouse the hover peek works too.
- The operator card names the seat in its colour ("RED · on the board"), since the hero portrait carries no seat colour.

## Play Mode checks

- [ ] Solo against CPUs: after ending your turn, your last operator stays in the tray through every CPU turn; with nothing ever selected, one of yours appears on the first CPU turn.
- [ ] During a CPU turn, clicking any piece (yours or a CPU's) swaps the tray to it; a second click puts it away; nothing moves or casts.
- [ ] Your turn, nothing selected: click an enemy you cannot land on → its kit, read-only. Click an enemy you can land on → the move, as before.
- [ ] Your turn, own operator selected: click an enemy → its kit, your selection cleared. Click your operator again → selected, cards live.
- [ ] Back on your turn with your operator still shown: pressing one of its ready cards arms it (and selects the operator).
- [ ] Read-only cards: cooldown counts are right; the energy line uses that seat's pool; hold and tap open the peek on touch.
- [ ] Hot seat with two humans: the second player does not start on the first player's operator.
- [ ] Upright (phone): the same, in the stacked tray.

## Log

- 2026-09-30 — **Opened, CG1 and CG2 in.** New on the host: `ShownOperator`, `CanCommand` and `ViewOperator` (`IControlPanelHost`), backed by `_viewedOperator` in `MatchBootstrap`. `ActionTray` draws the shown operator and renders it read-only when it is not the player's to command; `SquadRail` rows and chips the player cannot command show their operator on a click. Compiles; not yet played.
