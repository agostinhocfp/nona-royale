# Nona Royale — Operator Guide

> Location in repo: `docs/design/OPERATOR_GUIDE.md`
> Status: **OG1–OG5 built 2026-09-21.** Decisions D1–D6 are the designer's. The strategy copy (OG5) is Claude's draft and waits for the designer's rewrite.
> Related: `COMBAT_SYSTEMS.md` §5 (statuses), §10 (the roster, the only home of every number); `OPERATORS.md` (identity and camps, no numbers); `DRAFT.md` (the draft screen the guide plugs into); `MOBILE.md` (M1–M7, the layout rules it must follow); `STRANGER_TEST.md` §4 (debrief questions 4 and 6).

## Why this exists

Twelve operators, thirty-two abilities and about eighteen status keywords, and today a new player meets all of it in one place: the draft's detail panel, under a clock. The panel already shows a lot — role, health, speed, the passive or aura, and every ability with its cost, cooldown, range and description — but the descriptions are **flavour**, deliberately free of numbers ("Collection day. The emptier the enemy's reserves, the harder it lands…"). They make an operator memorable; they do not teach how it plays or how to beat it.

The guide is the place to learn the roster **without a clock**, and to look something up in the middle of a first match.

## Decisions

| #  | Decision | Chosen | Why |
| -- | -------- | ------ | --- |
| D1 | Entry points in the first version | **Title → OPERATORS first, then FULL DOSSIER from the draft.** In-match follows as OG4. | **The main menu is the primary entry, and it ships with the view itself (OG2)** (designer, 2026-09-21): it is where a new player can investigate the roster without being punished by the draft clock. In-match touches the most HUD code and should land on a shared view that has already settled. |
| D2 | Where the rules text comes from | **Generated from the definitions at runtime.** Nothing numeric is ever typed into guide copy. | `OPERATORS.md` carried its own stats and drifted until they were stripped out; the 2026-09-20/21 passes alone changed eight roster numbers. A hand-written guide would have needed eight edits that week. |
| D3 | Strategy copy | **Two number-free paragraphs per operator — "How to play" and "How to beat" — drafted by Claude from `COMBAT_SYSTEMS.md`'s balance reasoning, edited by the designer.** | It is the most useful part of the guide and the part only design knowledge can write. |
| D4 | The draft clock while a dossier is open | **Keeps running.** | Designer's call, and it is D1's other half: the draft is not where the roster is meant to be learned — the main menu is, with no clock at all. The draft's dossier is a reminder, taken at the draft's price. Requirement: the clock stays **visible** over the dossier, and a pick made by the clock running out must be obvious on return (§5). |
| D5 | Where text generation lives | **`NonaRoyale.Core`, in a `Text` namespace, pure C#.** | Testable everywhere the core is — the mono harness and EditMode alike — and it reads rules data without restating a rule. The view only turns its output into TMP markup. |
| D6 | Art | **No dependency on rendered art.** The dossier uses `OperatorArtLibrary.Portrait` when it exists and the procedural piece shape when it does not, exactly as the draft does. | One of twelve operators has rendered art today. The guide ships now and upgrades itself as art lands. |

## 1. What a dossier shows

1. **Header** — portrait or piece shape, name, role (`OperatorCopy.Role`) and camp, health, speed, and the "read at the table" line under it. Camp and tagline come from `GuideCopy` (OG5), the only hand-kept text in the dossier.
2. **Kit** — per ability, in cast order:
   - name, then cost · cooldown · range · targeting, as the draft's `AbilityMeta` already writes them;
   - **the rules line**, generated (§2): *"3 Normal · Burdened 2 turns · you heal 1 (2 while your Killzone is live)"*;
   - the flavour line underneath, dimmed.
   Passives and auras use the same shape, listed before the abilities: the rules line from their status or aura data, and a number-free flavour line from `OperatorDefinition.PassiveDescription` / `AuraDefinition.Description`. `RulesText.Traits` is the single list every screen reads — dossier, draft card and match (§5a).
