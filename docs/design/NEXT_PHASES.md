# Nona Royale — Next Phases (hand-off)

> Location in repo: `docs/design/NEXT_PHASES.md` · Project copy: `claude/NEXT_PHASES.md`
> Status: **Plan, 2026-09-16.** Written when the GUI phase closed at increment J. Each stage runs in its own chat. Open that chat with the stage's start prompt (at the end of each stage).
> Related: `GUI_PHASE.md` (the phase that just closed), `PRESENTATION.md`, `GDD.md` §2.2, ADR-0001, ADR-0004, ADR-0008, `ART_PIPELINE.md`, `ART_PROMPTS.md` (project), `OPERATORS.md`

## Where things stand

- **Stage 1 (draft): closed 2026-09-16.** DR1 and DR2 are committed and passed Play Mode. Its log, and notes for Stage 2, are in `DRAFT.md`. The decisions changed the plan below: ALL PICK became a free, simultaneous 30 s draft, and SNAKE became a second mode with 10 s per pick.
- **GUI phase: done through J** (title, setup, pause, end, in-match HUD). Everything up to and including J is committed (`feat(flow): title menu, main-menu return and remembered settings`).
- **K is parked.** The formal stranger test has no date. The designer ran an informal test at home: the GUI passed easily. The one note was that **the dice roll could be more obvious**, either by catching the eye or by sitting more centrally. That note goes to Stage 3.
- **The OnGUI dev panel stays** behind Tab (off by default) until a formal stranger test passes (ADR-0008 consequence 6). Known issue: when opened, it draws over the modal cards. Nobody needs it outside debugging.
- **Art:** Luka's look is settled (`OPERATORS.md`, 2026-09-16). The four run-4 crops are ready for Meshy (in the chat, `art/luka_meshy/`). The designer is working in Meshy alongside these stages.
- **Tests:** 476 passing in Unity (434 before Stage 1). The sim compiles.

## The stages, in order

| #   | Stage                          | Why this position                                                                                                  | Core work                     | Size          |
| --- | ------------------------------ | ------------------------------------------------------------------------------------------------------------------ | ----------------------------- | ------------- |
| 1   | **All Pick Draft**             | Changes the match flow (setup → draft → match) and gives the core a draft model. Bots need that model to pick.      | `Draft` model + tests         | 2 increments  |
| 2   | **CPU opponents**              | Solo play is in the project pitch, and bots let the designer playtest alone. The sim already plays whole matches.   | `Bots` namespace + tests      | 3 increments  |
| 3   | **Motion and feedback**        | Bot turns need to be watchable, and the dice note lands here. It adds a presentation queue that audio will hook into. | none expected                 | 2 increments  |
| 4   | **Art hookup + ADR-0009**      | Can jump the queue whenever Meshy output is ready. It is independent of 1–3.                                        | none                          | 1 increment   |
| 5   | **Audio**                      | Sounds fire on the animation beats from Stage 3, and voice lines need the operators' final identities.              | none                          | 1–2 increments |

**Increment ids:** `DR1`, `DR2` (draft), `BOT1`–`BOT3`, `MO1`–`MO2` (motion), `ART1`, `AU1`–`AU2` (audio). These don't clash with the GUI letters.

**Each stage keeps its own log** in a new design doc (named in each stage below), in the style of `GUI_PHASE.md`: goal, increments table, rules, then a dated log. Record each decision in that doc when it is made. Keep a copy in the project as `claude/<NAME>.md`, because the next stage's start prompt reads it from there.

---

## How every stage works (carry this into each chat)

