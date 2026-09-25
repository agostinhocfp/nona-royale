# Nona Royale — Presentation

> Location in repo: `docs/design/PRESENTATION.md`
> Status: **Living.** The view layer's rules — what it may do, what it must show, and how it behaves.
> Related: ADR-0004 (core architecture), `docs/design/COMBAT_SYSTEMS.md` (the rules being displayed), `docs/art/ART_DIRECTION.md` (what it looks like), `docs/art/ART_PIPELINE.md` (how art is made)

## Why this document exists

`COMBAT_SYSTEMS` owns the rules. `ART_DIRECTION` owns the look. `ART_PIPELINE` owns production. **Nothing owned what the view is allowed to do**, and view decisions were accumulating with nowhere to live.

That gap is how "it's only a preview, just compute it here" gets in. This document closes it.

It is not about aesthetics. It is about the constraints that keep the presentation layer honest about a game whose rules live somewhere else.

---

## 1. The view computes nothing

**It renders state and sends commands. That is the whole contract** (ADR-0004).

The dangerous case is not writing state — that boundary is obvious and enforced by the engine's API. It is the view _deriving_ a value the core could have told it.

> **Rule: if a number appears on screen and the core can produce it, the core produces it.**

A worked example, because it nearly went the other way. The move preview needs to know where an operator would land. The view already knows the dice roll and the operator's base speed, so it could do the multiplication itself — no core change, less code.

It would also be wrong, because distance depends on status modifiers and enemy auras the view cannot see. The preview would disagree with the rules **exactly when a slow or an aura is in play**, which is the moment a player most relies on it. So `GameEngine.PreviewLandings()` shares `Move`'s arithmetic and the view displays what it returns.

Same reasoning for status badges. The view receives `StatusApplied` and `StatusExpired` and could maintain badge state from them with no core change. But passives are granted at match start without an event, and bleed stacks are consumed at upkeep without a `StatusExpired` — so an inferred badge layer drifts. `StatusRegistry.ActiveKinds` exists instead.

**A board that lies is worse than a board that shows less.**

---

## 2. What must be visible, and when

**Always, without asking:**

|                               | Why                                                                                                                            |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Health, per operator          | Every combat decision is a threshold question                                                                                  |
| Active statuses, per operator | A status that cannot be seen may as well not apply. Most are tags; Evasion fades the piece and Hastened trails lime streaks (2026-09-24), because both sit on the same pieces all match |
| Why health changed            | Upkeep damage lands in a phase where nothing else moves; a bar dropping with no cause is the board declining to explain itself |
| Whose turn, and their energy  | The pool is shared, so it is a squad-level decision                                                                            |
| Each seat's debt              | It grows every turn it is owed and is what Sadist will hit for, so it decides who chases Revú; Roman numerals beside the pool, never on a piece (`COMBAT_SYSTEMS.md` §3.3) |
| Which operators are deployed  | Occupancy is the number the design keeps fighting for                                                                          |
| Safe cells                    | The only cells carrying a rule a player must see without asking                                                                |

**On demand, because showing it always would be noise:**

|                                | Trigger                                                        |
| ------------------------------ | -------------------------------------------------------------- |
| Where a move would land        | After rolling                                                  |
| Which cells an ability reaches | When an ability is selected                                    |
| Which cells an aura covers     | When its holder is hovered or selected: a faint lane (Catalyst's wake, Bouncer's drag) |
| Whether an ability can be cast | Always in the tray, from the engine, with the reason it cannot |
| What just happened, in words   | The event log, always present but scannable                    |

**Never shown:** the outcome of an action before it is committed. Landings are shown; whether a landing is contested is not. Showing a player the result of a fight before they choose it removes the choice.

**The hardest case is the one with no visible agent.** A bleed or mark tick resolves at upkeep, when nothing moves and nobody acted — so an operator loses health, and sometimes vanishes to its yard, with nothing on screen accounting for it. Every damage and neutralize event therefore carries its cause, and over-time damage is drawn differently from a hit. A collision or a cast needs no such label: the player watched it happen.