3. **How to play / How to beat** — the two D3 paragraphs.
4. **Keywords** — every status, damage type and board term in the rules lines is a link. Tapping one opens its glossary card (§3). This is what answers the Stranger Test's debrief questions 4 ("what is wrong with that piece?") and 6 ("what does safe mean?") without adding anything to the match HUD.

## 2. The rules line (OG1)

`RulesText.For(AbilityDefinition)` returns a list of **runs** — plain text, numbers, and keyword references — never a finished string. The view decides colour and markup; the core decides words and values.

- **One formatter per `EffectKind`**, nineteen in all. Most are a sentence fragment. The five with structure — `AttachCharge`, `DeployZone` (crowd zones included), `FollowUp`, `ProjectField`, `SetTable` — get a template filled entirely from their own fields.
- **Every modifier the data carries is written**, because it is the part a player cannot guess: crits and the heavy line, lifesteal, the bleeding bonus, the in-zone bonus, `MinimumDamage`, strikes-on-cast, the execute threshold.
- **Scope decides the subject**: *the target*, *you*, *enemies within 2 of you*, *enemies within 3 of the target*. Audience decides the mode line where an ability has two ("Enemy: … · Ally: …"), which is how All-In Mauling and Collision read.
- **Glossary numbers come from `CombatConfig`**, never literals: Slow's penalty, Burdened's cells, Evasion's chance, Bleed per stack, Mark per turn, Equilibrium's bands.
- **Refusal to guess.** An effect the formatter cannot describe returns a visible `[unwritten: Kind]` run, and a test fails on it (§6). Silent fallbacks are how a guide quietly lies.

## 3. The glossary (OG1)

`Glossary.Entries` — one entry per keyword the rules lines can emit: each `StatusKind` a roster ability or passive applies, the three damage types, and the board terms that need one (safe cell, spawn cell, yard, upkeep, collision). Each entry is a title, the `StatusPalette` colour the board already uses for it, and a definition built from `CombatConfig`. The glossary is also browsable on its own from the OPERATORS page.

## 4. The view (OG2)

**One `OperatorDossier` view**, built once per operator shown and reused by the title page, the draft and, later, the in-match sheet. The draft's `RebuildDetail` is refactored onto it rather than copied — the draft's short summary becomes the dossier's compact mode.

- **Upright (phone):** a 3-across tile grid as in the draft, tap → the dossier as a full-height scrolling page, BACK returns to the grid.
- **Wide:** the roster list on the left, the dossier on the right.
- **It builds once per selection** and never on a per-frame dirty flag. Rebuilding on a flag is what made the buttons flicker (GUI_PHASE, 2026-09-21).
- **First long touch-scrolled page with buttons inside it.** The `ScrollRect` needs a drag threshold that lets a swipe scroll without firing the button under the finger.

## 5. Entry points

- **OG2 — Title → OPERATORS. The primary entry point.** A new `TitleScreen` page beside PLAY and SETTINGS: the grid, the dossier, and a GLOSSARY tab. No clock, no match, nothing at stake — this is where a new player is meant to learn the roster, and it ships in the same increment as the view so the guide is usable from the first build that contains it.
- **OG3 — Draft → FULL DOSSIER.** On the focused card. It opens over the draft **with the clock still running and still visible** (D4). If the clock fills the seat's slot while the dossier is open, the dossier closes on the pick and the draft shows which card the clock took.
- **OG4 — In a match.** Tap any piece's portrait in the squad rail — an enemy's included, since "what does *that* do?" is the question a first match asks most — or open it from the pause menu. Read-only; the match does not pause for it unless the player pauses.

## 5a. Passives outside the guide

A passive is part of the kit on every screen that shows the kit, not only in the dossier.

- **Draft card:** one line per ability, then one per passive and aura, with the dossier's tag (`passive`, `aura · r2`). Four lines is the most any kit takes today (Fortuna, Sanity, Lethe), and the wide card's height holds four; a fifth needs `CardHeight` first.
- **Match:** gold chips on the selected operator's card in the action tray, under its health and statuses, shown in the yard too. A chip opens the trait's card (`GlossaryCard.ShowTrait`): whose it is, the board tag, the rules line with its keywords linked, the flavour. Esc closes it. Chips, not ability cards: a card in the ability row reads as castable, and it would put three operators at four cards across a phone.

