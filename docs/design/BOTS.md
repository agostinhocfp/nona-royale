# Nona Royale — CPU Opponents (Stage 2)

> Location in repo: `docs/design/BOTS.md` · Project copy: `claude/BOTS.md`
> Status: **Closed, 2026-09-16.** BOT1, BOT2 and BOT3 committed and passed Play Mode.
> Related: `NEXT_PHASES.md` (Stage 2), `DRAFT.md` (Stage 1 and its notes for this stage), `COMBAT_SYSTEMS.md`, `PRESENTATION.md` §1 and §4.3, ADR-0004, `tools/sim/sim-README.md`

## Goal

In setup, any seat can be HUMAN or CPU. CPU seats draft and play on their own, at a pace a human can follow. A table of four CPUs can be watched from the deal to the end screen.

## Decisions (settled 2026-09-16)

1. **Step-wise and query-first.**
   - `IBot.Next` returns **one** command at a time, so the driver can pace and animate each action.
   - The bot proposes only what the engine's queries allow (`PreviewLandings`, `CanDeploy`, `CheckAbility`, `LegalTargetsFor`, `LegalCellsFor`, `MustSpendRoll`, `CanRollAgain`).
   - If a command is still refused, the bot records it, never repeats it in that turn, and moves on.
   - A per-turn action cap forces `EndTurnCommand`, and a clear log line is written if even that is refused.
   - CPU refusals stay out of the toasts and the history chips, but are kept in the full log.
2. **Three personalities, one difficulty, all challenging.** The designer's condition: no personality is one-dimensional.
   - **Brawler:** spends freely, prefers kills and finishing blows, and lands on enemies when it can.
   - **Runner:** races and deploys eagerly. Casts defensively first, but **also casts offensively, at a lower rate**.
   - **Banker:** saves for its best ability and uses it on the highest-value target. **Spends on defence when an ally is in danger, at a lower rate.**
   - Difficulty tiers come later.
3. **Move choice is scored, never ruled.**
   - Each `PreviewLandings` option (and each deploy) gets a score from:
     - progress;
     - home-column entry and reaching home;
     - landing on a safe cell;
     - contact: landing on an enemy, weighted by personality;
     - a **cheap danger check**: a penalty for ending on the loop within reach of an enemy who could hit that cell next turn. Reach is measured along the track with `PathMap`.
   - Legality always comes from the engine.
4. **Bot randomness has its own stream**, `new SeededRandom(seed ^ BotConfig.SeedSalt)`. **It never draws from the match RNG**, so a seed gives the same dice whoever is playing.
5. **Draft picks.** Pick by personality priority with a little randomness, and avoid a squad with no healing or no burst where possible. Picks come only from `Available(seat)`, checked with `CanPick`, and draw from the bot stream.
6. **CPU drafting pace.**
   - **ALL PICK:** a shared rhythm. One CPU pick about every 1.5 s, in turn across the CPU seats, all inside the 30 s clock, while humans pick freely.
   - **SNAKE:** a CPU seat picks on its own turn, after the think delay.
   - The active-seat pointer skips CPU seats.
7. **Undo in SNAKE stays one step.** Undo would take back a CPU's pick and the CPU would simply pick again, so the draft screen refuses UNDO when the last pick was a CPU's.
8. **Pacing.**
   - About 0.5 s of thinking before each CPU action. The driver waits until no piece is walking.
   - A remembered **CPU speed** setting: Normal, Fast or Instant. Holding Space fast-forwards.
   - CPU time runs on scaled time, so pause freezes the bots.
9. **All-CPU tables are allowed** (watch mode). The minimum stays two seats.
10. **Sim.** `ScriptedPlayer` and its policies are unchanged, so the COMBAT_SYSTEMS §12 figures stay comparable. A new `bots` sim mode plays the core bots against each other (and against the scripted players) for balance reads.

## Increments

| #    | Increment        | What it delivers |
| ---- | ---------------- | ---------------- |
| BOT1 | **Core bots**    | `Core/Bots/`: `IBot`, `BotPersonality`, `BotConfig` (weights as config), `BotBrain` (step-wise), `MoveScorer`, `CastPlanner`, `DraftPicker`. EditMode tests: seeded full matches that never stall, bots never target themselves, never draw from the match RNG, each personality's signature preference, and legal draft squads. Sim `bots` mode. |
| BOT2 | **Driver**       | `BotDriver`: runs the current CPU seat when no card is open. It sends through the same `Send` path, waits for the think delay and for walks to finish, hides CPU refusals from toasts, and freezes on pause. `TurnButton` reads "BLUE IS THINKING", and the turn pill shows "(CPU)". |
| BOT3 | **Setup, draft, end** | Seat tiles cycle EMPTY → HUMAN → CPU, with a personality chip on CPU seats. `MatchSettings` gains a per-seat kind and personality. The draft screen auto-picks for CPU seats (decisions 6 and 7). The end screen and the rail mark CPU seats. A CPU speed row on the settings page. PRESENTATION §4.3. |