1. **Discipline.** Don't trust the project snapshot. Read the repo, and ask for any file you need. Project copies of `.cs` files are **stale** (for example, the project `Roster.cs` lists 8 operators; the repo has 9).
2. **Folder access is per session.** At the start, request the repo (`~/Documents/dev/gamedev/unity/nona-royale`) and the editor DLL folder (`C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data\Managed\UnityEngine`).
3. **Check HEAD first** (`.git/logs/HEAD`, last line). Never write into files that an uncommitted increment also touches.
4. **Settle the stage's open decisions with the designer before writing code.** Use a picker, and always mark the recommended option.
5. **Build and check in the cloud:** view compile against the editor DLLs, core tests, sim compile. Then write back to the repo with `device_commit_files`, using the device mtimes as the guard. Repo files are **CRLF**. Strip a stray final `\r` where the HEAD version has no trailing newline.
6. **Whole files** where possible. Every new file starts with a comment that gives its path.
7. **Log in the stage doc before the commit.** Hand over `git add <explicit paths>` plus `git commit -m "type(scope): subject" -m "body"`, with **no AI trailers**. Include the `.meta` files of new scripts.
8. **The designer runs Play Mode on Windows** and commits. Play Mode is the acceptance test.
9. **Standing rules:**
   - The view computes nothing (PRESENTATION §1).
   - Core has zero Unity types and no LINQ (the current core uses none; keep it that way).
   - Constants are config, not literals.
   - Every core rule ships with EditMode tests.
   - No singletons. `MatchBootstrap` is the composition root.
   - Display-only graphics never catch the pointer.
   - Only forward travel walks (PRESENTATION §3).
10. **Tooling notes** from the previous hand-off still apply:
    - If the device VM fails, stage and commit still work.
    - Device git needs `GIT_OPTIONAL_LOCKS=0`.
    - Run `apt-get update` before installing `dotnet-sdk-8.0`.
    - Tar `Assets/_Project/Scripts`, `Assets/Tests/EditMode`, `tools/tests-verify/Runner.cs` plus `nunit.framework.dll`, and the two `Library/ScriptAssemblies` DLLs into `Temp/src.tar`; tar the editor's `UnityEngine*.dll` into `Temp/unity.tar`.
    - Set up three cloud projects: a view library (net8, `LangVersion` 9), a test exe, and a sim build. Add a `nuget.config` with no package sources.

---

## Stage 1 — All Pick Draft (closed 2026-09-16; see `DRAFT.md`)

**Stage doc:** `docs/design/DRAFT.md`

### Goal

Before the match, the seats take turns picking their three operators from the full roster, with every pick visible. The setup screen's squad choice becomes **ALL PICK / RANDOM / ALPHA THREE**. When all the picks are in, the match deals with those squads.

### What exists

- `Roster.All` (9 operators) and `Roster.SquadSize` (3). `Roster.DraftRandom(IRandom)` draws distinct operators per seat from the **match RNG**. `Roster.ByName`.
- `MatchFactory.Create(seats, seed, squads: ...)` already takes explicit squads per seat and validates the count. A seat missing from the dictionary is drafted at random.
- `OperatorDefinition`: `Name`, `MaxHealth`, `BaseSpeed`, `Abilities`, `Aura`, `Passive`, `PassiveMagnitude`. There is **no role or tagline field**.
- `AbilityDefinition`: `Name`, `Description`, `EnergyCost`, `CooldownTurns`, `Range`/`HasUnlimitedRange`, `Targeting`.
- **GDD §2.2:**
  - Three **distinct** operators per seat. Two seats **may** field the same operator. Global uniqueness cannot be the rule at four seats (12 picks, 9 operators).
  - Pick order ("simultaneous or in turn order") is still **open**, and this stage closes it.
- **Setup and flow:**
  - `MatchSettings` has `Seats`, `Drafted`, `Seed` and `Clone`.
  - `SetupScreen` (a `ModalCard`) → `IMatchFlowHost.Deal(settings)` → `MatchBootstrap.NewMatch()`, which reads `randomSquads`.
  - `AppScreen` is { Title, Setup, Match, Paused, Results }, derived from which cards are open.
- **Mimi has two abilities** (Cryo Field is deliberately absent). The draft must show that honestly.

### Decisions to settle first (with recommendations)