---

## 3. Placement is not animated as movement

Pieces walk the track cell by cell. **Pulls, swaps and bounce-backs do not.**

All three arrive as `OperatorMoved`, and all three are _placement_ rather than movement (COMBAT_SYSTEMS §7.4, §7.2) — none triggers anything along the way. Animating them as a walk would show a journey the rules say never happened, and would teach a player to expect collisions that cannot occur.

The implementation test is simple: a move reporting equal or lower progress is settled directly; only forward travel is walked.

**Walking one cell at a time is load-bearing, not decorative.** It is the only code anywhere that enumerates the cells between two progress values, which makes it the only thing that can expose a discontinuous layout — and it is how the arm-tip gap in the old 48-cell board was caught, after four amendments of simulation had missed it (ADR-0002 Amendment 6). Do not optimise it into a lerp.

**A bounced move shows the cell it contested.** `OperatorMoved` carries the attempted landing beside the final one (`AttemptedTo`, `Bounced`). The piece walks to the contested cell, rests there briefly, and then settles back to the bounce cell. The walk is movement; the step back is placement, so it settles like a pull rather than walking. _Closed 2026-09-15; until then the event carried only the final progress, and the collision was legible in the log but not on the board._

Since MOTION.md increment MO2 the walk is a **hop per cell** with a small squash on landing (a glide under Reduced motion). It still visits every cell; only the drawing changed.

### 3.1 Sequencing

_Added 2026-09-16 (MOTION.md, increments MO1 and MO2)._ The engine answers a command at once. The board catches up **one action after another**, through the presentation queue:

