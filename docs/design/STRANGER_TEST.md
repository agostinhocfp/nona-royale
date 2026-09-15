# Nona Royale — Stranger Test

> Location in repo: `docs/design/STRANGER_TEST.md`
> Status: **Ready.** Pass bar (§4) confirmed 2026-09-15. Sessions planned for the week of 2026-09-21.
> Related: ADR-0008 (consequence 6 makes this test the gate before the `OnGUI` panel is deleted), `PRESENTATION.md` §2 (what must be visible), §4 (select, then commit)

## Why this document exists

ADR-0008 made one test binding: the uGUI HUD replaces the `OnGUI` panel only after a stranger can play on it with no explanation. The ADR never said how the test is run or what counts as a pass. A test with no written bar gets passed by whoever wants it passed.

It tests the **presentation**, not the design. Whether the game is fun is a different question, and a separate session answers it.

---

## 1. Who

- **Two strangers, one session each.** One plays games regularly, one does not. A single tester is an anecdote.
- **Nobody who has seen the game**, heard it explained, or read these docs.
- **One facilitator**, who watches and takes notes. The facilitator does not play.

## 2. Setup

Set these on `MatchBootstrap` before pressing Play:

| Field                | Value | Why                                                              |
| -------------------- | ----- | ---------------------------------------------------------------- |
| `useLegacyPanel`     | off   | The uGUI panel is what is being tested                           |
| `showPanel`          | on    | The stranger should not have to find Tab to start playing        |
| `showPieceHealth`    | on    | Whether always-on health is noise is one of the findings         |
| `randomSquads`       | off   | The alpha three: same squads for both testers, easier to compare |
| `players`            | 2     | The stranger plays **both seats**, as in hot-seat play           |
| `openingDeployments` | 2     | The adopted value                                                |

- **Two window sizes per session.** Play the first half at 1920×1080 and the second at 1280×720. Switch at a turn boundary. The HUD scales with the window, and this is the only way to check it with a real player.
- **Record the screen**, with the tester's voice if they agree. Notes miss half of what happens.

## 3. The session

1. **Say exactly this, and nothing else:** _"This is a board game for two. Play both sides. Work out how to play from the screen, and please think out loud."_
2. **Answer no rules questions.** To any question, reply: _"What does the screen tell you?"_ If the tester is stuck for **two minutes**, give the smallest hint that unblocks them and record it as an **assist**.
3. **Time box: 20 minutes** or the end of the match, whichever comes first.
4. **Note each of these as it happens**, with a timestamp:
   - a pause of more than ~10 seconds with no action;
   - a question, even one asked to themselves;
   - a rejected command (it appears in the Events list);
   - a misread: acting on something the screen did not say (clicking a piece to move it, expecting a cast to fire on selection, playing the wrong seat's operators).
5. **Debrief (5 minutes).** Pause a match in progress and ask the questions in §4, pointing at the screen. Write down the answers word for word.

## 4. Pass bar

The test passes when **both** testers meet all three conditions:

1. **Plays unassisted.** Completes at least **three full turns per seat** with **no assists**.
2. **Casts.** Completes at least **one ability cast** from select to CAST, including picking a target or a cell when the ability needs one.
3. **Reads the board.** Answers **at least five of these seven** debrief questions correctly. Each one checks an item from PRESENTATION §2 or §4:

   | #   | Question                                                          | Checks                               |
   | --- | ----------------------------------------------------------------- | ------------------------------------ |
   | 1   | Whose turn is it, and how much energy do they have?               | Turn strip                           |
   | 2   | Which of your pieces are on the board, and which are waiting?     | Deployed operators                   |
   | 3   | How much health does _that_ enemy piece have?                     | Per-piece health label               |
   | 4   | What is wrong with _that_ piece? (point at one with a status tag) | Status tags                          |
   | 5   | Why did _that_ piece lose health just now? (after an upkeep tick) | Cause of damage (floating text, log) |
   | 6   | Which squares are safe, and what does safe mean?                  | Safe cells                           |
   | 7   | Why can't you use _that_ ability right now?                       | Readiness reason in the panel        |

   Question 6 has two parts. Pointing at the right squares without knowing what they do is half an answer: record it, but it does not count.

**What does not fail the test:** not knowing a rule the screen never claims to teach (for example, that a 6 is needed to deploy the third operator). Record it as a finding for the rules or onboarding work, not as a HUD failure.

## 5. When it fails

Record each finding in the log below, tagged with **one** of the three axes ADR-0008 says move together. Fix them in the ADR's revisit order:

1. **Visibility** — shown at the wrong time, or missing (cheapest to change);
2. **Layout** — where on screen, how big, what it covers;
3. **Technology** — only if per-piece anchoring or pointer events are what fought back.

Fix, then run the test again with **new** strangers. A tester who has already seen the game is no longer a stranger.

### Findings log

| Session | Time | What happened | Axis | Assist? | Fix / decision |
| ------- | ---- | ------------- | ---- | ------- | -------------- |
|         |      |               |      |         |                |

**Already known.** Watch for these rather than fix them beforehand, so the test shows whether they matter:

- Move buttons are labelled with pip counts (`6`, `4`, `2`). Nothing says the bold one spends the whole roll.
- Status tags under a piece can overlap the health label of a piece on the cell directly below.
- The key hint (Tab / H / F2) shows only while the panel is hidden.
- Safe cells are coloured, but nothing on screen says what "safe" means.
- The energy cap is not shown, because `GameEngine` does not expose it (see `TurnStrip`).

## 6. When it passes

The `OnGUI` path is deleted **in one commit**, and that commit is logged (ADR-0008 consequence 6). It removes:

- **From `MatchBootstrap`:**
  - the drawing code: `OnGUI`, `DrawPanel`, `DrawMoveButtons`, `SeenEarlier`, `Faces`, `DrawAbilities`, `DrawTargetList`, `DrawTargets`, `DrawLog` and `_panelScroll`;
  - the legacy switch: `useLegacyPanel`, the F2 key and `_framedLegacy`;
  - the pixel reservation: the `PanelWidth` constant and the panel-rect guard in `HandleBoardClick`. `FrameCamera` then reserves only `ControlPanel.ReservedWidth`.
- **From `ControlPanel`:** the F2 part of the key hint.
- **From the docs:**
  - `PRESENTATION.md` §6: the "`OnGUI` for controls" bullet is rewritten, and the class remark that says "controls" in `MatchBootstrap` is checked.
  - `PRESENTATION.md` §7: the "`OnGUI` does not scale" item is closed.
  - ADR-0008: a line recording the removal commit.
  - This document: status set to **Passed**, with the dates and the findings log kept as history.
