# ADR-0014 — Each operator's device glows its own colour; the house and the UI keep cyan

- **Status:** Accepted 2026-09-30 (designer: "It doesn't make too much sense for them to glow the exact same color, coming from different backgrounds").
- **Date:** 2026-09-30
- **Location:** `docs/decisions/0014-operator-glow-colours.md`
- **Amends:** `ART_DIRECTION.md` §2.1, §3, §5 and §5.1 (cyan as the one powered colour). **Relates to:** ADR-0013 (chip pieces), `docs/design/CHIP_PIECES.md`, `ART_PROMPTS.md` asset blocks 7 and 8, `OPERATORS.md`.

## Context

Since 2026-09-19 every device in the cast lit the same holo cyan (`#5FE0E8`): the art bible reserved cyan for anything powered, so the board, the UI and all twelve operators shared one colour. With the chip portraits the cast became twelve people from different places, and one shared glow stopped making sense for them: the contractors and the owners did not buy their gear from the house. The designer painted Nuetu's lit chip red to make the point.

## Decision

1. **The house's tech is cyan, like the board.** The house built the room, so Bouncer, Kurbyn, Javi and Sanity glow the board's cyan. So does Luka, whose ring is house tech taken off a house man.
2. **Everyone else glows their own colour:** Syla magenta `#FF3FC8`, Mimi ice white `#DDF6FF`, Kian chartreuse `#D4FF3A`, Nuetu red `#F12A27` (measured on the designer's painted chip), Fortuna warm white `#FFF1CC`, Revú orchid `#C04DFF`, Lethe moon lilac `#EEE6FF`. Code: `OperatorGlow`.
3. **The glow is the device's, everywhere it shows:** the lit chip portrait, the chip's flare on a cast, the hero portrait's device glow while an ability is armed, the cast tell's sweep, line and rings, the caster's rim flare and the cast's light on the board.
4. **The interface keeps cyan.** Selection, a ready ability card, safe cells, the powered cells and the hero frame's arcs are the interface's language, not an operator's. The threat rim keeps its amber.
5. **No glow sits on a seat's hue or the threat rim's.** Seats are red, blue, green and violet and they are gameplay information; amber means an enemy is being hit. A saturated glow keeps more than 20° of hue from each (`OperatorGlowTests`). Pale glows (Mimi, Fortuna, Lethe) read as white light and are exempt. **Nuetu's red is the one recorded exception** (designer): it is hotter and brighter than the red seat and only shows while he casts.
6. **Devices are still dark at rest.** A glow of any colour means powered; nothing glows standing still.

## Consequences

- **Lit portraits follow the operator's colour.** The existing cyan twins were repainted by `tools/art/recolour_glow.py`, which swaps only the added cyan light for the new colour and leaves the hot white cores; the cyan originals are kept beside them as `<name>_chip_lit_cyan.png`. Nuetu's is the designer's own red edit, composited through his seam mask. New lit twins are generated in the operator's colour directly (`ART_PROMPTS.md` block 8 lit lines).
- **Revú could not have amber**, the colour his ledger first suggested: it is the threat rim. Fortuna could not have saturated gold for the same reason, so her glow is gilt-white light.
- **Three pale glows** (Mimi, Fortuna, Lethe) are told apart by tint and by the device, not by hue. If they blur together in play, Lethe's lilac is the one to push.
- **The hero frame's arcs stay cyan** under every operator: the frame is the tray's, not the operator's.

## Open

- [ ] **Play Mode check** of every glow on the board, on the four seat colours, and beside the threat rim.
- [ ] **Kian's lit twin** in chartreuse when his chip is made.

## Options considered

- **Every operator unique, the house included.** Twelve hues cannot all dodge four seat colours, the threat rim and each other; several would sit close and read alike at 64 px.
- **One colour per camp.** Four colours, but Syla, Mimi, Kian and Nuetu come from four different places and would still share one.
- **Cyan for everyone (the old rule).** Clean, but it says the whole cast shops at the same place.
