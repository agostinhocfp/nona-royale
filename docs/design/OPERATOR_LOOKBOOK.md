# Nona Royale — Operator Look Book (procedural Dark Deco figures)

> Location in repo: `docs/design/OPERATOR_LOOKBOOK.md` · Project copy: `claude/OPERATOR_LOOKBOOK.md`
> Status: **LB0 done. LB2 (3 of 12 recipes) and LB3 (judging window and rule tests) delivered 2026-09-21, awaiting Play Mode.** Written 2026-09-21. **v2 — ambition raised: these are meant to be good, not merely distinct.**
> Related: `ART_DIRECTION.md` §2.2 (Dark Deco cel — the spec), §3 (palette), §5.1 (the value ledger — the source of the recipes), §6.1; `ART_HOOKUP.md` (ART1 — the real-art path this must not break); `STAGE4_HANDOFF.md`; ADR-0009; ADR-0010 (URP 2D lights).

## Goal

Twelve operators, drawn entirely from code, that look like **deliberate Art Deco design work** rather than programmer placeholders — and that a family can tell apart at a glance on a phone.

The bar: a stranger seeing a screenshot should not be able to tell which operators have real renders and which are procedural, at board scale. At portrait scale they will, and that's fine.

## Why this is achievable, and not wishful

The locked rendering style is, in technical terms, **a vector specification**. §2.2's five rules are: three flat values per material, one uniform line weight, geometric primitive forms, no texture, and light drawn as hard shapes rather than simulated.

Everything in that list is what a rasteriser is good at, and everything a rasteriser is bad at — skin texture, soft gradients, hair strands, cloth simulation, subtle facial expression — the style already forbids. This is the rare case where the house style and the cheap tool agree. A painterly game would make this hopeless; Dark Deco makes it tractable.

The second piece of luck: **§5.1 already designed the twelve figures.** The value ledger assigns every operator a value solution, a silhouette shape and a dominant accent. The recipes below are transcriptions of that table, not inventions. When the real renders land, they're aiming at the same targets, so the placeholders are a rehearsal of the final cast rather than a detour.

## The stylistic move that makes this work

**Do not attempt human likeness.** A procedural figure that tries for a face lands in the uncanny valley of programmer art and looks cheap no matter how much effort goes in.

Aim instead at **Deco heraldry**: the figure as an emblem. Think period cigarette cards, casino chip inlays, hotel-lobby mosaic, the black-and-gold enamel of a Deco elevator panel. Bold silhouette, hard-edged internal shapes, no face or a face reduced to one shadow shape, everything built from stepped forms, tall diamonds and radial fans.

This reframes the limitation as an aesthetic. It also stays inside the locked style, since §2.2 already says anatomy is simplified *into* Deco shapes and §5 already demands silhouette-first identification. The placeholders become the most abstract end of the same visual language.

## The layer stack

Every figure is composed from the same ordered layers, each a flat shape, each rasterised with analytic anti-aliasing and supersampled 4× before downsampling. Nothing here is a gradient or a filter.

1. **Ground.** Seat-coloured base disc and ring under the feet, foreshortened by the tilt (this exists today for real art).
2. **Ink silhouette.** The figure's outline, dilated by one uniform line weight, in `#1C0E12`. One weight everywhere, per §2.2 rule 2.
3. **Base value.** The figure's body mass in its assigned base colour from §5.1.
4. **Shadow shape.** One hard-edged geometric shadow, cut from the lower right, per §2.2 rule 5's drawn key from the upper left. A wedge or a stepped shape, never a soft falloff.
5. **Highlight shape.** One hard-edged highlight on the upper left plane.
6. **Rim.** A drawn cool rim down both side edges and along the shoulders — a constant-width sliver of the silhouette, not a light calculation.
7. **Garment and hardware blocks.** The lapel wedge, waistcoat block, sleeves, the one discreet device. Aged brass for everyone except Fortuna, per §3. **Dark at rest — no cyan.**
8. **Deco furniture.** The operator's signature motif: stepped shoulder, radial fan, tall diamond inlay, chevron band. One per figure, placed the same way across the cast so the roster reads as a set.
9. **Pin.** The gold shape pin at the chest, unchanged.
10. **Powered overlay.** The cyan tell layer, drawn only while an ability is casting, per §5's "devices are DARK at rest".

Layers 2 to 8 are a pure function of the recipe, so the whole figure caches as one sprite per pose.

