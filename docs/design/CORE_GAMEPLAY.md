# Nona Royale — Core Gameplay Pass

> Location in repo: `docs/design/CORE_GAMEPLAY.md` · Project copy: `claude/CORE_GAMEPLAY.md`
> Status: **Open, 2026-09-30.** CG1–CG10 in; the designer's list is still being written.
> Related: `HUD_PASS.md` (the tray's stable geometry, H1), `PRESENTATION.md` §4 (board-first input), `CAST_ONBOARDING.md` (CO3, hold a card to read it), `MOBILE.md` (the upright tray)

## Goal

The designer's notes from playing the match, taken one at a time. This pass changes how the match plays and reads, not how it looks.

## Increments

| # | What | Status |
|---|---|---|
| CG1 | The tray shows an operator on other seats' turns | In, 2026-09-30 |
| CG2 | Clicking any seat's operator shows its kit | In, 2026-09-30 |
| CG3 | The match keeps running in the background | In for desktop, 2026-09-30; mobile needs match resume (open) |
| CG4 | A double click or double tap on a legal target casts | In, 2026-10-01 |
| CG5 | Tab and Shift+Tab step through the operators that can act | In, 2026-10-01 |
| CG6 | Hold F1 for every key | In, 2026-10-01 |
| CG7 | Zoom and pan the board: pinch on touch, the wheel on a desktop | In, 2026-10-01 |
| CG8 | Drone Strike's patch pulses slowly | In, 2026-10-01 |
| CG9 | Stun is drawn on the piece, not tagged | In, 2026-10-02 |
| CG10 | Killzone's cells run with lava | In, 2026-10-02 |

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

### CG3 — The match keeps running in the background

**Designer (2026-09-30):** "The match should keep running while the app is on the background."

- **Desktop and the Editor:** the Player setting *Run In Background* was off, and off means Unity stops its whole loop when the window loses focus, so the CPUs froze mid-turn the moment the player looked elsewhere. `MatchBootstrap.Start` now sets `Application.runInBackground = true`. Nothing in the game paused on focus loss itself: the only focus handler re-applies the cursor. Music and sound keep playing too.
- **Phones: not possible as asked.** iOS and Android suspend a backgrounded app's whole process; no setting keeps a Unity game's loop running there, and the OS may kill the process outright. What a phone can have instead is **resume**: save the match on `OnApplicationPause(true)` and restore it on launch. The pieces exist, a match is its seed and its command list (`Core/Replay`), so a save is small and a restore is a replay. Not built; open below.

### CG4 — Double click to cast

**Designer (2026-10-01):** "While an ability is selected, a double click or double tap on an enemy piece casts the ability." Widened at the designer's pick to **any legal target**, enemy or ally, so a heal or a pull is one gesture too.

- **The first click aims as before** (`ToggleTarget`). The second click on the same piece within 0.4 s (`DoubleClickSeconds`, unscaled) casts through `Host.Cast`, the same intent as CAST and Enter, so the engine's checks and refusals are unchanged.
- **The second click re-aims before casting.** When the piece was already the target, the first click let go of it; the double click still ends targeted and cast.
- **Any other click while aiming cancels a pending double click**, so click target, click elsewhere, click target does not cast.
- **Cell abilities are unchanged:** click to aim, then CAST or Enter. An ability that needs a target and a cell casts only once both are set.
- **Touch:** a tap arrives as a mouse click (Unity's touch-to-mouse simulation), so a double tap goes through the same path.
- **PRESENTATION §4 still holds.** Select-then-commit was about seeing the reach before spending; the first click shows the rings and the aim, and the second commits.

### CG5 — Tab through the operators

**Designer (2026-10-01):** picked from Claude's QoL list, on Tab rather than the proposed Q.

- **Tab selects the next operator of the seat to play that can act; Shift+Tab the previous.** "Can act" means a landing for the dice in hand, or an ability castable at some legal aim (`TurnOptions.IsCastable`). Yard pieces are left out, since deploying is one click on the pulse, and so are operators that are home.
- **Squad-rail order**, starting after the selected operator and wrapping. With nobody able to act, Tab does nothing; with only the selected one able to act, it stays selected.
- It goes through `ToggleOperator`, like a click, so an armed ability is let go. It waits while the board is busy, as 1–3 do.
- **The dev panel moved from Tab to F3** (dev builds only). The settings row's key hint says F3.

### CG6 — Hold F1 for the keys

- **`KeyHelpCard`**: a card listing every shortcut a player can press, up while F1 is held and gone on release. It takes no input: the pointer passes through and other keys still work.
- **Sections by screen:** Match (on the match or its pause menu), Draft (on the draft), and Anywhere (M, Shift+M, F1). Dev keys are left out.
- **Its own canvas at sorting order 1000**, because the draft brings itself to the front every frame and would bury a sibling-order overlay.
- Never opened on a touch screen with no keyboard.

### CG7 — Zoom and pan

**Designer (2026-10-01):** "Pinch-zoom and pan on phones", widened at the designer's pick to the mouse wheel on desktop too.

- **`BoardZoom`** narrows the view inside the fit `FrameCamera` solves: the field of view on the tilted camera, the orthographic size on the flat one. The framing hands it the fitted pose (`SetFit`); at zoom 1 it writes that pose back unchanged. Up to 2.5×.
- **The pan is clamped to the fitted picture,** so zooming out to 1 is also the reset. There is no reset gesture to learn. The pinch and the wheel keep the board point under the fingers or the cursor in place, so the wheel alone gets the player anywhere.
- **It runs before `CameraNudge`** (execution order −100) and hands it the zoomed pose as its base each frame, so a hit's shake shakes the view on screen.
- **Desktop:** the mouse wheel zooms toward the cursor, except over the HUD, where the wheel stays with the log and the panels.
- **Touch: taps count on release** (`TouchGestures`). A board tap is cancelled when a second finger joins, when the finger travels more than about a tenth of an inch, or when it landed on the HUD. Without this, the first finger of a pinch would select, move or aim. Two fingers pinch and drag; one finger never pans. Holding a finger to hurry a CPU now needs exactly one finger.
- **Every match opens on the whole board,** and the menus never zoom.
- Labels and the piece HUD are placed from the camera in `LateUpdate`, after the zoom, so they follow it. They keep their screen size, so they do not grow with the board.

### CG8 — Drone Strike's patch breathes

**Designer (2026-10-01):** "Make Drone Strike's patch pulse slowly."

- **`DeviceLayer`:** a beacon's area squares breathe around their resting alpha of 0.16, ±60% (0.06 to 0.26), one breath every 2.8 s, through `LightPulse.Breath` on unscaled time. The target ring and dot stay steady, so the anchor cell stays readable.
- **Beacons only.** Zones keep their steady areas, so a coming strike and a standing zone still look different (ADR-0007). `PaintCell` is Drone Strike's alone, so in practice this is Drone Strike's patch.
- **Reduced motion holds it still** at 0.16, set from `MatchBootstrap` beside the lighting's flag.

### CG9 — Stun on the piece

**Designer (2026-10-02):** "Remove the stun tag on operators during matches. Effective and seamless visual cues is the path. Just like the haste effect, it works."

- **`StunHalo`** (new, attached beside `HasteTrail` on every piece): three small yellow five-point stars circling the crown of the head, one lap every 1.8 s, each spinning slowly.
- **Depth from the draw order:** the orbit is a flat ellipse. A star on the near half draws in front of the figure (order 5), one on the far half behind it (order 2), smaller and dimmer.
- **`StatusPalette.IsDrawnOnPiece`** now includes Stun, so the board's tag row drops it. The tray's operator card still names it, as it still names Evasion and Haste.
- Hidden in the yard, on a shattered piece, and when the stun expires. The status comes from `ActiveStatusesOn`, through `OperatorPiece.Refresh(stunned:)`. Reduced motion holds the three stars still on the near side.

### CG10 — Killzone's lava

**Designer (2026-10-02):** "Add a subtle lava effect to Nuetu's Killzone."

- **`LavaTexture`** (new): 32 frames of 64×64 tileable lava, drawn in code once on first use. Domain-warped, ridged value noise gives molten veins under a dark crust. Every layer is periodic and drifts a whole period per 8 s loop, so it loops with no seam. Edges fade out, and alpha follows heat, so the crust lets the board show through.
- **`DeviceLayer`:** each covered cell of a blast zone gets two stacked lava frames that cross-fade, at order −1, under the seat tint. Each cell starts at its own point in the loop and is turned and flipped by its own hash, so neighbours are not stamped copies. Drawn with the unlit sprite material where it loads, so it glows the same under every light.
- **Armed vs lingering kept apart** (ADR-0007): lava at 0.6 alpha while armed, 0.34 once it only lingers. The seat tint over lava thins to 0.12 / 0.07, so it colours the lava instead of hiding it; the ring still says whose zone it is.
- **Tables stay plain.** `CellEffectSnapshot.IsTable` (new, from `StopsMovers`) tells Fortuna's table, a trap, from a blast zone. It names a kind of device, not an outcome, so PRESENTATION §2 holds. Killzone is the only blast zone today.
- Reduced motion holds the lava still.

## Play Mode checks

- [ ] Solo against CPUs: after ending your turn, your last operator stays in the tray through every CPU turn; with nothing ever selected, one of yours appears on the first CPU turn.
- [ ] During a CPU turn, clicking any piece (yours or a CPU's) swaps the tray to it; a second click puts it away; nothing moves or casts.
- [ ] Your turn, nothing selected: click an enemy you cannot land on → its kit, read-only. Click an enemy you can land on → the move, as before.
- [ ] Your turn, own operator selected: click an enemy → its kit, your selection cleared. Click your operator again → selected, cards live.
- [ ] Back on your turn with your operator still shown: pressing one of its ready cards arms it (and selects the operator).
- [ ] Read-only cards: cooldown counts are right; the energy line uses that seat's pool; hold and tap open the peek on touch.
- [ ] Hot seat with two humans: the second player does not start on the first player's operator.
- [ ] Upright (phone): the same, in the stacked tray.
- [ ] CG4: select a damage ability and double-click an amber-ringed enemy: it casts at once. Single-click it: it is only aimed, and CAST or Enter casts. With the enemy already targeted, double-click it again: it still casts.
- [ ] CG4: an ally-targeted ability (a heal, Velvet Rope's pull): a double click on the ally casts.
- [ ] CG4: click the target, click empty board, click the target within half a second: no cast. A double click on a piece that is not a legal target does nothing new.
- [ ] CG4 on a phone or the Device Simulator: a double tap on a target casts; a single tap aims.
- [ ] CG5: with dice in hand, Tab steps through the operators with landings or castable abilities, in rail order, and wraps; Shift+Tab goes back. A yard piece and a home piece are skipped. During a CPU turn, Tab does nothing.
- [ ] CG5: F3 toggles the dev panel in the Editor; Tab no longer does.
- [ ] CG6: hold F1 on the title, the draft, a match and the pause menu: the card shows the right sections, sits above everything, and closes on release. Clicks pass through it.
- [ ] CG7 desktop: the wheel zooms toward the cursor up to 2.5×, wheeling back out returns to the fitted board exactly, and over the log the wheel scrolls the log. Clicks, hover and the landing labels still line up while zoomed, under both the tilted and the flat camera. A big hit's shake still plays.
- [ ] CG7 touch (Device Simulator, then a phone): pinch zooms about the fingers, two-finger drag pans, a pinch never selects or moves anything, a single tap still selects, moves and aims, a tap that slides is ignored, and a double tap still casts. Holding one finger on a CPU turn hurries it; pinching does not.
- [ ] CG7: NEW MATCH and REMATCH open on the whole board; the title is never zoomed.
- [ ] CG8: cast Drone Strike: its patch breathes slowly (about 3 s a breath), the ring and dot stay steady, a zone's area (Killzone) does not pulse, and Reduced motion stops the breathing.
- [ ] CG9: stun an enemy (Killzone going off, Dargin Pulse): yellow stars circle its head, passing in front of and behind it, and there is no STUN tag under it. The tray card still says it is stunned. The stars go when the stun ends, and in the yard. Reduced motion: still stars. Check a chip piece and a rendered figure.
- [ ] CG10: cast Killzone: its cells glow with slow lava under a faint seat tint, with the seat ring on top; the cells do not pulse in step. After it goes off the lava is dimmer. Fortuna's table keeps its plain area. Move and aim highlights still draw over the lava. Reduced motion: still lava. Note any hitch on the first cast (the frames build then).
- [ ] CG3: start a match against CPUs, end your turn, switch to another window: the CPUs keep playing (Editor and a Windows build).

## Open

- [ ] **Match resume on mobile (CG3):** save the seed and command list when the app is backgrounded, restore by replay on the next launch, and offer "Resume match" on the title screen.

## Log

- 2026-10-02 — **CG9 and CG10 in: stun on the piece, lava in Killzone.** New: `View/StunHalo.cs`, `View/LavaTexture.cs`. Changed: `StatusPalette.IsDrawnOnPiece` (Stun), `OperatorPiece` (`StunHalo`, `CrownHeight`, `Refresh(stunned:)`), `MatchBootstrap` (passes the stun), `DeviceLayer` (lava cells, thinned tint over lava), `CellEffectSnapshot.IsTable` and `DeferredCellEffects.Snapshot` (core). Tests: `FortunaTableTests.TheBoardIsToldItIsATable`, and an `IsTable` check on the zone snapshot in `TurnStateMachineTests`. **Checked:** core 1005 passing (cloud runner); the view compiles against the Unity 6000.6 DLLs with and without `DEVELOPMENT_BUILD`; the lava algorithm was rendered to a still in Python for a look. Play Mode pending.
- 2026-10-01 — **Off `DEVELOPMENT_BUILD`.** Unity 6.6 deprecated the define (warning UAC0009 at `MatchBootstrap.cs(460)`). All six uses now read `Debug.isDebugBuild` at runtime, which is true in the editor and in development builds: `MatchBootstrap` (release forces the dev panel off, F3, the Ctrl+Shift+Numpad 0/9 chords, and `DevChord`/`DevWin`/`DevKnockOut` no longer wrapped), `FxBurn` (the `[FxBurn]` log line) and `ISettingsHost` (the dev panel row). **Changed guarantee:** release builds now contain the dev chord code, unreachable; G7 and G8c's "compiled out of release builds" no longer holds. Not compiled against the editor DLLs; the editor recompile is the check (no UAC0009, F3 and both chords still work in Play Mode).
- 2026-10-01 — **CG8 in: Drone Strike's patch breathes.** `DeviceLayer` keeps the beacon area renderers and drives their alpha in `Update`; `Spawn` returns its renderer; `Reduced` property set from `MatchBootstrap`. The view compiles against the Unity 6000.6 DLLs with and without `DEVELOPMENT_BUILD`. Play Mode pending.
- 2026-10-01 — **CG5–CG7 in**, from Claude's QoL list (the designer took 1, 3 and 8; 2, a second E press, was dropped once it turned out the engine already refuses End Turn while a move is owed). New: `View/BoardZoom.cs`, `View/TouchGestures.cs`, `View/KeyHelpCard.cs`. `MatchBootstrap`: `CycleOperator` and `CanAct`, `HandleKeyHelp`, the zoom's binding, fit and resets, taps on release on touch, the wheel, one-finger hurry, and the dev panel on F3. `ISettingsHost`: the dev panel row's hint. PRESENTATION §4.1 and MOBILE M4 updated. **Checked:** the view compiles against the Unity 6000.6 DLLs with and without `DEVELOPMENT_BUILD`, 0 warnings. Play Mode pending, and touch needs the Device Simulator or a phone.
- 2026-10-01 — **CG4 in: double click to cast** on any legal target. `MatchBootstrap` only: `DoubleClickSeconds` (0.4 s), `_lastTargetClick` and its time, `IsSecondClickOn`, and the second-click branch in `AimAt`. PRESENTATION §4.1 gains the line. Not compiled against the editor DLLs; Play Mode pending.
- 2026-09-30 — **CG3 in for desktop**: `Application.runInBackground = true` at start. Mobile resume recorded as an open item.
- 2026-09-30 — **Opened, CG1 and CG2 in.** New on the host: `ShownOperator`, `CanCommand` and `ViewOperator` (`IControlPanelHost`), backed by `_viewedOperator` in `MatchBootstrap`. `ActionTray` draws the shown operator and renders it read-only when it is not the player's to command; `SquadRail` rows and chips the player cannot command show their operator on a click. Compiles; not yet played.