## Rules for this stage

- Core has no Unity types and no LINQ. Bot weights are config, not literals.
- The bot reads the engine and the map; it never restates a rule to decide legality. Its own estimates (reach, value) are preferences only.
- Every bot behaviour ships with EditMode tests.
- The view computes nothing: the driver only asks the brain and sends.

## Log

- 2026-09-16 — **Stage opened** in the Stage 1 chat. HEAD: `docs(draft): close stage 1 and hand CPU drafting notes to stage 2`.
- 2026-09-16 — **Decisions 1–10 settled** with the designer. Changes from the plan:
  - All three personalities must be challenging: the Runner also casts offensively, and the Banker also spends defensively, each at a lower rate.
  - Snake undo stays one step, so UNDO is refused after a CPU's pick.
  - CPU drafting in ALL PICK uses a shared ~1.5 s rhythm.
- 2026-09-16 — **BOT1 built: the core bots.**
  - **New `Core/Bots/`:**
    - `BotPersonality` and `BotConfig`. `BotWeights` holds every preference in one currency (points, one cell of progress = 1), with a preset per personality.
    - `IBot`, and `BotBrain`, which is step-wise and remembers refusals for the turn. `HoldsForReserve` is the Banker's rule as a pure function.
    - `BotBoard` reads the table and estimates danger. `MoveScorer` ranks each deploy and each `PreviewLandings` option. `CastPlanner` scores each legal cast from its effect list, in the target's cast mode, and never on the caster itself.
    - `DraftPicker`, and `BotTable`, which plays bot seats for tests and the sim.
  - **Danger check** (decision 3), per enemy on the loop:
    - its strongest ability that it can nearly afford (energy + 3), that is nearly ready (≤ 1 turn), and that reaches the cell along the track;
    - plus collision odds from behind: 0.3 within 6 cells, 0.1 within 12.
  - **What the bots can use:** a board reader and the match configs (`CombatConfig`, `GameConfig`), passed in and defaulting as `MatchFactory` does. Legality always comes from the engine.
  - **Tests:** 16 new (`Bots/BotTests.cs`), **492 passing.**
    - Seeded full matches at 2–4 seats finish with zero refusals.
    - The same seed gives the same match, and with jitter off the bot's own stream doesn't change the match.
    - No self-targeting across 8 matches. A refused command isn't repeated.
    - Personalities: Brawler and Runner take a killing landing; the Runner enters the home column when it can; a landing within an enemy's reach scores lower; the Runner holds a poke but takes a kill while the Brawler takes the poke; self-damage that would kill the caster is negative; the Banker's reserve rule.
    - Drafting: CPU drafts are legal in both modes, and the sustain gap gets filled.
  - **Mutation check:** removing the self-target filter, the landing kill bonus, or the Banker's reserve each broke a test.
  - **Stand-in build now keeps core and tests in separate assemblies**, as Unity does, so a test that touches an `internal` member fails here too. (`PlayerState.SetEnergy` is internal; that is why the Banker test uses the pure function.)
  - **Sim:** new `bots` mode (`tools/sim/NonaRoyale.Sim/BotSweep.cs`, README row). `ScriptedPlayer` and its policies are unchanged.
  - **Tuning pass (300 matches per row):**
    - **Cleanse:** it now values each status it strips by how much that status hurts (a stun more than a slow). Neural Purge went from 0 to 0.14 casts per match.
    - **Beacons:** a further ×0.6, because enemies can step off.
    - **Banker:** cast threshold 1.6, reserve override 4.
  - **Results:**

    | Bots against | Brawler | Runner | Banker |
    | --- | --- | --- | --- |
    | Spendthrift scripted players (win rate) | 66% | 64% | 66% |
    | Each other, 4 seats (win per seat) | 27% | 24% | 24% |

    Across all 300 matches: 32.5 turns per seat, 19.8 knockouts, 66 casts, 100% finished, 0 refusals.
  - **Balance reads for the designer, not acted on** (win share of squads fielding each operator; 25% is neutral):
    - **High:** Syla 36%, Kurbyn 35%.
    - **Middle:** Javi, Bouncer, Nuetu 24–25%.
    - **Low:** Luka, Mimi 21%; Kian, Sanity 19%.
    - **Casts per match:** Drone Strike is the most cast ability at about 8, yet Kian is among the lowest win shares. Trauma Plate (0.4) and Neural Purge (0.14) are rarely cast. The bots may undervalue those two, or the abilities may be weak.