> **As built in LB0 (2026-09-21), the order differs in one deliberate way:** garments, hardware and motifs (7, 8) are drawn *before* the shadow and highlight (4, 5), and the shadow and highlight are **multiply** and **screen** layers rather than flat fills. One hard shadow cut then crosses suit, shirt and brass with the same edge — which is how a cel painter draws it — and every material still gets exactly three values, as long as the shadow and highlight shapes don't overlap (recipes subtract one from the other). The rim comes after both, so hardware on the outline is rimmed too. The code order is: ink → base → blocks → shades → lights → rim → ink lines → powered. See `FigureDrawing`.

**Also generate a normal map from the same shapes**, as VISUAL_PASS V5 planned for the board. The shapes are already known analytically, so a faceted normal map is nearly free, and URP's 2D lights then give the brass and gilt a real sheen as pieces move through the board's light pools. This is the single cheapest thing that will make the placeholders stop looking flat-pasted. **Deferred to after LB2** (designer, 2026-09-21): the shade and light layers already say which planes face away from and toward the key, so the normal map can be derived from them without a new field on any layer.

## The recipes

Straight from §5.1. Each row becomes an `OperatorLook`. The implementing session must re-read that table rather than trusting this copy.

| Operator | Silhouette primitive | Value solution | Accent / hardware |
| --- | --- | --- | --- |
| Luka | four-point X, forward wedge | warm light torso (camel) | cyan signet ring |
| Syla | downward triangle | light core inside a dark frame | cyan drone slits |
| Bouncer | wide low slab | black mass split by a hard white V | aged-brass gauntlet |
| Kurbyn | coiled four-point | mid-dark, broken by bare forearms | brass, nape cyan |
| Javi | upright cross | dark waistcoat block, two white sleeves | frosted canister seals |
| Sanity | octagon | large mid-brown mass | brass, prod cyan |
| Mimi | small dart | near-black and small | bright pale hardware |
| Fortuna | diamond (under review) | the only gold-dominant figure | gilt gold |
| Revú | three-point barbed hook | the only red torso | oxblood |
| Kian | five-point spiked crown | the only green torso, outline breaks upward | emerald, cyan rod tips |
| Nuetu | disc | the only all-light mass, cool dove-grey | black plates on grey |
| Lethe | six-point spark | the only true mid-grey figure | tarnished silver |

The four standing rules under §5.1 are constraints on the generator, not suggestions: the warm light torso is Luka's alone, gilt is Fortuna's alone, Mimi is solved by being smallest with the brightest hardware, and Lethe by being the only mid-grey with radiating upper points. **Encode them as tests**, below.

## Build the judging tools, not just the figures

This is where a procedural approach beats hand-drawn placeholders, and it's what makes "impressive" achievable rather than lucky.

- **Contact sheet window.** An editor window that draws all twelve figures, both poses, on the real board background, at real board scale, in one grid. Iterating on twelve figures without one is guesswork.
- **Squint test, automated.** The same sheet rendered as pure black silhouettes at 64 px, per §2.2's stated squint scale. If two are confusable here, the recipe is wrong, and §5's silhouette-first rule is violated.
- **Silhouette distinctness test.** Compare the 64 px silhouettes pairwise (intersection-over-union is enough) and fail the test above a threshold. This is the test that actually protects the goal, and it will catch a recipe drifting toward another operator's shape long before your eye does.
- **Background collision check.** Draw each figure over `board_carpet`, the gold inlay and the felt. §5.1 already names Revú-on-blood-velvet and Luka-on-lit-gold as the known collisions. Make it a sheet, not a memory.
- **Palette assertion.** No figure but Fortuna contains gilt; no standing or seated sprite contains cyan.

## Increments

| # | Increment | What it delivers |
| --- | --- | --- |
| LB0 | Rasteriser | Polygon fill with analytic AA, 4× supersampling, dilation for the ink line, hard-edged shadow and rim cuts, the layer compositor, caching. Two throwaway figures to prove it. **Done 2026-09-21.** |
| LB1 | Seat identity | Seat colour moves to the base disc and ring; the figure is untinted so its own value solution survives; optional name labels. **Done through ART1; labels skipped (designer, 2026-09-21).** |
| LB2 | The twelve | `OperatorLook`, `OperatorLookBook`, the twelve recipes, the Deco motif library. **In progress: Bouncer, Mimi, Nuetu.** |
| LB3 | Judging tools | Contact sheet, squint sheet, distinctness and palette tests. **Delivered 2026-09-21.** |
| LB4 | Portraits and tells | Draft-card portraits at a larger canvas with a Deco frame, plus the powered cyan overlay on cast. |

