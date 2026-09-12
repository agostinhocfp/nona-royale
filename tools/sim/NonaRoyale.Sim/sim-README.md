# Simulation harness

Headless Monte Carlo against the real rules. Every figure in ADR-0002 Amendment 3 and `COMBAT_SYSTEMS` §12 came from here.

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

Baseline check: the standard table's `Standard 48/6` row should read **16.8 turns, 4.0 neutralizes, 12% 3-up**. Seeds are fixed, so a different figure means something changed in the core — which is worth knowing either way.

## What it is and is not

**It is an integration test that reports statistics.** It drives `GameEngine` through commands and counts events, exactly as the Unity view does. It touches no service directly and holds no copy of the rules, so it cannot drift from them. A rule that throws under ten thousand matches was never going to survive a playtest — the stacked-collision contradiction in §7.5 was found on the first run.

**It is not a balance oracle.** `ScriptedPlayer` deploys whatever it can, fires whatever is affordable at whatever is nearest, and advances its leader. A competent human plays better, so turn counts are a mild over-estimate. What the harness measures reliably is the _relative_ effect of a configuration change, not an absolute figure.

**It says nothing about whether the game is fun.** Pacing and throughput only.

## Predecessor

`nona_sim.py` and `sensitivity.py` modelled the rules in Python before the core existed. They flattened every ability into damage at range 3 and overstated lethality by roughly half. Kept for history; do not trust their numbers.
