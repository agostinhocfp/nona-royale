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
- 2026-09-17 — **Lethe taught to the bots** (`COMBAT_SYSTEMS.md` §10.10). The bots needed four fixes. Each is pinned by `LetheBotTests` and mutation-checked:
  - **`BotBoard.ExpectedHit` reads the real shield pool** through the new `GameEngine.ShieldPoolOn`. It used to assume `ShieldPoolDefault` (2), so a bot would keep hitting a 99-point Nano Cell, and it misjudged part-spent plates.
  - **A stun on an ally is a cost.** `CastPlanner.Buff` scores it at −2.5, the value of stunning an enemy. Nano Cell only scores above zero when the ally is threatened.
  - **A cleanse also strips the ally's shield.** `HarmfulWorth` subtracts the protection the shield was still providing. A Javi bot no longer pops a bubble its side just paid for while the ally is under threat, but still frees a bubbled ally when nothing is near.
  - **Crowd zones are scored per crowd.** `CastPlanner` multiplies by N−1 and ignores the status of an effect without one. `DraftPicker` no longer counts a stun aimed at allies, or a zone with no status, as control.
  - **Bots sweep, 800 matches, before and after Lethe:** turns per seat 28.2 → 26.9, knockouts 13.6 → 12.4, casts 60.4 → 55.3. Lethe's win share is 24%, neutral, and no other operator moved more than 3 points. Nano Cell is cast 1.17 times a match, Eris' Exploit 1.04.
  - **Seen in the same sweep, and not Lethe's doing:** Predator's Read and Cryo Field are cast 0.00 times a match, before and after. The planner has no scoring branch for `Watch` or `ProjectField` effects. That is worth its own look.
- 2026-09-17 — **Vendetta lifesteal** (`COMBAT_SYSTEMS.md` §2.5). The planner credits each blow's drain as defence, capped by what the caster is missing and by the target's health, and weighted like a heal. `LifestealBotTests` checks that a healthy Luka gets no credit and a wounded one does.
  - **Bots sweep, 800 matches:** Luka 23% → 26%. The others: Kurbyn 33% → 32%, Syla 30%, Javi 27% → 29%, Nuetu 24% → 26%, Bouncer 25% → 23%, Lethe 24% → 23%, Sanity 21%, Kian 22% → 21%, Mimi 21% → 20%. Turns per seat 26.9 → 26.5.
- 2026-09-17 — **Sanity burdened** (`COMBAT_SYSTEMS.md` §5.16, §10.8). `DraftPicker.EffectiveSpeed` folds a haste or burden passive into speed at its average worth (±0.23), so a burdened Sanity no longer drafts as a plain 1.0 operator and a hastened Lethe no longer drafts as a plain one. Move scoring needed nothing: it reads the engine's previews, which already carry the burden.
  - **Bots sweep, 800 matches, adopted row:** Syla 32%, Kurbyn 30%, Sanity 27% (was 21%), Javi 26%, Luka 25%, Nuetu 25%, Bouncer 25%, Lethe 22%, Mimi 19%, Kian 19%. Turns per seat 26.5 → 25.1. Personalities: Runner 27%, Brawler 26%, Banker 22%.
  - **Measured and not adopted:** +1 damage alone (Sanity 24%), burden plus damage (31%), 0.75 speed plus damage (35%).
- 2026-09-17 — **Revú taught to the bots** (`COMBAT_SYSTEMS.md` §10.11). Each change is pinned by `RevuBotTests` and mutation-checked:
  - **`ExpectedHit` takes the cast's cost** and applies Equilibrium, so a cheap cast on a Revú scores double and a dear one half.
  - **A drain is worth the energy it can actually take**, at the new `EnergyDenial` weight (0.5 per point).
  - **Sadist is scored from the target's pool**, with the splash counted, and is worth nothing against a full pool.
  - **Bots sweep, 800 matches:** Kurbyn 33%, Javi 31%, Syla 30%, Luka 27%, Nuetu 26%, Sanity 25%, Lethe 24%, Bouncer 21%, Revú 21%, Kian 20%, Mimi 17%. Turns per seat 25.1 → 24.7. Leech Round 1.53 casts per match, Sadist 0.88.
  - **Not taught:** holding energy back to blunt Sadist, and going for collisions against him. Both are real counterplay, and the planner has no notion of either.
