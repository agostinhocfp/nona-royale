# ADR-0013 — Operators are chips: portrait chips on the board, figures and rigs retired

- **Status:** Accepted 2026-09-29 (designer, after CP1–CP4 in Play Mode: "Nailed it. Perfection."). The rig code's removal is agreed and pending (see Open).
- **Date:** 2026-09-29
- **Location:** `docs/decisions/0013-chip-pieces.md`
- **Supersedes:** ADR-0009 for board pieces. **Relates to:** ADR-0001 (2D locked), ADR-0010 (URP 2D), `docs/design/CHIP_PIECES.md` (the build log), `docs/design/OPERATOR_LOOKBOOK.md` (the rigs), `docs/design/CAST_ONBOARDING.md` (CO3a), `ART_PROMPTS.md` asset blocks 6–8.

## Context

ADR-0009 made operators standing figures rendered from 3D: a Meshy model per operator, a Blender script, then seated and standing sprites. The look book (LB0–LB5) added procedural figures and then rigged 2D figures for all twelve, as a stand-in until the renders existed. Both were slow per operator, and neither reached the finish the designer wanted at board scale; the generator could not hold 6-head proportions (six runs of evidence, `ART_PROMPTS.md`).

On 2026-09-29 the designer tried the opposite: a flat casino chip carrying a chest-up portrait. It took four increments (CP1–CP4): chips behind a Pieces switch, chips that stand out without size, a hero portrait in the tray with the dice moved out, and the painted hero frame. The verdict after Play Mode was that it nails the art and the HUD, and shortens the prototype's cycle.

## Decision

1. **Operators are chips on the board.** A flat token lying on the cell: the operator's portrait as the face, the seat colour on the body, a gilt ring, an ink outline, a drop and contact shadow. The same portrait sits in the tray as the hero portrait, in the painted Deco frame, and lights its device while an ability is armed. Code: `ChipView`, `ChipSprites`, `ChipArtLibrary`, `HeroPortrait`, `ChipArtImporter`.
2. **Two images per operator.** An unlit chip portrait and its lit twin, the twin a masked edit of the unlit one (`ART_PROMPTS.md` asset blocks 7 and 8), plus a device anchor measured on the image. No model, no rig, no render step. An operator without a portrait shows its shape in gold on black lacquer until it has one.
3. **ADR-0009 is superseded for board pieces.** No more Meshy models or Blender renders for the 2D game. ADR-0001's one 3D exception (2.5D sprite production) is therefore unused by this build.
4. **The rigs are dropped from the 2D build.** They are kept for the 3D version the designer plans years out: the designer tags the last commit that has them (`archive/rigs-2d`), then the rig code and its tests leave the project. The `.blend` and Meshy files in `art/source/characters/` stay.
5. **No ability icons in the tray.** The cards keep their words; hovering a card, or pressing and holding it on touch, shows the full description (CO3a). Invented symbols would be one more thing to learn before playing. `ART_PROMPTS.md` block 6 is retired from the tray.

## Consequences

- **The art pipeline is a prompt-and-review loop.** Generate, compare at 48, 64 and 96 px beside the approved chips, make the lit twin, measure the anchor, drop both files in `Resources/Art/Chips/`. The importer cuts them to a circle, including RGB files.
- **Seat identity moves onto the chip's body**; the shape pin and the seat disc under a figure are not used by chips.
- **The tray changed shape.** The hero portrait takes the tray's left; the dice moved to a dock over the board's bottom-left (wide) or the screen's bottom-left (upright).
- **The Pieces switch (Chips / Figures) stays for now,** defaulting to Chips. Figures still work through the renders, the look book and the pawn; with the rigs gone, a figure without a render falls back to the look book.
- **Per-operator art is small and cheap to change.** A redesign is one image pair; nothing downstream has to be rebuilt.

## Open

- [ ] **Remove the rig code** once `archive/rigs-2d` is tagged: `View/Figures/Rig/*`, the rig branch in `OperatorPiece`, the rig prewarm in `MatchBootstrap`, `RigTests`, `RigPieceTests`, `RigBoardTests`; update `OPERATOR_LOOKBOOK.md`. Reminder scheduled for 2026-09-30.
- [ ] **Decide the Figures option's future** once all twelve chips exist: keep it as a fallback, or remove the switch, the renders and the look book as well.
- [ ] **Chip portraits for the other nine** (`ART_PROMPTS.md` block 8).

## Options considered

- **Keep figures (ADR-0009) and finish the renders.** One model and a script run per operator, a rig on top, and the proportions still out of the generator's reach. Slow, and the chips already beat it.
- **Chips on the board, figures in the tray.** Two art sets per operator for no gain in play.
- **Keep the rigs in the project, dormant.** Free today, but they are compiled, tested and read by every session; the tag keeps them just as reachable.
