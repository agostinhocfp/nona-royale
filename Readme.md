# Nona Royale

A strategic 2D dice-rolling board game where operators use tech-enabled abilities to outmaneuver and defeat opponents on the race home.

Unity 2D (URP, Universal 2D template) · C# · solo development.

---

## Read this first

The project was restarted once because rules lived in several places at once and the copies disagreed — the energy formula existed twice with different thresholds, and the board size existed as three different numbers across the GDD and the code. **Every document below owns exactly one subject, and no other document restates it.** If you find the same number in two files, one of them is a bug.

| Subject                                               | Owner                           | Everything else      |
| ----------------------------------------------------- | ------------------------------- | -------------------- |
| What the game is                                      | `docs/PROJECT_IDENTITY.md`      | points here          |
| Match setup, modes, session flow                      | `docs/GDD.md`                   | points here          |
| Board geometry and size                               | ADR-0002, ADR-0003              | points here          |
| Combat, energy, status, targeting, roster **numbers** | `docs/design/COMBAT_SYSTEMS.md` | points here          |
| Operator flavour, archetype, roster scope             | `docs/design/OPERATORS.md`      | no numbers live here |
| Architecture                                          | ADR-0004, `CONVENTIONS.md`      | points here          |
| What it looks like                                    | `docs/art/ART_DIRECTION.md`     | points here          |
| How art gets made                                     | `docs/art/ART_PIPELINE.md`      | points here          |

Decisions are recorded as ADRs in `docs/decisions/`. An ADR is never edited to change a decision — it is amended with a dated section, or superseded by a new ADR. The history is the point.

---

## Architecture in one paragraph

Rules live in a pure-C# core (`NonaRoyale.Core`) that **cannot compile against Unity** — its assembly definition sets `noEngineReferences: true`, so the wall is enforced by the compiler, not by discipline. The core holds all state and all rules, is deterministic under an injected seed, and is reached through commands in and events out. Unity is a thin presentation layer: it renders state, translates input into commands, and owns nothing rules-related. See ADR-0004.

The test for where code goes: _does this decide what happens in the game, or just show it?_ Deciding is core. Showing is view.

## Assemblies

| Assembly                 | Location                         | References                         |
| ------------------------ | -------------------------------- | ---------------------------------- |
| `NonaRoyale.Core`        | `Assets/_Project/Scripts/Core/`  | **nothing** — not even UnityEngine |
| `NonaRoyale.Unity`       | `Assets/_Project/Scripts/Unity/` | Core, UnityEngine                  |
| `NonaRoyale.Core.Tests`  | `Assets/Tests/EditMode/`         | Core, NUnit                        |
| `NonaRoyale.Unity.Tests` | `Assets/Tests/PlayMode/`         | Core, Unity, NUnit                 |

---

## Getting set up

1. Open the project folder in Unity Hub. First open rebuilds `Library/` — it is this project's `node_modules`, generated from `Assets/`, and is never committed.
2. Confirm three settings that are **not** Unity defaults:
   - Project Settings → Editor → **Asset Serialization: Force Text** (without it, scenes are binary blobs and unmergeable)
   - Project Settings → Editor → **Version Control: Visible Meta Files**
   - Project Settings → Player → **Api Compatibility Level: .NET Standard 2.1**
3. `git lfs install` once per machine. Source art and audio are tracked via LFS (see `.gitattributes`).
4. Window → General → Test Runner → **EditMode** → Run All. Green is the baseline; a red EditMode suite means the core is broken regardless of what the game looks like.

## Testing

Every core rule ships with an EditMode test, named by behaviour, deterministic under a seeded RNG. **A rule without a test isn't done.** The full expected suite is enumerated in `COMBAT_SYSTEMS.md` §13 — it doubles as the implementation task list.

PlayMode tests cover runtime concerns only: input wiring, animation, rendering. Never rules.

## Simulation harness

`tools/sim/` holds a headless Monte Carlo harness that plays full matches under the combat rules with scripted policies, to measure pacing and combat throughput.

```
cd tools\sim
python nona_sim.py        REM speed bands and board profiles
python sensitivity.py     REM which rule actually drives match length
```

Every measured figure in ADR-0002 Amendment 2 came from this. **Caveat:** it is currently a Python _model_ of the rules, not an execution of them, so it can drift silently. It should be replaced by a console project driving the real core once that exists, at which point it stops being a model and becomes an integration test that also reports statistics.

It measures pacing and throughput. It says nothing about whether the game is fun.

## Commit conventions

Small, focused commits, present-tense summary (`add energy ledger`, `fix capture on safe cell`). **Never commit a rule change without its test in the same commit.** See `CONVENTIONS.md`.
