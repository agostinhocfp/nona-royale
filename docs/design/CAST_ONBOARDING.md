# Nona Royale — Cast onboarding (CO)

> Location in repo: `docs/design/CAST_ONBOARDING.md` · Project copy: `claude/CAST_ONBOARDING.md`
> Status: **Open, 2026-09-29.** CO0 (the words the cards need) written, waiting on Play Mode. **CO3a (hold any card to read its peek) built, waiting on a phone.** CO1–CO6 planned.
> Related: `STRANGER_TEST.md` (§4 condition 2, "Casts": the condition this pass exists to meet), `LAUNCH_UI_PASS.md` (G10a, the first-match tips this builds on), `HUD_PASS.md` (H1, the tray's stable geometry, which nothing here may break), `MOBILE.md` (M3: every change must work upright and on touch), `PRESENTATION.md` §1 (the view computes nothing), ADR-0008.

## Why this document exists

The first stranger session (non-gamer, 2026-09) went well, with one failure. The tester was invested, frustrated by knockouts, and asked for another round. **He never cast an ability and never noticed energy.** He watched the CPUs cast and understood that they were doing something Ludo does not have. He still did not work out that he could do it too.

That is a discoverability failure, not a comprehension failure. Players who grew up on RTS, MOBA and card games look for abilities because they expect them. A player whose frame is GTA or Football Manager does whatever the screen tells him to do next. At the moment he could cast, our screen told him to end the turn.

**Direction (designer, 2026-09-29):** in-match hints with visual feedback. The optional tutorial and the video stay on the roadmap, but they do not fix this: the players who need them most are the ones who skip optional things.

## Findings in the build (read 2026-09-29)

1. **The only pulse points at END TURN.** Once the dice are spent, `TurnButton` pulses END TURN in cyan, and nothing else on screen moves. The top bar says "Dice spent — cast, or press E to end the turn" whether or not anything is castable (`TurnStrip.Prompt`), so the word "cast" is noise.
2. **Ready cards are quiet, and only visible after a selection.** A castable ability card carries a static cyan edge at 0.55 alpha (`ActionTray.ReadyEdgeAlpha`). The cards only exist while an operator is selected. The squad rail and the pieces say nothing about who can cast. The rail's cyan "ready" means "can deploy", so the one "ready" on screen means something else.
3. **The Abilities tip is text in a corner, shown once.** `Coach.Ability` fires in the action phase when anything is castable. It is last in offer order, text-only, in the lower-left corner, and marked seen when shown. Seen tips persist across sessions (`SettingsStore.SaveSeenTips`), so a build the designer has already played shows none unless Tips is switched off and on (STRANGER_TEST §2).
4. **Burned energy is silent.** Overflow over the cap is burned when the next turn's first roll pays (`EnergyLedger.GrantForTurn`). The only trace is "(N lost at the cap)" in the event text. Every roll pays at least 1, so ending a turn at the cap always loses energy.
5. **A CPU's cast does not look like the player's cards.** It gets the cyan `CastTell` on the board and a toast line. Nothing on screen shows that "the thing Red just did" is the same kind of object as the cards in the player's own tray.
6. **The full ability text is one hover away on desktop, and never on touch.** The hover peek (G7b-3) shows the name, the meta and the rules line, never `AbilityDefinition.Description`. On touch a tap arms, so the peek only shows the armed ability. Traits already open a full card (`GlossaryCard.ShowTrait`); abilities have no equivalent.

## Rules that hold throughout

- **Engine answers only.** "Can cast" is `TurnOptions.IsCastable`; readiness reasons are `GameEngine.CheckAbility` and `TurnsUntilReady`; burn is `EnergyGranted.Burned`. Anything missing is added as a query with a test.
- **Nudges fade out.** Every nudge in CO1–CO4 is gated on a persisted count of the human player's casts (`SettingsStore`, beside the seen tips). Suggested: full strength until the first cast, reduced until the third, gone after. A player who already casts never sees them. Switching Tips off and on resets the count.
- **CPU seats are never nudged.** Nudges apply only to a human seat's turn, the same rule as `Coach`.
- **Stable geometry (H1).** Nothing reflows the tray or the rail. Markers are overlays or existing slots.
- **Touch first.** Each increment names its upright and touch behaviour.

## Increments

### CO0 — Words for the cards (designer, 2026-09-29)

- **Why first.** A card in the tray shows only its meta ("5e · r3 · cd 3"). The hover peek showed the rules line, which is exact but reads as a formula, and never showed the description. CO3's full card would have had nothing plain to say either: a third of the descriptions were flavour alone, and some were wrong.
- **Every ability's description rewritten** (32 across the twelve roster operators): what it does in plain words, and what follows from it. Still no numbers (`AbilityDefinition.Description`); the rules line carries them.
- **Facts, not advice** (designer, 2026-09-29). A description never says when to cast, what to pair it with or what it is good for, and passes no verdict on cost or cooldown ("cheap", "save it for the kill"). "It hits harder if they are already bleeding" is the model: the player gets the fact and makes the decision. The rule is written into `AbilityDefinition.Description`'s remarks.
- **Corrections along the way:**
  - Zero-Day promised "massive area-of-effect thermal damage"; it deals a small Tech hit and a slow.
  - Vendetta never mentioned its lifesteal.
  - Nano Cell's words predate the removal of its stun (2026-09-24); they now say it blocks collisions too.
  - Hermes' Ring now says where Tech damage comes from (Kian's abilities, Cryo-Pulse, Zero-Day) and that Normal and Atomic still land.
  - Sonic Disrupter now says that an enemy ahead of the caster is pushed further along.
  - Drone Strike now says enemies on a safe cell take nothing.