LB3 is tempting to skip and should not be. Build it right after LB2's first two or three recipes, then author the rest with it open.

**LB1 is not optional now.** With figures carrying assigned value solutions from §5.1, a seat tint over the top destroys the entire point: Nuetu's dove-grey and Mimi's near-black stop being distinguishable the moment both are painted the same seat colour. Seat identity has to live in the disc, the ring and the pin. This is also where Stage 4 decision 4 was already heading for real art, so it is one system, not two.

**LB1 rides ART1 (designer, 2026-09-21).** ART1 already built this for real renders: any `FigureArt` from `OperatorArtLibrary` is drawn untinted over the seat disc and ring, fitted by `FigureLayout.Fit`, and flashed through a white silhouette. A look-book figure is handed to the piece as a `FigureArt`, so it gets all of that with **no change to `OperatorPiece`**. What LB1 still owes: the optional name labels, and whatever the four-seat Play Mode check says about the disc and ring's strength.

## Honest limits

- **Faces.** Don't. One shadow shape where the face is, a hat brim, or a turned-away head. Any attempt at features will look worse than the abstraction.
- **Hands and props** below a certain size vanish at board scale. Keep hardware to one bold block.
- **Portraits are the weak point.** At card size the abstraction is visible as abstraction. Lean into it by framing the portrait as a Deco emblem in a gilt cartouche — an insignia rather than a photograph.
- **Timebox it.** LB0 to LB3 is a few focused days, not a week per figure. If a recipe isn't reading after two passes, simplify it rather than adding detail; at 64 px detail is noise.
- **The real risk is fondness.** Placeholders this deliberate can start competing with the real pipeline for attention, or feel too good to replace. The fallback order never changes: a real render always wins, and the look book is what draws when there isn't one.

## Play Mode checks owed

1. Twelve pieces out: each identifiable at normal zoom on a phone-sized Game view, in both cameras.
2. Seat identity still reads with four seats in play, now that it lives in the disc and ring.
3. Squint sheet: no two silhouettes confusable at 64 px.
4. Revú over blood-velvet carpet and Luka over lit gold inlay both hold.
5. No cyan anywhere at rest; cyan appears on a cast tell and leaves with it.
6. Only Fortuna carries gilt.
7. Hop, rise, shatter, idle and the hit flash still look right; `FootAnchorOffset` still keeps the feet planted.
8. Sorting and click-picking on a standing figure are unchanged.
9. An operator with real renders (Luka) is completely unaffected.
10. Frame time and memory: twelve cached figures plus normal maps, built lazily, with no hitch on first deploy.

**LB0's own checks** (two figures, not twelve): Bouncer and Nuetu seated at the table and standing on the floor, in both cameras; the hit flash on both; the `[LookBook]` build times in the Console, and whether starting a match hitches; Luka unchanged; `OperatorLookBook.Enabled = false` brings back the pawn.

## Architecture as built (LB0–LB3)

All under `Assets/_Project/Scripts/Unity/View/Figures/`. Everything but `FigureSprites` and `OperatorLookBook` is plain C# with no `UnityEngine`, so it runs in tests, on a worker thread, and outside the editor (every increment so far was previewed as PNGs before Unity opened).