1. **Pick order: snake (recommended).** Red, Blue, Green, Violet, then Violet, Green, Blue, Red, then forward again. This offsets first-pick advantage. The alternative is plain round-robin.
2. **Uniqueness.**
   - (a) **GDD rule (recommended):** distinct within a seat, duplicates allowed across seats. Works at every seat count and needs no GDD change.
   - (b) Optional "Unique picks" toggle, allowed only when there are 3 or fewer seats (9 picks fit 9 operators).
   - (c) Global uniqueness always. This is impossible at 4 seats.
   - If (b) is chosen, record it in GDD §2.2.
3. **Visibility.** Picks are **visible as they happen (recommended)**, which suits hot-seat. Hidden simultaneous picks would need a "pass the device" screen.
4. **Helpers.** A **RANDOM** button for the current pick, plus **RANDOM REST** to finish the draft (recommended). No timer for local play.
5. **Rematch keeps the squads (recommended).** NEW MATCH goes back through setup and the draft.
6. **Where the copy lives.** The draft card needs a one-line role ("Tank", "Assassin", …).
   - **View-side `OperatorCopy` table keyed by name, with a fallback (recommended).** Same pattern as `PieceShape`, and it keeps display text out of the rules.
   - Alternatively, a `Role` field on `OperatorDefinition`.
7. **Draft seed.** RANDOM picks draw from a **draft RNG derived from the seed** (for example `new SeededRandom(seed ^ DraftSalt)`), **not** the match RNG, so the dice stream does not depend on how many random picks were made. Note in the doc: explicit squads skip `DraftRandom`, so a seed now gives different dice under ALL PICK than under RANDOM. That's expected.

### Increments