- 2026-09-17 — **Revú tuned** (designer): health 8, Leech Round 2 damage with cooldown 1, Sadist cooldown 4. Bots sweep, 800 matches: Kurbyn 33%, Syla 33%, Javi 29%, Luka 25%, Lethe 25%, Revú 24% (was 21%), Sanity 24%, Nuetu 23%, Bouncer 23%, Kian 19%, Mimi 16%. Leech Round 2.87 casts per match (was 1.53). No bot change was needed.
- 2026-09-17 — **Cryo Field is finally cast.** `CastPlanner` gains a `ProjectField` branch. It values every enemy within the field's radius of the caster, at the tick damage times the ticks (duration − 1), with the delayed discount, and nothing while a field is already up. `BotBoard.DamageAgainstOne` counts the field too. Pinned by `MimiBotTests`, mutation-checked.
  - **With Mimi's 6 → 4 repricing, 800 matches:** Mimi 20% (16% before; 17% with the bot fix alone). Cryo Field 1.38 casts per match, Cryo-Pulse 2.53. Kurbyn 32%, Javi 32%, Syla 31%, Sanity 26%, Nuetu 25%, Luka 24%, Revú 24%, Bouncer 22%, Lethe 20%, Kian 19%.
  - **Still never cast:** Predator's Read. `CastPlanner` has no `Watch` branch, so Kurbyn's 32% comes from two abilities.
- 2026-09-17 — **Kian buffed** (designer; `COMBAT_SYSTEMS.md` §10.6): all three abilities cheaper, both emitters 2 Tech, Sonic Disrupter radius 3. No bot change was needed — the planner reads cost, amount, type and radius from the definitions.
  - **Bots sweep, 800 matches:** Kian 23% (was 19%). Syla 32%, Kurbyn 31%, Javi 30%, Sanity 26%, Luka 25%, Revú 23%, Bouncer 23%, Lethe 23% (was 20%), Nuetu 22%, Mimi 18% (was 20%, watch). Casts per match: Drone Strike 5.87, Sonic Disrupter 3.70, Inversion Matrix 3.05. Personalities: Runner 27%, Brawler 25%, Banker 24%. Turns per seat 26.3.
- 2026-09-17 — **Predator's Read is finally cast.** `CastPlanner` gains a `Watch` branch (`WatchValue`). Before it, every Kurbyn number was measured against two of his three abilities. Pinned by `WatchBotTests` (9) and four `StatusRegistryTests`; all nine mutants of the new logic fail a test.
  - **Value:** the strike (as a hit, or a kill if it would finish the target) times the chance the target moves, plus `WatchDenial` (1.5) times the chance it stays put. A seat with a single movable piece on the loop has to move it, so the chance is 1; otherwise it is `WatchMoveOdds` (0.55). No watch on a piece off the loop, on an ally, or on a piece already watched.
  - **Stuns are read for the target's next turn.** A stun cast this turn isn't active by `BotBoard.Has` until the enemy's turn, and one that ran through its last turn still reads as active, so `Has` answers the wrong turn for "will it move". New `StatusRegistry.HasOnNextTurn`, `GameEngine.HasStatusOnNextTurn` and `BotBoard.WillHave`. A target that will be stunned is worth nothing, and stunned squadmates aren't counted as alternatives.
  - **Bots sweep, 800 matches, before and after:** Predator's Read 0.00 → 0.77 casts per match. Kurbyn 31% → 32%, Syla 32% → 30%, Javi 30% → 29%, Sanity 26% → 24%, Luka 25%, Kian 23% → 24%, Nuetu 22% → 24%, Revú 23%, Bouncer 23%, Lethe 23% → 22%, Mimi 18%. Turns per seat 26.3 → 26.4, knockouts 13.1 → 13.3, casts 57.5 → 58.4. Bots beat the scripted players 67–73%.
  - **Reading:** the third ability adds about a point to Kurbyn, within noise. The top three (Kurbyn, Syla, Javi) are still the three 1.5× operators, so the planned trim can now be measured against his full kit.