| File | What it is |
| --- | --- |
| `FigureShape.cs` | A signed-distance tree in figure space (x across from the centre line, y up from the feet). Primitives: `Polygon`, `Circle`, `Ellipse`, `Rect`, `HalfPlane`, `Polyline` (unsigned: strokes only). Combinators: `Union`, `Intersect`, `Subtract`, `Offset`, `Mirrored`, `Symmetric`, `Translate`, `Scale`, `Rotate`. Every node carries bounds; a union skips children whose box is farther than the nearest hit, which is exact. |
| `FigureDrawing.cs` | The layer stack with its order fixed (see the note under the stack), plus `Cropped(waist)` for the seated pose. `FigureColour`, `FigureBlend`, `FigureLayer`. |
| `FigureRasterizer.cs` | Renders a drawing onto a `FigureCanvas` into a `FigureImage` (straight RGBA, bottom row first, opaque rows measured). Samples each texel once at its centre and supersamples only texels an edge crosses (about one in eight); the rim reads a texel-resolution grid of the silhouette distance. |
| `LookBookPalette.cs` | The colours a recipe may use: the shared drawn light and §3 swatches, then one block per operator. Filled from `UiTheme` by `OperatorLookBook.Palette`. |
| `OperatorLook.cs` | One recipe: the roster name, the waist the table cuts at, and a pure `Draw(palette)`. |
| `Looks/<Name>Look.cs` | One file per operator. **Bouncer, Mimi, Nuetu** so far. |
| `LookRoster.cs` | Every recipe; adding one is a file under `Looks/` and one line here. |
| `DecoMotifs.cs` | The shared vocabulary: line weight, rim widths (wider on dark figures), `ShadowSide`, `Crescent` (the cel sphere), `Ray`, `Fan`, `Chevron`. |
| `SilhouetteMetrics.cs` | The squint silhouette at 64 px, intersection-over-union between two, and the palette rules `IsCyan` and `IsGilt`. |
| `LookSheet.cs` | Composes the contact and squint sheets at real screen height with an area filter, the way mipmaps shrink a figure in play. |
| `FigureSprites.cs` | The only Unity conversion: `FigureImage` → mipmapped sprite, white silhouette, `FigureArt`. |
| `OperatorLookBook.cs` | Cache per operator and pose (shared across seats), `Enabled`, `Prewarm` on the thread pool, the build-time log, the palette from `UiTheme`. Knows no operator by name. |

`OperatorArtLibrary.Figure` asks the look book after a render and before giving up; `Rendered` answers for renders alone. `MatchBootstrap` calls `OperatorLookBook.Prewarm` just before the pieces bind. The look book's colours live in `UiTheme` under "Look book".

**The judging window** is `Editor/LookBookWindow.cs`: **Window → Nona Royale → Look Book**. It shows the contact sheet (every recipe, standing and seated, on the floor, carpet, lit gold and two felts, at phone ≈90 px, desktop ≈64 px or 200 px), the 64 px squint sheet, the overlap matrix (amber from 0.75, red above 0.85) and the ledger rules, and exports PNGs to `Logs/LookBook/`. The `NonaRoyale.EditorTools` assembly now references `NonaRoyale.Unity` for it.

**The rule tests** are `LookBookRulesTests`, run over every recipe in `LookRoster` with the shipping palette: no cyan at rest in either pose, a cast tell that does bring cyan, gilt only on Fortuna, nothing clipped by the canvas, a seated cut that keeps the head and drops the legs, and no two silhouettes overlapping more than 0.85 at 64 px.

## Log

- **2026-09-21 — LB0.** Designer decisions this session: LB1 rides the ART1 path (untinted figure, seat on disc, ring and pin); the rasteriser API approved as proposed; the throwaway pair is Bouncer and Nuetu, the two ends of the value ledger; normal maps wait until after LB2.
  - New: the six files above; `FigureRasterizerTests` (16, plain C#, also run outside Unity against a shim: all pass) and `OperatorLookBookTests` (7, editor). Changed: `OperatorArtLibrary` (fallback, `Rendered`), `UiTheme` (look-book colours), `MatchBootstrap` (one `Prewarm` line).
  - The Unity assembly and the edit-mode test assembly were compiled against the editor's own DLLs (the references in the editor's `.rsp`) with no errors and no new warnings.
  - Render cost: a 212×298 figure at 4×4 is 70–95 ms under a desktop JIT. Expect more under the editor's Mono, which is why it runs on the thread pool. The Console logs every build.
  - A test caught the gauntlet's ink running off the right edge of the first 202-wide canvas; the canvas is now x −1.1..1.108.
  - **Two things LB2 must know.** A figure is fitted to the pawn's height and then scaled by health, so the line weight on screen varies by about ±25% across the roster; if that reads as inconsistent, compensate in the recipe's line weight. And the seated look-book bust uses `ArtSeatedHeightScale` (1.6), which was tuned for Luka's seated render; check it against the table in Play Mode.