- **The peek leads with the words.** Hovering a card (or arming one on touch) opens the peek with the name and meta, the description, then the rules line in the dimmer text colour as the reference.
- **Tests:** `AbilityDescriptionTests` (new): no digits, at most 320 characters. Facts-not-advice is kept in review; no test can read it.
- **Descriptions are not in the rules fingerprint** (`RulesFingerprintTests`), so existing replays stay valid.
- Files: the twelve `Roster/*.cs`, `AbilityDefinition`, `ActionTray`, `AbilityDescriptionTests` (new; Unity writes its `.meta`).
- **Checked:** core 971 passing (969 + 2). The view was not compiled here: run `tools\view-compile.cmd`, or let Unity recompile.
- **Play Mode checklist:**
  - Hover each of an operator's three cards: the peek shows the name and meta, then the plain description, then the rules line, dimmer. Nothing is cut off at 1920×1080 or 1280×720.
  - Killzone and Tagged From Above, the two longest, still fit the peek above the bar without covering the top bar.
  - On touch (or with the simulator), arming a card shows the same peek for its few seconds.
  - Operator guide and draft dossier: the new words appear under each ability.

### CO1 — Stop pointing at END TURN

- While the human seat has a castable ability (`IsCastable` for any operator) and the cast count is below the fade threshold, END TURN is enabled but **does not pulse**.
- `TurnStrip.Prompt` names the caster instead of the generic line, e.g. "Dice spent — **Luka** can cast. Select her." (touch: "tap her"). With two or more castable operators: "Dice spent — Luka and Kian can cast."
- When nothing is castable, the prompt drops "cast" entirely: "Dice spent — press **E** to end the turn."
- Also before moving: once rolled, if something is castable, the move prompt gains a short suffix ("… · Luka can cast"). This mention is the one that makes "cast before or after moving" discoverable.
- Files: `TurnStrip`, `TurnButton`, a small castable-operators helper (Core, next to `TurnOptions`, with a test), `MatchBootstrap` (the cast counter).

### CO2 — Show who can cast

