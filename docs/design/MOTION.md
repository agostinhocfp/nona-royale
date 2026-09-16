# Nona Royale — Motion and Feedback (Stage 3)

> Location in repo: `docs/design/MOTION.md` · Project copy: `claude/MOTION.md`
> Status: **Open, 2026-09-16.** MO1 built, awaiting Play Mode. MO2 next.
> Related: `NEXT_PHASES.md` (Stage 3), `BOTS.md` (Stage 2), `PRESENTATION.md` §3 and §4, `ART_DIRECTION.md` §8, ADR-0008

## Goal

The board feels alive, and every action reads without the log:
- a dice moment nobody can miss;
- pieces that step, hits that land, casts that announce themselves;
- figures that breathe.

This covers the pitch's "simple animations (idle loops, move effects, card flips)", and makes CPU turns readable step by step.

## Decisions (settled 2026-09-16)

1. **Dice moment: a centre tumble.** This is the designer's playtest note.
   - Two dice tumble over the vault at the board's centre, settle on the engine's faces from `DiceRolled`, hold a beat, then fly to the tray.
   - Doubles get a "DOUBLES — roll again" callout.
   - The in-between faces are view-only and come from the view's own RNG. The landing is always the engine's.
2. **Sequencing: one action after another.** A `PresentationQueue` plays each batch in order: dice, then walks, then hits, then knockouts.
   - It exposes `IsBusy`. Human input and `BotDriver` wait on it; Space and E are buffered briefly.
   - Placement still snaps into place, never walks (PRESENTATION §3).
   - The queue raises named beats (`DiceLanded`, `Step`, `Hit`, `KO`, `CastTell`, …) for Stage 5's audio.
3. **Effects: all four sets, kept subtle.**
   - **Hops and rise:** pieces hop cell to cell with a little squash. A deploy rises from the seated bust to the standing pawn with a pop.
   - **Hits and knockouts:** about 60 ms of hit-stop and a small camera nudge on big hits. A knockout shatters in the seat colour, then the figure reappears seated in the yard.
   - **Cast tells:** a cyan sweep on the caster, then a line or arc to the target, or a drop onto the cell for beacons and zones. This also shows a CPU's target: the pre-move flash deferred from BOT2.
   - **Idle:** standing figures breathe slowly; seated ones sway.
4. **Settings.**
   - **Reduced motion** removes shake and hit-stop and shortens tweens.
   - **Animation speed** (Normal/Fast) applies to every seat's actions.
   - Both are remembered.
   - **CPU speed stays separate:** it only paces CPU thinking.

## Increments

| #   | Increment | What it delivers |
| --- | --------- | ---------------- |
| MO1 | **Queue and dice** | `View/PresentationQueue.cs` and `View/DiceRoller.cs`. `MatchBootstrap.Handle` hands batches to the queue. The tray's dice animate in when the roller finishes. Keys, board clicks and `BotDriver` wait while `IsBusy`, with a short input buffer. |
| MO2 | **Pieces, hits, casts, idle** | Hop walk, rise, KO shatter and return, hit-stop and nudge, cast tells (including CPU targets), idle breathing. The Reduced motion and animation speed settings. PRESENTATION §3.1 on sequencing. Named beats for Stage 5. The `FeedbackLayer` label for zero-day damage at upkeep, carried over from earlier hand-offs. |

## Rules for this stage

- The view computes nothing: every value an animation shows comes from an engine event or query.
- Gameplay animation runs on scaled time, and HUD pulses on unscaled time. Pause freezes everything except the HUD pulses.
- Display-only graphics never catch the pointer.
- Only forward travel walks. Placement snaps (PRESENTATION §3).
- No core changes are expected. If one turns out to be needed, it ships with EditMode tests.

## Log

