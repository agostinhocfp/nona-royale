# Nona Royale — Operator Look Book (procedural Dark Deco figures)

> Location in repo: `docs/design/OPERATOR_LOOKBOOK.md` · Project copy: `claude/OPERATOR_LOOKBOOK.md`
> Status: **LB0 and LB3 done. LB2 closed at 9 of 12 front-view recipes; the remaining three and the redraw of all twelve move into LB5, the rig. LB5a (rig core, Bouncer) approved 2026-09-21; LB5b (Bouncer rigged on the board) and LB5c (his event poses) delivered the same day, both awaiting Play Mode.** Written 2026-09-21. **v2 — ambition raised: these are meant to be good, not merely distinct.**
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
| LB2 | The twelve | `OperatorLook`, `OperatorLookBook`, the twelve recipes, the Deco motif library. **Closed at 9 of 12 front-view recipes; Luka, Fortuna and Lethe are drawn rigged in LB5 instead.** |
| LB3 | Judging tools | Contact sheet, squint sheet, distinctness and palette tests. **Delivered 2026-09-21.** |
| LB4 | Portraits and tells | Draft-card portraits at a larger canvas with a Deco frame, plus the powered cyan overlay on cast. **Portraits likely go to generator or Blender art instead (see LB5); the cast overlay moves into LB5c.** |
| LB5 | The rig | Three-quarter figures built from parts that move at the joints: idle, step, cast, hit, knockout and seated activity, facing where they go. **Planned 2026-09-21; see below.** |

LB3 is tempting to skip and should not be. Build it right after LB2's first two or three recipes, then author the rest with it open.

**LB1 is not optional now.** With figures carrying assigned value solutions from §5.1, a seat tint over the top destroys the entire point: Nuetu's dove-grey and Mimi's near-black stop being distinguishable the moment both are painted the same seat colour. Seat identity has to live in the disc, the ring and the pin. This is also where Stage 4 decision 4 was already heading for real art, so it is one system, not two.

**LB1 rides ART1 (designer, 2026-09-21).** ART1 already built this for real renders: any `FigureArt` from `OperatorArtLibrary` is drawn untinted over the seat disc and ring, fitted by `FigureLayout.Fit`, and flashed through a white silhouette. A look-book figure is handed to the piece as a `FigureArt`, so it gets all of that with **no change to `OperatorPiece`**. What LB1 still owes: the optional name labels, and whatever the four-seat Play Mode check says about the disc and ring's strength.

## LB5 — the rig and the three-quarter redraw

**Planned 2026-09-21. LB5a delivered and approved the same day; LB5b and LB5c delivered, awaiting Play Mode.**

### Why

The designer asked whether the front-view figures were the best procedural art could do. They are not: they are about halfway. Three authoring choices hold them back, and none of them is the rasteriser:

- **Front-on and mirrored.** The board camera looks down about 25° in three-quarter view, and §5 asks for three-quarter figures. A symmetric front-on figure on a tilted board reads as a paper doll stood on a table. It is the biggest single tell.
- **Frozen.** The only motion is the whole sprite squashing and hopping, and the seated pose is the standing one cut at the waist.
- **Blank heads.** §2.2 asks for hair as one mass with carved highlights; the heraldic approach allows one face shadow, a brim, a jaw. Today every head is an oval with a cap.

A rig fixes the first two, and the redraw it forces is the natural place to fix the third. It is also where procedural art can **beat** pre-rendered sprites at board scale, because pre-rendered sprites pay for every pose in frames. If LB5 lands well, ADR-0009's Blender pipeline can narrow to portraits and marketing art.

### Decisions (designer, 2026-09-21)

1. **Runtime cut-out rig**, not baked frames. Each part is its own small sprite, rotated at its joint in play: smooth at any frame rate, able to aim a cast at its target, little memory. Accepted costs: ink lines where limbs overlap the body (Dark Deco draws those anyway), and rim light on edges that are only outer in the rest pose.
2. **The full motion set in the first rig increment**: idle breath and weight shift, a step instead of the hop, a cast pose that raises the device toward the target, a hit recoil, a knockout collapse, and each operator's seated activity at the table (ART_PROMPTS character blocks, "Seated").
3. **Figures face where they go**: toward the next cell when walking, toward the target when casting, toward the board centre at rest.
4. **Redraw the nine in three-quarter view on the rig, and draw Luka, Fortuna and Lethe rigged from the start**, so no front-view work is thrown away on them. The fallback order does not change: a real render still wins (Luka), then a rig, then the pawn.