## 6. Tests

All in Core, so they run in the harness and in EditMode alike.

- **Every roster ability formats with no `[unwritten]` run.** Adding an effect kind without its formatter fails here.
- **Every keyword a rules line emits has a glossary entry**, and every glossary number matches `CombatConfig`.
- **Every roster passive and aura is a trait with a description, and no description has a digit** (`KitTraitTests`).
- **Every operator in `Roster.All` has guide copy** (D3). A thirteenth operator without its paragraphs fails the build — deliberately stricter than `OperatorCopy`'s fallback role word.
- **No digit in guide copy.** The D2 rule, enforced: a number typed into a strategy paragraph fails.
- **Golden lines** for a handful of abilities (Bio-Link Rage, Sadist, Vendetta, Miracle Pull, The Table), so a formatter change that rewords them is a visible diff, not a silent one.

## 7. Increments

| #   | What | Done when |
| --- | ---- | --------- |
| OG1 | `RulesText` and `Glossary` in `NonaRoyale.Core.Text`, and their tests | Every roster ability formats cleanly; golden lines pass; no UI yet |
| OG2 | The shared `OperatorDossier` view, and **Title → OPERATORS and GLOSSARY** on it | The main menu opens the full roster and glossary in both orientations, with generated rules lines |
| OG3 | The draft's detail panel moved onto the view; draft → FULL DOSSIER | The draft shows the generated rules lines; the clock stays visible over the dossier; nothing else on the draft changed |
| OG4 | In-match: squad rail portraits and the pause menu | Any piece's dossier, enemies included, in two taps |
| OG5 | Strategy copy for all twelve | Claude's draft in `GuideCopy`, the copy tests pass; **the designer's rewrite is still open** |

## 8. Not in the first version

- **A "try it" sandbox.** The engine is deterministic pure C#, so a scripted mini-board demonstrating each ability is feasible, and it is by far the most expensive part of this idea. Revisit once the guide exists and players have used it.
- **Voice on open.** Trivial once real voice files replace the blips (`VOICE_LINES.md`).
- **Localisation.** Every string lives in `Core/Text` or the copy file, so it can be extracted later; nothing is being built that would prevent it.

## Log

- 2026-09-21 — **Passives everywhere the kit is shown** (designer: passives must not be left out of the UI or the guide).
  - **The audit.** The dossier already listed every passive and aura with a rules line, but with no flavour, where every ability has one. The draft card showed one trait in a spare ability slot and dropped the rest: Fortuna's House Edge, Lethe's haste and Sanity's burden appeared only as status tags. The match's action tray showed none at all.
  - **Core.** `OperatorDefinition.PassiveDescription` and `AuraDefinition.Description`, optional in the constructors (test and sweep squads carry passives nobody reads) and required of the roster by test. `KitTrait` and `RulesText.Traits`: passives, then the aura, as one list. `RulesText.Passives` now returns `KitTrait`s. Flavour for all seven traits is **Claude's draft**, like the D3 copy, and waits for the designer's rewrite.
  - **View.** The dossier writes each trait's flavour under its rules line. The draft card lists every trait (§5a). The action tray's operator card gains trait chips that open `GlossaryCard.ShowTrait`; `GlossaryCard` now shares one frame between keyword and trait cards. `MatchBootstrap.HandleKeys` gives Esc to an open card first and holds the shortcuts under it.
  - **Tests.** `KitTraitTests` (7). Core suite 802 passing in the standalone runner.