- 2026-09-16 — **Kian: the designer's read against the sim.**
  - **The designer's read:** Kian is very strong in human hands and was a nerf candidate. In play he took a knockout in the first 5 turns, and almost a second, by pushing enemies off safe cells. The sim's 19% win share said the opposite.
  - **The cause was the CPU, not the operator.** It scored every push as +0.5 per cell per enemy, whatever the direction. A push that carries an enemy ahead of Kian toward its home column scored as a plus. Pushing an enemy off a safe cell, and landing a die on it afterwards, were not valued at all.
  - **Designer's decision: fix the CPU and don't nerf Kian.** Revisit after more human games.
  - **What changed:**
    - `BotBoard.PredictPush` works out where the push leaves an enemy, the same way the engine does.
    - `BotBoard.ReachableTrackCells` lists the cells the seat's dice can land on.
    - `CastPlanner.PushValue` values a push by:
      - cells the enemy loses (a forward push is negative);
      - being moved off a safe cell (`PushExposure` 2.0);
      - a landing a die in hand can make on the enemy's new cell, credited at 0.8 (`PushSetup`) and worth a knockout when this cast's damage plus the collision would finish it.
    - The move scorer then takes that landing as usual.
  - **Tests:** 3 new, **495 passing.**
    - A push off a safe cell into a landing counts as setting up a kill.
    - A forward push scores negative.
    - `PredictPush` matches the engine in 5 positions, including a shared cell and a wrap across cell 0.
    - Mutation check: breaking the shared-cell direction, or dropping the exposure bonus, fails a test.
  - **Measured (400 matches):**
    - **Push-then-land knockouts:** 211, against 120 with the new bonuses switched off, across 319 matches with Kian.
    - **Kian's win share:** 19% → 22% (the earlier 19% was a 300-match run).
    - **Other operators:** Kurbyn 34%, Syla 30%, Javi 30%, Nuetu 25%, Luka 23%, Bouncer 23%, Sanity 19%, Mimi 19%.
    - **Personalities:** unchanged, 26% / 25% / 24% per seat against each other; 67% / 63% / 68% against the scripted players.
  - **Not modelled yet:** where Kian's operator itself should move to set up a push, and where to paint Drone Strike for where enemies will be rather than where they are now. Humans read both; the CPU doesn't. Kian's sim number should be read as a floor.