### Proposed design (to confirm before LB5a)

Plain C# for everything but the Unity view, as before.

- **`FigureRig`**: a skeleton in figure space, three-quarter view. Named joints (root, hips, chest, neck, head, both shoulders, elbows, wrists, hips, knees, ankles, a device mount) with rest positions per operator, since a slab and a dart do not share bones.
- **`RigPart`**: one body part as a `FigureDrawing` in its joint's local space, with a draw order and **rim flags** (which edges take the rim: outer only for a far arm, both for the head). Parts overlap at the joints with round caps so a rotation never opens a seam.
- **`RigPose`**: joint angles, root offset and draw-order overrides (an arm raised in front of the head). Poses are data. **Shared templates** by build (slab, dart, disc, column) supply the walk cycle, hit and knockout; a recipe overrides only what is its own, usually the cast and the seated activity. That keeps twelve rigs from costing twelve times the authoring.
- **`OperatorRig`** replaces `OperatorLook` as the recipe type: the skeleton, the parts, the pose overrides, the seated table line.
- **Facing** bakes each part twice, once mirrored as geometry. *Corrected in LB5a:* the rim, which the rasteriser computes, stays on the key side whichever way the figure faces; the form shadows are drawn shapes and travel with the form, so a left-facing figure's side plane is shaded on the left. That is how flipped animation cels behave, and at 64–90 px it reads as form, not as the light moving. Lighting each facing properly would mean authoring its shadows twice.
- **`RigView`** (Unity) replaces the body sprite on `OperatorPiece` when an operator has a rig: one `SpriteRenderer` per part under the figure child, inside the existing `SortingGroup`; per-part white silhouettes for the hit flash; screen bounds from the union of parts. `FigureLayout` fits the rest pose.
- **`RigAnimator`** (Unity) blends between poses with the existing `MotionSettings` clocks and Reduced motion. It takes its cues from the hooks the piece already has: `Walk` and `Stepped` for the step, `Rise`, `Flash` for the hit, `Shatter` for the knockout, and the cast tell queued in `MatchBootstrap.QueueCastTell` for the cast.
- **Judging.** The Look Book window gains a pose strip per operator and an animated preview. The squint and overlap rules run on the composed rest pose, and new rig tests check that the feet stay planted through idle and cast, that no pose opens a seam at a joint, and that no pose shows cyan at rest.
- **Normal maps** (planned since LB0) come per part from the shade and light layers, so URP's 2D lights catch the brass and black cloth.
- **Cost.** Parts are rasterised once per facing, not per frame; animation is transforms only. Build time stays near today's, a little higher for the second facing, and runs on the thread pool as now.

### Increments

| # | Increment | What it delivers |
| --- | --- | --- |
| LB5a | Rig core **(delivered)** | `FigureRig`, `RigPart`, `RigPose`, `OperatorRig`, the build templates, composition of a posed rig into one image for judging, and tests. **Bouncer** redrawn in three-quarter view with rest, idle, cast and seated poses, previewed animated in the Look Book window. No game changes yet. |
| LB5b | On the board **(delivered)** | `RigView` and `RigAnimator` on `OperatorPiece`: idle, the step, the rise, facing, the hit flash per part, screen bounds, sorting. Bouncer only. |
| LB5c | Event poses **(delivered)** | Cast aimed at the target, hit recoil, knockout before the shatter, the seated activity, and the cast's cyan tell on the device. |
| LB5d | The cast | The eight other recipes redrawn in three-quarter view with a head pass each (hair masses, one face shadow, a signature head shape), and **Luka, Fortuna and Lethe** drawn rigged. Normal maps. |

Stop for Play Mode after each.

### Risks