- 2026-09-16 — **Stage opened** in the Stage 1–2 chat, while the designer playtests BOT2+BOT3.
- 2026-09-16 — **Decisions 1–4 settled**, all as recommended.
  - Code starts once BOT2+BOT3 is committed, since MO1 changes `MatchBootstrap.Handle`.
  - Watch-list from the playtest: stalled CPU turns, how readable CPU actions are at Normal speed, whether the three styles feel distinct, and how a CPU Kian uses the push.
  - Setup discoverability: the seat tile is the HUMAN/CPU control, which wasn't obvious at first. If testers miss it too, the tile should say "click to change".
- 2026-09-16 — **Stage 2 closed** (Play Mode pass, no fixes). MO1 unblocked.
- 2026-09-16 — **MO1 built: the queue and the dice.**
  - **New `View/PresentationQueue.cs`.**
    - Steps are an action, an optional "finished" condition and a minimum hold. Steps that finish at once chain in the same frame, so a batch with nothing to show (a refusal) settles immediately.
    - `IsBusy` is the gate. `BeatStarted` announces `DiceRolled`, `DiceLanded`, `Walk`, `Hit`, `Knockout` and `Settle` for Stage 5. MO2 adds the finer beats (`Step`, `CastTell`).
    - Scaled time, times `Speed`. A step still unfinished after 6 s is let go, so nothing can freeze the table.
    - `Flush` skips the cosmetic steps and runs the essential ones (the settle); `Clear` drops everything on a teardown or a new deal.
  - **New `View/DiceRoller.cs`** (decision 1).
    - Two ivory dice with pips are thrown in from a random side, spin and bounce over the vault, and land on the `DiceRolled` faces. They hold 0.4 s (0.85 s on doubles), then fly to the tray's dice row and fade.
    - The in-between faces come from the roller's own `System.Random`, never the match RNG.
    - A dark pool and a glow in the rolling seat's colour sit under the dice. Doubles show "DOUBLES · roll again" (or just "DOUBLES" when no roll is left).
    - Pips in the tumble, digits in the tray. A face above six falls back to a digit.
  - **New `View/UiPopIn.cs`:** a one-shot, unscaled pop used by the tray.
  - **`MatchBootstrap`:**
    - `Handle` now logs and clears a stale selection at once, then either settles immediately (the opening deal, CPU Instant) or queues the batch: dice → walks → hits (0.3 s) → knockouts (0.45 s) → settle.
    - The settle is the old tail of `Handle`: reposition, highlights, devices, top bar, turn button, history and toasts, end screen.
    - Hits now play after the walk, so a number lands on the cell where it happened with the mover already there.
    - An instant batch flushes the queue first, so the history never loses a batch.
    - **Input while busy:** board clicks, Enter and 1–3 wait. Deploy, Move and Cast intents are dropped. Space and E (and the Roll / End turn buttons) are kept for 0.4 s of real time and sent when the board is free. Esc, right-click and selection still work.
    - Holding Space on a CPU turn runs the queue and the dice ×3.
    - New components are fetched with an explicit null check (`Ensure<T>`), not `??`.
  - **`BotDriver`:** waits on `presentationBusy` (the queue, or any piece still moving) instead of walking pieces only. Instant still ignores it.
  - **`ActionTray`:** while the roller holds a new roll (`IControlPanelHost.DiceHeld`), the slots read "·" and the hint "Rolling…". When the faces arrive they pop in, once per roll. `DiceFaces` is the roller's flight target.
  - **`OperatorPiece.IsMoving`:** walking, or resting on a contested cell before a bounce.
  - **Checks:** the view compiles against the editor DLLs with no warnings; 496 core tests pass; the sim compiles. No core change. The queue's sequencing was exercised in a scratch harness (order, hold, flush, clear, pause, speed, 6 s cap), and a mutation to `Flush` was caught. The roller and the tray are uGUI, so Play Mode is their only layout check.
  - **Known and left for MO2:** the piece health readout and the rail read the engine at once, so a health drop can show a moment before its hit number. Walks still glide at 11 cells/s (the hop is MO2). The CPU speed setting doesn't shorten the dice moment; only the Space hurry does. MO2's animation speed setting covers that.
