# Nona Royale — Stage 6: Deterministic Replays

> Location in repo: `docs/design/REPLAY.md` · Project copy: `claude/REPLAY.md`
> Status: **RP1a built and passing in Unity 2026-09-23; commit pending.** RP1b, RP2 and RP3 not started.
> Related: `NEXT_PHASES.md` (stage rules), `PRESENTATION.md`, `CONVENTIONS.md`, `COMBAT_SYSTEMS.md`, ADR-0004 (core architecture), ADR-0012 (sides)

## Why this stage

`GameEngine`'s own remarks say that a seed plus the command stream reproduces the whole match. Bots draw from their own stream (`seed ^ SeedSalt`), and the view's dice tumble uses the view's RNG, so neither one changes the result. A replay is therefore a header plus the list of accepted commands. A full two-seat match is about 9 KB.

Replays were picked over two other candidates (a combat forecast / threat map, and a win-probability graph) because both of those build on them. What replays give us, most certain first:

1. **Proof that the core is deterministic.** The headline test replays seeded bot matches and compares the event streams. If it fails, the core has hidden nondeterminism, and knowing that matters more than the feature itself.
2. **A regression corpus.** A stored replay that desyncs after a rules change is a failing test that points at the change.
3. **Bug reports from playtesters.** "Send me the file" instead of "what did you do?".
4. **Save and resume** (RP3), and **replay to a position**, which the win-probability rollouts need later.
5. A viewer with pause, step and 2x (RP2).

## Decisions

### Designer, 2026-09-22

| Decision | Choice |
| --- | --- |
| RP1 scope | **Core only**: recorder, format, replayer, tests. No viewer UI. |
| Compatibility guard | **Content hash** of the configs and the roster, stamped in the file. Playback is **refused** on a mismatch. |
| Capture policy | **Auto-record every match**, keeping the **last 20** in a ring buffer. |
| Recording hook (after the seam check) | **One engine callback**, `GameEngine.Executed`, rather than an observer at each call site. |
| Match construction (after the seam check) | **`MatchRecipe` in the core.** The view and the replay build every match through the same code. |

### Claude, stated rather than asked

- The format is **JSON written by a hand-rolled writer in `Core`**. The core holds no Unity types, so `JsonUtility` is out. No JSON helper existed in the core before this stage.
- **One JSON object per line.** A file cut short by a crash still replays up to its last complete line, and that is exactly the file a crash report would carry.
- **Only accepted commands are recorded.** A rejected command changes nothing, and the seam check confirmed that (below).
- **The output is pure ASCII.** Anything outside printable ASCII is escaped as `\uXXXX`, so `Revú` is written `Revú`. The file reads the same under any encoding, BOM or no BOM.
- **Numbers are whole.** The reader refuses fractions and exponents, which keeps doubles and locales out of the file.
- **Operator ids stay plain ints.** They are safe because the header pins seat order and squad order (seam 3).
- **The fingerprint walks the roster in roster order**, not sorted by name (the plan said sorted). A random squad is drawn by index into `Roster.All`, so reordering the roster changes every random match, and the hash has to notice that.

## Seam check against the tree (2026-09-22, HEAD `3a9db82`)

The plan was written without repo access. Here is what the tree showed:

1. **Acceptance signal.** `Execute` returns the event list. Every one of the 21 rejection paths adds a single `CommandRejected` and returns before touching any state. "No `CommandRejected` in the list" is therefore an exact acceptance test. There are several `Execute` call sites (two in `MatchBootstrap`, plus `BotTable` and the tests), so the designer chose one engine callback over observers at each site.
2. **Command fields.** Every field already had a public getter. Commands carry no seat, because the engine acts for the current player. The replay records the seat anyway, as a desync check.
3. **Operator identity.** `MatchFactory` hands out ids `1..N` in seat order, then squad order. The order of `Roster.All` doesn't affect them. Since the header pins seats and squads in order, ints are stable.
4. **`AbilityEffect`** is a readonly struct with 23 public getters, and the configs expose getters too. The fingerprint dump is written by hand, and a reflection test catches any property it misses.

**A seam the plan didn't foresee: how a match is built.** `MatchBootstrap` picked one of three factory calls:

- **Random** passes `squads: null`, and the factory **draws the squads from the match RNG**. A replay that passed those same squads explicitly would skip the draw, and every die after it would differ. `Recipe_NamingTheDrawnSquads_ShiftsTheDice` shows this happening.
- **Alpha** goes through `CreateAlphaMatch`, which applies the `RosterSpeeds` overrides.
- The **board** (the view's compact board is `Cross("Compact", 3, laps: 2)`, not a preset), **`openingDeployments`** (2 by default in the inspector) and the **`TeamMap`** also shape the match. The plan's header left all of them out.

`MatchRecipe` now carries all of these, and `Build()` makes the factory call. In RP1b, `MatchBootstrap` builds through a recipe too, so the live match and its replay come from the same code.

**Baseline before RP1a:** 802 passed and 0 failed in the CLI harness (core plus EditMode, excluding `Tests/EditMode/Unity/`). The four reds the previous hand-off listed now pass.

**Nondeterminism scan:** no clock reads and no ad-hoc `System.Random` in the core. The one dictionary iteration in `MatchFactory` doesn't depend on order. The 60-match determinism test passes.

## RP1a — what was built

### The engine seam

`GameEngine.Executed` (`Action<PlayerColor, ICommand, IReadOnlyList<IGameEvent>>`) fires once at the end of every `Execute`, whether the command was accepted or refused. It passes the seat whose turn it was **when the command arrived**, captured before an end-turn hands over. The body of `Execute` moved into `ExecuteCore`, and nothing else in the engine changed.

### Files

```
Assets/_Project/Scripts/Core/MatchRecipe.cs              // SquadSource + MatchRecipe: one Build() for view and replay
Assets/_Project/Scripts/Core/GameEngine.cs               // + Executed event (modified)
Assets/_Project/Scripts/Core/Replay/Json/JsonNode.cs     // null, bool, whole numbers, strings, arrays, ordered objects
Assets/_Project/Scripts/Core/Replay/Json/JsonWriter.cs   // compact, single line, pure ASCII
Assets/_Project/Scripts/Core/Replay/Json/JsonReader.cs   // strict: no trailing text, fractions or duplicate keys
Assets/_Project/Scripts/Core/Replay/Json/JsonFormatException.cs
Assets/_Project/Scripts/Core/Replay/EnumText.cs          // enums by exact name, nothing else
Assets/_Project/Scripts/Core/Replay/CommandCodec.cs      // one writer + one reader per command
Assets/_Project/Scripts/Core/Replay/RecipeCodec.cs       // recipe <-> header object; board geometry, sides per seat
Assets/_Project/Scripts/Core/Replay/ReplayHeader.cs
Assets/_Project/Scripts/Core/Replay/ReplayEntry.cs
Assets/_Project/Scripts/Core/Replay/ReplayFile.cs
Assets/_Project/Scripts/Core/Replay/ReplayWriter.cs
Assets/_Project/Scripts/Core/Replay/ReplayReader.cs      // tolerates a cut tail and CRLF, nothing else
Assets/_Project/Scripts/Core/Replay/ReplayRecorder.cs    // listens to Executed, keeps accepted commands, LineRecorded for appends
Assets/_Project/Scripts/Core/Replay/ReplayPlayer.cs      // Play / PlayTo, + ReplayResult
Assets/_Project/Scripts/Core/Replay/RulesFingerprint.cs
Assets/_Project/Scripts/Core/Replay/ReplayExceptions.cs  // Format, Incompatible, Desync
```

### File format — `.nrr`, envelope format 1

Line 1 is the header. Each later line is one accepted command, in order. Every line ends in `\n`, including the last one. A real file:

```
{"format":1,"rules":"f7820c60","created":"2026-09-22T00:00:00Z","recipe":{"seed":6,"seats":["Red","Green"],"source":"Drafted","board":{"name":"Standard","circuit":52,"home":6,"laps":1},"opening":0,"teams":[0,1,2,3],"squads":{"Red":["Nuetu","Sanity","Luka"],"Green":["Lethe","Revú","Fortuna"]}},"fielded":{"Red":["Nuetu","Sanity","Luka"],"Green":["Lethe","Revú","Fortuna"]}}
{"n":1,"seat":"Red","cmd":"Roll"}
{"n":2,"seat":"Red","cmd":"Deploy","op":1}
{"n":3,"seat":"Red","cmd":"Move","op":1}
{"n":4,"seat":"Red","cmd":"End"}
```

- **`recipe.squads`** appears only for `Drafted`. Random and Alpha draw their own squads, and naming them would change the dice.
- **`fielded`** is always written. It records who played without replaying the file, and it doubles as a check: if the rebuilt squads differ, the replay desyncs at command 0.
- **`teams`** holds one team number per seat, Red to Violet. The three named maps read back as themselves (`AreSame`).
- **`labels`** (optional) is display text per seat, such as `"CPU · BRAWLER"`. The player never reads it.
- Command wire forms: `Roll`, `Deploy{op}`, `Move{op, die?}` (no `die` means the pooled move), `Cast{op, ability, target?, cell?{k, o?, i?}}`, `Cash{op, die}`, `End`.
- **`format`** is the envelope (the shape of the file). **`rules`** is the content guard. The two move independently.

### Reading rules

- Text after the last `\n` is the only part that can be a partial line. If it doesn't parse, it is dropped and `Truncated` is set. If it does parse, the crash landed between `}` and `\n`, and the line is kept.
- Everything else throws a `ReplayFormatException` that names the line: a bad line in the middle, a gap in `n`, a blank line, an unknown command (even on the last line), an unknown enum name, or a name that doesn't match exactly. A newer `format` throws `ReplayIncompatibleException`.

### Playing

`ReplayPlayer.Play(file)` and `PlayTo(file, n)`, where `n = 0` is the match as dealt. Both return `ReplayResult` (the match, every event from `Start` on, the number of commands played, and `Truncated`). A rules-hash mismatch throws `ReplayIncompatibleException` naming both hashes. Rebuilt squads that differ, a command recorded for the wrong seat, or a command the engine refuses all throw `ReplayDesyncException` with `Sequence`. **Nothing partial is ever returned as if it were whole.**

### Rules fingerprint

`RulesFingerprint.Current` is FNV-1a 32 over a canonical dump of every dial in `GameConfig`, `CombatConfig` and `EnergyConfig`, the `RosterSpeeds`, `Roster.SquadSize`, and every operator in roster order. For each operator it covers health, speed, both passives, the haste cap, the aura, and each ability in id order with its effects in declared order. **Text is left out** (names, descriptions, passive and aura copy), so rewording a kit doesn't invalidate replays. Doubles are written as invariant text plus their exact bits.

**The golden hash is `f7820c60`.** `Fingerprint_MatchesTheGoldenHash` fails on any rules change. When it does, update the constant **on purpose**: every replay recorded before that change will be refused from then on.

### Tests — `Assets/Tests/EditMode/Replay/`, 57 new

| File | Covers |
| --- | --- |
| `ReplayDeterminismTests` (11) | **60 seeded bot matches** (20 seeds x 2/3/4 seats, all personalities, all three squad sources, crossed pairs, both boards, 0–2 opening deploys) replayed **through text** to an identical event stream and final state; the recorder doesn't touch the RNG; `PlayTo`; the `Executed` seat through end-turns; every refusal emits only itself; tampering, wrong seat, forged squads and commands past the end all desync with the right `n` |
| `ReplayFileTests` (14) | Text round trip, pure ASCII, `Revú` and `·` escapes, truncation at every cut point, a complete tail without its newline, header-only files, bad or missing lines, unknown commands, newer format, CRLF |
| `RulesFingerprintTests` (8) | Golden hash, FNV-1a vectors, a comma-decimal culture, **every constructor dial moves the hash** (33), roster health / cost / effect / order / size, text ignored, **every public rules property named in the dump** (reflection) |
| `MatchRecipeTests` (10) | Random, Alpha and Drafted match the factory; naming drawn squads shifts the dice; bad recipes refused; the recipe codec round-trips every source x 3 boards x 4 side maps |
| `CommandCodecTests` (6) | Every command round-trips; **every concrete `ICommand` in the core has a case**; the documented wire form; unknown or missing fields fail loudly |
| `JsonTests` (8) | Round trip, escapes, strictness, depth limit, typed getters |

**Result (CLI harness, .NET 8, C# 9):** 859 passed and 0 failed, which is 802 + 57. The whole suite runs in about 2.5 s.

## RP1b — Unity glue (Play Mode only), next

- **`MatchBootstrap` builds through `MatchRecipe`.** `SquadMode.AllPick`/`Snake` becomes `Drafted`, `Random` stays `Random`, and `Alpha` stays `Alpha`. The recipe takes `Board`, `openingDeployments` and `_seatPlan.Teams`. Note: the drafted branch currently lets the factory draw for "a seat the draft did not cover". `MatchRecipe` refuses a drafted recipe with a gap, so any such gap will show up as an exception instead of a silent draw.
- `Assets/_Project/Scripts/Unity/Replay/ReplayStore.cs` writes to `Application.persistentDataPath/replays/`, names files `yyyyMMdd-HHmmss-<seed>.nrr`, and prunes to the newest 20 when it opens.
- `MatchBootstrap` creates a `ReplayRecorder` in `NewMatch`, **before** `Start()`. It writes `HeaderLine` straight away and appends on `LineRecorded` (append, never rewrite), so a crash keeps the tail. It disposes the recorder on teardown and reseed. It passes `DateTime.UtcNow.ToString("o")` as `createdUtc`, and seat labels from `SeatTag`.
- `SAVE REPLAY` on the end screen copies the current file into `saved/`, which the ring buffer doesn't touch.
- No settings row. Recording is always on.
- Check on Android that `persistentDataPath` is writable and that files don't pile up somewhere unexpected across installs.

## Later

- **RP2 — viewer:** feed a file through the `PresentationQueue`, with pause, step and speed. `PlayTo` is the rewind.
- **RP3 — save and resume:** resume from a ring-buffer file with `PlayTo(file, last)`, then hand control back.
- A **regression corpus**: check in a few `.nrr` files and replay them in a test. Every deliberate rules change then has to re-record them or accept that they are refused, which the golden hash already forces anyway.

## Done when

- [x] A seeded bot match replays to an identical event stream, for every seat count and personality (CLI).
- [x] A replay recorded before a config change is refused, naming both hashes (CLI).
- [x] A truncated file plays to its last complete line (CLI).
- [x] The recorder doesn't touch the match RNG (CLI).
- [x] The Unity Test Runner shows all 57 replay tests passing, `Fingerprint_MatchesTheGoldenHash` included, **under Mono** (the cross-runtime check). The run's six reds are all in `RigPieceTests`, not in replay code (log, 2026-09-23).
- [ ] (RP1b) Twenty matches leave exactly twenty files, and a pinned one survives.

## Standing rules (carried from `NEXT_PHASES.md`)

1. Don't trust the project snapshot; read the repo.
2. Check HEAD first (`.git/logs/HEAD`, last line).
3. The view computes nothing. The core has zero Unity types and no LINQ.
4. Constants are config, not literals. No singletons; `MatchBootstrap` is the composition root.
5. Every core rule ships with EditMode tests.
6. Whole files where possible, each starting with a comment giving its path.
7. Log in this doc **before** the commit. Hand over `git add <explicit paths>` plus `git commit -m "type(scope): subject" -m "body"`, **with no AI trailers**. Include the `.meta` files of new scripts and folders: Unity writes them when the editor gets focus, so focus the editor before staging.
8. **Line endings are mixed.** About half the core `.cs` files are LF and half CRLF, and most have no final newline. Preserve each existing file as it is. New `.cs` files in this stage are CRLF with no final newline, and docs are LF with a final newline. Check a file with `tail -c 2 | od -c`.
9. Play Mode on Windows is the acceptance test, and it is the only check for RP1b.
10. If code travels between machines, deliver it as a patch against a checked HEAD. The Fortuna tar was once built on a stale base and overwrote 28 commits of work.
11. **The session VM has no git-lfs.** Running `git status` there rewrites the index and leaves an `index.lock` behind that it can't delete. Read git state on Windows only.

## Log

- **2026-09-22 — stage opened.** Feature chosen from a shortlist of three. The other two (combat forecast / threat map, win-probability recap) are parked as candidates. Three decisions taken (table above). No code written, no repo read.
- **2026-09-22 — seams checked, RP1a built.** HEAD `3a9db82 feat(guide): passives and auras on every screen that shows a kit`. The working tree had no core changes; Fortuna, Mimi and Kian are all committed. The baseline was 802/0 green. The seam check found the match-construction trap described above, and the designer chose `GameEngine.Executed` plus `MatchRecipe` (both recommended). Built: `MatchRecipe`, the `Executed` hook, and `NonaRoyale.Core.Replay` (JSON subset, codecs, header, reader and writer, recorder, player, fingerprint). **The determinism proof passed on the first run** across 60 bot matches, so no hidden nondeterminism turned up. Tests went to 859/0 in the CLI harness. Golden rules hash `f7820c60`. Waiting on the Unity Test Runner (Mono) and the commit.
- **2026-09-23 — Unity Test Runner (Mono).** 1459 tests: 1451 passed, 6 failed, 2 inconclusive. **All 57 replay tests pass**, including `Replay_OfSeededBotMatch_ProducesIdenticalEventStream` (0.97 s) and `Fingerprint_MatchesTheGoldenHash`, so `f7820c60` is the same under Mono and .NET 8. The 6 failures and 2 inconclusives are all in `NonaRoyale.Unity.Tests.View` (`RigPieceTests`, one `OperatorLookBookTests`), and none of them involve replay code. Cause: the untracked Bouncer renders (`Art/Resources/Art/Operators/bouncer_{portrait,seated,standing}.png`, dated 2026-09-22 01:44, before this stage started) give Bouncer a real render, so the rig tests that bind Bouncer find no rig. `Bouncer_IsDrawnAsARig_SeatedAndStanding` already says so in its own `Assume`: "point this test at a rigged operator without one." This belongs to the art hookup, not to RP1a. RP1a is committed without those PNGs.