- 2026-09-16 — **BOT2 + BOT3 built together**, as one Play Mode pass. The driver can't be tried without CPU seats in setup, and the two increments touch the same files.
  - **Core:**
    - `DraftState.LastPickSeat` says whose pick UNDO would revert (1 test).
    - `BotConfig.DraftRandomFor(seed)` gives the CPU drafters their own stream, so draft picks can't shift how the CPUs play the match.
    - **496 passing.**
  - **Driver (BOT2):**
    - **`Unity/Composition/BotDriver.cs`:** plain C#, owned and ticked by `MatchBootstrap`.
      - It builds one `BotBrain` per CPU seat on `BotConfig.RandomFor(seed)`.
      - It waits while a card is open, while pieces walk (except at Instant) and for the think delay: 0.9 s before a turn's roll, 0.55 s between actions; ×0.35 at Fast; ×0.15 while Space is held; none at Instant.
      - It runs on scaled time, so pause freezes it.
    - **`MatchBootstrap.DriveBots`:**
      - Sends each CPU command through the same `Handle` path as a human command, naming casts for the history strip.
      - Logs CPU refusals as `[CPU Blue] refused …` and clears them from the batch before the toasts.
      - Logs a line when the action cap ends a turn.
      - At Instant, it places pieces without walking.
    - **Human input on CPU turns:** every `IControlPanelHost` intent is ignored, `IsCommandable` is false, and the board draws no landing hints.
    - **HUD:**
      - `TurnButton` reads "{SEAT} IS THINKING / hold Space to hurry", disabled.
      - `TurnStrip.Refresh(engine, seatTag)` names the seat "(CPU · STYLE)" with a hurry/pause prompt.
      - `SquadRail` tags CPU seats, and their rows are never buttons.
      - `IControlPanelHost` gained `CpuTurn` and `SeatTag`.
  - **Setup, draft and end (BOT3):**
    - **Settings:**
      - `MatchSettings` keeps a `SeatKind` (Human, Cpu) and a `BotPersonality` per seat. Both are remembered for empty seats and copied by `Clone`.
      - The default styles go by seat: Red Brawler, Blue Runner, Green Banker, Violet Brawler.
      - `MatchBootstrap` keeps them in `_seatPlan` across rematches.
      - New inspector fields: `cpuSeats` (first deal) and `cpuSpeed`.
    - **Setup:**
      - Seat tiles cycle EMPTY → HUMAN → CPU. The last two seats skip EMPTY and say why.
      - A CPU tile has a gold style chip that cycles BRAWLER → RUNNER → BANKER.
      - The seat note calls a table with no humans watch mode.
    - **Draft:**
      - CPU seats pick through `BotBrain.PickDraft` (decisions 6 and 7), falling back to a random pick.
      - ALL PICK: one pick every 1.5 s, first after 1.0 s, taking turns across CPU seats. SNAKE: 0.8 s into a CPU's turn.
      - The picker is never a CPU seat, and a CPU seat's slots can't be cleared. UNDO is refused after a CPU's pick.
      - FILL & START and RANDOM REST let CPUs choose their own picks first.
      - Seat rows read "CPU · STYLE · n/3".
    - **End screen:** CPU seats are tagged.
    - **Settings page:** a "CPU speed" row (`UiKit.ChoiceRow`, cycling Normal/Fast/Instant), saved to `PlayerPrefs` (`nr.settings.cpuSpeed`).
  - **Changed from the plan:** no landing/target flash before a CPU move. Stage 3's presentation queue is the place for it.
  - **Checks:** the view compiles against the editor DLLs, and the sim compiles. `BotDriver` lives in the Unity assembly, so it has no EditMode test; Play Mode is its test. PRESENTATION §4.3 updated.
- 2026-09-16 — **Stage closed.** The designer's Play Mode pass on BOT2+BOT3 found no issues, so there is no follow-up fix commit.
  - HEAD at close: `docs(motion): open stage 3 with the settled motion decisions`.
  - Still pending, not blocking: the designer's minor balance edits. Claude documents them afterwards (diff against HEAD, docs, sim before/after).
  - Kian stays un-nerfed; revisit after more human games.
  - Carried to Stage 3: the pre-move landing/target flash (MO2 cast tells), and the setup tile's "click to change" hint if testers miss it.
- 2026-09-16 — **The designer's balance edits, documented** (both landed in `balance(haste): cap the haste bonus at 3 cells per operator per turn`). Details and tables live in COMBAT_SYSTEMS.
  - **Haste cap** (§5.9): Hastened adds at most 3 extra cells per operator per turn. Bots sweep: noise only (Syla 31% → 30%).
  - **Sanity** (§10.8): Zero-Day range 2 → 3. Collision cost 7 → 6, cooldown 4 → 3, range 5 → 6.
    - Why: this stage's balance read had Syla 36% and Kurbyn 35% on top and Sanity 19% at the bottom, and human games agreed. The pass buffs Sanity and, with the haste cap and the 2026-09-15 evasion cut, nerfs the fast operators.
    - Sanity's win share 21% → 22%, about one standard error.
    - Bot-vs-bot matches got longer: 32.5 → 34.2 turns per seat, 19.9 → 22.0 knockouts, Collision cast 4.81 → 6.80 times a match. A Collision-only run shows it is the cause.
    - Watch match length in human games.
  - This closes the pending balance item above.
  - **Match length, adopted (COMBAT_SYSTEMS §1.1, §5.11, §12):** +1 health across the roster and regen at +1 every 3 turns for any wound, off safe cells. Regen had never run because of a config bug, now fixed.
    - Bots against bots: 34.2 → 28.0 turns per seat, 22.0 → 13.4 knockouts. Win shares moved 3 points at most; Sanity 22% → 20%.
    - Bots beat the scripted players 70% of the time, up from 65–68%.
    - Watch in human games whether combat now feels too soft. Regen is the first thing to take back.
- 2026-09-16 — **Haste nerfed to flat cells** (COMBAT_SYSTEMS §5.9): +1 on a roll of 6 or less, +2 above, once per roll per operator, capped at 3 a turn. Bots sweep before/after: noise only (Syla 31% → 32%, turns per seat 28.2 both). The payout fires about once a match, so judge it in human games.