1. **Dice**: the tumble over the vault, the landing on the engine's faces, the flight to the tray. The tray shows the new faces only when the dice arrive.
2. **Cast tell**: a cyan sweep on the caster, then a line to the target or a drop onto the cell. It reads the accepted command, so a CPU's aim shows the same way.
3. **Walks**: forward travel only (§3), all of the batch's walks together.
4. **Rises**: deployed operators snap onto their cell and stand up.
5. **Hits**: numbers, flashes, misses and blocks, at the cell where each happened. The bar and the label change with the number (they show what the event reports, not the engine's current value). Big hits get a short hit-stop and a camera nudge.
6. **Knockouts**: the burst, the shatter in the seat colour, a stronger stop and nudge.
7. **Settle**: pieces reposition (a shattered piece reappears seated with a pop), marks and devices redraw, and the top bar, turn button, history, toasts and end screen catch up.

Rules that keep it honest:

- **Nothing is invented.** Every step shows an event or the command that caused it. A step with nothing to show is skipped.
- **Commands wait while the board is busy.** Board clicks, Enter and the ability keys wait. Deploy, move and cast requests are dropped. Space and E are kept for 0.4 s and sent when the board is free. Esc, right-click and selection always work. The CPU driver waits too.
- **The settle always runs.** An instant batch (the opening deal, CPU Instant) skips the cosmetic steps of anything still playing but never its settle, so the history never loses a batch. No step can hold the board for more than 6 s.
- **Clocks.** Gameplay animation runs on scaled time, times the animation speed and the CPU hurry (Space). Pause freezes it; HUD pulses keep unscaled time. A hit-stop never touches a paused clock, and the pause never resumes into a slowed one.
- **Reduced motion** removes the hop, squash, idle sway, hit-stop and nudge, and shortens holds and tweens.
- **Beats.** The queue announces `DiceRolled`, `DiceLanded`, `CastTell`, `Walk`, `Step` (each hop), `Rise`, `Hit`, `Knockout` and `Settle` for audio (Stage 5).

---

## 4. Commitment needs a look first

Actions that spend a scarce resource get a **select, then commit** pattern rather than firing on one click.

Abilities: selecting highlights the cells the ability reaches; a second action casts. Reach turned out to be the binding constraint on the entire combat layer (ADR-0002 Amendment 4), so committing energy without seeing range was asking players to estimate the number the game is most sensitive to.

The tray also greys out what cannot be cast, with the reason — cooling down, not enough energy, stunned. That readiness comes from the engine, per §1; before it existed, a player learned an ability was on cooldown by pressing it and reading the rejection.

The cost is one extra click per ability use, in a match already running past its length budget. **Watch whether it drags.**

Deploy and move fire on one click. Neither spends energy, and both are already previewed.

### 4.1 The board is the primary control

_Added 2026-09-15, GUI phase increment E._ A stranger's first instinct is to click a piece, so the board answers clicks and the panel becomes the second way to do the same things. Both go through the same intents, so a click and a button can never disagree.

- **Click one of your pieces** to select it. Its landings are drawn alone, each labelled with the pips it spends. A second click lets go.
- **Click a landing** to move there. With several dice arriving on the same cell, the fewest pips win: the same result for less is never worse.
- **Click a pulsing yard piece** to deploy it. The pulse comes from `GameEngine.CanDeploy`, the same check the command runs.
- **With an ability selected, a click aims it.** A cell ability snaps to the nearest legal cell. A target ability takes an amber-ringed piece, and the rings come from `LegalTargetsFor`. Clicking your own non-target piece switches the selection.
- **Right-click or Esc steps back:** first the aim, then the ability, then the piece.
- **Esc with nothing left to step back from opens the pause menu** (increment H): resume, new match, settings, main menu. The MENU button on the top bar opens it too. While it is open the clock stops and the board ignores input. Once the match is over, Esc brings back the results instead.
- **Keys:** Space rolls, E ends the turn, 1–3 pick an ability, Enter casts.
- **A landing beats a piece only for the selected operator.** With nothing selected, a piece wins, so clicking your own piece never moves a different operator whose landing shares its cell.
- **Only clickable pieces lift under the pointer**: your own, and legal targets. A lift that promised nothing would teach the player to click at random.
- **The selection belongs to the seat that made it** and is dropped when the turn passes.

### 4.2 The in-match layout

_Added 2026-09-15, GUI phase increment F._ The board sits in the rectangle the HUD leaves free, and each edge has one owner:

| Edge   | Owner                      | Shows                                                                                                           |
| ------ | -------------------------- | --------------------------------------------------------------------------------------------------------------- |
| Top    | Top bar (`TurnStrip`)      | Seat, energy pips against the cap, round, what the turn is waiting for, key legend                              |
| Left   | Squad rail (`SquadRail`)   | Every seat's squad: waiting, ready to deploy, on board or home; health; statuses. The current seat's rows are clickable |
| Bottom | Action tray (`ActionTray`) | Dice, Roll, End turn, the selected operator, its abilities with cost, range and when each is ready, the aim, Cast |
| Right  | History strip (`HistoryStrip`) | One chip per action, newest on top: who acted (the piece's shape), what kind, one number. Hover for the full card. Turns divided by seat colour and round |

The dev panel (`ControlPanel`) takes the left edge instead of the rail while Tab is on. It is a debugging tool: reseed and pip buttons.

**Over the board, not beside it** (increment F2):

- **Turn button** (`TurnButton`). One button at the board's bottom-right corner always names the next step: ROLL, then MOVE FIRST (or DEPLOY FIRST, when a held 6 has nothing else to go to) with the dice left, then END TURN. Since 2026-09-25 ROLL counts down the 15 s roll clock on its hint, and a turn with nothing left but END TURN ends itself after a beat (`TurnPacer`; inspector toggles `rollClock` and `autoEndTurn` on `MatchBootstrap`). It breathes when pressing it is that step. There is a single place to look, as in a card game's end-turn button.
- **Turn pill** (`TurnBanner`). A slim line under the top bar at the start of every turn, "RED's turn · round 3 · Space to roll", which fades after the roll. It is the hot-seat hand-over cue. It started as a large centred card, which was too big.
- **Toasts** (`EventToasts`). Up to three short lines under the top bar, for casts, hits, knockouts, upkeep effects and refused commands. Upkeep damage has no visible agent (§2), so the screen has to say it without the player looking for it.
- **Full log** (`LogPanel`). An overlay beside the strip, opened with L or the strip's LOG button. It takes no width from the board.

**One chip per action, not per event.** A cast is an energy spend, several hits and a status. The player did one thing, so the strip shows one chip and the card lists the rest. The engine does not report which ability was cast, so the chip's name comes from the command the view itself sent. That is bookkeeping about its own action, not a rule.

**"Ready in N" counts your own turns to the one the ability returns on.** On the turn it is cast, a cooldown 2 ability reads 3. That answers the question a player plans with, and it comes from `GameEngine.TurnsUntilReady`, not from the view.

### 4.3 Match flow

_Added 2026-09-16, GUI phase increment I. Draft screen added 2026-09-16, increment DR2. CPU seats added 2026-09-16, increments BOT2–BOT3._

- **Play opens the title screen** (increment J): the NONA ROYALE wordmark over the empty room, on a light scrim so the tables and the vault read through, with PLAY, SETTINGS and QUIT. It is the only screen that quits the app, and QUIT asks twice. The inspector's Skip Setup deals straight into a match.
- **PLAY opens the setup screen**: seats (any two to four of the four colours; each tile cycles EMPTY → HUMAN → CPU, and a CPU tile has a chip that cycles its style: BRAWLER, RUNNER, BANKER), squads (ALL PICK, SNAKE, RANDOM or ALPHA THREE; since DR2), the seed (shown, with SHUFFLE), and DEAL, which reads DRAFT for the two drafted modes. BACK returns to the match, or to the title when there is none.
- **The drafted modes open the draft screen** (DR2, `DRAFT.md`). It is a full-canvas screen over a dark scrim.
  - **Layout:** the nine operators as a 3×3 grid of cards on the left. Each card shows the shape, name, role, health, speed, passive and aura tags, and the three abilities with cost, reach and cooldown; a missing ability is shown as not yet written. Small seat diamonds on a card mark the seats that already hold that operator. On the right: each seat's three slots, and a detail panel with the full ability descriptions of the last card hovered. At the top: the title, a status line and the clock; SNAKE adds a pick-order strip.
  - **Refused cards** fade and carry the core's reason (IN SQUAD, SQUAD FULL, NOT YOUR PICK).
  - **ALL PICK:** a seat row (or keys 1–4) chooses who is picking, and a seat that fills up passes the pointer on. Clicking a filled slot clears it. RANDOM, FILL & START, and START (Enter, once every slot is full) are available. At zero the table holds for a beat, then deals.
  - **SNAKE:** cards pick for the seat on the clock. RANDOM, UNDO (Backspace), RANDOM REST, and START (Enter, once complete).
  - **CPU seats pick on their own.** In ALL PICK, one CPU pick lands about every 1.5 s, taking turns across the CPU seats. In SNAKE, a CPU picks 0.8 s into its turn. Their seat rows read "CPU · STYLE", can't be chosen as the picker, and their slots can't be cleared. UNDO is refused after a CPU's pick. FILL & START and RANDOM REST let the CPUs choose their own remaining picks.
  - **BACK (Esc)** with any pick made asks first, and stops the clock while it asks. Leaving returns to setup with the same choices, and the match on the table (if any) is untouched until a draft finishes.
- **MAIN MENU** in the pause menu (asks twice) and on the end screen returns to the title and removes the match. The in-match HUD lives on its own canvas layer and is hidden there.
- **Display settings are remembered** between sessions (PlayerPrefs): health labels, the log, the dev panel, and CPU speed (Normal, Fast, Instant; since BOT3). The inspector values are the first-run defaults.
- **CPU turns play themselves** (BOT2, `BOTS.md`).
  - **Pacing:** each action waits for walking pieces and a short think delay (0.9 s before the roll, 0.55 s between actions at Normal; ×0.35 at Fast). Instant has no delay and no walk animations. Holding Space hurries the CPU. Pause freezes it.
  - **Input:** while a CPU plays, board clicks, keys and HUD buttons don't act, and the board shows no landing hints.
  - **HUD:** the turn button reads "BLUE IS THINKING". The top bar names the seat "(CPU · STYLE)" and says how to hurry or pause. The squad rail tags CPU seats.
  - **Refusals:** a CPU's refused command goes to the full log only, never to a toast.
  - **Watch mode:** a table with no human seats is allowed.
- **A finished match opens the end screen** a beat after the winning move: the winner, the round, the seed, and a row per seat with its squad (shapes and names, since DR2) and a CPU tag where it applies, operators home, knockouts and operators lost. REMATCH deals the same table with a fresh seed (and so, often, a different first seat), keeping drafted squads (RANDOM draws again); NEW MATCH opens setup; VIEW BOARD hides the screen until Esc; MAIN MENU returns to the title.
- **Every tally is an engine answer.** Knockout credit is a core rule (COMBAT_SYSTEMS §1.2), not something the view infers from who was nearby.
- While setup, the draft or the end screen is open, the board and the game keys are ignored. Enter deals, starts or rematches; Esc goes back.

---

## 5. Unknown state still draws

Anything that maps game state to a visual carries a default case.

The status palette gives an unrecognised `StatusKind` a grey badge. A status kind added to the core later appears on the board without anyone remembering to update the view — it just appears unlabelled rather than invisibly.

**A silently omitted status is a bug that looks like a working board.** An unfamiliar grey mark is a question a player asks out loud.

The same applies to operators. A silhouette is chosen by name with a fallback shape, so an operator added to the roster is drawn rather than dropped — but a fallback is not an identity, and any operator that reaches the pool wants its own shape before it reaches a player.

---

## 6. Prototype conventions

The current build exists to answer whether the game is fun, and everything in it is disposable.

- **No art assets, no prefabs.** Sprites are generated at runtime; the board, pieces and controls are built in code. One script on one empty GameObject.
- **`OnGUI` for controls — superseded.** The in-match HUD (§4.2) replaced it. The `OnGUI` panel survives only behind Tab and F2 until the stranger test passes (ADR-0008 consequence 6).
- **Silhouette carries identity.** Colour is taken by the seat, so operators are told apart by shape and size — the same constraint `ART_DIRECTION` §5 sets for the real art, arrived at early and crudely. A shape that is hard to read here is information for the art pass. Size tracks health against the frailest operator on the roster, not a round number, or the smallest piece stops reading as small.
- **The board is drawn from `PathMap`, never hard-coded.** `BoardLayout` is the only place that knows where a cell is; the core knows only that cell 14 follows cell 13. That separation is what let the board change from a ring to a cross without touching a rule or a test.
- **Drawability is enforced here, not in the core.** `BoardLayout` refuses a profile it cannot render as a continuous cross. The core tolerates any circuit divisible by four, because the rules do not care about arm geometry and nothing in the core should start caring — and the harness legitimately measures boards that will never be drawn. The cost of that separation is that an undrawable board can survive a long time in simulation, which is exactly what happened.

---

## 7. Open items

- **No lap indicator.** Not needed while only single-lap boards ship, but `BoardProfile.Laps` is implemented and any lapped board makes two operators on the same cell visually identical and positionally unrelated.
- **No indication of whose operator is whose beyond colour** at a glance across the table — fine for hot-seat, untested for anything else.
- **`OnGUI` does not scale with resolution.** The controls panel is a fixed pixel width, so the camera has to size and offset around it; that is handled, but the panel itself does not reflow. Fine on one machine, not a build. _Partly closed 2026-09-15 (ADR-0008):_ the uGUI HUD canvas scales (1920×1080 reference, match 0.5), and per-piece health labels are drawn on it. The `OnGUI` panel keeps fixed pixels until it is deleted after the stranger test; this item closes with that commit.
- **Mimi draws as a fallback-adjacent shape with no art brief behind it** (§5). She is in the pool; the silhouette was chosen to be distinct, not to be right.
