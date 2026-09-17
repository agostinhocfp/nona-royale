# Nona Royale — Project Identity

> Location in repo: `docs/PROJECT_IDENTITY.md`
> Status: **Locked** — the top-level "what is this game." Every other doc hangs off this.
> Related: `docs/GDD.md` (rules), `docs/art/ART_DIRECTION.md` (look), `docs/design/OPERATORS.md` (roster), ADRs 0001–0003.

_"Nona Royale" is the canonical title (ADR/ART_DIRECTION §0). "Mystic Heist" and any other names are dead codenames._

---

## Core identity statement

A strategic 2D dice-rolling board game where operators use tech-enabled abilities to outmaneuver and defeat opponents on the race home.

## One-sentence Reason for the audience to care

A stylish near-future casino where operators manipulate, bluff and betray each other to control the tables – and the house always has a trick.

## What this game is

A competitive multiplayer digital board game that blends tactical decision-making with dice-driven movement. Each player commands a squad of **three operators** (chosen from a pool of nine), navigating a modular grid board while deploying abilities, managing energy, and using positioning to eliminate rivals and get all their operators home safely.

The board is a **1:1 Ludo/Parcheesi track** (ADR-0003); the originality is the tactical combat layer built on top of it, not the movement itself.

## Core pillars

- **Tactical depth.** Every move matters — which operators you pick, how you spend energy, when you time abilities. A single well-placed ability can swing a match.
- **Dice-driven dynamics.** Embrace the chaos of the roll while keeping strategic control through operator stats, energy, and ability timing. Risk and reward, not pure luck.
- **Competitive edge.** Outthink up to three opponents in fast, positional matches where a full game runs ~15–20 minutes (per the board-size target, ADR-0002).

## Target experience

Players should feel like **masterminds orchestrating high-stakes operations** — balancing risk against reward, adapting to the unpredictability of the dice, and landing the thrill of a well-timed ability that turns the tide. Grounded but awesome: the world takes itself seriously and embraces its own style without apology.

## Type & setting

- **Genre:** digital board game, turn-based tactical strategy.
- **Setting:** a criminal underworld dressed as a high-society Art Deco casino — opulent, decaying, noir. Powers and abilities are **enabled by clean near-future tech** (the Marvel model: grounded-but-awesome, tech is the engine for the spectacle). Not a fantasy/magic world — see `ART_DIRECTION.md`.
- **Art style:** stylized **2D** (locked, ADR-0001), clean lines, dark casino glamour, Art Deco elegance, "classy but dangerous underworld."

## Key features (MVP scope)

- **3 operators animated for the MVP**, from a 9-operator pool; more added over time.
- **Modular, procedural board** on the Ludo track.
- **Match modes:** free-for-all and 2v2. (Ranked/competitive is **post-launch**.)
- Energy-gated abilities, dice-driven movement, capture/combat, safe spaces (per GDD + ADR-0003).

## Platform targets

- **Primary:** PC (Steam).
- **Stretch:** Android & iOS tablets.

## Audience

Strategy board-game players, indie-game fans, and casual-to-midcore gamers. Broadly "12+" as a rough audience descriptor — **not** a creative constraint on tone, violence, or content (that's deliberately unconstrained for now; revisit at a ratings/store pass if needed).

---

## What this doc deliberately does NOT decide

- **Rules detail** → `GDD.md`.
- **Look/aesthetic** → `ART_DIRECTION.md`.
- **Roster mechanics** → `OPERATORS.md`.
- **Board geometry** → ADR-0002 / ADR-0003.
- **How it's built (architecture)** → forthcoming architecture ADR + `CONVENTIONS.md` (not yet written — the current top open item).
