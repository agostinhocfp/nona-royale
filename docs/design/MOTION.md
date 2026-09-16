# Nona Royale — Motion and Feedback (Stage 3)

> Location in repo: `docs/design/MOTION.md` · Project copy: `claude/MOTION.md`
> Status: **Open, 2026-09-16.** Decisions settled. Code waits until BOT2+BOT3 is committed, because MO1 rewrites the same batch handling in `MatchBootstrap`.
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