- **Rail:** a cyan cast mark (a lit diamond, the energy pip's shape) on each castable operator's row. The deploy "ready" becomes "deploy" so the two cyan words stop sharing a meaning.
- **Board:** a slow cyan pulse on the castable piece's ring, under the selection ring. Below the fade threshold only.
- **Tray:** ready cards pulse (`UiKit.Pulse`, cyan) until the first cast; after that they keep the static edge, raised from 0.55 to full on the frame's outer line so a ready card is readable at a glance.
- Upright: the rail's folded form gets the same mark on the operator chip.
- Files: `SquadRail`, `OperatorPiece` or `PieceHudLayer` (whichever owns rings), `ActionTray`.

### CO3 — Hold a card to read it in full (designer, 2026-09-29)

- **Press and hold an ability card** (mouse or touch) for about 0.4 s: the full ability card opens over the match. Contents, in the trait card's frame (`GlossaryCard`):
  - group line: "Ability · Luka";
  - the name;
  - the meta line (cost, reach, cooldown), `OperatorDossier.Meta`;
  - the current state: "Ready", "Needs 6 energy, you have 2", "Ready in 2 turns", "Luka is stunned", from `CheckAbility`;
  - the rules line, with keywords linked to the glossary;
  - `Description`, in italics, as the trait card shows a trait's.
- **A hold never arms.** Releasing after a hold does nothing else; a short press still arms, as today.
- **The card stays open until dismissed** (tap outside or close), like the trait card, so its keywords can be followed.
- **Hold feedback:** from about 0.15 s, a thin cyan bar fills along the card's bottom edge, so the player learns that holding does something.
- **Desktop keeps the hover peek.** Right-click opens the same full card, the shortcut PC players expect.
- Implementation: a `HoldRelay` component (`IPointerDownHandler`, `IPointerUpHandler`, `IPointerExitHandler`), shaped like `HoverRelay`. It reports a hold and marks the press consumed; the card's click handler checks that and returns without arming. `GlossaryCard.ShowAbility(host, operatorName, ability, stateLine)` is the new entry point.
- Files: `HoldRelay` (new, `Assets/_Project/Scripts/Unity/View/HoldRelay.cs`), `GlossaryCard`, `ActionTray`.

#### CO3a — Hold any card to read its peek (designer, 2026-09-29, built)

The first slice, from the designer's phone playtest: on touch the peek only ever showed the armed ability, so a card that couldn't be cast could never be read.

- **Hold any ability card** (0.4 s, touch or mouse), castable or not: the tray's **peek** opens (name, meta, description, rules line), not yet the full glossary card. The hold never arms; its release is made ineligible for a click.
- **Tap a card that can't be cast:** the same peek, since there is nothing to arm. A castable card still arms on a tap, and its peek still shows for 4 s on touch, as before.
- **The peek names the card's state** after the meta ("ready in 2 turns", "needs 5e, have 2").
- **A peek opened by a press stays until the next press anywhere.** Hover peeks on desktop are unchanged.
- **Hold feedback:** a thin cyan bar fills along the card's bottom edge from 0.15 s.
- **The peek is lower** (designer, on a Galaxy S26): it stands on the top edge of the card row instead of the tray's, so upright it covers the aim line, the dice and the operator card rather than the board. Wide, the difference is the tray's padding.
- Still open from CO3: the full card in `GlossaryCard`, linked keywords, right-click on desktop.
- Files: `HoldRelay` (new), `ActionTray`; `HoldRelayTests` (new, EditMode).

### CO4 — Energy full

- At the cap on a human turn, the energy pips glow gold and pulse slowly.
- The END TURN hint reads "Energy full — next roll's energy is lost" instead of "E".
- When a turn grant burns (`EnergyGranted.Burned > 0`), a "−N lost" floats off the energy meter, and a toast carries the same words for the human seat.
- Files: `TurnStrip`, `TurnButton`, `MatchBootstrap` (the burn event), `EventToasts` or `FloatingText`.

### CO5 — Make CPU casts look like the player's cards

- When a CPU casts, its ability card appears for about 1.5 s beside the caster's rail row (wide) or above the tray (upright), in the tray card's chrome: seat-coloured name, ability name, meta. The project brief's "card flips" is the motion: it flips in and fades out. It never catches the pointer.
- The first time after that when the human seat can cast, a new tip, **"Your turn to cast"**, replaces the generic Abilities tip: "Your operators have abilities too. [Luka] can cast now: select her, pick a card, then Cast. Hold a card to read it."
- The tip card points at the castable operator's rail row rather than sitting in the corner. If that anchoring is too costly, it stays in the corner, and CO2's pulse does the pointing.
- Files: `CastTell` or a new `CastCard` view, `Coach`, `CoachCard`, `MatchBootstrap`.

### CO6 — Retest

- A **new** non-gamer tester, with STRANGER_TEST's pass bar. Condition 2 (a cast, unassisted) is the one this pass is judged on.
- Tips are switched off and on before the session, which also resets the cast counter.
- Record whether the first cast came from the prompt, the pulse, the rail mark, the tip or a CPU's example. Ask in the debrief.

## Order

CO1 and CO2 first: the cheapest, and between them they fix finding 1 and finding 2. CO3 next, since it is independent and the designer asked for it. CO4 and CO5 after. CO6 closes the pass.

## Log

- 2026-09-29 — Pass opened from the first stranger session. Findings read from the repo. CO3 added at the designer's request.
- 2026-09-29 — Pass doc committed (`04c8158`). **CO0 written:** every ability description rewritten with context, and the peek shows it. Core 972 passing; view compile and Play Mode owed.
- 2026-09-29 — **CO0 revised (designer): facts, not advice.** Every sentence telling the player when or how to cast came out ("open with Ace Shards, then follow with this", "save it for the kill", "cheap and ready again soon"). The rule went into `AbilityDefinition.Description`; the two-sentence test went with it, since it asked for exactly that advice. Core 971 passing.
- 2026-09-29 — **CO3a built** (designer, phone playtest): hold any ability card to read its peek, tap a card that can't be cast to read it, the peek names the card's state and sits on the card row rather than above the tray. Runtime, editor and EditMode test assemblies compile against the editor DLLs; not yet run in Unity or on the phone.
