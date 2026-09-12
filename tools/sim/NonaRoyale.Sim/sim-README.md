# Simulation harness

Headless Monte Carlo against the real rules. Every figure in ADR-0002 Amendments 3 and 4 and `COMBAT_SYSTEMS` §12 came from here.

> **The current figures are not trustworthy.** ADR-0002 Amendment 5 found that Kurbyn's passive speed bonus never reached the engine, so every match ever simulated ran him at his base speed. Band labels below and in the ADR are effective speeds; the matches used base speeds with Kurbyn 0.5 lower. Relative comparisons within a sweep survive — the fault was constant across rows — but every absolute turn count, p90, neutralize count and occupancy figure is withdrawn until the harness is re-run on the fixed core.

## Running

From the **repository root**, not from this folder — the project pulls the core sources from a path relative to the root.

```
dotnet run --project tools\sim\NonaRoyale.Sim -- 800
dotnet run --project tools\sim\NonaRoyale.Sim -- 800 laps
dotnet run --project tools\sim\NonaRoyale.Sim -- 800 opening
dotnet run --project tools\sim\NonaRoyale.Sim -- 800 reach
```

The `--` separates `dotnet`'s own arguments from the program's. Without it the match count never reaches `Main`.

| Sweep     | What it varies                                            |
| --------- | --------------------------------------------------------- |
| _(none)_  | Speed band, board profile, player count, collision damage |
| `laps`    | Loop size against lap count                               |
| `opening` | Operators pre-deployed, and ability reach                 |
| `reach`   | Ability reach on the adopted configuration, both profiles |

Roughly a minute per sweep at 800 matches per row.

## Reading the output

| Column  | Meaning                                                 |
| ------- | ------------------------------------------------------- |
| `turns` | Turns **per seat**, which is what ADR-0002 quotes       |
| `p90`   | 90th percentile — the long-match tail                   |
| `neut`  | Neutralizes per match, all players                      |
| `abil`  | Abilities resolved per match                            |
| `coll`  | Collisions per match                                    |
| `burn`  | Energy destroyed by the cap                             |
| `3-up`  | Share of turns with a player's whole squad on the board |

## Baseline check

**Currently unset.** Seeds are fixed, so one known-good row is what tells you the core moved under you — but the reference row has to come from a core that is right, and this one has not been re-run since Kurbyn's passive was wired in.

The previous row read _16.8 turns, 4.0 neutralizes, 12% 3-up_. It is retired twice over: it was measured at the Amendment 2 band, which Amendment 4 replaced, and at `openingDeployments = 0`, which is no longer the adopted configuration. Do not restore it.

To set a new one, from the repository root:

```
dotnet run --project tools\sim\NonaRoyale.Sim -- 800
```

Take the `Standard 48/6` row of the standard table and record `turns`, `neut` and `3-up` here. Re-record it whenever a rule changes deliberately — a tripwire that is expected to be wrong teaches people to ignore it.

Three things to settle before that row is worth trusting, in order:

1. **Kurbyn's passive is live and survives neutralize.** Fixed in the core; confirm the band labels and the `Move %` column now describe the match that actually ran.
2. **Slow stacking is ruled on.** `GameEngine` sums the status and aura speed channels, so Slow plus Intimidating Presence reaches the speed floor — which `COMBAT_SYSTEMS` §5.2 says should not happen. `ScriptedPlayer` fires whatever is affordable at whatever is nearest, so this condition occurs constantly and distorts every row.
3. **The mark is modelled.** It adds a damage source arriving at upkeep, where it can neutralize before a turn happens. No existing figure accounts for it.

## What it is and is not

**It is an integration test that reports statistics.** It drives `GameEngine` through commands and counts events, exactly as the Unity view does. It touches no service directly and holds no copy of the rules, so it cannot drift from them. A rule that throws under ten thousand matches was never going to survive a playtest — the stacked-collision contradiction in §7.5 was found on the first run.

**It cannot catch a rule that is wired to nothing.** The Kurbyn fault ran undetected through every sweep in three amendments, because a passive that is never applied throws no exception and produces a perfectly plausible number. The harness proves the rules do not contradict each other; it does not prove they are all connected.

**It is not a balance oracle.** `ScriptedPlayer` deploys whatever it can, fires whatever is affordable at whatever is nearest, and advances its leader. A competent human plays better, so turn counts are a mild over-estimate. What the harness measures reliably is the _relative_ effect of a configuration change, not an absolute figure.

**It says nothing about whether the game is fun.** Pacing and throughput only.

## Predecessor

`nona_sim.py` and `sensitivity.py` modelled the rules in Python before the core existed. They flattened every ability into damage at range 3 and overstated lethality by roughly half. Kept for history; do not trust their numbers.