- **Authoring cost.** Twelve rigs with poses is the most hand-placed geometry in the project. The shared templates are what keep it bounded; if a recipe needs more than its cast and seated poses of its own, the template is wrong.
- **Joint seams** under large rotations. Round caps and limits on joint angles; a test renders every pose and looks for gaps.
- **Style drift.** Per-part shading does not re-light as a part rotates, so a raised arm keeps its rest-pose shadow. Fine for the small angles of idle and step; the cast pose may need its own part drawing for the raised arm.
- **Motion budget.** MOTION.md's timings still govern. The step must fit the hop's time per cell, and the cast pose the cast tell's duration, or the presentation queue slows down.

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
| `Looks/<Name>Look.cs` | One file per operator. Nine so far; Luka, Fortuna and Lethe to come. |
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

- **2026-09-21 — LB2, six more recipes.** The designer approved the first three from the exported sheets ("turning out well") and asked for six more. Chosen: **Syla, Kurbyn, Javi, Sanity, Revú, Kian**, the six with settled briefs and no render. Left for last: **Luka** (his render always wins in play, so his recipe matters for the sheets only), **Fortuna** (her diamond silhouette is still under review in ART_DIRECTION §10) and **Lethe** (her six-point spark is best drawn with Kian's crown already on the sheet to compare against).
  - **Syla**: a stepped black cape falling to a point at the waist, a bladed fan collar behind the head, the ivory gown down the front and on to the ankle; drones on the shoulders, cradles at the hips. The downward triangle.
  - **Kurbyn**: the crouched spring, with elbows out, fists low, feet wide; charcoal with an open black waistcoat, brass braces, a loose tie, and bare pale forearms as the value break.
  - **Javi**: arms held level in white sleeves across a narrow charcoal body; a brass bandolier of steel canisters with frosted seals, one spent; grey gloves. The upright cross.
  - **Sanity**: one umber octagon, chamfered hard at all four corners, from the shoulders to the boots; charcoal livery at the shoulders, bare forearms, the tool roll as the brass line across him, the prod at his hip.
  - **Revú**: tall and narrow, with peaked shoulders either side of a long neck; the ledger case hanging on its brass chain is the barb. Oxblood `#4A1320`, a narrow shirt front rather than a V.
  - **Kian**: four emitter rods and the head make the five points; the §3 emerald itself for the jacket, brass frogging and spectacle rims, the disrupter disc at the sternum.
  - **Two lessons, both now in the recipes.** A rim down both sides of narrow trousers reads as two lit wires at board scale, so the thin-legged figures (Kurbyn, Javi, Revú, Kian, and Mimi before them) stop their rim at the hip. And a cool sheen screened over emerald drifts into cyan, so Kian's lit planes use the warm key.
  - **`IsCyan` now needs saturation 0.45** (holo cyan is 0.59). At 0.35 it caught one anti-aliased texel where Kian's steel rim meets his emerald jacket: teal by arithmetic, not a device lit at rest.
  - **Overlap at 64 px, everything at 0.65 or above:** Bouncer/Sanity 0.83, Sanity/Nuetu 0.79, Bouncer/Nuetu 0.76, Kurbyn/Nuetu 0.69, Mimi/Kian 0.66. The three wide figures cluster in the amber band, as expected; Sanity's first pass was 0.86, over the line, and chamfering his octagon harder brought it down. On the squint sheet all nine read apart by eye. In play, size separates them further (health 12, 10 and 7).
  - New test: every `LookBookPalette` colour must be filled from `UiTheme` (an unset one draws as a hole). All three assemblies compile against the editor's DLLs with no errors or warnings; rendered outside Unity, no recipe carries cyan at rest or gilt, and none is clipped.

- **2026-09-21 — LB2 revisions from the Look Book window.** Designer review of the six:
  - **Kurbyn's knees pointed at each other.** The stance is now wide and planted: thighs angle out from the hip, knees bend outward over the feet, shins near vertical.
  - **Syla's cape is dropped from the board figure only** (designer: "remove the cape for art type only, not the character in general"). Her character, portrait and generator briefs keep it. Without it, her dark frame is the drone housings (stepped wedges that slope down onto the shoulders, now the widest and hardest edge on her), the black opera gloves down both sides of the gown, the hip cradles and the bob, cut straight at the jaw with the face set into it. The ivory gown narrows to the ankle, so she still tapers from shoulders to feet. The trade-off: more ivory shows than inside the cape, so the core-in-a-frame reading is weaker; watch it against Luka's camel once his recipe lands. A black chevron low on the gown closes the frame at the bottom.
  - Overlap after the changes: Syla/Mimi 0.73, Syla/Kian 0.69; the rest unchanged.

- **2026-09-21 — LB5 planned.** Asked whether the front-view figures are the best procedural art can do, the answer was no: they are front-on and mirrored under a three-quarter camera, frozen, and blank-headed. The designer chose a runtime cut-out rig, the full motion set, figures that face where they go, and redrawing the nine in three-quarter view with Luka, Fortuna and Lethe drawn rigged from the start. LB2 closes at nine front-view recipes. Design and increments are in the LB5 section; the design is to be confirmed before LB5a starts.

- **2026-09-21 — LB5a, the rig core.** Go given on the LB5 design as written.
  - **New, plain C#, under `Figures/Rig/`:** `RigSkeleton` (named bones, pivots in figure space, parents turn children), `RigPose` (a turn, shift and scale per bone, hidden and shown parts, the table line; `Lerp` and `Mirrored`), `OperatorRig` with `RigPart` and `RigAnchor`, `RigPoses` (the shared templates by build, Heavy and Light: rest, idle A/B, step A/B, hit, knockout, and default cast and seated poses for a recipe to replace), `RigClips` (idle, step, cast and seated as timed blends), `RigComposer` (parts rasterised once at rest on their own canvases, then turned, placed and laid back to front with bilinear sampling, the way a sprite per part will be in play), `RigChecks` (feet planted, no seams at joints, no cyan at rest), `RigRoster`, and `Rigs/BouncerRig.cs`.
  - **The rasteriser** gains `RimEdges`, so a part lights only its outer edges (a far arm's right edge, never the edge against the body), and `FigureDrawing.Mirrored`, which reflects every shape and swaps the rim's sides.
  - **Bouncer redrawn in three-quarter view**, turned toward the right: the front of the jacket to the camera and the side plane receding in shadow, the V and the bow tie off-centre toward the facing side, the brass house pin, stepped near shoulder. The gauntlet is on his right arm, which is the near arm. Head pass: a shaved oval with one carved highlight, the heavy brow as one shadow shape, the broken nose in profile, an ear. His cast swings the gauntlet up and across toward the target with the feet planted; seated, the gauntlet is off and lies on the table while his bare forearm rests beside it (ART_PROMPTS, "the only operator who takes his device off"). Twelve parts, two of them props.
  - **The Look Book window** gains a Rig section: the pose strip (every pose, the cast with its tell, and the left-facing rest), the rule line, and an animated preview beside the rest pose with a clip picker, Play and Face left.
  - **`RigTests`**: the skeleton maths, and over every rig in the roster: every part on a real bone, every pose present, feet planted in idle and cast, no seam at any joint in any pose, no cyan at rest, the tell on cast, nothing clipped, facing left is the same shape reflected, the cast clip starts at rest and peaks at the cast pose. Outside Unity against a shim, all 76 tests in `FigureRasterizerTests`, `LookBookRulesTests` and `RigTests` pass; all three assemblies compile against the editor's DLLs with no errors or warnings.
  - **Cost:** Bouncer's twelve parts rasterise in about 45 ms per facing; composing a pose takes 7–17 ms, which only the judging window does. On the board the parts are sprites and a pose is transforms.
  - **Known for LB5b:** the clip timings are placeholders until they are fitted to MOTION.md; the knockout pose is rough; the table line clips in the composer, and on the board the table or a mask will have to.
  - **Approved** in the window by the designer ("Looks good") and committed (`b382048`).

- **2026-09-21 — LB5b, the rig on the board (Bouncer).**
  - **New, plain C#, under `Figures/Rig/`:** `RigAnimator` picks the clip and where in it (seated loop, rise, step, idle, in that order of priority) and answers with two poses and a blend; `FaceLeft` turns a figure only past a dead zone, so a piece straight above its target does not flicker. `RigImages` rasterises both facings of a rig once, plus the **seated cut**: the table line is carried into each part's own rest space under the seated pose and the part is cut along it, ink included, one line weight above the line so the ink ends on the table. Parts wholly above keep their rest image (Bouncer's head, near upper arm, bare forearm); wholly below are not drawn. `RigSkeleton.Evaluate(a, b, t, into)` places bones into an array with nothing allocated, since every piece asks every frame; `FigureDrawing.CroppedBy` cuts along any line (`Cropped` now calls it).
  - **New, Unity:** `OperatorRigArt` (the thread-pool build, the upload, the cache, an `Enabled` switch; sprites pivot at their lower-left corner and sit at an offset from the joint, so no pivot lies outside a sprite) and `RigView` (a joint, a sprite and a white-silhouette flash per part, under the piece's body child so `FigureLayout.Fit` places the rig exactly as it places a render; explicit draw order, part *i* at 4 + 2*i*, flash one above; facing and seated swap sprites, not renderers; screen bounds are the union of the drawn parts). Both are plain classes owned by the piece rather than components.
  - **`OperatorPiece`:** fallback order render → rig → look book → pawn, per pose. A rigged figure is "rendered art" for everything MO2 and ART1 do (untinted, the seat disc and ring, the white flash, the evasive fade). Its own motion replaces the whole-sprite tricks: the step replaces the hop and its squash (no lift, no landing punch on the contact shadow), the idle and the seated loop replace the breathing and the sway, and a deploy stands up from a crouch (45% of the way to the knockout pose, a slight overshoot) inside the existing pop. It faces toward the next cell while walking and toward `BoardCentre` at rest; a bounced piece resting on the contested cell keeps its facing. The **pin rides the chest bone**. The pin, bar and halo moved to order 40 and up so they draw over every part.
  - **Timings fitted to MOTION.md** (`RigClips`): idle one breath per 2π/1.8 s (MO2's breathing clock), seated loop 2π/0.9 s (the sway's), the step one step per cell at the hop's 8 cells/s (a 0.25 s cycle) and **driven by the cells covered**, not the clock, phased so every cell is arrived on at the passing pose; the cast 0.8 s, peaking by the cast tell's reach (0.12 s) and held past its linger (0.5 s). The judging window's step preview now runs at that true speed.
  - **Reduced motion** keeps the step (it is how the figure walks, not decoration), drops the idle and the seated loop as MO2 dropped the breathing and the sway, and keeps the short rise.
  - **`MatchBootstrap`:** prewarms rigs alongside the look book (an operator with a rig no longer prewarms its look-book figure) and hands each piece the board centre.
  - **Tests:** new `RigBoardTests` (array evaluation equals the blended pose, the clocks, every cell arrived on at rest, legs apart mid-cell, the animator's priorities, reduced motion, facing, both facings complete, extents, nothing drawn under the table in either facing, the head shared and the shins at most a knee) and `RigPieceTests` (Bouncer rigged seated and standing, the gauntlet off and on the table, pin and bar over every part, a render beats the rig, rigs off draws the look book, screen bounds). `OperatorLookBookTests` switches rigs off, since it tests the look book under them. Outside Unity against a shim, all 90 tests in `FigureRasterizerTests`, `LookBookRulesTests`, `RigTests` and `RigBoardTests` pass; all three assemblies compile against the editor's DLLs with no errors or warnings. `RigPieceTests` needs the editor.
  - **Cost:** both facings and the seated cut, cold, about 0.5 s on the thread pool at match load (warm, well under half that); on the board a pose is 11 bones and 12 transforms, with no allocation.
  - **Choices to judge in Play Mode:** a step per cell at 8 cells/s may read as scurrying (the hop never showed legs); the instant facing flip; the crouch depth and overshoot on the rise; the step kept under Reduced motion; the pin at the chest's centre line rather than on the lapel.

- **2026-09-21 — LB5c, event poses (Bouncer).** Built on the designer's go while away from the computer, so on top of an LB5b not yet seen in Play Mode; the two are meant to be judged, and committed, together.
  - **The cast, aimed.** `OperatorPiece.Cast(aim, tellSeconds)`, called from `QueueCastTell` as the tell plays, turns the figure to its target (the target's ground point, or the cell) and plays the recipe's cast with the casting arm (the near upper arm) turned further up or down by the line's angle to the target, clamped to ±30° (`RigAnimator.Aimed`, `Elevation`). The clip is stretched so its hold ends as the tell does (`RigClips.CastLengthFor`: 0.8 s for an aimed tell, 0.48 s for a sweep). A cast with no aim casts where the figure faces.
  - **The cyan tell on the device.** `RigImages` rasterises a powered image for each part that has a powered layer (Bouncer: the gauntlet's seams), on the same canvas as its rest image. `RigView.Powered` swaps it in from the cast's reach to its release, standing only; a walk, a hit or a rise puts it out. Nothing else on the figure lights (§8).
  - **The recoil.** `OperatorPiece.Recoil(from)`, called beside `Flash`, turns the figure to the striker (the caster, else the operator that walked into it) and plays the hit pose: snapped back in 60 ms, eased back over the rest of the hit's 0.3 s hold. The shared hit template was strengthened (chest 9° → 14°, head 7° → 10°, a deeper step back) because the first one barely showed at board scale.
  - **The knockout.** A standing rigged figure folds into the knockout pose over 0.25 s (easing in, so it drops rather than floats) and shatters as the fold ends; the shards then fly as before. `IsKnockingOut` covers the fold and the shards, and the knockout beat now also waits on it, so the settle never brings a figure back mid-fall. A flushed batch brings a still-folding figure back the same way as a shattered one.
  - **The seated activity** (ART_PROMPTS: "turned outward, watching the room rather than playing"): the seated loop is now one breath at the idle's pace, then the activity eased in, held and eased out, over the same 7 s. New shared pose `seated.look` (a lean back, the chin up); Bouncer's own sits back 2.5° and lifts his chin 11°, the bare arm kept on the felt.
  - **The animator's priorities** are now seated, knockout, hit, rise, walk, cast, idle. The walk beats the cast so a walk that follows a cast never waits on the arm coming down. A cast, a recoil or a fold holds its facing; turning drops a cast in progress, since a pose built for one facing is mirrored wrong for the other.
  - **The judging window** picks up the hit and knockout clips on its own (`RigClips.All`).
  - **Tests:** eight more in `RigBoardTests` (the tell lit only for the hold and put out by a walk, the hold ending with the tell, the aimed arm in both facings and clamped, the elevation maths, the fold holding and clearing, the recoil peaking and returning, the seated loop's breath and activity, only the device having a powered image and it being cyan) and three in `RigPieceTests` (a cast turns to its aim and a hit to the striker; a knockout folds before it shatters and reappearing ends it; no fold when seated). Outside Unity, all 98 plain C# tests pass; all three assemblies compile against the editor's DLLs with no errors or warnings.
  - **Choices to judge in Play Mode:** the aim's strength (the full line angle, clamped at 30°); turning to face the striker on a hit rather than rocking away from where the figure already faces; the knockout pose itself, still rough (a squat more than a fall); how visible the seated activity is at yard scale; the fold adding 0.25 s to a knockout beat.

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

## Start prompt for the implementing session (LB5d, the cast)

> Read `docs/design/OPERATOR_LOOKBOOK.md` in full, especially the LB5 section and the log, then `ART_DIRECTION.md` §2.2, §3, §5 and §5.1, and every character block in `ART_PROMPTS.md`. Then everything under `View/Figures/` (the front-view `Looks/` and the rig under `Rig/`, with `Rigs/BouncerRig.cs` as the pattern), and `Core/Abilities/Roster.cs` for the names. Ask me for the LB5b and LB5c Play Mode results first, and fix what they raise before anything else. Then build LB5d in batches of three, stopping after each for the judging window: the eight other front-view recipes redrawn as three-quarter rigs with a head pass each (hair as one mass, one face shadow, a signature head shape), each with its own cast and seated activity from ART_PROMPTS, then Luka, Fortuna and Lethe rigged from the start. Normal maps come last.