- **DR1 — core draft model**
  - New `Core/Draft/DraftState.cs` (pure C#), plus `DraftOrder` (snake or round-robin) and a small `DraftConfig`.
    - Constructor: `(IReadOnlyList<PlayerColor> seats, DraftOrder order, bool globallyUnique)`.
    - Queries: `CurrentSeat`, `PickNumber`, `IsComplete`, `Available(seat)`, `CanPick(seat, def)` returning a reason (duplicate in squad, taken, not your turn, complete), `PicksOf(seat)`.
    - Commands: `Pick(def)`, `Undo()` (optional; recommend yes for hot-seat misclicks, one step), `Squads()` → `IReadOnlyDictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>` for `MatchFactory.Create`.
    - `RandomPick(IRandom)` picks uniformly from `Available`.
  - Tests, named by behaviour:
    - `SnakeOrder_FourSeats_SecondRoundReverses`
    - `Pick_SameOperatorTwiceForOneSeat_IsRefused`
    - `Pick_SameOperatorAcrossSeats_IsAllowed`
    - `UniqueMode_RefusesTakenOperator`
    - `UniqueMode_RefusedAboveThreeSeats`
    - `Complete_AfterThreePicksPerSeat`
    - `Squads_FeedMatchFactory`
    - `RandomPick_IsDeterministicForSeed`
    - `Undo_RestoresTurnAndPool`
  - `MatchSettings` gets a squad mode enum (`AllPick`, `Random`, `Alpha`) in place of `Drafted`, plus the unique flag if (b) is chosen. `MatchBootstrap` passes the finished squads to `MatchFactory.Create`.
- **DR2 — the draft screen**
  - New `View/DraftScreen.cs`. A full-canvas screen, **not** a small card: it needs room.
    - **Roster grid (3×3 cards).** Each card shows the procedural shape (the portrait slot for Stage 4), name, role line, health, speed, and the three abilities with cost, range and cooldown. Hovering or selecting a card expands the ability descriptions.
    - **Seat columns.** Each seat gets its colour, three pick slots, and a "PICKING" marker.
    - **Controls.** A pick-order strip; PICK (Enter), RANDOM, RANDOM REST, UNDO (Backspace), BACK (Esc, returns to setup with a warning if picks exist); START when complete (Enter).
    - **Refused cards** grey out with the reason from `CanPick`. The view computes nothing.
  - `AppScreen.Draft`: setup's DEAL opens the draft when the mode is ALL PICK. The empty room stays behind a scrim.
  - The end screen shows each seat's three operators (small shapes) beside the tally.
  - Update PRESENTATION §4.3 and GDD §2.2 (draft order closed).

### Done when

- Setup → ALL PICK → draft → match works for 2, 3 and 4 seats.
- Duplicates behave as decided. RANDOM REST finishes the draft.
- Esc and BACK never lose a match silently. REMATCH keeps the squads.
- Tests pass, and the sim still compiles.

### Start prompt for the Stage 1 chat

> We're starting Stage 1 (All Pick Draft) from `claude/NEXT_PHASES.md`. Read it and `claude/HANDOFF_next_session.md` first, then request folder access, check HEAD, and walk me through the Stage 1 decisions before writing code.

---

## Stage 2 — CPU opponents

**Stage doc:** `docs/design/BOTS.md`

### Goal

In setup, any seat can be HUMAN or CPU. CPU seats draft and play on their own, at a pace a human can follow. A match of four CPUs can be watched from start to finish.

### What exists

- **`tools/sim/NonaRoyale.Sim`:**
  - `ScriptedPlayer` plays a whole turn: roll → deploy all → move (pooled, most-advanced first) → policy spend. It loops on "roll again".
  - Policies (`IEnergyPolicy`): `RacerPolicy`, `SpendthriftPolicy`, `BankerPolicy`. `Casting.TryCast` tries each targeting mode, enemies before allies, the caster excluded.
  - **It finds legal actions by sending commands and reading `CommandRejected`.** That is fine in a harness. In the game, every rejection becomes a toast and a history chip.
  - **COMBAT_SYSTEMS §12 was measured with these players.** Leave the sim's classes unchanged so the figures stay comparable.
- **Engine queries a bot can use instead of trial and error:**
  - Turn state: `Phase`, `CurrentPlayer`, `UnspentDice`, `MustSpendRoll`, `CanRollAgain`.
  - Movement and deployment: `CanDeploy(op)`, `PreviewLandings()` (per operator and die, including pooled), `IsHome(op)`.
  - Abilities: `CheckAbility(caster, ability)`, `LegalTargetsFor(caster, ability)`, `LegalCellsFor(caster, ability)`, `TurnsUntilReady`, `ActiveStatusesOn(op)`, `ActiveBeacons()`, `ActiveZones()`.
  - Tallies and ids: `EnergyCap`, `KnockoutsScoredBy`, `Match.AbilitiesByOperator`.
- **Commands:** `RollDiceCommand`, `DeployCommand(id)`, `MoveCommand(id, dieFace?)` (splitting exists), `UseAbilityCommand(caster, ability, target?, cell?)`, `EndTurnCommand`.
- **The view runs every batch at once.** `MatchBootstrap.Send` → `Handle` → `PlayFeedback`, `WalkMoves`, `Reposition`, and so on. Walks run side by side. `OperatorPiece.IsWalking` exists. There is no presentation queue yet (Stage 3).
- **Rules the bots must respect:**
  - `IsCommandable(op)` gates human input by seat.
  - Self-targeting is filtered in the view, and should be filtered in the bot too (see the Velvet Rope note in `EnergyPolicy.cs`).

### Decisions to settle first

1. **Step-wise brain (recommended).** `IBot.Next(match, seat)` returns **one** command at a time, so the driver can animate and pace each one. The alternative is a whole-turn script, which can't be paced.
2. **Query-first, rejection-safe (recommended).** The bot proposes only actions the engine's queries allow.
   - If a command is still rejected, the bot records it, never repeats it in that step, and moves on.
   - A per-turn action guard forces `EndTurnCommand`, and ends in a clear log line if even that is refused.
   - Rejections from bots are hidden from toasts but kept in the full log.
3. **Personalities.** Three styles that match the sim's axis but play better:
   - **Brawler** spends freely, prefers kills and finishing blows, and lands on enemies when it can.
   - **Runner** races, deploys eagerly, casts only defensively or when it's free.
   - **Banker** saves for its best ability and uses it on the highest-value target.
   - Recommended: all three at launch, with one difficulty. Difficulty tiers come later.
4. **Move choice.** Score each `PreviewLandings` option:
   - **Toward the score:** progress, home-column entry, reaching home, and landing on a safe cell.
   - **Contact:** landing on or bumping an enemy (weighted by personality), and a penalty for ending inside an enemy's threat range (use `LegalTargetsFor` from their side if it can be queried cheaply; otherwise skip it in v1).
   - These are **preferences, not rules**. Legality always comes from the engine.
5. **Bot randomness** comes from its own stream, `new SeededRandom(seed ^ BotSalt)`. **It never draws from the match RNG**, so the same seed gives the same dice whoever is playing.
6. **Draft picks.** Pick by personality priority with a little randomness, and avoid a squad with no healing or no burst if possible. Pacing differs by mode: ALL PICK has no turns, so CPU picks need their own rhythm inside the 30 s clock; SNAKE picks on the CPU seat's turn, inside the 10 s clock. See the Stage 2 notes at the end of `DRAFT.md`.
7. **Pacing.**
   - About 0.5 s of thinking before each action, and the driver waits until no piece is walking.
   - A **bot speed** setting (Normal / Fast / Instant), remembered. Space held = fast-forward.
   - Bot time uses scaled time, so pause freezes it.
8. **All-CPU tables are allowed (recommended).** Watch mode is useful for testing. The minimum stays two seats.
9. **The sim** keeps `ScriptedPlayer`. Optionally add a `bots` sim mode that runs the core bots against each other, for balance reads.

### Increments

- **BOT1 — core bots**
  - New `Core/Bots/`: `IBot`, `BotPersonality`, `BotBrain` (step-wise), `MoveScorer`, `CastPlanner`, `DraftPicker`, `BotConfig` (weights as config).
  - Tests:
    - `Bots_FullSeededMatches_NeverSendRejectedCommands` (N seeds, 2–4 seats, all personalities)
    - `Bots_MatchesAlwaysTerminate_WithinGuard`
    - `Bots_SameSeed_SameMatch`
    - `Bots_DoNotConsumeMatchRng`
    - `Brawler_PrefersKillingBlow`
    - `Runner_EntersHomeColumnWhenOffered`
    - `Bot_NeverTargetsSelf`
    - `DraftPicker_ProducesLegalSquad`
- **BOT2 — the driver**
  - New `Unity/Composition/BotDriver.cs` (or `View/`). It runs when the current seat is CPU and no card is open. It asks the brain, sends through the same `Send` path, waits for walks and the think delay, and repeats.
  - While a bot acts:
    - Human board input is ignored.
    - `TurnButton` reads "BLUE IS THINKING".
    - The turn pill shows "(CPU)".
  - Optionally, before a move the bot's landing ring and target ring flash briefly, so players can read what it did.
- **BOT3 — setup, draft and end**
  - Seat tiles cycle EMPTY → HUMAN → CPU, with a personality chip on CPU seats.
  - `MatchSettings` gains a per-seat kind and personality.
  - The draft screen auto-picks for CPU seats, paced as decided in item 6. The active-seat pointer skips CPU seats.
  - The end screen and the rail mark CPU seats.
  - The bot speed row goes on the settings page (`SettingsRows`, `SettingsStore`).
  - Update PRESENTATION §4.3.

### Done when

- One human against three CPUs plays a full match with no stuck turns.
- Four CPUs finish a match, and the board stays readable at Normal speed.
- Pause freezes the bots.
- No rejected bot command shows in toasts.
- Tests pass, and the sim still compiles.

### Start prompt for the Stage 2 chat

> Stage 2 (CPU opponents) from `claude/NEXT_PHASES.md`. Read it and `claude/DRAFT.md` (Stage 1's log), request folder access, check HEAD, and go through the Stage 2 decisions with me first.

---

## Stage 3 — Motion and feedback

**Stage doc:** `docs/design/MOTION.md`

### Goal

The board feels alive and every action reads without the log: a dice moment you can't miss, pieces that step, hits that land, casts that announce themselves, and figures that breathe. It covers the pitch's "simple animations (idle loops, move effects, card flips)".

### What exists

- **Pieces and feedback:**
  - `OperatorPiece`: `Walk(path, bouncedTo)`, `Settle`, `Place`, `Flash`, `IsWalking`, hover lift, a seated bust and a standing pawn.
  - `FeedbackLayer`: `Damage` (floater with cause), `Heal`, `Evaded`, `Absorbed`, `Neutralized` (seat-coloured burst), plus `ExpandingPulse`.
  - `FloatingText`, `UiPulse` (unscaled), `EventToasts`, `TurnBanner`, `TurnButton`.
- **Dice:** only the tray's dice section (`ActionTray.DiceSection`), showing the unspent faces and a hint line.
- **Ordering:** `MatchBootstrap.Handle` runs feedback **before** `Reposition`, so a burst lands where the hit happened. Batches are not queued.
- **Time:** pause sets `Time.timeScale = 0`. Gameplay animation uses scaled time, and HUD pulses use unscaled time.

### Decisions to settle first

1. **Dice moment** (the designer's note).
   - **(a) Recommended:** the dice tumble over the vault at the board's centre, settle on the real faces from `DiceRolled`, hold for a beat, then fly to the tray. Doubles get a "DOUBLES, roll again" callout.
   - (b) Bigger dice in the tray with a pulse.
   - (c) Dice beside the turn button.
   - The tumble shows random faces from the **view's** RNG and always lands on the engine's values.
2. **Presentation queue (recommended).**
   - A `PresentationQueue` plays batches in order: dice, then walks, then hits, then KOs.
   - It exposes `IsBusy`. Input and `BotDriver` wait on it.
   - Placement still settles, never walks (PRESENTATION §3).
   - The alternative is to keep everything concurrent, which reads poorly once bots chain actions.
3. **Juice budget.**
   - Hop per cell with a little squash.
   - Deploy = rise (bust to pawn, with a pop).
   - KO = shatter in the seat colour, then the figure reappears seated.
   - Hit-stop of about 60 ms and a small camera nudge on big hits.
   - Cast tell: a cyan sweep on the caster, then a line or arc to the target, or a drop onto the cell for beacons.
   - Idle breathing for standing figures, a slow sway for seated ones.
   - Recommended: all of the above, kept subtle.
4. **Accessibility.** Add a **Reduced motion** setting (no shake, no hit-stop, shorter tweens) and an animation speed (Normal / Fast). Both remembered.

### Increments

- **MO1 — queue and dice**
  - New `View/PresentationQueue.cs` and `View/DiceRoller.cs` (world-space or canvas dice with a tumble).
  - `MatchBootstrap.Handle` hands batches to the queue.
  - The tray's dice animate in when the roller finishes.
  - Keys and board clicks wait while `IsBusy`, with a short input buffer for Space and E.
- **MO2 — pieces, hits, casts, idle**
  - Hop walk, rise, KO return, hit-stop and nudge, cast tell, idle breathing.
  - The Reduced motion and speed settings.
  - Update PRESENTATION with a new §3.1 on sequencing.
  - **Hooks for Stage 5:** the queue raises named beats (`DiceLanded`, `Step`, `Hit`, `KO`, `CastTell`, and so on) that audio can subscribe to.

### Done when

- A new player notices the roll without being told.
- A bot turn reads step by step.
- Nothing overlaps in a way that hides a result.
- Pause freezes everything except the HUD pulses.
- Reduced motion works.

### Start prompt for the Stage 3 chat

> Stage 3 (Motion and feedback) from `claude/NEXT_PHASES.md`. Read it and the Stage 2 log (`claude/BOTS.md`), request folder access, check HEAD, then the Stage 3 decisions — the dice moment first.

---

## Stage 4 — Art hookup and ADR-0009 (can run any time)

**Stage doc:** ADR-0009 plus an `ART_PIPELINE.md` amendment. The code log goes in `docs/design/ART_HOOKUP.md`.

### Goal

Real operator art replaces the procedural figures one operator at a time, and the procedural shapes remain as the fallback. The 3D-to-2D route is written down as a decision.

### What exists

- **ADR-0001:** 2D is locked, and 3D is "concept only".
- **`ART_PIPELINE.md`:**
  - §1 names the 2.5D route (model in 3D, render to 2D) as "available, not adopted; its own ADR".
  - §4 specs are NEEDS DECISION (PPU 100, operator 256×384, bilinear, atlases per class, no normal maps on operators).
  - §6 lists the required character content: seated, standing, rise, ability tell, and KO/return.
  - §8 licensing is NEEDS DECISION.
- **`ART_PROMPTS.md` (project):**
  - Figures on a flat grey background, pivot at the feet, no floor shadow (the engine draws it).
  - The camera looks down at about 25° from a three-quarter view.
  - The seat colour is drawn by code, so one figure serves every seat.
- **`OperatorPiece`:**
  - Procedural bust and pawn with outline, a gold shape pin (`PieceShape` by name), and a selection glow plus halo.
  - The health bar hides while seated. `FigureScale` is 1.45.
- **Luka:** look settled, and four crops ready for Meshy multi-view.

### Decisions to settle first

1. **ADR-0009: adopt 2.5D production (recommended).**
   - Pipeline: Meshy multi-view image-to-3D → Blender.
     - Proportion fix to **6 heads**: head about ×1.3, shorter legs.
     - Clean-up, then rig (Rigify or Mixamo).
     - Poses: seated, standing ready, rise frames, tell, KO.
   - Render: a fixed orthographic camera at the board angle, with a toon shader plus a Freestyle outline (#1C0E12), a warm key from the upper left and a cool rim, to PNG.
   - The ADR amends ADR-0001's "3D is concept only" for **production of 2D sprites** (the game stays 2D) and ART_PIPELINE §1–§2.
   - Record the render settings as a reusable `.blend` template.
2. **Specs (ART_PIPELINE §4).**
   - Recommended: source renders at **512×768** (sharp on tablets and phones), imported at **PPU sized so a standing figure matches today's `FigureScale`**, pivot bottom-centre, bilinear, one `Operators` atlas, no normal maps on operators.
3. **Loading.**
   - **Recommended:** an `OperatorArt` ScriptableObject per operator (seated, standing, portrait, optional rise and KO frames), found by name through a `Resources/OperatorArt/` folder. This matches "no scene wiring".
   - The alternative is a catalogue asset referenced from `MatchBootstrap`.
4. **Seat identity with real art.**
   - Keep the tinted base disc and ring under the figure, and the shape pin.
   - Recommended: pin at the figure's chest; alternatively float it beside the health bar.
   - The figure itself is never tinted.
5. **Licensing (ART_PIPELINE §8).** Check Meshy's commercial terms on the designer's plan and record them. Start the per-asset provenance manifest.

### Increment

- **ART1**
  - `Unity/Data/OperatorArt.cs` (ScriptableObject) and `View/OperatorArtLibrary.cs` (lookup and cache).
  - `OperatorPiece` uses the sprites when present: seated in the yard, standing on the floor. The glow, halo and target ring are sized from the sprite bounds. Otherwise it falls back to the procedural figure.
  - The portrait appears on the draft card (Stage 1) and the end screen.
  - ADR-0009 is written, and ART_PIPELINE §1, §2, §4 and §8 are updated.
  - Import checklist: ART_PIPELINE §9.
  - First drop: Luka.

### Done when

- Luka shows in the yard and on the floor from real renders, readable at gameplay zoom and on a phone-sized Game view.
- Every other operator still draws procedurally.
- The ADR is accepted.

### Start prompt for the Stage 4 chat

> Stage 4 (Art hookup + ADR-0009) from `claude/NEXT_PHASES.md`. I have [describe the Meshy/Blender output]. Read the stage, `claude/ART_PROMPTS.md` and `claude/OPERATORS.md`, request folder access, check HEAD, then the Stage 4 decisions.

---

## Stage 5 — Audio

**Stage doc:** `docs/design/AUDIO.md`

### Goal

The game sounds like a casino after hours: dice, steps, hits, casts and UI clicks, plus short voice lines per operator (move, kill, death, quit, victory, and so on), with music for the title and the match. Everything is adjustable and remembered.

### What exists

- **Nothing.** No `AudioSource` anywhere.
- **Hooks:** event batches arrive in `MatchBootstrap.Handle`, and Stage 3 adds named beats on the presentation queue.
- **Settings:** `SettingsRows`, `SettingsStore` and `UiKit.ToggleRow` exist. **There is no slider widget.**

### Decisions to settle first

1. **Structure (recommended):**
   - `AudioDirector`: one MonoBehaviour with pooled sources, fed by queue beats and UI hooks.
   - `SoundBank`: a ScriptableObject of cue id → clips, with volume and pitch variation.
   - `VoiceSet`: one per operator, found by name like `OperatorArt`.
   - Mixer groups: Music, SFX, Voice, UI.
2. **Voice rules.**
   - One voice at a time, with priority: victory > death > kill > cast > deploy > move.
   - Per-operator cooldown, and move lines play by chance (about 30%), not on every move.
   - "Quit" plays when a match is abandoned through MAIN MENU.
   - Recommended slots: `deploy`, `move`, `cast` (generic, plus optional per-ability), `hit_taken`, `kill`, `death`, `victory`, `quit`.
3. **Music.** A title loop, a match loop, and a win sting. Duck the music under voice lines. In pause, keep the music playing at reduced volume, with SFX paused.
4. **Sources.**
   - Placeholder SFX from a CC0 library; record provenance, like the art.
   - Voice by actor or TTS. Record the licence before anything ships.
5. **Settings.** Sliders for Master, Music, SFX and Voice (a new `UiKit.SliderRow`), plus a Mute toggle. All remembered.

### Increments

- **AU1:** `AudioDirector`, `SoundBank`, the UI click hook in `UiKit.Button`, SFX on the Stage 3 beats, music, and the settings sliders.
- **AU2:** `VoiceSet` with the priority and cooldown rules, starting with Luka's line list and placeholder reads.

### Done when

- A match is fully sounded with placeholders.
- Voice lines never pile up.
- Volume settings persist, and pause behaves as decided.

### Start prompt for the Stage 5 chat

> Stage 5 (Audio) from `claude/NEXT_PHASES.md`. Read it and the Stage 3 log (`claude/MOTION.md`), request folder access, check HEAD, then the Stage 5 decisions.

---

## Still open, from earlier hand-offs (surface, don't act without a decision)

**Design**

- Palette lock: a visual sign-off on the ART_DIRECTION §3 hexes, especially holo cyan. After sign-off, only `UiTheme.cs` changes.
- ADR-0007 rider: does it apply when the zone is anywhere, or only when Nuetu stands in it?
- Javi's Neural Purge range is one short of Mimi's 6.
- Luka calls to confirm:
  - both of Blind Spot's hits are Normal;
  - the follow-up ignores safe cells and stealth;
  - the follow-up misses if Luka has been yarded.
- Archetypes still to write: "Duelist", "Brawler", "Engineer". Also open: a warded Luka against Mimi, and the tech level.
- Kian and Nuetu are contractors. Backstories are deferred.

**Code and tooling**

- `FeedbackLayer`: zero-day damage at upkeep is still unlabelled (a one-word fix; fold it into Stage 3).
- Nothing has been simulated with Luka, Sanity, Kian, Nuetu or Javi in a squad. The Stage 2 `bots` sim mode can read this.
- `StatusPalette` keeps its own colours outside `UiTheme`.
- The formal stranger test (K) runs when two testers are available. After a pass, delete OnGUI in one commit.