- 2026-09-17 — **Mimi buffed** (designer; `COMBAT_SYSTEMS.md` §10.4): health 6 → 7, Cryo Field tick 1 → 2, radius 2 → 3. No bot change was needed — the planner reads the definitions. She was the sweep's last place at 17%, seven points under neutral.
  - **Bots sweep, 800 matches, before and after on the same tree** (the Miracle Pull cooldown nerf was already in the working tree for both): Mimi 17% → 21% (+4, about 2.7 standard errors — out of the noise). Kurbyn 32%, Syla 30% → 31%, Javi 29% → 28%, Luka 26% → 25%, Sanity 25%, Nuetu 24% → 23%, Kian 24% → 23%, Revú 23%, Bouncer 23% → 21%, Lethe 22% → 21%. Turns per seat 26.4 → 26.6, casts 58.4 → 59.6, knockouts 13.2 → 13.2. Cryo Field 1.35 → 2.69 casts per match. Bots beat the scripted players 66–73%.
  - **Reading:** she leaves last place, but the bottom is now a three-way cluster (Mimi, Bouncer, Lethe at 21%), so the spread between the 1.5× tier (Kurbyn 32%, Syla 31%, Javi 28%) and the floor is unchanged — the speed trim is still the open lever.
  - **Tests:** the patch broke the 7 expectations pinned to the old numbers — radius-edge fixtures and tick arithmetic in `CryoFieldTests`, and the designer's-numbers assertions in `MimiBotTests`. Fixtures moved to the new radius edge, expectations to the new tick; **679 passing.**

- 2026-09-17 — **Speed bonus capped per turn** (designer; `COMBAT_SYSTEMS.md` §6.3): at most `SpeedBonusCellCap` (2) extra cells from speed per operator per turn, charged on every move. No bot change was needed — the bots read the engine's previews, and the cap flows through them.
  - **Bots sweep, 800 matches, before and after:** Syla 31% → 30%, Javi 28% → 27%, Kurbyn 32% → 33% (noise), Bouncer 21% → 24%, Mimi 21% → 23%, Revú 23% → 20% (two standard errors, watch), Lethe 21% → 22%, the rest within a point. Turns per seat 26.6 → 27.8, knockouts 13.2 → 14.4, collisions 12.2 → 13.3. Personalities: Runner 25% → 28% (watch). Bots beat the scripted players 67–75%.
  - **Measured and not adopted:** cap 1 — Syla 27%, Javi 27%, Kurbyn 32%, Sanity 24% → 28% (watch). Turns per seat 28.6.
  - **Reading:** the cap compresses the bottom of the table but does not dethrone the 1.5× tier at either setting, and Kurbyn does not move at all — his edge is the 30% evasion, not the cells. If he stays on top in human games, the next lever is Evasive Protocol, not the band.
  - **Tests:** 10 new (`SpeedCapTests`), and the cap cost two existing fixtures their assumptions — `HasteCapTests`' two speedster expectations now compute through the cap, and `GameEngineTests.EveryNeutralizeInAWholeMatch_IsTallied` lost its hard-coded seed's collisions, so the seed is now searched like every other roll-sensitive test. **689 passing.**