- 2026-09-21 — **First Play Mode look, and OG4–OG5** (designer: "it looks great"; the shapes were too big).
  - **Fixed: the silhouettes filled the rows.** `OperatorDossier.Icon` set a layout size on an icon whose box has no layout group, so the size was never applied and every shape kept a new rect's 100 × 100. It now sizes the rect itself. **Probably fixed: the dossier's name did not draw.** The display face has no ellipsis glyph, so a display-face label set to ellipsize draws nothing; the wordmark and the draft clock already set overflow for that reason, and the dossier's name, the guide title and the glossary card title now do too. Not seen fixed yet — confirm on the next look.
  - **OG4 — in a match.** Every silhouette in the squad rail opens that operator's dossier, enemies included: the rows, the upright chips, the folded CPU pips, each with a hit box larger than its mark so a finger can find a 14-unit pip. Inside the player's own row button the silhouette takes the click and the rest of the row still selects. The pause menu gains **OPERATORS**, and leaving the guide returns to the pause menu. The guide is a full-screen card, so bots and board wait under it as under the pause menu, and the match's music keeps playing, ducked. `IControlPanelHost.OpenDossier`, `IPauseHost.OpenGuide`, and `OperatorGuideScreen.Open` takes a per-opening return.
  - **OG5 — the copy, Claude's draft.** `Core/Text/GuideCopy.cs`: camp, the `OPERATORS.md` "read at the table" line (Fortuna had none; "The one who deals the dice" is invented), and How to play / How to beat for all twelve, written from the current kits and this week's balance passes. The dossier shows the camp beside the role, the tagline under the header, and the two paragraphs after the abilities; the draft's compact panel shows none of it. `GuideCopyTests` (4): every roster operator has an entry, no entry names someone who is not in the roster, the camps match `OPERATORS.md` (four house, four contractors, three owners, Luka alone), and **no string contains a digit** — words like "half" and "sixes" describe a rule's shape and are allowed.
  - **For the designer:** the copy is a draft to rewrite, not a finished voice. It is the one part of the guide that can go wrong with the rules and pass every test, so read it against the table after any balance pass that changes what an operator is *for*.
- 2026-09-21 — **OG1–OG3 built in one batch** (designer's request).
  - **OG1 — `NonaRoyale.Core.Text`.** `RulesLine` (runs: text, numbers, keyword links), `RulesText` (abilities, passives, auras) and `Glossary` (every status, the three damage types, twelve board terms), all read from the definitions and the three configs. Two-sided abilities are written as two modes on their own lines; consecutive effects on the same people share a clause; identical blows are counted ("3 × 1 Atomic"); anything that moves the caster or happens later is its own clause. `RulesTextTests` (18): every roster ability, passive and aura formats with nothing unwritten; every linked keyword has an entry; the glossary follows a tuned `CombatConfig`; five golden lines.
  - **OG2 — the view and the main menu.** `OperatorDossier` (one builder, full and compact), `RulesMarkup` (runs to TMP markup, status keywords in their board-tag colour, links only where something listens), `KeywordLinks` (tap a keyword → its id), `GlossaryCard` (one card at a time, over whatever screen), `UiKit.ScrollColumn`, and `OperatorGuideScreen`: ROSTER and GLOSSARY tabs, list-and-dossier wide, grid-then-page upright. Title gains **OPERATORS** second in the menu (four buttons: across wide, stacked upright). `MatchBootstrap` gains `AppScreen.Guide`; the guide returns to the title. `HudRoot` sets the event system's drag threshold to about two millimetres from the screen's density, so a tap is not lost to finger wobble inside the new scroll areas. `RulesMarkupTests` (5, Unity EditMode).
  - **OG3 — the draft.** The detail panel shows each ability's rules line instead of its flavour, with passives and auras as entries; FULL DOSSIER (wide, beside the name) and DOSSIER (upright, on the sheet) open the whole operator over the pool with the header and footer uncovered, so the clock and START stay in view (D4). A pick the clock makes for a human seat closes it; CPU picks do not, or ALL PICK's CPUs would shut it every second and a half. Esc closes the innermost: keyword card, dossier, sheet.
  - **Not yet:** camp in the header (above), the strategy paragraphs and their tests (OG5), and the in-match entry (OG4).
- 2026-09-21 — **The main menu is the primary entry** (designer): the guide must be reachable from the title screen so new players can investigate without being punished during the draft. Title → OPERATORS moved from OG3 into OG2, shipping with the view; the draft's entry follows in OG3.
- 2026-09-21 — **Accepted.** Designer chose D1 (title and draft first), D2 (generated rules text), D3 (Claude drafts, designer edits) and D4 (the draft clock keeps running). D5 and D6 follow from the codebase: text generation in Core so it is testable everywhere; no dependency on rendered art, since one operator of twelve has it.