- **2026-09-21 — LB0 Play Mode and tests.** Play Mode passed ("an improvement over the previous status quo"). The Test Runner showed 940/943: all 23 new tests passed, and 3 in `OperatorArtTests` failed, predating the look book.
  - `Measure_…Silhouette` has failed since ART1: the mask is uploaded with `makeNoLongerReadable`, and the test read it with `GetPixel`. The test now reads it back through a render texture.
  - `NoArt_…` and `StandingArt_…` compared against `BoardArt.Pawn`/`Bust` after the editor had destroyed them. Every lazy sprite cache in `BoardArt`, `DecoSprites`, `Primitives` and `RoomArt` used `??`, which cannot see a destroyed Unity object, so a static kept handing out a dead sprite. They now use Unity's `!=`, the dictionary caches check for dead entries, and `FigureArt.IsAlive` lets `OperatorArtLibrary` and the look book rebuild a destroyed figure. A regression test destroys `BoardArt.Pawn` and asks for it again.

- **2026-09-21 — LB2 begins, LB3 delivered.** Designer decisions: the first recipes are Bouncer and Nuetu (promoted from the sketches) and Mimi, the hardest value case; the judging tools are an editor window; LB1's name labels are skipped, since at phone scale a label is wider than the figure and the squad rail already names everyone.
  - **What the LB0 screenshots set.** A standing figure is about 90 px tall on a phone held upright and about 64 px at 1440p, so §2.2's 64 px squint scale is the real scale, not a stress test. On the near-black floor a dark figure's rim does most of the separating, so dark recipes now use a wider rim (`DecoMotifs.RimOnDark`, 0.045) than light ones (0.032). The seated Nuetu bust read well at the table; `ArtSeatedHeightScale` stays.
  - **Recipes.** Bouncer as sketched, with the wider rim. Nuetu gains his shaved head in place of the visor, a brass bio-link collar, the two belt discs that are his piece shape, and shorter legs set inside the disc so his outline stays apart from Bouncer's slab (overlap 0.79 → 0.76). Mimi is new: one narrow black shape, a pale frost-white rig with two emitters that break her outline upward like the fins of a dart, harness straps that run straight down (never a white V, which is Bouncer's), and no rim below the hem, because on legs that thin the rim was all there was.
  - **Overlap at 64 px:** Bouncer/Nuetu 0.76, Bouncer/Mimi 0.41, Mimi/Nuetu 0.49. Wide figures sit near 0.75 legitimately; the test fails above 0.85, and the window flags 0.75 and up in amber so it is looked at early.
  - `LookBookSketches.cs` is gone, and so are the sketch tests in `FigureRasterizerTests`: the rules now run over the roster in `LookBookRulesTests`. `UiTheme`'s `Sketch*` colours became `Look*` per-operator blocks.
  - All three assemblies (Unity, the edit-mode tests, the editor tools) compile against the editor's DLLs with no errors and no warnings. Rendered outside Unity: no cyan at rest, no gilt, nothing clipped, each figure 110–200 ms under an unwarmed JIT.

## Read before writing anything

`ART_DIRECTION.md` §2.2, §3, §5 and §5.1 in full — they are the spec, and this document is a summary of them. Then `View/BoardArt.cs`, `View/OperatorPiece.cs`, `View/FigureLayout.cs`, `View/OperatorArtLibrary.cs`, `View/UiTheme.cs`, `View/FigureTilt.cs`, the draft card, and `Core/Abilities/Roster.cs` for the authoritative names. For LB2 onward, also everything under `View/Figures/`. File names come from the design logs and need verifying against the tree.

Ask the designer for a Play Mode screenshot of the board with pieces out, in both cameras, before choosing how coarse the shapes should be.

## Delivery

Patch against a checked HEAD, applied with `git apply Temp/<name>.patch` from the repo root and verified against a fresh clone of that commit. Unity writes the `.meta` files on next focus; `git add` them with the rest. No AI trailers.

Suggested commits:

```
feat(view): shape rasteriser for procedural Deco figures
feat(view): seat identity moves to the base disc and ring
feat(view): twelve procedural operator figures from the value ledger
feat(tools): operator contact sheet, squint sheet and distinctness tests
feat(ui): Deco portraits and the powered cast overlay
```

## Start prompt for the implementing session (LB2, the next recipes)

> Read `docs/design/OPERATOR_LOOKBOOK.md` including its log, then `ART_DIRECTION.md` §2.2, §3, §5 and §5.1 in full, `ART_PROMPTS.md`'s character blocks for the operators in hand, then everything under `View/Figures/`. Ask me for the Look Book window's exported sheets and a Play Mode screenshot first. Then write the next three recipes, render them outside Unity against the sheets and the overlap matrix before handing them over, and stop for Play Mode.