- 2026-09-17 — **Kurbyn rebuilt: evasion 12%, Predator's Read removed, haste not speed** (designer; `COMBAT_SYSTEMS.md` §10.3). Syla's Ace Shards patch (3 → 2 damage, range 2) is in the same tree, committed by the designer. No planner change was needed — the haste passive flows through the previews, and the `Watch` branch in `CastPlanner` is now dead weight, harmless, for the next watch carrier. `WatchBotTests` retired with the ability.
  - **Bots sweep, 800 matches, against the speed-cap baseline:** Kurbyn 33% → 28%, Bouncer 24% → 28%, Javi 27% → 31%, Syla 30% → 24% (her patch, confounded), Luka 25% → 24%, Kian 23% → 24%, Sanity 24%, Mimi 23% → 24%, Revú 20% → 23%, Nuetu 23%, Lethe 22% → 21%. Turns per seat 27.8 → 27.5, knockouts 14.4 → 13.9, casts 62.3, collisions 13.3. Personalities 23/28/24 → 25/25/25. Bots beat the scripted players 70–73%.
  - **Reading:** the rebuild did what two speed-cap settings could not — Kurbyn left the top for the first time since measurements began, and the table is a 21–28% pack with one exception. **Javi at 31% is the new outlier**, up four points without being touched: either the field bunched around him or his support kit prices the new, longer matches better. That is the next measurement, not this one.
  - **Tests:** 689 → 668 passing — `PredatorsReadTests` (16) and `WatchBotTests` (9) retired, `KurbynTests` added (3) plus the cap-2 case in `HasteCapTests`; stale expectations moved in `GameEngineTests`, `BurdenTests`, `SpeedCapTests` and, for Syla's patch, `AbilityResolverTests`.
- 2026-09-18 — **Fortuna taught to the bots** (`COMBAT_SYSTEMS.md` §10.12). Four changes, each pinned by `FortunaBotTests` and mutation-checked:
  - **`CashPlanner` decides whether to sell a die** (§3.4). It is a comparison, not a rule: the die is worth the best landing it could buy, which `MoveScorer` has already priced including the danger of the cell it would end on, and two energy at the new `EnergyGain` weight (1.0 per point — above `EnergyCost`'s 0.45, which is the price a bot charges itself so it spends rather than hoards). A deploy is never sold, and no special case says so.
  - **`CastPlanner` values dice in cells** (§6.8). A re-deal is worth the mean face less the lowest die in hand; a set face is worth the pips it adds; both are credited with a deploy when somebody is in the yard, and a set double with the roll it is owed, discounted.
  - **A table is scored from the traffic behind it** (§7.7), not from who stands on it. `BotBoard.EnemiesBehind` is the mirror of `EnemiesAhead`. **The first pass scored it from occupants and it was cast 0.61 times a match**; asking the right question took it to 2.47.
  - **`DraftPicker` gained a tempo term** (`DraftTempo`, 1.0): one per dice ability and one for the House Edge. It is not enough — see below.
  - **Bots sweep, 800 matches**, on the 2026-09-18 balance pass: Kurbyn 28%, Kian 28%, Sanity 27%, Javi 27%, Bouncer 26%, Syla 26%, Luka 25%, Lethe 23%, **Fortuna 23%**, Nuetu 22%, Mimi 22%, Revú 22%. Turns per seat 26.7, neutralizes 13.4, refusals 0. Deal Again 2.96 casts a match, Boxcars 2.62, The Table 2.54, **dice sold 5.35**. Twelve operators changed the draft distribution, so this is a new baseline rather than a comparison — and the field is the tightest it has been.
  - **Found by her, fixed for everyone:** `CheckAbility` answered Ready for a caster standing in a home column, where the resolver has always refused. It produced 111 refusals in the first sweep and none after.
  - **Not fixed: the draft picker reads her last** (6.6 against a field of 8.9 to 17.9). It values damage, burst and sustain, and her kit has none of the three. Recorded in `COMBAT_SYSTEMS.md` §12 and pinned by a test rather than papered over with a weight; it costs nothing in the sweep, where squads are random, and it is wrong on the draft screen.
- 2026-09-21 — **The bots learned sanctuary** (`COMBAT_SYSTEMS.md` §4.4, third amendment). `BotBoard.ExpectedHit` returns 0 against an operator the engine reports as sheltered, and `CastPlanner.Debuff` scores a slow or stun at 0 on a target standing on its own spawn cell — both asked of `GameEngine` (`IsSheltered`, `Resists`) rather than restated. A push's landing is valued with `ExpectedHitOnceExposed`, because the collision happens where the push leaves the target, not on the shelter it is leaving. **Not taught: camping.** Nothing in `MoveScorer` values standing on a safe cell, so the bots gain shelter by accident and the sweeps under-read the rule — safe-cell occupancy did not rise (22.9% → 22.0%).
- 2026-09-21 — **Sadist's floor is scored** (§3.3). `CastPlanner` floors the primary figure the same way the resolver does, so a full pool now reads as the weakest case rather than a blank; `RevuBotTests` moved from "worth nothing" to "worth less". Bots sweep, 4000 matches: Sadist 0.77 → 1.10 casts a match.
- 2026-09-21 — **Burdened is scored** (`COMBAT_SYSTEMS.md` §5.16, §10.7). `CastPlanner.StatusWorth` had no case for it, so Bio-Link Rage's new rider was worth zero to the planner. It is now 1.0, the same as Slow, on the same reasoning: both take cells off a roll. **Still not taught, and it is the gap that matters here:** nothing in `MoveScorer` or the planner prices the bot's *own* mobility, so a bot that has been burdened plays exactly as before and a bot choosing a cast cannot tell that denying a fast enemy its cells is worth more than denying a slow one. Mobility denial is the one thing the harness cannot measure — head to head, Nuetu's squad against Kurbyn's, the rider is worth about a point and a half, and human play is where it has to be judged.
- 2026-09-24 — **Revú's debt taught to the bots** (`COMBAT_SYSTEMS.md` §3.3, §10.11). Each change is pinned by `RevuBotTests` and mutation-checked:
  - **A loan is worth the room the cap has left** (`EnergyDenial`, 0.5 a point), not what the target's pool holds: the seat pays or owes either way.
  - **Sadist is scored from the target seat's debt**, floored at 2, less a quarter point per point of debt it clears, since calling a debt forgoes what it would have drained.
  - **A seat in debt pays for casting into its own bill** (`CastPlanner.DebtShortfall`, new weight `DebtShortfall` 0.6 a point): the part of a cast's cost that would leave debt unpaid at the end of the turn is charged on top of the energy.
  - **Landing on a creditor is worth the debt it burns** (`MoveScorer.Contact`, new weight `DebtBurn` 0.8 a point), win or lose the contest.
  - **Bots sweep, 800 matches, same seeds as HEAD:** Revú 25% → 27% (noise band ±1.6), Leech Round 2.43 → 2.56 casts, Sadist 1.18 → 0.77, collisions 3.20 → 3.08, turns per seat 26.1 → 25.7. Across all four rows 79% of debt was paid and Sadist called in 0 debt 35% of the time — the bots still fire it into a seat that owes nothing, which a human would not.
  - **Not taught:** carrying a debt on purpose to afford a big cast, and positioning to put a debtor on the dice line behind Revú. Burns ran about one in ten matches.
- 2026-09-24 — **Debt stops being collected** (`COMBAT_SYSTEMS.md` §3.3). With no bill there is nothing for `DebtShortfall` to price, so the term and its weight are gone, and so is Sadist's small penalty for the drain a called debt used to forgo. `EnergyDenial` is renamed `DebtWorth` (0.5 a point): a loan is now worth a point of a later Sadist, not energy. `DebtBurn` (0.8) is unchanged and now meets larger debts — burns ran 0.28 a match worth 3.8 each, against 0.13 worth 2.1. **Not taught, and it is the obvious next thing:** letting a loan ripen. The bots still call Sadist on whatever the target owes, including nothing, 11% of the time.
